using System;
using System.Collections;
using System.Collections.Generic;
using System.Text;
using Mirror;
using Mirror.Examples.Basic;
using UnityEngine;
using System.Threading;

[System.Serializable]
public class SyncDictionaryIntFloat : SyncDictionary<int, float> { }


public class PolePositionManager : NetworkBehaviour
{

    [SyncVar] public int numPlayers = 0;//numero de jugadores
    [SyncVar] public int actualPlayerID = 0;
    [SyncVar] int clasFinished = 0;
    [HideInInspector] int playersReady = 0; 
    [SyncVar] [HideInInspector] int playersEnded;

    SyncDictionaryIntFloat clasTimes = new SyncDictionaryIntFloat();

    [SerializeField] GameObject[] spawns;
    [SerializeField] GameObject[] PodiumPos;

    public int totalLaps = 3;
    private int initialPlayers;

    public NetworkManager networkManager;//controlador de la conexion
    private UIManager m_UImanager;
    public SetupPlayer setupPlayer;
    public PlayerController playerController;
    public MirrorManager mirrorManager;


    public GameObject checkPointList;
    public GameObject postGameBackground;

    float[] arcAux;

    public readonly List<PlayerInfo> m_Players = new List<PlayerInfo>(4);
    public CircuitController m_CircuitController;//controlador del circuito
    private GameObject[] m_DebuggingSpheres;//esfera para uso en el debug

    private float timer=0;

    private float tempTime = 0;
    public float totalTime = 0;
    private float[] arcLengths;
    private float[] playerTimes;

    //Saber si se esta en la vuelta de clasificación o en la carrera final
    public bool clasification = true;
    public bool isRaceEnded; //Determina si ha terminado la carrera y se utiliza para enviar segun qué información al HUD y ahorrar cálculos cuando la carrera ya ha terminado
    private bool hasStarted = false; //Determina si la carrera ha comenzado. Se utiliza para sincronizar los contadores de todos los jugadores y para no contar el tiempo antes de que todos estén listos



    //Delegado para sincronizar el comienzo de la partida
    public delegate void SyncStart();

    public event SyncStart StartRaceEvent;

    //Delegado para la actualización de las vueltas y el tiempo en la interfaz.
    public delegate void OnLapChangeDelegate(int currentLap, double lapTime, double totalTime, int totalLaps);

    public event OnLapChangeDelegate updateTime;

    //Delegado para la actualización de las vueltas y el tiempo en la interfaz.
    public delegate void OnClasLapChangeDelegate(double lapTime);

    public event OnClasLapChangeDelegate updateClasTime;

    private string m_CurrentOrder = "";

    //Variable para actualizar el orden de los jugadores en la interfaz
    private string Order
    {
        get { return m_CurrentOrder; }
        set
        {
            m_CurrentOrder = value;
            if (OnOrderChangeEvent != null)
                OnOrderChangeEvent(m_CurrentOrder);
        }
    }

    private bool m_BackDirection = false;

    //Variable para actualizar si el jugador local va marcha atras de los jugadores en la interfaz
    public bool BackDirection
    {
        get { return m_BackDirection; }
        set
        {           
            if (OnBackDirectionChangeEvent != null && m_BackDirection != value)
                OnBackDirectionChangeEvent(value);

            m_BackDirection = value;
        }
    }

    public bool currentCrashed = false;

    //Variable para actualizar si el jugador local se ha chocado de los jugadores en la interfaz
    public bool crashed
    {
        get { return currentCrashed; }
        set
        {
            
            if (OnCrashedStateChangeEvent != null && currentCrashed != value)
                OnCrashedStateChangeEvent(value);

            currentCrashed = value;
        }
    }
    //Delegado para la actualización de la interfaz en función de si el coche va o no marcha atras.
    public delegate void OnBackDirectionChangeDelegate(bool newVal);

    public event OnBackDirectionChangeDelegate OnBackDirectionChangeEvent;    //Delegado para la actualización de la interfaz en función de si el coche esta o no apoyado correctamente en la carretera.
    public delegate void OnCrashedStateChangeDelegate(bool newVal);

