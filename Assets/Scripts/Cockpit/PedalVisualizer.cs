using UnityEngine;
using VirtualDrivingSchool.Input;
using VirtualDrivingSchool.Vehicle;

namespace VirtualDrivingSchool.Cockpit
{
    /// <summary>
    /// Displays a real-time HUD pedal telemetry overlay showing Clutch (with bite point zone),
    /// Brake, and Gas pedals. Essential for driver training with the Logitech G920 pedal set.
    /// </summary>
    public class PedalVisualizer : MonoBehaviour
    {
        [Header("References")]
        public LogitechG920Manager input;
        public Transmission transmission;
        public EngineSimulation engine;

        [Header("Display Settings")]
        public bool showHUD = true;
        public Vector2 hudPosition = new Vector2(25, 25);
        public Vector2 pedalSize = new Vector2(38, 160);
        public float spacing = 18f;

        private GUIStyle titleStyle;
        private GUIStyle labelStyle;
        private Texture2D whiteTex;

        private void Awake()
        {
            if (input == null) input = LogitechG920Manager.Instance;
            if (transmission == null) transmission = FindFirstObjectByType<Transmission>();
            if (engine == null) engine = FindFirstObjectByType<EngineSimulation>();

            whiteTex = new Texture2D(1, 1);
            whiteTex.SetPixel(0, 0, Color.white);
            whiteTex.Apply();
        }

        private void OnGUI()
        {
            if (!showHUD) return;
            if (input == null) input = LogitechG920Manager.Instance;

            InitStyles();

            float x = hudPosition.x;
            float y = Screen.height - hudPosition.y - pedalSize.y - 60f;

            // Background box
            Rect bgRect = new Rect(x - 12, y - 35, (pedalSize.x * 3) + (spacing * 2) + 24, pedalSize.y + 70);
            DrawRect(bgRect, new Color(0.08f, 0.08f, 0.1f, 0.85f));

            GUI.Label(new Rect(x, y - 30, 200, 25), "PEDAL TELEMETRY", titleStyle);

            float clutchVal = input != null ? input.Clutch : 0f;
            float brakeVal = input != null ? input.Brake : 0f;
            float gasVal = input != null ? input.Throttle : 0f;

            // 1. CLUTCH PEDAL (Left)
            DrawPedalBar(x, y, "CLUTCH", clutchVal, new Color(0.2f, 0.6f, 1f), true);

            // 2. BRAKE PEDAL (Middle)
            DrawPedalBar(x + pedalSize.x + spacing, y, "BRAKE", brakeVal, new Color(1f, 0.25f, 0.25f), false);

            // 3. GAS PEDAL (Right)
            DrawPedalBar(x + (pedalSize.x * 2) + (spacing * 2), y, "GAS", gasVal, new Color(0.25f, 0.9f, 0.35f), false);

            // Clutch State String below
            string clutchStatus = "CLUTCH ENGAGED (Foot Off)";
            Color statusColor = Color.green;

            if (transmission != null)
            {
                if (clutchVal > transmission.bitePointEnd)
                {
                    clutchStatus = "DISENGAGED (Free)";
                    statusColor = new Color(0.5f, 0.8f, 1f);
                }
                else if (clutchVal >= transmission.bitePointStart)
                {
                    clutchStatus = "FRICTION BITE POINT!";
                    statusColor = Color.yellow;
                }
            }

            GUI.color = statusColor;
            GUI.Label(new Rect(x, y + pedalSize.y + 12, bgRect.width - 24, 25), clutchStatus, labelStyle);
            GUI.color = Color.white;
        }

        private void DrawPedalBar(float px, float py, string name, float fillPercent, Color barColor, bool isClutch)
        {
            Rect barOuter = new Rect(px, py, pedalSize.x, pedalSize.y);
            // Outer frame
            DrawRect(barOuter, new Color(0.15f, 0.15f, 0.18f, 1f));

            // Bite point zone highlight on clutch bar
            if (isClutch && transmission != null)
            {
                float biteStartNorm = transmission.bitePointStart;
                float biteEndNorm = transmission.bitePointEnd;
                float biteTop = py + (1f - biteEndNorm) * pedalSize.y;
                float biteHeight = (biteEndNorm - biteStartNorm) * pedalSize.y;
                DrawRect(new Rect(px, biteTop, pedalSize.x, biteHeight), new Color(1f, 0.85f, 0.2f, 0.35f));
            }

            // Fill rect (rises from bottom)
            float fillHeight = pedalSize.y * Mathf.Clamp01(fillPercent);
            float fillY = py + (pedalSize.y - fillHeight);
            DrawRect(new Rect(px + 2, fillY, pedalSize.x - 4, fillHeight), barColor);

            // Labels
            GUI.Label(new Rect(px - 10, py - 20, pedalSize.x + 20, 20), name, labelStyle);
            GUI.Label(new Rect(px - 10, py + pedalSize.y + 2, pedalSize.x + 20, 20), $"{(int)(fillPercent * 100)}%", labelStyle);
        }

        private void DrawRect(Rect rect, Color color)
        {
            Color oldColor = GUI.color;
            GUI.color = color;
            GUI.DrawTexture(rect, whiteTex);
            GUI.color = oldColor;
        }

        private void InitStyles()
        {
            if (titleStyle == null)
            {
                titleStyle = new GUIStyle(GUI.skin.label);
                titleStyle.fontSize = 11;
                titleStyle.fontStyle = FontStyle.Bold;
                titleStyle.normal.textColor = new Color(0.85f, 0.85f, 0.9f);
            }
            if (labelStyle == null)
            {
                labelStyle = new GUIStyle(GUI.skin.label);
                labelStyle.fontSize = 10;
                labelStyle.alignment = TextAnchor.MiddleCenter;
                labelStyle.normal.textColor = Color.white;
            }
        }
    }
}
