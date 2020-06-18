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
    //Mutex mutex = new Mutex();

    public int numPlayers = 0;//numero de jugadores
    [SyncVar] public int actualPlayerID = 0;
    [SyncVar] int reconFinished = 0;
    [SyncVar] [HideInInspector] int playersReady;

    SyncDictionaryIntFloat clasTimes = new SyncDictionaryIntFloat();

    [SerializeField] GameObject[] spawns;

    public int totalLaps = 3;

    public NetworkManager networkManager;//controlador de la conexion
    private UIManager m_UImanager;
    public SetupPlayer setupPlayer;
    public PlayerController playerController;
    public MirrorManager mirrorManager;
    public GameObject checkPointList;
    public GameObject postGameBackground;

    float[] arcAux;

    private readonly List<PlayerInfo> m_Players = new List<PlayerInfo>(4);
    public CircuitController m_CircuitController;//controlador del circuito
    private GameObject[] m_DebuggingSpheres;//esfera para uso en el debug

    private float timer=0;

    private float tempTime = 0;
    public float totalTime = 0;
    private float[] arcLengths;
    private float[] playerTimes;

    //Saber si se esta de reconocimiento o en la carrera final
    public bool reconocimiento = true;
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

    public delegate void OnBackDirectionChangeDelegate(bool newVal);

    public event OnBackDirectionChangeDelegate OnBackDirectionChangeEvent;

    public delegate void OnCrashedStateChangeDelegate(bool newVal);

    public event OnCrashedStateChangeDelegate OnCrashedStateChangeEvent;

    public delegate void OnOrderChangeDelegate(string newVal);

    public event OnOrderChangeDelegate OnOrderChangeEvent;

    public event OnOrderChangeDelegate updateResults;

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

    void ManageStart()
    {
        mirrorManager.CmdPlayerReady(); //Llama al Command en setupPlayer que se encargará de llamar a "RpcManageStart" en este script
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
    {
        if (!hasStarted)
        {
            setupPlayer.m_PlayerController.canMove = true; //Actualiza el bool canMove en el playerController del jugador de este cliente, gracias a que el PolePositionManager de cada cliente guarda una referencia al jugador local de ese cliente

            if (StartRaceEvent != null)
                StartRaceEvent();

            hasStarted = true;
        }       
    }

    //Esta llamada Rpc está en este lugar por el mismo motivo de la anterior
    [ClientRpc]
    public void RpcManageStart()
    {
        playersReady++; //Suma un jugador listo
        //print("JUGADORES LISTOS: " + playersReady);

        if (playersReady >= numPlayers) //Si los jugadores preparados igualan o superan a la cantidad de jugadores,
        {
            mirrorManager.CmdStartRace(); //Llama al Command que más tarde llamará al RpcStartRace de este script
        }
    }

    //Función que se ejecuta cada vez que el valor de reconFinished cambia, es decir, cada vez que un jugador termina la vuelta de reconocimiento.
    //[TargetRpc]
    //void RpcHookRecon(NetworkConnection target, int position)
    //{
    //    foreach (PlayerInfo info in m_Players)
    //    {
    //        if (info.LocalPlayer)
    //        {
    //            info.gameObject.transform.position = spawns[position].transform.position;
    //        }
    //    }
    //}

    [ClientRpc]
    void RpcHook(float[] times, float[] sortedTimes, int finished)
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
            return;

        
        if (reconocimiento)
        {
            if (!setupPlayer.gameObject.GetComponent<PlayerInfo>().hasEnded)
            {
                tempTime += Time.deltaTime;
                totalTime += Time.deltaTime;
            }

            timer += Time.deltaTime;
            updateReconProgress();
        }
        if (hasStarted) //Solo actualiza el estado de la carrera si ha comenzado. Así ahorramos cálculos innecesarios
        {
            if (!setupPlayer.gameObject.GetComponent<PlayerInfo>().hasEnded)
            {
                tempTime += Time.deltaTime;
                totalTime += Time.deltaTime;
            }
            
            timer += Time.deltaTime;
            UpdateRaceProgress();
        }     
    }

    public int updatePlayersID()
    {
        int aux = actualPlayerID;
        actualPlayerID++;
        return aux;
    }

    //añade un jugador
    public void AddPlayer(PlayerInfo player)
    {
        print("Nombre: " + player.Name + " ID: " + player.ID);
        m_Players.Add(player);
        arcLengths = new float[m_Players.Count];
        playerTimes = new float[m_Players.Count];
        arcAux = new float[m_Players.Count];
    }

    public void PostGameCamera()
    {
        if (Camera.main != null)
            Camera.main.gameObject.GetComponent<CameraController>().m_Focus = postGameBackground;
    }

    private class PlayerInfoComparer : Comparer<PlayerInfo>
    {
        float[] m_ArcLengths;
        List<PlayerInfo> players;

        public PlayerInfoComparer(float[] arcLengths, List<PlayerInfo> par_players)
        {
            m_ArcLengths = arcLengths;
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

        //Método que recibe un player info, y devuelve su índice en la lista de playerinfos.
        //Esto es importante porque la posición de cada player info varía en cada iteración si un jugador adelanta a otro, por lo que usar el id para saber la posición en la lista
        //de un player info como se hacía al principio terminaría dando errores y no detectando bien quien va delante de quien.
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

    //Método que gestiona los datos de la vuelta de reconocimiento, en lugar de la carrera.
    public void updateReconProgress()
    {
        float[] arcLengths = new float[m_Players.Count]; //Es MUY ineficiente que se declare un nuevo array en cada frame
        for (int i = 0; i < m_Players.Count; ++i)
        {
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

    //Hace lo mismo que PlayerInfoComparer pero con sus tiempos y no con los Arclengths
    private class PlayerTimeComparer : Comparer<PlayerInfo>
    {
        float[] m_playerTimes;
        List<PlayerInfo> players;

        public PlayerTimeComparer(float[] playerTimes, List<PlayerInfo> par_players)
        {
            m_playerTimes = playerTimes;
            players = par_players;
        }

        public override int Compare(PlayerInfo x, PlayerInfo y)
        {
            if (this.m_playerTimes[GetIndex(x)] < m_playerTimes[GetIndex(y)])
                return 1;
            else return -1;
        }

        ////Método que recibe un player info, y devuelve su índice en la lista de playerinfos.
        ////Esto es importante porque la posición de cada player info varía en cada iteración si un jugador adelanta a otro, por lo que usar el id para saber la posición en la lista
        ////de un player info como se hacía al principio terminaría dando errores y no detectando bien quien va delante de quien.
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
        // Update car arc-lengths
        //arcLengths = new float[m_Players.Count]; //Es MUY ineficiente que se declare un nuevo array en cada frame
        //Solo seria necesario aumentar el tamaño del array cada vez que se añade o se quita un jugador.

        for (int i = 0; i < m_Players.Count; ++i)
        {
            if (!m_Players[i].hasEnded)
            {
                //Si el jugador es local, se actualizan los tiempos.
                //if (m_Players[i].LocalPlayer)
                //{
                m_Players[i].totalTime = totalTime;
                //}

                arcLengths[i] = ComputeCarArcLength(i);
                //print("ORIGINAL: " + i + " " +  arcLengths[i]);
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
                        //print("ARCLENGHT " + arcLengths[i]);
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

                //print((Math.Abs(arcLengths[i]) - Math.Abs(arcAux[i])));
                arcAux[i] = arcLengths[i];
    
                if(m_Players[i].LocalPlayer)
                    m_Players.Sort(new PlayerInfoComparer(arcLengths, m_Players));

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

                //Con esto se llamaría al evento para actualizar la posición. Falta saber quien es el jugador local para poner su posicion en lugar de la de otro
                //if(OnPositionChangeEvent != null)
                //{

                //}


                //Debug.Log("El orden de carrera es: " + myRaceOrder + "\n");
            }
            else
            {
                //Mantenemos la actualización de tiempo para que cuando un jugador acabe el resto siga actualizando su tiempo local.
                //Además, utilizaremos el totalTime local de cada cliente para simular el tiempo actual de los demás jugadores.
                //Es un poco falso, pero como nos aseguramos de que todos comienzan al mismo tiempo, resulta ser pragmático

                //totalTime += Time.deltaTime; //Sigue actualizando el tiempo total de carrera, que se utilizará para los demás jugadores.
                arcLengths[i] = ComputeCarArcLength(i);

                if (m_Players[i].LocalPlayer)
                    m_Players.Sort(new PlayerTimeComparer(playerTimes, m_Players));

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

                    cont++;
                    //print("Aumenta CONTADOR");
                }

                if (updateResults != null)
                    updateResults(myResults);

            }
        }

    }

    public void resetLapTime()
    {
        tempTime = 0;
    }

    public void UpdateServerReconTime(int ID)
    {
        //foreach (PlayerInfo player in m_Players)
        //{
        //    if (player.LocalPlayer)
        //    {
        //        player.gameObject.GetComponent<SetupPlayer>().CmdFinishRecon(tempTime, ID);
        //    }
        //}
        setupPlayer.CmdFinishRecon(tempTime, ID);
        playerController.localMove = false;
        tempTime = 0;
        totalTime = 0;
        //tempTime = 0;
        //totalTime = 0;
        print("Canmove: " + playerController.canMove + " localmove: " + playerController.localMove);
    }



    public void UpdateReconTime(float newTime, int ID)
    {
        //mutex.WaitOne();
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

        reconFinished++;
        RpcHook(times.ToArray(), sortedTimes.ToArray(), reconFinished);
        //NetworkConnectionToClient[] clients = new NetworkConnectionToClient[networkManager.maxConnections];

        //for (int i = 0; i < clients.Length; i++)
        //{
        //    NetworkServer.connections.TryGetValue(i, out clients[i]);
        //}
        //mutex.ReleaseMutex();
    }



    //¿Calculos redundantes?
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