    public event OnCrashedStateChangeDelegate OnCrashedStateChangeEvent;
    //Delegado para la actualización de la interfaz mostrando el orden de los coches (jugadores).
    public delegate void OnOrderChangeDelegate(string newVal);

    public event OnOrderChangeDelegate OnOrderChangeEvent;

    public event OnOrderChangeDelegate updateResults;

    public delegate void SyncEnd();
    public event SyncEnd allPlayersEndedEvent;
    public event SyncEnd playerPlayAgainEvent;

    private void Awake()
    {
        if (networkManager == null) networkManager = FindObjectOfType<NetworkManager>();//duda
        if (m_CircuitController == null) m_CircuitController = FindObjectOfType<CircuitController>();//duda

        m_UImanager = FindObjectOfType<UIManager>();


        m_DebuggingSpheres = new GameObject[networkManager.maxConnections];
        for (int i = 0; i < networkManager.maxConnections; ++i)
        {
            m_DebuggingSpheres[i] = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            m_DebuggingSpheres[i].GetComponent<SphereCollider>().enabled = false;
        }
    }

    private void Start()
    {
        m_UImanager.PlayerReadyEvent += ManageStart; //Suscribe al evento que lanza el botón de "Ready" el proceso que se encarga de llamar al Command correspondiente

    }

    //GetComponent<NetworkIdentity>().AssignClientAuthority(this.GetComponent<NetworkIdentity>().connectionToClient);
    //Esta linea se usa en caso de que un command no funcione, pero teoricamente nunca es necesaria

    /// <summary>
    ///Llama al Command en setupPlayer que se encargará de llamar a "RpcManageStart" en este script
    /// </summary>
    void ManageStart()
    {
        mirrorManager.CmdPlayerReady(); 
    }

    #region RPC Calls

    //-------------------------
    //LLAMADAS RPC
    //-------------------------

    //Esta llamada Rpc a todos los clientes se ejecuta en este script porque este objeto es único en el juego. Si se ejecutase desde playerController cada cliente
    //actualizaría solamente el booleano del jugador cuyo playerController lanzó la llamada Rpc. Para que se actualice como es debido, se le llama desde aquí
    //utilizando a su vez un comando en setupPlayer que llama a esta llamada Rpc, que en cada cliente actualizará el valor local del jugador de ese cliente
    //mediante referencia directa de componentes.
    [ClientRpc]
    public void RpcStartRace()
    {        mirrorManager.CmdPrintServer("Se entra al rpc final");
        if (!hasStarted)
        {            
            setupPlayer.m_PlayerController.canMove = true; //Actualiza el bool canMove en el playerController del jugador de este cliente, gracias a que el PolePositionManager de cada cliente guarda una referencia al jugador local de ese cliente
            mirrorManager.CmdPrintServer("Se entra al if dentro del rpc final");
            if (StartRaceEvent != null)
                StartRaceEvent();

            hasStarted = true;
        }       
    }




    /// <summary>
    /// //Esta llamada Rpc está en este lugar por el mismo motivo de la anterior
    /// </summary>
    [ClientRpc]
    public void RpcManageStart()
    {
        playersReady++; //Suma un jugador listo
        //print("JUGADORES LISTOS: " + playersReady);
        mirrorManager.CmdPrintServer("playersready: " + playersReady + " numplayers: " + numPlayers);
        if (playersReady >= numPlayers) //Si los jugadores preparados igualan o superan a la cantidad de jugadores,
        {            mirrorManager.CmdPrintServer("Se entra al if");
            initialPlayers = playersReady; //Se utilizará para saber cuando acabar la partida por abandono
            mirrorManager.CmdStartRace(); //Llama al Command que más tarde llamará al RpcStartRace de este script
        }
    }

    /// <summary>
    /// Rpc que se ejecuta cuando un jugador quiere jugar otra vez.
    /// </summary>
    [ClientRpc]
    public void RpcPlayAgain()
    {
        if (playerPlayAgainEvent != null)
            playerPlayAgainEvent();
    }
    /// <summary>
    /// //Función que se ejecuta cuando un coche ha completado la vuelta de clasificación
    /// </summary>
    /// <param name="times"></param>
    /// <param name="sortedTimes"></param>
    /// <param name="finished"></param>

