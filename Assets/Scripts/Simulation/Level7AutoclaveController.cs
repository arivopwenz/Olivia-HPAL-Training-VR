using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Unity.XR.CoreUtils;
using UnityEngine.XR.Interaction.Toolkit.Interactables;

/// <summary>
/// OLIVIA VR — Level 7 AutoclaveController.cs
///
/// FLOW LEVEL 7 — Autoclave Inspection (research-driven, real HPAL SOP):
///   1. Player di DCS → tekan tombol DCS 7
///   2. Fade teleport ke platform inspeksi autoclave (di atas pad concrete)
///   3. HUD: "Buka valve underflow di bawah autoclave untuk izinkan slurry masuk"
///   4. Player grab `L7_LiquidUnderflow_Handwheel*` (group ke pivot runtime)
///   5. Putar handwheel → 100% open → cairan ungu mulai naik di shader (world-Y clip)
///   6. Player aktifkan X-Ray Vision (X key) → shell autoclave transparan biru,
///      slurry `L7_XRay_InnerSlurry_Surface` jadi visible
///   7. Slurry naik dari Y bawah autoclave (~3.1) ke Y atas (~14.5) dalam ~12 detik
///   8. Player monitor parameter di gauge analog + koordinasi DCS
///   9. Saat fill 100% + X-Ray sudah dilihat + safety drill done → quest complete
///  10. Lapor HT: "DCS, suhu 250, tekanan 47.5 atm, agitator 60 RPM"
///  11. Fade ke Level 8
///
/// CATATAN: Sample port dihilangkan dari Level 7 (sampling sebenarnya di Level 9 Flash Vessel).
/// Autoclave 250°C/50 Bar terlalu berbahaya untuk sampling langsung.
/// </summary>
public class Level7AutoclaveController : MonoBehaviour
{
    [Header("=== Player ===")]
    [SerializeField] private Transform _playerRigRoot;
    [SerializeField] private Transform _teleportTargetAutoclave;
    [Tooltip("Spawn target di atas autoclave untuk fase monitor X-Ray + acid injection.")]
    [SerializeField] private Transform _teleportTargetTopDeck;
    [SerializeField] private float _fadeTransitionDuration = 2.5f;
    [SerializeField] private float _jedaSetelahValveOpen = 4f;

    [Header("=== Underflow Inlet Valve ===")]
    [Tooltip("Pivot runtime hasil group L7_LiquidUnderflow_Handwheel_Hub + Spokes + OuterRing.")]
    [SerializeField] private Transform _inletValvePivot;
    [SerializeField] private XRGrabInteractable _inletValveGrab;
    [SerializeField] private Vector3 _inletValveAxisLocal = Vector3.up;
    [SerializeField] private float _inletValveFullOpenDegrees = 1080f;

    [Header("=== Inner Slurry Visual (dalam autoclave) ===")]
    [Tooltip("L7_XRay_InnerSlurry_Surface — mesh cairan di dalam autoclave yang naik perlahan.")]
    [SerializeField] private Transform _innerSlurrySurface;
    [SerializeField] private float _slurryFillDuration = 12f;
    [Tooltip("Slurry akan mengisi sampai persentase ini (default 0.5 = setengah autoclave).")]
    [Range(0.3f, 1f)] [SerializeField] private float _slurryFillTargetPercent = 0.5f;

    [Header("=== Acid Drop Animation (dari atas) ===")]
    [Tooltip("Durasi total acid drop dari atas ke permukaan slurry.")]
    [SerializeField] private float _acidDropDuration = 6f;
    [SerializeField] private Color _acidColor = new Color(1f, 0.92f, 0.35f); // kuning asam sulfat
    [SerializeField] private float _acidStreamWidth = 0.7f;

    [Header("=== Liquid Swirl VFX (mengikuti rotor) ===")]
    [Tooltip("Liquid akan berputar mengikuti agitator setelah X-Ray aktif.")]
    [SerializeField] private float _swirlIntensity = 0.6f;
    [SerializeField] private float _swirlRPM = 30f; // setengah dari agitator RPM

    [Header("=== Autoclave Shell (X-Ray) ===")]
    [SerializeField] private GameObject _autoclaveField;
    [SerializeField] private List<Renderer> _shellRenderers = new List<Renderer>();
    [SerializeField] private Material _xrayMaterial;
    [SerializeField] private GameObject[] _xrayOnlyObjects;
    [SerializeField] private KeyCode _xrayToggleKey = KeyCode.X;

    [Header("=== Agitator ===")]
    [SerializeField] private Transform[] _agitatorShafts;
    [Tooltip("Target RPM final saat mesin sudah hidup penuh.")]
    [SerializeField] private float _agitatorRPM = 60f;
    [SerializeField] private Vector3 _agitatorAxis = Vector3.up;
    [Tooltip("Axis dunia (world space) untuk rotate seluruh agitator rotor (Y default).")]
    [SerializeField] private bool _agitatorAxisWorldSpace = true;
    [Tooltip("Arah rotasi: -1 = ke kiri (counter-clockwise lihat dari atas), +1 = ke kanan.")]
    [SerializeField] private float _agitatorDirection = -1f;
    [Tooltip("Durasi ramp-up dari 0 ke target RPM (sirine + slow start).")]
    [SerializeField] private float _agitatorRampUpDuration = 5f;

    [Header("=== Status Indicator Lamps ===")]
    [Tooltip("Lampu merah — nyala saat valve sedang diputar (proses).")]
    [SerializeField] private Renderer _redLamp;
    [Tooltip("Lampu hijau — nyala saat valve 100% open + slurry filling.")]
    [SerializeField] private Renderer _greenLamp;
    [SerializeField] private Color _redLampColor = new Color(1f, 0.1f, 0.1f);
    [SerializeField] private Color _greenLampColor = new Color(0.1f, 1f, 0.2f);
    [SerializeField] private Color _lampOffColor = new Color(0.2f, 0.2f, 0.2f);

    [Header("=== Safety Drill ===")]
    [SerializeField] private Transform _safetyTargetPSV;
    [SerializeField] private Transform _safetyTargetESD;
    [SerializeField] private Transform _safetyTargetQuench;
    [SerializeField] private Transform _safetyTargetExit;
    [SerializeField] private KeyCode _safetyDrillNextKey = KeyCode.S;

    [Header("=== Audio ===")]
    [SerializeField] private AudioSource _reactorHumAudio;
    [SerializeField] private AudioSource _slurryFlowAudio;
    [SerializeField] private AudioSource _sirenAudio;
    [SerializeField] private AudioSource _engineStartAudio;
    [Range(0f, 1f)] [SerializeField] private float _reactorHumVolume = 0.4f;
    [Range(0f, 1f)] [SerializeField] private float _slurryFlowVolume = 0.45f;
    [Range(0f, 1f)] [SerializeField] private float _sirenVolume = 0.55f;
    [Range(0f, 1f)] [SerializeField] private float _engineStartVolume = 0.6f;

    [Header("=== Gauge Display (real-time) ===")]
    [SerializeField] private float _temperatureValue = 252f;
    [SerializeField] private float _pressureValue = 47.5f;
    [SerializeField] private float _rpmValue = 60f;

    [Header("=== HUD Messages ===")]
    [TextArea(2, 4)] [SerializeField] private string _msgStart =
        "Inspeksi autoclave. Cari handwheel underflow di bawah autoclave dan putar untuk membuka aliran slurry.";
    [TextArea(2, 4)] [SerializeField] private string _msgValveOpened =
        "Valve terbuka! Slurry mulai mengalir masuk autoclave (pantau cairan naik 12 detik).";
    [TextArea(2, 4)] [SerializeField] private string _msgValveFullOpen =
        "Slurry sudah penuh! Sekarang TEKAN [X] untuk aktifkan X-Ray Vision tembus shell.";
    [TextArea(2, 4)] [SerializeField] private string _msgXrayReminder =
        "Tekan [X] untuk aktifkan X-Ray Vision (lihat slurry + agitator dalam autoclave).";
    [TextArea(2, 4)] [SerializeField] private string _msgXrayActive =
        "X-Ray aktif. Slurry & agitator dalam autoclave terlihat. Pantau proses sampai penuh.";
    [TextArea(2, 4)] [SerializeField] private string _msgSafetyDrill =
        "SAFETY CHECK: Sistem sedang verifikasi 4 titik darurat (PSV/ESD/Quench/Exit) sebelum mesin nyala.";
    [TextArea(2, 4)] [SerializeField] private string _msgComplete =
        "Inspeksi lengkap! Lapor HT: 'autoclave normal, suhu 250, tekanan 47.5 atm, agitator 60 RPM'.";

    // ========== Runtime State ==========
    private enum Phase
    {
        Idle,
        MenungguDcsStart,
        TeleportKeAutoclave,
        BukaInletValve,
        TeleportKeTopDeck,
        ValveSelesaiMonitor,    // Setelah valve full open: di top deck, fade transition
        AcidInjectionFalling,   // Asam jatuh dari atas 3-4 detik
        SlurryRising,           // Slurry naik dari bawah ke 3/4 (12 detik)
        XrayDanMonitor,
        SafetyDrill,
        Selesai
    }

    private Phase _phase = Phase.Idle;
    private bool _levelActive;
    private bool _xrayActive;
    private bool _slurryFillStarted;
    private float _slurryFillProgress; // 0..1
    private float _inletValveDegrees;
    private float _inletValveOpenPercent;
    private bool _inletValveGrabbed;
    private bool _inletValveHover;

    private bool _inletYawValid;
    private float _inletYawLast;
    private Transform _inletInteractorAttach;
    private Quaternion _inletPivotBaseRotation = Quaternion.identity;
    private float _agitatorAngle;
    private float _agitatorCurrentRPM = 0f; // ramp-up dari 0 ke target
    private bool _agitatorRunning = false;
    private int _safetyDrillStep = 0; // 0..4
    private Material[] _originalShellMaterials;
    private PlayerHUD _hud;
    private Coroutine _seqCoroutine;

    // Slurry fill (shader-based world-Y mask)
    private Material _slurryFillMaterial;
    private Renderer _innerSlurryRenderer;
    private float _slurryYBottom = 3.1f;
    private float _slurryYTop = 14.5f;

    private void Awake()
    {
        _hud = FindFirstObjectByType<PlayerHUD>();
        AutoFindReferences();
        EnsureXRayMaterial();
        EnsureInletValvePivot();
        EnsureSlurrySetup();
    }

