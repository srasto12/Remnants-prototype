using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[RequireComponent(typeof(CharacterController))]
public class FlyingChaser : MonoBehaviour
{
    public enum AIState { Idle, Roaring, Chasing, Dead }

    [Header("Targets & Layers")]
    public Transform player;
    public LayerMask groundMask;
    public LayerMask targetMask;

    [Header("Movement")]
    public float moveSpeed = 6f;
    public float turnSpeed = 10f;

    [Header("Ranges")]
    public float detectionRange = 30f;
    public float meleeRange = 2.5f;

    [Header("Combat & Cooldowns")]
    public float sharedCooldown = 5f;
    public int dashDamage = 25;

    [Header("Poison Attack")]
    public GameObject poisonPuddlePrefab;
    public GameObject poisonSpitProjectilePrefab;
    public float spitSpeed = 20f;
    public float spitArcHeight = 1.5f;
    public float puddleLifetime = 3f;
    public float puddleTickDamage = 5f;
    public float puddleTickInterval = 0.5f;
    public Transform muzzle;

    [Header("Audio")]
    public AudioClip roarSfx;
    [Range(0f, 1f)]
    public float roarVolume = 1f;
    public float roarMinDistance = 5f;
    public float roarMaxDistance = 40f;

    [Header("Poison Multi-Spit Settings")]
    public int spitCount = 3;            // 一次幾發（1=單發）
    public float spitSideSpacing = 1.8f;   // 左右間距（以玩家 right 為基準）
    public float spitForwardOffset = 0f;   // 往玩家前方偏移（0=腳下）
    public float spitBurstInterval = 0f;   // 連發間隔（0=同時）

    [Header("Dash")]
    public float dashDistance = 10f;
    public float dashSpeed = 10f;

    [Header("Melee Attack")]
    public GameObject meleeHitboxLeft;
    public GameObject meleeHitboxRight;
    public int meleeDamageLeft = 20;
    public int meleeDamageRight = 20;

    [Header("Animator & Visuals")]
    public Animator animator;
    public Transform visualRoot;
    public float extraFacingOffsetY = 0f;

    [Header("Animator Triggers")]
    public string trigRoar = "roar";
    public string trigMelee = "attack";
    public string trigPoison = "poison";
    public string trigDash = "charge";
    public string trigDeath = "death";

    private CharacterController _cc;
    private AIState _state = AIState.Idle;
    private bool _hasRoared = false;
    private bool _isDashing = false;
    private bool _isAttacking = false;
    private float _cooldownTimer = 0f;
    private Vector3 _dashDir;
    private float _dashRemaining;
    private Quaternion _visualYawCalib = Quaternion.identity;
    private bool _visualWarnedSelf = false;
    private float _fixedY;

    private void Awake()
    {
        _cc = GetComponent<CharacterController>();
        if (animator == null) animator = GetComponent<Animator>();

        if (meleeHitboxLeft) meleeHitboxLeft.SetActive(false);
        if (meleeHitboxRight) meleeHitboxRight.SetActive(false);

        if (animator)
        {
            animator.cullingMode = AnimatorCullingMode.AlwaysAnimate;
            animator.applyRootMotion = false;
        }

        _fixedY = transform.position.y;
        CalibrateVisualYaw();
    }

    private void Update()
    {
        if (player == null || _state == AIState.Dead) return;

        Vector3 toPlayer = player.position - transform.position;
        float distToPlayer = toPlayer.magnitude;
        Vector3 flatToPlayer = Vector3.ProjectOnPlane(toPlayer, Vector3.up);

        if (!_hasRoared && distToPlayer <= detectionRange)
            TriggerRoar();

        if (_isDashing)
        {
            float step = Mathf.Min(_dashRemaining, dashSpeed * Time.deltaTime);
            Vector3 horizDelta = _dashDir * step;
            _cc.Move(horizDelta);
            MaintainY();
            _dashRemaining -= step;
            if (_dashRemaining <= 0f)
            {
                _isDashing = false;
                _isAttacking = false;
            }
            return;
        }

        // === 追擊 ===
        if (_state == AIState.Chasing && !_isAttacking)
        {
            if (flatToPlayer.sqrMagnitude > 0.001f)
            {
                Quaternion targetRot = Quaternion.LookRotation(flatToPlayer.normalized, Vector3.up);
                transform.rotation = Quaternion.Slerp(transform.rotation, targetRot, Time.deltaTime * turnSpeed);
            }

            if (distToPlayer > 1)
            {
                Vector3 horizFwd = Vector3.ProjectOnPlane(transform.forward, Vector3.up);
                _cc.Move(horizFwd * moveSpeed * Time.deltaTime);
            }
        }

        MaintainY();

        // === 攻擊冷卻 ===
        if (_state == AIState.Chasing)
        {
            if (_cooldownTimer > 0f) _cooldownTimer -= Time.deltaTime;
            else TrySelectAttack(distToPlayer);
        }
    }

