using UnityEngine;
using System;
using System.Collections;
using UnityEngine.SceneManagement;
using System.Collections.Generic;

/// <summary>
/// OLIVIA VR - GameLevelManager.cs
/// Mengatur progres 14 level, validasi voice report, dan transisi antar level.
/// </summary>
public class GameLevelManager : MonoBehaviour
{
    public static GameLevelManager Instance { get; private set; }

    public enum GameLevel
    {
        Level0_Tutorial = 0,
        Level1_APD = 1,
        Level2_DCSPrep = 2,
        Level3_OreSlurry = 3,
        Level4_SlurryPump = 4,
        Level5_SteamValve = 5,
        Level6_AcidInjection = 6,
        Level7_Autoclave = 7,
        Level8_Monitoring = 8,
        Level9_FlashVessel = 9,
        Level10_CCD = 10,
        Level11_MHP = 11,
        Level12_TailingDischarge = 12,
        Level13_TailingWaste = 13,
        Level14_Emergency = 14
    }

    public enum Level3Phase
    {
        Idle = 0,
        MenungguTombolDcs = 1,
        MenungguLaporanAwal = 2,
        LaporanAwalDiterima = 3,
        ObservasiLapangan = 4,
        SiapLaporanAkhir = 5,
        Selesai = 6
    }

    public enum Level4Phase
    {
        Idle = 0,
        MenungguTombolDcs = 1,
        AturFlowRate = 2,
        MenungguLaporanFlow = 5,    // Setelah flow=450, lapor HT awal "slurry pump aktif"
        ObservasiPump = 3,           // Teleport ke field, lihat liquid mulai mengalir dari tank ke preheater
        ObservasiPreheater = 4,      // Liquid sudah sampai preheater
        MenungguLaporanAkhir = 8,    // Lapor HT akhir "cairan sudah di preheater"
        KembaliKeDcs = 6,
        Selesai = 7
    }

    [Serializable]
    public class LevelData
    {
        public GameLevel level;
        [TextArea(2, 4)] public string namaLevel;
        [TextArea(2, 4)] public string deskripsiQuest;
        public int nomorTombolDCS;
        public bool butuhVoiceReport;
        public string kataKunciVoice;
        public string kataKunciVoiceAwal;
        [TextArea(2, 5)] public string laporanVoiceAwal;
        [TextArea(2, 5)] public string laporanVoiceLengkap;
        public string audioBalasanNPC;
        public float targetFlowRate;
        public float targetAcidRatio;
        public float targetSuhu;
        public float targetTekanan;
        public float targetRPM;
        public float targetPH;
    }

    [Serializable]
    public class LaporanHtDinamis
    {
        public GameLevel level;
        [TextArea(1, 2)] public string kataKunciVoice;
        [TextArea(1, 2)] public string kataKunciVoiceAwal;
        [TextArea(2, 5)] public string laporanVoiceAwal;
        [TextArea(2, 5)] public string laporanVoiceLengkap;
        [TextArea(2, 5)] public string aliasTambahanPerBaris;
    }

    public static event Action<GameLevel> OnLevelStarted;
    public static event Action<GameLevel, int> OnLevelComplete;
    public static event Action<int> OnDCSButtonShouldHighlight;
    public static event Action<GameLevel> OnEmergencyTriggered;
    public static event Action<GameLevel> OnDCSViewConfirmed;
    public static event Action<int> OnDCSButtonPressed;
    public static event Action<string> OnVoiceReportAccepted;
    public static event Action<GameLevel, GameLevel, float> OnLevelTransitionRequested;
    public static event Action<Level3Phase> OnLevel3PhaseChanged;
    public static event Action OnLevel3OreReachedSlurry;
    public static event Action<Level4Phase> OnLevel4PhaseChanged;
    public static event Action OnLevel3LaporanAkhirDiterima;

    [Header("=== Status Level ===")]
    [SerializeField] private GameLevel _currentLevel = GameLevel.Level0_Tutorial;
    [SerializeField] private bool _levelSedangBerjalan;

    [Header("=== Parameter Real-Time ===")]
    [SerializeField] private float _flowRateSaatIni;
    [SerializeField] private float _acidRatioSaatIni;
    [SerializeField] private float _suhuSaatIni = 25f;
    [SerializeField] private float _tekananSaatIni = 1f;
    [SerializeField] private float _rpmSaatIni;
    [SerializeField] private float _phSaatIni = 7f;

    [Header("=== Scoring ===")]
    [SerializeField] private float[] _skorPerLevel = new float[15];
    [SerializeField] private float _skorTotal;

    [Header("=== Durasi Transisi ===")]
    [SerializeField] private float _durasiTransisiDefault = 2.75f;
    [SerializeField] private float _durasiTransisiLevel3 = 9.5f;
    [Tooltip("Durasi transisi Level 4 → 5 (samakan dengan Level 1, agak lama biar ada fade smooth).")]
    [SerializeField] private float _durasiTransisiLevel4 = 8f;

    [Header("=== Validasi Voice Report ===")]
    [SerializeField] private bool _izinkanKeywordPendekSebagaiCadangan = true;

    [Header("=== Laporan HT Dinamis (Bisa Kamu Edit) ===")]
    [SerializeField] private bool _gunakanLaporanHtDinamis = true;
    [SerializeField] private List<LaporanHtDinamis> _laporanHtPerLevel = new List<LaporanHtDinamis>();

    [Header("=== Referensi Script ===")]
    [SerializeField] private PhaseManager _phaseManager;

    private readonly Dictionary<GameLevel, LevelData> _dataLevel = new Dictionary<GameLevel, LevelData>();
    private bool _voiceReportSudahDilakukan;
    private bool _dcsTombolSudahDitekan;
    private bool _dcsSudahDilihat;
    private bool _level3OreSudahMasukSlurry;
    private bool _tundaTransisiLevel3;
    private bool _flowRateLevel4Dikonfirmasi;
    private bool _level5PreheaterReady;
    private bool _level6AcidComplete;
    private bool _level7AutoclaveInspected;
    private bool _level10CcdComplete;
    private bool _level11MhpComplete;
    private bool _level12TailingFilterComplete;
    private bool _level13DryStackComplete;
    private bool _level14EsdPressed;
    private float _waktuMulaiLevel;
    [SerializeField] private Level3Phase _level3Phase = Level3Phase.Idle;
    [SerializeField] private Level4Phase _level4Phase = Level4Phase.Idle;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
        if (transform.parent == null)
            DontDestroyOnLoad(gameObject);

        if (_phaseManager == null)
            _phaseManager = FindFirstObjectByType<PhaseManager>();

