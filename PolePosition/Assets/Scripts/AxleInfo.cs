using UnityEngine;

[System.Serializable]
public class AxleInfo
{
    public WheelCollider leftWheel; //colision rueda izquierda
    public WheelCollider rightWheel; //colision rueda derecha
    public bool motor; //motor
    public bool steering; //direccion
}