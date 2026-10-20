using UnityEngine;
using UnityEngine.Rendering;

/// <summary>
/// Ground-break / wall-impact / drag effects for enemies that are thrown, knocked down or ragdolling.
///  - Hard impact on the GROUND: a crater (dark dug-up patch), a ring of chunks thrown outward that stay for ~30 s, and a dust puff.
///  - Hard impact on a WALL: a dent/scar on the wall, chunks sprayed around the impact that fall and pile up at the base, dust.
///  - Scrape: while sliding, a dark groove is dragged along the surface and chunks are kicked up in the surface's colour.
/// Added automatically by EnemyGrappleTarget (or add it to the prefab yourself to tweak values).
/// Everything is built in code; assign Particle Material / Debris Sprite only if you want your own look.
/// </summary>
[RequireComponent(typeof(Rigidbody))]
public class EnemyContactEffects : MonoBehaviour
{
    [Header("Master scale")]
    [Tooltip("Scales EVERY size at once: rubble, chunks, craters, scars, groove width, heaps, dust. 1 = the values below as typed. Counts and speeds are not scaled.")]
    [SerializeField, Min(0.1f)] private float effectScale = 1f;

    [Header("When")]
    [Tooltip("Scrape effect only plays while the enemy is thrown or knocked down (not during normal walking).")]
    [SerializeField] private bool onlyWhenThrownOrKnockedDown = true;
    [SerializeField] private float minSlideSpeed = 3f;
    [SerializeField] private float maxSlideSpeed = 25f;

    [Header("Scrape (drag)")]
    [SerializeField] private float scrapeInterval = 0.025f;
    [SerializeField] private int scrapeMaxCount = 7;
    [SerializeField] private Vector2 scrapeSize = new Vector2(0.15f, 0.4f);
    [SerializeField] private float scrapeKickSpeed = 4f;

    [Header("Hard impact chunks (scales with intensity)")]
    [SerializeField] private int burstMinCount = 15;
    [SerializeField] private int burstMaxCount = 80;
    [SerializeField] private float burstMinSpeed = 5f;
    [SerializeField] private float burstMaxSpeed = 16f;
    [SerializeField] private Vector2 burstSize = new Vector2(0.25f, 0.7f);
    [Tooltip("Hits on walls get this multiplier on chunk count.")]
    [SerializeField] private float wallBurstMultiplier = 1.5f;

    [Header("Ground break / wall scars (stay for Particle Lifetime)")]
    [SerializeField] private bool leaveScars = true;
    [Tooltip("Crater radius on the ground (min..max, scales with hit intensity).")]
    [SerializeField] private Vector2 craterRadius = new Vector2(1.2f, 3.5f);
    [Tooltip("Dent/scar radius on walls (min..max, scales with hit intensity).")]
    [SerializeField] private Vector2 wallScarRadius = new Vector2(0.8f, 2.2f);
    [Tooltip("Extra irregular patches around the crater rim (makes it look broken up).")]
    [SerializeField] private int extraPatches = 4;
    [Tooltip("Width of the groove dragged while sliding.")]
    [SerializeField] private float grooveWidth = 0.8f;
    [SerializeField, Range(0f, 1f)] private float scarMaxAlpha = 0.7f;
    [SerializeField] private Color scarColor = new Color(0.07f, 0.06f, 0.06f, 1f);
    [SerializeField] private int maxScars = 400;

    [Header("Rubble buildup (solid pieces that pile up along grooves, around craters and at wall bases)")]
    [SerializeField] private bool leaveRubble = true;
    [Tooltip("Size of one rubble piece (min..max). Scaled up by hit strength.")]
    [SerializeField] private Vector2 rubbleSize = new Vector2(0.3f, 0.8f);
    [Tooltip("Multiplies the rubble colour. Lower = darker, like the reference.")]
    [SerializeField, Range(0.2f, 1f)] private float rubbleDarkness = 0.6f;
    [Tooltip("Hard cap of rubble pieces PER ENEMY. Lower this first if you get lag.")]
    [SerializeField] private int maxRubble = 2500;
    [Tooltip("Pieces dropped per scrape tick while sliding (min..max, scales with speed).")]
    [SerializeField] private Vector2Int rubblePerScrape = new Vector2Int(1, 4);
    [Tooltip("How wide the rubble band is compared to the groove width.")]
    [SerializeField] private float rubbleSpread = 1.6f;
    [Tooltip("Pieces in the ring around a crater (min..max, scales with hit strength).")]
    [SerializeField] private Vector2Int craterRimCount = new Vector2Int(10, 36);
    [Tooltip("Pieces stuck on the wall around the dent.")]
    [SerializeField] private Vector2Int wallChipCount = new Vector2Int(5, 16);
    [Tooltip("Pieces in the heap on the floor at the wall's base.")]
    [SerializeField] private Vector2Int wallHeapCount = new Vector2Int(12, 45);
    [SerializeField] private Vector2 wallHeapRadius = new Vector2(0.9f, 2.4f);
    [SerializeField] private Vector2 wallHeapHeight = new Vector2(0.35f, 1.1f);
    [Tooltip("If the floor is further below the wall hit than this, no heap is made.")]
    [SerializeField] private float maxWallDrop = 25f;
    [Tooltip("Rubble is placed on the surface found by a raycast, so it never floats. Turn off only to compare.")]
    [SerializeField] private bool snapRubbleToSurface = true;
    [Tooltip("Layers that rubble can sit on. Include your ground and walls.")]
    [SerializeField] private LayerMask rubbleSurfaceMask = ~0;

