using UnityEngine;

public class HitboxDamage : MonoBehaviour
{
    public int damage = 20; // 會在啟動近戰時由 FlyingChaser 設定也行
    public string[] damageTags = new[] { "Player" };
    private bool _hasHitThisActivation = false;

    private void OnEnable()
    {
        _hasHitThisActivation = false;
    }

    private void OnTriggerEnter(Collider other)
    {
        if (_hasHitThisActivation) return;
        if (IsInTags(other.tag))
        {
            var dmg = other.GetComponentInParent<IDamageable>();
            if (dmg != null)
            {
                Debug.Log("Hit");
                dmg.TakeDamage(damage, this.gameObject);
                _hasHitThisActivation = true; // 單次啟用只打一次
            }
        }
    }

    private bool IsInTags(string tag)
    {
        foreach (var t in damageTags)
        {
            if (otherTagEquals(tag, t)) return true;
        }
        return false;
    }

    private bool otherTagEquals(string a, string b) => string.Equals(a, b, System.StringComparison.Ordinal);
}