    private void Start()
    {
        // Failsafe: jika scene loaded dengan Level7 sebagai current level, aktifkan.
        // Ini menutup race condition dimana OnLevelStarted fire sebelum subscribe.
        if (GameLevelManager.Instance != null
            && GameLevelManager.Instance.CurrentLevel == GameLevelManager.GameLevel.Level7_Autoclave
            && !_levelActive)
        {
            ActivateLevel();
        }
    }

    private void OnEnable()
    {
        GameLevelManager.OnLevelStarted += OnLevelStarted;
        GameLevelManager.OnDCSButtonPressed += OnDcsButtonPressed;
        GameLevelManager.OnVoiceReportAccepted += OnVoiceReportAccepted;

        if (GameLevelManager.Instance != null
            && GameLevelManager.Instance.CurrentLevel == GameLevelManager.GameLevel.Level7_Autoclave
            && !_levelActive)
        {
            ActivateLevel();
        }
    }

    private void OnDisable()
    {
        GameLevelManager.OnLevelStarted -= OnLevelStarted;
        GameLevelManager.OnDCSButtonPressed -= OnDcsButtonPressed;
        GameLevelManager.OnVoiceReportAccepted -= OnVoiceReportAccepted;
        if (_seqCoroutine != null) StopCoroutine(_seqCoroutine);
        StopAudio(_reactorHumAudio);
        StopAudio(_slurryFlowAudio);
        StopAudio(_sirenAudio);
        StopAudio(_engineStartAudio);
    }

    // Flag untuk menunggu laporan HT manual dari player.
    private bool _waitingForVoiceReport;
    private bool _voiceReportReceived;

    private void OnVoiceReportAccepted(string keyword)
    {
        if (!_levelActive) return;
        if (_waitingForVoiceReport)
        {
            _voiceReportReceived = true;
        }
    }

    /// <summary>Coroutine helper: tunggu sampai player lapor HT (tahan T, bicara, lepas).</summary>
    private IEnumerator WaitForVoiceReport(string hudMessage, float hudDuration = 8f)
    {
        _waitingForVoiceReport = true;
        _voiceReportReceived = false;
        if (_hud != null) _hud.ShowNotifPublic(hudMessage, hudDuration);
        while (!_voiceReportReceived) yield return null;
        _waitingForVoiceReport = false;
    }

    private void OnLevelStarted(GameLevelManager.GameLevel level)
    {
        if (level == GameLevelManager.GameLevel.Level7_Autoclave)
        {
            ActivateLevel();
        }
        else
        {
            _levelActive = false;
            ResetVisuals();
            _phase = Phase.Idle;
        }
    }

    private void ActivateLevel()
    {
        _levelActive = true;
        AutoFindReferences();
        EnsureXRayMaterial();
        EnsureInletValvePivot();
        EnsureSlurrySetup();
        ResetState();
        // Force shell ke material UVAtlas asli (kalau scene save terakhir state X-Ray aktif).
        ForceRestoreShellToOriginal();
        _phase = Phase.MenungguDcsStart;

        if (GameLevelManager.Instance != null)
        {
            GameLevelManager.Instance.SetSuhu(_temperatureValue);
            GameLevelManager.Instance.SetTekanan(_pressureValue);
            GameLevelManager.Instance.SetRPM(_rpmValue);
        }
        StartReactorAudio();
        Debug.Log("[Level7] ActivateLevel — phase=MenungguDcsStart, slurry siap, valve siap.");
    }

    private void OnDcsButtonPressed(int nomorTombol)
    {
        if (!_levelActive || nomorTombol != 7) return;
        if (_phase != Phase.MenungguDcsStart) return;
        StartSequence(TeleportKeAutoclaveCoroutine());
    }

    private IEnumerator TeleportKeAutoclaveCoroutine()
    {
        _phase = Phase.TeleportKeAutoclave;
        float d = Mathf.Max(2f, _fadeTransitionDuration);
        if (_hud != null) _hud.PlayManualFade(d);
        yield return new WaitForSeconds(d * 0.5f);
        TeleportPlayerToAutoclave();
        yield return new WaitForSeconds(d * 0.5f);

        _phase = Phase.BukaInletValve;
        if (_hud != null) _hud.ShowNotifPublic(_msgStart);
        EnsureInletValvePivot();
    }

    private void Update()
    {
        if (!_levelActive) return;

        AnimateAgitator();

        if (_phase == Phase.BukaInletValve)
        {
            if (_inletGH != null) _inletValveOpenPercent = _inletGH.OpenPercent01;
            bool changed = false;
            if (_inletValveOpenPercent > 0.01f && _inletValveOpenPercent < 0.99f) SetStatusLamp(false);
            if (changed)
            {
                UpdateInletValveVisuals();
                // Saat valve diputar (% 0..99), lampu MERAH ON.
                if (_inletValveOpenPercent > 0.01f && _inletValveOpenPercent < 0.99f)
                    SetStatusLamp(false);
            }

            if (_inletValveOpenPercent >= 0.99f && !_slurryFillStarted)
            {
                _slurryFillStarted = true;
                StartSequence(SlurryFillCoroutine());
            }
        }

        if (Input.GetKeyDown(_xrayToggleKey)) ToggleXRay();

        // Slurry fill animation now handled inside SlurryFillCoroutine (with 75% cap), not auto in Update.
        // Safety drill sekarang auto-progress via coroutine, tidak butuh user input.

        // Liquid swirl effect (post-fill, mengikuti rotor)
        if (_swirlActive)
        {
            UpdateLiquidSwirl();
        }
    }

    private IEnumerator SlurryFillCoroutine()
    {
        _phase = Phase.ValveSelesaiMonitor;
        SetStatusLamp(true); // Lampu hijau: valve full open.

        // === STEP 1: Jeda 4 detik, lalu fade teleport ke top deck ===
        if (_hud != null) _hud.ShowNotifPublic("Valve full open! Bersiap pindah ke deck atas...", 4f);
        yield return new WaitForSeconds(_jedaSetelahValveOpen);

        float fadeDur = Mathf.Max(2f, _fadeTransitionDuration);
        if (_hud != null) _hud.PlayManualFade(fadeDur);
        yield return new WaitForSeconds(fadeDur * 0.5f);
        TeleportPlayerToTopDeck();
        yield return new WaitForSeconds(fadeDur * 0.5f);

        // === STEP 2: TUNGGU PLAYER LAPOR HT MANUAL: "Slurry mulai masuk autoclave" ===
        yield return StartCoroutine(WaitForVoiceReport(
            "LAPOR HT (tahan T): 'Slurry mulai masuk autoclave dari underflow.'\nSetelah laporan diterima, tekan X untuk aktifkan X-Ray.", 12f));
        GameLevelManager.Instance?.NotifyLevel7GaugesLogged();

        // === STEP 3: Tunggu player tekan X untuk X-Ray (shell BARU transparan di sini) ===
        _phase = Phase.XrayDanMonitor;
        float remindTimer = 0f;
        while (!_xrayActive)
        {
            remindTimer += Time.deltaTime;
            if (remindTimer >= 8f)
            {
                if (_hud != null) _hud.ShowNotifPublic("Tekan [X] untuk aktifkan X-Ray Vision (lihat dalam autoclave).", 6f);
                remindTimer = 0f;
            }
            yield return null;
        }

        // === STEP 4: X-Ray aktif → animasi slurry naik 0% → 50% (setengah) ===
        _phase = Phase.SlurryRising;
        if (_hud != null) _hud.ShowNotifPublic("X-RAY ON: Pantau slurry naik dari bawah ke setengah autoclave.", 8f);
        EnsureSlurryFlowAudio();
        StartAudio(_slurryFlowAudio, _slurryFlowVolume);

        float t = 0f;
        while (t < _slurryFillDuration)
        {
            t += Time.deltaTime;
            float prog = Mathf.Clamp01(t / _slurryFillDuration) * _slurryFillTargetPercent;
            _slurryFillProgress = prog;
            UpdateSlurryShader(prog);
            yield return null;
        }
        StopAudio(_slurryFlowAudio);

        // === STEP 5: Acid drop dari atas (3.5 detik) ===
        if (_hud != null) _hud.ShowNotifPublic("Acid sulfuric injection dari pipa atas...", 5f);
        EnsureAcidDropEffect();
        yield return StartCoroutine(AcidDropCoroutine());

        // === STEP 6: TUNGGU PLAYER LAPOR HT MANUAL: "Autoclave terisi, mesin siap" ===
        yield return StartCoroutine(WaitForVoiceReport(
            "LAPOR HT (tahan T): 'Autoclave terisi setengah, acid masuk, mesin siap dinyalakan.'\nSetelah laporan diterima, mesin akan menyala.", 12f));

        // === STEP 7: Siren + Engine + Agitator ramp-up ===
        GameLevelManager.Instance?.NotifyLevel7SafetyDrillDone();
        GameLevelManager.Instance?.NotifyLevel7ScaleMarked();
        GameLevelManager.Instance?.NotifyLevel7SampleTaken();
        yield return StartCoroutine(StartupSequenceCoroutine());

        _phase = Phase.Selesai;
        ShowMissionCompleteUI();
    }

    private IEnumerator StartupSequenceCoroutine()
    {
        if (_hud != null) _hud.ShowNotifPublic(
            "LAPOR HT: 'Autoclave terisi penuh, parameter normal. Mulai proses oksidasi.'", 6f);
        yield return new WaitForSeconds(2f);

        // Siren startup (alarm bahwa rotor akan mulai berputar).
        if (_hud != null) _hud.ShowNotifPublic("⚠ SIRINE: Mesin akan menyala. Bersiap.", 4f);
        EnsureSirenAudio();
        StartAudio(_sirenAudio, _sirenVolume);
        yield return new WaitForSeconds(3.5f);
        StopAudio(_sirenAudio);

        // Engine ignition sound + agitator slow start.
        if (_hud != null) _hud.ShowNotifPublic("MESIN HIDUP: agitator stir mulai berputar...", 5f);
        EnsureEngineStartAudio();
        StartAudio(_engineStartAudio, _engineStartVolume);
        _agitatorRunning = true;
        _agitatorCurrentRPM = 0f;
        // Ramp-up dari 0 ke target RPM secara perlahan.
        float rampT = 0f;
        while (rampT < _agitatorRampUpDuration)
        {
            rampT += Time.deltaTime;
            float p = Mathf.Clamp01(rampT / _agitatorRampUpDuration);
            float eased = p * p * (3f - 2f * p);
            _agitatorCurrentRPM = _agitatorRPM * eased;
            yield return null;
        }
        _agitatorCurrentRPM = _agitatorRPM;
        StopAudio(_engineStartAudio);

        StartLiquidSwirl();
        if (_hud != null) _hud.ShowNotifPublic(
            "Mesin sudah hidup. Proses autoclave berjalan. Misi selesai!", 6f);
        yield return new WaitForSeconds(2f);

        _phase = Phase.Selesai;
        ShowMissionCompleteUI();
    }

