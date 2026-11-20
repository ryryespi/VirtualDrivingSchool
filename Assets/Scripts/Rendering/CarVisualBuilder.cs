using UnityEngine;
using VirtualDrivingSchool.Cockpit;
using VirtualDrivingSchool.Input;

namespace VirtualDrivingSchool.Rendering
{
    /// <summary>
    /// Builds the player car's visual shell: Kenney low-poly body, cockpit interior
    /// (dashboard, binnacle, pillars, roof liner, door cards), a steering wheel that follows
    /// the G920, mirrors, and wheel meshes. Physics (Rigidbody / WheelColliders) is untouched.
    /// Falls back to simple primitives if the art library is missing.
    /// </summary>
    public static class CarVisualBuilder
    {
        // Body fit for the hatchback model: ~1.95 m wide, 1.43 m tall, 4.56 m long, 2.6 m wheelbase.
        static readonly Vector3 BodyScale = new Vector3(1.5f, 1.3f, 1.6f);
        static readonly Vector3 BodyOffset = new Vector3(0f, -0.1f, 0f);

        public static void BuildBodyAndCockpit(GameObject car, LogitechG920Manager input)
        {
            VdsArtLibrary lib = VdsArtLibrary.Get();
            Material dark = lib != null ? lib.interiorDark : null;
            Material trim = lib != null ? lib.interiorTrim : null;

            // ---------- Exterior body ----------
            if (lib != null && lib.playerCarBody != null)
            {
                GameObject body = Object.Instantiate(lib.playerCarBody, car.transform, false);
                body.name = "Body_Mesh";
                body.transform.localPosition = BodyOffset;
                body.transform.localScale = BodyScale;
                // The kit car has its own wheels; ours are driven by WheelColliders instead.
                foreach (Transform t in body.GetComponentsInChildren<Transform>(true))
                {
                    if (t != body.transform && t.name.StartsWith("wheel")) t.gameObject.SetActive(false);
                }
                foreach (Collider c in body.GetComponentsInChildren<Collider>(true)) Object.Destroy(c);
            }
            else
            {
                Material paint = lib != null ? lib.fallbackPaint : null;
                Box(car.transform, "Body_Mesh", new Vector3(0f, 0.4f, 0f), new Vector3(1.85f, 0.7f, 4.3f), paint);
                Box(car.transform, "Cabin_Mesh", new Vector3(0f, 0.95f, -0.2f), new Vector3(1.5f, 0.6f, 2.2f), paint);
                // (Box() strips primitive colliders; physics colliders are added below)
            }

            // Physics colliders: identical to the old primitive body + cabin cubes, so mass/inertia,
            // curb strikes and cone hits behave exactly as before.
            BoxCollider bodyCol = car.AddComponent<BoxCollider>();
            bodyCol.center = new Vector3(0f, 0.4f, 0f);
            bodyCol.size = new Vector3(1.85f, 0.7f, 4.3f);
            BoxCollider cabinCol = car.AddComponent<BoxCollider>();
            cabinCol.center = new Vector3(0f, 0.95f, -0.2f);
            cabinCol.size = new Vector3(1.5f, 0.6f, 2.2f);

            // ---------- Cockpit interior (only seen from the driver's seat) ----------
            GameObject interior = new GameObject("Cockpit_Interior");
            interior.transform.SetParent(car.transform, false);
            Transform it = interior.transform;

            Box(it, "Dashboard", new Vector3(0f, 0.74f, 0.78f), new Vector3(1.72f, 0.2f, 0.5f), dark);
            Box(it, "Dashboard_Top", new Vector3(0f, 0.85f, 0.88f), new Vector3(1.72f, 0.03f, 0.36f), trim);
            Box(it, "Gauge_Binnacle", new Vector3(-0.4f, 0.885f, 0.64f), new Vector3(0.44f, 0.09f, 0.16f), dark);
            Box(it, "Gauge_Face", new Vector3(-0.4f, 0.885f, 0.555f), new Vector3(0.36f, 0.06f, 0.01f), trim);
            Box(it, "Center_Console", new Vector3(0f, 0.5f, 0.35f), new Vector3(0.28f, 0.35f, 0.7f), dark);
            Box(it, "Roof_Liner", new Vector3(0f, 1.27f, -0.3f), new Vector3(1.6f, 0.03f, 1.4f), trim);
            Box(it, "Door_Left", new Vector3(-0.9f, 0.68f, -0.15f), new Vector3(0.06f, 0.5f, 1.9f), trim);
            Box(it, "Door_Right", new Vector3(0.9f, 0.68f, -0.15f), new Vector3(0.06f, 0.5f, 1.9f), trim);
            Beam(it, "A_Pillar_Left", new Vector3(-0.84f, 0.86f, 0.98f), new Vector3(-0.74f, 1.27f, 0.45f), 0.05f, dark);
            Beam(it, "A_Pillar_Right", new Vector3(0.84f, 0.86f, 0.98f), new Vector3(0.74f, 1.27f, 0.45f), 0.05f, dark);
            Box(it, "Windshield_Header", new Vector3(0f, 1.25f, 0.45f), new Vector3(1.5f, 0.06f, 0.08f), dark);

            // ---------- Steering wheel (follows wheel input) ----------
            GameObject column = new GameObject("Steering_Column");
            column.transform.SetParent(it, false);
            column.transform.localPosition = new Vector3(-0.4f, 0.86f, 0.42f);
            column.transform.localRotation = Quaternion.Euler(22f, 0f, 0f);
            GameObject wheel = new GameObject("SteeringWheel_Mesh");
            wheel.transform.SetParent(column.transform, false);
            wheel.AddComponent<MeshFilter>().sharedMesh = TorusMesh(0.185f, 0.022f, 20, 6);
            wheel.AddComponent<MeshRenderer>().sharedMaterial = dark;
            Box(wheel.transform, "Hub", new Vector3(0f, 0f, 0.01f), new Vector3(0.1f, 0.08f, 0.04f), trim);
            Box(wheel.transform, "Spoke_L", new Vector3(-0.09f, -0.01f, 0.01f), new Vector3(0.17f, 0.03f, 0.015f), dark);
            Box(wheel.transform, "Spoke_R", new Vector3(0.09f, -0.01f, 0.01f), new Vector3(0.17f, 0.03f, 0.015f), dark);
            Box(wheel.transform, "Spoke_B", new Vector3(0f, -0.1f, 0.01f), new Vector3(0.03f, 0.15f, 0.015f), dark);
            Box(wheel.transform, "Top_Marker", new Vector3(0f, 0.185f, -0.005f), new Vector3(0.025f, 0.05f, 0.05f), lib != null ? lib.signRed : null);
            SteeringWheelVisual sw = wheel.AddComponent<SteeringWheelVisual>();
            sw.input = input;

            // ---------- Mirrors ----------
            CreateMirror(it, "Mirror_InteriorCenter", MirrorCamera.MirrorType.InteriorRearView,
                new Vector3(0.05f, 1.17f, 0.5f), new Vector2(0.26f, 0.075f), Quaternion.Euler(4f, -12f, 0f), lib, dark);
            CreateMirror(car.transform, "Mirror_DriverLeft", MirrorCamera.MirrorType.DriverSideLeft,
                new Vector3(-1.02f, 0.92f, 0.62f), new Vector2(0.2f, 0.12f), Quaternion.Euler(0f, 18f, 0f), lib, dark);
        }

