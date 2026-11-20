using UnityEngine;
using UnityEngine.Rendering;

namespace VirtualDrivingSchool.Rendering
{
    /// <summary>
    /// Central list of every model and material the runtime world/car builders use.
    /// Lives in Resources/VDSArtLibrary so all referenced meshes, materials and shaders
    /// are always included in player builds (no Shader.Find on strippable shaders).
    /// Generated/refreshed by Tools > Virtual Driving School > Setup Graphics (URP).
    /// </summary>
    [CreateAssetMenu(menuName = "Virtual Driving School/Art Library", fileName = "VDSArtLibrary")]
    public class VdsArtLibrary : ScriptableObject
    {
        public const string ResourcePath = "VDSArtLibrary";

        [Header("Vehicles (Kenney Car Kit, CC0)")]
        public GameObject playerCarBody;
        public GameObject wheel;
        public GameObject[] parkedCars;
        public GameObject cone;

        [Header("Street props (Kenney City Kit Roads, CC0)")]
        public GameObject stopSign;
        public GameObject streetLight;
        public GameObject barrier;
        public GameObject trafficLight;

        [Header("Scenery (Kenney Suburban + Nature Kits, CC0)")]
        public GameObject[] houses;
        public GameObject fence;
        public GameObject[] trees;
        public GameObject[] bushes;
        public GameObject[] rocks;

        [Header("Materials (URP Simple Lit / Unlit)")]
        public Material grass;
        public Material lotAsphalt;
        public Material roadAsphalt;
        public Material concrete;
        public Material lineWhite;
        public Material lineYellow;
        public Material interiorDark;
        public Material interiorTrim;
        public Material mirrorGlass;
        public Material signRed;
        public Material fallbackPaint;
        public Material skybox;

        [Header("Post-processing (High quality only)")]
        public VolumeProfile highQualityProfile;

        private static VdsArtLibrary cached;
        private static bool searched;

        /// <summary>Returns the library, or null if the setup tool has not been run (callers fall back to primitives).</summary>
        public static VdsArtLibrary Get()
        {
            if (!searched)
            {
                cached = Resources.Load<VdsArtLibrary>(ResourcePath);
                searched = true;
                if (cached == null) Debug.LogWarning("[VDS] VDSArtLibrary not found in Resources. Run Tools > Virtual Driving School > Setup Graphics (URP).");
            }
            return cached;
        }

        public GameObject Pick(GameObject[] list, int index)
        {
            if (list == null || list.Length == 0) return null;
            return list[Mathf.Abs(index) % list.Length];
        }
    }
}
