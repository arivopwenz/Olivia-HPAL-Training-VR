using UnityEngine;

/// <summary>
/// OLIVIA VR - DcsMonitorActivator.cs
///
/// Mengatur visibility DCS monitor canvas berdasarkan level:
///   - Level 1 (loker): monitor MATI
///   - Level 2 (DCS Prep): monitor MATI di awal — player harus nyalain manual via Tombol DCS 2
///   - Level 3+ (sampai 14): monitor OTOMATIS NYALA setiap level start
///
/// Pemakaian: pasang di GameObject yang kontrol monitor (bisa parent canvas).
/// Set monitorRoot ke DCS_Monitor_Canvas (atau GameObject root yang kamu mau toggle).
/// </summary>
public class DcsMonitorActivator : MonoBehaviour
{
    [Header("=== Referensi ===")]
    [SerializeField] private GameObject monitorRoot;

    [Header("=== Konfigurasi ===")]
    [Tooltip("Tombol DCS untuk power on (default 2 = Level 2).")]
    [SerializeField] private int tombolPower = 2;

    [Tooltip("Level yang mengharuskan player nyalain monitor manual via tombol power.")]
    [SerializeField] private GameLevelManager.GameLevel manualPowerOnLevel = GameLevelManager.GameLevel.Level2_DCSPrep;

    [Tooltip("Level di mana monitor harus mati saat scene load / level start (default Level 1 dan 2).")]
    [SerializeField] private GameLevelManager.GameLevel[] hiddenAtLevels = new[]
    {
        GameLevelManager.GameLevel.Level0_Tutorial,
        GameLevelManager.GameLevel.Level1_APD,
        GameLevelManager.GameLevel.Level2_DCSPrep
    };

    private bool _sudahDinyalakanManual;

    private void Awake()
    {
        if (monitorRoot != null)
            monitorRoot.SetActive(false);
    }

    private void OnEnable()
    {
        GameLevelManager.OnDCSButtonPressed += OnDcsPressed;
        GameLevelManager.OnLevelStarted += OnLevelStarted;
    }

    private void OnDisable()
    {
        GameLevelManager.OnDCSButtonPressed -= OnDcsPressed;
        GameLevelManager.OnLevelStarted -= OnLevelStarted;
    }

    private void OnLevelStarted(GameLevelManager.GameLevel level)
    {
        if (monitorRoot == null) return;

        bool harusMati = false;
        for (int i = 0; i < hiddenAtLevels.Length; i++)
        {
            if (hiddenAtLevels[i] == level) { harusMati = true; break; }
        }

        if (harusMati)
        {
            // Level 1/2: monitor mati. Reset flag manual supaya kalau player mundur ke Level 2 lagi
            // (mis. via debug menu), monitor mati lagi sampai dinyalain manual.
            monitorRoot.SetActive(false);
            if (level == manualPowerOnLevel)
                _sudahDinyalakanManual = false;
        }
        else
        {
            // Level 3+: monitor otomatis nyala (asumsinya sudah pernah dinyalakan di Level 2).
            monitorRoot.SetActive(true);
            _sudahDinyalakanManual = true;
        }
    }

    private void OnDcsPressed(int nomorTombol)
    {
        if (nomorTombol != tombolPower) return;
        if (monitorRoot == null) return;

        // Tombol power: nyalakan monitor saat di level yang membutuhkan manual power-on
        if (GameLevelManager.Instance != null &&
            GameLevelManager.Instance.CurrentLevel == manualPowerOnLevel)
        {
            monitorRoot.SetActive(true);
            _sudahDinyalakanManual = true;
        }
    }
}