        static void CreateMirror(Transform parent, string name, MirrorCamera.MirrorType type, Vector3 pos, Vector2 size, Quaternion rot, VdsArtLibrary lib, Material housing)
        {
            GameObject root = new GameObject(name);
            root.transform.SetParent(parent, false);
            root.transform.localPosition = pos;
            root.transform.localRotation = rot;

            Box(root.transform, "Housing", new Vector3(0f, 0f, 0.02f), new Vector3(size.x + 0.025f, size.y + 0.025f, 0.035f), housing);

            GameObject glass = GameObject.CreatePrimitive(PrimitiveType.Quad); // visible face points back at the driver
            glass.name = "Glass";
            glass.transform.SetParent(root.transform, false);
            glass.transform.localScale = new Vector3(size.x, size.y, 1f);
            Object.DestroyImmediate(glass.GetComponent<Collider>());
            Renderer r = glass.GetComponent<Renderer>();
            r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            r.receiveShadows = false;
            if (lib != null && lib.mirrorGlass != null)
            {
                r.material = new Material(lib.mirrorGlass);
                r.material.mainTextureScale = new Vector2(-1f, 1f); // mirror-flip the camera image
                r.material.mainTextureOffset = new Vector2(1f, 0f);
            }

            GameObject camObj = new GameObject(type + "_Camera");
            camObj.transform.SetParent(root.transform, false);
            camObj.transform.localPosition = new Vector3(0f, 0f, -0.06f); // just in front of the glass
            camObj.transform.localRotation = Quaternion.Euler(type == MirrorCamera.MirrorType.InteriorRearView ? 7f : 3f, 180f, 0f); // look backwards
            Camera cam = camObj.AddComponent<Camera>();
            cam.enabled = false;

            MirrorCamera mc = root.AddComponent<MirrorCamera>();
            mc.mirrorType = type;
            mc.mirrorGlassRenderer = r;
            mc.mirrorCam = cam;
        }

