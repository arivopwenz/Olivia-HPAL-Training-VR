using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System.Collections;

/// <summary>
/// OLIVIA VR - PlayerHUD.cs (Updated v3.0)
/// HUD pemain yang menampilkan:
/// - Status level aktif saat ini
/// - Checklist 7 APD (Real-time update)
/// - Notifikasi quest (popup bawah layar)
/// - Panduan walkie talkie
///
/// Sekarang subscribe ke GameLevelManager dan PhaseManager (v3.0)
/// BUKAN lagi ke event PhaseManager lama yang sudah dihapus.
/// </summary>
public class PlayerHUD : MonoBehaviour
{
    [Header("=== Panel Header ===")]
    public TextMeshProUGUI txtLevelLabel;   // "LEVEL 1 — APD Safety"
    public TextMeshProUGUI txtQuestLabel;   // Deskripsi quest aktif
    public Image bgHeader;

    [Header("=== Checklist APD (7 Item) ===")]
    public TextMeshProUGUI taskHelm;
    public TextMeshProUGUI taskRompi;
    public TextMeshProUGUI taskKacamata;
    public TextMeshProUGUI taskSepatu;
    public TextMeshProUGUI taskSarungTangan;
    public TextMeshProUGUI taskRespirator;
    public TextMeshProUGUI taskWalkieTalkie;

    [Header("=== Panel Operasional ===")]
    public GameObject panelOperasional;
    public TextMeshProUGUI txtParameterInfo;   // Info parameter saat ini (Flow Rate, dll)

    [Header("=== Panel Walkie Talkie Hint ===")]
    public GameObject panelWalkieTalkieHint;
    public TextMeshProUGUI txtHintKataKunci;   // "Ucapkan: 'slurry pump aktif'"

    [Header("=== Notifikasi Bawah ===")]
    public GameObject panelNotif;
    public TextMeshProUGUI txtNotif;
    public Image bgNotif;

    // Warna
    private Color _cDone   = new Color(0.2f,  0.95f, 0.45f); // Hijau: selesai
    private Color _cTodo   = new Color(0.65f, 0.65f, 0.65f); // Abu: belum
    private Color _cActive = new Color(1f,    0.88f, 0.15f); // Kuning: aktif
    private Color _cBlue   = new Color(0.3f,  0.85f, 1f);    // Biru: info

    // ============================================================
    //  LIFECYCLE
    // ============================================================
    void Start()
    {
        // Subscribe ke events baru (GameLevelManager + PhaseManager v3.0)
        GameLevelManager.OnLevelStarted   += OnLevelMulai;
        GameLevelManager.OnLevelComplete  += OnLevelSelesai;
        PhaseManager.OnApdItemWorn        += OnSatuApdDipakai;
        PhaseManager.OnAPD7Lengkap        += OnSemuaApdLengkap;
        WalkieTalkieManager.OnPTTDitekan  += OnPTTPress;
        WalkieTalkieManager.OnPTTDilepas  += OnPTTRelease;

        // Init UI
        if (panelNotif != null)          panelNotif.SetActive(false);
        if (panelOperasional != null)    panelOperasional.SetActive(false);
        if (panelWalkieTalkieHint != null) panelWalkieTalkieHint.SetActive(false);

        SetLevelLabel("LEVEL 0 — TUTORIAL", _cActive);
        SetQuestLabel("Pelajari cara berjalan, grab, dan gunakan Walkie Talkie.");
    }

    void OnDestroy()
    {
        GameLevelManager.OnLevelStarted   -= OnLevelMulai;
        GameLevelManager.OnLevelComplete  -= OnLevelSelesai;
        PhaseManager.OnApdItemWorn        -= OnSatuApdDipakai;
        PhaseManager.OnAPD7Lengkap        -= OnSemuaApdLengkap;
        WalkieTalkieManager.OnPTTDitekan  -= OnPTTPress;
        WalkieTalkieManager.OnPTTDilepas  -= OnPTTRelease;
    }

