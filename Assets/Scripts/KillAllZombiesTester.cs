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
        // Unity 2023+ preferred API
        ZombieAI[] zombies = Object.FindObjectsByType<ZombieAI>(FindObjectsSortMode.None);
        Boss1ZombieAI[] bosses = Object.FindObjectsByType<Boss1ZombieAI>(FindObjectsSortMode.None);
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
        Debug.Log($"[KillAllZombiesTester] Applied damage to {killed} zombies.");
    }

    [ContextMenu("Kill All Zombies (Editor)")]
    void ContextKillAll() => KillAll();
}
