using UnityEngine;

namespace VirtualDrivingSchool.Environment
{
    /// <summary>
    /// Builds the complete 3D driving school training world: asphalt tarmac,
    /// parking lot with stall lines, parallel parking curb, obstacle vehicles,
    /// cone slalom course, reverse corridor, road loop, stop signs, and hill start ramp.
    /// </summary>
    public class WorldGenerator : MonoBehaviour
    {
        [Header("Materials")]
        public Material asphaltMat;
        public Material concreteMat;
        public Material lineWhiteMat;
        public Material lineYellowMat;
        public Material coneMat;
        public Material obstacleCarMat;

        public static WorldGenerator Instance { get; private set; }

        private void Awake()
        {
            if (Instance == null) Instance = this;
            BuildWorldMaterials();
        }

        private void Start()
        {
            GenerateCompleteEnvironment();
        }

        private void BuildWorldMaterials()
        {
            Shader standardShader = Shader.Find("Standard");
            if (standardShader == null) standardShader = Shader.Find("Universal Render Pipeline/Lit");
            if (standardShader == null) standardShader = Shader.Find("Mobile/Diffuse");

            if (asphaltMat == null)
            {
                asphaltMat = new Material(standardShader);
                asphaltMat.color = new Color(0.18f, 0.18f, 0.20f);
            }
            if (concreteMat == null)
            {
                concreteMat = new Material(standardShader);
                concreteMat.color = new Color(0.65f, 0.65f, 0.65f);
            }
            if (lineWhiteMat == null)
            {
                lineWhiteMat = new Material(standardShader);
                lineWhiteMat.color = new Color(0.95f, 0.95f, 0.95f);
            }
            if (lineYellowMat == null)
            {
                lineYellowMat = new Material(standardShader);
                lineYellowMat.color = new Color(0.95f, 0.8f, 0.1f);
            }
            if (coneMat == null)
            {
                coneMat = new Material(standardShader);
                coneMat.color = new Color(1f, 0.4f, 0.05f);
            }
            if (obstacleCarMat == null)
            {
                obstacleCarMat = new Material(standardShader);
                obstacleCarMat.color = new Color(0.2f, 0.35f, 0.65f);
            }
        }

        public void GenerateCompleteEnvironment()
        {
            GameObject worldRoot = new GameObject("Environment_TrainingGrounds");

            // 1. Ground Plane
            CreateGround(worldRoot.transform);

            // 2. Parking Lot Stalls
            CreateParkingLot(worldRoot.transform);

            // 3. Parallel Parking Bay with Curbs & Obstacle Cars
            CreateParallelParkingZone(worldRoot.transform);

            // 4. Reverse Alley with Traffic Cones
            CreateReverseConeAlley(worldRoot.transform);

            // 5. Slalom Cone Course
            CreateSlalomCourse(worldRoot.transform);

            // 6. Perimeter Road Network & Intersection
            CreateRoadNetwork(worldRoot.transform);

            // 7. Hill Start Incline Ramp
            CreateHillStartRamp(worldRoot.transform);
        }

        private void CreateGround(Transform parent)
        {
            GameObject ground = GameObject.CreatePrimitive(PrimitiveType.Plane);
            ground.name = "Ground_Tarmac";
            ground.transform.SetParent(parent, false);
            ground.transform.position = Vector3.zero;
            ground.transform.localScale = new Vector3(30f, 1f, 30f); // 300m x 300m
            ground.GetComponent<Renderer>().material = asphaltMat;
        }

