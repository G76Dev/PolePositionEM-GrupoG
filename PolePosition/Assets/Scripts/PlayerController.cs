using System;
using System.Collections.Generic;
using UnityEngine;
using Mirror;
using System.Collections;

/*
	Documentation: https://mirror-networking.com/docs/Guides/NetworkBehaviour.html
	API Reference: https://mirror-networking.com/docs/api/Mirror.NetworkBehaviour.html
*/

public class PlayerController : NetworkBehaviour
{
    #region Variables

    [Header("Movement")] public List<AxleInfo> axleInfos;
    //velocidades del motor
    public float forwardMotorTorque = 100000;
    public float backwardMotorTorque = 50000;
    //angulo maximo de direccion
    public float maxSteeringAngle = 15;
    //maximo valo de frenado
    public float engineBrake = 1e+12f;
    public float footBrake = 1e+24f;
    //velocidad maxima
    public float topSpeed = 200f;
    public float downForce = 100f;
    public float slipLimit = 0.2f;

    private bool cheatRecon = true;
    private bool cheatRace = true;
    public bool localMove = true;

    public float timerCrash = 0;

    //En teoria hay que quitar el syncvar
    [SyncVar] public bool canMove = false; //Decide si el jugador puede moverse
    //Comienza a false a la espera de que RpcStart le permita moverse

    //


    public float crashTimer = 0;

    private float CurrentRotation { get; set; }
    private float InputAcceleration { get; set; }
    private float InputSteering { get; set; }
    private float InputBrake { get; set; }

    private PlayerInfo m_PlayerInfo;
    public MirrorManager m_Mirror;
    private PolePositionManager m_PoleManager;
    private CheckpointController m_CheckPointController;

    private WheelFrictionCurve friction;

    private Rigidbody m_Rigidbody;
    private float m_SteerHelper = 0.8f;


    private float m_CurrentSpeed = 0;

    //velocidad
    private float Speed
    {
        get { return m_CurrentSpeed; }
        set
        {
            if (Math.Abs(m_CurrentSpeed - value) < float.Epsilon) return;
            m_CurrentSpeed = value;
            if (OnSpeedChangeEvent != null)
                OnSpeedChangeEvent(m_CurrentSpeed);
        }
    }

    public delegate void OnSpeedChangeDelegate(float newVal);

    public event OnSpeedChangeDelegate OnSpeedChangeEvent;

    #endregion Variables

    #region Unity Callbacks

    public void Awake()
    {
        m_Rigidbody = GetComponent<Rigidbody>();
        m_PoleManager = FindObjectOfType<PolePositionManager>();
        m_CheckPointController = GetComponent<CheckpointController>();

        //Esta variable no se usa de momento
        m_PlayerInfo = GetComponent<PlayerInfo>();
        m_PlayerInfo.checkpointCount = 0;
        //control de friccion
        friction = axleInfos[0].rightWheel.sidewaysFriction;
        friction.extremumSlip = 0.3f;
    }

    private void Start()
    {

    }

    public void Update()
    {
        //print(canMove);
        //print(crashTimer);

        //¿Espera activa?
        //Activar o desactivar el playerController por eventos en lugar de comprobar booleanos.
        if (canMove || localMove)
        {
            InputAcceleration = Input.GetAxis("Vertical");
            InputSteering = Input.GetAxis(("Horizontal"));
            InputBrake = Input.GetAxis("Jump");
            Speed = m_Rigidbody.velocity.magnitude;
            timerCrash += Time.deltaTime;

        }
        else
        {
            Speed = 0;
            m_Rigidbody.velocity = Vector3.zero;
            InputAcceleration = 0;
            InputSteering = 0;
            InputBrake = 0;
        }

        //CHEATS
        if (Input.GetKeyDown(KeyCode.G) && cheatRecon)
        {
            m_CheckPointController.EndClasificactionLap();
            cheatRecon = false;
        }

        if (Input.GetKeyDown(KeyCode.H) && cheatRace)
        {
            m_CheckPointController.EndRace();
            cheatRace = false;
        }

    }

