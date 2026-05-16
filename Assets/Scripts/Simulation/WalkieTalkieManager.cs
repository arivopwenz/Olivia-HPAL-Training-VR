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
    [SerializeField] private GameObject _walkieTalkieInHand;
    [SerializeField] private Transform _rightHandAnchor;
    [SerializeField] private Animator _walkieAnimator;
    [SerializeField] private string _animShowTrigger = "Show";
    [SerializeField] private string _animHideTrigger = "Hide";
    [SerializeField] private bool _autoShowOnPTT = true;

    [Header("=== Voice Recognition ===")]
#if UNITY_STANDALONE_WIN || UNITY_EDITOR_WIN
    [SerializeField] private ConfidenceLevel _tingkatKepercayaan = ConfidenceLevel.Medium;
#endif
    [SerializeField] private float _delaySebelumDiproses = 1.35f;
    [SerializeField] private float _delaySebelumBalasan = 0.75f;
    [SerializeField] private bool _pakaiFallbackKeyboard = true;
    [SerializeField] private bool _autoSubmitKeywordSaatSpeechGagal = true;
    [SerializeField] private float _minimumDurasiPttUntukFallback = 0.25f;

    [Header("=== Status (Read Only) ===")]
    [SerializeField] private bool _pttSedangDitekan;
    [SerializeField] private bool _recognizerAktif;
    [SerializeField] private string _lastKeyword = "";

    public static event Action<string> OnKeywordTerdeteksi;
    public static event Action OnPTTDitekan;
    public static event Action OnPTTDilepas;

#if UNITY_STANDALONE_WIN || UNITY_EDITOR_WIN
    private KeywordRecognizer _recognizer;
