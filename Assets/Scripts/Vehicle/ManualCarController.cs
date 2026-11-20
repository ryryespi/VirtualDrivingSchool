using System;
using UnityEngine;
using VirtualDrivingSchool.Input;

namespace VirtualDrivingSchool.Vehicle
{
    public enum DriveType
    {
        RearWheelDrive,
        FrontWheelDrive,
        AllWheelDrive
    }

    [System.Serializable]
    public class WheelAxle
    {
        public WheelCollider leftWheel;
        public WheelCollider rightWheel;
        public Transform leftVisualMesh;
        public Transform rightVisualMesh;
        public bool isMotor;
        public bool isSteering;
    }

    /// <summary>
    /// Master car controller managing wheel physics, engine torque distribution,
    /// braking, steering, and synchronization with the Logitech G920 controller.
    /// </summary>
    [RequireComponent(typeof(Rigidbody))]
    public class ManualCarController : MonoBehaviour
    {
        [Header("Subsystems")]
        public EngineSimulation engine;
        public Transmission transmission;
        public LogitechG920Manager input;

        [Header("Drivetrain Configuration")]
        public DriveType driveType = DriveType.RearWheelDrive;
        public WheelAxle frontAxle;
        public WheelAxle rearAxle;

        [Header("Brake Configuration")]
        public float maxFootBrakeTorque = 3000f;
        public float maxHandbrakeTorque = 5000f;
        [Range(0.5f, 0.85f)] public float frontBrakeBias = 0.70f;

        [Header("Vehicle Dynamics")]
        public Vector3 centerOfMassOffset = new Vector3(0f, -0.4f, 0.1f);
        public float downforceCoefficient = 40f;
        public float wheelBase = 2.6f; // Distance between front and rear axles (m)
        public float trackWidth = 1.6f; // Distance between left and right wheels (m)

        public Rigidbody Rb { get; private set; }
        public float SpeedKmh { get; private set; }
        public float SpeedMph { get; private set; }
        public float AverageDrivenWheelRPM { get; private set; }

        private Vector3 spawnPosition;
        private Quaternion spawnRotation;

        private void Awake()
        {
            Rb = GetComponent<Rigidbody>();
            Rb.centerOfMass = centerOfMassOffset;

            if (engine == null) engine = GetComponent<EngineSimulation>();
            if (transmission == null) transmission = GetComponent<Transmission>();
            if (input == null) input = LogitechG920Manager.Instance;

            spawnPosition = transform.position;
            spawnRotation = transform.rotation;
        }

        private void Start()
        {
            ConfigureWheelFriction(frontAxle.leftWheel);
            ConfigureWheelFriction(frontAxle.rightWheel);
            ConfigureWheelFriction(rearAxle.leftWheel);
            ConfigureWheelFriction(rearAxle.rightWheel);
        }

        private void Update()
        {
            // Sync visual meshes with WheelCollider physics poses
            UpdateWheelVisuals(frontAxle.leftWheel, frontAxle.leftVisualMesh);
            UpdateWheelVisuals(frontAxle.rightWheel, frontAxle.rightVisualMesh);
            UpdateWheelVisuals(rearAxle.leftWheel, rearAxle.leftVisualMesh);
            UpdateWheelVisuals(rearAxle.rightWheel, rearAxle.rightVisualMesh);

            // Speed calculation
            float forwardSpeed = Vector3.Dot(Rb.linearVelocity, transform.forward);
            SpeedKmh = forwardSpeed * 3.6f;
            SpeedMph = forwardSpeed * 2.23694f;

            if (input != null && input.ResetVehicle)
            {
                ResetToSpawn();
            }
        }

        private void FixedUpdate()
        {
            if (input == null) input = LogitechG920Manager.Instance;

            float steerInput = input != null ? input.Steering : 0f;
            float throttleInput = input != null ? input.Throttle : 0f;
            float brakeInput = input != null ? input.Brake : 0f;
            float clutchInput = input != null ? input.Clutch : 0f;
            bool handbrakeInput = input != null && input.Handbrake;
            int requestedGear = input != null ? input.SelectedGear : 0;
            bool ignitionKey = input != null && input.StarterIgnition;

            // Compute driven wheel RPM
            CalculateAverageWheelRPM();

            // 1. Process Transmission (clutch engagement, gear grind, gear change)
            transmission.ProcessTransmission(requestedGear, clutchInput, engine.currentRPM, AverageDrivenWheelRPM);

            // 2. Process Engine Simulation (RPM, ignition, stalls, torque)
            engine.ProcessEngine(throttleInput, transmission.clutchEngagement, transmission.driveShaftRPM, transmission.currentGear, ignitionKey);

            // 3. Calculate final wheel drive torque
            float outputTorque = transmission.CalculateWheelTorque(engine.currentTorque, engine.currentRPM);

            // 4. Apply Steering (Ackermann geometry)
            ApplyAckermannSteering(steerInput);

            // 5. Apply Motor & Brake Torques
            ApplyDriveTorque(outputTorque);
            ApplyBraking(brakeInput, handbrakeInput);

            // 6. Apply Aerodynamic Downforce
            ApplyDownforce();
        }

