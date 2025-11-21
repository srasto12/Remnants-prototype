using System;
using UnityEngine;

public class PlayerHealth : MonoBehaviour
{
    [Header("Health Settings")]
    public int maxHealth = 100;
    public int currentHealth;

    [Header("Debug Damage (for testing only)")]
    public bool allowDebugDamage = true;
    public KeyCode debugDamageKey = KeyCode.H;
    public int debugDamageAmount = 10;

    // Event so the UI can listen and update
    public event Action<int, int> OnHealthChanged; // (current, max)

    void Start()
    {
        // Start at full health
        currentHealth = maxHealth;
        NotifyHealthChanged();
    }

    void Update()
    {
        if (allowDebugDamage && Input.GetKeyDown(debugDamageKey))
        {
            TakeDamage(debugDamageAmount);
        }
    }

    public void TakeDamage(int amount)
    {
        if (currentHealth <= 0) return;

        amount = Mathf.Abs(amount);
        currentHealth = Mathf.Clamp(currentHealth - amount, 0, maxHealth);

        NotifyHealthChanged();

        if (currentHealth <= 0)
        {
            Die();
        }
    }

    public void Heal(int amount)
    {
        if (currentHealth >= maxHealth) return;

        amount = Mathf.Abs(amount);
        currentHealth = Mathf.Clamp(currentHealth + amount, 0, maxHealth);

        NotifyHealthChanged();
    }

    void NotifyHealthChanged()
    {
        OnHealthChanged?.Invoke(currentHealth, maxHealth);
    }

    void Die()
    {
        Debug.Log("Player died!");
    }
}