    public void FixedUpdate()
    {
        InputSteering = Mathf.Clamp(InputSteering, -1, 1);
        InputAcceleration = Mathf.Clamp(InputAcceleration, -1, 1);
        InputBrake = Mathf.Clamp(InputBrake, 0, 1);

        float steering = maxSteeringAngle * InputSteering;
        if (m_Rigidbody.velocity.magnitude < 0.3)
        {
            friction.extremumSlip = 0.3f;
            if(axleInfos[0].leftWheel.sidewaysFriction.extremumSlip != 0.3)
            {
                m_Mirror.CmdFric(m_PlayerInfo.ID, 0.3f);
            }
        }
        else
        {
            friction.extremumSlip = 0.2f;
            if (axleInfos[0].leftWheel.sidewaysFriction.extremumSlip != 0.2)
            {
                m_Mirror.CmdFric(m_PlayerInfo.ID, 0.2f);
            }
        }

        foreach (AxleInfo axleInfo in axleInfos)
        {
            
            if (axleInfo.steering)
            {
                axleInfo.leftWheel.steerAngle = steering;
                axleInfo.rightWheel.steerAngle = steering;
            }

            if (axleInfo.motor)
            {
                if (InputAcceleration > float.Epsilon)
                {
                    axleInfo.leftWheel.motorTorque = forwardMotorTorque;
                    axleInfo.leftWheel.brakeTorque = 0;
                    axleInfo.rightWheel.motorTorque = forwardMotorTorque;
                    axleInfo.rightWheel.brakeTorque = 0;
                }

                if (InputAcceleration < -float.Epsilon)
                {
                    axleInfo.leftWheel.motorTorque = -backwardMotorTorque;
                    axleInfo.leftWheel.brakeTorque = 0;
                    axleInfo.rightWheel.motorTorque = -backwardMotorTorque;
                    axleInfo.rightWheel.brakeTorque = 0;
                }

                if (Math.Abs(InputAcceleration) < float.Epsilon)
                {
                    axleInfo.leftWheel.motorTorque = 0;
                    axleInfo.leftWheel.brakeTorque = engineBrake;
                    axleInfo.rightWheel.motorTorque = 0;
                    axleInfo.rightWheel.brakeTorque = engineBrake;
                }

                if (InputBrake > 0)
                {
                    axleInfo.leftWheel.brakeTorque = footBrake;
                    axleInfo.rightWheel.brakeTorque = footBrake;
                }
            }
            axleInfo.rightWheel.sidewaysFriction = friction;

            axleInfo.leftWheel.sidewaysFriction = friction;
            ApplyLocalPositionToVisuals(axleInfo.leftWheel);
            ApplyLocalPositionToVisuals(axleInfo.rightWheel);
        }
        SteerHelper();
        SpeedLimiter();
        AddDownForce();
        //TractionControl();
    }

    #endregion

    #region Methods

    // crude traction control that reduces the power to wheel if the car is wheel spinning too much
    private void TractionControl()
    {
        foreach (var axleInfo in axleInfos)
        {
            WheelHit wheelHitLeft;
            WheelHit wheelHitRight;
            axleInfo.leftWheel.GetGroundHit(out wheelHitLeft);
            axleInfo.rightWheel.GetGroundHit(out wheelHitRight);

            if (wheelHitLeft.forwardSlip >= slipLimit)
            {
                var howMuchSlip = (wheelHitLeft.forwardSlip - slipLimit) / (1 - slipLimit);
                axleInfo.leftWheel.motorTorque -= axleInfo.leftWheel.motorTorque * howMuchSlip * slipLimit;
            }

            if (wheelHitRight.forwardSlip >= slipLimit)
            {
                var howMuchSlip = (wheelHitRight.forwardSlip - slipLimit) / (1 - slipLimit);
                axleInfo.rightWheel.motorTorque -= axleInfo.rightWheel.motorTorque * howMuchSlip * slipLimit;
            }

        }
    }

    // this is used to add more grip in relation to speed
    private void AddDownForce()
    {
        foreach (var axleInfo in axleInfos)
        {
            axleInfo.leftWheel.attachedRigidbody.AddForce(
                -transform.up * (downForce * axleInfo.leftWheel.attachedRigidbody.velocity.magnitude));
        }
    }

    private void SpeedLimiter()
    {
        float speed = m_Rigidbody.velocity.magnitude;
        if (speed > topSpeed)
            m_Rigidbody.velocity = topSpeed * m_Rigidbody.velocity.normalized;
    }

