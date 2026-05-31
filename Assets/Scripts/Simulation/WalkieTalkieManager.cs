using UnityEngine;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif
#if UNITY_STANDALONE_WIN || UNITY_EDITOR_WIN
using UnityEngine.Windows.Speech;
#endif
using System;
using System.Collections;
using System.Collections.Generic;

/// <summary>
/// OLIVIA VR - WalkieTalkieManager.cs
/// Menangani PTT, voice report, jeda radio, dan audio balasan NPC.
/// </summary>
public class WalkieTalkieManager : MonoBehaviour
{
    public static WalkieTalkieManager Instance { get; private set; }

    [Header("=== Komponen Audio ===")]
    [SerializeField] private AudioSource _audioSourceRadio;
    [SerializeField] private AudioClip _suaraStaticBuka;
    [SerializeField] private AudioClip _suaraStaticTutup;

    [Header("=== Audio Balasan NPC (Urut Level 0-14) ===")]
    [SerializeField] private AudioClip[] _audioBalasanNPC = new AudioClip[15];

    [Header("=== Visual HT (Opsional) ===")]
    [SerializeField] private GameObject _walkieTalkieInHand;    [SerializeField] private Transform _rightHandAnchor;
    [SerializeField] private Animator _walkieAnimator;
    [SerializeField] private string _animShowTrigger = "Show";
    [SerializeField] private string _animHideTrigger = "Hide";
    [SerializeField] private bool _autoShowOnPTT = true;
    [SerializeField] private bool _hidePhysicalWalkieSaatPTTSelesai = false;

    [Header("=== Voice Recognition ===")]
    [SerializeField] private float _delaySebelumDiproses = 1.35f;
    [SerializeField] private float _delaySebelumBalasan = 0.75f;
    [SerializeField] private float _initialSilenceTimeout = 5f;
    [SerializeField] private float _autoSilenceTimeout = 3f;
    [SerializeField] private bool _pakaiFallbackKeyboard = true;
    [SerializeField] private bool _izinkanMouseUntukPTT = false;
    [SerializeField] private bool _modeTanpaVoiceUntukTempatRame = false;
    [SerializeField] private bool _autoSubmitKeywordSaatSpeechGagal = true;
    [SerializeField] private float _minimumDurasiPttUntukFallback = 0.25f;

    [Header("=== Debug Mic Input ===")]
    [SerializeField] private bool _debugMicInput = true;
    [SerializeField] private int _micSampleRate = 16000;
    [SerializeField] private int _micWindowSize = 128;
    [SerializeField] private float _ambangMicTerdengar = 0.01f;

    [Header("=== Status (Read Only) ===")]
    [SerializeField] private bool _pttSedangDitekan;
    [SerializeField] private bool _recognizerAktif;
    [SerializeField] private string _lastKeyword = "";
    [SerializeField] private float _debugMicPeak;

