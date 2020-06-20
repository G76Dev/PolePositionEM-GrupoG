using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class CheckpointController : MonoBehaviour
{
    private ScriptManager scriptManager;

    [HideInInspector] GameObject checkpointList;

    public delegate void changeLapDelegate();

    public event changeLapDelegate changeLapEvent;
    public event changeLapDelegate endRaceEvent;


    public void Awake()
    {
        scriptManager = GetComponent<ScriptManager>();
    }

    // Start is called before the first frame update
    void Start()
    {
        scriptManager.playerInfo.checkpointCount = 0;
        checkpointList = ScriptManager.polePositionManager.checkPointList;
        //print("HIJOS: " + checkpointList.transform.childCount);
    }

    //Este método se encarga de dectecar y gestionar las colisiones con los checkpoints, actualizando el progreso del jugador en la carrera.
    void OnTriggerEnter(Collider col)
    {
        //Solo se comprobará el checkpoint si el objeto collider tiene el tag adecuado
        if (col.tag == "Checkpoint")
        {
            int nextCheckPoint = (scriptManager.playerInfo.CheckPoint + 1) % checkpointList.transform.childCount;

            if (int.Parse(col.name) == nextCheckPoint)
            {
                scriptManager.playerInfo.checkpointCount++;

                if (scriptManager.playerInfo.checkpointCount == checkpointList.transform.childCount)
                {
                    if (!ScriptManager.polePositionManager.reconocimiento)
                    {
                        scriptManager.playerInfo.CurrentLap++;
                    }
                    else
                    {
                        if (scriptManager.playerInfo.LocalPlayer)
                        {
                            EndClasificactionLap();
                        }
                    }

                    if (scriptManager.playerInfo.CurrentLap >= ScriptManager.polePositionManager.totalLaps)
                    {
                        EndRace();
                        return;
                    }

                    scriptManager.playerInfo.checkpointCount = 0;

                    if (changeLapEvent != null)
                        changeLapEvent();
                }
                scriptManager.playerInfo.CheckPoint = nextCheckPoint;
            }

        }
    }

    public void EndClasificactionLap()
    {
        ScriptManager.polePositionManager.reconocimiento = false;
        ScriptManager.polePositionManager.UpdateServerReconTime(scriptManager.playerInfo.ID);
        ScriptManager.UIManager.FinishClasificationLap();
    }

    public void EndRace()
    {
        scriptManager.playerController.canMove = false; //El jugador que haya superado la carrera se dejará de mover.

        //To do: teletransportar al podio

        scriptManager.playerInfo.totalTime = ScriptManager.polePositionManager.totalTime;

        //To do: enviar el tiempo a los demás jugadores.

        scriptManager.playerInfo.hasEnded = true;
        ScriptManager.polePositionManager.isRaceEnded = true;
        ScriptManager.polePositionManager.managePlayersEnded();
        if (endRaceEvent != null)
            endRaceEvent();
    }
}