    // finds the corresponding visual wheel
    // correctly applies the transform
    public void ApplyLocalPositionToVisuals(WheelCollider col)
    {
        if (col.transform.childCount == 0)
        {
            return;
        }

        Transform visualWheel = col.transform.GetChild(0);
        Vector3 position;
        Quaternion rotation;
        col.GetWorldPose(out position, out rotation);
        var myTransform = visualWheel.transform;
        myTransform.position = position;
        myTransform.rotation = rotation;
    }


    private void SteerHelper()
    {
        int cont;
        foreach (var axleInfo in axleInfos)
        {
            WheelHit[] wheelHit = new WheelHit[2];
            axleInfo.leftWheel.GetGroundHit(out wheelHit[0]);
            axleInfo.rightWheel.GetGroundHit(out wheelHit[1]);
            cont = 0;
            foreach (var wh in wheelHit)
            {
                if (wh.normal == Vector3.zero) //Este if detecta cuando el coche se ha chocado y no puede seguir avanzando
                {
                    if(!m_PlayerInfo.hasEnded && timerCrash >= 0.25)
                        m_PoleManager.crashed = true;

                    //To Do: Activar señal grafica que indique que el coche se ha ahostiado
                    //print("ME HE AHOSTIADO");

                    if (Input.GetAxis("ResetCar") > 0)
                    {
                        m_PoleManager.crashed = false;
                        int segIdx;
                        float carDist;
                        Vector3 newPosition;
                        m_PoleManager.m_CircuitController.ComputeClosestPointArcLength(transform.position, out segIdx, out newPosition, out carDist); ;
                        transform.position = newPosition;

                        print("checkcount" + m_PlayerInfo.checkpointCount);
                        switch (m_PlayerInfo.checkpointCount)
                        {
                            case 1:
                                transform.rotation = Quaternion.Euler(0, 180, 0);
                                transform.position = m_PoleManager.checkPointList.transform.GetChild(1).position;
                                break;
                            case 2:
                                transform.rotation = Quaternion.Euler(0, 90, 0);
                                transform.position = m_PoleManager.checkPointList.transform.GetChild(2).position;
                                break;
                            case 3:
                                transform.rotation = Quaternion.Euler(0, 0, 0);
                                transform.position = m_PoleManager.checkPointList.transform.GetChild(3).position;
                                break;
                            case 4:
                                transform.rotation = Quaternion.Euler(0, -90, 0);
                                transform.position = m_PoleManager.checkPointList.transform.GetChild(4).position;
                                break;
                            default:
                                transform.rotation = Quaternion.Euler(0, -90, 0);
                                transform.position = m_PoleManager.checkPointList.transform.GetChild(0).position;
                                break;
                        }
                        

                        if (canMove)
                        {
                            localMove = false;
                            canMove = false; //Esto existe para crear una penalizacion de tiempo por darle la vuelta al coche, pero de momento funciona un poco mal
                            StartCoroutine("RestartMovementRace", 1);
                        }
                        else
                        {
                            localMove = false; //Esto existe para crear una penalizacion de tiempo por darle la vuelta al coche, pero de momento funciona un poco mal
                            StartCoroutine("RestartMovementRecon", 1);
                        }
                    }

                    return; // wheels arent on the ground so dont realign the rigidbody velocity
                }
                else
                {
                    cont++;
                    m_PoleManager.crashed = false;
                    if (cont >= 2)
                        timerCrash = 0;
                }
            }

        }

        // this if is needed to avoid gimbal lock problems that will make the car suddenly shift direction
        if (Mathf.Abs(CurrentRotation - transform.eulerAngles.y) < 10f)
        {
            var turnAdjust = (transform.eulerAngles.y - CurrentRotation) * m_SteerHelper;
            Quaternion velRotation = Quaternion.AngleAxis(turnAdjust, Vector3.up);
            m_Rigidbody.velocity = velRotation * m_Rigidbody.velocity;
        }

        CurrentRotation = transform.eulerAngles.y;
    }

    IEnumerator RestartMovementRace(float sec)
    {
        //print("Corutineando");
        m_PoleManager.crashed = false;
        yield return new WaitForSeconds(sec);
        m_PoleManager.crashed = false;
        canMove = true;
        localMove = true;
    }
    IEnumerator RestartMovementRecon(float sec)
    {
        //print("Corutineando");
        yield return new WaitForSeconds(sec);
        m_PoleManager.crashed = false;
        localMove = true;
    }

    #endregion
}