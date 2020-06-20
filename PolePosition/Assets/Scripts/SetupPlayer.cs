using System;
using Mirror;
using UnityEngine;
using Random = System.Random;
using System.Threading;
using UnityEngine.UI;
/*
	Documentation: https://mirror-networking.com/docs/Guides/NetworkBehaviour.html
	API Reference: https://mirror-networking.com/docs/api/Mirror.NetworkBehaviour.html
*/
public class SetupPlayer : NetworkBehaviour
{
    [SyncVar(hook = nameof(SetID))] private int m_ID; //ID del jugador
    [SyncVar(hook = nameof(SetNombre))] private string m_Name; //Nombre del jugador

    //private UIManager scriptManager.UIManager;
    [SerializeField] NetworkManager m_NetworkManager;
    //private NetworkController m_networkController;
    private ScriptManager scriptManager;
    //public PlayerController scriptManager.playerController;
    //private CheckpointController scriptManager.checkPointController;
    public PlayerInfo playerInfo;
    //private MirrorManager m_MirrorManager;
    //private PolePositionManager scriptManager.polePositionManager;

    //almacenamos el script de selecion del modelo del coche
    public CharacterSelection m_selection;
    [SyncVar(hook = nameof(SetColor))] private int color;

    #region Start & Stop Callbacks

    /// <summary>
    /// This is invoked for NetworkBehaviour objects when they become active on the server.
    /// <para>This could be triggered by NetworkServer.Listen() for objects in the scene, or by NetworkServer.Spawn() for objects that are dynamically created.</para>
    /// <para>This will be called for objects on a "host" as well as for object on a dedicated server.</para>
    /// </summary>
    public override void OnStartServer()
    {
        base.OnStartServer();
        //m_ID = connectionToClient.connectionId;
    }

    /// <summary>
    /// Called on every NetworkBehaviour when it is activated on a client.
    /// <para>Objects on the host have this function called, as there is a local client on the host. The values of SyncVars on object are guaranteed to be initialized correctly with the latest state from the server when this function is called on the client.</para>
    /// </summary>
    public override void OnStartClient()
    {
        base.OnStartClient();

        scriptManager = GetComponent<ScriptManager>(); //Asignamos aquí el scriptManager. Podria dar problemas si el startclient se ejecutase antes del awake de scriptManager

        //Inicialización de los valores del playerInfo
        scriptManager.playerInfo.ID = 0;
        scriptManager.playerInfo.Name = m_Name;
        scriptManager.playerInfo.CurrentLap = 0;
        scriptManager.playerInfo.CurrentPosition = 0;
        scriptManager.playerInfo.checkpointCount = 0;
        Material(color);

        //Guardamos en el playerinfo si este pertenece al jugador local o a otro. Se utiliza en el pole position para distintos aspectos.
        scriptManager.playerInfo.LocalPlayer = isLocalPlayer;

        if (isLocalPlayer)
        {
            scriptManager.playerInfo.OnPositionChangeEvent += OnPositionChangeEventHandler;

            //scriptManager.polePositionManager.setupPlayer = this;
            //scriptManager.polePositionManager.mirrorManager = GetComponent<MirrorManager>();
            //scriptManager.polePositionManager.playerController = GetComponent<PlayerController>();

            //m_networkController = FindObjectOfType<NetworkController>();
            //m_networkController.playerInfo = scriptManager.playerInfo;
            //m_networkController.scriptManager.playerController = scriptManager.playerController;
            //------------------------------------------------------------------
            // EVENTOS
            //------------------------------------------------------------------
            //Asignar los correspondientes eventos a sus llamadas
            ScriptManager.polePositionManager.StartRaceEvent += ScriptManager.UIManager.HideReadyButton;
            ScriptManager.polePositionManager.allPlayersEndedEvent += ScriptManager.UIManager.UpdateStateResult;
            ScriptManager.polePositionManager.allPlayersEndedEvent += ScriptManager.UIManager.canPlayAgain;
            ScriptManager.polePositionManager.playerPlayAgainEvent += ScriptManager.UIManager.addPlayAgainCounter;
            ScriptManager.polePositionManager.playerPlayAgainEvent += ScriptManager.UIManager.playAgain;
            scriptManager.checkPointController.changeLapEvent += ScriptManager.polePositionManager.resetLapTime;
            scriptManager.checkPointController.endRaceEvent += ScriptManager.polePositionManager.PostGameCamera;
            //scriptManager.checkPointController.endRaceEvent += scriptManager.polePositionManager.managePlayersEnded;
            scriptManager.checkPointController.endRaceEvent += ScriptManager.UIManager.endResultsHUD;            ScriptManager.UIManager.PlayerReadyEvent += ScriptManager.polePositionManager.ManageStart;            ScriptManager.UIManager.mirror = GetComponent<MirrorManager>();
        }

        //Añade el jugador a la lista en todos los clientes y servidor
        if(isClient)
        {
            ScriptManager.polePositionManager.AddPlayer(scriptManager.playerInfo);
            //addPlayertoServer()
        }     
    }

