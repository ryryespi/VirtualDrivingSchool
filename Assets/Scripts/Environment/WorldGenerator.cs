using System.Collections.Generic;
using UnityEngine;
using VirtualDrivingSchool.Rendering;

namespace VirtualDrivingSchool.Environment
{
    /// <summary>
    /// Builds the complete 3D driving school training world: grass surroundings, asphalt lot
    /// with painted stall lines, parallel parking curb, obstacle vehicles, cone slalom course,
    /// reverse corridor, perimeter road loop with stop signs and street lights, hill start ramp,
    /// and a suburban backdrop (houses, fence, trees).
    ///
    /// Low-end GPU strategy: flat ground/paint pieces are merged into one mesh per material,
    /// all static scenery is static-batched after generation, materials are shared (SRP Batcher /
    /// GPU instancing), and there are no realtime point lights.
    /// Gameplay objects (ParkingBayTrigger, CurbStrikeDetector, TrafficCone, colliders) are unchanged.
    /// </summary>
    public class WorldGenerator : MonoBehaviour
    {
        [Header("Materials (auto-filled from Resources/VDSArtLibrary)")]
        public Material asphaltMat;
        public Material concreteMat;
        public Material lineWhiteMat;
        public Material lineYellowMat;
        public Material coneMat;
        public Material obstacleCarMat;
        public Material grassMat;
        public Material roadMat;

        [Header("Scenery")]
        public bool spawnScenery = true;
        [Tooltip("Merge static scenery at runtime (fewer draw calls). Disable only for debugging.")]
        public bool staticBatchScenery = true;

        public static WorldGenerator Instance { get; private set; }

        // Heights of the flat layers (cm apart to avoid z-fighting at distance)
        const float LotY = 0.02f, RoadY = 0.035f, LineY = 0.05f, RoadLineY = 0.055f;

        private VdsArtLibrary lib;
        private readonly Dictionary<Material, FlatBatch> flat = new Dictionary<Material, FlatBatch>();
        private readonly Dictionary<GameObject, Bounds> boundsCache = new Dictionary<GameObject, Bounds>();
        private System.Random rng = new System.Random(1234);
        private int carVariant;

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
            lib = VdsArtLibrary.Get();
            if (lib != null)
            {
                if (asphaltMat == null) asphaltMat = lib.lotAsphalt;
                if (roadMat == null) roadMat = lib.roadAsphalt;
                if (grassMat == null) grassMat = lib.grass;
                if (concreteMat == null) concreteMat = lib.concrete;
                if (lineWhiteMat == null) lineWhiteMat = lib.lineWhite;
                if (lineYellowMat == null) lineYellowMat = lib.lineYellow;
                if (obstacleCarMat == null) obstacleCarMat = lib.fallbackPaint;
            }

            // Fallbacks only if the art library is missing (editor without setup run)
            Shader fallback = Shader.Find("Universal Render Pipeline/Simple Lit");
            if (fallback == null) fallback = Shader.Find("Standard");
            Material Make(Color c) { var m = new Material(fallback); m.color = c; m.enableInstancing = true; return m; }
            if (asphaltMat == null) asphaltMat = Make(new Color(0.18f, 0.18f, 0.20f));
            if (roadMat == null) roadMat = asphaltMat;
            if (grassMat == null) grassMat = Make(new Color(0.3f, 0.5f, 0.22f));
            if (concreteMat == null) concreteMat = Make(new Color(0.65f, 0.65f, 0.65f));
            if (lineWhiteMat == null) lineWhiteMat = Make(new Color(0.95f, 0.95f, 0.95f));
            if (lineYellowMat == null) lineYellowMat = Make(new Color(0.95f, 0.8f, 0.1f));
            if (coneMat == null) coneMat = Make(new Color(1f, 0.4f, 0.05f));
            if (obstacleCarMat == null) obstacleCarMat = Make(new Color(0.2f, 0.35f, 0.65f));
        }

