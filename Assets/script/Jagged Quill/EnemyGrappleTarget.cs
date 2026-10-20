using System.Collections.Generic;
using UnityEngine;

[RequireComponent(typeof(Rigidbody))]
[RequireComponent(typeof(EnemyHealth))]
public class EnemyGrappleTarget : MonoBehaviour
{
    [Header("Grapple Settings")]
    [SerializeField] private bool canBeGrappled = true;

    [Header("Blast Impact")]
    [SerializeField] private LayerMask blastImpactEnvironmentMask;
    [Tooltip("Damage numbers live in ImpactSettings (the official stat). Here: should a thrown enemy also hit other enemies?")]
    [SerializeField] private bool collideWithEnemies = true;

    [Header("Impact Visual")]
    [SerializeField] private ParticleSystem impactEffectPrefab;
    [SerializeField] private Color impactColor =
        new Color(1f, 0.2f, 0.05f, 1f);

    // "Thrown" window: starts at the blast, ends when the enemy has slowed down / settled.
    private bool thrown;
    private float thrownTimer;
    private float slowTimer;
    private float lastImpactTime = -999f;

    private Rigidbody enemyRigidbody;
    private EnemyHealth enemyHealth;

    private bool isBeingGrappled;

    private bool hasAttachOffset;
    private Vector3 localAttachOffset;

    public bool CanBeGrappled => canBeGrappled;
    public bool IsBeingGrappled => isBeingGrappled;
    public bool IsThrown => thrown;

    public Rigidbody Body => enemyRigidbody;
    public EnemyHealth Health => enemyHealth;

    public Vector3 GrapplePosition =>
        hasAttachOffset
            ? transform.TransformPoint(localAttachOffset)
            : (enemyRigidbody != null ? enemyRigidbody.worldCenterOfMass : transform.position);

    private void Awake()
    {
        enemyRigidbody = GetComponent<Rigidbody>();
        enemyHealth = GetComponent<EnemyHealth>();

        // Debris / scrape effects for every grappleable enemy (no prefab changes needed).
        if (GetComponent<EnemyContactEffects>() == null)
            gameObject.AddComponent<EnemyContactEffects>();
    }

    public void BeginGrapple(Vector3 worldHitPoint)
    {
        if (!canBeGrappled)
            return;

        isBeingGrappled = true;
        localAttachOffset = transform.InverseTransformPoint(worldHitPoint);
        hasAttachOffset = true;

        if (enemyRigidbody != null)
        {
            enemyRigidbody.WakeUp();
        }

        bool isImmune = enemyHealth != null && enemyHealth.IsImmuneToKnockdownAndPull;

        if (!isImmune)
        {
            GetComponent<EnemyKnockdownState>()?.NotifyForceApplied(float.MaxValue);
        }
    }

    public void EndGrapple()
    {
        isBeingGrappled = false;
        hasAttachOffset = false;
    }

    public void PullToward(
        Vector3 targetPosition,
        float pullForce,
        float maxPullSpeed)
    {
        if (!isBeingGrappled ||
            enemyRigidbody == null ||
            enemyRigidbody.isKinematic ||
            (enemyHealth != null && enemyHealth.IsImmuneToKnockdownAndPull))
        {
            return;
        }

        Vector3 direction =
            targetPosition -
            enemyRigidbody.worldCenterOfMass;

        if (direction.sqrMagnitude <= 0.001f)
            return;

        direction.Normalize();

        float resistance = enemyHealth != null ? enemyHealth.KnockbackResistance : 1f;

        enemyRigidbody.AddForce(
            direction * pullForce / Mathf.Max(0.01f, resistance),
            ForceMode.Force
        );

        if (maxPullSpeed > 0f &&
            enemyRigidbody.linearVelocity.magnitude > maxPullSpeed)
        {
            enemyRigidbody.linearVelocity =
                enemyRigidbody.linearVelocity.normalized *
                maxPullSpeed;
        }
    }

