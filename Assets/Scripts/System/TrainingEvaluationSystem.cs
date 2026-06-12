using UnityEngine;
using System;
using System.Collections.Generic;
using System.Linq;

/// <summary>
/// OLIVIA VR - TrainingEvaluationSystem.cs
/// Sistem evaluasi komprehensif untuk VR training simulator HPAL.
/// Tracking: waktu, error, efisiensi, safety compliance, dan skill assessment per level.
/// </summary>
public class TrainingEvaluationSystem : MonoBehaviour
{
    public static TrainingEvaluationSystem Instance { get; private set; }

    #region Data Structures

    [Serializable]
    public class LevelPerformance
    {
        public GameLevelManager.GameLevel level;
        public string namaLevel;
        
        // Waktu
        public float waktuMulai;
        public float waktuSelesai;
        public float durasiDetik;
        public float targetWaktuOptimal;  // Benchmark untuk scoring
        
        // Scoring komponen
        public float skorWaktu;           // 0-30 poin (cepat = baik)
        public float skorAkurasi;         // 0-30 poin (minim error)
        public float skorSafety;          // 0-25 poin (APD + prosedur)
        public float skorEfisiensi;       // 0-15 poin (flow smooth, minim retry)
        public float skorTotal;           // 0-100 poin
        
        // Metrik detail
        public int jumlahError;           // Kesalahan operasional
        public int jumlahRetry;           // Ulangi task
        public int jumlahHint;            // Minta bantuan marker/hint
        public bool safetyViolation;      // APD tidak lengkap / skip prosedur
        public bool parameterAccurate;    // Parameter (pH/suhu/flow) di range target
        
        // Catatan spesifik
        public List<string> errorLog;
        public List<string> achievementLog;
        
        public LevelPerformance()
        {
            errorLog = new List<string>();
            achievementLog = new List<string>();
        }
    }

    [Serializable]
    public class SessionSummary
    {
        public string sessionID;
        public DateTime tanggalMulai;
        public float totalDurasiMenit;
        public float skorRataRata;
        public float skorTotal;
        public int levelDiselesaikan;
        public int totalError;
        public int totalRetry;
        public bool lulus;  // >= 70 skor rata-rata
        public string gradeOverall;  // A+ (90-100), A (80-89), B (70-79), C (<70)
        public List<LevelPerformance> performancePerLevel;

        public SessionSummary()
        {
            performancePerLevel = new List<LevelPerformance>();
        }
    }

    public enum ErrorType
    {
        APDIncomplete,          // APD tidak lengkap
        ParameterOutOfRange,    // pH/suhu/flow di luar target
        ProcedureSkipped,       // Skip langkah wajib
        UnsafeAction,           // Aksi tidak aman (misal buka valve tanpa konfirmasi)
        WrongSequence,          // Urutan salah
        TimeoutExceeded,        // Timeout task
        EquipmentDamage         // Merusak peralatan (edge case)
    }

    #endregion

    #region Fields

    [Header("=== Session Aktif ===")]
    [SerializeField] private SessionSummary _sessionAktif;
    [SerializeField] private LevelPerformance _levelAktif;
    
    [Header("=== Target Waktu Optimal (detik) ===")]
    [Tooltip("Benchmark waktu ideal per level untuk scoring. Kalau lebih cepat = bonus, lebih lambat = penalty.")]
    [SerializeField] private float[] _targetWaktuOptimal = new float[15]
    {
        60f,   // Level 0 Tutorial
        120f,  // Level 1 APD
        90f,   // Level 2 DCS Prep
        180f,  // Level 3 Ore Slurry
        150f,  // Level 4 Slurry Pump
        200f,  // Level 5 Steam Valve
        240f,  // Level 6 Acid Injection
        300f,  // Level 7 Autoclave
        360f,  // Level 8 Flash Train
        0f,    // Level 9 (retired)
        420f,  // Level 10 CCD
        480f,  // Level 11 MHP
        360f,  // Level 12 Tailing Filter
        300f,  // Level 13 Dry Stack
        180f   // Level 14 Emergency
    };

    [Header("=== Bobot Scoring ===")]
    [SerializeField] private float _bobotWaktu = 30f;
    [SerializeField] private float _bobotAkurasi = 30f;
    [SerializeField] private float _bobotSafety = 25f;
    [SerializeField] private float _bobotEfisiensi = 15f;

    [Header("=== Referensi ===")]
    private GameLevelManager _glm;
    private PhaseManager _phaseManager;

    private Dictionary<GameLevelManager.GameLevel, LevelPerformance> _performanceCache;

    #endregion

    #region Unity Lifecycle

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;

        _glm = FindFirstObjectByType<GameLevelManager>();
        _phaseManager = FindFirstObjectByType<PhaseManager>();
        _performanceCache = new Dictionary<GameLevelManager.GameLevel, LevelPerformance>();

