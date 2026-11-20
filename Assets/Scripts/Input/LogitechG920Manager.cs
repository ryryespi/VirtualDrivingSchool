using System;
using UnityEngine;

namespace VirtualDrivingSchool.Input
{
    /// <summary>
    /// Handles input from the Logitech G920 Racing Wheel, 3-pedal set (Clutch, Brake, Gas),
    /// 6-speed Driving Force Shifter, with full fallback for Keyboard & Standard Gamepad.
    /// </summary>
    public class LogitechG920Manager : MonoBehaviour
    {
        public static LogitechG920Manager Instance { get; private set; }

        [Header("Controller State")]
        [SerializeField] private bool logitechDetected = false;
        [SerializeField] private string detectedJoystickName = "";
        [SerializeField] private bool forceKeyboardMode = false;

        [Header("Pedal Calibration & Deadzones")]
        [Tooltip("Logitech pedals usually output 1.0 when unpressed and -1.0 when fully pressed.")]
        [SerializeField] private bool invertThrottle = true;
        [SerializeField] private bool invertBrake = true;
        [SerializeField] private bool invertClutch = true;

        [Range(0f, 0.2f)] public float steeringDeadzone = 0.02f;
        [Range(0f, 0.2f)] public float pedalDeadzone = 0.05f;

        [Header("Steering Sensitivity")]
        [Range(0.5f, 2.5f)] public float steeringLinearity = 1.0f; // 1 = linear, >1 = more precise at center
        [Tooltip("Maximum steering lock angle for front wheels (degrees)")]
        public float maxSteerAngle = 35f;

        // Current Normalized Inputs (0.0 to 1.0 for pedals, -1.0 to 1.0 for steering)
        public float Steering { get; private set; }
        public float Throttle { get; private set; }
        public float Brake { get; private set; }
        public float Clutch { get; private set; } // 0.0 = foot off pedal (engaged), 1.0 = fully pressed (disengaged)
        public bool Handbrake { get; private set; }

        // Transmission Inputs
        public int SelectedGear { get; private set; } // -1 = Reverse, 0 = Neutral, 1 to 6 = Forward gears
        public bool StarterIgnition { get; private set; }
        public bool Horn { get; private set; }

        // Turn signals & accessories
        public bool TurnSignalLeft { get; private set; }
        public bool TurnSignalRight { get; private set; }
        public bool HazardLights { get; private set; }
        public bool ChangeCameraView { get; private set; }
        public bool LookLeft { get; private set; }
        public bool LookRight { get; private set; }
        public bool LookBehind { get; private set; }
        public bool ResetVehicle { get; private set; }

        // Wheel calibration and gear state
        private WheelCalibrationData calibration;
        private int paddleGear = 0;
        private bool hShifterUsed = false;
        private bool prevDpadLeft, prevDpadRight, prevDpadDown;
        private bool calibrationPrompted = false;

        // Keyboard manual transmission simulation
        private int keyboardGear = 0;
        private float simulatedClutch = 0f;

        private void Awake()
        {
            if (Instance == null) Instance = this;
            else Destroy(gameObject);

            calibration = WheelCalibrationData.Load();
            if (GetComponent<WheelCalibrationWizard>() == null) gameObject.AddComponent<WheelCalibrationWizard>();
            CheckConnectedControllers();
        }

        private void Update()
        {
            // Periodically check if controller plugged/unplugged
            if (Time.frameCount % 120 == 0)
            {
                CheckConnectedControllers();
            }

            if (WheelCalibrationWizard.IsActive)
            {
                ClearInputs();
                return;
            }

            if (logitechDetected && !forceKeyboardMode && !calibrationPrompted && (calibration == null || !calibration.calibrated))
            {
                calibrationPrompted = true;
                OpenCalibration();
                return;
            }

            if (logitechDetected && !forceKeyboardMode)
            {
                PollLogitechWheel();
            }
            else
            {
                PollKeyboardAndGamepad();
            }
        }