    private void MaintainY()
    {
        float deltaY = _fixedY - transform.position.y;
        _cc.Move(new Vector3(0f, deltaY, 0f));
    }

    private void LateUpdate()
    {
        if (!visualRoot) return;
        if (visualRoot == transform && !_visualWarnedSelf)
        {
            Debug.LogWarning("[FlyingChaser] 建議將 visualRoot 指向子物件。");
            _visualWarnedSelf = true;
        }
        visualRoot.rotation = transform.rotation * _visualYawCalib * Quaternion.Euler(0f, extraFacingOffsetY, 0f);
    }

    private void TriggerRoar()
    {
        if (_hasRoared || animator == null) return;
        _hasRoared = true;
        _state = AIState.Roaring;
        _isAttacking = true;
        animator.SetTrigger(trigRoar);
        if (roarSfx)
        {
            AudioSource src = gameObject.AddComponent<AudioSource>();
            src.clip = roarSfx;
            src.volume = roarVolume;
            src.spatialBlend = 1f; // 3D
            src.minDistance = roarMinDistance;
            src.maxDistance = roarMaxDistance;
            src.rolloffMode = AudioRolloffMode.Linear;
            src.Play();
            Destroy(src, roarSfx.length);
        }
    }

    private void TrySelectAttack(float distToPlayer)
    {
        if (animator == null) return;
        bool canMelee = distToPlayer <= meleeRange + 0.1f;
        int choice = canMelee ? 0 : UnityEngine.Random.Range(1, 3); // 0=melee,1=poison,2=dash
        _isAttacking = true;
        _cooldownTimer = sharedCooldown;
        switch (choice)
        {
            case 0: animator.SetTrigger(trigMelee); break;
            case 1: animator.SetTrigger(trigPoison); break;
            case 2: animator.SetTrigger(trigDash); break;
        }
    }

    // ===== 動畫事件 =====
    public void Anim_RoarEnd()
    {
        if (_state != AIState.Dead)
        {
            _isAttacking = false;
            _state = AIState.Chasing;
        }
    }

    public void Anim_MeleeStartLeft()
    {
        if (meleeHitboxLeft)
        {
            var hb = meleeHitboxLeft.GetComponent<HitboxDamage>();
            if (hb) hb.damage = meleeDamageLeft;
            meleeHitboxLeft.SetActive(true);
        }
    }
    public void Anim_MeleeEndLeft()
    {
        if (meleeHitboxLeft) meleeHitboxLeft.SetActive(false);
    }
    public void Anim_MeleeStartRight()
    {
        if (meleeHitboxRight)
        {
            var hb = meleeHitboxRight.GetComponent<HitboxDamage>();
            if (hb) hb.damage = meleeDamageRight;
            meleeHitboxRight.SetActive(true);
        }
    }
    public void Anim_MeleeEndRight()
    {
        if (meleeHitboxRight) meleeHitboxRight.SetActive(false);
        _isAttacking = false;
    }

    // ===== 毒液：多發/連發，從口中射出，落在玩家腳下（或前方偏移）生成水窪 =====
    public void Anim_PoisonFire()
    {
        if (player == null) return;

        Vector3 start = muzzle ? muzzle.position : transform.position + Vector3.up;

        // 以玩家的水平 forward/right 決定散佈方向
        Vector3 pFwd = Vector3.ProjectOnPlane(player.forward, Vector3.up).normalized;
        Vector3 pRight = Vector3.ProjectOnPlane(player.right, Vector3.up).normalized;

        // 準備目標清單（左右對稱展開）
        List<Vector3> targets = new List<Vector3>();
        int half = Mathf.Max(0, spitCount - 1) / 2; // 方便處理奇偶
        if (spitCount <= 1)
        {
            targets.Add(player.position + pFwd * spitForwardOffset);
        }
        else
        {
            for (int i = -half; i <= half; i++)
            {
                if (spitCount % 2 == 0 && i == 0) continue; // 偶數時跳過 0，確保發數正確
                Vector3 offset = pRight * (i * spitSideSpacing) + pFwd * spitForwardOffset;
                targets.Add(player.position + offset);
                if (targets.Count >= spitCount) break;
            }
        }

        // 同時 or 連發
        if (spitBurstInterval <= 0f)
        {
            foreach (var tgt in targets)
                FireOneSpitToGround(start, tgt);
        }
        else
        {
            StartCoroutine(Co_SpitBurst(start, targets));
        }
    }

