using System;
using UnityEngine;

namespace VirtualDrivingSchool.Vehicle
{
    public enum EngineState
    {
        Off,
        Cranking,
        Running,
        Stalled
    }

    /// <summary>
    /// Simulates internal combustion engine physics: RPM inertia, torque curves,
    /// starter motor ignition, rev limiter, engine braking, and realistic manual stall.
    /// </summary>
    public class EngineSimulation : MonoBehaviour
    {
        [Header("Engine Parameters")]
        public float idleRPM = 800f;
        public float maxRPM = 6800f;
        public float revLimiterRPM = 6500f;
        public float stallRPM = 450f;
        public float peakTorque = 240f; // Nm
        public float peakTorqueRPM = 4000f;
        public float flywheelInertia = 0.22f; // Flywheel mass resistance
        public float starterCrankDuration = 0.5f;

        [Header("Current Engine State")]
        public EngineState state = EngineState.Off;
        public float currentRPM = 0f;
        public float currentTorque = 0f;
        public bool isStalled => state == EngineState.Stalled;
        public bool isRunning => state == EngineState.Running;

        // Events
        public event Action OnEngineStarted;
        public event Action OnEngineStalled;
        public event Action OnEngineCrank;

        private float crankTimer = 0f;
        private float revLimiterCutTimer = 0f;

        private void Start()
        {
            currentRPM = 0f;
            state = EngineState.Off;
        }

        public void ProcessEngine(float throttleInput, float clutchEngagement, float driveShaftRPM, int currentGear, bool ignitionKey)
        {
            switch (state)
            {
                case EngineState.Off:
                case EngineState.Stalled:
                    HandleIgnition(ignitionKey, clutchEngagement, currentGear);
                    currentRPM = Mathf.MoveTowards(currentRPM, 0f, Time.deltaTime * 600f);
                    currentTorque = 0f;
                    break;

                case EngineState.Cranking:
                    crankTimer += Time.deltaTime;
                    currentRPM = Mathf.Lerp(150f, idleRPM * 1.1f, crankTimer / starterCrankDuration);
                    if (crankTimer >= starterCrankDuration)
                    {
                        state = EngineState.Running;
                        currentRPM = idleRPM;
                        OnEngineStarted?.Invoke();
                    }
                    currentTorque = 0f;
                    break;

                case EngineState.Running:
                    UpdateRunningEngine(throttleInput, clutchEngagement, driveShaftRPM, currentGear);
                    break;
            }
        }

        private void HandleIgnition(bool ignitionKey, float clutchEngagement, int currentGear)
        {
            // Safety requirement: to start a manual car, clutch must be depressed OR gear in Neutral!
            bool safeToStart = (clutchEngagement < 0.2f) || (currentGear == 0);

            if (ignitionKey && safeToStart)
            {
                if (state != EngineState.Cranking)
                {
                    state = EngineState.Cranking;
                    crankTimer = 0f;
                    OnEngineCrank?.Invoke();
                }
            }
            else
            {
                if (state == EngineState.Cranking)
                {
                    state = EngineState.Off;
                }
            }
        }

        private void UpdateRunningEngine(float throttleInput, float clutchEngagement, float driveShaftRPM, int currentGear)
        {
            // Rev limiter handling
            if (currentRPM >= revLimiterRPM)
            {
                revLimiterCutTimer = 0.08f;
            }
            bool fuelCut = false;
            if (revLimiterCutTimer > 0f)
            {
                revLimiterCutTimer -= Time.deltaTime;
                fuelCut = true;
            }

            // Target free-rev RPM based on throttle
            float targetFreeRPM = Mathf.Lerp(idleRPM, maxRPM, fuelCut ? 0f : throttleInput);

            // If clutch is disengaged or neutral, engine revs freely
            if (clutchEngagement < 0.05f || currentGear == 0)
            {
                float revRate = (targetFreeRPM > currentRPM) ? 3500f : 2000f;
                currentRPM = Mathf.MoveTowards(currentRPM, targetFreeRPM, revRate * Time.deltaTime / flywheelInertia);
            }
            else
            {
                // Clutch is engaging/engaged: engine RPM is coupled with driveshaft RPM!
                float mechanicalRPM = Mathf.Abs(driveShaftRPM);

                // Blend engine RPM towards mechanical driveshaft speed
                float coupledRPM = Mathf.Lerp(targetFreeRPM, mechanicalRPM, clutchEngagement);

                currentRPM = Mathf.MoveTowards(currentRPM, coupledRPM, 4000f * Time.deltaTime);

                // Check for ENGINE STALL:
                // If clutch is engaged and vehicle speed/driveshaft is too low, engine RPM drops below stall threshold!
                if (currentRPM < stallRPM)
                {
                    TriggerStall();
                    return;
                }
            }

            // Calculate engine torque based on RPM and throttle
            currentTorque = CalculateTorqueCurve(currentRPM, throttleInput);
        }

        private float CalculateTorqueCurve(float rpm, float throttle)
        {
            if (state != EngineState.Running) return 0f;

            // Normalized bell-shaped torque curve peaked at peakTorqueRPM
            float rpmFactor = Mathf.Clamp01(rpm / maxRPM);
            float torqueCurve = Mathf.Sin(rpmFactor * Mathf.PI);

            // Engine power output
            float rawTorque = peakTorque * (0.4f + 0.6f * torqueCurve) * throttle;

            // Engine braking when off throttle
            if (throttle < 0.05f && rpm > idleRPM)
            {
                float engineBrakeDrag = -50f * (rpm / maxRPM);
                rawTorque = engineBrakeDrag;
            }

            return rawTorque;
        }

        public void TriggerStall()
        {
            state = EngineState.Stalled;
            currentRPM = 0f;
            currentTorque = 0f;
            OnEngineStalled?.Invoke();
        }

        public void ForceStartEngine()
        {
            state = EngineState.Running;
            currentRPM = idleRPM;
            OnEngineStarted?.Invoke();
        }
    }
}
