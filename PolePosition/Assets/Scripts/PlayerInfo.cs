using System.Collections;
using System.Collections.Generic;
using Mirror;
using UnityEngine;

public class PlayerInfo : MonoBehaviour
{
    //Delegado para la actualización de la posición en la interfaz.
    public delegate void OnPositionChangeDelegate(int newVal);

    public event OnPositionChangeDelegate OnPositionChangeEvent;

    public string Name { get; set; }

    public int ID { get; set; }

    //Variable de la posicion
    private int Position;
    //Variabla pensada para actualizar y leer Position. Cuando se actualiza la variable posición, se actualiza también su representación en la interfaz si el jugador es local.
    //Ya que solo el jugador local tendrá definido el evento, solo este actualizará la interfaz.
    public int CurrentPosition
    {
        get { return Position; }
        set
        {
            Position = value;
            if(OnPositionChangeEvent != null)
                OnPositionChangeEvent(value);
        }
    }

    private int lap;
    public int CurrentLap { get; set; }

    // Almacenamos el valor de la seleccion de color de cada jugador
    public int ModelCar { get; set; }

    //Booleano que indica si es el jugador local.
    public bool LocalPlayer { get; set; }

    public bool Ready { get; set; } //deberia ser syncvar

    public int CheckPoint { get; set; }

    public int checkpointCount { get; set; }

    public float totalTime { get; set; }

    public bool hasEnded { get; set; }


    public override string ToString()
    {
        return Name;
    }
}