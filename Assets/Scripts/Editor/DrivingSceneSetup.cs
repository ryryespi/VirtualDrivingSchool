#if UNITY_EDITOR
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using VirtualDrivingSchool.Audio;
using VirtualDrivingSchool.Cockpit;
using VirtualDrivingSchool.Environment;
using VirtualDrivingSchool.Input;
using VirtualDrivingSchool.Lessons;
using VirtualDrivingSchool.UI;
using VirtualDrivingSchool.Vehicle;

namespace VirtualDrivingSchool.Editor
{
    /// <summary>
    /// One-click setup tool to generate the complete 3D Driving Simulator scene
    /// with physics vehicle, Logitech G920 controller bindings, cockpit, mirrors,
    /// audio, and training environments.
    /// </summary>
    public static class DrivingSceneSetup
    {
        [MenuItem("Driving Simulator/Build Complete Driving School Scene")]
        public static void BuildCompleteScene()
        {
            Scene scene = EditorSceneManager.NewScene(NewSceneSetup.DefaultGameObjects, NewSceneMode.Single);

            // 1. Setup Lighting
            Light sun = Object.FindFirstObjectByType<Light>();
            if (sun != null)
            {
                sun.type = LightType.Directional;
                sun.transform.rotation = Quaternion.Euler(45f, -30f, 0f);
                sun.intensity = 1.2f;
                sun.shadows = LightShadows.Soft;
            }

            // 2. Setup Environment Generator
            GameObject worldGenObj = new GameObject("Environment_Manager");
            WorldGenerator worldGen = worldGenObj.AddComponent<WorldGenerator>();

            // 3. Setup Driving Lesson Manager
            GameObject lessonObj = new GameObject("Lesson_Manager");
            DrivingLessonManager lessonManager = lessonObj.AddComponent<DrivingLessonManager>();

            // 4. Setup Input Manager
            GameObject inputObj = new GameObject("Input_Manager");
            LogitechG920Manager inputManager = inputObj.AddComponent<LogitechG920Manager>();

            // 5. Construct Complete 3D Car
            GameObject car = BuildCarGameObject(inputManager);

            // 6. Connect references
            lessonManager.car = car.GetComponent<ManualCarController>();
            lessonManager.engine = car.GetComponent<EngineSimulation>();
            lessonManager.transmission = car.GetComponent<Transmission>();
            lessonManager.input = inputManager;

            // 7. Setup Camera Controller
            SetupCameras(car);

            // 8. Setup UI Canvas / Overlay
            GameObject uiObj = new GameObject("UI_DrivingSchoolHUD");
            DrivingInstructorUI instructorUI = uiObj.AddComponent<DrivingInstructorUI>();
            instructorUI.lessonManager = lessonManager;
            instructorUI.inputManager = inputManager;

            // Save Scene
            string scenePath = "Assets/Scenes/DrivingSchool_Main.unity";
            EditorSceneManager.SaveScene(scene, scenePath);
            Debug.Log($"<color=green><b>Driving Simulator Scene successfully built and saved to: {scenePath}</b></color>");
        }