        InisialisasiDataLevel();
        IsiLaporanHtDinamisJikaKosong();
        TerapkanLaporanHtDinamis();
    }

    private void OnValidate()
    {
        IsiLaporanHtDinamisJikaKosong();
    }

    private void Start()
    {
        var sceneName = SceneManager.GetActiveScene().name;
        if (_currentLevel == GameLevel.Level0_Tutorial && sceneName.Contains("Level1"))
            _currentLevel = GameLevel.Level1_APD;

        MulaiLevel(_currentLevel);
    }

    private void InisialisasiDataLevel()
    {
        TambahLevel(new LevelData
        {
            level = GameLevel.Level0_Tutorial,
            namaLevel = "Level 0 - Tutorial",
            deskripsiQuest = "Pelajari cara berjalan, grab objek, dan menggunakan Walkie Talkie.",
            nomorTombolDCS = 0,
            butuhVoiceReport = false,
            kataKunciVoice = "",
            kataKunciVoiceAwal = "",
            laporanVoiceAwal = "",
            laporanVoiceLengkap = "",
            audioBalasanNPC = ""
        });

        TambahLevel(new LevelData
        {
            level = GameLevel.Level1_APD,
            namaLevel = "Level 1 - Persiapan APD",
            deskripsiQuest = "Pakai 8 APD wajib sebelum keluar dari area loker.",
            nomorTombolDCS = 1,
            butuhVoiceReport = true,
            kataKunciVoice = "apd lengkap",
            kataKunciVoiceAwal = "",
            laporanVoiceAwal = "",
            laporanVoiceLengkap = "DCS, APD lengkap. Operator siap masuk ke area proses.",
            audioBalasanNPC = "audio_level1_balasan"
        });

        TambahLevel(new LevelData
        {
            level = GameLevel.Level2_DCSPrep,
            namaLevel = "Level 2 - DCS Preparation",
            deskripsiQuest = "Aktifkan DCS, cek area, lalu kirim laporan HT sebelum operasi dimulai.",
            nomorTombolDCS = 2,
            butuhVoiceReport = true,
            kataKunciVoice = "siapkan area",
            kataKunciVoiceAwal = "",
            laporanVoiceAwal = "",
            laporanVoiceLengkap = "Field, siapkan area crusher. Operator DCS standby memulai operasi.",
            audioBalasanNPC = "audio_level2_balasan"
        });

        TambahLevel(new LevelData
        {
            level = GameLevel.Level3_OreSlurry,
            namaLevel = "Level 3 - Ore ke Slurry",
            deskripsiQuest = "Aktifkan alur awal mesin, amati ore masuk ke slurry tank, lalu kirim laporan HT lengkap.",
            nomorTombolDCS = 3,
            butuhVoiceReport = true,
            kataKunciVoice = "ore masuk",
            kataKunciVoiceAwal = "jalankan alur ore",
            laporanVoiceAwal = "Field, jalankan alur ore ke slurry tank. Operator DCS standby monitoring.",
            laporanVoiceLengkap = "DCS, ore sudah masuk ke slurry tank. Level cairan lima puluh persen dan proses aman.",
            audioBalasanNPC = "audio_level3_balasan"
        });

        TambahLevel(new LevelData
        {
            level = GameLevel.Level4_SlurryPump,
            namaLevel = "Level 4 - Slurry Pump",
            deskripsiQuest = "Tekan tombol 4 dan atur flow rate slurry ke 450 meter kubik per jam.",
            nomorTombolDCS = 4,
            butuhVoiceReport = true,
            kataKunciVoice = "slurry pump aktif",
            kataKunciVoiceAwal = "",
            laporanVoiceAwal = "",
            laporanVoiceLengkap = "Field, slurry pump aktif. Flow rate sudah diset empat ratus lima puluh meter kubik per jam.",
            audioBalasanNPC = "audio_level4_balasan",
            targetFlowRate = 450f
        });

        TambahLevel(new LevelData
        {
            level = GameLevel.Level5_SteamValve,
            namaLevel = "Level 5 - Steam Valve",
            deskripsiQuest = "Buka katup steam dan laporkan kenaikan suhu pre-heater.",
            nomorTombolDCS = 5,
            butuhVoiceReport = true,
            kataKunciVoice = "katup steam terbuka",
            kataKunciVoiceAwal = "",
            laporanVoiceAwal = "",
            laporanVoiceLengkap = "DCS, katup steam terbuka. Suhu pre-heater sudah naik ke rentang operasi.",
            audioBalasanNPC = "audio_level5_balasan",
            targetSuhu = 190f
        });

        TambahLevel(new LevelData
        {
            level = GameLevel.Level6_AcidInjection,
            namaLevel = "Level 6 - Acid Injection",
            deskripsiQuest = "Aktifkan injeksi asam, capai rasio 350 kilogram per ton, lalu laporkan.",
            nomorTombolDCS = 6,
            butuhVoiceReport = true,
            kataKunciVoice = "acid aktif",
            kataKunciVoiceAwal = "",
            laporanVoiceAwal = "",
            laporanVoiceLengkap = "Field, acid injection aktif. Rasio asam tiga ratus lima puluh kilogram per ton dan pH turun ke satu koma nol.",
            audioBalasanNPC = "audio_level6_balasan",
            targetAcidRatio = 350f,
            targetPH = 1.0f
        });

        TambahLevel(new LevelData
        {
            level = GameLevel.Level7_Autoclave,
            namaLevel = "Level 7 - Autoclave",
            deskripsiQuest = "Monitor autoclave dan laporkan suhu, tekanan, serta RPM agitator.",
            nomorTombolDCS = 7,
            butuhVoiceReport = true,
            kataKunciVoice = "suhu 250",
            kataKunciVoiceAwal = "",
            laporanVoiceAwal = "",
            laporanVoiceLengkap = "DCS, suhu dua ratus lima puluh dua derajat, tekanan empat puluh tujuh koma lima atmosfer, dan agitator enam puluh RPM.",
            audioBalasanNPC = "audio_level7_balasan",
            targetSuhu = 252f,
            targetTekanan = 47.5f,
            targetRPM = 60f
        });

        TambahLevel(new LevelData
        {
            level = GameLevel.Level8_Monitoring,
            namaLevel = "Level 8 - Monitoring Ketat",
            deskripsiQuest = "Stabilkan suhu, tekanan, dan RPM autoclave dari DCS, lalu laporkan kondisi aman.",
            nomorTombolDCS = 8,
            butuhVoiceReport = true,
            kataKunciVoice = "parameter stabil",
            kataKunciVoiceAwal = "",
            laporanVoiceAwal = "",
            laporanVoiceLengkap = "DCS, parameter stabil. Suhu, tekanan, dan RPM kembali dalam batas SOP.",
            audioBalasanNPC = "audio_level8_balasan",
            targetSuhu = 252f,
            targetTekanan = 47.5f,
            targetRPM = 60f
        });

        TambahLevel(new LevelData
        {
            level = GameLevel.Level9_FlashVessel,
            namaLevel = "Level 9 - Flash Vessel",
            deskripsiQuest = "Buka letdown valve, amati flash vessel, dan pastikan tekanan turun stabil.",
            nomorTombolDCS = 9,
            butuhVoiceReport = true,
            kataKunciVoice = "flash vessel normal",
            kataKunciVoiceAwal = "",
            laporanVoiceAwal = "",
            laporanVoiceLengkap = "DCS, flash vessel normal. Tekanan turun ke dua belas atmosfer dan pelepasan uap aman.",
            audioBalasanNPC = "audio_level9_balasan",
            targetTekanan = 12f
        });

        TambahLevel(new LevelData
        {
            level = GameLevel.Level10_CCD,
            namaLevel = "Level 10 - CCD Activation",
            deskripsiQuest = "Aktifkan rangkaian CCD, amati pemisahan padat-cair, lalu laporkan.",
            nomorTombolDCS = 10,
            butuhVoiceReport = true,
            kataKunciVoice = "ccd aktif",
            kataKunciVoiceAwal = "",
            laporanVoiceAwal = "",
            laporanVoiceLengkap = "DCS, sistem CCD aktif. Pemisahan padat dan cair berjalan stabil.",
            audioBalasanNPC = "audio_level10_balasan"
        });

        TambahLevel(new LevelData
        {
            level = GameLevel.Level11_MHP,
            namaLevel = "Level 11 - Neutralization & MHP Sampling",
            deskripsiQuest = "Netralisasi larutan hasil CCD, bentuk MHP, lalu ambil sampel produk.",
            nomorTombolDCS = 11,
            butuhVoiceReport = true,
            kataKunciVoice = "mhp terbentuk",
            kataKunciVoiceAwal = "",
            laporanVoiceAwal = "",
            laporanVoiceLengkap = "DCS, MHP terbentuk. pH netralisasi stabil dan sampel produk siap.",
            audioBalasanNPC = "audio_level11_balasan",
            targetPH = 5.5f
        });

        TambahLevel(new LevelData
        {
            level = GameLevel.Level12_TailingDischarge,
            namaLevel = "Level 12 - Tailing Neutralization & Filter Press",
            deskripsiQuest = "Netralisasi tailing, jalankan filter press, dan pastikan cake siap dikirim ke dry stack.",
            nomorTombolDCS = 12,
            butuhVoiceReport = true,
            kataKunciVoice = "limbah dialirkan",
            kataKunciVoiceAwal = "",
            laporanVoiceAwal = "",
            laporanVoiceLengkap = "DCS, limbah tailing sudah dinetralkan. Filter press selesai dan cake siap ke dry stack.",
            audioBalasanNPC = "audio_level12_balasan",
            targetPH = 7.5f
        });

        TambahLevel(new LevelData
        {
            level = GameLevel.Level13_TailingWaste,
            namaLevel = "Level 13 - Dry Stack Tailing",
            deskripsiQuest = "Polishing pH tailing ke 8.5, tekan cake sampai moisture di bawah 25%, lalu amankan ke dry stack.",
            nomorTombolDCS = 13,
            butuhVoiceReport = true,
            kataKunciVoice = "tailing aman",
            kataKunciVoiceAwal = "",
            laporanVoiceAwal = "",
            laporanVoiceLengkap = "DCS, netralisasi berhasil. pH delapan koma lima dan tailing aman di dry stack.",
            audioBalasanNPC = "audio_level13_balasan",
            targetPH = 8.5f
        });

        TambahLevel(new LevelData
        {
            level = GameLevel.Level14_Emergency,
            namaLevel = "Level 14 - Darurat K3",
            deskripsiQuest = "Deteksi kebocoran/pressure critical, laporkan emergency via HT, lalu tekan tombol ESD merah.",
            nomorTombolDCS = 14,
            butuhVoiceReport = true,
            kataKunciVoice = "emergency",
            kataKunciVoiceAwal = "",
            laporanVoiceAwal = "",
            laporanVoiceLengkap = "Emergency, emergency. Kebocoran terdeteksi di sektor proses. Tekanan kritis, aktifkan ESD dan evakuasi.",
            audioBalasanNPC = "audio_level14_balasan"
        });

        Log("INIT", $"Berhasil memuat data {_dataLevel.Count} level.", "cyan");
    }

    private void TambahLevel(LevelData data)
    {
        _dataLevel[data.level] = data;
    }

    private void IsiLaporanHtDinamisJikaKosong()
    {
        if (_laporanHtPerLevel == null)
            _laporanHtPerLevel = new List<LaporanHtDinamis>();

        for (int i = 0; i <= 14; i++)
        {
            GameLevel level = (GameLevel)i;
            bool sudahAda = false;
            for (int j = 0; j < _laporanHtPerLevel.Count; j++)
            {
                if (_laporanHtPerLevel[j] != null && _laporanHtPerLevel[j].level == level)
                {
                    sudahAda = true;
                    break;
                }
            }

            if (!sudahAda)
                _laporanHtPerLevel.Add(BuatLaporanHtDefault(level));
        }
    }

    private LaporanHtDinamis BuatLaporanHtDefault(GameLevel level)
    {
        var data = new LaporanHtDinamis { level = level };
        switch (level)
        {
            case GameLevel.Level1_APD:
                data.kataKunciVoice = "apd lengkap";
                data.laporanVoiceLengkap = "DCS, APD lengkap. Operator siap masuk ke area proses.";
                data.aliasTambahanPerBaris = "ppe complete\nsafety gear complete";
                break;
            case GameLevel.Level2_DCSPrep:
                data.kataKunciVoice = "siapkan area";
                data.laporanVoiceLengkap = "Field, siapkan area crusher. Operator DCS standby memulai operasi.";
                data.aliasTambahanPerBaris = "prepare area\ncrusher area ready";
                break;
            case GameLevel.Level3_OreSlurry:
                data.kataKunciVoiceAwal = "jalankan alur ore";
                data.kataKunciVoice = "ore masuk";
                data.laporanVoiceAwal = "Field, jalankan alur ore ke slurry tank. Operator DCS standby monitoring.";
                data.laporanVoiceLengkap = "DCS, ore sudah masuk ke slurry tank. Level cairan lima puluh persen dan proses aman.";
                data.aliasTambahanPerBaris = "start ore flow\nstart ore line\nore ready\nore in slurry tank";
                break;
            case GameLevel.Level4_SlurryPump:
                data.kataKunciVoice = "slurry pump aktif";
                data.laporanVoiceLengkap = "Field, slurry pump aktif. Flow rate sudah diset empat ratus lima puluh meter kubik per jam.";
                data.aliasTambahanPerBaris = "slurry pump active\nflow set";
                break;
            case GameLevel.Level5_SteamValve:
                data.kataKunciVoice = "katup steam terbuka";
                data.laporanVoiceLengkap = "DCS, katup steam terbuka. Suhu pre-heater sudah naik ke rentang operasi.";
                data.aliasTambahanPerBaris = "steam valve open\nheater temperature up";
                break;
            case GameLevel.Level6_AcidInjection:
                data.kataKunciVoice = "acid aktif";
                data.laporanVoiceLengkap = "Field, acid injection aktif. Rasio asam tiga ratus lima puluh kilogram per ton dan pH turun ke satu koma nol.";
                data.aliasTambahanPerBaris = "acid injection active\nacid ratio set";
                break;
            case GameLevel.Level7_Autoclave:
                data.kataKunciVoice = "suhu 250";
                data.laporanVoiceLengkap = "DCS, suhu dua ratus lima puluh dua derajat, tekanan empat puluh tujuh koma lima atmosfer, dan agitator enam puluh RPM.";
                data.aliasTambahanPerBaris = "autoclave stable\ntemperature pressure rpm";
                break;
            case GameLevel.Level8_Monitoring:
                data.kataKunciVoice = "parameter stabil";
                data.laporanVoiceLengkap = "DCS, parameter stabil. Suhu, tekanan, dan RPM kembali dalam batas SOP.";
                data.aliasTambahanPerBaris = "parameter stable\noperation stable";
                break;
            case GameLevel.Level9_FlashVessel:
                data.kataKunciVoice = "flash vessel normal";
                data.laporanVoiceLengkap = "DCS, flash vessel normal. Tekanan turun ke dua belas atmosfer dan pelepasan uap aman.";
                data.aliasTambahanPerBaris = "flash vessel normal\npressure release safe";
                break;
            case GameLevel.Level10_CCD:
                data.kataKunciVoice = "ccd aktif";
                data.laporanVoiceLengkap = "DCS, sistem CCD aktif. Pemisahan padat dan cair berjalan stabil.";
                data.aliasTambahanPerBaris = "ccd active\nseparation started";
                break;
            case GameLevel.Level11_MHP:
                data.kataKunciVoice = "mhp terbentuk";
                data.laporanVoiceLengkap = "DCS, MHP terbentuk. pH netralisasi stabil dan sampel produk siap.";
                data.aliasTambahanPerBaris = "mhp formed\nprecipitation normal\nmhp sample ready";
                break;
            case GameLevel.Level12_TailingDischarge:
                data.kataKunciVoice = "limbah dialirkan";
                data.laporanVoiceLengkap = "DCS, limbah tailing sudah dinetralkan. Filter press selesai dan cake siap ke dry stack.";
                data.aliasTambahanPerBaris = "tailing discharge safe\nwaste transferred\nfilter press complete";
                break;
            case GameLevel.Level13_TailingWaste:
                data.kataKunciVoice = "tailing aman";
                data.laporanVoiceLengkap = "DCS, netralisasi berhasil. pH delapan koma lima dan tailing aman di dry stack.";
                data.aliasTambahanPerBaris = "tailing safe\npH 8.5\ndry stack aman\ndry stack safe";
                break;
            case GameLevel.Level14_Emergency:
                data.kataKunciVoice = "emergency";
                data.laporanVoiceLengkap = "Emergency, emergency. Kebocoran terdeteksi di sektor proses. Semua personel segera evakuasi.";
                data.aliasTambahanPerBaris = "emergency evacuation\nleak detected";
                break;
        }

        return data;
    }

    private void TerapkanLaporanHtDinamis()
    {
        if (!_gunakanLaporanHtDinamis || _laporanHtPerLevel == null)
            return;

        for (int i = 0; i < _laporanHtPerLevel.Count; i++)
        {
            LaporanHtDinamis laporan = _laporanHtPerLevel[i];
            if (laporan == null || !_dataLevel.TryGetValue(laporan.level, out var data))
                continue;

            if (!string.IsNullOrWhiteSpace(laporan.kataKunciVoice))
                data.kataKunciVoice = laporan.kataKunciVoice;

            if (!string.IsNullOrWhiteSpace(laporan.kataKunciVoiceAwal))
                data.kataKunciVoiceAwal = laporan.kataKunciVoiceAwal;

            if (!string.IsNullOrWhiteSpace(laporan.laporanVoiceAwal))
                data.laporanVoiceAwal = laporan.laporanVoiceAwal;

            if (!string.IsNullOrWhiteSpace(laporan.laporanVoiceLengkap))
                data.laporanVoiceLengkap = laporan.laporanVoiceLengkap;
        }
    }

    private void SetLevel3Phase(Level3Phase phase)
    {
        if (_level3Phase == phase)
            return;

        _level3Phase = phase;
        OnLevel3PhaseChanged?.Invoke(_level3Phase);
    }

    private void SetLevel4Phase(Level4Phase phase)
    {
        if (_level4Phase == phase)
            return;

        _level4Phase = phase;
        OnLevel4PhaseChanged?.Invoke(_level4Phase);
        Log("LEVEL 4", $"Phase: {_level4Phase}", "cyan");
    }

    /// <summary>Untuk dipanggil oleh Level4SlurryPumpController dari luar.</summary>
    public void NotifyLevel4PhaseAdvance(Level4Phase phase) => SetLevel4Phase(phase);

    /// <summary>
    /// Dipanggil Level4SlurryPumpController setelah seluruh sequence Level 4 selesai
    /// (lapor flow → observasi pump → lapor alir → observasi preheater → kembali ke DCS).
    /// </summary>
    public void NotifyLevel4Selesai()
    {
        if (_currentLevel != GameLevel.Level4_SlurryPump)
            return;

        // Mark voice report sebagai sudah dilakukan supaya CekKondisiLevelSelesai pass.
        _voiceReportSudahDilakukan = true;
        SetLevel4Phase(Level4Phase.Selesai);
        CekKondisiLevelSelesai();
    }

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
        _dcsSudahDilihat = false;
        _level3OreSudahMasukSlurry = false;
        _flowRateLevel4Dikonfirmasi = false;
        _level5PreheaterReady = false;
        _level6AcidComplete = false;
        _level7AutoclaveInspected = false;
        _level10CcdComplete = false;
        _level11MhpComplete = false;
        _level12TailingFilterComplete = false;
        _level13DryStackComplete = false;
        _level14EsdPressed = false;
        _tundaTransisiLevel3 = false;
        _waktuMulaiLevel = Time.time;

        var data = _dataLevel[level];
        Log("LEVEL MULAI", $"<b>{data.namaLevel}</b>\nQuest: {data.deskripsiQuest}", "yellow");

        // PENTING: fire OnLevelStarted DULU sebelum SetLevel3Phase/SetLevel4Phase.
        // Subscriber (PlayerHUD, dll) menyetel _levelAktif di sini. Kalau phase event
        // fire duluan, subscriber masih anggap level sebelumnya → quest text tidak update.
        OnLevelStarted?.Invoke(level);

        SetLevel3Phase(level == GameLevel.Level3_OreSlurry ? Level3Phase.MenungguTombolDcs : Level3Phase.Idle);
        SetLevel4Phase(level == GameLevel.Level4_SlurryPump ? Level4Phase.MenungguTombolDcs : Level4Phase.Idle);

        if (data.nomorTombolDCS > 0)
            OnDCSButtonShouldHighlight?.Invoke(data.nomorTombolDCS);
    }

    private void ResetApdUntukAreaDcsJikaPerlu(GameLevel level)
    {
        if (level != GameLevel.Level2_DCSPrep && level != GameLevel.Level3_OreSlurry)
            return;

        if (_phaseManager == null)
            _phaseManager = FindFirstObjectByType<PhaseManager>();

        _phaseManager?.ResetKeAreaDcsSaja();
    }

    public void SelesaikanLevel(GameLevel level)
    {
        if (_currentLevel != level || !_levelSedangBerjalan)
            return;

        if (level == GameLevel.Level3_OreSlurry)
            SetLevel3Phase(Level3Phase.Selesai);

        float waktuSelesai = Time.time - _waktuMulaiLevel;
        float skor = HitungSkorLevel(level, waktuSelesai);
        _skorPerLevel[(int)level] = skor;
        _levelSedangBerjalan = false;

        Log("LEVEL SELESAI", $"<b>{_dataLevel[level].namaLevel}</b> selesai! Skor: <b>{skor:F0}/100</b>", "green");
        OnLevelComplete?.Invoke(level, (int)skor);

        int levelBerikutnya = (int)level + 1;
        if (levelBerikutnya <= 14)
            StartCoroutine(TransisiKeLevel(level, (GameLevel)levelBerikutnya));
        else
            SelesaikanSemua();
    }

    private IEnumerator TransisiKeLevel(GameLevel levelSebelum, GameLevel levelBerikutnya)
    {
        float durasi = GetDurasiTransisi(levelSebelum);
        OnLevelTransitionRequested?.Invoke(levelSebelum, levelBerikutnya, durasi);
        yield return new WaitForSeconds(durasi);
        MulaiLevel(levelBerikutnya);
    }

    private void SelesaikanSemua()
    {
        _skorTotal = 0f;
        for (int i = 0; i < _skorPerLevel.Length; i++)
            _skorTotal += _skorPerLevel[i];

        _skorTotal /= 15f;
        Log("SIMULASI SELESAI",
            $"Semua level selesai! Nilai akhir: <b>{_skorTotal:F1}/100</b>. " +
            (_skorTotal >= 70f ? "Lulus dan sertifikat virtual diraih." : "Belum lulus, silakan ulangi simulasi."),
            "green");
    }

    public void OnDCSTombolDitekan(int nomorTombol)
    {
        TryOnDCSTombolDitekan(nomorTombol);
    }

    public bool TryOnDCSTombolDitekan(int nomorTombol)
    {
        if (!_dataLevel.ContainsKey(_currentLevel))
            return false;

        var data = _dataLevel[_currentLevel];
        if (_currentLevel == GameLevel.Level2_DCSPrep && !_dcsSudahDilihat)
        {
            Log("URUTAN", "Lihat area DCS dulu sebelum menekan tombol DCS 2.", "orange");
            return false;
        }

        if (data.nomorTombolDCS != nomorTombol)
        {
            Log("PERINGATAN",
                $"Tombol {nomorTombol} bukan tombol aktif sekarang. Harusnya tekan tombol {data.nomorTombolDCS}.",
                "orange");
            return false;
        }

        _dcsTombolSudahDitekan = true;
        if (_currentLevel == GameLevel.Level3_OreSlurry)
            SetLevel3Phase(Level3Phase.MenungguLaporanAwal);
        if (_currentLevel == GameLevel.Level4_SlurryPump &&
            (_level4Phase == Level4Phase.Idle || _level4Phase == Level4Phase.MenungguTombolDcs))
        {
            SetLevel4Phase(Level4Phase.AturFlowRate);
        }

        OnDCSButtonPressed?.Invoke(nomorTombol);
        Log("DCS", $"Tombol {nomorTombol} ditekan untuk level <b>{data.namaLevel}</b>.", "cyan");

        // Level 4 menggunakan flow multi-phase, jangan auto-selesaikan saat tombol ditekan.
        if (_currentLevel != GameLevel.Level4_SlurryPump)
            CekKondisiLevelSelesai();
        return true;
    }

    public bool OnVoiceKeywordTerdeteksi(string keyword)
    {
        if (!_dataLevel.ContainsKey(_currentLevel))
            return false;

        var data = _dataLevel[_currentLevel];
        if (_currentLevel == GameLevel.Level1_APD)
            return HandleVoiceLevel1(data, keyword);

        if (_currentLevel == GameLevel.Level3_OreSlurry)
            return HandleVoiceLevel3(data, keyword);

        if (_currentLevel == GameLevel.Level4_SlurryPump)
            return HandleVoiceLevel4(data, keyword);

        if (_currentLevel == GameLevel.Level2_DCSPrep && !_dcsSudahDilihat)
        {
            Log("VOICE", "Urutan belum benar. Lihat area DCS dulu sebelum kirim laporan HT.", "orange");
            return false;
        }

        if (data.nomorTombolDCS > 0 && !_dcsTombolSudahDitekan)
        {
            Log("VOICE", $"Urutan belum benar. Tekan tombol DCS {data.nomorTombolDCS} dulu sebelum laporan HT.", "orange");
            return false;
        }

        if (_currentLevel == GameLevel.Level5_SteamValve && !_level5PreheaterReady)
        {
            Log("VOICE", "Pre-heater belum mencapai suhu operasi. Buka katup steam sampai suhu minimal 180 C dulu.", "orange");
            return false;
        }

        if (_currentLevel == GameLevel.Level6_AcidInjection && !_level6AcidComplete)
        {
            Log("VOICE", "Acid injection belum sesuai SOP. Capai 350 kg/ton dan pH 1.0 dulu.", "orange");
            return false;
        }

        if (_currentLevel == GameLevel.Level7_Autoclave && !_level7AutoclaveInspected)
        {
            Log("VOICE", "Autoclave belum selesai diinspeksi. Cek gauge suhu, tekanan, dan RPM dulu.", "orange");
            return false;
        }

        if (_currentLevel == GameLevel.Level8_Monitoring && !ParameterAutoklaveSesuaiSOP())
        {
            Log("VOICE", "Parameter belum stabil. Koreksi suhu, tekanan, dan RPM sampai masuk batas SOP dulu.", "orange");
            return false;
        }

        if (_currentLevel == GameLevel.Level9_FlashVessel &&
            Mathf.Abs(_tekananSaatIni - data.targetTekanan) > 1.5f)
        {
            Log("VOICE", "Flash vessel belum stabil. Tunggu tekanan turun ke 12 atm dulu sebelum laporan HT.", "orange");
            return false;
        }

        if (_currentLevel == GameLevel.Level10_CCD && !_level10CcdComplete)
        {
            Log("VOICE", "CCD belum stabil. Tunggu pemisahan padat-cair selesai dulu sebelum laporan HT.", "orange");
            return false;
        }

        if (_currentLevel == GameLevel.Level11_MHP && !_level11MhpComplete)
        {
            Log("VOICE", "MHP belum siap. Tunggu neutralization dan sampling selesai dulu sebelum laporan HT.", "orange");
            return false;
        }

        if (_currentLevel == GameLevel.Level12_TailingDischarge && !_level12TailingFilterComplete)
        {
            Log("VOICE", "Tailing treatment belum selesai. Tunggu netralisasi dan filter press selesai dulu sebelum laporan HT.", "orange");
            return false;
        }

        if (_currentLevel == GameLevel.Level13_TailingWaste && !_level13DryStackComplete)
        {
            Log("VOICE", "Dry stack belum aman. Tunggu pH 8.5, moisture cake <25%, dan penyimpanan dry stack selesai dulu.", "orange");
            return false;
        }

        if (!VoiceReportCocok(data, keyword))
        {
            Log("VOICE", $"Ucapan '{keyword}' belum cocok untuk level ini.", "orange");
            return false;
        }

        _voiceReportSudahDilakukan = true;
        OnVoiceReportAccepted?.Invoke(keyword);
        Log("VOICE REPORT", $"Laporan diterima: '<i>{keyword}</i>'", "cyan");
        CekKondisiLevelSelesai();
        return true;
    }

    private bool HandleVoiceLevel1(LevelData data, string keyword)
    {
        if (_phaseManager == null)
            _phaseManager = FindFirstObjectByType<PhaseManager>();

        if (_phaseManager != null && !_phaseManager.APDLengkapSempurna)
        {
            Log("VOICE", "APD belum lengkap. Walkie Talkie belum boleh menyelesaikan Level 1.", "orange");
            return false;
        }

        if (string.IsNullOrWhiteSpace(keyword))
        {
            Log("VOICE", $"Ucapan '{keyword}' belum cocok untuk Level 1.", "orange");
            return false;
        }

        if (!VoiceReportCocok(data, keyword))
            Log("VOICE", $"Level 1 menerima frasa WT non-kosong sebagai cek radio: '<i>{keyword}</i>'", "cyan");

        _voiceReportSudahDilakukan = true;
        OnVoiceReportAccepted?.Invoke(keyword);
        Log("VOICE REPORT", $"Laporan APD Level 1 diterima: '<i>{keyword}</i>'", "cyan");
        return true;
    }

    private bool HandleVoiceLevel3(LevelData data, string keyword)
    {
        switch (_level3Phase)
        {
            case Level3Phase.MenungguLaporanAwal:
                if (!VoiceReportCocokDenganCadangan(data, keyword, data.laporanVoiceAwal, data.kataKunciVoiceAwal))
                {
                    Log("VOICE", $"Ucapan '{keyword}' belum cocok untuk laporan awal Level 3.", "orange");
                    return false;
                }

                SetLevel3Phase(Level3Phase.LaporanAwalDiterima);
                OnVoiceReportAccepted?.Invoke(keyword);
                Log("VOICE REPORT", $"Laporan awal Level 3 diterima: '<i>{keyword}</i>'", "cyan");
                return true;

            case Level3Phase.SiapLaporanAkhir:
                if (!VoiceReportCocokDenganCadangan(data, keyword, data.laporanVoiceLengkap, data.kataKunciVoice))
                {
                    Log("VOICE", $"Ucapan '{keyword}' belum cocok untuk laporan akhir Level 3.", "orange");
                    return false;
                }

                _voiceReportSudahDilakukan = true;
                OnVoiceReportAccepted?.Invoke(keyword);
                Log("VOICE REPORT", $"Laporan akhir Level 3 diterima: '<i>{keyword}</i>'", "cyan");
                // Fire event supaya panel pilihan bisa muncul SEBELUM transisi otomatis.
                OnLevel3LaporanAkhirDiterima?.Invoke();
                // Hanya selesaikan otomatis kalau tidak ada handler yang menahan transisi.
                if (!_tundaTransisiLevel3)
                    CekKondisiLevelSelesai();
                return true;

            case Level3Phase.ObservasiLapangan:
            case Level3Phase.LaporanAwalDiterima:
                Log("VOICE", "Level 3 sedang proses teleport atau observasi. Tunggu slurry mencapai 25% sebelum laporan akhir.", "orange");
                return false;

            default:
                Log("VOICE", "Level 3 belum berada pada tahap yang menerima laporan HT.", "orange");
                return false;
        }
    }

    /// <summary>
    /// Handler voice Level 4 — single laporan setelah cairan masuk preheater.
    ///   Phase MenungguLaporanFlow → "slurry pump aktif" → KembaliKeDcs
    ///   Phase lain → reject (player harus selesaikan langkah dulu).
    /// </summary>
    private bool HandleVoiceLevel4(LevelData data, string keyword)
    {
        // Validasi sequencing dasar.
        if (data.nomorTombolDCS > 0 && !_dcsTombolSudahDitekan)
        {
            Log("VOICE", $"Tekan tombol DCS {data.nomorTombolDCS} dulu sebelum laporan HT.", "orange");
            return false;
        }

        switch (_level4Phase)
        {
            case Level4Phase.MenungguTombolDcs:
            case Level4Phase.AturFlowRate:
                Log("VOICE", "Atur flow rate ke 450 m3/h dulu sebelum lapor HT.", "orange");
                return false;

            // Laporan AWAL: "slurry pump aktif" — diucapkan di DCS setelah flow rate 450 tercapai.
            // Setelah diterima → fade & teleport ke field untuk observasi liquid mengalir.
            case Level4Phase.MenungguLaporanFlow:
                {
                    string frasaUtama = "Field, slurry pump aktif. Flow rate sudah diset empat ratus lima puluh meter kubik per jam.";
                    string aliasPendek = string.IsNullOrEmpty(data.kataKunciVoice) ? "slurry pump aktif" : data.kataKunciVoice;
                    bool match = VoiceReportCocokDenganCadangan(data, keyword, frasaUtama, aliasPendek);
                    if (!match)
                    {
                        Log("VOICE", $"Ucapan '{keyword}' belum cocok untuk laporan awal Level 4. Sebut 'slurry pump aktif'.", "orange");
                        return false;
                    }
                    OnVoiceReportAccepted?.Invoke(keyword);
                    Log("VOICE REPORT", $"Laporan AWAL Level 4 diterima: '<i>{keyword}</i>'. Teleport ke field.", "cyan");
                    SetLevel4Phase(Level4Phase.ObservasiPump);
                    return true;
                }

            case Level4Phase.ObservasiPump:
                Log("VOICE", "Tunggu liquid mengalir sampai pre-heater dulu sebelum lapor.", "orange");
                return false;

            case Level4Phase.ObservasiPreheater:
                Log("VOICE", "Liquid baru saja sampai. Tunggu HUD prompt lapor HT akhir.", "orange");
                return false;

            // Laporan AKHIR: "cairan sudah di preheater" / "slurry mengalir ke preheater"
            // Setelah diterima → tunggu balasan audio NPC → fade balik ke DCS untuk Level 5.
            case Level4Phase.MenungguLaporanAkhir:
                {
                    string frasaUtama = "DCS, cairan slurry sudah masuk pre-heater. Operasi pumping berjalan normal.";
                    bool match = VoiceReportCocokDenganCadangan(data, keyword, frasaUtama, "cairan sudah di preheater");
                    if (!match)
                        match = VoiceReportCocokDenganCadangan(data, keyword, frasaUtama, "slurry sampai preheater");
                    if (!match)
                        match = VoiceReportCocokDenganCadangan(data, keyword, frasaUtama, "slurry masuk preheater");
                    if (!match)
                        match = VoiceReportCocokDenganCadangan(data, keyword, frasaUtama, "preheater normal");
                    if (!match)
                    {
                        Log("VOICE", $"Ucapan '{keyword}' belum cocok untuk laporan akhir Level 4. Sebut 'cairan sudah di preheater'.", "orange");
                        return false;
                    }
                    OnVoiceReportAccepted?.Invoke(keyword);
                    Log("VOICE REPORT", $"Laporan AKHIR Level 4 diterima: '<i>{keyword}</i>'. Kembali ke DCS.", "cyan");
                    SetLevel4Phase(Level4Phase.KembaliKeDcs);
                    return true;
                }

            case Level4Phase.KembaliKeDcs:
            case Level4Phase.Selesai:
                Log("VOICE", "Sedang transisi.", "orange");
                return false;

            default:
                return false;
        }
    }

    public void NotifyDcsViewed()
    {
        if (_currentLevel != GameLevel.Level2_DCSPrep || _dcsSudahDilihat)
            return;

        _dcsSudahDilihat = true;
        OnDCSViewConfirmed?.Invoke(_currentLevel);
        Log("DCS", "Pemain telah melihat area DCS.", "cyan");
    }

    public bool TryGetLevelData(GameLevel level, out LevelData data)
    {
        return _dataLevel.TryGetValue(level, out data);
    }

    public void NotifyLevel3FieldSequenceStarted()
    {
        if (_currentLevel != GameLevel.Level3_OreSlurry)
            return;

        if (_level3Phase != Level3Phase.LaporanAwalDiterima)
            return;

        SetLevel3Phase(Level3Phase.ObservasiLapangan);
        Log("LEVEL 3", "Teleport ke area crusher/slurry selesai. Observasi lapangan dimulai.", "cyan");
    }

    public void NotifyLevel3OreReachedSlurry()
    {
        if (_currentLevel != GameLevel.Level3_OreSlurry)
            return;

        if (_level3Phase != Level3Phase.ObservasiLapangan)
            return;

        if (_level3OreSudahMasukSlurry)
            return;

        _level3OreSudahMasukSlurry = true;
        OnLevel3OreReachedSlurry?.Invoke();
        Log("LEVEL 3", "Ore/laterit sudah mencapai slurry tank. Lanjut amati pengisian tank sampai 25%.", "green");
    }

    public void NotifyLevel3SlurryReady(float percent)
    {
        if (_currentLevel != GameLevel.Level3_OreSlurry)
            return;

        if (_level3Phase != Level3Phase.ObservasiLapangan)
            return;

        if (!_level3OreSudahMasukSlurry)
        {
            Log("LEVEL 3", "Slurry belum boleh dinilai 25% karena ore belum terdeteksi masuk ke tank.", "orange");
            return;
        }

        if (percent < 25f)
            return;

        SetLevel3Phase(Level3Phase.SiapLaporanAkhir);
        Log("LEVEL 3", $"Slurry mencapai {percent:F0}%. Laporan akhir HT sekarang diwajibkan.", "green");
    }

    public string GetLaporanVoiceDisplay(GameLevel level)
    {
        if (!_dataLevel.TryGetValue(level, out var data))
            return string.Empty;

        if (level == GameLevel.Level3_OreSlurry)
        {
            if (_level3Phase == Level3Phase.MenungguLaporanAwal ||
                _level3Phase == Level3Phase.LaporanAwalDiterima)
                return data.laporanVoiceAwal;

            if (_level3Phase == Level3Phase.SiapLaporanAkhir || _level3Phase == Level3Phase.Selesai)
                return data.laporanVoiceLengkap;
        }

        if (level == GameLevel.Level4_SlurryPump)
        {
            if (_level4Phase == Level4Phase.MenungguLaporanFlow)
                return data.laporanVoiceLengkap; // "Field, slurry pump aktif. Flow rate..."
        }

        return string.IsNullOrWhiteSpace(data.laporanVoiceLengkap) ? data.kataKunciVoice : data.laporanVoiceLengkap;
    }

    public void SetFlowRate(float nilai)
    {
        _flowRateSaatIni = Mathf.Clamp(nilai, 0f, 600f);
        CekParameterLevel4();
    }

    public void SetAcidRatio(float nilai)
    {
        _acidRatioSaatIni = Mathf.Clamp(nilai, 0f, 500f);
        CekParameterLevel6();
    }

    public void SetSuhu(float nilai) => _suhuSaatIni = nilai;
    public void SetTekanan(float nilai) => _tekananSaatIni = nilai;
    public void SetRPM(float nilai) => _rpmSaatIni = nilai;
    public void SetPH(float nilai)
    {
        _phSaatIni = nilai;
        CekParameterLevel6();
    }

    public float FlowRate => _flowRateSaatIni;
    public float AcidRatio => _acidRatioSaatIni;
    public float Suhu => _suhuSaatIni;
    public float Tekanan => _tekananSaatIni;
    public float RPM => _rpmSaatIni;
    public float PH => _phSaatIni;
    public GameLevel CurrentLevel => _currentLevel;
    public bool LevelAktif => _levelSedangBerjalan;
    public Level3Phase CurrentLevel3Phase => _level3Phase;

    /// <summary>
    /// Set true untuk menahan transisi otomatis Level 3 → 4 setelah laporan akhir diterima.
    /// Panel pilihan akan memanggil ini sebelum show, lalu LanjutkanTransisiLevel3() saat player pilih "Lanjut".
    /// </summary>
    public void TundaTransisiLevel3(bool tunda) { _tundaTransisiLevel3 = tunda; }

    /// <summary>
    /// Dipanggil oleh panel pilihan saat player memilih "Lanjut ke tahap berikutnya".
    /// Melanjutkan transisi yang ditunda.
    /// </summary>
    public void LanjutkanTransisiLevel3()
    {
        _tundaTransisiLevel3 = false;
        CekKondisiLevelSelesai();
    }
    public Level4Phase CurrentLevel4Phase => _level4Phase;
    public bool Level3OreSudahMasukSlurry => _level3OreSudahMasukSlurry;

    public void NotifyLevel5PreheaterReady()
    {
        if (_currentLevel != GameLevel.Level5_SteamValve)
            return;

        _level5PreheaterReady = true;
        Log("LEVEL 5", "Pre-heater reached operating temperature. Final HT report is now allowed.", "green");
    }

    public void NotifyLevel6AcidInjectionComplete()
    {
        if (_currentLevel != GameLevel.Level6_AcidInjection)
            return;

        _level6AcidComplete = true;
        Log("LEVEL 6", "Acid ratio and pH reached SOP. Final HT report is now allowed.", "green");
    }

    public void NotifyLevel7AutoclaveInspectionComplete()
    {
        if (_currentLevel != GameLevel.Level7_Autoclave)
            return;

        _level7AutoclaveInspected = true;
        Log("LEVEL 7", "Autoclave gauges inspected. Final HT report is now allowed.", "green");
    }

    public void NotifyLevel10CCDComplete()
    {
        if (_currentLevel != GameLevel.Level10_CCD)
            return;

        _level10CcdComplete = true;
        Log("LEVEL 10", "CCD separation stable. Final HT report is now allowed.", "green");
    }

    public void NotifyLevel11MHPComplete()
    {
        if (_currentLevel != GameLevel.Level11_MHP)
            return;

        _level11MhpComplete = true;
        Log("LEVEL 11", "MHP sample ready. Final HT report is now allowed.", "green");
    }

    public void NotifyLevel12TailingFilterComplete()
    {
        if (_currentLevel != GameLevel.Level12_TailingDischarge)
            return;

        _level12TailingFilterComplete = true;
        Log("LEVEL 12", "Tailing neutralization and filter press complete. Final HT report is now allowed.", "green");
    }

    public void NotifyLevel13DryStackComplete()
    {
        if (_currentLevel != GameLevel.Level13_TailingWaste)
            return;

        _level13DryStackComplete = true;
        Log("LEVEL 13", "Dry stack tailing secured. Final HT report is now allowed.", "green");
    }

    public void NotifyLevel14EsdPressed()
    {
        if (_currentLevel != GameLevel.Level14_Emergency)
            return;

        _level14EsdPressed = true;
        SetSuhu(90f);
        SetTekanan(1.5f);
        SetRPM(0f);
        Log("LEVEL 14", "ESD aktif. Menunggu laporan HT emergency jika belum diterima.", "green");
        CekKondisiLevelSelesai();
    }

    private void CekParameterLevel4()
    {
        if (_currentLevel != GameLevel.Level4_SlurryPump)
            return;

        var data = _dataLevel[GameLevel.Level4_SlurryPump];
        if (Mathf.Abs(_flowRateSaatIni - data.targetFlowRate) <= 10f)
        {
            // Flow tercapai. Hanya promote sekali.
            if (_level4Phase == Level4Phase.MenungguTombolDcs ||
                _level4Phase == Level4Phase.AturFlowRate)
            {
                Log("FLOW OK", $"Flow rate {_flowRateSaatIni} m3/h. Target tercapai. SEKARANG lapor HT.", "green");
                _dcsTombolSudahDitekan = true;
                _flowRateLevel4Dikonfirmasi = true;
                // Promote ke MenungguLaporanFlow — player WAJIB lapor HT dulu sebelum
                // teleport ke field. Tidak ada auto-teleport langsung.
                SetLevel4Phase(Level4Phase.MenungguLaporanFlow);
            }
        }
    }

    public bool FlowRateLevel4Dikonfirmasi => _flowRateLevel4Dikonfirmasi;

    private void CekParameterLevel6()
    {
        if (_currentLevel != GameLevel.Level6_AcidInjection)
            return;

        var data = _dataLevel[GameLevel.Level6_AcidInjection];
        bool acidOK = Mathf.Abs(_acidRatioSaatIni - data.targetAcidRatio) <= 10f;
        bool phOK = Mathf.Abs(_phSaatIni - data.targetPH) <= 0.15f || _phSaatIni <= 1.1f;
        if (acidOK && phOK)
        {
            Log("ACID OK", $"Rasio asam {_acidRatioSaatIni} kg/ton dan pH {_phSaatIni:F1}. Target SOP tercapai.", "green");
            _dcsTombolSudahDitekan = true;
            _level6AcidComplete = true;
            CekKondisiLevelSelesai();
        }
    }

    public bool ParameterAutoklaveSesuaiSOP()
    {
        if (!_dataLevel.ContainsKey(_currentLevel))
            return false;

        var data = _dataLevel[_currentLevel];
        if (data.targetSuhu <= 0f)
            return true;

        bool suhuOK = Mathf.Abs(_suhuSaatIni - data.targetSuhu) <= 5f;
        bool tekananOK = Mathf.Abs(_tekananSaatIni - data.targetTekanan) <= 2f;
        bool rpmOK = Mathf.Abs(_rpmSaatIni - data.targetRPM) <= 5f;
        return suhuOK && tekananOK && rpmOK;
    }

    private void CekKondisiLevelSelesai()
    {
        if (!_levelSedangBerjalan)
            return;

        var data = _dataLevel[_currentLevel];
        bool tombolOK = data.nomorTombolDCS == 0 || _dcsTombolSudahDitekan;
        bool voiceOK = !data.butuhVoiceReport || _voiceReportSudahDilakukan;
        if (_currentLevel == GameLevel.Level14_Emergency)
            tombolOK = tombolOK && _level14EsdPressed;

        if (tombolOK && voiceOK)
            SelesaikanLevel(_currentLevel);
    }

    public void TriggerEmergency()
    {
        Log("DARURAT", "Kondisi darurat terdeteksi! Segera lapor dan tekan ESD.", "red");
        OnEmergencyTriggered?.Invoke(_currentLevel);
        MulaiLevel(GameLevel.Level14_Emergency);
    }

    private float HitungSkorLevel(GameLevel level, float waktuSelesai)
    {
        return Mathf.Clamp(100f - (waktuSelesai / 5f), 30f, 100f);
    }

    private float GetDurasiTransisi(GameLevel levelSebelum)
    {
        if (levelSebelum == GameLevel.Level3_OreSlurry) return _durasiTransisiLevel3;
        if (levelSebelum == GameLevel.Level4_SlurryPump) return _durasiTransisiLevel4;
        return _durasiTransisiDefault;
    }

    private bool VoiceReportCocok(LevelData data, string ucapan)
    {
        return VoiceReportCocokDenganCadangan(data, ucapan, data.laporanVoiceLengkap, data.kataKunciVoice);
    }

    private bool VoiceReportCocokDenganCadangan(LevelData data, string ucapan, string laporanLengkap, string keywordPendek)
    {
        string spoken = NormalizeVoiceText(ucapan);
        if (string.IsNullOrWhiteSpace(spoken))
            return false;

        if (TextVoiceCocok(spoken, laporanLengkap))
            return true;

        if (_izinkanKeywordPendekSebagaiCadangan && TextVoiceCocok(spoken, keywordPendek))
            return true;

        return VoiceReportCocokDenganAliasEnglish(data, spoken, laporanLengkap, keywordPendek);
    }

    private bool VoiceReportCocokDenganAliasEnglish(LevelData data, string spoken, string laporanLengkap, string keywordPendek)
    {
        foreach (string alias in GetVoiceAliasesDinamis(data.level))
        {
            if (TextVoiceCocok(spoken, alias))
                return true;
        }

        foreach (string alias in GetVoiceAliasesEnglish(data.level, laporanLengkap, keywordPendek))
        {
            if (TextVoiceCocok(spoken, alias))
                return true;
        }

        return false;
    }

    private IEnumerable<string> GetVoiceAliasesDinamis(GameLevel level)
    {
        if (!_gunakanLaporanHtDinamis || _laporanHtPerLevel == null)
            yield break;

        for (int i = 0; i < _laporanHtPerLevel.Count; i++)
        {
            LaporanHtDinamis laporan = _laporanHtPerLevel[i];
            if (laporan == null || laporan.level != level || string.IsNullOrWhiteSpace(laporan.aliasTambahanPerBaris))
                continue;

            string[] aliases = laporan.aliasTambahanPerBaris.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries);
            for (int j = 0; j < aliases.Length; j++)
            {
                string alias = aliases[j].Trim();
                if (!string.IsNullOrWhiteSpace(alias))
                    yield return alias;
            }
        }
    }

    private IEnumerable<string> GetVoiceAliasesEnglish(GameLevel level, string laporanLengkap, string keywordPendek)
    {
        switch (level)
        {
            case GameLevel.Level1_APD:
                yield return "apd lengkap";
                yield return "ppe complete";
                yield return "safety gear complete";
                break;

            case GameLevel.Level2_DCSPrep:
                yield return "siapkan area";
                yield return "siapkan area crusher";
                yield return "prepare area";
                yield return "crusher area ready";
                break;

            case GameLevel.Level3_OreSlurry:
                if (string.Equals(laporanLengkap, _dataLevel[GameLevel.Level3_OreSlurry].laporanVoiceAwal, StringComparison.Ordinal))
                {
                    yield return "jalankan alur ore";
                    yield return "jalankan ore";
                    yield return "start ore flow";
                    yield return "start ore line";
                }
                else
                {
                    yield return "ore masuk";
                    yield return "ore sudah masuk";
                    yield return "ore masuk slurry";
                    yield return "ore ready";
                    yield return "ore in slurry tank";
                }
                break;

            case GameLevel.Level4_SlurryPump:
                yield return "slurry pump aktif";
                yield return "cairan sudah di preheater";
                yield return "slurry masuk preheater";
                yield return "slurry pump active";
                yield return "flow set";
                break;

            case GameLevel.Level5_SteamValve:
                yield return "katup steam terbuka";
                yield return "steam valve open";
                yield return "heater temperature up";
                break;

            case GameLevel.Level6_AcidInjection:
                yield return "acid aktif";
                yield return "injeksi asam aktif";
                yield return "acid injection active";
                yield return "acid ratio set";
                break;

            case GameLevel.Level7_Autoclave:
                yield return "suhu 250";
                yield return "suhu dua ratus lima puluh";
                yield return "tekanan 50";
                yield return "autoclave stable";
                yield return "temperature pressure rpm";
                break;

            case GameLevel.Level8_Monitoring:
                yield return "parameter stabil";
                yield return "parameter stable";
                yield return "operation stable";
                break;

            case GameLevel.Level9_FlashVessel:
                yield return "flash vessel normal";
                yield return "pressure release safe";
                break;

            case GameLevel.Level10_CCD:
                yield return "ccd aktif";
                yield return "ccd active";
                yield return "separation started";
                break;

            case GameLevel.Level11_MHP:
                yield return "mhp terbentuk";
                yield return "mhp formed";
                yield return "precipitation normal";
                break;

            case GameLevel.Level12_TailingDischarge:
                yield return "limbah dialirkan";
                yield return "tailing discharge safe";
                yield return "waste transferred";
                yield return "filter press complete";
                break;

            case GameLevel.Level13_TailingWaste:
                yield return "tailing aman";
                yield return "tailing safe";
                yield return "pH 8.5";
                yield return "filter press complete";
                yield return "dry stack aman";
                yield return "dry stack safe";
                break;

            case GameLevel.Level14_Emergency:
                yield return "emergency evacuation";
                yield return "leak detected";
                break;
        }

        if (!string.IsNullOrWhiteSpace(keywordPendek))
            yield return keywordPendek;
    }

    private bool TextVoiceCocok(string spoken, string target)
    {
        string normalizedTarget = NormalizeVoiceText(target);
        if (string.IsNullOrWhiteSpace(normalizedTarget))
            return false;

        // Exact match dulu (cepat).
        if (spoken == normalizedTarget)
            return true;

        // Fuzzy: spoken mengandung target (player bicara lebih banyak)
        if (spoken.Contains(normalizedTarget))
            return true;

        // Fuzzy: target mengandung spoken (player bicara substring penting dari target)
        // Hanya kalau spoken cukup panjang (≥ 2 kata) dan ≥ 30% panjang target.
        if (normalizedTarget.Contains(spoken) && spoken.Length >= 6
            && spoken.Split(' ').Length >= 2
            && spoken.Length >= normalizedTarget.Length * 0.3f)
            return true;

        // Token overlap: ≥ 60% kata target ada di spoken (atau sebaliknya).
        var targetTokens = normalizedTarget.Split(new[] { ' ' }, System.StringSplitOptions.RemoveEmptyEntries);
        var spokenTokens = spoken.Split(new[] { ' ' }, System.StringSplitOptions.RemoveEmptyEntries);
        if (targetTokens.Length >= 2 && spokenTokens.Length >= 2)
        {
            var spokenSet = new HashSet<string>(spokenTokens);
            int overlap = 0;
            foreach (var t in targetTokens)
                if (spokenSet.Contains(t)) overlap++;
            float overlapRatioOfTarget = (float)overlap / targetTokens.Length;
            float overlapRatioOfSpoken = (float)overlap / spokenTokens.Length;
            if (overlapRatioOfTarget >= 0.6f || overlapRatioOfSpoken >= 0.7f)
                return true;
        }

        return false;
    }

    private string NormalizeVoiceText(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return string.Empty;

        var chars = new List<char>(value.Length);
        bool lastWasSpace = false;
        foreach (char raw in value.ToLowerInvariant())
        {
            char c = char.IsLetterOrDigit(raw) ? raw : ' ';
            if (c == ' ')
            {
                if (lastWasSpace)
                    continue;

                lastWasSpace = true;
            }
            else
            {
                lastWasSpace = false;
            }

            chars.Add(c);
        }

        return new string(chars.ToArray()).Trim();
    }

    private void Log(string label, string pesan, string warna = "white")
    {
        Debug.Log($"<color={warna}>[GLM - {label}]</color> {pesan}");
    }