    [Header("Dust puff")]
    [SerializeField] private bool dustPuff = true;
    [SerializeField] private Vector2Int dustCount = new Vector2Int(5, 14);
    [SerializeField] private Vector2 dustSize = new Vector2(1.5f, 4.5f);
    [SerializeField] private float dustLifetime = 1.6f;
    [SerializeField, Range(0f, 1f)] private float dustAlpha = 0.45f;

    [Header("Blast dust (auto blast)")]
    [SerializeField] private bool blastDust = true;
    [SerializeField] private int blastDustCount = 14;
    [SerializeField] private float blastDustSize = 3f;
    [SerializeField] private float blastDustSpeed = 3f;

    [Header("Particles")]
    [Tooltip("Optional. Use your own sprite for the chunks (a small SOLID shape, e.g. a rock). Empty = 3D cube chunks.")]
    [SerializeField] private Sprite debrisSprite;
    [Tooltip("Optional. For chunks: URP Particles/Simple Lit or Unlit (opaque). Auto-created if empty.")]
    [SerializeField] private Material particleMaterial;
    [SerializeField] private Color fallbackColor = new Color(0.55f, 0.55f, 0.55f, 1f);
    [Tooltip("How long chunks and scars stay before they start fading/shrinking (seconds).")]
    [SerializeField] private float particleLifetime = 30f;
    [Tooltip("Chunks stay full size and scars fully visible until this fraction of their life, then they vanish.")]
    [SerializeField, Range(0.5f, 0.99f)] private float shrinkStartFraction = 0.85f;
    [Tooltip("Hard cap of live chunks PER ENEMY.")]
    [SerializeField] private int maxParticles = 1500;
    [SerializeField] private float particleGravity = 2.5f;
    [SerializeField] private bool particlesCollideWithWorld = true;
    [Tooltip("Chunk colour = surface colour x a random value in this range. Lower = darker chunks.")]
    [SerializeField] private Vector2 brightnessRange = new Vector2(0.5f, 0.85f);

    // Scaled sizes: every use in the code goes through these, so Effect Scale changes everything together.
    private Vector2 ScrapeSize => scrapeSize * effectScale;
    private Vector2 BurstSize => burstSize * effectScale;
    private Vector2 CraterRadius => craterRadius * effectScale;
    private Vector2 WallScarRadius => wallScarRadius * effectScale;
    private Vector2 RubbleSize => rubbleSize * effectScale;
    private Vector2 WallHeapRadius => wallHeapRadius * effectScale;
    private Vector2 WallHeapHeight => wallHeapHeight * effectScale;
    private Vector2 DustSize => dustSize * effectScale;
    private float GrooveWidth => grooveWidth * effectScale;

    private Rigidbody rb;
    private EnemyHealth health;
    private EnemyGrappleTarget grappleTarget;
    private EnemyKnockdownState knockdown;

    private ParticleSystem debris;
    private ParticleSystem scars;
    private ParticleSystem dust;
    private ParticleSystem rubble;

    private float nextScrapeTime;
    private int scrapeCounter;

    private Collider cachedCollider;
    private Color cachedColor;

    private void Awake()
    {
        rb = GetComponent<Rigidbody>();
        health = GetComponent<EnemyHealth>();
        grappleTarget = GetComponent<EnemyGrappleTarget>();
        knockdown = GetComponent<EnemyKnockdownState>();

        BuildDebrisSystem();
        BuildScarSystem();
        BuildDustSystem();
        BuildRubbleSystem();
    }

    private void OnEnable() => GameEvents.OnEnemyImpact += HandleImpact;
    private void OnDisable() => GameEvents.OnEnemyImpact -= HandleImpact;

    private void OnDestroy()
    {
        // Particle objects live in the scene root so chunks/scars outlive the enemy.
        float t = particleLifetime * 1.1f + 1f;
        if (debris != null) Destroy(debris.gameObject, t);
        if (scars != null) Destroy(scars.gameObject, t);
        if (dust != null) Destroy(dust.gameObject, dustLifetime * 1.3f + 1f);
        if (rubble != null) Destroy(rubble.gameObject, t);
    }

    private bool IsActiveState()
    {
        if (!onlyWhenThrownOrKnockedDown) return true;
        if (grappleTarget != null && grappleTarget.IsThrown) return true;
        if (knockdown != null && knockdown.State != EnemyKnockdownState.KnockState.Normal) return true;
        return false;
    }

    // ================= Scrape / drag =================

