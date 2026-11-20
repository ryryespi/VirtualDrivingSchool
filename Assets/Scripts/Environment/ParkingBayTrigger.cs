using UnityEngine;
using VirtualDrivingSchool.Lessons;
using VirtualDrivingSchool.Vehicle;

namespace VirtualDrivingSchool.Environment
{
    /// <summary>
    /// Evaluates parking alignment, centering between lines, and curb proximity
    /// inside perpendicular and parallel parking stalls.
    /// </summary>
    public class ParkingBayTrigger : MonoBehaviour
    {
        [Header("Bay Attributes")]
        public bool isParallelParkingBay = false;
        public Vector3 intendedForwardDirection = Vector3.forward;
        public float maxAcceptableAngleError = 15f; // degrees
        public float maxAcceptableCenterOffset = 0.6f; // meters

        [Header("Live Evaluation")]
        public bool carInside = false;
        public float currentAlignmentScore = 0f;

        private ManualCarController currentCar;
        private float stationaryTimer = 0f;

        private void OnTriggerEnter(Collider other)
        {
            ManualCarController car = other.GetComponentInParent<ManualCarController>();
            if (car != null)
            {
                currentCar = car;
                carInside = true;
                stationaryTimer = 0f;
            }
        }

        private void OnTriggerStay(Collider other)
        {
            if (currentCar == null) return;

            // Check if car has come to a complete stop inside the bay
            if (Mathf.Abs(currentCar.SpeedMph) < 0.3f)
            {
                stationaryTimer += Time.deltaTime;
                if (stationaryTimer >= 1.2f)
                {
                    EvaluateParking(currentCar);
                }
            }
            else
            {
                stationaryTimer = 0f;
            }
        }

        private void OnTriggerExit(Collider other)
        {
            ManualCarController car = other.GetComponentInParent<ManualCarController>();
            if (car != null && car == currentCar)
            {
                currentCar = null;
                carInside = false;
                stationaryTimer = 0f;
            }
        }

        private void EvaluateParking(ManualCarController car)
        {
            Vector3 worldIntendedDir = transform.TransformDirection(intendedForwardDirection).normalized;
            Vector3 carForward = car.transform.forward;

            // For parallel parking, car could park forward or reverse aligned
            float angleDelta = Vector3.Angle(carForward, worldIntendedDir);
            if (isParallelParkingBay && angleDelta > 90f)
            {
                angleDelta = Vector3.Angle(-carForward, worldIntendedDir);
            }

            // Angular score (0 to 100%)
            float angleScore = Mathf.Clamp01(1f - (angleDelta / maxAcceptableAngleError)) * 100f;

            // Lateral centering score
            Vector3 localPos = transform.InverseTransformPoint(car.transform.position);
            float lateralOffset = Mathf.Abs(localPos.x);
            float positionScore = Mathf.Clamp01(1f - (lateralOffset / maxAcceptableCenterOffset)) * 100f;

            // Combined weighted score
            currentAlignmentScore = (angleScore * 0.6f) + (positionScore * 0.4f);

            if (DrivingLessonManager.Instance != null)
            {
                DrivingLessonManager.Instance.NotifyParkedInBay(currentAlignmentScore, isParallelParkingBay);
            }
        }

        private void OnDrawGizmos()
        {
            Gizmos.color = isParallelParkingBay ? Color.yellow : Color.cyan;
            Gizmos.DrawWireCube(transform.position, transform.lossyScale);
            Gizmos.color = Color.blue;
            Gizmos.DrawRay(transform.position, transform.TransformDirection(intendedForwardDirection) * 2f);
        }
    }
}