        private void ApplyAckermannSteering(float steerInput)
        {
            float maxAngle = input != null ? input.maxSteerAngle : 35f;
            float targetAngle = steerInput * maxAngle;

            if (Mathf.Approximately(steerInput, 0f))
            {
                frontAxle.leftWheel.steerAngle = 0f;
                frontAxle.rightWheel.steerAngle = 0f;
                return;
            }

            // Ackermann steering calculation: inner wheel turns tighter than outer wheel
            float radius = wheelBase / Mathf.Tan(Mathf.Abs(targetAngle) * Mathf.Deg2Rad);
            if (steerInput > 0f) // Turning Right
            {
                frontAxle.rightWheel.steerAngle = Mathf.Atan(wheelBase / (radius - (trackWidth / 2f))) * Mathf.Rad2Deg;
                frontAxle.leftWheel.steerAngle = Mathf.Atan(wheelBase / (radius + (trackWidth / 2f))) * Mathf.Rad2Deg;
            }
            else // Turning Left
            {
                frontAxle.leftWheel.steerAngle = -Mathf.Atan(wheelBase / (radius - (trackWidth / 2f))) * Mathf.Rad2Deg;
                frontAxle.rightWheel.steerAngle = -Mathf.Atan(wheelBase / (radius + (trackWidth / 2f))) * Mathf.Rad2Deg;
            }
        }

        private void ApplyDriveTorque(float totalTorque)
        {
            float frontShare = 0f;
            float rearShare = 0f;

            switch (driveType)
            {
                case DriveType.RearWheelDrive:
                    rearShare = 0.5f;
                    break;
                case DriveType.FrontWheelDrive:
                    frontShare = 0.5f;
                    break;
                case DriveType.AllWheelDrive:
                    frontShare = 0.25f;
                    rearShare = 0.25f;
                    break;
            }

            if (frontAxle.isMotor)
            {
                frontAxle.leftWheel.motorTorque = totalTorque * frontShare;
                frontAxle.rightWheel.motorTorque = totalTorque * frontShare;
            }
            if (rearAxle.isMotor)
            {
                rearAxle.leftWheel.motorTorque = totalTorque * rearShare;
                rearAxle.rightWheel.motorTorque = totalTorque * rearShare;
            }
        }

        private void ApplyBraking(float footBrake, bool handbrake)
        {
            float frontBrakeTorque = footBrake * maxFootBrakeTorque * frontBrakeBias;
            float rearBrakeTorque = footBrake * maxFootBrakeTorque * (1f - frontBrakeBias);

            if (handbrake)
            {
                rearBrakeTorque += maxHandbrakeTorque;
            }

            frontAxle.leftWheel.brakeTorque = frontBrakeTorque;
            frontAxle.rightWheel.brakeTorque = frontBrakeTorque;
            rearAxle.leftWheel.brakeTorque = rearBrakeTorque;
            rearAxle.rightWheel.brakeTorque = rearBrakeTorque;
        }

        private void CalculateAverageWheelRPM()
        {
            if (driveType == DriveType.RearWheelDrive)
            {
                AverageDrivenWheelRPM = (rearAxle.leftWheel.rpm + rearAxle.rightWheel.rpm) * 0.5f;
            }
            else if (driveType == DriveType.FrontWheelDrive)
            {
                AverageDrivenWheelRPM = (frontAxle.leftWheel.rpm + frontAxle.rightWheel.rpm) * 0.5f;
            }
            else
            {
                AverageDrivenWheelRPM = (frontAxle.leftWheel.rpm + frontAxle.rightWheel.rpm + rearAxle.leftWheel.rpm + rearAxle.rightWheel.rpm) * 0.25f;
            }
        }

        private void ApplyDownforce()
        {
            float speedSq = Rb.linearVelocity.sqrMagnitude;
            Rb.AddForce(-transform.up * downforceCoefficient * speedSq);
        }

        private void UpdateWheelVisuals(WheelCollider col, Transform visual)
        {
            if (col == null || visual == null) return;
            Vector3 pos;
            Quaternion rot;
            col.GetWorldPose(out pos, out rot);
            visual.position = pos;
            visual.rotation = rot;
        }

        private void ConfigureWheelFriction(WheelCollider col)
        {
            if (col == null) return;

            WheelFrictionCurve forward = col.forwardFriction;
            forward.extremumSlip = 0.2f;
            forward.extremumValue = 1.0f;
            forward.asymptoteSlip = 0.8f;
            forward.asymptoteValue = 0.7f;
            forward.stiffness = 1.8f;
            col.forwardFriction = forward;

            WheelFrictionCurve sideways = col.sidewaysFriction;
            sideways.extremumSlip = 0.25f;
            sideways.extremumValue = 1.0f;
            sideways.asymptoteSlip = 0.7f;
            sideways.asymptoteValue = 0.65f;
            sideways.stiffness = 1.6f;
            col.sidewaysFriction = sideways;
        }

        public void ResetToSpawn()
        {
            transform.position = spawnPosition + Vector3.up * 0.5f;
            transform.rotation = spawnRotation;
            Rb.linearVelocity = Vector3.zero;
            Rb.angularVelocity = Vector3.zero;
            engine.currentRPM = 0f;
            engine.state = EngineState.Off;
        }
    }
}
