using System;
using UnityEngine;

public static class GameEvents
{
    public static event Action<EnemyHealth> OnEnemyKilled;
    public static event Action<EnemyHealth, float> OnEnemyHit; // enemy, damage dealt
    public static event Action OnDash;
    public static event Action<Vector3> OnEnemyBlast; // blast origin
    public static event Action<Vector3, float> OnDiveImpact; // point, damage
    public static event Action<float> OnPlayerDamaged; // damage taken
    public static event Action<ImpactInfo> OnEnemyImpact; // a thrown enemy collided (every damaging hit)
    public static event Action<EnemyHealth, ImpactInfo> OnEnemyKilledByCollision; // died from collision damage

    public static void RaiseEnemyKilled(EnemyHealth enemy) => OnEnemyKilled?.Invoke(enemy);
    public static void RaiseEnemyHit(EnemyHealth enemy, float damage) => OnEnemyHit?.Invoke(enemy, damage);
    public static void RaiseDash() => OnDash?.Invoke();
    public static void RaiseEnemyBlast(Vector3 origin) => OnEnemyBlast?.Invoke(origin);
    public static void RaiseDiveImpact(Vector3 point, float damage) => OnDiveImpact?.Invoke(point, damage);
    public static void RaisePlayerDamaged(float damage) => OnPlayerDamaged?.Invoke(damage);
    public static void RaiseEnemyImpact(ImpactInfo info) => OnEnemyImpact?.Invoke(info);
    public static void RaiseEnemyKilledByCollision(EnemyHealth enemy, ImpactInfo info) => OnEnemyKilledByCollision?.Invoke(enemy, info);
}