using UnityEngine;

public class RewardChoiceOption : MonoBehaviour
{
    [Header("What this pedestal offers")]
    [SerializeField] private PowerUpData reward;

    [Header("Visual (optional)")]
    [SerializeField] private GameObject symbolVisual; // swap sprite/mesh based on reward.Type later
    [SerializeField] private GameObject selectedVfx;

    public PowerUpData Reward => reward;
    public PowerUpType Type => reward != null ? reward.Type : PowerUpType.Neutral;

    private TransitionRoom room;

    private void Awake()
    {
        room = GetComponentInParent<TransitionRoom>();
    }

    // Called by the interact system (or a trigger, see below) when the player commits here.
    public void Choose()
    {
        if (room == null || reward == null) return;
        room.CommitChoice(this);
    }

    public void PlaySelectedVfx()
    {
        if (selectedVfx != null)
            Instantiate(selectedVfx, transform.position, Quaternion.identity);
    }
}