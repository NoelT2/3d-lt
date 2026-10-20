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
        if ((bounceLayers.value & (1 << collision.gameObject.layer)) == 0)
            return;

        if (bounceCount >= maxBounces)
        {
            Destroy(gameObject);
            return;
        }

        ContactPoint contact = collision.GetContact(0);

        Vector3 incomingVelocity = collision.relativeVelocity;

        if (incomingVelocity.sqrMagnitude < 0.001f)
        {
            incomingVelocity = rb.linearVelocity;
        }

        float speed = incomingVelocity.magnitude;

        if (speed <= 0.01f)
            return;

        Vector3 reflectedDirection =
            Vector3.Reflect(incomingVelocity.normalized, contact.normal);

        bounceCount++;

        transform.position = contact.point + contact.normal * surfaceOffset;

        rb.linearVelocity = reflectedDirection * speed;
        rb.angularVelocity = Vector3.zero;

        transform.rotation = Quaternion.LookRotation(reflectedDirection);
    }
}