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
    [SerializeField] float chargePushSpeed = 12f;   // 衝刺速度
    [SerializeField] float chargeMaxTime = 1.2f;  // 最長衝刺時間（保險）
    [SerializeField] float chargeStopRange = 1.8f;  // 與玩家距離小於此值就停
    [SerializeField] float chargeObstacleStopRadius = 0.5f; // 障礙探測半徑（SphereCast）

    [Header("Shockwave (Projectile)")]
    public Transform shockwaveSpawn;                // 能量波生成點（可用胸口/手掌），未設則用 transform
    public GameObject shockwaveProjectilePrefab;    // 指到能量波預置
    public float shockwaveSpeed = 14f;              // 投射物速度
    public float shockwaveLife = 3f;               // 存活時間（秒）
    public int shockwaveDamage = 18;              // 傷害
    public float shockwaveHitRadius = 0.3f;         // 命中半徑（Projectile 也會用到）

    [Header("HP")]
    public int maxHP = 600;

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

    // Charge 狀態
    bool _charging;
    float _chargeEndTime;

    // 攻擊看門狗 / Fallback
    float _attackForceExitAt = 0f;
    [SerializeField] string meleeStateName = "attack";
    [SerializeField] string chargeStateName = "charge";
    [SerializeField] string spikeStateName = "attackSpike";

    // internal
    int _hp;
    bool _invincible = true;
    State _state = State.Idle;
    bool _raging;
    float _nextSkillTime;

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

    void Start() => EnterIdleState();

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
            case State.Rage: break; // 協程控制
            case State.Chase: ChaseUpdate(); break;
            case State.Attack: AttackUpdate(); break;
        }

        // BlendTree speed（m/s）
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
        _nextSkillTime = Time.time + 1.0f; // 緩衝 1 秒再出招
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

        // 週期性 Repath（位置變化夠大才重下）
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

        // 調速與轉身
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

        // 技能（完全隨機，不看距離）
        if (Time.time >= _nextSkillTime)
        {
            int roll = Random.Range(0, 3); // 0,1,2
            if (roll == 0) TriggerSkill(trigMelee);
            else if (roll == 1) TriggerSkill(trigCharge);
            else TriggerSkill(trigSpike);

            _nextSkillTime = Time.time + globalSkillCooldown;
        }
    }

    void AttackUpdate()
    {
        // 攻擊時也維持面向玩家
        if (player)
        {
            Vector3 dir = player.position - transform.position; dir.y = 0f;
            if (dir.sqrMagnitude > 0.0001f)
            {
                var q = Quaternion.LookRotation(dir);
                transform.rotation = Quaternion.RotateTowards(transform.rotation, q, 720f * Time.deltaTime);
            }
        }

        // A) Charge 期間：持續追到玩家身邊（含障礙偵測）
        if (_charging)
        {
            if (AgentActiveOnNavMesh()) agent.isStopped = true; // 停用 NavMesh 推進，改用程式位移

            // 追蹤玩家方向（半追尾）
            Vector3 toPlayer = (player ? (player.position - transform.position) : transform.forward);
            toPlayer.y = 0f;
            if (toPlayer.sqrMagnitude > 0.0001f)
                toPlayer.Normalize();
            transform.position += toPlayer * chargePushSpeed * Time.deltaTime;

            // 碰撞/障礙提前停止
            if (Physics.SphereCast(transform.position + Vector3.up * 0.5f, chargeObstacleStopRadius, toPlayer, out var hit, 0.8f, ~0, QueryTriggerInteraction.Ignore))
            {
                // 撞到牆或玩家 → 停
                EndChargeToChase();
                return;
            }

            // 到玩家身邊或時間到 → 停
            float dist = player ? Vector3.Distance(transform.position, player.position) : Mathf.Infinity;
            if (dist <= chargeStopRange || Time.time >= _chargeEndTime)
            {
                EndChargeToChase();
                return;
            }
            return;
        }

        // B) 攻擊看門狗：超時自動回 Chase
        if (_attackForceExitAt > 0f && Time.time >= _attackForceExitAt)
        {
            _attackForceExitAt = 0f;
            BackToChase();
            return;
        }

        // C) Fallback：clip 播完回 Chase
        var info = animator.GetCurrentAnimatorStateInfo(0);
        bool inAtk =
            info.IsName(meleeStateName) ||
            info.IsName(chargeStateName) ||
            info.IsName(spikeStateName);

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

        // 確保攻擊時動畫 1 倍速
        animator.speed = 1f;

        if (AgentActiveOnNavMesh())
            agent.ResetPath();

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
        if (_hp <= 0)
        {
            _state = State.Dead;
            if (agent) agent.enabled = true;
            animator.SetTrigger(trigDie);
        }
    }

    // ========== 動畫事件（Event） ========

    // 近戰
    public void OnMeleeStart()
    {
        if (AgentActiveOnNavMesh()) agent.isStopped = true;
    }
    public void OnMeleeHit()
    {
        if (!player) return;
        if (Vector3.Distance(transform.position, player.position) <= meleeRange + 0.4f)
            player.GetComponent<PlayerHealth>()?.TakeDamage(meleeDamage);
    }
    public void OnMeleeEnd() => BackToChase();

    // Charge
    public void OnChargeStart()
    {
        _charging = true;
        _chargeEndTime = Time.time + chargeMaxTime;
        if (AgentActiveOnNavMesh()) agent.isStopped = true;
    }
    //public void OnChargeEnd() => EndChargeToChase();

    // Shockwave（投射物）
    public void OnShockwaveEmit()
    {
        if (!shockwaveProjectilePrefab) { Debug.LogWarning($"{name}: shockwaveProjectilePrefab not assigned."); return; }

        Vector3 origin = shockwaveSpawn ? shockwaveSpawn.position : transform.position + Vector3.up * 1.0f;
        Vector3 dir = player ? (player.position + Vector3.up * 0.9f - origin) : transform.forward;
        dir.y = 0f; // 視覺較穩定（需要仰角就註解掉）
        if (dir.sqrMagnitude < 0.0001f) dir = transform.forward;
        dir.Normalize();

        var go = Instantiate(shockwaveProjectilePrefab, origin, Quaternion.LookRotation(dir, Vector3.up));
        var proj = go.GetComponent<ShockwaveProjectile>();
        if (proj)
        {
            proj.Initialize(dir, shockwaveSpeed, shockwaveDamage, shockwaveLife, shockwaveHitRadius, this);
        }
        else
        {
            // 無腳本也盡量推進（最簡單線性位移）
            go.AddComponent<ShockwaveProjectile>().Initialize(dir, shockwaveSpeed, shockwaveDamage, shockwaveLife, shockwaveHitRadius, this);
        }
    }
    public void OnShockwaveEnd() => BackToChase();

    // =========== 安全封裝 / 工具 =============
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

    void SafeSetStopped(bool stopped)
    {
        if (AgentActiveOnNavMesh())
            agent.isStopped = stopped;
    }

    bool SafeSetDestination(Vector3 pos)
    {
        if (!AgentActiveOnNavMesh()) return false;
        return agent.SetDestination(pos);
    }

#if UNITY_EDITOR
    void OnGUI()
    {
        if (!agent) return;
        string s =
            "STATE=" + _state + " inv=" + _invincible + "\n" +
            "enabled=" + agent.enabled + " onNav=" + agent.isOnNavMesh + " stopped=" + agent.isStopped + "\n" +
            "hasPath=" + agent.hasPath + " pending=" + agent.pathPending + " status=" + agent.pathStatus + "\n" +
            "vel=" + (AgentActiveOnNavMesh() ? agent.velocity.magnitude.ToString("F2") : "0.00") +
            " destDist=" + (agent.hasPath ? agent.remainingDistance.ToString("F2") : "-");
        GUI.Label(new Rect(15, 15, 560, 80), s);
    }
#endif
}
