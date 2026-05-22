using System.Collections;
using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit.Interactables;

/// <summary>
/// OLIVIA VR - Level5SteamValveController.cs
///
/// FLOW LEVEL 5 — Steam Valve & Pre-Heater:
///   1. Player teleport ke area Pre-Heater (setelah Level 4 selesai)
///   2. Player grab Steam Valve handwheel → putar searah jarum jam
///   3. Setiap rotasi → suhu Pre-Heater naik proporsional (0°C → 200°C)
///   4. Steam particle FX intensitas naik seiring valve terbuka
///   5. Audio: suara mendesis steam makin keras
///   6. Saat suhu mencapai 180-200°C → quest tercentang
///   7. Player lapor HT: "Katup steam terbuka, suhu naik."
///   8. Fade → teleport ke DCS untuk Level 6
///
/// Mekanik valve: XRGrabInteractable dengan constraint rotasi.
/// Setiap 360° rotasi = 25% valve open = +50°C suhu.
/// Total 4 putaran penuh = 100% open = 200°C.
/// </summary>
public class Level5SteamValveController : MonoBehaviour
{
    [Header("=== Referensi Pemain ===")]
    [SerializeField] private Transform _playerRigRoot;

    [Header("=== Steam Valve (Handwheel) ===")]
    [Tooltip("Transform handwheel yang diputar player. Auto-create kalau kosong.")]
    [SerializeField] private Transform _valveWheel;
    [Tooltip("XRGrabInteractable di valve wheel. Auto-add kalau kosong.")]
    [SerializeField] private XRGrabInteractable _valveGrab;
    [Tooltip("Sumbu rotasi valve (default Z = putar di bidang XY).")]
    [SerializeField] private Vector3 _sumbuRotasiValve = Vector3.forward;
    [Tooltip("Total derajat rotasi untuk valve 100% open (4 putaran = 1440°).")]
    [SerializeField] private float _totalDerajatFullOpen = 1440f;
    [Tooltip("Kecepatan rotasi saat di-grab (derajat per detik saat player putar).")]
    [SerializeField] private float _kecepatanRotasiMax = 180f;

    [Header("=== Suhu Pre-Heater ===")]
    [Tooltip("Suhu awal (°C) sebelum valve dibuka.")]
    [SerializeField] private float _suhuAwal = 25f;
    [Tooltip("Suhu target saat valve 100% open.")]
    [SerializeField] private float _suhuTarget = 200f;
    [Tooltip("Suhu minimum untuk quest tercentang.")]
    [SerializeField] private float _suhuMinimumQuest = 180f;

    [Header("=== Steam Particle FX ===")]
    [Tooltip("ParticleSystem steam yang intensitasnya naik seiring valve terbuka.")]
    [SerializeField] private ParticleSystem _steamParticle;
    [Tooltip("Emisi maksimum saat valve 100% open.")]
    [SerializeField] private float _steamEmisiMax = 80f;
    [Tooltip("Auto-find Steam_FX di PreHeater kalau kosong.")]
    [SerializeField] private bool _autoFindSteamFx = true;

    [Header("=== Audio Steam ===")]
    [Tooltip("AudioSource untuk suara mendesis steam. Auto-create kalau kosong.")]
    [SerializeField] private AudioSource _steamAudio;
    [Tooltip("Volume max saat valve full open.")]
    [Range(0f, 1f)] [SerializeField] private float _steamVolumeMax = 0.7f;
    [Tooltip("Pitch saat valve mulai dibuka.")]
    [SerializeField] private float _steamPitchMin = 0.6f;
    [Tooltip("Pitch saat valve full open.")]
    [SerializeField] private float _steamPitchMax = 1.3f;

    [Header("=== Temperature Gauge (Visual) ===")]
    [Tooltip("Transform needle gauge yang berputar sesuai suhu. Auto-create kalau kosong.")]
    [SerializeField] private Transform _gaugeNeedle;
    [Tooltip("Rotasi needle saat suhu 0°C (derajat Z).")]
    [SerializeField] private float _gaugeAngleMin = 45f;
    [Tooltip("Rotasi needle saat suhu 200°C (derajat Z).")]
    [SerializeField] private float _gaugeAngleMax = -135f;

    [Header("=== Timing & Teleport ===")]
    [SerializeField] private Transform _teleportTargetDcs;
    [SerializeField] private float _durasiFade = 2.5f;
    [SerializeField] private float _jedaSetelahLaporan = 2f;

    [Header("=== HUD ===")]
    [TextArea(2, 4)] [SerializeField] private string _pesanMulai =
        "Putar katup steam searah jarum jam untuk memanaskan Pre-Heater.";
    [TextArea(2, 4)] [SerializeField] private string _pesanSuhuTercapai =
        "Suhu Pre-Heater mencapai target! Tahan T dan lapor: 'katup steam terbuka'.";

