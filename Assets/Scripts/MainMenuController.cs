using UnityEngine;
using UnityEngine.SceneManagement;
using System.Collections; 

public class MainMenuController : MonoBehaviour
{
    [Header("Scene Indices")]
    public int level1BuildIndex = 1; //ensure pehla level 1 marked h

    public void OnPlayPressed()
    {
        // Load level 1
        SceneManager.LoadScene(level1BuildIndex);
    }

    public void OnQuitPressed()
    {
        StartCoroutine(QuitAfterDelay());
    }
    private IEnumerator QuitAfterDelay()
    {
        yield return new WaitForSeconds(2f);  // delay here for the sfx
#if UNITY_EDITOR
            UnityEditor.EditorApplication.isPlaying = false;  // Stop play mode in the editor
#else
        Application.Quit(); 
#endif
    }
}
