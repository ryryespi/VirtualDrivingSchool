using UnityEngine;

namespace VirtualDrivingSchool.Input
{
    /// <summary>
    /// On-screen wheel and pedal calibration. Opens automatically the first time a wheel is
    /// detected without a saved calibration, or any time with F9. Each step auto-advances
    /// when the control is held for about a second, so no extra button presses are needed.
    /// </summary>
    public class WheelCalibrationWizard : MonoBehaviour
    {
        public static bool IsActive { get; private set; }
        public static WheelCalibrationWizard Instance { get; private set; }

        enum Step { Intro, Left, Right, Gas, GasRelease, Brake, BrakeRelease, Clutch, ClutchRelease, Done }

        Step step;
        float[] baseline;
        float holdTimer;
        int candidateAxis;
        float candidateValue;
        WheelCalibrationData data;
        float doneTimer;
        string deviceName;

        const float MoveThreshold = 0.5f;
        const float HoldSeconds = 1.0f;

        void Awake()
        {
            if (Instance == null) Instance = this; else { Destroy(this); return; }
        }

        public void Begin(string device)
        {
            deviceName = device;
            data = WheelCalibrationData.Load();
            data.deviceName = device;
            step = Step.Intro;
            holdTimer = 0f;
            IsActive = true;
        }

        void Update()
        {
            if (UnityEngine.Input.GetKeyDown(KeyCode.F9))
            {
                if (IsActive) { IsActive = false; return; }
                var mgr = LogitechG920Manager.Instance;
                Begin(mgr != null ? mgr.DetectedControllerName : "Wheel");
            }
            if (!IsActive) return;

            if (UnityEngine.Input.GetKeyDown(KeyCode.Escape)) { IsActive = false; return; }

            switch (step)
            {
                case Step.Intro:
                    if (UnityEngine.Input.GetKeyDown(KeyCode.Return) || UnityEngine.Input.GetKeyDown(KeyCode.KeypadEnter) ||
                        UnityEngine.Input.GetKeyDown(KeyCode.JoystickButton0))
                    {
                        baseline = WheelAxes.Snapshot();
                        step = Step.Left; holdTimer = 0f;
                    }
                    break;
                case Step.Left:
                    if (DetectHeldAxis(0, out int lAxis, out float lVal))
                    {
                        data.steerAxis = lAxis; data.steerCenter = baseline[lAxis]; data.steerLeft = lVal;
                        step = Step.Right;
                    }
                    break;
                case Step.Right:
                    if (HeldOnAxis(data.steerAxis, data.steerCenter, out float rVal) && Mathf.Sign(rVal - data.steerCenter) != Mathf.Sign(data.steerLeft - data.steerCenter))
                    {
                        data.steerRight = rVal;
                        baseline = WheelAxes.Snapshot();
                        step = Step.Gas;
                    }
                    break;
                case Step.Gas:
                    if (DetectHeldAxis(data.steerAxis, out int gAxis, out float gVal)) { data.throttle = new PedalMap(gAxis, baseline[gAxis], gVal); step = Step.GasRelease; }
                    break;
                case Step.GasRelease:
                    if (Released(data.throttle)) { baseline = WheelAxes.Snapshot(); step = Step.Brake; }
                    break;
                case Step.Brake:
                    if (DetectHeldAxis(data.steerAxis, out int bAxis, out float bVal, data.throttle.axis)) { data.brake = new PedalMap(bAxis, baseline[bAxis], bVal); step = Step.BrakeRelease; }
                    break;
                case Step.BrakeRelease:
                    if (Released(data.brake)) { baseline = WheelAxes.Snapshot(); step = Step.Clutch; }
                    break;
                case Step.Clutch:
                    if (UnityEngine.Input.GetKeyDown(KeyCode.Space)) { data.clutch = new PedalMap(0, 0f, 0f); Finish(); break; }
                    if (DetectHeldAxis(data.steerAxis, out int cAxis, out float cVal, data.throttle.axis, data.brake.axis)) { data.clutch = new PedalMap(cAxis, baseline[cAxis], cVal); step = Step.ClutchRelease; }
                    break;
                case Step.ClutchRelease:
                    if (Released(data.clutch)) Finish();
                    break;
                case Step.Done:
                    doneTimer -= Time.unscaledDeltaTime;
                    if (doneTimer <= 0f) IsActive = false;
                    break;
            }
        }

        void Finish()
        {
            data.calibrated = true;
            data.Save();
            if (LogitechG920Manager.Instance != null) LogitechG920Manager.Instance.ReloadCalibration();
            step = Step.Done; doneTimer = 2.5f;
        }

