using System.Collections;
using TMPro;
using Unity.XR.CoreUtils;
using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit.Interactables;

/// <summary>
/// Level 6: pre-heater outlet route, slurry into autoclave, acid dosing setup, field acid verification.
/// </summary>
public class Level6AcidInjectionController : MonoBehaviour
{
    private enum Phase
    {
        Idle,
        MenungguDcsStart,
        MenungguLaporanOutlet,
        TeleportKeValveSlurry,
        BukaValveSlurry,
        SlurryMasukAutoclave,
        MenungguLaporanSlurry,
        KembaliKeDcsAcid,
        DcsAcidSetup,
        TeleportKeAcidSkid,
        BukaValveAcid,
        TekanLocalStart,
        LeakInspection,
        AcidMengalir,
        MenungguLaporanAkhir,
        Selesai
    }

    [Header("=== Player / Teleport ===")]
    [SerializeField] private Transform _playerRigRoot;
    [SerializeField] private Transform _teleportTargetDcs;
    [SerializeField] private Transform _teleportTargetSlurryValve;
    [SerializeField] private Transform _teleportTargetAcidSkid;
    [SerializeField] private float _durasiFade = 3.2f;

    [Header("=== Slurry Transfer Valve ===")]
    [SerializeField] private Transform _slurryValveWheel;
    [SerializeField] private XRGrabInteractable _slurryValveGrab;
    [SerializeField] private Vector3 _slurryValveAxisLocal = Vector3.up;
    [SerializeField] private Transform _slurryGaugeNeedle;
    [SerializeField] private Renderer _slurryLampRed;
    [SerializeField] private Renderer _slurryLampGreen;
    [SerializeField] private float _slurryFullOpenDegrees = 1080f;
    [SerializeField] private bool _slurryHandTrackingAlwaysOn = false;

    [Header("=== Acid Field Verification ===")]
    [SerializeField] private Transform _acidValveWheel;
    [SerializeField] private XRGrabInteractable _acidValveGrab;
    [SerializeField] private Vector3 _acidValveAxisLocal = Vector3.up;
    [SerializeField] private Transform _acidGaugeNeedle;
    [SerializeField] private Renderer _acidLampRed;
    [SerializeField] private Renderer _acidLampGreen;
    [SerializeField] private float _acidFullOpenDegrees = 720f;

    [Header("=== DCS Acid Controls ===")]
    [SerializeField] private XRSimpleInteractable _btnAcidPlus;
    [SerializeField] private XRSimpleInteractable _btnAcidMinus;
    [SerializeField] private XRSimpleInteractable _btnAcidStrokePlus;
    [SerializeField] private XRSimpleInteractable _btnAcidStrokeMinus;
    [SerializeField] private XRSimpleInteractable _btnAcidTankSelect;
    [SerializeField] private XRSimpleInteractable _btnAcidArm;
    [SerializeField] private TMPro.TMP_Text _displayAcidRatio;
    [SerializeField] private TMPro.TMP_Text _displayPH;
    [SerializeField] private TMPro.TMP_Text _displayStatus;
    [SerializeField] private TMPro.TMP_Text _displayStrokePercent;
    [SerializeField] private TMPro.TMP_Text _displayTankSelected;
    [SerializeField] private TMPro.TMP_Text _displayArmStatus;
    [SerializeField] private float _acidRatioTarget = 350f;
    [SerializeField] private float _acidRatioTolerance = 10f;
    [SerializeField] private float _acidRatioMax = 500f;
    [SerializeField] private float _acidStepPerClick = 10f;
    [SerializeField] private float _strokePercentTarget = 70f;
    [SerializeField] private float _strokePercentTolerance = 5f;
    [SerializeField] private float _strokeStepPerClick = 5f;
    [SerializeField] private float _phStart = 5f;
    [SerializeField] private float _phTarget = 1f;

    [Header("=== Field Acid Skid ===")]
    [SerializeField] private XRSimpleInteractable _btnAcidLocalStart;
    [SerializeField] private XRSimpleInteractable _btnAcidLeakOk;
    [SerializeField] private Renderer _acidPumpRunningLamp;
    [SerializeField] private Transform _acidPumpRotor;
    [SerializeField] private float _leakInspectionDuration = 8f;

    [Header("=== Acid Field Calibration Column ===")]
    [Tooltip("GameObject Transparent_CalibrationColumn yang akan diisi cairan dari bawah ke atas saat acid mengalir.")]
    [SerializeField] private Transform _calibrationColumn;
    [SerializeField] private Transform _calibrationColumnLiquid;
    [SerializeField] private float _columnFillDuration = 14f;
    [SerializeField] private TMPro.TMP_Text _columnLevelLabel;
    [SerializeField] private Transform _calibrationColumnTopTick;

    [Header("=== Flow Visuals ===")]
    [SerializeField] private Transform _preheaterOutlet;
    [SerializeField] private Transform _autoclaveInlet;
    [SerializeField] private Transform _acidLineStart;
    [SerializeField] private Transform _acidLineEnd;
    [SerializeField] private GameObject _autoclaveLiquidObject;
    [SerializeField] private float _delaySetelahValveTerbuka = 2.5f;
    [SerializeField] private float _durasiSlurryFlow = 16f;
    [SerializeField] private float _durasiAutoclaveFill = 18f;
    [SerializeField] private float _durasiAcidFlow = 14f;
    [SerializeField] private float _durasiAcidPipeFlow = 30f;

    [Header("=== Audio ===")]
    [SerializeField] private AudioSource _flowAudio;
    [SerializeField] private AudioSource _acidPumpAudio;

    [Header("=== HUD ===")]
    [TextArea(2, 4)] [SerializeField] private string _msgStartDcs =
        "Level 6: Tekan tombol DCS 6 untuk authorize jalur pre-heater ke autoclave.";
    [TextArea(2, 4)] [SerializeField] private string _msgReportOutlet =
        "Lapor HT: 'Outlet pre-heater dibuka, segera salurkan ke autoclave'.";
    [TextArea(2, 4)] [SerializeField] private string _msgOpenSlurryValve =
        "Buka valve jalur slurry: dekatkan tangan ke ring setir, lalu gerakkan tangan kiri-kanan/melingkar. Setir akan mengikuti gerak tangan.";
    [TextArea(2, 4)] [SerializeField] private string _msgSlurryArrived =
        "Slurry panas sudah masuk autoclave. Lapor HT: 'slurry masuk autoclave'.";
    [TextArea(2, 4)] [SerializeField] private string _msgDcsAcid =
        "Aktifkan acid system di DCS: set rasio H2SO4 350 kg/ton dan stroke pompa metering 70%. Status akan ARMED otomatis saat semua parameter benar.";
    [TextArea(2, 4)] [SerializeField] private string _msgOpenAcidValve =
        "Buka isolation valve H2SO4: tahan grip + putar, atau tekan R (buka) / F (tutup) terus sampai lampu jalur hijau.";
    [TextArea(2, 4)] [SerializeField] private string _msgLocalStart =
        "Verifikasi skid asam di field. Jika area aman, lapor HT: 'field acid skid aman'.";
    [TextArea(2, 4)] [SerializeField] private string _msgLeakInspect =
        "Periksa flange & sparger. Jika tidak ada bocor, lapor HT untuk izinkan acid masuk autoclave.";
    [TextArea(2, 4)] [SerializeField] private string _msgFinalReport =
        "Acid injection aktif. Lapor HT: 'acid aktif, rasio 350 kilo, pH 1.0'.";

    private Phase _phase;
    private PlayerHUD _hud;
    private Material _redOnMat;
    private Material _redOffMat;
    private Material _greenOnMat;
    private Material _greenOffMat;
    private Material _slurryMat;
    private Material _acidMat;
    private GameObject _slurryFlowObject;
    private GameObject _acidFlowObject;
    private GameObject _acidFlowRunX;
    private GameObject _acidFlowRunZ;
    private Transform _acidPipeRunX;
    private Transform _acidPipeRunZ;
    private GameObject _runtimeRoot;
    private Coroutine _sequenceCoroutine;

    private float _acidRatioCurrent;
    private float _phCurrent;
    private float _strokePercentCurrent;
    private int _tankSelected; // 0 = A, 1 = B
    private bool _acidArmed;
    private float _slurryValveDegrees;
    private float _acidValveDegrees;
    private float _slurryOpenPercent;
    private float _acidOpenPercent;
    private bool _slurryArrivedAtAutoclave;
    private bool _acidDcsReady;
    private bool _acidLocalStarted;
    private bool _acidLeakInspectComplete;
    private bool _acidQuestComplete;
    private bool _slurryGrabbed;
    private bool _acidGrabbed;
    private bool _slurryYawValid;
    private bool _acidYawValid;
    private bool _acidButtonsWired;
    private bool _acidPlusWired;
    private bool _acidMinusWired;
    private bool _strokePlusWired;
    private bool _strokeMinusWired;
    private bool _tankSelectWired;
    private bool _acidArmWired;
    private bool _localStartWired;
    private bool _leakOkWired;
    private float _slurryYawLast;
    private float _acidYawLast;
    private float _leakInspectStartTime;
    private Transform _slurryInteractorAttach;
    private Transform _acidInteractorAttach;
    private Transform _leftHandTracker;
    private Transform _rightHandTracker;
    private Transform _activeSlurryHandTracker;
    private GesturalHandwheel _slurryValveHandwheel;
    private Quaternion _slurryWheelBase = Quaternion.identity;
    private Quaternion _acidWheelBase = Quaternion.identity;
    private Quaternion _slurryGaugeBase = Quaternion.identity;
    private Quaternion _acidGaugeBase = Quaternion.identity;

    private void Awake()
    {
        _hud = FindFirstObjectByType<PlayerHUD>();
        AutoFindReferences();
        EnsureRuntimeObjects();
        WireListeners();
        _phCurrent = _phStart;
        UpdateAcidDisplay();
    }

    private void OnEnable()
    {
        GameLevelManager.OnLevelStarted += OnLevelStarted;
        GameLevelManager.OnDCSButtonPressed += OnDcsButtonPressed;
        GameLevelManager.OnVoiceReportAccepted += OnVoiceReportAccepted;
    }

    private void OnDisable()
    {
        GameLevelManager.OnLevelStarted -= OnLevelStarted;
        GameLevelManager.OnDCSButtonPressed -= OnDcsButtonPressed;
        GameLevelManager.OnVoiceReportAccepted -= OnVoiceReportAccepted;
        if (_sequenceCoroutine != null) StopCoroutine(_sequenceCoroutine);
        StopAudio(_flowAudio);
        StopAudio(_acidPumpAudio);
    }

    private void OnLevelStarted(GameLevelManager.GameLevel level)
    {
        if (level != GameLevelManager.GameLevel.Level6_AcidInjection)
        {
            ResetVisualsOnly();
            _phase = Phase.Idle;
            return;
        }

        AutoFindReferences();
        EnsureRuntimeObjects();
        CaptureBaseRotations();
        ResetLevelState();
        TeleportPlayer(_teleportTargetDcs);
        _phase = Phase.MenungguDcsStart;
        _hud?.ShowNotifPublic(_msgStartDcs);
    }

    private void OnDcsButtonPressed(int nomorTombol)
    {
        if (!IsLevel6() || nomorTombol != 6 || _phase != Phase.MenungguDcsStart)
            return;

        _phase = Phase.MenungguLaporanOutlet;
        SetLampPair(_slurryLampRed, _slurryLampGreen, false);
        _hud?.ShowNotifPublic(_msgReportOutlet);
    }

    private void OnVoiceReportAccepted(string keyword)
    {
        if (!IsLevel6()) return;

        if (_phase == Phase.MenungguLaporanOutlet)
        {
            StartSequence(TeleportKeSlurryValveCoroutine());
            return;
        }

        if (_phase == Phase.MenungguLaporanSlurry)
        {
            StartSequence(KembaliKeDcsAcidCoroutine());
            return;
        }

        if (_phase == Phase.TekanLocalStart)
        {
            StartSequence(AcidFlowCoroutine());
            return;
        }
    }

    private void Update()
    {
        bool level6Active = IsLevel6();
        if (!level6Active)
        {
            if (_slurryHandTrackingAlwaysOn)
                HandleSlurryValveHandTracking(false);
            return;
        }

        if (_phase == Phase.BukaValveSlurry)
        {
            HandleSlurryValveHandTracking(true);
        }
        else
        {
            if (_slurryHandTrackingAlwaysOn)
                HandleSlurryValveHandTracking(false);
        }

        if (_phase == Phase.DcsAcidSetup)
        {
            SimulateAcidDcsKeyboard();
            CheckExternalAcidTarget();
        }
        else if (_phase == Phase.BukaValveAcid)
        {
            // Phase ini sudah tidak digunakan. Verifikasi acid skid sekarang lewat laporan HT.
            StartSequence(LocalStartAcidCoroutine());
        }
        else if (_phase == Phase.LeakInspection)
        {
            AnimateAcidPumpRotor();
        }
    }

    public void IncreaseAcidRatio()
    {
        if (!IsLevel6() || _phase != Phase.DcsAcidSetup) return;
        _acidRatioCurrent = Mathf.Clamp(_acidRatioCurrent + _acidStepPerClick, 0f, _acidRatioMax);
        OnAcidRatioChanged();
    }

    public void DecreaseAcidRatio()
    {
        if (!IsLevel6() || _phase != Phase.DcsAcidSetup) return;
        _acidRatioCurrent = Mathf.Clamp(_acidRatioCurrent - _acidStepPerClick, 0f, _acidRatioMax);
        OnAcidRatioChanged();
    }

    public void IncreaseAcidStroke()
    {
        if (!IsLevel6() || _phase != Phase.DcsAcidSetup) return;
        _strokePercentCurrent = Mathf.Clamp(_strokePercentCurrent + _strokeStepPerClick, 0f, 100f);
        GameLevelManager.Instance?.SetAcidStroke(_strokePercentCurrent);
        UpdateAcidDisplay();
        TryAdvanceDcsAcid();
    }

    public void DecreaseAcidStroke()
    {
        if (!IsLevel6() || _phase != Phase.DcsAcidSetup) return;
        _strokePercentCurrent = Mathf.Clamp(_strokePercentCurrent - _strokeStepPerClick, 0f, 100f);
        GameLevelManager.Instance?.SetAcidStroke(_strokePercentCurrent);
        UpdateAcidDisplay();
        TryAdvanceDcsAcid();
    }

    public void ToggleAcidTank()
    {
        // Tank selection is fixed. The former SWAP control has been removed.
    }

