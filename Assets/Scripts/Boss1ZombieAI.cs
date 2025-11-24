using System.Collections;
using UnityEngine;
using UnityEngine.AI;

public class Boss1ZombieAI : MonoBehaviour
{
    public enum State { Idle, Rage, Chase, Attack, Dead }

    [Header("References")]
    public NavMeshAgent agent;
    public Animator animator;
    public Transform player;

    [Header("Animator Parameters / States")]
    [SerializeField] string speedParam = "speed";
    [SerializeField] string trigMelee = "attack";
    [SerializeField] string trigCharge = "charge";
    [SerializeField] string trigSpike = "attackSpike";
    [SerializeField] string trigDie = "death";
    [SerializeField] string rageStateName = "rage";
    [SerializeField] string idleBlendStateName = "IdleWalkRun";
    [SerializeField] float rageCrossFade = 0.05f;

    [Header("Movement")]
    public float detectRange = 10f;
    public float walkSpeed = 2f;
    public float runSpeed = 4.5f;

    [Header("Melee Attack")]
    public float meleeRange = 2.2f;
    public int meleeDamage = 25;

    [Header("Charge Attack")]
    [SerializeField] float chargePushSpeed = 12f;
    [SerializeField] float chargeMaxTime = 1.2f;
    [SerializeField] float chargeStopRange = 1.8f;
    [SerializeField] float chargeObstacleStopRadius = 0.5f; 

    [Header("Shockwave (Projectile)")]
    public Transform shockwaveSpawn;
    public GameObject shockwaveProjectilePrefab;
    public float shockwaveSpeed = 14f;
    public float shockwaveLife = 3f;
    public int shockwaveDamage = 18;
    public float shockwaveHitRadius = 0.3f;

    [Header("HP")]
    public int maxHP = 50; // Kept low so you can kill it easily

    [Header("Skill Cooldown")]
    public float globalSkillCooldown = 5f;

    [Header("Debug / Safety")]
    [SerializeField] bool forceDisableRootMotion = true;
    [SerializeField] bool autoWarpToNavMesh = true;
    [SerializeField] float warpProbeRadius = 3f;

    [Header("Chase Smoothing")]
    [SerializeField] float chaseRepathInterval = 0.5f;
    [SerializeField] float repathDistance = 0.6f;
    [SerializeField] float speedDamp = 0.05f;
    [SerializeField] bool useDesiredVelocity = true;
    Vector3 _lastDest;
    float _nextRepathAt;

    bool _charging;
    float _chargeEndTime;

    float _attackForceExitAt = 0f;
    [SerializeField] string meleeStateName = "attack";
    [SerializeField] string chargeStateName = "charge";
    [SerializeField] string spikeStateName = "attackSpike";

    int _hp;
    bool _invincible = true;
    State _state = State.Idle;
    bool _raging;
    float _nextSkillTime;
    
    // --- NEW: Variables for Hit Flash ---
    private SkinnedMeshRenderer meshRenderer;
    private Color originalColor;
    // ------------------------------------

    void Awake()
    {
        if (!agent) agent = GetComponent<NavMeshAgent>();
        if (!animator) animator = GetComponent<Animator>();
        if (!player) player = GameObject.FindGameObjectWithTag("Player")?.transform;

        _hp = maxHP;

        if (animator)
        {
            if (forceDisableRootMotion) animator.applyRootMotion = false;
            animator.updateMode = AnimatorUpdateMode.Normal;
            animator.cullingMode = AnimatorCullingMode.AlwaysAnimate;
            animator.SetFloat(speedParam, 0f);
        }

        if (agent)
        {
            agent.updatePosition = true;
            agent.updateRotation = false;
            agent.isStopped = true;
            agent.acceleration = 18f;
            agent.angularSpeed = 720f;
            agent.autoBraking = false;
            agent.stoppingDistance = 1.0f;
            if (agent.radius < 0.3f) agent.radius = 0.4f;
            if (agent.height < 1.8f) agent.height = 2.0f;
        }

        var rb = GetComponent<Rigidbody>();
        if (rb) { rb.isKinematic = true; rb.detectCollisions = true; }
    }

    void Start()
    {
        // --- NEW: Find the renderer and save color ---
        meshRenderer = GetComponentInChildren<SkinnedMeshRenderer>();
        if (meshRenderer != null)
        {
            originalColor = meshRenderer.material.color;
        }
        // ---------------------------------------------

        // Start the coroutine to fix the "isStopped" error
        StartCoroutine(InitializeAgent());
    }

    IEnumerator InitializeAgent()
    {
        yield return new WaitForEndOfFrame();
        EnterIdleState();
    }

    void EnterIdleState()
    {
        _state = State.Idle;
        _invincible = true;
        EnsureAgentOnNavMesh();
        SafeSetStopped(true);
        if (animator && !string.IsNullOrEmpty(idleBlendStateName))
            animator.CrossFadeInFixedTime(idleBlendStateName, 0.1f, 0, 0f);
    }

