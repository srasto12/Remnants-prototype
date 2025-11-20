using UnityEngine;
using UnityEngine.SceneManagement;

public class TeleportToNextLevel : MonoBehaviour
{
    [Header("Scene Indices")]
    public int nextLevelBuildIndex = 2;  

    private void OnTriggerEnter(Collider other)
    {
        if (other.CompareTag("Player"))  
        {
            TeleportPlayerToNextLevel();
        }
    }

    private void TeleportPlayerToNextLevel()
    {
        // Load the next level
        SceneManager.LoadScene(nextLevelBuildIndex);
    }
}