        public void CheckConnectedControllers()
        {
            string[] joysticks = UnityEngine.Input.GetJoystickNames();
            logitechDetected = false;
            detectedJoystickName = "None";

            for (int i = 0; i < joysticks.Length; i++)
            {
                string joy = joysticks[i];
                if (!string.IsNullOrEmpty(joy))
                {
                    string lower = joy.ToLower();
                    if (lower.Contains("g920") || lower.Contains("g923") || lower.Contains("g29") || lower.Contains("g27") || lower.Contains("logitech") || lower.Contains("driving force") || lower.Contains("wheel"))
                    {
                        logitechDetected = true;
                        detectedJoystickName = joy;
                        break;
                    }
                    else if (detectedJoystickName == "None")
                    {
                        detectedJoystickName = joy;
                    }
                }
            }
        }

        /// <summary>
        /// Reads the physical wheel, pedals, shifter and buttons using the saved calibration.
        /// </summary>
        private void PollLogitechWheel()
        {
            if (calibration == null) calibration = WheelCalibrationData.Load();

            // --- STEERING ---
            float rawSteer = calibration.ReadSteering();
            if (Mathf.Abs(rawSteer) < steeringDeadzone) rawSteer = 0f;
            else rawSteer = Mathf.Sign(rawSteer) * Mathf.Pow(Mathf.Abs(rawSteer), steeringLinearity);
            Steering = Mathf.Clamp(rawSteer, -1f, 1f);

            // --- PEDALS (0 = foot off, 1 = floored) ---
            Throttle = ApplyPedalDeadzone(calibration.throttle.Read());
            Brake = ApplyPedalDeadzone(calibration.brake.Read());
            Clutch = ApplyPedalDeadzone(calibration.clutch.Read());

            // --- GEARS: H-pattern shifter wins when used, otherwise paddles shift sequentially ---
            int hGear = DetectHShifterGear();
            bool anyShifterSlot = hGear != 0;
            if (anyShifterSlot) hShifterUsed = true;

            if (UnityEngine.Input.GetKeyDown(KeyCode.JoystickButton4) || UnityEngine.Input.GetKeyDown(KeyCode.JoystickButton5))
                hShifterUsed = false; // driver switched back to paddles

            if (hShifterUsed)
            {
                paddleGear = hGear;
                SelectedGear = hGear; // stick in the middle = neutral
            }
            else
            {
                if (UnityEngine.Input.GetKeyDown(KeyCode.JoystickButton5) && paddleGear < 6) paddleGear++;
                if (UnityEngine.Input.GetKeyDown(KeyCode.JoystickButton4) && paddleGear > -1) paddleGear--;
                SelectedGear = paddleGear;
            }

            // --- BUTTONS ---
            Handbrake = UnityEngine.Input.GetKey(KeyCode.JoystickButton0) || UnityEngine.Input.GetKey(KeyCode.Space);
            Horn = UnityEngine.Input.GetKey(KeyCode.JoystickButton1) || UnityEngine.Input.GetKey(KeyCode.F);
            StarterIgnition = UnityEngine.Input.GetKey(KeyCode.JoystickButton2) || UnityEngine.Input.GetKey(KeyCode.E);
            ChangeCameraView = UnityEngine.Input.GetKeyDown(KeyCode.JoystickButton3) || UnityEngine.Input.GetKeyDown(KeyCode.C);
            ResetVehicle = UnityEngine.Input.GetKeyDown(KeyCode.JoystickButton6) || UnityEngine.Input.GetKeyDown(KeyCode.R);

            // --- D-PAD (edge-triggered so a held press toggles a signal once) ---
            float dpadX = SafeAxisRaw("DpadX");
            float dpadY = SafeAxisRaw("DpadY");
            bool left = dpadX < -0.5f, right = dpadX > 0.5f, down = dpadY < -0.5f;

            TurnSignalLeft = (left && !prevDpadLeft) || UnityEngine.Input.GetKeyDown(KeyCode.LeftBracket);
            TurnSignalRight = (right && !prevDpadRight) || UnityEngine.Input.GetKeyDown(KeyCode.RightBracket);
            HazardLights = (down && !prevDpadDown) || UnityEngine.Input.GetKeyDown(KeyCode.H);
            prevDpadLeft = left; prevDpadRight = right; prevDpadDown = down;

            LookLeft = UnityEngine.Input.GetKey(KeyCode.Q);
            LookRight = UnityEngine.Input.GetKey(KeyCode.E) && !StarterIgnition;
            LookBehind = UnityEngine.Input.GetKey(KeyCode.JoystickButton8) || UnityEngine.Input.GetKey(KeyCode.B);
        }

