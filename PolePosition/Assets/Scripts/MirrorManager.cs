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

    /// <summary>
    /// Llama a un Rpc para el comienzo de la partida.
    /// </summary>
    [Command]
    public void CmdStartRace()
    {
        m_PolePositionManager.RpcStartRace();
    }

    /// <summary>
    /// Llama a un rpc para gestionar el comienzo de la partida.
    /// </summary>
    [Command]
    public void CmdPlayerReady()
    {
        m_PolePositionManager.RpcManageStart();
    }

    /// <summary>
    /// Método que sirve para imprimir por consola del servidor datos que no están en el mismo.
    /// </summary>
    /// <param name="value"></param>
    [Command]
    public void CmdPrintServer(string value)
    {
        print(value);
    }

    /// <summary>
    /// Llama a un Rpc para jugar otra vez.
    /// </summary>
    [Command]
    public void CmdPlayAgain()
    {
        m_PolePositionManager.RpcPlayAgain();
    }

    /// <summary>
    /// 
    /// </summary>
    [Command]
    public void CmdEndRace()
    {

    }

    /// <summary>
    /// Métodos para eliminar la deriva del coche local y los otros que visualiza.
    /// </summary>
    /// <param name="id"></param>
    /// <param name="friction"></param>
    [ClientRpc]
    private void RpcFric(int id, float friction)
    {
        int index = -1;
        for (int i = 0; i < m_PolePositionManager.m_Players.Count; i++)
        {
            if (m_PolePositionManager.m_Players[i].ID == id)
            {
                index = i;
            }
        }

        if (index != -1)
        {
            WheelFrictionCurve fric = m_PolePositionManager.m_Players[index].GetComponent<PlayerController>().axleInfos[0].rightWheel.sidewaysFriction;
            fric.extremumSlip = friction;
            m_PolePositionManager.m_Players[index].GetComponent<PlayerController>().axleInfos[0].rightWheel.sidewaysFriction = fric;
            m_PolePositionManager.m_Players[index].GetComponent<PlayerController>().axleInfos[0].leftWheel.sidewaysFriction = fric;
            m_PolePositionManager.m_Players[index].GetComponent<PlayerController>().axleInfos[1].rightWheel.sidewaysFriction = fric;
            m_PolePositionManager.m_Players[index].GetComponent<PlayerController>().axleInfos[1].leftWheel.sidewaysFriction = fric;
            print("Dentro de la drriva del host");
        }
        
    }

    /// <summary>
    /// Sirve para que cada jugador local mande su friccion al resto, para que a todos se les aplique igual.
    /// </summary>
    /// <param name="id"></param>
    /// <param name="friction"></param>
    [Command]
    public void CmdFric(int id, float friction)
    {
        int index = -1;
        for (int i = 0; i < m_PolePositionManager.m_Players.Count; i++)
        {
            if(m_PolePositionManager.m_Players[i].ID == id)
            {
                index = i;
            }
        }

        if (index != -1)
        {
            WheelFrictionCurve fric = m_PolePositionManager.m_Players[index].GetComponent<PlayerController>().axleInfos[0].rightWheel.sidewaysFriction;
            fric.extremumSlip = friction;
            m_PolePositionManager.m_Players[index].GetComponent<PlayerController>().axleInfos[0].rightWheel.sidewaysFriction = fric;
            m_PolePositionManager.m_Players[index].GetComponent<PlayerController>().axleInfos[0].leftWheel.sidewaysFriction = fric;
            m_PolePositionManager.m_Players[index].GetComponent<PlayerController>().axleInfos[1].rightWheel.sidewaysFriction = fric;
            m_PolePositionManager.m_Players[index].GetComponent<PlayerController>().axleInfos[1].leftWheel.sidewaysFriction = fric;
            RpcFric(id, friction);
            print("Dentro de la drriva del cliente");
        }
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