#endif
    private bool _sistemSiap;
    private float _waktuMulaiPtt = -1f;
    private string _pendingSpeechText = string.Empty;
    private bool _adaKeywordPadaSesiPtt;
    private Coroutine _coroutineProsesLaporan;

    private readonly string[] _frasaFallbackDasar =
    {
        "apd lengkap",
        "siapkan area",
        "ore masuk",
        "slurry pump aktif",
        "katup steam terbuka",
        "acid aktif",
        "suhu 250",
        "parameter stabil",
        "flash vessel normal",
        "ccd aktif",
        "mhp terbentuk",
        "limbah dialirkan",
        "tailing aman",
        "emergency",
        "evakuasi"
    };

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
        InisialisasiRecognizer();
    }

    private void Update()
    {
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
        if (mouse != null)
        {
            if (mouse.leftButton.wasPressedThisFrame) OnPTTPress();
            else if (mouse.leftButton.wasReleasedThisFrame) OnPTTRelease();
        }
#elif ENABLE_LEGACY_INPUT_MANAGER
        if (Input.GetKeyDown(KeyCode.T)) OnPTTPress();
        else if (Input.GetKeyUp(KeyCode.T)) OnPTTRelease();

        if (_pakaiFallbackKeyboard && _pttSedangDitekan && Input.GetKeyDown(KeyCode.Alpha1))
            SimulasikanKeywordLevelAktif();

        if (Input.GetMouseButtonDown(0)) OnPTTPress();
        else if (Input.GetMouseButtonUp(0)) OnPTTRelease();
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
            _recognizer = new KeywordRecognizer(BangunDatabaseFrasa(), _tingkatKepercayaan);
            _recognizer.OnPhraseRecognized += OnFrasaTerdeteksi;
            _sistemSiap = true;
            Log("INIT", "KeywordRecognizer siap untuk laporan HT lengkap.", "cyan");
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

    private string[] BangunDatabaseFrasa()
    {
        var phrases = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (string item in _frasaFallbackDasar)
            phrases.Add(item);

        if (GameLevelManager.Instance != null)
        {
            for (int i = 1; i <= 14; i++)
            {
                var level = (GameLevelManager.GameLevel)i;
                if (!GameLevelManager.Instance.TryGetLevelData(level, out var data))
                    continue;

                if (!string.IsNullOrWhiteSpace(data.kataKunciVoice))
                    phrases.Add(data.kataKunciVoice);

                if (!string.IsNullOrWhiteSpace(data.laporanVoiceLengkap))
                    phrases.Add(data.laporanVoiceLengkap);
            }
        }

        var result = new string[phrases.Count];
        phrases.CopyTo(result);
        return result;
    }

    private void BersihkanRecognizer()
    {
#if UNITY_STANDALONE_WIN || UNITY_EDITOR_WIN
        if (_recognizer == null)
            return;

        _recognizer.OnPhraseRecognized -= OnFrasaTerdeteksi;
        if (_recognizer.IsRunning)
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
        _waktuMulaiPtt = Time.time;
        OnPTTDitekan?.Invoke();

        if (_coroutineProsesLaporan != null)
        {
            StopCoroutine(_coroutineProsesLaporan);
            _coroutineProsesLaporan = null;
        }

        if (_autoShowOnPTT)
            TampilkanHT(true);

        if (_audioSourceRadio != null && _suaraStaticBuka != null)
            _audioSourceRadio.PlayOneShot(_suaraStaticBuka);

#if UNITY_STANDALONE_WIN || UNITY_EDITOR_WIN
        if (_recognizer != null && !_recognizer.IsRunning)
        {
            _recognizer.Start();
            _recognizerAktif = true;
        }
#else
        _recognizerAktif = false;
#endif

        Log("PTT", "MENDENGARKAN... Sampaikan laporan HT sampai selesai.", "yellow");
    }

    public void OnPTTRelease()
    {
        if (!_pttSedangDitekan)
            return;

        _pttSedangDitekan = false;
        OnPTTDilepas?.Invoke();

        if (_autoShowOnPTT)
            TampilkanHT(false);

        if (_audioSourceRadio != null && _suaraStaticTutup != null)
            _audioSourceRadio.PlayOneShot(_suaraStaticTutup);

#if UNITY_STANDALONE_WIN || UNITY_EDITOR_WIN
        if (_recognizer != null && _recognizer.IsRunning)
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
    private void OnFrasaTerdeteksi(PhraseRecognizedEventArgs args)
    {
        if (!_pttSedangDitekan)
            return;

        string phrase = args.text?.Trim() ?? string.Empty;
        if (string.IsNullOrWhiteSpace(phrase))
            return;

        _pendingSpeechText = phrase.Length >= _pendingSpeechText.Length ? phrase : _pendingSpeechText;
        _lastKeyword = phrase;
        _adaKeywordPadaSesiPtt = true;
        Log("DETECTED", $"Frasa tertangkap: '<b>{phrase}</b>' (confidence: {args.confidence})", "cyan");
    }
#endif

    private IEnumerator ProsesLaporanSetelahJeda()
    {
        yield return new WaitForSeconds(_delaySebelumDiproses);

        string laporan = _pendingSpeechText;
        if (string.IsNullOrWhiteSpace(laporan))
            laporan = AmbilLaporanFallback();

        if (string.IsNullOrWhiteSpace(laporan))
        {
            Log("VOICE", "Tidak ada laporan yang bisa diproses pada sesi HT ini.", "orange");
            _coroutineProsesLaporan = null;
            yield break;
        }

        _lastKeyword = laporan;
        OnKeywordTerdeteksi?.Invoke(laporan);
        GameLevelManager.Instance?.OnVoiceKeywordTerdeteksi(laporan);
        StartCoroutine(MainkanBalasanNPC());
        _coroutineProsesLaporan = null;
    }

    private string AmbilLaporanFallback()
    {
        if (!_autoSubmitKeywordSaatSpeechGagal || !_pakaiFallbackKeyboard || GameLevelManager.Instance == null)
            return string.Empty;

        float durasiPtt = _waktuMulaiPtt < 0f ? 0f : Time.time - _waktuMulaiPtt;
        if (durasiPtt < _minimumDurasiPttUntukFallback)
            return string.Empty;

        if (!GameLevelManager.Instance.TryGetLevelData(GameLevelManager.Instance.CurrentLevel, out var data))
            return string.Empty;

        string fallback = string.IsNullOrWhiteSpace(data.laporanVoiceLengkap) ? data.kataKunciVoice : data.laporanVoiceLengkap;
        if (string.IsNullOrWhiteSpace(fallback))
            return string.Empty;

        Log("FALLBACK", $"Speech tidak menangkap frasa. Auto-submit laporan: '<b>{fallback}</b>'", "orange");
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

    public bool PTTAktif => _pttSedangDitekan;
    public bool SistemSiap => _sistemSiap;
    public bool RecognizerAktif => _recognizerAktif;
    public string LastKeyword => _lastKeyword;

    private void TampilkanHT(bool tampil)
    {
        if (_walkieTalkieInHand == null)
            return;

        if (tampil)
        {
            if (_rightHandAnchor != null)
            {
                _walkieTalkieInHand.transform.SetParent(_rightHandAnchor, false);
                _walkieTalkieInHand.transform.localPosition = Vector3.zero;
                _walkieTalkieInHand.transform.localRotation = Quaternion.identity;
            }

            _walkieTalkieInHand.SetActive(true);
            if (_walkieAnimator != null && !string.IsNullOrEmpty(_animShowTrigger))
                _walkieAnimator.SetTrigger(_animShowTrigger);
        }
        else
        {
            if (_walkieAnimator != null && !string.IsNullOrEmpty(_animHideTrigger))
                _walkieAnimator.SetTrigger(_animHideTrigger);
            else
                _walkieTalkieInHand.SetActive(false);
        }
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
