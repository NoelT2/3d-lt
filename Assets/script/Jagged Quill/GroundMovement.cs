using UnityEngine;
using UnityEngine.AI;

[RequireComponent(typeof(NavMeshAgent))]
[RequireComponent(typeof(Rigidbody))]
public class GroundMovement : MonoBehaviour, IEnemyMovement
{
    [Header("Target")]
    [SerializeField] private Transform target;

    [Header("Movement")]
    [SerializeField] private float moveSpeed = 3.5f;
    [SerializeField] private float stoppingDistance = 2f;

    [Header("Hit Flinch")]
    [Tooltip("While suppressed, the agent stops overwriting velocity so knockback impulses can actually play out.")]
    private float velocityOverrideSuppressedUntil;

    [Header("Ranged Kiting")]
    private float desiredMin;
    private float desiredMax;

    private NavMeshAgent agent;
    private Rigidbody rb;

    public bool HasTarget => target != null;
    public float DistanceToTarget => target != null ? Vector3.Distance(transform.position, target.position) : float.MaxValue;

    public Transform Target => target;

    public void SetManualRotation(bool manual)
    {
        agent.updateRotation = !manual;
    }

    private void Awake()
    {
        agent = GetComponent<NavMeshAgent>();
        rb = GetComponent<Rigidbody>();

        agent.speed = moveSpeed;
        agent.stoppingDistance = stoppingDistance;

        agent.updatePosition = false;
        agent.updateRotation = true;

        if (target == null)
        {
            GameObject player = GameObject.FindGameObjectWithTag("Player");
            if (player != null) target = player.transform;
        }
    }

    private void OnEnable()
    {
        if (agent != null && agent.isOnNavMesh)
        {
            agent.Warp(rb != null ? rb.position : transform.position);
        }
    }

    public void SetDesiredRange(float min, float max) { desiredMin = min; desiredMax = max; }

    private void Update()
    {
        if (target == null || !agent.enabled || !agent.isOnNavMesh)
            return;

        if (desiredMax > 0f)
        {
            float dist = DistanceToTarget;

            if (dist < desiredMin)
            {
                // too close: back off
                Vector3 away = (transform.position - target.position).normalized;
                agent.SetDestination(transform.position + away * (desiredMin - dist));
            }
            else if (dist > desiredMax)
            {
                // too far: close in
                agent.SetDestination(target.position);
            }
            else
            {
                // in the engagement window: hold ground, stop pathing toward the player
                agent.SetDestination(transform.position);
            }
        }
        else
        {
            // no ranged kiting configured (melee enemy): unchanged, always close distance
            agent.SetDestination(target.position);
        }
    }

    public void SuppressMovementOverride(float duration)
    {
        velocityOverrideSuppressedUntil = Mathf.Max(velocityOverrideSuppressedUntil, Time.time + duration);
    }

    private void FixedUpdate()
    {
        if (!agent.enabled || !agent.isOnNavMesh)
            return;

        if (Time.time < velocityOverrideSuppressedUntil)
        {
            // Hands-off: let the impulse from TakeDamage play out under normal
            // rigidbody physics/drag instead of being overwritten every frame.
            // Keep the agent's internal path state synced so it doesn't drift
            // out of sync with where physics actually put the body.
            agent.nextPosition = rb.position;
            return;
        }

        // Drive horizontal velocity from the agent's desired direction instead of
        // teleporting via MovePosition. The Rigidbody stays fully dynamic, so
        // gravity, bullet knockback, bowling collisions, and dive-stomp AOE all
        // still apply normally, while the agent still follows its NavMesh path.
        // When the path is blocked/unreachable, desiredVelocity naturally drops
        // toward zero instead of fighting position every frame — no more jitter.
        Vector3 desiredVelocity = agent.desiredVelocity;

        rb.linearVelocity = new Vector3(desiredVelocity.x, rb.linearVelocity.y, desiredVelocity.z);

        // Keep the agent's internal path state synced to where physics actually
        // put the body, since we're no longer using agent.nextPosition/MovePosition.
        agent.nextPosition = rb.position;
    }

    public bool IsInRange(float range) => DistanceToTarget <= range;

    private void OnDrawGizmosSelected()
    {
        if (desiredMax <= 0f) return; // melee-only enemy: no kiting configured, draw nothing

        Gizmos.color = Color.red;
        Gizmos.DrawWireSphere(transform.position, desiredMin);

        Gizmos.color = Color.yellow;
        Gizmos.DrawWireSphere(transform.position, desiredMax);
    }
}