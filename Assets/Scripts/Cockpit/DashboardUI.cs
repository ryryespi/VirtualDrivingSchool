using UnityEngine;
using VirtualDrivingSchool.Input;
using VirtualDrivingSchool.Vehicle;

namespace VirtualDrivingSchool.Cockpit
{
    /// <summary>
    /// Renders the instrument cluster HUD with Tachometer, Speedometer,
    /// Gear Indicator, Handbrake, and Engine Stall warning annunciators.
    /// </summary>
    public class DashboardUI : MonoBehaviour
    {
        [Header("References")]
        public ManualCarController car;
        public EngineSimulation engine;
        public Transmission transmission;
        public LogitechG920Manager input;

        [Header("Cluster Position")]
        public Vector2 clusterSize = new Vector2(380, 160);

        private Texture2D whiteTex;
        private GUIStyle rpmStyle;
        private GUIStyle speedStyle;
        private GUIStyle gearStyle;
        private GUIStyle warningStyle;
        private GUIStyle statusStyle;

        private float blinkTimer = 0f;
        private bool blinkState = false;

        private void Awake()
        {
            if (car == null) car = FindFirstObjectByType<ManualCarController>();
            if (engine == null && car != null) engine = car.engine;
            if (transmission == null && car != null) transmission = car.transmission;
            if (input == null) input = LogitechG920Manager.Instance;

            whiteTex = new Texture2D(1, 1);
            whiteTex.SetPixel(0, 0, Color.white);
            whiteTex.Apply();
        }

        private void Update()
        {
            blinkTimer += Time.deltaTime;
            if (blinkTimer > 0.35f)
            {
                blinkTimer = 0f;
                blinkState = !blinkState;
            }
        }

        private void OnGUI()
        {
            if (car == null) return;
            InitStyles();

            float x = (Screen.width - clusterSize.x) * 0.5f;
            float y = Screen.height - clusterSize.y - 25f;

            // Background panel
            Rect panelRect = new Rect(x, y, clusterSize.x, clusterSize.y);
            DrawRect(panelRect, new Color(0.06f, 0.07f, 0.09f, 0.78f));
            DrawOutline(panelRect, new Color(0.2f, 0.25f, 0.3f, 0.8f), 2);

            // 1. GEAR DISPLAY (Center)
            string gearStr = transmission != null ? transmission.GetGearDisplayString() : "N";
            Color gearColor = Color.white;
            if (gearStr == "R") gearColor = new Color(1f, 0.3f, 0.3f);
            else if (gearStr == "N") gearColor = new Color(0.3f, 0.8f, 1f);

            GUI.color = gearColor;
            GUI.Label(new Rect(x + (clusterSize.x * 0.5f) - 30, y + 20, 60, 60), gearStr, gearStyle);
            GUI.color = Color.white;

            // 2. SPEEDOMETER (Left)
            int speedMph = (int)Mathf.Abs(car.SpeedMph);
            GUI.Label(new Rect(x + 20, y + 25, 100, 40), $"{speedMph}", speedStyle);
            GUI.Label(new Rect(x + 25, y + 65, 90, 20), "MPH", statusStyle);

            // 3. TACHOMETER (Right)
            int rpm = engine != null ? (int)engine.currentRPM : 0;
            Color rpmColor = (rpm > 6000) ? Color.red : new Color(0.9f, 0.9f, 0.9f);
            GUI.color = rpmColor;
            GUI.Label(new Rect(x + clusterSize.x - 120, y + 25, 100, 40), $"{rpm}", rpmStyle);
            GUI.color = Color.white;
            GUI.Label(new Rect(x + clusterSize.x - 115, y + 65, 90, 20), "RPM", statusStyle);

            // RPM Horizontal Meter Bar
            float rpmPercent = Mathf.Clamp01(rpm / (engine != null ? engine.maxRPM : 7000f));
            float barW = clusterSize.x - 40;
            DrawRect(new Rect(x + 20, y + 92, barW, 8), new Color(0.2f, 0.2f, 0.25f));
            Color barFillColor = (rpmPercent > 0.85f) ? Color.red : (rpmPercent > 0.65f ? Color.yellow : new Color(0.2f, 0.7f, 1f));
            DrawRect(new Rect(x + 20, y + 92, barW * rpmPercent, 8), barFillColor);

            // 4. WARNING & STATUS LIGHTS (Bottom strip)
            float iconY = y + 115;

            // Turn signals
            if (input != null && input.TurnSignalLeft && blinkState)
            {
                GUI.color = Color.green;
                GUI.Label(new Rect(x + 25, iconY, 30, 25), "◄◄", warningStyle);
            }
            if (input != null && input.TurnSignalRight && blinkState)
            {
                GUI.color = Color.green;
                GUI.Label(new Rect(x + clusterSize.x - 55, iconY, 30, 25), "►►", warningStyle);
            }
            GUI.color = Color.white;

            // Handbrake warning
            if (input != null && input.Handbrake)
            {
                GUI.color = new Color(1f, 0.2f, 0.2f);
                GUI.Label(new Rect(x + 80, iconY, 50, 25), "(P) BRAKE", warningStyle);
            }

            // Engine Stall or Off Alert
            if (engine != null && engine.isStalled)
            {
                if (blinkState)
                {
                    GUI.color = Color.red;
                    GUI.Label(new Rect(x + 145, iconY, 160, 25), "ENGINE STALLED!", warningStyle);
                }
            }
            else if (engine != null && engine.state == EngineState.Off)
            {
                GUI.color = Color.yellow;
                GUI.Label(new Rect(x + 140, iconY, 170, 25), "IGNITION OFF (HOLD E)", warningStyle);
            }
            GUI.color = Color.white;

            // Top Status Bar: Controller Name
            string ctlName = (input != null && input.IsLogitechDetected) ? $"LOGITECH {input.DetectedControllerName.ToUpper()}" : "KEYBOARD & MOUSE MODE";
            GUI.Label(new Rect(x, y - 22, clusterSize.x, 20), ctlName, statusStyle);
        }