        private float ApplyPedalDeadzone(float v)
        {
            if (v < pedalDeadzone) return 0f;
            return Mathf.Clamp01((v - pedalDeadzone) / (1f - pedalDeadzone));
        }

        private static float SafeAxisRaw(string axis)
        {
            try { return UnityEngine.Input.GetAxisRaw(axis); } catch (ArgumentException) { return 0f; }
        }

        private void ClearInputs()
        {
            Steering = 0f; Throttle = 0f; Brake = 0f; Clutch = 1f; Handbrake = true;
            StarterIgnition = false; Horn = false; ChangeCameraView = false; ResetVehicle = false;
            TurnSignalLeft = false; TurnSignalRight = false; HazardLights = false;
            LookLeft = false; LookRight = false; LookBehind = false;
        }

        /// <summary>Reloads the saved wheel calibration (called by the setup wizard).</summary>
        public void ReloadCalibration() { calibration = WheelCalibrationData.Load(); }

        /// <summary>Opens the on-screen wheel and pedal setup.</summary>
        public void OpenCalibration()
        {
            if (WheelCalibrationWizard.Instance == null) gameObject.AddComponent<WheelCalibrationWizard>();
            WheelCalibrationWizard.Instance.Begin(detectedJoystickName);
        }
        private int DetectHShifterGear()
        {
            // Logitech Driving Force Shifter gear slots:
            // Gear 1: Button 12
            // Gear 2: Button 13
            // Gear 3: Button 14
            // Gear 4: Button 15
            // Gear 5: Button 16
            // Gear 6: Button 17
            // Reverse (push knob down + 6th gear position): Button 18
            if (UnityEngine.Input.GetKey(KeyCode.JoystickButton18)) return -1; // Reverse
            if (UnityEngine.Input.GetKey(KeyCode.JoystickButton12)) return 1;
            if (UnityEngine.Input.GetKey(KeyCode.JoystickButton13)) return 2;
            if (UnityEngine.Input.GetKey(KeyCode.JoystickButton14)) return 3;
            if (UnityEngine.Input.GetKey(KeyCode.JoystickButton15)) return 4;
            if (UnityEngine.Input.GetKey(KeyCode.JoystickButton16)) return 5;
            if (UnityEngine.Input.GetKey(KeyCode.JoystickButton17)) return 6;

            return 0; // Neutral
        }

        private float ReadPedalRaw(string axisName, string fallbackAxis, int rawAxisNumber)
        {
            try
            {
                return UnityEngine.Input.GetAxis(axisName);
            }
            catch
            {
                try
                {
                    return UnityEngine.Input.GetAxis(fallbackAxis);
                }
                catch
                {
                    return 0f;
                }
            }
        }

        private float NormalizePedal(float raw, bool inverted)
        {
            // Logitech pedals report +1.0 unpressed, -1.0 pressed.
            float val = raw;
            if (inverted)
            {
                // Convert range [+1, -1] -> [0, 1]
                val = (1.0f - raw) * 0.5f;
            }
            else
            {
                // Convert range [-1, +1] -> [0, 1]
                val = (raw + 1.0f) * 0.5f;
            }

            if (val < pedalDeadzone) return 0f;
            return Mathf.Clamp01((val - pedalDeadzone) / (1f - pedalDeadzone));
        }