        public void GenerateCompleteEnvironment()
        {
            GameObject worldRoot = new GameObject("Environment_TrainingGrounds");

            // 1. Ground: grass, asphalt lot, sidewalk ring
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

            // 8. Backdrop: fence, houses, trees
            if (spawnScenery && lib != null) CreateScenery(worldRoot.transform);

            // 9. Merge flat paint/ground pieces, then static-batch all non-physics scenery
            BuildFlatMeshes(worldRoot.transform);
            if (staticBatchScenery) StaticBatch(worldRoot);
        }

        // ------------------------------------------------------------------ zones
        private void CreateGround(Transform parent)
        {
            GameObject ground = new GameObject("Ground_Collider");
            ground.transform.SetParent(parent, false);
            BoxCollider col = ground.AddComponent<BoxCollider>();
            col.center = new Vector3(0f, -0.5f, 0f);
            col.size = new Vector3(900f, 1f, 900f);

            Flat(grassMat, 0f, 0f, 900f, 900f, 0f, 0.25f);                 // grass to the horizon, 4 m texture tiles
            Flat(asphaltMat, 0f, 0f, 122.5f, 122.5f, LotY, 1f / 6f);       // training lot
            // Sidewalk ring just outside the perimeter road
            float s = 70f, w = 2.5f;
            Flat(concreteMat, 0f, s, 2f * s + w * 2f, w, LotY, 0.5f);
            Flat(concreteMat, 0f, -s, 2f * s + w * 2f, w, LotY, 0.5f);
            Flat(concreteMat, s, 0f, w, 2f * s, LotY, 0.5f);
            Flat(concreteMat, -s, 0f, w, 2f * s, LotY, 0.5f);
        }

        private void CreateParkingLot(Transform parent)
        {
            GameObject lotRoot = new GameObject("Zone_ParkingLot");
            lotRoot.transform.SetParent(parent, false);
            lotRoot.transform.localPosition = Vector3.zero;

            // Create a row of 8 parking bays
            float bayWidth = 3.2f;
            float bayLength = 6.0f;
            Vector3 startPos = new Vector3(-12f, 0.02f, 20f);

            for (int i = 0; i < 8; i++)
            {
                float bayX = startPos.x + (i * bayWidth);
                // Left partition line + back limit line
                Flat(lineWhiteMat, bayX, startPos.z + bayLength * 0.5f, 0.15f, bayLength, LineY, 1f);
                Flat(lineWhiteMat, bayX + bayWidth * 0.5f, startPos.z + bayLength, bayWidth, 0.15f, LineY, 1f);

                // Parked cars in bays 1, 2 and 5 leave open stalls for parking practice
                if (i == 1 || i == 2 || i == 5)
                {
                    Vector3 carPos = new Vector3(bayX + (bayWidth * 0.5f), 0.8f, startPos.z + (bayLength * 0.5f));
                    CreateObstacleCar(lotRoot.transform, carPos, Quaternion.Euler(0f, i == 2 ? 180f : 0f, 0f));
                }
                // Bay 4 is the target Perpendicular Parking Bay!
                else if (i == 4)
                {
                    Vector3 bayCenter = new Vector3(bayX + (bayWidth * 0.5f), 0.5f, startPos.z + (bayLength * 0.5f));
                    CreateParkingTrigger(lotRoot.transform, bayCenter, new Vector3(bayWidth * 0.9f, 2f, bayLength * 0.9f), false, Vector3.forward);
                    // Painted target box so the learner can see the bay
                    Flat(lineYellowMat, bayCenter.x, startPos.z + 0.6f, bayWidth - 0.6f, 0.12f, LineY, 1f);
                }
            }

            // Rightmost end line
            Flat(lineWhiteMat, startPos.x + 8 * bayWidth, startPos.z + bayLength * 0.5f, 0.15f, bayLength, LineY, 1f);

            // Planted island behind the bays (visual only, no collider)
            Flat(concreteMat, startPos.x + 4 * bayWidth, startPos.z + bayLength + 1.6f, 8 * bayWidth + 1f, 2.2f, LotY + 0.006f, 0.5f);
            if (lib != null)
            {
                for (int i = 0; i < 9; i++)
                {
                    Vector3 p = new Vector3(startPos.x + i * bayWidth, 0.03f, startPos.z + bayLength + 1.6f);
                    Prop(lotRoot.transform, lib.Pick(lib.bushes, i), p, Rand(0f, 360f), 4.5f, false);
                }
                Prop(lotRoot.transform, lib.Pick(lib.trees, 1), new Vector3(startPos.x - 1.5f, 0.03f, startPos.z + bayLength + 1.6f), 30f, 4.5f, true);
                Prop(lotRoot.transform, lib.Pick(lib.trees, 0), new Vector3(startPos.x + 8 * bayWidth + 1.5f, 0.03f, startPos.z + bayLength + 1.6f), 80f, 4.5f, true);
            }
        }

