
using UnityEngine;
using UnityEngine.SceneManagement;
using static System.Net.Mime.MediaTypeNames;

public class MainMenuController : MonoBehaviour
{
    [Header("Scene Indices")]
    public int level1BuildIndex = 1; // Level1 is build index 1

    public void OnPlayPressed()
    {
        // Load level 1
        SceneManager.LoadScene(level1BuildIndex);
    }

    public void OnQuitPressed()
    {
#if UNITY_EDITOR
        UnityEditor.EditorApplication.isPlaying = false;
#else
        Application.Quit();
#endif
    }
}
