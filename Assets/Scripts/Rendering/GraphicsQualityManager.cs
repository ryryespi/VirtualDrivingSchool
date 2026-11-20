using System;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace VirtualDrivingSchool.Rendering
{
    /// <summary>
    /// Two graphics presets tuned for low-end Windows 10 PCs (integrated GPUs):
    ///  Low  (default) - hard shadows, 40 m shadow distance, no MSAA, no HDR, no post-processing, 256px mirrors.
    ///  High           - soft shadows, 2 cascades, 2x MSAA, HDR, mild bloom + color grading, 512px mirrors.
    /// Press F10 to toggle. The choice is saved in PlayerPrefs.
    /// </summary>
    public class GraphicsQualityManager : MonoBehaviour
    {
        public const string PrefKey = "VDS_GraphicsHigh";
        public static GraphicsQualityManager Instance { get; private set; }
        public static bool IsHigh { get; private set; }
        public static event Action<bool> QualityChanged;

        public KeyCode toggleKey = KeyCode.F10;

        private Volume postVolume;
        private float toastUntil;

        public static GraphicsQualityManager Ensure()
        {
            if (Instance != null) return Instance;
            var existing = FindFirstObjectByType<GraphicsQualityManager>();
            if (existing != null) return existing;
            return new GameObject("Graphics_Quality").AddComponent<GraphicsQualityManager>();
        }

        private void Awake()
        {
            if (Instance != null && Instance != this) { Destroy(this); return; }
            Instance = this;
            Apply(PlayerPrefs.GetInt(PrefKey, 0) == 1, false);
        }

        private void Update()
        {
            if (UnityEngine.Input.GetKeyDown(toggleKey))
            {
                Apply(!IsHigh, true);
            }
        }

        public void Apply(bool high, bool save)
        {
            IsHigh = high;

            int level = FindQualityLevel(high ? "High" : "Low");
            if (level >= 0 && QualitySettings.GetQualityLevel() != level)
            {
                QualitySettings.SetQualityLevel(level, true);
            }

            EnsureVolume();
            if (postVolume != null) postVolume.enabled = high;

            Camera main = Camera.main;
            if (main != null)
            {
                var data = main.GetUniversalAdditionalCameraData();
                if (data != null)
                {
                    data.renderPostProcessing = high;
                    data.antialiasing = AntialiasingMode.None;
                }
                main.allowHDR = high;
                main.allowMSAA = high;
                main.farClipPlane = high ? 450f : 320f;
            }

            if (save)
            {
                PlayerPrefs.SetInt(PrefKey, high ? 1 : 0);
                PlayerPrefs.Save();
                toastUntil = Time.unscaledTime + 2.5f;
            }

            QualityChanged?.Invoke(high);
        }

        private void EnsureVolume()
        {
            if (postVolume != null) return;
            VdsArtLibrary lib = VdsArtLibrary.Get();
            if (lib == null || lib.highQualityProfile == null) return;
            GameObject go = new GameObject("PostProcess_Volume_High");
            go.transform.SetParent(transform, false);
            postVolume = go.AddComponent<Volume>();
            postVolume.isGlobal = true;
            postVolume.priority = 10f;
            postVolume.sharedProfile = lib.highQualityProfile;
        }

        private static int FindQualityLevel(string name)
        {
            string[] names = QualitySettings.names;
            for (int i = 0; i < names.Length; i++)
            {
                if (string.Equals(names[i], name, StringComparison.OrdinalIgnoreCase)) return i;
            }
            return -1;
        }

        private void OnGUI()
        {
            if (Time.unscaledTime > toastUntil) return;
            string msg = "Graphics: " + (IsHigh ? "HIGH" : "LOW (best for low-end PCs)") + "   [F10 to switch]";
            var style = new GUIStyle(GUI.skin.box) { fontSize = 16, alignment = TextAnchor.MiddleCenter };
            style.normal.textColor = Color.white;
            GUI.Box(new Rect(Screen.width * 0.5f - 210f, Screen.height - 120f, 420f, 34f), msg, style);
        }
    }
}
