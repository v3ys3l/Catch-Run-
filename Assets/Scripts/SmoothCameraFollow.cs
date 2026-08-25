using UnityEngine;

public class SmoothCameraFollow : MonoBehaviour
{
    public Transform target;
    public float smoothSpeed = 0.125f;
    public Vector3 offset = new Vector3(0, 0, -10); // Default Z offset for 2D top-down

    private Vector3 velocity = Vector3.zero; // Used by SmoothDamp

    void LateUpdate()
    {
        if (target != null)
        {
            Vector3 desiredPosition = target.position + offset;
            // Using Vector3.Lerp for a simpler smooth follow
            // Vector3 smoothedPosition = Vector3.Lerp(transform.position, desiredPosition, smoothSpeed * Time.deltaTime);
            // transform.position = smoothedPosition;

            // Using Vector3.SmoothDamp for a more configurable and often better feeling smooth follow
            // The 'velocity' variable is a reference used by SmoothDamp to track current velocity, leave it as is.
            transform.position = Vector3.SmoothDamp(transform.position, desiredPosition, ref velocity, smoothSpeed);
        }
    }

    public void SetTarget(Transform newTarget)
    {
        target = newTarget;
        // Optional: Immediately snap to target when it's first set
        if (target != null)
        {
            Vector3 desiredPosition = target.position + offset;
            transform.position = desiredPosition; 
            velocity = Vector3.zero; // Reset velocity for SmoothDamp
        }
        Debug.Log($"[SmoothCameraFollow] Target set to: {(target != null ? target.name : "null")}");
    }
}
