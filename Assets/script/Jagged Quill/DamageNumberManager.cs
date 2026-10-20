using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class DamageNumberManager : MonoBehaviour
{
    [SerializeField] private DamageNumberPopup popupPrefab;
    [SerializeField] private float aggregationWindow = 0.08f; // hits within this window combine into one number
    [SerializeField] private Vector3 spawnOffset = new Vector3(0f, 2f, 0f);
    [SerializeField] private float spawnJitterRadius = 0.3f;

    private class PendingDamage
    {
        public float total;
        public Coroutine flushRoutine;
    }

    private readonly Dictionary<EnemyHealth, PendingDamage> pending = new Dictionary<EnemyHealth, PendingDamage>();

    private void OnEnable() => GameEvents.OnEnemyHit += HandleEnemyHit;
    private void OnDisable() => GameEvents.OnEnemyHit -= HandleEnemyHit;

    private void HandleEnemyHit(EnemyHealth enemy, float damage)
    {
        if (enemy == null || popupPrefab == null) return;

        if (!pending.TryGetValue(enemy, out PendingDamage p))
        {
            p = new PendingDamage();
            pending[enemy] = p;
        }

        p.total += damage;

        if (p.flushRoutine == null)
        {
            p.flushRoutine = StartCoroutine(FlushAfterWindow(enemy, p));
        }
    }

    private IEnumerator FlushAfterWindow(EnemyHealth enemy, PendingDamage p)
    {
        yield return new WaitForSeconds(aggregationWindow);

        if (enemy != null)
        {
            Vector3 jitter = Random.insideUnitSphere * spawnJitterRadius;
            jitter.y = Mathf.Abs(jitter.y);
            Vector3 spawnPos = enemy.transform.position + spawnOffset + jitter;

            DamageNumberPopup popup = Instantiate(popupPrefab, spawnPos, Quaternion.identity);
            popup.Init(p.total);
        }

        pending.Remove(enemy);
    }
}