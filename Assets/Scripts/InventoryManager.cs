using UnityEngine;
using TMPro;
using UnityEngine.UI;
using System.Collections.Generic;

public class InventoryManager : MonoBehaviour
{
    public static InventoryManager Instance;

    [Header("UI References")]
    public GameObject inventoryPanel;
    public Transform itemContainer;
    public GameObject itemSlotPrefab;
    public TextMeshProUGUI currencyText;

    private bool isOpen = false;

    private void Awake()
    {
        Instance = this;
    }

    private void Start()
    {
        if (inventoryPanel != null)
            inventoryPanel.SetActive(false);

        UpdateInventoryUI();
    }

    private void Update()
    {
        if (Input.GetKeyDown(KeyCode.I))
        {
            ToggleInventory();
        }
    }

    public void ToggleInventory()
    {
        isOpen = !isOpen;
        inventoryPanel.SetActive(isOpen);
        if (isOpen)
        {
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
            UpdateInventoryUI();
        }
        else
        {
            Cursor.lockState = CursorLockMode.Locked;
            Cursor.visible = false;
        }
    }

    public void UpdateInventoryUI()
    {
        if (PlayerDataManager.Instance == null || itemContainer == null) return;

        // Clear old slots
        foreach (Transform t in itemContainer)
            Destroy(t.gameObject);

        // Update gold text
        if (currencyText != null)
            currencyText.text = "Gold: " + PlayerDataManager.Instance.playerCurrency;

        // Populate new slots
        List<PlayerDataManager.InventoryItem> items = PlayerDataManager.Instance.inventory;
        foreach (var item in items)
        {
            GameObject slot = Instantiate(itemSlotPrefab, itemContainer);

            var nameTxt = slot.transform.Find("ItemName").GetComponent<TextMeshProUGUI>();
            var qtyTxt = slot.transform.Find("Quantity").GetComponent<TextMeshProUGUI>();
            var iconImg = slot.transform.Find("Icon").GetComponent<Image>();

            if (nameTxt) nameTxt.text = item.itemName;
            if (qtyTxt) qtyTxt.text = "x" + item.quantity;
            if (iconImg) iconImg.sprite = GetItemIcon(item.itemID); // optional later
        }
    }

    private Sprite GetItemIcon(int itemID)
    {
        return ItemDatabase.Instance.GetIcon(itemID);
    }

}
