using UnityEngine;
using UnityEngine.InputSystem;
using System.Collections;

public class MeleeController : MonoBehaviour
{
    [Header("Combat Settings")]
    public int damage = 25;
    public float attackRate = 1.5f; 
    public float range = 4.0f;      
    public float hitDelay = 0.5f;   
    
    [Header("References")]
    public Animator playerAnimator;

    private float nextAttack = 0f;
    private int animHash = Animator.StringToHash("MeleeAttack");

    void Update()
    {
        if (Mouse.current.leftButton.isPressed && Time.time >= nextAttack)
        {
            nextAttack = Time.time + (1f / attackRate);
            StartCoroutine(AttackSequence()); 
        }
    }

    IEnumerator AttackSequence()
    {
        if(playerAnimator != null) playerAnimator.SetTrigger(animHash);

        
        yield return new WaitForSeconds(hitDelay);

        
        CheckForHit();
    }

    void CheckForHit()
    {
        
        Transform cam = Camera.main.transform;
        Vector3 hitPosition = cam.position + (cam.forward * 2.0f);

        
        Debug.DrawRay(hitPosition, Vector3.up, Color.green, 2.0f);

        
        Collider[] hits = Physics.OverlapSphere(hitPosition, range); 

        Debug.Log("Delayed Swing Finished! Objects detected: " + hits.Length);

        foreach (var hit in hits)
        {
            
            if (hit.CompareTag("Enemy"))
            {
               
                Boss1ZombieAI zombie = hit.GetComponent<Boss1ZombieAI>();
                if (zombie == null) zombie = hit.GetComponentInParent<Boss1ZombieAI>();

                if (zombie != null)
                {
                    Debug.Log("HIT ZOMBIE AFTER DELAY!");
                    zombie.TakeDamage(damage); 
                }
            }
        }
    }

    
    void OnDrawGizmosSelected()
    {
        if (Camera.main != null)
        {
            Gizmos.color = Color.red;
            Transform cam = Camera.main.transform;
            Vector3 hitPosition = cam.position + (cam.forward * 2.0f);
            Gizmos.DrawWireSphere(hitPosition, range);
        }
    }
}