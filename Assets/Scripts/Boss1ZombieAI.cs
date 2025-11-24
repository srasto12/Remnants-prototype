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
    [SerializeField] string chargeStateName = "charge";
    [SerializeField] float rageCrossFade = 0.05f;
    [SerializeField] string dieStateName = "death";

    // hash
    int _hashIdle, _hashRage, _hashCharge, _hashMelee, _hashSpike, _hashDie;

    [Header("Movement")]
    public float detectRange = 10f;
    public float walkSpeed = 2f;
    public float runSpeed = 4.5f;

    [Header("Melee")]
    public float meleeRange = 2.2f;
    public float meleeTriggerPadding = 0.25f;
    public int meleeDamage = 25;

    [Header("Charge")]
    [SerializeField] float chargePushSpeed = 12f;
    [SerializeField] float chargeMaxTime = 1.2f;
    [SerializeField] float chargeObstacleStopRadius = 0.5f;
    [SerializeField] float chargeDistance = 8f;     // 固定衝刺距離

    bool _charging;
    float _chargeEndTime;
    Vector3 _chargeDir;
    Vector3 _chargeStartPos;                        // 衝刺起點

    [Header("Shockwave")]
    public Transform shockwaveSpawn;
    public GameObject shockwaveProjectilePrefab;
    public float shockwaveSpeed = 14f;
    public float shockwaveLife = 3f;
    public int shockwaveDamage = 18;
    public float shockwaveHitRadius = 0.3f;

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
    float _nextRepathAt;

    [Header("Audio")]
    public AudioClip rageSfx;
    [Range(0f, 1f)]
    public float rageVolume = 1f;
    public float rageMinDistance = 5f;
    public float rageMaxDistance = 40f;

    // 攻擊看門狗 / Fallback
    float _attackForceExitAt = 0f;
    [SerializeField] string meleeStateName = "attack";
    [SerializeField] string spikeStateName = "attackSpike";

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

            _hashIdle = !string.IsNullOrEmpty(idleBlendStateName) ? Animator.StringToHash(idleBlendStateName) : 0;
            _hashRage = !string.IsNullOrEmpty(rageStateName) ? Animator.StringToHash(rageStateName) : 0;
            _hashCharge = !string.IsNullOrEmpty(chargeStateName) ? Animator.StringToHash(chargeStateName) : 0;
            _hashMelee = !string.IsNullOrEmpty(meleeStateName) ? Animator.StringToHash(meleeStateName) : 0;
            _hashSpike = !string.IsNullOrEmpty(spikeStateName) ? Animator.StringToHash(spikeStateName) : 0;
            _hashDie   = !string.IsNullOrEmpty(dieStateName) ? Animator.StringToHash(dieStateName) : 0;
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

        if (_hashIdle != 0)
            animator.CrossFadeInFixedTime(_hashIdle, 0.1f, 0, 0f);
    }

    void Update()
    {
        if (_state == State.Dead || player == null) return;

        switch (_state)
        {
            case State.Idle:  IdleUpdate();  break;
            case State.Rage:                 break;
            case State.Chase: ChaseUpdate(); break;
            case State.Attack: AttackUpdate(); break;
        }

        // 非 Charge 時才推 BlendTree 速度
        if (animator && !_charging)
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

        if (rageSfx)
        {
            AudioSource src = gameObject.AddComponent<AudioSource>();
            src.clip = rageSfx;
            src.volume = rageVolume;
            src.spatialBlend = 1f; // 3D
            src.minDistance = rageMinDistance;
            src.maxDistance = rageMaxDistance;
            src.rolloffMode = AudioRolloffMode.Linear;
            src.Play();
            Destroy(src, rageSfx.length);
        }

        if (_hashRage != 0)
            animator.CrossFadeInFixedTime(_hashRage, rageCrossFade, 0, 0f);
        else
            Debug.LogError("[BossAI] Rage state name not found on Animator.");

        yield return WaitForStateToFinish(_hashRage);

        _invincible = false;

        EnsureAgentOnNavMesh();
        SafeSetStopped(false);
        if (player) { SafeSetDestination(player.position); }

        _nextRepathAt = 0f;
        _nextSkillTime = Time.time + 1.0f;
        _state = State.Chase;
    }

    IEnumerator WaitForStateToFinish(int stateHash, int layer = 0, float doneNormTime = 0.98f)
    {
        while (true)
        {
            var info = animator.GetCurrentAnimatorStateInfo(layer);
            if (info.shortNameHash == stateHash && !animator.IsInTransition(layer)) break;
            yield return null;
        }
        while (true)
        {
            var info = animator.GetCurrentAnimatorStateInfo(layer);
            if (info.shortNameHash == stateHash && !animator.IsInTransition(layer) && info.normalizedTime >= doneNormTime)
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

        // 技能時機（此示例先固定觸發 Charge）
        if (Time.time >= _nextSkillTime)
        {
            float meleeTriggerRange = meleeRange + meleeTriggerPadding;
            if (dist <= meleeTriggerRange)
            {
                // 距離夠 → 直接執行近戰
                TriggerSkill(trigMelee);
            }
            else
            {
                // 距離不夠 → 在 衝刺 / 衝擊波 中二選一
                if (Random.value < 0.5f) TriggerSkill(trigCharge);
                else TriggerSkill(trigSpike);
            }
            _nextSkillTime = Time.time + globalSkillCooldown;
        }
    }

    void AttackUpdate()
    {
        if (!_charging && player)
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

            // 固定朝衝刺方向前進（不再追玩家）
            transform.rotation = Quaternion.LookRotation(_chargeDir);

            if (_hashCharge != 0)
            {
                var st = animator.GetCurrentAnimatorStateInfo(0);
                if (!animator.IsInTransition(0) && st.shortNameHash != _hashCharge)
                    animator.CrossFadeInFixedTime(_hashCharge, 0.05f, 0, 0f);
            }

            transform.position += _chargeDir * chargePushSpeed * Time.deltaTime;

            if (AgentActiveOnNavMesh()) agent.nextPosition = transform.position;

            // 碰撞提前終止
            if (Physics.SphereCast(transform.position + Vector3.up * 0.5f, chargeObstacleStopRadius, _chargeDir,
                out var hit, 0.8f, ~0, QueryTriggerInteraction.Ignore))
            {
                StopCharge();
                return;
            }

            // 以「沿衝刺方向的位移」判斷是否到達距離上限或時間上限
            float traveled = Vector3.Dot(transform.position - _chargeStartPos, _chargeDir);
            if (traveled >= chargeDistance || Time.time >= _chargeEndTime)
            {
                StopCharge();
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
        bool inAtk =
            info.shortNameHash == _hashMelee ||
            info.shortNameHash == _hashCharge ||
            info.shortNameHash == _hashSpike;

        if (!animator.IsInTransition(0) && inAtk && info.normalizedTime >= 0.98f)
        {
            BackToChase();
        }
    }

    void StopCharge()
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

        if (_hashIdle != 0)
            animator.CrossFadeInFixedTime(_hashIdle, 0.08f, 0, 0f);
    }

    void TriggerSkill(string trig)
    {
        _state = State.Attack;
        animator.ResetTrigger(trigMelee);
        animator.ResetTrigger(trigCharge);
        animator.ResetTrigger(trigSpike);
        animator.SetTrigger(trig);

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

            if (AgentActiveOnNavMesh())
            {
                agent.isStopped = true;
                agent.ResetPath();
                agent.enabled = false;
            }

            var col = GetComponent<Collider>();
            if (col) col.enabled = false;

            animator.ResetTrigger(trigMelee);
            animator.ResetTrigger(trigCharge);
            animator.ResetTrigger(trigSpike);
            animator.SetTrigger(trigDie);

            if (_hashDie != 0) StartCoroutine(DespawnOnAnimDone(_hashDie, 0));
            else StartCoroutine(DespawnAfterSeconds(3.0f));
        }
    }

    // ===== Animation Events =====
    public void OnMeleeStart()
    {
        if (AgentActiveOnNavMesh()) agent.isStopped = true;

        float meleeTriggerRange = meleeRange + meleeTriggerPadding;
        if (player && Vector3.Distance(transform.position, player.position) > meleeTriggerRange + 0.05f)
        {
            BackToChase();
        }
    }

    public void OnMeleeHit()
    {
        if (!player) return;
        if (Vector3.Distance(transform.position, player.position) <= meleeRange + 0.4f)
            player.GetComponent<PlayerHealth>()?.TakeDamage(meleeDamage);
    }
    public void OnMeleeEnd() => BackToChase();

    // Charge 起手
    public void OnChargeStart()
    {
        if (_charging) return;

        _charging = true;
        _chargeEndTime = Time.time + chargeMaxTime;

        // 固定用當下 forward 作為衝刺方向與距離參考
        _chargeStartPos = transform.position;
        _chargeDir = transform.forward;
        _chargeDir.y = 0f;
        if (_chargeDir.sqrMagnitude < 0.0001f) _chargeDir = transform.forward;
        _chargeDir.Normalize();

        if (AgentActiveOnNavMesh())
        {
            agent.isStopped = true;
            agent.ResetPath(); // 確保不再嘗試跟隨玩家
        }

        transform.rotation = Quaternion.LookRotation(_chargeDir);

        if (_hashCharge != 0)
            animator.CrossFadeInFixedTime(_hashCharge, 0.05f, 0, 0f);
    }

    // Shockwave
    public void OnShockwaveEmit()
    {
        if (!shockwaveProjectilePrefab)
        {
            Debug.LogWarning($"{name}: shockwaveProjectilePrefab not assigned.", this);
            return;
        }

        Vector3 origin = shockwaveSpawn ? shockwaveSpawn.position : transform.position + Vector3.up * 1.0f;
        Vector3 dir = player ? (player.position + Vector3.up * 0.9f - origin) : transform.forward;
        dir.y = 0f;
        if (dir.sqrMagnitude < 0.0001f) dir = transform.forward;
        dir.Normalize();

        var go = Instantiate(shockwaveProjectilePrefab, origin, Quaternion.LookRotation(dir, Vector3.up));
        var proj = go.GetComponent<ShockwaveProjectile>();
        if (!proj) proj = go.AddComponent<ShockwaveProjectile>();
        proj.Initialize(dir, shockwaveSpeed, shockwaveDamage, shockwaveLife, shockwaveHitRadius, this);
    }
    public void OnShockwaveEnd() => BackToChase();

    public void OnDeathEnd()
    {
        if (_state != State.Dead) return;
        Destroy(gameObject);
    }

    // ===== utils =====
    bool AgentActiveOnNavMesh() => agent && agent.enabled && agent.isOnNavMesh;

    void EnsureAgentOnNavMesh()
    {
        if (!agent) return;
        if (!agent.enabled) agent.enabled = true;

        if (!agent.isOnNavMesh && autoWarpToNavMesh)
        {
            if (NavMesh.SamplePosition(transform.position, out var hit, warpProbeRadius, NavMesh.AllAreas))
                agent.Warp(hit.position);
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

    IEnumerator DespawnOnAnimDone(int stateHash, int layer, float doneNormTime = 0.98f)
    {
        while (true)
        {
            var st = animator.GetCurrentAnimatorStateInfo(layer);
            if (st.shortNameHash == stateHash && !animator.IsInTransition(layer)) break;
            yield return null;
        }
        while (true)
        {
            var st = animator.GetCurrentAnimatorStateInfo(layer);
            if (st.shortNameHash == stateHash && !animator.IsInTransition(layer) && st.normalizedTime >= doneNormTime)
                break;
            yield return null;
        }
        Destroy(gameObject);
    }

    IEnumerator DespawnAfterSeconds(float seconds)
    {
        yield return new WaitForSeconds(seconds);
        Destroy(gameObject);
    }

#if UNITY_EDITOR
    void OnGUI()
    {
        if (!agent) return;

        bool active = agent.enabled && agent.isOnNavMesh;

        string stopped = active ? agent.isStopped.ToString() : "-";
        string hasPath = active ? agent.hasPath.ToString() : "-";
        string pending = active ? agent.pathPending.ToString() : "-";
        string status  = active ? agent.pathStatus.ToString() : "-";
        string vel     = active ? agent.velocity.magnitude.ToString("F2") : "0.00";
        string remDist = (active && agent.hasPath) ? agent.remainingDistance.ToString("F2") : "-";

        string s =
            "STATE=" + _state + " inv=" + _invincible + "\n" +
            "enabled=" + agent.enabled + " onNav=" + agent.isOnNavMesh + " stopped=" + stopped + "\n" +
            "hasPath=" + hasPath + " pending=" + pending + " status=" + status + "\n" +
            "vel=" + vel + " destDist=" + remDist;

        GUI.Label(new Rect(15, 15, 560, 80), s);
    }
#endif
}
