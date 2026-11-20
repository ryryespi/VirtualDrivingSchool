using UnityEngine;
using UnityEngine.Rendering.Universal;
using VirtualDrivingSchool.Rendering;

namespace VirtualDrivingSchool.Cockpit
{
    /// <summary>
    /// Renders dynamic reflection textures for driver-side, passenger-side,
    /// and interior rear-view mirrors, crucial for reversing and parallel parking.
    /// Low-end friendly: 256 px and every other frame on Low quality, no shadows or
    /// post-processing in mirror views, short draw distance.
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
        public int lowQualityResolution = 256;

        private RenderTexture renderTex;
        private bool lowQuality;
        private int frameOffset;
        private static int mirrorCount;

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
            mirrorCam.depth = -1; // Render before main camera
            mirrorCam.allowHDR = false;
            mirrorCam.allowMSAA = false;
            var data = mirrorCam.GetUniversalAdditionalCameraData();
            if (data != null)
            {
                data.renderShadows = false;
                data.renderPostProcessing = false;
                data.antialiasing = AntialiasingMode.None;
                data.requiresDepthTexture = false;
                data.requiresColorTexture = false;
            }
            frameOffset = mirrorCount++; // alternate mirrors on different frames

            ApplyQuality(GraphicsQualityManager.IsHigh);
            GraphicsQualityManager.QualityChanged += ApplyQuality;
        }

        private void ApplyQuality(bool high)
        {
            if (mirrorCam == null) return;
            lowQuality = !high;
            int res = high ? textureResolution : lowQualityResolution;
            mirrorCam.farClipPlane = high ? 120f : 80f;

            if (renderTex == null || renderTex.width != res)
            {
                if (renderTex != null)
                {
                    mirrorCam.targetTexture = null;
                    renderTex.Release();
                    Destroy(renderTex);
                }
                renderTex = new RenderTexture(res, res / 2, 16, RenderTextureFormat.ARGB32);
                renderTex.name = $"{mirrorType}_RT";
                renderTex.Create();
                mirrorCam.targetTexture = renderTex;

                // Apply texture to mirror surface material
                if (mirrorGlassRenderer != null)
                {
                    mirrorGlassRenderer.material.mainTexture = renderTex;
                }
            }
            mirrorCam.enabled = true;
        }

        private void LateUpdate()
        {
            if (mirrorCam == null) return;
            // Low quality: refresh each mirror every other frame (mirrors alternate) to halve their cost.
            mirrorCam.enabled = !lowQuality || ((Time.frameCount + frameOffset) % 2 == 0);
        }

        private void OnDestroy()
        {
            GraphicsQualityManager.QualityChanged -= ApplyQuality;
            if (renderTex != null)
            {
                renderTex.Release();
                Destroy(renderTex);
            }
        }
    }
}
