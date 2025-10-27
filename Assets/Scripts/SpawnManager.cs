using UnityEngine;
using UnityEngine.AI;
using System.Collections.Generic;

public class SpawnManager : MonoBehaviour
{
    public static SpawnManager Instance { get; private set; }

    [Header("Prefabs")]
    public ZombieAI normalZombiePrefab;    // Regular zombie (ZombieAI)
    public HeavyZombie heavyZombiePrefab;  // Heavy zombie (HeavyZombie)
    public ZombieAI fastZombiePrefab;      // Fast zombie (uses ZombieAI directly)

    [Header("Spawn Weights (higher = more likely)")]
    [Tooltip("Weight for normal zombie spawns")]
    public int normalWeight = 5;
    [Tooltip("Weight for heavy zombie spawns (lower = rarer)")]
    public int heavyWeight = 1;
    [Tooltip("Weight for fast zombie spawns")]
    public int fastWeight = 3;

    [Header("Pooling")]
    public int initialPoolSizeNormal = 10;
    public int initialPoolSizeHeavy = 5;
    public int initialPoolSizeFast = 8;

    // Separate pools for each zombie type
    private readonly Queue<ZombieAI> _poolNormal = new Queue<ZombieAI>();
    private readonly Queue<ZombieAI> _poolHeavy = new Queue<ZombieAI>();
    private readonly Queue<ZombieAI> _poolFast = new Queue<ZombieAI>();

    [Header("Spawn Points (Optional)")]
    public Transform[] spawnPoints;

    [Header("Random Area (Optional)")]
    public Vector3 areaCenter;
    public Vector3 areaSize = new Vector3(50, 0, 50);

    [Header("Rules")]
    public int maxAlive = 15;
    public float spawnInterval = 3f;
    public float minSpawnDistanceFromPlayer = 12f;

    private Transform _player;
    private float _timer;
    private readonly List<ZombieAI> _alive = new List<ZombieAI>();

    private enum ZombieKind { Normal, Heavy, Fast }

    // Records which zombie instance belongs to which kind
    private readonly Dictionary<ZombieAI, ZombieKind> _kindOf = new Dictionary<ZombieAI, ZombieKind>();