    public void Anim_PoisonEnd() => _isAttacking = false;

    private IEnumerator Co_SpitBurst(Vector3 start, List<Vector3> targets)
    {
        foreach (var tgt in targets)
        {
            FireOneSpitToGround(start, tgt);
            yield return new WaitForSeconds(spitBurstInterval);
        }
    }

    private void FireOneSpitToGround(Vector3 start, Vector3 targetCenter)
    {
        // 從目標上方往下找地面，確保落在地上
        Vector3 probe = targetCenter + Vector3.up * 5f;
        Vector3 targetPoint = targetCenter;
        Vector3 targetNormal = Vector3.up;

        if (Physics.Raycast(probe, Vector3.down, out RaycastHit hit, 30f, groundMask))
        {
            targetPoint = hit.point;
            targetNormal = hit.normal;
        }

        LaunchPoisonSpit(start, targetPoint, targetNormal);
    }

    private void LaunchPoisonSpit(Vector3 start, Vector3 targetPoint, Vector3 targetNormal)
    {
        if (!poisonSpitProjectilePrefab) return;

        var go = Instantiate(poisonSpitProjectilePrefab, start, Quaternion.identity);
        var proj = go.GetComponent<PoisonSpitProjectile>();
        if (!proj)
        {
            Debug.LogWarning("PoisonSpitProjectile missing on prefab.");
            Destroy(go);
            return;
        }

        proj.Init(start, targetPoint, spitArcHeight, spitSpeed, groundMask,
            (hitPos, hitNormal) =>
            {
                if (poisonPuddlePrefab)
                {
                    var puddle = Instantiate(poisonPuddlePrefab,
                        hitPos + Vector3.up * 0.02f,
                        Quaternion.FromToRotation(Vector3.up, hitNormal));

                    var p = puddle.GetComponent<PoisonPuddle>();
                    if (p)
                    {
                        p.lifetime = puddleLifetime;
                        p.tickDamage = puddleTickDamage;
                        p.tickInterval = puddleTickInterval;
                        p.targetMask = targetMask;
                    }
                }
            });
    }

    // ===== Dash =====
    public void Anim_DashStart()
    {
        _dashDir = Vector3.ProjectOnPlane(transform.forward, Vector3.up).normalized;
        _dashRemaining = dashDistance;
        _isDashing = true;
        _isAttacking = true;
    }

    private void OnControllerColliderHit(ControllerColliderHit hit)
    {
        if (_isDashing)
        {
            var dmg = hit.collider.GetComponentInParent<IDamageable>();
            if (dmg != null) dmg.TakeDamage(dashDamage, gameObject);
        }
    }

    private void CalibrateVisualYaw()
    {
        if (!visualRoot) { _visualYawCalib = Quaternion.identity; return; }
        Vector3 parentF = Vector3.ProjectOnPlane(transform.forward, Vector3.up);
        Vector3 childF = Vector3.ProjectOnPlane(visualRoot.forward, Vector3.up);
        if (parentF.sqrMagnitude < 1e-6f) parentF = Vector3.forward;
        if (childF.sqrMagnitude < 1e-6f) childF = Vector3.forward;
        float signed = Mathf.Acos(Mathf.Clamp(Vector3.Dot(parentF, childF), -1f, 1f)) * Mathf.Rad2Deg;
        float sign = Mathf.Sign(Vector3.Cross(parentF, childF).y);
        _visualYawCalib = Quaternion.Euler(0f, -signed * sign, 0f);
    }

    // 提供給 MonsterHealth 等外部呼叫
    public void NotifyDamaged(GameObject source)
    {
        if (!_hasRoared && _state != AIState.Dead)
            TriggerRoar();
    }

    public void Die()
    {
        if (_state == AIState.Dead) return;
        _state = AIState.Dead;
        _isAttacking = false;
        _isDashing = false;

        if (animator)
            animator.SetTrigger(trigDeath);
    }
}
