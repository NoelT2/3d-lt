using UnityEngine;

[RequireComponent(typeof(Rigidbody))]
public class FlyerMovement : MonoBehaviour, IEnemyMovement
{
    [Header("Target")]
    [SerializeField] private Transform target;

    [Header("Movement")]
    [SerializeField] private float moveSpeed = 5f;
    [SerializeField] private float stoppingDistance = 6f;
    [SerializeField] private float acceleration = 10f;

    [Header("Hover Height")]
    [SerializeField] private float preferredHeightAboveTarget = 3f;
    [SerializeField] private LayerMask groundMask = ~0;
    [SerializeField] private float minHeightAboveGround = 2f;

    [Header("Bump Recovery")]
    [Tooltip("Relative collision speed above this triggers a brief hands-off recovery via EnemyKnockdownState.")]
    [SerializeField] private float bumpSpeedThreshold = 3f;
    [SerializeField] private LayerMask bumpDetectMask = ~0;

    private Rigidbody rb;
    private EnemyKnockdownState knockdownState;

    public bool HasTarget => target != null;
    public float DistanceToTarget => target != null ? Vector3.Distance(transform.position, target.position) : float.MaxValue;
    public Transform Target => target;

    // Flyers already hover at stoppingDistance, so no kiting logic is needed
    public void SetDesiredRange(float min, float max) { }

    // FlyerMovement never rotates the body itself, so there's nothing to hand over
    public void SetManualRotation(bool manual) { }

    private void Awake()
    {
        rb = GetComponent<Rigidbody>();
        rb.useGravity = false; // flyers hold altitude themselves, not via physics gravity

        knockdownState = GetComponent<EnemyKnockdownState>();

        if (target == null)
        {
            GameObject player = GameObject.FindGameObjectWithTag("Player");
            if (player != null) target = player.transform;
        }
    }

    private void FixedUpdate()
    {
        // While recovering from a bump/blast/pull, go completely hands-off and
        // let EnemyKnockdownState + physics resolve it — overwriting velocity
        // here every frame is exactly what caused the wobble/fighting before.
        if (knockdownState != null && knockdownState.State != EnemyKnockdownState.KnockState.Normal)
            return;

        if (target == null)
        {
            ApplyGroundClamp(Vector3.zero);
            return;
        }

        Vector3 desiredPosition = target.position + Vector3.up * preferredHeightAboveTarget;
        Vector3 toDesired = desiredPosition - transform.position;
        float distanceToTarget = Vector3.Distance(transform.position, target.position);

        Vector3 desiredVelocity;

        if (distanceToTarget <= stoppingDistance)
        {
            Vector3 heightOnly = new Vector3(0f, toDesired.y, 0f);
            desiredVelocity = heightOnly.normalized * Mathf.Min(moveSpeed, heightOnly.magnitude);
        }
        else
        {
            desiredVelocity = toDesired.normalized * moveSpeed;
        }

        Vector3 newVelocity = Vector3.MoveTowards(rb.linearVelocity, desiredVelocity, acceleration * Time.fixedDeltaTime);
        ApplyGroundClamp(newVelocity);
    }

    private void OnCollisionEnter(Collision collision)
    {
        if (knockdownState == null)
            return;

        if ((bumpDetectMask.value & (1 << collision.gameObject.layer)) == 0)
            return;

        float impactSpeed = collision.relativeVelocity.magnitude;

        if (impactSpeed >= bumpSpeedThreshold)
        {
            // Reuse the same force-magnitude gate the knockdown system already
            // uses elsewhere (bullets, blast, pull) — this just adds "flying
            // into something" as another valid trigger source.
            knockdownState.NotifyForceApplied(impactSpeed * rb.mass);
        }
    }

    private void ApplyGroundClamp(Vector3 velocity)
    {
        if (Physics.Raycast(transform.position, Vector3.down, out RaycastHit hit, 50f, groundMask, QueryTriggerInteraction.Ignore))
        {
            float heightAboveGround = hit.distance;

            if (heightAboveGround < minHeightAboveGround && velocity.y < 0f)
            {
                velocity.y = 0f;
            }
        }

        rb.linearVelocity = velocity;
    }

    public bool IsInRange(float range) => DistanceToTarget <= range;
}