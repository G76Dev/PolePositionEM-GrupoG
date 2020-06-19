using System;
using System.Collections;
using System.Collections.Generic;
using Mirror;
using UnityEngine;
using UnityEngine.UI;

public class UIManager : NetworkBehaviour
{
    public bool showGUI = true;

    private NetworkManager m_NetworkManager;
    //private NetworkController m_NetworkController;
    private PolePositionManager m_PolePositionManager;
    public MirrorManager m_mirrorManager;
    [SyncVar] int playAgainCounter;

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

    [SerializeField] private Text textCrash;
    [SerializeField] private Text textMarchaAtras;

    [Header("End Results HUD")]
    [SerializeField]
    private GameObject endResults;

    [SerializeField] private Text endText;
    [SerializeField] private Text stateText;
    [SerializeField] private Button playAgainButton;
    [SerializeField] private Button returnToMenuButton;
    [SerializeField] private Text playAgainText;
    [SerializeField] private GameObject playAgainWindow;


    //Delegate events
    public delegate void SyncStart();

    public event SyncStart PlayerReadyEvent;

    public delegate void RestartRace();

    public event RestartRace RestartRaceEvent;

    public delegate void PlayerDisconnect();

    public event PlayerDisconnect ReturnToMainMenuEvent;


    private void Awake()
    {
        m_NetworkManager = FindObjectOfType<NetworkManager>();
        //m_NetworkController = FindObjectOfType<NetworkController>();
        m_PolePositionManager = FindObjectOfType<PolePositionManager>();
    }


    private void Start()
    {
        buttonHost.onClick.AddListener(() => StartHost());
        buttonClient.onClick.AddListener(() => StartClient());
        buttonServer.onClick.AddListener(() => StartServer());
        readyButton.onClick.AddListener(() => playerIsReady());
        returnToMenuButton.onClick.AddListener(() => returnToMainMenu());
        playAgainButton.onClick.AddListener(() =>
        {
            m_mirrorManager.CmdPlayAgain();
            playAgainButton.interactable = false;
        });
        ActivateMainMenu();
        textCrash.transform.parent.gameObject.SetActive(false);
        textMarchaAtras.transform.parent.gameObject.SetActive(false);
    }

    public void StartClasificationLap()
    {
        HideReadyButton();
        textPosition.gameObject.transform.parent.gameObject.SetActive(false);
        textOrder.gameObject.transform.parent.gameObject.SetActive(false);
    }
    public void FinishClasificationLap()
    {
        readyButton.gameObject.SetActive(true);
        textPosition.gameObject.transform.parent.gameObject.SetActive(true);
        textOrder.gameObject.transform.parent.gameObject.SetActive(true);
    }

    public void UpdateEndResults(string results)
    {
        endText.text = results;
    }

    public void UpdateStateResult()
    {
        stateText.text = "RACE ENDED. ALL PLAYERS MADE IT";
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
        textLaps.text += "Time of this lap: " + "\n" + currentTime + "\n";
        textLaps.text += "Total time: " + "\n" + totalTime;
    }

    public void UpdateClasLap(double currentTime)
    {
        textLaps.text = "Clasification Lap" + "\n";
        textLaps.text += "Time of the lap: " + "\n" + currentTime + "\n";
    }

    public void UpdateOrder(string newOrder)
    {
        textOrder.text = newOrder;
    }

    public void ActivateMainMenu()
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
    
    public void alternateCrash(bool newVal)
    {
        textCrash.transform.parent.gameObject.SetActive(newVal);
    }
    /// <summary>
    /// Suscrito al evento AllPlayersEndedEvent, habilita el botón de "play again" solamente cuando todos los demás jugadores hayan terminado la presente carrera.
    /// </summary>
    public void canPlayAgain()
    {
        //Activa el botón en la interfaz
        playAgainButton.interactable = true;

    }

    public void alternateMarchaAtras(bool newVal)
    {
        textMarchaAtras.transform.parent.gameObject.SetActive(newVal);
    }
    /// <summary>
    /// Se encarga de gestionar la cantidad de jugadores que quieren jugar otra partida en todos los clientes.
    /// Esto implica activar la ventana correspondiente, actualizar el número de jugadores que quieren jugar otra vez, y si estos son mayoría,
    /// lanza el evento que reinicia la partida completa.
    /// </summary>
    public void playAgain()
    {

        playAgainText.text = playAgainCounter + " / " + m_PolePositionManager.numPlayers +  " PLAYERS " + "\n WANT TO PLAY AGAIN";

        if(playAgainCounter > m_PolePositionManager.numPlayers/2)
        {
            print("COMENZANDO DE NUEVO LA CARRERA");
            if (RestartRaceEvent != null)
                RestartRaceEvent();
        }

    }

    public void addPlayAgainCounter()
    {
        playAgainCounter++;
        if (!playAgainWindow.activeSelf)
            playAgainWindow.SetActive(true);

    }

    private void returnToMainMenu()
    {
        ActivateMainMenu();

        //To Do: llamar a una funcion que se encargue de gestionar la desconexión en caso de que sea cliente y en caso de que sea servidor
        if(ReturnToMainMenuEvent != null)
        {
            ReturnToMainMenuEvent();
        }

        if (m_mirrorManager.isClient)
        {
            m_PolePositionManager.QuitPlayer(m_PolePositionManager.setupPlayer.m_PlayerInfo);
            m_NetworkManager.StopClient();
        } 
        else
        {
            m_NetworkManager.StopServer();
        }

        //m_NetworkManager.StopClient();
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