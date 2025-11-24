using UnityEngine;

public class ShockwaveProjectile : MonoBehaviour
{
    Vector3 _dir;
    float _speed;
    int _damage;
    float _life;
    float _hitRadius = 0.3f;
    MonoBehaviour _owner;

    [Header("Hit Settings")]
    public LayerMask damageMask = ~0;      // 需要可改成只打 Player 的 Layer
    public bool destroyOnHit = true;       // 命中即毀
    public GameObject hitVfxPrefab;        // 命中特效（可不設）
    public float hitVfxLife = 2f;

    float _dieAt;
    bool _done;

    public void Initialize(Vector3 dir, float speed, int damage, float life, float hitRadius, MonoBehaviour owner)
    {
        _dir = dir.normalized;
        _speed = speed;
        _damage = damage;
        _life = life;
        _hitRadius = Mathf.Max(0.05f, hitRadius);
        _owner = owner;
        _dieAt = Time.time + _life;
    }

    void OnEnable()
    {
        // 若你的 VFX 需要重新播放，可在這裡抓取並 Play
        // var vfx = GetComponentInChildren<UnityEngine.VFX.VisualEffect>();
        // if (vfx) { vfx.Reinit(); vfx.Play(); }
        // var ps = GetComponentInChildren<ParticleSystem>();
        // if (ps) ps.Play();
    }

    void Update()
    {
        if (_done) return;

        // 線性推進
        transform.position += _dir * _speed * Time.deltaTime;

        // 命中偵測（簡單 OverlapSphere；有 Trigger Collider 也會用 OnTriggerEnter）
        var cols = Physics.OverlapSphere(transform.position, _hitRadius, damageMask, QueryTriggerInteraction.Ignore);
        for (int i = 0; i < cols.Length; i++)
        {
            if (_owner && cols[i].transform.IsChildOf(_owner.transform)) continue;

            var ph = cols[i].GetComponent<PlayerHealth>();
            if (ph)
            {
                ph.TakeDamage(_damage);
                Impact();
                return;
            }
        }

        // 壽命到自毀
        if (Time.time >= _dieAt)
            Impact();
    }

    void Impact()
    {
        if (_done) return;
        _done = true;

        if (hitVfxPrefab)
        {
            var vfx = Instantiate(hitVfxPrefab, transform.position, Quaternion.identity);
            Destroy(vfx, hitVfxLife);
        }

        if (destroyOnHit)
            Destroy(gameObject);
    }

    void OnTriggerEnter(Collider other)
    {
        if (_done) return;
        if (_owner && other.transform.IsChildOf(_owner.transform)) return;

        // Layer 過濾
        if (((1 << other.gameObject.layer) & damageMask.value) == 0) return;

        var ph = other.GetComponent<PlayerHealth>();
        if (ph)
        {
            ph.TakeDamage(_damage);
            Impact();
        }
    }

#if UNITY_EDITOR
    void OnDrawGizmosSelected()
    {
        Gizmos.color = new Color(0.2f, 0.8f, 1f, 0.35f);
        Gizmos.DrawSphere(transform.position, _hitRadius);
    }
#endif
}