    public void addPlayertoServer()
    {
            //scriptManager.mirrorManager.CmdAddPlayer(m_ID, m_Name, 0, 0, 0);
            print("JUGADOR AÑADIDO. JUGADORES ACTUALIZADOS: " + ScriptManager.polePositionManager.numPlayers);
    }

    /// <summary>
    /// Called when the local player object has been set up.
    /// <para>This happens after OnStartClient(), as it is triggered by an ownership message from the server. This is an appropriate place to activate components or functionality that should only be active for the local player, such as cameras and input.</para>
    /// </summary>
    public override void OnStartLocalPlayer()
    {
        //Tambien se podría usar un countdown tradicional pero no se como comunicar estas clases y sus componentes como si fuesen hilos distintos.
        //Si se pudiera, bastaría con hacer aquí un countdown.signal(), y que haya un countdownEvent con tantas señales como jugadores en PolePosition que haga un
        //wait() para que no comience la carrera hasta que cada cliente de su señal.
        CmdSaveColor(m_selection.selection);
        CmdSaveNombre(ScriptManager.UIManager.userName);
        CmdUpdateActualIDPlayer();    
    }


    #endregion

    private void Awake()
    {       
        playerInfo = GetComponent<PlayerInfo>();
        m_NetworkManager = FindObjectOfType<NetworkManager>();
        //scriptManager.UIManager = FindObjectOfType<UIManager>();
        //scriptManager.polePositionManager = FindObjectOfType<PolePositionManager>();
        //scriptManager.checkPointController = GetComponent<CheckpointController>();

        //buscamos el objeto que contenga el script de seleccion de modelo
        m_selection = GameObject.FindGameObjectWithTag("Cars Container").GetComponent<CharacterSelection>();
    }

    // Start is called before the first frame update
    void Start()
    {

        if (isLocalPlayer)
        {
            //ScriptManager.polePositionManager.scriptManager = scriptManager;
            //usamos la funcion material que recibe el valor selection con el que establecemos el color del coche
            //la variable color se compartira entre los distintos jugadores para que se pueda ver a cada jugador del color que deseen           

            scriptManager.playerController.enabled = true;
            ScriptManager.polePositionManager.OnOrderChangeEvent += OnOrderChangeEventHandler;
            scriptManager.playerController.OnSpeedChangeEvent += OnSpeedChangeEventHandler;
            ScriptManager.polePositionManager.updateTime += OnLapChangeEventHandler;
            ScriptManager.polePositionManager.updateResults += OnRaceEndEventHandler;
            ScriptManager.polePositionManager.updateClasTime += OnClasLapChangeEventHandler;
            ScriptManager.polePositionManager.OnBackDirectionChangeEvent += UpdateBackDirectionUI;
            ScriptManager.polePositionManager.OnCrashedStateChangeEvent += UpdateCrashedUI;

            ConfigureCamera();
            ScriptManager.UIManager.StartClasificationLap();
        }
        else if(isClient)
        {
           //Si el coche no es el del jugador local, se oculta a la vista del jugador, para que corra la vuelta de clasificación y decidir el orden de salida.
            Renderer[] renders = gameObject.GetComponentsInChildren<Renderer>();
            Collider[] colliders = gameObject.GetComponentsInChildren<Collider>();

            foreach (Renderer r in renders)
            {
                r.enabled = false;
            }
            foreach (Collider c in colliders)
            {
                c.enabled = false;
            }
        }
        //foreach (AxleInfo axle in scriptManager.playerController.axleInfos)
        //{
        //    Cuando la velocidad es mayor a un valor pequeño, reducimos la fricción con el suelo para que el control del coche sea más rápido y fluido.
        //    WheelFrictionCurve aux = axle.leftWheel.sidewaysFriction;
        //    aux.extremumSlip = 0.4f;
        //    axle.rightWheel.sidewaysFriction = aux;
        //    axle.leftWheel.sidewaysFriction = aux;
        //}
    }

