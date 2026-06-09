using System.Collections;
using System.Collections.Generic;
using TMPro;
using Unity.XR.CoreUtils;
using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit.Interactables;

/// <summary>
/// OLIVIA VR — Level 8 FlashTrainController.cs
///
/// FLOW LEVEL 8 — Flash Train 3-Stage Pressure Letdown (HPAL SOP):
///   1. Player tekan DCS 8 → fade teleport ke depan FV1
///   2. Putar bypass handwheel FV1 (10-turn) → P 47→12 atm, lampu RED→GREEN
///   3. Interlock check: P_FV1 < 13 atm sebelum FV2 bisa dibuka
///   4. Putar bypass handwheel FV2 → P 12→3 atm
///   5. Putar steam valve FV3 → P 3→1.05 atm (atmospheric flash)
///   6. Slurry mengalir ke CCD via Feed_FromFlashVessel_To_CCD1
///   7. Voice report HT (tahan T) → Mission Complete Canvas
///   8. STAY (lihat proses) atau KEMBALI KE DCS → Level 9 (CCD sampling & QC)
///
/// PATTERN: Sequential gating dengan pressure interlock + 10-turn handwheel + cascade panel.
/// </summary>
public class Level8FlashTrainController : MonoBehaviour
{
    [Header("=== Player ===")]
    [SerializeField] private Transform _playerRigRoot;
    [SerializeField] private Transform _teleportTargetField;
    [SerializeField] private float _fadeTransitionDuration = 2.5f;

    [Header("=== Flash Vessel Stages ===")]
    [Tooltip("FV1 HP Flash: 47atm/250C → 12atm/195C")]
    [SerializeField] private FlashStage _fv1 = new FlashStage { stageName = "FV1 HP", pressureStart = 47.5f, pressureTarget = 12f, pressureTolerance = 1f, tempStart = 250f, tempTarget = 195f };
    [Tooltip("FV2 MP Flash: 12atm/195C → 3atm/145C")]
    [SerializeField] private FlashStage _fv2 = new FlashStage { stageName = "FV2 MP", pressureStart = 12f, pressureTarget = 3f, pressureTolerance = 0.5f, tempStart = 195f, tempTarget = 145f };
    [Tooltip("FV3 LP/Atmospheric Flash: 3atm/145C → 1.05atm/102C")]
    [SerializeField] private FlashStage _fv3 = new FlashStage { stageName = "FV3 LP", pressureStart = 3f, pressureTarget = 1.05f, pressureTolerance = 0.05f, tempStart = 145f, tempTarget = 102f };

    [Header("=== Letdown Bypass Handwheels ===")]
    [SerializeField] private Transform _fv1HandwheelHub;
    [SerializeField] private Transform _fv2HandwheelHub;
    [SerializeField] private Transform _fv3HandwheelHub;
    [SerializeField] private float _handwheelFullOpenDegrees = 300f; // ~0.8 putaran (dipermudah lagi dari 540)
    [Tooltip("Pengali sensitivitas twist. 1.5 = sedikit amplifikasi, terkendali. Jangan 5 (kebut).")]
    [SerializeField] private float _gesturalGain = 1.5f;

    [Header("=== Cascade Panel + Slurry Visualisation ===")]
    [SerializeField] private Renderer _fv1StatusStrip;
    [SerializeField] private Renderer _fv2StatusStrip;
    [SerializeField] private Renderer _fv3StatusStrip;
    [SerializeField] private TextMeshPro _fv1PanelText;
    [SerializeField] private TextMeshPro _fv2PanelText;
    [SerializeField] private TextMeshPro _fv3PanelText;
    [SerializeField] private Transform _fv1SlurryGhost;
    [SerializeField] private Transform _fv2SlurryGhost;
    [SerializeField] private Transform _fv3SlurryGhost;

    [Header("=== Vapor Outlet (steam recovery FX) ===")]
    [SerializeField] private Transform _fv1VaporRiser;
    [SerializeField] private Transform _fv2VaporRiser;
    [SerializeField] private Transform _fv3VaporRiser;

    [Header("=== Steam Anchors (uap keluar TEPAT di sini) ===")]
    [Tooltip("SteamRiser_Connect_-7 (uap FV1, paling besar)")]
    [SerializeField] private Transform _steamAnchor1;
    [Tooltip("SteamRiser_Connect_0 (uap FV2, lebih kecil)")]
    [SerializeField] private Transform _steamAnchor2;
    [Tooltip("SteamRiser_Connect_7 (uap FV3, paling kecil)")]
    [SerializeField] private Transform _steamAnchor3;

    [Header("=== Per-Vessel Spawn Points (teleport tiap vessel) ===")]
    [Tooltip("SpawnPoint_Lv8 (di depan FV1)")]
    [SerializeField] private Transform _spawnFv1;
    [Tooltip("SpawnPoint_Lv8 (1) (di depan FV2)")]
    [SerializeField] private Transform _spawnFv2;
    [Tooltip("SpawnPoint_Lv8 (2) (di depan FV3)")]
    [SerializeField] private Transform _spawnFv3;

    [Header("=== Steam Intensity per Vessel (turun bertahap) ===")]
    [Tooltip("FV1 paling besar & keras, FV2 sedang, FV3 paling kecil")]
    [SerializeField] private float _steamMultFv1 = 1.0f;
    [SerializeField] private float _steamMultFv2 = 0.6f;
    [SerializeField] private float _steamMultFv3 = 0.32f;
    [SerializeField] private float _interVesselFadeDuration = 1.8f;

    [Header("=== Sample System ===")]
    [SerializeField] private float _sampleCoolDuration = 6f;
    [SerializeField] private Color _sampleHotColor = new Color(1f, 0.2f, 0.1f);
    [SerializeField] private Color _sampleCoolColor = new Color(0.45f, 0.15f, 0.55f);

    [Header("=== Audio ===")]
    [SerializeField] private AudioSource _steamReleaseAudio;
    [SerializeField] private AudioSource _alarmAudio;
    [Range(0f, 1f)] [SerializeField] private float _steamReleaseVolume = 0.95f;

    [Header("=== Keys (Debug) ===")]
    [SerializeField] private KeyCode _key1Open = KeyCode.Alpha1;
    [SerializeField] private KeyCode _key2Open = KeyCode.Alpha2;
    [SerializeField] private KeyCode _key3Open = KeyCode.Alpha3;

    [Header("=== HUD Messages ===")]
    [TextArea(2, 4)] [SerializeField] private string _msgStart =
        "FV1: uap panas menyembur deras! Putar handwheel kuning (CW) untuk MENUTUP valve sampai uap berhenti.\nSetelah tertutup, cairan mengalir ke FV2.";
    [TextArea(2, 4)] [SerializeField] private string _msgFv1Done =
        "FV1 tertutup, uap berhenti. Cairan mengalir ke FV2... berpindah ke FV2.";
    [TextArea(2, 4)] [SerializeField] private string _msgFv2Done =
        "FV2 tertutup, uap lebih kecil berhenti. Cairan mengalir ke FV3... berpindah ke FV3.";
    [TextArea(2, 4)] [SerializeField] private string _msgFv3Done =
        "FV3 tertutup. Tekanan turun ke ~1 atm, suhu ~102°C. Flash train STABIL.\nLapor HT (tahan T): 'Flash train stabil, slurry siap dialirkan ke CCD.'";
    [TextArea(2, 4)] [SerializeField] private string _msgFv2Intro =
        "FV2: uap masih keluar (lebih kecil dari FV1). Putar handwheel untuk menutup valve.";
    [TextArea(2, 4)] [SerializeField] private string _msgFv3Intro =
        "FV3: uap tipis terakhir. Putar handwheel untuk menutup, lalu cairan lanjut ke CCD.";
    [TextArea(2, 4)] [SerializeField] private string _msgSamplingDone =
        "3 sample terkumpul! Masuk ke LABORATORIUM QC (gedung di samping), lalu tekan [L] untuk analisa sample.";
    [TextArea(2, 4)] [SerializeField] private string _msgLabComplete =
        "Lab QC sukses, semua parameter dalam SOP. Lapor HT (tahan T) untuk akhir level.";

    // ========== Runtime ==========
    private enum Phase { Idle, MenungguDcs, TeleportField, OpenAutoclaveValve, OpenFV1, OpenFV2, OpenFV3, Sampling, LabSubmit, MenungguLapor, Selesai }
    private Phase _phase = Phase.Idle;
    private bool _levelActive;
    private PlayerHUD _hud;
    private Coroutine _seqCoroutine;
    private bool _waitingForVoiceReport;
    private bool _voiceReportReceived;

    // Sample state
    private bool[] _sampleTaken = new bool[3];
    private GameObject _missionCompleteCanvas;
    private GameObject _labQcCanvas;

    // Vapor FX
    private ParticleSystem _fv1VaporFX;
    private ParticleSystem _fv2VaporFX;
    private ParticleSystem _fv3VaporFX;

    // Sample bottle visuals
    private GameObject[] _sampleBottles = new GameObject[3];
    private static readonly Color[] _sampleStageColors = {
        new Color(1f, 0.25f, 0.1f),   // FV1 hot red
        new Color(1f, 0.6f, 0.15f),   // FV2 mid orange
        new Color(0.85f, 0.85f, 0.2f) // FV3 cool yellow
    };

    // Sample station (mekanik fisik: dekati 3 vessel, botol terisi)
    private GameObject[] _sampleStations = new GameObject[3];
    private GameObject[] _stationBottles = new GameObject[3];
    private Transform[] _stationFillLiquid = new Transform[3];
    private float[] _bottleFillProgress = new float[3];
    private bool[] _bottleFilling = new bool[3];
    private bool _samplingStationsBuilt;
    private float _sampleProximityRadius = 2.8f;

    // Lab building + analyzer
    private GameObject _labBuilding;
    private Transform[] _labSlotLiquids = new Transform[3];
    private Transform _labAnalyzerRotor;
    private Transform _labResultScreen;
    private bool _labBuilt;
    private bool _labSubmitted;
    private Vector3 _labDoorPos;

    // Cached handwheel state
    private HandwheelState _hw1, _hw2, _hw3;

    // Steam-closing rework runtime
    private float[] _vaporBaseRate = new float[3];   // base emission rate captured per vessel
    private float[] _steamMult = new float[3];        // per-vessel intensity multiplier
    private bool _transitioning;                       // true selama fade+teleport antar vessel

    // Autoclave -> Flash letdown valve (dibuka pemain di AWAL, sebelum FV1) + X-ray slurry
    private Transform _autoclaveValveHub;              // AutoclaveToFlash_LetdownValve_Handwheel_Hub
    private HandwheelState _hwAuto;                    // state handwheel valve autoclave
    private FlashStage _autoStage = new FlashStage { stageName = "AUTOCLAVE LETDOWN", pressureStart = 47f, pressureTarget = 47f, tempStart = 250f, tempTarget = 250f };
    private System.Collections.Generic.List<Renderer> _slurryFlowRenderers = new System.Collections.Generic.List<Renderer>();
    private float _slurryFlowPhase;                    // untuk animasi scroll/pulse slurry
    private bool _slurryFlowActive;
    private AutoclaveSlurryFlowDriver _flowDriver;     // driver plug slurry (autoclave->flash)
    private bool _flowReachedFlash;                    // flag dari driver saat front sampai flash
    private bool _interimReportDone;                   // laporan interim WT diterima
    private bool _waitingInterimReport;                // sedang menunggu laporan interim WT

    // Gating laporan WT antar-langkah (lapor dulu sebelum lanjut)
    private bool _awaitingStepReport;
    private bool _stepReportReceived;

    [System.Serializable]
    public class FlashStage
    {
        public string stageName;
        public float pressureStart;
        public float pressureTarget;
        public float pressureTolerance;
        public float tempStart;
        public float tempTarget;
        [HideInInspector] public float pressureCurrent;
        [HideInInspector] public float tempCurrent;
        [HideInInspector] public float openPercent; // 0..1
        [HideInInspector] public bool isStable;
    }

    private class HandwheelState
    {
        public Transform hub;
        public Transform[] parts;
        public Quaternion[] baseRotations;
        public Vector3[] basePositions;
        public Vector3 pivotWorld;
        public Vector3 axisWorld;
        public float degrees;
        public bool initialized;

        // XR grab tracking
        public bool grabbed;
        public bool hovered;
        public Transform interactorAttach;
        public bool yawValid;
        public float yawLast;
        // Pakai XRSimpleInteractable: deteksi "pegang" TANPA memindahkan objek.
        // (XRGrabInteractable lama bikin handwheel ketarik ke tangan -> bug bunderan ikut).
        public UnityEngine.XR.Interaction.Toolkit.Interactables.XRSimpleInteractable grab;

        // BARU: kalau pakai model L5_SteamValve_Handwheel_Redesign + GesturalHandwheel,
        // rotasi & interaksi di-handle komponen ini (mekanisme ikut tangan seperti Level 5/7).
        public GesturalHandwheel gh;
    }

    private void Awake()
    {
        _hud = FindFirstObjectByType<PlayerHUD>();
        AutoFindReferences();
    }

    private void OnEnable()
    {
        GameLevelManager.OnLevelStarted += OnLevelStarted;
        GameLevelManager.OnDCSButtonPressed += OnDcsButtonPressed;
        GameLevelManager.OnVoiceReportAccepted += OnVoiceReportAccepted;
        // Laporan interim dalam Level 8 (sebelum tiap vessel) lewat WT.
        WalkieTalkieManager.OnPTTDilepas += OnPttReleasedForStep;
    }

    private void OnDisable()
    {
        GameLevelManager.OnLevelStarted -= OnLevelStarted;
        GameLevelManager.OnDCSButtonPressed -= OnDcsButtonPressed;
        GameLevelManager.OnVoiceReportAccepted -= OnVoiceReportAccepted;
        WalkieTalkieManager.OnPTTDilepas -= OnPttReleasedForStep;
        if (_seqCoroutine != null) StopCoroutine(_seqCoroutine);
        StopAudio(_steamReleaseAudio);
        StopAudio(_alarmAudio);
    }

    // Laporan WT antar-langkah: setiap kali player lepas PTT saat kita menunggu laporan step,
    // anggap laporan diterima (frasa bebas) lalu lanjut.
    private void OnPttReleasedForStep()
    {
        if (_levelActive && _awaitingStepReport) _stepReportReceived = true;
    }

    private void OnLevelStarted(GameLevelManager.GameLevel level)
    {
        if (level == GameLevelManager.GameLevel.Level8_Monitoring)
        {
            ActivateLevel();
        }
        else
        {
            _levelActive = false;
            _phase = Phase.Idle;
            // Ganti level: matikan SEMUA animasi Level 8 (slurry autoclave->flash, uap, audio).
            StopAllLevel8Animations();
        }
    }

    // Matikan semua animasi/FX Level 8 saat pindah level (anti-nyangkut di level belakang).
    private void StopAllLevel8Animations()
    {
        if (_seqCoroutine != null) { StopCoroutine(_seqCoroutine); _seqCoroutine = null; }
        _transitioning = false;
        _waitingInterimReport = false;
        if (_flowDriver != null) _flowDriver.StopFlow(true);   // hentikan + sembunyikan plug slurry
        _slurryFlowActive = false;
        ResolveSlurryFlowRenderers();
        SetStaticSlurryVisible(false);
        StopVaporFX(_fv1VaporFX);
        StopVaporFX(_fv2VaporFX);
        StopVaporFX(_fv3VaporFX);
        StopAudio(_steamReleaseAudio);
        StopAudio(_alarmAudio);
    }