        /// <summary>Visual for one road wheel. Parent is the WheelCollider object; returns the transform ManualCarController poses.</summary>
        public static Transform CreateWheelVisual(Transform wheelColliderObj, bool isLeft, float radius)
        {
            VdsArtLibrary lib = VdsArtLibrary.Get();
            GameObject pivot = new GameObject("Mesh");
            pivot.transform.SetParent(wheelColliderObj, false);
            if (lib != null && lib.wheel != null)
            {
                GameObject w = Object.Instantiate(lib.wheel, pivot.transform, false);
                w.name = "Wheel_Model";
                float s = (radius * 2f) / 0.6f; // kit wheel is 0.6 m tall
                w.transform.localScale = new Vector3(s * 0.8f, s, s);
                w.transform.localRotation = isLeft ? Quaternion.Euler(0f, 180f, 0f) : Quaternion.identity;
                foreach (Collider c in w.GetComponentsInChildren<Collider>(true)) Object.Destroy(c);
            }
            else
            {
                GameObject cyl = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
                cyl.transform.SetParent(pivot.transform, false);
                cyl.transform.localRotation = Quaternion.Euler(0f, 0f, 90f);
                cyl.transform.localScale = new Vector3(radius * 2f, 0.12f, radius * 2f);
                Object.DestroyImmediate(cyl.GetComponent<Collider>());
            }
            return pivot.transform;
        }

        // ------------------------------------------------------------------
        public static GameObject Box(Transform parent, string name, Vector3 localPos, Vector3 size, Material mat)
        {
            GameObject g = GameObject.CreatePrimitive(PrimitiveType.Cube);
            g.name = name;
            g.transform.SetParent(parent, false);
            g.transform.localPosition = localPos;
            g.transform.localScale = size;
            Object.DestroyImmediate(g.GetComponent<Collider>());
            if (mat != null) g.GetComponent<Renderer>().sharedMaterial = mat;
            return g;
        }

        static void Beam(Transform parent, string name, Vector3 a, Vector3 b, float thickness, Material mat)
        {
            GameObject g = Box(parent, name, (a + b) * 0.5f, new Vector3(thickness, thickness, Vector3.Distance(a, b)), mat);
            g.transform.localRotation = Quaternion.LookRotation(b - a, Vector3.up);
        }

        static Mesh torusCache;
        static Mesh TorusMesh(float radius, float tube, int seg, int sides)
        {
            if (torusCache != null) return torusCache;
            var v = new Vector3[seg * sides];
            var n = new Vector3[seg * sides];
            var t = new int[seg * sides * 6];
            for (int i = 0; i < seg; i++)
            {
                float a = i * Mathf.PI * 2f / seg;
                Vector3 c = new Vector3(Mathf.Cos(a), Mathf.Sin(a), 0f);
                for (int j = 0; j < sides; j++)
                {
                    float b = j * Mathf.PI * 2f / sides;
                    Vector3 dir = c * Mathf.Cos(b) + Vector3.forward * Mathf.Sin(b);
                    v[i * sides + j] = c * radius + dir * tube;
                    n[i * sides + j] = dir;
                }
            }
            int k = 0;
            for (int i = 0; i < seg; i++)
            {
                int i2 = (i + 1) % seg;
                for (int j = 0; j < sides; j++)
                {
                    int j2 = (j + 1) % sides;
                    int a0 = i * sides + j, a1 = i2 * sides + j, a2 = i2 * sides + j2, a3 = i * sides + j2;
                    t[k++] = a0; t[k++] = a2; t[k++] = a1;
                    t[k++] = a0; t[k++] = a3; t[k++] = a2;
                }
            }
            torusCache = new Mesh { name = "SteeringWheelRing", vertices = v, normals = n, triangles = t };
            torusCache.RecalculateBounds();
            return torusCache;
        }
    }

    /// <summary>Rotates the cockpit steering wheel with the wheel/keyboard steering input (G920 = 900 degrees lock to lock).</summary>
    public class SteeringWheelVisual : MonoBehaviour
    {
        public LogitechG920Manager input;
        public float lockToLockDegrees = 900f;

        private void LateUpdate()
        {
            if (input == null) input = LogitechG920Manager.Instance;
            float steer = input != null ? input.Steering : 0f;
            transform.localRotation = Quaternion.Euler(0f, 0f, -steer * lockToLockDegrees * 0.5f);
        }
    }
}
