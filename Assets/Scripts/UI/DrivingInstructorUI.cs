using UnityEngine;
using VirtualDrivingSchool.Input;
using VirtualDrivingSchool.Lessons;

namespace VirtualDrivingSchool.UI
{
    /// <summary>
    /// Renders the driving school instructor HUD, lesson objectives checklist,
    /// voice feedback banner, mistake penalty counters, and lesson switcher.
    /// </summary>
    public class DrivingInstructorUI : MonoBehaviour
    {
        [Header("References")]
        public DrivingLessonManager lessonManager;
        public LogitechG920Manager inputManager;

        [Header("UI Dimensions")]
        public Vector2 panelSize = new Vector2(400, 240);
        public bool showLessonMenu = false;
        public bool showCalibrationModal = false;

        private Texture2D whiteTex;
        private GUIStyle headerStyle;
        private GUIStyle feedbackStyle;
        private GUIStyle objectiveStyle;
        private GUIStyle checkStyle;
        private GUIStyle buttonStyle;

        private void Awake()
        {
            if (lessonManager == null) lessonManager = DrivingLessonManager.Instance;
            if (inputManager == null) inputManager = LogitechG920Manager.Instance;

            whiteTex = new Texture2D(1, 1);
            whiteTex.SetPixel(0, 0, Color.white);
            whiteTex.Apply();
        }

        private void OnGUI()
        {
            if (lessonManager == null) lessonManager = DrivingLessonManager.Instance;
            if (inputManager == null) inputManager = LogitechG920Manager.Instance;

            InitStyles();

            // 1. TOP-LEFT INSTRUCTOR CARD
            DrawInstructorPanel();

            // 2. PENALTY SCOREBOARD (Top Right)
            DrawScoreboard();

            // 3. TOP ACTION BAR (Lesson Select, Calibrate G920, Restart)
            DrawActionBar();

            // 4. LESSON SELECTION MODAL
            if (showLessonMenu)
            {
                DrawLessonSelectorModal();
            }

            // 5. G920 CALIBRATION MODAL
            if (showCalibrationModal)
            {
                DrawCalibrationModal();
            }

            // 6. LESSON COMPLETION BANNER
            if (lessonManager != null && lessonManager.isLessonComplete)
            {
                DrawCompletionBanner();
            }
        }

        private void DrawInstructorPanel()
        {
            float x = 20f;
            float y = 20f;

            // Panel Background
            Rect panel = new Rect(x, y, panelSize.x, panelSize.y);
            DrawRect(panel, new Color(0.07f, 0.08f, 0.12f, 0.92f));
            DrawOutline(panel, new Color(0.2f, 0.45f, 0.8f, 0.8f), 2);

            // Title Header
            string lessonName = FormatLessonTitle(lessonManager.currentLesson);
            GUI.Label(new Rect(x + 15, y + 10, panelSize.x - 30, 28), lessonName, headerStyle);

            // Instructor Feedback Box
            Rect feedbackBox = new Rect(x + 12, y + 42, panelSize.x - 24, 52);
            DrawRect(feedbackBox, new Color(0.12f, 0.16f, 0.22f, 0.95f));
            DrawOutline(feedbackBox, new Color(0.3f, 0.6f, 1f, 0.5f), 1);
            GUI.Label(new Rect(feedbackBox.x + 8, feedbackBox.y + 6, feedbackBox.width - 16, feedbackBox.height - 12), $"“{lessonManager.instructorFeedback}”", feedbackStyle);

            // Objectives Checklist
            float objY = y + 104f;
            for (int i = 0; i < lessonManager.currentObjectives.Count; i++)
            {
                var obj = lessonManager.currentObjectives[i];
                bool isCurrent = (i == lessonManager.currentStepIndex);

                Color checkColor = obj.isCompleted ? Color.green : (isCurrent ? Color.yellow : new Color(0.5f, 0.5f, 0.5f));
                GUI.color = checkColor;
                string checkIcon = obj.isCompleted ? "[✓]" : (isCurrent ? "[►]" : "[ ]");
                GUI.Label(new Rect(x + 15, objY, 30, 22), checkIcon, checkStyle);

                GUI.color = isCurrent ? Color.white : (obj.isCompleted ? new Color(0.7f, 0.9f, 0.7f) : new Color(0.6f, 0.6f, 0.6f));
                GUI.Label(new Rect(x + 48, objY, panelSize.x - 60, 22), obj.instructionText, objectiveStyle);
                GUI.color = Color.white;

                objY += 26f;
            }
        }