    // ============================================================
    //  EVENT HANDLERS
    // ============================================================
    void OnLevelMulai(GameLevelManager.GameLevel level)
    {
        int idx = (int)level;
        string[] namaLevel = new string[]
        {
            "LEVEL 0 — TUTORIAL",
            "LEVEL 1 — APD SAFETY",
            "LEVEL 2 — DCS PREPARATION",
            "LEVEL 3 — ORE & SLURRY",
            "LEVEL 4 — SLURRY PUMP",
            "LEVEL 5 — STEAM VALVE",
            "LEVEL 6 — ACID INJECTION",
            "LEVEL 7 — AUTOCLAVE",
            "LEVEL 8 — MONITORING DCS",
            "LEVEL 9 — FLASH VESSEL",
            "LEVEL 10 — CCD",
            "LEVEL 11 — MHP SAMPLING",
            "LEVEL 12 — TAILING DISCHARGE",
            "LEVEL 13 — TAILING WASTE",
            "LEVEL 14 — DARURAT K3"
        };

        string[] questText = new string[]
        {
            "Pelajari cara berjalan, grab, dan gunakan Walkie Talkie.",
            "Pakai 7 APD wajib: Helm, Rompi, Kacamata, Sepatu, Sarung Tangan, Respirator, dan HT.",
            "Aktifkan DCS. Laporkan area via HT: 'siapkan area'.",
            "X-Ray Crusher & Slurry Tank. Laporkan: 'ore masuk'.",
            "Tekan Tombol 4 DCS. Atur Flow Rate ke 450 m³/h dengan [+] / [-]. Lapor: 'slurry pump aktif'.",
            "Putar Rotary Valve Steam di Pre-Heater. Target: 180-200°C. Lapor: 'katup steam terbuka'.",
            "Tekan Tombol 6 DCS. Atur rasio H₂SO₄ ke 350 kg/ton. Target pH: 1.0. Lapor: 'acid aktif'.",
            "X-Ray Autoclave. Cek: Suhu 250°C, Tekanan 50 atm, RPM 60. Lapor: 'suhu dua ratus lima puluh'.",
            "Pantau parameter 60 detik. Koreksi RPM/Tekanan dengan [+]/[-]. Lapor: 'parameter stabil'.",
            "X-Ray Flash Vessel. Tekanan turun ke 12 atm. Lapor: 'flash vessel normal'.",
            "Tekan Tombol 10 DCS (CCD). Lapor: 'CCD aktif'.",
            "Grab botol sampel dari tangki MHP. Lapor: 'MHP terbentuk'.",
            "Tekan Tombol 12 DCS (Tailing Discharge). Lapor: 'limbah dialirkan'.",
            "Taburkan kapur hingga pH 8.5. Aktifkan Filter Press. Lapor: 'tailing aman'.",
            "DARURAT! Lapor evakuasi via HT: 'emergency'. Tekan tombol ESD merah segera!"
        };

        Color warnaHeader = (level == GameLevelManager.GameLevel.Level14_Emergency)
            ? new Color(1f, 0.2f, 0.2f)   // Merah untuk emergency
            : _cBlue;

        if (idx < namaLevel.Length)
        {
            SetLevelLabel(namaLevel[idx], warnaHeader);
            SetQuestLabel(questText[idx]);
        }

        // Tampilkan panel walkie talkie hint untuk level yang butuh voice report
        bool butuhVoice = idx >= 1;
        if (panelWalkieTalkieHint != null)
            panelWalkieTalkieHint.SetActive(butuhVoice);

        // Update hint kata kunci
        UpdateHintKataKunci(level);

        // Tampilkan panel operasional mulai Level 2
        if (panelOperasional != null)
            panelOperasional.SetActive(idx >= 2);

        ShowNotif($"Level {idx} dimulai!", false);
    }

    void OnLevelSelesai(GameLevelManager.GameLevel level, int skor)
    {
        ShowNotif($"✓ Level {(int)level} Selesai! Skor: {skor}/100", true);
    }