        private void DrawRect(Rect rect, Color color)
        {
            Color old = GUI.color;
            GUI.color = color;
            GUI.DrawTexture(rect, whiteTex);
            GUI.color = old;
        }

        private void DrawOutline(Rect rect, Color color, int thickness)
        {
            DrawRect(new Rect(rect.x, rect.y, rect.width, thickness), color);
            DrawRect(new Rect(rect.x, rect.y + rect.height - thickness, rect.width, thickness), color);
            DrawRect(new Rect(rect.x, rect.y, thickness, rect.height), color);
            DrawRect(new Rect(rect.x + rect.width - thickness, rect.y, thickness, rect.height), color);
        }

        private void InitStyles()
        {
            if (gearStyle == null)
            {
                gearStyle = new GUIStyle(GUI.skin.label);
                gearStyle.fontSize = 42;
                gearStyle.fontStyle = FontStyle.Bold;
                gearStyle.alignment = TextAnchor.MiddleCenter;
            }
            if (speedStyle == null)
            {
                speedStyle = new GUIStyle(GUI.skin.label);
                speedStyle.fontSize = 32;
                speedStyle.fontStyle = FontStyle.Bold;
                speedStyle.alignment = TextAnchor.MiddleCenter;
                speedStyle.normal.textColor = Color.white;
            }
            if (rpmStyle == null)
            {
                rpmStyle = new GUIStyle(GUI.skin.label);
                rpmStyle.fontSize = 28;
                rpmStyle.fontStyle = FontStyle.Bold;
                rpmStyle.alignment = TextAnchor.MiddleCenter;
            }
            if (warningStyle == null)
            {
                warningStyle = new GUIStyle(GUI.skin.label);
                warningStyle.fontSize = 11;
                warningStyle.fontStyle = FontStyle.Bold;
                warningStyle.alignment = TextAnchor.MiddleCenter;
            }
            if (statusStyle == null)
            {
                statusStyle = new GUIStyle(GUI.skin.label);
                statusStyle.fontSize = 10;
                statusStyle.fontStyle = FontStyle.Bold;
                statusStyle.alignment = TextAnchor.MiddleCenter;
                statusStyle.normal.textColor = new Color(0.6f, 0.7f, 0.8f);
            }
        }
    }
}
