using UnityEngine;

[RequireComponent(typeof(Rigidbody))]
public class BulletRicochet : MonoBehaviour
{
    [Header("Ricochet Settings")]
    [SerializeField] private LayerMask bounceLayers;
    [SerializeField] private int maxBounces = 3;
    [SerializeField] private float surfaceOffset = 0.01f;

    private Rigidbody rb;
    private int bounceCount;

    private void Awake()
    {
        rb = GetComponent<Rigidbody>();
    }

    private void OnCollisionEnter(Collision collision)
    {
        // Only bounce from selected layers, such as Environment
        if ((bounceLayers.value & (1 << collision.gameObject.layer)) == 0)
            return;

        if (bounceCount >= maxBounces)
        {
            Destroy(gameObject);
            return;
        }

        ContactPoint contact = collision.GetContact(0);

        // Use the incoming velocity before calculating the reflection
        Vector3 incomingVelocity = collision.relativeVelocity;

        if (incomingVelocity.sqrMagnitude < 0.001f)
        {
            incomingVelocity = rb.linearVelocity;
        }

        float speed = incomingVelocity.magnitude;

        if (speed <= 0.01f)
            return;

        // Mirror-like reflection
        Vector3 reflectedDirection =
            Vector3.Reflect(incomingVelocity.normalized, contact.normal);

        bounceCount++;

        // Move slightly away from the surface to avoid an immediate second collision
        transform.position = contact.point + contact.normal * surfaceOffset;

        // Preserve the bullet's speed after the bounce
        rb.linearVelocity = reflectedDirection * speed;
        rb.angularVelocity = Vector3.zero;

        // Make the bullet face its new direction
        transform.rotation = Quaternion.LookRotation(reflectedDirection);
    }
}