        private void CreateParkingLot(Transform parent)
        {
            GameObject lotRoot = new GameObject("Zone_ParkingLot");
            lotRoot.transform.SetParent(parent, false);
            lotRoot.transform.position = new Vector3(0f, 0f, 0f);

            // Create a row of 8 parking bays
            float bayWidth = 3.2f;
            float bayLength = 6.0f;
            Vector3 startPos = new Vector3(-12f, 0.02f, 20f);

            for (int i = 0; i < 8; i++)
            {
                float bayX = startPos.x + (i * bayWidth);
                Vector3 linePos = new Vector3(bayX, 0.02f, startPos.z + (bayLength * 0.5f));

                // Left partition line
                CreateStripe(lotRoot.transform, linePos, new Vector3(0.15f, 0.02f, bayLength), lineWhiteMat);

                // Back limit line
                CreateStripe(lotRoot.transform, new Vector3(bayX + (bayWidth * 0.5f), 0.02f, startPos.z + bayLength), new Vector3(bayWidth, 0.02f, 0.15f), lineWhiteMat);

                // Place obstacle cars in bays 1, 2, 5, 7 to leave open stalls for parking practice
                if (i == 1 || i == 2 || i == 5)
                {
                    Vector3 carPos = new Vector3(bayX + (bayWidth * 0.5f), 0.8f, startPos.z + (bayLength * 0.5f));
                    CreateObstacleCar(lotRoot.transform, carPos, Quaternion.identity);
                }
                // Bay 4 is the target Perpendicular Parking Bay!
                else if (i == 4)
                {
                    Vector3 bayCenter = new Vector3(bayX + (bayWidth * 0.5f), 0.5f, startPos.z + (bayLength * 0.5f));
                    CreateParkingTrigger(lotRoot.transform, bayCenter, new Vector3(bayWidth * 0.9f, 2f, bayLength * 0.9f), false, Vector3.forward);
                }
            }

            // Rightmost end line
            CreateStripe(lotRoot.transform, new Vector3(startPos.x + (8 * bayWidth), 0.02f, startPos.z + (bayLength * 0.5f)), new Vector3(0.15f, 0.02f, bayLength), lineWhiteMat);
        }

        private void CreateParallelParkingZone(Transform parent)
        {
            GameObject zone = new GameObject("Zone_ParallelParking");
            zone.transform.SetParent(parent, false);
            zone.transform.position = new Vector3(-35f, 0f, 20f);

            // Sidewalk / Curb (raised concrete)
            GameObject curb = GameObject.CreatePrimitive(PrimitiveType.Cube);
            curb.name = "Sidewalk_Curb";
            curb.transform.SetParent(zone.transform, false);
            curb.transform.position = new Vector3(2.5f, 0.12f, 15f);
            curb.transform.localScale = new Vector3(3.0f, 0.24f, 35f);
            curb.GetComponent<Renderer>().material = concreteMat;
            curb.AddComponent<CurbStrikeDetector>();

            // Front obstacle car
            CreateObstacleCar(zone.transform, new Vector3(0f, 0.8f, 25f), Quaternion.identity);

            // Rear obstacle car
            CreateObstacleCar(zone.transform, new Vector3(0f, 0.8f, 5f), Quaternion.identity);

            // Target Parallel Parking Bay between the two cars
            CreateParkingTrigger(zone.transform, new Vector3(0f, 0.5f, 15f), new Vector3(2.8f, 2f, 7.5f), true, Vector3.forward);

            // Curb edge marker line
            CreateStripe(zone.transform, new Vector3(1.0f, 0.02f, 15f), new Vector3(0.12f, 0.02f, 20f), lineWhiteMat);
        }

        private void CreateReverseConeAlley(Transform parent)
        {
            GameObject alley = new GameObject("Zone_ReverseAlley");
            alley.transform.SetParent(parent, false);
            alley.transform.position = new Vector3(25f, 0f, 0f);

            // Two rows of cones forming a tight 3.4m wide corridor 30m long
            float width = 3.6f;
            for (float z = 0f; z <= 30f; z += 3.5f)
            {
                CreateCone(alley.transform, new Vector3(-width * 0.5f, 0.35f, z));
                CreateCone(alley.transform, new Vector3(width * 0.5f, 0.35f, z));
            }

            // End garage target bay
            CreateParkingTrigger(alley.transform, new Vector3(0f, 0.5f, 32f), new Vector3(3.2f, 2f, 6f), false, -Vector3.forward);
        }