        private static GameObject BuildCarGameObject(LogitechG920Manager inputManager)
        {
            GameObject car = new GameObject("Player_ManualCar");
            car.tag = "Player";
            car.transform.position = new Vector3(-8f, 0.45f, 2f);
            car.transform.rotation = Quaternion.identity;

            Rigidbody rb = car.AddComponent<Rigidbody>();
            rb.mass = 1350f; // Typical manual compact/sedan mass
            rb.linearDamping = 0.05f;
            rb.angularDamping = 0.1f;
            rb.interpolation = RigidbodyInterpolation.Interpolate;

            // Main Car Body Collider & Mesh
            GameObject bodyVisual = GameObject.CreatePrimitive(PrimitiveType.Cube);
            bodyVisual.name = "Body_Mesh";
            bodyVisual.transform.SetParent(car.transform, false);
            bodyVisual.transform.localPosition = new Vector3(0f, 0.4f, 0f);
            bodyVisual.transform.localScale = new Vector3(1.85f, 0.7f, 4.3f);

            // Cabin / Roof
            GameObject cabinVisual = GameObject.CreatePrimitive(PrimitiveType.Cube);
            cabinVisual.name = "Cabin_Mesh";
            cabinVisual.transform.SetParent(car.transform, false);
            cabinVisual.transform.localPosition = new Vector3(0f, 0.95f, -0.2f);
            cabinVisual.transform.localScale = new Vector3(1.5f, 0.6f, 2.2f);

            // Materials
            Material carPaintMat = new Material(Shader.Find("Universal Render Pipeline/Lit"));
            carPaintMat.color = new Color(0.85f, 0.15f, 0.15f); // Cherry Red
            bodyVisual.GetComponent<Renderer>().material = carPaintMat;
            cabinVisual.GetComponent<Renderer>().material = carPaintMat;

            // Steering Wheel Mesh (inside cockpit)
            GameObject steerWheel = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            steerWheel.name = "SteeringWheel_Mesh";
            steerWheel.transform.SetParent(car.transform, false);
            steerWheel.transform.localPosition = new Vector3(-0.4f, 0.85f, 0.45f);
            steerWheel.transform.localRotation = Quaternion.Euler(65f, 0f, 0f);
            steerWheel.transform.localScale = new Vector3(0.38f, 0.04f, 0.38f);
            Object.DestroyImmediate(steerWheel.GetComponent<Collider>());

            // Interior Rear View Mirror
            GameObject mirrorCenter = GameObject.CreatePrimitive(PrimitiveType.Cube);
            mirrorCenter.name = "Mirror_InteriorCenter";
            mirrorCenter.transform.SetParent(car.transform, false);
            mirrorCenter.transform.localPosition = new Vector3(0f, 1.15f, 0.5f);
            mirrorCenter.transform.localRotation = Quaternion.Euler(0f, 180f, 0f);
            mirrorCenter.transform.localScale = new Vector3(0.25f, 0.08f, 0.02f);
            Object.DestroyImmediate(mirrorCenter.GetComponent<Collider>());
            MirrorCamera mirrorCamComp = mirrorCenter.AddComponent<MirrorCamera>();
            mirrorCamComp.mirrorType = MirrorCamera.MirrorType.InteriorRearView;
            mirrorCamComp.mirrorGlassRenderer = mirrorCenter.GetComponent<Renderer>();

            // Left Side Mirror
            GameObject mirrorLeft = GameObject.CreatePrimitive(PrimitiveType.Cube);
            mirrorLeft.name = "Mirror_DriverLeft";
            mirrorLeft.transform.SetParent(car.transform, false);
            mirrorLeft.transform.localPosition = new Vector3(-1.0f, 0.85f, 0.7f);
            mirrorLeft.transform.localRotation = Quaternion.Euler(0f, 175f, 0f);
            mirrorLeft.transform.localScale = new Vector3(0.18f, 0.10f, 0.03f);
            Object.DestroyImmediate(mirrorLeft.GetComponent<Collider>());
            MirrorCamera mirrorLeftComp = mirrorLeft.AddComponent<MirrorCamera>();
            mirrorLeftComp.mirrorType = MirrorCamera.MirrorType.DriverSideLeft;
            mirrorLeftComp.mirrorGlassRenderer = mirrorLeft.GetComponent<Renderer>();

            // Setup 4 Wheel Colliders & Visuals
            WheelAxle frontAxle = CreateAxle(car, "Front", 1.3f, 0.82f, true, true);
            WheelAxle rearAxle = CreateAxle(car, "Rear", -1.3f, 0.82f, true, false);

            // Attach Car Subsystems
            EngineSimulation engine = car.AddComponent<EngineSimulation>();
            Transmission transmission = car.AddComponent<Transmission>();
            ManualCarController carController = car.AddComponent<ManualCarController>();

            carController.engine = engine;
            carController.transmission = transmission;
            carController.input = inputManager;
            carController.frontAxle = frontAxle;
            carController.rearAxle = rearAxle;

            // Audio & HUD
            car.AddComponent<ProceduralCarAudio>();
            DashboardUI dash = car.AddComponent<DashboardUI>();
            dash.car = carController;
            dash.engine = engine;
            dash.transmission = transmission;
            dash.input = inputManager;

            PedalVisualizer pedals = car.AddComponent<PedalVisualizer>();
            pedals.input = inputManager;
            pedals.transmission = transmission;
            pedals.engine = engine;

            return car;
        }

