using UnityEngine;
using UnityEngine.InputSystem;

public class WeaponController : MonoBehaviour
{
    public enum ShotgunMode { Linear, BranchUnderwater }

    [Header("Weapon Mode Switcher")]
    [SerializeField] private ShotgunMode currentMode = ShotgunMode.Linear;

    [Header("Weapon Animators")]
    [SerializeField] private Animator vGunAnimator;
    [SerializeField] private Animator wGunAnimator;


    [Header("Focus Animations")]
    [SerializeField] private string leftFocusAnim = "Shotgun_Focus_L";
    [SerializeField] private string rightFocusAnim = "Shotgun_Focus";
    [SerializeField] private string leftFocusShootAnim = "Shotgun_Focus_LS";
    [SerializeField] private string rightFocusShootAnim = "Shotgun_Focus_S";

    [Header("Animation Settings")]
    [SerializeField] private string leftRecoilAnim = "Shotgun_Recoil";
    [SerializeField] private string rightRecoilAnim = "Shotgun_Recoil_R";

    [Header("Projectile Settings")]
    [SerializeField] private GameObject bulletPrefab;
    [SerializeField] private Transform leftside;
    [SerializeField] private Transform rightside;
    [SerializeField] private float bulletSpeed = 50f;

    [Header("Camera Reference")]
    [SerializeField] private Transform playerCamera;

    [Header("Aiming")]
    [Tooltip("Bullets aim at whatever the crosshair points at (up to this distance), so they converge on the screen centre instead of flying parallel from the gun sides.")]
    [SerializeField] private float aimMaxDistance = 100f;
    [SerializeField] private LayerMask aimMask = ~0;

    [Header("Shotgun Spread Settings")]
    [SerializeField] private int pelletCount = 8;
    [SerializeField] private float spreadIntensity = 0.08f;

    [Header("Charged Shot (RMB)")]
    [Tooltip("Optional. Falls back to Bullet Prefab if empty. Needs BulletDamage + Rigidbody.")]
    [SerializeField] private GameObject chargedBulletPrefab;
    [Tooltip("Seconds of holding RMB to reach full charge. Match this to your focus animation length.")]
    [SerializeField] private float chargeTime = 1.2f;
    [Tooltip("Releasing below this charge (0-1) cancels the shot. Set to 0 to always fire.")]
    [SerializeField] private float minChargeToFire = 0.2f;
    [SerializeField] private float chargedBulletSpeed = 80f;
    [SerializeField] private float minDamageMultiplier = 2f;
    [SerializeField] private float maxDamageMultiplier = 10f;
    [Tooltip("Visual/hitbox size of the charged bullet relative to the prefab.")]
    [SerializeField] private float chargedBulletScale = 2f;

    [SerializeField] private GrappleController grappleController;

    private bool isLeftWeaponTurn = true;

    private bool isFocusing = false;

    private float charge01;
    public float Charge01 => charge01;
    public bool IsCharging => isFocusing;

    void Start()
    {
        if (playerCamera == null && Camera.main != null)
        {
            playerCamera = Camera.main.transform;
        }

        if (grappleController == null)
        {
            grappleController =
                GetComponentInParent<GrappleController>();
        }

        int bulletLayer = LayerMask.NameToLayer("Bullet");
        if (bulletLayer != -1) aimMask = aimMask.value & ~(1 << bulletLayer);
    }

    void Update()
    {
        if (Mouse.current == null)
            return;
        if (Mouse.current.rightButton.wasPressedThisFrame &&
            !isFocusing)
        {
            BeginFocus();
        }
        if (isFocusing)
        {
            charge01 = Mathf.MoveTowards(
                charge01,
                1f,
                Time.deltaTime / Mathf.Max(0.01f, chargeTime)
            );
        }
        if (Mouse.current.rightButton.wasReleasedThisFrame &&
            isFocusing)
        {
            EndFocus();
        }
        if (!isFocusing &&
            Mouse.current.leftButton.wasPressedThisFrame)
        {
            FireWeapon();
        }
    }

    private void OnDisable()
    {
        // NEW
        isFocusing = false;
        charge01 = 0f;
    }

    private void BeginFocus()
    {
        isFocusing = true;

        if (vGunAnimator != null)
        {
            vGunAnimator.Play(leftFocusAnim, 0, 0f);
        }

        if (wGunAnimator != null)
        {
            wGunAnimator.Play(rightFocusAnim, 0, 0f);
        }
    }

    private void EndFocus()
    {
        isFocusing = false;

        if (vGunAnimator != null)
        {
            vGunAnimator.Play(leftFocusShootAnim, 0, 0f);
        }

        if (wGunAnimator != null)
        {
            wGunAnimator.Play(rightFocusShootAnim, 0, 0f);
        }

        if (charge01 >= minChargeToFire)
        {
            FireChargedShot(charge01);
        }

        charge01 = 0f;
    }