        private void CreateSlalomCourse(Transform parent)
        {
            GameObject slalom = new GameObject("Zone_Slalom");
            slalom.transform.SetParent(parent, false);
            slalom.transform.position = new Vector3(0f, 0f, -30f);

            for (int i = 0; i < 7; i++)
            {
                CreateCone(slalom.transform, new Vector3(0f, 0.35f, i * 10f));
            }
        }

        private void CreateRoadNetwork(Transform parent)
        {
            GameObject roadRoot = new GameObject("Zone_RoadNetwork");
            roadRoot.transform.SetParent(parent, false);

            // Perimeter road lanes (asphalt loops)
            float roadWidth = 7.5f;
            float outerDist = 65f;

            // North Road
            CreateStripe(roadRoot.transform, new Vector3(0f, 0.02f, outerDist), new Vector3(140f, 0.02f, roadWidth), asphaltMat);
            CreateDashedCenterLine(roadRoot.transform, new Vector3(0f, 0.03f, outerDist), true, 140f);

            // South Road
            CreateStripe(roadRoot.transform, new Vector3(0f, 0.02f, -outerDist), new Vector3(140f, 0.02f, roadWidth), asphaltMat);
            CreateDashedCenterLine(roadRoot.transform, new Vector3(0f, 0.03f, -outerDist), true, 140f);

            // East Road
            CreateStripe(roadRoot.transform, new Vector3(outerDist, 0.02f, 0f), new Vector3(roadWidth, 0.02f, 140f), asphaltMat);
            CreateDashedCenterLine(roadRoot.transform, new Vector3(outerDist, 0.03f, 0f), false, 140f);

            // West Road
            CreateStripe(roadRoot.transform, new Vector3(-outerDist, 0.02f, 0f), new Vector3(roadWidth, 0.02f, 140f), asphaltMat);
            CreateDashedCenterLine(roadRoot.transform, new Vector3(-outerDist, 0.03f, 0f), false, 140f);

            // 4-Way Intersection Stop Signs
            CreateStopSign(roadRoot.transform, new Vector3(outerDist - 5f, 0f, outerDist - 5f));
            CreateStopSign(roadRoot.transform, new Vector3(-outerDist + 5f, 0f, -outerDist + 5f));
        }

        private void CreateHillStartRamp(Transform parent)
        {
            GameObject rampRoot = new GameObject("Zone_HillStartRamp");
            rampRoot.transform.SetParent(parent, false);
            rampRoot.transform.position = new Vector3(-45f, 0f, -40f);

            // Ramp: 14 degrees slope, 25m length
            GameObject ramp = GameObject.CreatePrimitive(PrimitiveType.Cube);
            ramp.name = "Hill_Incline";
            ramp.transform.SetParent(rampRoot.transform, false);
            ramp.transform.localPosition = new Vector3(0f, 3.0f, 12f);
            ramp.transform.localRotation = Quaternion.Euler(-14f, 0f, 0f);
            ramp.transform.localScale = new Vector3(6f, 0.4f, 26f);
            ramp.GetComponent<Renderer>().material = concreteMat;

            // Stop line painted on the ramp
            GameObject stopLine = GameObject.CreatePrimitive(PrimitiveType.Cube);
            stopLine.name = "Hill_StopLine";
            stopLine.transform.SetParent(ramp.transform, false);
            stopLine.transform.localPosition = new Vector3(0f, 0.52f, 0f);
            stopLine.transform.localScale = new Vector3(1f, 0.05f, 0.08f);
            stopLine.GetComponent<Renderer>().material = lineYellowMat;
            Destroy(stopLine.GetComponent<Collider>());
        }