    void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
    }

    void Start()
    {
        _player = GameObject.FindGameObjectWithTag("Player")?.transform;

        // Prewarm pools
        for (int i = 0; i < initialPoolSizeNormal; i++)
        {
            var z = Instantiate(normalZombiePrefab, new Vector3(99999, 99999, 99999), Quaternion.identity, transform);
            z.gameObject.SetActive(false);
            _poolNormal.Enqueue(z);
            _kindOf[z] = ZombieKind.Normal;
        }
        for (int i = 0; i < initialPoolSizeHeavy; i++)
        {
            var z = Instantiate(heavyZombiePrefab, new Vector3(99999, 99999, 99999), Quaternion.identity, transform);
            z.gameObject.SetActive(false);
            _poolHeavy.Enqueue(z);
            _kindOf[z] = ZombieKind.Heavy;
        }
        for (int i = 0; i < initialPoolSizeFast; i++)
        {
            var z = Instantiate(fastZombiePrefab, new Vector3(99999, 99999, 99999), Quaternion.identity, transform);
            z.gameObject.SetActive(false);
            _poolFast.Enqueue(z);
            _kindOf[z] = ZombieKind.Fast;
        }
    }

    void Update()
    {
        _timer += Time.deltaTime;
        if (_timer >= spawnInterval)
        {
            _timer = 0f;
            TrySpawn();
        }
    }

    void TrySpawn()
    {
        if (_alive.Count >= maxAlive) return;
        if (!GetValidSpawnPosition(out Vector3 pos)) return;

        ZombieKind kind = PickKind();

        ZombieAI zombie = DequeueFromPool(kind);
        if (zombie == null)
        {
            zombie = InstantiateFallback(kind);
            if (zombie == null) return;
            zombie.gameObject.SetActive(false);
            _kindOf[zombie] = kind; // Map it to correct kind
        }

        // Set position while inactive to avoid timing issues
        zombie.transform.position = pos;
        zombie.transform.rotation = Quaternion.identity;

        zombie.gameObject.SetActive(true);
        zombie.ResetZombie(pos);

        _alive.Add(zombie);
    }

    ZombieKind PickKind()
    {
        int wNormal = (normalZombiePrefab != null && normalWeight > 0) ? normalWeight : 0;
        int wHeavy = (heavyZombiePrefab != null && heavyWeight > 0) ? heavyWeight : 0;
        int wFast = (fastZombiePrefab != null && fastWeight > 0) ? fastWeight : 0;

        int total = wNormal + wHeavy + wFast;
        if (total <= 0)
        {
            if (normalZombiePrefab != null) return ZombieKind.Normal;
            if (fastZombiePrefab != null) return ZombieKind.Fast;
            return ZombieKind.Heavy;
        }

        int r = Random.Range(0, total);
        if (r < wNormal) return ZombieKind.Normal;
        r -= wNormal;
        if (r < wHeavy) return ZombieKind.Heavy;
        return ZombieKind.Fast;
    }

    ZombieAI DequeueFromPool(ZombieKind kind)
    {
        switch (kind)
        {
            case ZombieKind.Heavy:
                if (_poolHeavy.Count > 0) return _poolHeavy.Dequeue();
                break;
            case ZombieKind.Fast:
                if (_poolFast.Count > 0) return _poolFast.Dequeue();
                break;
            default:
                if (_poolNormal.Count > 0) return _poolNormal.Dequeue();
                break;
        }
        return null;
    }

    ZombieAI InstantiateFallback(ZombieKind kind)
    {
        switch (kind)
        {
            case ZombieKind.Heavy:
                if (heavyZombiePrefab != null)
                    return Instantiate(heavyZombiePrefab, new Vector3(99999, 99999, 99999), Quaternion.identity, transform);
                if (fastZombiePrefab != null)
                    return Instantiate(fastZombiePrefab, new Vector3(99999, 99999, 99999), Quaternion.identity, transform);
                if (normalZombiePrefab != null)
                    return Instantiate(normalZombiePrefab, new Vector3(99999, 99999, 99999), Quaternion.identity, transform);
                break;

            case ZombieKind.Fast:
                if (fastZombiePrefab != null)
                    return Instantiate(fastZombiePrefab, new Vector3(99999, 99999, 99999), Quaternion.identity, transform);
                if (normalZombiePrefab != null)
                    return Instantiate(normalZombiePrefab, new Vector3(99999, 99999, 99999), Quaternion.identity, transform);
                if (heavyZombiePrefab != null)
                    return Instantiate(heavyZombiePrefab, new Vector3(99999, 99999, 99999), Quaternion.identity, transform);
                break;

            default: // Normal
                if (normalZombiePrefab != null)
                    return Instantiate(normalZombiePrefab, new Vector3(99999, 99999, 99999), Quaternion.identity, transform);
                if (fastZombiePrefab != null)
                    return Instantiate(fastZombiePrefab, new Vector3(99999, 99999, 99999), Quaternion.identity, transform);
                if (heavyZombiePrefab != null)
                    return Instantiate(heavyZombiePrefab, new Vector3(99999, 99999, 99999), Quaternion.identity, transform);
                break;
        }
        return null;
    }

    bool GetValidSpawnPosition(out Vector3 position)
    {
        // Try fixed spawn points first
        if (spawnPoints != null && spawnPoints.Length > 0)
        {
            for (int attempt = 0; attempt < 10; attempt++)
            {
                var sp = spawnPoints[Random.Range(0, spawnPoints.Length)];
                var candidate = sp.position;
                if (IsFarFromPlayer(candidate) && NavMesh.SamplePosition(candidate, out var hit, 8f, NavMesh.AllAreas))
                {
                    position = hit.position;
                    return true;
                }
            }
        }

        // Random area fallback
        Bounds b = new Bounds(areaCenter, areaSize);
        for (int attempt = 0; attempt < 24; attempt++)
        {
            var rand = new Vector3(
                Random.Range(b.min.x, b.max.x),
                b.center.y,
                Random.Range(b.min.z, b.max.z)
            );
            if (!IsFarFromPlayer(rand)) continue;

            if (NavMesh.SamplePosition(rand, out var hit, 10f, NavMesh.AllAreas))
            {
                position = hit.position;
                return true;
            }
        }

        position = Vector3.zero;
        return false;
    }

    bool IsFarFromPlayer(Vector3 point)
    {
        if (_player == null) return true;
        return Vector3.Distance(point, _player.position) >= minSpawnDistanceFromPlayer;
    }

    // Called by ZombieAI when it dies
    public void Despawn(ZombieAI z)
    {
        if (_alive.Contains(z)) _alive.Remove(z);

        var agent = z.GetComponent<NavMeshAgent>();
        if (agent)
        {
            if (agent.enabled) agent.ResetPath();
            agent.enabled = false;
        }

        z.gameObject.SetActive(false);

        // Return to the correct pool
        if (_kindOf.TryGetValue(z, out var kind))
        {
            switch (kind)
            {
                case ZombieKind.Heavy: _poolHeavy.Enqueue(z); break;
                case ZombieKind.Fast: _poolFast.Enqueue(z); break;
                default: _poolNormal.Enqueue(z); break;
            }
        }
        else
        {
            // Fallback for untracked zombies
            if (z is HeavyZombie) _poolHeavy.Enqueue(z);
            else _poolNormal.Enqueue(z);
        }
    }

    void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.yellow;
        Gizmos.DrawWireCube(areaCenter, areaSize);
        Gizmos.color = Color.red;
        if (spawnPoints != null)
            foreach (var sp in spawnPoints) if (sp) Gizmos.DrawSphere(sp.position, 0.35f);
    }
}
