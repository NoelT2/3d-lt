using UnityEngine;
using UnityEngine.InputSystem;

public class ActivePowerUpController : MonoBehaviour
{
    private RunPowerUpInventory inventory = new RunPowerUpInventory();
    public RunPowerUpInventory Inventory => inventory;

    private void OnEnable()
    {
        GameEvents.OnDash += HandleDash;
        // Hook OnJump/OnGrapple here too, once those events exist.
    }

    private void OnDisable()
    {
        GameEvents.OnDash -= HandleDash;
    }

    private void Update()
    {
        if (Keyboard.current == null) return;

        if (Keyboard.current.rKey.wasPressedThisFrame)
        {
            bool fired = inventory.UseSelected();
            if (!fired) Debug.Log("No active power-up to use (or it's on cooldown/empty).");
        }

        // Placeholder scroll input — swap for mouse wheel or bracket keys later.
        if (Keyboard.current.pageUpKey.wasPressedThisFrame) inventory.Scroll(-1);
        if (Keyboard.current.pageDownKey.wasPressedThisFrame) inventory.Scroll(1);

        // 1-9 quick-swap into the center slot.
        for (int i = 0; i < 9; i++)
        {
            if (Keyboard.current[Key.Digit1 + i].wasPressedThisFrame)
            {
                inventory.SelectFromWindow(i, 9);
                break;
            }
        }
    }

    private void HandleDash()
    {
        inventory.UseByTrigger(PowerUpTrigger.OnDash);
    }

    // Called by PowerUpManager when a new active is granted.
    public void GrantActive(PowerUpData data)
    {
        if (data.BehaviorPrefab == null)
        {
            Debug.LogError($"{data.PowerUpName} has no Behavior Prefab assigned.");
            return;
        }

        PowerUp instance = Instantiate(data.BehaviorPrefab, transform);
        instance.Initialize(data);
        inventory.Add(data, instance);
    }
}