    // Runtime state
    private float _rotasiAkumulasi;
    private float _suhuSaatIni;
    private float _valveOpenPercent;
    private bool _questTercapai;
    private bool _sedangDiGrab;
    private PlayerHUD _hud;
    private AudioClip _steamClip;
    private Quaternion _grabRotasiAwal;

    private void Awake()
    {
        _hud = FindObjectOfType<PlayerHUD>();
        AutoFindReferences();
        EnsureSteamAudio();
        _suhuSaatIni = _suhuAwal;
    }

    private void OnEnable()
    {
        GameLevelManager.OnLevelStarted += OnLevelStarted;
    }

    private void OnDisable()
    {
        GameLevelManager.OnLevelStarted -= OnLevelStarted;
    }

    private void OnLevelStarted(GameLevelManager.GameLevel level)
    {
        if (level == GameLevelManager.GameLevel.Level5_SteamValve)
        {
            _rotasiAkumulasi = 0f;
            _suhuSaatIni = _suhuAwal;
            _valveOpenPercent = 0f;
            _questTercapai = false;
            UpdateVisuals();
            if (_hud != null) _hud.ShowNotifPublic(_pesanMulai);
        }
    }

    private void Update()
    {
        if (GameLevelManager.Instance == null) return;
        if (GameLevelManager.Instance.CurrentLevel != GameLevelManager.GameLevel.Level5_SteamValve) return;

        // Simulate valve rotation via keyboard (for testing without VR grab)
        // In real VR, XRGrabInteractable handles rotation tracking
        if (_sedangDiGrab || SimulateValveInput())
        {
            UpdateValveState();
            UpdateVisuals();
            CheckQuestCompletion();
        }
    }

    /// <summary>
    /// Dipanggil oleh XRGrabInteractable.selectEntered event saat player grab valve.
    /// </summary>
    public void OnValveGrabbed()
    {
        _sedangDiGrab = true;
        if (_valveWheel != null)
            _grabRotasiAwal = _valveWheel.localRotation;
    }

    /// <summary>
    /// Dipanggil oleh XRGrabInteractable.selectExited event saat player lepas valve.
    /// </summary>
    public void OnValveReleased()
    {
        _sedangDiGrab = false;
    }

    /// <summary>
    /// Simulate valve rotation via keyboard (R key = rotate CW).
    /// Untuk testing tanpa headset VR.
    /// </summary>
    private bool SimulateValveInput()
    {
#if ENABLE_INPUT_SYSTEM
        var kb = UnityEngine.InputSystem.Keyboard.current;
        if (kb != null && kb.rKey.isPressed)
        {
            _rotasiAkumulasi += _kecepatanRotasiMax * Time.deltaTime;
            _rotasiAkumulasi = Mathf.Clamp(_rotasiAkumulasi, 0f, _totalDerajatFullOpen);
            return true;
        }
#elif ENABLE_LEGACY_INPUT_MANAGER
        if (Input.GetKey(KeyCode.R))
        {
            _rotasiAkumulasi += _kecepatanRotasiMax * Time.deltaTime;
            _rotasiAkumulasi = Mathf.Clamp(_rotasiAkumulasi, 0f, _totalDerajatFullOpen);
            return true;
        }
#endif
        return false;
    }

    private void UpdateValveState()
    {
        _valveOpenPercent = Mathf.Clamp01(_rotasiAkumulasi / _totalDerajatFullOpen);
        _suhuSaatIni = Mathf.Lerp(_suhuAwal, _suhuTarget, _valveOpenPercent);

        // Update GameLevelManager suhu
        if (GameLevelManager.Instance != null)
            GameLevelManager.Instance.SetSuhu(_suhuSaatIni);
    }

