using UnityEngine;

public class MonsterHealth : MonoBehaviour, IDamageable
{
    public int maxHP = 200;
    public int currentHP;
    public FlyingChaser ai;

    private void Awake()
    {
        currentHP = maxHP;
        if (ai == null) ai = GetComponent<FlyingChaser>();
    }

    public void TakeDamage(int amount, GameObject source)
    {
        if (currentHP <= 0) return;
        currentHP -= amount;
        if (ai) ai.NotifyDamaged(source);
        if (currentHP <= 0)
        {
            currentHP = 0;
            if (ai) ai.Die();
        }
    }
}