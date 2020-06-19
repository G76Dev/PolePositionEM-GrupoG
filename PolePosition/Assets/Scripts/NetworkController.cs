using Mirror;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class NetworkController : NetworkManager
{

    public PlayerController m_PlayerController;
    private UIManager m_UImanager;
    public PlayerInfo m_PlayerInfo;
    private PolePositionManager m_PolePositionManager;
    [HideInInspector] public MirrorManager m_MirrorManager;

    public override void Awake()
    {
        
        //m_PlayerController = GetComponent<PlayerController>();
        //m_NetworkManager = FindObjectOfType<NetworkManager>();
        m_UImanager = FindObjectOfType<UIManager>();
        m_PolePositionManager = FindObjectOfType<PolePositionManager>();
        //m_PlayerInfo = GetComponent<PlayerInfo>();
    }

    // Start is called before the first frame update
    void Start()
    {
        
    }

    public override void OnClientDisconnect(NetworkConnection conn)
    {
        //Que veria el cliente si resulta que se desconecta del servidor por error.
        m_UImanager.ActivateMainMenu();
        m_PlayerController.canMove = false;
        if (Camera.main != null)
        {
            Camera.main.gameObject.GetComponent<CameraController>().m_Focus = this.gameObject;
        }


        base.OnClientDisconnect(conn);
    }

    public override void OnServerDisconnect(NetworkConnection conn)
    {
        //m_PolePositionManager.RpcPlayerQuit(4);
        print("JUGADOR DESCONECTADO");

        base.OnServerDisconnect(conn);
    }

    public override void OnServerReady(NetworkConnection conn)
    {
        base.OnServerReady(conn);
    }

    public override void OnServerAddPlayer(NetworkConnection conn)
    {
        //Aqui se podría hacer las gestiones que se hacian hasta ahora en el setupPlayer
        m_PolePositionManager.numPlayers++;
        print("NETWORK MANAGER: Jugador añadido");

        base.OnServerAddPlayer(conn);
    }

    public override void ServerChangeScene(string newSceneName)
    {
        //Esto y otras funciones similares se pueden usar para crear un servidor completamente autoritativo

        base.ServerChangeScene(newSceneName);
    }

    public override void OnApplicationQuit()
    {

        m_PolePositionManager.QuitPlayer(m_PlayerInfo);


        


        //StopServer();
        //startser

        base.OnApplicationQuit();
    }

    public override void OnStartHost()
    {
        base.OnStartHost();
    }

    public override void OnServerConnect(NetworkConnection conn)
    {
        
        print("PEPITO SE UNE A LA LUCHA");

        base.OnServerConnect(conn);
    }


    public override void OnStopClient()
    {

        base.OnStopClient();
    }

    // Update is called once per frame
    void Update()
    {
        
    }
}
