using System;
using Mirror;
using UnityEngine;
using Random = System.Random;
using System.Threading;

public class MirrorManager : NetworkBehaviour
{

    private UIManager m_UIManager;
    private NetworkManager m_NetworkManager;
    private PlayerController m_PlayerController;
    private PlayerInfo m_PlayerInfo;
    private PolePositionManager m_PolePositionManager;

    private void Awake()
    {
        m_PlayerInfo = GetComponent<PlayerInfo>();
        m_PlayerController = GetComponent<PlayerController>();
        m_NetworkManager = FindObjectOfType<NetworkManager>();
        m_PolePositionManager = FindObjectOfType<PolePositionManager>();
        m_UIManager = FindObjectOfType<UIManager>();
    }

    //-------------------------
        //COMANDOS
    //-------------------------
    //Los commands se ejecutan aquí porque solamente los scripts asociados al prefab del jugador pueden enviar mensajes Command al servidor en esta version de Mirror.
    //Para poder hacerlo desde otros scripts, esos scripts contendrán una referencia directa al SetupPlayer del jugador local de ese cliente/servidor,
    //y desde aquí ejecutarán el comando necesario utilizando la referencia directa a este script.

    [Command]
    public void CmdStartRace()
    {
        m_PolePositionManager.RpcStartRace();
    }

    [Command]
    public void CmdPlayerReady()
    {
        m_PolePositionManager.RpcManageStart();
    }

    [Command]
    public void CmdPrintServer(string value)
    {
        print(value);
    }

    [Command]
    public void CmdPlayAgain()
    {
        m_PolePositionManager.RpcPlayAgain();
    }

    [Command]
    public void CmdEndRace()
    {

    }


    // Start is called before the first frame update
    void Start()
    {
        
    }

    // Update is called once per frame
    void Update()
    {
        
    }
}
