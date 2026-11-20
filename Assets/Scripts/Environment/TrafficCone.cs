using UnityEngine;
using VirtualDrivingSchool.Lessons;

namespace VirtualDrivingSchool.Environment
{
    /// <summary>
    /// Interactive physics traffic cone that tumbles when struck,
    /// triggers audio, and records driving school collision penalties.
    /// </summary>
    [RequireComponent(typeof(Rigidbody))]
    public class TrafficCone : MonoBehaviour
    {
        public AudioClip knockSound;
        private AudioSource audioSource;
        private Rigidbody rb;
        private bool hasCollided = false;

        private void Awake()
        {
            rb = GetComponent<Rigidbody>();
            rb.mass = 4f; // Light rubber cone weight
            rb.centerOfMass = new Vector3(0f, -0.3f, 0f); // Low center of gravity

            audioSource = GetComponent<AudioSource>();
            if (audioSource == null) audioSource = gameObject.AddComponent<AudioSource>();
            audioSource.spatialBlend = 1f;
        }

        private void OnCollisionEnter(Collision collision)
        {
            if (hasCollided) return;

            if (collision.gameObject.CompareTag("Player") || collision.gameObject.GetComponentInParent<Vehicle.ManualCarController>() != null)
            {
                hasCollided = true;

                if (knockSound != null)
                {
                    audioSource.PlayOneShot(knockSound, 0.8f);
                }

                if (DrivingLessonManager.Instance != null)
                {
                    DrivingLessonManager.Instance.RecordConeCollision();
                }

                // Add an impulse based on impact
                rb.AddForce(collision.relativeVelocity * 1.5f, ForceMode.Impulse);
            }
        }
    }
}
