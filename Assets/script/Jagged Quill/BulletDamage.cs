using UnityEngine;

public class BulletDamage : MonoBehaviour
{
    [Header("Damage")]
    [SerializeField] private float damage = 10f;

    [Header("Knockback")]
    [SerializeField] private float knockbackRange = 15f;
    [SerializeField] private float knockbackForce = 8f;

    [Header("Fast bullet hit detection")]
    [Tooltip("Each physics step the bullet checks the path it is about to travel, so it can't skip through enemies at any speed. Thickness of that check (meters).")]
    [SerializeField] private float sweepRadius = 0.05f;

    [Header("Bullet crater (environment hits) - set per bullet prefab")]
    [SerializeField] private bool leaveMarks = true;
    [Tooltip("Crater size (min..max). Charged shots scale up automatically.")]
    [SerializeField] private Vector2 markSize = new Vector2(0.35f, 0.7f);
    [Tooltip("Seconds the crater stays before fading out.")]
    [SerializeField] private float markLifetime = 10f;
    [Tooltip("Small chunks thrown out of the crater, in the surface colour (min..max).")]
    [SerializeField] private Vector2Int chunkCount = new Vector2Int(2, 5);
    [SerializeField] private float chunkSize = 0.07f;
    [SerializeField] private float chunkLifetime = 5f;
    [Tooltip("Size of the small dust puff. 0 = no dust.")]
    [SerializeField] private float dustSize = 0.8f;

    private bool hasHit = false;
    private Vector3 spawnPosition;
    private Rigidbody rb;
    private bool hasRicochet;

    private static readonly RaycastHit[] sweepHits = new RaycastHit[16];

    private float damageMultiplier = 1f;
    private float Damage
    {
        get
        {
            float mult = damageMultiplier;
            if (PlayerStats.Local != null)
                mult *= PlayerStats.Local.DamageMultiplier;
            return damage * mult;
        }
    }

    private void Awake()
    {
        spawnPosition = transform.position;
        rb = GetComponent<Rigidbody>();
        hasRicochet = GetComponent<BulletRicochet>() != null;

        LayerSetup();
    }

    // Looks ahead along the bullet's path for this physics step. Catches targets that the
    // bullet would otherwise jump over between two steps (the faster the bullet, the bigger the jump).
    private void FixedUpdate()
    {
        if (hasHit || rb == null)
            return;

        Vector3 velocity = rb.linearVelocity;
        float speed = velocity.magnitude;
        if (speed < 0.1f)
            return;

        float distance = speed * Time.fixedDeltaTime * 1.1f;
        int count = Physics.SphereCastNonAlloc(
            transform.position, sweepRadius, velocity / speed,
            sweepHits, distance, ~0, QueryTriggerInteraction.Ignore);

        float best = float.MaxValue;
        int bestIndex = -1;
        for (int i = 0; i < count; i++)
        {
            RaycastHit h = sweepHits[i];
            Collider c = h.collider;
            if (c == null || h.distance <= 0f || h.distance >= best)
                continue;   // distance 0 = already overlapping; OnTriggerEnter handles that case
            if (c.GetComponentInParent<BulletDamage>() != null)
                continue;
            if (c.GetComponentInParent<PlayerHealth>() != null)
                continue;
            // Ricochet bullets keep bouncing off the environment as before; only enemies are swept.
            if (hasRicochet && c.GetComponentInParent<EnemyHealth>() == null)
                continue;

            best = h.distance;
            bestIndex = i;
        }

        if (bestIndex >= 0)
            ProcessHit(sweepHits[bestIndex].collider, sweepHits[bestIndex].point, sweepHits[bestIndex].normal);
    }

    private void OnTriggerEnter(Collider other)
    {
        Vector3 hitPoint = other.bounds.ClosestPoint(transform.position);
        if (hasHit)
            return;
        if (other.GetComponentInParent<BulletDamage>() != null)
            return;
        if (other.GetComponentInParent<PlayerHealth>() != null)
            return;

        ProcessHit(other, hitPoint, null);
    }