    private void ActivateLevel()
    {
        _levelActive = true;
        AutoFindReferences();
        ResetAllStages();
        InitHandwheelStates();
        InitCascadePanels();
        _phase = Phase.MenungguDcs;
        for (int i = 0; i < 3; i++) _sampleTaken[i] = false;
        CleanupSampleBottles();
        CleanupVaporFX();
        _labSubmitted = false;
        if (_labBuilding != null) _labBuilding.SetActive(false);
        if (_missionCompleteCanvas != null) _missionCompleteCanvas.SetActive(false);
        if (_labQcCanvas != null) _labQcCanvas.SetActive(false);
        Debug.Log("[Level8] Flash Train activated. Phase=MenungguDcs.");
    }

    private void OnDcsButtonPressed(int nomorTombol)
    {
        if (!_levelActive || nomorTombol != 8) return;
        if (_phase != Phase.MenungguDcs) return;
        StartSequence(StartSequenceCoroutine());
    }

    private IEnumerator StartSequenceCoroutine()
    {
        _phase = Phase.TeleportField;
        float d = Mathf.Max(2f, _fadeTransitionDuration);
        if (_hud != null) _hud.PlayManualFade(d);
        yield return new WaitForSeconds(d * 0.5f);
        // Teleport ke valve letdown autoclave. Spawn DIDEPAN OuterRing handwheel valve.
        Transform outerRing = FindByNameInactive("AutoclaveToFlash_LetdownValve_Handwheel_OuterRing");
        Transform firstTarget = outerRing != null ? outerRing
            : (_autoclaveValveHub != null ? _autoclaveValveHub : (_spawnFv1 != null ? _spawnFv1 : _teleportTargetField));
        // Spawn berdiri ~3m di depan valve menghadap valve.
        _teleportTargetField = MakeStandSpot(firstTarget, _spawnFv1);
        TeleportPlayerToField();
        yield return new WaitForSeconds(d * 0.5f);

        EnsureSteamReleaseAudio();

        // AWAL: sembunyikan batang kuning statis + plug aliran (belum ada animasi sampai valve dibuka).
        ResolveSlurryFlowRenderers();
        SetStaticSlurryVisible(false);
        if (_flowDriver != null) _flowDriver.StopFlow(true);
        _slurryFlowActive = false;

        // FASE AWAL: buka valve letdown autoclave -> flash. Slurry panas mulai mengalir di pipa X-ray.
        if (_autoclaveValveHub != null)
        {
            _hwAuto = BuildHandwheelState(_autoclaveValveHub);
            EnsureHandwheelInteractable(_hwAuto);
            ResetStage(_autoStage);
            _phase = Phase.OpenAutoclaveValve;
            if (_hud != null) _hud.ShowNotifPublic(
                "Buka valve letdown Autoclave→Flash: putar handwheel di atas valve (CW). Slurry panas 250°C akan mengalir ke Flash Vessel.", 9f);
        }
        else
        {
            // Fallback: kalau valve tidak ada, langsung ke FV1 (perilaku lama).
            if (_hud != null) _hud.ShowNotifPublic(_msgStart, 8f);
            EnterVesselPhase(0);
        }
    }

    // Buat titik berdiri ~2.5m dari target (arah ke spawn fallback), menghadap target.
    private Transform MakeStandSpot(Transform target, Transform fallback)
    {
        if (target == null) return fallback;
        var existing = GameObject.Find("SpawnPoint_Lvl8_AutoValve_Runtime");
        var sp = existing != null ? existing : new GameObject("SpawnPoint_Lvl8_AutoValve_Runtime");
        Vector3 dir = new Vector3(1f, 0f, 0f); // berdiri di sisi +X valve (arah player umum)
        // Berdiri ~3.0m di depan valve supaya handwheel terlihat penuh tanpa menutupi layar.
        Vector3 pos = target.position + dir * 3.0f;
        pos.y = Mathf.Max(0.1f, target.position.y - 1.55f);
        sp.transform.position = pos;
        Vector3 look = target.position - pos; look.y = 0f;
        sp.transform.rotation = look.sqrMagnitude > 0.001f ? Quaternion.LookRotation(look.normalized, Vector3.up) : Quaternion.identity;
        return sp.transform;
    }

    // Mulai fase satu vessel: uap keluar BESAR di steam riser-nya, audio sesuai intensitas vessel.
    private void EnterVesselPhase(int idx)
    {
        _transitioning = false;
        Transform anchor = idx == 0 ? _steamAnchor1 : idx == 1 ? _steamAnchor2 : _steamAnchor3;
        float mult = idx == 0 ? _steamMultFv1 : idx == 1 ? _steamMultFv2 : _steamMultFv3;
        _steamMult[idx] = mult;

        if (idx == 0) { EnsureVaporFX(anchor, ref _fv1VaporFX); StartVaporFX(_fv1VaporFX); _phase = Phase.OpenFV1; _vaporBaseRate[0] = 45f; SetVaporIntensity(_fv1VaporFX, 45f, mult); }
        else if (idx == 1) { EnsureVaporFX(anchor, ref _fv2VaporFX); StartVaporFX(_fv2VaporFX); _phase = Phase.OpenFV2; _vaporBaseRate[1] = 45f; SetVaporIntensity(_fv2VaporFX, 45f, mult); }
        else { EnsureVaporFX(anchor, ref _fv3VaporFX); StartVaporFX(_fv3VaporFX); _phase = Phase.OpenFV3; _vaporBaseRate[2] = 45f; SetVaporIntensity(_fv3VaporFX, 45f, mult); }

        // Audio uap: vessel pertama paling keras, makin kecil tiap vessel.
        EnsureSteamReleaseAudio();
        StartAudio(_steamReleaseAudio, Mathf.Max(0.05f, _steamReleaseVolume * mult));
    }

    private void Update()
    {
        if (!_levelActive) return;

        // Track handwheel rotation untuk semua 3 vessel
        if (_phase == Phase.OpenAutoclaveValve) UpdateAutoclaveValve();
        if (_phase == Phase.OpenFV1) UpdateHandwheel(_hw1, _fv1, _key1Open);
        if (_phase == Phase.OpenFV2) UpdateHandwheel(_hw2, _fv2, _key2Open);
        if (_phase == Phase.OpenFV3) UpdateHandwheel(_hw3, _fv3, _key3Open);

        // Animasi slurry panas X-ray mengalir di pipa autoclave->flash.
        UpdateSlurryFlowAnim();

        // Update slurry pool visibility based on stage progress
        UpdateSlurryPoolVisuals();
        UpdateCascadePanelTexts();

        // Check stage transitions
        CheckStageProgress();

        // Sampling Level 8 dipindahkan ke Level 9 CCD. Legacy phase ini ditutup
        // supaya player tidak lagi diarahkan mengambil sample dari flash vessel.
        if (_phase == Phase.Sampling)
        {
            GameLevelManager.Instance?.NotifyLevel8FlashLetdownDone();
            GameLevelManager.Instance?.NotifyLevel8SampleTaken();
            _phase = Phase.MenungguLapor;
            if (_hud != null) _hud.ShowNotifPublic(_msgFv3Done, 6f);
        }
    }

    // ============================================================
    //  HANDWHEEL ROTATION (10-turn, world-axis stable)
    // ============================================================

    // ============================================================
    //  HANDWHEEL ROTATION (10-turn, world-axis stable)
    // ============================================================

    private void UpdateAutoclaveValve()
    {
        if (_transitioning) return;
        var hw = _hwAuto;
        if (hw == null || !hw.initialized)
        {
            _autoStage.openPercent = Mathf.Clamp01(_autoStage.openPercent + Time.deltaTime * 0.35f);
        }
        else if (hw.gh != null)
        {
            // Model L5 + GesturalHandwheel: baca bukaan dari komponen (ikut tangan).
            _autoStage.openPercent = Mathf.Clamp01(hw.gh.OpenPercent01);
        }
        else
        {
            float deltaDeg = 0f;
            if (Input.GetKey(_key1Open)) deltaDeg += 360f * Time.deltaTime; // fallback keyboard (key 1)
            deltaDeg += GetGesturalDelta(hw);
            if (Mathf.Abs(deltaDeg) > 0.0001f)
            {
                hw.degrees = Mathf.Clamp(hw.degrees + deltaDeg, 0f, _handwheelFullOpenDegrees);
                ApplyHandwheelRotation(hw);
                _autoStage.openPercent = Mathf.Clamp01(hw.degrees / _handwheelFullOpenDegrees);
            }
        }

        // Valve penuh terbuka -> MULAI aliran slurry (plug muncul dari autoclave menuju flash).
        if (_autoStage.openPercent >= 0.99f && !_transitioning)
        {
            StartSequence(AutoValveOpenedSequence());
        }
    }

    // Setelah valve dibuka: slurry mengalir -> tunggu sampai sentuh flash vessel -> uap & suara
    // build-up perlahan -> wajib lapor WT "autoclave dibuka menuju flash vessel" -> balasan -> pindah FV1.
    private IEnumerator AutoValveOpenedSequence()
    {
        _transitioning = true;
        if (_flowDriver != null) _flowDriver.StartFlow();
        _slurryFlowActive = true;
        if (_hud != null) _hud.ShowNotifPublic("Valve terbuka. Slurry panas 250°C mengalir dari Autoclave menuju Flash Vessel...", 6f);

        // 1) Tunggu slurry SAMPAI ke flash vessel (front progress ~1.0).
        float fallback = 0f;
        while (_flowDriver != null && _flowDriver.FrontProgress01 < 0.98f && fallback < 12f)
        {
            fallback += Time.deltaTime;
            yield return null;
        }

        // 2) Begitu slurry menyentuh FV1: uap PERLAHAN muncul + suara build-up (bukan langsung kencang).
        if (_hud != null) _hud.ShowNotifPublic("Slurry mencapai Flash Vessel. Uap mulai keluar...", 5f);
        EnsureVaporFX(_steamAnchor1, ref _fv1VaporFX); StartVaporFX(_fv1VaporFX);
        _vaporBaseRate[0] = 45f;
        EnsureSteamReleaseAudio();
        float t = 0f, ramp = 4.5f;   // build-up 4.5 detik
        while (t < ramp)
        {
            t += Time.deltaTime;
            float k = Mathf.SmoothStep(0f, 1f, t / ramp);     // perlahan naik
            SetVaporIntensity(_fv1VaporFX, _vaporBaseRate[0], k * _steamMultFv1);
            StartAudio(_steamReleaseAudio, Mathf.Max(0.04f, _steamReleaseVolume * _steamMultFv1 * k));
            yield return null;
        }

        // 3) Setelah suara kencang -> WAJIB lapor WT.
        if (_hud != null) _hud.ShowNotifPublic("Uap stabil & kencang. Lapor HT (tahan T): 'Autoclave telah dibuka menuju Flash Vessel.'", 8f);
        yield return WaitForInterimReport();

        // 4) Balasan diterima -> fade + teleport ke FV1.
        float d = Mathf.Max(1f, _interVesselFadeDuration);
        if (_hud != null) _hud.PlayManualFade(d);
        yield return new WaitForSeconds(d * 0.5f);
        if (_spawnFv1 != null) { _teleportTargetField = _spawnFv1; TeleportPlayerToField(); }
        yield return new WaitForSeconds(d * 0.5f);

        // 5) Sebelum buka FV1 -> wajib lapor dulu, baru handwheel FV1 aktif.
        yield return PreVesselReport(0);
        EnterVesselPhase(0);
        _seqCoroutine = null;
    }

    // Tunggu pemain lapor via WT (interim, TIDAK menyelesaikan level). Balasan NPC dimainkan.
    private IEnumerator WaitForInterimReport()
    {
        _interimReportDone = false;
        _waitingInterimReport = true;
        // Subscribe sekali ke PTT release -> anggap laporan terkirim.
        WalkieTalkieManager.OnPTTDilepas += OnInterimPtt;
        float guard = 0f;
        while (!_interimReportDone)
        {
            guard += Time.deltaTime;
            yield return null;
        }
        WalkieTalkieManager.OnPTTDilepas -= OnInterimPtt;
        _waitingInterimReport = false;
        // Balasan NPC (pakai audio balasan level 8 kalau ada).
        yield return PlayNpcReply();
    }

    private void OnInterimPtt()
    {
        if (_waitingInterimReport) _interimReportDone = true;
    }

    // Mainkan balasan NPC (audio HT asli + SFX static) untuk laporan interim.
    private IEnumerator PlayNpcReply()
    {
        var wtm = WalkieTalkieManager.Instance;
        if (wtm != null) wtm.MainkanBalasanInterim();   // SFX static + suara balasan level aktif
        if (_hud != null) _hud.ShowNotifPublic("HT: \"Copy. Diterima.\"", 3f);
        yield return new WaitForSeconds(2.2f);
    }

    // Lapor SEBELUM membuka tiap vessel: "Flash Vessel fase ke-N akan dibuka." -> balasan -> lanjut.
    private IEnumerator PreVesselReport(int idx)
    {
        string[] fase = { "pertama", "kedua", "ketiga" };
        string nm = idx >= 0 && idx < 3 ? fase[idx] : (idx + 1).ToString();
        if (_hud != null) _hud.ShowNotifPublic($"Lapor HT (tahan T): 'Flash Vessel fase {nm} akan dibuka.'", 8f);
        yield return WaitForInterimReport();
        if (_hud != null) _hud.ShowNotifPublic($"Diterima. Putar handwheel Flash Vessel {idx + 1} untuk menutup uap.", 5f);
    }
    private float GetGesturalDelta(HandwheelState hw)
    {
        if ((hw.grabbed || hw.hovered) && hw.interactorAttach != null)
        {
            // TWIST tangan (orientasi controller) — versi yang terbukti bisa diputar dgn ray/hover.
            Vector3 axis = hw.axisWorld;
            Vector3 handVec = hw.interactorAttach.up;
            Vector3 projected = Vector3.ProjectOnPlane(handVec, axis);
            if (projected.sqrMagnitude < 0.02f) { handVec = hw.interactorAttach.right; projected = Vector3.ProjectOnPlane(handVec, axis); }
            if (projected.sqrMagnitude > 0.0001f)
            {
                projected.Normalize();
                Vector3 refF = Vector3.ProjectOnPlane(Vector3.up, axis);
                if (refF.sqrMagnitude < 0.0001f) refF = Vector3.ProjectOnPlane(Vector3.right, axis);
                refF.Normalize();
                float yawNow = Vector3.SignedAngle(refF, projected, axis);
                if (!hw.yawValid) { hw.yawLast = yawNow; hw.yawValid = true; }
                else { float d = Mathf.DeltaAngle(hw.yawLast, yawNow); hw.yawLast = yawNow; if (Mathf.Abs(d) > 60f) d = 0f; return d * Mathf.Max(1f, _gesturalGain); }
            }
        }
        else hw.yawValid = false;
        return 0f;
    }

