using UnityEngine;
using System;
using System.Collections;
using System.Collections.Generic;

/// <summary>
/// OLIVIA VR - GameLevelManager.cs
/// "Otak Utama" yang mengatur 14 Level simulasi HPAL.
/// Menggantikan sistem fase linear lama menjadi sistem manajemen level berbasis state.
/// 
/// ALUR: Level 0 (Tutorial) → Level 1 (APD) → Level 2-12 (Operasional HPAL) → Level 13 (Tailing) → Level 14 (K3 Darurat)
/// </summary>
public class GameLevelManager : MonoBehaviour
{
    // ============================================================
    //  SINGLETON
    // ============================================================
    public static GameLevelManager Instance { get; private set; }

    // ============================================================
    //  DEFINISI LEVEL (14 Level + Tutorial)
    // ============================================================
    public enum GameLevel
    {
        Level0_Tutorial        = 0,
        Level1_APD             = 1,
        Level2_DCSPrep         = 2,
        Level3_OreSlurry       = 3,
        Level4_SlurryPump      = 4,
        Level5_SteamValve      = 5,
        Level6_AcidInjection   = 6,
        Level7_Autoclave       = 7,
        Level8_Monitoring      = 8,
        Level9_FlashVessel     = 9,
        Level10_CCD            = 10,
        Level11_MHP            = 11,
        Level12_TailingDischarge = 12,
        Level13_TailingWaste   = 13,
        Level14_Emergency      = 14
    }

    // ============================================================
    //  DATA PER LEVEL: Target SOP Pabrik
    // ============================================================
    [Serializable]
    public class LevelData
    {
        public GameLevel level;
        [TextArea(2, 4)]
        public string namaLevel;
        [TextArea(2, 4)]
        public string deskripsiQuest;
        public int nomorTombolDCS;          // Tombol DCS ke berapa yang harus ditekan (0 = tidak ada)
        public bool butuhVoiceReport;       // Apakah level ini wajib laporan HT?
        public string kataKunciVoice;       // Kata kunci yang harus diucapkan pemain
        public string audioBalasanNPC;      // Nama file AudioClip balasan NPC

        // Parameter SOP Target (untuk level tertentu)
        public float targetFlowRate;        // m³/h (Level 4)
        public float targetAcidRatio;       // kg/ton (Level 6)
        public float targetSuhu;            // °C (Level 7-8)
        public float targetTekanan;         // atm (Level 7-8)
        public float targetRPM;             // RPM Agitator (Level 7-8)
        public float targetPH;              // pH Target (Level 6, 13)
    }

    // ============================================================
    //  EVENTS (Observer Pattern)
    // ============================================================
    public static event Action<GameLevel> OnLevelStarted;       // Level baru dimulai
    public static event Action<GameLevel, int> OnLevelComplete; // Level selesai (level, skor 0-100)
    public static event Action<int> OnDCSButtonShouldHighlight; // Tombol DCS ke-X harus menyala
    public static event Action<GameLevel> OnEmergencyTriggered; // Darurat dimulai

    // ============================================================
    //  INSPECTOR
    // ============================================================
    [Header("=== Status Level ===")]
    [SerializeField] private GameLevel _currentLevel = GameLevel.Level0_Tutorial;
    [SerializeField] private bool _levelSedangBerjalan = false;

    [Header("=== Parameter Real-Time (Sinkron dengan Lapangan) ===")]
    [SerializeField] private float _flowRateSaatIni   = 0f;    // m³/h
    [SerializeField] private float _acidRatioSaatIni  = 0f;    // kg/ton
    [SerializeField] private float _suhuSaatIni       = 25f;   // °C
    [SerializeField] private float _tekananSaatIni    = 1f;    // atm
    [SerializeField] private float _rpmSaatIni        = 0f;    // RPM
    [SerializeField] private float _phSaatIni         = 7f;    // pH

    [Header("=== Scoring ===")]
    [SerializeField] private float[] _skorPerLevel = new float[15]; // Index = nomor level
    [SerializeField] private float _skorTotal = 0f;

