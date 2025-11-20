using UnityEngine;
using UnityEngine.SceneManagement;
using TMPro;
using System;
using System.Collections;

public class ShopManager : MonoBehaviour
{
    [Header("UI References")]
    public TextMeshProUGUI currencyText;
    public Transform shopContainer;
    public GameObject shopItemPrefab;

    [Header("Inventory UI")]
    public GameObject inventoryPanel;              // Assign in Inspector
    public Transform inventoryContainer;           // Content area for inventory items
    public GameObject inventoryItemPrefab;         // Prefab for displaying inventory entries

    [Serializable]
    public class ShopItem
    {
        public int itemID;
        public string itemName;
        public int price;
        public Sprite icon;
    }

    [Header("Shop Items")]
    public ShopItem[] itemsForSale;

    private void Start()
    {
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;

        UpdateCurrencyDisplay();
        PopulateShop();

        if (inventoryPanel != null)
            inventoryPanel.SetActive(false);
    }

    private void Update()
    {
        if (Input.GetKeyDown(KeyCode.I))
        {
            ToggleInventory();
        }
    }

    private void ToggleInventory()
    {
        if (inventoryPanel == null) return;

        bool isActive = !inventoryPanel.activeSelf;
        inventoryPanel.SetActive(isActive);

        if (isActive)
        {
            UpdateInventoryDisplay();
            UpdateCurrencyDisplay();
        }
    }

    private void UpdateInventoryDisplay()
    {
        if (inventoryContainer == null || inventoryItemPrefab == null) return;

        foreach (Transform child in inventoryContainer)
            Destroy(child.gameObject);

        var data = PlayerDataManager.Instance;
        if (data == null) return;

        foreach (var item in data.inventory)
        {
            GameObject go = Instantiate(inventoryItemPrefab, inventoryContainer);
            var nameTxt = go.transform.Find("Name")?.GetComponent<TextMeshProUGUI>();
            var qtyTxt = go.transform.Find("Quantity")?.GetComponent<TextMeshProUGUI>();

            if (nameTxt) nameTxt.text = item.itemName;
            if (qtyTxt) qtyTxt.text = "x" + item.quantity;
        }
    }

    private void UpdateCurrencyDisplay()
    {
        if (PlayerDataManager.Instance != null && currencyText != null)
            currencyText.text = "Gold: " + PlayerDataManager.Instance.playerCurrency;
    }

    private void PopulateShop()
    {
        foreach (Transform t in shopContainer)
            Destroy(t.gameObject);

        foreach (var it in itemsForSale)
        {
            GameObject go = Instantiate(shopItemPrefab, shopContainer);

            var nameTxt = go.transform.Find("Name").GetComponent<TextMeshProUGUI>();
            var priceTxt = go.transform.Find("Price").GetComponent<TextMeshProUGUI>();
            var iconImg = go.transform.Find("Icon").GetComponent<UnityEngine.UI.Image>();
            var buyBtn = go.transform.Find("BuyButton").GetComponent<UnityEngine.UI.Button>();
            var denyOverlay = go.transform.Find("DenyOverlay")?.gameObject;

            if (denyOverlay) denyOverlay.SetActive(false);

            if (nameTxt) nameTxt.text = it.itemName;
            if (priceTxt) priceTxt.text = it.price.ToString();
            if (iconImg && it.icon != null) iconImg.sprite = it.icon;

            var currentItem = it;
            buyBtn.onClick.AddListener(() => TryBuyItem(currentItem, denyOverlay));
        }
    }

    public void TryBuyItem(ShopItem item, GameObject denyOverlay)
    {
        var data = PlayerDataManager.Instance;
        if (data == null)
        {
            UnityEngine.Debug.LogWarning("No PlayerDataManager found in scene!");
            return;
        }

        if (data.CanAfford(item.price))
        {
            data.SpendCurrency(item.price);
            data.AddItem(item.itemName, item.itemID);
            UpdateCurrencyDisplay();
            if (inventoryPanel != null && inventoryPanel.activeSelf)
                UpdateInventoryDisplay();

            UnityEngine.Debug.Log($"Bought {item.itemName} (ID: {item.itemID}) for {item.price}");
        }
        else
        {
            UnityEngine.Debug.Log($"Cannot afford {item.itemName}");
            if (denyOverlay != null)
                StartCoroutine(ShowDenyOverlay(denyOverlay));
        }
    }

    private IEnumerator ShowDenyOverlay(GameObject overlay)
    {
        overlay.SetActive(true);
        yield return new WaitForSeconds(2f);
        overlay.SetActive(false);
    }

    public void OnBackButton()
    {
        string last = PlayerDataManager.Instance != null ? PlayerDataManager.Instance.LastSceneName : "MainScene";

        if (!string.IsNullOrEmpty(last))
            SceneManager.LoadScene(last);
        else
            SceneManager.LoadScene("MainScene");
    }
}
