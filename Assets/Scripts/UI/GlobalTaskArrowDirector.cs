using UnityEngine;

/// <summary>
/// OLIVIA VR - GlobalTaskArrowDirector.cs
///
/// Menampilkan arrow indicator (panduan 3D) untuk task aktif di Level 2+.
/// Bekerja barengan dengan TaskArrowDirector (yang khusus highlight tombol DCS) — director ini
/// fokus ke task non-DCS seperti walkie talkie, sampling tank, valve, dll.
///
/// Strategi:
///   - Saat OnLevelStarted, baca state level aktif lewat GameLevelManager + PhaseManager.
///   - Tampilkan arrow ke target yang relevan (walkie talkie kalau lapor HT belum dilakukan,
///     atau target lain berdasarkan flag spesifik level).
///   - Hide arrow setelah task target selesai.
///
/// Pemakaian:
///   1. Drag script ini ke GameObject baru di scene (mis. "TaskHint_GlobalArrowDirector_All").
///   2. Biarkan field _arrow kosong → script auto-create child arrow (clone dari TaskHint_Arrow3D).
///   3. Pastikan WalkieTalkieManager._walkieTalkieInHand sudah di-assign supaya target ketemu.
/// </summary>
[DisallowMultipleComponent]
public sealed class GlobalTaskArrowDirector : MonoBehaviour
{
    [Header("=== Arrow Indicator ===")]
    [Tooltip("DirectionArrowIndicator yang dipakai. Kalau kosong, akan dibuat otomatis sebagai child.")]
    [SerializeField] private DirectionArrowIndicator _arrow;
    [Tooltip("Nama GameObject anak yang dipakai saat auto-create arrow.")]
    [SerializeField] private string _arrowChildName = "TaskHint_GlobalArrow_FieldTask";

    [Header("=== Polling ===")]
    [Tooltip("Berapa detik sekali state level di-cek ulang. Lebih kecil = arrow lebih responsif tapi sedikit lebih CPU.")]
    [Range(0.05f, 1f)]
    [SerializeField] private float _intervalUpdate = 0.25f;

    [Header("=== Targets (Auto-resolve kalau kosong) ===")]
    [Tooltip("Walkie Talkie fisik yang ada di pinggang/tangan player. Kalau kosong → cari WalkieTalkieManager._walkieTalkieInHand.")]
    [SerializeField] private Transform _walkieTalkieTarget;
    [Tooltip("Tank slurry untuk Level 3 saat menunggu cairan 50%.")]
    [SerializeField] private Transform _slurryTankTarget;
    [Tooltip("Sample bottle MHP untuk Level 11.")]
    [SerializeField] private Transform _mhpSampleTarget;
    [Tooltip("Tombol ESD untuk Level 14.")]
    [SerializeField] private Transform _esdButtonTarget;

    private GameLevelManager _glm;
    private PhaseManager _pm;
    private WalkieTalkieManager _wtm;
    private float _nextUpdateTime;
    private bool _arrowActive;
    private Transform _currentTarget;

    private void Awake()
    {
        EnsureArrow();
        if (_arrow != null) _arrow.Hide();
    }

    private void OnEnable()
    {
        GameLevelManager.OnLevelStarted += OnLevelStarted;
        GameLevelManager.OnLevelComplete += OnLevelComplete;
        GameLevelManager.OnVoiceReportAccepted += OnVoiceReportAccepted;
    }

    private void OnDisable()
    {
        GameLevelManager.OnLevelStarted -= OnLevelStarted;
        GameLevelManager.OnLevelComplete -= OnLevelComplete;
        GameLevelManager.OnVoiceReportAccepted -= OnVoiceReportAccepted;
    }

    private void Update()
    {
        if (Time.time < _nextUpdateTime) return;
        _nextUpdateTime = Time.time + _intervalUpdate;

        EnsureRefs();
        EvaluateTask();
    }

    private void OnLevelStarted(GameLevelManager.GameLevel level)
    {
        // Reset target tiap awal level supaya tidak nyambung dari level sebelumnya.
        HideArrow();
        _nextUpdateTime = Time.time + 0.5f; // beri jeda 0.5s biar level data settle dulu.
    }

    private void OnLevelComplete(GameLevelManager.GameLevel level, int skor)
    {
        HideArrow();
    }

    private void OnVoiceReportAccepted(string keyword)
    {
        // Saat laporan HT diterima, hide arrow walkie talkie sampai task berikutnya muncul.
        HideArrow();
    }

    private void EvaluateTask()
    {
        if (_glm == null)
        {
            HideArrow();
            return;
        }

        var level = _glm.CurrentLevel;

        // Level 0 + 1 di-handle oleh director khusus (Level1ApdTaskHintDirector & TaskArrowDirector).
        if (level <= GameLevelManager.GameLevel.Level1_APD)
        {
            HideArrow();
            return;
        }

        Transform target = ResolveTaskTarget(level);
        if (target == null)
        {
            HideArrow();
            return;
        }

        // Show / update target.
        if (target != _currentTarget)
        {
            _currentTarget = target;
            EnsureArrow();
            if (_arrow != null)
            {
                _arrow.Show(target);
                _arrowActive = true;
            }
        }
        else if (!_arrowActive && _arrow != null)
        {
            _arrow.Show(target);
            _arrowActive = true;
        }
    }