    private void OnCollisionStay(Collision collision)
    {
        if (debris == null || collision.contactCount <= 0) return;
        if (Time.time < nextScrapeTime) return;
        if (!IsActiveState()) return;

        ContactPoint contact = collision.GetContact(0);

        Vector3 velocity = rb.linearVelocity;
        Vector3 tangent = velocity - Vector3.Project(velocity, contact.normal);
        float slideSpeed = tangent.magnitude;
        if (slideSpeed < minSlideSpeed) return;

        nextScrapeTime = Time.time + scrapeInterval;
        scrapeCounter++;

        float t = Mathf.InverseLerp(minSlideSpeed, maxSlideSpeed, slideSpeed);
        int count = Mathf.Max(1, Mathf.RoundToInt(Mathf.Lerp(1f, scrapeMaxCount, t)));
        Color surface = GetSurfaceColor(collision.collider);
        Vector3 trailDir = -tangent.normalized;

        for (int i = 0; i < count; i++)
        {
            Vector3 vel =
                trailDir * Random.Range(0.2f, 0.6f) * slideSpeed * 0.5f +
                contact.normal * Random.Range(0.5f, 1.2f) * scrapeKickSpeed +
                Random.insideUnitSphere * scrapeKickSpeed * 0.5f;

            EmitChunk(contact.point + contact.normal * 0.05f, vel, surface,
                      Random.Range(ScrapeSize.x, ScrapeSize.y) * Mathf.Lerp(0.8f, 1.4f, t));
        }

        // Dark groove dragged along the surface (the "dug" trail).
        if (leaveScars)
        {
            float width = GrooveWidth * Mathf.Lerp(0.7f, 1.4f, t);
            float length = Mathf.Max(0.5f, slideSpeed * scrapeInterval * 2.4f);
            EmitScar(contact.point, contact.normal, width, length, tangent.normalized,
                     scarMaxAlpha * 0.65f, ScarTint(surface));
        }

        if (leaveRubble)
            EmitGrooveRubble(contact.point, contact.normal, tangent.normalized, slideSpeed, t, surface);

        if (dustPuff && scrapeCounter % 3 == 0)
            EmitDust(contact.point, contact.normal, surface, 1, Mathf.Lerp(DustSize.x, DustSize.y, t) * 0.5f);
    }

    // ================= Hard impact =================

    private void HandleImpact(ImpactInfo info)
    {
        if (debris == null || info.enemy != health) return;

        bool isWall = Mathf.Abs(info.normal.y) < 0.7f;

        // Hitting another enemy shouldn't dig the ground or leave rubble.
        if (leaveScars && info.hitEnvironment) SpawnImpactScars(info, isWall);
        if (leaveRubble && info.hitEnvironment) SpawnImpactRubble(info, isWall);

        if (dustPuff)
        {
            int n = Mathf.RoundToInt(Mathf.Lerp(dustCount.x, dustCount.y, info.intensity));
            EmitDust(info.point, info.normal, info.surfaceColor, n, Mathf.Lerp(DustSize.x, DustSize.y, info.intensity));
        }

        SpawnImpactChunks(info, isWall);
    }

    private void SpawnImpactChunks(ImpactInfo info, bool isWall)
    {
        float radius = Mathf.Lerp(
            isWall ? WallScarRadius.x : CraterRadius.x,
            isWall ? WallScarRadius.y : CraterRadius.y,
            info.intensity);

        int count = Mathf.RoundToInt(
            Mathf.Lerp(burstMinCount, burstMaxCount, info.intensity) * (isWall ? wallBurstMultiplier : 1f));
        float speed = Mathf.Lerp(burstMinSpeed, burstMaxSpeed, info.intensity);

        Vector3 n = info.normal;

        for (int i = 0; i < count; i++)
        {
            Vector3 radial = Vector3.ProjectOnPlane(Random.onUnitSphere, n);
            if (radial.sqrMagnitude < 0.001f) radial = Vector3.Cross(n, Vector3.up);
            radial.Normalize();

            Vector3 dir;
            Vector3 pos;
            float speedMul;

            if (isWall)
            {
                // Spray around the dent; gravity then drops the chunks so they pile up at the wall's base.
                dir = radial * 0.8f + n * 0.5f + Random.insideUnitSphere * 0.3f;
                pos = info.point + radial * Random.Range(0f, radius * 0.6f) + n * 0.1f;
                speedMul = Random.Range(0.2f, 0.8f);
            }
            else
            {
                // Ring of chunks thrown outward and up from the crater.
                dir = radial * 0.9f + n * Random.Range(0.4f, 1f);
                pos = info.point + radial * Random.Range(0f, radius * 0.5f) + n * 0.1f;
                speedMul = Random.Range(0.4f, 1f);
            }

            EmitChunk(pos, dir.normalized * speed * speedMul, info.surfaceColor,
                      Random.Range(BurstSize.x, BurstSize.y) * Mathf.Lerp(0.7f, 1.5f, info.intensity));
        }
    }

    private void SpawnImpactScars(ImpactInfo info, bool isWall)
    {
        float radius = Mathf.Lerp(
            isWall ? WallScarRadius.x : CraterRadius.x,
            isWall ? WallScarRadius.y : CraterRadius.y,
            info.intensity);

        Vector3 n = info.normal;
        Color tint = ScarTint(info.surfaceColor);

        // Wide dug-up patch + a smaller, darker pit in the middle.
        EmitScar(info.point, n, radius * 2f, radius * 2f, Vector3.zero, scarMaxAlpha * 0.8f, tint);
        EmitScar(info.point, n, radius * 1.1f, radius * 1.1f, Vector3.zero, scarMaxAlpha, tint * 0.6f);

        // Irregular patches around the rim so it reads as broken ground, not a perfect circle.
        for (int i = 0; i < extraPatches; i++)
        {
            Vector3 radial = Vector3.ProjectOnPlane(Random.onUnitSphere, n);
            if (radial.sqrMagnitude < 0.001f) continue;
            radial.Normalize();

            Vector3 pos = info.point + radial * radius * Random.Range(0.5f, 1f);
            float size = radius * Random.Range(0.7f, 1.3f);
            EmitScar(pos, n, size, size * Random.Range(0.6f, 1f), Vector3.zero,
                     scarMaxAlpha * Random.Range(0.4f, 0.7f), tint);
        }
    }