    private void ProcessHit(Collider other, Vector3 hitPoint, Vector3? surfaceNormal)
    {
        if (hasHit)
            return;
        hasHit = true;
        EnemyHealth enemy = other.GetComponentInParent<EnemyHealth>();
        if (enemy != null)
        {
            float distanceFromSpawn =
                Vector3.Distance(spawnPosition, enemy.transform.position);
            if (distanceFromSpawn <= knockbackRange)
            {
                Vector3 knockbackDirection =
                    enemy.transform.position - spawnPosition;
                knockbackDirection.y = 0f;
                enemy.TakeDamage(
                    Damage,
                    knockbackDirection,
                    knockbackForce,
                    hitPoint
                );
                Debug.Log(
                    $"Enemy hit within knockback range: " +
                    $"{distanceFromSpawn:F1}m"
                );
            }
            else
            {
                enemy.TakeDamage(Damage);
                Debug.Log(
                    $"Enemy hit outside knockback range: " +
                    $"{distanceFromSpawn:F1}m"
                );
            }
        }
        else
        {
            if (leaveMarks)
            {
                try { LeaveMark(other, hitPoint, surfaceNormal); }
                catch (System.Exception e) { Debug.LogWarning($"Bullet mark failed: {e.Message}"); }
            }
            Debug.Log($"Bullet hit environment: {other.gameObject.name}");
        }
        Destroy(gameObject);
    }

    // Stamps a small crater (patch, chunks, dust) in the colour of the surface that was hit.
    private void LeaveMark(Collider other, Vector3 hitPoint, Vector3? surfaceNormal)
    {
        // Props that can move would leave marks floating in the air.
        if (other.attachedRigidbody != null && !other.attachedRigidbody.isKinematic)
            return;

        Vector3 velocity = rb != null ? rb.linearVelocity : Vector3.zero;
        Vector3 dir = velocity.sqrMagnitude > 0.01f ? velocity.normalized : transform.forward;

        Vector3 point = hitPoint;
        Vector3 normal;

        if (surfaceNormal.HasValue)
        {
            normal = surfaceNormal.Value;   // exact, from the look-ahead check
        }
        else
        {
            // Trigger-event path: find the surface with a short ray back along the bullet's path.
            float back = Mathf.Max(0.75f, velocity.magnitude * Time.fixedDeltaTime * 2f);
            if (other.Raycast(new Ray(transform.position - dir * back, dir), out RaycastHit hit, back + 1f))
            {
                point = hit.point;
                normal = hit.normal;
            }
            else
            {
                normal = -dir;   // bullet started inside the collider: approximate
            }
        }

        if (Vector3.Dot(normal, dir) > 0f) normal = -normal;

        float chargeScale = Mathf.Lerp(1f, 2.5f, Mathf.InverseLerp(1f, 10f, damageMultiplier));
        float size = Random.Range(markSize.x, markSize.y) * chargeScale;
        int chunks = Mathf.RoundToInt(Random.Range(chunkCount.x, chunkCount.y + 1) * chargeScale);

        Color surface = ImpactSurface.GetColor(other, Color.gray);

        BulletMarks.Spawn(point, normal, surface, size, markLifetime,
                          chunks, chunkSize, chunkLifetime, dustSize * chargeScale);
    }

    private void LayerSetup()
    {
        int bulletLayer = LayerMask.NameToLayer("Bullet");

        if (bulletLayer == -1)
        {
            Debug.LogError("The Bullet layer does not exist.");
            return;
        }

        Physics.IgnoreLayerCollision(
            bulletLayer,
            bulletLayer,
            true
        );
    }
    
    public void SetDamageMultiplier(float multiplier)
    {
        damageMultiplier = multiplier;
    }
}