using UnityEngine;

public interface IEnemyMovement
{
    bool HasTarget { get; }
    Transform Target { get; }
    float DistanceToTarget { get; }
    bool IsInRange(float range);
    void SetDesiredRange(float min, float max);
    void SetManualRotation(bool manual);
}