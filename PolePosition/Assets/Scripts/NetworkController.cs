using Mirror;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class NetworkController : NetworkManager
{
    public ScriptManager scriptManager;

    public override void Awake()
    {
        ScriptManager.networkController = this;

        //scriptManager.playerController = GetComponent<PlayerController>();
        //m_NetworkManager = FindObjectOfType<NetworkManager>();
        //scriptManager.UIManager = FindObjectOfType<UIManager>();
        //scriptManager.polePositionManager = FindObjectOfType<PolePositionManager>();
        //m_PlayerInfo = GetComponent<PlayerInfo>();
    }

    // Start is called before the first frame update
    void Start()
    {
        
    }

    public override void OnClientDisconnect(NetworkConnection conn)
    {
        //Que veria el cliente si resulta que se desconecta del servidor por error.
        ScriptManager.UIManager.ActivateMainMenu();
        scriptManager.playerController.canMove = false;

        //To do: Desconectar clientes si es necesario y resetear las variables.
        //To do: Si es posible, poner un mensaje de "desconectado".


        if (Camera.main != null)
        {
            Camera.main.gameObject.GetComponent<CameraController>().m_Focus = this.gameObject;
        }


        base.OnClientDisconnect(conn);
    }

    public override void OnServerDisconnect(NetworkConnection conn)
    {
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
        ScriptManager.polePositionManager.numPlayers++;
        
        print("NETWORK MANAGER: Jugador añadido");

        base.OnServerAddPlayer(conn);
    }
    public override void ServerChangeScene(string newSceneName)
    {
        //Esto y otras funciones similares se pueden usar para crear un servidor completamente autoritativo

        base.ServerChangeScene(newSceneName);
    }

    public override void OnStartServer()
    {
        //print("ADDRESS: " + this.networkAddress);


        base.OnStartServer();
    }

    public override void OnApplicationQuit()
    {
        //En el caso de que sea un cliente, destruye su coche antes de cerrar la aplicacion y actualiza las variables
        if(this.mode == NetworkManagerMode.ClientOnly || this.mode == NetworkManagerMode.Host)
        ScriptManager.polePositionManager.QuitPlayer(scriptManager.playerInfo);

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


}
