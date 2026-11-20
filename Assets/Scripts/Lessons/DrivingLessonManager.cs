using System;
using System.Collections.Generic;
using UnityEngine;
using VirtualDrivingSchool.Input;
using VirtualDrivingSchool.Vehicle;

namespace VirtualDrivingSchool.Lessons
{
    public enum LessonType
    {
        Lesson1_ClutchBitePoint,
        Lesson2_SmoothShifting,
        Lesson3_ReverseAndBacking,
        Lesson4_PerpendicularParking,
        Lesson5_ParallelParking,
        Lesson6_HillStart,
        FreeRoam
    }

    [System.Serializable]
    public class LessonObjective
    {
        public string title;
        public string instructionText;
        public bool isCompleted;
    }

    /// <summary>
    /// Manages the structured driving lessons, evaluating the driver's clutch work,
    /// shifting, parking alignment, hill starts, and reversing accuracy.
    /// </summary>
    public class DrivingLessonManager : MonoBehaviour
    {
        public static DrivingLessonManager Instance { get; private set; }

        [Header("Current Lesson")]
        public LessonType currentLesson = LessonType.Lesson1_ClutchBitePoint;
        public int currentStepIndex = 0;
        public List<LessonObjective> currentObjectives = new List<LessonObjective>();

        [Header("Vehicle & Input References")]
        public ManualCarController car;
        public EngineSimulation engine;
        public Transmission transmission;
        public LogitechG920Manager input;

        [Header("Telemetry & Scoring")]
        public int stallCount = 0;
        public int coneKnockdowns = 0;
        public int curbStrikes = 0;
        public float lessonTimer = 0f;
        public bool isLessonComplete = false;
        public string instructorFeedback = "";

        // Events
        public event Action OnLessonStateChanged;

        private float targetProgressTimer = 0f;
        private Vector3 startPosition;

        private void Awake()
        {
            if (Instance == null) Instance = this;
            else Destroy(gameObject);

            if (car == null) car = FindFirstObjectByType<ManualCarController>();
            if (engine == null && car != null) engine = car.engine;
            if (transmission == null && car != null) transmission = car.transmission;
            if (input == null) input = LogitechG920Manager.Instance;
        }

        private void Start()
        {
            if (engine != null)
            {
                engine.OnEngineStalled += HandleEngineStalled;
            }
            startPosition = car != null ? car.transform.position : Vector3.zero;
            LoadLesson(currentLesson);
        }

        private void Update()
        {
            if (isLessonComplete) return;

            lessonTimer += Time.deltaTime;
            EvaluateCurrentLessonStep();
        }