        private void CreateParallelParkingZone(Transform parent)
        {
            GameObject zone = new GameObject("Zone_ParallelParking");
            zone.transform.SetParent(parent, false);
            zone.transform.localPosition = new Vector3(-35f, 0f, 20f);

            // Sidewalk / Curb (raised concrete)
            GameObject curb = GameObject.CreatePrimitive(PrimitiveType.Cube);
            curb.name = "Sidewalk_Curb";
            curb.transform.SetParent(zone.transform, false);
            curb.transform.localPosition = new Vector3(2.5f, 0.12f, 15f);
            curb.transform.localScale = new Vector3(3.0f, 0.24f, 35f);
            curb.GetComponent<Renderer>().sharedMaterial = concreteMat;
            curb.AddComponent<CurbStrikeDetector>();

            // Front and rear obstacle cars
            CreateObstacleCar(zone.transform, zone.transform.TransformPoint(new Vector3(0f, 0.8f, 25f)), Quaternion.identity);
            CreateObstacleCar(zone.transform, zone.transform.TransformPoint(new Vector3(0f, 0.8f, 5f)), Quaternion.identity);

            // Target Parallel Parking Bay between the two cars
            CreateParkingTrigger(zone.transform, zone.transform.TransformPoint(new Vector3(0f, 0.5f, 15f)), new Vector3(2.8f, 2f, 7.5f), true, Vector3.forward);

            // Curb edge marker line + bay box
            Vector3 z0 = zone.transform.position;
            Flat(lineWhiteMat, z0.x + 1.0f, z0.z + 15f, 0.12f, 20f, LineY, 1f);

            // Street lights and planters on the sidewalk
            if (lib != null)
            {
                for (int i = 0; i < 4; i++)
                {
                    Vector3 p = zone.transform.TransformPoint(new Vector3(3.4f, 0.24f, 1f + i * 11f));
                    Prop(zone.transform, lib.streetLight, p, 90f, 9f, true);
                    Prop(zone.transform, lib.Pick(lib.bushes, i + 1), zone.transform.TransformPoint(new Vector3(3.2f, 0.24f, 6.5f + i * 11f)), Rand(0, 360), 4f, false);
                }
            }
        }

        private void CreateReverseConeAlley(Transform parent)
        {
            GameObject alley = new GameObject("Zone_ReverseAlley");
            alley.transform.SetParent(parent, false);
            alley.transform.localPosition = new Vector3(25f, 0f, 0f);

            // Two rows of cones forming a tight 3.6 m wide corridor 30 m long
            float width = 3.6f;
            for (float z = 0f; z <= 30f; z += 3.5f)
            {
                CreateCone(alley.transform, alley.transform.TransformPoint(new Vector3(-width * 0.5f, 0.35f, z)));
                CreateCone(alley.transform, alley.transform.TransformPoint(new Vector3(width * 0.5f, 0.35f, z)));
            }

            // End garage target bay
            CreateParkingTrigger(alley.transform, alley.transform.TransformPoint(new Vector3(0f, 0.5f, 32f)), new Vector3(3.2f, 2f, 6f), false, -Vector3.forward);

            // Painted garage box + barriers behind it
            Vector3 a = alley.transform.position;
            Flat(lineWhiteMat, a.x - 1.7f, a.z + 32f, 0.15f, 6f, LineY, 1f);
            Flat(lineWhiteMat, a.x + 1.7f, a.z + 32f, 0.15f, 6f, LineY, 1f);
            Flat(lineWhiteMat, a.x, a.z + 35f, 3.55f, 0.15f, LineY, 1f);
            if (lib != null && lib.barrier != null)
            {
                for (int i = -1; i <= 1; i++)
                {
                    GameObject b = Prop(alley.transform, lib.barrier, new Vector3(a.x + i * 1.6f, 0f, a.z + 37f), 90f, 7f, false);
                    if (b != null) AddBox(b, new Vector3(0f, 0.45f, 0f), new Vector3(0.4f, 0.9f, 1.6f), true);
                }
            }
        }