    public void BlastAwayFrom(
        Vector3 sourcePosition,
        float horizontalImpulse,
        float upwardImpulse)
    {
        if (enemyRigidbody == null ||
            enemyRigidbody.isKinematic ||
            (enemyHealth != null && enemyHealth.IsImmuneToKnockdownAndPull))
        {
            return;
        }

        Vector3 awayFromSource =
            enemyRigidbody.worldCenterOfMass -
            sourcePosition;

        awayFromSource.y = 0f;

        if (awayFromSource.sqrMagnitude <= 0.001f)
        {
            awayFromSource = transform.forward;
            awayFromSource.y = 0f;
        }

        awayFromSource.Normalize();

        float resistance = enemyHealth != null ? enemyHealth.KnockbackResistance : 1f;

        Vector3 impulse =
            (awayFromSource * horizontalImpulse + Vector3.up * upwardImpulse) /
            Mathf.Max(0.01f, resistance);

        enemyRigidbody.WakeUp();
        enemyRigidbody.AddForce(impulse, ForceMode.Impulse);
        GetComponent<EnemyKnockdownState>()?.NotifyForceApplied(impulse.magnitude);

        BeginThrown();
        GetComponent<EnemyContactEffects>()?.PlayBlastDust(sourcePosition);
        GameEvents.RaiseEnemyBlast(sourcePosition);
    }

    public void StopPullingAt(Vector3 targetPosition)
    {
        if (enemyRigidbody == null ||
            enemyRigidbody.isKinematic)
        {
            return;
        }

        Vector3 directionToTarget =
            targetPosition -
            enemyRigidbody.worldCenterOfMass;

        if (directionToTarget.sqrMagnitude <= 0.001f)
            return;

        directionToTarget.Normalize();

        float velocityTowardTarget =
            Vector3.Dot(
                enemyRigidbody.linearVelocity,
                directionToTarget
            );

        if (velocityTowardTarget > 0f)
        {
            enemyRigidbody.linearVelocity -=
                directionToTarget *
                velocityTowardTarget;
        }
    }

    private void OnDisable()
    {
        isBeingGrappled = false;
    }

    private void BeginThrown()
    {
        thrown = true;
        thrownTimer = 0f;
        slowTimer = 0f;
        lastImpactTime = -999f;
    }

    private void FixedUpdate()
    {
        if (!thrown) return;

        ImpactSettings s = ImpactSettings.Current;
        thrownTimer += Time.fixedDeltaTime;

        bool slow = enemyRigidbody == null || enemyRigidbody.linearVelocity.magnitude < s.thrownEndSpeed;
        slowTimer = slow ? slowTimer + Time.fixedDeltaTime : 0f;

        if (slowTimer >= s.thrownEndDelay || thrownTimer >= s.thrownMaxDuration)
            thrown = false;
    }

