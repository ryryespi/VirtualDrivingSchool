using UnityEngine;

namespace VirtualDrivingSchool.Cockpit
{
    /// <summary>
    /// Renders dynamic reflection textures for driver-side, passenger-side,
    /// and interior rear-view mirrors, crucial for reversing and parallel parking.
    /// </summary>
    public class MirrorCamera : MonoBehaviour
    {
        public enum MirrorType
        {
            DriverSideLeft,
            PassengerSideRight,
            InteriorRearView
        }

        public MirrorType mirrorType;
        public Renderer mirrorGlassRenderer;
        public Camera mirrorCam;
        public int textureResolution = 512;

        private RenderTexture renderTex;

        private void Start()
        {
            if (mirrorCam == null)
            {
                GameObject camObj = new GameObject($"{mirrorType}_Camera");
                camObj.transform.SetParent(transform, false);
                mirrorCam = camObj.AddComponent<Camera>();
            }

            // Configure mirror camera
            mirrorCam.fieldOfView = (mirrorType == MirrorType.InteriorRearView) ? 45f : 55f;
            mirrorCam.nearClipPlane = 0.2f;
            mirrorCam.farClipPlane = 120f;
            mirrorCam.depth = -1; // Render before main camera

            // Create high-performance RenderTexture
            renderTex = new RenderTexture(textureResolution, textureResolution, 16, RenderTextureFormat.ARGB32);
            renderTex.Create();
            mirrorCam.targetTexture = renderTex;

            // Apply texture to mirror surface material
            if (mirrorGlassRenderer != null)
            {
                mirrorGlassRenderer.material.mainTexture = renderTex;
            }
        }

        private void OnDestroy()
        {
            if (renderTex != null)
            {
                renderTex.Release();
                Destroy(renderTex);
            }
        }
    }
}