    public static event Action<string> OnKeywordTerdeteksi;
    public static event Action OnPTTDitekan;
    public static event Action OnPTTDilepas;

#if UNITY_STANDALONE_WIN || UNITY_EDITOR_WIN
    private DictationRecognizer _recognizer;
#endif
    private bool _sistemSiap;
    private float _waktuMulaiPtt = -1f;
    private string _pendingSpeechText = string.Empty;
    private string _pendingHypothesisText = string.Empty;
    private bool _adaKeywordPadaSesiPtt;
    private Coroutine _coroutineProsesLaporan;
    private Coroutine _coroutineSmoothShow;
    private bool _pttDariWalkieFisik;
    private AudioClip _debugMicClip;
    private string _debugMicDevice;
    private bool _debugMicMonitoring;

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
    }

    private void Start()
    {
        EnsureAudioFallback();
        InisialisasiRecognizer();
    }

    // Kalau audio HT belum di-assign di inspector, buat AudioSource + clip prosedural
    // (static hiss "ksshk" + nada balasan radio) supaya HT TETAP bersuara tanpa setup manual.
    private void EnsureAudioFallback()
    {
        if (_audioSourceRadio == null)
        {
            var go = new GameObject("HT_Radio_AudioSource");
            go.transform.SetParent(transform, false);
            _audioSourceRadio = go.AddComponent<AudioSource>();
            _audioSourceRadio.playOnAwake = false;
            _audioSourceRadio.spatialBlend = 0f; // 2D, langsung di telinga player (radio)
            _audioSourceRadio.volume = 0.85f;
        }
        if (_suaraStaticBuka == null) _suaraStaticBuka = GenStatic(0.22f, 24500, true);
        if (_suaraStaticTutup == null) _suaraStaticTutup = GenStatic(0.18f, 24501, false);
        // balasan NPC default (nada radio "beep-boop") kalau slot kosong.
        for (int i = 0; i < _audioBalasanNPC.Length; i++)
            if (_audioBalasanNPC[i] == null) _audioBalasanNPC[i] = GenRadioReply(0.9f, 23000 + i);
    }

    private static AudioClip GenStatic(float dur, int seed, bool rising)
    {
        int sr = 22050; int n = Mathf.CeilToInt(dur * sr); var data = new float[n];
        var rnd = new System.Random(seed);
        for (int i = 0; i < n; i++)
        {
            float t = (float)i / n;
            float env = rising ? Mathf.Clamp01(t * 3f) * (1f - t) * 2f : (1f - t);
            data[i] = ((float)rnd.NextDouble() * 2f - 1f) * 0.5f * env;
        }
        var c = AudioClip.Create("HT_Static_" + seed, n, 1, sr, false); c.SetData(data, 0); return c;
    }

    private static AudioClip GenRadioReply(float dur, int seed)
    {
        // "Copy" radio voice-ish: 2 burst nada termodulasi + sedikit noise band (kedengeran kayak radio).
        int sr = 22050; int n = Mathf.CeilToInt(dur * sr); var data = new float[n];
        var rnd = new System.Random(seed);
        for (int i = 0; i < n; i++)
        {
            float t = (float)i / n;
            // dua suku kata
            float syl = (t < 0.42f) ? 1f : (t > 0.5f && t < 0.95f ? 1f : 0f);
            float baseHz = (t < 0.42f) ? 320f : 260f;
            float vib = Mathf.Sin(2f * Mathf.PI * 14f * t) * 12f; // getar suara
            float tone = Mathf.Sin(2f * Mathf.PI * (baseHz + vib) * t * dur);
            float tone2 = Mathf.Sin(2f * Mathf.PI * (baseHz * 1.5f) * t * dur) * 0.4f;
            float noise = ((float)rnd.NextDouble() * 2f - 1f) * 0.10f;
            float env = syl * Mathf.Clamp01(Mathf.Sin(t * Mathf.PI)) ;
            data[i] = (tone + tone2 + noise) * 0.28f * env;
        }
        var c = AudioClip.Create("HT_Reply_" + seed, n, 1, sr, false); c.SetData(data, 0); return c;
    }


    private void Update()
    {
        if (_debugMicMonitoring)
            UpdateMicDebugLevel();

#if ENABLE_INPUT_SYSTEM
        var keyboard = Keyboard.current;
        if (keyboard != null)
        {
            if (keyboard.tKey.wasPressedThisFrame) OnPTTPress();
            else if (keyboard.tKey.wasReleasedThisFrame) OnPTTRelease();

            if (_pakaiFallbackKeyboard && _pttSedangDitekan && keyboard.digit1Key.wasPressedThisFrame)
                SimulasikanKeywordLevelAktif();
        }

        var mouse = Mouse.current;
        if (_izinkanMouseUntukPTT && mouse != null)
        {
            if (mouse.leftButton.wasPressedThisFrame) OnPTTPress();
            else if (mouse.leftButton.wasReleasedThisFrame) OnPTTRelease();
        }
#elif ENABLE_LEGACY_INPUT_MANAGER
        if (Input.GetKeyDown(KeyCode.T)) OnPTTPress();
        else if (Input.GetKeyUp(KeyCode.T)) OnPTTRelease();

        if (_pakaiFallbackKeyboard && _pttSedangDitekan && Input.GetKeyDown(KeyCode.Alpha1))
            SimulasikanKeywordLevelAktif();

        if (_izinkanMouseUntukPTT)
        {
            if (Input.GetMouseButtonDown(0)) OnPTTPress();
            else if (Input.GetMouseButtonUp(0)) OnPTTRelease();
        }
#endif
    }

    private void OnDestroy()
    {
        BersihkanRecognizer();
    }

    private void OnApplicationQuit()
    {
        BersihkanRecognizer();
    }

    private void InisialisasiRecognizer()
    {
#if UNITY_STANDALONE_WIN || UNITY_EDITOR_WIN
        try
        {
            BuatDictationRecognizer();
            _sistemSiap = true;
            Log("INIT", "DictationRecognizer siap untuk laporan HT natural.", "cyan");
        }
        catch (Exception e)
        {
            Log("ERROR", $"Gagal inisialisasi recognizer: {e.Message}", "red");
            _sistemSiap = _pakaiFallbackKeyboard;
        }
#else
        _sistemSiap = _pakaiFallbackKeyboard;
        Log("INIT", "Speech API tidak tersedia di platform ini. Fallback testing aktif.", "orange");
#endif
    }

