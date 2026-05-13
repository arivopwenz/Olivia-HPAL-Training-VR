using UnityEngine;
using UnityEngine.Windows.Speech;
using System;
using System.Collections;

/// <summary>
/// OLIVIA VR - WalkieTalkieManager.cs
/// Sistem Voice Command menggunakan KeywordRecognizer (offline, built-in Unity).
/// Mendeteksi ucapan pemain via tombol PTT, memvalidasi ke GameLevelManager,
/// lalu membunyikan audio balasan NPC yang sudah direkam.
///
/// CARA SETUP DI UNITY:
///   1. Buat GameObject "WalkieTalkieManager" di scene
///   2. Attach script ini
///   3. Assign AudioSource (komponen audio di WalkieTalkie prefab)
///   4. Drag AudioClip suara static ke suaraStaticBuka / suaraStaticTutup
///   5. Drag 15 AudioClip balasan NPC ke array audioBalasanNPC (index = nomor level)
///   6. Tombol PTT di VR memanggil OnPTTPress() dan OnPTTRelease()
/// </summary>
public class WalkieTalkieManager : MonoBehaviour
{
    // ============================================================
    //  SINGLETON
    // ============================================================
    public static WalkieTalkieManager Instance { get; private set; }

    // ============================================================
    //  INSPECTOR
    // ============================================================
    [Header("=== Komponen Audio ===")]
    [SerializeField] private AudioSource _audioSourceRadio;
    [SerializeField] private AudioClip   _suaraStaticBuka;   // "krshh" saat PTT ditekan
    [SerializeField] private AudioClip   _suaraStaticTutup;  // "krshh" saat PTT dilepas

    [Header("=== Audio Balasan NPC (Urut Level 0-14, total 15 slot) ===")]
    [Tooltip("Index 0 = Level 0 Tutorial, Index 14 = Level 14 Emergency")]
    [SerializeField] private AudioClip[] _audioBalasanNPC = new AudioClip[15];

    [Header("=== Pengaturan Voice Recognition ===")]
    [SerializeField] private ConfidenceLevel _tingkatKepercayaan = ConfidenceLevel.Medium;
    [SerializeField] private float _delaySebelumBalasan = 0.8f;

    [Header("=== Status (Read Only) ===")]
    [SerializeField] private bool   _pttSedangDitekan = false;
    [SerializeField] private bool   _recognizerAktif  = false;
    [SerializeField] private string _lastKeyword      = "";

    // ============================================================
    //  EVENTS
    // ============================================================
    public static event Action<string> OnKeywordTerdeteksi;
    public static event Action         OnPTTDitekan;
    public static event Action         OnPTTDilepas;

    // ============================================================
    //  PRIVATE
    // ============================================================
    private KeywordRecognizer _recognizer;
    private bool _sistemSiap = false;

    // Semua kata kunci dari 14 level dalam satu flat array
    // KeywordRecognizer harus tahu semua kemungkinan di awal
    private readonly string[] _semuaKataKunci = new string[]
    {
        // Level 1: APD
        "APD lengkap",
        // Level 2: DCS Prep
        "siapkan area", "cek crusher",
        // Level 3: Ore ke Slurry
        "ore masuk", "level aman", "slurry siap",
        // Level 4: Slurry Pump
        "slurry pump aktif", "empat ratus lima puluh kubik",
        // Level 5: Steam Valve
        "katup steam terbuka", "suhu naik",
        // Level 6: Acid Injection
        "acid aktif", "rasio tiga ratus lima puluh",
        // Level 7: Autoclave
        "suhu dua ratus lima puluh", "tekanan lima puluh atm", "agitator enam puluh RPM",
        // Level 8: Monitoring
        "parameter stabil", "koreksi selesai",
        // Level 9: Flash Vessel
        "flash vessel normal", "tekanan dua belas atm",
        // Level 10: CCD
        "CCD aktif",
        // Level 11: MHP
        "MHP terbentuk", "produk normal",
        // Level 12: Tailing Discharge
        "limbah dialirkan",
        // Level 13: Tailing Waste
        "tailing aman", "pH delapan setengah", "filter press selesai",
        // Level 14: Emergency
        "emergency", "evakuasi", "kebocoran terdeteksi",
        // Umum
        "lapor selesai", "konfirmasi", "siap melaksanakan"
    };

