// EnemyArmor.cs
using UnityEngine;

[RequireComponent(typeof(EnemyHealth))]
[RequireComponent(typeof(Rigidbody))]
public class EnemyArmor : MonoBehaviour
{
    [Header("Armor")]
    [SerializeField] private float armorHealth = 50f;
    [SerializeField] private float armoredMass = 60f;

    [Header("Visual (optional)")]
    [SerializeField] private GameObject armorVisual; // yellow glow object to toggle

    private EnemyHealth enemyHealth;
    private Rigidbody enemyRigidbody;
    private float originalMass;

    private void Awake()
    {
        enemyHealth = GetComponent<EnemyHealth>();
        enemyRigidbody = GetComponent<Rigidbody>();
    }

    private void OnEnable()
    {
        originalMass = enemyRigidbody.mass;
        enemyRigidbody.mass = armoredMass;

        enemyHealth.SetArmor(this);

        if (armorVisual != null)
            armorVisual.SetActive(true);
    }

    private void OnDisable()
    {
        enemyHealth.SetArmor(null);
    }

    /// Called by EnemyHealth before it touches health/knockback.
    /// Always fully absorbs the hit; breaks (and removes itself) if depleted.
    public bool AbsorbDamage(float damageAmount)
    {
        armorHealth -= damageAmount;

        if (armorHealth <= 0f)
            Break();

        return true;
    }

    private void Break()
    {
        enemyRigidbody.mass = originalMass;

        if (armorVisual != null)
            armorVisual.SetActive(false);

        Debug.Log($"{gameObject.name}'s armor broke.");

        Destroy(this); // component removed, as requested
    }
}