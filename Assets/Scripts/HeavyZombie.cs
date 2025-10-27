using System.Collections;
using UnityEngine;
//using Cinemachine;
using UnityEngine.VFX;

/// <summary>
/// Heavy zombie: slower & tougher variant with higher melee damage.
/// On death: spawns/plays an explosion VFX, triggers Cinemachine 3 impulses,
/// applies radial damage/force, then hands off to base death flow (animation + despawn).
/// </summary>
public class HeavyZombie : ZombieAI
{
    [Header("Heavy Melee")]
    [Tooltip("Damage dealt to player when attack animation hits.")]
    public int heavyAttackDamage = 25;

    [Header("Explosion Damage/Force")]
    [Tooltip("Damage applied to entities within the explosion radius on death.")]
    public int explosionDamage = 50;

    [Tooltip("Explosion radius in meters.")]
    public float explosionRadius = 4f;

    [Tooltip("Impulse applied to rigidbodies within the radius.")]
    public float explosionForce = 500f;

    [Tooltip("World offset added to the computed explosion center (raise Y to avoid ground clipping).")]
    public Vector3 worldOffset = new Vector3(0f, 0.2f, 0f);

    public enum ExplosionAnchor { TransformPosition, RendererCenter, HipsBone }

    [Tooltip("How to choose the explosion center on the zombie.")]
    public ExplosionAnchor anchorMode = ExplosionAnchor.RendererCenter;

    [Header("Explosion VFX (prefer scene object, fallback to prefab)")]
    [Tooltip("IN-SCENE disabled VFX object to reuse (enable → play → auto disable).")]
    public GameObject explosionVfxSceneObject;   // Drag a disabled object from Hierarchy (optional)

    [Tooltip("PROJECT prefab used if no scene object is assigned (instantiate → destroy).")]
    public GameObject explosionVfxPrefab;        // Drag a prefab from Project (optional)

    [Tooltip("Auto-disable delay for the IN-SCENE VFX (seconds).")]
    public float sceneVfxAutoHideDelay = 4f;

    [Tooltip("Lifetime for instantiated VFX (seconds).")]
    public float instantiatedVfxLifetime = 6f;

    [Header("Audio")]
    public AudioClip explosionSfx;
    public float explosionSfxVolume = 1f;

    // ---------------- Combat ----------------
    /// <summary>
    /// Animation Event hook: heavier melee damage than base.
    /// </summary>
    public override void OnAttackHit()
    {
        if (!player) return;

        // Small cushion so slight pose/foot sliding still registers
        if (Vector3.Distance(transform.position, player.position) <= attackDistance + 0.3f)
        {
            var ph = player.GetComponent<PlayerHealth>();
            if (ph != null) ph.TakeDamage(heavyAttackDamage);
        }
    }

    // ---------------- Death / Explosion ----------------
    /// <summary>
    /// Orchestrates VFX + impulse + radial damage/force, then calls base.Die().
    /// Base handles animation trigger and (via event or delay) despawn.
    /// </summary>
    protected override void Die()
    {
        // 1) Compute a single unified center for VFX, impulse, damage & force
        Vector3 center = GetExplosionCenter() + worldOffset;

        // 2) VFX & Cinemachine impulse
        PlayExplosionVfxAndImpulse(center);

        // 3) SFX
        if (explosionSfx) AudioSource.PlayClipAtPoint(explosionSfx, center, explosionSfxVolume);

        // 4) Area damage + physics force
        var hits = Physics.OverlapSphere(center, explosionRadius);
        foreach (var col in hits)
        {
            // Player or any other health receiver
            col.GetComponent<PlayerHealth>()?.TakeDamage(explosionDamage);

            // Physics push
            var rb = col.attachedRigidbody;
            if (rb) rb.AddExplosionForce(explosionForce, center, explosionRadius, 0.5f, ForceMode.Impulse);

            // Optional friendly fire (damage other zombies too)
            var other = col.GetComponent<ZombieAI>();
            if (other && other != this) other.TakeDamage(explosionDamage);
        }

        // 5) Continue with the base death flow (animation trigger → event/delay → SpawnManager.Despawn)
        base.Die();
    }