    public void ConfirmSafetyDrillStep()
    {
        // LEGACY method — sekarang safety drill auto-progress di SlurryFillCoroutine.
        // Method ini di-keep agar backwards-compatible kalau masih ada yg call dari menu/debug.
        if (!_levelActive) return;
    }

    // ============================================================
    //  MISSION COMPLETE UI (canvas dengan Stay / Lanjut DCS)
    // ============================================================

    private GameObject _missionCompleteCanvas;

    private void ShowMissionCompleteUI()
    {
        if (_missionCompleteCanvas != null)
        {
            _missionCompleteCanvas.SetActive(true);
            return;
        }
        _missionCompleteCanvas = BuildMissionCompleteCanvas();
    }

    private GameObject BuildMissionCompleteCanvas()
    {
        var canvasGO = new GameObject("L7_MissionComplete_Canvas");
        canvasGO.transform.SetParent(transform, false);
        var canvas = canvasGO.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.WorldSpace;
        canvasGO.AddComponent<UnityEngine.UI.CanvasScaler>();
        canvasGO.AddComponent<UnityEngine.UI.GraphicRaycaster>();

        // Posisikan canvas di depan player (di top deck).
        Vector3 pos = _playerRigRoot != null ? _playerRigRoot.position : Vector3.zero;
        var headTransform = _playerRigRoot != null ? GetPlayerHeadTransform() : null;
        if (headTransform != null)
        {
            pos = headTransform.position + headTransform.forward * 1.8f;
            canvasGO.transform.rotation = Quaternion.LookRotation(headTransform.forward, Vector3.up);
        }
        else
        {
            pos += new Vector3(0, 1.5f, 1.8f);
        }
        canvasGO.transform.position = pos;
        var rect = canvasGO.GetComponent<RectTransform>();
        rect.sizeDelta = new Vector2(1.6f, 0.9f);
        canvasGO.transform.localScale = Vector3.one * 0.6f;

        // Background panel
        var bg = new GameObject("BG");
        bg.transform.SetParent(canvasGO.transform, false);
        var bgImg = bg.AddComponent<UnityEngine.UI.Image>();
        bgImg.color = new Color(0.08f, 0.12f, 0.2f, 0.92f);
        var bgRect = bg.GetComponent<RectTransform>();
        bgRect.anchorMin = Vector2.zero; bgRect.anchorMax = Vector2.one;
        bgRect.offsetMin = Vector2.zero; bgRect.offsetMax = Vector2.zero;

        // Title
        var titleGO = new GameObject("Title");
        titleGO.transform.SetParent(canvasGO.transform, false);
        var titleText = titleGO.AddComponent<UnityEngine.UI.Text>();
        titleText.text = "✓ LEVEL 7 SELESAI";
        titleText.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        titleText.fontSize = 36;
        titleText.color = new Color(0.4f, 1f, 0.5f);
        titleText.alignment = TextAnchor.MiddleCenter;
        titleText.fontStyle = FontStyle.Bold;
        var titleRect = titleGO.GetComponent<RectTransform>();
        titleRect.anchorMin = new Vector2(0, 0.7f); titleRect.anchorMax = new Vector2(1, 1f);
        titleRect.offsetMin = Vector2.zero; titleRect.offsetMax = Vector2.zero;

        // Subtitle
        var subtitleGO = new GameObject("Subtitle");
        subtitleGO.transform.SetParent(canvasGO.transform, false);
        var subText = subtitleGO.AddComponent<UnityEngine.UI.Text>();
        subText.text = "Autoclave proses berjalan normal.\nLanjutkan ke level berikutnya?";
        subText.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        subText.fontSize = 22;
        subText.color = Color.white;
        subText.alignment = TextAnchor.MiddleCenter;
        var subRect = subtitleGO.GetComponent<RectTransform>();
        subRect.anchorMin = new Vector2(0, 0.4f); subRect.anchorMax = new Vector2(1, 0.7f);
        subRect.offsetMin = Vector2.zero; subRect.offsetMax = Vector2.zero;

        // Button: STAY
        var btnStay = CreateUIButton(canvasGO.transform, "STAY (lihat proses)",
            new Vector2(0.05f, 0.05f), new Vector2(0.48f, 0.32f),
            new Color(0.2f, 0.4f, 0.7f),
            () => HideMissionCompleteUI());

        // Button: KEMBALI KE DCS
        var btnNext = CreateUIButton(canvasGO.transform, "KEMBALI KE DCS",
            new Vector2(0.52f, 0.05f), new Vector2(0.95f, 0.32f),
            new Color(0.3f, 0.7f, 0.4f),
            () => GoToNextLevel());

        return canvasGO;
    }

    private GameObject CreateUIButton(Transform parent, string label,
        Vector2 anchorMin, Vector2 anchorMax,
        Color color, System.Action onClick)
    {
        var btn = new GameObject(label);
        btn.transform.SetParent(parent, false);
        var img = btn.AddComponent<UnityEngine.UI.Image>();
        img.color = color;
        var rect = btn.GetComponent<RectTransform>();
        rect.anchorMin = anchorMin; rect.anchorMax = anchorMax;
        rect.offsetMin = Vector2.zero; rect.offsetMax = Vector2.zero;
        var bb = btn.AddComponent<UnityEngine.UI.Button>();
        bb.targetGraphic = img;
        bb.onClick.AddListener(() => onClick?.Invoke());

        var txt = new GameObject("Text");
        txt.transform.SetParent(btn.transform, false);
        var t = txt.AddComponent<UnityEngine.UI.Text>();
        t.text = label;
        t.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        t.fontSize = 22;
        t.color = Color.white;
        t.alignment = TextAnchor.MiddleCenter;
        t.fontStyle = FontStyle.Bold;
        var tr = txt.GetComponent<RectTransform>();
        tr.anchorMin = Vector2.zero; tr.anchorMax = Vector2.one;
        tr.offsetMin = Vector2.zero; tr.offsetMax = Vector2.zero;
        return btn;
    }

    private Transform GetPlayerHeadTransform()
    {
        if (_playerRigRoot == null) return null;
        var origin = _playerRigRoot.GetComponent<XROrigin>();
        if (origin != null && origin.Camera != null) return origin.Camera.transform;
        var cam = _playerRigRoot.GetComponentInChildren<Camera>();
        return cam != null ? cam.transform : null;
    }

    private void HideMissionCompleteUI()
    {
        if (_missionCompleteCanvas != null) _missionCompleteCanvas.SetActive(false);
    }

    private void GoToNextLevel()
    {
        HideMissionCompleteUI();
        // Trigger transisi ke Level 8 via GameLevelManager.
        var glm = GameLevelManager.Instance;
        if (glm != null)
        {
            // Pakai SelesaikanLevelDanLanjut atau MulaiLevel langsung.
            // Cek API: GameLevel.Level8_Monitoring.
            glm.MulaiLevel(GameLevelManager.GameLevel.Level8_Monitoring);
        }
    }

    // ============================================================
    //  X-RAY VISION
    // ============================================================

    public void ToggleXRay()
    {
        _xrayActive = !_xrayActive;
        if (_xrayActive)
        {
            ApplyXRayMaterial();
            SetXRayObjectsVisible(true);
            GameLevelManager.Instance?.NotifyLevel7XrayActivated();
            if (_hud != null) _hud.ShowNotifPublic(_msgXrayActive, 8f);
        }
        else
        {
            RestoreShellMaterials();
            SetXRayObjectsVisible(false);
        }
    }

    private void ApplyXRayMaterial()
    {
        if (_xrayMaterial == null) EnsureXRayMaterial();
        // Cache hanya jika belum cached. Kalau sebelumnya sudah X-Ray (e.g. dari run lama),
        // kita tidak overwrite cache (yang sudah simpan original).
        if (_originalShellMaterials == null && _shellRenderers != null && _shellRenderers.Count > 0)
        {
            _originalShellMaterials = new Material[_shellRenderers.Count];
            for (int i = 0; i < _shellRenderers.Count; i++)
            {
                if (_shellRenderers[i] != null)
                {
                    var curMat = _shellRenderers[i].sharedMaterial;
                    // Skip kalau current sudah XRay material — pakai fallback dari "M_Level7_Autoclave_UVAtlas".
                    if (curMat != null && curMat.name.Contains("XRayShell"))
                    {
                        // Try to find original from project assets.
                        var fallback = FindAutoclaveOriginalMaterial();
                        _originalShellMaterials[i] = fallback;
                    }
                    else
                    {
                        _originalShellMaterials[i] = curMat;
                    }
                }
            }
        }
        if (_shellRenderers != null)
        {
            for (int i = 0; i < _shellRenderers.Count; i++)
                if (_shellRenderers[i] != null) _shellRenderers[i].sharedMaterial = _xrayMaterial;
        }
    }

    private Material FindAutoclaveOriginalMaterial()
    {
        // Cari material asli dari renderer lain yang masih punya UV atlas.
        var allRenderers = FindObjectsByType<Renderer>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        foreach (var r in allRenderers)
        {
            if (r.sharedMaterial == null) continue;
            string n = r.sharedMaterial.name;
            if (n.Contains("Level7_Autoclave_UVAtlas") && !n.Contains("XRayShell"))
            {
                return r.sharedMaterial;
            }
        }
        return null;
    }

    private void RestoreShellMaterials()
    {
        if (_originalShellMaterials == null || _shellRenderers == null) return;
        for (int i = 0; i < _shellRenderers.Count && i < _originalShellMaterials.Length; i++)
        {
            if (_shellRenderers[i] != null && _originalShellMaterials[i] != null)
                _shellRenderers[i].sharedMaterial = _originalShellMaterials[i];
        }
    }