#if UNITY_STANDALONE_WIN || UNITY_EDITOR_WIN
    private void BuatDictationRecognizer()
    {
        BersihkanRecognizer();

        _recognizer = new DictationRecognizer();
        _recognizer.InitialSilenceTimeoutSeconds = _initialSilenceTimeout;
        _recognizer.AutoSilenceTimeoutSeconds = _autoSilenceTimeout;
        _recognizer.DictationHypothesis += OnFrasaHipotesis;
        _recognizer.DictationResult += OnFrasaTerdeteksi;
        _recognizer.DictationComplete += OnDictationComplete;
        _recognizer.DictationError += OnDictationError;
    }
#endif

    private void BersihkanRecognizer()
    {
#if UNITY_STANDALONE_WIN || UNITY_EDITOR_WIN
        if (_recognizer == null)
            return;

        _recognizer.DictationHypothesis -= OnFrasaHipotesis;
        _recognizer.DictationResult -= OnFrasaTerdeteksi;
        _recognizer.DictationComplete -= OnDictationComplete;
        _recognizer.DictationError -= OnDictationError;
        if (_recognizer.Status == SpeechSystemStatus.Running)
            _recognizer.Stop();

        _recognizer.Dispose();
        _recognizer = null;
#endif
    }

    public void OnPTTPress()
    {
        if (!_sistemSiap || _pttSedangDitekan)
            return;

        _pttSedangDitekan = true;
        _adaKeywordPadaSesiPtt = false;
        _pendingSpeechText = string.Empty;
        _pendingHypothesisText = string.Empty;
        _waktuMulaiPtt = Time.time;
        OnPTTDitekan?.Invoke();

        if (_coroutineProsesLaporan != null)
        {
            StopCoroutine(_coroutineProsesLaporan);
            _coroutineProsesLaporan = null;
        }

        if (_autoShowOnPTT && !_pttDariWalkieFisik)
            TampilkanHT(true);

        if (_audioSourceRadio != null && _suaraStaticBuka != null)
            _audioSourceRadio.PlayOneShot(_suaraStaticBuka);

        if (_debugMicInput)
            MulaiMicDebugMonitor();

#if UNITY_STANDALONE_WIN || UNITY_EDITOR_WIN
        if (_recognizer == null)
            BuatDictationRecognizer();

        if (_recognizer != null && _recognizer.Status != SpeechSystemStatus.Running)
        {
            _recognizer.Start();
            _recognizerAktif = true;
        }
#else
        _recognizerAktif = false;
#endif

        Log("PTT",
            _modeTanpaVoiceUntukTempatRame
                ? "MODE TANPA VOICE AKTIF. Tahan T sebentar lalu lepas untuk kirim laporan level aktif."
                : "MENDENGARKAN... Sampaikan laporan HT sampai selesai.",
            "yellow");
    }

    public void OnPTTRelease()
    {
        if (!_pttSedangDitekan)
            return;

        _pttSedangDitekan = false;
        OnPTTDilepas?.Invoke();

        if (_autoShowOnPTT && !_pttDariWalkieFisik)
            TampilkanHT(false);

        if (_audioSourceRadio != null && _suaraStaticTutup != null)
            _audioSourceRadio.PlayOneShot(_suaraStaticTutup);

        if (_debugMicInput)
            HentikanMicDebugMonitor();

#if UNITY_STANDALONE_WIN || UNITY_EDITOR_WIN
        if (_recognizer != null && _recognizer.Status == SpeechSystemStatus.Running)
        {
            _recognizer.Stop();
            _recognizerAktif = false;
        }
#else
        _recognizerAktif = false;
#endif

        Log("PTT", "Berhenti mendengarkan. Memproses laporan radio...", "white");

        if (_coroutineProsesLaporan != null)
            StopCoroutine(_coroutineProsesLaporan);

        _coroutineProsesLaporan = StartCoroutine(ProsesLaporanSetelahJeda());
    }

