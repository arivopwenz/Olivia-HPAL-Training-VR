using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System.Text;
using System.Linq;

/// <summary>
/// OLIVIA VR - EvaluationReportUI.cs
/// UI panel untuk menampilkan hasil evaluasi training (per level & summary akhir).
/// </summary>
public class EvaluationReportUI : MonoBehaviour
{
    [Header("=== Panel ===")]
    [SerializeField] private GameObject _panelLevelComplete;
    [SerializeField] private GameObject _panelSessionSummary;

    [Header("=== Level Complete UI ===")]
    [SerializeField] private TextMeshProUGUI _txtLevelNama;
    [SerializeField] private TextMeshProUGUI _txtDurasi;
    [SerializeField] private TextMeshProUGUI _txtSkorWaktu;
    [SerializeField] private TextMeshProUGUI _txtSkorAkurasi;
    [SerializeField] private TextMeshProUGUI _txtSkorSafety;
    [SerializeField] private TextMeshProUGUI _txtSkorEfisiensi;
    [SerializeField] private TextMeshProUGUI _txtSkorTotal;
    [SerializeField] private TextMeshProUGUI _txtGrade;
    [SerializeField] private TextMeshProUGUI _txtError;
    [SerializeField] private TextMeshProUGUI _txtRetry;
    [SerializeField] private TextMeshProUGUI _txtAchievements;
    [SerializeField] private Image _imgGradeIcon;
    [SerializeField] private Button _btnLanjut;

    [Header("=== Session Summary UI ===")]
    [SerializeField] private TextMeshProUGUI _txtSessionID;
    [SerializeField] private TextMeshProUGUI _txtTanggal;
    [SerializeField] private TextMeshProUGUI _txtLevelDiselesaikan;
    [SerializeField] private TextMeshProUGUI _txtDurasiTotal;
    [SerializeField] private TextMeshProUGUI _txtSkorTotalSession;
    [SerializeField] private TextMeshProUGUI _txtSkorRataRata;
    [SerializeField] private TextMeshProUGUI _txtGradeSession;
    [SerializeField] private TextMeshProUGUI _txtLulus;
    [SerializeField] private TextMeshProUGUI _txtTotalError;
    [SerializeField] private TextMeshProUGUI _txtTotalRetry;
    [SerializeField] private TextMeshProUGUI _txtLevelBreakdown;
    [SerializeField] private Button _btnSelesai;

    [Header("=== Sprite Grade Icons ===")]
    [SerializeField] private Sprite _iconAPlus;
    [SerializeField] private Sprite _iconA;
    [SerializeField] private Sprite _iconB;
    [SerializeField] private Sprite _iconC;

    [Header("=== Referensi ===")]
    private TrainingEvaluationSystem _evalSystem;

    private void Awake()
    {
        _evalSystem = TrainingEvaluationSystem.Instance;
        if (_evalSystem == null)
            _evalSystem = FindFirstObjectByType<TrainingEvaluationSystem>();

        if (_btnLanjut != null)
            _btnLanjut.onClick.AddListener(SembunyikanPanelLevel);

        if (_btnSelesai != null)
            _btnSelesai.onClick.AddListener(SembunyikanPanelSession);

        SembunyikanSemuaPanel();
    }

    private void OnEnable()
    {
        GameLevelManager.OnLevelComplete += OnLevelSelesai;
    }

    private void OnDisable()
    {
        GameLevelManager.OnLevelComplete -= OnLevelSelesai;
    }

    #region Event Handlers

    private void OnLevelSelesai(GameLevelManager.GameLevel level, int nomorTombol)
    {
        // Tunggu sebentar biar evaluasi selesai dihitung
        Invoke(nameof(TampilkanHasilLevel), 0.5f);
    }

    #endregion

    #region Public Methods

    /// <summary>
    /// Tampilkan hasil evaluasi level yang baru selesai.
    /// </summary>
    public void TampilkanHasilLevel()
    {
        if (_evalSystem == null || _panelLevelComplete == null)
            return;

        var glm = GameLevelManager.Instance;
        if (glm == null) return;

        var perf = _evalSystem.DapatkanPerformanceLevel(glm.CurrentLevel);
        if (perf == null)
        {
            Debug.LogWarning("[EvalUI] Tidak ada performance data untuk level saat ini.");
            return;
        }

        // Isi data
        SetText(_txtLevelNama, perf.namaLevel);
        SetText(_txtDurasi, $"{FormatWaktu(perf.durasiDetik)} / {FormatWaktu(perf.targetWaktuOptimal)} (target)");
        SetText(_txtSkorWaktu, $"{perf.skorWaktu:F1} / 30");
        SetText(_txtSkorAkurasi, $"{perf.skorAkurasi:F1} / 30");
        SetText(_txtSkorSafety, $"{perf.skorSafety:F1} / 25");
        SetText(_txtSkorEfisiensi, $"{perf.skorEfisiensi:F1} / 15");
        SetText(_txtSkorTotal, $"{perf.skorTotal:F1} / 100");

        string grade = TentukanGrade(perf.skorTotal);
        SetText(_txtGrade, grade);
        SetGradeIcon(grade);

        SetText(_txtError, $"{perf.jumlahError}");
        SetText(_txtRetry, $"{perf.jumlahRetry}");

        // Achievements
        if (perf.achievementLog.Count > 0)
        {
            StringBuilder sb = new StringBuilder();
            foreach (var ach in perf.achievementLog)
                sb.AppendLine($"• {ach}");
            SetText(_txtAchievements, sb.ToString());
        }
        else
        {
            SetText(_txtAchievements, "Tidak ada achievement khusus.");
        }

        _panelLevelComplete.SetActive(true);
    }

