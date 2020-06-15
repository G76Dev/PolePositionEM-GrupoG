using System;
using System.Collections;
using System.Collections.Generic;
using Mirror;
using UnityEngine;
using UnityEngine.UI;

public class UIManager : MonoBehaviour
{
    public bool showGUI = true;

    private NetworkManager m_NetworkManager;
    private PolePositionManager m_PolePositionManager;
    private MirrorManager m_mirrorManager;

    public String userName;//string donde almacenar el nombre del jugador que posteriormente pasaremos al playerInfo

    [Header("Main Menu")] [SerializeField] private GameObject mainMenu;
    [SerializeField] private Button buttonHost;
    [SerializeField] private Button buttonClient;
    [SerializeField] private Button buttonServer;
    [SerializeField] private InputField inputFieldIP;

    [SerializeField] private InputField nameField; //campo donde almacenar el nombre del jugador
    //igual que para poner la ip se puede aprovechar para añadir el nombre

    [Header("In-Game HUD")] [SerializeField]
    private GameObject inGameHUD;

    [SerializeField] private Button readyButton;
    [SerializeField] private Text textReady;
    [SerializeField] private Text textSpeed;
    [SerializeField] private Text textLaps;
    [SerializeField] private Text textPosition;
    [SerializeField] private Text textOrder;

    [Header("End Results HUD")]
    [SerializeField]
    private GameObject endResults;

    [SerializeField] private Text endText;


    //Delegate events
    public delegate void SyncStart();

    public event SyncStart PlayerReadyEvent;


    private void Awake()
    {
        m_NetworkManager = FindObjectOfType<NetworkManager>();
        m_PolePositionManager = FindObjectOfType<PolePositionManager>();
    }


    private void Start()
    {
        buttonHost.onClick.AddListener(() => StartHost());
        buttonClient.onClick.AddListener(() => StartClient());
        buttonServer.onClick.AddListener(() => StartServer());
        readyButton.onClick.AddListener(() => playerIsReady());
        ActivateMainMenu();

    }

    public void UpdateEndResults(string results)
    {
        endText.text = results;
    }

    public void UpdateSpeed(int speed)
    {
        textSpeed.text = "Speed " + speed + " Km/h";
    }

    public void UpdatePosition(int pos)
    {
        textPosition.text = "Position: " + pos;
    }

    public void UpdateLap(int lap, double currentTime, double totalTime, int totalLaps)
    {
        textLaps.text = "Current lap: " + lap + "/ " + totalLaps + "\n"; //Cambiar MAX LAPS por una variable que almacene el numero de vueltas a recorrer. 
        textLaps.text += "Time of this lap: " + "\n" +  currentTime + "\n";
        textLaps.text += "Total time: " + "\n" +  totalTime;
    }

    public void UpdateOrder(string newOrder)
    {
        textOrder.text = newOrder;
    }

    private void ActivateMainMenu()
    {
        mainMenu.SetActive(true);
        inGameHUD.SetActive(false);
        endResults.SetActive(false);
    }

    private void playerIsReady()
    {
        //Desactiva la interactividad del boton
        readyButton.interactable = false;
        //Cambia el alfa del botón para que sea visible el cambio
        Color newCol = readyButton.image.color;
        newCol.a = 0.5f;
        readyButton.image.color = newCol;
        //Cambia el alfa del texto para que sea visible el cambio
        newCol = textReady.color;
        newCol.a = 0.5f;
        textReady.color = newCol;

        if(PlayerReadyEvent != null)
        {
            PlayerReadyEvent();
        }
    }
    


    public void HideReadyButton()
    {
        readyButton.gameObject.SetActive(false);
    }

    private void ActivateInGameHUD()
    {
        mainMenu.SetActive(false);
        inGameHUD.SetActive(true);
    }

    public void endResultsHUD()
    {
        mainMenu.SetActive(false);
        inGameHUD.SetActive(false);
        endResults.SetActive(true);
    }

    private void StartHost()
    {
        //se almacena el nombre en este caso del host
        userName = nameField.text;
        m_NetworkManager.StartHost();
        ActivateInGameHUD();
     
    }

    private void StartClient()
    {
        //se almacena el nombre en este caso del cliene
        userName = nameField.text;
        m_NetworkManager.StartClient();
        m_NetworkManager.networkAddress = inputFieldIP.text;

        
        ActivateInGameHUD();
    }

    private void StartServer()
    {
        m_NetworkManager.StartServer();
        ActivateInGameHUD();
    }
}