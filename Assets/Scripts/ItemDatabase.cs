using UnityEngine;
using System.Collections.Generic;

public class ItemDatabase : MonoBehaviour
{
    public static ItemDatabase Instance;

    [System.Serializable]
    public class ItemData
    {
        public int itemID;
        public string itemName;
        public Sprite icon;
        public GameObject worldPrefab;   // later used for equipping weapons
    }

    public List<ItemData> items = new List<ItemData>();
    private Dictionary<int, ItemData> lookup = new Dictionary<int, ItemData>();

    private void Awake()
    {
        Instance = this;

        lookup.Clear();
        foreach (var item in items)
        {
            if (!lookup.ContainsKey(item.itemID))
                lookup.Add(item.itemID, item);
        }
    }

    public Sprite GetIcon(int id)
    {
        return lookup.ContainsKey(id) ? lookup[id].icon : null;
    }

    public GameObject GetPrefab(int id)
    {
        return lookup.ContainsKey(id) ? lookup[id].worldPrefab : null;
    }
}