    private void UpdateHandwheel(HandwheelState hw, FlashStage stage, KeyCode debugKey)
    {
        if (hw == null || !hw.initialized) return;

        // === MODE BARU: GesturalHandwheel (model L5) — baca OpenPercent01 langsung. ===
        if (hw.gh != null)
        {
            float prevOpen = stage.openPercent;
            stage.openPercent = Mathf.Clamp01(hw.gh.OpenPercent01);
            if (Mathf.Abs(stage.openPercent - prevOpen) < 0.0001f) return;
            stage.pressureCurrent = Mathf.Lerp(stage.pressureStart, stage.pressureTarget, stage.openPercent);
            stage.tempCurrent = Mathf.Lerp(stage.tempStart, stage.tempTarget, stage.openPercent);
            int gi = stage == _fv1 ? 0 : stage == _fv2 ? 1 : 2;
            float gMult = _steamMult[gi] > 0f ? _steamMult[gi] : (gi == 0 ? _steamMultFv1 : gi == 1 ? _steamMultFv2 : _steamMultFv3);
            float gRemain = 1f - stage.openPercent;
            ParticleSystem gps = gi == 0 ? _fv1VaporFX : gi == 1 ? _fv2VaporFX : _fv3VaporFX;
            SetVaporIntensity(gps, _vaporBaseRate[gi], gRemain * gMult);
            StartAudio(_steamReleaseAudio, Mathf.Max(0.05f, _steamReleaseVolume * gMult * gRemain));
            return;
        }

        float deltaDeg = 0f;

        // 1) Keyboard fallback (simulator/desktop): tahan tombol 1/2/3 untuk memutar BUKA (CW).
        //    Tahan Shift+key untuk memutar TUTUP (CCW), tapi gak diizinkan di SOP — biarkan jalan satu arah.
        if (Input.GetKey(debugKey)) deltaDeg += 360f * Time.deltaTime; // 1 putaran/detik

        // 2) REAL VR FEEL (gaya Level 6 TrackWheelRotation): ukur POSISI tangan player
        //    relatif ke pusat wheel, proyeksikan ke bidang disc, lalu hitung sudut.
        //    Saat tangan bergerak tangensial mengelilingi wheel (cara natural memutar
        //    setir), sudut bergeser dan wheel ikut berputar real-time. Lebih riil & immersive
        //    daripada twist orientasi pergelangan.
        deltaDeg += GetGesturalDelta(hw);

        if (Mathf.Abs(deltaDeg) < 0.0001f) return;

        hw.degrees = Mathf.Clamp(hw.degrees + deltaDeg, 0f, _handwheelFullOpenDegrees);
        ApplyHandwheelRotation(hw);

        // Update stage open percent
        stage.openPercent = Mathf.Clamp01(hw.degrees / _handwheelFullOpenDegrees);
        stage.pressureCurrent = Mathf.Lerp(stage.pressureStart, stage.pressureTarget, stage.openPercent);
        stage.tempCurrent = Mathf.Lerp(stage.tempStart, stage.tempTarget, stage.openPercent);

        // MEKANIK: memutar handwheel = MENUTUP valve uap. Makin diputar (openPercent naik),
        // uap makin MENGECIL sampai habis, lalu cairan mengalir ke vessel berikutnya.
        int idx = stage == _fv1 ? 0 : stage == _fv2 ? 1 : 2;
        float vesselMult = _steamMult[idx] > 0f ? _steamMult[idx] : (idx == 0 ? _steamMultFv1 : idx == 1 ? _steamMultFv2 : _steamMultFv3);
        float steamRemain = 1f - stage.openPercent;               // 1=uap penuh, 0=tertutup
        ParticleSystem ps = idx == 0 ? _fv1VaporFX : idx == 1 ? _fv2VaporFX : _fv3VaporFX;
        SetVaporIntensity(ps, _vaporBaseRate[idx], steamRemain * vesselMult);

        // Audio: uap masih kuat saat valve terbuka, mengecil saat ditutup. Skala per vessel.
        StartAudio(_steamReleaseAudio, Mathf.Max(0.05f, _steamReleaseVolume * vesselMult * steamRemain));
    }

    // Atur kuat-lemah uap: emission rate + start speed + size proportional ke intensitas.
    private void SetVaporIntensity(ParticleSystem ps, float baseRate, float intensity)
    {
        if (ps == null) return;
        intensity = Mathf.Clamp01(intensity);
        var emission = ps.emission;
        float rate = (baseRate > 0f ? baseRate : 45f) * intensity;
        emission.rateOverTime = rate;
        if (intensity <= 0.02f)
        {
            if (ps.isPlaying) ps.Stop(true, ParticleSystemStopBehavior.StopEmitting);
        }
        else if (!ps.isPlaying)
        {
            ps.Play();
        }
    }

    private void ApplyHandwheelRotation(HandwheelState hw)
    {
        if (hw.parts == null) return;
        Quaternion delta = Quaternion.AngleAxis(hw.degrees, hw.axisWorld);
        for (int i = 0; i < hw.parts.Length; i++)
        {
            if (hw.parts[i] == null) continue;
            hw.parts[i].rotation = delta * hw.baseRotations[i];
            Vector3 offset = hw.basePositions[i] - hw.pivotWorld;
            offset = delta * offset;
            hw.parts[i].position = hw.pivotWorld + offset;
        }
    }

    private void InitHandwheelStates()
    {
        // Deck handwheel sudah dibuat di AutoFindReferences. Di sini tinggal bangun state + interactable.
        _hw1 = BuildHandwheelState(_fv1HandwheelHub);
        _hw2 = BuildHandwheelState(_fv2HandwheelHub);
        _hw3 = BuildHandwheelState(_fv3HandwheelHub);
        EnsureHandwheelInteractable(_hw1);
        EnsureHandwheelInteractable(_hw2);
        EnsureHandwheelInteractable(_hw3);
    }

    // Handwheel runtime yang dibuat di deck (kalau handwheel asli posisinya tinggi/terhalang).
    private Transform _deckHwRoot;

    private void EnsureRuntimeDeckHandwheels()
    {
        // PAKAI HANDWHEEL ASLI dari FBX flash vessel v2 (sudah ada di scene, tepat di tiap vessel,
        // dekat spawn point). Hub-nya: FV1_To_FV2_..._BypassHandwheel_Hub (Z~102),
        // FV2_To_FV3_..._BypassHandwheel_Hub (Z~105), FV3_SteamValve_Handwheel_Hub (Z~108).
        // Ini menghilangkan bug "handwheel jatuh / di tempat salah" dari runtime deck handwheel.
        Transform hw1 = FindFlashHandwheelHub("FV1_To_FV2_InterstageLetdownValve_BypassHandwheel_Hub");
        Transform hw2 = FindFlashHandwheelHub("FV2_To_FV3_InterstageLetdownValve_BypassHandwheel_Hub");
        Transform hw3 = FindFlashHandwheelHub("FV3_SteamValve_Handwheel_Hub");

        if (hw1 != null) _fv1HandwheelHub = hw1;
        if (hw2 != null) _fv2HandwheelHub = hw2;
        if (hw3 != null) _fv3HandwheelHub = hw3;

        // Bersihkan deck handwheel runtime lama (X=-61) kalau ada — itu sumber bug "jatuh".
        var oldDeck = GameObject.Find("L8_DeckHandwheels_Runtime");
        if (oldDeck != null) SafeDestroy(oldDeck);

        // Fallback terakhir: kalau hub FBX tidak ketemu, pakai L5 condensate handwheel lama.
        if (_fv1HandwheelHub == null) _fv1HandwheelHub = FindCondensateHandwheelHub("(1)");
        if (_fv2HandwheelHub == null) _fv2HandwheelHub = FindCondensateHandwheelHub("(2)");
        if (_fv3HandwheelHub == null) _fv3HandwheelHub = FindCondensateHandwheelHub("(3)");
    }

    /// <summary>Cari Hub handwheel FBX flash vessel (nama persis) di area flash vessel.</summary>
    private Transform FindFlashHandwheelHub(string exactName)
    {
        foreach (var tr in FindObjectsByType<Transform>(FindObjectsInactive.Include, FindObjectsSortMode.None))
        {
            if (tr == null || !tr.gameObject.scene.IsValid()) continue;
            if (tr.name != exactName) continue;
            if (tr.position.x > -40f) continue; // area flash vessel
            return tr;
        }
        return null;
    }

    /// <summary>
    /// Cari Hub handwheel L5_Condensate_Drain_Handwheel dengan suffix tertentu (mis "(1)")
    /// yang ada di area flash vessel (X &lt; -40). Return Hub transform sebagai pivot.
    /// </summary>
    private Transform FindCondensateHandwheelHub(string suffix)
    {
        Transform best = null;
        foreach (var tr in FindObjectsByType<Transform>(FindObjectsInactive.Include, FindObjectsSortMode.None))
        {
            if (tr == null || !tr.gameObject.scene.IsValid()) continue;
            if (!tr.name.StartsWith("L5_Condensate_Drain_Handwheel_Hub")) continue;
            if (!tr.name.EndsWith(suffix)) continue;
            if (tr.position.x > -40f) continue; // hanya yang di area flash vessel
            best = tr;
            break;
        }
        return best;
    }

    private Transform BuildDeckHandwheel(string name, Vector3 pos, Transform existingHub)
    {
        // Kalau sudah pernah dibuat, reuse.
        var found = GameObject.Find(name);
        Transform hubT;
        if (found != null)
        {
            hubT = found.transform;
        }
        else
        {
            // Root hub (pivot rotasi) — identity rotation. Disc menghadap +X (arah player),
            // wheel berputar di sumbu X dunia.
            var hubGO = new GameObject(name);
            hubGO.transform.SetParent(_deckHwRoot, false);
            hubGO.transform.position = pos;
            hubGO.transform.rotation = Quaternion.identity;

            // Visual: rim (cylinder pipih, sumbu disc = X) + 4 spoke (bar di bidang YZ) + hub.
            var rim = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            rim.name = "Rim";
            rim.transform.SetParent(hubGO.transform, false);
            // Cylinder default sumbu Y. Rotate 90 di Z => sumbu cylinder jadi X (disc menghadap X).
            rim.transform.localRotation = Quaternion.Euler(0f, 0f, 90f);
            // Scale: (radiusXZ, tebalY->X, radiusXZ). Tebal kecil di X, diameter besar.
            rim.transform.localScale = new Vector3(1.1f, 0.10f, 1.1f);
            var rimCol = rim.GetComponent<Collider>(); if (rimCol != null) SafeDestroy(rimCol);
            ApplyHandwheelMaterial(rim.GetComponent<Renderer>());

            for (int i = 0; i < 4; i++)
            {
                var spoke = GameObject.CreatePrimitive(PrimitiveType.Cube);
                spoke.name = "Spoke_" + i;
                spoke.transform.SetParent(hubGO.transform, false);
                // Spoke bar di bidang YZ (tegak lurus sumbu X). Putar di sumbu X.
                spoke.transform.localRotation = Quaternion.Euler(45f * i, 0f, 0f);
                spoke.transform.localPosition = Vector3.zero;
                spoke.transform.localScale = new Vector3(0.09f, 0.09f, 2.0f);
                var sc = spoke.GetComponent<Collider>(); if (sc != null) SafeDestroy(sc);
                ApplyHandwheelMaterial(spoke.GetComponent<Renderer>());
            }
            var hubCenter = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            hubCenter.name = "Hub";
            hubCenter.transform.SetParent(hubGO.transform, false);
            hubCenter.transform.localScale = Vector3.one * 0.3f;
            var hc = hubCenter.GetComponent<Collider>(); if (hc != null) SafeDestroy(hc);
            ApplyHandwheelMaterial(hubCenter.GetComponent<Renderer>());

            hubT = hubGO.transform;
        }
        return hubT;
    }

    private Material _handwheelMat;
    private void ApplyHandwheelMaterial(Renderer r)
    {
        if (r == null) return;
        if (_handwheelMat == null)
        {
            Shader sh = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard");
            _handwheelMat = new Material(sh);
            // Oranye industri supaya jelas terlihat sebagai kontrol yang bisa diputar.
            Color orange = new Color(0.95f, 0.45f, 0.05f);
            if (_handwheelMat.HasProperty("_BaseColor")) _handwheelMat.SetColor("_BaseColor", orange);
            if (_handwheelMat.HasProperty("_Color")) _handwheelMat.SetColor("_Color", orange);
            if (_handwheelMat.HasProperty("_Metallic")) _handwheelMat.SetFloat("_Metallic", 0.3f);
            if (_handwheelMat.HasProperty("_Smoothness")) _handwheelMat.SetFloat("_Smoothness", 0.5f);
        }
        r.sharedMaterial = _handwheelMat;
    }

    private void EnsureHandwheelInteractable(HandwheelState hw)
    {
        if (hw == null || hw.hub == null) return;
        // Kalau pakai model L5 + GesturalHandwheel, komponen itu sudah pasang collider+interactable
        // sendiri di clone. Jangan tambah interactable di hub (hindari dobel/bentrok).
        if (hw.gh != null) return;
        Transform target = hw.hub;
        var go = target.gameObject;

        try
        {
            // Collider untuk area select. NON-trigger supaya XR interactor (ray/sphere cast) bisa hit.
            // Radius besar supaya gampang di-target di VR/simulator.
            Collider col = target.GetComponent<Collider>();
            var sphere = col as SphereCollider;
            if (sphere == null)
            {
                sphere = go.AddComponent<SphereCollider>();
                col = sphere;
            }
            sphere.radius = LocalRadiusForWorld(target, 0.7f);
            sphere.isTrigger = false;

            // BUANG XRGrabInteractable + Rigidbody kalau ada (sisa versi lama) supaya objek
            // TIDAK ketarik mengikuti tangan. Handwheel hanya BERPUTAR di tempat.
            var oldGrab = target.GetComponent<UnityEngine.XR.Interaction.Toolkit.Interactables.XRGrabInteractable>();
            if (oldGrab != null) Destroy(oldGrab);
            var rb = target.GetComponent<Rigidbody>();
            if (rb != null) Destroy(rb);

            // XRSimpleInteractable: deteksi pegang TANPA memindahkan objek.
            var simple = target.GetComponent<UnityEngine.XR.Interaction.Toolkit.Interactables.XRSimpleInteractable>();
            if (simple == null) simple = go.AddComponent<UnityEngine.XR.Interaction.Toolkit.Interactables.XRSimpleInteractable>();
            hw.grab = simple;
            if (simple == null) return;

            // PENTING: daftarkan collider ke interactable secara EKSPLISIT. Kalau collider
            // ditambah via script, list 'colliders' interactable kosong (=0) -> XR interactor
            // gak bisa select -> handwheel gak bisa diputar. Ini bug utama sebelumnya.
            simple.colliders.Clear();
            foreach (var c in go.GetComponents<Collider>())
                if (c != null) simple.colliders.Add(c);
            // Refresh registrasi collider di interaction manager.
            simple.enabled = false;
            simple.enabled = true;

            simple.selectEntered.RemoveAllListeners();
            simple.selectExited.RemoveAllListeners();
            simple.hoverEntered.RemoveAllListeners();
            simple.hoverExited.RemoveAllListeners();
            simple.selectEntered.AddListener((args) =>
            {
                hw.grabbed = true;
                hw.interactorAttach = args.interactorObject != null ? args.interactorObject.transform : null;
                hw.yawValid = false;
            });
            simple.selectExited.AddListener((args) =>
            {
                hw.grabbed = false;
                hw.interactorAttach = null;
                hw.yawValid = false;
            });
            // Hover juga memutar (gampang: cukup arahkan ray ke handwheel, gak harus klik select).
            simple.hoverEntered.AddListener((args) =>
            {
                hw.hovered = true;
                hw.interactorAttach = args.interactorObject != null ? args.interactorObject.transform : hw.interactorAttach;
            });
            simple.hoverExited.AddListener((args) =>
            {
                hw.hovered = false;
            });
        }
        catch (System.Exception e)
        {
            Debug.LogWarning($"[Level8] Gagal setup interactable untuk handwheel '{go.name}': {e.Message}. Handwheel tetap bisa diputar via keyboard 1/2/3.");
        }
    }

