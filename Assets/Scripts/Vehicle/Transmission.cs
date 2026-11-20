using System;
using UnityEngine;

namespace VirtualDrivingSchool.Vehicle
{
    /// <summary>
    /// Models a realistic 6-speed manual transmission with clutch friction,
    /// bite point, gear ratios, engine braking, and gear grinding.
    /// </summary>
    public class Transmission : MonoBehaviour
    {
        [Header("Gear Ratios")]
        public float reverseRatio = -3.8f;
        public float neutralRatio = 0f;
        public float[] forwardRatios = new float[6] { 3.6f, 2.1f, 1.45f, 1.0f, 0.82f, 0.65f };
        public float finalDriveRatio = 3.73f;

        [Header("Clutch Simulation & Bite Point")]
        [Tooltip("Clutch pedal travel: 0 = fully released (foot off), 1 = fully depressed (disengaged)")]
        [Range(0.2f, 0.5f)] public float bitePointStart = 0.35f;
        [Range(0.5f, 0.8f)] public float bitePointEnd = 0.65f;
        [Tooltip("Clutch torque capacity in Newton-meters when fully clamped")]
        public float clutchMaxTorque = 380f;

        [Header("Current Transmission State")]
        public int currentGear = 0; // -1 = Reverse, 0 = Neutral, 1-6 = Forward
        public float clutchEngagement = 0f; // 0 = slipping completely, 1 = locked
        public float driveShaftRPM = 0f;
        public float transmissionOutputTorque = 0f;
        public bool isGearsGrinding = false;

        public event Action OnGearChanged;
        public event Action OnGearGrind;

        private float grindTimer = 0f;

        public void ProcessTransmission(int requestedGear, float clutchPedalInput, float engineRPM, float averageWheelRPM)
        {
            // Calculate clutch engagement based on pedal position:
            // When clutch pedal is pressed down (> bitePointEnd), clutch is fully disengaged (engagement = 0)
            // When clutch pedal is fully released (< bitePointStart), clutch is fully engaged (engagement = 1)
            // Between bitePointStart and bitePointEnd, clutch slips (friction bite zone)
            if (clutchPedalInput >= bitePointEnd)
            {
                clutchEngagement = 0f; // Disengaged
            }
            else if (clutchPedalInput <= bitePointStart)
            {
                clutchEngagement = 1f; // Fully clamped
            }
            else
            {
                // Smooth progressive bite point curve
                float t = 1f - ((clutchPedalInput - bitePointStart) / (bitePointEnd - bitePointStart));
                clutchEngagement = Mathf.SmoothStep(0f, 1f, t);
            }

            // Handle Gear Changes & Grinding
            if (requestedGear != currentGear)
            {
                // If attempting to change gears while clutch is engaged and car is in motion -> Grind gears!
                if (clutchEngagement > 0.25f && Mathf.Abs(averageWheelRPM) > 30f)
                {
                    TriggerGearGrind();
                }
                else
                {
                    currentGear = requestedGear;
                    OnGearChanged?.Invoke();
                }
            }

            // Grind timer decay
            if (grindTimer > 0f)
            {
                grindTimer -= Time.deltaTime;
                isGearsGrinding = grindTimer > 0f;
            }

            // Calculate drive shaft RPM from wheel speed
            float totalRatio = GetTotalRatio();
            driveShaftRPM = averageWheelRPM * totalRatio;
        }

        public float CalculateWheelTorque(float engineTorque, float engineRPM)
        {
            float totalRatio = GetTotalRatio();
            if (Mathf.Approximately(totalRatio, 0f) || currentGear == 0)
            {
                transmissionOutputTorque = 0f;
                return 0f;
            }

            // Torque transferred across the clutch plate
            // Slipping clutch transfers torque proportional to clutch engagement
            float transmittedTorque = engineTorque * clutchEngagement;

            // Multiply by gear ratios and final drive
            transmissionOutputTorque = transmittedTorque * totalRatio;
            return transmissionOutputTorque;
        }

        public float GetTotalRatio()
        {
            if (currentGear == -1) return reverseRatio * finalDriveRatio;
            if (currentGear == 0) return 0f;
            if (currentGear >= 1 && currentGear <= forwardRatios.Length)
            {
                return forwardRatios[currentGear - 1] * finalDriveRatio;
            }
            return 0f;
        }

        public string GetGearDisplayString()
        {
            if (currentGear == -1) return "R";
            if (currentGear == 0) return "N";
            return currentGear.ToString();
        }

        private void TriggerGearGrind()
        {
            grindTimer = 0.6f;
            isGearsGrinding = true;
            OnGearGrind?.Invoke();
        }
    }
}