#if UNITY_STANDALONE_WIN || UNITY_EDITOR_WIN
    private void OnFrasaHipotesis(string text)
    {
        string phrase = text?.Trim() ?? string.Empty;
        if (string.IsNullOrWhiteSpace(phrase))
            return;

        _pendingHypothesisText = phrase;
    }

    private void OnFrasaTerdeteksi(string text, ConfidenceLevel confidence)
    {
        string phrase = text?.Trim() ?? string.Empty;
        if (string.IsNullOrWhiteSpace(phrase))
            return;

        _pendingSpeechText = string.IsNullOrWhiteSpace(_pendingSpeechText)
            ? phrase
            : $"{_pendingSpeechText} {phrase}".Trim();
        _pendingHypothesisText = phrase;
        _lastKeyword = phrase;
        _adaKeywordPadaSesiPtt = true;
        Log("DETECTED", $"Dictation tertangkap: '<b>{phrase}</b>' (confidence: {confidence})", "cyan");
    }

    private void OnDictationComplete(DictationCompletionCause cause)
    {
        _recognizerAktif = false;
        if (string.IsNullOrWhiteSpace(_pendingSpeechText) && !string.IsNullOrWhiteSpace(_pendingHypothesisText))
        {
            _pendingSpeechText = _pendingHypothesisText.Trim();
            Log("DICTATION", $"Tidak ada hasil final. Memakai hypothesis terakhir: '<b>{_pendingSpeechText}</b>'", "orange");
        }

        Log("DICTATION", $"Dictation selesai dengan status: {cause}", cause == DictationCompletionCause.Complete ? "cyan" : "orange");
    }

    private void OnDictationError(string error, int hresult)
    {
        _recognizerAktif = false;
        Log("ERROR", $"Dictation error: {error} (0x{hresult:X})", "red");
    }