        private void CreateSlalomCourse(Transform parent)
        {
            GameObject slalom = new GameObject("Zone_Slalom");
            slalom.transform.SetParent(parent, false);
            // Shifted south so it no longer overlaps the parking-bay row
            slalom.transform.localPosition = new Vector3(0f, 0f, -45f);

            for (int i = 0; i < 7; i++)
            {
                CreateCone(slalom.transform, slalom.transform.TransformPoint(new Vector3(0f, 0.35f, i * 10f)));
            }
            // Start / finish lines
            Vector3 s = slalom.transform.position;
            Flat(lineWhiteMat, s.x, s.z - 6f, 6f, 0.3f, LineY, 1f);
            Flat(lineWhiteMat, s.x, s.z + 66f, 6f, 0.3f, LineY, 1f);
        }

        private void CreateRoadNetwork(Transform parent)
        {
            GameObject roadRoot = new GameObject("Zone_RoadNetwork");
            roadRoot.transform.SetParent(parent, false);

            // Perimeter road lanes (asphalt loops)
            float roadWidth = 7.5f;
            float outerDist = 65f;
            float len = 2f * outerDist + roadWidth;
            float edge = roadWidth * 0.5f - 0.3f;

            // North / South / East / West roads with white edge lines and dashed yellow centre lines
            Flat(roadMat, 0f, outerDist, len, roadWidth, RoadY, 1f / 6f);
            Flat(roadMat, 0f, -outerDist, len, roadWidth, RoadY, 1f / 6f);
            Flat(roadMat, outerDist, 0f, roadWidth, len, RoadY + 0.001f, 1f / 6f);
            Flat(roadMat, -outerDist, 0f, roadWidth, len, RoadY + 0.001f, 1f / 6f);
            foreach (float sgn in new[] { 1f, -1f })
            {
                float d = outerDist * sgn;
                Flat(lineWhiteMat, 0f, d + edge, len - 1f, 0.15f, RoadLineY, 1f);
                Flat(lineWhiteMat, 0f, d - edge, len - 1f, 0.15f, RoadLineY, 1f);
                Flat(lineWhiteMat, d + edge, 0f, 0.15f, len - 1f, RoadLineY, 1f);
                Flat(lineWhiteMat, d - edge, 0f, 0.15f, len - 1f, RoadLineY, 1f);
            }
            CreateDashedCenterLine(roadRoot.transform, new Vector3(0f, RoadLineY, outerDist), true, 2f * outerDist - roadWidth);
            CreateDashedCenterLine(roadRoot.transform, new Vector3(0f, RoadLineY, -outerDist), true, 2f * outerDist - roadWidth);
            CreateDashedCenterLine(roadRoot.transform, new Vector3(outerDist, RoadLineY, 0f), false, 2f * outerDist - roadWidth);
            CreateDashedCenterLine(roadRoot.transform, new Vector3(-outerDist, RoadLineY, 0f), false, 2f * outerDist - roadWidth);

            // 4-Way Intersection Stop Signs (+ stop bars)
            CreateStopSign(roadRoot.transform, new Vector3(outerDist - 5f, 0f, outerDist - 5f));
            CreateStopSign(roadRoot.transform, new Vector3(-outerDist + 5f, 0f, -outerDist + 5f));

            // Street lights along the inside of the loop (visual only; no realtime lights for performance)
            if (lib != null && lib.streetLight != null)
            {
                float inner = outerDist - roadWidth * 0.5f - 1.2f;
                for (float t = -50f; t <= 50f; t += 25f)
                {
                    // lamp arm points along the model's -Z; turn it to hang over the road
                    Prop(roadRoot.transform, lib.streetLight, new Vector3(t, 0f, inner), 180f, 9f, true);
                    Prop(roadRoot.transform, lib.streetLight, new Vector3(t, 0f, -inner), 0f, 9f, true);
                    Prop(roadRoot.transform, lib.streetLight, new Vector3(inner, 0f, t), -90f, 9f, true);
                    Prop(roadRoot.transform, lib.streetLight, new Vector3(-inner, 0f, t), 90f, 9f, true);
                }
            }
        }

