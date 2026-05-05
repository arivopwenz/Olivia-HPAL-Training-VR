using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// Singleton GameManager — mengatur state game dan perpindahan antar fase.
/// Persist antar scene (DontDestroyOnLoad).
/// </summary>
public class GameManager : MonoBehaviour
{
    public static GameManager Instance { get; private set; }

    // ========== GAME STATE ==========
    public enum GamePhase
    {
        Tutorial,       // Fase 0: Onboarding
        ControlRoom,    // Fase 1: Ruang Kontrol DCS
        APDCheck,       // Fase 1.5: Pakai APD
        PlantFloor,     // Fase 2: Lantai Pabrik + X-Ray
        Emergency,      // Fase 3: Skenario Darurat
        Result          // Hasil: Skor & Sertifikat
    }

    [Header("Current State")]
    [SerializeField] private GamePhase currentPhase = GamePhase.Tutorial;
    public GamePhase CurrentPhase => currentPhase;

    // ========== SCORE DATA ==========
    [Header("Score Tracking")]
    public float emergencyResponseTime = 0f;   // Waktu tanggap darurat (detik)
    public float valveAccuracy = 0f;            // Akurasi putar katup (0-100%)
    public float k3Compliance = 0f;             // Kepatuhan K3 (0-100%)
    public float scaleInspectionScore = 0f;     // Skor inspeksi kerak (0-100%)
    public bool isEmergencySuccess = false;     // Berhasil atau gagal?

    // ========== SCENE NAMES ==========
    [Header("Scene Configuration")]
    public string tutorialScene = "TutorialScene";
    public string controlRoomScene = "ControlRoomScene";
    public string plantFloorScene = "PlantFloorScene";
    public string emergencyScene = "EmergencyScene";
    public string resultScene = "ResultScene";

    // ========== EVENTS ==========
    public System.Action<GamePhase> OnPhaseChanged;

    private void Awake()
    {
        // Singleton pattern
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;
        DontDestroyOnLoad(gameObject);
    }

    /// <summary>
    /// Pindah ke fase berikutnya secara berurutan.
    /// </summary>
    public void GoToNextPhase()
    {
        switch (currentPhase)
        {
            case GamePhase.Tutorial:
                SetPhase(GamePhase.ControlRoom);
                break;
            case GamePhase.ControlRoom:
                SetPhase(GamePhase.APDCheck);
                break;
            case GamePhase.APDCheck:
                SetPhase(GamePhase.PlantFloor);
                break;
            case GamePhase.PlantFloor:
                SetPhase(GamePhase.Emergency);
                break;
            case GamePhase.Emergency:
                SetPhase(GamePhase.Result);
                break;
            case GamePhase.Result:
                // Restart ke tutorial
                ResetGame();
                SetPhase(GamePhase.Tutorial);
                break;
        }
    }

    /// <summary>
    /// Set fase tertentu dan load scene yang sesuai.
    /// </summary>
    public void SetPhase(GamePhase newPhase)
    {
        currentPhase = newPhase;
        OnPhaseChanged?.Invoke(currentPhase);
        Debug.Log($"[GameManager] Phase changed to: {currentPhase}");

        // Load scene sesuai fase
        string sceneName = GetSceneForPhase(newPhase);
        if (!string.IsNullOrEmpty(sceneName))
        {
            SceneLoader.LoadScene(sceneName);
        }
    }

    /// <summary>
    /// Mapping fase ke nama scene.
    /// APDCheck ada di dalam PlantFloorScene (bukan scene terpisah).
    /// </summary>
    private string GetSceneForPhase(GamePhase phase)
    {
        switch (phase)
        {
            case GamePhase.Tutorial:    return tutorialScene;
            case GamePhase.ControlRoom: return controlRoomScene;
            case GamePhase.APDCheck:    return plantFloorScene; // APD check di scene yang sama
            case GamePhase.PlantFloor:  return null; // Sudah di scene ini
            case GamePhase.Emergency:   return emergencyScene;
            case GamePhase.Result:      return resultScene;
            default: return null;
        }
    }

    /// <summary>
    /// Reset semua skor untuk mulai ulang.
    /// </summary>
    public void ResetGame()
    {
        emergencyResponseTime = 0f;
        valveAccuracy = 0f;
        k3Compliance = 0f;
        scaleInspectionScore = 0f;
        isEmergencySuccess = false;
        Debug.Log("[GameManager] Game reset!");
    }

    /// <summary>
    /// Hitung total skor (0-100).
    /// </summary>
    public float CalculateTotalScore()
    {
        // Waktu tanggap: max 45 detik, semakin cepat semakin bagus
        float timeScore = Mathf.Clamp01(1f - (emergencyResponseTime / 45f)) * 100f;

        float totalScore = (timeScore * 0.30f) +
                          (valveAccuracy * 0.25f) +
                          (k3Compliance * 0.25f) +
                          (scaleInspectionScore * 0.20f);

        return Mathf.Round(totalScore);
    }

    /// <summary>
    /// Dapatkan grade berdasarkan skor.
    /// </summary>
    public string GetGrade(float score)
    {
        if (score >= 95) return "A+";
        if (score >= 90) return "A";
        if (score >= 85) return "A-";
        if (score >= 80) return "B+";
        if (score >= 75) return "B";
        if (score >= 70) return "B-";
        if (score >= 65) return "C+";
        if (score >= 60) return "C";
        return "D";
    }
}