    [Header("=== Referensi Script ===")]
    [SerializeField] private PhaseManager _phaseManager;

    // ============================================================
    //  DATA LEVEL (Otomatis di-populate lewat kode)
    // ============================================================
    private Dictionary<GameLevel, LevelData> _dataLevel = new Dictionary<GameLevel, LevelData>();

    // Tracking per level
    private bool _voiceReportSudahDilakukan = false;
    private bool _dcsTombolSudahDitekan     = false;
    private float _waktuMulaiLevel          = 0f;

    // ============================================================
    //  LIFECYCLE
    // ============================================================
    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;
        DontDestroyOnLoad(gameObject);
        InisialisasiDataLevel();
    }

    private void Start()
    {
        MulaiLevel(GameLevel.Level0_Tutorial);
    }

    // ============================================================
    //  INISIALISASI DATA 14 LEVEL (Patokan SOP Pabrik)
    // ============================================================
    private void InisialisasiDataLevel()
    {
        TambahLevel(new LevelData {
            level = GameLevel.Level0_Tutorial,
            namaLevel = "Level 0 — Tutorial",
            deskripsiQuest = "Pelajari cara berjalan, grab objek, dan menggunakan Walkie Talkie.",
            nomorTombolDCS = 0, butuhVoiceReport = false,
            kataKunciVoice = "", audioBalasanNPC = ""
        });

        TambahLevel(new LevelData {
            level = GameLevel.Level1_APD,
            namaLevel = "Level 1 — Persiapan APD",
            deskripsiQuest = "Pakai 7 APD wajib: Helm, Rompi, Kacamata, Sepatu, Sarung Tangan, Respirator, dan Radio HT.",
            nomorTombolDCS = 1, butuhVoiceReport = true,
            kataKunciVoice = "APD lengkap",
            audioBalasanNPC = "audio_level1_balasan"  // "Copy, pintu Safety Gate terbuka."
        });

        TambahLevel(new LevelData {
            level = GameLevel.Level2_DCSPrep,
            namaLevel = "Level 2 — DCS Preparation",
            deskripsiQuest = "Aktifkan DCS. Laporkan area via HT sebelum memulai operasi.",
            nomorTombolDCS = 2, butuhVoiceReport = true,
            kataKunciVoice = "siapkan area",
            audioBalasanNPC = "audio_level2_balasan"  // "Siap, menuju area Crusher."
        });

        TambahLevel(new LevelData {
            level = GameLevel.Level3_OreSlurry,
            namaLevel = "Level 3 — Ore ke Slurry (Lapangan)",
            deskripsiQuest = "Gunakan X-Ray untuk melihat Ore dihancurkan Crusher dan masuk ke Slurry Tank. Laporkan saat cairan mencapai 25%.",
            nomorTombolDCS = 3, butuhVoiceReport = true,
            kataKunciVoice = "ore masuk",
            audioBalasanNPC = "audio_level3_balasan"  // "Copy, standby aktivasi Slurry Pump."
        });

        TambahLevel(new LevelData {
            level = GameLevel.Level4_SlurryPump,
            namaLevel = "Level 4 — Slurry Pump (DCS)",
            deskripsiQuest = "Tekan Tombol 4 di DCS. Atur Flow Rate ke 450 m³/h menggunakan tombol [+] dan [-].",
            nomorTombolDCS = 4, butuhVoiceReport = true,
            kataKunciVoice = "slurry pump aktif",
            audioBalasanNPC = "audio_level4_balasan",  // "Copy, memantau aliran ke Pre-heater."
            targetFlowRate = 450f  // SOP: 450 m³/h
        });

        TambahLevel(new LevelData {
            level = GameLevel.Level5_SteamValve,
            namaLevel = "Level 5 — Steam Valve (Lapangan)",
            deskripsiQuest = "Putar Rotary Valve steam di Pre-Heater hingga suhu naik ke 180-200°C. Laporkan via HT.",
            nomorTombolDCS = 5, butuhVoiceReport = true,
            kataKunciVoice = "katup steam terbuka",
            audioBalasanNPC = "audio_level5_balasan",  // "Copy, bersiap untuk injeksi asam."
            targetSuhu = 190f  // Target Pre-Heater: 180-200°C
        });

        TambahLevel(new LevelData {
            level = GameLevel.Level6_AcidInjection,
            namaLevel = "Level 6 — Acid Injection (DCS)",
            deskripsiQuest = "Tekan Tombol 6. Atur rasio H₂SO₄ ke 350 kg/ton menggunakan [+] dan [-]. pH harus turun ke 1.0.",
            nomorTombolDCS = 6, butuhVoiceReport = true,
            kataKunciVoice = "acid aktif",
            audioBalasanNPC = "audio_level6_balasan",  // "Copy, aman masuk Autoclave."
            targetAcidRatio = 350f,  // SOP: 350 kg/ton bijih
            targetPH = 1.0f          // pH Target: 1.0
        });

        TambahLevel(new LevelData {
            level = GameLevel.Level7_Autoclave,
            namaLevel = "Level 7 — Monitor Autoclave (Lapangan)",
            deskripsiQuest = "Gunakan X-Ray. Pastikan: Suhu 250-255°C, Tekanan 45-50 atm, Agitator 60 RPM. Laporkan semua angka.",
            nomorTombolDCS = 7, butuhVoiceReport = true,
            kataKunciVoice = "suhu 250",
            audioBalasanNPC = "audio_level7_balasan",  // "Copy, parameter sesuai SOP."
            targetSuhu = 252f,    // SOP Autoclave: 250-255°C (tengah)
            targetTekanan = 47.5f, // SOP Autoclave: 45-50 atm (tengah)
            targetRPM = 60f        // SOP Agitator: 60 RPM
        });

        TambahLevel(new LevelData {
            level = GameLevel.Level8_Monitoring,
            namaLevel = "Level 8 — Monitoring Ketat (DCS)",
            deskripsiQuest = "Pantau parameter 60 detik. Koreksi jika RPM drop atau Tekanan naik menggunakan [+] / [-].",
            nomorTombolDCS = 8, butuhVoiceReport = true,
            kataKunciVoice = "parameter stabil",
            audioBalasanNPC = "audio_level8_balasan",  // "Copy, proses optimal."
            targetSuhu = 252f,
            targetTekanan = 47.5f,
            targetRPM = 60f
        });

        TambahLevel(new LevelData {
            level = GameLevel.Level9_FlashVessel,
            namaLevel = "Level 9 — Flash Vessel (Lapangan)",
            deskripsiQuest = "X-Ray Flash Vessel. Tekanan turun ke 12 atm. Uap keluar itu normal. Laporkan kondisi aman.",
            nomorTombolDCS = 9, butuhVoiceReport = true,
            kataKunciVoice = "flash vessel normal",
            audioBalasanNPC = "audio_level9_balasan",  // "Copy, siap ke CCD."
            targetTekanan = 12f    // Target Flash Vessel: 12 atm
        });

        TambahLevel(new LevelData {
            level = GameLevel.Level10_CCD,
            namaLevel = "Level 10 — CCD Activation (DCS)",
            deskripsiQuest = "Tekan Tombol 10 (CCD Activation). Pemisahan padat-cair dimulai.",
            nomorTombolDCS = 10, butuhVoiceReport = true,
            kataKunciVoice = "CCD aktif",
            audioBalasanNPC = "audio_level10_balasan"  // "Copy, menuju area presipitasi."
        });

        TambahLevel(new LevelData {
            level = GameLevel.Level11_MHP,
            namaLevel = "Level 11 — MHP Sampling (Lapangan)",
            deskripsiQuest = "Grab botol sampel. Ambil sampel dari tangki MHP. Laporkan hasil presipitasi.",
            nomorTombolDCS = 11, butuhVoiceReport = true,
            kataKunciVoice = "MHP terbentuk",
            audioBalasanNPC = "audio_level11_balasan"  // "Copy, produksi utama selesai."
        });

        TambahLevel(new LevelData {
            level = GameLevel.Level12_TailingDischarge,
            namaLevel = "Level 12 — Tailing Discharge (DCS)",
            deskripsiQuest = "Tekan Tombol 12 untuk mengalirkan limbah tailing ke tangki netralisasi.",
            nomorTombolDCS = 12, butuhVoiceReport = true,
            kataKunciVoice = "limbah dialirkan",
            audioBalasanNPC = "audio_level12_balasan"  // "Copy, siap melakukan netralisasi."
        });

        TambahLevel(new LevelData {
            level = GameLevel.Level13_TailingWaste,
            namaLevel = "Level 13 — Tailing Waste Management (Lapangan)",
            deskripsiQuest = "Taburkan kapur limestone ke tangki asam hingga pH naik ke 8.0-9.0. Aktifkan Filter Press. Moisture < 25%.",
            nomorTombolDCS = 13, butuhVoiceReport = true,
            kataKunciVoice = "tailing aman",
            audioBalasanNPC = "audio_level13_balasan",  // "Copy, lingkungan aman."
            targetPH = 8.5f  // Target pH Tailing: 8.0 - 9.0
        });

        TambahLevel(new LevelData {
            level = GameLevel.Level14_Emergency,
            namaLevel = "Level 14 — DARURAT K3 (Kebocoran)",
            deskripsiQuest = "KEBOCORAN TERDETEKSI! Laporkan evakuasi via HT, lalu tekan tombol ESD merah di DCS!",
            nomorTombolDCS = 14, butuhVoiceReport = true,
            kataKunciVoice = "emergency",
            audioBalasanNPC = "audio_level14_balasan"  // "Copy, kami evakuasi sekarang!"
        });

        Log("INIT", $"Berhasil memuat data {_dataLevel.Count} level.", "cyan");
    }

    private void TambahLevel(LevelData data) => _dataLevel[data.level] = data;

    // ============================================================
    //  KONTROL LEVEL UTAMA
    // ============================================================
    public void MulaiLevel(GameLevel level)
    {
        if (!_dataLevel.ContainsKey(level))
        {
            Log("ERROR", $"Data level {level} tidak ditemukan!", "red");
            return;
        }

        _currentLevel = level;
        _levelSedangBerjalan = true;
        _voiceReportSudahDilakukan = false;
        _dcsTombolSudahDitekan = false;
        _waktuMulaiLevel = Time.time;

        var data = _dataLevel[level];
        Log("LEVEL MULAI", $"<b>{data.namaLevel}</b>\nQuest: {data.deskripsiQuest}", "yellow");

        // Kasih tahu tombol DCS mana yang harus menyala
        if (data.nomorTombolDCS > 0)
            OnDCSButtonShouldHighlight?.Invoke(data.nomorTombolDCS);

        OnLevelStarted?.Invoke(level);
    }

    public void SelesaikanLevel(GameLevel level)
    {
        if (_currentLevel != level || !_levelSedangBerjalan) return;

        float waktuSelesai = Time.time - _waktuMulaiLevel;
        float skor = HitungSkorLevel(level, waktuSelesai);
        _skorPerLevel[(int)level] = skor;

        _levelSedangBerjalan = false;
        Log("LEVEL SELESAI", $"<b>{_dataLevel[level].namaLevel}</b> selesai! Skor: <b>{skor:F0}/100</b>", "green");
        OnLevelComplete?.Invoke(level, (int)skor);

        // Auto-pindah ke level berikutnya setelah delay singkat
        int levelBerikutnya = (int)level + 1;
        if (levelBerikutnya <= 14)
            StartCoroutine(TransisiKeLevel((GameLevel)levelBerikutnya));
        else
            SelesaikanSemua();
    }

    private IEnumerator TransisiKeLevel(GameLevel levelBerikutnya)
    {
        yield return new WaitForSeconds(2.5f);
        MulaiLevel(levelBerikutnya);
    }

    private void SelesaikanSemua()
    {
        _skorTotal = 0f;
        for (int i = 0; i < _skorPerLevel.Length; i++)
            _skorTotal += _skorPerLevel[i];
        _skorTotal /= 15f;

        Log("SIMULASI SELESAI", $"Semua level selesai! Nilai Akhir: <b>{_skorTotal:F1}/100</b>. " +
            (_skorTotal >= 70f ? "LULUS — Sertifikat K3 Virtual diraih!" : "BELUM LULUS — Ulangi simulasi."), "green");
    }

    // ============================================================
    //  DIPANGGIL DARI TOMBOL DCS
    // ============================================================

    /// <summary>
    /// Dipanggil oleh DCSTombol.cs saat pemain menekan tombol di panel DCS.
    /// </summary>
    public void OnDCSTombolDitekan(int nomorTombol)
    {
        if (!_dataLevel.ContainsKey(_currentLevel)) return;

        var data = _dataLevel[_currentLevel];
        if (data.nomorTombolDCS != nomorTombol)
        {
            Log("PERINGATAN", $"Tombol {nomorTombol} bukan tombol aktif sekarang! Harusnya tekan Tombol {data.nomorTombolDCS}.", "orange");
            return;
        }

        _dcsTombolSudahDitekan = true;
        Log("DCS", $"Tombol {nomorTombol} ditekan untuk level <b>{data.namaLevel}</b>.", "cyan");
        CekKondisiLevelSelesai();
    }

    // ============================================================
    //  DIPANGGIL DARI VOICE COMMAND SYSTEM
    // ============================================================

    /// <summary>
    /// Dipanggil oleh WalkieTalkieManager.cs saat kata kunci terdeteksi.
    /// </summary>
    public void OnVoiceKeywordTerdeteksi(string keyword)
    {
        if (!_dataLevel.ContainsKey(_currentLevel)) return;

        var data = _dataLevel[_currentLevel];
        string keywordBersih = keyword.ToLower().Trim();
        string targetBersih = data.kataKunciVoice.ToLower().Trim();

        if (!keywordBersih.Contains(targetBersih) && !targetBersih.Contains(keywordBersih))
        {
            Log("VOICE", $"Kata kunci '{keyword}' tidak sesuai untuk level ini.", "orange");
            return;
        }

        _voiceReportSudahDilakukan = true;
        Log("VOICE REPORT", $"Laporan diterima: '<i>{keyword}</i>'", "cyan");
        CekKondisiLevelSelesai();
    }

    // ============================================================
    //  SETTER PARAMETER REAL-TIME (Dipanggil dari DCSParameterControl.cs)
    // ============================================================

    public void SetFlowRate(float nilai)
    {
        _flowRateSaatIni = Mathf.Clamp(nilai, 0f, 600f);
        // Sinkronisasi shader cairan lapangan akan di-handle oleh subscriber
        CekParameterLevel4();
    }

    public void SetAcidRatio(float nilai)
    {
        _acidRatioSaatIni = Mathf.Clamp(nilai, 0f, 500f);
        CekParameterLevel6();
    }

    public void SetSuhu(float nilai)   { _suhuSaatIni    = nilai; }
    public void SetTekanan(float nilai) { _tekananSaatIni  = nilai; }
    public void SetRPM(float nilai)    { _rpmSaatIni     = nilai; }
    public void SetPH(float nilai)     { _phSaatIni      = nilai; }

    // Properties untuk dibaca oleh UI dan sistem lain
    public float FlowRate    => _flowRateSaatIni;
    public float AcidRatio   => _acidRatioSaatIni;
    public float Suhu        => _suhuSaatIni;
    public float Tekanan     => _tekananSaatIni;
    public float RPM         => _rpmSaatIni;
    public float PH          => _phSaatIni;
    public GameLevel CurrentLevel => _currentLevel;
    public bool LevelAktif   => _levelSedangBerjalan;

    // ============================================================
    //  VALIDASI PARAMETER (Sesuai Target SOP Pabrik)
    // ============================================================

    private void CekParameterLevel4()
    {
        if (_currentLevel != GameLevel.Level4_SlurryPump) return;
        var data = _dataLevel[GameLevel.Level4_SlurryPump];
        float toleransi = 10f; // ±10 m³/h
        if (Mathf.Abs(_flowRateSaatIni - data.targetFlowRate) <= toleransi)
        {
            Log("FLOW OK", $"Flow Rate {_flowRateSaatIni} m³/h. Target {data.targetFlowRate} m³/h tercapai!", "green");
            _dcsTombolSudahDitekan = true; // Anggap sebagai aksi sukses
            CekKondisiLevelSelesai();
        }
    }

    private void CekParameterLevel6()
    {
        if (_currentLevel != GameLevel.Level6_AcidInjection) return;
        var data = _dataLevel[GameLevel.Level6_AcidInjection];
        float toleransi = 10f; // ±10 kg/ton
        if (Mathf.Abs(_acidRatioSaatIni - data.targetAcidRatio) <= toleransi)
        {
            Log("ACID OK", $"Rasio Asam {_acidRatioSaatIni} kg/ton. Target {data.targetAcidRatio} kg/ton tercapai!", "green");
            _dcsTombolSudahDitekan = true;
            CekKondisiLevelSelesai();
        }
    }

    public bool ParameterAutoklaveSesuaiSOP()
    {
        if (!_dataLevel.ContainsKey(_currentLevel)) return false;
        var data = _dataLevel[_currentLevel];
        if (data.targetSuhu <= 0) return true; // Level ini tidak punya target suhu

        bool suhuOK    = Mathf.Abs(_suhuSaatIni    - data.targetSuhu)    <= 5f;
        bool tekananOK = Mathf.Abs(_tekananSaatIni  - data.targetTekanan) <= 2f;
        bool rpmOK     = Mathf.Abs(_rpmSaatIni      - data.targetRPM)     <= 5f;
        return suhuOK && tekananOK && rpmOK;
    }

    // ============================================================
    //  KONDISI LEVEL SELESAI
    // ============================================================

    private void CekKondisiLevelSelesai()
    {
        if (!_levelSedangBerjalan) return;
        var data = _dataLevel[_currentLevel];

        bool tombolOK = (data.nomorTombolDCS == 0) || _dcsTombolSudahDitekan;
        bool voiceOK  = !data.butuhVoiceReport || _voiceReportSudahDilakukan;

        if (tombolOK && voiceOK)
            SelesaikanLevel(_currentLevel);
    }

    // ============================================================
    //  EMERGENCY (Level 14)
    // ============================================================
    public void TriggerEmergency()
    {
        Log("‼ DARURAT", "Kondisi darurat terdeteksi! Segera lapor dan tekan ESD!", "red");
        OnEmergencyTriggered?.Invoke(_currentLevel);
        MulaiLevel(GameLevel.Level14_Emergency);
    }

    // ============================================================
    //  SCORING
    // ============================================================
    private float HitungSkorLevel(GameLevel level, float waktuSelesai)
    {
        // Skor dasar 100, dikurangi sesuai waktu yang terlalu lama
        float skorWaktu = Mathf.Clamp(100f - (waktuSelesai / 5f), 30f, 100f);
        return skorWaktu;
    }

    // ============================================================
    //  UTILITIES
    // ============================================================
    private void Log(string label, string pesan, string warna = "white")
    {
        Debug.Log($"<color={warna}>[GLM - {label}]</color> {pesan}");
    }

#if UNITY_EDITOR
    // Tombol debug di Inspector untuk testing cepat
    [ContextMenu("DEBUG: Selesaikan Level Ini")]
    private void DebugSelesaikanLevel()  => SelesaikanLevel(_currentLevel);

    [ContextMenu("DEBUG: Trigger Emergency")]
    private void DebugTriggerEmergency() => TriggerEmergency();

    [ContextMenu("DEBUG: Pindah ke Level Berikutnya")]
    private void DebugPindahLevel()
    {
        int next = (int)_currentLevel + 1;
        if (next <= 14) MulaiLevel((GameLevel)next);
    }
#endif
}
