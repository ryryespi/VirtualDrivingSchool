using UnityEngine;
using VirtualDrivingSchool.Audio;
using VirtualDrivingSchool.Cockpit;
using VirtualDrivingSchool.Environment;
using VirtualDrivingSchool.Input;
using VirtualDrivingSchool.Lessons;
using VirtualDrivingSchool.UI;
using VirtualDrivingSchool.Vehicle;

namespace VirtualDrivingSchool
{
    /// <summary>
    /// Automatic runtime initializer. If the scene does not yet have
    /// the vehicle, world, or UI wired up, GameBootstrapper spawns everything
    /// seamlessly on Start, making the simulator instantly playable.
    /// </summary>
    public class GameBootstrapper : MonoBehaviour
    {
        [Header("Options")]
        public bool autoSpawnWorld = true;
        public bool autoSpawnVehicle = true;
        public bool autoSetupLighting = true;

        private void Awake()
        {
            if (autoSetupLighting)
            {
                SetupLighting();
            }

            // Ensure Input Manager exists
            LogitechG920Manager input = FindFirstObjectByType<LogitechG920Manager>();
            if (input == null)
            {
                GameObject inputObj = new GameObject("Input_Manager");
                input = inputObj.AddComponent<LogitechG920Manager>();
            }

            // Ensure World Generator exists and generates grounds
            WorldGenerator worldGen = FindFirstObjectByType<WorldGenerator>();
            if (worldGen == null && autoSpawnWorld)
            {
                GameObject worldObj = new GameObject("Environment_Manager");
                worldGen = worldObj.AddComponent<WorldGenerator>();
            }

            // Ensure Car exists
            ManualCarController car = FindFirstObjectByType<ManualCarController>();
            if (car == null && autoSpawnVehicle)
            {
                car = SpawnCompleteVehicle(input);
            }

            // Ensure Driving Lesson Manager exists
            DrivingLessonManager lesson = FindFirstObjectByType<DrivingLessonManager>();
            if (lesson == null)
            {
                GameObject lessonObj = new GameObject("Lesson_Manager");
                lesson = lessonObj.AddComponent<DrivingLessonManager>();
                lesson.car = car;
                lesson.engine = car.engine;
                lesson.transmission = car.transmission;
                lesson.input = input;
            }

            // Ensure HUD UI exists
            DrivingInstructorUI instructorUI = FindFirstObjectByType<DrivingInstructorUI>();
            if (instructorUI == null)
            {
                GameObject uiObj = new GameObject("UI_DrivingSchoolHUD");
                instructorUI = uiObj.AddComponent<DrivingInstructorUI>();
                instructorUI.lessonManager = lesson;
                instructorUI.inputManager = input;
            }
        }

        private void SetupLighting()
        {
            Light sun = FindFirstObjectByType<Light>();
            if (sun == null)
            {
                GameObject sunObj = new GameObject("Directional_Sun");
                sun = sunObj.AddComponent<Light>();
                sun.type = LightType.Directional;
                sun.transform.rotation = Quaternion.Euler(45f, -30f, 0f);
                sun.intensity = 1.2f;
                sun.shadows = LightShadows.Soft;
            }
        }