    // Pasang model L5_SteamValve_Handwheel_Redesign + GesturalHandwheel pada valve hub.
    // Mekanisme berputar ikut tangan (sama Level 5/7). Sembunyikan handwheel FBX lama.
    private GesturalHandwheel EnsureL5GesturalWheel(Transform valveHub, KeyCode key)
    {
        if (valveHub == null) return null;
        string cloneName = "L8_L5Wheel_" + valveHub.name;
        Transform existing = valveHub.Find(cloneName);
        GameObject clone;
        if (existing != null)
        {
            clone = existing.gameObject;
        }
        else
        {
            Transform src = FindByNameInactive("L5_SteamValve_Handwheel_Redesign");
            if (src == null) return null;
            clone = Instantiate(src.gameObject);
            clone.name = cloneName;
            clone.transform.SetParent(valveHub, true);
            clone.transform.position = valveHub.position;
            clone.transform.rotation = valveHub.rotation;
            clone.transform.localScale = Vector3.one;
            clone.SetActive(true);
            foreach (var b in clone.GetComponentsInChildren<MonoBehaviour>(true))
                if (b != null && !(b is GesturalHandwheel)) b.enabled = false;
        }
        HideOldFbxHandwheel(valveHub, clone.transform);
        var gh = clone.GetComponent<GesturalHandwheel>();
        if (gh == null) gh = clone.AddComponent<GesturalHandwheel>();
        gh.fullOpenDegrees = _handwheelFullOpenDegrees;
        gh.gesturalGain = 1.0f;
        gh.debugKey = key;
        gh.Setup(clone.transform, null);
        return gh;
    }

    // Sembunyikan renderer handwheel FBX lama (hub + sibling OuterRing/Spoke/Hub set yang sama)
    // supaya tidak dobel dengan model L5 baru. JANGAN sembunyikan clone (keep).
    private void HideOldFbxHandwheel(Transform hub, Transform keep)
    {
        var hr = hub.GetComponent<Renderer>();
        if (hr != null) hr.enabled = false;
        if (hub.parent != null)
        {
            foreach (Transform sib in hub.parent)
            {
                if (sib == hub || sib == keep || sib.IsChildOf(keep)) continue;
                if (Vector3.Distance(sib.position, hub.position) > 1.2f) continue;
                if (sib.name.Contains("OuterRing") || sib.name.Contains("Spoke") || sib.name.Contains("Hub"))
                    foreach (var r in sib.GetComponentsInChildren<Renderer>(true))
                        if (r != null && !r.transform.IsChildOf(keep)) r.enabled = false;
            }
        }
    }

    private HandwheelState BuildHandwheelState(Transform hub)
    {
        if (hub == null) return null;
        var hw = new HandwheelState();
        hw.hub = hub;
        hw.pivotWorld = hub.position;

        // === BARU: pakai model L5_SteamValve_Handwheel_Redesign + GesturalHandwheel ===
        // Mekanisme PERSIS Level 5/7: wheel berputar mengikuti gerakan tangan player.
        KeyCode key = hub == _fv2HandwheelHub ? _key2Open : hub == _fv3HandwheelHub ? _key3Open : _key1Open;
        var gh = EnsureL5GesturalWheel(hub, key);
        if (gh != null)
        {
            hw.gh = gh;
            hw.initialized = true;
            return hw; // GesturalHandwheel meng-handle rotasi + collider/hover/select sendiri.
        }
        // (Fallback ke mekanisme lama di bawah kalau model L5 tidak ketemu.)

        // Deck handwheel runtime (nama "L8_..._DeckHandwheel"): disc menghadap +X,
        // jadi spin di sumbu X dunia. parts = hub saja (rim/spoke CHILD ikut berputar).
        if (hub.name.StartsWith("L8_") && hub.name.Contains("DeckHandwheel"))
        {
            hw.axisWorld = Vector3.right;
            hw.parts = new[] { hub };
            hw.baseRotations = new[] { hub.rotation };
            hw.basePositions = new[] { hub.position };
            hw.degrees = 0f;
            hw.initialized = true;
            return hw;
        }

        // Handwheel FBX flash vessel v2: hub = FV*_..._BypassHandwheel_Hub / FV3_SteamValve_Handwheel_Hub.
        // PENTING: origin GROUP jauh dari pusat disc (offset ~8m), jadi JANGAN putar group di originnya
        // (nanti disc-nya ngorbit/"jatuh"). Putar tiap part (hub+ring+spoke) di PUSAT DISC (= hub.position),
        // sumbu = world X (disc flat di bidang YZ). Ini bikin stir berputar di porosnya seperti roda.
        if (hub.name.StartsWith("FV1_To_FV2") || hub.name.StartsWith("FV2_To_FV3") || hub.name.StartsWith("FV3_SteamValve"))
        {
            hw.axisWorld = Vector3.right;          // disc normal = X
            hw.pivotWorld = hub.position;          // pusat disc (poros stir)

            var fbxParts = new List<Transform>();
            fbxParts.Add(hub);
            Transform grp = hub.parent;
            if (grp != null)
            {
                foreach (Transform sib in grp)
                {
                    if (sib == hub) continue;
                    if (sib.name.Contains("OuterRing") || sib.name.Contains("Spoke") || sib.name.Contains("Hub"))
                        fbxParts.Add(sib);
                }
            }
            hw.parts = fbxParts.ToArray();
            hw.baseRotations = new Quaternion[hw.parts.Length];
            hw.basePositions = new Vector3[hw.parts.Length];
            for (int i = 0; i < hw.parts.Length; i++)
            {
                hw.baseRotations[i] = hw.parts[i].rotation;
                hw.basePositions[i] = hw.parts[i].position;
            }
            hw.degrees = 0f;
            hw.initialized = true;
            return hw;
        }

        // Handwheel asli L5_Condensate_Drain_Handwheel: disc berada di bidang YZ,
        // hub.up menunjuk ±X (sumbu putar = world X). Ada 3 set dengan nama mirip di scene,
        // jadi grup hanya sibling yang BERADA DI POSISI HUB YANG SAMA (jarak < 0.6m).
        if (hub.name.StartsWith("L5_Condensate_Drain_Handwheel"))
        {
            Vector3 axisL5 = hub.up.normalized;
            if (axisL5.sqrMagnitude < 0.001f) axisL5 = Vector3.right;
            // Snap ke world X kalau dominan X.
            if (Mathf.Abs(axisL5.x) > 0.9f) axisL5 = new Vector3(Mathf.Sign(axisL5.x), 0f, 0f);
            hw.axisWorld = axisL5;

            var partsL5 = new List<Transform>();
            partsL5.Add(hub);
            if (hub.parent != null)
            {
                foreach (Transform sib in hub.parent)
                {
                    if (sib == hub) continue;
                    if (!sib.name.StartsWith("L5_Condensate_Drain_Handwheel")) continue;
                    // Hanya part dari SET YANG SAMA (posisi dekat hub).
                    if (Vector3.Distance(sib.position, hub.position) > 0.7f) continue;
                    if (sib.name.Contains("OuterRing") || sib.name.Contains("Spoke") || sib.name.Contains("Hub"))
                        partsL5.Add(sib);
                }
            }
            hw.parts = partsL5.ToArray();
            hw.baseRotations = new Quaternion[hw.parts.Length];
            hw.basePositions = new Vector3[hw.parts.Length];
            for (int i = 0; i < hw.parts.Length; i++)
            {
                hw.baseRotations[i] = hw.parts[i].rotation;
                hw.basePositions[i] = hw.parts[i].position;
            }
            hw.degrees = 0f;
            hw.initialized = true;
            return hw;
        }

        // Sumbu rotasi: handwheel orange field hub.up = (0,-1,0), berarti rotate di world Y axis.
        // Untuk bypass handwheel asli, hub.up bisa berbeda.
        Vector3 axis = hub.up.normalized;
        if (axis.sqrMagnitude < 0.001f) axis = Vector3.up;
        // Konversi ke world axis Y kalau axis pointing ke ±Y (handwheel orange flat)
        if (Mathf.Abs(axis.y) > 0.9f) axis = new Vector3(0, Mathf.Sign(axis.y), 0);
        hw.axisWorld = axis;

        // Cache parts: hub itself + sibling siblings yang nama-nya mirip Spoke / OuterRing / Handwheel
        var parts = new List<Transform>();
        parts.Add(hub);
        if (hub.parent != null)
        {
            foreach (Transform sibling in hub.parent)
            {
                if (sibling == hub) continue;
                if (sibling.name.Contains("Spoke") || sibling.name.Contains("OuterRing")
                    || sibling.name.Contains("Handwheel") || sibling.name.Contains("Spoke_A")
                    || sibling.name.Contains("Spoke_B"))
                {
                    parts.Add(sibling);
                }
            }
        }
        hw.parts = parts.ToArray();
        hw.baseRotations = new Quaternion[hw.parts.Length];
        hw.basePositions = new Vector3[hw.parts.Length];
        for (int i = 0; i < hw.parts.Length; i++)
        {
            hw.baseRotations[i] = hw.parts[i].rotation;
            hw.basePositions[i] = hw.parts[i].position;
        }
        hw.degrees = 0f;
        hw.initialized = true;
        return hw;
    }

    // ============================================================
    //  STAGE PROGRESSION
    // ============================================================

    private void CheckStageProgress()
    {
        if (_transitioning) return;

        // MEKANIK BARU: putar handwheel sampai habis = uap FV1 TERTUTUP -> cairan mengalir ke FV2.
        if (_phase == Phase.OpenFV1 && _fv1.openPercent >= 0.99f)
        {
            _fv1.isStable = true;
            SetStatusStripColor(_fv1StatusStrip, Color.green);
            StopVaporFX(_fv1VaporFX);
            if (_hud != null) _hud.ShowNotifPublic(_msgFv1Done, 5f);
            StartSequence(InterVesselTransition(1));
            return;
        }

        // FV2 valve tertutup -> cairan mengalir ke FV3.
        if (_phase == Phase.OpenFV2 && _fv2.openPercent >= 0.99f && _fv1.isStable)
        {
            _fv2.isStable = true;
            SetStatusStripColor(_fv2StatusStrip, Color.green);
            StopVaporFX(_fv2VaporFX);
            if (_hud != null) _hud.ShowNotifPublic(_msgFv2Done, 5f);
            StartSequence(InterVesselTransition(2));
            return;
        }

        // FV3 valve tertutup -> flash train selesai, cairan mengalir ke Step/Area berikutnya (CCD).
        if (_phase == Phase.OpenFV3 && _fv3.openPercent >= 0.99f && _fv2.isStable)
        {
            _fv3.isStable = true;
            SetStatusStripColor(_fv3StatusStrip, Color.green);
            StopVaporFX(_fv3VaporFX);
            StopAudio(_steamReleaseAudio);
            GameLevelManager.Instance?.NotifyLevel8FlashLetdownDone();
            GameLevelManager.Instance?.NotifyLevel8SampleTaken();
            _phase = Phase.MenungguLapor;
            if (_hud != null) _hud.ShowNotifPublic(_msgFv3Done, 9f);
            StartSequence(WaitForFinalReportCoroutine());
        }
    }

    // Jeda fade-out sebentar lalu spawn di SpawnPoint vessel berikutnya, mulai fase uap berikutnya.
    private IEnumerator InterVesselTransition(int nextIdx)
    {
        _transitioning = true;
        float d = Mathf.Max(1f, _interVesselFadeDuration);
        if (_hud != null) _hud.PlayManualFade(d);
        yield return new WaitForSeconds(d * 0.5f);

        // Teleport ke spawn vessel berikutnya.
        Transform target = nextIdx == 1 ? _spawnFv2 : _spawnFv3;
        if (target != null) { _teleportTargetField = target; TeleportPlayerToField(); }

        yield return new WaitForSeconds(d * 0.5f);

        // WAJIB lapor WT dulu sebelum buka vessel berikutnya, baru handwheel aktif.
        yield return PreVesselReport(nextIdx);

        // Mulai fase vessel berikutnya: uap besar lagi (tapi lebih kecil dari sebelumnya), audio lebih pelan.
        EnterVesselPhase(nextIdx);
        _seqCoroutine = null;
    }

    // ============================================================
    //  CASCADE PANEL VISUAL
    // ============================================================

    private void InitCascadePanels()
    {
        SetStatusStripColor(_fv1StatusStrip, Color.red);
        SetStatusStripColor(_fv2StatusStrip, Color.red);
        SetStatusStripColor(_fv3StatusStrip, Color.red);
    }

    private void SetStatusStripColor(Renderer strip, Color c)
    {
        if (strip == null) return;
        var mat = strip.material;
        if (mat.HasProperty("_BaseColor")) mat.SetColor("_BaseColor", c);
        if (mat.HasProperty("_Color")) mat.SetColor("_Color", c);
        mat.EnableKeyword("_EMISSION");
        if (mat.HasProperty("_EmissionColor")) mat.SetColor("_EmissionColor", c * 1.5f);
    }

    private void UpdateCascadePanelTexts()
    {
        if (_fv1PanelText != null) _fv1PanelText.text = FormatPanelText(_fv1);
        if (_fv2PanelText != null) _fv2PanelText.text = FormatPanelText(_fv2);
        if (_fv3PanelText != null) _fv3PanelText.text = FormatPanelText(_fv3);
    }

    private string FormatPanelText(FlashStage s)
    {
        string status = s.isStable ? "<color=#5DFC8B>STABLE</color>" : "<color=#FFA040>OPENING</color>";
        return $"<b>{s.stageName}</b>\nP={s.pressureCurrent:F1} atm\nT={s.tempCurrent:F0}°C\n{status}";
    }

    // ============================================================
    //  SLURRY POOL X-RAY GHOST
    // ============================================================

    private Vector3[] _slurryGhostBaseScales = new Vector3[3];
    private bool _slurryGhostBaseCaptured;

    private void UpdateSlurryPoolVisuals()
    {
        if (!_slurryGhostBaseCaptured)
        {
            if (_fv1SlurryGhost != null) _slurryGhostBaseScales[0] = _fv1SlurryGhost.localScale;
            if (_fv2SlurryGhost != null) _slurryGhostBaseScales[1] = _fv2SlurryGhost.localScale;
            if (_fv3SlurryGhost != null) _slurryGhostBaseScales[2] = _fv3SlurryGhost.localScale;
            _slurryGhostBaseCaptured = true;
        }

        AnimateSlurryGhost(_fv1SlurryGhost, _slurryGhostBaseScales[0], _fv1.openPercent);
        AnimateSlurryGhost(_fv2SlurryGhost, _slurryGhostBaseScales[1], _fv2.openPercent);
        AnimateSlurryGhost(_fv3SlurryGhost, _slurryGhostBaseScales[2], _fv3.openPercent);
    }

    private void AnimateSlurryGhost(Transform ghost, Vector3 baseScale, float t)
    {
        if (ghost == null) return;
        bool show = t > 0.05f;
        if (ghost.gameObject.activeSelf != show) ghost.gameObject.SetActive(show);
        if (show)
        {
            // Scale Y dari 0.1 ke base sesuai t (slurry pool naik level)
            Vector3 s = baseScale;
            s.y = baseScale.y * Mathf.Lerp(0.1f, 1f, t);
            ghost.localScale = s;
        }
    }

