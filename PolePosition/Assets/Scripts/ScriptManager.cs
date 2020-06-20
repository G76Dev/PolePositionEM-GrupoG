using Mirror;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class ScriptManager : NetworkBehaviour
{
    public SetupPlayer setupPlayer;
    public PlayerInfo playerInfo;
    public PlayerController playerController;
    public MirrorManager mirrorManager;
    public CheckpointController checkPointController;
    static public PolePositionManager polePositionManager;
    static public NetworkController networkController;
    static public UIManager UIManager;

    private void Awake()
    {
        setupPlayer = GetComponent<SetupPlayer>();
        playerInfo = GetComponent<PlayerInfo>();
        playerController = GetComponent<PlayerController>();
        mirrorManager = GetComponent<MirrorManager>();
        checkPointController = GetComponent<CheckpointController>();


        //Si esto da problemas, llevarlo al Start
        //polePositionManager = FindObjectOfType<PolePositionManager>();
        //networkController = FindObjectOfType<NetworkController>();
        //UIManager = FindObjectOfType<UIManager>();



        print("ScriptManager SETUP");
    }



    void Start()
    {
        if (isLocalPlayer)
        {
            ScriptManager.polePositionManager.scriptManager = this;
            ScriptManager.UIManager.scriptManager = this;
            ScriptManager.networkController.scriptManager = this;
            //polePositionManager.scriptManager = this;
            //UIManager.scriptManager = this;
            //networkController.scriptManager = this;
        }
    }

}
