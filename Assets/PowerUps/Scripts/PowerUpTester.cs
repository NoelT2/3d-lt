using UnityEngine;
using UnityEngine.InputSystem; // Menggunakan New Input System proyek Anda

public class PowerUpTester : MonoBehaviour
{
    [Header("Test Target Aset")]
    [SerializeField] private PowerUpData activePowerUpToTest; // Tarik aset 'Ball2' Anda ke sini

    private PowerUpManager powerUpManager;

    private void Start()
    {
        // Mencari komponen PowerUpManager secara otomatis di dalam Scene
        powerUpManager = Object.FindAnyObjectByType<PowerUpManager>();
        
        if (powerUpManager == null)
        {
            Debug.LogError("[Tester] PowerUpManager tidak ditemukan di Scene! Pastikan Manager terpasang.");
        }
    }

    private void Update()
    {
        if (powerUpManager == null || activePowerUpToTest == null) return;

        Keyboard currentKeyboard = Keyboard.current;
        if (currentKeyboard == null) return;

        // ---------------------------------------------------------------------
        // TOMBOL T: MENAMBAH / DISKONTRIBUSI POWER-UP BARU (GRANT)
        // ---------------------------------------------------------------------
        if (currentKeyboard.tKey.wasPressedThisFrame)
        {
            Debug.Log($"[Tester] Menekan T: Memberikan Power-Up {activePowerUpToTest.PowerUpName} ke pemain.");
            
            // Memanggil fungsi utama sistem Anda untuk memberikan item
            powerUpManager.Grant(activePowerUpToTest);
        }

        // ---------------------------------------------------------------------
        // TOMBOL G: MENGGUNAKAN / MENGURANGI SATU STACK ITEM YANG SEDANG DIPILIH
        // ---------------------------------------------------------------------
        if (currentKeyboard.gKey.wasPressedThisFrame)
        {
            // Mencari controller aktif untuk mengakses fungsi inventaris internal Anda
            ActivePowerUpController controller = powerUpManager.GetComponent<ActivePowerUpController>();
            if (controller != null && controller.Inventory != null)
            {
                Debug.Log("[Tester] Menekan G: Mensimulasikan penggunaan 1 Stack item aktif terpilih.");
                
                // Memanggil fungsi bawaan UseSelected() dari RunPowerUpInventory Anda
                bool suksesDigunakan = controller.Inventory.UseSelected();
                
                if (suksesDigunakan)
                {
                    Debug.Log("[Tester] Berhasil memicu UseSelected(). Stack berkurang.");
                }
                else
                {
                    Debug.LogWarning("[Tester] Gagal menggunakan item. Inventaris mungkin kosong atau fungsi Use() membalas false.");
                }
            }
        }
    }
}
