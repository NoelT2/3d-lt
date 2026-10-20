using UnityEngine;

[RequireComponent(typeof(CharacterController))]
public class PlayerHealth : MonoBehaviour
{
    [Header("Health")]
    [SerializeField] private float maxHealth = 1000f;
    private float health;

    [Header("Camera Shake")]
    [SerializeField] private CameraShake cameraShake;
    [Tooltip("Fixed shake amount applied on any hit, regardless of damage taken.")]
    [SerializeField] private float hitTrauma = 0.35f;

    [Header("Knockback")]
    [SerializeField] private CharacterController controller;

    private void Awake()
    {
        if (controller == null)
            controller = GetComponent<CharacterController>();

        if (cameraShake == null && Camera.main != null)
            cameraShake = Camera.main.GetComponent<CameraShake>();

        PlayerStats stats = GetComponent<PlayerStats>();
        float healthMult = stats != null ? stats.MaxHealthMultiplier : 1f;
        health = maxHealth * healthMult;
    }

    public void TakeDamage(float damageAmount, Vector3 knockbackDirection, float knockbackForce)
    {
        if (damageAmount <= 0f)
            return;

        health -= damageAmount;
        GameEvents.RaisePlayerDamaged(damageAmount);

        if (cameraShake != null)
        {
            cameraShake.AddTrauma(hitTrauma); // fixed, not scaled by damage
        }

        if (knockbackForce > 0f && knockbackDirection.sqrMagnitude > 0.001f && controller != null)
        {
            controller.Move(knockbackDirection.normalized * knockbackForce);
        }

        Debug.Log($"Player took {damageAmount} damage. Health: {health}");

        if (health <= 0f)
        {
            Die();
        }
    }

    private void Die()
    {
        Debug.Log("Player died.");
    }
}