        private ManualCarController SpawnCompleteVehicle(LogitechG920Manager input)
        {
            GameObject carObj = new GameObject("Player_ManualCar");
            carObj.tag = "Player";
            carObj.transform.position = new Vector3(-8f, 0.45f, 2f);
            carObj.transform.rotation = Quaternion.identity;

            Rigidbody rb = carObj.AddComponent<Rigidbody>();
            rb.mass = 1350f;
            rb.linearDamping = 0.05f;
            rb.angularDamping = 0.1f;
            rb.interpolation = RigidbodyInterpolation.Interpolate;

            // Chassis Mesh
            GameObject body = GameObject.CreatePrimitive(PrimitiveType.Cube);
            body.name = "Body_Mesh";
            body.transform.SetParent(carObj.transform, false);
            body.transform.localPosition = new Vector3(0f, 0.4f, 0f);
            body.transform.localScale = new Vector3(1.85f, 0.7f, 4.3f);

            GameObject cabin = GameObject.CreatePrimitive(PrimitiveType.Cube);
            cabin.name = "Cabin_Mesh";
            cabin.transform.SetParent(carObj.transform, false);
            cabin.transform.localPosition = new Vector3(0f, 0.95f, -0.2f);
            cabin.transform.localScale = new Vector3(1.5f, 0.6f, 2.2f);

            Material carMat = new Material(Shader.Find("Standard"));
            carMat.color = new Color(0.85f, 0.15f, 0.15f);
            body.GetComponent<Renderer>().material = carMat;
            cabin.GetComponent<Renderer>().material = carMat;

            // Steering Wheel Mesh (syncs with G920)
            GameObject wheelMesh = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            wheelMesh.name = "SteeringWheel_Mesh";
            wheelMesh.transform.SetParent(carObj.transform, false);
            wheelMesh.transform.localPosition = new Vector3(-0.4f, 0.85f, 0.45f);
            wheelMesh.transform.localRotation = Quaternion.Euler(65f, 0f, 0f);
            wheelMesh.transform.localScale = new Vector3(0.38f, 0.04f, 0.38f);
            Destroy(wheelMesh.GetComponent<Collider>());

            // Interior Rear View Mirror
            GameObject mirrorCenter = GameObject.CreatePrimitive(PrimitiveType.Cube);
            mirrorCenter.name = "Mirror_InteriorCenter";
            mirrorCenter.transform.SetParent(carObj.transform, false);
            mirrorCenter.transform.localPosition = new Vector3(0f, 1.15f, 0.5f);
            mirrorCenter.transform.localRotation = Quaternion.Euler(0f, 180f, 0f);
            mirrorCenter.transform.localScale = new Vector3(0.25f, 0.08f, 0.02f);
            Destroy(mirrorCenter.GetComponent<Collider>());
            MirrorCamera mirrorCamComp = mirrorCenter.AddComponent<MirrorCamera>();
            mirrorCamComp.mirrorType = MirrorCamera.MirrorType.InteriorRearView;
            mirrorCamComp.mirrorGlassRenderer = mirrorCenter.GetComponent<Renderer>();

            // Driver-side Left Mirror
            GameObject mirrorLeft = GameObject.CreatePrimitive(PrimitiveType.Cube);
            mirrorLeft.name = "Mirror_DriverLeft";
            mirrorLeft.transform.SetParent(carObj.transform, false);
            mirrorLeft.transform.localPosition = new Vector3(-1.0f, 0.85f, 0.7f);
            mirrorLeft.transform.localRotation = Quaternion.Euler(0f, 175f, 0f);
            mirrorLeft.transform.localScale = new Vector3(0.18f, 0.10f, 0.03f);
            Destroy(mirrorLeft.GetComponent<Collider>());
            MirrorCamera mirrorLeftComp = mirrorLeft.AddComponent<MirrorCamera>();
            mirrorLeftComp.mirrorType = MirrorCamera.MirrorType.DriverSideLeft;
            mirrorLeftComp.mirrorGlassRenderer = mirrorLeft.GetComponent<Renderer>();

            // Axles
            WheelAxle frontAxle = CreateAxle(carObj, "Front", 1.3f, 0.82f, true, true);
            WheelAxle rearAxle = CreateAxle(carObj, "Rear", -1.3f, 0.82f, true, false);

            // Car Systems
            EngineSimulation engine = carObj.AddComponent<EngineSimulation>();
            Transmission transmission = carObj.AddComponent<Transmission>();
            ManualCarController carController = carObj.AddComponent<ManualCarController>();

            carController.engine = engine;
            carController.transmission = transmission;
            carController.input = input;
            carController.frontAxle = frontAxle;
            carController.rearAxle = rearAxle;

            // Audio & HUDs
            carObj.AddComponent<ProceduralCarAudio>();
            DashboardUI dash = carObj.AddComponent<DashboardUI>();
            dash.car = carController;
            dash.engine = engine;
            dash.transmission = transmission;
            dash.input = input;

            PedalVisualizer pedals = carObj.AddComponent<PedalVisualizer>();
            pedals.input = input;
            pedals.transmission = transmission;
            pedals.engine = engine;

            SetupCamera(carObj);

            return carController;
        }

        private WheelAxle CreateAxle(GameObject car, string name, float zPos, float xOffset, bool isMotor, bool isSteering)
        {
            WheelAxle axle = new WheelAxle();
            axle.isMotor = isMotor;
            axle.isSteering = isSteering;

            // Left
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
            Destroy(leftVisual.GetComponent<Collider>());
            axle.leftWheel = leftCol;
            axle.leftVisualMesh = leftVisual.transform;

            // Right
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
            Destroy(rightVisual.GetComponent<Collider>());
            axle.rightWheel = rightCol;
            axle.rightVisualMesh = rightVisual.transform;

            return axle;
        }

        private void SetupCamera(GameObject car)
        {
            Camera mainCam = Camera.main;
            if (mainCam == null)
            {
                GameObject camObj = new GameObject("MainCamera");
                mainCam = camObj.AddComponent<Camera>();
                camObj.AddComponent<AudioListener>();
            }

            GameObject driverHead = new GameObject("Anchor_DriverHead");
            driverHead.transform.SetParent(car.transform, false);
            driverHead.transform.localPosition = new Vector3(-0.4f, 1.05f, 0.05f);

            GameObject chaseAnchor = new GameObject("Anchor_Chase");
            chaseAnchor.transform.SetParent(car.transform, false);
            chaseAnchor.transform.localPosition = new Vector3(0f, 2.2f, -5.5f);

            GameObject hoodAnchor = new GameObject("Anchor_Hood");
            hoodAnchor.transform.SetParent(car.transform, false);
            hoodAnchor.transform.localPosition = new Vector3(0f, 0.9f, 1.2f);

            GameObject overheadAnchor = new GameObject("Anchor_Overhead");
            overheadAnchor.transform.SetParent(car.transform, false);
            overheadAnchor.transform.localPosition = new Vector3(0f, 14f, 0f);
            overheadAnchor.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);

            CockpitCameraController camCtl = mainCam.GetComponent<CockpitCameraController>();
            if (camCtl == null) camCtl = mainCam.gameObject.AddComponent<CockpitCameraController>();

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
