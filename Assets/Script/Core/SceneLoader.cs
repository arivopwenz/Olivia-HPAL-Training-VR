using UnityEngine;
using UnityEngine.SceneManagement;
using System.Collections;

/// <summary>
/// SceneLoader — handle transisi antar scene dengan loading screen.
/// Bisa dipanggil secara statis dari mana saja.
/// </summary>
public class SceneLoader : MonoBehaviour
{
    public static SceneLoader Instance { get; private set; }

    [Header("Transition Settings")]
    [SerializeField] private float fadeOutDuration = 0.5f;
    [SerializeField] private float fadeInDuration = 0.5f;

    // Event yang bisa di-subscribe untuk fade effect
    public static System.Action OnFadeOutStart;
    public static System.Action OnFadeOutComplete;
    public static System.Action OnFadeInStart;
    public static System.Action OnFadeInComplete;

    private bool isLoading = false;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;
        DontDestroyOnLoad(gameObject);
    }

    /// <summary>
    /// Load scene by name (static shortcut).
    /// </summary>
    public static void LoadScene(string sceneName)
    {
        if (Instance != null)
        {
            Instance.StartLoadScene(sceneName);
        }
        else
        {
            // Fallback jika Instance belum ada
            Debug.LogWarning("[SceneLoader] Instance not found, loading directly.");
            SceneManager.LoadScene(sceneName);
        }
    }

    /// <summary>
    /// Load scene dengan transisi fade.
    /// </summary>
    public void StartLoadScene(string sceneName)
    {
        if (isLoading)
        {
            Debug.LogWarning("[SceneLoader] Already loading a scene!");
            return;
        }
        StartCoroutine(LoadSceneRoutine(sceneName));
    }

    private IEnumerator LoadSceneRoutine(string sceneName)
    {
        isLoading = true;

        // Fade out
        OnFadeOutStart?.Invoke();
        Debug.Log($"[SceneLoader] Fading out...");
        yield return new WaitForSeconds(fadeOutDuration);
        OnFadeOutComplete?.Invoke();

        // Load scene async
        Debug.Log($"[SceneLoader] Loading scene: {sceneName}");
        AsyncOperation asyncLoad = SceneManager.LoadSceneAsync(sceneName);
        asyncLoad.allowSceneActivation = false;

        // Tunggu sampai 90% (Unity standard)
        while (asyncLoad.progress < 0.9f)
        {
            yield return null;
        }

        // Aktivasi scene
        asyncLoad.allowSceneActivation = true;
        yield return null; // Tunggu 1 frame setelah scene aktif

        // Fade in
        OnFadeInStart?.Invoke();
        Debug.Log($"[SceneLoader] Fading in...");
        yield return new WaitForSeconds(fadeInDuration);
        OnFadeInComplete?.Invoke();

        isLoading = false;
        Debug.Log($"[SceneLoader] Scene '{sceneName}' loaded successfully!");
    }

    /// <summary>
    /// Reload scene yang sedang aktif.
    /// </summary>
    public static void ReloadCurrentScene()
    {
        LoadScene(SceneManager.GetActiveScene().name);
    }
}
