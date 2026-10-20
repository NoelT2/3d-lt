using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.InputSystem; // Menggunakan New Input System API

public class ActivePowerUpCardUI : MonoBehaviour
{
    [Header("Front Card (Currently Selected)")]
    [SerializeField] private GameObject frontCardRoot;
    [SerializeField] private Image frontIcon;
    [SerializeField] private Text frontStackText; 

    [Header("Back Stack (Queued, Blank Placeholders)")]
    [SerializeField] private List<GameObject> backCards = new List<GameObject>();

    // Cache referensi internal ke runtime inventaris
    private RunPowerUpInventory inventory;

    private void Start()
    {
        // Mencari objek PowerUpManager yang ada di scene secara otomatis saat game dimulai
        PowerUpManager manager = Object.FindAnyObjectByType<PowerUpManager>();
        
        if (manager != null)
        {
            ActivePowerUpController controller = manager.GetComponent<ActivePowerUpController>();
            if (controller != null)
            {
                inventory = controller.Inventory;
            }
        }

        // BERLANGGANAN EVENT: Otomatis memperbarui UI saat isi inventaris berubah
        if (inventory != null)
        {
            inventory.OnChanged += UpdateCardUI;
        }

        UpdateCardUI();
    }

    private void OnDestroy()
    {
        // Melepas langganan event untuk mencegah memory leak
        if (inventory != null)
        {
            inventory.OnChanged -= UpdateCardUI;
        }
    }

    private void Update()
    {
        HandleScrollInput();
    }

    /// <summary>
    /// Logika Navigasi Menggunakan Roda Scroll Mouse Tengah via New Input System
    /// </summary>
    private void HandleScrollInput()
    {
        if (inventory == null || inventory.Actives.Count <= 1) return;

        // Menggunakan New Input System untuk mendeteksi scroll mouse
        Mouse currentMouse = Mouse.current;
        if (currentMouse == null) return;

        float scrollValue = currentMouse.scroll.ReadValue().y;

        if (scrollValue > 0f) // Scroll Ke Atas
        {
            inventory.Scroll(-1); // Menggunakan fungsi bawaan dari RunPowerUpInventory
            TriggerShuffleAnimation();
            UpdateCardUI();
        }
        else if (scrollValue < 0f) // Scroll Ke Bawah
        {
            inventory.Scroll(1);  // Menggunakan fungsi bawaan dari RunPowerUpInventory
            TriggerShuffleAnimation();
            UpdateCardUI();
        }
    }

    /// <summary>
    /// Memperbarui visual kartu berdasarkan data nyata dari RunPowerUpInventory
    /// </summary>
    public void UpdateCardUI()
    {
        if (inventory == null) return;

        int totalActivePowerUps = inventory.Actives.Count;

        // Kondisi 0 Stack: Jika tidak ada active power-up sama sekali
        if (totalActivePowerUps == 0)
        {
            frontCardRoot.SetActive(false);
            SetBackCardsVisibility(0);
            return;
        }

        frontCardRoot.SetActive(true);

        // Mengambil item yang sedang aktif dipilih saat ini menggunakan properti internal bawaan Anda (.Selected)
        RunPowerUpInventory.Entry currentSelectedEntry = inventory.Selected;
        
        if (currentSelectedEntry != null)
        {
            if (currentSelectedEntry.data != null)
            {
                frontIcon.sprite = currentSelectedEntry.data.Icon; 
            }
            
            frontStackText.text = currentSelectedEntry.stackCount.ToString();
        }

        SetBackCardsVisibility(totalActivePowerUps);
    }

    private void SetBackCardsVisibility(int totalPowerUps)
    {
        for (int i = 0; i < backCards.Count; i++)
        {
            // Kartu putih antrean belakang aktif secara bertahap
            if (i < (totalPowerUps - 1))
            {
                backCards[i].SetActive(true);
            }
            else
            {
                backCards[i].SetActive(false);
            }
        }
    }

    private void TriggerShuffleAnimation()
    {
        if (inventory != null)
        {
            Debug.Log($"[UI] Card Shuffled! Current Selected Index: {inventory.SelectedIndex}");
        }
    }

    public void TriggerBurnAnimation(bool isCardDestroyed)
    {
        if (isCardDestroyed)
        {
            Debug.Log("[UI Visual] Front card burning down to ashes...");
        }
        else
        {
            Debug.Log("[UI Visual] Stack number burn flash triggered.");
        }
    }
}