    // ================= Rubble buildup =================

    private static readonly RaycastHit[] hitBuffer = new RaycastHit[12];

    // Finds the nearest static surface along -normal from just above 'desired'. Keeps rubble on the real surface.
    private bool TrySnap(Vector3 desired, Vector3 normal, out Vector3 point, out Vector3 surfaceNormal)
    {
        point = desired;
        surfaceNormal = normal;
        if (!snapRubbleToSurface) return true;

        float up = 1.2f * Mathf.Max(1f, effectScale);
        float down = 1.8f * Mathf.Max(1f, effectScale);
        int n = Physics.RaycastNonAlloc(desired + normal * up, -normal, hitBuffer, up + down,
                                        rubbleSurfaceMask, QueryTriggerInteraction.Ignore);
        float best = float.MaxValue;
        bool found = false;
        for (int i = 0; i < n; i++)
        {
            RaycastHit h = hitBuffer[i];
            if (h.collider.attachedRigidbody != null) continue;   // enemies, props, the player
            if (h.distance <= 0f || h.distance >= best) continue;
            best = h.distance;
            point = h.point;
            surfaceNormal = h.normal;
            found = true;
        }
        return found;
    }

    private bool FindFloorBelow(Vector3 from, out Vector3 point, out Vector3 normal)
    {
        point = from;
        normal = Vector3.up;
        int n = Physics.RaycastNonAlloc(from, Vector3.down, hitBuffer, maxWallDrop,
                                        rubbleSurfaceMask, QueryTriggerInteraction.Ignore);
        float best = float.MaxValue;
        bool found = false;
        for (int i = 0; i < n; i++)
        {
            RaycastHit h = hitBuffer[i];
            if (h.collider.attachedRigidbody != null) continue;
            if (h.normal.y < 0.5f || h.distance <= 0f || h.distance >= best) continue;
            best = h.distance;
            point = h.point;
            normal = h.normal;
            found = true;
        }
        return found;
    }

    // One solid piece resting on a surface (sunk in a little, slightly tilted, flat-ish like real rubble).
    private void EmitRubble(Vector3 surfacePoint, Vector3 normal, float size, Color surface, float lift = 0f)
    {
        if (rubble == null) return;

        Vector3 dims = new Vector3(
            size * Random.Range(0.7f, 1.5f),
            size * Random.Range(0.5f, 1.1f),
            size * Random.Range(0.7f, 1.5f));

        Quaternion rot = Quaternion.FromToRotation(Vector3.up, normal) *
                         Quaternion.Euler(Random.Range(-40f, 40f), Random.Range(0f, 360f), Random.Range(-40f, 40f));

        float v = Random.Range(brightnessRange.x, brightnessRange.y) * rubbleDarkness;
        if (Random.value < 0.12f) v *= 1.5f;   // a few lighter pieces so it isn't one flat colour

        ParticleSystem.EmitParams p = new ParticleSystem.EmitParams
        {
            position = surfacePoint + normal * (dims.y * 0.28f + lift),
            rotation3D = rot.eulerAngles,
            startSize3D = dims,
            startColor = new Color(Mathf.Clamp01(surface.r * v), Mathf.Clamp01(surface.g * v), Mathf.Clamp01(surface.b * v), 1f),
            startLifetime = particleLifetime * Random.Range(0.95f, 1.05f),
            velocity = Vector3.zero
        };
        rubble.Emit(p, 1);
    }

    // Pieces scattered across the dragged groove: dense band, some stacked on top of each other.
    private void EmitGrooveRubble(Vector3 point, Vector3 normal, Vector3 dir, float slideSpeed, float t, Color surface)
    {
        int count = Mathf.RoundToInt(Mathf.Lerp(rubblePerScrape.x, rubblePerScrape.y, t));
        Vector3 side = Vector3.Cross(normal, dir);
        if (side.sqrMagnitude < 0.001f) return;
        side.Normalize();

        float halfBand = GrooveWidth * Mathf.Lerp(0.7f, 1.4f, t) * 0.5f * rubbleSpread;
        float step = slideSpeed * scrapeInterval * 2f;
        float sizeMul = Mathf.Lerp(0.7f, 1.3f, t);

        for (int i = 0; i < count; i++)
        {
            float lateral = (Random.value + Random.value - 1f) * halfBand;      // clustered toward the middle
            float along = Random.Range(-0.5f, 0.5f) * step;
            Vector3 want = point + side * lateral + dir * along;

            if (!TrySnap(want, normal, out Vector3 p, out Vector3 n)) continue;

            float size = Random.Range(RubbleSize.x, RubbleSize.y) * sizeMul;
            EmitRubble(p, n, size, surface);

            if (Random.value < 0.3f)   // second piece stacked on the first
                EmitRubble(p + side * Random.Range(-0.15f, 0.15f) * size, n, size * Random.Range(0.5f, 0.9f), surface, size * 0.45f);
        }
    }

