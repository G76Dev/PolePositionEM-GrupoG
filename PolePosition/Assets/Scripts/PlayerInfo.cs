using System.Collections;
using System.Collections.Generic;
using Mirror;
using UnityEngine;

public class PlayerInfo : MonoBehaviour
{
    public string Name { get; set; }

    public int ID { get; set; }

    public int CurrentPosition { get; set; }

    public int CurrentLap { get; set; }

    // Almacenamos el valor de la seleccion de color de cada jugador
    public int ModelCar { get; set; }

    //Booleano que indica si es el jugador local.
    public bool LocalPlayer { get; set; }

    public override string ToString()
    {
        return Name;
    }
}