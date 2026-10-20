using UnityEngine;

/// <summary>
/// Placeholder for the wind effect: puts a ring sprite in the direction the player is
/// actually moving. Works with walking, dash, super dash, grapple and dive because it
/// measures the real position change instead of reading any one script's velocity.
/// Put this on the Player object (the one with Movement / CharacterController).
/// </summary>
public class MomentumMarker : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private Transform ring;          // the ring sprite object
    [SerializeField] private Transform playerCamera;  // leave empty to use Camera.main

    [Header("Marker")]
    [SerializeField] private float distance = 6f;     // how far in front of the camera the ring sits
    [SerializeField] private float minSpeed = 2f;     // ring hides below this speed
    [SerializeField] private float smoothing = 15f;   // higher = snappier, lower = smoother

    [Header("Debug")]
    [SerializeField] private float currentSpeed;      // read-only, to watch in the Inspector

    // Other scripts (the future wind effect) can read these.
    public Vector3 MomentumVelocity { get; private set; }
    public Vector3 MomentumDirection { get; private set; }
    public float Speed => MomentumVelocity.magnitude;

    private Vector3 lastPosition;

    private void Start()
    {
        lastPosition = transform.position;
        if (playerCamera == null && Camera.main != null)
            playerCamera = Camera.main.transform;
    }

    private void LateUpdate()
    {
        float dt = Time.deltaTime;
        if (dt <= 0.0001f) return; // paused

        Vector3 rawVelocity = (transform.position - lastPosition) / dt;
        lastPosition = transform.position;

        // Ignore teleports (respawn / unstuck) so the ring doesn't spike.
        if (rawVelocity.magnitude > 400f) rawVelocity = Vector3.zero;

        MomentumVelocity = Vector3.Lerp(MomentumVelocity, rawVelocity, 1f - Mathf.Exp(-smoothing * dt));
        currentSpeed = MomentumVelocity.magnitude;

        if (ring == null || playerCamera == null) return;

        bool moving = currentSpeed > minSpeed;
        if (ring.gameObject.activeSelf != moving) ring.gameObject.SetActive(moving);
        if (!moving) return;

        MomentumDirection = MomentumVelocity.normalized;
        ring.position = playerCamera.position + MomentumDirection * distance;
        ring.rotation = playerCamera.rotation; // billboard: always faces the camera
    }
}