    // ---------------- Helpers ----------------
    /// <summary>
    /// Robust center selection. Prefer renderer bounds or Hips for skinned meshes.
    /// </summary>
    Vector3 GetExplosionCenter()
    {
        switch (anchorMode)
        {
            case ExplosionAnchor.RendererCenter:
                var rends = GetComponentsInChildren<Renderer>();
                if (rends != null && rends.Length > 0)
                {
                    Bounds b = rends[0].bounds;
                    for (int i = 1; i < rends.Length; i++) b.Encapsulate(rends[i].bounds);
                    return b.center;
                }
                return transform.position;

            case ExplosionAnchor.HipsBone:
                var anim = GetComponent<Animator>();
                if (anim)
                {
                    var hips = anim.GetBoneTransform(HumanBodyBones.Hips);
                    if (hips) return hips.position;
                }
                return transform.position;

            case ExplosionAnchor.TransformPosition:
            default:
                return transform.position;
        }
    }

    /// <summary>
    /// Prefer reusing a disabled IN-SCENE VFX object; otherwise instantiate a prefab.
    /// Always forces particles/VFX Graph to play and triggers all embedded CM3 Impulse Sources.
    /// </summary>
    void PlayExplosionVfxAndImpulse(Vector3 center)
    {
        // 1) IN-SCENE reuse path
        if (explosionVfxSceneObject && explosionVfxSceneObject.scene.IsValid())
        {
            var vfx = explosionVfxSceneObject;
            vfx.transform.SetPositionAndRotation(center, Quaternion.identity);
            vfx.SetActive(true);

            ForcePlayAllEffects(vfx);
            //TriggerEmbeddedImpulses(vfx, center);

            if (sceneVfxAutoHideDelay > 0f)
                StartCoroutine(DisableAfterDelay(vfx, sceneVfxAutoHideDelay));
            return;
        }

        // 2) Prefab instantiate path
        if (explosionVfxPrefab)
        {
            var vfx = Instantiate(explosionVfxPrefab, center, Quaternion.identity);
            vfx.SetActive(true);

            ForcePlayAllEffects(vfx);
            //TriggerEmbeddedImpulses(vfx, center);

            if (instantiatedVfxLifetime > 0f)
                Destroy(vfx, instantiatedVfxLifetime);
            return;
        }

        Debug.LogWarning("[HeavyZombie] No explosion VFX assigned (scene object or prefab).");
    }

    /// <summary>
    /// Ensures all child ParticleSystems and VFX Graph components are playing.
    /// </summary>
    void ForcePlayAllEffects(GameObject root)
    {
        if (!root) return;

        // ParticleSystem
        var psList = root.GetComponentsInChildren<ParticleSystem>(true);
        foreach (var ps in psList)
        {
            if (!ps) continue;
            ps.gameObject.SetActive(true);
            ps.Clear(true);
            ps.Play(true);
        }

        //// VFX Graph
        //var vfxList = root.GetComponentsInChildren<VisualEffect>(true);
        //foreach (var ve in vfxList)
        //{
        //    if (!ve) continue;
        //    ve.gameObject.SetActive(true);
        //    if (!ve.alive) ve.Play();
        //}
    }

    /// <summary>
    /// Finds all CinemachineImpulseSource components under the VFX root and generates impulses.
    /// Works with Cinemachine 3. Ensure your camera has an Impulse Listener with matching channel.
    /// </summary>
    //void TriggerEmbeddedImpulses(GameObject root, Vector3 center)
    //{
    //    if (!root) return;

    //    var sources = root.GetComponentsInChildren<CinemachineImpulseSource>(true);
    //    if (sources == null || sources.Length == 0)
    //    {
    //        // Not fatal—just means your VFX has no impulse component.
    //        return;
    //    }

    //    foreach (var src in sources)
    //    {
    //        if (!src) continue;
    //        // Ensure the impulse origin matches the explosion center
    //        src.transform.position = center;
    //        src.GenerateImpulse(); // CM3: uses the assigned Impulse Definition
    //    }
    //}

    IEnumerator DisableAfterDelay(GameObject obj, float delay)
    {
        yield return new WaitForSeconds(delay);
        if (obj) obj.SetActive(false);
    }

    // ---------------- Gizmos ----------------
    void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.red;
        Gizmos.DrawWireSphere(GetExplosionCenter() + worldOffset, explosionRadius);
    }
}