    // ============================================================
    //  SAMPLING SYSTEM
    // ============================================================

    private void TakeSample(int idx)
    {
        if (idx < 0 || idx >= 3) return;
        if (_sampleTaken[idx]) return;
        _sampleTaken[idx] = true;
        _bottleFilling[idx] = false;
        // Kalau dipanggil via keyboard (tanpa station fill), pastikan liquid botol station penuh.
        if (_stationFillLiquid[idx] != null)
        {
            _stationFillLiquid[idx].localScale = new Vector3(0.82f, 1.7f, 0.82f);
            _stationFillLiquid[idx].localPosition = new Vector3(0f, -0.95f + 1.7f * 0.5f, 0f);
        }
        else
        {
            // Tidak ada station (fallback lama) → spawn botol visual mengambang.
            SpawnSampleBottleVisual(idx);
        }
        string stageName = idx == 0 ? "FV1 HP (195°C)" : idx == 1 ? "FV2 MP (145°C)" : "FV3 LP (102°C)";
        if (_hud != null) _hud.ShowNotifPublic($"Sample {stageName} collected. ({CountSamples()}/3)", 4f);
        if (AllSamplesTaken())
        {
            if (_hud != null) _hud.ShowNotifPublic(_msgSamplingDone, 6f);
        }
    }

    public bool AllSamplesTaken()
    {
        foreach (var s in _sampleTaken) if (!s) return false;
        return true;
    }

    private int CountSamples()
    {
        int c = 0;
        foreach (var s in _sampleTaken) if (s) c++;
        return c;
    }

    // ============================================================
    //  VAPOR PARTICLE FX
    // ============================================================

    private void EnsureVaporFX(Transform riser, ref ParticleSystem ps)
    {
        if (ps != null) return;
        // Anchor: pakai riser kalau ada, kalau tidak buat anchor sendiri (uap "di mana saja" sesuai permintaan).
        Transform anchor = riser;
        if (anchor == null)
        {
            var anchorGO = new GameObject("L8_VaporAnchor_Runtime");
            anchorGO.transform.SetParent(transform, false);
            anchorGO.transform.position = new Vector3(-63f, 13f, 105f);
            anchor = anchorGO.transform;
        }
        var go = new GameObject("L8_VaporFX_" + anchor.name);
        go.transform.SetParent(anchor, false);
        go.transform.localPosition = Vector3.up * 0.3f;
        ps = go.AddComponent<ParticleSystem>();
        ps.Stop();
        var main = ps.main;
        main.startLifetime = 3.2f;
        main.startSpeed = 2.0f;
        main.startSize = 0.6f;      // lebih besar, uap tebal
        main.startColor = new Color(1f, 1f, 0.92f, 0.65f);
        main.simulationSpace = ParticleSystemSimulationSpace.World;
        main.maxParticles = 140;
        main.duration = 3f;
        main.loop = true;
        var emission = ps.emission;
        emission.rateOverTime = 45f;  // lebih banyak partikel
        var shape = ps.shape;
        shape.shapeType = ParticleSystemShapeType.Cone;
        shape.angle = 18f;
        shape.radius = 0.18f;
        var vel = ps.velocityOverLifetime;
        vel.enabled = true;
        vel.y = 1.6f;
        var size = ps.sizeOverLifetime;
        size.enabled = true;
        size.size = new ParticleSystem.MinMaxCurve(1f, AnimationCurve.Linear(0f, 1f, 1f, 3.2f));
        var colorGrad = ps.colorOverLifetime;
        colorGrad.enabled = true;
        Gradient grad = new Gradient();
        grad.SetKeys(
            new[] { new GradientColorKey(new Color(1f, 1f, 0.95f), 0f), new GradientColorKey(new Color(0.85f, 0.88f, 0.92f), 1f) },
            new[] { new GradientAlphaKey(0f, 0f), new GradientAlphaKey(0.75f, 0.15f), new GradientAlphaKey(0f, 1f) }
        );
        colorGrad.color = grad;
        var renderer = go.GetComponent<ParticleSystemRenderer>();
        renderer.material = new Material(Shader.Find("Universal Render Pipeline/Particles/Unlit")
            ?? Shader.Find("Particles/Standard Unlit")
            ?? Shader.Find("Sprites/Default"));
    }

    private void StartVaporFX(ParticleSystem ps)
    {
        if (ps != null && !ps.isPlaying) ps.Play();
    }

    private void StopVaporFX(ParticleSystem ps)
    {
        if (ps != null && ps.isPlaying) ps.Stop();
    }

    private void CleanupVaporFX()
    {
        if (_fv1VaporFX != null) { StopVaporFX(_fv1VaporFX); Destroy(_fv1VaporFX.gameObject); _fv1VaporFX = null; }
        if (_fv2VaporFX != null) { StopVaporFX(_fv2VaporFX); Destroy(_fv2VaporFX.gameObject); _fv2VaporFX = null; }
        if (_fv3VaporFX != null) { StopVaporFX(_fv3VaporFX); Destroy(_fv3VaporFX.gameObject); _fv3VaporFX = null; }
    }

    // ============================================================
    //  SAMPLE BOTTLE VISUAL
    // ============================================================

    private void SpawnSampleBottleVisual(int idx)
    {
        if (_sampleBottles[idx] != null) return;
        Transform riser = idx == 0 ? _fv1VaporRiser : idx == 1 ? _fv2VaporRiser : _fv3VaporRiser;
        Vector3 basePos = riser != null ? riser.position : transform.position;
        Vector3 pos = basePos + new Vector3(idx * 1.5f - 1.5f, -0.5f, 0.5f);
        var bottle = GameObject.CreatePrimitive(PrimitiveType.Capsule);
        bottle.name = $"L8_SampleBottle_FV{idx + 1}";
        bottle.transform.position = pos;
        bottle.transform.localScale = new Vector3(0.12f, 0.18f, 0.12f);
        Destroy(bottle.GetComponent<Collider>());
        var rend = bottle.GetComponent<Renderer>();
        var mat = new Material(Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard"));
        mat.color = _sampleStageColors[idx];
        mat.EnableKeyword("_EMISSION");
        if (mat.HasProperty("_EmissionColor"))
            mat.SetColor("_EmissionColor", _sampleStageColors[idx] * 1.5f);
        if (mat.HasProperty("_BaseColor"))
            mat.SetColor("_BaseColor", _sampleStageColors[idx]);
        rend.sharedMaterial = mat;
        _sampleBottles[idx] = bottle;
        StartCoroutine(CoolSampleBottle(idx, mat));
    }

    private IEnumerator CoolSampleBottle(int idx, Material mat)
    {
        float elapsed = 0f;
        Color hot = _sampleStageColors[idx];
        Color cool = _sampleCoolColor;
        while (elapsed < _sampleCoolDuration)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.Clamp01(elapsed / _sampleCoolDuration);
            Color c = Color.Lerp(hot, cool, t);
            if (mat != null)
            {
                mat.color = c;
                if (mat.HasProperty("_EmissionColor"))
                    mat.SetColor("_EmissionColor", c * Mathf.Lerp(1.5f, 0.3f, t));
            }
            yield return null;
        }
    }

    private void CleanupSampleBottles()
    {
        for (int i = 0; i < 3; i++)
        {
            if (_sampleBottles[i] != null) { Destroy(_sampleBottles[i]); _sampleBottles[i] = null; }
        }
        CleanupSampleStations();
    }

    // ============================================================
    //  SAMPLE STATION — mekanik fisik (dekati vessel, ambil botol)
    // ============================================================

    /// <summary>Dipanggil saat 3 valve sudah stabil. Bangun 3 sample station di depan tiap vessel.</summary>
    private void BeginSamplingStations()
    {
        if (_samplingStationsBuilt) { for (int i=0;i<3;i++) if(_sampleStations[i]!=null) _sampleStations[i].SetActive(true); return; }
        _samplingStationsBuilt = true;

        // Z tiap vessel dari handwheel/ghost. Station ditaruh di depan handwheel (sisi player, X lebih besar).
        Transform[] hubs = { _fv1HandwheelHub, _fv2HandwheelHub, _fv3HandwheelHub };
        for (int i = 0; i < 3; i++)
        {
            float z = hubs[i] != null ? hubs[i].position.z : (96.7f + i * 4f);
            float x = hubs[i] != null ? hubs[i].position.x + 2.0f : -52.5f; // sedikit ke sisi player
            Vector3 stationPos = new Vector3(x, 0.0f, z);
            _sampleStations[i] = BuildSampleStation(i, stationPos);
        }
        if (_hud != null)
            _hud.ShowNotifPublic("Ambil 3 sample: DEKATI tiap flash vessel (botol di pedestal). Mendekat = botol terisi otomatis. Atau tekan Q/W/E.", 9f);

        // Bangun gedung lab QC sekarang supaya player bisa lihat tujuannya.
        BuildLabBuilding();
    }

