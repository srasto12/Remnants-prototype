using UnityEngine;

public class ShockwaveProjectile : MonoBehaviour
{
    Vector3 _dir;
    float _speed;
    int _damage;
    float _life;
    float _hitRadius = 0.3f;
    MonoBehaviour _owner; // 可不用；用來避免打到自己

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

    void Update()
    {
        if (_done) return;

        // 推進
        transform.position += _dir * _speed * Time.deltaTime;

        // 命中偵測（簡單 OverlapSphere；也可改 Raycast 提前命中）
        var cols = Physics.OverlapSphere(transform.position, _hitRadius, ~0, QueryTriggerInteraction.Ignore);
        for (int i = 0; i < cols.Length; i++)
        {
            // 避免打到自己（可依你的玩家 Layer 做過濾）
            if (_owner && cols[i].transform.IsChildOf(_owner.transform)) continue;

            var ph = cols[i].GetComponent<PlayerHealth>();
            if (ph)
            {
                ph.TakeDamage(_damage);
                Explode();
                return;
            }
        }

        // 壽命到自毀
        if (Time.time >= _dieAt)
            Explode();
    }

    void Explode()
    {
        if (_done) return;
        _done = true;
        // 這裡可加特效 / 音效
        Destroy(gameObject);
    }

    // 若你的 Prefab 帶 Trigger Collider，也可用這個：
    void OnTriggerEnter(Collider other)
    {
        if (_done) return;
        var ph = other.GetComponent<PlayerHealth>();
        if (ph)
        {
            ph.TakeDamage(_damage);
            Explode();
        }
    }
}
