using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class CheckpointController : MonoBehaviour
{
    private PlayerInfo m_PlayerInfo;
    private PlayerController m_PlayerController;
    private PolePositionManager m_PoleManager;
    private UIManager m_UImanager;

    [HideInInspector] GameObject checkpointList;




    public delegate void changeLapDelegate();

    public event changeLapDelegate changeLapEvent;
    public event changeLapDelegate endRaceEvent;


    public void Awake()
    {
        m_PoleManager = FindObjectOfType<PolePositionManager>();
        m_PlayerController = GetComponent<PlayerController>();
        m_PlayerInfo = GetComponent<PlayerInfo>();
        m_UImanager = FindObjectOfType<UIManager>();
        m_PlayerInfo.checkpointCount = 0;
    }

    // Start is called before the first frame update
    void Start()
    {
        checkpointList = m_PoleManager.checkPointList;
        //print("HIJOS: " + checkpointList.transform.childCount);
    }

    //Este método se encarga de dectecar y gestionar las colisiones con los checkpoints, actualizando el progreso del jugador en la carrera.
    void OnTriggerEnter(Collider col)
    {
        //Solo se comprobará el checkpoint si el objeto collider tiene el tag adecuado
        if (col.tag == "Checkpoint")
        {
            int nextCheckPoint = (m_PlayerInfo.CheckPoint + 1) % checkpointList.transform.childCount;

            if (int.Parse(col.name) == nextCheckPoint)
            {
                //print("CHECKPOINT " + nextCheckPoint + " ALCANZADO");
                m_PlayerInfo.checkpointCount++;

                if (m_PlayerInfo.checkpointCount == checkpointList.transform.childCount)
                {
                    if (!m_PoleManager.reconocimiento)
                    {
                        m_PlayerInfo.CurrentLap++;
                    }
                    else
                    {
                        if (m_PlayerInfo.LocalPlayer)
                        {
                            m_PoleManager.reconocimiento = false;
                            m_PoleManager.UpdateServerReconTime(m_PlayerInfo.ID);
                            m_UImanager.FinishClasificationLap();
                        }                       
                    }
                    

                    if(m_PlayerInfo.CurrentLap >= m_PoleManager.totalLaps)
                    {
                        EndRace();

                        return;
                    }

                    m_PlayerInfo.checkpointCount = 0;

                    if (changeLapEvent != null)
                        changeLapEvent();
                    //print("CAMBIO DE VUELTA WEY");
                }
                m_PlayerInfo.CheckPoint = nextCheckPoint;
            }

        }
    }

    public void EndClasificactionLap()
    {
        m_PoleManager.reconocimiento = false;
        m_PoleManager.UpdateServerReconTime(m_PlayerInfo.ID);
    }

    public void EndRace()
    {
        m_PlayerController.canMove = false; //El jugador que haya superado la carrera se dejará de mover.
                                            //To do: teletransportar al podio



        m_PlayerInfo.totalTime = m_PoleManager.totalTime;
        //To do: enviar el tiempo a los demás jugadores.


        m_PlayerInfo.hasEnded = true;
        m_PoleManager.isRaceEnded = true;

        m_PoleManager.managePlayersEnded();
        m_PoleManager.updatePodium();

        if (endRaceEvent != null)
            endRaceEvent();
    }


    // Update is called once per frame
    void Update()
    {

    }
}
