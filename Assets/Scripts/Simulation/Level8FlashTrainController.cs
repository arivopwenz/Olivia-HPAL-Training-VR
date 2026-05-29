using System.Collections;
using System.Collections.Generic;
using TMPro;
using Unity.XR.CoreUtils;
using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit.Interactables;

/// <summary>
/// OLIVIA VR — Level 8 FlashTrainController.cs
///
/// FLOW LEVEL 8 — Flash Train 3-Stage Pressure Letdown + Sampling (HPAL SOP):
///   1. Player tekan DCS 8 → fade teleport ke depan FV1
///   2. Putar bypass handwheel FV1 (10-turn) → P 47→12 atm, lampu RED→GREEN
///   3. Interlock check: P_FV1 < 13 atm sebelum FV2 bisa dibuka
///   4. Putar bypass handwheel FV2 → P 12→3 atm
///   5. Putar steam valve FV3 → P 3→1.05 atm (atmospheric flash)
///   6. Slurry mengalir ke CCD via Feed_FromFlashVessel_To_CCD1
///   7. Sampling 3 bottles dari sample port masing-masing FV (Q/W/E keys atau XR grab)
///   8. Submit ke lab → pop-up canvas dengan analisis: free acid, Ni, Co, Fe, T
///   9. Voice report HT (tahan T) → Mission Complete Canvas
///  10. STAY (lihat proses) atau KEMBALI KE DCS → Level 9 (CCD)
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
    [SerializeField] private float _handwheelFullOpenDegrees = 3600f; // 10 putaran

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

    [Header("=== Sample System ===")]
    [SerializeField] private float _sampleCoolDuration = 6f;
    [SerializeField] private Color _sampleHotColor = new Color(1f, 0.2f, 0.1f);
    [SerializeField] private Color _sampleCoolColor = new Color(0.45f, 0.15f, 0.55f);

    [Header("=== Audio ===")]
    [SerializeField] private AudioSource _steamReleaseAudio;
    [SerializeField] private AudioSource _alarmAudio;
    [Range(0f, 1f)] [SerializeField] private float _steamReleaseVolume = 0.5f;

    [Header("=== Keys (Debug) ===")]
    [SerializeField] private KeyCode _key1Open = KeyCode.Alpha1;
    [SerializeField] private KeyCode _key2Open = KeyCode.Alpha2;
    [SerializeField] private KeyCode _key3Open = KeyCode.Alpha3;
    [SerializeField] private KeyCode _keySample1 = KeyCode.Q;
    [SerializeField] private KeyCode _keySample2 = KeyCode.W;
    [SerializeField] private KeyCode _keySample3 = KeyCode.E;
    [SerializeField] private KeyCode _keySubmitLab = KeyCode.L;

    [Header("=== HUD Messages ===")]
    [TextArea(2, 4)] [SerializeField] private string _msgStart =
        "Flash Train: buka 3 letdown valve berurutan FV1 → FV2 → FV3.\nPutar handwheel kuning di setiap vessel (10 putaran clockwise).";
    [TextArea(2, 4)] [SerializeField] private string _msgFv1Done =
        "FV1 stable di 12 atm. Pindah ke FV2, buka letdown handwheel berikutnya.";
    [TextArea(2, 4)] [SerializeField] private string _msgFv2Done =
        "FV2 stable di 3 atm. Pindah ke FV3 atmospheric flash.";
    [TextArea(2, 4)] [SerializeField] private string _msgFv3Done =
        "FV3 stable di 1.05 atm. Slurry mengalir ke CCD.\nAmbil 3 sample bottle dari sample port masing-masing FV (Q/W/E).";
    [TextArea(2, 4)] [SerializeField] private string _msgSamplingDone =
        "3 sample collected. Submit ke lab dengan tekan [L].";
    [TextArea(2, 4)] [SerializeField] private string _msgLabComplete =
        "Lab QC sukses, semua parameter dalam SOP. Lapor HT (tahan T) untuk akhir level.";

    // ========== Runtime ==========
    private enum Phase { Idle, MenungguDcs, TeleportField, OpenFV1, OpenFV2, OpenFV3, Sampling, LabSubmit, MenungguLapor, Selesai }
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

    // Cached handwheel state
    private HandwheelState _hw1, _hw2, _hw3;

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
        public Transform interactorAttach;
        public bool yawValid;
        public float yawLast;
        public UnityEngine.XR.Interaction.Toolkit.Interactables.XRGrabInteractable grab;
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
    }

    private void OnDisable()
    {
        GameLevelManager.OnLevelStarted -= OnLevelStarted;
        GameLevelManager.OnDCSButtonPressed -= OnDcsButtonPressed;
        GameLevelManager.OnVoiceReportAccepted -= OnVoiceReportAccepted;
        if (_seqCoroutine != null) StopCoroutine(_seqCoroutine);
        StopAudio(_steamReleaseAudio);
        StopAudio(_alarmAudio);
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
        }
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
        TeleportPlayerToField();
        yield return new WaitForSeconds(d * 0.5f);

        _phase = Phase.OpenFV1;
        if (_hud != null) _hud.ShowNotifPublic(_msgStart, 8f);
        EnsureSteamReleaseAudio();
    }

    private void Update()
    {
        if (!_levelActive) return;

        // Track handwheel rotation untuk semua 3 vessel
        if (_phase == Phase.OpenFV1) UpdateHandwheel(_hw1, _fv1, _key1Open);
        if (_phase == Phase.OpenFV2) UpdateHandwheel(_hw2, _fv2, _key2Open);
        if (_phase == Phase.OpenFV3) UpdateHandwheel(_hw3, _fv3, _key3Open);

        // Update slurry pool visibility based on stage progress
        UpdateSlurryPoolVisuals();
        UpdateCascadePanelTexts();

        // Check stage transitions
        CheckStageProgress();

        // Sampling input (only in Sampling phase)
        if (_phase == Phase.Sampling)
        {
            if (Input.GetKeyDown(_keySample1)) TakeSample(0);
            if (Input.GetKeyDown(_keySample2)) TakeSample(1);
            if (Input.GetKeyDown(_keySample3)) TakeSample(2);
            if (Input.GetKeyDown(_keySubmitLab) && AllSamplesTaken()) SubmitLabQC();
        }
    }

    // ============================================================
    //  HANDWHEEL ROTATION (10-turn, world-axis stable)
    // ============================================================

    private void UpdateHandwheel(HandwheelState hw, FlashStage stage, KeyCode debugKey)
    {
        if (hw == null || !hw.initialized) return;

        // Keyboard debug: press to advance handwheel (R untuk speed up testing)
        float deltaDeg = 0f;
        if (Input.GetKey(debugKey)) deltaDeg += 720f * Time.deltaTime; // 2 turn/sec dengan key

        // XR rotation tracking via grabbed interactor
        if (hw.grabbed && hw.interactorAttach != null)
        {
            Vector3 axis = hw.axisWorld;
            Vector3 projected = Vector3.ProjectOnPlane(hw.interactorAttach.forward, axis).normalized;
            if (projected.sqrMagnitude > 0.001f)
            {
                Vector3 reference = Vector3.ProjectOnPlane(Vector3.right, axis).normalized;
                float yaw = Vector3.SignedAngle(reference, projected, axis);
                if (!hw.yawValid)
                {
                    hw.yawLast = yaw;
                    hw.yawValid = true;
                }
                else
                {
                    float yawDelta = -Mathf.DeltaAngle(hw.yawLast, yaw) * 1.6f;
                    hw.yawLast = yaw;
                    if (Mathf.Abs(yawDelta) < 0.05f || Mathf.Abs(yawDelta) > 35f)
                        yawDelta = 360f * Time.deltaTime; // Auto-rotate fallback
                    deltaDeg += yawDelta;
                }
            }
        }

        if (Mathf.Abs(deltaDeg) < 0.001f) return;

        hw.degrees = Mathf.Clamp(hw.degrees + deltaDeg, 0f, _handwheelFullOpenDegrees);
        ApplyHandwheelRotation(hw);

        // Update stage open percent
        stage.openPercent = Mathf.Clamp01(hw.degrees / _handwheelFullOpenDegrees);
        // Pressure & temperature lerp dari start ke target sesuai openPercent
        stage.pressureCurrent = Mathf.Lerp(stage.pressureStart, stage.pressureTarget, stage.openPercent);
        stage.tempCurrent = Mathf.Lerp(stage.tempStart, stage.tempTarget, stage.openPercent);

        // Audio: play steam release saat sedang membuka
        StartAudio(_steamReleaseAudio, _steamReleaseVolume * stage.openPercent);
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
        _hw1 = BuildHandwheelState(_fv1HandwheelHub);
        _hw2 = BuildHandwheelState(_fv2HandwheelHub);
        _hw3 = BuildHandwheelState(_fv3HandwheelHub);
        EnsureHandwheelInteractable(_hw1);
        EnsureHandwheelInteractable(_hw2);
        EnsureHandwheelInteractable(_hw3);
    }

    private void EnsureHandwheelInteractable(HandwheelState hw)
    {
        if (hw == null || hw.hub == null) return;
        // Pakai parent assembly sebagai target grab supaya seluruh handwheel ikut visual.
        Transform target = hw.hub;

        // Tambahkan SphereCollider kalau belum ada (untuk grab area).
        if (target.GetComponent<Collider>() == null)
        {
            var col = target.gameObject.AddComponent<SphereCollider>();
            col.radius = 0.4f;
            col.isTrigger = false;
        }

        // Rigidbody kinematic supaya grab interactable bekerja tanpa physics push.
        var rb = target.GetComponent<Rigidbody>() ?? target.gameObject.AddComponent<Rigidbody>();
        rb.isKinematic = true;
        rb.useGravity = false;

        // XRGrabInteractable
        hw.grab = target.GetComponent<UnityEngine.XR.Interaction.Toolkit.Interactables.XRGrabInteractable>()
                ?? target.gameObject.AddComponent<UnityEngine.XR.Interaction.Toolkit.Interactables.XRGrabInteractable>();
        hw.grab.enabled = true;
        hw.grab.selectEntered.RemoveAllListeners();
        hw.grab.selectExited.RemoveAllListeners();
        hw.grab.selectEntered.AddListener((args) =>
        {
            hw.grabbed = true;
            hw.interactorAttach = args.interactorObject != null ? args.interactorObject.transform : null;
            hw.yawValid = false;
        });
        hw.grab.selectExited.AddListener((args) =>
        {
            hw.grabbed = false;
            hw.interactorAttach = null;
            hw.yawValid = false;
        });
    }

    private HandwheelState BuildHandwheelState(Transform hub)
    {
        if (hub == null) return null;
        var hw = new HandwheelState();
        hw.hub = hub;
        hw.pivotWorld = hub.position;
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
        // FV1 → FV2 transition
        if (_phase == Phase.OpenFV1 && _fv1.openPercent >= 0.99f)
        {
            _fv1.isStable = true;
            SetStatusStripColor(_fv1StatusStrip, Color.green);
            _phase = Phase.OpenFV2;
            if (_hud != null) _hud.ShowNotifPublic(_msgFv1Done, 6f);
        }

        // FV2 → FV3 transition (interlock: FV1 must be stable)
        if (_phase == Phase.OpenFV2 && _fv2.openPercent >= 0.99f && _fv1.isStable)
        {
            _fv2.isStable = true;
            SetStatusStripColor(_fv2StatusStrip, Color.green);
            _phase = Phase.OpenFV3;
            if (_hud != null) _hud.ShowNotifPublic(_msgFv2Done, 6f);
        }

        // FV3 done → Sampling
        if (_phase == Phase.OpenFV3 && _fv3.openPercent >= 0.99f && _fv2.isStable)
        {
            _fv3.isStable = true;
            SetStatusStripColor(_fv3StatusStrip, Color.green);
            _phase = Phase.Sampling;
            if (_hud != null) _hud.ShowNotifPublic(_msgFv3Done, 9f);
            StopAudio(_steamReleaseAudio);
        }
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
        string stageName = idx == 0 ? "FV1 HP (195°C)" : idx == 1 ? "FV2 MP (145°C)" : "FV3 LP (102°C)";
        if (_hud != null) _hud.ShowNotifPublic($"Sample {stageName} collected. ({CountSamples()}/3)", 4f);
        if (AllSamplesTaken())
        {
            if (_hud != null) _hud.ShowNotifPublic(_msgSamplingDone, 6f);
        }
    }

    private bool AllSamplesTaken()
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

    private void SubmitLabQC()
    {
        _phase = Phase.LabSubmit;
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
        AddUIText(canvasGO.transform, "Title", "▼ LABORATORY QC ANALYSIS",
            new Color(0.3f, 0.9f, 0.6f), 30, FontStyle.Bold, TextAnchor.MiddleCenter,
            new Vector2(0, 0.85f), new Vector2(1, 1f));

        // Sample rows
        string[] sampleData = {
            "FV1 HP (195°C):  Free acid 18.0 g/L | Ni 5.2 g/L | Co 0.45 g/L | Fe 0.8 g/L  ✓",
            "FV2 MP (145°C):  Free acid 18.5 g/L | Ni 5.3 g/L | Co 0.46 g/L | Fe 0.7 g/L  ✓",
            "FV3 LP (102°C):  Free acid 19.0 g/L | Ni 5.4 g/L | Co 0.47 g/L | Fe 0.6 g/L  ✓"
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
            "VERDICT: Semua dalam SOP. Slurry siap ke CCD.\n(Free acid 15-25, Ni > 4.5, Fe < 1.5)",
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
        GameLevelManager.Instance?.NotifyLevel7SampleTaken(); // pakai existing API kalau Level 8 belum punya
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
        ShowMissionCompleteCanvas();
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
        AddUIButton(canvasGO.transform, "KEMBALI KE DCS → LEVEL 9",
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
        GameLevelManager.Instance?.MulaiLevel(GameLevelManager.GameLevel.Level9_FlashVessel);
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
            origin.MoveCameraToWorldLocation(_teleportTargetField.position);
            origin.MatchOriginUpCameraForward(Vector3.up, _teleportTargetField.forward);
        }
        _playerRigRoot.SetPositionAndRotation(_teleportTargetField.position, _teleportTargetField.rotation);
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

        if (_teleportTargetField == null)
        {
            // Coba SpawnPoint_Lvl8 atau buat runtime di depan FV1
            var sp = GameObject.Find("SpawnPoint_Lvl8_FlashTrain");
            if (sp == null) sp = GameObject.Find("SpawnPoint_Lvl8");
            if (sp != null) _teleportTargetField = sp.transform;
            else _teleportTargetField = CreateRuntimeSpawnPoint();
        }

        // Handwheels — prioritaskan handwheel orange yang user buat di field (di depan flash vessel),
        // dengan fallback ke bypass handwheel pada vessel itu sendiri kalau tidak ada.
        if (_fv1HandwheelHub == null)
        {
            _fv1HandwheelHub = FindFieldHandwheelByAssembly("IsolationValve_Assembly_03");
            if (_fv1HandwheelHub == null) _fv1HandwheelHub = FindByNameInactive("FV1_To_FV2_InterstageLetdownValve_BypassHandwheel");
        }
        if (_fv2HandwheelHub == null)
        {
            _fv2HandwheelHub = FindFieldHandwheelByAssembly("IsolationValve_Assembly_02");
            if (_fv2HandwheelHub == null) _fv2HandwheelHub = FindByNameInactive("FV2_To_FV3_InterstageLetdownValve_BypassHandwheel");
        }
        if (_fv3HandwheelHub == null)
        {
            _fv3HandwheelHub = FindFieldHandwheelByAssembly("LetdownValve_Assembly");
            if (_fv3HandwheelHub == null) _fv3HandwheelHub = FindByNameInactive("FV3_SteamValve_Handwheel");
        }

        // Cascade panels
        if (_fv1StatusStrip == null) _fv1StatusStrip = FindRendererByName("FV1_PressureCascadePanel_StatusStrip");
        if (_fv2StatusStrip == null) _fv2StatusStrip = FindRendererByName("FV2_PressureCascadePanel_StatusStrip");
        if (_fv3StatusStrip == null) _fv3StatusStrip = FindRendererByName("FV3_PressureCascadePanel_StatusStrip");
        if (_fv1PanelText == null) _fv1PanelText = FindTextMeshPro("FV1_PressureCascadePanel_Text");
        if (_fv2PanelText == null) _fv2PanelText = FindTextMeshPro("FV2_PressureCascadePanel_Text");
        if (_fv3PanelText == null) _fv3PanelText = FindTextMeshPro("FV3_PressureCascadePanel_Text");

        // Slurry ghost
        if (_fv1SlurryGhost == null) _fv1SlurryGhost = FindByNameInactive("FV1_XRay_SlurryPool_Ghost");
        if (_fv2SlurryGhost == null) _fv2SlurryGhost = FindByNameInactive("FV2_XRay_SlurryPool_Ghost");
        if (_fv3SlurryGhost == null) _fv3SlurryGhost = FindByNameInactive("FV3_XRay_SlurryPool_Ghost");

        // Vapor risers
        if (_fv1VaporRiser == null) _fv1VaporRiser = FindByNameInactive("FV1_TopVaporOutlet_Riser");
        if (_fv2VaporRiser == null) _fv2VaporRiser = FindByNameInactive("FV2_TopVaporOutlet_Riser");
        if (_fv3VaporRiser == null) _fv3VaporRiser = FindByNameInactive("FV3_TopVaporOutlet_Riser");
    }

    private Transform CreateRuntimeSpawnPoint()
    {
        // Spawn di field flash train, dekat handwheel orange yang user buat (-20..-35, 1.5, 109.8)
        // Player landing di Z lebih dekat = 108, hadap ke handwheel (Z+).
        var hwFv1 = FindByNameInactive("IsolationValve_Assembly_03");
        Vector3 pos;
        if (hwFv1 != null)
        {
            // Spawn 2.5m di depan handwheel, sedikit ke kanan supaya bisa lihat 3 handwheel.
            pos = new Vector3(-27.6f, 0.75f, 108.0f);
        }
        else
        {
            pos = new Vector3(-27.6f, 0.75f, 108.0f);
        }

        var sp = new GameObject("SpawnPoint_Lvl8_FlashTrain_Runtime");
        sp.transform.position = pos;
        // Hadap ke handwheel (Z+ direction)
        sp.transform.rotation = Quaternion.LookRotation(Vector3.forward, Vector3.up);
        return sp.transform;
    }

    private Transform FindByNameInactive(string name)
    {
        foreach (Transform t in FindObjectsByType<Transform>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            if (t != null && t.name == name && t.gameObject.scene.IsValid()) return t;
        return null;
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
        _steamReleaseAudio.spatialBlend = 0.4f;
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

    // ============================================================
    //  PUBLIC (Debug)
    // ============================================================

    public bool LevelActive => _levelActive;
    public string CurrentPhase => _phase.ToString();
    public float Fv1Pressure => _fv1.pressureCurrent;
    public float Fv2Pressure => _fv2.pressureCurrent;
    public float Fv3Pressure => _fv3.pressureCurrent;

    [ContextMenu("Debug: Force Activate Level 8")]
    public void DebugActivate() => ActivateLevel();

    [ContextMenu("Debug: Skip to Sampling")]
    public void DebugSkipToSampling()
    {
        _fv1.openPercent = 1f; _fv1.isStable = true; _fv1.pressureCurrent = _fv1.pressureTarget; _fv1.tempCurrent = _fv1.tempTarget;
        _fv2.openPercent = 1f; _fv2.isStable = true; _fv2.pressureCurrent = _fv2.pressureTarget; _fv2.tempCurrent = _fv2.tempTarget;
        _fv3.openPercent = 1f; _fv3.isStable = true; _fv3.pressureCurrent = _fv3.pressureTarget; _fv3.tempCurrent = _fv3.tempTarget;
        SetStatusStripColor(_fv1StatusStrip, Color.green);
        SetStatusStripColor(_fv2StatusStrip, Color.green);
        SetStatusStripColor(_fv3StatusStrip, Color.green);
        _phase = Phase.Sampling;
        if (_hud != null) _hud.ShowNotifPublic(_msgFv3Done, 6f);
    }
}
