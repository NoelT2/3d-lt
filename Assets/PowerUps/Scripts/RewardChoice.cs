using UnityEngine;

public class RewardChoice
{
    public string Title;
    public string Description;
    public Sprite Icon;
    public object Payload; // PowerUpData, a stat option, etc — caller interprets it
}