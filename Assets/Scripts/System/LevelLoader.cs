using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// OLIVIA VR - LevelLoader.cs
/// 
/// Jembatan antara GameLevelManager (logika game) dan 
/// LoadingScreenManager (tampilan loading).
/// 
/// Cara Pakai:
///   - Pasang skrip ini ke GameObject "GameManager" di setiap scene.
///   - Pastikan nama scene di Build Settings SAMA PERSIS dengan
///     yang ada di array _sceneNames di bawah.
/// 
/// Urutan nama scene yang WAJIB kamu daftarkan di Build Settings:
///   0: Level_0_Tutorial
///   1: Level_1_APD
///   2: Level_2_DCSPrep
///   3: Level_3_OreSlurry
///   4: Level_4_SlurryPump
///   5: Level_5_SteamValve
///   6: Level_6_AcidInjection
///   7: Level_7_Autoclave
///   8: Level_8_Monitoring
///   9: Level_9_FlashVessel
///  10: Level_10_CCD
///  11: Level_11_MHP
///  12: Level_12_TailingDischarge
///  13: Level_13_TailingWaste
///  14: Level_14_Emergency
/// </summary>
public class LevelLoader : MonoBehaviour
{
    // ============================================================
    //  SINGLETON
    // ============================================================
    public static LevelLoader Instance { get; private set; }

    // ============================================================
    //  NAMA SCENE PER LEVEL
    //  Wajib sama persis dengan nama di Build Settings Unity!
    // ============================================================
    [Header("=== Daftar Nama Scene ===")]
    [Tooltip("Nama scene harus sama persis dengan yang ada di File > Build Settings")]
    public string[] namaScene = {
        "Level_0_Tutorial",
        "Level_1_APD",
        "Level_2_DCSPrep",
        "Level_3_OreSlurry",
        "Level_4_SlurryPump",
        "Level_5_SteamValve",
        "Level_6_AcidInjection",
        "Level_7_Autoclave",
        "Level_8_Monitoring",
        "Level_9_FlashVessel",
        "Level_10_CCD",
        "Level_11_MHP",
        "Level_12_TailingDischarge",
        "Level_13_TailingWaste",
        "Level_14_Emergency"
    };

    [Header("=== Pengaturan ===")]
    [Tooltip("Aktifkan ini hanya saat testing — skip loading screen")]
    public bool skipLoadingScreen = false;

    // ============================================================
    //  INTERNAL
    // ============================================================
    private int _currentLevelIndex = 0;

    // ============================================================
    //  LIFECYCLE
    // ============================================================
    void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;
        DontDestroyOnLoad(gameObject);
    }

    void OnEnable()
    {
        // CATATAN: Game ini single-scene (semua level dalam Level1.unity).
        // Subscription ke OnLevelStarted SENGAJA dilucuti — kalau ikut subscribe,
        // LevelLoader akan coba load scene "Level_2_DCSPrep" dst. yang TIDAK ADA,
        // dan menyebabkan tabrakan teleport / posisi reset saat level berubah.
        //
        // Aktifkan kembali HANYA jika project di-migrate ke multi-scene
        // (tiap level punya .unity sendiri di Build Settings).
        // GameLevelManager.OnLevelStarted += OnGameLevelBerubah;
    }

    void OnDisable()
    {
        // GameLevelManager.OnLevelStarted -= OnGameLevelBerubah;
    }

    // ============================================================
    //  EVENT HANDLER
    // ============================================================
    private void OnGameLevelBerubah(GameLevelManager.GameLevel level)
    {
        int levelInt = (int)level;
        LoadLevel(levelInt);
    }

    // ============================================================
    //  API PUBLIK
    // ============================================================

    /// <summary>
    /// Muat level berdasarkan indeks angka.
    /// Dipanggil otomatis dari event GameLevelManager.
    /// </summary>
    public void LoadLevel(int levelIndex)
    {
        if (levelIndex < 0 || levelIndex >= namaScene.Length)
        {
            Debug.LogError($"[LevelLoader] Indeks level {levelIndex} tidak valid! Max: {namaScene.Length - 1}");
            return;
        }

        _currentLevelIndex = levelIndex;
        string targetScene = namaScene[levelIndex];

        // Cek apakah scene terdaftar di Build Settings
        if (!IsSceneTerdaftar(targetScene))
        {
            Debug.LogError($"[LevelLoader] Scene '{targetScene}' TIDAK ditemukan di Build Settings! " +
                           $"Pergi ke File > Build Settings dan tambahkan scene tersebut.");
            return;
        }

        Debug.Log($"[LevelLoader] Memuat Level {levelIndex}: {targetScene}");

        if (skipLoadingScreen)
        {
            // Mode debug: langsung loncat tanpa loading screen
            SceneManager.LoadScene(targetScene);
        }
        else
        {
            // Mode normal: pakai loading screen
            if (LoadingScreenManager.Instance != null)
                LoadingScreenManager.Instance.LoadLevel(levelIndex, targetScene);
            else
                Debug.LogError("[LevelLoader] LoadingScreenManager tidak ditemukan! Pastikan ada di scene.");
        }
    }

    /// <summary>
    /// Muat ulang level saat ini (misal: saat pemain gagal).
    /// </summary>
    public void ReloadLevel()
    {
        LoadLevel(_currentLevelIndex);
    }

    /// <summary>
    /// Langsung loncat ke level tertentu (untuk testing/cheat).
    /// </summary>
    [ContextMenu("DEBUG: Loncat ke Level Berikutnya")]
    public void DebugNextLevel()
    {
        LoadLevel(_currentLevelIndex + 1);
    }

    // ============================================================
    //  HELPER: Validasi Scene di Build Settings
    // ============================================================
    private bool IsSceneTerdaftar(string sceneName)
    {
        for (int i = 0; i < SceneManager.sceneCountInBuildSettings; i++)
        {
            string path = SceneUtility.GetScenePathByBuildIndex(i);
            // Ambil nama file tanpa path dan ekstensi
            string namaFile = System.IO.Path.GetFileNameWithoutExtension(path);
            if (namaFile == sceneName) return true;
        }
        return false;
    }

    // ============================================================
    //  PROPERTIES
    // ============================================================
    public int CurrentLevelIndex => _currentLevelIndex;
    public string CurrentSceneName => namaScene[_currentLevelIndex];
}
