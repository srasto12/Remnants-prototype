using UnityEngine;
using UnityEngine.AI;

public class ZombieAI : MonoBehaviour
{
    public enum State { Idle, Patrol, Chase, Attack, Dead }
    public State state = State.Idle;

    [Header("References")]
    public NavMeshAgent agent;
    public Transform player;
    public Animator animator;

    [Header("Animator Parameter Names")]
    [SerializeField] string speedParam = "speed";
    [SerializeField] string attackTrigger = "attack";
    [SerializeField] string dieTrigger = "die";

    [Header("Detection")]
    public float viewDistance = 15f;
    [Range(0, 180)] public float viewAngle = 110f;
    public float hearDistance = 8f;
    public LayerMask obstacleMask;

    [Header("Movement & Attack")]
    public float chaseSpeed = 3.5f;
    public float walkSpeed = 1.5f;
    public float attackDistance = 1.8f;
    public float attackCooldown = 1.2f;
    public int attackDamage = 10;

    [Header("Patrol Waypoints")]
    public Transform[] waypoints;
    int _wpIndex;

    [Header("Health")]
    public int maxHP = 100;
    int _hp;

    [Header("Weapon Hitbox (Optional)")]
    public Collider weaponHitbox;

    [Header("Root Motion (Optional)")]
    public bool useRootMotion = false;

    [Header("Death")]
    [SerializeField] bool useDeathAnimationEvent = true;

    protected bool _ready = false;
    float _nextAttackTime;

    // ---------- UNITY LIFECYCLE ----------
    void Awake()
    {
        if (!agent) agent = GetComponent<NavMeshAgent>();
        if (!player) player = GameObject.FindGameObjectWithTag("Player")?.transform;
        if (!animator) animator = GetComponent<Animator>();
        _hp = maxHP;

        if (useRootMotion && animator)
        {
            animator.applyRootMotion = true;
            if (agent)
            {
                agent.updatePosition = false;
                agent.updateRotation = false;
            }
        }
    }

    void OnEnable()
    {
        _ready = false;
        if (agent) agent.enabled = false;
    }

    void OnDisable()
    {
        _ready = false;
        if (agent)
        {
            if (agent.enabled) agent.ResetPath();
            agent.enabled = false;
        }
    }

    void Update()
    {
        if (!_ready || player == null || state == State.Dead) return;
        if (agent == null || !agent.enabled || !agent.isOnNavMesh) return;

        switch (state)
        {
            case State.Idle:
            case State.Patrol:
                LookForPlayer();
                PatrolUpdate();
                break;
            case State.Chase:
                ChaseUpdate();
                break;
            case State.Attack:
                AttackUpdate();
                break;
        }
        UpdateAnimator();
    }

    // ---------- MAIN LOGIC ----------
    public void ResetZombie(Vector3 desiredPos)
    {
        if (!agent) agent = GetComponent<NavMeshAgent>();
        if (!animator) animator = GetComponent<Animator>();

        transform.position = desiredPos;
        transform.rotation = Quaternion.identity;
        agent.enabled = true;

        if (NavMesh.SamplePosition(desiredPos, out var hit, 10f, NavMesh.AllAreas))
        {
            if (!agent.Warp(hit.position))
            {
                Debug.LogWarning($"{name}: Warp failed; disabling agent.");
                agent.enabled = false;
                _ready = false;
                return;
            }
        }
        else
        {
            Debug.LogWarning($"{name}: No NavMesh near spawn; disabling agent.");
            agent.enabled = false;
            _ready = false;
            return;
        }

        _hp = maxHP;
        state = (waypoints != null && waypoints.Length > 0) ? State.Patrol : State.Idle;
        agent.speed = walkSpeed;
        if (animator && !string.IsNullOrEmpty(speedParam)) animator.SetFloat(speedParam, 0f);

        if (state == State.Patrol && waypoints != null && waypoints.Length > 0)
            agent.SetDestination(waypoints[_wpIndex].position);

        _ready = true;
    }

