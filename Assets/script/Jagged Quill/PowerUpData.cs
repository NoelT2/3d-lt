using UnityEngine;

[CreateAssetMenu(fileName = "PowerUpData", menuName = "Jagged Quill/Power Up Data")]
public class PowerUpData : ScriptableObject
{
    [Header("Identity")]
    [SerializeField] private string powerUpName;
    public Sprite icon;
    [TextArea] [SerializeField] private string description;

    [Header("Classification")]
    [SerializeField] private PowerUpType type = PowerUpType.Neutral;
    [SerializeField] private PowerUpCategory category = PowerUpCategory.Passive;

    [Header("Rarity (optional, for reward rolls later)")]
    [SerializeField] private int rarityWeight = 100; // higher = more common

    [Header("Behavior")]
    [Tooltip("The MonoBehaviour type that implements this power-up's effect. Must inherit from PowerUp.")]
    [SerializeField] private PowerUp behaviorPrefab;

    public string PowerUpName => powerUpName;
    public Sprite Icon => icon;
    public string Description => description;
    public PowerUpType Type => type;
    public PowerUpCategory Category => category;
    public int RarityWeight => rarityWeight;
    public PowerUp BehaviorPrefab => behaviorPrefab;
}