        public void LoadLesson(LessonType lesson)
        {
            currentLesson = lesson;
            currentStepIndex = 0;
            lessonTimer = 0f;
            isLessonComplete = false;
            stallCount = 0;
            currentObjectives.Clear();

            switch (lesson)
            {
                case LessonType.Lesson1_ClutchBitePoint:
                    currentObjectives.Add(new LessonObjective { title = "Step 1: Start Ignition", instructionText = "Push clutch fully down (or neutral) and hold Start (Key 'E' or Wheel Button 2)." });
                    currentObjectives.Add(new LessonObjective { title = "Step 2: Engage 1st Gear", instructionText = "Keep clutch fully depressed and shift into 1st Gear." });
                    currentObjectives.Add(new LessonObjective { title = "Step 3: Find Bite Point", instructionText = "Slowly release clutch into the gold Bite Zone until car creeps forward." });
                    currentObjectives.Add(new LessonObjective { title = "Step 4: Accelerate & Stop", instructionText = "Add gentle gas, fully release clutch, drive 20 meters and brake to a stop." });
                    instructorFeedback = "Welcome! Manual driving requires feeling the clutch bite point. Let's begin by starting the engine.";
                    break;

                case LessonType.Lesson2_SmoothShifting:
                    currentObjectives.Add(new LessonObjective { title = "Step 1: Launch in 1st", instructionText = "Accelerate in 1st gear up to 15 MPH." });
                    currentObjectives.Add(new LessonObjective { title = "Step 2: Upshift to 2nd", instructionText = "Depress clutch, shift to 2nd, and smoothly release clutch." });
                    currentObjectives.Add(new LessonObjective { title = "Step 3: Cruise & Downshift", instructionText = "Reach 25 MPH, then brake gently and downshift back to Neutral." });
                    instructorFeedback = "Practice gear synchronization: remember to release throttle when pressing the clutch to shift!";
                    break;

                case LessonType.Lesson3_ReverseAndBacking:
                    currentObjectives.Add(new LessonObjective { title = "Step 1: Select Reverse", instructionText = "Push down shifter into 6th gear position for Reverse (or key 'R')." });
                    currentObjectives.Add(new LessonObjective { title = "Step 2: Modulate Clutch", instructionText = "Use mirror cameras to back up slowly down the cone alley." });
                    currentObjectives.Add(new LessonObjective { title = "Step 3: Stop in Bay", instructionText = "Stop completely inside the marked reverse parking bay." });
                    instructorFeedback = "In reverse, keep your speed under 5 MPH by slipping the clutch in the friction zone.";
                    break;

                case LessonType.Lesson4_PerpendicularParking:
                    currentObjectives.Add(new LessonObjective { title = "Step 1: Approach Bay", instructionText = "Drive at low speed toward the designated parking bay." });
                    currentObjectives.Add(new LessonObjective { title = "Step 2: Center Inside Lines", instructionText = "Turn wheel smoothly and drive straight between the white parking lines." });
                    currentObjectives.Add(new LessonObjective { title = "Step 3: Secure Vehicle", instructionText = "Come to a complete stop, shift to Neutral, and engage Handbrake." });
                    instructorFeedback = "Perpendicular parking: take a wide swing and center the car between the stall lines.";
                    break;

                case LessonType.Lesson5_ParallelParking:
                    currentObjectives.Add(new LessonObjective { title = "Step 1: Pull Alongside Car", instructionText = "Align next to the front obstacle vehicle 1 meter away." });
                    currentObjectives.Add(new LessonObjective { title = "Step 2: Reverse 45° Angle", instructionText = "Clutch in, select Reverse, cut wheel hard right until 45° to the curb." });
                    currentObjectives.Add(new LessonObjective { title = "Step 3: Counter-Steer & Park", instructionText = "Counter-steer left into the stall, straighten up within 30cm of the curb." });
                    instructorFeedback = "Parallel parking: watch the passenger side mirror to judge curb distance.";
                    break;

                case LessonType.Lesson6_HillStart:
                    currentObjectives.Add(new LessonObjective { title = "Step 1: Hold on Incline", instructionText = "Stop on the steep hill and pull the Handbrake (Space / Button A)." });
                    currentObjectives.Add(new LessonObjective { title = "Step 2: Pre-Load Clutch", instructionText = "Shift to 1st, bring clutch to bite point until engine note deepens." });
                    currentObjectives.Add(new LessonObjective { title = "Step 3: Launch Without Rollback", instructionText = "Release Handbrake as you add throttle. Climb hill with zero rollback!" });
                    instructorFeedback = "The dreaded hill start! The handbrake holds the car while you find the clutch bite point.";
                    break;

                case LessonType.FreeRoam:
                    currentObjectives.Add(new LessonObjective { title = "Free Exploration", instructionText = "Drive freely through the parking lot, road network, and obstacles." });
                    instructorFeedback = "Free practice mode. Practice parking, shifting, and road rules at your leisure.";
                    break;
            }

            OnLessonStateChanged?.Invoke();
        }

        private void EvaluateCurrentLessonStep()
        {
            if (car == null || engine == null || transmission == null) return;

            switch (currentLesson)
            {
                case LessonType.Lesson1_ClutchBitePoint:
                    EvaluateLesson1();
                    break;
                case LessonType.Lesson2_SmoothShifting:
                    EvaluateLesson2();
                    break;
                case LessonType.Lesson3_ReverseAndBacking:
                    EvaluateLesson3();
                    break;
                case LessonType.Lesson4_PerpendicularParking:
                    EvaluateLesson4();
                    break;
                case LessonType.Lesson5_ParallelParking:
                    EvaluateLesson5();
                    break;
                case LessonType.Lesson6_HillStart:
                    EvaluateLesson6();
                    break;
            }
        }

        private void EvaluateLesson1()
        {
            // Step 0: Start Ignition
            if (currentStepIndex == 0)
            {
                if (engine.isRunning)
                {
                    CompleteStep(0, "Engine started smoothly! Now press the clutch and select 1st gear.");
                }
            }
            // Step 1: Engage 1st Gear
            else if (currentStepIndex == 1)
            {
                if (transmission.currentGear == 1 && input != null && input.Clutch > 0.7f)
                {
                    CompleteStep(1, "1st gear selected! Slowly ease the clutch up to the friction zone.");
                }
            }
            // Step 2: Find Bite Point & Creep Forward
            else if (currentStepIndex == 2)
            {
                if (car.SpeedMph > 2f && engine.isRunning)
                {
                    CompleteStep(2, "Excellent! You found the bite point! Now add gentle gas and release clutch.");
                }
            }
            // Step 3: Drive and Stop
            else if (currentStepIndex == 3)
            {
                float distTraveled = Vector3.Distance(car.transform.position, startPosition);
                if (distTraveled > 18f && Mathf.Abs(car.SpeedMph) < 0.5f)
                {
                    CompleteStep(3, "Lesson 1 Complete! Perfect clutch launch without stalling!");
                    FinishLesson();
                }
            }
        }

