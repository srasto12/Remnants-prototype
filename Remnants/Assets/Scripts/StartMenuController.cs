using UnityEngine;
using UnityEngine.SceneManagement;

public class StartMenuController : MonoBehaviour
{
    public void OnStartClick()
    {
        SceneManager.LoadScene("MainScene");
    }

    public void OnExitClick()
    {
#if UNITY_EDITOR
            // just to stop the game if it is not built yet
#else
        // Quit the application when built and deployed
        Application.Quit();
#endif
    }
}