    [ClientRpc]
    void RpcClasFinished(float[] times, float[] sortedTimes, int finished)
    {
        

        print("Jugadores completados " + finished);
        print("times length " + times.Length);
        print("times length " + sortedTimes.Length);
        if (finished >= numPlayers)
        {
            tempTime = 0;
            totalTime = 0;
            int cont = 0;
            float aux;
            print("Numplayers: " + numPlayers + " mplayers: " + m_Players.Count);
            for (int i = 0; i < times.Length; i++)
            {
                print("Objeto times: " + i + " " + times[i]);
                print("Objeto sorted: " + i + " " + sortedTimes[i]);
                aux = times[i];
                if (aux != 0)
                {
                    foreach (float time in sortedTimes)
                    {
                        if (time != 0)
                        {
                            if (aux == time)
                            {
                                
                                Renderer[] renders = m_Players[i].gameObject.GetComponentsInChildren<Renderer>();
                                Collider[] colliders = m_Players[i].gameObject.GetComponentsInChildren<Collider>();                              
                                foreach (Renderer r in renders)
                                {
                                    r.enabled = true;
                                    print("Cosa rara");
                                }
                                foreach (Collider c in colliders)
                                {
                                    c.enabled = true;
                                    print("Cosa rara");
                                }
                                
                                m_Players[i].gameObject.transform.position = spawns[cont].transform.position;
                                m_Players[i].gameObject.transform.rotation = Quaternion.Euler(0, -90, 0);

                                m_Players[i].gameObject.GetComponent<Rigidbody>().velocity = Vector3.zero;
                                m_Players[i].checkpointCount = 4;
                                m_Players[i].CheckPoint = 4;
                            }
                            cont++;
                        }
                    }
                    cont = 0;
                }
            }

        }
    }
    #endregion

    private void Update()
    {
        if (m_Players.Count == 0)
        {
            return;
        }
        else if (numPlayers == 1 && initialPlayers > 1 && !isRaceEnded) //Si solo queda un jugador, termina la partida por abandono
        {
            //Estas lineas son homologas al EndRace() del CheckPointController
            setupPlayer.m_PlayerController.canMove = false;

            setupPlayer.m_PlayerInfo.totalTime = totalTime;
            setupPlayer.m_PlayerInfo.hasEnded = true;

            isRaceEnded = true;

            managePlayersEnded();
            updatePodium();



            PostGameCamera();
            m_UImanager.endResultsHUD();

        }




        if (clasification)
        {
            if (!setupPlayer.gameObject.GetComponent<PlayerInfo>().hasEnded)
            {
                tempTime += Time.deltaTime;
            }
            totalTime += Time.deltaTime;
            

            timer += Time.deltaTime;
            updateClasProgress();
        }
        if (hasStarted) //Solo actualiza el estado de la carrera si ha comenzado. Así ahorramos cálculos innecesarios
        {
            if (!setupPlayer.gameObject.GetComponent<PlayerInfo>().hasEnded)
            {
                tempTime += Time.deltaTime;
            }
            totalTime += Time.deltaTime;

            timer += Time.deltaTime;
            UpdateRaceProgress();
        }
    }

    /// <summary>
    /// Función llamada mediante un command por cada jugador para que se le asigne un ID en función del orden en el que se conecten.
    /// </summary>
    /// <returns></returns>
    public int updatePlayersID()
    {
        int aux = actualPlayerID;
        actualPlayerID++;
        return aux;
    }