    /// <summary>
    /// Tentukan target arrow berdasarkan state level aktif.
    /// Return null kalau tidak ada task aktif yang butuh panduan.
    /// </summary>
    private Transform ResolveTaskTarget(GameLevelManager.GameLevel level)
    {
        if (_glm == null) return null;

        // Sekuensing umum: kalau tombol DCS untuk level ini belum ditekan, biarkan TaskArrowDirector
        // (yang highlight DCS button) yang handle. Director ini tidak menggambar target lain.
        bool tombolDcsDibutuhkan = _glm.NomorTombolDcsLevelIni > 0;
        if (tombolDcsDibutuhkan && !_glm.SudahTekanTombolDcs)
            return null;

        // Default fallback: target = walkie talkie kalau laporan HT belum dilakukan.
        // Per-level kita override sesuai sub-state.
        switch (level)
        {
            case GameLevelManager.GameLevel.Level2_DCSPrep:
                if (!_glm.SudahLihatDcs) return null;            // tunggu lihat DCS dulu
                if (!_glm.SudahTekanTombolDcs) return null;      // TaskArrowDirector handle
                if (!_glm.SudahLaporanHt) return GetWalkieTalkieTarget();
                return null;

            case GameLevelManager.GameLevel.Level3_OreSlurry:
                // Level 3 sub-phase logic: arrow ke walkie talkie kalau lagi nunggu lapor.
                return GetWalkieTalkieTarget();

            case GameLevelManager.GameLevel.Level4_SlurryPump:
                return GetWalkieTalkieTarget();

            case GameLevelManager.GameLevel.Level5_SteamValve:
            case GameLevelManager.GameLevel.Level6_AcidInjection:
            case GameLevelManager.GameLevel.Level7_Autoclave:
            case GameLevelManager.GameLevel.Level8_Monitoring:
            case GameLevelManager.GameLevel.Level9_FlashVessel:
            case GameLevelManager.GameLevel.Level10_CCD:
            case GameLevelManager.GameLevel.Level12_TailingDischarge:
            case GameLevelManager.GameLevel.Level13_TailingWaste:
                if (!_glm.SudahLaporanHt) return GetWalkieTalkieTarget();
                return null;

            case GameLevelManager.GameLevel.Level11_MHP:
                if (_mhpSampleTarget != null) return _mhpSampleTarget;
                if (!_glm.SudahLaporanHt) return GetWalkieTalkieTarget();
                return null;

            case GameLevelManager.GameLevel.Level14_Emergency:
                if (_esdButtonTarget != null) return _esdButtonTarget;
                return GetWalkieTalkieTarget();

            default:
                return null;
        }
    }

    private Transform GetWalkieTalkieTarget()
    {
        if (_walkieTalkieTarget != null && _walkieTalkieTarget.gameObject.activeInHierarchy)
            return _walkieTalkieTarget;

        if (_wtm != null)
        {
            var t = _wtm.WalkieTalkieInHandTransform;
            if (t != null && t.gameObject.activeInHierarchy)
                return t;
        }

        // Fallback by name.
        GameObject go = GameObject.Find("WalkieTalkie") ?? GameObject.Find("Walkie_Talkie") ?? GameObject.Find("WT_Body");
        if (go != null)
        {
            _walkieTalkieTarget = go.transform;
            return _walkieTalkieTarget;
        }

        return null;
    }

    private void EnsureRefs()
    {
        if (_glm == null) _glm = GameLevelManager.Instance;
        if (_pm == null) _pm = PhaseManager.Instance;
        if (_wtm == null) _wtm = FindFirstObjectByType<WalkieTalkieManager>(FindObjectsInactive.Include);
    }

    private void EnsureArrow()
    {
        if (_arrow != null) return;

        // Cari arrow indicator yang sudah ada di scene tapi belum dipakai TaskArrowDirector.
        var existing = FindObjectsByType<DirectionArrowIndicator>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        foreach (var d in existing)
        {
            if (d == null) continue;
            // Skip kalau sudah dipasang ke TaskArrowDirector lain.
            if (d.transform.parent != null && d.transform.parent.GetComponent<TaskArrowDirector>() != null) continue;
            _arrow = d;
            return;
        }

        // Belum ada → buat baru sebagai child diri sendiri.
        var go = new GameObject(_arrowChildName);
        go.transform.SetParent(transform, false);
        _arrow = go.AddComponent<DirectionArrowIndicator>();
    }

    private void HideArrow()
    {
        if (_arrow != null) _arrow.Hide();
        _arrowActive = false;
        _currentTarget = null;
    }
}
