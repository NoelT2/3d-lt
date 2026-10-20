using UnityEngine;

/// <summary>
/// The ONE official place for collision / impact damage numbers (thrown enemies, dive, ...).
/// Optional: create an asset via  Assets > Create > Game > Impact Settings  and save it as
/// "Assets/Resources/ImpactSettings.asset". Everything reads it through ImpactSettings.Current,
/// so there is nothing to assign on prefabs. If no asset exists, the defaults below are used.
/// </summary>
[CreateAssetMenu(fileName = "ImpactSettings", menuName = "Game/Impact Settings")]
public class ImpactSettings : ScriptableObject
{
    [Header("Official impact damage")]
    [Tooltip("The cap for any single collision/impact. Dive damage = this x Dive Damage Scale.")]
    public float maxImpactDamage = 150f;
    [Tooltip("Damage of a hit that only just passes the speed threshold.")]
    public float baseImpactDamage = 35f;
    [Tooltip("Extra damage per unit of momentum (mass x speed) above the threshold.")]
    public float damagePerMomentum = 1.5f;

    [Header("Thrown-enemy collisions")]
    [Tooltip("Minimum speed INTO the surface (along the contact normal) for a hit to count. Scraping along a surface doesn't count.")]
    public float impactSpeedThreshold = 6f;
    [Tooltip("Minimum time between damaging hits on the same enemy (stops damage spam while resting on a surface).")]
    public float perContactCooldown = 0.12f;
    [Tooltip("An enemy stops counting as 'thrown' after this long...")]
    public float thrownMaxDuration = 6f;
    [Tooltip("...or after it stays slower than this speed for Thrown End Delay seconds.")]
    public float thrownEndSpeed = 2f;
    public float thrownEndDelay = 0.4f;

    [Header("Impact AOE (scales with intensity 0..1)")]
    public float aoeRadiusMin = 1.5f;
    public float aoeRadiusMax = 4f;
    public float aoeKnockbackMin = 3f;
    public float aoeKnockbackMax = 10f;

    [Header("Effect scale (scales with intensity 0..1)")]
    public float effectScaleMin = 0.6f;
    public float effectScaleMax = 1.6f;

    private static ImpactSettings current;

    public static ImpactSettings Current
    {
        get
        {
            if (current == null)
            {
                current = Resources.Load<ImpactSettings>("ImpactSettings");
                if (current == null)
                    current = CreateInstance<ImpactSettings>();
            }
            return current;
        }
    }
}