        private void CreateHillStartRamp(Transform parent)
        {
            GameObject rampRoot = new GameObject("Zone_HillStartRamp");
            rampRoot.transform.SetParent(parent, false);
            rampRoot.transform.localPosition = new Vector3(-45f, 0f, -40f);

            // Ramp: 14 degrees slope, 25m length
            GameObject ramp = GameObject.CreatePrimitive(PrimitiveType.Cube);
            ramp.name = "Hill_Incline";
            ramp.transform.SetParent(rampRoot.transform, false);
            ramp.transform.localPosition = new Vector3(0f, 3.0f, 12f);
            ramp.transform.localRotation = Quaternion.Euler(-14f, 0f, 0f);
            ramp.transform.localScale = new Vector3(6f, 0.4f, 26f);
            ramp.GetComponent<Renderer>().sharedMaterial = concreteMat;

            // Stop line painted on the ramp
            GameObject stopLine = GameObject.CreatePrimitive(PrimitiveType.Cube);
            stopLine.name = "Hill_StopLine";
            stopLine.transform.SetParent(ramp.transform, false);
            stopLine.transform.localPosition = new Vector3(0f, 0.52f, 0f);
            stopLine.transform.localScale = new Vector3(1f, 0.05f, 0.08f);
            stopLine.GetComponent<Renderer>().sharedMaterial = lineYellowMat;
            DestroyImmediate(stopLine.GetComponent<Collider>());

            // Edge stripes so the slope reads clearly
            foreach (float x in new[] { -0.46f, 0.46f })
            {
                GameObject e = GameObject.CreatePrimitive(PrimitiveType.Cube);
                e.name = "Hill_EdgeLine";
                e.transform.SetParent(ramp.transform, false);
                e.transform.localPosition = new Vector3(x, 0.52f, 0f);
                e.transform.localScale = new Vector3(0.025f, 0.05f, 0.98f);
                e.GetComponent<Renderer>().sharedMaterial = lineWhiteMat;
                DestroyImmediate(e.GetComponent<Collider>());
            }
        }

