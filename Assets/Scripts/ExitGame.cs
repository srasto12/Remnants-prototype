using UnityEngine;
#if UNITY_EDITOR
using UnityEditor;   // 只有在 Editor 才能用
#endif

public class ExitGame : MonoBehaviour
{
    void Update()
    {
        if (Input.GetKeyDown(KeyCode.Escape))
        {
#if UNITY_EDITOR
            EditorApplication.isPlaying = false; // 在 Editor 停止遊戲
#else
            Application.Quit(); // 在 Build 出來的遊戲中結束程式
#endif
        }
    }
}
