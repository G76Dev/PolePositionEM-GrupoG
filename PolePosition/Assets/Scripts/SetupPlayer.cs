using System;
using Mirror;
using UnityEngine;
using Random = System.Random;
using System.Threading;

/*
	Documentation: https://mirror-networking.com/docs/Guides/NetworkBehaviour.html
	API Reference: https://mirror-networking.com/docs/api/Mirror.NetworkBehaviour.html
*/

public class SetupPlayer : NetworkBehaviour
{
    [SyncVar] private int m_ID;
    [SyncVar] private string m_Name;
    //CountdownEvent comenzar = new CountdownEvent(2);

    private UIManager m_UIManager;
    private NetworkManager m_NetworkManager;
    private PlayerController m_PlayerController;
    private PlayerInfo m_PlayerInfo;
    private PolePositionManager m_PolePositionManager;
    //almacenamos el script de selecion del modelo del coche
    private CharacterSelection m_selection;
    private int color;

    #region Start & Stop Callbacks

    /// <summary>
    /// This is invoked for NetworkBehaviour objects when they become active on the server.
    /// <para>This could be triggered by NetworkServer.Listen() for objects in the scene, or by NetworkServer.Spawn() for objects that are dynamically created.</para>
    /// <para>This will be called for objects on a "host" as well as for object on a dedicated server.</para>
    /// </summary>
    public override void OnStartServer()
    {
        base.OnStartServer();
        m_ID = connectionToClient.connectionId;
    }

    /// <summary>
    /// Called on every NetworkBehaviour when it is activated on a client.
    /// <para>Objects on the host have this function called, as there is a local client on the host. The values of SyncVars on object are guaranteed to be initialized correctly with the latest state from the server when this function is called on the client.</para>
    /// </summary>
    public override void OnStartClient()
    {
        base.OnStartClient();
        m_PlayerInfo.ID = m_ID;
        m_PlayerInfo.Name = "Player" + m_ID;
        m_PlayerInfo.CurrentLap = 0;
        m_PlayerInfo.CurrentPosition = 0;

        //Guardamos en el playerinfo si este pertenece al jugador local o a otro. Se utiliza en el pole position para distintos aspectos.
        m_PlayerInfo.LocalPlayer = isLocalPlayer;
        m_PolePositionManager.AddPlayer(m_PlayerInfo);
    }

    /// <summary>
    /// Called when the local player object has been set up.
    /// <para>This happens after OnStartClient(), as it is triggered by an ownership message from the server. This is an appropriate place to activate components or functionality that should only be active for the local player, such as cameras and input.</para>
    /// </summary>
    public override void OnStartLocalPlayer()
    {
    }

    #endregion

    private void Awake()
    {
        m_PlayerInfo = GetComponent<PlayerInfo>();
        m_PlayerController = GetComponent<PlayerController>();
        m_NetworkManager = FindObjectOfType<NetworkManager>();
        m_PolePositionManager = FindObjectOfType<PolePositionManager>();
        m_UIManager = FindObjectOfType<UIManager>();
        //buscamos el objeto que contenga el script de seleccion de modelo
        m_selection = GameObject.FindGameObjectWithTag("Cars Container").GetComponent<CharacterSelection>();
    }

    // Start is called before the first frame update
    void Start()
    {
        if (isLocalPlayer)
        {
            //usamos la funcion material que recibe el valor selection con el que establecemos el color del coche
            //la variable color se compartira entre los distintos jugadores para que se pueda ver a cada jugador del color que deseen
            color = m_selection.selection;
            Material(color);

            m_PlayerController.enabled = true;
            m_PlayerController.OnSpeedChangeEvent += OnSpeedChangeEventHandler;
            m_PolePositionManager.OnPositionChangeEvent += OnPositionChangeEventHandler;
            m_PolePositionManager.OnLapChangeEvent += OnLapChangeEventHandler;
            ConfigureCamera();
        }
        else
        {
            //Hacer que el objeto detecte el color elegido por su jugador y lo pinte.
        }
    }

    void OnSpeedChangeEventHandler(float speed)
    {
        m_UIManager.UpdateSpeed((int) speed * 5); // 5 for visualization purpose (km/h)
    }

    //Actualizamos el valor de la posición en la interfaz del jugador.
    void OnPositionChangeEventHandler(int position)
    {
        m_UIManager.UpdatePosition(position);
    }

    //Actualizamos el valor de la vuelta actual, y el tiempo, en la interfaz del jugador.
    void OnLapChangeEventHandler(int lap, int currentTime, int totalTime)
    {
        m_UIManager.UpdateLap(lap, currentTime, totalTime);
    }

    void ConfigureCamera()
    {
        if (Camera.main != null) Camera.main.gameObject.GetComponent<CameraController>().m_Focus = this.gameObject;
    }

    //esta funcion nos permite establecer el color del coche de acuerdo a un switch
    //la intencion es que contenga los colores lo mas parecido a los modelos que se usan dentro del juego
    void Material(int n)
    {
        switch (n)
        {
            case 0:
                GetComponentInChildren<MeshRenderer>().materials[1].color = Color.red;
                break;
            case 1:
                GetComponentInChildren<MeshRenderer>().materials[1].color = Color.white;
                break;
            case 2:
                GetComponentInChildren<MeshRenderer>().materials[1].color = Color.yellow;
                break;
            default:
                GetComponentInChildren<MeshRenderer>().materials[1].color = Color.green;
                break;
        }
        
    }
}