    private void SpawnImpactRubble(ImpactInfo info, bool isWall)
    {
        float radius = Mathf.Lerp(
            isWall ? WallScarRadius.x : CraterRadius.x,
            isWall ? WallScarRadius.y : CraterRadius.y,
            info.intensity);

        if (isWall) SpawnWallRubble(info, radius);
        else SpawnCraterRubble(info, radius);
    }

    private void SpawnCraterRubble(ImpactInfo info, float radius)
    {
        Vector3 n = info.normal;
        float sizeMul = Mathf.Lerp(1f, 1.8f, info.intensity);

        // Raised ring around the rim (some pieces stacked = a berm).
        int rim = Mathf.RoundToInt(Mathf.Lerp(craterRimCount.x, craterRimCount.y, info.intensity));
        for (int i = 0; i < rim; i++)
        {
            Vector3 radial = RandomTangent(n);
            Vector3 want = info.point + radial * radius * Random.Range(0.85f, 1.3f);
            if (!TrySnap(want, n, out Vector3 p, out Vector3 sn)) continue;

            float size = Random.Range(RubbleSize.x, RubbleSize.y) * sizeMul;
            EmitRubble(p, sn, size, info.surfaceColor);

            if (Random.value < 0.45f)
                EmitRubble(p - radial * size * 0.3f, sn, size * Random.Range(0.6f, 0.9f), info.surfaceColor, size * 0.5f);
        }

        // Smaller loose pieces scattered inside the crater.
        int inner = rim / 2;
        for (int i = 0; i < inner; i++)
        {
            Vector3 want = info.point + RandomTangent(n) * radius * Random.Range(0f, 0.75f);
            if (!TrySnap(want, n, out Vector3 p, out Vector3 sn)) continue;
            EmitRubble(p, sn, Random.Range(RubbleSize.x, RubbleSize.y) * 0.6f, info.surfaceColor);
        }

        // A few big slabs thrown further out on hard hits.
        if (info.intensity > 0.4f)
        {
            int slabs = Mathf.RoundToInt(Mathf.Lerp(1f, 4f, info.intensity));
            for (int i = 0; i < slabs; i++)
            {
                Vector3 want = info.point + RandomTangent(n) * radius * Random.Range(1.2f, 2f);
                if (!TrySnap(want, n, out Vector3 p, out Vector3 sn)) continue;
                EmitRubble(p, sn, RubbleSize.y * Random.Range(1.6f, 2.4f), info.surfaceColor);
            }
        }
    }

    private void SpawnWallRubble(ImpactInfo info, float radius)
    {
        Vector3 n = info.normal;

        // Chips stuck on the wall around the dent.
        int chips = Mathf.RoundToInt(Mathf.Lerp(wallChipCount.x, wallChipCount.y, info.intensity));
        for (int i = 0; i < chips; i++)
        {
            Vector3 want = info.point + RandomTangent(n) * radius * Random.Range(0.1f, 1f);
            if (!TrySnap(want, n, out Vector3 p, out Vector3 sn)) continue;
            EmitRubble(p, sn, Random.Range(RubbleSize.x, RubbleSize.y) * 0.55f, info.surfaceColor);
        }

        // Heap on the floor at the base of the wall.
        Vector3 away = new Vector3(n.x, 0f, n.z);
        if (away.sqrMagnitude < 0.01f) return;
        away.Normalize();

        if (!FindFloorBelow(info.point + away * 0.4f, out Vector3 floorPoint, out Vector3 floorNormal)) return;

        float heapRadius = Mathf.Lerp(WallHeapRadius.x, WallHeapRadius.y, info.intensity);
        float heapHeight = Mathf.Lerp(WallHeapHeight.x, WallHeapHeight.y, info.intensity);
        int count = Mathf.RoundToInt(Mathf.Lerp(wallHeapCount.x, wallHeapCount.y, info.intensity));
        Vector3 center = floorPoint + away * heapRadius * 0.45f;

        SpawnHeap(center, floorNormal, heapRadius, heapHeight, count, info.surfaceColor);
    }

    // Cone-shaped pile: tall and made of bigger pieces in the middle, low and small at the edge.
    private void SpawnHeap(Vector3 center, Vector3 normal, float radius, float height, int count, Color surface)
    {
        for (int i = 0; i < count; i++)
        {
            float d01 = Mathf.Sqrt(Random.value);                       // uniform over the disc
            Vector3 want = center + RandomTangent(normal) * radius * d01;
            if (!TrySnap(want, normal, out Vector3 p, out Vector3 sn)) continue;

            float h = height * Mathf.Pow(1f - d01, 1.3f) * Random.Range(0.35f, 1f);
            float size = Random.Range(RubbleSize.x, RubbleSize.y) * Mathf.Lerp(1.3f, 0.7f, d01);
            EmitRubble(p, sn, size, surface, h);
        }
    }

    private static Vector3 RandomTangent(Vector3 normal)
    {
        Vector3 r = Vector3.ProjectOnPlane(Random.onUnitSphere, normal);
        if (r.sqrMagnitude < 0.001f) r = Vector3.Cross(normal, Vector3.up);
        if (r.sqrMagnitude < 0.001f) r = Vector3.Cross(normal, Vector3.right);
        return r.normalized;
    }

    // ================= Emit helpers =================