#if UNITY_EDITOR
    [ContextMenu("DEBUG: Selesaikan Level Ini")]
    private void DebugSelesaikanLevel() => SelesaikanLevel(_currentLevel);

    [ContextMenu("DEBUG: Trigger Emergency")]
    private void DebugTriggerEmergency() => TriggerEmergency();

    [ContextMenu("DEBUG: Pindah ke Level Berikutnya")]
    private void DebugPindahLevel()
    {
        int next = (int)_currentLevel + 1;
        if (next <= 14)
            MulaiLevel((GameLevel)next);
    }
#endif


#if UNITY_EDITOR
    [ContextMenu("DEBUG: Skip ke Level 3 (Auto-equip APD dasar)")]
    private void DebugSkipKeLevel3()
    {
        if (PhaseManager.Instance != null)
        {
            PhaseManager.Instance.OnHelmetWorn();
            PhaseManager.Instance.OnVestWorn();
            PhaseManager.Instance.OnGlassesWorn();
            PhaseManager.Instance.OnBootsWorn();
            PhaseManager.Instance.OnGlovesWorn();
            PhaseManager.Instance.OnRespiratiorWorn();
            PhaseManager.Instance.OnEarplugWorn();
            PhaseManager.Instance.OnWalkieTalkieTaken();
            // Pastikan masker fisik pindah ke socket baju supaya saat Level 3 player bisa interaksi.
            PhaseManager.Instance.PastikanMaskerAdaDiSocketBaju();
            // Reset status respirator karena di Level 3 player wajib pakai ulang.
            PhaseManager.Instance.OnRespiratorRemoved();
        }
        MulaiLevel(GameLevel.Level3_OreSlurry);
        // Auto: tandai DCS sudah ditekan dan laporan awal sudah diterima supaya langsung fase teleport ke field.
        TryOnDCSTombolDitekan(3);
        SetLevel3PhaseDebug(Level3Phase.LaporanAwalDiterima);
        OnVoiceReportAccepted?.Invoke("jalankan alur ore");
    }

    private void SetLevel3PhaseDebug(Level3Phase phase)
    {
        SetLevel3Phase(phase);
    }
