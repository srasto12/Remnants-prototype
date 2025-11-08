using UnityEngine;
using UnityEngine.SceneManagement;
using TMPro; // Import TextMeshPro namespace
using System;
using System.Diagnostics;

public class ShopManager : MonoBehaviour
{
    [Header("UI References")]
    public TextMeshProUGUI currencyText; // Use TMP type instead of UnityEngine.UI.Text
    public Transform shopContainer;
    public GameObject shopItemPrefab;

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
    }

    private void UpdateCurrencyDisplay()
    {
        if (PlayerDataManager.Instance != null && currencyText != null)
            currencyText.text = "Gold: " + PlayerDataManager.Instance.playerCurrency;
    }

    private void PopulateShop()
    {
        // Clear any old shop entries before creating new ones
        foreach (Transform t in shopContainer)
            Destroy(t.gameObject);

        // Spawn new shop item entries
        foreach (var it in itemsForSale)
        {
            GameObject go = Instantiate(shopItemPrefab, shopContainer);

            // Use TMP for Name and Price text fields
            var nameTxt = go.transform.Find("Name").GetComponent<TextMeshProUGUI>();
            var priceTxt = go.transform.Find("Price").GetComponent<TextMeshProUGUI>();
            var iconImg = go.transform.Find("Icon").GetComponent<UnityEngine.UI.Image>();
            var buyBtn = go.transform.Find("BuyButton").GetComponent<UnityEngine.UI.Button>();

            if (nameTxt) nameTxt.text = it.itemName;
            if (priceTxt) priceTxt.text = it.price.ToString();
            if (iconImg && it.icon != null) iconImg.sprite = it.icon;

            // Capture local reference so that listener uses the right item
            var currentItem = it;
            buyBtn.onClick.AddListener(() => TryBuyItem(currentItem));
        }
    }

    public void TryBuyItem(ShopItem item)
    {
        var data = PlayerDataManager.Instance;
        if (data == null)
        {
            UnityEngine.Debug.LogWarning("No PlayerDataManager found in scene!"); // Explicitly using UnityEngine.Debug
            return;
        }

        if (data.CanAfford(item.price))
        {
            data.SpendCurrency(item.price);
            data.AddItem(item.itemName, item.itemID);
            UpdateCurrencyDisplay();
            UnityEngine.Debug.Log($"Bought {item.itemName} (ID: {item.itemID}) for {item.price}"); // Explicitly using UnityEngine.Debug
        }
        else
        {
            UnityEngine.Debug.Log($"Cannot afford {item.itemName}"); // Explicitly using UnityEngine.Debug
        }
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
