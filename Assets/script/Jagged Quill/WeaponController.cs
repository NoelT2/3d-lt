using UnityEngine;
using UnityEngine.InputSystem;

public class WeaponController : MonoBehaviour
{
    // 1. TAMBAHKAN BARIS INI (Membuat menu pilihan mode shotgun)
    public enum ShotgunMode { Linear, BranchUnderwater }

    // 2. TAMBAHKAN BARIS INI (Menyediakan slot pilihan di Inspector)
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

    [Header("Shotgun Spread Settings")]
    [SerializeField] private int pelletCount = 8;
    [SerializeField] private float spreadIntensity = 0.08f;

    [SerializeField] private GrappleController grappleController;

    private bool isLeftWeaponTurn = true;

    private bool isFocusing = false;

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
    }

    void Update()
    {
        if (Mouse.current == null)
            return;
        // Start both focus animations
        if (Mouse.current.rightButton.wasPressedThisFrame &&
            !isFocusing)
        {
            BeginFocus();
        }
        // Start both focus-shoot animations
        if (Mouse.current.rightButton.wasReleasedThisFrame &&
            isFocusing)
        {
            EndFocus();
        }
        // Prevent normal firing while RMB focus is active
        if (!isFocusing &&
            Mouse.current.leftButton.wasPressedThisFrame)
        {
            FireWeapon();
        }
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
    }

    private void FireWeapon()
    {
        if (isLeftWeaponTurn)
        {
            if (vGunAnimator != null) vGunAnimator.Play(leftRecoilAnim);
            
            // Mengecek mode yang dipilih pemain
            if (currentMode == ShotgunMode.Linear) SpawnBulletLinear(leftside);
            else SpawnBulletBranch(leftside);
        }
        else
        {
            if (wGunAnimator != null) wGunAnimator.Play(rightRecoilAnim);
            
            // Mengecek mode yang dipilih pemain
            if (currentMode == ShotgunMode.Linear) SpawnBulletLinear(rightside);
            else SpawnBulletBranch(rightside);
        }

        isLeftWeaponTurn = !isLeftWeaponTurn;
    }

    private void SpawnBulletLinear(Transform spawnPoint)
    {
        if (bulletPrefab == null || spawnPoint == null || playerCamera == null) return;

        for (int i = 0; i < pelletCount; i++)
        {
            float randomX = Random.Range(-spreadIntensity, spreadIntensity);
            float randomY = Random.Range(-spreadIntensity, spreadIntensity);

            Vector3 targetDirection = playerCamera.forward + (playerCamera.right * randomX) + (playerCamera.up * randomY);
            targetDirection = targetDirection.normalized;

            GameObject newBullet = Instantiate(bulletPrefab, spawnPoint.position, Quaternion.LookRotation(targetDirection));

            Rigidbody rb = newBullet.GetComponent<Rigidbody>();
            if (rb != null)
            {
                rb.linearVelocity = targetDirection * bulletSpeed;

                // Mematikan komponen Constant Force agar peluru melesat lurus linear sempurna
                ConstantForce cf =
                    newBullet.GetComponent<ConstantForce>();
                if (cf != null)
                {
                    // Linear mode should always disable ConstantForce.
                    cf.enabled = false;
                } 
            }

            Destroy(newBullet, 2.5f);
        }
    }

    private void SpawnBulletBranch(Transform spawnPoint)
    {
        if (bulletPrefab == null || spawnPoint == null || playerCamera == null) return;

        for (int i = 0; i < pelletCount; i++)
        {
            float randomX = Random.Range(-spreadIntensity, spreadIntensity);
            float randomY = Random.Range(-spreadIntensity, spreadIntensity);

            Vector3 targetDirection = playerCamera.forward + (playerCamera.right * randomX) + (playerCamera.up * randomY);
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
                    // Disable shotgun momentum while grappled.
                    cf.enabled = false;
                }
            }

            Destroy(newBullet, 2.5f);
        }
    }
}