    void OnSatuApdDipakai(string namaApd)
    {
        string n = namaApd.ToLower();
        if      (n.Contains("helm"))           SetTaskDone(taskHelm);
        else if (n.Contains("rompi"))          SetTaskDone(taskRompi);
        else if (n.Contains("kacamata"))       SetTaskDone(taskKacamata);
        else if (n.Contains("sepatu"))         SetTaskDone(taskSepatu);
        else if (n.Contains("sarung"))         SetTaskDone(taskSarungTangan);
        else if (n.Contains("respirator") || n.Contains("masker")) SetTaskDone(taskRespirator);
        else if (n.Contains("walkie") || n.Contains("ht"))         SetTaskDone(taskWalkieTalkie);

        ShowNotif($"✓ {namaApd} terpasang!", false);
    }

    void OnSemuaApdLengkap()
    {
        // Pastikan semua centang APD hijau
        SetTaskDone(taskHelm);
        SetTaskDone(taskRompi);
        SetTaskDone(taskKacamata);
        SetTaskDone(taskSepatu);
        SetTaskDone(taskSarungTangan);
        SetTaskDone(taskRespirator);
        SetTaskDone(taskWalkieTalkie);
        ShowNotif("Semua APD lengkap! Safety Gate terbuka.", true);
    }

    void OnPTTPress()
    {
        // Indikator PTT aktif — bisa tambahkan visual "Mendengarkan..." di UI
        if (txtNotif != null) txtNotif.text = "🎙 BERBICARA...";
        if (panelNotif != null) panelNotif.SetActive(true);
    }

    void OnPTTRelease()
    {
        // Sembunyikan indikator mendengarkan setelah 1 detik
        StartCoroutine(HideNotifDelayed(1.5f));
    }

    // ============================================================
    //  UPDATE HINT WALKIE TALKIE
    // ============================================================
    private void UpdateHintKataKunci(GameLevelManager.GameLevel level)
    {
        if (txtHintKataKunci == null) return;
        string[] hints = new string[]
        {
            "",
            "Ucapkan: 'APD lengkap'",
            "Ucapkan: 'siapkan area'",
            "Ucapkan: 'ore masuk'",
            "Ucapkan: 'slurry pump aktif'",
            "Ucapkan: 'katup steam terbuka'",
            "Ucapkan: 'acid aktif'",
            "Ucapkan: 'suhu dua ratus lima puluh'",
            "Ucapkan: 'parameter stabil'",
            "Ucapkan: 'flash vessel normal'",
            "Ucapkan: 'CCD aktif'",
            "Ucapkan: 'MHP terbentuk'",
            "Ucapkan: 'limbah dialirkan'",
            "Ucapkan: 'tailing aman'",
            "Ucapkan: 'emergency' atau 'evakuasi'!"
        };
        int idx = (int)level;
        if (idx < hints.Length) txtHintKataKunci.text = hints[idx];
    }

    // ============================================================
    //  UI HELPERS
    // ============================================================
    void SetLevelLabel(string teks, Color warna)
    {
        if (txtLevelLabel == null) return;
        txtLevelLabel.text  = teks;
        txtLevelLabel.color = warna;
        if (bgHeader != null)
            bgHeader.color = new Color(warna.r * 0.15f, warna.g * 0.15f, warna.b * 0.15f, 0.95f);
    }

    void SetQuestLabel(string teks)
    {
        if (txtQuestLabel != null) txtQuestLabel.text = teks;
    }

    void SetTaskDone(TextMeshProUGUI txt)
    {
        if (txt == null) return;
        string t = txt.text;
        if (t.StartsWith("[ ]")) txt.text = "[✓]" + t.Substring(3);
        txt.color = _cDone;
    }

    void ShowNotif(string pesan, bool sukses)
    {
        if (panelNotif == null) return;
        StopCoroutine("HideNotif");
        panelNotif.SetActive(true);
        if (txtNotif != null) txtNotif.text = pesan;
        if (bgNotif != null)
            bgNotif.color = sukses
                ? new Color(0.08f, 0.45f, 0.12f, 0.95f)
                : new Color(0.06f, 0.18f, 0.42f, 0.95f);
        StartCoroutine("HideNotif");
    }

    IEnumerator HideNotif()
    {
        yield return new WaitForSeconds(4.5f);
        if (panelNotif != null) panelNotif.SetActive(false);
    }

    IEnumerator HideNotifDelayed(float delay)
    {
        yield return new WaitForSeconds(delay);
        if (panelNotif != null) panelNotif.SetActive(false);
    }
}
