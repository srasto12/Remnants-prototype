using UnityEngine;

public class SimplePlayerDamageable : MonoBehaviour, IDamageable
{
    public int maxHP = 100;
    public int currentHP;

    private void Awake()
    {
        currentHP = maxHP;
    }

    public void TakeDamage(int amount, GameObject source)
    {
        currentHP -= amount;
        currentHP = Mathf.Max(currentHP, 0);
        Debug.Log($"Player took {amount} from {source.name}. HP: {currentHP}/{maxHP}");
        if (currentHP <= 0)
        {
            Debug.Log("Player Dead");
        }
    }
}