    /// <summary>
    ///  Añade un jugador
    /// </summary>
    /// <param name="player"></param>
    public void AddPlayer(PlayerInfo player)
    {
        print("Nombre: " + player.Name + " ID: " + player.ID);
        m_Players.Add(player);
        arcLengths = new float[m_Players.Count];
        playerTimes = new float[m_Players.Count];
        arcAux = new float[m_Players.Count];
    }
    /// <summary>
    /// Se activa para eliminar a un jugador de la partida.
    /// </summary>
    /// <param name="player"></param>
    public void QuitPlayer(PlayerInfo player)
    {
        m_Players.Remove(player);

        numPlayers--;

        arcLengths = new float[m_Players.Count];
        playerTimes = new float[m_Players.Count];
        arcAux = new float[m_Players.Count];
    }
    /// <summary>
    /// //Método que se activa cuando el jugador local termina la carrera. Mueve la camara de sitio para que se vea el podium.
    /// </summary>
    public void PostGameCamera()
    {
        if (Camera.main != null)
            Camera.main.gameObject.GetComponent<CameraController>().m_Focus = postGameBackground;
    }

    /// <summary>
    /// Clase para comparar y ordenar los jugadores en función de su posición en la carrera.
    /// </summary>
    private class PlayerInfoComparer : Comparer<PlayerInfo>
    {
        float[] m_ArcLengths;
        List<PlayerInfo> players;

        public PlayerInfoComparer(float[] arcLengths, List<PlayerInfo> par_players)
        {
            m_ArcLengths = new float[arcLengths.Length];

            for (int i = 0; i < arcLengths.Length; i++)
            {
                m_ArcLengths[i] = arcLengths[i];
            }

            players = new List<PlayerInfo>();
            foreach (PlayerInfo info in par_players)
            {
                players.Add(info);
            }           
        }

        public override int Compare(PlayerInfo x, PlayerInfo y)
        {
            if (this.m_ArcLengths[GetIndex(x)] < m_ArcLengths[GetIndex(y)])
                return 1;
            else return -1;
        }




        /// <summary>
        ///Método que recibe un player info, y devuelve su índice en la lista de playerinfos.
        ///Esto es importante porque la posición de cada player info varía en cada iteración si un jugador adelanta a otro, por lo que usar el id para saber la posición en la lista
        ///de un player info como se hacía al principio terminaría dando errores y no detectando bien quien va delante de quien.
        /// </summary>
        /// <param name="pi"></param>
        /// <returns></returns>
        public int GetIndex(PlayerInfo pi)
        {
            int index = -1;
            print("Jugadores en la lista: " + players.Count);
            for (int i = 0; i < players.Count; i++)
            {
                print("ID del pi:" + pi.ID + " ID de la lista: " + players[i].ID);
                if (pi.ID == players[i].ID)
                {
                    print("Indice encontrado si eso: " + i);
                    index = i;
                    return index;
                }
            }
            print("No encontrado, se devuelve -1");
            return index;
        }
    }

    /// <summary>
    /// Método que gestiona los datos de la vuelta de clasificación, en lugar de la carrera.
    /// </summary>
    public void updateClasProgress()
    {
        float[] arcLengths = new float[m_Players.Count]; //Es MUY ineficiente que se declare un nuevo array en cada frame
        for (int i = 0; i < m_Players.Count; ++i)
        {
            //Lo primero: si descubre que un jugador ha abandonado la partida, lo quita de la lista.
            if(m_Players[i] == null)
            {
                QuitPlayer(m_Players[i]);
            }

            //Si el jugador es local, se actualizan los tiempos.
            if (m_Players[i].LocalPlayer)
            { 
                arcLengths[i] = ComputeCarArcLength(i);
                //print("ORIGINAL: " + i + " " +  arcLengths[i]);
                if (m_Players[i].LocalPlayer && updateTime != null)
                {
                    updateClasTime(Math.Round(tempTime, 2));
                }


                if (this.m_Players[i].CurrentLap == 0)
                {
                    //Si el valor es positivo en la vuelta 0...
                    if ((Math.Abs(arcLengths[i]) - Math.Abs(arcAux[i])) > 0.01) //Intentar hacerlo sin valores absolutos (mas eficiente)
                    {
                        print("El jugador " + m_Players[i].ID + " va hacia atrás");

                        if (m_Players[i].LocalPlayer && !m_Players[i].hasEnded /*&& timer >= 0.25*/)
                            BackDirection = true;

                    }
                    else
                    {
                        if (m_Players[i].LocalPlayer)
                        {
                            BackDirection = false;
                            timer = 0;
                        }
                    }
                }
                else
                {
                    //En el resto de vueltas, un valor negativo indicará que el jugador va hacia atrás, como es normal.
                    if ((Math.Abs(arcLengths[i]) - Math.Abs(arcAux[i])) < 0.01) //Intentar hacerlo sin valores absolutos (mas eficiente)
                    {
                        print("El jugador " + m_Players[i].ID + " va hacia atrás");
                        if (m_Players[i].LocalPlayer && !m_Players[i].hasEnded /*&& timer >= 0.25*/)
                            BackDirection = true;

                    }
                    else
                    {
                        if (m_Players[i].LocalPlayer)
                        {
                            BackDirection = false;
                            timer = 0;
                        }
                    }
                }
                arcAux[i] = arcLengths[i];
            }      
        }
    }

