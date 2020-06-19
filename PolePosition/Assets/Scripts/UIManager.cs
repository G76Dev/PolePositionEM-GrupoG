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

    /// <summary>
    /// Método llamado al iniciar la vuelta de clasificación, para que no se muestren más elementos de los necesarios en la interfaz.
    /// </summary>
    public void StartClasificationLap()
    {
        HideReadyButton();
        textPosition.gameObject.transform.parent.gameObject.SetActive(false);
        textOrder.gameObject.transform.parent.gameObject.SetActive(false);
    }

    /// <summary>
    /// Método que se llama al terminar la vuelta de clasificación, para mostrar los elementos de la interfaz necesarios.
    /// </summary>
    public void FinishClasificationLap()
    {
        readyButton.gameObject.SetActive(true);
        textPosition.gameObject.transform.parent.gameObject.SetActive(true);
        textOrder.gameObject.transform.parent.gameObject.SetActive(true);
    }


    /// <summary>
    /// Método llamado para actualizar el texto de los resultados de la carrera.
    /// </summary>
    /// <param name="results"></param>
    public void UpdateEndResults(string results)
    {
        endText.text = results;
    }
    /// <summary>
    /// Se actualiza uno de los textos, para indicar que la carrera ha terminado para todos los jugadores.
    /// </summary>
    public void UpdateStateResult()
    {
        stateText.text = "RACE ENDED. ALL PLAYERS MADE IT";
    }

    /// <summary>
    /// Actualiza el valor de la interfaz que muestra la velocidad del jugador.
    /// </summary>
    /// <param name="speed"></param>
    public void UpdateSpeed(int speed)
    {
        textSpeed.text = "Speed " + speed + " Km/h";
    }

    /// <summary>
    /// Actualiza el valor de la interfaz que muestra la posición del jugador actualmente.
    /// </summary>
    /// <param name="pos"></param>
    public void UpdatePosition(int pos)
    {
        textPosition.text = "Position: " + pos;
    }

    /// <summary>
    /// Actualiza la interfaz para mostrar la vuelta actual, el tiempo que el jugador lleva en esa vuelta, el tiempo total de la carrera, y el número total de vueltas necesarias.
    /// </summary>
    /// <param name="lap"></param>
    /// <param name="currentTime"></param>
    /// <param name="totalTime"></param>
    /// <param name="totalLaps"></param>
    public void UpdateLap(int lap, double currentTime, double totalTime, int totalLaps)
    {

        textLaps.text = "Current lap: " + lap + "/ " + totalLaps + "\n"; //Cambiar MAX LAPS por una variable que almacene el numero de vueltas a recorrer. 
        textLaps.text += "Time of this lap: " + "\n" + currentTime + "\n";
        textLaps.text += "Total time: " + "\n" + totalTime;
    }

    /// <summary>
    /// Método que actualiza en la interfaz el tiempo que el jugador lleva corriendo la vuelta de clasificación.
    /// </summary>
    /// <param name="currentTime"></param>
    public void UpdateClasLap(double currentTime)
    {
        textLaps.text = "Clasification Lap" + "\n";
        textLaps.text += "Time of the lap: " + "\n" + currentTime + "\n";
    }

    /// <summary>
    /// Método que muestra en la interfaz el orden de los jugadores en la carrera.
    /// </summary>
    /// <param name="newOrder"></param>
    public void UpdateOrder(string newOrder)
    {
        textOrder.text = newOrder;
    }

    /// <summary>
    /// Método que muestra el menú principal, y oculta los demás.
    /// </summary>
    public void ActivateMainMenu()
    {
        mainMenu.SetActive(true);
        inGameHUD.SetActive(false);
        endResults.SetActive(false);
    }

    /// <summary>
    /// Método que se ejecuta cuando el jugador pulsa el botón de ready.
    /// </summary>
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
    
    /// <summary>
    /// Activa o desactiva el aviso de que el coche no está bien apoyado en la carretera (podría volcar o ha volcado)
    /// </summary>
    /// <param name="newVal"></param>
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


    /// <summary>
    /// 
    /// </summary>
    public void addPlayAgainCounter()
    {
        playAgainCounter++;
        if (!playAgainWindow.activeSelf)
            playAgainWindow.SetActive(true);

    }

    /// <summary>
    /// 
    /// </summary>
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

    /// <summary>
    /// Oculta el botón de ready
    /// </summary>
    public void HideReadyButton()
    {
        readyButton.gameObject.SetActive(false);
    }

    /// <summary>
    /// Activa la interfaz del juego y desactiva el menú principal.
    /// </summary>
    private void ActivateInGameHUD()
    {
        mainMenu.SetActive(false);
        inGameHUD.SetActive(true);
    }

    /// <summary>
    /// Se activa el menú que muestra los resultados de la carrera, y se desactivan los demás.
    /// </summary>
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

        m_NetworkManager.networkAddress = (inputFieldIP.text != "") ? inputFieldIP.text : "localhost";
        m_NetworkManager.StartClient();
        

        
        ActivateInGameHUD();
    }

    private void StartServer()
    {
        m_NetworkManager.StartServer();
        ActivateInGameHUD();
    }
}