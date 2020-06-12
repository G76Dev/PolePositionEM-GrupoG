using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class CheckpointController : MonoBehaviour
{
    private PlayerInfo m_PlayerInfo;
    private PolePositionManager m_PoleManager;
    [HideInInspector] GameObject checkpointList;



    public delegate void changeLapDelegate();

    public event changeLapDelegate changeLapEvent;
    public event changeLapDelegate endRaceEvent;


    public void Awake()
    {
        m_PoleManager = FindObjectOfType<PolePositionManager>();
        m_PlayerInfo = GetComponent<PlayerInfo>();
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
                    m_PlayerInfo.CurrentLap++;

                    if(m_PlayerInfo.CurrentLap >= m_PoleManager.totalLaps)
                    {
                        if (endRaceEvent != null)
                            endRaceEvent();

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


    // Update is called once per frame
    void Update()
    {
        
    }
}
