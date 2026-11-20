using UnityEngine;
using VirtualDrivingSchool.Lessons;

namespace VirtualDrivingSchool.Environment
{
    /// <summary>
    /// Detects curb strikes during parallel parking and tight turns.
    /// </summary>
    public class CurbStrikeDetector : MonoBehaviour
    {
        private float lastStrikeTime = 0f;

        private void OnCollisionEnter(Collision collision)
        {
            if (Time.time - lastStrikeTime < 1.0f) return;

            if (collision.gameObject.GetComponentInParent<Vehicle.ManualCarController>() != null)
            {
                lastStrikeTime = Time.time;
                if (DrivingLessonManager.Instance != null)
                {
                    DrivingLessonManager.Instance.RecordCurbStrike();
                }
            }
        }
    }
}