    public void ArmAcidSystem()
    {
        // Arming is automatic after all DCS acid parameters reach their targets.
    }

#if UNITY_EDITOR
    public void DebugEnterDcsAcidSetup()
    {
        if (_sequenceCoroutine != null)
        {
            StopCoroutine(_sequenceCoroutine);
            _sequenceCoroutine = null;
        }

        AutoFindReferences();
        EnsureRuntimeObjects();
        _acidRatioCurrent = 0f;
        _strokePercentCurrent = 0f;
        _phCurrent = _phStart;
        _tankSelected = 0;
        _acidArmed = false;
        _acidDcsReady = false;
        _slurryArrivedAtAutoclave = true;

        GameLevelManager.Instance?.SetAcidRatio(_acidRatioCurrent);
        GameLevelManager.Instance?.SetAcidStroke(_strokePercentCurrent);
        GameLevelManager.Instance?.SetPH(_phCurrent);

        if (_teleportTargetDcs == null)
            _teleportTargetDcs = FindTransformByName("SpawnPoint_DCS");

        TeleportPlayer(_teleportTargetDcs);
        _phase = Phase.DcsAcidSetup;
        ShowAcidControls(true);
        UpdateAcidDisplay();
        _hud?.ShowNotifPublic("DEBUG Level 6: atur Acid Dose 350, Pump Stroke 70%, lalu pH Leach 1.0.");
    }

    public void DebugSetDcsAcidTarget()
    {
        if (_phase != Phase.DcsAcidSetup)
            DebugEnterDcsAcidSetup();

        _acidRatioCurrent = _acidRatioTarget;
        _strokePercentCurrent = _strokePercentTarget;
        _phCurrent = _phTarget;

        GameLevelManager.Instance?.SetAcidRatio(_acidRatioCurrent);
        GameLevelManager.Instance?.SetAcidStroke(_strokePercentCurrent);
        GameLevelManager.Instance?.SetPH(_phCurrent);

        UpdateAcidDisplay();
        TryAdvanceDcsAcid();
    }
#endif

    private void OnAcidRatioChanged()
    {
        GameLevelManager.Instance?.SetAcidRatio(_acidRatioCurrent);
        UpdateAcidDisplay();
        TryAdvanceDcsAcid();
    }

    private void TryAdvanceDcsAcid()
    {
        if (_acidDcsReady) return;
        bool ratioOk = Mathf.Abs(_acidRatioCurrent - _acidRatioTarget) <= _acidRatioTolerance && _phCurrent <= 1.1f;
        bool strokeOk = Mathf.Abs(_strokePercentCurrent - _strokePercentTarget) <= _strokePercentTolerance;
        _acidArmed = ratioOk && strokeOk;
        UpdateAcidDisplay();
        if (!_acidArmed) return;

        _acidDcsReady = true;
        GameLevelManager.Instance?.NotifyLevel6DcsAcidRatioReady();
        _hud?.ShowNotifPublic("DCS acid armed (ratio + stroke OK). Pindah ke field acid skid untuk verifikasi.");
        StartSequence(TeleportKeAcidSkidCoroutine());
    }

    private void CheckExternalAcidTarget()
    {
        if (_acidDcsReady || GameLevelManager.Instance == null) return;
        _acidRatioCurrent = GameLevelManager.Instance.AcidRatio;
        _strokePercentCurrent = GameLevelManager.Instance.AcidStroke;
        _phCurrent = GameLevelManager.Instance.PH;
        UpdateAcidDisplay();
        TryAdvanceDcsAcid();
    }

    private IEnumerator TeleportKeSlurryValveCoroutine()
    {
        _phase = Phase.TeleportKeValveSlurry;
        yield return FadeHalfTeleport(_teleportTargetSlurryValve);
        XRInteractorRecovery.PulihkanRayInteractor();
        _phase = Phase.BukaValveSlurry;
        _hud?.ShowNotifPublic(_msgOpenSlurryValve);
        SetLampPair(_slurryLampRed, _slurryLampGreen, false);
    }

    private IEnumerator SlurryFlowCoroutine()
    {
        _phase = Phase.SlurryMasukAutoclave;
        SetLampPair(_slurryLampRed, _slurryLampGreen, true);
        if (_hud != null) _hud.ShowNotifPublic("Valve terbuka penuh! Cairan panas mengalir di pipa menuju autoclave...");
        yield return new WaitForSeconds(_delaySetelahValveTerbuka);
        EnsureFlowAudio();
        StartAudio(_flowAudio, 0.65f);
        yield return AnimateLineFlow(_slurryFlowObject, _preheaterOutlet, _autoclaveInlet, _durasiSlurryFlow, 0.34f);
        yield return AnimateAutoclaveFill();
        StopAudio(_flowAudio);
        _slurryArrivedAtAutoclave = true;
        GameLevelManager.Instance?.NotifyLevel6SlurryMasukAutoclaveReady();
        _phase = Phase.MenungguLaporanSlurry;
        _hud?.ShowNotifPublic(_msgSlurryArrived);
    }

    private IEnumerator KembaliKeDcsAcidCoroutine()
    {
        _phase = Phase.KembaliKeDcsAcid;
        // Re-find DCS spawn in case it was null
        if (_teleportTargetDcs == null) _teleportTargetDcs = FindTransformByName("SpawnPoint_DCS");
        yield return FadeHalfTeleport(_teleportTargetDcs);
        // Double-ensure teleport berhasil (kadang XR Origin tidak pindah di simulator).
        TeleportPlayer(_teleportTargetDcs);
        XRInteractorRecovery.PulihkanRayInteractor();
        _phase = Phase.DcsAcidSetup;
        ShowAcidControls(true);
        _hud?.ShowNotifPublic(_msgDcsAcid);
    }

    private IEnumerator TeleportKeAcidSkidCoroutine()
    {
        _phase = Phase.TeleportKeAcidSkid;
        yield return new WaitForSeconds(1.5f);
        yield return FadeHalfTeleport(_teleportTargetAcidSkid);
        XRInteractorRecovery.PulihkanRayInteractor();
        // Langsung skip button field: verifikasi acid skid cukup lewat laporan HT.
        _phase = Phase.TekanLocalStart;
        SetLampPair(_acidLampRed, _acidLampGreen, false);
        _hud?.ShowNotifPublic(_msgLocalStart);
    }

    public void PressAcidLocalStart()
    {
        if (!IsLevel6() || _phase != Phase.TekanLocalStart) return;
        StartSequence(LeakInspectionCoroutine());
    }

    public void PressAcidLeakOk()
    {
        if (!IsLevel6() || _phase != Phase.LeakInspection) return;
        if (Time.time - _leakInspectStartTime < _leakInspectionDuration)
        {
            _hud?.ShowNotifPublic("Tunggu inspeksi minimal 8 detik sebelum izinkan flow.");
            return;
        }
        _acidLeakInspectComplete = true;
        StartSequence(AcidFlowCoroutine());
    }

    private IEnumerator LocalStartAcidCoroutine()
    {
        _phase = Phase.TekanLocalStart;
        SetLampPair(_acidLampRed, _acidLampGreen, true);
        _hud?.ShowNotifPublic(_msgLocalStart);
        yield break;
    }

    private IEnumerator LeakInspectionCoroutine()
    {
        _phase = Phase.LeakInspection;
        _acidLocalStarted = true;
        _leakInspectStartTime = Time.time;
        if (_acidPumpRunningLamp != null)
        {
            EnsureLampMaterials();
            _acidPumpRunningLamp.sharedMaterial = _greenOnMat;
        }
        EnsureAcidPumpAudio();
        StartAudio(_acidPumpAudio, 0.32f);
        _hud?.ShowNotifPublic(_msgLeakInspect);
        yield break;
    }

    private void AnimateAcidPumpRotor()
    {
        if (_acidPumpRotor != null)
            _acidPumpRotor.Rotate(Vector3.up, 360f * Time.deltaTime, Space.Self);
    }

    private IEnumerator AcidFlowCoroutine()
    {
        _phase = Phase.AcidMengalir;
        SetLampPair(_acidLampRed, _acidLampGreen, true);
        EnsureAcidPumpAudio();
        if (_acidPumpAudio != null && !_acidPumpAudio.isPlaying) StartAudio(_acidPumpAudio, 0.42f);
        else if (_acidPumpAudio != null) _acidPumpAudio.volume = 0.5f;

        if (_hud != null) _hud.ShowNotifPublic("Acid mengalir ke autoclave. Lihat cairan naik di calibration column.");

        // Dua segmen cairan mengisi Pipe_RunZ lalu Pipe_RunX. Level calibration
        // column mengikuti progres gabungan jalur secara realtime.
        yield return AnimateAcidPipeRoute(Mathf.Max(12f, _durasiAcidPipeFlow));

        StopAudio(_acidPumpAudio);
        _acidQuestComplete = true;
        GameLevelManager.Instance?.NotifyLevel6AcidInjectionComplete();
        _phase = Phase.MenungguLaporanAkhir;
        _hud?.ShowNotifPublic(_msgFinalReport);
    }