        private void DrawScoreboard()
        {
            float w = 210f;
            float h = 100f;
            float x = Screen.width - w - 20f;
            float y = 20f;

            Rect rect = new Rect(x, y, w, h);
            DrawRect(rect, new Color(0.08f, 0.08f, 0.1f, 0.88f));
            DrawOutline(rect, new Color(0.3f, 0.3f, 0.35f, 0.7f), 1);

            GUI.Label(new Rect(x + 12, y + 8, w - 24, 20), "DRIVING FAULTS", headerStyle);

            GUI.color = (lessonManager.stallCount > 0) ? Color.red : Color.white;
            GUI.Label(new Rect(x + 12, y + 32, w - 24, 18), $"Engine Stalls: {lessonManager.stallCount}", objectiveStyle);

            GUI.color = (lessonManager.coneKnockdowns > 0) ? Color.yellow : Color.white;
            GUI.Label(new Rect(x + 12, y + 52, w - 24, 18), $"Cone Hits: {lessonManager.coneKnockdowns}", objectiveStyle);

            GUI.color = (lessonManager.curbStrikes > 0) ? Color.red : Color.white;
            GUI.Label(new Rect(x + 12, y + 72, w - 24, 18), $"Curb Strikes: {lessonManager.curbStrikes}", objectiveStyle);
            GUI.color = Color.white;
        }

        private void DrawActionBar()
        {
            float x = panelSize.x + 40f;
            float y = 20f;

            if (GUI.Button(new Rect(x, y, 130, 32), "Switch Lesson", buttonStyle))
            {
                showLessonMenu = !showLessonMenu;
                showCalibrationModal = false;
            }

            if (GUI.Button(new Rect(x + 140, y, 140, 32), "G920 Setup / Invert", buttonStyle))
            {
                showCalibrationModal = !showCalibrationModal;
                showLessonMenu = false;
            }

            if (GUI.Button(new Rect(x + 290, y, 100, 32), "Restart (R)", buttonStyle))
            {
                lessonManager.RestartCurrentLesson();
            }
        }

        private void DrawLessonSelectorModal()
        {
            float w = 340f;
            float h = 330f;
            float x = (Screen.width - w) * 0.5f;
            float y = (Screen.height - h) * 0.5f;

            Rect modal = new Rect(x, y, w, h);
            DrawRect(modal, new Color(0.06f, 0.08f, 0.12f, 0.98f));
            DrawOutline(modal, new Color(0.2f, 0.5f, 0.9f), 2);

            GUI.Label(new Rect(x + 20, y + 15, w - 40, 25), "SELECT DRIVING LESSON", headerStyle);

            LessonType[] lessons = (LessonType[])System.Enum.GetValues(typeof(LessonType));
            float btnY = y + 50f;

            foreach (var l in lessons)
            {
                string label = FormatLessonTitle(l);
                if (GUI.Button(new Rect(x + 20, btnY, w - 40, 32), label, buttonStyle))
                {
                    lessonManager.LoadLesson(l);
                    showLessonMenu = false;
                }
                btnY += 38f;
            }
        }