    void PatrolUpdate()
    {
        if (state != State.Patrol) return;
        if (!agent.pathPending && agent.remainingDistance <= agent.stoppingDistance + 0.1f)
        {
            _wpIndex = (_wpIndex + 1) % waypoints.Length;
            agent.SetDestination(waypoints[_wpIndex].position);
        }
    }

    void LookForPlayer()
    {
        Vector3 toPlayer = player.position - transform.position;
        float dist = toPlayer.magnitude;

        if (dist <= hearDistance) { StartChase(); return; }

        if (dist <= viewDistance)
        {
            Vector3 dir = toPlayer.normalized;
            float angle = Vector3.Angle(transform.forward, dir);
            if (angle <= viewAngle * 0.5f)
            {
                if (!Physics.Raycast(transform.position + Vector3.up * 1.6f, dir, dist, obstacleMask))
                    StartChase();
            }
        }
    }

    void StartChase()
    {
        state = State.Chase;
        agent.speed = chaseSpeed;
    }

    void ChaseUpdate()
    {
        if (agent.enabled && agent.isOnNavMesh)
        {
            agent.SetDestination(player.position);
        }
        else
        {
            if (NavMesh.SamplePosition(transform.position, out var hit, 3f, NavMesh.AllAreas))
                agent.Warp(hit.position);
            return;
        }

        float dist = Vector3.Distance(transform.position, player.position);
        if (dist <= attackDistance)
        {
            state = State.Attack;
            agent.ResetPath();
        }
        else if (dist > viewDistance * 1.5f)
        {
            state = (waypoints != null && waypoints.Length > 0) ? State.Patrol : State.Idle;
            agent.speed = walkSpeed;
            if (state == State.Patrol && waypoints != null && waypoints.Length > 0)
                agent.SetDestination(waypoints[_wpIndex].position);
        }
    }

    void AttackUpdate()
    {
        Vector3 look = player.position - transform.position;
        look.y = 0f;
        if (look.sqrMagnitude > 0.001f)
            transform.rotation = Quaternion.Slerp(transform.rotation, Quaternion.LookRotation(look), 10f * Time.deltaTime);

        float dist = Vector3.Distance(transform.position, player.position);
        if (dist > attackDistance + 0.2f) { StartChase(); return; }

        if (Time.time >= _nextAttackTime)
        {
            _nextAttackTime = Time.time + attackCooldown;
            if (animator && !string.IsNullOrEmpty(attackTrigger)) animator.SetTrigger(attackTrigger);
        }
    }

    public void TakeDamage(int dmg)
    {
        if (state == State.Dead) return;
        _hp -= dmg;
        if (_hp <= 0) Die();
        else StartChase();
    }

    // ---------- VIRTUAL HOOKS ----------
    public virtual void OnAttackHit()
    {
        if (!player) return;
        if (Vector3.Distance(transform.position, player.position) <= attackDistance + 0.3f)
            player.GetComponent<PlayerHealth>()?.TakeDamage(attackDamage);
    }

    public virtual void OnAttackStart()
    {
        if (weaponHitbox) weaponHitbox.enabled = true;
    }

    public virtual void OnAttackEnd()
    {
        if (weaponHitbox) weaponHitbox.enabled = false;
    }

    protected virtual void Die()
    {
        state = State.Dead;

        if (animator && !string.IsNullOrEmpty(dieTrigger))
            animator.SetTrigger(dieTrigger);

        if (agent)
        {
            if (agent.enabled) agent.ResetPath();
            agent.enabled = false;
        }
        _ready = false;
    }

    public void OnDeathAnimationComplete()
    {
        if (!useDeathAnimationEvent) return;
        SpawnManager.Instance?.Despawn(this);
    }

    // ---------- ANIMATION HOOK ----------
    void OnAnimatorMove()
    {
        if (!useRootMotion || !animator) return;
        transform.position += animator.deltaPosition;
        transform.rotation *= animator.deltaRotation;
        if (agent) agent.nextPosition = transform.position;
    }

    void UpdateAnimator()
    {
        if (!animator) return;
        float s = (agent && agent.enabled) ? agent.velocity.magnitude : 0f;
        if (!string.IsNullOrEmpty(speedParam)) animator.SetFloat(speedParam, s);
    }
}
