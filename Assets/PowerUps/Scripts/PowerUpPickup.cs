using UnityEngine;

[RequireComponent(typeof(Collider))]
public class PowerUpPickup : MonoBehaviour
{
    [SerializeField] private PowerUpData data;
    [SerializeField] private GameObject pickupVfx; // optional, plays on collect
    [SerializeField] private LayerMask playerMask;

    private bool collected;

    private void Awake()
    {
        GetComponent<Collider>().isTrigger = true;
    }

    private void OnTriggerEnter(Collider other)
    {
        if (collected) return;

        if ((playerMask.value & (1 << other.gameObject.layer)) == 0)
            return;

        PowerUpManager manager = other.GetComponentInParent<PowerUpManager>();
        if (manager == null) return;

        collected = true;
        manager.Grant(data);

        if (pickupVfx != null)
            Instantiate(pickupVfx, transform.position, Quaternion.identity);

        Destroy(gameObject);
    }
}