        /// <summary>
        /// Keyboard & Standard Gamepad fallback for testing without G920 plugged in
        /// </summary>
        private void PollKeyboardAndGamepad()
        {
            // Steering: A/D or Left/Right arrows
            float steerTarget = 0f;
            if (UnityEngine.Input.GetKey(KeyCode.A) || UnityEngine.Input.GetKey(KeyCode.LeftArrow)) steerTarget -= 1f;
            if (UnityEngine.Input.GetKey(KeyCode.D) || UnityEngine.Input.GetKey(KeyCode.RightArrow)) steerTarget += 1f;

            // Smooth keyboard steering
            Steering = Mathf.MoveTowards(Steering, steerTarget, Time.deltaTime * 3.5f);

            // Throttle: W or Up Arrow
            Throttle = (UnityEngine.Input.GetKey(KeyCode.W) || UnityEngine.Input.GetKey(KeyCode.UpArrow)) ? 1f : 0f;

            // Brake: S or Down Arrow
            Brake = (UnityEngine.Input.GetKey(KeyCode.S) || UnityEngine.Input.GetKey(KeyCode.DownArrow)) ? 1f : 0f;

            // Clutch: Left Shift (Hold down to disengage clutch, release to engage)
            float clutchTarget = UnityEngine.Input.GetKey(KeyCode.LeftShift) ? 1f : 0f;
            simulatedClutch = Mathf.MoveTowards(simulatedClutch, clutchTarget, Time.deltaTime * 5f);
            Clutch = simulatedClutch;

            // Handbrake: Spacebar
            Handbrake = UnityEngine.Input.GetKey(KeyCode.Space);

            // Keyboard Shifter:
            // Number keys 1-6 = Gears 1-6, R = Reverse, N = Neutral
            if (UnityEngine.Input.GetKeyDown(KeyCode.Alpha1)) keyboardGear = 1;
            else if (UnityEngine.Input.GetKeyDown(KeyCode.Alpha2)) keyboardGear = 2;
            else if (UnityEngine.Input.GetKeyDown(KeyCode.Alpha3)) keyboardGear = 3;
            else if (UnityEngine.Input.GetKeyDown(KeyCode.Alpha4)) keyboardGear = 4;
            else if (UnityEngine.Input.GetKeyDown(KeyCode.Alpha5)) keyboardGear = 5;
            else if (UnityEngine.Input.GetKeyDown(KeyCode.Alpha6)) keyboardGear = 6;
            else if (UnityEngine.Input.GetKeyDown(KeyCode.R) && !UnityEngine.Input.GetKey(KeyCode.LeftControl)) keyboardGear = -1;
            else if (UnityEngine.Input.GetKeyDown(KeyCode.N)) keyboardGear = 0;

            // Sequential shifting keys: E = Upshift, Q = Downshift
            if (UnityEngine.Input.GetKeyDown(KeyCode.PageUp) && keyboardGear < 6) keyboardGear++;
            if (UnityEngine.Input.GetKeyDown(KeyCode.PageDown) && keyboardGear > -1) keyboardGear--;

            SelectedGear = keyboardGear;

            // Ignition: Key 'E'
            StarterIgnition = UnityEngine.Input.GetKey(KeyCode.E);

            // Horn: Key 'F'
            Horn = UnityEngine.Input.GetKey(KeyCode.F);

            // Camera View: Key 'C'
            ChangeCameraView = UnityEngine.Input.GetKeyDown(KeyCode.C);

            // Turn signals
            TurnSignalLeft = UnityEngine.Input.GetKeyDown(KeyCode.LeftBracket);
            TurnSignalRight = UnityEngine.Input.GetKeyDown(KeyCode.RightBracket);
            HazardLights = UnityEngine.Input.GetKeyDown(KeyCode.H);

            // Look Around
            LookLeft = UnityEngine.Input.GetKey(KeyCode.Q);
            LookRight = UnityEngine.Input.GetKey(KeyCode.E) && !StarterIgnition;
            LookBehind = UnityEngine.Input.GetKey(KeyCode.B);

            ResetVehicle = UnityEngine.Input.GetKeyDown(KeyCode.Backspace);
        }

        // Public helper methods for settings UI
        public bool IsLogitechDetected => logitechDetected;
        public string DetectedControllerName => detectedJoystickName;

        public void SetPedalInversion(bool throttle, bool brake, bool clutch)
        {
            invertThrottle = throttle;
            invertBrake = brake;
            invertClutch = clutch;
        }

        public void ToggleForceKeyboardMode()
        {
            forceKeyboardMode = !forceKeyboardMode;
        }
    }
}
