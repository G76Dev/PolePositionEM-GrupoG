using System;
using System.Collections;
using System.Collections.Generic;
using System.Text;
using Mirror;
using Mirror.Examples.Basic;
using UnityEngine;

public class PolePositionManager : NetworkBehaviour
{
    public int numPlayers;//numero de jugadores
    public int playersReady;
    public NetworkManager networkManager;//controlador de la conexion

    float[] arcAux;

    private readonly List<PlayerInfo> m_Players = new List<PlayerInfo>(4);
    private CircuitController m_CircuitController;//controlador del circuito
    private GameObject[] m_DebuggingSpheres;//esfera para uso en el debug

    private float tempTime = 0;
    private float totalTime = 0;

    //Delegado para la actualización de la posición en la interfaz.
    public delegate void OnPositionChangeDelegate(int newVal);

    public event OnPositionChangeDelegate OnPositionChangeEvent;

    //Delegado para la actualización de las vueltas y el tiempo en la interfaz.
    public delegate void OnLapChangeDelegate(int newVal, int newVal2, int newVal3);

    public event OnLapChangeDelegate OnLapChangeEvent;

    private void Awake()
    {
        if (networkManager == null) networkManager = FindObjectOfType<NetworkManager>();//duda
        if (m_CircuitController == null) m_CircuitController = FindObjectOfType<CircuitController>();//duda



        m_DebuggingSpheres = new GameObject[networkManager.maxConnections];
        for (int i = 0; i < networkManager.maxConnections; ++i)
        {
            m_DebuggingSpheres[i] = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            m_DebuggingSpheres[i].GetComponent<SphereCollider>().enabled = false;
        }
    }

    private void Update()
    {
        if (m_Players.Count == 0)
            return;

        //Este if comprueba que todos los jugadores esten listos. Si no lo estan, deshace la cuenta y en el siguiente frame vuelve a comprobar si estan todos.
        //Cuando ya esten todos listos, se ignora la comprobacion y se comienza la carrera.
        //Esto probablemente no deba ir en el update y de hecho no es más que pseudocodigo
        if(playersReady < numPlayers)
        {
            foreach(var player in m_Players)
            {
                if (player.Ready)
                    playersReady++;
            }

            if(playersReady < numPlayers)
            {
                playersReady = 0;
            } 
            else
            {
                return;
            }
        } else
        {
            UpdateRaceProgress();
        }





        //print("vuelta  " + m_Players[0].CurrentLap); //Hay que hacer que cambie de vuelta

    }

    //añade un jugador
    public void AddPlayer(PlayerInfo player)
    {
        m_Players.Add(player);
        arcAux = new float[m_Players.Count];
    }

    private class PlayerInfoComparer : Comparer<PlayerInfo>
    {
        float[] m_ArcLengths;
        List<PlayerInfo> players;

        public PlayerInfoComparer(float[] arcLengths, List<PlayerInfo> par_players)
        {
            m_ArcLengths = arcLengths;
            players = par_players;
        }

        public override int Compare(PlayerInfo x, PlayerInfo y)
        {
            if (this.m_ArcLengths[GetIndex(x)] < m_ArcLengths[GetIndex(y)])
                return 1;
            else return -1;
        }

        //Método que recibe un player info, y devuelve su índice en la lista de playerinfos.
        //Esto es importante porque la posición de cada player info varía en cada iteración si un jugador adelanta a otro, por lo que usar el id para saber la posición en la lista
        //de un player info como se hacía al principio terminaría dando errores y no detectando bien quien va delante de quien.
        public int GetIndex(PlayerInfo pi)
        {
            int index = -1;
            for (int i = 0; i < players.Count; i++)
            {
                if (pi.Equals(players[i]))
                {
                    index = i;
                }
            }
            return index;
        }
    }

    public void UpdateRaceProgress()
    {
        // Update car arc-lengths
        float[] arcLengths = new float[m_Players.Count]; //Es MUY ineficiente que se declare un nuevo array en cada frame

        for (int i = 0; i < m_Players.Count; ++i)
        {
            //Si el jugador es local, se actualizan los tiempos.
            if (m_Players[i].LocalPlayer)
            {
                tempTime += Time.deltaTime;
                totalTime += Time.deltaTime;
            }

            arcLengths[i] = ComputeCarArcLength(i);
            //print("ORIGINAL: " + i + " " +  arcLengths[i]);
            if (m_Players[i].LocalPlayer && OnLapChangeEvent != null)
            {
                OnLapChangeEvent(m_Players[i].CurrentLap, (int)tempTime, (int)(totalTime));
            }

            //Cuando la diferencia entre la posicion anterior y la nueva sea muy grande...
            if ((Math.Abs(arcLengths[i]) - Math.Abs(arcAux[i])) > 300) //Necesita mejoras (a veces cuenta dos vueltas, y si aumentas mucho el valor, a veces no la cuenta), pero por ahora funciona
            {
                print("Nuevo lap, vamos por la: " + m_Players[i].CurrentLap + 1);
                m_Players[i].CurrentLap++; //Aumenta la vuelta (o eso se supone, porque a mi (Nacho) esta cosa no se me activa nunca salvo la primera pasada)
                //Posible implementacion con un collider.

                //Al cambiar de vuelta el tiempo de la vuelta se reinicia.
                tempTime = 0;              
                //To Do: Evitar el cheese de dar vuelta atras en la salida y atravesar la meta
            } 
            else
            {
                if ((Math.Abs(arcLengths[i]) - Math.Abs(arcAux[i])) > 0.01) //Intentar hacerlo sin valores absolutos (mas eficiente)
                {
                    print("El jugador " + m_Players[i].ID + " va hacia atrás");
                }
            }
            //print((Math.Abs(arcLengths[i]) - Math.Abs(arcAux[i])));
            arcAux[i] = arcLengths[i];
        }

        m_Players.Sort(new PlayerInfoComparer(arcLengths, m_Players));

        string myRaceOrder = "";
        int cont = 1;
        foreach (var _player in m_Players)
        {
            if (_player.CurrentPosition != cont)
            {
                _player.CurrentPosition = cont;
                if (_player.LocalPlayer && OnPositionChangeEvent != null)
                {
                    OnPositionChangeEvent((_player.CurrentPosition));
                }
            }
            myRaceOrder += _player.Name + " ";         

            cont++;
        }

        //Con esto se llamaría al evento para actualizar la posición. Falta saber quien es el jugador local para poner su posicion en lugar de la de otro
        //if(OnPositionChangeEvent != null)
        //{
            
        //}


        Debug.Log("El orden de carrera es: " + myRaceOrder + "\n" );
    }

    float ComputeCarArcLength(int ID)
    {
        // Compute the projection of the car position to the closest circuit 
        // path segment and accumulate the arc-length along of the car along
        // the circuit.
        Vector3 carPos = this.m_Players[ID].transform.position;

        int segIdx;
        float carDist;
        Vector3 carProj;

        float minArcL =
            this.m_CircuitController.ComputeClosestPointArcLength(carPos, out segIdx, out carProj, out carDist);

        this.m_DebuggingSpheres[ID].transform.position = carProj;

        if (this.m_Players[ID].CurrentLap == 0)
        {
            minArcL -= m_CircuitController.CircuitLength;
        }
        else
        {
            minArcL += m_CircuitController.CircuitLength *
                       (m_Players[ID].CurrentLap - 1);
        }

        //print(minArcL);

        return minArcL;
    }
}