        // Helper primitive creators
        private void CreateStripe(Transform parent, Vector3 pos, Vector3 size, Material mat)
        {
            GameObject stripe = GameObject.CreatePrimitive(PrimitiveType.Cube);
            stripe.transform.SetParent(parent, false);
            stripe.transform.position = pos;
            stripe.transform.localScale = size;
            stripe.GetComponent<Renderer>().material = mat;
            Destroy(stripe.GetComponent<Collider>());
        }

        private void CreateDashedCenterLine(Transform parent, Vector3 center, bool isHorizontal, float length)
        {
            float dashLen = 2.5f;
            float gap = 2.5f;
            float total = dashLen + gap;
            int count = (int)(length / total);

            for (int i = -count / 2; i <= count / 2; i++)
            {
                Vector3 p = isHorizontal ? new Vector3(center.x + (i * total), center.y, center.z) : new Vector3(center.x, center.y, center.z + (i * total));
                Vector3 s = isHorizontal ? new Vector3(dashLen, 0.02f, 0.18f) : new Vector3(0.18f, 0.02f, dashLen);
                CreateStripe(parent, p, s, lineYellowMat);
            }
        }

        private void CreateObstacleCar(Transform parent, Vector3 pos, Quaternion rot)
        {
            GameObject car = GameObject.CreatePrimitive(PrimitiveType.Cube);
            car.name = "ObstacleCar";
            car.transform.SetParent(parent, false);
            car.transform.position = pos;
            car.transform.rotation = rot;
            car.transform.localScale = new Vector3(1.9f, 1.4f, 4.4f);
            car.GetComponent<Renderer>().material = obstacleCarMat;

            // Add cabin roof
            GameObject cabin = GameObject.CreatePrimitive(PrimitiveType.Cube);
            cabin.transform.SetParent(car.transform, false);
            cabin.transform.localPosition = new Vector3(0f, 0.5f, -0.1f);
            cabin.transform.localScale = new Vector3(0.85f, 0.6f, 0.55f);
            cabin.GetComponent<Renderer>().material = obstacleCarMat;
            Destroy(cabin.GetComponent<Collider>());
        }

        private void CreateCone(Transform parent, Vector3 pos)
        {
            GameObject cone = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            cone.name = "TrafficCone";
            cone.transform.SetParent(parent, false);
            cone.transform.position = pos;
            cone.transform.localScale = new Vector3(0.45f, 0.35f, 0.45f);
            cone.GetComponent<Renderer>().material = coneMat;
            cone.AddComponent<TrafficCone>();
        }

        private void CreateParkingTrigger(Transform parent, Vector3 center, Vector3 size, bool isParallel, Vector3 forwardDir)
        {
            GameObject triggerObj = new GameObject(isParallel ? "Trigger_ParallelBay" : "Trigger_ParkingBay");
            triggerObj.transform.SetParent(parent, false);
            triggerObj.transform.position = center;
            BoxCollider col = triggerObj.AddComponent<BoxCollider>();
            col.isTrigger = true;
            col.size = size;

            ParkingBayTrigger bay = triggerObj.AddComponent<ParkingBayTrigger>();
            bay.isParallelParkingBay = isParallel;
            bay.intendedForwardDirection = forwardDir;
        }

        private void CreateStopSign(Transform parent, Vector3 pos)
        {
            GameObject pole = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            pole.name = "StopSign_Pole";
            pole.transform.SetParent(parent, false);
            pole.transform.position = pos + Vector3.up * 1.2f;
            pole.transform.localScale = new Vector3(0.08f, 1.2f, 0.08f);
            pole.GetComponent<Renderer>().material = concreteMat;

            GameObject sign = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            sign.name = "StopSign_Octagon";
            sign.transform.SetParent(pole.transform, false);
            sign.transform.localPosition = new Vector3(0f, 0.9f, 0f);
            sign.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
            sign.transform.localScale = new Vector3(7f, 0.1f, 7f);

            Material signMat = new Material(Shader.Find("Standard"));
            signMat.color = new Color(0.85f, 0.1f, 0.1f);
            sign.GetComponent<Renderer>().material = signMat;
        }
    }
}