    private IEnumerator AnimateColumnFill(float duration)
    {
        EnsureCalibrationColumnLiquid();
        if (_calibrationColumnLiquid == null || _calibrationColumn == null)
        {
            yield return new WaitForSeconds(duration);
            yield break;
        }

        _calibrationColumnLiquid.gameObject.SetActive(true);

        // Liquid hidup di world space (parent runtimeRoot, no rotation/scale dependency).
        // Cylinder primitive: total tinggi = localScale.y * 2 unit.
        float fullScaleY = _columnLiquidFullScaleY; // scale Y saat 100% liquid
        float scaleXZ = _columnLiquidLocalScaleXZ;
        float bottomY = _columnLiquidBottomWorldY;
        Vector3 worldXZ = _columnLiquidWorldXZ;

        float elapsed = 0f;
        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.Clamp01(elapsed / duration);
            float currentScaleY = Mathf.Lerp(0.001f, fullScaleY, t);
            _calibrationColumnLiquid.localScale = new Vector3(scaleXZ, currentScaleY, scaleXZ);
            _calibrationColumnLiquid.rotation = Quaternion.identity;
            // Posisi tengah liquid (world Y) = bottomY + currentScaleY (half height = scaleY * 1).
            float liquidWorldY = bottomY + currentScaleY;
            _calibrationColumnLiquid.position = new Vector3(worldXZ.x, liquidWorldY, worldXZ.z);

            if (_columnLevelLabel != null)
                _columnLevelLabel.text = $"COLUMN: {(t * 100f):F0}%";

            yield return null;
        }
    }

    private IEnumerator FadeHalfTeleport(Transform target)
    {
        float d = Mathf.Max(0.8f, _durasiFade);
        _hud?.PlayManualFade(d);
        yield return new WaitForSeconds(d * 0.5f);
        TeleportPlayer(target);
        yield return new WaitForSeconds(d * 0.5f);
    }

    private IEnumerator AnimateLineFlow(GameObject flowObject, Transform start, Transform end, float duration, float diameter)
    {
        if (flowObject == null || start == null || end == null)
        {
            yield return new WaitForSeconds(duration);
            yield break;
        }

        flowObject.SetActive(true);
        float elapsed = 0f;
        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(elapsed / duration));
            ConfigureCylinderBetween(flowObject.transform, start.position, Vector3.Lerp(start.position, end.position, t), diameter);
            PulseRenderer(flowObject, t);
            yield return null;
        }
        ConfigureCylinderBetween(flowObject.transform, start.position, end.position, diameter);
    }

    private IEnumerator AnimateAutoclaveFill()
    {
        EnsureAutoclaveLiquid();
        if (_autoclaveLiquidObject == null)
        {
            yield return new WaitForSeconds(_durasiAutoclaveFill);
            yield break;
        }

        _autoclaveLiquidObject.SetActive(true);
        Vector3 baseScale = GetAutoclaveLiquidTargetScale();
        Vector3 basePos = GetAutoclaveLiquidTargetPosition();
        float bottomY = basePos.y - baseScale.y * 0.5f;
        float elapsed = 0f;

        while (elapsed < _durasiAutoclaveFill)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(elapsed / _durasiAutoclaveFill));
            Vector3 scale = new Vector3(baseScale.x, Mathf.Lerp(0.04f, baseScale.y, t), baseScale.z);
            _autoclaveLiquidObject.transform.localScale = scale;
            _autoclaveLiquidObject.transform.position = new Vector3(basePos.x, bottomY + scale.y * 0.5f, basePos.z);
            _autoclaveLiquidObject.transform.Rotate(Vector3.up, Time.deltaTime * 12f, Space.World);
            yield return null;
        }
    }

    private void UpdateSlurryValveVisuals()
    {
        _slurryOpenPercent = Mathf.Clamp01(_slurryValveDegrees / _slurryFullOpenDegrees);
        if (_slurryValveWheel != null && _slurryValveHandwheel == null)
            _slurryValveWheel.localRotation = _slurryWheelBase * Quaternion.AngleAxis(_slurryValveDegrees, SafeAxis(_slurryValveAxisLocal, Vector3.up));
        if (_slurryGaugeNeedle != null)
            _slurryGaugeNeedle.localRotation = _slurryGaugeBase * Quaternion.AngleAxis(-Mathf.Lerp(0f, 145f, _slurryOpenPercent), Vector3.forward);
        SetLampPair(_slurryLampRed, _slurryLampGreen, _slurryOpenPercent >= 0.99f);
    }

    private void UpdateAcidValveVisuals()
    {
        _acidOpenPercent = Mathf.Clamp01(_acidValveDegrees / _acidFullOpenDegrees);
        if (_acidValveWheel != null)
            _acidValveWheel.localRotation = _acidWheelBase * Quaternion.AngleAxis(_acidValveDegrees, SafeAxis(_acidValveAxisLocal, Vector3.up));
        if (_acidGaugeNeedle != null)
            _acidGaugeNeedle.localRotation = _acidGaugeBase * Quaternion.AngleAxis(-Mathf.Lerp(0f, 160f, _acidOpenPercent), Vector3.forward);
        SetLampPair(_acidLampRed, _acidLampGreen, _acidOpenPercent >= 0.99f);
    }

    private bool TrackWheelRotation(Transform wheel, Transform attach, Vector3 localAxis, ref float yawLast, ref bool yawValid, ref float degrees, float maxDegrees)
    {
        if (wheel == null || attach == null)
        {
            yawValid = false;
            return false;
        }

        // Sumbu putar wheel dalam world space.
        Vector3 axisWorld = wheel.TransformDirection(localAxis).normalized;
        if (axisWorld.sqrMagnitude < 0.001f) axisWorld = Vector3.up;

        // REAL VR FEEL: ukur posisi tangan player relatif ke pusat wheel, lalu hitung sudut
        // pada bidang tegak lurus sumbu wheel. Saat tangan bergerak tangensial mengelilingi
        // wheel (cara natural memutar setir), sudut bergeser dan wheel ikut berputar real-time.
        Vector3 fromCenter = attach.position - wheel.position;
        Vector3 inPlane = Vector3.ProjectOnPlane(fromCenter, axisWorld);
        if (inPlane.sqrMagnitude < 0.0009f) return false; // tangan terlalu dekat pusat -> ambigu

        // Reference radial (kanan wheel) untuk mengukur sudut absolut.
        Vector3 reference = wheel.parent != null
            ? Vector3.ProjectOnPlane(wheel.parent.right, axisWorld).normalized
            : Vector3.ProjectOnPlane(Vector3.right, axisWorld).normalized;
        if (reference.sqrMagnitude < 0.001f) reference = Vector3.right;

        float yaw = Vector3.SignedAngle(reference, inPlane.normalized, axisWorld);
        if (!yawValid)
        {
            yawLast = yaw;
            yawValid = true;
            return false;
        }

        // Delta sudut antara frame ini dan sebelumnya. Pengali 1.0 = 1:1 (real). Tanda
        // dinegasi supaya searah jarum jam menambah pembukaan valve (konvensi lapangan).
        float delta = -Mathf.DeltaAngle(yawLast, yaw);
        yawLast = yaw;

        // Filter noise kecil + outlier besar (e.g. teleport/glitch tracking).
        if (Mathf.Abs(delta) < 0.05f || Mathf.Abs(delta) > 60f) return false;

        degrees = Mathf.Clamp(degrees + delta, 0f, maxDegrees);
        return true;
    }

    private Transform ResolveSlurryHandTracker()
    {
        if (_slurryValveWheel == null)
            return null;

        if (_slurryGrabbed && _slurryInteractorAttach != null)
            return _slurryInteractorAttach;

        if (_leftHandTracker == null || _rightHandTracker == null)
            CacheHandTrackers();

        Vector3 axisWorld = _slurryValveWheel.TransformDirection(SafeAxis(_slurryValveAxisLocal, Vector3.up)).normalized;
        if (axisWorld.sqrMagnitude < 0.001f) axisWorld = _slurryValveWheel.forward;

        Transform best = null;
        float bestScore = float.MaxValue;
        ScoreHandTracker(_leftHandTracker, _slurryValveWheel.position, axisWorld, ref best, ref bestScore);
        ScoreHandTracker(_rightHandTracker, _slurryValveWheel.position, axisWorld, ref best, ref bestScore);

        if (best == null)
        {
            _activeSlurryHandTracker = null;
            _slurryYawValid = false;
            return null;
        }

        if (_activeSlurryHandTracker != best)
        {
            _activeSlurryHandTracker = best;
            _slurryYawValid = false;
        }

        return best;
    }

    private void ScoreHandTracker(Transform candidate, Vector3 wheelCenter, Vector3 axisWorld, ref Transform best, ref float bestScore)
    {
        if (candidate == null || !candidate.gameObject.activeInHierarchy)
            return;

        Vector3 fromCenter = candidate.position - wheelCenter;
        float axialDistance = Mathf.Abs(Vector3.Dot(fromCenter, axisWorld));
        Vector3 inPlane = Vector3.ProjectOnPlane(fromCenter, axisWorld);
        float radialDistance = inPlane.magnitude;

        // Tangan harus berada dekat bidang setir dan di area ring, bukan terlalu jauh.
        if (axialDistance > 0.85f || radialDistance < 0.08f || radialDistance > 1.25f)
            return;

        float score = Mathf.Abs(radialDistance - 0.48f) + axialDistance * 0.35f;
        if (score < bestScore)
        {
            bestScore = score;
            best = candidate;
        }
    }

    private void HandleSlurryValveHandTracking(bool allowQuestAdvance)
    {
        bool changed = false;
        if (_slurryValveHandwheel != null)
        {
            float degrees = Mathf.Clamp01(_slurryValveHandwheel.OpenPercent01) * _slurryFullOpenDegrees;
            changed = Mathf.Abs(degrees - _slurryValveDegrees) > 0.01f;
            _slurryValveDegrees = degrees;
        }
        else
        {
            Transform handTarget = ResolveSlurryHandTracker();
            changed = TrackWheelRotation(_slurryValveWheel, handTarget, _slurryValveAxisLocal, ref _slurryYawLast, ref _slurryYawValid, ref _slurryValveDegrees, _slurryFullOpenDegrees);
        }

        if (changed) UpdateSlurryValveVisuals();
        if (allowQuestAdvance && _slurryOpenPercent >= 0.99f)
            StartSequence(SlurryFlowCoroutine());
    }

    private bool SimulateKeyboard(ref float degrees, float maxDegrees)
    {
#if ENABLE_INPUT_SYSTEM
        var kb = UnityEngine.InputSystem.Keyboard.current;
        if (kb == null) return false;
        float delta = 0f;
        if (kb.rKey.isPressed) delta += 240f * Time.deltaTime;
        if (kb.fKey.isPressed) delta -= 240f * Time.deltaTime;
        if (Mathf.Abs(delta) < 0.001f) return false;
        degrees = Mathf.Clamp(degrees + delta, 0f, maxDegrees);
        return true;
#elif ENABLE_LEGACY_INPUT_MANAGER
        float delta = 0f;
        if (Input.GetKey(KeyCode.R)) delta += 240f * Time.deltaTime;
        if (Input.GetKey(KeyCode.F)) delta -= 240f * Time.deltaTime;
        if (Mathf.Abs(delta) < 0.001f) return false;
        degrees = Mathf.Clamp(degrees + delta, 0f, maxDegrees);
        return true;
#else
        return false;
#endif
    }

    private void SimulateAcidDcsKeyboard()
    {
#if ENABLE_INPUT_SYSTEM
        var kb = UnityEngine.InputSystem.Keyboard.current;
        if (kb == null) return;
        if (kb.equalsKey.wasPressedThisFrame || kb.numpadPlusKey.wasPressedThisFrame) IncreaseAcidRatio();
        if (kb.minusKey.wasPressedThisFrame || kb.numpadMinusKey.wasPressedThisFrame) DecreaseAcidRatio();
        if (kb.rightBracketKey.wasPressedThisFrame) IncreaseAcidStroke();
        if (kb.leftBracketKey.wasPressedThisFrame) DecreaseAcidStroke();
        if (kb.tKey.wasPressedThisFrame) ToggleAcidTank();
        if (kb.aKey.wasPressedThisFrame) ArmAcidSystem();
#elif ENABLE_LEGACY_INPUT_MANAGER
        if (Input.GetKeyDown(KeyCode.Equals) || Input.GetKeyDown(KeyCode.KeypadPlus)) IncreaseAcidRatio();
        if (Input.GetKeyDown(KeyCode.Minus) || Input.GetKeyDown(KeyCode.KeypadMinus)) DecreaseAcidRatio();
        if (Input.GetKeyDown(KeyCode.RightBracket)) IncreaseAcidStroke();
        if (Input.GetKeyDown(KeyCode.LeftBracket)) DecreaseAcidStroke();
        if (Input.GetKeyDown(KeyCode.T)) ToggleAcidTank();
        if (Input.GetKeyDown(KeyCode.A)) ArmAcidSystem();
#endif
    }

    private void SimulateFieldAcidKeyboard()
    {
#if ENABLE_INPUT_SYSTEM
        var kb = UnityEngine.InputSystem.Keyboard.current;
        if (kb == null) return;
        if (_phase == Phase.TekanLocalStart && kb.gKey.wasPressedThisFrame) PressAcidLocalStart();
        if (_phase == Phase.LeakInspection && kb.hKey.wasPressedThisFrame) PressAcidLeakOk();
#elif ENABLE_LEGACY_INPUT_MANAGER
        if (_phase == Phase.TekanLocalStart && Input.GetKeyDown(KeyCode.G)) PressAcidLocalStart();
        if (_phase == Phase.LeakInspection && Input.GetKeyDown(KeyCode.H)) PressAcidLeakOk();
#endif
    }

    private void UpdateAcidDisplay()
    {
        // Display text TANPA unit — unit sudah ada di label kolom samping.
        string ratioStr = _acidRatioCurrent.ToString("F0");
        string phStr = "pH " + _phCurrent.ToString("F1");
        string strokeStr = _strokePercentCurrent.ToString("F0") + "%";
        string tankStr = _tankSelected == 0 ? "A" : "B";
        string armStr = _acidArmed ? "ARMED" : "DISARM";

        SetDisplayText(_displayAcidRatio, ratioStr, new Color(1f, 0.9f, 0.15f));
        SetDisplayText(_displayPH, phStr, Color.yellow);
        SetDisplayText(_displayStrokePercent, strokeStr, new Color(0.5f, 0.85f, 1f));
        SetDisplayText(_displayTankSelected, tankStr, Color.white);
        SetDisplayText(_displayArmStatus, armStr, _acidArmed ? Color.green : Color.red);

        if (_displayStatus != null)
        {
            bool ratioOk = Mathf.Abs(_acidRatioCurrent - _acidRatioTarget) <= _acidRatioTolerance && _phCurrent <= 1.1f;
            bool strokeOk = Mathf.Abs(_strokePercentCurrent - _strokePercentTarget) <= _strokePercentTolerance;
            bool full = ratioOk && strokeOk && _acidArmed;
            string statusText;
            Color statusColor;
            if (full) { statusText = "GO TO FIELD"; statusColor = Color.green; }
            else if (ratioOk && strokeOk) { statusText = "ARMED"; statusColor = Color.green; }
            else if (ratioOk) { statusText = "STROKE 70%"; statusColor = Color.yellow; }
            else { statusText = "RATIO 350"; statusColor = Color.yellow; }
            SetDisplayText(_displayStatus, statusText, statusColor);
        }
    }

    private void SetDisplayText(TMPro.TMP_Text tmp, string text, Color color)
    {
        if (tmp != null)
        {
            tmp.text = text;
            tmp.color = color;

            L6PanelTextSyncer syncer = tmp.GetComponentInParent<L6PanelTextSyncer>();
            if (syncer != null && syncer.legacy != null)
            {
                syncer.legacy.text = text;
                syncer.legacy.color = color;
            }
        }
        // Update TextMesh fallback child kalau ada
        if (tmp != null)
        {
            for (int i = 0; i < tmp.transform.childCount; i++)
            {
                Transform c = tmp.transform.GetChild(i);
                TextMesh tm = c.GetComponent<TextMesh>();
                if (tm != null)
                {
                    tm.text = text;
                    tm.color = color;
                }
            }
        }
    }

    private void StartSequence(IEnumerator routine)
    {
        if (_sequenceCoroutine != null) StopCoroutine(_sequenceCoroutine);
        _sequenceCoroutine = StartCoroutine(routine);
    }

    private bool IsLevel6()
    {
        return GameLevelManager.Instance != null && GameLevelManager.Instance.CurrentLevel == GameLevelManager.GameLevel.Level6_AcidInjection;
    }

    private void ResetLevelState()
    {
        _acidRatioCurrent = 0f;
        _phCurrent = _phStart;
        _strokePercentCurrent = 0f;
        GameLevelManager.Instance?.SetAcidStroke(0f);
        _tankSelected = 0;
        _acidArmed = false;
        _slurryValveDegrees = 0f;
        _acidValveDegrees = 0f;
        _slurryOpenPercent = 0f;
        _acidOpenPercent = 0f;
        _slurryArrivedAtAutoclave = false;
        _acidDcsReady = false;
        _acidLocalStarted = false;
        _acidLeakInspectComplete = false;
        _acidQuestComplete = false;
        _slurryGrabbed = false;
        _acidGrabbed = false;
        ShowAcidControls(false);
        ResetVisualsOnly();
        UpdateAcidDisplay();
        GameLevelManager.Instance?.SetAcidRatio(0f);
        GameLevelManager.Instance?.SetPH(_phStart);
    }

    private void ResetVisualsOnly()
    {
        if (_slurryFlowObject != null) _slurryFlowObject.SetActive(false);
        if (_acidFlowObject != null) _acidFlowObject.SetActive(false);
        if (_autoclaveLiquidObject != null) _autoclaveLiquidObject.SetActive(false);
        if (_calibrationColumnLiquid != null) _calibrationColumnLiquid.gameObject.SetActive(false);
        StopAudio(_flowAudio);
        StopAudio(_acidPumpAudio);
        SetLampPair(_slurryLampRed, _slurryLampGreen, false);
        SetLampPair(_acidLampRed, _acidLampGreen, false);
        if (_acidPumpRunningLamp != null)
        {
            EnsureLampMaterials();
            _acidPumpRunningLamp.sharedMaterial = _greenOffMat;
        }
        UpdateSlurryValveVisuals();
        // UpdateAcidValveVisuals tidak perlu (acid valve dihilangkan)
    }

    private void AutoFindReferences()
    {
        if (_playerRigRoot == null)
        {
            GameObject rig = GameObject.Find("XR Origin (XR Rig)") ?? GameObject.Find("XR Origin") ?? GameObject.FindWithTag("Player");
            if (rig != null) _playerRigRoot = rig.transform;
        }

        if (_teleportTargetDcs == null) _teleportTargetDcs = FindTransformByName("SpawnPoint_DCS");
        if (_teleportTargetSlurryValve == null) _teleportTargetSlurryValve = FindTransformByName("SpawnPoint_Lvl6");
        if (_teleportTargetAcidSkid == null) _teleportTargetAcidSkid = FindTransformByName("SpawnPoint_Lvl6_AcidSkid") ?? FindTransformByName("SpawnPoint_Lvl6");
        CacheHandTrackers();
        if (_slurryValveHandwheel == null) _slurryValveHandwheel = FindNearestSlurryHandwheel();
        if (_slurryValveWheel == null && _slurryValveHandwheel != null) _slurryValveWheel = _slurryValveHandwheel.transform;
        if (_slurryValveWheel == null) _slurryValveWheel = FindTransformContains("L6_SlurryRoute_ValveWheel_Runtime");
        if (_acidValveWheel == null) _acidValveWheel = FindTransformContains("L6_AcidSkid_ValveWheel_Runtime") ?? FindTransformContains("AcidInjection_IsolationValve_Handwheel");
        if (_slurryGaugeNeedle == null) _slurryGaugeNeedle = FindNearestNeedle(_slurryValveWheel);
        if (_acidGaugeNeedle == null) _acidGaugeNeedle = FindTransformContains("AcidInjectionSystem_Redesign_Model/PressureGauge_Needle") ?? FindNearestNeedle(_acidValveWheel);
        if (_btnAcidPlus == null) _btnAcidPlus = FindOrCreateSceneInteractable("PS_AcidRatio_pr", "A_PARAM_AcidRatio_PLUS") ?? FindInteractable("Btn_AcidPlus");
        if (_btnAcidMinus == null) _btnAcidMinus = FindOrCreateSceneInteractable("PS_AcidRatio_mr", "A_PARAM_AcidRatio_MINUS") ?? FindInteractable("Btn_AcidMinus");
        if (_btnAcidStrokePlus == null) _btnAcidStrokePlus = FindOrCreateSceneInteractable("PS_AcidStroke_pr", "A_PARAM_AcidStroke_PLUS") ?? FindInteractable("Btn_AcidStrokePlus");
        if (_btnAcidStrokeMinus == null) _btnAcidStrokeMinus = FindOrCreateSceneInteractable("PS_AcidStroke_mr", "A_PARAM_AcidStroke_MINUS") ?? FindInteractable("Btn_AcidStrokeMinus");
        if (_btnAcidTankSelect == null) _btnAcidTankSelect = FindInteractable("Btn_AcidTankSelect");
        if (_btnAcidArm == null) _btnAcidArm = FindInteractable("Btn_AcidArm");
        if (_acidPumpRotor == null) _acidPumpRotor = FindTransformContains("AcidInjection_Pump_Rotor") ?? FindTransformContains("Pump_Rotor");
        if (_preheaterOutlet == null) _preheaterOutlet = FindTransformContains("Pipe_Preheater_Outlet_CleanRiser") ?? FindTransformContains("Pipe_Preheater_Outlet_CleanElbow") ?? FindTransformContains("Preheater_Outlet");
        if (_autoclaveInlet == null) _autoclaveInlet = FindTransformContains("Pipe_Autoclave_SlurryInlet_SideNozzle") ?? FindTransformContains("Autoclave_Inlet") ?? FindTransformContains("Autoclave_Left_Cap");
        if (_acidLineStart == null) _acidLineStart = FindTransformContains("Dosing_Handwheel") ?? FindTransformContains("IsolationValve_Handwheel") ?? FindTransformContains("AcidTank_B_OutletIsolationHandwheel") ?? FindTransformContains("AcidInjection_IsolationValveBlock");
        if (_acidLineEnd == null) _acidLineEnd = FindTransformContains("L7_Label_ACID_IN") ?? FindTransformContains("Autoclave_AcidInlet") ?? FindTransformContains("Autoclave_Right_Cap");
    }

    private void EnsureRuntimeObjects()
    {
        if (_runtimeRoot == null)
        {
            Transform existingRoot = transform.Find("Level6_Runtime_Objects") ?? FindTransformByName("Level6_Runtime_Objects");
            if (existingRoot != null)
            {
                _runtimeRoot = existingRoot.gameObject;
                if (existingRoot.parent != transform)
                    existingRoot.SetParent(transform, true);
            }
            else
            {
                _runtimeRoot = new GameObject("Level6_Runtime_Objects");
                _runtimeRoot.transform.SetParent(transform, false);
            }
        }

        _slurryMat = _slurryMat != null ? _slurryMat : CreateTransparentMat("M_L6_Slurry_Purple_Runtime", new Color(0.45f, 0.12f, 0.75f, 0.78f), true);
        _acidMat = _acidMat != null ? _acidMat : CreateTransparentMat("M_L6_Acid_Amber_Runtime", new Color(1f, 0.72f, 0.08f, 0.72f), true);
        EnsureWheelFallbacks();
        EnsureFlowAnchors();
        EnsureLampFallbacks();
        if (_slurryValveHandwheel == null)
            EnsureInteractable(ref _slurryValveWheel, ref _slurryValveGrab, OnSlurryGrabbed, OnSlurryReleased, OnSlurryHoverEntered, OnSlurryHoverExited);
        // Acid valve tidak diperlukan; player tekan button instead.
        CaptureBaseRotations();
        _slurryFlowObject = EnsureFlowCylinder("L6_SlurryFlow_Preheater_To_Autoclave", _slurryMat);
        _acidFlowObject = EnsureFlowCylinder("L6_AcidFlow_To_Autoclave", _acidMat);
        EnsureAcidPipeFlowObjects();
        EnsureAutoclaveLiquid();
        EnsureFieldAcidButtonsFallback();
        WireAcidButtons();
    }

    private void EnsureWheelFallbacks()
    {
        if (_slurryValveWheel == null)
            _slurryValveWheel = CreateWheelFallback("L6_SlurryRoute_ValveWheel_Runtime", _teleportTargetSlurryValve != null ? _teleportTargetSlurryValve.position + new Vector3(0.7f, 1.15f, 1.0f) : new Vector3(18.7f, 3.2f, 57f));
        // Acid valve/button field dihilangkan: verifikasi acid skid cukup lewat laporan HT.
        // Tidak perlu valve runtime. Skip _acidValveWheel.
    }

    private void EnsureFlowAnchors()
    {
        Vector3 autoclaveCenter = GetAutoclaveCenter();
        Transform preheater = FindTransformContains("Level5_PreHeater_Blender_Industrial_UV_Overview") ?? FindTransformContains("PreHeater") ?? FindTransformContains("Preheater");

        if (_preheaterOutlet == null)
        {
            Vector3 p = preheater != null ? preheater.position + new Vector3(2.2f, 1.45f, 0f) : autoclaveCenter + new Vector3(-9.5f, 1.2f, -1.8f);
            _preheaterOutlet = CreateAnchor("L6_PreheaterOutlet_RuntimeAnchor", p);
        }

        if (_autoclaveInlet == null)
            _autoclaveInlet = CreateAnchor("L6_AutoclaveSlurryInlet_RuntimeAnchor", autoclaveCenter + new Vector3(-2.8f, 1.1f, 0f));

        if (_acidLineStart == null)
        {
            Vector3 p = _acidValveWheel != null ? _acidValveWheel.position : (_teleportTargetAcidSkid != null ? _teleportTargetAcidSkid.position + new Vector3(-0.7f, 1.2f, 0.7f) : autoclaveCenter + new Vector3(-4.2f, 0.9f, 2.4f));
            _acidLineStart = CreateAnchor("L6_AcidLineStart_RuntimeAnchor", p);
        }

        if (_acidLineEnd == null)
            _acidLineEnd = CreateAnchor("L6_AutoclaveAcidInlet_RuntimeAnchor", autoclaveCenter + new Vector3(1.9f, 1.35f, 1.8f));
    }

    private Transform CreateAnchor(string name, Vector3 position)
    {
        Transform existing = _runtimeRoot != null ? _runtimeRoot.transform.Find(name) : null;
        if (existing != null)
        {
            existing.position = position;
            return existing;
        }

        GameObject go = new GameObject(name);
        if (_runtimeRoot != null)
            go.transform.SetParent(_runtimeRoot.transform, true);
        go.transform.position = position;
        return go.transform;
    }

    private Transform CreateWheelFallback(string name, Vector3 pos)
    {
        GameObject root = new GameObject(name);
        root.transform.SetParent(_runtimeRoot.transform, true);
        root.transform.position = pos;
        root.transform.rotation = Quaternion.Euler(90f, 0f, 0f);
        Material mat = CreateOpaqueMat(name + "_Mat", new Color(1f, 0.65f, 0.05f, 1f));
        GameObject ring = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        ring.name = name + "_Ring";
        ring.transform.SetParent(root.transform, false);
        ring.transform.localScale = new Vector3(0.34f, 0.34f, 0.055f);
        Renderer rr = ring.GetComponent<Renderer>();
        if (rr != null) rr.sharedMaterial = mat;
        for (int i = 0; i < 3; i++)
        {
            GameObject spoke = GameObject.CreatePrimitive(PrimitiveType.Cube);
            spoke.name = name + "_Spoke_" + i;
            spoke.transform.SetParent(root.transform, false);
            spoke.transform.localRotation = Quaternion.Euler(0f, 0f, i * 60f);
            spoke.transform.localScale = new Vector3(0.55f, 0.035f, 0.035f);
            Renderer sr = spoke.GetComponent<Renderer>();
            if (sr != null) sr.sharedMaterial = mat;
        }
        return root.transform;
    }

    private void EnsureFieldAcidButtonsFallback()
    {
        if (_runtimeRoot == null) return;

        // Auto-find calibration column dari scene jika belum di-set.
        if (_calibrationColumn == null)
        {
            GameObject col = GameObject.Find("Transparent_CalibrationColumn");
            if (col == null)
            {
                foreach (var go in Resources.FindObjectsOfTypeAll<GameObject>())
                    if (go.name == "Transparent_CalibrationColumn" && go.scene.IsValid()) { col = go; break; }
            }
            if (col != null) _calibrationColumn = col.transform;
        }

        if (_calibrationColumnTopTick == null)
            _calibrationColumnTopTick = FindTransformByName("CalibrationColumn_Tick_08");

        // Anchor base: calibration column kalau ada, kalau tidak fallback ke spawn point acid skid.
        Vector3 anchorPos;
        if (_calibrationColumn != null)
            anchorPos = _calibrationColumn.position;
        else if (_teleportTargetAcidSkid != null)
            anchorPos = _teleportTargetAcidSkid.position;
        else
            anchorPos = new Vector3(-15.5f, 2.5f, 42f);

        // Panel base: di sebelah kanan column (player face left toward column).
        Vector3 panelBase = anchorPos + new Vector3(1.0f, -0.3f, 0f);

        if (_acidPumpRunningLamp == null)
        {
            Transform existingLamp = _runtimeRoot.transform.Find("L6_AcidSkid_PumpRunLamp_Runtime");
            if (existingLamp != null)
            {
                _acidPumpRunningLamp = existingLamp.GetComponent<Renderer>();
            }
            else
            {
                GameObject lamp = GameObject.CreatePrimitive(PrimitiveType.Sphere);
                lamp.name = "L6_AcidSkid_PumpRunLamp_Runtime";
                lamp.transform.SetParent(_runtimeRoot.transform, true);
                lamp.transform.position = panelBase + new Vector3(0f, 0.4f, 0f);
                lamp.transform.localScale = Vector3.one * 0.18f;
                Collider lc = lamp.GetComponent<Collider>();
                if (lc != null) DestroySafely(lc);
                _acidPumpRunningLamp = lamp.GetComponent<Renderer>();
            }
            EnsureLampMaterials();
            if (_acidPumpRunningLamp != null) _acidPumpRunningLamp.sharedMaterial = _greenOffMat;
        }

        // Bikin pedestal dasar di bawah button supaya gak floating
        Transform pedestal = _runtimeRoot.transform.Find("L6_AcidSkid_Pedestal_Runtime");
        if (pedestal == null)
        {
            GameObject ped = GameObject.CreatePrimitive(PrimitiveType.Cube);
            ped.name = "L6_AcidSkid_Pedestal_Runtime";
            ped.transform.SetParent(_runtimeRoot.transform, true);
            ped.transform.position = panelBase + new Vector3(0f, -0.12f, 0f);
            ped.transform.localScale = new Vector3(0.5f, 0.18f, 0.7f);
            Collider pc = ped.GetComponent<Collider>();
            if (pc != null) DestroySafely(pc);
            Renderer pr = ped.GetComponent<Renderer>();
            if (pr != null) pr.sharedMaterial = CreateOpaqueMat("L6_AcidPedestalMat", new Color(0.25f, 0.25f, 0.28f));
        }

        // Bikin liquid mesh di dalam calibration column (sub-cylinder, scale Y growing 0->1)
        EnsureCalibrationColumnLiquid();
    }

    private void EnsureCalibrationColumnLiquid()
    {
        if (_calibrationColumn == null) return;

        // Hitung ukuran column dari renderer bounds (WORLD space — selalu axis-aligned).
        Renderer colRend = _calibrationColumn.GetComponent<Renderer>();
        Vector3 worldBoundsSize = colRend != null ? colRend.bounds.size : new Vector3(0.3f, 1.0f, 0.3f);
        Vector3 worldCenter = colRend != null ? colRend.bounds.center : _calibrationColumn.position;

        // Tinggi column world = world Y bounds (column kelihatan vertikal di scene).
        float columnHeight = worldBoundsSize.y;
        // Diameter (XZ rata-rata)
        float columnDiameter = (worldBoundsSize.x + worldBoundsSize.z) * 0.5f;

        float columnBottom = worldCenter.y - columnHeight * 0.5f;
        float tickTopY = _calibrationColumnTopTick != null
            ? _calibrationColumnTopTick.position.y
            : worldCenter.y + columnHeight * 0.35f;

        // Batas liquid tepat di tick 08, dengan sedikit clearance dari dasar tabung.
        float liquidBottom = columnBottom + columnHeight * 0.035f;
        float innerHeight = Mathf.Clamp(tickTopY - liquidBottom, columnHeight * 0.1f, columnHeight * 0.94f);
        float innerDiameter = columnDiameter * 0.85f;

        Transform parent = _runtimeRoot != null ? _runtimeRoot.transform : null;
        Transform existingLiquid = _calibrationColumnLiquid != null
            ? _calibrationColumnLiquid
            : (parent != null ? parent.Find("L6_CalibrationColumn_Liquid_Runtime") : null);
        GameObject liquid;
        if (existingLiquid != null)
        {
            liquid = existingLiquid.gameObject;
        }
        else
        {
            // Bikin liquid sebagai child runtime root dengan world-space transform supaya tidak
            // ikut rotation/scale parent column yang kompleks.
            liquid = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            liquid.name = "L6_CalibrationColumn_Liquid_Runtime";
            Collider lcol = liquid.GetComponent<Collider>();
            if (lcol != null) DestroySafely(lcol);
            liquid.transform.SetParent(parent, true);
        }

        liquid.transform.rotation = Quaternion.identity; // cylinder Y axis = world up
        liquid.transform.position = new Vector3(worldCenter.x, liquidBottom, worldCenter.z);
        liquid.transform.localScale = new Vector3(innerDiameter, 0.001f, innerDiameter);

        Renderer rend = liquid.GetComponent<Renderer>();
        if (rend != null)
            rend.sharedMaterial = _acidMat != null ? _acidMat : CreateTransparentMat("M_L6_AcidColumn_Runtime", new Color(1f, 0.72f, 0.08f, 0.85f), true);
        liquid.SetActive(false);
        _calibrationColumnLiquid = liquid.transform;

        // Simpan params untuk AnimateColumnFill (semua dalam world space)
        _columnLiquidFullScaleY = innerHeight * 0.5f; // cylinder primitive total Y = scale.y * 2
        _columnLiquidLocalScaleXZ = innerDiameter;
        _columnLiquidBottomWorldY = liquidBottom;
        _columnLiquidWorldXZ = new Vector3(worldCenter.x, 0f, worldCenter.z);
    }

    private float _columnLiquidFullScaleY;
    private float _columnLiquidLocalScaleXZ;
    private float _columnLiquidBottomWorldY;
    private Vector3 _columnLiquidWorldXZ;

    private XRSimpleInteractable CreateMushroomButton(string name, Vector3 pos, Color color, string label)
    {
        Transform existing = _runtimeRoot != null ? _runtimeRoot.transform.Find(name) : null;
        GameObject root = existing != null ? existing.gameObject : new GameObject(name);
        if (existing == null && _runtimeRoot != null) root.transform.SetParent(_runtimeRoot.transform, true);
        root.transform.position = pos;

        // Orient button untuk menghadap player spawn (label readable dari spawn point view).
        if (_teleportTargetAcidSkid != null)
        {
            Vector3 toPlayer = (_teleportTargetAcidSkid.position - pos);
            toPlayer.y = 0f;
            if (toPlayer.sqrMagnitude > 0.001f)
            {
                // Forward button (label face) menghadap player.
                root.transform.rotation = Quaternion.LookRotation(-toPlayer.normalized, Vector3.up);
            }
        }

        Transform stem = root.transform.Find("Stem");
        if (stem == null)
        {
            GameObject stemGo = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            stemGo.name = "Stem";
            stemGo.transform.SetParent(root.transform, false);
            stemGo.transform.localPosition = new Vector3(0f, 0f, 0f);
            stemGo.transform.localScale = new Vector3(0.12f, 0.08f, 0.12f);
            Renderer sr = stemGo.GetComponent<Renderer>();
            if (sr != null) sr.sharedMaterial = CreateOpaqueMat(name + "_StemMat", new Color(0.18f, 0.18f, 0.18f));
            // Hapus collider stem (collider hanya di cap untuk grab)
            Collider sc = stemGo.GetComponent<Collider>();
            if (sc != null) DestroySafely(sc);
        }

        Transform cap = root.transform.Find("Cap");
        if (cap == null)
        {
            GameObject capGo = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            capGo.name = "Cap";
            capGo.transform.SetParent(root.transform, false);
            capGo.transform.localPosition = new Vector3(0f, 0.12f, 0f);
            capGo.transform.localScale = new Vector3(0.32f, 0.16f, 0.32f);
            Renderer cr = capGo.GetComponent<Renderer>();
            if (cr != null) cr.sharedMaterial = CreateOpaqueMat(name + "_CapMat", color, true);
            // Cap collider remove karena root yang punya
            Collider cc = capGo.GetComponent<Collider>();
            if (cc != null) DestroySafely(cc);
        }

        SphereCollider col = root.GetComponent<SphereCollider>();
        if (col == null) col = root.AddComponent<SphereCollider>();
        col.center = new Vector3(0f, 0.12f, 0f);
        col.radius = LocalRadiusForWorld(root.transform, 0.22f);
        col.isTrigger = false;

        XRSimpleInteractable simple = root.GetComponent<XRSimpleInteractable>();
        if (simple == null) simple = root.AddComponent<XRSimpleInteractable>();

        // Label panel di atas button (background gelap + teks putih besar, billboard mengarah ke +Z)
        Transform existingLabel = root.transform.Find("LabelPanel");
        if (existingLabel == null && !string.IsNullOrEmpty(label))
        {
            // Background plate
            GameObject panel = GameObject.CreatePrimitive(PrimitiveType.Cube);
            panel.name = "LabelPanel";
            panel.transform.SetParent(root.transform, false);
            panel.transform.localPosition = new Vector3(0f, 0.45f, 0f);
            panel.transform.localScale = new Vector3(0.55f, 0.22f, 0.04f);
            Collider pc = panel.GetComponent<Collider>();
            if (pc != null) DestroySafely(pc);
            Renderer pr = panel.GetComponent<Renderer>();
            if (pr != null) pr.sharedMaterial = CreateOpaqueMat(name + "_LabelBg", new Color(0.06f, 0.06f, 0.08f));

            // Text (TextMesh world-space)
            GameObject txtGo = new GameObject("LabelText");
            txtGo.transform.SetParent(panel.transform, false);
            txtGo.transform.localPosition = new Vector3(0f, 0f, -0.55f);
            txtGo.transform.localRotation = Quaternion.identity;
            TextMesh tm = txtGo.AddComponent<TextMesh>();
            tm.text = label;
            tm.characterSize = 0.05f;
            tm.fontSize = 80;
            tm.fontStyle = FontStyle.Bold;
            tm.anchor = TextAnchor.MiddleCenter;
            tm.alignment = TextAlignment.Center;
            tm.color = color;
            // Add second text di sisi belakang (depan dari player view) supaya selalu kelihatan
            GameObject txtBack = new GameObject("LabelTextBack");
            txtBack.transform.SetParent(panel.transform, false);
            txtBack.transform.localPosition = new Vector3(0f, 0f, 0.55f);
            txtBack.transform.localRotation = Quaternion.Euler(0f, 180f, 0f);
            TextMesh tmBack = txtBack.AddComponent<TextMesh>();
            tmBack.text = label;
            tmBack.characterSize = 0.05f;
            tmBack.fontSize = 80;
            tmBack.fontStyle = FontStyle.Bold;
            tmBack.anchor = TextAnchor.MiddleCenter;
            tmBack.alignment = TextAlignment.Center;
            tmBack.color = color;
        }

        return simple;
    }

    private void EnsureLampFallbacks()
    {
        if (_slurryValveWheel != null && (_slurryLampRed == null || _slurryLampGreen == null))
        {
            if (!TryUseExistingLampPanel("L6_SlurryRoute_LampPanel_Runtime", ref _slurryLampRed, ref _slurryLampGreen))
                CreateLampPanel("L6_SlurryRoute_LampPanel_Runtime", _slurryValveWheel.position + new Vector3(0.85f, 0.35f, 0.35f), ref _slurryLampRed, ref _slurryLampGreen);
        }

        if (_acidLampRed == null || _acidLampGreen == null)
        {
            if (!TryUseExistingLampPanel("L6_AcidSkid_LampPanel_Runtime", ref _acidLampRed, ref _acidLampGreen))
            {
                Vector3 lampPos;
                if (_calibrationColumn != null) lampPos = _calibrationColumn.position + new Vector3(1.0f, 0.6f, 0f);
                else if (_acidValveWheel != null) lampPos = _acidValveWheel.position + new Vector3(0.65f, 0.35f, 0.35f);
                else if (_teleportTargetAcidSkid != null) lampPos = _teleportTargetAcidSkid.position + new Vector3(0.7f, 0.5f, 0f);
                else lampPos = new Vector3(-15f, 3f, 42f);
                CreateLampPanel("L6_AcidSkid_LampPanel_Runtime", lampPos, ref _acidLampRed, ref _acidLampGreen);
            }
        }
    }

    private bool TryUseExistingLampPanel(string name, ref Renderer red, ref Renderer green)
    {
        Transform panel = _runtimeRoot != null ? _runtimeRoot.transform.Find(name) : null;
        if (panel == null) return false;

        Transform redLamp = panel.Find(name + "_RedLamp");
        Transform greenLamp = panel.Find(name + "_GreenLamp");
        if (red == null && redLamp != null) red = redLamp.GetComponent<Renderer>();
        if (green == null && greenLamp != null) green = greenLamp.GetComponent<Renderer>();
        return red != null && green != null;
    }

    private void CreateLampPanel(string name, Vector3 pos, ref Renderer red, ref Renderer green)
    {
        if (TryUseExistingLampPanel(name, ref red, ref green))
            return;

        GameObject panel = GameObject.CreatePrimitive(PrimitiveType.Cube);
        panel.name = name;
        panel.transform.SetParent(_runtimeRoot.transform, true);
        panel.transform.position = pos;
        panel.transform.localScale = new Vector3(0.72f, 0.46f, 0.08f);
        Renderer pr = panel.GetComponent<Renderer>();
        if (pr != null) pr.sharedMaterial = CreateOpaqueMat(name + "_PanelMat", new Color(0.45f, 0.5f, 0.48f, 1f));

        GameObject r = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        r.name = name + "_RedLamp";
        r.transform.SetParent(panel.transform, false);
        r.transform.localPosition = new Vector3(-0.22f, 0.03f, -0.62f);
        r.transform.localScale = Vector3.one * 0.18f;
        red = r.GetComponent<Renderer>();

        GameObject g = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        g.name = name + "_GreenLamp";
        g.transform.SetParent(panel.transform, false);
        g.transform.localPosition = new Vector3(0.22f, 0.03f, -0.62f);
        g.transform.localScale = Vector3.one * 0.18f;
        green = g.GetComponent<Renderer>();
    }

    private void EnsureInteractable(
        ref Transform wheel,
        ref XRGrabInteractable grab,
        UnityEngine.Events.UnityAction<UnityEngine.XR.Interaction.Toolkit.SelectEnterEventArgs> selectEnter,
        UnityEngine.Events.UnityAction<UnityEngine.XR.Interaction.Toolkit.SelectExitEventArgs> selectExit,
        UnityEngine.Events.UnityAction<UnityEngine.XR.Interaction.Toolkit.HoverEnterEventArgs> hoverEnter,
        UnityEngine.Events.UnityAction<UnityEngine.XR.Interaction.Toolkit.HoverExitEventArgs> hoverExit)
    {
        if (wheel == null) return;

        var existingGrab = wheel.GetComponent<XRGrabInteractable>();
        if (existingGrab != null)
        {
            existingGrab.enabled = false;
            DestroySafely(existingGrab);
            grab = null;
        }

        if (wheel.GetComponent<Collider>() == null)
        {
            SphereCollider col = wheel.gameObject.AddComponent<SphereCollider>();
            col.radius = LocalRadiusForWorld(wheel, 0.72f);
            col.isTrigger = false;
        }
        else if (wheel.GetComponent<Collider>() is SphereCollider sphere)
        {
            sphere.radius = LocalRadiusForWorld(wheel, 0.72f);
            sphere.isTrigger = false;
        }
        // Pastikan semua collider enabled
        foreach (var col in wheel.GetComponentsInChildren<Collider>(true))
            if (col != null) col.enabled = true;

        var rbExist = wheel.GetComponent<Rigidbody>();
        if (rbExist == null)
        {
            rbExist = wheel.gameObject.AddComponent<Rigidbody>();
        }
        // Force kinematic + no gravity supaya valve tidak jatuh karena gravity
        rbExist.isKinematic = true;
        rbExist.useGravity = false;

        var simple = wheel.GetComponent<XRSimpleInteractable>() ?? wheel.gameObject.AddComponent<XRSimpleInteractable>();
        simple.colliders.Clear();
        foreach (var col in wheel.GetComponentsInChildren<Collider>(true))
            if (col != null) simple.colliders.Add(col);

        simple.selectEntered.RemoveListener(selectEnter);
        simple.selectExited.RemoveListener(selectExit);
        simple.hoverEntered.RemoveListener(hoverEnter);
        simple.hoverExited.RemoveListener(hoverExit);
        simple.selectEntered.AddListener(selectEnter);
        simple.selectExited.AddListener(selectExit);
        simple.hoverEntered.AddListener(hoverEnter);
        simple.hoverExited.AddListener(hoverExit);

        simple.enabled = false;
        simple.enabled = true;
    }

    private void WireListeners()
    {
        WireAcidButtons();
    }

    private void WireAcidButtons()
    {
        if (_btnAcidPlus != null && !_acidPlusWired)
        {
            _btnAcidPlus.selectEntered.AddListener(_ => IncreaseAcidRatio());
            _acidPlusWired = true;
        }

        if (_btnAcidMinus != null && !_acidMinusWired)
        {
            _btnAcidMinus.selectEntered.AddListener(_ => DecreaseAcidRatio());
            _acidMinusWired = true;
        }

        if (_btnAcidStrokePlus != null && !_strokePlusWired)
        {
            _btnAcidStrokePlus.selectEntered.AddListener(_ => IncreaseAcidStroke());
            _strokePlusWired = true;
        }

        if (_btnAcidStrokeMinus != null && !_strokeMinusWired)
        {
            _btnAcidStrokeMinus.selectEntered.AddListener(_ => DecreaseAcidStroke());
            _strokeMinusWired = true;
        }

        if (_btnAcidTankSelect != null && !_tankSelectWired)
        {
            _btnAcidTankSelect.selectEntered.AddListener(_ => ToggleAcidTank());
            _tankSelectWired = true;
        }

        if (_btnAcidArm != null && !_acidArmWired)
        {
            _btnAcidArm.selectEntered.AddListener(_ => ArmAcidSystem());
            _acidArmWired = true;
        }

        // Field LOCAL START / LEAK OK buttons are no longer part of Level 6 flow.
        // Acid skid verification is handled by HT report instead.

        _acidButtonsWired = _acidPlusWired || _acidMinusWired;
    }

    private void CaptureBaseRotations()
    {
        if (_slurryValveWheel != null) _slurryWheelBase = _slurryValveWheel.localRotation;
        if (_acidValveWheel != null) _acidWheelBase = _acidValveWheel.localRotation;
        if (_slurryGaugeNeedle != null) _slurryGaugeBase = _slurryGaugeNeedle.localRotation;
        if (_acidGaugeNeedle != null) _acidGaugeBase = _acidGaugeNeedle.localRotation;
    }

    private GameObject EnsureFlowCylinder(string name, Material mat)
    {
        Transform existing = _runtimeRoot.transform.Find(name);
        GameObject go = existing != null ? existing.gameObject : GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        go.name = name;
        go.transform.SetParent(_runtimeRoot.transform, true);
        Collider col = go.GetComponent<Collider>();
        if (col != null) DestroySafely(col);
        Renderer renderer = go.GetComponent<Renderer>();
        if (renderer != null) renderer.sharedMaterial = mat;
        go.SetActive(false);
        return go;
    }

    private void EnsureAutoclaveLiquid()
    {
        // DIHAPUS atas permintaan: L6_Autoclave_PurpleLiquid_Rising_Runtime tidak dibuat lagi.
        // Cairan ungu di dalam autoclave digantikan oleh aliran via pipa L5/L7 (tabung) saja.
        return;
    }

    private Vector3 GetAutoclaveLiquidTargetPosition()
    {
        Vector3 center = GetAutoclaveCenter();
        if (center != Vector3.zero) return center + new Vector3(0f, 1.15f, 0f);
        if (_autoclaveInlet != null) return _autoclaveInlet.position + new Vector3(-7f, 1.4f, 0f);
        return new Vector3(-16f, 7f, 83.7f);
    }

    private Vector3 GetAutoclaveCenter()
    {
        Transform left = FindTransformContains("Autoclave_Left_Cap") ?? FindTransformContains("L7_Autoclave_EndCap_Left");
        Transform right = FindTransformContains("Autoclave_Right_Cap") ?? FindTransformContains("L7_Autoclave_EndCap_Right");
        if (left != null && right != null)
            return (left.position + right.position) * 0.5f;
        Transform shell = FindTransformContains("Level7_Autoclave_Blender_Industrial_UV_Auto") ?? FindTransformContains("Autoclave_Shell");
        if (shell != null) return shell.position;
        if (_autoclaveInlet != null) return _autoclaveInlet.position;
        return Vector3.zero;
    }

    private Vector3 GetAutoclaveLiquidTargetScale()
    {
        return new Vector3(4.8f, 2.6f, 2.4f);
    }

    private void ConfigureCylinderBetween(Transform cyl, Vector3 start, Vector3 end, float diameter)
    {
        Vector3 delta = end - start;
        float length = Mathf.Max(0.01f, delta.magnitude);
        cyl.position = start + delta * 0.5f;
        cyl.rotation = Quaternion.FromToRotation(Vector3.up, delta.normalized);
        cyl.localScale = new Vector3(diameter, length * 0.5f, diameter);
    }

    private void PulseRenderer(GameObject go, float t)
    {
        if (go == null) return;
        float pulse = 1f + Mathf.Sin(Time.time * 9f) * 0.025f;
        go.transform.localScale = new Vector3(go.transform.localScale.x * pulse, go.transform.localScale.y, go.transform.localScale.z * pulse);
    }

    private void SetLampPair(Renderer red, Renderer green, bool success)
    {
        EnsureLampMaterials();
        if (red != null) red.sharedMaterial = success ? _redOffMat : _redOnMat;
        if (green != null) green.sharedMaterial = success ? _greenOnMat : _greenOffMat;
    }

    private void EnsureLampMaterials()
    {
        if (_redOnMat != null) return;
        _redOnMat = CreateOpaqueMat("M_L6_RedLamp_ON", Color.red, true);
        _redOffMat = CreateOpaqueMat("M_L6_RedLamp_OFF", new Color(0.18f, 0.02f, 0.02f, 1f));
        _greenOnMat = CreateOpaqueMat("M_L6_GreenLamp_ON", Color.green, true);
        _greenOffMat = CreateOpaqueMat("M_L6_GreenLamp_OFF", new Color(0.02f, 0.14f, 0.04f, 1f));
    }

    private Material CreateOpaqueMat(string name, Color color, bool emission = false)
    {
        Shader shader = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard");
        Material mat = new Material(shader) { name = name, color = color };
        if (mat.HasProperty("_BaseColor")) mat.SetColor("_BaseColor", color);
        if (mat.HasProperty("_Color")) mat.SetColor("_Color", color);
        if (mat.HasProperty("_Surface")) mat.SetFloat("_Surface", 0f); // 0 = Opaque
        if (mat.HasProperty("_Blend")) mat.SetFloat("_Blend", 0f);
        if (mat.HasProperty("_AlphaClip")) mat.SetFloat("_AlphaClip", 0f);
        if (mat.HasProperty("_SrcBlend")) mat.SetFloat("_SrcBlend", (float)UnityEngine.Rendering.BlendMode.One);
        if (mat.HasProperty("_DstBlend")) mat.SetFloat("_DstBlend", (float)UnityEngine.Rendering.BlendMode.Zero);
        if (mat.HasProperty("_ZWrite")) mat.SetFloat("_ZWrite", 1f);
        if (mat.HasProperty("_Smoothness")) mat.SetFloat("_Smoothness", 0.4f);
        if (mat.HasProperty("_Metallic")) mat.SetFloat("_Metallic", 0.0f);
        mat.DisableKeyword("_SURFACE_TYPE_TRANSPARENT");
        mat.DisableKeyword("_ALPHABLEND_ON");
        mat.SetOverrideTag("RenderType", "Opaque");
        mat.renderQueue = 2000;
        if (emission)
        {
            mat.EnableKeyword("_EMISSION");
            if (mat.HasProperty("_EmissionColor")) mat.SetColor("_EmissionColor", color * 2.5f);
        }
        return mat;
    }

    private Material CreateTransparentMat(string name, Color color, bool emission)
    {
        Material mat = CreateOpaqueMat(name, color, emission);
        if (mat.HasProperty("_Surface")) mat.SetFloat("_Surface", 1f);
        if (mat.HasProperty("_SrcBlend")) mat.SetFloat("_SrcBlend", (float)UnityEngine.Rendering.BlendMode.SrcAlpha);
        if (mat.HasProperty("_DstBlend")) mat.SetFloat("_DstBlend", (float)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
        if (mat.HasProperty("_ZWrite")) mat.SetFloat("_ZWrite", 0f);
        mat.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
        mat.EnableKeyword("_ALPHABLEND_ON");
        mat.renderQueue = 3000;
        return mat;
    }

    private Transform FindTransformByName(string name)
    {
        GameObject go = GameObject.Find(name);
        if (go != null) return go.transform;
        Transform[] all = FindObjectsByType<Transform>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        foreach (Transform t in all)
            if (string.Equals(t.name, name, System.StringComparison.OrdinalIgnoreCase)) return t;
        return null;
    }

    private void CacheHandTrackers()
    {
        if (_leftHandTracker == null)
        {
            _leftHandTracker =
                FindTransformByName("OLIVIA_Left_TransparentHand") ??
                FindTransformByName("Left Controller") ??
                FindTransformByName("Left Hand") ??
                FindTransformContains("LeftHand") ??
                FindTransformContains("Left Hand");
        }

        if (_rightHandTracker == null)
        {
            _rightHandTracker =
                FindTransformByName("OLIVIA_Right_TransparentHand") ??
                FindTransformByName("Right Controller") ??
                FindTransformByName("Right Hand") ??
                FindTransformContains("RightHand") ??
                FindTransformContains("Right Hand");
        }
    }

private GesturalHandwheel FindNearestSlurryHandwheel()
    {
        Vector3 anchor = _teleportTargetSlurryValve != null ? _teleportTargetSlurryValve.position : new Vector3(-1.1f, 1.5f, 50.1f);
        GesturalHandwheel best = null;
        float bestScore = float.MaxValue;

        GesturalHandwheel[] all = FindObjectsByType<GesturalHandwheel>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        for (int i = 0; i < all.Length; i++)
        {
            GesturalHandwheel gh = all[i];
            if (gh == null || gh.transform == null) continue;

            string path = GetPath(gh.transform);
            if (path.IndexOf("L5_Condensate_Drain_Handwheel", System.StringComparison.OrdinalIgnoreCase) < 0 &&
                path.IndexOf("PreHeater", System.StringComparison.OrdinalIgnoreCase) < 0 &&
                path.IndexOf("Preheater", System.StringComparison.OrdinalIgnoreCase) < 0)
                continue;

            float score = Vector3.SqrMagnitude(gh.transform.position - anchor);
            if (score < bestScore)
            {
                bestScore = score;
                best = gh;
            }
        }

        return best;
    }

    private Transform FindTransformContains(string token)
    {
        Transform[] all = FindObjectsByType<Transform>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        foreach (Transform t in all)
            if (t.name.IndexOf(token, System.StringComparison.OrdinalIgnoreCase) >= 0) return t;
        foreach (Transform t in all)
        {
            string path = GetPath(t);
            if (path.IndexOf(token, System.StringComparison.OrdinalIgnoreCase) >= 0) return t;
        }
        return null;
    }

    private Transform FindNearestNeedle(Transform near)
    {
        if (near == null) return null;
        Transform best = null;
        float bestDist = float.MaxValue;
        Transform[] all = FindObjectsByType<Transform>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        foreach (Transform t in all)
        {
            if (t.name.IndexOf("Needle", System.StringComparison.OrdinalIgnoreCase) < 0) continue;
            float d = Vector3.SqrMagnitude(t.position - near.position);
            if (d < bestDist)
            {
                best = t;
                bestDist = d;
            }
        }
        return best;
    }

    private XRSimpleInteractable FindInteractable(string name)
    {
        Transform t = FindTransformByName(name);
        return t != null ? t.GetComponent<XRSimpleInteractable>() : null;
    }

private XRSimpleInteractable FindOrCreateSceneInteractable(params string[] names)
    {
        for (int i = 0; i < names.Length; i++)
        {
            Transform t = FindTransformByName(names[i]);
            if (t == null) continue;

            XRSimpleInteractable simple = t.GetComponent<XRSimpleInteractable>();
            if (simple == null)
                simple = t.gameObject.AddComponent<XRSimpleInteractable>();

            Collider collider = t.GetComponent<Collider>();
            if (collider == null)
            {
                BoxCollider box = t.gameObject.AddComponent<BoxCollider>();
                MeshFilter mesh = t.GetComponent<MeshFilter>();
                if (mesh != null && mesh.sharedMesh != null)
                {
                    box.center = mesh.sharedMesh.bounds.center;
                    box.size = mesh.sharedMesh.bounds.size * 1.8f;
                }
                else
                {
                    box.center = Vector3.zero;
                    box.size = Vector3.one * 0.18f;
                }
                collider = box;
            }

            collider.enabled = true;
            collider.isTrigger = false;
            simple.colliders.Clear();
            simple.colliders.Add(collider);
            simple.enabled = false;
            simple.enabled = true;
            return simple;
        }

        return null;
    }

    private bool HasSceneDcsAcidButtons()
    {
        return FindTransformByName("PS_AcidRatio_pr") != null &&
               FindTransformByName("PS_AcidRatio_mr") != null &&
               FindTransformByName("PS_AcidStroke_pr") != null &&
               FindTransformByName("PS_AcidStroke_mr") != null;
    }


    private string GetPath(Transform t)
    {
        string path = t.name;
        Transform p = t.parent;
        while (p != null)
        {
            path = p.name + "/" + path;
            p = p.parent;
        }
        return path;
    }

    private Vector3 SafeAxis(Vector3 axis, Vector3 fallback)
    {
        return axis.sqrMagnitude > 0.0001f ? axis.normalized : fallback;
    }

    private void DestroySafely(Object target)
    {
        if (target == null) return;
        if (Application.isPlaying) Destroy(target);
        else DestroyImmediate(target);
    }

    private void ShowAcidControls(bool show)
    {
        if (show) EnsureDcsAcidPanel();
        if (_dcsAcidPanelRoot != null) _dcsAcidPanelRoot.SetActive(show);
        if (_displayAcidRatio != null) _displayAcidRatio.gameObject.SetActive(show);
        if (_displayPH != null) _displayPH.gameObject.SetActive(show);
        if (_displayStatus != null) _displayStatus.gameObject.SetActive(show);
        if (_displayStrokePercent != null) _displayStrokePercent.gameObject.SetActive(show);
        if (_displayTankSelected != null) _displayTankSelected.gameObject.SetActive(show);
        if (_displayArmStatus != null) _displayArmStatus.gameObject.SetActive(show);
        if (show) UpdateAcidDisplay();
    }

    private GameObject _dcsAcidPanelRoot;

    private void EnsureDcsAcidPanel()
    {
        if (_dcsAcidPanelRoot != null) return;

        XRSimpleInteractable sceneAcidPlus = _btnAcidPlus;
        XRSimpleInteractable sceneAcidMinus = _btnAcidMinus;
        XRSimpleInteractable sceneStrokePlus = _btnAcidStrokePlus;
        XRSimpleInteractable sceneStrokeMinus = _btnAcidStrokeMinus;

        Transform existingRoot = _runtimeRoot != null ? _runtimeRoot.transform.Find("L6_DCS_AcidControlPanel_Runtime") : null;
        if (existingRoot == null) existingRoot = FindTransformByName("L6_DCS_AcidControlPanel_Runtime");

        GameObject root;
        if (existingRoot != null)
        {
            root = existingRoot.gameObject;
            if (_runtimeRoot != null && existingRoot.parent != _runtimeRoot.transform)
                existingRoot.SetParent(_runtimeRoot.transform, true);
            _dcsAcidPanelRoot = root;
        }
        else
        {
            root = new GameObject("L6_DCS_AcidControlPanel_Runtime");
            if (_runtimeRoot != null) root.transform.SetParent(_runtimeRoot.transform, true);
        }

        // TARUH DI LAYAR KIRI (VW_Side_L_Screen) supaya enak dilihat di layar video wall.
        // Operator menghadap +Z; panel ditaruh tepat di depan layar kiri & menghadap operator (-Z).
        Transform leftScreen = FindTransformByName("VW_Side_L_Screen") ?? FindTransformContains("VW_Side_L_Screen");
        if (leftScreen != null)
        {
            Vector3 sc = leftScreen.position;            // (~-5.67, 10.42, 20.02)
            Vector3 panelPos = new Vector3(sc.x, sc.y, sc.z - 0.06f);
            root.transform.position = panelPos;
            // Operator berdiri di sisi -Z menghadap +Z. Supaya SEMUA teks panel terbaca
            // (tidak mirror), konten harus menghadap operator: rotate root 180° Y.
            root.transform.rotation = Quaternion.Euler(0f, 180f, 0f);
            _dcsAcidPanelRoot = root;
        }
        else
        {
            // Fallback: posisi lama di kiri view DCS kalau layar tidak ketemu.
            Vector3 spawnPos = _teleportTargetDcs != null ? _teleportTargetDcs.position : new Vector3(-2.12f, 8.36f, 16.28f);
            Vector3 spawnFwd = _teleportTargetDcs != null ? _teleportTargetDcs.forward : Vector3.forward;
            Vector3 leftDir = Vector3.Cross(Vector3.up, spawnFwd).normalized;
            Vector3 panelPos = spawnPos + spawnFwd * 1.4f + leftDir * 1.6f + Vector3.up * 1.0f;
            root.transform.position = panelPos;
            Vector3 awayFromPlayer = (panelPos - spawnPos);
            awayFromPlayer.y = 0f;
            awayFromPlayer.Normalize();
            if (awayFromPlayer.sqrMagnitude < 0.001f) awayFromPlayer = spawnFwd.normalized;
            root.transform.rotation = Quaternion.LookRotation(awayFromPlayer, Vector3.up);
            _dcsAcidPanelRoot = root;
        }

        // Background plate (1.4m wide x 1.0m tall) — lebih kecil dari sebelumnya
        if (root.transform.Find("Bg") != null)
        {
            ApplyAcidPanelLayout(root.transform);
            BindExistingAcidPanelRefs(root.transform);
            RestoreSceneAcidButtons(sceneAcidPlus, sceneAcidMinus, sceneStrokePlus, sceneStrokeMinus);
            WireAcidButtons();
            UpdateAcidDisplay();
            return;
        }

        GameObject bg = GameObject.CreatePrimitive(PrimitiveType.Cube);
        bg.name = "Bg";
        bg.transform.SetParent(root.transform, false);
        bg.transform.localPosition = new Vector3(0f, 0f, 0.06f);
        bg.transform.localScale = new Vector3(1.4f, 1.28f, 0.05f);
        Collider bgCol = bg.GetComponent<Collider>();
        if (bgCol != null) DestroySafely(bgCol);
        Renderer bgRend = bg.GetComponent<Renderer>();
        if (bgRend != null) bgRend.sharedMaterial = CreateOpaqueMat("L6_DcsAcidPanelBg", new Color(0.04f, 0.05f, 0.09f));

        // Title bar (top)
        GameObject titleBg = GameObject.CreatePrimitive(PrimitiveType.Cube);
        titleBg.name = "TitleBg";
        titleBg.transform.SetParent(root.transform, false);
        titleBg.transform.localPosition = new Vector3(0f, 0.54f, 0.04f);
        titleBg.transform.localScale = new Vector3(1.38f, 0.16f, 0.01f);
        Collider tbcol = titleBg.GetComponent<Collider>(); if (tbcol != null) DestroySafely(tbcol);
        Renderer tbRend = titleBg.GetComponent<Renderer>();
        if (tbRend != null) tbRend.sharedMaterial = CreateOpaqueMat("L6_DcsAcidPanelTitleBg", new Color(0.10f, 0.40f, 0.65f));
        CreateLabelText(root.transform, "ACID INJECTION CONTROL", new Vector3(0f, 0.54f, 0.09f), 0.30f, Color.white);

        // Row 1: ACID RATIO (kompak)
        CreateRow(root.transform, "ratio",
            label: "ACID RATIO", unit: "kg/ton", target: "Target: 350",
            yPos: 0.30f,
            display: out _displayAcidRatio,
            btnPlus: out _btnAcidPlus, btnMinus: out _btnAcidMinus);

        // Row 2: PUMP STROKE
        CreateRow(root.transform, "stroke",
            label: "PUMP STROKE", unit: "%", target: "Target: 70",
            yPos: 0.07f,
            display: out _displayStrokePercent,
            btnPlus: out _btnAcidStrokePlus, btnMinus: out _btnAcidStrokeMinus);

        // Row 3: TANK SELECT
        CreateLabelText(root.transform, "TANK", new Vector3(0.57f, -0.17f, 0.09f), 0.26f, Color.white, TextAnchor.MiddleRight);
        _displayTankSelected = CreatePanelDisplay(root.transform, "TankValue", new Vector3(0.10f, -0.17f, 0.09f), 0.32f, Color.white);
        _btnAcidTankSelect = null;

        // Row 4: ARM + status
        _displayArmStatus = CreatePanelDisplay(root.transform, "ArmStatus", new Vector3(0.35f, -0.39f, 0.09f), 0.32f, Color.red);
        _btnAcidArm = null;

        // Status bar (bottom): pH + status
        _displayPH = CreatePanelDisplay(root.transform, "PHValue", new Vector3(0.57f, -0.55f, 0.09f), 0.26f, Color.yellow, TextAnchor.MiddleRight);
        _displayStatus = CreatePanelDisplay(root.transform, "StatusValue", new Vector3(-0.28f, -0.55f, 0.09f), 0.26f, Color.yellow);

        // Wire up listeners
        if (!_acidPlusWired && _btnAcidPlus != null) { _btnAcidPlus.selectEntered.AddListener(_ => IncreaseAcidRatio()); _acidPlusWired = true; }
        if (!_acidMinusWired && _btnAcidMinus != null) { _btnAcidMinus.selectEntered.AddListener(_ => DecreaseAcidRatio()); _acidMinusWired = true; }
        if (!_strokePlusWired && _btnAcidStrokePlus != null) { _btnAcidStrokePlus.selectEntered.AddListener(_ => IncreaseAcidStroke()); _strokePlusWired = true; }
        if (!_strokeMinusWired && _btnAcidStrokeMinus != null) { _btnAcidStrokeMinus.selectEntered.AddListener(_ => DecreaseAcidStroke()); _strokeMinusWired = true; }
        RestoreSceneAcidButtons(sceneAcidPlus, sceneAcidMinus, sceneStrokePlus, sceneStrokeMinus);
        ApplyAcidPanelLayout(root.transform);
        WireAcidButtons();
        UpdateAcidDisplay();

        // Skala panel supaya pas di depan layar kiri (layar 1.0x1.5m; panel 1.4x1.0m).
        if (_dcsAcidPanelRoot != null && FindTransformByName("VW_Side_L_Screen") != null)
            _dcsAcidPanelRoot.transform.localScale = Vector3.one * 0.62f;
    }

    private void BindExistingAcidPanelRefs(Transform root)
    {
        if (root == null) return;

        if (_displayAcidRatio == null) _displayAcidRatio = FindPanelText(root, "ratioValue");
        if (_displayStrokePercent == null) _displayStrokePercent = FindPanelText(root, "strokeValue");
        if (_displayTankSelected == null) _displayTankSelected = FindPanelText(root, "TankValue");
        if (_displayArmStatus == null) _displayArmStatus = FindPanelText(root, "ArmStatus");
        if (_displayPH == null) _displayPH = FindPanelText(root, "PHValue");
        if (_displayStatus == null) _displayStatus = FindPanelText(root, "StatusValue");

        // Bind tombol +/- ratio & stroke dari panel (penting untuk jalur reuse panel permanen).
        if (_btnAcidPlus == null) _btnAcidPlus = FindPanelInteractable(root, "BtnratioPlus");
        if (_btnAcidMinus == null) _btnAcidMinus = FindPanelInteractable(root, "BtnratioMinus");
        if (_btnAcidStrokePlus == null) _btnAcidStrokePlus = FindPanelInteractable(root, "BtnstrokePlus");
        if (_btnAcidStrokeMinus == null) _btnAcidStrokeMinus = FindPanelInteractable(root, "BtnstrokeMinus");
        _btnAcidTankSelect = null;
        _btnAcidArm = null;
    }

    private void EnsureAcidPipeFlowObjects()
    {
        if (_acidFlowObject == null) return;

        if (_acidPipeRunX == null) _acidPipeRunX = FindTransformByName("Pipe_RunX");
        if (_acidPipeRunZ == null) _acidPipeRunZ = FindTransformByName("Pipe_RunZ");

        _acidFlowRunX = EnsureAcidPipeSegment("Flow_Pipe_RunX");
        _acidFlowRunZ = EnsureAcidPipeSegment("Flow_Pipe_RunZ");

        Renderer parentRenderer = _acidFlowObject.GetComponent<Renderer>();
        if (parentRenderer != null) parentRenderer.enabled = false;
    }

    private GameObject EnsureAcidPipeSegment(string name)
    {
        Transform existing = _acidFlowObject.transform.Find(name);
        GameObject segment;
        if (existing != null)
        {
            segment = existing.gameObject;
        }
        else
        {
            segment = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            segment.name = name;
            segment.transform.SetParent(_acidFlowObject.transform, true);
            Collider col = segment.GetComponent<Collider>();
            if (col != null) DestroySafely(col);
        }

        Renderer renderer = segment.GetComponent<Renderer>();
        if (renderer != null)
        {
            renderer.sharedMaterial = _acidMat;
            renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            renderer.receiveShadows = false;
        }
        segment.SetActive(false);
        return segment;
    }

    private void GetPipeEndpoints(Transform pipe, out Vector3 a, out Vector3 b)
    {
        Renderer renderer = pipe != null ? pipe.GetComponent<Renderer>() : null;
        Bounds bounds = renderer != null
            ? renderer.bounds
            : new Bounds(pipe != null ? pipe.position : Vector3.zero, Vector3.one);

        Vector3 size = bounds.size;
        if (size.x >= size.y && size.x >= size.z)
        {
            a = bounds.center - Vector3.right * size.x * 0.5f;
            b = bounds.center + Vector3.right * size.x * 0.5f;
        }
        else if (size.z >= size.x && size.z >= size.y)
        {
            a = bounds.center - Vector3.forward * size.z * 0.5f;
            b = bounds.center + Vector3.forward * size.z * 0.5f;
        }
        else
        {
            a = bounds.center - Vector3.up * size.y * 0.5f;
            b = bounds.center + Vector3.up * size.y * 0.5f;
        }
    }

    private float GetPipeDiameter(Transform pipe)
    {
        Renderer renderer = pipe != null ? pipe.GetComponent<Renderer>() : null;
        if (renderer == null) return 0.2f;

        Vector3 size = renderer.bounds.size;
        if (size.x >= size.y && size.x >= size.z) return Mathf.Min(size.y, size.z);
        if (size.z >= size.x && size.z >= size.y) return Mathf.Min(size.x, size.y);
        return Mathf.Min(size.x, size.z);
    }

    private IEnumerator AnimateAcidPipeRoute(float duration)
    {
        EnsureAcidPipeFlowObjects();
        EnsureCalibrationColumnLiquid();

        if (_acidPipeRunX == null || _acidPipeRunZ == null ||
            _acidFlowRunX == null || _acidFlowRunZ == null)
        {
            yield return AnimateLineFlow(_acidFlowObject, _acidLineStart, _acidLineEnd, duration, 0.2f);
            yield break;
        }

        GetPipeEndpoints(_acidPipeRunZ, out Vector3 zA, out Vector3 zB);
        GetPipeEndpoints(_acidPipeRunX, out Vector3 xA, out Vector3 xB);

        // Sambungan kedua pipe adalah pasangan endpoint dengan jarak terdekat.
        Vector3 zJoint;
        Vector3 xJoint;
        Vector3 zStart;
        Vector3 xEnd;
        float aa = Vector3.Distance(zA, xA);
        float ab = Vector3.Distance(zA, xB);
        float ba = Vector3.Distance(zB, xA);
        float bb = Vector3.Distance(zB, xB);
        float nearest = Mathf.Min(aa, ab, ba, bb);
        if (nearest == aa) { zJoint = zA; xJoint = xA; zStart = zB; xEnd = xB; }
        else if (nearest == ab) { zJoint = zA; xJoint = xB; zStart = zB; xEnd = xA; }
        else if (nearest == ba) { zJoint = zB; xJoint = xA; zStart = zA; xEnd = xB; }
        else { zJoint = zB; xJoint = xB; zStart = zA; xEnd = xA; }

        float zLength = Vector3.Distance(zStart, zJoint);
        float xLength = Vector3.Distance(xJoint, xEnd);
        float totalLength = Mathf.Max(0.001f, zLength + xLength);
        float zEndProgress = zLength / totalLength;
        float diameter = Mathf.Min(GetPipeDiameter(_acidPipeRunX), GetPipeDiameter(_acidPipeRunZ)) * 0.56f;

        _acidFlowObject.SetActive(true);
        Renderer parentRenderer = _acidFlowObject.GetComponent<Renderer>();
        if (parentRenderer != null) parentRenderer.enabled = false;
        _acidFlowRunZ.SetActive(true);
        _acidFlowRunX.SetActive(false);
        SetColumnFillProgress(0f);

        float elapsed = 0f;
        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            float routeProgress = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(elapsed / duration));

            if (routeProgress <= zEndProgress)
            {
                float segmentProgress = zEndProgress > 0f ? routeProgress / zEndProgress : 1f;
                ConfigureCylinderBetween(_acidFlowRunZ.transform, zStart, Vector3.Lerp(zStart, zJoint, segmentProgress), diameter);
                _acidFlowRunX.SetActive(false);
            }
            else
            {
                ConfigureCylinderBetween(_acidFlowRunZ.transform, zStart, zJoint, diameter);
                _acidFlowRunX.SetActive(true);
                float segmentProgress = Mathf.InverseLerp(zEndProgress, 1f, routeProgress);
                ConfigureCylinderBetween(_acidFlowRunX.transform, xJoint, Vector3.Lerp(xJoint, xEnd, segmentProgress), diameter);
            }

            PulseRenderer(_acidFlowRunZ, routeProgress);
            if (_acidFlowRunX.activeSelf) PulseRenderer(_acidFlowRunX, routeProgress);
            SetColumnFillProgress(routeProgress);
            yield return null;
        }

        ConfigureCylinderBetween(_acidFlowRunZ.transform, zStart, zJoint, diameter);
        _acidFlowRunX.SetActive(true);
        ConfigureCylinderBetween(_acidFlowRunX.transform, xJoint, xEnd, diameter);
        SetColumnFillProgress(1f);
    }

    private void SetColumnFillProgress(float progress)
    {
        EnsureCalibrationColumnLiquid();
        if (_calibrationColumnLiquid == null) return;

        float t = Mathf.Clamp01(progress);
        _calibrationColumnLiquid.gameObject.SetActive(t > 0.001f);
        float currentScaleY = Mathf.Lerp(0.001f, _columnLiquidFullScaleY, t);
        _calibrationColumnLiquid.localScale =
            new Vector3(_columnLiquidLocalScaleXZ, currentScaleY, _columnLiquidLocalScaleXZ);
        _calibrationColumnLiquid.rotation = Quaternion.identity;
        _calibrationColumnLiquid.position =
            new Vector3(_columnLiquidWorldXZ.x, _columnLiquidBottomWorldY + currentScaleY, _columnLiquidWorldXZ.z);

        if (_columnLevelLabel != null)
            _columnLevelLabel.text = $"COLUMN: {(t * 100f):F0}%";
    }

    private void ApplyAcidPanelLayout(Transform root)
    {
        if (root == null) return;

        SetAcidPanelTransform(root, "Bg", new Vector3(0f, 0f, 0.06f), new Vector3(1.4f, 1.28f, 0.05f));
        SetAcidPanelTransform(root, "TitleBg", new Vector3(0f, 0.54f, 0.04f), new Vector3(1.38f, 0.16f, 0.01f));
        SetAcidPanelTransform(root, "ratioValue", new Vector3(0.05f, 0.30f, 0.09f));
        SetAcidPanelTransform(root, "BtnratioPlus", new Vector3(-0.30f, 0.30f, 0.055f));
        SetAcidPanelTransform(root, "BtnratioMinus", new Vector3(-0.50f, 0.30f, 0.055f));
        SetAcidPanelTransform(root, "strokeValue", new Vector3(0.05f, 0.07f, 0.09f));
        SetAcidPanelTransform(root, "BtnstrokePlus", new Vector3(-0.30f, 0.07f, 0.055f));
        SetAcidPanelTransform(root, "BtnstrokeMinus", new Vector3(-0.50f, 0.07f, 0.055f));
        SetAcidPanelTransform(root, "TankValue", new Vector3(0.10f, -0.17f, 0.09f));
        SetAcidPanelTransform(root, "ArmStatus", new Vector3(0.35f, -0.39f, 0.09f));
        SetAcidPanelTransform(root, "PHValue", new Vector3(0.57f, -0.55f, 0.09f));
        SetAcidPanelTransform(root, "StatusValue", new Vector3(-0.28f, -0.55f, 0.09f));

        TextMesh[] texts = root.GetComponentsInChildren<TextMesh>(true);
        for (int i = 0; i < texts.Length; i++)
        {
            Transform textTransform = texts[i].transform;
            textTransform.localRotation = Quaternion.Euler(0f, 180f, 0f);

            if (textTransform.parent != root)
            {
                textTransform.localPosition = new Vector3(0f, 0f, 0.55f);
                continue;
            }

            Vector3 position = textTransform.localPosition;
            position.z = 0.09f;

            switch (texts[i].text)
            {
                case "ACID INJECTION CONTROL": position = new Vector3(0f, 0.54f, 0.09f); break;
                case "ACID RATIO": position = new Vector3(0.57f, 0.325f, 0.09f); break;
                case "Target: 350": position = new Vector3(0.57f, 0.26f, 0.09f); break;
                case "kg/ton": position = new Vector3(-0.16f, 0.265f, 0.09f); break;
                case "PUMP STROKE": position = new Vector3(0.57f, 0.095f, 0.09f); break;
                case "Target: 70": position = new Vector3(0.57f, 0.03f, 0.09f); break;
                case "%": position = new Vector3(-0.16f, 0.035f, 0.09f); break;
                case "TANK": position = new Vector3(0.57f, -0.17f, 0.09f); break;
            }

            textTransform.localPosition = position;
        }
    }

    private void SetAcidPanelTransform(Transform root, string childName, Vector3 localPosition, Vector3? localScale = null)
    {
        Transform child = root.Find(childName);
        if (child == null) return;
        child.localPosition = localPosition;
        child.localRotation = Quaternion.identity;
        if (localScale.HasValue) child.localScale = localScale.Value;
    }

    private TMPro.TMP_Text FindPanelText(Transform root, string baseName)
    {
        TMPro.TMP_Text[] texts = root.GetComponentsInChildren<TMPro.TMP_Text>(true);
        for (int i = 0; i < texts.Length; i++)
        {
            if (texts[i].name == baseName + "_TmpHidden")
                return texts[i];
        }

        for (int i = 0; i < texts.Length; i++)
        {
            if (texts[i].name.IndexOf(baseName, System.StringComparison.OrdinalIgnoreCase) >= 0)
                return texts[i];
        }

        return null;
    }

    private XRSimpleInteractable FindPanelInteractable(Transform root, string name)
    {
        Transform child = root.Find(name);
        return child != null ? child.GetComponent<XRSimpleInteractable>() : null;
    }

    private void RestoreSceneAcidButtons(
        XRSimpleInteractable sceneAcidPlus,
        XRSimpleInteractable sceneAcidMinus,
        XRSimpleInteractable sceneStrokePlus,
        XRSimpleInteractable sceneStrokeMinus)
    {
        if (sceneAcidPlus != null) _btnAcidPlus = sceneAcidPlus;
        if (sceneAcidMinus != null) _btnAcidMinus = sceneAcidMinus;
        if (sceneStrokePlus != null) _btnAcidStrokePlus = sceneStrokePlus;
        if (sceneStrokeMinus != null) _btnAcidStrokeMinus = sceneStrokeMinus;
    }

    private void CreateRow(Transform parent, string idPrefix, string label, string unit, string target, float yPos,
        out TMPro.TMP_Text display,
        out XRSimpleInteractable btnPlus, out XRSimpleInteractable btnMinus)
    {
        // Label kiri (compact)
        CreateLabelText(parent, label, new Vector3(0.57f, yPos + 0.025f, 0.09f), 0.24f, Color.white, TextAnchor.MiddleRight);
        // Sub-label target di bawahnya
        CreateLabelText(parent, target, new Vector3(0.57f, yPos - 0.04f, 0.09f), 0.18f, new Color(0.65f, 0.85f, 1f), TextAnchor.MiddleRight);

        // Display value tengah
        display = CreatePanelDisplay(parent, idPrefix + "Value", new Vector3(0.05f, yPos, 0.09f), 0.36f, new Color(1f, 0.9f, 0.15f));

        // Tombol +/- kanan
        btnPlus = CreateFlatButton(parent, "Btn" + idPrefix + "Plus", new Vector3(-0.30f, yPos, 0.055f), new Color(0.1f, 0.7f, 0.2f), "+");
        btnMinus = CreateFlatButton(parent, "Btn" + idPrefix + "Minus", new Vector3(-0.50f, yPos, 0.055f), new Color(0.7f, 0.15f, 0.15f), "-");

        // Unit kecil di samping value
        CreateLabelText(parent, unit, new Vector3(-0.16f, yPos - 0.035f, 0.09f), 0.18f, new Color(0.7f, 0.9f, 1f), TextAnchor.MiddleRight);
    }

    private XRSimpleInteractable CreateFlatButton(Transform parent, string name, Vector3 localPos, Color color, string label)
    {
        GameObject btn = GameObject.CreatePrimitive(PrimitiveType.Cube);
        btn.name = name;
        btn.transform.SetParent(parent, false);
        btn.transform.localPosition = localPos;
        btn.transform.localScale = new Vector3(0.18f, 0.16f, 0.06f);

        // Make sure existing collider is BoxCollider with correct size and active
        BoxCollider bc = btn.GetComponent<BoxCollider>();
        if (bc == null) bc = btn.AddComponent<BoxCollider>();
        bc.isTrigger = false;
        bc.enabled = true;
        bc.size = new Vector3(1.5f, 1.5f, 2f); // bigger than visible box for easier hit

        Renderer rend = btn.GetComponent<Renderer>();
        if (rend != null)
        {
            // Pakai opaque (non-emissive) supaya button cube body kelihatan solid.
            rend.sharedMaterial = CreateOpaqueMat(name + "_Mat", color, false);
        }

        // Label text di permukaan button (depan, menghadap player).
        GameObject txtGo = new GameObject("Lbl");
        txtGo.transform.SetParent(btn.transform, false);
        txtGo.transform.localPosition = new Vector3(0f, 0f, 0.55f);
        txtGo.transform.localRotation = Quaternion.Euler(0f, 180f, 0f);
        TextMesh tm = txtGo.AddComponent<TextMesh>();
        tm.text = label;
        tm.characterSize = 0.06f;
        tm.fontSize = 90;
        tm.fontStyle = FontStyle.Bold;
        tm.anchor = TextAnchor.MiddleCenter;
        tm.alignment = TextAlignment.Center;
        tm.color = Color.white;
        var tmMr = txtGo.GetComponent<MeshRenderer>();
        if (tmMr != null) { tmMr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off; tmMr.receiveShadows = false; }

        XRSimpleInteractable simple = btn.GetComponent<XRSimpleInteractable>();
        if (simple == null) simple = btn.AddComponent<XRSimpleInteractable>();
        return simple;
    }

    private TMPro.TMP_Text CreateTmpDisplay(Transform parent, string name, Vector3 localPos, float size, Color color, TextAnchor anchor = TextAnchor.MiddleCenter)
    {
        // Legacy: forward to new clean implementation.
        return CreatePanelDisplay(parent, name, localPos, size, color, anchor);
    }

    /// <summary>
    /// Bikin display text di panel (untuk angka readout). Returns TMP_Text wrapper yang
    /// sebenarnya update TextMesh legacy 3D di dalamnya — supaya rendering reliable
    /// tanpa butuh setup TMP font asset.
    /// </summary>
    private TMPro.TextMeshProUGUI CreatePanelDisplay(Transform parent, string name, Vector3 localPos, float size, Color color, TextAnchor anchor = TextAnchor.MiddleCenter)
    {
        // Bikin GameObject dengan TextMesh (legacy 3D) saja — TMP_Text dummy
        GameObject go = new GameObject(name);
        go.transform.SetParent(parent, false);
        go.transform.localPosition = localPos;
        // Flip 180° Y supaya angka terbaca benar oleh operator (tidak mirror).
        go.transform.localRotation = Quaternion.Euler(0f, 180f, 0f);

        TextMesh tm = go.AddComponent<TextMesh>();
        tm.text = "--";
        tm.characterSize = 0.05f * size;
        tm.fontSize = 60;
        tm.fontStyle = FontStyle.Bold;
        tm.anchor = anchor;
        tm.alignment = anchor == TextAnchor.MiddleLeft ? TextAlignment.Left :
                       anchor == TextAnchor.MiddleRight ? TextAlignment.Right :
                       TextAlignment.Center;
        tm.color = color;
        var mr = go.GetComponent<MeshRenderer>();
        if (mr != null) { mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off; mr.receiveShadows = false; }

        // Wrap dalam proxy: bikin RectTransform + TextMeshProUGUI yang ke-link ke TextMesh
        // supaya kode UpdateAcidDisplay() tetap bisa pakai .text setter.
        // Tambah komponen helper LegacyTextProxy yang sync TMP_Text.text → TextMesh.text.
        var canvasGo = new GameObject(name + "_TmpProxy");
        canvasGo.transform.SetParent(go.transform, false);
        canvasGo.transform.localPosition = Vector3.zero;
        canvasGo.transform.localRotation = Quaternion.identity;
        var canvas = canvasGo.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.WorldSpace;
        canvas.sortingOrder = -1; // di belakang
        var rt = canvasGo.GetComponent<RectTransform>();
        rt.sizeDelta = new Vector2(0.001f, 0.001f); // tiny invisible
        rt.localScale = Vector3.one * 0.001f;

        var tmpGo = new GameObject(name + "_TmpHidden");
        tmpGo.transform.SetParent(canvasGo.transform, false);
        var tmpRt = tmpGo.AddComponent<RectTransform>();
        tmpRt.sizeDelta = new Vector2(1f, 1f);
        var tmp = tmpGo.AddComponent<TMPro.TextMeshProUGUI>();
        tmp.text = "--";
        tmp.color = color;
        // Make invisible
        var cg = tmpGo.AddComponent<CanvasGroup>();
        cg.alpha = 0f;
        cg.blocksRaycasts = false;
        cg.interactable = false;

        // Sync component: kalau TMP.text di-update, set TextMesh.text juga.
        var syncer = go.AddComponent<L6PanelTextSyncer>();
        syncer.tmp = tmp;
        syncer.legacy = tm;

        return tmp;
    }

    private void CreateLabelText(Transform parent, string text, Vector3 localPos, float size, Color color, TextAnchor anchor = TextAnchor.MiddleCenter)
    {
        GameObject go = new GameObject("Lbl_" + text.Substring(0, System.Math.Min(text.Length, 8)));
        go.transform.SetParent(parent, false);
        go.transform.localPosition = localPos;
        // Flip 180° Y supaya teks terbaca benar oleh operator (menghadap -Z), tidak mirror.
        go.transform.localRotation = Quaternion.Euler(0f, 180f, 0f);
        TextMesh tm = go.AddComponent<TextMesh>();
        tm.text = text;
        tm.characterSize = 0.04f * size;
        tm.fontSize = 60;
        tm.fontStyle = FontStyle.Bold;
        tm.anchor = anchor;
        tm.alignment = anchor == TextAnchor.MiddleLeft ? TextAlignment.Left :
                       anchor == TextAnchor.MiddleRight ? TextAlignment.Right :
                       TextAlignment.Center;
        tm.color = color;
        var mr = go.GetComponent<MeshRenderer>();
        if (mr != null) { mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off; mr.receiveShadows = false; }
    }

    private void TeleportPlayer(Transform target)
    {
        if (target == null) return;
        if (_playerRigRoot == null) AutoFindReferences();
        if (_playerRigRoot == null) return;
        CharacterController cc = _playerRigRoot.GetComponent<CharacterController>();
        bool wasEnabled = cc != null && cc.enabled;
        if (wasEnabled) cc.enabled = false;
        XROrigin origin = _playerRigRoot.GetComponent<XROrigin>();
        if (origin != null)
        {
            origin.MoveCameraToWorldLocation(target.position);
            origin.MatchOriginUpCameraForward(Vector3.up, target.forward);
        }
        // Fallback: juga set position/rotation langsung supaya pasti pindah.
        _playerRigRoot.SetPositionAndRotation(target.position, target.rotation);
        if (wasEnabled) cc.enabled = true;
    }

    private void EnsureFlowAudio()
    {
        if (_flowAudio != null) return;
        _flowAudio = gameObject.AddComponent<AudioSource>();
        _flowAudio.loop = true;
        _flowAudio.spatialBlend = 0.4f;
        _flowAudio.clip = GenerateNoiseClip("L6SlurryFlow", 4f, 22050, 0.35f);
    }

    private void EnsureAcidPumpAudio()
    {
        if (_acidPumpAudio != null) return;
        _acidPumpAudio = gameObject.AddComponent<AudioSource>();
        _acidPumpAudio.loop = true;
        _acidPumpAudio.spatialBlend = 0.5f;
        _acidPumpAudio.clip = GenerateNoiseClip("L6AcidPump", 3f, 22050, 0.28f);
    }

    private AudioClip GenerateNoiseClip(string name, float duration, int sampleRate, float gain)
    {
        int total = Mathf.CeilToInt(duration * sampleRate);
        float[] data = new float[total];
        System.Random rnd = new System.Random(name.GetHashCode());
        float lp = 0f;
        for (int i = 0; i < total; i++)
        {
            float n = ((float)rnd.NextDouble() - 0.5f) * 2f;
            lp += 0.08f * (n - lp);
            float hum = Mathf.Sin(2f * Mathf.PI * 85f * i / sampleRate) * 0.18f;
            data[i] = (lp + hum) * gain;
        }
        AudioClip clip = AudioClip.Create(name, total, 1, sampleRate, false);
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

    private void OnSlurryGrabbed(UnityEngine.XR.Interaction.Toolkit.SelectEnterEventArgs args)
    {
        _slurryGrabbed = true;
        _slurryInteractorAttach = args.interactorObject != null ? args.interactorObject.transform : null;
        _slurryYawValid = false;
    }

    private void OnSlurryReleased(UnityEngine.XR.Interaction.Toolkit.SelectExitEventArgs args)
    {
        _slurryGrabbed = false;
        _slurryInteractorAttach = null;
        _slurryYawValid = false;
    }

    private void OnSlurryHoverEntered(UnityEngine.XR.Interaction.Toolkit.HoverEnterEventArgs args)
    {
        _slurryGrabbed = true;
        _slurryInteractorAttach = args.interactorObject != null ? args.interactorObject.transform : null;
        _slurryYawValid = false;
    }

    private void OnSlurryHoverExited(UnityEngine.XR.Interaction.Toolkit.HoverExitEventArgs args)
    {
        _slurryGrabbed = false;
        _slurryInteractorAttach = null;
        _slurryYawValid = false;
    }

    private void OnAcidGrabbed(UnityEngine.XR.Interaction.Toolkit.SelectEnterEventArgs args)
    {
        _acidGrabbed = true;
        _acidInteractorAttach = args.interactorObject != null ? args.interactorObject.transform : null;
        _acidYawValid = false;
    }

    private void OnAcidReleased(UnityEngine.XR.Interaction.Toolkit.SelectExitEventArgs args)
    {
        _acidGrabbed = false;
        _acidInteractorAttach = null;
        _acidYawValid = false;
    }

    private static float LocalRadiusForWorld(Transform t, float worldRadius)
    {
        Vector3 s = t != null ? t.lossyScale : Vector3.one;
        float maxAxis = Mathf.Max(Mathf.Abs(s.x), Mathf.Abs(s.y), Mathf.Abs(s.z), 0.0001f);
        return worldRadius / maxAxis;
    }

    public float AcidRatioCurrent => _acidRatioCurrent;
    public float PHCurrent => _phCurrent;
    public bool SlurryArrivedAtAutoclave => _slurryArrivedAtAutoclave;
    public bool AcidQuestComplete => _acidQuestComplete;
}

/// <summary>
/// Syncer kecil: setiap frame, copy text dari hidden TextMeshProUGUI ke TextMesh legacy.
/// Supaya code yang set _displayXxx.text bisa tetap pakai TMP API tapi rendering pakai TextMesh.
/// </summary>
[ExecuteAlways]
public class L6PanelTextSyncer : MonoBehaviour
{
    public TMPro.TMP_Text tmp;
    public TextMesh legacy;
    private string _last;

    private void LateUpdate()
    {
        if (tmp == null || legacy == null) return;
        if (tmp.text != _last)
        {
            _last = tmp.text;
            legacy.text = tmp.text;
            legacy.color = tmp.color;
        }
    }
}