    /// <summary>
    /// Force restore shell ke original material — dipanggil saat ActivateLevel
    /// untuk menjamin shell solid di awal level (bukan transparent dari run lama).
    /// Hanya restore renderer yang saat ini pakai X-Ray material (transparent).
    /// </summary>
    private void ForceRestoreShellToOriginal()
    {
        if (_shellRenderers == null) return;
        for (int i = 0; i < _shellRenderers.Count; i++)
        {
            if (_shellRenderers[i] == null) continue;
            var cur = _shellRenderers[i].sharedMaterial;
            if (cur == null) continue;
            // Hanya restore kalau material saat ini = X-Ray runtime (transparent).
            // Jangan sentuh material yang sudah benar (UV atlas / Flange Dark / dll).
            bool isXRay = cur.name.Contains("XRayShell") || cur.name.Contains("XRay_Runtime")
                       || cur.renderQueue >= 3000; // transparent queue
            if (!isXRay) continue;

            // Cari material asli dari nama renderer: Shell_Band pakai Ind_Flange_Dark, sisanya UV atlas.
            string rName = _shellRenderers[i].gameObject.name;
            Material original;
            if (rName.Contains("Shell_Band"))
            {
                original = FindMaterialByName("Ind_Flange_Dark");
            }
            else
            {
                original = FindAutoclaveOriginalMaterial();
            }
            if (original != null) _shellRenderers[i].sharedMaterial = original;
        }
        _originalShellMaterials = null;
    }

    private Material FindMaterialByName(string matName)
    {
        // Cari material dari renderer lain di scene yang pakai material dengan nama ini.
        var allRenderers = FindObjectsByType<Renderer>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        foreach (var r in allRenderers)
        {
            if (r.sharedMaterial != null && r.sharedMaterial.name == matName)
                return r.sharedMaterial;
        }
        return null;
    }

    private void SetXRayObjectsVisible(bool visible)
    {
        if (_xrayOnlyObjects == null) return;
        foreach (var go in _xrayOnlyObjects)
            if (go != null) go.SetActive(visible);
    }