    private GameObject BuildSampleStation(int idx, Vector3 pos)
    {
        var root = new GameObject($"L8_SampleStation_FV{idx + 1}");
        root.transform.SetParent(transform, false);
        root.transform.position = pos;

        // Pedestal (kotak kecil tempat botol).
        var pedestal = GameObject.CreatePrimitive(PrimitiveType.Cube);
        pedestal.name = "Pedestal";
        pedestal.transform.SetParent(root.transform, false);
        pedestal.transform.localPosition = new Vector3(0f, 0.5f, 0f);
        pedestal.transform.localScale = new Vector3(0.5f, 1.0f, 0.5f);
        var pedCol = pedestal.GetComponent<Collider>(); if (pedCol != null) Destroy(pedCol);
        ApplyStationMaterial(pedestal.GetComponent<Renderer>(), new Color(0.25f, 0.27f, 0.32f));

        // Botol (glass, transparan) di atas pedestal.
        var bottle = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        bottle.name = "Bottle";
        bottle.transform.SetParent(root.transform, false);
        bottle.transform.localPosition = new Vector3(0f, 1.25f, 0f);
        bottle.transform.localScale = new Vector3(0.16f, 0.22f, 0.16f);
        var botCol = bottle.GetComponent<Collider>(); if (botCol != null) Destroy(botCol);
        var botRend = bottle.GetComponent<Renderer>();
        var glass = new Material(Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard"));
        SetupTransparent(glass, new Color(0.8f, 0.85f, 0.9f, 0.25f));
        botRend.sharedMaterial = glass;
        _stationBottles[idx] = bottle;

        // Liquid di dalam botol (mulai kosong: scale Y 0).
        var liquid = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        liquid.name = "Liquid";
        liquid.transform.SetParent(bottle.transform, false);
        liquid.transform.localScale = new Vector3(0.82f, 0.001f, 0.82f);
        liquid.transform.localPosition = new Vector3(0f, -0.95f, 0f); // anchor di dasar botol
        var liqCol = liquid.GetComponent<Collider>(); if (liqCol != null) Destroy(liqCol);
        var liqRend = liquid.GetComponent<Renderer>();
        var liqMat = new Material(Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard"));
        liqMat.color = _sampleStageColors[idx];
        liqMat.EnableKeyword("_EMISSION");
        if (liqMat.HasProperty("_EmissionColor")) liqMat.SetColor("_EmissionColor", _sampleStageColors[idx] * 1.2f);
        if (liqMat.HasProperty("_BaseColor")) liqMat.SetColor("_BaseColor", _sampleStageColors[idx]);
        liqRend.sharedMaterial = liqMat;
        _stationFillLiquid[idx] = liquid.transform;

        // Label panah/teks sederhana di atas (TextMesh) supaya jelas ini sample point.
        var labelGO = new GameObject("Label");
        labelGO.transform.SetParent(root.transform, false);
        labelGO.transform.localPosition = new Vector3(0f, 1.9f, 0f);
        var tm = labelGO.AddComponent<TextMesh>();
        tm.text = $"SAMPLE\nFV{idx + 1}";
        tm.fontSize = 48;
        tm.characterSize = 0.025f;
        tm.anchor = TextAnchor.MiddleCenter;
        tm.alignment = TextAlignment.Center;
        tm.color = new Color(0.4f, 1f, 0.7f);

        return root;
    }

    private void ApplyStationMaterial(Renderer r, Color c)
    {
        if (r == null) return;
        var m = new Material(Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard"));
        if (m.HasProperty("_BaseColor")) m.SetColor("_BaseColor", c);
        if (m.HasProperty("_Color")) m.SetColor("_Color", c);
        if (m.HasProperty("_Metallic")) m.SetFloat("_Metallic", 0.4f);
        r.sharedMaterial = m;
    }

    private void SetupTransparent(Material m, Color c)
    {
        m.color = c;
        if (m.HasProperty("_BaseColor")) m.SetColor("_BaseColor", c);
        if (m.HasProperty("_Surface")) m.SetFloat("_Surface", 1f); // Transparent (URP)
        m.SetFloat("_Mode", 3f);
        m.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.SrcAlpha);
        m.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
        m.SetInt("_ZWrite", 0);
        m.DisableKeyword("_SURFACE_TYPE_OPAQUE");
        m.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
        m.renderQueue = 3000;
    }

    /// <summary>Cek jarak player ke tiap station; kalau dekat & belum terisi → mulai isi botol.</summary>
    private void UpdateSamplingProximity()
    {
        if (!_samplingStationsBuilt) return;
        Vector3 head = GetPlayerHeadPosition();
        for (int i = 0; i < 3; i++)
        {
            if (_sampleTaken[i] || _bottleFilling[i]) continue;
            if (_sampleStations[i] == null) continue;
            // Jarak HORIZONTAL saja (abaikan Y) supaya tinggi kamera tidak bikin gagal trigger.
            Vector3 a = head; a.y = 0f;
            Vector3 b = _sampleStations[i].transform.position; b.y = 0f;
            float d = Vector3.Distance(a, b);
            if (d <= _sampleProximityRadius)
            {
                _bottleFilling[i] = true;
                if (_hud != null) _hud.ShowNotifPublic($"Mengambil sample FV{i + 1}... botol terisi.", 3f);
            }
        }
    }

    /// <summary>Animasi botol terisi liquid (scale Y naik), lalu tandai sample diambil.</summary>
    private void UpdateSampleBottleAnimations()
    {
        for (int i = 0; i < 3; i++)
        {
            if (!_bottleFilling[i] || _sampleTaken[i]) continue;
            _bottleFillProgress[i] += Time.deltaTime / 2.0f; // 2 detik untuk penuh
            float t = Mathf.Clamp01(_bottleFillProgress[i]);
            if (_stationFillLiquid[i] != null)
            {
                // Liquid naik dari dasar: scale Y 0 → 1.7, reposisi supaya anchor di dasar.
                float h = Mathf.Lerp(0.001f, 1.7f, t);
                _stationFillLiquid[i].localScale = new Vector3(0.82f, h, 0.82f);
                _stationFillLiquid[i].localPosition = new Vector3(0f, -0.95f + h * 0.5f, 0f);
            }
            if (t >= 1f)
            {
                TakeSample(i); // tandai diambil + notif + cek semua
            }
        }
    }

    private void CleanupSampleStations()
    {
        _samplingStationsBuilt = false;
        for (int i = 0; i < 3; i++)
        {
            if (_sampleStations[i] != null) { Destroy(_sampleStations[i]); _sampleStations[i] = null; }
            _stationBottles[i] = null;
            _stationFillLiquid[i] = null;
            _bottleFillProgress[i] = 0f;
            _bottleFilling[i] = false;
        }
    }

    // ============================================================
    //  LAB BUILDING (QC) — gedung + analyzer + animasi
    // ============================================================

    /// <summary>Bangun gedung lab QC dekat area flash train (runtime primitive, fallback dari Blender).</summary>
    /// <summary>Bangun gedung lab QC. Pakai model Blender (FBX) kalau ada, fallback primitive.</summary>
    private void BuildLabBuilding()
    {
        if (_labBuilt) { if (_labBuilding != null) _labBuilding.SetActive(true); return; }
        _labBuilt = true;

        // Posisi lab di sisi player (X lebih besar dari handwheel), Z di area flash train.
        float baseZ = _fv2HandwheelHub != null ? _fv2HandwheelHub.position.z : 105f;
        Vector3 labOrigin = new Vector3(-43f, 0f, baseZ + 6f);

        // === Coba load model Blender FBX dulu ===
        GameObject fbxPrefab = Resources.Load<GameObject>("QCLab"); // kalau ditaruh di Resources
#if UNITY_EDITOR
        if (fbxPrefab == null)
            fbxPrefab = UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Art/Lab/QCLab.fbx");
#endif
        if (fbxPrefab != null)
        {
            var inst = Instantiate(fbxPrefab);
            inst.name = "L8_LabBuilding";
            inst.transform.SetParent(transform, false);
            // FBX dibuat di Blender dengan Z-up; importer Unity konversi ke Y-up. Posisikan di labOrigin.
            inst.transform.position = labOrigin;
            inst.transform.rotation = Quaternion.identity;
            _labBuilding = inst;

            // Wire fungsional dari child FBX (nama persis dari build_lab.py).
            _labAnalyzerRotor = FindChildDeep(inst.transform, "Lab_Analyzer_Rotor");
            _labResultScreen = FindChildDeep(inst.transform, "Lab_ResultScreen");
            _labSlotLiquids[0] = FindChildDeep(inst.transform, "Lab_SlotLiquid_1");
            _labSlotLiquids[1] = FindChildDeep(inst.transform, "Lab_SlotLiquid_2");
            _labSlotLiquids[2] = FindChildDeep(inst.transform, "Lab_SlotLiquid_3");

            // Liquid slot mulai kosong (scale Y kecil). Simpan base scale utk animasi isi.
            for (int i = 0; i < 3; i++)
            {
                if (_labSlotLiquids[i] != null)
                {
                    var s = _labSlotLiquids[i].localScale;
                    _labSlotLiquidBaseY[i] = s.y; // tinggi penuh dari model
                    _labSlotLiquids[i].localScale = new Vector3(s.x, s.y * 0.02f, s.z);
                }
            }

            // Layar hasil: tambah TextMesh anak supaya bisa update teks progress.
            if (_labResultScreen != null)
            {
                var screenTextGO = new GameObject("ScreenText");
                screenTextGO.transform.SetParent(_labResultScreen, false);
                screenTextGO.transform.localPosition = new Vector3(0f, 0f, 0.7f);
                screenTextGO.transform.localRotation = Quaternion.identity;
                screenTextGO.transform.localScale = Vector3.one * 0.6f;
                var scrTm = screenTextGO.AddComponent<TextMesh>();
                scrTm.text = "QC ANALYZER\nStandby...";
                scrTm.fontSize = 40; scrTm.characterSize = 0.05f; scrTm.anchor = TextAnchor.MiddleCenter;
                scrTm.alignment = TextAlignment.Center; scrTm.color = new Color(0.4f, 0.9f, 0.7f);
                _labScreenText = scrTm;
            }

            // Papan nama di atas pintu.
            var sign = new GameObject("Lab_Sign");
            sign.transform.SetParent(inst.transform, false);
            sign.transform.localPosition = new Vector3(0f, 3.9f, 3.5f);
            var stm = sign.AddComponent<TextMesh>();
            stm.text = "LABORATORIUM QC";
            stm.fontSize = 60; stm.characterSize = 0.04f; stm.anchor = TextAnchor.MiddleCenter;
            stm.alignment = TextAlignment.Center; stm.color = new Color(0.2f, 0.9f, 1f);

            _labDoorPos = labOrigin + new Vector3(0, 1f, 3.5f);
            Debug.Log("[Level8] Lab building dari FBX Blender ter-load.");
            return;
        }

        // === FALLBACK: build primitive kalau FBX tidak ada ===
        BuildLabBuildingPrimitive(labOrigin);
    }

    private readonly float[] _labSlotLiquidBaseY = new float[3] { 1.7f, 1.7f, 1.7f };

    private Transform FindChildDeep(Transform root, string name)
    {
        if (root == null) return null;
        foreach (var tr in root.GetComponentsInChildren<Transform>(true))
            if (tr.name == name) return tr;
        return null;
    }

    private void BuildLabBuildingPrimitive(Vector3 labOrigin)
    {
        var root = new GameObject("L8_LabBuilding");
        root.transform.SetParent(transform, false);
        root.transform.position = labOrigin;
        _labBuilding = root;

        Color wallCol = new Color(0.82f, 0.84f, 0.88f);
        Color floorCol = new Color(0.3f, 0.32f, 0.36f);
        float W = 8f, D = 7f, H = 3.5f, t = 0.2f;

        AddBox(root.transform, "Lab_Floor", new Vector3(0, 0.05f, 0), new Vector3(W, 0.1f, D), floorCol);
        AddBox(root.transform, "Lab_Ceiling", new Vector3(0, H, 0), new Vector3(W, 0.1f, D), wallCol);
        AddBox(root.transform, "Lab_Wall_Back", new Vector3(0, H/2, -D/2), new Vector3(W, H, t), wallCol);
        AddBox(root.transform, "Lab_Wall_Left", new Vector3(-W/2, H/2, 0), new Vector3(t, H, D), wallCol);
        AddBox(root.transform, "Lab_Wall_Right", new Vector3(W/2, H/2, 0), new Vector3(t, H, D), wallCol);
        AddBox(root.transform, "Lab_Wall_Front_L", new Vector3(-W/2 + 1.5f, H/2, D/2), new Vector3(3f, H, t), wallCol);
        AddBox(root.transform, "Lab_Wall_Front_R", new Vector3(W/2 - 1.5f, H/2, D/2), new Vector3(3f, H, t), wallCol);
        AddBox(root.transform, "Lab_Wall_Front_Top", new Vector3(0, H - 0.5f, D/2), new Vector3(2.2f, 1f, t), wallCol);
        _labDoorPos = labOrigin + new Vector3(0, 1f, D/2);

        var sign = new GameObject("Lab_Sign");
        sign.transform.SetParent(root.transform, false);
        sign.transform.localPosition = new Vector3(0, H + 0.4f, D/2);
        var stm = sign.AddComponent<TextMesh>();
        stm.text = "LABORATORIUM QC";
        stm.fontSize = 60; stm.characterSize = 0.04f; stm.anchor = TextAnchor.MiddleCenter;
        stm.alignment = TextAlignment.Center; stm.color = new Color(0.2f, 0.9f, 1f);

        AddBox(root.transform, "Lab_Table", new Vector3(0, 0.8f, -D/2 + 1.2f), new Vector3(4f, 0.15f, 1.2f), new Color(0.5f, 0.52f, 0.55f));
        AddBox(root.transform, "Lab_Analyzer_Body", new Vector3(0, 1.5f, -D/2 + 1.2f), new Vector3(2.4f, 1.4f, 0.9f), new Color(0.15f, 0.18f, 0.25f));

        var rotor = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        rotor.name = "Lab_Analyzer_Rotor";
        rotor.transform.SetParent(root.transform, false);
        rotor.transform.localPosition = new Vector3(0, 1.95f, -D/2 + 0.7f);
        rotor.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
        rotor.transform.localScale = new Vector3(0.5f, 0.08f, 0.5f);
        var rotorCol = rotor.GetComponent<Collider>(); if (rotorCol != null) Destroy(rotorCol);
        ApplyStationMaterial(rotor.GetComponent<Renderer>(), new Color(0.6f, 0.65f, 0.7f));
        _labAnalyzerRotor = rotor.transform;

        for (int i = 0; i < 3; i++)
        {
            float sx = -1f + i * 1f;
            var slot = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            slot.name = $"Lab_Slot_{i + 1}";
            slot.transform.SetParent(root.transform, false);
            slot.transform.localPosition = new Vector3(sx, 0.95f, -D/2 + 1.5f);
            slot.transform.localScale = new Vector3(0.18f, 0.22f, 0.18f);
            var slotCol = slot.GetComponent<Collider>(); if (slotCol != null) Destroy(slotCol);
            var slotMat = new Material(Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard"));
            SetupTransparent(slotMat, new Color(0.8f, 0.85f, 0.9f, 0.25f));
            slot.GetComponent<Renderer>().sharedMaterial = slotMat;

            var liq = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            liq.name = "SlotLiquid";
            liq.transform.SetParent(slot.transform, false);
            liq.transform.localScale = new Vector3(0.82f, 0.001f, 0.82f);
            liq.transform.localPosition = new Vector3(0, -0.95f, 0);
            var lc = liq.GetComponent<Collider>(); if (lc != null) Destroy(lc);
            var lm = new Material(Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard"));
            lm.color = _sampleStageColors[i];
            lm.EnableKeyword("_EMISSION");
            if (lm.HasProperty("_EmissionColor")) lm.SetColor("_EmissionColor", _sampleStageColors[i] * 1.2f);
            liq.GetComponent<Renderer>().sharedMaterial = lm;
            _labSlotLiquids[i] = liq.transform;
            _labSlotLiquidBaseY[i] = 1.7f;
        }

        var screen = AddBox(root.transform, "Lab_ResultScreen", new Vector3(0, 2.4f, -D/2 + 0.25f), new Vector3(3f, 1.4f, 0.08f), new Color(0.05f, 0.1f, 0.15f));
        _labResultScreen = screen.transform;
        var screenTextGO = new GameObject("ScreenText");
        screenTextGO.transform.SetParent(screen.transform, false);
        screenTextGO.transform.localPosition = new Vector3(0, 0, 0.6f);
        screenTextGO.transform.localScale = Vector3.one * 0.06f;
        var scrTm = screenTextGO.AddComponent<TextMesh>();
        scrTm.text = "QC ANALYZER\nStandby...";
        scrTm.fontSize = 40; scrTm.characterSize = 0.5f; scrTm.anchor = TextAnchor.MiddleCenter;
        scrTm.alignment = TextAlignment.Center; scrTm.color = new Color(0.4f, 0.9f, 0.7f);
        _labScreenText = scrTm;
    }

    private TextMesh _labScreenText;

    private GameObject AddBox(Transform parent, string name, Vector3 localPos, Vector3 size, Color c)
    {
        var box = GameObject.CreatePrimitive(PrimitiveType.Cube);
        box.name = name;
        box.transform.SetParent(parent, false);
        box.transform.localPosition = localPos;
        box.transform.localScale = size;
        ApplyStationMaterial(box.GetComponent<Renderer>(), c);
        return box;
    }



    private void SubmitLabQC()
    {
        if (_labSubmitted) return;
        _labSubmitted = true;
        _phase = Phase.LabSubmit;
        StartSequence(LabAnalysisCoroutine());
    }

    /// <summary>Animasi analyzer lab: isi 3 slot botol → rotor berputar → progress di layar → hasil → canvas.</summary>
    private IEnumerator LabAnalysisCoroutine()
    {
        // Pastikan lab ada.
        BuildLabBuilding();
        if (_hud != null) _hud.ShowNotifPublic("Sample dimasukkan ke analyzer lab. Proses analisa berjalan...", 6f);

        // 1) Isi 3 slot liquid (botol "dituang" ke analyzer) berurutan.
        for (int i = 0; i < 3; i++)
        {
            if (_labSlotLiquids[i] == null) continue;
            Vector3 baseScale = _labSlotLiquids[i].localScale;
            Vector3 basePos = _labSlotLiquids[i].localPosition;
            float fullY = _labSlotLiquidBaseY[i];
            float t = 0f;
            while (t < 0.6f)
            {
                t += Time.deltaTime;
                float p = Mathf.Clamp01(t / 0.6f);
                float h = Mathf.Lerp(fullY * 0.02f, fullY, p);
                _labSlotLiquids[i].localScale = new Vector3(baseScale.x, h, baseScale.z);
                // naik dari dasar: geser Y setengah pertambahan tinggi (cylinder pivot di tengah)
                _labSlotLiquids[i].localPosition = basePos + new Vector3(0, (h - fullY * 0.02f) * 0.5f, 0);
                yield return null;
            }
        }

        // 2) Rotor analyzer berputar + progress bar di layar (5 detik).
        EnsureSteamReleaseAudio(); // pakai audio yang ada utk "mesin jalan" (low)
        float dur = 5f, e = 0f;
        while (e < dur)
        {
            e += Time.deltaTime;
            if (_labAnalyzerRotor != null)
                _labAnalyzerRotor.Rotate(Vector3.up, 360f * Time.deltaTime, Space.Self);
            if (_labScreenText != null)
            {
                int pct = Mathf.RoundToInt(Mathf.Clamp01(e / dur) * 100f);
                int bars = Mathf.RoundToInt(pct / 10f);
                _labScreenText.text = "ANALISA QC...\n[" + new string('#', bars) + new string('-', 10 - bars) + "] " + pct + "%";
            }
            yield return null;
        }
        if (_labScreenText != null) _labScreenText.text = "QC SELESAI\nSemua dalam SOP ✓";

        // 3) Tampilkan canvas hasil detail + tombol ACCEPT.
        ShowLabQcCanvas();
    }

    // ============================================================
    //  LAB QC POP-UP CANVAS
    // ============================================================

    private void ShowLabQcCanvas()
    {
        if (_labQcCanvas != null)
        {
            _labQcCanvas.SetActive(true);
            return;
        }
        _labQcCanvas = BuildLabQcCanvas();
    }

    private GameObject BuildLabQcCanvas()
    {
        var canvasGO = new GameObject("L8_LabQC_Canvas");
        canvasGO.transform.SetParent(transform, false);
        var canvas = canvasGO.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.WorldSpace;
        canvasGO.AddComponent<UnityEngine.UI.CanvasScaler>();
        canvasGO.AddComponent<UnityEngine.UI.GraphicRaycaster>();

        Vector3 pos = GetPlayerHeadPosition() + GetPlayerHeadForward() * 1.6f;
        canvasGO.transform.position = pos;
        canvasGO.transform.rotation = Quaternion.LookRotation(GetPlayerHeadForward(), Vector3.up);
        var rect = canvasGO.GetComponent<RectTransform>();
        rect.sizeDelta = new Vector2(2f, 1.4f);
        canvasGO.transform.localScale = Vector3.one * 0.5f;

        // Background
        AddUIPanel(canvasGO.transform, "BG", new Color(0.05f, 0.1f, 0.15f, 0.95f), Vector2.zero, Vector2.one);

        // Title
        AddUIText(canvasGO.transform, "Title", "▼ QC FLASH SLURRY — VERIFIKASI AUTOCLAVE",
            new Color(0.3f, 0.9f, 0.6f), 30, FontStyle.Bold, TextAnchor.MiddleCenter,
            new Vector2(0, 0.85f), new Vector2(1, 1f));

        // Sample rows
        string[] sampleData = {
            "FV1 HP slurry: Ni larut 5.2 g/L | Co 0.45 | Fe 0.8 | Acid 18.0  ✓",
            "FV2 MP slurry: Ni larut 5.3 g/L | Co 0.46 | pH 1.2 | Solid 31%  ✓",
            "FV3 LP slurry: 102°C / 1.05 atm | Ni recovery 94% | siap CCD  ✓"
        };
        for (int i = 0; i < 3; i++)
        {
            float yMin = 0.55f - i * 0.13f;
            float yMax = yMin + 0.12f;
            AddUIText(canvasGO.transform, $"Sample{i}", sampleData[i], Color.white, 18, FontStyle.Normal,
                TextAnchor.MiddleLeft, new Vector2(0.05f, yMin), new Vector2(0.95f, yMax));
        }

        // Verdict
        AddUIText(canvasGO.transform, "Verdict",
            "VERDICT: Autoclave berhasil melindi Ni/Co. Slurry aman di-sampling setelah flash dan siap masuk CCD.\n(Beda dengan Level 9: CCD mengecek overflow PLS jernih.)",
            new Color(0.6f, 1f, 0.7f), 18, FontStyle.Italic, TextAnchor.MiddleCenter,
            new Vector2(0.05f, 0.16f), new Vector2(0.95f, 0.32f));

        // Accept button
        AddUIButton(canvasGO.transform, "ACCEPT & LANJUT",
            new Vector2(0.3f, 0.04f), new Vector2(0.7f, 0.14f),
            new Color(0.2f, 0.6f, 0.3f), () => OnLabAccepted());

        return canvasGO;
    }

    private void OnLabAccepted()
    {
        if (_labQcCanvas != null) _labQcCanvas.SetActive(false);
        // Notify GLM bahwa sample sudah diambil + lab approved
        GameLevelManager.Instance?.NotifyLevel8SampleTaken();
        _phase = Phase.MenungguLapor;
        if (_hud != null) _hud.ShowNotifPublic(_msgLabComplete, 8f);
        StartSequence(WaitForFinalReportCoroutine());
    }

    private IEnumerator WaitForFinalReportCoroutine()
    {
        _waitingForVoiceReport = true;
        _voiceReportReceived = false;
        while (!_voiceReportReceived) yield return null;
        _waitingForVoiceReport = false;
        _phase = Phase.Selesai;
        // TANPA canvas tengah: langsung fade-out PELAN lalu lanjut ke level berikutnya (CCD).
        StartSequence(FinishLevelWithSlowFade());
    }

    // Fade-out pelan lalu lanjut transisi resmi GLM (Level 8 -> Level 9/CCD).
    private IEnumerator FinishLevelWithSlowFade()
    {
        // Matikan animasi Level 8 dulu (slurry, uap, audio) supaya bersih saat pindah.
        if (_flowDriver != null) _flowDriver.StopFlow(true);
        _slurryFlowActive = false;
        StopVaporFX(_fv1VaporFX); StopVaporFX(_fv2VaporFX); StopVaporFX(_fv3VaporFX);
        StopAudio(_steamReleaseAudio);

        float fade = 3.5f;   // fade pelan
        if (_hud != null) _hud.PlayManualFade(fade);
        yield return new WaitForSeconds(fade * 0.6f);
        var glm = GameLevelManager.Instance;
        if (glm != null) glm.LanjutkanTransisiLevel8();
        _seqCoroutine = null;
    }

    private void OnVoiceReportAccepted(string keyword)
    {
        if (!_levelActive) return;
        if (_waitingForVoiceReport) _voiceReportReceived = true;
    }

    // ============================================================
    //  MISSION COMPLETE CANVAS
    // ============================================================

    private void ShowMissionCompleteCanvas()
    {
        if (_missionCompleteCanvas != null) { _missionCompleteCanvas.SetActive(true); return; }
        var canvasGO = new GameObject("L8_MissionComplete_Canvas");
        canvasGO.transform.SetParent(transform, false);
        var canvas = canvasGO.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.WorldSpace;
        canvasGO.AddComponent<UnityEngine.UI.CanvasScaler>();
        canvasGO.AddComponent<UnityEngine.UI.GraphicRaycaster>();

        Vector3 pos = GetPlayerHeadPosition() + GetPlayerHeadForward() * 1.8f;
        canvasGO.transform.position = pos;
        canvasGO.transform.rotation = Quaternion.LookRotation(GetPlayerHeadForward(), Vector3.up);
        var rect = canvasGO.GetComponent<RectTransform>();
        rect.sizeDelta = new Vector2(1.6f, 0.9f);
        canvasGO.transform.localScale = Vector3.one * 0.6f;

        AddUIPanel(canvasGO.transform, "BG", new Color(0.08f, 0.12f, 0.2f, 0.92f), Vector2.zero, Vector2.one);
        AddUIText(canvasGO.transform, "Title", "✓ LEVEL 8 SELESAI",
            new Color(0.4f, 1f, 0.5f), 36, FontStyle.Bold, TextAnchor.MiddleCenter,
            new Vector2(0, 0.7f), new Vector2(1, 1f));
        AddUIText(canvasGO.transform, "Sub", "Flash Train 3-stage stabil. Slurry mengalir ke CCD.",
            Color.white, 22, FontStyle.Normal, TextAnchor.MiddleCenter,
            new Vector2(0, 0.4f), new Vector2(1, 0.7f));
        AddUIButton(canvasGO.transform, "STAY (lihat proses)",
            new Vector2(0.05f, 0.05f), new Vector2(0.48f, 0.32f),
            new Color(0.2f, 0.4f, 0.7f), () => HideMissionComplete());
        AddUIButton(canvasGO.transform, "KEMBALI KE DCS → LEVEL 9 (CCD)",
            new Vector2(0.52f, 0.05f), new Vector2(0.95f, 0.32f),
            new Color(0.3f, 0.7f, 0.4f), () => GoToNextLevel());

        _missionCompleteCanvas = canvasGO;
    }

    private void HideMissionComplete()
    {
        if (_missionCompleteCanvas != null) _missionCompleteCanvas.SetActive(false);
    }

    private void GoToNextLevel()
    {
        HideMissionComplete();
        var glm = GameLevelManager.Instance;
        if (glm != null)
        {
            // Lanjutkan transisi yang ditahan (Level 8 -> Level 9) lewat flow resmi GLM.
            glm.LanjutkanTransisiLevel8();
        }
    }

    // ============================================================
    //  UI HELPERS
    // ============================================================

    private void AddUIPanel(Transform parent, string name, Color color, Vector2 anchorMin, Vector2 anchorMax)
    {
        var go = new GameObject(name);
        go.transform.SetParent(parent, false);
        var img = go.AddComponent<UnityEngine.UI.Image>();
        img.color = color;
        var rect = go.GetComponent<RectTransform>();
        rect.anchorMin = anchorMin; rect.anchorMax = anchorMax;
        rect.offsetMin = Vector2.zero; rect.offsetMax = Vector2.zero;
    }

    private void AddUIText(Transform parent, string name, string text, Color color, int fontSize,
        FontStyle style, TextAnchor anchor, Vector2 anchorMin, Vector2 anchorMax)
    {
        var go = new GameObject(name);
        go.transform.SetParent(parent, false);
        var t = go.AddComponent<UnityEngine.UI.Text>();
        t.text = text;
        t.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        t.fontSize = fontSize;
        t.color = color;
        t.fontStyle = style;
        t.alignment = anchor;
        t.supportRichText = true;
        var rect = go.GetComponent<RectTransform>();
        rect.anchorMin = anchorMin; rect.anchorMax = anchorMax;
        rect.offsetMin = Vector2.zero; rect.offsetMax = Vector2.zero;
    }

    private void AddUIButton(Transform parent, string label, Vector2 anchorMin, Vector2 anchorMax,
        Color color, System.Action onClick)
    {
        var go = new GameObject(label);
        go.transform.SetParent(parent, false);
        var img = go.AddComponent<UnityEngine.UI.Image>();
        img.color = color;
        var rect = go.GetComponent<RectTransform>();
        rect.anchorMin = anchorMin; rect.anchorMax = anchorMax;
        rect.offsetMin = Vector2.zero; rect.offsetMax = Vector2.zero;
        var btn = go.AddComponent<UnityEngine.UI.Button>();
        btn.targetGraphic = img;
        btn.onClick.AddListener(() => onClick?.Invoke());

        var txtGo = new GameObject("Text");
        txtGo.transform.SetParent(go.transform, false);
        var txt = txtGo.AddComponent<UnityEngine.UI.Text>();
        txt.text = label;
        txt.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        txt.fontSize = 20;
        txt.color = Color.white;
        txt.alignment = TextAnchor.MiddleCenter;
        txt.fontStyle = FontStyle.Bold;
        var trect = txtGo.GetComponent<RectTransform>();
        trect.anchorMin = Vector2.zero; trect.anchorMax = Vector2.one;
        trect.offsetMin = Vector2.zero; trect.offsetMax = Vector2.zero;
    }

    // ============================================================
    //  HELPERS
    // ============================================================

    private Vector3 GetPlayerHeadPosition()
    {
        if (_playerRigRoot == null) return Vector3.zero;
        var origin = _playerRigRoot.GetComponent<XROrigin>();
        if (origin != null && origin.Camera != null) return origin.Camera.transform.position;
        var cam = _playerRigRoot.GetComponentInChildren<Camera>();
        return cam != null ? cam.transform.position : _playerRigRoot.position + Vector3.up * 1.6f;
    }

    private Vector3 GetPlayerHeadForward()
    {
        if (_playerRigRoot == null) return Vector3.forward;
        var origin = _playerRigRoot.GetComponent<XROrigin>();
        if (origin != null && origin.Camera != null) return origin.Camera.transform.forward;
        var cam = _playerRigRoot.GetComponentInChildren<Camera>();
        return cam != null ? cam.transform.forward : _playerRigRoot.forward;
    }

    private void TeleportPlayerToField()
    {
        if (_playerRigRoot == null || _teleportTargetField == null) return;
        var cc = _playerRigRoot.GetComponent<CharacterController>();
        bool ccOn = cc != null && cc.enabled;
        if (ccOn) cc.enabled = false;
        var origin = _playerRigRoot.GetComponent<XROrigin>();
        if (origin != null)
        {
            // KANONIK: spawn = posisi KAKI; kamera = kaki + CameraYOffset. JANGAN double-set
            // (MoveCamera + SetPositionAndRotation) -> bikin player melayang ~1.36m.
            Vector3 camTarget = _teleportTargetField.position + Vector3.up * origin.CameraYOffset;
            origin.MoveCameraToWorldLocation(camTarget);
            origin.MatchOriginUpCameraForward(Vector3.up, _teleportTargetField.forward);
        }
        else
        {
            _playerRigRoot.SetPositionAndRotation(_teleportTargetField.position, _teleportTargetField.rotation);
        }
        if (ccOn) cc.enabled = true;
    }

    private void ResetAllStages()
    {
        ResetStage(_fv1);
        ResetStage(_fv2);
        ResetStage(_fv3);
    }

    private void ResetStage(FlashStage s)
    {
        s.pressureCurrent = s.pressureStart;
        s.tempCurrent = s.tempStart;
        s.openPercent = 0f;
        s.isStable = false;
    }

    private void StartSequence(IEnumerator routine)
    {
        if (_seqCoroutine != null) StopCoroutine(_seqCoroutine);
        _seqCoroutine = StartCoroutine(routine);
    }

    // Hancurkan objek dengan aman baik di play mode maupun edit mode (mis. saat AutoFindReferences
    // dipanggil dari editor/MCP). Destroy() biasa error "may not be called from edit mode".
    private static void SafeDestroy(UnityEngine.Object obj)
    {
        if (obj == null) return;
        if (Application.isPlaying) Destroy(obj);
        else DestroyImmediate(obj);
    }

    // ============================================================
    //  AUTOCLAVE -> FLASH X-RAY SLURRY FLOW
    // ============================================================

    private void ResolveSlurryFlowRenderers()
    {
        _slurryFlowRenderers.Clear();
        foreach (var t in FindObjectsByType<Transform>(FindObjectsInactive.Include, FindObjectsSortMode.None))
        {
            if (t == null || !t.gameObject.scene.IsValid()) continue;
            // Slurry STATIS di dalam pipa (tube + core + elbow). PLUG (bergerak) dikecualikan -> diatur driver.
            if (t.name.StartsWith("AutoclaveToFlash_Slurry") && !t.name.Contains("Plug"))
            {
                var r = t.GetComponent<Renderer>();
                if (r != null) _slurryFlowRenderers.Add(r);
            }
        }
        // Tint SEMUA material slurry (static + plug share Mat_Slurry*) jadi COKLAT cairan
        // mengikuti output akhir autoclave (slurry leached panas coklat-kemerahan).
        TintSlurryFlowBrown();
    }

    // Set warna slurry pipa autoclave->flash jadi coklat cairan (base + emisi redup).
    private Material _slurryLiquidMat;
    private void TintSlurryFlowBrown()
    {
        Color brownBase = new Color(0.34f, 0.19f, 0.09f);   // coklat karat slurry
        Color brownEmis = new Color(0.30f, 0.13f, 0.04f);   // emisi panas redup

        // Material CAIRAN coklat (pakai shader liquid autoclave Olivia/L7SlurryFill,
        // tanpa clip = pipa penuh) -> bagian tube/liquid tampak cairan beriak nyata.
        if (_slurryLiquidMat == null)
        {
            Shader liq = Shader.Find("Olivia/L7SlurryFill");
            if (liq != null)
            {
                _slurryLiquidMat = new Material(liq) { name = "M_L8_SlurryLiquid_Runtime" };
                _slurryLiquidMat.SetColor("_BaseColor", new Color(0.40f, 0.22f, 0.10f));
                _slurryLiquidMat.SetColor("_DeepColor", new Color(0.20f, 0.10f, 0.04f));
                _slurryLiquidMat.SetColor("_EmissionColor", brownEmis);
                _slurryLiquidMat.SetFloat("_EmissionIntensity", 0.35f);
                _slurryLiquidMat.SetFloat("_FillY", 99999f);     // pipa: jangan clip
                _slurryLiquidMat.SetFloat("_Alpha", 0.86f);
                _slurryLiquidMat.SetFloat("_RippleScale", 9f);
                _slurryLiquidMat.SetFloat("_RippleSpeed", 1.3f);
                _slurryLiquidMat.SetFloat("_RippleStrength", 0.08f);
                _slurryLiquidMat.SetFloat("_SwirlSpeed", 0f);
                _slurryLiquidMat.EnableKeyword("_EMISSION");
                _slurryLiquidMat.renderQueue = 3010;
            }
        }

        foreach (var t in FindObjectsByType<Transform>(FindObjectsInactive.Include, FindObjectsSortMode.None))
        {
            if (t == null || !t.gameObject.scene.IsValid()) continue;
            if (!t.name.StartsWith("AutoclaveToFlash_Slurry")) continue;
            var r = t.GetComponent<Renderer>();
            if (r == null) continue;

            // Bagian CAIRAN/tube (Liquid/Seg/Core/XRayFlow/Elbow) -> shader cairan coklat.
            bool isLiquidPart = t.name.Contains("_Liquid") || t.name.Contains("_Seg")
                || t.name.Contains("_Core") || t.name.Contains("_XRayFlow") || t.name.Contains("Elbow");
            if (isLiquidPart && _slurryLiquidMat != null)
            {
                r.sharedMaterial = _slurryLiquidMat;
                continue;
            }

            // Bagian padatan (Rock/Cap) -> tetap coklat (solid tersuspensi).
            foreach (var m in r.sharedMaterials)
            {
                if (m == null) continue;
                if (m.HasProperty("_BaseColor")) m.SetColor("_BaseColor", brownBase);
                if (m.HasProperty("_Color")) m.SetColor("_Color", brownBase);
                m.EnableKeyword("_EMISSION");
                if (m.HasProperty("_EmissionColor")) m.SetColor("_EmissionColor", brownEmis);
            }
        }
    }

    // Sembunyikan / tampilkan slurry STATIS (batang kuning di dalam pipa).
    private void SetStaticSlurryVisible(bool visible)
    {
        foreach (var r in _slurryFlowRenderers)
            if (r != null && r.enabled != visible) r.enabled = visible;
    }

    private void SetSlurryFlowActive(bool on)
    {
        _slurryFlowActive = on;
        // Slurry selalu terlihat (X-ray) tapi emisi/pulse hidup saat mengalir.
        foreach (var r in _slurryFlowRenderers)
        {
            if (r == null) continue;
            if (r.gameObject.activeSelf != true) r.gameObject.SetActive(true);
        }
    }

    private void UpdateSlurryFlowAnim()
    {
        if (!_slurryFlowActive || _slurryFlowRenderers.Count == 0) return;
        _slurryFlowPhase += Time.deltaTime * 2.2f;
        // Pulse emisi (slurry panas mengalir) + scroll tekstur kalau ada.
        float pulse = 0.7f + 0.3f * Mathf.Sin(_slurryFlowPhase * 3f);
        Color hot = new Color(0.45f, 0.22f, 0.07f) * (1.3f * pulse);  // COKLAT slurry panas (output autoclave)
        foreach (var r in _slurryFlowRenderers)
        {
            if (r == null) continue;
            var m = r.material;
            if (m.HasProperty("_EmissionColor")) { m.EnableKeyword("_EMISSION"); m.SetColor("_EmissionColor", hot); }
            if (m.HasProperty("_BaseMap")) m.SetTextureOffset("_BaseMap", new Vector2(0f, -_slurryFlowPhase * 0.5f));
            if (m.HasProperty("_MainTex")) m.SetTextureOffset("_MainTex", new Vector2(0f, -_slurryFlowPhase * 0.5f));
        }
    }

    // ============================================================
    //  AUTO-FIND
    // ============================================================

    private void AutoFindReferences()
    {
        if (_playerRigRoot == null)
        {
            GameObject rig = GameObject.Find("XR Origin (XR Rig)") ?? GameObject.Find("XR Origin") ?? GameObject.FindWithTag("Player");
            if (rig != null) _playerRigRoot = rig.transform;
        }

        // Cascade panels (model aktif)
        if (_fv1StatusStrip == null) _fv1StatusStrip = FindRendererByName("FV1_PressureCascadePanel_StatusStrip");
        if (_fv2StatusStrip == null) _fv2StatusStrip = FindRendererByName("FV2_PressureCascadePanel_StatusStrip");
        if (_fv3StatusStrip == null) _fv3StatusStrip = FindRendererByName("FV3_PressureCascadePanel_StatusStrip");
        if (_fv1PanelText == null) _fv1PanelText = FindTextMeshPro("FV1_PressureCascadePanel_Text");
        if (_fv2PanelText == null) _fv2PanelText = FindTextMeshPro("FV2_PressureCascadePanel_Text");
        if (_fv3PanelText == null) _fv3PanelText = FindTextMeshPro("FV3_PressureCascadePanel_Text");

        // Slurry ghost (model aktif) — dipakai juga untuk menentukan Z deck handwheel.
        if (_fv1SlurryGhost == null) _fv1SlurryGhost = FindByNameInactive("FV1_XRay_SlurryPool_Ghost");
        if (_fv2SlurryGhost == null) _fv2SlurryGhost = FindByNameInactive("FV2_XRay_SlurryPool_Ghost");
        if (_fv3SlurryGhost == null) _fv3SlurryGhost = FindByNameInactive("FV3_XRay_SlurryPool_Ghost");

        // Vapor risers
        if (_fv1VaporRiser == null) _fv1VaporRiser = FindByNameInactive("FV1_TopVaporOutlet_Riser");
        if (_fv2VaporRiser == null) _fv2VaporRiser = FindByNameInactive("FV2_TopVaporOutlet_Riser");
        if (_fv3VaporRiser == null) _fv3VaporRiser = FindByNameInactive("FV3_TopVaporOutlet_Riser");

        // Steam anchors: uap keluar TEPAT di SteamRiser_Connect_* (sesuai permintaan user).
        if (_steamAnchor1 == null) _steamAnchor1 = FindByNameInactive("SteamRiser_Connect_-7");
        if (_steamAnchor2 == null) _steamAnchor2 = FindByNameInactive("SteamRiser_Connect_0");
        if (_steamAnchor3 == null) _steamAnchor3 = FindByNameInactive("SteamRiser_Connect_7");

        // Per-vessel spawn points (teleport tiap vessel sebelum putar handwheel).
        // FORCE-resolve by name (abaikan serialized lama yg mungkin nyasar) supaya
        // spawn FV1/2/3 PERSIS di SpawnPoint_Lv8 / (1) / (2).
        var sp1 = FindByNameInactive("SpawnPoint_Lv8");
        var sp2 = FindByNameInactive("SpawnPoint_Lv8 (1)");
        var sp3 = FindByNameInactive("SpawnPoint_Lv8 (2)");
        if (sp1 != null) _spawnFv1 = sp1;
        if (sp2 != null) _spawnFv2 = sp2;
        if (sp3 != null) _spawnFv3 = sp3;
        // Spawn awal level = SpawnPoint_Lv8 (depan FV1).
        if (_teleportTargetField == null) _teleportTargetField = _spawnFv1;

        // Autoclave -> Flash letdown valve handwheel (dibuka pemain di AWAL) + X-ray slurry flow.
        if (_autoclaveValveHub == null)
            _autoclaveValveHub = FindByNameInactive("AutoclaveToFlash_LetdownValve_Handwheel_Hub");
        ResolveSlurryFlowRenderers();
        if (_flowDriver == null)
            _flowDriver = FindFirstObjectByType<AutoclaveSlurryFlowDriver>(FindObjectsInactive.Include);

        // Handwheel: buat 3 handwheel runtime di deck depan tiap vessel (jelas & terjangkau).
        // Z diambil dari slurry ghost vessel masing-masing. Ini meng-override referensi handwheel.
        EnsureRuntimeDeckHandwheels();

        // Spawn: dihitung SETELAH deck handwheel ada, supaya player menghadap ke handwheel deck.
        if (_teleportTargetField == null)
            _teleportTargetField = CreateRuntimeSpawnPoint();
    }

    private Transform CreateRuntimeSpawnPoint()
    {
        // Hitung posisi spawn dari ketiga handwheel (yang akan diputar player).
        // Player berdiri di depan barisan handwheel dan MENGHADAP ke arahnya.
        Vector3 hwCenter;
        var hubs = new System.Collections.Generic.List<Vector3>();
        if (_fv1HandwheelHub != null) hubs.Add(_fv1HandwheelHub.position);
        if (_fv2HandwheelHub != null) hubs.Add(_fv2HandwheelHub.position);
        if (_fv3HandwheelHub != null) hubs.Add(_fv3HandwheelHub.position);
        if (hubs.Count > 0)
        {
            hwCenter = Vector3.zero;
            foreach (var h in hubs) hwCenter += h;
            hwCenter /= hubs.Count;
        }
        else
        {
            hwCenter = new Vector3(-67.68f, 15.6f, 104.6f); // fallback ke handwheel aktif diketahui
        }

        // Handwheel barisan di X~-67.7 (sisi belakang platform). Player berdiri ~3m ke arah +X
        // (sisi depan, dekat cascade panel) menghadap ke handwheel (-X). Y sejajar handwheel.
        float standY = Mathf.Max(0.1f, hwCenter.y - 1.5f);
        Vector3 pos = new Vector3(hwCenter.x + 3.5f, standY, hwCenter.z);

        Vector3 lookDir = hwCenter - pos;
        if (lookDir.sqrMagnitude < 0.001f) lookDir = Vector3.left;
        Quaternion rot = Quaternion.LookRotation(lookDir.normalized, Vector3.up);

        // Reuse spawn lama kalau ada (reposisi + reorient).
        var existing = GameObject.Find("SpawnPoint_Lvl8_FlashTrain_Runtime");
        var sp = existing != null ? existing : new GameObject("SpawnPoint_Lvl8_FlashTrain_Runtime");
        sp.transform.position = pos;
        sp.transform.rotation = rot;
        return sp.transform;
    }

    private Transform FindByNameInactive(string name)
    {
        // Prioritaskan objek yang AKTIF (model flash vessel yang terlihat),
        // baru fallback ke inactive (model lama disabled) kalau tidak ada yang aktif.
        Transform inactiveMatch = null;
        foreach (Transform t in FindObjectsByType<Transform>(FindObjectsInactive.Include, FindObjectsSortMode.None))
        {
            if (t == null || t.name != name || !t.gameObject.scene.IsValid()) continue;
            if (t.gameObject.activeInHierarchy) return t; // active diutamakan
            if (inactiveMatch == null) inactiveMatch = t;
        }
        return inactiveMatch;
    }

    /// <summary>
    /// Cari handwheel orange yang user buat di field — strukturnya:
    /// IsolationValve_Assembly_XX > IsolationValve_Handwheel > Handwheel_Hub
    /// LetdownValve_Assembly > LetdownValve_Handwheel > Handwheel_Hub
    /// Return Handwheel_Hub yang akan jadi pivot rotasi.
    /// </summary>
    private Transform FindFieldHandwheelByAssembly(string assemblyName)
    {
        Transform assembly = FindByNameInactive(assemblyName);
        if (assembly == null) return null;
        // Cari child Handwheel_Hub (recursive)
        return FindChildRecursive(assembly, "Handwheel_Hub");
    }

    private Transform FindChildRecursive(Transform parent, string name)
    {
        if (parent == null) return null;
        for (int i = 0; i < parent.childCount; i++)
        {
            var child = parent.GetChild(i);
            if (child.name == name) return child;
            var found = FindChildRecursive(child, name);
            if (found != null) return found;
        }
        return null;
    }

    private Renderer FindRendererByName(string name)
    {
        var t = FindByNameInactive(name);
        return t != null ? t.GetComponent<Renderer>() : null;
    }

    private TextMeshPro FindTextMeshPro(string name)
    {
        var t = FindByNameInactive(name);
        return t != null ? t.GetComponent<TextMeshPro>() : null;
    }

    // ============================================================
    //  AUDIO
    // ============================================================

    private void EnsureSteamReleaseAudio()
    {
        if (_steamReleaseAudio != null) return;
        var go = new GameObject("L8_SteamRelease_Audio");
        go.transform.SetParent(transform, false);
        _steamReleaseAudio = go.AddComponent<AudioSource>();
        _steamReleaseAudio.loop = true;
        _steamReleaseAudio.spatialBlend = 0f; // 2D supaya jelas terdengar keras (bukan tergantung jarak)
        _steamReleaseAudio.volume = 0.95f;
        _steamReleaseAudio.clip = GenerateSteamHiss("L8Steam", 4f, 22050);
    }

    private AudioClip GenerateSteamHiss(string name, float duration, int sampleRate)
    {
        int total = Mathf.CeilToInt(duration * sampleRate);
        float[] data = new float[total];
        var rnd = new System.Random(name.GetHashCode());
        float lp = 0f;
        for (int i = 0; i < total; i++)
        {
            float n = ((float)rnd.NextDouble() - 0.5f) * 2f;
            lp += 0.15f * (n - lp);
            data[i] = lp * 0.55f;
        }
        var clip = AudioClip.Create(name, total, 1, sampleRate, false);
        clip.SetData(data, 0);
        return clip;
    }

    private void StartAudio(AudioSource src, float volume)
    {
        if (src == null) return;
        src.volume = volume;
        if (!src.isPlaying) src.Play();
    }

    private void StopAudio(AudioSource src)
    {
        if (src != null && src.isPlaying) src.Stop();
    }

    private static float LocalRadiusForWorld(Transform t, float worldRadius)
    {
        Vector3 s = t != null ? t.lossyScale : Vector3.one;
        float maxAxis = Mathf.Max(Mathf.Abs(s.x), Mathf.Abs(s.y), Mathf.Abs(s.z), 0.0001f);
        return worldRadius / maxAxis;
    }

    // ============================================================
    //  PUBLIC (Debug)
    // ============================================================

    public bool LevelActive => _levelActive;
    public string CurrentPhase => _phase.ToString();
    public float Fv1Pressure => _fv1.pressureCurrent;
    public float Fv2Pressure => _fv2.pressureCurrent;
    public float Fv3Pressure => _fv3.pressureCurrent;
    public bool Fv1Stable => _fv1 != null && _fv1.isStable;
    public bool Fv2Stable => _fv2 != null && _fv2.isStable;
    public bool Fv3Stable => _fv3 != null && _fv3.isStable;
    public bool AutoclaveValveOpened => _autoStage != null && _autoStage.openPercent >= 0.99f;
    public bool AllStagesStable => Fv1Stable && Fv2Stable && Fv3Stable;
    public bool Sample2Taken => _sampleTaken != null && _sampleTaken.Length > 1 && _sampleTaken[1];
    public bool Sample3Taken => _sampleTaken != null && _sampleTaken.Length > 2 && _sampleTaken[2];
    public bool IsWaitingDcs => _levelActive && _phase == Phase.MenungguDcs;
    public bool IsOpenFV1 => _phase == Phase.OpenFV1;
    public bool IsOpenFV2 => _phase == Phase.OpenFV2;
    public bool IsOpenFV3 => _phase == Phase.OpenFV3;
    public bool IsSamplingPhase => _phase == Phase.Sampling;
    public bool IsLabSubmitPhase => _phase == Phase.LabSubmit;
    public bool IsWaitingVoice => _phase == Phase.MenungguLapor;
    public bool IsCompleted => _phase == Phase.Selesai;

    [ContextMenu("Debug: Force Activate Level 8")]
    public void DebugActivate() => ActivateLevel();

    [ContextMenu("Debug: Skip to Flash Complete")]
    public void DebugSkipToSampling()
    {
        _fv1.openPercent = 1f; _fv1.isStable = true; _fv1.pressureCurrent = _fv1.pressureTarget; _fv1.tempCurrent = _fv1.tempTarget;
        _fv2.openPercent = 1f; _fv2.isStable = true; _fv2.pressureCurrent = _fv2.pressureTarget; _fv2.tempCurrent = _fv2.tempTarget;
        _fv3.openPercent = 1f; _fv3.isStable = true; _fv3.pressureCurrent = _fv3.pressureTarget; _fv3.tempCurrent = _fv3.tempTarget;
        SetStatusStripColor(_fv1StatusStrip, Color.green);
        SetStatusStripColor(_fv2StatusStrip, Color.green);
        SetStatusStripColor(_fv3StatusStrip, Color.green);
        GameLevelManager.Instance?.NotifyLevel8FlashLetdownDone();
        GameLevelManager.Instance?.NotifyLevel8SampleTaken();
        _phase = Phase.MenungguLapor;
        if (_hud != null) _hud.ShowNotifPublic(_msgFv3Done, 6f);
    }
}