        MulaiSessionBaru();
    }

    private void OnEnable()
    {
        GameLevelManager.OnLevelStarted += OnLevelMulai;
        GameLevelManager.OnLevelComplete += OnLevelSelesai;
    }

    private void OnDisable()
    {
        GameLevelManager.OnLevelStarted -= OnLevelMulai;
        GameLevelManager.OnLevelComplete -= OnLevelSelesai;
    }

    #endregion

    #region Public API

    /// <summary>
    /// Catat error operasional (misal APD tidak lengkap, parameter salah).
    /// </summary>
    public void CatatError(ErrorType tipe, string deskripsi)
    {
        if (_levelAktif == null) return;

        _levelAktif.jumlahError++;
        _levelAktif.errorLog.Add($"[{tipe}] {deskripsi}");

        if (tipe == ErrorType.APDIncomplete || tipe == ErrorType.UnsafeAction || tipe == ErrorType.ProcedureSkipped)
            _levelAktif.safetyViolation = true;

        Debug.LogWarning($"[EVAL] Error dicatat: {tipe} - {deskripsi}");
    }

    /// <summary>
    /// Catat retry (pemain ulangi task karena gagal).
    /// </summary>
    public void CatatRetry()
    {
        if (_levelAktif == null) return;
        _levelAktif.jumlahRetry++;
    }

    /// <summary>
    /// Catat hint (pemain lihat marker/hint bantuan).
    /// </summary>
    public void CatatHint()
    {
        if (_levelAktif == null) return;
        _levelAktif.jumlahHint++;
    }

    /// <summary>
    /// Catat achievement (milestone positif, misal "tidak ada error", "waktu di bawah optimal").
    /// </summary>
    public void CatatAchievement(string deskripsi)
    {
        if (_levelAktif == null) return;
        _levelAktif.achievementLog.Add(deskripsi);
        Debug.Log($"[EVAL] Achievement: {deskripsi}");
    }

    /// <summary>
    /// Validasi parameter operasional (pH, suhu, flow rate, dll).
    /// Return true jika dalam toleransi target.
    /// </summary>
    public bool ValidasiParameter(string namaParam, float nilaiAktual, float nilaiTarget, float toleransiPersen = 5f)
    {
        float lower = nilaiTarget * (1f - toleransiPersen / 100f);
        float upper = nilaiTarget * (1f + toleransiPersen / 100f);
        bool akurat = nilaiAktual >= lower && nilaiAktual <= upper;

        if (!akurat && _levelAktif != null)
        {
            CatatError(ErrorType.ParameterOutOfRange, 
                $"{namaParam}: {nilaiAktual:F1} (target {nilaiTarget:F1} ± {toleransiPersen}%)");
            _levelAktif.parameterAccurate = false;
        }
        else if (akurat && _levelAktif != null)
        {
            _levelAktif.parameterAccurate = true;
        }

        return akurat;
    }

    /// <summary>
    /// Dapatkan performance level tertentu (untuk UI/report).
    /// </summary>
    public LevelPerformance DapatkanPerformanceLevel(GameLevelManager.GameLevel level)
    {
        return _performanceCache.TryGetValue(level, out var perf) ? perf : null;
    }

    /// <summary>
    /// Dapatkan summary session lengkap (untuk end-of-training report).
    /// </summary>
    public SessionSummary DapatkanSessionSummary()
    {
        return _sessionAktif;
    }

    /// <summary>
    /// Export report ke JSON (untuk analytics/database).
    /// </summary>
    public string ExportReportJSON()
    {
        return JsonUtility.ToJson(_sessionAktif, true);
    }

    #endregion

    #region Private Methods

    private void MulaiSessionBaru()
    {
        _sessionAktif = new SessionSummary
        {
            sessionID = Guid.NewGuid().ToString(),
            tanggalMulai = DateTime.Now
        };
        _performanceCache.Clear();
        Debug.Log($"[EVAL] Session baru dimulai: {_sessionAktif.sessionID}");
    }

    private void OnLevelMulai(GameLevelManager.GameLevel level)
    {
        // Skip level retired
        if (level == GameLevelManager.GameLevel.Level9_FlashVessel)
            return;

        _levelAktif = new LevelPerformance
        {
            level = level,
            namaLevel = $"Level {(int)level}",
            waktuMulai = Time.time,
            targetWaktuOptimal = _targetWaktuOptimal[(int)level],
            parameterAccurate = true  // Default true, berubah jadi false saat ada error parameter
        };

        Debug.Log($"[EVAL] Level {level} dimulai. Target waktu: {_levelAktif.targetWaktuOptimal}s");
    }

    private void OnLevelSelesai(GameLevelManager.GameLevel level, int nomorTombol)
    {
        if (_levelAktif == null || _levelAktif.level != level)
            return;

        _levelAktif.waktuSelesai = Time.time;
        _levelAktif.durasiDetik = _levelAktif.waktuSelesai - _levelAktif.waktuMulai;

        // Hitung scoring
        HitungSkorLevel(_levelAktif);

        // Simpan ke cache & session
        _performanceCache[level] = _levelAktif;
        _sessionAktif.performancePerLevel.Add(_levelAktif);

        // Update summary session
        UpdateSessionSummary();

        // Log hasil
        Debug.Log($"[EVAL] Level {level} selesai. Durasi: {_levelAktif.durasiDetik:F1}s, Skor: {_levelAktif.skorTotal:F1}/100");

        _levelAktif = null;
    }

    private void HitungSkorLevel(LevelPerformance perf)
    {
        // 1. SKOR WAKTU (0-30 poin)
        // Rumus: 30 * (1 - (durasi - target) / target), capped 0-30
        // Kalau lebih cepat dari target = bonus sampai 30, kalau lebih lambat = penalty
        float rasioWaktu = perf.targetWaktuOptimal > 0 ? (perf.durasiDetik / perf.targetWaktuOptimal) : 1f;
        perf.skorWaktu = Mathf.Clamp(_bobotWaktu * (2f - rasioWaktu), 0f, _bobotWaktu);

        // Bonus achievement jika sangat cepat (< 80% target)
        if (rasioWaktu < 0.8f)
            perf.achievementLog.Add("⚡ Speedrun: Waktu di bawah 80% target!");

        // 2. SKOR AKURASI (0-30 poin)
        // Base: 30, penalty per error -3, per retry -2
        perf.skorAkurasi = _bobotAkurasi - (perf.jumlahError * 3f) - (perf.jumlahRetry * 2f);
        perf.skorAkurasi = Mathf.Max(perf.skorAkurasi, 0f);

        // Bonus jika tidak ada error sama sekali
        if (perf.jumlahError == 0)
            perf.achievementLog.Add("✓ Flawless Execution: Tidak ada error!");

        // 3. SKOR SAFETY (0-25 poin)
        // Base: 25, penalty besar jika ada safety violation (-15), penalty per hint -2
        perf.skorSafety = _bobotSafety;
        if (perf.safetyViolation)
            perf.skorSafety -= 15f;
        perf.skorSafety -= perf.jumlahHint * 2f;
        perf.skorSafety = Mathf.Max(perf.skorSafety, 0f);

        // 4. SKOR EFISIENSI (0-15 poin)
        // Parameter akurat = 10, tidak ada retry = 5
        perf.skorEfisiensi = 0f;
        if (perf.parameterAccurate)
            perf.skorEfisiensi += 10f;
        if (perf.jumlahRetry == 0)
            perf.skorEfisiensi += 5f;

        // Total
        perf.skorTotal = perf.skorWaktu + perf.skorAkurasi + perf.skorSafety + perf.skorEfisiensi;
        perf.skorTotal = Mathf.Clamp(perf.skorTotal, 0f, 100f);

        // Achievement untuk skor tinggi
        if (perf.skorTotal >= 95f)
            perf.achievementLog.Add("🏆 Perfect Score: Skor hampir sempurna!");
        else if (perf.skorTotal >= 85f)
            perf.achievementLog.Add("⭐ Excellent: Performa sangat baik!");
    }

    private void UpdateSessionSummary()
    {
        var summary = _sessionAktif;
        summary.levelDiselesaikan = summary.performancePerLevel.Count;
        summary.totalDurasiMenit = summary.performancePerLevel.Sum(p => p.durasiDetik) / 60f;
        summary.skorTotal = summary.performancePerLevel.Sum(p => p.skorTotal);
        summary.skorRataRata = summary.levelDiselesaikan > 0 ? summary.skorTotal / summary.levelDiselesaikan : 0f;
        summary.totalError = summary.performancePerLevel.Sum(p => p.jumlahError);
        summary.totalRetry = summary.performancePerLevel.Sum(p => p.jumlahRetry);

        // Grade
        summary.lulus = summary.skorRataRata >= 70f;
        if (summary.skorRataRata >= 90f)
            summary.gradeOverall = "A+";
        else if (summary.skorRataRata >= 80f)
            summary.gradeOverall = "A";
        else if (summary.skorRataRata >= 70f)
            summary.gradeOverall = "B";
        else
            summary.gradeOverall = "C";
    }

    #endregion

    #region Debug / Editor

    [ContextMenu("Debug: Print Session Summary")]
    private void DebugPrintSummary()
    {
        if (_sessionAktif == null)
        {
            Debug.LogWarning("[EVAL] Belum ada session aktif.");
            return;
        }

        Debug.Log($"=== SESSION SUMMARY ===\n" +
                  $"ID: {_sessionAktif.sessionID}\n" +
                  $"Tanggal: {_sessionAktif.tanggalMulai}\n" +
                  $"Level Diselesaikan: {_sessionAktif.levelDiselesaikan}\n" +
                  $"Durasi Total: {_sessionAktif.totalDurasiMenit:F1} menit\n" +
                  $"Skor Total: {_sessionAktif.skorTotal:F1}\n" +
                  $"Skor Rata-rata: {_sessionAktif.skorRataRata:F1}/100\n" +
                  $"Grade: {_sessionAktif.gradeOverall}\n" +
                  $"Lulus: {(_sessionAktif.lulus ? "YA" : "TIDAK")}\n" +
                  $"Total Error: {_sessionAktif.totalError}\n" +
                  $"Total Retry: {_sessionAktif.totalRetry}");
    }

    [ContextMenu("Debug: Export JSON")]
    private void DebugExportJSON()
    {
        string json = ExportReportJSON();
        Debug.Log($"[EVAL] JSON Report:\n{json}");
    }

    #endregion
}