        // Finds the axis (other than the excluded ones) that moved the most from baseline and has been held ~1s.
        bool DetectHeldAxis(int exclude, out int axis, out float value, int exclude2 = 0, int exclude3 = 0)
        {
            axis = 0; value = 0f;
            int best = 0; float bestDelta = 0f;
            for (int i = 1; i <= WheelAxes.AxisCount; i++)
            {
                if (i == exclude || i == exclude2 || i == exclude3) continue;
                float d = Mathf.Abs(WheelAxes.Raw(i) - baseline[i]);
                if (d > bestDelta) { bestDelta = d; best = i; }
            }
            if (bestDelta < MoveThreshold) { holdTimer = 0f; candidateAxis = 0; return false; }
            float cur = WheelAxes.Raw(best);
            if (best != candidateAxis) { candidateAxis = best; holdTimer = 0f; candidateValue = cur; }
            if (Mathf.Abs(cur - baseline[best]) > Mathf.Abs(candidateValue - baseline[best])) candidateValue = cur;
            holdTimer += Time.unscaledDeltaTime;
            if (holdTimer < HoldSeconds) return false;
            axis = best; value = candidateValue; holdTimer = 0f; candidateAxis = 0;
            return true;
        }

        bool HeldOnAxis(int axis, float center, out float value)
        {
            value = WheelAxes.Raw(axis);
            if (Mathf.Abs(value - center) < MoveThreshold) { holdTimer = 0f; return false; }
            holdTimer += Time.unscaledDeltaTime;
            if (holdTimer < HoldSeconds) return false;
            holdTimer = 0f; return true;
        }

        // After a full press, record the true resting value once the pedal comes back up.
        bool Released(PedalMap p)
        {
            float raw = WheelAxes.Raw(p.axis);
            if (Mathf.Abs(raw - p.full) < MoveThreshold) { holdTimer = 0f; return false; }
            holdTimer += Time.unscaledDeltaTime;
            if (holdTimer < 0.5f) return false;
            p.rest = raw; holdTimer = 0f; return true;
        }

        void OnGUI()
        {
            if (!IsActive) return;
            float w = 560, h = 230;
            var r = new Rect((Screen.width - w) / 2, (Screen.height - h) / 2, w, h);
            GUI.color = new Color(0, 0, 0, 0.85f);
            GUI.DrawTexture(r, Texture2D.whiteTexture);
            GUI.color = Color.white;
            var title = new GUIStyle(GUI.skin.label) { fontSize = 22, fontStyle = FontStyle.Bold, alignment = TextAnchor.UpperCenter, wordWrap = true };
            var body = new GUIStyle(GUI.skin.label) { fontSize = 17, alignment = TextAnchor.UpperCenter, wordWrap = true };
            GUI.Label(new Rect(r.x + 20, r.y + 18, w - 40, 30), "Wheel & Pedal Setup", title);
            GUI.Label(new Rect(r.x + 20, r.y + 60, w - 40, 110), Instruction(), body);
            if (step != Step.Intro && step != Step.Done && holdTimer > 0f)
                GUI.Label(new Rect(r.x + 20, r.y + 165, w - 40, 25), "Hold... " + Mathf.CeilToInt((HoldSeconds - holdTimer) * 10) / 10f, body);
            var small = new GUIStyle(body) { fontSize = 13 };
            GUI.Label(new Rect(r.x + 20, r.y + h - 32, w - 40, 22), "Device: " + deviceName + "    Esc = cancel    F9 = recalibrate anytime", small);
        }

        string Instruction()
        {
            switch (step)
            {
                case Step.Intro: return "Center the wheel and take your feet off all pedals.\nThen press Enter (or the A button on the wheel).";
                case Step.Left: return "Turn the wheel all the way LEFT and hold it.";
                case Step.Right: return "Now turn the wheel all the way RIGHT and hold it.";
                case Step.Gas: return "Center the wheel. Press the GAS pedal (right) all the way down and hold.";
                case Step.GasRelease: return "Release the gas pedal.";
                case Step.Brake: return "Press the BRAKE pedal (middle) all the way down and hold.";
                case Step.BrakeRelease: return "Release the brake pedal.";
                case Step.Clutch: return "Press the CLUTCH pedal (left) all the way down and hold.\n(No clutch pedal? Press Space to skip.)";
                case Step.ClutchRelease: return "Release the clutch pedal.";
                case Step.Done: return "All set! Your wheel and pedals are calibrated and saved.";
            }
            return "";
        }
    }
}