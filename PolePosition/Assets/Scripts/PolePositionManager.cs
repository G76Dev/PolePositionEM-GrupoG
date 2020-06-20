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
    #region VARIABLES

    private readonly List<PlayerInfo> m_Players = new List<PlayerInfo>(4);
    private GameObject[] m_DebuggingSpheres;//esfera para uso en el debug



    

    #region INSPECTOR VARS    [Header("Player Variables")]
    [SyncVar] public int numPlayers = 0;//numero de jugadores totales del juego. Se actualiza solamente a traves de metodos del scriptManager.networkController.
    [SyncVar] public int actualPlayerID = 0; //Numero ID del jugador local de este cliente.
    [SyncVar] int reconFinished = 0; //Numero de jugadores que han terminado la vuelta de reconocimiento
    [HideInInspector] int playersReady = 0; //Numero de jugadores que han presionado el botón "ready"
    [SyncVar] [HideInInspector] int playersEnded; //Numero de jugadores que han terminado la carrera
    private int initialPlayers;

    SyncDictionaryIntFloat clasTimes = new SyncDictionaryIntFloat();

    [Header("Spawn location objects")]
    [SerializeField] GameObject[] spawns;
    [SerializeField] GameObject[] PodiumPos;

    [Tooltip("Numero de vueltas totales de la carrera")] public int totalLaps = 3;

    //Referencias a todos los scripts
    public ScriptManager scriptManager;
    private NetworkManager m_networkManager;


    [Tooltip("Lista de checkpoints de la carrera")] public GameObject checkPointList;
    [Tooltip("Objeto al que se enfoca la camara al terminar la carrera")] public GameObject postGameBackground;

    public CircuitController m_CircuitController;//controlador del circuito

    #endregion

    #region TIME/DISTANCE/ORDER VARS

    [SyncVar] private float tempTime = 0; //Tiempo parcial de la vuelta actual 
    [SyncVar] public float totalTime = 0; //Tiempo total de la carrera
    private float auxTimer; //Usado para intentar arreglar el flickering de la vuelta atras
    private float[] arcLengths; //Almacena la distancia recorrida del circuito de cada jugador del actual frame
    private float[] arcAux;     //Almacena la distancia recorrida del circuito de cada jugador del ultimo frame
    private float[] playerTimes;//Supuestamente almacena el tiempo de cada jugador

    //Variable para actualizar el orden de los jugadores en la interfaz
    private string m_CurrentOrder = "";
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
    #endregion

    #region BOOLS

    /// <summary>
    /// Saber si se esta de reconocimiento o en la carrera final
    /// </summary>
    public bool reconocimiento = true;

    /// <summary>
    /// Determina si ha terminado la carrera y se utiliza para enviar segun qué información al HUD y ahorrar cálculos cuando la carrera ya ha terminado
    /// </summary>
    public bool isRaceEnded;

    /// <summary>
    /// Determina si la carrera ha comenzado. 
    /// Se utiliza para sincronizar los contadores de todos los jugadores y para no contar el tiempo antes de que todos estén listos.
    /// </summary>
    private bool hasStarted = false;

    //Variable para actualizar si el jugador local va marcha atras de los jugadores en la interfaz
    private bool m_BackDirection = false;
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

    //Variable para actualizar si el jugador local se ha chocado de los jugadores en la interfaz
    public bool currentCrashed = false;
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

    #endregion

    #region EVENTS

    public delegate void OnBackDirectionChangeDelegate(bool newVal);
    public event OnBackDirectionChangeDelegate OnBackDirectionChangeEvent;
    public delegate void OnCrashedStateChangeDelegate(bool newVal);
    public event OnCrashedStateChangeDelegate OnCrashedStateChangeEvent;
    public delegate void OnOrderChangeDelegate(string newVal);
    public event OnOrderChangeDelegate OnOrderChangeEvent;
    public event OnOrderChangeDelegate updateResults;

    public delegate void SyncEnd();
    public event SyncEnd allPlayersEndedEvent;
    public event SyncEnd playerPlayAgainEvent;

    //Delegado para sincronizar el comienzo de la partida
    public delegate void SyncStart();
    public event SyncStart StartRaceEvent;

    //Delegado para la actualización de las vueltas y el tiempo en la interfaz.
    public delegate void OnLapChangeDelegate(int currentLap, double lapTime, double totalTime, int totalLaps);
    public event OnLapChangeDelegate updateTime;
    //Delegado para la actualización de las vueltas y el tiempo en la interfaz.
    public delegate void OnClasLapChangeDelegate(double lapTime);
    public event OnClasLapChangeDelegate updateClasTime;

    #endregion

    #endregion

    private void Awake()
    {
        if (m_networkManager == null) m_networkManager = FindObjectOfType<NetworkManager>();
        if (m_CircuitController == null) m_CircuitController = FindObjectOfType<CircuitController>();        ScriptManager.polePositionManager = this;
        m_DebuggingSpheres = new GameObject[m_networkManager.maxConnections];
        for (int i = 0; i < m_networkManager.maxConnections; ++i)
        {
            m_DebuggingSpheres[i] = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            m_DebuggingSpheres[i].GetComponent<SphereCollider>().enabled = false;
        }
    }

    private void Start()
    {
        //scriptManager.UIManager.PlayerReadyEvent += ManageStart; //Suscribe al evento que lanza el botón de "Ready" el proceso que se encarga de llamar al Command correspondiente
    }

    public void ManageStart()
    {
        if (isClient)
            scriptManager.mirrorManager.CmdPlayerReady(); //Llama al Command en setupPlayer que se encargará de llamar a "RpcManageStart" en este script
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
    {        initialPlayers = playersReady; //Cada cliente tendrá su copia de initialPlayers para poder ejecutar la lonesomeVictory

        if (!hasStarted)
        {
            scriptManager.playerController.canMove = true; //Actualiza el bool canMove en el playerController del jugador de este cliente, gracias a que el PolePositionManager de cada cliente guarda una referencia al jugador local de ese cliente

            if (StartRaceEvent != null)
                StartRaceEvent();
            //ya no se puede entrar con mas jugadores
            ScriptManager.UIManager.canEnter = false;
            scriptManager.mirrorManager.CmdBlockEntrance();
            hasStarted = true;
        }
    }
    //Esta llamada Rpc está en este lugar por el mismo motivo de la anterior
    [ClientRpc]
    public void RpcManageStart()
    {
        playersReady++; //Suma un jugador listo
        //print("JUGADORES LISTOS: " + playersReady);
        //scriptManager.mirrorManager.CmdPrintServer("playersready: " + playersReady + " numplayers: " + numPlayers);
        if (playersReady >= numPlayers) //Si los jugadores preparados igualan o superan a la cantidad de jugadores,
        {            //scriptManager.mirrorManager.CmdPrintServer("Se entra al if");
            initialPlayers = playersReady; //Se utilizará para saber cuando acabar la partida por abandono
            scriptManager.mirrorManager.CmdStartRace(); //Llama al Command que más tarde llamará al RpcStartRace de este script

        }
    }

    [ClientRpc]
    public void RpcSendTime(float time)
    {
        totalTime = time;
        print("a");
    }

    [ClientRpc]
    public void RpcPlayAgain()
    {
        if (playerPlayAgainEvent != null)
            playerPlayAgainEvent();
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
    public void RpcHook(float[] times, float[] sortedTimes, int finished)
    {
        //print("Jugadores completados " + finished);
        //print("times length " + times.Length);
        //print("times length " + sortedTimes.Length);
        if (finished >= numPlayers)
        {
            tempTime = 0;
            totalTime = 0;
            int cont = 0;
            float aux;
            //print("Numplayers: " + numPlayers + " mplayers: " + m_Players.Count);
            for (int i = 0; i < times.Length; i++)
            {
                //print("Objeto times: " + i + " " + times[i]);
                //print("Objeto sorted: " + i + " " + sortedTimes[i]);
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
                                }
                                foreach (Collider c in colliders)
                                {
                                    c.enabled = true;
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
    }    [TargetRpc]    public void TargetCmdClasTimes(NetworkConnection target, float[] times, float[] sortedTimes, int finished)
    {
        scriptManager.mirrorManager.CmdHookClasTimes(times, sortedTimes, finished);
    }
    #endregion

    private void Update()
    {
        //Si no hay ninguna jugador, ignora el update
        if (numPlayers == 0)
        {
            print("RETURN");
            return;
        }
        else if (numPlayers == 1 && initialPlayers > 1 && !isRaceEnded && isClient) //Si solo queda un jugador, termina la partida por abandono
        {
            lonesomeVictory();
        }

        if (reconocimiento)
        {
            if (isClient)
            {
                //El tiempo se calcula localmente en el cliente durante la carrera de clasificacion para que sea independiente del momento en el que se une el jugador
                totalTime += Time.deltaTime;
                if (!scriptManager.playerInfo.hasEnded)
                {
                    tempTime += Time.deltaTime;
                }            }
            updateReconProgress();
        }
        if (hasStarted) //Solo actualiza el estado de la carrera si ha comenzado. Así ahorramos cálculos innecesarios
        {
            //Ahora que la carrera ha comenzado, el tiempo se maneja exclusivamente desde el servidor
            if (isServer)
            {
                //print("Aumentando contador de tiempo");
                totalTime += Time.deltaTime;
                if (!isRaceEnded) //Solo necesita definir el tiempo por vuelta si no ha terminado la carrera
                {
                    tempTime += Time.deltaTime;
                }
            }            
            if(isClient)
            UpdateRaceProgress();
        }
    }


    #region PLAYER MANAGEMENT

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

    public void QuitPlayer(PlayerInfo player)
    {
        m_Players.Remove(player);

        numPlayers--;

        arcLengths = new float[m_Players.Count];
        playerTimes = new float[m_Players.Count];
        arcAux = new float[m_Players.Count];
    }

    private void lonesomeVictory()
    {
        //Estas lineas son homologas al EndRace() del CheckPointController
        scriptManager.playerController.canMove = false;
        scriptManager.playerInfo.totalTime = totalTime;
        scriptManager.playerInfo.hasEnded = true;

        isRaceEnded = true;

        managePlayersEnded();
        updatePodium(scriptManager.playerInfo, 0);

        PostGameCamera();
        ScriptManager.UIManager.endResultsHUD();
    }

    //Este método solo se ejecuta en el servidor, y se encarga de recibir los "ready" de los jugadores y de lanzar el evento correspondiente cuando todos están listos
    public void anotherPlayerIsReady()
    {
        playersReady++; //Suma un jugador listo
        if (playersReady >= numPlayers) //Si los jugadores preparados igualan o superan a la cantidad de jugadores, lanza el ClientRPC que comenzará la carrera
        {
            initialPlayers = playersReady; //Pero primero el servidor se guarda su copia de estas variables
            RpcStartRace();
            hasStarted = true; //Una vez los jugadores comienzan a moverse, el servidor pone este booleano a true para llevar el progreso de la carrera adecuadamente
            reconocimiento = false; //Y por si acaso, volvemos a poner esto a false.
            NetworkServer.dontListen = true; //Ahora que ha comenzado la carrrera, ya no se aceptarán más jugadores.
        }
    }

    /// <summary>
    /// Suscrito al evento EndRaceEvent. Ordena los jugadores por tiempo, y recorre la lista. Teletransporta a los jugadores que hayan terminado a su correspondiente posición
    /// en el podio.
    /// </summary>
    public void updatePodium(PlayerInfo player, int pos)
    {
        if (player.hasEnded)
        {
            player.gameObject.transform.position = PodiumPos[pos].transform.position;
            player.gameObject.transform.rotation = Quaternion.Euler(0, -90, 0);
        }
    }
    /// <summary>
    /// Suscrito al evento EndRaceEvent. Este metodo es llamado por el evento endRaceEvent. Se gestiona de aumentar la variable playersEnded. Si esta variable es igual o mayor al numero de
    /// jugadores, actualiza el estado de la carrera en la interfaz, ya que quiere decir que todos han terminado.
    /// </summary>
    public void managePlayersEnded()
    {
        if (playersEnded < numPlayers)
        {
            playersEnded++;
            //print("JUGADORES QUE HAN TERMINADO LA CARRERA: " + playersEnded);
        }
        if (allHaveEnded())
        {
            if (allPlayersEndedEvent != null)
                allPlayersEndedEvent();
        }
    }

    private bool allHaveEnded()
    {
        return playersEnded >= numPlayers;
    }

    public void PostGameCamera()
    {
        if (Camera.main != null)
            Camera.main.gameObject.GetComponent<CameraController>().m_Focus = postGameBackground;
    }

    #endregion

    #region COMPARERS

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
        //Método que recibe un player info, y devuelve su índice en la lista de playerinfos.
        //Esto es importante porque la posición de cada player info varía en cada iteración si un jugador adelanta a otro, por lo que usar el id para saber la posición en la lista
        //de un player info como se hacía al principio terminaría dando errores y no detectando bien quien va delante de quien.
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

    //Hace lo mismo que PlayerInfoComparer pero con sus tiempos y no con los Arclengths
    private class PlayerTimeComparer : Comparer<PlayerInfo>
    {
        float[] m_playerTimes;
        List<PlayerInfo> players;

        public PlayerTimeComparer(float[] playerTimes, List<PlayerInfo> par_players)
        {
            m_playerTimes = new float[playerTimes.Length];
            for (int i = 0; i < playerTimes.Length; i++)
            {
                m_playerTimes[i] = playerTimes[i]; //PlayerTimes nunca se modifica. Realmente sirve para algo?
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

    #endregion


    //Método que gestiona los datos de la vuelta de reconocimiento, en lugar de la carrera.
    public void updateReconProgress()
    {
        for (int i = 0; i < m_Players.Count; ++i)
        {
            //Lo primero: si descubre que un jugador ha abandonado la partida, lo quita de la lista.
            if (m_Players[i] == null)
                QuitPlayer(m_Players[i]);

            if (m_Players[i].LocalPlayer)
            {
                arcLengths[i] = ComputeCarArcLength(i);
                if (m_Players[i].LocalPlayer && updateTime != null)
                {
                    updateClasTime(Math.Round(tempTime, 2));
                }

                //Detectar si el jugador va vuelta atras
                #region CLASIFICATION BACKMARCH

                if (this.m_Players[i].CurrentLap == 0)
                {
                    //Si el valor es positivo en la vuelta 0...
                    if ((Math.Abs(arcLengths[i]) - Math.Abs(arcAux[i])) > 0.01) //Intentar hacerlo sin valores absolutos (mas eficiente)
                    {
                        auxTimer += Time.deltaTime;
                        if (m_Players[i].LocalPlayer && !m_Players[i].hasEnded /* && auxTimer >= 0.25*/)
                            BackDirection = true;
                    }
                    else
                    {
                        if (m_Players[i].LocalPlayer)
                        {
                            auxTimer = 0;
                            BackDirection = false;
                        }
                    }
                }
                else
                {
                    //En el resto de vueltas, un valor negativo indicará que el jugador va hacia atrás, como es normal.

                    if ((Math.Abs(arcLengths[i]) - Math.Abs(arcAux[i])) < 0.01) //Intentar hacerlo sin valores absolutos (mas eficiente)
                    {
                        auxTimer += Time.deltaTime;
                        if (m_Players[i].LocalPlayer && !m_Players[i].hasEnded /* && auxTimer >= 0.25*/)
                            BackDirection = true;
                    }
                    else
                    {
                        if (m_Players[i].LocalPlayer)
                        {
                            auxTimer = 0;
                            BackDirection = false;
                        }
                    }
                }
                arcAux[i] = arcLengths[i];
                #endregion


            }
        }
    }

    public void UpdateRaceProgress()
    {
        for (int i = 0; i < m_Players.Count; ++i)
        {
            if (!m_Players[i].hasEnded)
            {
                //Si el jugador no ha terminado la carrera, se sigue actualizando de manera normal su progreso
                #region RACE NOT ENDED UPDATE
                //Lo primero: si descubre que un jugador ha abandonado la partida, lo quita de la lista.
                if (m_Players[i] == null)
                {
                    QuitPlayer(m_Players[i]);
                }

                arcLengths[i] = ComputeCarArcLength(i);

                //if(isServer)
                m_Players[i].totalTime = totalTime;


                if (m_Players[i].LocalPlayer && updateTime != null)
                {
                    updateTime(m_Players[i].CurrentLap, Math.Round(tempTime, 2), Math.Round(totalTime, 2), totalLaps);
                    //tempTime += Time.deltaTime;
                }

                #region BACKMARCH

                if (this.m_Players[i].CurrentLap == 0)
                {
                    //Si el valor es positivo en la vuelta 0...
                    if (arcLengths[i] > arcAux[i])
                    {
                        auxTimer += Time.deltaTime;
                        if (m_Players[i].LocalPlayer && !m_Players[i].hasEnded /* && auxTimer >= 0.25*/)
                            BackDirection = true;
                    }
                    else
                    {
                        if (m_Players[i].LocalPlayer)
                        {
                            auxTimer = 0;
                            BackDirection = false;
                        }
                    }
                }
                else  //Si la vuelta actual no es 0...
                {
                    if (arcLengths[i] < arcAux[i])
                    {
                        auxTimer += Time.deltaTime;
                        if (m_Players[i].LocalPlayer && !m_Players[i].hasEnded /* && auxTimer >= 0.25*/)
                            BackDirection = true;
                    }
                    else
                    {
                        if (m_Players[i].LocalPlayer)
                        {
                            auxTimer = 0;
                            BackDirection = false;
                        }
                    }
                }

                arcAux[i] = arcLengths[i];

                #endregion

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

                #endregion
            }
            else //SI el jugador ha terminado la carrera, ejecuta esta version del metodo.
            {
                //Lo primero: si descubre que un jugador ha abandonado la partida, lo quita de la lista.
                if (m_Players[i] == null)
                {
                    QuitPlayer(m_Players[i]);
                }
                //Sigue calculando las distancias para seguir actualizándolas
                arcLengths[i] = ComputeCarArcLength(i);

                #region UPDATE RESULTS

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

                    updatePodium(_player, cont - 1);

                    cont++;
                }
                if (updateResults != null)
                    updateResults(myResults);

                #endregion
            }
        }

        //Si el jugador no ha terminado, ordena por posición los jugadores para mostrar así correctamente el orden
        if (!isRaceEnded)
        {
            m_Players.Sort(new PlayerInfoComparer(arcLengths, m_Players));
            //print("Actualizar por pos");
        }
        else //Si ha terminado, este cliente organizará a los jugadores por tiempo para mostrarlos en el orden adecuado en la pantalla de resultados
        {
            m_Players.Sort(new PlayerTimeComparer(playerTimes, m_Players));
            //print("Actualizar por tiempo");
        }
    }

    #region TIME MANAGEMENT
    public void resetLapTime()
    {
        tempTime = 0;
    }

    public void UpdateServerReconTime(int ID)
    {
        if (isClient)
            scriptManager.setupPlayer.CmdFinishRecon(tempTime, ID);

        scriptManager.playerController.canMove = false;
        //tempTime = 0;
        //stotalTime = 0;
    }

    //Se ejecuta solamente en el servidor cuando un cliente ejecuta EndClasificationLap y llama a UpdateServerReconTime, que llama a un Command que llama a esta funcion
    public void UpdateReconTime(NetworkConnection target, float newTime, int ID)
    {
        clasTimes.Add(ID, newTime);
        List<float> times = new List<float>();
        List<float> sortedTimes = new List<float>();
        float aux;
        for (int i = 0; i < ScriptManager.networkController.numPlayers; i++)
        {
            clasTimes.TryGetValue(i, out aux);

            times.Add(aux);
            sortedTimes.Add(aux);
        }
        sortedTimes.Sort();
        //print("players length " + scriptManager.networkController.numPlayers);
        //print("times length " + times.ToArray().Length);
        //print("times length " + sortedTimes.ToArray().Length);
        reconFinished++;
        //Hace un targetRPC al jugador que ha terminado la vuelta de clasificacion y dentro de se target llama a CmdClasTimes
        TargetCmdClasTimes(target, times.ToArray(), sortedTimes.ToArray(), reconFinished);

        //Cuando hayan terminado todos los jugadores, resetea el tiempo en el servidor.
        if(reconFinished >= numPlayers && isServer)
        {
            totalTime = 0;
            tempTime = 0;
        }

    }

    #endregion

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

        return minArcL;
    }


    //esto tiene que estar aqui
    [ClientRpc]    public void RpcGetCanEnterFromServer(bool cE)
    {
        ScriptManager.UIManager.canEnter = cE;
        ScriptManager.UIManager.doneWaiting = true;
    }

}