    private void EmitChunk(Vector3 position, Vector3 velocity, Color color, float size)
    {
        ParticleSystem.EmitParams p = new ParticleSystem.EmitParams
        {
            position = position,
            velocity = velocity,
            startColor = Vary(color),
            startSize = size,
            startLifetime = particleLifetime * Random.Range(0.92f, 1.08f),
            angularVelocity3D = Random.insideUnitSphere * 360f
        };
        debris.Emit(p, 1);
    }

    // Flat quad lying on the surface. 'width' across, 'length' along lengthDir (random direction if zero).
    private void EmitScar(Vector3 point, Vector3 normal, float width, float length,
                          Vector3 lengthDir, float alpha, Color tint)
    {
        if (scars == null) return;

        Vector3 up = lengthDir;
        if (up.sqrMagnitude < 0.001f)
            up = Vector3.ProjectOnPlane(Random.onUnitSphere, normal);
        if (up.sqrMagnitude < 0.001f)
            up = Vector3.Cross(normal, Vector3.up);

        Quaternion rotation = Quaternion.LookRotation(-normal, up.normalized);

        ParticleSystem.EmitParams p = new ParticleSystem.EmitParams
        {
            position = point + normal * Random.Range(0.02f, 0.045f), // tiny offset so it doesn't z-fight
            rotation3D = rotation.eulerAngles,
            startSize3D = new Vector3(width, length, 1f),
            startColor = new Color(tint.r, tint.g, tint.b, alpha),
            startLifetime = particleLifetime,
            velocity = Vector3.zero
        };
        scars.Emit(p, 1);
    }

    private void EmitDust(Vector3 point, Vector3 normal, Color surface, int count, float size)
    {
        if (dust == null) return;

        Color c = Color.Lerp(surface, Color.white, 0.35f);
        c.a = dustAlpha;

        for (int i = 0; i < count; i++)
        {
            Vector3 vel =
                Vector3.ProjectOnPlane(Random.onUnitSphere, normal) * Random.Range(0.5f, 2.5f) +
                normal * Random.Range(0.5f, 2.5f);

            ParticleSystem.EmitParams p = new ParticleSystem.EmitParams
            {
                position = point + normal * 0.1f + Random.insideUnitSphere * 0.4f,
                velocity = vel,
                startColor = c,
                startSize = size * Random.Range(0.6f, 1.2f),
                startLifetime = dustLifetime * Random.Range(0.8f, 1.2f)
            };
            dust.Emit(p, 1);
        }
    }

        // Called by EnemyGrappleTarget the moment this enemy is auto-blasted.
    public void PlayBlastDust(Vector3 blastSource)
    {
        if (!blastDust || dust == null) return;

        Collider col = GetComponentInChildren<Collider>();
        Bounds b = col != null ? col.bounds : new Bounds(transform.position, Vector3.one);

        Vector3 away = b.center - blastSource;
        away.y = 0f;
        away = away.sqrMagnitude > 0.001f ? away.normalized : transform.forward;

        Color c = Color.Lerp(fallbackColor, Color.white, 0.35f);
        c.a = dustAlpha;

        for (int i = 0; i < blastDustCount; i++)
        {
            Vector3 offset = new Vector3(
                Random.Range(-b.extents.x, b.extents.x),
                Random.Range(-b.extents.y, b.extents.y),
                Random.Range(-b.extents.z, b.extents.z));

            Vector3 vel = Random.onUnitSphere * Random.Range(0.5f, 1f) * blastDustSpeed
                        + away * blastDustSpeed * 0.5f;

            ParticleSystem.EmitParams p = new ParticleSystem.EmitParams
            {
                position = b.center + offset,
                velocity = vel,
                startColor = c,
                startSize = blastDustSize * effectScale * Random.Range(0.6f, 1.2f),
                startLifetime = dustLifetime * Random.Range(0.8f, 1.2f)
            };
            dust.Emit(p, 1);
        }
    }

    private Color Vary(Color c)
    {
        float v = Random.Range(brightnessRange.x, brightnessRange.y);
        return new Color(Mathf.Clamp01(c.r * v), Mathf.Clamp01(c.g * v), Mathf.Clamp01(c.b * v), 1f);
    }

    private Color ScarTint(Color surface)
    {
        // Mostly the dark scar colour, slightly tinted by what was hit.
        Color dark = new Color(surface.r * 0.3f, surface.g * 0.3f, surface.b * 0.3f, 1f);
        return Color.Lerp(scarColor, dark, 0.35f);
    }

    private Color GetSurfaceColor(Collider col)
    {
        if (col != cachedCollider)
        {
            cachedCollider = col;
            cachedColor = ImpactSurface.GetColor(col, fallbackColor);
        }
        return cachedColor;
    }

    // ================= Particle system construction =================

    private ParticleSystem CreateSystem(string suffix, int max, float gravity, float lifetime, out ParticleSystemRenderer renderer)
    {
        GameObject go = new GameObject(name + suffix);
        go.transform.position = transform.position;

        ParticleSystem ps = go.AddComponent<ParticleSystem>();
        ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);

        ParticleSystem.MainModule main = ps.main;
        main.loop = true;                       // kept alive; we emit manually
        main.playOnAwake = false;
        main.simulationSpace = ParticleSystemSimulationSpace.World;
        main.maxParticles = Mathf.Max(20, max);
        main.gravityModifier = gravity;
        main.startLifetime = lifetime;
        main.cullingMode = ParticleSystemCullingMode.AlwaysSimulate;

