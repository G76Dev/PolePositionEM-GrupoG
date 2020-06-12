using System;
using System.Collections;
using System.Collections.Generic;
using System.Text;
using Mirror;
using Mirror.Examples.Basic;
using UnityEngine;

public class PolePositionManager : NetworkBehaviour
{
    public int numPlayers;//numero de jugadores
    [SyncVar] [HideInInspector] int playersReady;
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
    private CircuitController m_CircuitController;//controlador del circuito
    private GameObject[] m_DebuggingSpheres;//esfera para uso en el debug

    private float tempTime = 0;
    private float totalTime = 0;
    


    //Delegado para sincronizar el comienzo de la partida
    public delegate void SyncStart();

    public event SyncStart StartRaceEvent;

    //Delegado para la actualización de la posición en la interfaz.
    public delegate void OnPositionChangeDelegate(int newVal);

    public event OnPositionChangeDelegate OnPositionChangeEvent;

    //Delegado para la actualización de las vueltas y el tiempo en la interfaz.
    public delegate void OnLapChangeDelegate(int newVal, int newVal2, int newVal3);

    public event OnLapChangeDelegate updateTime;

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

        setupPlayer.m_PlayerController.canMove = true; //Actualiza el bool canMove en el playerController del jugador de este cliente, gracias a que el PolePositionManager de cada cliente guarda una referencia al jugador local de ese cliente

        if(StartRaceEvent != null)
            StartRaceEvent();
    }

    //Esta llamada Rpc está en este lugar por el mismo motivo de la anterior
    [ClientRpc]
    public void RpcManageStart()
    {
        playersReady++; //Suma un jugador listo
        //print("JUGADORES LISTOS: " + playersReady);

        if (playersReady >= numPlayers) //Si los jugadores preparados igualan o superan a la cantidad de jugadores,
        {
            if (isServer) //Si es el servidor
                mirrorManager.CmdStartRace(); //Llama al Command que más tarde llamará al RpcStartRace de este script
        }
    }

    #endregion

    private void Update()
    {
        if (m_Players.Count == 0)
            return;

            UpdateRaceProgress();

    }

    //añade un jugador
    public void AddPlayer(PlayerInfo player)
    {
        m_Players.Add(player);
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
            players = par_players;
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
                if (pi.Equals(players[i]))
                {
                    index = i;
                }
            }
            return index;
        }
    }

    public void UpdateRaceProgress()
    {
        // Update car arc-lengths
        float[] arcLengths = new float[m_Players.Count]; //Es MUY ineficiente que se declare un nuevo array en cada frame

        for (int i = 0; i < m_Players.Count; ++i)
        {
            //Si el jugador es local, se actualizan los tiempos.
            if (m_Players[i].LocalPlayer)
            {
                tempTime += Time.deltaTime;
                totalTime += Time.deltaTime;
            }

            arcLengths[i] = ComputeCarArcLength(i);
            //print("ORIGINAL: " + i + " " +  arcLengths[i]);
            if (m_Players[i].LocalPlayer && updateTime != null)
            {
                updateTime(m_Players[i].CurrentLap, (int)tempTime, (int)(totalTime));
            }


            if (this.m_Players[i].CurrentLap == 0)
            {
                //Si el valor es positivo en la vuelta 0...
                if ((Math.Abs(arcLengths[i]) - Math.Abs(arcAux[i])) > 0.01) //Intentar hacerlo sin valores absolutos (mas eficiente)
                {
                    print("El jugador " + m_Players[i].ID + " va hacia atrás");
                }
            }
            else
            {
                //En el resto de vueltas, un valor negativo indicará que el jugador va hacia atrás, como es normal.
                if ((Math.Abs(arcLengths[i]) - Math.Abs(arcAux[i])) < 0.01) //Intentar hacerlo sin valores absolutos (mas eficiente)
                {
                    print("El jugador " + m_Players[i].ID + " va hacia atrás");
                }
            }


            
            //print((Math.Abs(arcLengths[i]) - Math.Abs(arcAux[i])));
            arcAux[i] = arcLengths[i];
        }

        m_Players.Sort(new PlayerInfoComparer(arcLengths, m_Players));

        string myRaceOrder = "";
        int cont = 1;
        foreach (var _player in m_Players)
        {
            if (_player.CurrentPosition != cont)
            {
                _player.CurrentPosition = cont;
                if (_player.LocalPlayer && OnPositionChangeEvent != null)
                {
                    OnPositionChangeEvent((_player.CurrentPosition));
                }
            }
            myRaceOrder += _player.Name + " ";         

            cont++;
        }

        //Con esto se llamaría al evento para actualizar la posición. Falta saber quien es el jugador local para poner su posicion en lugar de la de otro
        //if(OnPositionChangeEvent != null)
        //{
            
        //}


        Debug.Log("El orden de carrera es: " + myRaceOrder + "\n" );
    }

    public void resetLapTime()
    {
        tempTime = 0;
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