    void Update()
    {
        if (_state == State.Dead || player == null) return;

        switch (_state)
        {
            case State.Idle: IdleUpdate(); break;
            case State.Rage: break; 
            case State.Chase: ChaseUpdate(); break;
            case State.Attack: AttackUpdate(); break;
        }

        if (animator)
        {
            float spd = 0f;
            if (AgentActiveOnNavMesh())
            {
                var v = useDesiredVelocity ? agent.desiredVelocity : agent.velocity;
                spd = v.magnitude;
            }
            animator.SetFloat(speedParam, spd, speedDamp, Time.deltaTime);
        }
    }

    void IdleUpdate()
    {
        if (Vector3.Distance(transform.position, player.position) <= detectRange)
            StartCoroutine(TriggerRage());
    }

    IEnumerator TriggerRage()
    {
        if (_raging) yield break;
        _raging = true;
        _state = State.Rage;
        if (animator)
        {
            if (!animator.HasState(0, Animator.StringToHash(rageStateName)))
                Debug.LogError("[BossAI] Animator state '" + rageStateName + "' not found on Layer 0.");
            animator.CrossFadeInFixedTime(rageStateName, rageCrossFade, 0, 0f);
        }
        yield return WaitForStateToFinish(rageStateName);
        _invincible = false;
        EnsureAgentOnNavMesh();
        SafeSetStopped(false);
        if (player) { SafeSetDestination(player.position); _lastDest = player.position; }
        _nextRepathAt = 0f;
        _nextSkillTime = Time.time + 1.0f;
        _state = State.Chase;
    }

    IEnumerator WaitForStateToFinish(string stateName, int layer = 0, float doneNormTime = 0.98f)
    {
        while (true)
        {
            var info = animator.GetCurrentAnimatorStateInfo(layer);
            if (info.IsName(stateName) && !animator.IsInTransition(layer)) break;
            yield return null;
        }
        while (true)
        {
            var info = animator.GetCurrentAnimatorStateInfo(layer);
            if (info.IsName(stateName) && !animator.IsInTransition(layer) && info.normalizedTime >= doneNormTime)
                break;
            yield return null;
        }
    }

    void ChaseUpdate()
    {
        EnsureAgentOnNavMesh();
        SafeSetStopped(false);
        if (Time.time >= _nextRepathAt && AgentActiveOnNavMesh())
        {
            _nextRepathAt = Time.time + chaseRepathInterval;
            if (player)
            {
                Vector3 want = player.position;
                if (!agent.hasPath || (agent.destination - want).sqrMagnitude > repathDistance * repathDistance)
                {
                    agent.SetDestination(want);
                    _lastDest = want;
                }
            }
        }
        float dist = player ? Vector3.Distance(transform.position, player.position) : Mathf.Infinity;
        if (AgentActiveOnNavMesh())
            agent.speed = (dist > 6f) ? runSpeed : walkSpeed;
        if (player)
        {
            Vector3 dir = player.position - transform.position; dir.y = 0f;
            if (dir.sqrMagnitude > 0.0001f)
            {
                Quaternion target = Quaternion.LookRotation(dir);
                transform.rotation = Quaternion.RotateTowards(transform.rotation, target, 720f * Time.deltaTime);
            }
        }
        if (Time.time >= _nextSkillTime)
        {
            int roll = Random.Range(0, 3);
            if (roll == 0) TriggerSkill(trigMelee);
            else if (roll == 1) TriggerSkill(trigCharge);
            else TriggerSkill(trigSpike);
            _nextSkillTime = Time.time + globalSkillCooldown;
        }
    }

    void AttackUpdate()
    {
        if (player)
        {
            Vector3 dir = player.position - transform.position; dir.y = 0f;
            if (dir.sqrMagnitude > 0.0001f)
            {
                var q = Quaternion.LookRotation(dir);
                transform.rotation = Quaternion.RotateTowards(transform.rotation, q, 720f * Time.deltaTime);
            }
        }
        if (_charging)
        {
            if (AgentActiveOnNavMesh()) agent.isStopped = true;
            Vector3 toPlayer = (player ? (player.position - transform.position) : transform.forward);
            toPlayer.y = 0f;
            if (toPlayer.sqrMagnitude > 0.0001f) toPlayer.Normalize();
            transform.position += toPlayer * chargePushSpeed * Time.deltaTime;
            if (Physics.SphereCast(transform.position + Vector3.up * 0.5f, chargeObstacleStopRadius, toPlayer, out var hit, 0.8f, ~0, QueryTriggerInteraction.Ignore))
            {
                EndChargeToChase();
                return;
            }
            float dist = player ? Vector3.Distance(transform.position, player.position) : Mathf.Infinity;
            if (dist <= chargeStopRange || Time.time >= _chargeEndTime)
            {
                EndChargeToChase();
                return;
            }
            return;
        }
        if (_attackForceExitAt > 0f && Time.time >= _attackForceExitAt)
        {
            _attackForceExitAt = 0f;
            BackToChase();
            return;
        }
        var info = animator.GetCurrentAnimatorStateInfo(0);
        bool inAtk = info.IsName(meleeStateName) || info.IsName(chargeStateName) || info.IsName(spikeStateName);
        if (!animator.IsInTransition(0) && inAtk && info.normalizedTime >= 0.98f)
        {
            BackToChase();
        }
    }