#endif

    private IEnumerator ProsesLaporanSetelahJeda()
    {
        yield return new WaitForSeconds(_delaySebelumDiproses);

        // Tentukan apakah PTT cukup lama untuk dianggap "ucapan asli" (mendukung mode tanpa voice).
        float durasiPtt = _waktuMulaiPtt < 0f ? 0f : Time.time - _waktuMulaiPtt;
        bool pttCukupLama = durasiPtt >= _minimumDurasiPttUntukFallback;

        string laporan = _pendingSpeechText;

        // Fallback Mode Tanpa Voice: KALAU mode aktif & PTT cukup lama, langsung pakai laporan manual
        // tanpa harus menunggu speech engine final. Ini menyelesaikan kasus speech text ngaco / kotor.
        if (_modeTanpaVoiceUntukTempatRame && pttCukupLama)
        {
            string laporanManual = AmbilLaporanManualTanpaVoice();
            if (!string.IsNullOrWhiteSpace(laporanManual))
            {
                if (string.IsNullOrWhiteSpace(laporan))
                {
                    Log("VOICE", $"Mode tanpa voice aktif: pakai laporan manual '<b>{laporanManual}</b>' (speech kosong).", "green");
                    laporan = laporanManual;
                }
                else
                {
                    Log("VOICE", $"Mode tanpa voice aktif: prioritas laporan manual '<b>{laporanManual}</b>' di atas speech '<i>{laporan}</i>'.", "green");
                    laporan = laporanManual;
                }
            }
        }

        if (string.IsNullOrWhiteSpace(laporan) && TryGetLevel1MicFallback(out string laporanLevel1))
            laporan = laporanLevel1;
        if (string.IsNullOrWhiteSpace(laporan))
            laporan = AmbilLaporanFallback();

        if (string.IsNullOrWhiteSpace(laporan))
        {
            Log("VOICE", "Tidak ada frasa valid yang tertangkap. Mic masuk, tapi speech engine tidak mengubah ucapan menjadi teks.", "orange");
            _coroutineProsesLaporan = null;
            yield break;
        }

        _lastKeyword = laporan;
        OnKeywordTerdeteksi?.Invoke(laporan);
        bool laporanDiterima = GameLevelManager.Instance != null &&
                               GameLevelManager.Instance.OnVoiceKeywordTerdeteksi(laporan);

        // Retry pertama: kalau ditolak & mode tanpa voice aktif, coba dengan laporan manual yang berbeda.
        if (!laporanDiterima && _modeTanpaVoiceUntukTempatRame && pttCukupLama && GameLevelManager.Instance != null)
        {
            string laporanManual = AmbilLaporanManualTanpaVoice();
            if (!string.IsNullOrWhiteSpace(laporanManual) && laporanManual != laporan)
            {
                Log("VOICE", $"Retry dengan laporan manual: '<i>{laporanManual}</i>'.", "orange");
                laporan = laporanManual;
                _lastKeyword = laporan;
                laporanDiterima = GameLevelManager.Instance.OnVoiceKeywordTerdeteksi(laporan);
            }
        }

        // Retry final: kalau masih ditolak & mode tanpa voice aktif, force-accept dengan
        // memberitahu GLM untuk bypass keyword matching (tetap respect syarat sequencing lain).
        if (!laporanDiterima && _modeTanpaVoiceUntukTempatRame && pttCukupLama && GameLevelManager.Instance != null)
        {
            laporanDiterima = GameLevelManager.Instance.ForceAcceptVoiceUntukLevelAktif(laporan);
            if (laporanDiterima)
                Log("VOICE", $"Force-accept laporan '<b>{laporan}</b>' untuk Level {(int)GameLevelManager.Instance.CurrentLevel} (mode tanpa voice).", "green");
        }

        if (laporanDiterima)
            StartCoroutine(MainkanBalasanNPC());
        else
            Log("VOICE", $"Laporan HT tidak sesuai instruksi level aktif: '<b>{laporan}</b>'", "orange");

        _coroutineProsesLaporan = null;
    }

    private bool TryGetLevel1MicFallback(out string laporan)
    {
        laporan = string.Empty;
        if (GameLevelManager.Instance == null ||
            GameLevelManager.Instance.CurrentLevel != GameLevelManager.GameLevel.Level1_APD)
            return false;

        if (PhaseManager.Instance == null || !PhaseManager.Instance.APDLengkapSempurna)
            return false;

        bool adaSuara = _debugMicInput && _debugMicPeak >= _ambangMicTerdengar;
        bool adaTeksParsial = _adaKeywordPadaSesiPtt || !string.IsNullOrWhiteSpace(_pendingHypothesisText);
        if (!adaSuara && !adaTeksParsial)
            return false;

        laporan = GameLevelManager.Instance.GetLaporanVoiceDisplay(GameLevelManager.GameLevel.Level1_APD);
        if (string.IsNullOrWhiteSpace(laporan))
            laporan = "APD lengkap";

        Log("VOICE", $"Level 1 fallback: mic/teks WT terdeteksi, kirim laporan '<b>{laporan}</b>'.", "cyan");
        return true;
    }

    private string AmbilLaporanManualTanpaVoice()
    {
        if (!_modeTanpaVoiceUntukTempatRame || GameLevelManager.Instance == null)
        {
            Log("MANUAL", $"Skip: modeTanpaVoice={_modeTanpaVoiceUntukTempatRame} | GLM={(GameLevelManager.Instance != null)}", "orange");
            return string.Empty;
        }

        float durasiPtt = _waktuMulaiPtt < 0f ? 0f : Time.time - _waktuMulaiPtt;
        if (durasiPtt < _minimumDurasiPttUntukFallback)
        {
            Log("MANUAL", $"Skip: durasi PTT terlalu pendek ({durasiPtt:F2}s < {_minimumDurasiPttUntukFallback:F2}s).", "orange");
            return string.Empty;
        }

        string laporan = GameLevelManager.Instance.GetLaporanVoiceDisplay(GameLevelManager.Instance.CurrentLevel);
        if (string.IsNullOrWhiteSpace(laporan))
        {
            // Fallback: pakai kata kunci level aktif kalau laporan voice display kosong.
            laporan = GameLevelManager.Instance.GetKataKunciVoiceUntukLevel(GameLevelManager.Instance.CurrentLevel);
        }
        if (string.IsNullOrWhiteSpace(laporan))
        {
            Log("MANUAL", $"Skip: laporan kosong dari GLM (level={GameLevelManager.Instance.CurrentLevel}).", "orange");
            return string.Empty;
        }

        Log("MANUAL", $"Mode tanpa voice aktif. Mengirim laporan otomatis: '<b>{laporan}</b>'", "green");
        return laporan;
    }

    private string AmbilLaporanFallback()
    {
        if (!_autoSubmitKeywordSaatSpeechGagal || !_pakaiFallbackKeyboard || GameLevelManager.Instance == null)
            return string.Empty;

        float durasiPtt = _waktuMulaiPtt < 0f ? 0f : Time.time - _waktuMulaiPtt;
        if (durasiPtt < _minimumDurasiPttUntukFallback)
            return string.Empty;

        bool adaSuara = _debugMicInput && _debugMicPeak >= _ambangMicTerdengar;
        bool adaTeksParsial = _adaKeywordPadaSesiPtt || !string.IsNullOrWhiteSpace(_pendingHypothesisText);
        if (!adaSuara && !adaTeksParsial)
            return string.Empty;

        if (!GameLevelManager.Instance.TryGetLevelData(GameLevelManager.Instance.CurrentLevel, out var data))
            return string.Empty;

        string fallback = GameLevelManager.Instance.GetLaporanVoiceDisplay(GameLevelManager.Instance.CurrentLevel);
        if (string.IsNullOrWhiteSpace(fallback))
            fallback = string.IsNullOrWhiteSpace(data.laporanVoiceLengkap) ? data.kataKunciVoice : data.laporanVoiceLengkap;
        if (string.IsNullOrWhiteSpace(fallback))
            return string.Empty;

        Log("FALLBACK", $"Mic terdengar tapi speech text kosong. Auto-submit laporan: '<b>{fallback}</b>'", "cyan");
        return fallback;
    }

    private IEnumerator MainkanBalasanNPC()
    {
        yield return new WaitForSeconds(_delaySebelumBalasan);
        if (GameLevelManager.Instance == null || _audioSourceRadio == null)
            yield break;

        int levelIndex = (int)GameLevelManager.Instance.CurrentLevel;
        if (levelIndex < _audioBalasanNPC.Length && _audioBalasanNPC[levelIndex] != null)
        {
            _audioSourceRadio.PlayOneShot(_audioBalasanNPC[levelIndex]);
            Log("NPC REPLY", $"Memainkan audio balasan level {levelIndex}.", "green");
        }
        else
        {
            Log("NPC REPLY", $"Audio balasan level {levelIndex} belum di-assign.", "orange");
        }
    }

    public void MainkanAudioManual(int levelIndex)
    {
        StartCoroutine(MainkanBalasanNPC());
    }

    /// <summary>
    /// Balasan HT untuk laporan INTERIM (di tengah level, mis. Level 8 buka valve / tiap vessel).
    /// Memainkan SFX static HT + audio balasan NPC level aktif (suara asli) sekali.
    /// </summary>
    public void MainkanBalasanInterim()
    {
        StartCoroutine(BalasanInterimRoutine());
    }

    private IEnumerator BalasanInterimRoutine()
    {
        // SFX static "ksshk" pembuka radio.
        if (_audioSourceRadio != null && _suaraStaticBuka != null)
            _audioSourceRadio.PlayOneShot(_suaraStaticBuka);
        yield return new WaitForSeconds(_delaySebelumBalasan);

        if (_audioSourceRadio != null && GameLevelManager.Instance != null)
        {
            int levelIndex = (int)GameLevelManager.Instance.CurrentLevel;
            if (levelIndex >= 0 && levelIndex < _audioBalasanNPC.Length && _audioBalasanNPC[levelIndex] != null)
                _audioSourceRadio.PlayOneShot(_audioBalasanNPC[levelIndex]);
        }
        yield return new WaitForSeconds(0.4f);
        // SFX static penutup.
        if (_audioSourceRadio != null && _suaraStaticTutup != null)
            _audioSourceRadio.PlayOneShot(_suaraStaticTutup);
    }

    public bool PTTAktif => _pttSedangDitekan;
    public bool SistemSiap => _sistemSiap;
    public bool RecognizerAktif => _recognizerAktif;
    public string LastKeyword => _lastKeyword;
    public Transform WalkieTalkieInHandTransform => _walkieTalkieInHand != null ? _walkieTalkieInHand.transform : null;

    public void BeginPhysicalWalkiePTT()
    {
        _pttDariWalkieFisik = true;
        OnPTTPress();
    }

    public void EndPhysicalWalkiePTT()
    {
        OnPTTRelease();
        _pttDariWalkieFisik = false;
    }

    private void TampilkanHT(bool tampil)
    {
        if (_walkieTalkieInHand == null)
            return;

        // Cek status WT: apakah sudah dipakai player (PhaseManager.isWalkieTalkieTaken)
        bool sudahDipakai = PhaseManager.Instance != null && PhaseManager.Instance.isWalkieTalkieTaken;

        if (tampil)
        {
            // Hanya manipulasi posisi/parent kalau WT memang sudah dipakai player.
            // Kalau belum dipakai (masih di socket scanner / lantai), JANGAN lepas — biarin di tempatnya.
            if (!sudahDipakai)
            {
                // Beep saja sebagai feedback, tidak rubah transform.
                if (_walkieAnimator != null && !string.IsNullOrEmpty(_animShowTrigger))
                    _walkieAnimator.SetTrigger(_animShowTrigger);
                return;
            }

            // Disable XR socket interactors untuk sementara supaya WT tidak "ke-snap" ke socket lain
            // saat menghadap depan (mis. socket dada/mouth menarik balik).
            DisableSocketInteractors(_walkieTalkieInHand, true);

            // Freeze rigidbody supaya tidak jatuh saat parent dilepas.
            var rb = _walkieTalkieInHand.GetComponent<Rigidbody>();
            if (rb != null)
            {
                rb.linearVelocity = Vector3.zero;
                rb.angularVelocity = Vector3.zero;
                rb.isKinematic = true;
            }

            if (_rightHandAnchor != null)
            {
                _walkieTalkieInHand.transform.SetParent(_rightHandAnchor, false);
                // Smooth lerp ke posisi tangan (bukan instan)
                if (_coroutineSmoothShow != null)
                    StopCoroutine(_coroutineSmoothShow);
                _coroutineSmoothShow = StartCoroutine(SmoothMoveHTKeTarget());
            }

            // Disable XRGrabInteractable supaya tidak "kesedot" socket lain
            var grab = _walkieTalkieInHand.GetComponent<UnityEngine.XR.Interaction.Toolkit.Interactables.XRGrabInteractable>();
            if (grab != null && grab.interactionManager != null && grab.isSelected)
            {
                grab.interactionManager.CancelInteractableSelection((UnityEngine.XR.Interaction.Toolkit.Interactables.IXRSelectInteractable)grab);
            }
            if (grab != null) grab.enabled = false;

            _walkieTalkieInHand.SetActive(true);
            if (_walkieAnimator != null && !string.IsNullOrEmpty(_animShowTrigger))
                _walkieAnimator.SetTrigger(_animShowTrigger);
        }
        else
        {
            if (_walkieAnimator != null && !string.IsNullOrEmpty(_animHideTrigger))
                _walkieAnimator.SetTrigger(_animHideTrigger);
            else if (sudahDipakai && _hidePhysicalWalkieSaatPTTSelesai)
                _walkieTalkieInHand.SetActive(false);

            if (sudahDipakai)
            {
                // Re-enable grab + socket interactors saat HT disembunyikan.
                var grab = _walkieTalkieInHand.GetComponent<UnityEngine.XR.Interaction.Toolkit.Interactables.XRGrabInteractable>();
                if (grab != null) grab.enabled = true;
                // Re-enable semua collider supaya player bisa grab manual dari chest dock.
                foreach (var col in _walkieTalkieInHand.GetComponentsInChildren<Collider>(true))
                    if (col != null) col.enabled = true;
                DisableSocketInteractors(_walkieTalkieInHand, false);

                // Restore rigidbody supaya bisa di-grab/lepas normal.
                var rb = _walkieTalkieInHand.GetComponent<Rigidbody>();
                if (rb != null)
                {
                    if (!rb.isKinematic)
                    {
                        rb.linearVelocity = Vector3.zero;
                        rb.angularVelocity = Vector3.zero;
                    }
                    rb.isKinematic = true;
                    rb.useGravity = false;
                }

                // Pastikan walkie active supaya tetap visible dan bisa di-grab.
                if (!_walkieTalkieInHand.activeSelf)
                    _walkieTalkieInHand.SetActive(true);
            }
        }
    }

    private void DisableSocketInteractors(GameObject target, bool disable)
    {
        if (target == null) return;

        // Hindari socket interactor di scene "menarik" balik object yang sedang ditampilkan.
        // Cari semua XRSocketInteractor yang nama-nya mengandung "WalkieTalkie", "Mouth", atau "WT"
        // (socket yang biasanya men-snap walkie talkie di mouth/torso/dada).
        var sockets = UnityEngine.Object.FindObjectsByType<UnityEngine.XR.Interaction.Toolkit.Interactors.XRSocketInteractor>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        foreach (var sock in sockets)
        {
            if (sock == null) continue;
            string n = sock.gameObject.name.ToLowerInvariant();
            if (n.Contains("walkietalkie") || n.Contains("walkie") || n.Contains("mouth") || n.Contains("ht_socket"))
            {
                sock.socketActive = !disable; // disable=true → socketActive=false
                sock.enabled = !disable;
            }
        }
    }

    /// <summary>
    /// Smooth lerp walkie talkie ke posisi tangan (0.25 detik) supaya tidak instan.
    /// </summary>
    private IEnumerator SmoothMoveHTKeTarget()
    {
        if (_walkieTalkieInHand == null) yield break;

        Transform t = _walkieTalkieInHand.transform;
        Vector3 startPos = t.localPosition;
        Quaternion startRot = t.localRotation;
        float duration = 0.25f;
        float elapsed = 0f;

        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            float progress = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(elapsed / duration));
            t.localPosition = Vector3.Lerp(startPos, Vector3.zero, progress);
            t.localRotation = Quaternion.Slerp(startRot, Quaternion.identity, progress);
            yield return null;
        }

        t.localPosition = Vector3.zero;
        t.localRotation = Quaternion.identity;
        _coroutineSmoothShow = null;
    }

    private void SimulasikanKeywordLevelAktif()
    {
        if (!_pttSedangDitekan || GameLevelManager.Instance == null)
            return;

        string laporan = GameLevelManager.Instance.GetLaporanVoiceDisplay(GameLevelManager.Instance.CurrentLevel);
        if (string.IsNullOrWhiteSpace(laporan))
            return;

        _pendingSpeechText = laporan;
        _adaKeywordPadaSesiPtt = true;
        _lastKeyword = laporan;
        Log("FALLBACK", $"Frasa testing disiapkan: '<b>{laporan}</b>'", "cyan");
    }

    private void MulaiMicDebugMonitor()
    {
        _debugMicPeak = 0f;
        _debugMicDevice = null;
        _debugMicMonitoring = false;

        if (Microphone.devices == null || Microphone.devices.Length == 0)
        {
            Log("MIC", "Tidak ada device microphone yang terdeteksi oleh Unity.", "red");
            return;
        }

        _debugMicDevice = Microphone.devices[0];
        _debugMicClip = Microphone.Start(_debugMicDevice, true, 5, _micSampleRate);
        if (_debugMicClip == null)
        {
            Log("MIC", $"Gagal memulai monitor mic dari device '{_debugMicDevice}'.", "red");
            return;
        }

        _debugMicMonitoring = true;
        Log("MIC", $"Monitor mic aktif: '{_debugMicDevice}'. Silakan bicara saat tahan T.", "cyan");
    }

    private void HentikanMicDebugMonitor()
    {
        if (!_debugMicMonitoring)
            return;

        UpdateMicDebugLevel();
        _debugMicMonitoring = false;
        Microphone.End(_debugMicDevice);

        bool micTerdengar = _debugMicPeak >= _ambangMicTerdengar;
        Log("MIC",
            micTerdengar
                ? $"Mic terdeteksi. Peak={_debugMicPeak:F4}"
                : $"Mic terlalu kecil / tidak masuk. Peak={_debugMicPeak:F4}",
            micTerdengar ? "green" : "orange");
    }

    private void UpdateMicDebugLevel()
    {
        if (_debugMicClip == null || string.IsNullOrEmpty(_debugMicDevice))
            return;

        int micPos = Microphone.GetPosition(_debugMicDevice);
        if (micPos <= 0 || _micWindowSize <= 0 || micPos < _micWindowSize)
            return;

        float[] samples = new float[_micWindowSize];
        _debugMicClip.GetData(samples, micPos - _micWindowSize);

        float peak = 0f;
        for (int i = 0; i < samples.Length; i++)
        {
            float abs = Mathf.Abs(samples[i]);
            if (abs > peak)
                peak = abs;
        }

        if (peak > _debugMicPeak)
            _debugMicPeak = peak;
    }

    private void Log(string label, string pesan, string warna = "white")
    {
        Debug.Log($"<color={warna}>[HT-{label}]</color> {pesan}");
    }

#if UNITY_EDITOR
    [ContextMenu("DEBUG: Simulasi PTT Press")]
    private void D_PTTPress() => OnPTTPress();

    [ContextMenu("DEBUG: Simulasi PTT Release")]
    private void D_PTTRelease() => OnPTTRelease();

    [ContextMenu("DEBUG: Siapkan Laporan Level Aktif")]
    private void D_KW_LevelAktif() => SimulasikanKeywordLevelAktif();
#endif
}