    private void EnsureXRayMaterial()
    {
        if (_xrayMaterial != null) return;
        Shader shader = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard");
        Material mat = new Material(shader);
        mat.name = "M_L7_XRayShell_Runtime";
        Color blueGhost = new Color(0.3f, 0.7f, 1f, 0.18f);
        if (mat.HasProperty("_BaseColor")) mat.SetColor("_BaseColor", blueGhost);
        if (mat.HasProperty("_Color")) mat.SetColor("_Color", blueGhost);
        if (mat.HasProperty("_Surface")) mat.SetFloat("_Surface", 1f);
        if (mat.HasProperty("_Blend")) mat.SetFloat("_Blend", 0f);
        if (mat.HasProperty("_SrcBlend")) mat.SetFloat("_SrcBlend", (float)UnityEngine.Rendering.BlendMode.SrcAlpha);
        if (mat.HasProperty("_DstBlend")) mat.SetFloat("_DstBlend", (float)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
        if (mat.HasProperty("_ZWrite")) mat.SetFloat("_ZWrite", 0f);
        mat.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
        mat.EnableKeyword("_ALPHABLEND_ON");
        mat.SetOverrideTag("RenderType", "Transparent");
        mat.renderQueue = 3000;
        mat.EnableKeyword("_EMISSION");
        if (mat.HasProperty("_EmissionColor")) mat.SetColor("_EmissionColor", new Color(0.2f, 0.5f, 0.9f) * 0.5f);
        _xrayMaterial = mat;
    }

    // ============================================================
    //  INNER SLURRY — Shader-based world-Y clip plane
    // ============================================================

    private GameObject _innerSlurryVolume; // Runtime cylinder primitive untuk volume slurry penuh.

    private void EnsureSlurrySetup()
    {
        if (_innerSlurrySurface == null) return;
        _innerSlurryRenderer = _innerSlurrySurface.GetComponent<Renderer>();
        if (_innerSlurryRenderer == null) return;

        // Hitung Y bottom & top dunia dari bounds shell autoclave (bukan dari mesh original tipis).
        var shellRenderer = FindByNameInactive("L7_Autoclave_PressureShell")?.GetComponent<Renderer>();
        if (shellRenderer != null)
        {
            // Inner volume = shell minus 0.3m wall thickness.
            _slurryYBottom = shellRenderer.bounds.min.y + 0.3f;
            _slurryYTop = shellRenderer.bounds.max.y - 0.3f;
        }
        else
        {
            var b = _innerSlurryRenderer.bounds;
            _slurryYBottom = b.min.y;
            _slurryYTop = b.max.y;
        }

        // Build material baru jika belum ada ATAU shader-nya bukan custom Olivia.
        bool needRebuild = _slurryFillMaterial == null
            || _slurryFillMaterial.shader == null
            || _slurryFillMaterial.shader.name != "Olivia/L7SlurryFill";
        if (needRebuild)
        {
            _slurryFillMaterial = BuildSlurryFillMaterial();
        }
        _innerSlurryRenderer.sharedMaterial = _slurryFillMaterial;

        // Disable mesh statis L7_XRay_OpenLiquidCutFace (cutaway purple texture default dari Blender).
        var staticCutFace = FindByNameInactive("L7_XRay_OpenLiquidCutFace");
        if (staticCutFace != null && staticCutFace.gameObject.activeSelf)
        {
            staticCutFace.gameObject.SetActive(false);
        }

        // === BUAT INNER VOLUME CYLINDER (filled solid liquid yg mengisi tengah autoclave) ===
        EnsureInnerSlurryVolume();

        // Mulai dari empty (di bawah Y bottom = 0% fill).
        UpdateSlurryShader(0f);
    }

    /// <summary>
    /// Buat cylinder primitive runtime yang mengisi inner volume autoclave (tengahnya, bukan cuma pinggir).
    /// Pakai shader yang sama dengan _innerSlurrySurface, jadi fillY clip kerja sama-sama.
    /// </summary>
    private void EnsureInnerSlurryVolume()
    {
        if (_innerSlurryVolume != null) return;

        // Cari posisi center + dimensi shell.
        var shellRenderer = FindByNameInactive("L7_Autoclave_PressureShell")?.GetComponent<Renderer>();
        if (shellRenderer == null) return;
        var sb = shellRenderer.bounds;

        _innerSlurryVolume = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        _innerSlurryVolume.name = "L7_InnerSlurry_Volume_Runtime";
        _innerSlurryVolume.transform.SetParent(transform, false);
        // Hapus collider supaya tidak block agitator atau player.
        var col = _innerSlurryVolume.GetComponent<Collider>();
        if (col != null) Destroy(col);

        // Posisikan di center shell.
        _innerSlurryVolume.transform.position = sb.center;

        // Cylinder primitive default = vertical (Y axis up, length 2m, radius 0.5m).
        // Kita perlu cylinder horizontal (sumbu panjang = X) sesuai autoclave.
        // Rotate 90° di Z untuk membuat cylinder lying on side dengan length axis di X.
        _innerSlurryVolume.transform.rotation = Quaternion.Euler(0f, 0f, 90f);

        // Scale untuk match dimensi inner volume autoclave:
        // - Length (sekarang scale Y setelah rotate = world X): cylinder bawaan 2m → scale Y = X size / 2
        //   Inner length = shell X - 1m (ada flange/endcap di kiri-kanan).
        // - Radius (scale X dan Z): cylinder default radius 0.5 → scale = radius / 0.5
        //   Inner radius = min(Y, Z) / 2 - 0.3 (wall thickness)
        float innerLength = sb.size.x - 1.0f; // 35.8 - 1 = 34.8m (lebih panjang, isi lebih banyak)
        float innerRadius = Mathf.Min(sb.size.y, sb.size.z) / 2f - 0.15f; // ~4.6m (lebih lebar, isi tengah penuh)
        _innerSlurryVolume.transform.localScale = new Vector3(innerRadius * 2f, innerLength * 0.5f, innerRadius * 2f);

        // Apply shared slurry material.
        var renderer = _innerSlurryVolume.GetComponent<Renderer>();
        renderer.sharedMaterial = _slurryFillMaterial;

        _innerSlurryVolume.SetActive(false); // Mulai hidden.
    }

    private Material BuildSlurryFillMaterial()
    {
        // Pakai custom shader Olivia/L7SlurryFill yang clip world-Y.
        Shader shader = Shader.Find("Olivia/L7SlurryFill");
        if (shader == null)
        {
            // Fallback URP/Lit kalau shader belum ke-import.
            shader = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard");
            Debug.LogWarning("[Level7] Olivia/L7SlurryFill shader tidak ditemukan, fallback ke URP/Lit (no clip).");
        }
        Material mat = new Material(shader);
        mat.name = "M_L7_SlurryFill_Runtime";
        // HPAL slurry/PLS = cairan asam panas, render sebagai air 3D translucent realistic
        // (biru-teal, gradient gelap di dalam, permukaan glowing). Bukan solid ungu.
        Color shallow = new Color(0.16f, 0.55f, 0.62f);   // warna dekat permukaan (terang teal)
        Color deep = new Color(0.03f, 0.17f, 0.27f);      // warna dalam (gelap biru)
        if (mat.HasProperty("_BaseColor")) mat.SetColor("_BaseColor", shallow);
        if (mat.HasProperty("_Color")) mat.SetColor("_Color", shallow);
        if (mat.HasProperty("_DeepColor")) mat.SetColor("_DeepColor", deep);
        if (mat.HasProperty("_EmissionColor")) mat.SetColor("_EmissionColor", new Color(0.12f, 0.45f, 0.55f));
        if (mat.HasProperty("_EmissionIntensity")) mat.SetFloat("_EmissionIntensity", 0.25f);
        if (mat.HasProperty("_SurfaceGlow")) mat.SetFloat("_SurfaceGlow", 2.5f);
        if (mat.HasProperty("_SurfaceWidth")) mat.SetFloat("_SurfaceWidth", 0.45f);
        if (mat.HasProperty("_DepthRange")) mat.SetFloat("_DepthRange", 8f);
        if (mat.HasProperty("_FresnelPower")) mat.SetFloat("_FresnelPower", 3f);
        if (mat.HasProperty("_SpecPower")) mat.SetFloat("_SpecPower", 64f);
        if (mat.HasProperty("_SpecIntensity")) mat.SetFloat("_SpecIntensity", 1.4f);
        if (mat.HasProperty("_RippleScale")) mat.SetFloat("_RippleScale", 6f);
        if (mat.HasProperty("_RippleSpeed")) mat.SetFloat("_RippleSpeed", 0.8f);
        if (mat.HasProperty("_RippleStrength")) mat.SetFloat("_RippleStrength", 0.06f);
        if (mat.HasProperty("_Alpha")) mat.SetFloat("_Alpha", 0.7f); // translucent
        if (mat.HasProperty("_FillY")) mat.SetFloat("_FillY", -1000f);
        mat.EnableKeyword("_EMISSION");
        // Force render queue Transparent.
        mat.renderQueue = 3010;
        return mat;
    }

    private void UpdateSlurryShader(float t)
    {
        // Inner slurry SURFACE (mesh asli tipis) — di-disable sepenuhnya, kita pakai volume cylinder.
        if (_innerSlurrySurface != null && _innerSlurrySurface.gameObject.activeSelf)
            _innerSlurrySurface.gameObject.SetActive(false);

        // 0% fill: hide volume cylinder completely.
        bool shouldShow = t > 0.005f;
        if (_innerSlurryVolume != null && _innerSlurryVolume.activeSelf != shouldShow)
        {
            _innerSlurryVolume.SetActive(shouldShow);
        }
        if (!shouldShow) return;

        if (_slurryFillMaterial == null) return;

        // Set _FillY = lerp dari (slurryYBottom - 0.05) ke (slurryYTop + 0.1) berdasarkan progress t.
        float fillY = Mathf.Lerp(_slurryYBottom - 0.05f, _slurryYTop + 0.1f, t);
        if (_slurryFillMaterial.HasProperty("_FillY"))
            _slurryFillMaterial.SetFloat("_FillY", fillY);

        // Emission intensity meningkat saat penuh.
        if (_slurryFillMaterial.HasProperty("_EmissionIntensity"))
            _slurryFillMaterial.SetFloat("_EmissionIntensity", 0.4f + 0.6f * t);

        // Alpha lebih transparan saat fill belum penuh (rotor visible), lebih opaque saat penuh.
        if (_slurryFillMaterial.HasProperty("_Alpha"))
            _slurryFillMaterial.SetFloat("_Alpha", Mathf.Lerp(0.45f, 0.75f, t));
    }

    // ============================================================
    //  INLET VALVE — group handwheel parts (RotateAround approach)
    // ============================================================

    // Cached parts dan baseline rotations untuk rotate around pivot.
    private Transform[] _handwheelParts;
    private Quaternion[] _handwheelPartsBaseRotation;
    private Vector3[] _handwheelPartsBasePosition;
    private Vector3 _handwheelPivotWorld;
    private Vector3 _handwheelAxisWorld = Vector3.up;
    private GesturalHandwheel _inletGH;

    private HandwheelVirtualPivot _handwheelVirtualPivot;

    private void EnsureInletValvePivot()
    {
        if (_inletGH != null) return;

        Transform hub = FindByNameInactive("L7_LiquidUnderflow_Handwheel_Hub");
        if (hub == null) return;

        if (_inletValvePivot == null)
        {
            GameObject pivotGo = new GameObject("L7_InletValve_Pivot_Runtime");
            Transform pivot = pivotGo.transform;
            pivot.position = hub.position;
            pivot.rotation = hub.rotation;
            _inletValvePivot = pivot;
            _inletPivotBaseRotation = pivot.rotation;
        }

        string[] partTokens = {
            "L7_LiquidUnderflow_Handwheel_Hub",
            "L7_LiquidUnderflow_Handwheel_OuterRing",
            "L7_LiquidUnderflow_Handwheel_Spoke_00",
            "L7_LiquidUnderflow_Handwheel_Spoke_01",
            "L7_LiquidUnderflow_Handwheel_Spoke_02",
            "L7_LiquidUnderflow_Handwheel_Spoke_03"
        };
        var parts = new System.Collections.Generic.List<Transform>();
        foreach (var token in partTokens)
        {
            Transform p = FindByNameInactive(token);
            if (p != null) parts.Add(p);
        }
        Transform l5Visual = ReplaceInletHandwheelVisualWithL5Model(hub, parts.ToArray());
        if (l5Visual != null)
        {
            parts.Clear();
            parts.Add(hub);
            parts.Add(l5Visual);
        }
        _handwheelParts = parts.ToArray();

        // BARU: putaran PERSIS seperti Level 8 Flash Vessel (GesturalHandwheel di hub).
        _inletGH = hub.GetComponent<GesturalHandwheel>();
        if (_inletGH == null) _inletGH = hub.gameObject.AddComponent<GesturalHandwheel>();
        _inletGH.fullOpenDegrees = _inletValveFullOpenDegrees;
        _inletGH.Setup(hub, _handwheelParts);
    }

    private Transform ReplaceInletHandwheelVisualWithL5Model(Transform hub, Transform[] oldParts)
    {
        if (hub == null)
            return null;
        Transform existing = hub.Find("L7_L5_Condensate_Drain_Handwheel_StirRedesign_Runtime")
                          ?? hub.Find("L7_L5_Condensate_Drain_Handwheel_StirRedesign_Scene");
        if (existing != null)
            return existing;

        Transform source = FindByNameInactive("L5_Condensate_Drain_Handwheel_StirRedesign");
        if (source == null)
            source = FindByNameContainsInactive("L5_Condensate_Drain_Handwheel_StirRedesign");
        if (source == null)
            source = FindByNameContainsInactive("L5_Condensate_Drain_Handwheel");
        if (source == null)
        {
            SetRenderersEnabled(oldParts, true);
            return null;
        }

        GameObject clone = Instantiate(source.gameObject);
        clone.name = "L7_L5_Condensate_Drain_Handwheel_StirRedesign_Runtime";
        clone.transform.SetParent(hub, true);
        clone.transform.position = hub.position;
        clone.transform.rotation = hub.rotation;
        clone.transform.localScale = Vector3.one;
        clone.SetActive(true);

        foreach (var behaviour in clone.GetComponentsInChildren<MonoBehaviour>(true))
        {
            if (behaviour != null && behaviour.GetType() != typeof(GesturalHandwheel))
                behaviour.enabled = false;
        }
        foreach (var collider in clone.GetComponentsInChildren<Collider>(true))
            if (collider != null) Destroy(collider);
        foreach (var rb in clone.GetComponentsInChildren<Rigidbody>(true))
            if (rb != null) Destroy(rb);

        Renderer[] cloneRenderers = clone.GetComponentsInChildren<Renderer>(true);
        bool cloneVisible = false;
        for (int i = 0; i < cloneRenderers.Length; i++)
        {
            if (cloneRenderers[i] == null) continue;
            cloneRenderers[i].enabled = true;
            cloneVisible = true;
        }

        if (cloneVisible)
            SetRenderersEnabled(oldParts, false);
        else
            SetRenderersEnabled(oldParts, true);

        return clone.transform;
    }

    private void SetRenderersEnabled(Transform[] roots, bool enabled)
    {
        if (roots == null)
            return;
        for (int i = 0; i < roots.Length; i++)
        {
            if (roots[i] == null) continue;
            Renderer[] renderers = roots[i].GetComponentsInChildren<Renderer>(true);
            for (int r = 0; r < renderers.Length; r++)
                if (renderers[r] != null) renderers[r].enabled = enabled;
        }
    }

    /// <summary>
    /// Rotate pivot transform — HandwheelVirtualPivot di LateUpdate akan ikutin parts.
    /// </summary>
    private void RotateHandwheelParts(float totalDegrees)
    {
        if (_inletValvePivot == null) return;
        // Rotasi di local axis pivot (Y default = poros valve).
        Vector3 axis = Vector3.up; // local axis pivot
        _inletValvePivot.localRotation = _inletPivotBaseRotation * Quaternion.AngleAxis(totalDegrees, axis);
    }

    // Legacy method kept for backward compatibility (no-op now).
    private void RotateHandwheelParts_Legacy(float totalDegrees)
    {
        if (_handwheelParts == null || _handwheelParts.Length == 0) return;

        Quaternion deltaRot = Quaternion.AngleAxis(totalDegrees, _handwheelAxisWorld);
        for (int i = 0; i < _handwheelParts.Length; i++)
        {
            var part = _handwheelParts[i];
            if (part == null) continue;
            // Rotate part-nya sendiri pada axis world.
            part.rotation = deltaRot * _handwheelPartsBaseRotation[i];
            // Rotate posisi mengelilingi pivot.
            Vector3 offset = _handwheelPartsBasePosition[i] - _handwheelPivotWorld;
            offset = deltaRot * offset;
            part.position = _handwheelPivotWorld + offset;
        }

        // Sync visual pivot biar XR grab tetap di tengah handwheel.
        if (_inletValvePivot != null)
        {
            _inletValvePivot.position = _handwheelPivotWorld;
            _inletValvePivot.rotation = deltaRot * _inletPivotBaseRotation;
        }
    }

    private void EnsureInletValveInteractable()
    {
        if (_inletValvePivot == null) return;

        // Mekanisme BARU (seperti FV1_To_FV2_..._BypassHandwheel): XRSimpleInteractable supaya
        // handwheel hanya BERPUTAR di tempat (tidak ketarik mengikuti tangan); hover & grab sama-sama memutar.
        var oldGrab = _inletValvePivot.GetComponent<XRGrabInteractable>();
        if (oldGrab != null) Destroy(oldGrab);
        var oldRb = _inletValvePivot.GetComponent<Rigidbody>();
        if (oldRb != null) Destroy(oldRb);
        _inletValveGrab = null;

        var valveCollider = _inletValvePivot.GetComponent<SphereCollider>();
        if (valveCollider == null) valveCollider = _inletValvePivot.gameObject.AddComponent<SphereCollider>();
        valveCollider.radius = LocalRadiusForWorld(_inletValvePivot, 0.7f);
        valveCollider.isTrigger = false;
        foreach (var c in _inletValvePivot.GetComponentsInChildren<Collider>(true))
            if (c != null) c.enabled = true;

        var simple = _inletValvePivot.GetComponent<XRSimpleInteractable>();
        if (simple == null) simple = _inletValvePivot.gameObject.AddComponent<XRSimpleInteractable>();
        // Daftarkan collider EKSPLISIT (kalau ditambah via script, list interactable kosong -> gak bisa di-select).
        simple.colliders.Clear();
        foreach (var c in _inletValvePivot.GetComponents<Collider>())
            if (c != null) simple.colliders.Add(c);
        simple.enabled = false; simple.enabled = true;

        simple.selectEntered.RemoveAllListeners();
        simple.selectExited.RemoveAllListeners();
        simple.hoverEntered.RemoveAllListeners();
        simple.hoverExited.RemoveAllListeners();
        simple.selectEntered.AddListener(OnInletGrabbed);
        simple.selectExited.AddListener(OnInletReleased);
        simple.hoverEntered.AddListener((a) => { _inletValveHover = true; _inletInteractorAttach = a.interactorObject != null ? a.interactorObject.transform : _inletInteractorAttach; });
        simple.hoverExited.AddListener((a) => { _inletValveHover = false; _inletYawValid = false; });
    }

    private void OnInletGrabbed(UnityEngine.XR.Interaction.Toolkit.SelectEnterEventArgs args)
    {
        _inletValveGrabbed = true;
        _inletInteractorAttach = args.interactorObject != null ? args.interactorObject.transform : null;
        _inletYawValid = false;
        // Saat player mulai grab → lampu MERAH ON (sedang diputar).
        SetStatusLamp(false);
    }

    private void OnInletReleased(UnityEngine.XR.Interaction.Toolkit.SelectExitEventArgs args)
    {
        _inletValveGrabbed = false;
        _inletInteractorAttach = null;
        _inletYawValid = false;
    }

    private bool TrackInletValveRotation()
    {
        if (_inletValvePivot == null || _inletInteractorAttach == null) return false;
        Vector3 axisWorld = _inletValvePivot.parent != null
            ? _inletValvePivot.parent.TransformDirection(_inletValveAxisLocal).normalized
            : _inletValvePivot.TransformDirection(_inletValveAxisLocal).normalized;
        if (axisWorld.sqrMagnitude < 0.001f) axisWorld = Vector3.up;

        // BARU: ikut TWIST tangan pemain (controller.up diproyeksikan ke bidang disc), bukan .forward.
        Vector3 handVec = _inletInteractorAttach.up;
        Vector3 projected = Vector3.ProjectOnPlane(handVec, axisWorld);
        if (projected.sqrMagnitude < 0.01f) { handVec = _inletInteractorAttach.right; projected = Vector3.ProjectOnPlane(handVec, axisWorld); }
        if (projected.sqrMagnitude < 0.0001f) return false;
        projected.Normalize();
        Vector3 reference = Vector3.ProjectOnPlane(Vector3.up, axisWorld);
        if (reference.sqrMagnitude < 0.0001f) reference = Vector3.ProjectOnPlane(Vector3.right, axisWorld);
        reference.Normalize();
        float yaw = Vector3.SignedAngle(reference, projected, axisWorld);
        if (!_inletYawValid) { _inletYawLast = yaw; _inletYawValid = true; return false; }
        float dYaw = Mathf.DeltaAngle(_inletYawLast, yaw);
        _inletYawLast = yaw;
        if (Mathf.Abs(dYaw) > 35f) dYaw = 0f;
        float delta = dYaw * 5f; // gesturalGain: gerakan kecil tangan -> putaran besar.
        if (Mathf.Abs(delta) < 0.0001f) return false;
        _inletValveDegrees = Mathf.Clamp(_inletValveDegrees + delta, 0f, _inletValveFullOpenDegrees);
        return true;
    }

    private bool SimulateValveKeyboard()
    {
        float delta = 0f;
        if (Input.GetKey(KeyCode.R)) delta += 240f * Time.deltaTime;
        if (Input.GetKey(KeyCode.F)) delta -= 240f * Time.deltaTime;
        if (Mathf.Abs(delta) < 0.001f) return false;
        _inletValveDegrees = Mathf.Clamp(_inletValveDegrees + delta, 0f, _inletValveFullOpenDegrees);
        return true;
    }

    private void UpdateInletValveVisuals()
    {
        _inletValveOpenPercent = Mathf.Clamp01(_inletValveDegrees / _inletValveFullOpenDegrees);
        // Rotate semua handwheel parts mengelilingi pivot pada sumbu poros.
        RotateHandwheelParts(_inletValveDegrees);
    }

    // ============================================================
    //  AGITATOR ANIMATION
    // ============================================================

    private void AnimateAgitator()
    {
        // Tidak running = stop total (mesin mati saat awal level).
        if (!_agitatorRunning) return;
        if (_agitatorShafts == null || _agitatorShafts.Length == 0) return;

        // Akumulasi sudut (seperti preheater Level 5). Direction: -1 = kiri (CCW), +1 = kanan.
        float degPerSec = _agitatorCurrentRPM * 6f;
        float sign = Mathf.Sign(_agitatorDirection != 0f ? _agitatorDirection : -1f);
        _agitatorAngle += degPerSec * sign * Time.deltaTime;
        // Wrap supaya tidak overflow.
        if (_agitatorAngle > 360f) _agitatorAngle -= 360f;
        else if (_agitatorAngle < -360f) _agitatorAngle += 360f;

        // Pakai pola preheater: localRotation = base * AngleAxis(angle, localAxis).
        // Karena rotor sudah grouped (parent transform), rotate parent di local Y (yang = world Y up
        // untuk autoclave). EnsureAgitatorBaseRotation cache base rotation sekali.
        EnsureAgitatorBaseRotations();
        for (int i = 0; i < _agitatorShafts.Length; i++)
        {
            var rotor = _agitatorShafts[i];
            if (rotor == null) continue;
            if (i >= _agitatorBaseRotations.Length) continue;
            // Rotate di sumbu Y dunia: konversi ke local axis rotor.
            Vector3 localYaxis = rotor.parent != null
                ? rotor.parent.InverseTransformDirection(Vector3.up).normalized
                : Vector3.up;
            rotor.localRotation = _agitatorBaseRotations[i] * Quaternion.AngleAxis(_agitatorAngle, localYaxis);
        }
    }

    private Quaternion[] _agitatorBaseRotations;

    private void EnsureAgitatorBaseRotations()
    {
        if (_agitatorBaseRotations != null && _agitatorBaseRotations.Length == _agitatorShafts.Length) return;
        _agitatorBaseRotations = new Quaternion[_agitatorShafts.Length];
        for (int i = 0; i < _agitatorShafts.Length; i++)
        {
            _agitatorBaseRotations[i] = _agitatorShafts[i] != null
                ? _agitatorShafts[i].localRotation
                : Quaternion.identity;
        }
    }

    // Cache shaft transform untuk setiap rotor (avoid per-frame Find).
    private System.Collections.Generic.Dictionary<int, Transform> _shaftCenterCache
        = new System.Collections.Generic.Dictionary<int, Transform>();

    private Transform GetCachedShaftCenter(Transform rotor)
    {
        int id = rotor.GetInstanceID();
        if (_shaftCenterCache.TryGetValue(id, out var cached) && cached != null) return cached;

        // Cari child yang nama-nya mengandung "VerticalShaft" atau "_Shaft".
        Transform shaft = null;
        for (int j = 0; j < rotor.childCount; j++)
        {
            var c = rotor.GetChild(j);
            if (c.name.Contains("VerticalShaft") || c.name.EndsWith("_Shaft"))
            {
                shaft = c;
                break;
            }
        }
        if (shaft != null) _shaftCenterCache[id] = shaft;
        return shaft;
    }

    // ============================================================
    //  STATUS LAMP CONTROL (red/green indicator on autoclave panel)
    // ============================================================

    /// <summary>
    /// Set status indicator. red=true → lampu merah ON (proses memutar valve).
    /// red=false → lampu hijau ON (valve full open / sukses).
    /// </summary>
    private void SetStatusLamp(bool greenOn)
    {
        if (_redLamp != null)
        {
            ApplyLampColor(_redLamp, greenOn ? _lampOffColor : _redLampColor, !greenOn);
        }
        if (_greenLamp != null)
        {
            ApplyLampColor(_greenLamp, greenOn ? _greenLampColor : _lampOffColor, greenOn);
        }
    }

    private void ApplyLampColor(Renderer r, Color col, bool emissive)
    {
        if (r == null) return;
        // Pakai material instance supaya tidak kena UV atlas yang shared.
        if (r.sharedMaterial != null && r.sharedMaterial.shader != null)
        {
            // Buat instance khusus untuk lamp ini sekali.
            var key = "_LampInstanceKey_" + r.GetInstanceID();
            if (r.material == r.sharedMaterial || !r.material.name.Contains("Lamp_Instance"))
            {
                var src = r.sharedMaterial;
                Shader sh = src.shader;
                var matNew = new Material(sh) { name = "M_L7_Lamp_Instance_" + r.gameObject.name };
                if (src.HasProperty("_BaseColor")) matNew.SetColor("_BaseColor", src.GetColor("_BaseColor"));
                if (src.HasProperty("_Color")) matNew.SetColor("_Color", src.GetColor("_Color"));
                r.material = matNew;
            }
        }
        var mat = r.material;
        if (mat.HasProperty("_BaseColor")) mat.SetColor("_BaseColor", col);
        if (mat.HasProperty("_Color")) mat.SetColor("_Color", col);
        if (emissive)
        {
            mat.EnableKeyword("_EMISSION");
            if (mat.HasProperty("_EmissionColor")) mat.SetColor("_EmissionColor", col * 2.5f);
        }
        else
        {
            if (mat.HasProperty("_EmissionColor")) mat.SetColor("_EmissionColor", Color.black);
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
        if (_teleportTargetAutoclave == null)
        {
            GameObject sp = GameObject.Find("SpawnPoint_Lvl7");
            if (sp != null) _teleportTargetAutoclave = sp.transform;
        }
        if (_teleportTargetTopDeck == null)
        {
            GameObject sp2 = GameObject.Find("SpawnPoint_Lvl7_TopDeck");
            if (sp2 != null) _teleportTargetTopDeck = sp2.transform;
        }
        if (_innerSlurrySurface == null)
            _innerSlurrySurface = FindByNameInactive("L7_XRay_InnerSlurry_Surface");
        if (_autoclaveField == null)
        {
            GameObject af = GameObject.Find("Autoclave_Field");
            if (af != null) _autoclaveField = af;
        }
        if (_shellRenderers == null || _shellRenderers.Count == 0)
        {
            _shellRenderers = new List<Renderer>();
            string[] shellTokens = {
                "L7_Autoclave_PressureShell",
                "L7_Autoclave_EndCap_Left",
                "L7_Autoclave_EndCap_Right",
                "L7_Autoclave_Left_HeavyFlange",
                "L7_Autoclave_Right_HeavyFlange",
                "L7_Autoclave_Top_Longitudinal_Seam",
                "L7_Autoclave_Dark_StiffenerBand_",
                "Autoclave_Shell_Band_"
            };
            Transform[] all = FindObjectsByType<Transform>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            foreach (Transform t in all)
            {
                foreach (var token in shellTokens)
                {
                    if (t.name.IndexOf(token, System.StringComparison.OrdinalIgnoreCase) >= 0)
                    {
                        Renderer r = t.GetComponent<Renderer>();
                        if (r != null && !_shellRenderers.Contains(r)) _shellRenderers.Add(r);
                        break;
                    }
                }
            }
        }

        // Auto-find status indicator lamps.
        if (_redLamp == null)
        {
            Transform t = FindByNameInactive("L7_Local_Control_EStop");
            if (t != null) _redLamp = t.GetComponent<Renderer>();
        }
        if (_greenLamp == null)
        {
            Transform t = FindByNameInactive("L7_Local_Control_RunLamp");
            if (t != null) _greenLamp = t.GetComponent<Renderer>();
        }

        // Auto-find agitator rotors (5 buah: 00..04).
        if (_agitatorShafts == null || _agitatorShafts.Length == 0)
        {
            var rotors = new List<Transform>();
            for (int i = 0; i < 8; i++)
            {
                Transform rot = FindByNameInactive($"L7_XRay_AgitatorRotor_{i:00}");
                if (rot != null) rotors.Add(rot);
            }
            _agitatorShafts = rotors.ToArray();
        }
    }

    private Transform FindByNameInactive(string name)
    {
        foreach (Transform t in FindObjectsByType<Transform>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            if (t != null && t.name == name && t.gameObject.scene.IsValid()) return t;
        return null;
    }

    private Transform FindByNameContainsInactive(string token)
    {
        foreach (Transform t in FindObjectsByType<Transform>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            if (t != null && t.gameObject.scene.IsValid() && t.name.IndexOf(token, System.StringComparison.OrdinalIgnoreCase) >= 0)
                return t;
        return null;
    }

    private void TeleportPlayerToAutoclave()
    {
        TeleportToTransform(_teleportTargetAutoclave);
    }

    private void TeleportPlayerToTopDeck()
    {
        TeleportToTransform(_teleportTargetTopDeck);
    }

    private void TeleportToTransform(Transform target)
    {
        if (_playerRigRoot == null || target == null) return;
        var cc = _playerRigRoot.GetComponent<CharacterController>();
        bool ccOn = cc != null && cc.enabled;
        if (ccOn) cc.enabled = false;
        var origin = _playerRigRoot.GetComponent<XROrigin>();
        if (origin != null)
        {
            origin.MoveCameraToWorldLocation(target.position);
            origin.MatchOriginUpCameraForward(Vector3.up, target.forward);
        }
        _playerRigRoot.SetPositionAndRotation(target.position, target.rotation);
        if (ccOn) cc.enabled = true;
    }

    // ============================================================
    //  AUDIO
    // ============================================================

    private void StartReactorAudio()
    {
        if (_reactorHumAudio == null)
        {
            var go = new GameObject("L7_ReactorHum_Audio");
            go.transform.SetParent(transform, false);
            _reactorHumAudio = go.AddComponent<AudioSource>();
            _reactorHumAudio.loop = true;
            _reactorHumAudio.spatialBlend = 0.4f;
            _reactorHumAudio.clip = GenerateHum("L7Hum", 4f, 22050);
        }
        _reactorHumAudio.volume = _reactorHumVolume;
        if (!_reactorHumAudio.isPlaying) _reactorHumAudio.Play();
    }

    private void EnsureSlurryFlowAudio()
    {
        if (_slurryFlowAudio != null) return;
        var go = new GameObject("L7_SlurryFlow_Audio");
        go.transform.SetParent(transform, false);
        _slurryFlowAudio = go.AddComponent<AudioSource>();
        _slurryFlowAudio.loop = true;
        _slurryFlowAudio.spatialBlend = 0.45f;
        _slurryFlowAudio.clip = GenerateNoise("L7Flow", 4f, 22050, 0.35f);
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

    private AudioClip GenerateHum(string name, float duration, int sampleRate)
    {
        int total = Mathf.CeilToInt(duration * sampleRate);
        float[] data = new float[total];
        float p1 = 0f, p2 = 0f;
        for (int i = 0; i < total; i++)
        {
            p1 += 2f * Mathf.PI * 50f / sampleRate;
            p2 += 2f * Mathf.PI * 100f / sampleRate;
            data[i] = (Mathf.Sin(p1) * 0.6f + Mathf.Sin(p2) * 0.3f) * 0.3f;
        }
        var clip = AudioClip.Create(name, total, 1, sampleRate, false);
        clip.SetData(data, 0);
        return clip;
    }

    private AudioClip GenerateNoise(string name, float duration, int sampleRate, float gain)
    {
        int total = Mathf.CeilToInt(duration * sampleRate);
        float[] data = new float[total];
        var rnd = new System.Random(name.GetHashCode());
        float lp = 0f;
        for (int i = 0; i < total; i++)
        {
            float n = ((float)rnd.NextDouble() - 0.5f) * 2f;
            lp += 0.08f * (n - lp);
            data[i] = lp * gain;
        }
        var clip = AudioClip.Create(name, total, 1, sampleRate, false);
        clip.SetData(data, 0);
        return clip;
    }

    private AudioClip GenerateSiren(string name, float duration, int sampleRate)
    {
        // Klasik siren: frekuensi sweep 600 Hz <-> 900 Hz, sinusoidal modulation 1.5 Hz.
        int total = Mathf.CeilToInt(duration * sampleRate);
        float[] data = new float[total];
        float phase = 0f;
        for (int i = 0; i < total; i++)
        {
            float t = (float)i / sampleRate;
            float freq = 750f + 150f * Mathf.Sin(2f * Mathf.PI * 1.5f * t);
            phase += 2f * Mathf.PI * freq / sampleRate;
            float wave = Mathf.Sin(phase) * 0.4f;
            // Add second harmonic for richer texture.
            wave += Mathf.Sin(phase * 2f) * 0.15f;
            data[i] = wave;
        }
        var clip = AudioClip.Create(name, total, 1, sampleRate, false);
        clip.SetData(data, 0);
        return clip;
    }

    private AudioClip GenerateEngineStart(string name, float duration, int sampleRate)
    {
        // Engine ignition: low rumble starting slow, accelerating pitch.
        int total = Mathf.CeilToInt(duration * sampleRate);
        float[] data = new float[total];
        float phase = 0f;
        var rnd = new System.Random(name.GetHashCode());
        float lp = 0f;
        for (int i = 0; i < total; i++)
        {
            float t = (float)i / sampleRate;
            // Pitch ramp dari 35 Hz ke 90 Hz.
            float freq = Mathf.Lerp(35f, 90f, Mathf.Clamp01(t / duration));
            phase += 2f * Mathf.PI * freq / sampleRate;
            float wave = Mathf.Sin(phase) * 0.5f;
            // Add noise overlay
            float n = ((float)rnd.NextDouble() - 0.5f) * 2f;
            lp += 0.1f * (n - lp);
            wave += lp * 0.25f;
            data[i] = wave * 0.6f;
        }
        var clip = AudioClip.Create(name, total, 1, sampleRate, false);
        clip.SetData(data, 0);
        return clip;
    }

    private void EnsureSirenAudio()
    {
        if (_sirenAudio != null) return;
        var go = new GameObject("L7_Siren_Audio");
        go.transform.SetParent(transform, false);
        _sirenAudio = go.AddComponent<AudioSource>();
        _sirenAudio.loop = true;
        _sirenAudio.spatialBlend = 0.3f;
        _sirenAudio.clip = GenerateSiren("L7Siren", 4f, 22050);
    }

    private void EnsureEngineStartAudio()
    {
        if (_engineStartAudio != null) return;
        var go = new GameObject("L7_EngineStart_Audio");
        go.transform.SetParent(transform, false);
        _engineStartAudio = go.AddComponent<AudioSource>();
        _engineStartAudio.loop = false;
        _engineStartAudio.spatialBlend = 0.5f;
        _engineStartAudio.clip = GenerateEngineStart("L7Engine", 5f, 22050);
    }

    // ============================================================
    //  STATE RESET
    // ============================================================

    private void ResetState()
    {
        _xrayActive = false;
        _slurryFillStarted = false;
        _slurryFillProgress = 0f;
        _inletValveDegrees = 0f;
        _inletValveOpenPercent = 0f;
        _inletValveGrabbed = false;
        _inletYawValid = false;
        _agitatorAngle = 0f;
        _agitatorRunning = false;
        _agitatorCurrentRPM = 0f;
        _safetyDrillStep = 0;
        _swirlActive = false;
        if (_missionCompleteCanvas != null) _missionCompleteCanvas.SetActive(false);
        ResetVisuals();
    }

    private void ResetVisuals()
    {
        RestoreShellMaterials();
        SetXRayObjectsVisible(false);
        UpdateSlurryShader(0f);
        UpdateInletValveVisuals();
        // Lampu off saat reset state.
        if (_redLamp != null) ApplyLampColor(_redLamp, _lampOffColor, false);
        if (_greenLamp != null) ApplyLampColor(_greenLamp, _lampOffColor, false);
    }

    private void StartSequence(IEnumerator routine)
    {
        if (_seqCoroutine != null) StopCoroutine(_seqCoroutine);
        _seqCoroutine = StartCoroutine(routine);
    }

    // ============================================================
    //  ACID DROP ANIMATION (asam sulfat dari atas — cylinder mesh + UV scroll)
    // ============================================================

    private GameObject _acidDropObj;
    private Material _acidMaterial;
    private Renderer _acidStreamRenderer;
    private GameObject _acidSplashObj;
    private ParticleSystem _acidSplashParticle;

    private void EnsureAcidDropEffect()
    {
        if (_acidDropObj != null) return;

        // Buat cylinder primitive sebagai stream liquid yang panjangnya bisa di-animate.
        _acidDropObj = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        _acidDropObj.name = "L7_AcidStream_Cylinder_Runtime";
        // Hapus default collider, kita tidak butuh collision.
        var col = _acidDropObj.GetComponent<Collider>();
        if (col != null) Destroy(col);
        _acidDropObj.transform.SetParent(transform, false);

        _acidStreamRenderer = _acidDropObj.GetComponent<Renderer>();

        // Material kuning asam dengan emission + URP transparent.
        Shader shader = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard");
        _acidMaterial = new Material(shader) { name = "M_L7_AcidStream_Runtime" };
        if (_acidMaterial.HasProperty("_BaseColor")) _acidMaterial.SetColor("_BaseColor", _acidColor);
        if (_acidMaterial.HasProperty("_Color")) _acidMaterial.SetColor("_Color", _acidColor);
        if (_acidMaterial.HasProperty("_Smoothness")) _acidMaterial.SetFloat("_Smoothness", 0.85f);
        _acidMaterial.EnableKeyword("_EMISSION");
        if (_acidMaterial.HasProperty("_EmissionColor"))
            _acidMaterial.SetColor("_EmissionColor", _acidColor * 1.5f);
        _acidStreamRenderer.sharedMaterial = _acidMaterial;
        _acidDropObj.SetActive(false);

        // Splash particle system di bawah (saat acid hits surface).
        _acidSplashObj = new GameObject("L7_AcidSplash_Runtime");
        _acidSplashObj.transform.SetParent(transform, false);
        _acidSplashParticle = _acidSplashObj.AddComponent<ParticleSystem>();
        var main = _acidSplashParticle.main;
        main.duration = 0.5f;
        main.loop = true;
        main.startLifetime = 0.6f;
        main.startSpeed = 1.5f;
        main.startSize = 0.08f;
        main.startColor = _acidColor;
        main.maxParticles = 120;
        main.simulationSpace = ParticleSystemSimulationSpace.World;
        main.gravityModifier = 0.4f;
        var emission = _acidSplashParticle.emission;
        emission.rateOverTime = 80f;
        var shape = _acidSplashParticle.shape;
        shape.shapeType = ParticleSystemShapeType.Cone;
        shape.angle = 35f;
        shape.radius = 0.05f;
        var renderer = _acidSplashParticle.GetComponent<ParticleSystemRenderer>();
        renderer.material = _acidMaterial;
        _acidSplashParticle.Stop();
    }

    private IEnumerator AcidDropCoroutine()
    {
        if (_acidDropObj == null) yield break;

        // Anchor titik atas: outlet pipa kuning (Pipe_Autoclave_AcidInject_TopNozzle bottom).
        Vector3 acidTopAnchor = new Vector3(-10.43f, 10.5f, 85.66f);
        Transform topNozzle = FindByNameInactive("Pipe_Autoclave_AcidInject_TopNozzle");
        if (topNozzle != null)
        {
            var nozzleR = topNozzle.GetComponent<Renderer>();
            // Outlet = bottom dari nozzle bounds.
            if (nozzleR != null)
                acidTopAnchor = new Vector3(nozzleR.bounds.center.x, nozzleR.bounds.min.y, nozzleR.bounds.center.z);
            else
                acidTopAnchor = topNozzle.position + Vector3.down * 0.5f;
        }

        // Anchor titik bawah: permukaan slurry saat ini.
        float currentSurfaceY = Mathf.Lerp(_slurryYBottom, _slurryYTop, _slurryFillProgress);
        Vector3 acidBottom = new Vector3(acidTopAnchor.x, currentSurfaceY + 0.1f, acidTopAnchor.z);

        // Setup splash particle di posisi bottom.
        if (_acidSplashObj != null)
        {
            _acidSplashObj.transform.position = acidBottom;
            _acidSplashObj.transform.rotation = Quaternion.LookRotation(Vector3.up); // shoot upward sedikit
        }

        _acidDropObj.SetActive(true);
        float duration = _acidDropDuration;
        float halfPhase = duration * 0.25f;
        float fullLen = Vector3.Distance(acidTopAnchor, acidBottom);
        float radius = _acidStreamWidth * 0.5f;

        // Phase 1: stream extending dari top ke bottom (0 → full length)
        float t = 0f;
        while (t < halfPhase)
        {
            t += Time.deltaTime;
            float p = Mathf.Clamp01(t / halfPhase);
            float currentLen = fullLen * p;
            Vector3 currentBottom = Vector3.Lerp(acidTopAnchor, acidBottom, p);
            UpdateAcidCylinder(acidTopAnchor, currentBottom, currentLen, radius);
            // Emission flicker
            if (_acidMaterial.HasProperty("_EmissionColor"))
                _acidMaterial.SetColor("_EmissionColor", _acidColor * (1.5f + 0.5f * Mathf.Sin(t * 12f)));
            yield return null;
        }

        // Start splash saat phase 2 mulai (acid sudah mencapai permukaan).
        if (_acidSplashParticle != null) _acidSplashParticle.Play();

        // Phase 2: hold full stream + UV scroll downward + flicker
        float holdT = 0f;
        float holdDur = duration - 2f * halfPhase;
        while (holdT < holdDur)
        {
            holdT += Time.deltaTime;
            UpdateAcidCylinder(acidTopAnchor, acidBottom, fullLen, radius);
            // UV scroll: offset Y ke bawah secara konstan untuk efek liquid mengalir.
            if (_acidMaterial.HasProperty("_BaseMap_ST"))
            {
                Vector2 offset = new Vector2(0f, -Time.time * 2f);
                _acidMaterial.SetTextureOffset("_BaseMap", offset);
            }
            if (_acidMaterial.HasProperty("_EmissionColor"))
                _acidMaterial.SetColor("_EmissionColor", _acidColor * (1.5f + 0.5f * Mathf.Sin(Time.time * 12f)));
            yield return null;
        }

        // Phase 3: shrink dari atas (acid mulai habis dari nozzle)
        float t3 = 0f;
        while (t3 < halfPhase)
        {
            t3 += Time.deltaTime;
            float p = Mathf.Clamp01(t3 / halfPhase);
            Vector3 currentTop = Vector3.Lerp(acidTopAnchor, acidBottom, p);
            float currentLen = fullLen * (1f - p);
            UpdateAcidCylinder(currentTop, acidBottom, currentLen, radius);
            yield return null;
        }

        _acidDropObj.SetActive(false);
        if (_acidSplashParticle != null) _acidSplashParticle.Stop();
    }

    /// <summary>
    /// Update transform cylinder primitive supaya menjangkau dari startPos ke endPos dengan length & radius.
    /// </summary>
    private void UpdateAcidCylinder(Vector3 startPos, Vector3 endPos, float length, float radius)
    {
        if (_acidDropObj == null) return;
        Vector3 mid = (startPos + endPos) * 0.5f;
        Vector3 dir = (endPos - startPos).normalized;
        if (dir.sqrMagnitude < 0.001f) dir = Vector3.down;
        _acidDropObj.transform.position = mid;
        // Cylinder primitive default tinggi 2m di sumbu Y. Scale Y untuk match length.
        _acidDropObj.transform.up = dir;
        // Cylinder default Y axis = up (length 2 saat scale 1). Jadi scale Y = length / 2.
        _acidDropObj.transform.localScale = new Vector3(radius * 2f, length * 0.5f, radius * 2f);
    }

    // ============================================================
    //  LIQUID SWIRL VFX (post-fill, mengikuti rotor)
    // ============================================================

    private bool _swirlActive;
    private float _swirlAngle;

    private void StartLiquidSwirl()
    {
        _swirlActive = true;
        _swirlAngle = 0f;
        if (_slurryFillMaterial != null)
        {
            // Naikkan emission saat swirl aktif untuk efek "fluid hot mixing"
            if (_slurryFillMaterial.HasProperty("_EmissionIntensity"))
                _slurryFillMaterial.SetFloat("_EmissionIntensity", 1.0f);
        }
    }

    private void UpdateLiquidSwirl()
    {
        if (_innerSlurrySurface == null || _slurryFillMaterial == null) return;
        // Rotate slurry mesh perlahan di sumbu Y dunia (mengikuti rotor swirl)
        _swirlAngle += _swirlRPM * 6f * Time.deltaTime;
        // Alih-alih rotate mesh (karena bisa keluar dari shell), kita modulasi emission warna untuk efek swirl ringan.
        // Warna swirl tetap di range air biru-teal (bukan ungu) supaya cairan terlihat seperti air realistic.
        float pulseR = 0.10f + 0.06f * Mathf.Sin(_swirlAngle * Mathf.Deg2Rad);
        float pulseG = 0.42f + 0.08f * Mathf.Sin(_swirlAngle * Mathf.Deg2Rad * 0.7f);
        float pulseB = 0.55f + 0.10f * Mathf.Cos(_swirlAngle * Mathf.Deg2Rad * 1.3f);
        if (_slurryFillMaterial.HasProperty("_EmissionColor"))
            _slurryFillMaterial.SetColor("_EmissionColor", new Color(pulseR, pulseG, pulseB) * _swirlIntensity);
    }

    // ============================================================
    //  PUBLIC (untuk debug)
    // ============================================================

    public bool XRayActive => _xrayActive;
    public float SlurryFillProgress => _slurryFillProgress;
    public bool LevelActive => _levelActive;

    [ContextMenu("Debug: Force Activate Level7")]
    public void DebugForceActivate() => ActivateLevel();

    [ContextMenu("Debug: Set Slurry 50%")]
    public void DebugSlurryHalf()
    {
        _slurryFillProgress = 0.5f;
        UpdateSlurryShader(0.5f);
    }

    [ContextMenu("Debug: Set Slurry 100%")]
    public void DebugSlurryFull()
    {
        _slurryFillProgress = 1f;
        UpdateSlurryShader(1f);
    }

    private static float LocalRadiusForWorld(Transform t, float worldRadius)
    {
        Vector3 s = t != null ? t.lossyScale : Vector3.one;
        float maxAxis = Mathf.Max(Mathf.Abs(s.x), Mathf.Abs(s.y), Mathf.Abs(s.z), 0.0001f);
        return worldRadius / maxAxis;
    }
}