#endif


#if UNITY_EDITOR
    [ContextMenu("DEBUG: Skip ke Level 4 (Flow Rate)")]
    private void DebugSkipKeLevel4()
    {
        if (PhaseManager.Instance != null)
        {
            PhaseManager.Instance.OnHelmetWorn();
            PhaseManager.Instance.OnVestWorn();
            PhaseManager.Instance.OnGlassesWorn();
            PhaseManager.Instance.OnBootsWorn();
            PhaseManager.Instance.OnGlovesWorn();
            PhaseManager.Instance.OnRespiratiorWorn();
            PhaseManager.Instance.OnEarplugWorn();
            PhaseManager.Instance.OnWalkieTalkieTaken();
        }
        MulaiLevel(GameLevel.Level4_SlurryPump);

        // Auto-teleport player ke area DCS supaya bisa langsung tekan tombol [+]/[-] flow rate.
        TeleportPlayerKeSpawnPoint("SpawnPoint_Lvl4", fallbackName: "SpawnPoint_DCS");

        Log("DEBUG", "Skip ke Level 4. Tekan tombol DCS 4 lalu atur flow rate ke 450 m3/h.", "yellow");
    }

    /// <summary>
    /// Helper teleport XR Origin ke spawn point berdasarkan nama. Cari fallback kalau primary tidak ada.
    /// </summary>
    private void TeleportPlayerKeSpawnPoint(string namaSpawn, string fallbackName = null)
    {
        var spawn = GameObject.Find(namaSpawn);
        if (spawn == null && !string.IsNullOrEmpty(fallbackName))
            spawn = GameObject.Find(fallbackName);

        if (spawn == null)
        {
            Log("DEBUG", $"SpawnPoint '{namaSpawn}' tidak ditemukan. Skip teleport.", "yellow");
            return;
        }

        var xrOrigin = GameObject.Find("XR Origin (XR Rig)")
                    ?? GameObject.Find("XR Origin")
                    ?? GameObject.Find("XR Rig")
                    ?? GameObject.FindGameObjectWithTag("Player");

        if (xrOrigin == null)
        {
            Log("DEBUG", "XR Origin tidak ditemukan untuk teleport.", "yellow");
            return;
        }

        var cc = xrOrigin.GetComponent<CharacterController>();
        bool ccEnabled = cc != null && cc.enabled;
        if (ccEnabled) cc.enabled = false;

        xrOrigin.transform.position = spawn.transform.position;
        xrOrigin.transform.rotation = spawn.transform.rotation;

        if (ccEnabled) cc.enabled = true;

        Log("DEBUG", $"Player teleported ke '{spawn.name}' di {spawn.transform.position}.", "green");
    }
#endif
}