    private void FireChargedShot(float charge)
    {
        GameObject prefab = chargedBulletPrefab != null ? chargedBulletPrefab : bulletPrefab;
        if (prefab == null || playerCamera == null) return;

        Vector3 dir = playerCamera.forward;
        Vector3 spawnPos = playerCamera.position + dir * 0.6f;

        GameObject bullet = Instantiate(prefab, spawnPos, Quaternion.LookRotation(dir));
        bullet.transform.localScale *= chargedBulletScale;

        BulletDamage bulletDamage = bullet.GetComponent<BulletDamage>();
        if (bulletDamage != null)
        {
            bulletDamage.SetDamageMultiplier(
                Mathf.Lerp(minDamageMultiplier, maxDamageMultiplier, charge)
            );
        }

        Rigidbody rb = bullet.GetComponent<Rigidbody>();
        if (rb != null)
        {
            rb.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;
            rb.linearVelocity = dir * chargedBulletSpeed;

            ConstantForce cf = bullet.GetComponent<ConstantForce>();
            if (cf != null) cf.enabled = false;
        }

        Destroy(bullet, 3f);
    }

    private void FireWeapon()
    {
        if (isLeftWeaponTurn)
        {
            if (vGunAnimator != null) vGunAnimator.Play(leftRecoilAnim);
            
            if (currentMode == ShotgunMode.Linear) SpawnBulletLinear(leftside);
            else SpawnBulletBranch(leftside);
        }
        else
        {
            if (wGunAnimator != null) wGunAnimator.Play(rightRecoilAnim);
            
            if (currentMode == ShotgunMode.Linear) SpawnBulletLinear(rightside);
            else SpawnBulletBranch(rightside);
        }

        isLeftWeaponTurn = !isLeftWeaponTurn;
    }

    public void PlayGrappleBlastAnimation()
    {
        if (isLeftWeaponTurn)
        {
            if (vGunAnimator != null)
            {
                vGunAnimator.Play(
                    leftRecoilAnim, 0, 0f);
            }
        }
        else
        {
            if (wGunAnimator != null)
            {
                wGunAnimator.Play(
                    rightRecoilAnim, 0, 0f);
            }
        }

        // Keep grapple blasts synchronized with
        // the normal alternating weapon sequence.
        isLeftWeaponTurn = !isLeftWeaponTurn;
    }

        // The point the crosshair is looking at (or far ahead if it's pointing at nothing).
    private Vector3 GetAimPoint()
    {
        Vector3 origin = playerCamera.position;
        Vector3 dir = playerCamera.forward;

        if (Physics.Raycast(origin, dir, out RaycastHit hit, aimMaxDistance, aimMask, QueryTriggerInteraction.Ignore))
            return hit.point;

        return origin + dir * aimMaxDistance;
    }

    // Direction from a gun to the aim point. Falls back to straight ahead if the point is behind the muzzle.
    private Vector3 AimDirection(Vector3 spawnPos, Vector3 aimPoint)
    {
        Vector3 toAim = aimPoint - spawnPos;
        if (Vector3.Dot(toAim, playerCamera.forward) < 0.3f)
            return playerCamera.forward;
        return toAim.normalized;
    }

    private void SpawnBulletLinear(Transform spawnPoint)
    {
        if (bulletPrefab == null || spawnPoint == null || playerCamera == null) return;
        Vector3 baseDirection = AimDirection(spawnPoint.position, GetAimPoint());

        for (int i = 0; i < pelletCount; i++)
        {
            float randomX = Random.Range(-spreadIntensity, spreadIntensity);
            float randomY = Random.Range(-spreadIntensity, spreadIntensity);

            Vector3 targetDirection = baseDirection + (playerCamera.right * randomX) + (playerCamera.up * randomY);
            targetDirection = targetDirection.normalized;

            GameObject newBullet = Instantiate(bulletPrefab, spawnPoint.position, Quaternion.LookRotation(targetDirection));

            Rigidbody rb = newBullet.GetComponent<Rigidbody>();
            if (rb != null)
            {
                rb.linearVelocity = targetDirection * bulletSpeed;

                ConstantForce cf =
                    newBullet.GetComponent<ConstantForce>();
                if (cf != null)
                {
                    cf.enabled = false;
                } 
            }

            Destroy(newBullet, 2.5f);
        }
    }

    private void SpawnBulletBranch(Transform spawnPoint)
    {
        if (bulletPrefab == null || spawnPoint == null || playerCamera == null) return;
        Vector3 baseDirection = AimDirection(spawnPoint.position, GetAimPoint());

        for (int i = 0; i < pelletCount; i++)
        {
            float randomX = Random.Range(-spreadIntensity, spreadIntensity);
            float randomY = Random.Range(-spreadIntensity, spreadIntensity);

            Vector3 targetDirection = baseDirection + (playerCamera.right * randomX) + (playerCamera.up * randomY);
            targetDirection = targetDirection.normalized;

            GameObject newBullet = Instantiate(bulletPrefab, spawnPoint.position, Quaternion.LookRotation(targetDirection));

            Rigidbody rb = newBullet.GetComponent<Rigidbody>();
            if (rb != null)
            {
                rb.linearVelocity =
                    targetDirection *
                    bulletSpeed;
                ConstantForce cf =
                    newBullet.GetComponent<ConstantForce>();
                if (cf != null &&
                    grappleController != null &&
                    grappleController.IsGrappling)
                {
                    cf.enabled = false;
                }
            }

            Destroy(newBullet, 2.5f);
        }
    }
}