    public void ConfigureCamera()
    {
        if (Camera.main != null) Camera.main.gameObject.GetComponent<CameraController>().m_Focus = this.gameObject;
    }

    #region EVENTS

    void OnSpeedChangeEventHandler(float speed)
    {
        ScriptManager.UIManager.UpdateSpeed((int) speed * 5); // 5 for visualization purpose (km/h)
    }

    void OnOrderChangeEventHandler(string newOrder)
    {
        ScriptManager.UIManager.UpdateOrder(newOrder);
    }

    //Actualizamos el valor de la posición en la interfaz del jugador.
    void OnPositionChangeEventHandler(int position)
    {
        ScriptManager.UIManager.UpdatePosition(position);
    }

    //Actualizamos el valor de la vuelta actual, y el tiempo, en la interfaz del jugador.
    void OnLapChangeEventHandler(int lap, double currentTime, double totalTime, int totalLaps)
    {
        ScriptManager.UIManager.UpdateLap(lap, currentTime, totalTime, totalLaps);
    }

    //Actualizamos el valor de la vuelta actual, y el tiempo, en la interfaz del jugador.
    void OnClasLapChangeEventHandler(double currentTime)
    {
        ScriptManager.UIManager.UpdateClasLap(currentTime);
    }

    void OnRaceEndEventHandler(string results)
    {
        ScriptManager.UIManager.UpdateEndResults(results);
    }

    #endregion

    #region COMMANDS

    //Función que se ejecuta en el servidor para actualizar el valor de la variable del color.
    [Command]
    public void CmdSaveColor(int colorNuevo)
    {
        color = colorNuevo;
    }

    //Función que se ejecuta en el servidor para actualizar el valor de la variable del nombre.
    [Command]
    void CmdSaveNombre(string nombre)
    {
        m_Name = nombre;
    }

    [Command]
    void CmdUpdateActualIDPlayer()
    {
        m_ID = ScriptManager.polePositionManager.updatePlayersID();
    }

    //Sirve para ejecutar el command de polePosition desde un objeto con permisos para ello (el jugador local)
    [Command]
    public void CmdFinishRecon(float time, int ID)
    {
        ScriptManager.polePositionManager.UpdateReconTime(connectionToClient, time, ID);
    }

    #endregion

    #region MISC

    //esta funcion nos permite establecer el color del coche de acuerdo a un switch
    //la intencion es que contenga los colores lo mas parecido a los modelos que se usan dentro del juego
    void Material(int n)
    {
        switch (n)
        {
            case 0:
                GetComponentInChildren<MeshRenderer>().materials[1].color = Color.red;
                break;
            case 1:                GetComponentInChildren<MeshRenderer>().materials[1].color = Color.white;
                break;
            case 2:
                GetComponentInChildren<MeshRenderer>().materials[1].color = Color.yellow;
                break;
            default:
                GetComponentInChildren<MeshRenderer>().materials[1].color = Color.green;
                break;
        }
    }

    //Función que se ejecuta cuando cambia el valor de la variable color. Actualiza el color del coche con el color nuevo.
    void SetColor(int oldColor, int newColor)
    {
        GameObject.Find("Debug").GetComponent<Text>().text = "Nuevo color" + newColor;
        Material(newColor);
    }
    //Función que se ejecuta cuando cambia el valor de la variable m_Name. Actualiza el nombre del jugador con el nombre nuevo.
    void SetNombre(string antiguoNombre, string nuevoNombre)
    {
        playerInfo.Name = nuevoNombre;
    }

    //Función que se ejecuta cuando cambia el valor de la variable m_Name. Actualiza el nombre del jugador con el nombre nuevo.
    void SetID(int antiguoID, int nuevoID)
    {
        playerInfo.ID = nuevoID;
        print("ant id: " + antiguoID);
        print("ID: " + nuevoID);
    }

    void UpdateCrashedUI(bool newVal)
    {
        print("UI Crashed");
        ScriptManager.UIManager.alternateCrash(newVal);
    }

    void UpdateBackDirectionUI(bool newVal)
    {
        print("UI back direction");
        ScriptManager.UIManager.alternateMarchaAtras(newVal);
    }

    #endregion
}