        private void CreateScenery(Transform parent)
        {
            GameObject root = new GameObject("Scenery_Backdrop");
            root.transform.SetParent(parent, false);
            Transform r = root.transform;

            // Perimeter fence (visual segments + one long collider per side to keep cars in)
            float f = 74f;
            if (lib.fence != null)
            {
                float seg = 6.7f;
                for (float t = -f + seg * 0.5f; t < f; t += seg)
                {
                    Prop(r, lib.fence, new Vector3(t, 0f, f), 0f, 4f, false);
                    Prop(r, lib.fence, new Vector3(t, 0f, -f), 0f, 4f, false);
                    Prop(r, lib.fence, new Vector3(f, 0f, t), 90f, 4f, false);
                    Prop(r, lib.fence, new Vector3(-f, 0f, t), 90f, 4f, false);
                }
            }
            AddBox(root, new Vector3(0f, 0.6f, f), new Vector3(2f * f, 1.2f, 0.3f), false);
            AddBox(root, new Vector3(0f, 0.6f, -f), new Vector3(2f * f, 1.2f, 0.3f), false);
            AddBox(root, new Vector3(f, 0.6f, 0f), new Vector3(0.3f, 1.2f, 2f * f), false);
            AddBox(root, new Vector3(-f, 0.6f, 0f), new Vector3(0.3f, 1.2f, 2f * f), false);

            // Houses facing the school on all four sides, trees in between
            int h = 0;
            for (float t = -66f; t <= 66f; t += 26.4f)
            {
                House(r, new Vector3(t, 0f, 92f), 180f, h++);
                House(r, new Vector3(t + 6f, 0f, -92f), 0f, h++);
                House(r, new Vector3(92f, 0f, t - 6f), -90f, h++);
                House(r, new Vector3(-92f, 0f, t + 6f), 90f, h++);
            }
            // Belt of trees between the fence and the houses (square ring, 78-83 m out)
            int i = 0;
            for (float t = -84f; t <= 84f; t += 10.5f)
            {
                for (int side = 0; side < 4; side++, i++)
                {
                    float d = Rand(78f, 83f), u = t + Rand(-2f, 2f);
                    Vector3 p = side == 0 ? new Vector3(u, 0f, d) : side == 1 ? new Vector3(u, 0f, -d) : side == 2 ? new Vector3(d, 0f, u) : new Vector3(-d, 0f, u);
                    Prop(r, lib.Pick(lib.trees, i * 7 + side), p, Rand(0, 360), Rand(4.0f, 5.5f), false);
                    if (i % 2 == 0) Prop(r, lib.Pick(lib.bushes, i), p + new Vector3(Rand(-3f, 3f), 0f, Rand(-3f, 3f)), Rand(0, 360), 5f, false);
                }
            }
            // Corner groves inside the loop
            Vector3[] groves = { new Vector3(52f, 0f, 52f), new Vector3(-52f, 0f, 52f), new Vector3(52f, 0f, -52f), new Vector3(-54f, 0f, -54f) };
            foreach (Vector3 g in groves)
            {
                Flat(grassMat, g.x, g.z, 9f, 9f, LotY + 0.008f, 0.25f);
                for (int k = 0; k < 3; k++)
                    Prop(r, lib.Pick(lib.trees, k + (int)g.x), g + new Vector3(Rand(-3f, 3f), 0.03f, Rand(-3f, 3f)), Rand(0, 360), Rand(4f, 5f), true);
                Prop(r, lib.Pick(lib.rocks, 0), g + new Vector3(2f, 0.03f, -2.5f), Rand(0, 360), 2.5f, false);
            }
        }

        private void House(Transform parent, Vector3 pos, float yaw, int index)
        {
            Prop(parent, lib.Pick(lib.houses, index), pos, yaw, 9f, false);
            Flat(concreteMat, pos.x, pos.z, 14f, 14f, 0.012f, 0.5f);
        }

        // ------------------------------------------------------------------ helpers
        private void CreateDashedCenterLine(Transform parent, Vector3 center, bool isHorizontal, float length)
        {
            float dashLen = 2.5f;
            float gap = 2.5f;
            float total = dashLen + gap;
            int count = (int)(length / total);

            for (int i = -count / 2; i <= count / 2; i++)
            {
                float x = isHorizontal ? center.x + (i * total) : center.x;
                float z = isHorizontal ? center.z : center.z + (i * total);
                Flat(lineYellowMat, x, z, isHorizontal ? dashLen : 0.18f, isHorizontal ? 0.18f : dashLen, center.y, 1f);
            }
        }

        private void CreateObstacleCar(Transform parent, Vector3 pos, Quaternion rot)
        {
            GameObject car = new GameObject("ObstacleCar");
            car.transform.SetParent(parent, false);
            car.transform.position = new Vector3(pos.x, 0f, pos.z);
            car.transform.rotation = rot;

            // Same collision volume as the old 1.9 x 1.4 x 4.4 block
            AddBox(car, new Vector3(0f, pos.y, 0f), new Vector3(1.9f, 1.4f, 4.4f), true);

            GameObject model = lib != null ? lib.Pick(lib.parkedCars, carVariant++) : null;
            if (model != null)
            {
                Bounds b = ModelBounds(model);
                GameObject v = Instantiate(model, car.transform, false);
                v.name = "Model_" + model.name;
                float sx = 1.85f / Mathf.Max(0.01f, b.size.x);
                float sz = 4.4f / Mathf.Max(0.01f, b.size.z);
                float sy = sx * 1.05f;
                v.transform.localScale = new Vector3(sx, sy, sz);
                v.transform.localPosition = new Vector3(-b.center.x * sx, -b.min.y * sy, -b.center.z * sz);
            }
            else
            {
                CarVisualBuilder.Box(car.transform, "Body", new Vector3(0f, pos.y, 0f), new Vector3(1.9f, 1.4f, 4.4f), obstacleCarMat);
            }
        }

