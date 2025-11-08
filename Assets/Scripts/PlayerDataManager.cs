using UnityEngine;
using System.Collections.Generic;
using UnityEngine.SceneManagement;
using System;

public class PlayerDataManager : MonoBehaviour
{
    public static PlayerDataManager Instance;

    [Header("Player Data")]
    public int playerCurrency = 100;

    [Header("Inventory")]
    public List<InventoryItem> inventory = new List<InventoryItem>();

    [Header("Scene Tracking")]
    public string LastSceneName;

    [Serializable]
    public class InventoryItem
    {
        public int itemID;
        public string itemName;
        public int quantity;
    }

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
        DontDestroyOnLoad(gameObject);
    }

    public bool CanAfford(int cost) => playerCurrency >= cost;

    public void SpendCurrency(int cost)
    {
        playerCurrency -= cost;
        if (playerCurrency < 0) playerCurrency = 0;
    }

    public void AddItem(string itemName, int itemID = -1)
    {
        // Check if item already exists
        var existingItem = inventory.Find(i => i.itemID == itemID && i.itemName == itemName);

        if (existingItem != null)
        {
            existingItem.quantity++;
        }
        else
        {
            inventory.Add(new InventoryItem
            {
                itemID = itemID,
                itemName = itemName,
                quantity = 1
            });
        }

        UnityEngine.Debug.Log($"Added item: {itemName} (ID: {itemID})");
    }

    public void SaveLastScene()
    {
        LastSceneName = SceneManager.GetActiveScene().name;
    }
}
