using UnityEngine;
using VirtualDrivingSchool.Input;

namespace VirtualDrivingSchool.Cockpit
{
    public enum CameraViewMode
    {
        CockpitInterior,
        ChaseThirdPerson,
        HoodBumper,
        OverheadParking
    }

    /// <summary>
    /// Multi-mode camera controller supporting realistic driver head movement,
    /// mirror glancing, and external training views.
    /// </summary>
    public class CockpitCameraController : MonoBehaviour
    {
        [Header("Target & View Anchors")]
        public Transform vehicleTransform;
        public Transform cockpitDriverHeadAnchor;
        public Transform hoodAnchor;
        public Transform chaseAnchor;
        public Transform overheadAnchor;

        [Header("Camera Settings")]
        public Camera targetCamera;
        public CameraViewMode currentMode = CameraViewMode.CockpitInterior;

        [Header("Cockpit Head Inertia & Look Around")]
        public float headBobIntensity = 0.05f;
        public float glanceAngle = 70f;
        public float glanceSmoothSpeed = 12f;

        private Vector3 defaultLocalHeadPos;
        private float currentLookYaw = 0f;
        private Rigidbody vehicleRb;

        private void Start()
        {
            if (targetCamera == null) targetCamera = Camera.main;
            if (vehicleTransform != null) vehicleRb = vehicleTransform.GetComponent<Rigidbody>();
            if (cockpitDriverHeadAnchor != null) defaultLocalHeadPos = cockpitDriverHeadAnchor.localPosition;
        }

        private void Update()
        {
            LogitechG920Manager input = LogitechG920Manager.Instance;
            if (input != null && input.ChangeCameraView)
            {
                CycleCameraMode();
            }

            // Look left / right inputs for mirrors and blind spots
            float targetYaw = 0f;
            if (input != null)
            {
                if (input.LookLeft) targetYaw = -glanceAngle;
                else if (input.LookRight) targetYaw = glanceAngle;
                else if (input.LookBehind) targetYaw = 175f;
            }

            currentLookYaw = Mathf.Lerp(currentLookYaw, targetYaw, Time.deltaTime * glanceSmoothSpeed);
        }

        private void LateUpdate()
        {
            if (vehicleTransform == null || targetCamera == null) return;

            switch (currentMode)
            {
                case CameraViewMode.CockpitInterior:
                    UpdateCockpitView();
                    break;
                case CameraViewMode.ChaseThirdPerson:
                    UpdateChaseView();
                    break;
                case CameraViewMode.HoodBumper:
                    UpdateHoodView();
                    break;
                case CameraViewMode.OverheadParking:
                    UpdateOverheadView();
                    break;
            }
        }

        private void UpdateCockpitView()
        {
            if (cockpitDriverHeadAnchor == null) return;

            // Apply subtle G-force displacement
            Vector3 accelOffset = Vector3.zero;
            if (vehicleRb != null)
            {
                Vector3 localVel = vehicleTransform.InverseTransformDirection(vehicleRb.linearVelocity);
                accelOffset = new Vector3(-localVel.x * 0.005f, 0f, -localVel.z * 0.003f);
            }

            targetCamera.transform.position = cockpitDriverHeadAnchor.position + vehicleTransform.TransformDirection(accelOffset);
            targetCamera.transform.rotation = cockpitDriverHeadAnchor.rotation * Quaternion.Euler(0f, currentLookYaw, 0f);
        }

        private void UpdateChaseView()
        {
            if (chaseAnchor == null) return;
            targetCamera.transform.position = Vector3.Lerp(targetCamera.transform.position, chaseAnchor.position, Time.deltaTime * 10f);
            targetCamera.transform.LookAt(vehicleTransform.position + vehicleTransform.forward * 2f + Vector3.up * 0.8f);
        }

        private void UpdateHoodView()
        {
            if (hoodAnchor == null) return;
            targetCamera.transform.position = hoodAnchor.position;
            targetCamera.transform.rotation = hoodAnchor.rotation;
        }

        private void UpdateOverheadView()
        {
            if (overheadAnchor == null) return;
            targetCamera.transform.position = overheadAnchor.position;
            targetCamera.transform.rotation = overheadAnchor.rotation;
        }

        public void CycleCameraMode()
        {
            currentMode = (CameraViewMode)(((int)currentMode + 1) % 4);
        }
    }
}
