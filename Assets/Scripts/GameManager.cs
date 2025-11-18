using System.Collections.Generic;
using UnityEngine;

public class GameManager : MonoBehaviour
{
    public static GameManager Instance { get; private set; }

    [Header("Player Economy")]
    public int currency = 100; // start value (set in inspector)

    [Header("Player Inventory")]
    public List<string> inventory = new List<string>(); // store item IDs or names

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

    public bool CanAfford(int amount)
    {
        return currency >= amount;
    }

    public bool SpendCurrency(int amount)
    {
        if (!CanAfford(amount)) return false;
        currency -= amount;
        return true;
    }

    public void AddCurrency(int amount)
    {
        currency += amount;
    }

    public void AddItemToInventory(string itemId)
    {
        inventory.Add(itemId);
    }
}

