using UnityEngine;

public class PlayerStats : MonoBehaviour
{
    [Header("Multipliers (1 = unmodified)")]
    [SerializeField] private float damageMultiplier = 1f;
    [SerializeField] private float moveSpeedMultiplier = 1f;
    [SerializeField] private float dashCooldownMultiplier = 1f;
    [SerializeField] private float grappleReloadMultiplier = 1f;
    [SerializeField] private float maxHealthMultiplier = 1f;

    public float DamageMultiplier => damageMultiplier;
    public float MoveSpeedMultiplier => moveSpeedMultiplier;
    public float DashCooldownMultiplier => dashCooldownMultiplier;
    public float GrappleReloadMultiplier => grappleReloadMultiplier;
    public float MaxHealthMultiplier => maxHealthMultiplier;

    // Power-ups call these to add/remove their contribution.
    // Passing a negative value on removal cancels out what was added on pickup.
    public void AddDamageMultiplier(float delta) => damageMultiplier += delta;
    public void AddMoveSpeedMultiplier(float delta) => moveSpeedMultiplier += delta;
    public void AddDashCooldownMultiplier(float delta) => dashCooldownMultiplier += delta;
    public void AddGrappleReloadMultiplier(float delta) => grappleReloadMultiplier += delta;
    public void AddMaxHealthMultiplier(float delta) => maxHealthMultiplier += delta;
    public static PlayerStats Local { get; private set; }

    private void Awake()
    {
        Local = this;
    }
    public void ResetAll()
    {
        damageMultiplier = 1f;
        moveSpeedMultiplier = 1f;
        dashCooldownMultiplier = 1f;
        grappleReloadMultiplier = 1f;
        maxHealthMultiplier = 1f;
    }

    private void OnEnable()
    {
        GameEvents.OnEnemyKilled += HandleKilled;
        GameEvents.OnDash += HandleDash;
    }

    private void OnDisable()
    {
        GameEvents.OnEnemyKilled -= HandleKilled;
        GameEvents.OnDash -= HandleDash;
    }

    private void HandleKilled(EnemyHealth e) => Debug.Log($"Killed: {e.name}");
    private void HandleDash() => Debug.Log("Dashed");
}