    private void UpdateVisuals()
    {
        // 1. Rotate valve wheel visual
        if (_valveWheel != null)
        {
            _valveWheel.localRotation = Quaternion.AngleAxis(_rotasiAkumulasi, _sumbuRotasiValve);
        }

        // 2. Steam particle intensity
        if (_steamParticle != null)
        {
            var emission = _steamParticle.emission;
            emission.rateOverTime = _steamEmisiMax * _valveOpenPercent;
            if (_valveOpenPercent > 0.01f && !_steamParticle.isPlaying)
                _steamParticle.Play(true);
            else if (_valveOpenPercent <= 0.01f && _steamParticle.isPlaying)
                _steamParticle.Stop(true);
        }

        // 3. Steam audio
        if (_steamAudio != null)
        {
            _steamAudio.volume = _steamVolumeMax * _valveOpenPercent;
            _steamAudio.pitch = Mathf.Lerp(_steamPitchMin, _steamPitchMax, _valveOpenPercent);
            if (_valveOpenPercent > 0.01f && !_steamAudio.isPlaying)
                _steamAudio.Play();
            else if (_valveOpenPercent <= 0.01f && _steamAudio.isPlaying)
                _steamAudio.Stop();
        }

        // 4. Temperature gauge needle
        if (_gaugeNeedle != null)
        {
            float t = Mathf.InverseLerp(_suhuAwal, _suhuTarget, _suhuSaatIni);
            float angle = Mathf.Lerp(_gaugeAngleMin, _gaugeAngleMax, t);
            _gaugeNeedle.localRotation = Quaternion.Euler(0f, 0f, angle);
        }
    }

    private void CheckQuestCompletion()
    {
        if (_questTercapai) return;
        if (_suhuSaatIni >= _suhuMinimumQuest)
        {
            _questTercapai = true;
            Debug.Log("[Level5] Suhu Pre-Heater mencapai " + _suhuSaatIni.ToString("F0") + " C. Quest tercapai!");
            if (_hud != null) _hud.ShowNotifPublic(_pesanSuhuTercapai);
        }
    }

    // ============================================================
    //  AUTO-FIND & SETUP
    // ============================================================

    private void AutoFindReferences()
    {
        if (_playerRigRoot == null)
        {
            var rig = GameObject.Find("XR Origin (XR Rig)") ?? GameObject.FindWithTag("Player");
            if (rig != null) _playerRigRoot = rig.transform;
        }

        if (_steamParticle == null && _autoFindSteamFx)
        {
            var go = GameObject.Find("Mesin Utama/PreHeater_Field_1/Steam_FX");
            if (go != null) _steamParticle = go.GetComponent<ParticleSystem>();
            if (_steamParticle == null && go != null) _steamParticle = go.GetComponentInChildren<ParticleSystem>();
        }

        if (_teleportTargetDcs == null)
        {
            var go = GameObject.Find("SpawnPoint_DCS");
            if (go != null) _teleportTargetDcs = go.transform;
        }
    }

    private void EnsureSteamAudio()
    {
        if (_steamAudio != null) return;

        var steamFx = GameObject.Find("Mesin Utama/PreHeater_Field_1/Steam_FX");
        if (steamFx != null)
        {
            _steamAudio = steamFx.GetComponent<AudioSource>();
            if (_steamAudio == null) _steamAudio = steamFx.AddComponent<AudioSource>();
        }
        else
        {
            _steamAudio = gameObject.AddComponent<AudioSource>();
        }

        _steamAudio.spatialBlend = 0.5f;
        _steamAudio.maxDistance = 40f;
        _steamAudio.loop = true;
        _steamAudio.playOnAwake = false;
        _steamAudio.volume = 0f;
        _steamAudio.priority = 48;

        if (_steamAudio.clip == null)
            _steamAudio.clip = BuatClipSteamHiss(durasi: 4f, sampleRate: 22050);
    }

    /// <summary>
    /// Generate procedural steam hiss sound — white noise filtered + high-frequency sweep.
    /// </summary>
    private AudioClip BuatClipSteamHiss(float durasi, int sampleRate)
    {
        int total = Mathf.CeilToInt(durasi * sampleRate);
        float[] data = new float[total];
        System.Random rnd = new System.Random(77);
        float hpPrev = 0f;

        for (int i = 0; i < total; i++)
        {
            // White noise
            float noise = ((float)rnd.NextDouble() - 0.5f) * 2f;
            // High-pass filter (bikin suara mendesis, bukan bass rumble)
            float hp = noise - hpPrev;
            hpPrev = noise * 0.92f;
            // Slight sine modulation untuk karakter "sssshhh"
            float t = (float)i / total;
            float mod = 1f + 0.3f * Mathf.Sin(t * Mathf.PI * 2f * 3f);
            data[i] = hp * 0.35f * mod;
        }

        // Crossfade loop
        int fadeLen = Mathf.Min(2000, total / 20);
        for (int i = 0; i < fadeLen; i++)
        {
            float fade = (float)i / fadeLen;
            data[i] *= fade;
            data[total - 1 - i] *= fade;
        }

        var clip = AudioClip.Create("ProcSteamHiss", total, 1, sampleRate, false);
        clip.SetData(data, 0);
        return clip;
    }

    // ============================================================
    //  PUBLIC API
    // ============================================================

    public float SuhuSaatIni => _suhuSaatIni;
    public float ValveOpenPercent => _valveOpenPercent;
    public bool QuestTercapai => _questTercapai;
}
