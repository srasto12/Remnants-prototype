using UnityEngine;

[System.Serializable]
public class ItemData
{
    public string id;
    public string itemName;
    public int price;
    public Sprite icon;

    public ItemData() { }

    public ItemData(string id, string name, int price)
    {
        this.id = id;
        this.itemName = name;
        this.price = price;
        this.icon = null;
    }
}