        private void EvaluateLesson2()
        {
            if (currentStepIndex == 0)
            {
                if (transmission.currentGear == 1 && car.SpeedMph > 14f)
                {
                    CompleteStep(0, "Speed reached! Clutch in and shift to 2nd gear.");
                }
            }
            else if (currentStepIndex == 1)
            {
                if (transmission.currentGear == 2 && transmission.clutchEngagement > 0.8f)
                {
                    CompleteStep(1, "Clean upshift to 2nd! Accelerate to 25 MPH, then brake to a stop.");
                }
            }
            else if (currentStepIndex == 2)
            {
                if (car.SpeedMph > 22f)
                {
                    targetProgressTimer += Time.deltaTime;
                }
                if (targetProgressTimer > 1.5f && Mathf.Abs(car.SpeedMph) < 1f && transmission.currentGear == 0)
                {
                    CompleteStep(2, "Lesson 2 Complete! Mastered smooth acceleration and braking.");
                    FinishLesson();
                }
            }
        }

        private void EvaluateLesson3()
        {
            if (currentStepIndex == 0)
            {
                if (transmission.currentGear == -1)
                {
                    CompleteStep(0, "Reverse engaged! Use side and rear mirrors to back down the lane.");
                }
            }
            else if (currentStepIndex == 1)
            {
                if (car.SpeedMph < -2f)
                {
                    CompleteStep(1, "Keep backing straight. Watch cone clearance!");
                }
            }
            else if (currentStepIndex == 2)
            {
                // Completed when stopping in parking trigger
            }
        }

        private void EvaluateLesson4()
        {
            // Handled when reaching ParkingBayTrigger
        }

        private void EvaluateLesson5()
        {
            // Handled when parallel parked
        }

        private void EvaluateLesson6()
        {
            if (currentStepIndex == 0)
            {
                if (input != null && input.Handbrake && Mathf.Abs(car.SpeedMph) < 0.5f)
                {
                    CompleteStep(0, "Handbrake set. Now select 1st gear and bring clutch to the bite point.");
                }
            }
            else if (currentStepIndex == 1)
            {
                if (transmission.currentGear == 1 && input != null && input.Clutch > 0.35f && input.Clutch < 0.65f)
                {
                    CompleteStep(1, "Bite point held! Now apply light throttle and release the handbrake!");
                }
            }
            else if (currentStepIndex == 2)
            {
                if (input != null && !input.Handbrake && car.SpeedMph > 3f)
                {
                    CompleteStep(2, "Flawless hill start! Zero rollback!");
                    FinishLesson();
                }
            }
        }

        public void NotifyParkedInBay(float alignmentScore, bool isParallel)
        {
            if (currentLesson == LessonType.Lesson4_PerpendicularParking && !isParallel)
            {
                if (alignmentScore > 75f)
                {
                    instructorFeedback = $"Great 90-degree parking! Alignment: {(int)alignmentScore}%";
                    CompleteStep(currentStepIndex, instructorFeedback);
                    FinishLesson();
                }
                else
                {
                    instructorFeedback = $"Car is crooked ({(int)alignmentScore}%). Straighten up!";
                }
            }
            else if (currentLesson == LessonType.Lesson5_ParallelParking && isParallel)
            {
                if (alignmentScore > 70f)
                {
                    instructorFeedback = $"Superb parallel park! Alignment: {(int)alignmentScore}%";
                    CompleteStep(currentStepIndex, instructorFeedback);
                    FinishLesson();
                }
            }
        }

        public void RecordConeCollision()
        {
            coneKnockdowns++;
            instructorFeedback = "Ouch! You hit a traffic cone! Penalty incurred.";
        }

        public void RecordCurbStrike()
        {
            curbStrikes++;
            instructorFeedback = "Watch out! Curb strike detected! Check your mirrors.";
        }

        private void HandleEngineStalled()
        {
            stallCount++;
            instructorFeedback = "STALL! You let the clutch out too quickly without enough throttle! Push clutch in and restart.";
        }

        private void CompleteStep(int stepIdx, string feedback)
        {
            if (stepIdx < currentObjectives.Count)
            {
                currentObjectives[stepIdx].isCompleted = true;
            }
            currentStepIndex = Mathf.Min(currentStepIndex + 1, currentObjectives.Count - 1);
            instructorFeedback = feedback;
            OnLessonStateChanged?.Invoke();
        }

        private void FinishLesson()
        {
            isLessonComplete = true;
            OnLessonStateChanged?.Invoke();
        }

        public void RestartCurrentLesson()
        {
            if (car != null) car.ResetToSpawn();
            LoadLesson(currentLesson);
        }
    }
}