        private void CreateCone(Transform parent, Vector3 pos)
        {
            GameObject cone = new GameObject("TrafficCone");
            cone.transform.SetParent(parent, false);
            cone.transform.position = pos;

            // Same collider as the old cylinder primitive (0.45 x 0.7 x 0.45)
            CapsuleCollider col = cone.AddComponent<CapsuleCollider>();
            col.radius = 0.225f;
            col.height = 0.7f;
            cone.AddComponent<TrafficCone>(); // adds Rigidbody via RequireComponent

            if (lib != null && lib.cone != null)
            {
                GameObject v = Instantiate(lib.cone, cone.transform, false);
                v.name = "Cone_Model";
                v.transform.localPosition = new Vector3(0f, -0.35f, 0f);
                v.transform.localScale = Vector3.one * 1.2f;
            }
            else
            {
                GameObject c = CarVisualBuilder.Box(cone.transform, "Cone_Body", Vector3.zero, new Vector3(0.45f, 0.7f, 0.45f), coneMat);
                c.name = "Cone_Body";
            }
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
            if (lib != null && lib.stopSign != null)
            {
                // Face traffic approaching the corner
                GameObject s = Prop(parent, lib.stopSign, pos, pos.x > 0 ? -135f : 45f, 5f, true);
                if (s != null) s.name = "StopSign";
                return;
            }

            GameObject pole = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            pole.name = "StopSign_Pole";
            pole.transform.SetParent(parent, false);
            pole.transform.position = pos + Vector3.up * 1.2f;
            pole.transform.localScale = new Vector3(0.08f, 1.2f, 0.08f);
            pole.GetComponent<Renderer>().sharedMaterial = concreteMat;

            GameObject sign = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            sign.name = "StopSign_Octagon";
            sign.transform.SetParent(pole.transform, false);
            sign.transform.localPosition = new Vector3(0f, 0.9f, 0f);
            sign.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
            sign.transform.localScale = new Vector3(7f, 0.1f, 7f);
            sign.GetComponent<Renderer>().sharedMaterial = lib != null ? lib.signRed : lineYellowMat;
        }

        /// <summary>Places a kit model on the ground, uniformly scaled; optional thin trunk/pole collider.</summary>
        private GameObject Prop(Transform parent, GameObject prefab, Vector3 pos, float yaw, float scale, bool poleCollider)
        {
            if (prefab == null) return null;
            GameObject g = Instantiate(prefab, parent, false);
            g.transform.position = pos;
            g.transform.rotation = Quaternion.Euler(0f, yaw, 0f);
            g.transform.localScale = Vector3.one * scale;
            if (poleCollider)
            {
                CapsuleCollider c = g.AddComponent<CapsuleCollider>();
                c.radius = 0.25f / scale;
                c.height = 4f / scale;
                c.center = new Vector3(0f, 2f / scale, 0f);
            }
            return g;
        }

        private static BoxCollider AddBox(GameObject go, Vector3 center, Vector3 size, bool local)
        {
            GameObject target = go;
            if (!local)
            {
                target = new GameObject("Collider");
                target.transform.SetParent(go.transform, false);
            }
            BoxCollider c = target.AddComponent<BoxCollider>();
            c.center = center;
            c.size = size;
            if (local && go.transform.lossyScale != Vector3.one)
            {
                Vector3 s = go.transform.lossyScale;
                c.center = new Vector3(center.x / s.x, center.y / s.y, center.z / s.z);
                c.size = new Vector3(size.x / s.x, size.y / s.y, size.z / s.z);
            }
            return c;
        }

