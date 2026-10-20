using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.InputSystem;

public class SlotMachineUI : MonoBehaviour
{
    [Header("UI Layout Elements")]
    [SerializeField] private RectTransform contentPanel; 
    [SerializeField] private RectTransform glowingPointer; 
    [SerializeField] private GameObject slotPrefab; 

    [Header("Configuration")]
    [SerializeField] private float slotHeight = 80f; 
    
    private const int maxFixedSlots = 9; 
    private const int centerSlotIndex = 4; // Kotak ke-5 (Indeks 4)

    [Header("Smooth Transition Settings")]
    [SerializeField] private float lerpSpeed = 15f; 

    private List<RunPowerUpInventory.Entry> visualQueue = new List<RunPowerUpInventory.Entry>();
    private List<GameObject> spawnedSlots = new List<GameObject>();
    private RunPowerUpInventory inventory;
    
    private int currentVisualSelectedIndex = 0;

    private Vector2 targetContentPos;
    private Vector2 targetPointerPos;

    private bool isSyncing = false;

    private void Start()
    {
        PowerUpManager manager = Object.FindAnyObjectByType<PowerUpManager>();
        if (manager != null)
        {
            ActivePowerUpController controller = manager.GetComponent<ActivePowerUpController>();
            if (controller != null)
            {
                inventory = controller.Inventory;
                inventory.OnChanged += HandleInventoryChanged; 
            }
        }

        // Set awal agar posisi tidak melompat liar di frame pertama
        if (contentPanel != null) targetContentPos = contentPanel.anchoredPosition;
        if (glowingPointer != null) targetPointerPos = glowingPointer.anchoredPosition;

        SyncQueueWithInventory();
    }

    private void OnDestroy()
    {
        if (inventory != null)
        {
            inventory.OnChanged -= HandleInventoryChanged;
        }
    }

    private void Update()
    {
        HandleQuickSwapInput();
        HandleScrollInput();
        SmoothMoveElements();
    }

    private void HandleInventoryChanged()
    {
        if (isSyncing) return; 
        SyncQueueWithInventory();
    }

    private void SyncQueueWithInventory()
    {
        if (inventory == null) return;

        isSyncing = true; 

        // 1. Sinkronisasi item baru
        foreach (var entry in inventory.Actives)
        {
            if (!visualQueue.Exists(e => e.data == entry.data))
            {
                visualQueue.Add(entry);
            }
        }

        // 2. Hapus item yang stack-nya habis (0)
        visualQueue.RemoveAll(vEntry => !System.Array.Exists(
            new List<RunPowerUpInventory.Entry>(inventory.Actives).ToArray(), 
            e => e.data == vEntry.data
        ));

        // Selaraskan indeks terpilih dengan inventaris internal jika dipicu dari luar
        if (inventory.Selected != null)
        {
            int foundVisualIdx = visualQueue.FindIndex(e => e.data == inventory.Selected.data);
            if (foundVisualIdx != -1)
            {
                currentVisualSelectedIndex = foundVisualIdx;
            }
        }

        if (visualQueue.Count == 0) currentVisualSelectedIndex = 0;
        else currentVisualSelectedIndex = Mathf.Clamp(currentVisualSelectedIndex, 0, visualQueue.Count - 1);

        RefreshVisuals();

        isSyncing = false; 
    }

    public void RefreshVisuals()
    {
        int totalItems = visualQueue.Count;
        UpdateSlotSpawns(totalItems);

        if (totalItems == 0) return;

        // Render Ikon dari Atas ke Bawah
        for (int i = 0; i < totalItems; i++)
        {
            var entry = visualQueue[i];
            Image iconImage = spawnedSlots[i].GetComponentInChildren<Image>();
            if (iconImage != null && entry.data != null)
            {
                iconImage.sprite = entry.data.Icon;
            }
        }

        // =========================================================================
        // PROSES KALKULASI VERTIKAL POSISI PERGESERAN (TOP-TO-BOTTOM)
        // =========================================================================
        
        // Titik awal 0 absolut berada di ujung paling atas (Top Anchor) dari panel Cage Anda
        float cageTopAbsoluteY = 0f;

        // SCENARIO A: Jumlah Item <= 9 (Layout Diam Menempel di Atas, Pointer Meluncur Turun)
        if (totalItems <= maxFixedSlots)
        {
            // Kontainer Content diam mengunci mati di posisi titik awal (Statis)
            targetContentPos = Vector2.zero;

            // Pointer meluncur turun mengikuti kotak fisik secara akurat:
            // Item 1 (Indeks 0) -> Berada tepat di titik cageTopAbsoluteY (Kotak 1)
            // Item 2 (Indeks 1) -> Berada di bawahnya sejauh -80 piksel (Kotak 2), dst.
            float targetPointerY = cageTopAbsoluteY - (currentVisualSelectedIndex * slotHeight);
            targetPointerPos = new Vector2(glowingPointer.anchoredPosition.x, targetPointerY);
        }
        // SCENARIO B: Jumlah Item > 9 (Pointer Mengunci Diam di Kotak 5 Tengah, Item Bergerak Lift)
        else
        {
            // Pointer dikunci mati secara absolut sejajar di posisi Kotak 5 Tengah (-320 unit dari atas)
            float fixedCenterY = cageTopAbsoluteY - (centerSlotIndex * slotHeight);
            targetPointerPos = new Vector2(glowingPointer.anchoredPosition.x, fixedCenterY);

            // Barisan kontainer Content bergeser naik-turun melewati pointer tengah secara linier
            float targetContentY = (currentVisualSelectedIndex - centerSlotIndex) * slotHeight;
            targetContentPos = new Vector2(contentPanel.anchoredPosition.x, targetContentY);
        }

        NotifyInventoryOfSelection();
    }

