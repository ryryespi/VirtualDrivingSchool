using System;
using UnityEngine;

namespace VirtualDrivingSchool.Input
{
    /// <summary>
    /// Raw access to every joystick axis (JoyAxis1..JoyAxis12 in the Input Manager) and a
    /// saved per-device calibration, so any Logitech wheel/pedal mapping works on any PC.
    /// </summary>
    public static class WheelAxes
    {
        public const int AxisCount = 12;

        public static float Raw(int axis)
        {
            if (axis < 1 || axis > AxisCount) return 0f;
            try { return UnityEngine.Input.GetAxisRaw("JoyAxis" + axis); }
            catch (ArgumentException) { return 0f; }
        }

        public static float[] Snapshot()
        {
            var s = new float[AxisCount + 1];
            for (int i = 1; i <= AxisCount; i++) s[i] = Raw(i);
            return s;
        }
    }

    [Serializable]
    public class PedalMap
    {
        public int axis;      // 1-based joystick axis, 0 = not mapped
        public float rest;    // raw value with foot off
        public float full;    // raw value fully pressed

        public PedalMap(int axis, float rest, float full) { this.axis = axis; this.rest = rest; this.full = full; }

        public float Read()
        {
            if (axis <= 0 || Mathf.Approximately(rest, full)) return 0f;
            float raw = WheelAxes.Raw(axis);
            // Some drivers report 0 until a pedal is first touched; treat exact 0 at a +-1 rest as "not touched yet".
            if (raw == 0f && Mathf.Abs(rest) > 0.9f && Mathf.Abs(full) > 0.9f) return 0f;
            return Mathf.Clamp01(Mathf.InverseLerp(rest, full, raw));
        }
    }

    [Serializable]
    public class WheelCalibrationData
    {
        public string deviceName = "";
        public int steerAxis = 1;
        public float steerCenter = 0f, steerLeft = -1f, steerRight = 1f;
        public PedalMap throttle = new PedalMap(2, 1f, -1f);
        public PedalMap brake = new PedalMap(3, 1f, -1f);
        public PedalMap clutch = new PedalMap(4, 1f, -1f);
        public bool calibrated = false;

        const string Key = "VDS_WheelCalibration_v1";

        public float ReadSteering()
        {
            float raw = WheelAxes.Raw(steerAxis);
            float toRight = steerRight - steerCenter;
            float toLeft = steerLeft - steerCenter;
            if (Mathf.Approximately(toRight, 0f) || Mathf.Approximately(toLeft, 0f)) return 0f;
            float r = (raw - steerCenter) / toRight;
            if (r >= 0f) return Mathf.Clamp01(r);
            return -Mathf.Clamp01((raw - steerCenter) / toLeft);
        }

        public static WheelCalibrationData Load()
        {
            string json = PlayerPrefs.GetString(Key, "");
            if (!string.IsNullOrEmpty(json))
            {
                try { var d = JsonUtility.FromJson<WheelCalibrationData>(json); if (d != null) return d; } catch { }
            }
            return new WheelCalibrationData();
        }

        public void Save()
        {
            PlayerPrefs.SetString(Key, JsonUtility.ToJson(this));
            PlayerPrefs.Save();
        }

        public static void Clear() { PlayerPrefs.DeleteKey(Key); }
    }
}