        private Bounds ModelBounds(GameObject prefab)
        {
            if (boundsCache.TryGetValue(prefab, out Bounds cached)) return cached;
            Bounds b = new Bounds();
            bool init = false;
            foreach (MeshFilter mf in prefab.GetComponentsInChildren<MeshFilter>(true))
            {
                if (mf.sharedMesh == null) continue;
                Matrix4x4 m = prefab.transform.worldToLocalMatrix * mf.transform.localToWorldMatrix;
                Bounds mb = mf.sharedMesh.bounds;
                Vector3 c = mb.center, e = mb.extents;
                for (int i = 0; i < 8; i++)
                {
                    Vector3 corner = c + Vector3.Scale(e, new Vector3((i & 1) == 0 ? -1 : 1, (i & 2) == 0 ? -1 : 1, (i & 4) == 0 ? -1 : 1));
                    Vector3 p = m.MultiplyPoint3x4(corner);
                    if (!init) { b = new Bounds(p, Vector3.zero); init = true; } else b.Encapsulate(p);
                }
            }
            boundsCache[prefab] = b;
            return b;
        }

        private float Rand(float a, float b) => a + (float)rng.NextDouble() * (b - a);

        // ------------------------------------------------------------------ flat merged meshes
        private class FlatBatch
        {
            public readonly List<Vector3> v = new List<Vector3>();
            public readonly List<Vector2> uv = new List<Vector2>();
            public readonly List<int> t = new List<int>();
        }

        /// <summary>Queues a flat, upward-facing rectangle (world space, world-scaled UVs). No collider.</summary>
        private void Flat(Material mat, float cx, float cz, float sx, float sz, float y, float uvPerMeter)
        {
            if (mat == null) return;
            if (!flat.TryGetValue(mat, out FlatBatch fb)) { fb = new FlatBatch(); flat[mat] = fb; }
            int i = fb.v.Count;
            float hx = sx * 0.5f, hz = sz * 0.5f;
            fb.v.Add(new Vector3(cx - hx, y, cz - hz));
            fb.v.Add(new Vector3(cx - hx, y, cz + hz));
            fb.v.Add(new Vector3(cx + hx, y, cz + hz));
            fb.v.Add(new Vector3(cx + hx, y, cz - hz));
            for (int k = 0; k < 4; k++) fb.uv.Add(new Vector2(fb.v[i + k].x, fb.v[i + k].z) * uvPerMeter);
            fb.t.Add(i); fb.t.Add(i + 1); fb.t.Add(i + 2);
            fb.t.Add(i); fb.t.Add(i + 2); fb.t.Add(i + 3);
        }

        private void BuildFlatMeshes(Transform parent)
        {
            GameObject root = new GameObject("Ground_Paint_Merged");
            root.transform.SetParent(parent, false);
            foreach (var kv in flat)
            {
                Mesh m = new Mesh { name = "Flat_" + kv.Key.name };
                if (kv.Value.v.Count > 65000) m.indexFormat = UnityEngine.Rendering.IndexFormat.UInt32;
                m.SetVertices(kv.Value.v);
                m.SetUVs(0, kv.Value.uv);
                m.SetTriangles(kv.Value.t, 0);
                Vector3[] n = new Vector3[kv.Value.v.Count];
                for (int i = 0; i < n.Length; i++) n[i] = Vector3.up;
                m.normals = n;
                m.RecalculateBounds();
                m.UploadMeshData(true);
                GameObject g = new GameObject(m.name);
                g.transform.SetParent(root.transform, false);
                g.AddComponent<MeshFilter>().sharedMesh = m;
                MeshRenderer r = g.AddComponent<MeshRenderer>();
                r.sharedMaterial = kv.Key;
                r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off; // flat decals never cast
            }
            flat.Clear();
        }

        private static void StaticBatch(GameObject worldRoot)
        {
            var list = new List<GameObject>();
            foreach (MeshRenderer r in worldRoot.GetComponentsInChildren<MeshRenderer>())
            {
                if (r.GetComponentInParent<Rigidbody>() != null) continue; // cones stay dynamic
                MeshFilter mf = r.GetComponent<MeshFilter>();
                if (mf == null || mf.sharedMesh == null || !mf.sharedMesh.isReadable) continue;
                list.Add(r.gameObject);
            }
            StaticBatchingUtility.Combine(list.ToArray(), worldRoot);
        }
    }
}