    /// <summary>
    /// Suscrito al evento EndRaceEvent. Ordena los jugadores por tiempo, y recorre la lista. Teletransporta a los jugadores que hayan terminado a su correspondiente posición
    /// en el podio.
    /// </summary>
    public void updatePodium()
    {
        //m_Players.Sort(new PlayerTimeComparer(playerTimes, m_Players));

        for (int i = 0; i > m_Players.Count; i++)
        {
            if (m_Players[i].hasEnded)
            {
                m_Players[i].gameObject.transform.position = PodiumPos[i].transform.position;
                m_Players[i].gameObject.transform.rotation = Quaternion.Euler(0, -90, 0);
            }
        }
    }

    /// <summary>
    /// Suscrito al evento EndRaceEvent. Este metodo es llamado por el evento endRaceEvent. Se gestiona de aumentar la variable playersEnded. Si esta variable es igual o mayor al numero de
    /// jugadores, actualiza el estado de la carrera en la interfaz, ya que quiere decir que todos han terminado.
    /// </summary>
    public void managePlayersEnded()
    {
        //Hacer ClientRPC
        if (playersEnded < numPlayers)
        {
            playersEnded++;
            print("JUGADORES QUE HAN TERMINADO LA CARRERA: " + playersEnded);
        }

        if (allHaveEnded())
        {
            if (allPlayersEndedEvent != null)
                allPlayersEndedEvent();
        }

    }

    /// <summary>
    /// Comprueba si todos los jugadores han terminado
    /// </summary>
    /// <returns></returns>
    private bool allHaveEnded()
    {
        return playersEnded >= numPlayers;
    }

    /// <summary>
    /// Hace lo mismo que PlayerInfoComparer pero con sus tiempos y no con los Arclengths
    /// </summary>
    private class PlayerTimeComparer : Comparer<PlayerInfo>
    {
        float[] m_playerTimes;
        List<PlayerInfo> players;

        public PlayerTimeComparer(float[] playerTimes, List<PlayerInfo> par_players)
        {
            m_playerTimes = new float[playerTimes.Length];
            for (int i = 0; i < playerTimes.Length; i++)
            {
                m_playerTimes[i] = playerTimes[i];
            }
            players = new List<PlayerInfo>();
            foreach (PlayerInfo player in par_players)
            {
                players.Add(player);
            }
        }

        public override int Compare(PlayerInfo x, PlayerInfo y)
        {
            if (this.m_playerTimes[GetIndex(x)] < m_playerTimes[GetIndex(y)])
                return 1;
            else return -1;
        }




        /// <summary>
        ///Método que recibe un player info, y devuelve su índice en la lista de playerinfos.
        ///Esto es importante porque la posición de cada player info varía en cada iteración si un jugador adelanta a otro, por lo que usar el id para saber la posición en la lista
        ///de un player info como se hacía al principio terminaría dando errores y no detectando bien quien va delante de quien.
        /// </summary>
        /// <param name="pi"></param>
        /// <returns></returns>
        public int GetIndex(PlayerInfo pi)
        {
            int index = -1;
            for (int i = 0; i < players.Count; i++)
            {
                if (pi.ID == players[i].ID)
                {
                    index = i;
                    return index;
                }
            }
            return index;
        }
    }