        private static WheelAxle CreateAxle(GameObject car, string name, float zPos, float xOffset, bool isMotor, bool isSteering)
        {
            WheelAxle axle = new WheelAxle();
            axle.isMotor = isMotor;
            axle.isSteering = isSteering;

            // Left Wheel
            GameObject leftObj = new GameObject($"Wheel_{name}_Left");
            leftObj.transform.SetParent(car.transform, false);
            leftObj.transform.localPosition = new Vector3(-xOffset, 0.35f, zPos);
            WheelCollider leftCol = leftObj.AddComponent<WheelCollider>();
            leftCol.radius = 0.35f;
            leftCol.suspensionDistance = 0.18f;

            JointSpring spr = leftCol.suspensionSpring;
            spr.spring = 32000f;
            spr.damper = 3500f;
            spr.targetPosition = 0.5f;
            leftCol.suspensionSpring = spr;

            GameObject leftVisual = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            leftVisual.name = "Mesh";
            leftVisual.transform.SetParent(leftObj.transform, false);
            leftVisual.transform.localRotation = Quaternion.Euler(0f, 0f, 90f);
            leftVisual.transform.localScale = new Vector3(0.7f, 0.12f, 0.7f);
            Object.DestroyImmediate(leftVisual.GetComponent<Collider>());
            axle.leftWheel = leftCol;
            axle.leftVisualMesh = leftVisual.transform;

            // Right Wheel
            GameObject rightObj = new GameObject($"Wheel_{name}_Right");
            rightObj.transform.SetParent(car.transform, false);
            rightObj.transform.localPosition = new Vector3(xOffset, 0.35f, zPos);
            WheelCollider rightCol = rightObj.AddComponent<WheelCollider>();
            rightCol.radius = 0.35f;
            rightCol.suspensionDistance = 0.18f;
            rightCol.suspensionSpring = spr;

            GameObject rightVisual = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            rightVisual.name = "Mesh";
            rightVisual.transform.SetParent(rightObj.transform, false);
            rightVisual.transform.localRotation = Quaternion.Euler(0f, 0f, 90f);
            rightVisual.transform.localScale = new Vector3(0.7f, 0.12f, 0.7f);
            Object.DestroyImmediate(rightVisual.GetComponent<Collider>());
            axle.rightWheel = rightCol;
            axle.rightVisualMesh = rightVisual.transform;

            return axle;
        }

        private static void SetupCameras(GameObject car)
        {
            Camera mainCam = Camera.main;
            if (mainCam == null)
            {
                GameObject camObj = new GameObject("MainCamera");
                mainCam = camObj.AddComponent<Camera>();
                camObj.AddComponent<AudioListener>();
            }

            // Driver Seat Head Position (Left hand drive, typical for Logitech G920)
            GameObject driverHead = new GameObject("Anchor_DriverHead");
            driverHead.transform.SetParent(car.transform, false);
            driverHead.transform.localPosition = new Vector3(-0.4f, 1.05f, 0.05f);

            // Chase Camera Anchor
            GameObject chaseAnchor = new GameObject("Anchor_Chase");
            chaseAnchor.transform.SetParent(car.transform, false);
            chaseAnchor.transform.localPosition = new Vector3(0f, 2.2f, -5.5f);

            // Hood Camera Anchor
            GameObject hoodAnchor = new GameObject("Anchor_Hood");
            hoodAnchor.transform.SetParent(car.transform, false);
            hoodAnchor.transform.localPosition = new Vector3(0f, 0.9f, 1.2f);

            // Overhead Parking Anchor
            GameObject overheadAnchor = new GameObject("Anchor_Overhead");
            overheadAnchor.transform.SetParent(car.transform, false);
            overheadAnchor.transform.localPosition = new Vector3(0f, 14f, 0f);
            overheadAnchor.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);

            CockpitCameraController camCtl = mainCam.gameObject.AddComponent<CockpitCameraController>();
            camCtl.vehicleTransform = car.transform;
            camCtl.targetCamera = mainCam;
            camCtl.cockpitDriverHeadAnchor = driverHead.transform;
            camCtl.chaseAnchor = chaseAnchor.transform;
            camCtl.hoodAnchor = hoodAnchor.transform;
            camCtl.overheadAnchor = overheadAnchor.transform;
            camCtl.currentMode = CameraViewMode.CockpitInterior;
        }
    }
}
#endif
