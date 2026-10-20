public class PU_TestPassive : PowerUp
{
    protected override void OnAcquired() => UnityEngine.Debug.Log("Passive acquired!");
    public override void OnStackAdded(int n) => UnityEngine.Debug.Log($"Passive stacked to {n}");
    public override bool Use() => false; // passives aren't triggered by R
}