    public void UpdateRaceProgress()
    {  

        for (int i = 0; i < m_Players.Count; ++i)
        {
            if (!m_Players[i].hasEnded)
            {
                //Lo primero: si descubre que un jugador ha abandonado la partida, lo quita de la lista.
                if (m_Players[i] == null)
                {
                    QuitPlayer(m_Players[i]);
                }
                m_Players[i].totalTime = totalTime;


                //tempTime += Time.deltaTime;

                arcLengths[i] = ComputeCarArcLength(i);
                if (m_Players[i].LocalPlayer && updateTime != null)
                {
                    updateTime(m_Players[i].CurrentLap, Math.Round(tempTime, 2), Math.Round(totalTime, 2), totalLaps);
                }


                if (this.m_Players[i].CurrentLap == 0)
                {
                    //Si el valor es positivo en la vuelta 0...
                    if ((Math.Abs(arcLengths[i]) - Math.Abs(arcAux[i])) > 0.01) //Intentar hacerlo sin valores absolutos (mas eficiente)
                    {
                        print("El jugador " + m_Players[i].ID + " va hacia atrás");
                        if (m_Players[i].LocalPlayer && !m_Players[i].hasEnded /*&& timer >= 0.25*/)
                                BackDirection = true;
                    }
                    else
                    {
                        if (m_Players[i].LocalPlayer)
                        {
                            BackDirection = false;
                            timer = 0;
                        }
                           
                    }
                }
                else
                {
                    //En el resto de vueltas, basta con comprobar los valores directos y ver si el de este frame es inferior al anterior.
                    if (arcLengths[i] < arcAux[i]) //Intentar hacerlo sin valores absolutos (mas eficiente)
                    {
                        print("El jugador " + m_Players[i].ID + " va hacia atrás");
                        if (m_Players[i].LocalPlayer && !m_Players[i].hasEnded /*&& timer >= 0.25*/)
                            BackDirection = true;
                    }
                    else
                    {
                        if (m_Players[i].LocalPlayer)
                        {
                            BackDirection = false;
                            timer = 0;
                        }
                            
                    }
                }
                arcAux[i] = arcLengths[i];
                 
                string myRaceOrder = "";
                int cont = 1;
                foreach (var _player in m_Players)
                {
                    if (_player.CurrentPosition != cont)
                        _player.CurrentPosition = cont;

                    myRaceOrder += "P" + cont + ": " + _player.Name + "\n";

                    cont++;
                }

                //Si el orden ha cambiado, actualizamos el valor de la interfaz.
                if (!Order.Equals(myRaceOrder))
                {
                    Order = myRaceOrder;
                }
            }
            else
            {
                //Lo primero: si descubre que un jugador ha abandonado la partida, lo quita de la lista.
                if (m_Players[i] == null)
                {
                    QuitPlayer(m_Players[i]);
                }

                //Mantenemos la actualización de tiempo para que cuando un jugador acabe el resto siga actualizando su tiempo local.
                //Además, utilizaremos el totalTime local de cada cliente para simular el tiempo actual de los demás jugadores.
                //Es un poco falso, pero como nos aseguramos de que todos comienzan al mismo tiempo, resulta ser pragmático
                //totalTime += Time.deltaTime; //Sigue actualizando el tiempo total de carrera, que se utilizará para los demás jugadores.
                arcLengths[i] = ComputeCarArcLength(i);
                string myResults = "";
                int cont = 1;
                foreach (var _player in m_Players)
                {
                    if (_player.CurrentPosition != cont)
                        _player.CurrentPosition = cont;

                    switch (cont)
                    {
                        case 1:
                            myResults += "FIRST PLACE: " + _player.Name + " || TIME: " + Math.Round(_player.totalTime, 2) + "\n";
                            break;

                        case 2:
                            if (_player.hasEnded)
                            {
                                myResults += "SECOND PLACE: " + _player.Name + " || TIME: " + Math.Round(_player.totalTime, 2) + "\n";
                                //print("Segundo puesto IS REAL");
                            }
                            else
                            {
                                myResults += "SECOND PLACE: " + _player.Name + " || TIME: " + Math.Round(totalTime, 2) + "\n";
                            }
                            break;

                        case 3:
                            if (_player.hasEnded)
                            {
                                myResults += "THIRD PLACE: " + _player.Name + " || TIME: " + Math.Round(_player.totalTime, 2) + "\n";
                            }
                            else
                            {
                                myResults += "THIRD PLACE: " + _player.Name + " || TIME: " + Math.Round(totalTime, 2) + "\n";
                            }
                            break;

                        case 4:
                            if (_player.hasEnded)
                            {
                                myResults += "LAST PLACE: " + _player.Name + " || TIME: " + Math.Round(_player.totalTime, 2) + "\n";
                            }
                            else
                            {
                                myResults += "LAST PLACE: " + _player.Name + " || TIME: " + Math.Round(totalTime, 2) + "\n";
                            }
                            break;

                        default: //Esto no debería pasar
                            myResults += "??? PLACE: " + _player.Name + " || TIME: " + Math.Round(totalTime, 2) + "\n";
                            break;

                    }

                    updatePodium();

                    cont++;
                }

                if (updateResults != null)
                    updateResults(myResults);

            }
        }

        if (!setupPlayer.m_PlayerInfo.hasEnded)
        {
            m_Players.Sort(new PlayerInfoComparer(arcLengths, m_Players));
            print("Actualizar por pos");
        }
        else
        {
            m_Players.Sort(new PlayerTimeComparer(playerTimes, m_Players));
            print("Actualizar por tiempo");
        }

    }