    /// <summary>
    /// Tampilkan summary akhir session (setelah semua level selesai atau player exit).
    /// </summary>
    public void TampilkanSummarySession()
    {
        if (_evalSystem == null || _panelSessionSummary == null)
            return;

        var summary = _evalSystem.DapatkanSessionSummary();
        if (summary == null)
        {
            Debug.LogWarning("[EvalUI] Tidak ada session summary.");
            return;
        }

        // Isi data
        SetText(_txtSessionID, summary.sessionID);
        SetText(_txtTanggal, summary.tanggalMulai.ToString("dd/MM/yyyy HH:mm"));
        SetText(_txtLevelDiselesaikan, $"{summary.levelDiselesaikan}");
        SetText(_txtDurasiTotal, $"{summary.totalDurasiMenit:F1} menit");
        SetText(_txtSkorTotalSession, $"{summary.skorTotal:F1}");
        SetText(_txtSkorRataRata, $"{summary.skorRataRata:F1} / 100");
        SetText(_txtGradeSession, summary.gradeOverall);
        SetText(_txtLulus, summary.lulus ? "<color=green>LULUS ✓</color>" : "<color=red>BELUM LULUS ✗</color>");
        SetText(_txtTotalError, $"{summary.totalError}");
        SetText(_txtTotalRetry, $"{summary.totalRetry}");

        // Level breakdown
        StringBuilder sb = new StringBuilder();
        sb.AppendLine("=== BREAKDOWN PER LEVEL ===\n");
        foreach (var perf in summary.performancePerLevel.OrderBy(p => (int)p.level))
        {
            sb.AppendLine($"<b>{perf.namaLevel}</b>");
            sb.AppendLine($"  Skor: {perf.skorTotal:F1}/100 ({TentukanGrade(perf.skorTotal)})");
            sb.AppendLine($"  Durasi: {FormatWaktu(perf.durasiDetik)}");
            sb.AppendLine($"  Error: {perf.jumlahError}, Retry: {perf.jumlahRetry}");
            sb.AppendLine();
        }
        SetText(_txtLevelBreakdown, sb.ToString());

        _panelSessionSummary.SetActive(true);
    }

    #endregion

    #region Private Methods

    private void SembunyikanPanelLevel()
    {
        if (_panelLevelComplete != null)
            _panelLevelComplete.SetActive(false);
    }

    private void SembunyikanPanelSession()
    {
        if (_panelSessionSummary != null)
            _panelSessionSummary.SetActive(false);
    }

    private void SembunyikanSemuaPanel()
    {
        SembunyikanPanelLevel();
        SembunyikanPanelSession();
    }

    private void SetText(TextMeshProUGUI txt, string value)
    {
        if (txt != null)
            txt.text = value;
    }

    private string TentukanGrade(float skor)
    {
        if (skor >= 95f) return "A+";
        if (skor >= 90f) return "A";
        if (skor >= 85f) return "A-";
        if (skor >= 80f) return "B+";
        if (skor >= 75f) return "B";
        if (skor >= 70f) return "B-";
        if (skor >= 65f) return "C+";
        if (skor >= 60f) return "C";
        return "D";
    }

    private void SetGradeIcon(string grade)
    {
        if (_imgGradeIcon == null) return;

        Sprite icon = null;
        if (grade.StartsWith("A+"))
            icon = _iconAPlus;
        else if (grade.StartsWith("A"))
            icon = _iconA;
        else if (grade.StartsWith("B"))
            icon = _iconB;
        else
            icon = _iconC;

        _imgGradeIcon.sprite = icon;
        _imgGradeIcon.enabled = (icon != null);
    }

    private string FormatWaktu(float detik)
    {
        int menit = Mathf.FloorToInt(detik / 60f);
        int sisa = Mathf.FloorToInt(detik % 60f);
        return $"{menit}m {sisa}s";
    }

    #endregion

    #region Debug

    [ContextMenu("Debug: Tampilkan Summary Session")]
    private void DebugTampilkanSummary()
    {
        TampilkanSummarySession();
    }

    #endregion
}
