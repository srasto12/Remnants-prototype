using UnityEngine;
using UnityEngine.InputSystem;
using System.Collections;

public class GunController : MonoBehaviour
{
    [Header("Gun Settings")]
    public bool isAutomatic = true; 
    public int damage = 10;
    public float fireRate = 10f; 
    public float gunRange = 100f;
    public LayerMask shootableMask; 

    [Header("Ammo Settings")]
    public int maxAmmo = 30;        // Maximum bullets per mag
    public float reloadTime = 1.5f; // Time to reload in seconds
    private int currentAmmo;
    private bool isReloading = false;

    [Header("Effects")]
    public ParticleSystem muzzleFlash;
    public AudioSource gunSound;
    
    [Header("References")]
    public Animator playerAnimator; 

    private int shootTriggerHash = Animator.StringToHash("Shoot");
    private float nextFireTime = 0f;

    void Start()
    {
        currentAmmo = maxAmmo; // Start with full ammo
    }

    void OnEnable()
    {
        isReloading = false; // Reset reload status when switching weapons
    }

    void Update()
    {
        if (isReloading) return; // If reloading, we can't shoot

        // Auto-Reload if empty
        if (currentAmmo <= 0)
        {
            StartCoroutine(Reload());
            return;
        }

        // Manual Reload (Press R)
        if (Keyboard.current.rKey.wasPressedThisFrame && currentAmmo < maxAmmo)
        {
            StartCoroutine(Reload());
            return;
        }

        // Shooting Logic
        bool shootInput = isAutomatic ? Mouse.current.leftButton.isPressed : Mouse.current.leftButton.wasPressedThisFrame;

        if (shootInput && Time.time >= nextFireTime)
        {
            nextFireTime = Time.time + (1f / fireRate);
            Shoot();
        }
    }

    IEnumerator Reload()
    {
        isReloading = true;
        Debug.Log("Reloading...");

        // Wait for reload time
        yield return new WaitForSeconds(reloadTime);

        currentAmmo = maxAmmo;
        isReloading = false;
        Debug.Log("Reload Complete! Ammo: " + currentAmmo);
    }

    void Shoot()
    {
        currentAmmo--; // Reduce bullet count
        
        // Play Effects
        if (muzzleFlash != null) muzzleFlash.Play();
        if (gunSound != null) gunSound.Play();
        
        // Play Animation
        if (playerAnimator != null)
        {
            playerAnimator.SetTrigger(shootTriggerHash);
        }

        // Raycast Attack
        RaycastHit hit;
        Ray ray = Camera.main.ViewportPointToRay(new Vector3(0.5f, 0.5f, 0)); 

        if (Physics.Raycast(ray, out hit, gunRange, shootableMask)) 
        {
            if (hit.transform.CompareTag("Enemy"))
            {
                Boss1ZombieAI enemy = hit.transform.GetComponent<Boss1ZombieAI>();
                if (enemy != null)
                {
                    enemy.TakeDamage(damage);
                }
            }
        }
    }
}