    // ============================================================
    //  LIFECYCLE
    // ============================================================
    private void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
        DontDestroyOnLoad(gameObject);
    }

    private void Start()
    {
        InisialisasiRecognizer();
    }

    private void OnDestroy()  => BersihkanRecognizer();
    private void OnApplicationQuit() => BersihkanRecognizer();

    // ============================================================
    //  INISIALISASI RECOGNIZER
    // ============================================================
    private void InisialisasiRecognizer()
    {
        try
        {
            _recognizer = new KeywordRecognizer(_semuaKataKunci, _tingkatKepercayaan);
            _recognizer.OnPhraseRecognized += OnFrasaTerdeteksi;
            _sistemSiap = true;
            Log("INIT", $"KeywordRecognizer siap — {_semuaKataKunci.Length} kata kunci terdaftar.", "cyan");
        }
        catch (Exception e)
        {
            Log("ERROR", $"Gagal inisialisasi: {e.Message}", "red");
            _sistemSiap = false;
        }
    }

    private void BersihkanRecognizer()
    {
        if (_recognizer == null) return;
        _recognizer.OnPhraseRecognized -= OnFrasaTerdeteksi;
        if (_recognizer.IsRunning) _recognizer.Stop();
        _recognizer.Dispose();
        _recognizer = null;
    }

    // ============================================================
    //  KONTROL PTT (Push-To-Talk)
    //  Hubungkan ke XR Button Interactable di prefab Walkie Talkie
    // ============================================================

    /// <summary>Dipanggil saat pemain MENEKAN tombol PTT di Walkie Talkie VR.</summary>
    public void OnPTTPress()
    {
        if (!_sistemSiap || _pttSedangDitekan) return;
        _pttSedangDitekan = true;
        OnPTTDitekan?.Invoke();

        if (_audioSourceRadio != null && _suaraStaticBuka != null)
            _audioSourceRadio.PlayOneShot(_suaraStaticBuka);

        if (_recognizer != null && !_recognizer.IsRunning)
        {
            _recognizer.Start();
            _recognizerAktif = true;
            Log("PTT", "MENDENGARKAN... Bicara sekarang!", "yellow");
        }
    }

    /// <summary>Dipanggil saat pemain MELEPAS tombol PTT di Walkie Talkie VR.</summary>
    public void OnPTTRelease()
    {
        if (!_pttSedangDitekan) return;
        _pttSedangDitekan = false;
        OnPTTDilepas?.Invoke();

        if (_audioSourceRadio != null && _suaraStaticTutup != null)
            _audioSourceRadio.PlayOneShot(_suaraStaticTutup);

        if (_recognizer != null && _recognizer.IsRunning)
        {
            _recognizer.Stop();
            _recognizerAktif = false;
            Log("PTT", "Berhenti mendengarkan.", "white");
        }
    }

    // ============================================================
    //  CALLBACK DARI UNITY SPEECH API
    // ============================================================
    private void OnFrasaTerdeteksi(PhraseRecognizedEventArgs args)
    {
        // Pastikan hanya merespons saat PTT ditekan
        if (!_pttSedangDitekan) return;

        string keyword = args.text;
        _lastKeyword   = keyword;
        Log("DETECTED", $"Kata kunci: '<b>{keyword}</b>' (Confidence: {args.confidence})", "cyan");

        // Kirim ke GameLevelManager untuk divalidasi terhadap level aktif
        GameLevelManager.Instance?.OnVoiceKeywordTerdeteksi(keyword);
        OnKeywordTerdeteksi?.Invoke(keyword);

        // Mainkan audio balasan NPC setelah jeda singkat
        StartCoroutine(MainkanBalasanNPC());
    }

    // ============================================================
    //  AUDIO BALASAN NPC
    // ============================================================
    private IEnumerator MainkanBalasanNPC()
    {
        yield return new WaitForSeconds(_delaySebelumBalasan);
        if (GameLevelManager.Instance == null || _audioSourceRadio == null) yield break;

        int levelIndex = (int)GameLevelManager.Instance.CurrentLevel;

        if (levelIndex < _audioBalasanNPC.Length && _audioBalasanNPC[levelIndex] != null)
        {
            _audioSourceRadio.PlayOneShot(_audioBalasanNPC[levelIndex]);
            Log("NPC REPLY", $"Memainkan audio balasan Level {levelIndex}.", "green");
        }
        else
        {
            Log("NPC REPLY", $"[!] Audio balasan Level {levelIndex} belum di-assign di Inspector!", "orange");
        }
    }

    /// <summary>Mainkan audio balasan spesifik (bisa dipanggil dari luar untuk testing).</summary>
    public void MainkanAudioManual(int levelIndex)
    {
        StartCoroutine(MainkanBalasanNPC());
    }

    // ============================================================
    //  PROPERTIES
    // ============================================================
    public bool   PTTAktif    => _pttSedangDitekan;
    public bool   SistemSiap  => _sistemSiap;
    public string LastKeyword => _lastKeyword;

    private void Log(string label, string pesan, string warna = "white")
        => Debug.Log($"<color={warna}>[HT-{label}]</color> {pesan}");

    // ============================================================
    //  DEBUG (EDITOR ONLY)
    // ============================================================
#if UNITY_EDITOR
    [ContextMenu("DEBUG: Simulasi PTT Press")]
    private void D_PTTPress()   => OnPTTPress();

    [ContextMenu("DEBUG: Simulasi PTT Release")]
    private void D_PTTRelease() => OnPTTRelease();

    [ContextMenu("DEBUG: Test 'APD lengkap' (Level 1)")]
    private void D_KW_APD()     => GameLevelManager.Instance?.OnVoiceKeywordTerdeteksi("APD lengkap");

    [ContextMenu("DEBUG: Test 'slurry pump aktif' (Level 4)")]
    private void D_KW_Pump()    => GameLevelManager.Instance?.OnVoiceKeywordTerdeteksi("slurry pump aktif");

    [ContextMenu("DEBUG: Test 'suhu dua ratus lima puluh' (Level 7)")]
    private void D_KW_Suhu()    => GameLevelManager.Instance?.OnVoiceKeywordTerdeteksi("suhu dua ratus lima puluh");

    [ContextMenu("DEBUG: Test 'emergency' (Level 14)")]
    private void D_KW_Emg()     => GameLevelManager.Instance?.OnVoiceKeywordTerdeteksi("emergency");
#endif
}