    private void HandleQuickSwapInput()
    {
        if (visualQueue.Count == 0) return;

        Keyboard currentKeyboard = Keyboard.current;
        if (currentKeyboard == null) return;

        Key[] quickSwapKeys = new Key[] 
        {
            Key.Digit1, Key.Digit2, Key.Digit3, 
            Key.Digit4, Key.Digit5, Key.Digit6, 
            Key.Digit7, Key.Digit8, Key.Digit9
        };

        int totalItems = visualQueue.Count;

        for (int i = 0; i < quickSwapKeys.Length; i++)
        {
            if (i < totalItems && currentKeyboard[quickSwapKeys[i]].wasPressedThisFrame)
            {
                if (totalItems <= maxFixedSlots)
                {
                    currentVisualSelectedIndex = i;
                    RefreshVisuals();
                }
                else
                {
                    // EKSEKUSI ALGORITMA ANTREAN TERIKAT KASINO (>9):
                    var selectedEntry = visualQueue[i];
                    visualQueue.RemoveAt(i);
                    visualQueue.Insert(centerSlotIndex, selectedEntry);
                    currentVisualSelectedIndex = centerSlotIndex;

                    if (inventory != null)
                    {
                        int rawActiveIndex = new List<RunPowerUpInventory.Entry>(inventory.Actives).FindIndex(e => e.data == selectedEntry.data);
                        if (rawActiveIndex != -1)
                        {
                            isSyncing = true; 
                            inventory.SelectFromWindow(rawActiveIndex, inventory.Actives.Count);
                            isSyncing = false; 
                        }
                    }

                    RefreshVisuals();
                }
                break;
            }
        }
    }

    private void HandleScrollInput()
    {
        if (visualQueue.Count <= 1) return;

        Mouse currentMouse = Mouse.current;
        if (currentMouse == null) return;

        float scrollValue = currentMouse.scroll.ReadValue().y;

        if (scrollValue > 0f) 
        {
            currentVisualSelectedIndex--;
            if (currentVisualSelectedIndex < 0) currentVisualSelectedIndex = visualQueue.Count - 1;
            RefreshVisuals();
        }
        else if (scrollValue < 0f) 
        {
            currentVisualSelectedIndex++;
            if (currentVisualSelectedIndex >= visualQueue.Count) currentVisualSelectedIndex = 0;
            RefreshVisuals();
        }
    }

    private void SmoothMoveElements()
    {
        if (contentPanel == null || glowingPointer == null) return;

        contentPanel.anchoredPosition = Vector2.Lerp(contentPanel.anchoredPosition, targetContentPos, Time.deltaTime * lerpSpeed);
        glowingPointer.anchoredPosition = Vector2.Lerp(glowingPointer.anchoredPosition, targetPointerPos, Time.deltaTime * lerpSpeed);
    }

    private void UpdateSlotSpawns(int targetCount)
    {
        while (spawnedSlots.Count < targetCount)
        {
            GameObject newSlot = Instantiate(slotPrefab, contentPanel);
            spawnedSlots.Add(newSlot);
        }
        while (spawnedSlots.Count > targetCount)
        {
            Destroy(spawnedSlots[spawnedSlots.Count - 1]);
            spawnedSlots.RemoveAt(spawnedSlots.Count - 1);
        }
    }

    private void NotifyInventoryOfSelection()
    {
        if (inventory == null || visualQueue.Count == 0 || isSyncing) return;

        var currentTargetData = visualQueue[currentVisualSelectedIndex].data;
        int rawActiveIndex = new List<RunPowerUpInventory.Entry>(inventory.Actives).FindIndex(e => e.data == currentTargetData);
        
        if (rawActiveIndex != -1)
        {
            isSyncing = true;
            inventory.SelectFromWindow(rawActiveIndex, inventory.Actives.Count);
            isSyncing = false;
        }
    }
}