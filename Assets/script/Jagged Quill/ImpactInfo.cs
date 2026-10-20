using UnityEngine;

/// <summary>Everything an effect (particles, rings, impact frame...) needs to know about one collision.</summary>
public struct ImpactInfo
{
    public EnemyHealth enemy;     // the enemy that was thrown
    public Collider other;        // what it hit
    public Vector3 point;
    public Vector3 normal;
    public Vector3 relativeVelocity;
    public float normalSpeed;     // speed into the surface
    public float momentum;        // mass x normalSpeed
    public float damage;          // final (capped) damage dealt to the thrown enemy
    public float intensity;       // 0..1, use this to scale effects
    public bool hitEnvironment;   // false = it hit another enemy
    public Color surfaceColor;    // colour of the thing it hit (for debris / dust)
}

public static class ImpactSurface
{
    private static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");
    private static readonly int ColorId = Shader.PropertyToID("_Color");

    /// <summary>Best-effort colour of a collider's surface (material colour; textures are not sampled).</summary>
    public static Color GetColor(Collider collider, Color fallback)
    {
        if (collider == null) return fallback;

        Renderer r = collider.GetComponent<Renderer>();
        if (r == null) r = collider.GetComponentInParent<Renderer>();
        if (r == null) return fallback;

        Material m = r.sharedMaterial;
        if (m == null) return fallback;

        if (m.HasProperty(BaseColorId)) return m.GetColor(BaseColorId);
        if (m.HasProperty(ColorId)) return m.GetColor(ColorId);
        return fallback;
    }
}
