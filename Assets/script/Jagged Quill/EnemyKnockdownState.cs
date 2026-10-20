using UnityEngine;

[RequireComponent(typeof(Rigidbody))]
public class EnemyKnockdownState : MonoBehaviour
{
    [Header("Knockdown Trigger")]
    [SerializeField] private float knockdownForceThreshold = 6f;

    [Header("Recovery")]
    [SerializeField] private float settleVelocityThreshold = 0.3f;
    [SerializeField] private float settleDuration = 0.35f;
    [SerializeField] private float getUpDuration = 0.6f;

    [Header("Ground Check")]
    [SerializeField] private LayerMask groundMask = ~0;
    [SerializeField] private float groundCheckDistance = 0.3f;

    [Header("References (optional)")]
    [SerializeField] private MonoBehaviour[] aiScriptsToDisable;

    public enum KnockState { Normal, KnockedDown, GettingUp }
    public KnockState State { get; private set; } = KnockState.Normal;

    private Rigidbody rb;
    private UnityEngine.AI.NavMeshAgent agent;
    private FlyerMovement flyer;
    private float settledTimer;
    private float getUpTimer;

    private RigidbodyConstraints normalConstraints;
    private Quaternion getUpStartRotation;

    private void Awake()
    {
        rb = GetComponent<Rigidbody>();
        agent = GetComponent<UnityEngine.AI.NavMeshAgent>();
        flyer = GetComponent<FlyerMovement>();
        normalConstraints = rb.constraints;
    }

    public void NotifyForceApplied(float forceMagnitude)
    {
        if (forceMagnitude < knockdownForceThreshold)
            return;

        if (State == KnockState.Normal || State == KnockState.GettingUp)
        {
            EnterKnockdown();
        }
        else
        {
            settledTimer = 0f;
        }
    }

    private void EnterKnockdown()
    {
        State = KnockState.KnockedDown;
        settledTimer = 0f;

        SetAIEnabled(false);

        if (agent != null)
            agent.enabled = false;

        // Flyers hold altitude via useGravity = false while flying normally.
        // Turn gravity ON during knockdown so a blasted/dropped flyer actually
        // falls and tumbles like a proper ragdoll instead of hanging in place.
        if (flyer != null)
        {
            rb.useGravity = true;
        }

        rb.constraints = RigidbodyConstraints.None;
    }

    private void FixedUpdate()
    {
        if (State == KnockState.KnockedDown)
        {
            bool isGrounded = Physics.Raycast(
                transform.position, Vector3.down,
                groundCheckDistance, groundMask,
                QueryTriggerInteraction.Ignore);

            // Flyers don't need to touch the ground to be "settled" — resting
            // on velocity/angular velocity alone is enough, since they may
            // tumble to a stop mid-air or on the ground depending on the hit.
            bool isSettled = (flyer != null || isGrounded) &&
                              rb.linearVelocity.magnitude < settleVelocityThreshold &&
                              rb.angularVelocity.magnitude < settleVelocityThreshold;

            settledTimer = isSettled ? settledTimer + Time.fixedDeltaTime : 0f;

            if (settledTimer >= settleDuration)
            {
                State = KnockState.GettingUp;
                getUpTimer = 0f;
                getUpStartRotation = rb.rotation;
                // trigger your stand-up / recover animation here
            }
        }
        else if (State == KnockState.GettingUp)
        {
            getUpTimer += Time.fixedDeltaTime;

            float t = Mathf.Clamp01(getUpTimer / getUpDuration);
            Quaternion uprightYawOnly = Quaternion.Euler(0f, getUpStartRotation.eulerAngles.y, 0f);
            rb.MoveRotation(Quaternion.Slerp(getUpStartRotation, uprightYawOnly, t));

            if (getUpTimer >= getUpDuration)
            {
                State = KnockState.Normal;

                rb.constraints = normalConstraints;
                rb.angularVelocity = Vector3.zero;
                rb.MoveRotation(Quaternion.Euler(0f, getUpStartRotation.eulerAngles.y, 0f));

                if (flyer != null)
                {
                    // Hand control back to FlyerMovement's own hover logic —
                    // it will naturally fly back up to preferredHeightAboveTarget
                    // from wherever it landed. No NavMesh snapping needed.
                    rb.useGravity = false;
                    SetAIEnabled(true);
                }
                else if (agent != null)
                {
                    RigidbodyInterpolation originalInterpolation = rb.interpolation;
                    rb.interpolation = RigidbodyInterpolation.None;

                    if (UnityEngine.AI.NavMesh.SamplePosition(rb.position, out var hit, 2f, UnityEngine.AI.NavMesh.AllAreas))
                    {
                        rb.position = hit.position;
                        agent.enabled = true;
                    }
                    else
                    {
                        agent.enabled = true;
                    }

                    rb.interpolation = originalInterpolation;
                    SetAIEnabled(true);
                }
                else
                {
                    SetAIEnabled(true);
                }
            }
        }
    }

    private void SetAIEnabled(bool enabled)
    {
        if (aiScriptsToDisable == null) return;
        foreach (var script in aiScriptsToDisable)
        {
            if (script != null) script.enabled = enabled;
        }
    }
}