        ParticleSystem.EmissionModule emission = ps.emission;
        emission.enabled = false;

        ParticleSystem.ShapeModule shape = ps.shape;
        shape.enabled = false;

        renderer = go.GetComponent<ParticleSystemRenderer>();
        renderer.shadowCastingMode = ShadowCastingMode.Off;
        renderer.receiveShadows = false;

        return ps;
    }

    private void BuildDebrisSystem()
    {
        ParticleSystemRenderer renderer;
        debris = CreateSystem("_ContactDebris", maxParticles, particleGravity, particleLifetime, out renderer);

        ParticleSystem.MainModule main = debris.main;
        main.startRotation3D = true;

        ParticleSystem.SizeOverLifetimeModule sizeOverLifetime = debris.sizeOverLifetime;
        sizeOverLifetime.enabled = true;
        sizeOverLifetime.size = new ParticleSystem.MinMaxCurve(1f,
            new AnimationCurve(
                new Keyframe(0f, 1f),
                new Keyframe(shrinkStartFraction, 1f),
                new Keyframe(1f, 0f)));

        if (particlesCollideWithWorld)
        {
            ParticleSystem.CollisionModule collision = debris.collision;
            collision.enabled = true;
            collision.type = ParticleSystemCollisionType.World;
            collision.mode = ParticleSystemCollisionMode.Collision3D;
            collision.bounce = 0.2f;
            collision.dampen = 0.5f;
            collision.lifetimeLoss = 0f;        // chunks must not lose life when they land (they should stay 30 s)
            collision.quality = ParticleSystemCollisionQuality.Low;
            collision.radiusScale = 0.5f;
        }

        renderer.alignment = ParticleSystemRenderSpace.World;

        Material baseMaterial = particleMaterial != null ? particleMaterial : CreateFallbackMaterial();

        if (debrisSprite != null && baseMaterial != null)
        {
            Material m = new Material(baseMaterial);
            Texture tex = debrisSprite.texture;
            m.mainTexture = tex;
            if (m.HasProperty("_BaseMap")) m.SetTexture("_BaseMap", tex);
            renderer.renderMode = ParticleSystemRenderMode.Billboard;
            renderer.sharedMaterial = m;
        }
        else
        {
            renderer.renderMode = ParticleSystemRenderMode.Mesh;
            renderer.mesh = GetPrimitiveMesh(PrimitiveType.Cube, ref cubeMesh);
            renderer.sharedMaterial = baseMaterial;
        }

        debris.Play();
    }

    private void BuildRubbleSystem()
    {
        if (!leaveRubble) return;

        ParticleSystemRenderer renderer;
        rubble = CreateSystem("_ContactRubble", maxRubble, 0f, particleLifetime, out renderer);

        ParticleSystem.MainModule main = rubble.main;
        main.startRotation3D = true;
        main.startSize3D = true;

        // Stays full size, then shrinks away at the end of its life.
        ParticleSystem.SizeOverLifetimeModule sizeOverLifetime = rubble.sizeOverLifetime;
        sizeOverLifetime.enabled = true;
        sizeOverLifetime.size = new ParticleSystem.MinMaxCurve(1f,
            new AnimationCurve(
                new Keyframe(0f, 1f),
                new Keyframe(shrinkStartFraction, 1f),
                new Keyframe(1f, 0f)));

        // Always real 3D cubes (never the billboard sprite), so they read as solid rubble.
        Material baseMaterial = particleMaterial != null ? particleMaterial : CreateFallbackMaterial();
        Material m = baseMaterial != null ? new Material(baseMaterial) : null;
        if (m != null)
        {
            m.mainTexture = null;
            if (m.HasProperty("_BaseMap")) m.SetTexture("_BaseMap", null);
        }

        renderer.renderMode = ParticleSystemRenderMode.Mesh;
        renderer.mesh = GetPrimitiveMesh(PrimitiveType.Cube, ref cubeMesh);
        renderer.alignment = ParticleSystemRenderSpace.World;
        renderer.sharedMaterial = m;

        rubble.Play();
    }

    private void BuildScarSystem()
    {
        if (!leaveScars) return;

        ParticleSystemRenderer renderer;
        scars = CreateSystem("_ContactScars", maxScars, 0f, particleLifetime, out renderer);

        ParticleSystem.MainModule main = scars.main;
        main.startRotation3D = true;
        main.startSize3D = true;

        // Fully visible, then fade out at the end of life.
        ParticleSystem.ColorOverLifetimeModule col = scars.colorOverLifetime;
        col.enabled = true;
        col.color = new ParticleSystem.MinMaxGradient(FadeOutGradient(shrinkStartFraction));

        renderer.renderMode = ParticleSystemRenderMode.Mesh;
        renderer.mesh = GetPrimitiveMesh(PrimitiveType.Quad, ref quadMesh);
        renderer.alignment = ParticleSystemRenderSpace.World;
        renderer.sortMode = ParticleSystemSortMode.YoungestInFront;
        renderer.sharedMaterial = GetSoftMaterial(GetScarTexture(), ref scarMaterial);

        scars.Play();
    }

    private void BuildDustSystem()
    {
        if (!dustPuff) return;

        ParticleSystemRenderer renderer;
        dust = CreateSystem("_ContactDust", 200, -0.05f, dustLifetime, out renderer);

        ParticleSystem.SizeOverLifetimeModule size = dust.sizeOverLifetime;
        size.enabled = true;
        size.size = new ParticleSystem.MinMaxCurve(1f,
            new AnimationCurve(new Keyframe(0f, 0.5f), new Keyframe(1f, 1.25f)));

        ParticleSystem.ColorOverLifetimeModule col = dust.colorOverLifetime;
        col.enabled = true;
        col.color = new ParticleSystem.MinMaxGradient(FadeOutGradient(0.0f));

        ParticleSystem.VelocityOverLifetimeModule vel = dust.velocityOverLifetime;
        vel.enabled = false;

        renderer.renderMode = ParticleSystemRenderMode.Billboard;
        renderer.sharedMaterial = GetSoftMaterial(GetSoftTexture(), ref dustMaterial);

        dust.Play();
    }

    private static Gradient FadeOutGradient(float fadeStart)
    {
        Gradient g = new Gradient();
        g.SetKeys(
            new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
            new[] { new GradientAlphaKey(1f, 0f), new GradientAlphaKey(1f, fadeStart), new GradientAlphaKey(0f, 1f) });
        return g;
    }

    // ================= Shared assets (built once) =================

    private static Mesh cubeMesh;
    private static Mesh quadMesh;

    private static Mesh GetPrimitiveMesh(PrimitiveType type, ref Mesh cache)
    {
        if (cache != null) return cache;

        GameObject temp = GameObject.CreatePrimitive(type);
        cache = temp.GetComponent<MeshFilter>().sharedMesh;
        Destroy(temp);
        return cache;
    }

    private static Texture2D scarTexture;
    private static Texture2D softTexture;
    private static Material scarMaterial;
    private static Material dustMaterial;

    // Irregular dark blob with a noisy edge (colour is white; the particle colour tints it).
    private static Texture2D GetScarTexture()
    {
        if (scarTexture != null) return scarTexture;

        const int n = 128;
        Texture2D tex = new Texture2D(n, n, TextureFormat.RGBA32, false);
        tex.wrapMode = TextureWrapMode.Clamp;
        tex.filterMode = FilterMode.Bilinear;

        float seed = Random.value * 100f;
        for (int y = 0; y < n; y++)
        {
            for (int x = 0; x < n; x++)
            {
                Vector2 p = new Vector2((x + 0.5f) / n - 0.5f, (y + 0.5f) / n - 0.5f) * 2f;
                float d = p.magnitude;

                float noise = Mathf.PerlinNoise(x * 0.07f + seed, y * 0.07f + seed);
                float fine = Mathf.PerlinNoise(x * 0.25f + seed * 2f, y * 0.25f + seed * 2f);
                float edge = 0.72f + (noise - 0.5f) * 0.5f + (fine - 0.5f) * 0.12f;

                float a = Mathf.Clamp01((edge - d) / 0.22f);
                a = a * a * (3f - 2f * a);
                a *= Mathf.Lerp(0.75f, 1f, 1f - Mathf.Clamp01(d)); // slightly denser in the middle

                tex.SetPixel(x, y, new Color(1f, 1f, 1f, a));
            }
        }

        tex.Apply();
        scarTexture = tex;
        return tex;
    }

    // Soft round puff.
    private static Texture2D GetSoftTexture()
    {
        if (softTexture != null) return softTexture;

        const int n = 64;
        Texture2D tex = new Texture2D(n, n, TextureFormat.RGBA32, false);
        tex.wrapMode = TextureWrapMode.Clamp;
        tex.filterMode = FilterMode.Bilinear;

        for (int y = 0; y < n; y++)
        {
            for (int x = 0; x < n; x++)
            {
                Vector2 p = new Vector2((x + 0.5f) / n - 0.5f, (y + 0.5f) / n - 0.5f) * 2f;
                float a = Mathf.Clamp01(1f - p.magnitude);
                a *= a;
                tex.SetPixel(x, y, new Color(1f, 1f, 1f, a));
            }
        }

        tex.Apply();
        softTexture = tex;
        return tex;
    }

    // Sprites/Default is transparent, double-sided, uses vertex colour and is always included in builds.
    private static Material GetSoftMaterial(Texture2D texture, ref Material cache)
    {
        if (cache != null) return cache;

        Shader shader = Shader.Find("Sprites/Default");
        if (shader == null) shader = Shader.Find("Universal Render Pipeline/Particles/Unlit");
        if (shader == null) return null;

        cache = new Material(shader);
        cache.mainTexture = texture;
        return cache;
    }

    private static Material fallbackMaterial;

    private static Material CreateFallbackMaterial()
    {
        if (fallbackMaterial != null) return fallbackMaterial;

        string[] shaderNames =
        {
            "Universal Render Pipeline/Particles/Simple Lit",   // lit, so chunks have shading like the reference
            "Universal Render Pipeline/Particles/Unlit",
            "Particles/Standard Unlit",
            "Sprites/Default"
        };

        foreach (string shaderName in shaderNames)
        {
            Shader shader = Shader.Find(shaderName);
            if (shader != null)
            {
                fallbackMaterial = new Material(shader);
                return fallbackMaterial;
            }
        }

        Debug.LogWarning("EnemyContactEffects: no particle shader found. Assign 'Particle Material' on the component.");
        return null;
    }
}