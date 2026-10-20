using UnityEngine;

public enum PowerUpTrigger
{
    Manual,      // fired by pressing R
    OnDash,      // auto-fires right after a dash input
    OnJump,      // auto-fires right after a jump input (e.g. double jump)
    OnGrapple    // auto-fires right after grapple fire
}

public abstract class PowerUp : MonoBehaviour
{
    [SerializeField] private PowerUpTrigger trigger = PowerUpTrigger.Manual;
    public PowerUpTrigger Trigger => trigger;

    private PowerUpData data;
    public PowerUpData Data => data;

    public void Initialize(PowerUpData sourceData)
    {
        data = sourceData;
        OnAcquired();
    }

    protected virtual void OnAcquired() { }
    public virtual void OnStackAdded(int newStackCount) { }
    public virtual void OnRemoved() { }

    // Called by ActivePowerUpController (manual) or by the matching
    // GameEvents subscription (auto-trigger types). Return false if the
    // power-up couldn't fire (on cooldown, no charge, etc) — the caller
    // decides whether that still consumes a stack.
    public abstract bool Use();
}