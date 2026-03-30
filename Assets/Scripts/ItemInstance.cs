[System.Serializable]
public class ItemInstance
{
    public ItemData Data { get; private set; }
    public int Quantity { get; private set; }

    public ItemInstance(ItemData data, int quantity)
    {
        Data = data;
        Quantity = quantity;
    }

    public void Add(int amount)
    {
        Quantity += amount;
    }

    public void Remove(int amount)
    {
        Quantity -= amount;
    }
}