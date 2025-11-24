using System.Collections;
using UnityEngine;

[RequireComponent(typeof(Collider))]
public class PoisonPuddle : MonoBehaviour
{
    public float lifetime = 3f;
    public float tickInterval = 0.5f;
    public float tickDamage = 5f;
    public LayerMask targetMask;

    private void OnEnable()
    {
        // 保證 Trigger
        var col = GetComponent<Collider>();
        col.isTrigger = true;
        StartCoroutine(LifeRoutine());
    }

    private IEnumerator LifeRoutine()
    {
        float t = 0f;
        float tickTimer = 0f;
        while (t < lifetime)
        {
            t += Time.deltaTime;
            tickTimer += Time.deltaTime;
            if (tickTimer >= tickInterval)
            {
                tickTimer = 0f;
                DoTickDamage();
            }
            yield return null;
        }
        Destroy(gameObject);
    }

    private void DoTickDamage()
    {
        // 用重疊膠囊或球體偵測 puddle 區域；此處用自身觸發區域更簡單：
        // 對於站在 puddle 上的對象，我們在 OnTriggerStay 也可以做；
        // 為避免頻率太高，這裡改為每 tick 主動檢測半徑。
        float radius = 1.2f; // 依據 prefab 大小自行調整
        Collider[] hits = Physics.OverlapSphere(transform.position, radius, targetMask);
        foreach (var h in hits)
        {
            var dmg = h.GetComponentInParent<IDamageable>();
            if (dmg != null)
            {
                dmg.TakeDamage(Mathf.RoundToInt(tickDamage), this.gameObject);
            }
        }
    }
}