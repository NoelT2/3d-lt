using UnityEngine;

public class TransitionRoom : MonoBehaviour
{
    [SerializeField] private RewardChoiceOption[] options;

    private bool committed;

    public bool HasCommitted => committed;

    public void CommitChoice(RewardChoiceOption chosen)
    {
        if (committed) return; // already picked, ignore further interacts

        committed = true;

        RunManager.Instance?.SetPendingReward(chosen.Reward);
        chosen.PlaySelectedVfx();

        Debug.Log($"Transition room: committed to {chosen.Reward.PowerUpName} ({chosen.Type})");

        // Optional: disable the other pedestals' colliders so they can't be interacted with after commit.
        foreach (RewardChoiceOption option in options)
        {
            if (option != chosen)
                option.gameObject.SetActive(false);
        }
    }
}