using UnityEngine;
using UnityEngine.SceneManagement;

public class AudioManager : MonoBehaviour
{
    private AudioSource musicSource;

    [Header("Scene Music Clips")]
    public AudioClip mainMenuMusic;
    public AudioClip level1Music;
    public AudioClip otherLevelMusic;

    [Range(0f, 1f)] public float musicVolume = 0.6f;

    void Awake()
    {
        // Prevent duplicates
        AudioManager existing = FindFirstObjectByType<AudioManager>();
        if (existing != null && existing != this)
        {
            Destroy(gameObject);
            return;
        }

        DontDestroyOnLoad(gameObject);

        musicSource = gameObject.AddComponent<AudioSource>();
        musicSource.loop = true;
        musicSource.volume = musicVolume;

        // Start correct music for the current scene
        PlayMusicForScene(SceneManager.GetActiveScene().name);

        // Change track whenever a new scene loads
        SceneManager.sceneLoaded += OnSceneLoaded;
    }

    private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        PlayMusicForScene(scene.name);
    }

    private void PlayMusicForScene(string sceneName)
    {
        AudioClip newClip = null;

        // Main menu has its own track
        if (sceneName == "MainMenu")
            newClip = mainMenuMusic;

        // Level1 and ShopScene share the same track
        else if (sceneName == "Level1" || sceneName == "ShopScene")
            newClip = level1Music;

        // Optional: other levels
        else
            newClip = otherLevelMusic;

        // If the same music is already playing, do nothing
        if (newClip == musicSource.clip)
            return;

        if (newClip == null)
        {
            musicSource.Stop();
        }
        else
        {
            musicSource.clip = newClip;
            musicSource.Play();
        }
    }
}