    /// <summary>
    /// Resetea el tiempo de la vuelta a 0, normalmente porque se ha terminado una vuelta y pasado a la siguiente.
    /// </summary>
    public void resetLapTime()
    {
        tempTime = 0;
    }

    /// <summary>
    /// Función llamada cuando un jugador termina la vuelta de clasificación.
    /// </summary>
    /// <param name="ID"></param>
    public void UpdateServerClasTime(int ID)
    {
        setupPlayer.CmdFinishClas(tempTime, ID);
        playerController.localMove = false;
    }


    /// <summary>
    /// Método llamado desde un command cuando un jugador termina su vuelta de clasificación. En última instancia, llama a un Rcp que notifica esto al resto de jugadores
    ///para que hagan los cálculos pertienentes.
    /// </summary>
    /// <param name="newTime"></param>
    /// <param name="ID"></param>
    public void UpdateClasTime(float newTime, int ID)
    {
        clasTimes.Add(ID, newTime);
        List<float> times = new List<float>();
        List<float> sortedTimes = new List<float>();

        float aux;
        for (int i = 0; i < networkManager.numPlayers; i++)
        {
            clasTimes.TryGetValue(i, out aux);
            print("Aux: " + aux);
            times.Add(aux);
            sortedTimes.Add(aux);
        }
        sortedTimes.Sort();
        print("players length " + networkManager.numPlayers);
        print("times length " + times.ToArray().Length);
        print("times length " + sortedTimes.ToArray().Length);

        clasFinished++;
        RpcClasFinished(times.ToArray(), sortedTimes.ToArray(), clasFinished);
    }


    float ComputeCarArcLength(int ID)
    {
        // Compute the projection of the car position to the closest circuit 
        // path segment and accumulate the arc-length along of the car along
        // the circuit.
        Vector3 carPos = this.m_Players[ID].transform.position;

        int segIdx;
        float carDist;
        Vector3 carProj;

        float minArcL =
            this.m_CircuitController.ComputeClosestPointArcLength(carPos, out segIdx, out carProj, out carDist);

        this.m_DebuggingSpheres[ID].transform.position = carProj;

        if (this.m_Players[ID].CurrentLap == 0)
        {
            minArcL -= m_CircuitController.CircuitLength;
        }
        else
        {
            minArcL += m_CircuitController.CircuitLength *
                       (m_Players[ID].CurrentLap - 1);
        }

        //print(minArcL);

        return minArcL;
    }
}