    // Every hard collision while thrown deals momentum-based damage (not a one-time hit).
    private void OnCollisionEnter(Collision collision)
    {
        if (!thrown || collision.contactCount <= 0)
            return;

        ImpactSettings s = ImpactSettings.Current;

        if (Time.time - lastImpactTime < s.perContactCooldown)
            return;

        bool isEnvironment = (blastImpactEnvironmentMask.value & (1 << collision.gameObject.layer)) != 0;

        EnemyHealth otherEnemy = collision.collider.GetComponentInParent<EnemyHealth>();
        bool isOtherEnemy = otherEnemy != null && otherEnemy != enemyHealth;

        if (!isEnvironment && !(isOtherEnemy && collideWithEnemies))
            return;

        ContactPoint contact = collision.GetContact(0);

        // Only the speed INTO the surface counts, so scraping/sliding doesn't deal impact damage.
        float normalSpeed = Mathf.Abs(Vector3.Dot(collision.relativeVelocity, contact.normal));
        if (normalSpeed < s.impactSpeedThreshold)
            return;

        lastImpactTime = Time.time;

        float mass = enemyRigidbody.mass;
        float momentum = mass * normalSpeed;
        float momentumAboveThreshold = Mathf.Max(0f, momentum - mass * s.impactSpeedThreshold);

        float damage = Mathf.Clamp(
            s.baseImpactDamage + momentumAboveThreshold * s.damagePerMomentum,
            s.baseImpactDamage,
            s.maxImpactDamage);

        float intensity = Mathf.Clamp01(Mathf.InverseLerp(s.baseImpactDamage, s.maxImpactDamage, damage));

        ImpactInfo info = new ImpactInfo
        {
            enemy = enemyHealth,
            other = collision.collider,
            point = contact.point,
            normal = contact.normal,
            relativeVelocity = collision.relativeVelocity,
            normalSpeed = normalSpeed,
            momentum = momentum,
            damage = damage,
            intensity = intensity,
            hitEnvironment = !isOtherEnemy,
            surfaceColor = ImpactSurface.GetColor(collision.collider, impactColor)
        };

        GameEvents.RaiseEnemyImpact(info);

        ApplyImpactAOE(info);

        // The thrown enemy is the "rock": it takes the full (capped) impact damage itself.
        if (enemyHealth != null)
            enemyHealth.TakeCollisionDamage(damage, info, Vector3.zero, 0f);

        Debug.Log(
            $"Enemy impact at {contact.point}. Speed: {normalSpeed:F1}, Momentum: {momentum:F1}, " +
            $"Damage: {damage:F1}, Intensity: {intensity:F2}");
    }

    private void ApplyImpactAOE(ImpactInfo info)
    {
        ImpactSettings s = ImpactSettings.Current;

        float radius = Mathf.Lerp(s.aoeRadiusMin, s.aoeRadiusMax, info.intensity);
        float knockbackAtCenter = Mathf.Lerp(s.aoeKnockbackMin, s.aoeKnockbackMax, info.intensity);

        SpawnImpactEffect(info);

        Collider[] nearbyColliders =
            Physics.OverlapSphere(info.point, radius, ~0, QueryTriggerInteraction.Ignore);

        HashSet<EnemyHealth> damagedEnemies = new HashSet<EnemyHealth>();

        foreach (Collider nearbyCollider in nearbyColliders)
        {
            EnemyHealth nearbyHealth = nearbyCollider.GetComponentInParent<EnemyHealth>();

            if (nearbyHealth == null ||
                nearbyHealth == enemyHealth ||
                damagedEnemies.Contains(nearbyHealth))
            {
                continue;
            }

            damagedEnemies.Add(nearbyHealth);

            Vector3 awayFromImpact = nearbyHealth.transform.position - info.point;
            float distance = awayFromImpact.magnitude;
            float falloff = 1f - Mathf.Clamp01(distance / radius);

            if (awayFromImpact.sqrMagnitude <= 0.001f)
                awayFromImpact = info.normal;
            else
                awayFromImpact.Normalize();

            nearbyHealth.TakeCollisionDamage(
                info.damage * falloff,
                info,
                awayFromImpact,
                knockbackAtCenter * falloff);
        }
    }

    private void SpawnImpactEffect(ImpactInfo info)
    {
        if (impactEffectPrefab == null)
            return;

        Quaternion rotation =
            info.normal.sqrMagnitude > 0.001f
                ? Quaternion.LookRotation(info.normal)
                : Quaternion.identity;

        ParticleSystem effect = Instantiate(impactEffectPrefab, info.point, rotation);

        // Bigger hit = bigger effect.
        ImpactSettings s = ImpactSettings.Current;
        effect.transform.localScale *= Mathf.Lerp(s.effectScaleMin, s.effectScaleMax, info.intensity);

        ParticleSystem.MainModule main = effect.main;
        main.startColor = impactColor;

        effect.Play();

        Destroy(effect.gameObject, 5f);
    }

    private void OnDrawGizmosSelected()
    {
        Gizmos.color = new Color(impactColor.r, impactColor.g, impactColor.b, 0.25f);
        Gizmos.DrawWireSphere(transform.position, ImpactSettings.Current.aoeRadiusMax);
    }
}