        private void DrawCalibrationModal()
        {
            float w = 380f;
            float h = 260f;
            float x = (Screen.width - w) * 0.5f;
            float y = (Screen.height - h) * 0.5f;

            Rect modal = new Rect(x, y, w, h);
            DrawRect(modal, new Color(0.06f, 0.08f, 0.12f, 0.98f));
            DrawOutline(modal, new Color(0.9f, 0.6f, 0.2f), 2);

            GUI.Label(new Rect(x + 20, y + 15, w - 40, 25), "LOGITECH G920 PEDAL CALIBRATION", headerStyle);

            GUI.Label(new Rect(x + 20, y + 45, w - 40, 35), "Logitech pedals typically report reversed axis values on PC. Use these quick presets if pedals stick to 100%:", objectiveStyle);

            if (GUI.Button(new Rect(x + 20, y + 90, w - 40, 32), "Preset: Inverted Pedals (Standard G920)", buttonStyle))
            {
                inputManager.SetPedalInversion(true, true, true);
                showCalibrationModal = false;
            }

            if (GUI.Button(new Rect(x + 20, y + 130, w - 40, 32), "Preset: Normal Pedals (Direct 0-1)", buttonStyle))
            {
                inputManager.SetPedalInversion(false, false, false);
                showCalibrationModal = false;
            }

            if (GUI.Button(new Rect(x + 20, y + 170, w - 40, 32), "Toggle Keyboard Mode", buttonStyle))
            {
                inputManager.ToggleForceKeyboardMode();
                showCalibrationModal = false;
            }

            if (GUI.Button(new Rect(x + 20, y + 215, w - 40, 28), "Close", buttonStyle))
            {
                showCalibrationModal = false;
            }
        }

        private void DrawCompletionBanner()
        {
            float w = 500f;
            float h = 130f;
            float x = (Screen.width - w) * 0.5f;
            float y = 140f;

            Rect banner = new Rect(x, y, w, h);
            DrawRect(banner, new Color(0.05f, 0.35f, 0.15f, 0.95f));
            DrawOutline(banner, Color.green, 3);

            GUI.Label(new Rect(x, y + 15, w, 32), "★ LESSON PASSED! ★", headerStyle);
            GUI.Label(new Rect(x + 20, y + 50, w - 40, 30), lessonManager.instructorFeedback, feedbackStyle);

            if (GUI.Button(new Rect(x + 150, y + 85, 200, 32), "Next Lesson / Continue", buttonStyle))
            {
                int nextIdx = ((int)lessonManager.currentLesson + 1) % System.Enum.GetValues(typeof(LessonType)).Length;
                lessonManager.LoadLesson((LessonType)nextIdx);
            }
        }

        private string FormatLessonTitle(LessonType lesson)
        {
            switch (lesson)
            {
                case LessonType.Lesson1_ClutchBitePoint: return "Lesson 1: Clutch Bite Point & Starting";
                case LessonType.Lesson2_SmoothShifting: return "Lesson 2: Upshifting & Downshifting";
                case LessonType.Lesson3_ReverseAndBacking: return "Lesson 3: Reversing & Mirror Backing";
                case LessonType.Lesson4_PerpendicularParking: return "Lesson 4: 90° Bay Parking";
                case LessonType.Lesson5_ParallelParking: return "Lesson 5: Parallel Parking";
                case LessonType.Lesson6_HillStart: return "Lesson 6: Incline / Hill Start";
                case LessonType.FreeRoam: return "Free Roam & Practice";
                default: return lesson.ToString();
            }
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
            if (headerStyle == null)
            {
                headerStyle = new GUIStyle(GUI.skin.label);
                headerStyle.fontSize = 14;
                headerStyle.fontStyle = FontStyle.Bold;
                headerStyle.normal.textColor = Color.white;
            }
            if (feedbackStyle == null)
            {
                feedbackStyle = new GUIStyle(GUI.skin.label);
                feedbackStyle.fontSize = 11;
                feedbackStyle.fontStyle = FontStyle.Italic;
                feedbackStyle.wordWrap = true;
                feedbackStyle.normal.textColor = new Color(0.9f, 0.95f, 1f);
            }
            if (objectiveStyle == null)
            {
                objectiveStyle = new GUIStyle(GUI.skin.label);
                objectiveStyle.fontSize = 11;
                objectiveStyle.wordWrap = false;
                objectiveStyle.normal.textColor = Color.white;
            }
            if (checkStyle == null)
            {
                checkStyle = new GUIStyle(GUI.skin.label);
                checkStyle.fontSize = 12;
                checkStyle.fontStyle = FontStyle.Bold;
            }
            if (buttonStyle == null)
            {
                buttonStyle = new GUIStyle(GUI.skin.button);
                buttonStyle.fontSize = 11;
                buttonStyle.fontStyle = FontStyle.Bold;
            }
        }
    }
}
