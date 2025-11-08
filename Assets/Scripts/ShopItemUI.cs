// This is the script for the shop item prefabs. It will work on the attributes ive added to the prefab like the texts or image or the button. It will have a logic for can or cannot purchase
using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class ShopItemUI : MonoBehaviour
{
    [Header("UI Refs")]
    public Image iconImage;
    public TMPro.TMP_Text nameText;   // explicit qualification avoids ambiguity
    public TMPro.TMP_Text priceText;
    public Button buyButton;

    ItemData item;
    System.Action<ItemData> onBuyCallback;

    public void Setup(ItemData data, System.Action<ItemData> onBuy)
    {
        item = data;
        onBuyCallback = onBuy;

        if (iconImage != null) iconImage.sprite = item.icon;
        if (nameText != null) nameText.text = item.itemName;
        if (priceText != null) priceText.text = item.price.ToString();

        if (buyButton != null)
        {
            buyButton.onClick.RemoveAllListeners();
            buyButton.onClick.AddListener(() => { onBuyCallback?.Invoke(item); });
        }

        Refresh();
    }

    public void Refresh()
    {
        if (buyButton == null || item == null) return;

        bool canAfford = GameManager.Instance != null && GameManager.Instance.CanAfford(item.price);
        buyButton.interactable = canAfford;

        // change label to "Owned" if the player already has it
        bool alreadyOwned = GameManager.Instance != null && GameManager.Instance.inventory.Contains(item.id);
        if (alreadyOwned)
        {
            buyButton.interactable = false;
            if (priceText != null) priceText.text = "Owned";
        }
    }
}
