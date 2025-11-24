using UnityEngine;
using UnityEngine.SceneManagement;
using System.Collections.Generic;

public class KillAllZombiesTester : MonoBehaviour
{
    public KeyCode key = KeyCode.E;
    public int damageAmount = 99999;

    void Update()
    {
        if (Input.GetKeyDown(key))
            KillAll();
    }

    public void KillAll()
    {
        ZombieAI[] zombies = Object.FindObjectsByType<ZombieAI>(FindObjectsSortMode.None);
        Boss1ZombieAI[] bosses = Object.FindObjectsByType<Boss1ZombieAI>(FindObjectsSortMode.None);
        FlyingChaser[] boss2s = Object.FindObjectsByType<FlyingChaser>(FindObjectsSortMode.None);

        int killed = 0;

        foreach (var z in zombies)
        {
            if (z == null) continue;
            z.TakeDamage(damageAmount);
            killed++;
        }

        foreach (var z in bosses)
        {
            if (z == null) continue;
            z.TakeDamage(damageAmount);
            killed++;
        }

        foreach (var b in boss2s)
        {
            if (b == null) continue;

            var hp = b.GetComponent<MonsterHealth>();
            if (hp != null)
            {
                hp.TakeDamage(damageAmount, this.gameObject);
            }
            else
            {
                b.Die();
            }
            killed++;
        }

        Debug.Log($"[KillAllZombiesTester] Applied damage to {killed} zombies (incl. bosses).");
    }


    [ContextMenu("Kill All Zombies (Editor)")]
    void ContextKillAll() => KillAll();
}