    void EndChargeToChase()
    {
        _charging = false;
        BackToChase();
    }

    void BackToChase()
    {
        if (_state == State.Dead) return;
        _state = State.Chase;
        if (AgentActiveOnNavMesh())
        {
            agent.isStopped = false;
            if (player) SafeSetDestination(player.position);
        }
        animator.speed = 1f;
    }

    void TriggerSkill(string trig)
    {
        _state = State.Attack;
        animator.ResetTrigger(trigMelee);
        animator.ResetTrigger(trigCharge);
        animator.ResetTrigger(trigSpike);
        animator.SetTrigger(trig);
        animator.speed = 1f;
        if (AgentActiveOnNavMesh()) agent.ResetPath();
        _attackForceExitAt = Time.time + 2.5f;
    }

    public void TakeDamage(int dmg)
    {
        if (_state == State.Dead) return;
        if (_state == State.Idle && _invincible)
        {
            StartCoroutine(TriggerRage());
            return;
        }
        if (_invincible) return;
        _hp -= dmg;
        
        // --- NEW: Start the Red Flash ---
        StartCoroutine(FlashRed());
        // --------------------------------

        if (_hp <= 0)
        {
            _hp = 0; 
            _state = State.Dead;
            if (agent) agent.enabled = false; 
            animator.SetTrigger(trigDie);
            Destroy(gameObject, 5f); 
        }
    }

    // ========== Animation Events ========
    public void OnMeleeStart() { if (AgentActiveOnNavMesh()) agent.isStopped = true; }
    public void OnMeleeHit()
    {
        // Removed PlayerHealth check to avoid dependency errors if you don't have it yet
        // if (!player) return;
        // player.GetComponent<PlayerHealth>()?.TakeDamage(meleeDamage);
    }
    public void OnMeleeEnd() => BackToChase();
    public void OnChargeStart()
    {
        _charging = true;
        _chargeEndTime = Time.time + chargeMaxTime;
        if (AgentActiveOnNavMesh()) agent.isStopped = true;
    }
    public void OnShockwaveEmit()
    {
        if (!shockwaveProjectilePrefab) { return; }
        Vector3 origin = shockwaveSpawn ? shockwaveSpawn.position : transform.position + Vector3.up * 1.0f;
        Vector3 dir = player ? (player.position + Vector3.up * 0.9f - origin) : transform.forward;
        dir.y = 0f; 
        if (dir.sqrMagnitude < 0.0001f) dir = transform.forward;
        dir.Normalize();
        var go = Instantiate(shockwaveProjectilePrefab, origin, Quaternion.LookRotation(dir, Vector3.up));
    }
    public void OnShockwaveEnd() => BackToChase();

    // =========== Utility Functions =============
    bool AgentActiveOnNavMesh() => agent && agent.enabled && agent.isOnNavMesh;
    void EnsureAgentOnNavMesh()
    {
        if (!agent) return;
        if (!agent.enabled) agent.enabled = true;
        if (!agent.isOnNavMesh && autoWarpToNavMesh)
        {
            if (NavMesh.SamplePosition(transform.position, out var hit, warpProbeRadius, NavMesh.AllAreas))
            {
                agent.Warp(hit.position);
            }
            else
            {
                var probe = transform.position + Vector3.up * 0.5f;
                if (NavMesh.SamplePosition(probe, out hit, warpProbeRadius + 2f, NavMesh.AllAreas))
                    agent.Warp(hit.position);
            }
        }
    }
    void SafeSetStopped(bool stopped) { if (AgentActiveOnNavMesh()) agent.isStopped = stopped; }
    bool SafeSetDestination(Vector3 pos) { if (AgentActiveOnNavMesh()) return false; return agent.SetDestination(pos); }

#if UNITY_EDITOR
    void OnGUI()
    {
        if (!agent) return;
        if (!agent.isOnNavMesh || !agent.enabled)
        {
            GUI.Label(new Rect(15, 15, 560, 80), "Agent initializing...");
            return;
        }
        string s =
            "STATE=" + _state + " inv=" + _invincible + "\n" +
            "enabled=" + agent.enabled + " onNav=" + agent.isOnNavMesh + " stopped=" + agent.isStopped + "\n" +
            "hasPath=" + agent.hasPath + " pending=" + agent.pathPending + " status=" + agent.pathStatus + "\n" +
            "vel=" + (AgentActiveOnNavMesh() ? agent.velocity.magnitude.ToString("F2") : "0.00") +
            " destDist=" + (agent.hasPath ? agent.remainingDistance.ToString("F2") : "-");
        GUI.Label(new Rect(15, 15, 560, 80), s);
    }
#endif

    // --- NEW: The function to change color ---
    IEnumerator FlashRed()
    {
        if (meshRenderer == null) yield break;
        
        meshRenderer.material.color = Color.red; 
        
        yield return new WaitForSeconds(0.1f); 
        
        meshRenderer.material.color = originalColor; 
    }
}