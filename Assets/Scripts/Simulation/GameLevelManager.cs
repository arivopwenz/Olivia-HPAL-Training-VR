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

    [Serializable]
    public class LevelData
    {
        public GameLevel level;
        [TextArea(2, 4)] public string namaLevel;
        [TextArea(2, 4)] public string deskripsiQuest;
        public int nomorTombolDCS;
        public bool butuhVoiceReport;
        public string kataKunciVoice;
        [TextArea(2, 5)] public string laporanVoiceLengkap;
        public string audioBalasanNPC;
        public float targetFlowRate;
        public float targetAcidRatio;
        public float targetSuhu;
        public float targetTekanan;
        public float targetRPM;
        public float targetPH;
    }

    public static event Action<GameLevel> OnLevelStarted;
    public static event Action<GameLevel, int> OnLevelComplete;
    public static event Action<int> OnDCSButtonShouldHighlight;
    public static event Action<GameLevel> OnEmergencyTriggered;
    public static event Action<GameLevel> OnDCSViewConfirmed;
    public static event Action<int> OnDCSButtonPressed;
    public static event Action<string> OnVoiceReportAccepted;
    public static event Action<GameLevel, GameLevel, float> OnLevelTransitionRequested;

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
    [SerializeField] private float _durasiTransisiLevel3 = 4.5f;

    [Header("=== Referensi Script ===")]
    [SerializeField] private PhaseManager _phaseManager;

    private readonly Dictionary<GameLevel, LevelData> _dataLevel = new Dictionary<GameLevel, LevelData>();
    private bool _voiceReportSudahDilakukan;
    private bool _dcsTombolSudahDitekan;
    private bool _dcsSudahDilihat;
    private float _waktuMulaiLevel;

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

        InisialisasiDataLevel();
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
            laporanVoiceLengkap = "DCS, ore sudah masuk ke slurry tank. Level cairan dua puluh lima persen dan proses aman.",
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
            deskripsiQuest = "Pantau parameter dan laporkan kondisi stabil setelah koreksi selesai.",
            nomorTombolDCS = 8,
            butuhVoiceReport = true,
            kataKunciVoice = "parameter stabil",
            laporanVoiceLengkap = "Field, parameter stabil. Koreksi selesai dan operasi kembali dalam batas SOP.",
            audioBalasanNPC = "audio_level8_balasan",
            targetSuhu = 252f,
            targetTekanan = 47.5f,
            targetRPM = 60f
        });

        TambahLevel(new LevelData
        {
            level = GameLevel.Level9_FlashVessel,
            namaLevel = "Level 9 - Flash Vessel",
            deskripsiQuest = "Pastikan pelepasan tekanan berjalan normal, lalu kirim laporan aman.",
            nomorTombolDCS = 9,
            butuhVoiceReport = true,
            kataKunciVoice = "flash vessel normal",
            laporanVoiceLengkap = "DCS, flash vessel normal. Tekanan turun ke dua belas atmosfer dan pelepasan uap dalam kondisi aman.",
            audioBalasanNPC = "audio_level9_balasan",
            targetTekanan = 12f
        });

        TambahLevel(new LevelData
        {
            level = GameLevel.Level10_CCD,
            namaLevel = "Level 10 - CCD Activation",
            deskripsiQuest = "Aktifkan CCD dan laporkan proses separasi padat-cair dimulai.",
            nomorTombolDCS = 10,
            butuhVoiceReport = true,
            kataKunciVoice = "ccd aktif",
            laporanVoiceLengkap = "Field, sistem CCD aktif. Pemisahan padat dan cair sudah dimulai.",
            audioBalasanNPC = "audio_level10_balasan"
        });

        TambahLevel(new LevelData
        {
            level = GameLevel.Level11_MHP,
            namaLevel = "Level 11 - MHP Sampling",
            deskripsiQuest = "Ambil sampel MHP dan kirim laporan hasil presipitasi.",
            nomorTombolDCS = 11,
            butuhVoiceReport = true,
            kataKunciVoice = "mhp terbentuk",
            laporanVoiceLengkap = "DCS, MHP terbentuk. Sampel presipitasi menunjukkan produk utama dalam kondisi normal.",
            audioBalasanNPC = "audio_level11_balasan"
        });

        TambahLevel(new LevelData
        {
            level = GameLevel.Level12_TailingDischarge,
            namaLevel = "Level 12 - Tailing Discharge",
            deskripsiQuest = "Alirkan tailing ke sistem netralisasi dan laporkan status pembuangan.",
            nomorTombolDCS = 12,
            butuhVoiceReport = true,
            kataKunciVoice = "limbah dialirkan",
            laporanVoiceLengkap = "Field, limbah tailing sudah dialirkan ke tangki netralisasi. Sistem pembuangan aman.",
            audioBalasanNPC = "audio_level12_balasan"
        });

        TambahLevel(new LevelData
        {
            level = GameLevel.Level13_TailingWaste,
            namaLevel = "Level 13 - Tailing Waste Management",
            deskripsiQuest = "Naikkan pH tailing, jalankan filter press, lalu kirim laporan akhir.",
            nomorTombolDCS = 13,
            butuhVoiceReport = true,
            kataKunciVoice = "tailing aman",
            laporanVoiceLengkap = "DCS, tailing aman. pH delapan koma lima dan filter press selesai sesuai prosedur lingkungan.",
            audioBalasanNPC = "audio_level13_balasan",
            targetPH = 8.5f
        });

        TambahLevel(new LevelData
        {
            level = GameLevel.Level14_Emergency,
            namaLevel = "Level 14 - Darurat K3",
            deskripsiQuest = "Laporkan kebocoran dan evakuasi, lalu tekan tombol ESD merah.",
            nomorTombolDCS = 14,
            butuhVoiceReport = true,
            kataKunciVoice = "emergency",
            laporanVoiceLengkap = "Emergency, emergency. Kebocoran terdeteksi di sektor proses. Semua personel segera evakuasi.",
            audioBalasanNPC = "audio_level14_balasan"
        });

        Log("INIT", $"Berhasil memuat data {_dataLevel.Count} level.", "cyan");
    }

    private void TambahLevel(LevelData data)
    {
        _dataLevel[data.level] = data;
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
        _waktuMulaiLevel = Time.time;

        var data = _dataLevel[level];
        Log("LEVEL MULAI", $"<b>{data.namaLevel}</b>\nQuest: {data.deskripsiQuest}", "yellow");

        if (data.nomorTombolDCS > 0)
            OnDCSButtonShouldHighlight?.Invoke(data.nomorTombolDCS);

        OnLevelStarted?.Invoke(level);
    }

    public void SelesaikanLevel(GameLevel level)
    {
        if (_currentLevel != level || !_levelSedangBerjalan)
            return;

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
        if (!_dataLevel.ContainsKey(_currentLevel))
            return;

        var data = _dataLevel[_currentLevel];
        if (data.nomorTombolDCS != nomorTombol)
        {
            Log("PERINGATAN",
                $"Tombol {nomorTombol} bukan tombol aktif sekarang. Harusnya tekan tombol {data.nomorTombolDCS}.",
                "orange");
            return;
        }

        _dcsTombolSudahDitekan = true;
        OnDCSButtonPressed?.Invoke(nomorTombol);
        Log("DCS", $"Tombol {nomorTombol} ditekan untuk level <b>{data.namaLevel}</b>.", "cyan");
        CekKondisiLevelSelesai();
    }

    public void OnVoiceKeywordTerdeteksi(string keyword)
    {
        if (!_dataLevel.ContainsKey(_currentLevel))
            return;

        var data = _dataLevel[_currentLevel];
        if (!VoiceReportCocok(data, keyword))
        {
            Log("VOICE", $"Ucapan '{keyword}' belum cocok untuk level ini.", "orange");
            return;
        }

        _voiceReportSudahDilakukan = true;
        OnVoiceReportAccepted?.Invoke(keyword);
        Log("VOICE REPORT", $"Laporan diterima: '<i>{keyword}</i>'", "cyan");
        CekKondisiLevelSelesai();
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

    public string GetLaporanVoiceDisplay(GameLevel level)
    {
        if (!_dataLevel.TryGetValue(level, out var data))
            return string.Empty;

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
    public void SetPH(float nilai) => _phSaatIni = nilai;

    public float FlowRate => _flowRateSaatIni;
    public float AcidRatio => _acidRatioSaatIni;
    public float Suhu => _suhuSaatIni;
    public float Tekanan => _tekananSaatIni;
    public float RPM => _rpmSaatIni;
    public float PH => _phSaatIni;
    public GameLevel CurrentLevel => _currentLevel;
    public bool LevelAktif => _levelSedangBerjalan;

    private void CekParameterLevel4()
    {
        if (_currentLevel != GameLevel.Level4_SlurryPump)
            return;

        var data = _dataLevel[GameLevel.Level4_SlurryPump];
        if (Mathf.Abs(_flowRateSaatIni - data.targetFlowRate) <= 10f)
        {
            Log("FLOW OK", $"Flow rate {_flowRateSaatIni} m3/h. Target {data.targetFlowRate} m3/h tercapai.", "green");
            _dcsTombolSudahDitekan = true;
            CekKondisiLevelSelesai();
        }
    }

    private void CekParameterLevel6()
    {
        if (_currentLevel != GameLevel.Level6_AcidInjection)
            return;

        var data = _dataLevel[GameLevel.Level6_AcidInjection];
        if (Mathf.Abs(_acidRatioSaatIni - data.targetAcidRatio) <= 10f)
        {
            Log("ACID OK", $"Rasio asam {_acidRatioSaatIni} kg/ton. Target {data.targetAcidRatio} kg/ton tercapai.", "green");
            _dcsTombolSudahDitekan = true;
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
        return levelSebelum == GameLevel.Level3_OreSlurry ? _durasiTransisiLevel3 : _durasiTransisiDefault;
    }

    private bool VoiceReportCocok(LevelData data, string ucapan)
    {
        string spoken = NormalizeVoiceText(ucapan);
        if (string.IsNullOrWhiteSpace(spoken))
            return false;

        return TextVoiceCocok(spoken, data.kataKunciVoice) ||
               TextVoiceCocok(spoken, data.laporanVoiceLengkap);
    }

    private bool TextVoiceCocok(string spoken, string target)
    {
        string normalizedTarget = NormalizeVoiceText(target);
        if (string.IsNullOrWhiteSpace(normalizedTarget))
            return false;

        return spoken.Contains(normalizedTarget) || normalizedTarget.Contains(spoken);
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
}
