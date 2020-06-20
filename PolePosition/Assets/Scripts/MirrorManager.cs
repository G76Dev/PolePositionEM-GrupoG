using System;
using Mirror;
using UnityEngine;
using Random = System.Random;
using System.Threading;
using Microsoft.Win32;

public class MirrorManager : NetworkBehaviour
{

    private ScriptManager scriptManager;

    // Start is called before the first frame update
    void Start()
    {
        scriptManager = GetComponent<ScriptManager>();
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
        ScriptManager.polePositionManager.RpcStartRace();
    }

    /*[Command]
    public void CmdStartRaceOnlyServer()
    {
        ScriptManager.polePositionManager.StartRaceOnlyServer();
    }*/

    [Command]
    public void CmdPlayerReady()
    {
        ScriptManager.polePositionManager.anotherPlayerIsReady();
    }

    [Command]
    public void CmdPrintServer(string value)
    {
        print(value);
    }

    [Command]
    public void CmdPlayAgain()
    {
        ScriptManager.polePositionManager.RpcPlayAgain();
    }

    [Command]
    public void CmdHookClasTimes(float[] times, float[] sortedTimes, int finished)
    {
        ScriptManager.polePositionManager.RpcHook(times, sortedTimes, finished);
    }

    //[Command]
    //public void CmdAddPlayer(int id, string N, int CLap, int CPos, int checkCount)
    //{
    //    PlayerInfo player = new PlayerInfo();
    //    player.ID = id;
    //    player.name = N;
    //    player.CurrentLap = CLap;
    //    player.CurrentPosition = CPos;
    //    player.checkpointCount = checkCount;

    //    ScriptManager.polePositionManager.AddPlayer(player);
    //}

    [Command]
    public void CmdBlockEntrance()
    {
        ScriptManager.UIManager.canEnter = false;
    }


    [Command]
    public void CmdGetCanEnterFromServer()
    {
        print(ScriptManager.UIManager.canEnter);
        ScriptManager.polePositionManager.RpcGetCanEnterFromServer(ScriptManager.UIManager.canEnter);
    }

}
