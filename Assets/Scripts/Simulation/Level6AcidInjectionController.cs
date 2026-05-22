using System.Collections;
using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit.Interactables;
using TMPro;

/// <summary>
/// OLIVIA VR - Level6AcidInjectionController.cs (v2.0)
///
/// LEVEL 6 FLOW:
///   Phase 1 (Field): Player watches slurry flow from PreHeater through pipe into Autoclave
///   Phase 2 (Field): Slurry arrives at Autoclave → HUD prompt to return to DCS
///   Phase 3 (DCS):   Player presses DCS button 6, adjusts acid ratio to 350 kg/ton
///   Phase 4 (DCS):   pH drops to 1.0 → quest complete → report via WT
///   Phase 5:         Fade → teleport to field for Level 7 (Autoclave monitoring)
///
/// Two separate systems:
///   A) Slurry liquid fills pipe from PreHeater outlet to Autoclave inlet (physics wobble)
///   B) Acid Injection: separate tank + dosing pump → injects into Autoclave via Pipe_Inlet_Acid
/// </summary>
public class Level6AcidInjectionController : MonoBehaviour
{
    [Header("=== Player Reference ===")]
    [SerializeField] private Transform _playerRigRoot;

    [Header("=== Teleport Targets ===")]
    [SerializeField] private Transform _teleportTargetField;
    [SerializeField] private Transform _teleportTargetDcs;

    [Header("=== Liquid Flow: PreHeater → Autoclave ===")]
    [Tooltip("Pipe segment(s) that liquid fills. Auto-find 'Pipe_PreheaterToAutoclave' children.")]
    [SerializeField] private Transform[] _pipeSegments;
    [Tooltip("Duration for liquid to travel from PreHeater to Autoclave (seconds).")]
    [SerializeField] private float _liquidFlowDuration = 12f;
    [SerializeField] private AnimationCurve _liquidFlowCurve = AnimationCurve.EaseInOut(0f, 0f, 1f, 1f);
    [Tooltip("Liquid fill diameter relative to pipe (0.95 = nearly full).")]
    [Range(0.5f, 0.99f)] [SerializeField] private float _liquidDiameter = 0.92f;

    [Header("=== Acid Injection Controls (DCS) ===")]
    [SerializeField] private XRSimpleInteractable _btnAcidPlus;
    [SerializeField] private XRSimpleInteractable _btnAcidMinus;
    [SerializeField] private TextMeshProUGUI _displayAcidRatio;
    [SerializeField] private TextMeshProUGUI _displayPH;
    [SerializeField] private TextMeshProUGUI _displayStatus;

    [Header("=== Acid Parameters ===")]
    [SerializeField] private float _acidRatioTarget = 350f;
    [SerializeField] private float _acidRatioTolerance = 10f;
    [SerializeField] private float _acidRatioMax = 500f;
    [SerializeField] private float _acidStepPerClick = 10f;
    [SerializeField] private float _phStart = 5.0f;
    [SerializeField] private float _phTarget = 1.0f;

    [Header("=== Visual: pH Beaker Hologram ===")]
    [SerializeField] private Renderer _beakerRenderer;
    [SerializeField] private bool _autoCreateBeaker = true;
    [SerializeField] private Color _colorPH7 = new Color(0.2f, 0.8f, 0.3f, 0.9f);
    [SerializeField] private Color _colorPH4 = new Color(0.9f, 0.8f, 0.1f, 0.9f);
    [SerializeField] private Color _colorPH2 = new Color(0.95f, 0.5f, 0.1f, 0.9f);
    [SerializeField] private Color _colorPH1 = new Color(0.95f, 0.15f, 0.1f, 0.9f);

    [Header("=== Audio ===")]
    [SerializeField] private AudioSource _liquidFlowAudio;
    [SerializeField] private AudioSource _acidPumpAudio;
    [Range(0f, 1f)] [SerializeField] private float _liquidFlowVolume = 0.5f;
    [Range(0f, 1f)] [SerializeField] private float _acidPumpVolume = 0.4f;

    [Header("=== Timing ===")]
    [SerializeField] private float _fadeTransitionDuration = 2.5f;
    [SerializeField] private float _delayBeforeDcsTeleport = 2f;
    [SerializeField] private float _delayAfterReport = 2f;

    [Header("=== HUD Messages ===")]
    [TextArea(2, 4)] [SerializeField] private string _msgWatchFlow =
        "Watch the heated slurry flow from Pre-Heater into the Autoclave.";
    [TextArea(2, 4)] [SerializeField] private string _msgSlurryArrived =
        "Slurry has entered the Autoclave. Return to DCS to inject acid.";
    [TextArea(2, 4)] [SerializeField] private string _msgAdjustAcid =
        "Press DCS button 6, then adjust acid ratio to 350 kg/ton.";
    [TextArea(2, 4)] [SerializeField] private string _msgAcidComplete =
        "Acid injection optimal! Report via WT: 'acid aktif, rasio 350 kg per ton, pH 1.0'.";

    // Runtime state
    private float _acidRatioCurrent;
    private float _phCurrent;
    private bool _slurryArrivedAtAutoclave;
    private bool _acidQuestComplete;
    private PlayerHUD _hud;
    private Coroutine _sequenceCoroutine;
    private GameObject[] _liquidFillObjects;
    private GameObject _beakerRuntime;
    private MaterialPropertyBlock _mpb;

    private void Awake()
    {
        _hud = FindObjectOfType<PlayerHUD>();
        _mpb = new MaterialPropertyBlock();
        _acidRatioCurrent = 0f;
        _phCurrent = _phStart;
        AutoFindReferences();
        SetupAcidButtons();
    }

    private void OnEnable()
    {
        GameLevelManager.OnLevelStarted += OnLevelStarted;
    }

    private void OnDisable()
    {
        GameLevelManager.OnLevelStarted -= OnLevelStarted;
        if (_sequenceCoroutine != null) { StopCoroutine(_sequenceCoroutine); _sequenceCoroutine = null; }
    }

    private void OnLevelStarted(GameLevelManager.GameLevel level)
    {
        if (level == GameLevelManager.GameLevel.Level6_AcidInjection)
        {
            _acidRatioCurrent = 0f;
            _phCurrent = _phStart;
            _slurryArrivedAtAutoclave = false;
            _acidQuestComplete = false;
            HideLiquidFills();
            HideBeaker();
            // Start field observation sequence
            _sequenceCoroutine = StartCoroutine(SequenceSlurryFlowToAutoclave());
        }
        else
        {
            HideLiquidFills();
            HideBeaker();
            StopAudio(_liquidFlowAudio);
            StopAudio(_acidPumpAudio);
        }
    }

    // ============================================================
    //  PHASE 1-2: SLURRY FLOW FROM PREHEATER TO AUTOCLAVE
    // ============================================================

    private IEnumerator SequenceSlurryFlowToAutoclave()
    {
        // Teleport player to field observation point
        yield return new WaitForSeconds(0.5f);
        if (_hud != null) _hud.ShowNotifPublic(_msgWatchFlow);

        // Start liquid flow audio
        EnsureLiquidFlowAudio();
        StartAudio(_liquidFlowAudio, _liquidFlowVolume);

        // Animate liquid filling each pipe segment sequentially
        yield return StartCoroutine(AnimateLiquidFillPipes());

        // Liquid arrived at Autoclave
        _slurryArrivedAtAutoclave = true;
        StopAudio(_liquidFlowAudio);

        if (_hud != null) _hud.ShowNotifPublic(_msgSlurryArrived);
        yield return new WaitForSeconds(_delayBeforeDcsTeleport);

        // Fade & teleport to DCS for acid injection
        if (_hud != null) _hud.PlayManualFade(_fadeTransitionDuration);
        yield return new WaitForSeconds(_fadeTransitionDuration * 0.5f);
        TeleportPlayer(_teleportTargetDcs);
        yield return new WaitForSeconds(_fadeTransitionDuration * 0.5f);

        // Show acid injection prompt
        if (_hud != null) _hud.ShowNotifPublic(_msgAdjustAcid);
        ShowBeaker();
        _sequenceCoroutine = null;
    }

    private IEnumerator AnimateLiquidFillPipes()
    {
        if (_pipeSegments == null || _pipeSegments.Length == 0)
        {
            // Auto-find pipe segments
            var pipeParent = GameObject.Find("Mesin Utama/Pipe_PreheaterToAutoclave");
            if (pipeParent != null)
            {
                _pipeSegments = new Transform[pipeParent.transform.childCount];
                for (int i = 0; i < pipeParent.transform.childCount; i++)
                    _pipeSegments[i] = pipeParent.transform.GetChild(i);
            }
        }

        if (_pipeSegments == null || _pipeSegments.Length == 0)
        {
            Debug.LogWarning("[Level6] No pipe segments found for liquid animation.");
            yield return new WaitForSeconds(_liquidFlowDuration);
            yield break;
        }

        // Create liquid fill cylinders for each pipe segment
        EnsureLiquidFillObjects();

        float durationPerSegment = _liquidFlowDuration / _pipeSegments.Length;

        for (int i = 0; i < _pipeSegments.Length; i++)
        {
            var pipe = _pipeSegments[i];
            var liquid = _liquidFillObjects[i];
            if (pipe == null || liquid == null) continue;

            liquid.transform.SetParent(pipe, false);
            liquid.transform.localPosition = new Vector3(0f, -1f, 0f);
            liquid.transform.localRotation = Quaternion.identity;
            liquid.transform.localScale = new Vector3(_liquidDiameter, 0.001f, _liquidDiameter);
            liquid.SetActive(true);

            var wobble = liquid.GetComponent<PipeFlowAnimator>();

            float elapsed = 0f;
            while (elapsed < durationPerSegment)
            {
                elapsed += Time.deltaTime;
                float t = Mathf.Clamp01(elapsed / durationPerSegment);
                float curveT = _liquidFlowCurve.Evaluate(t);

                float scaleY = Mathf.Lerp(0.001f, 1f, curveT);
                Vector3 baseScale = new Vector3(_liquidDiameter, scaleY, _liquidDiameter);
                Vector3 basePos = new Vector3(0f, -1f + scaleY, 0f);
                liquid.transform.localScale = baseScale;
                liquid.transform.localPosition = basePos;

                if (wobble != null)
                {
                    wobble.UpdateBaseScale(baseScale);
                    wobble.UpdateBasePosition(basePos);
                }

                yield return null;
            }

            // Final state for this segment
            liquid.transform.localScale = new Vector3(_liquidDiameter, 1f, _liquidDiameter);
            liquid.transform.localPosition = Vector3.zero;
            if (wobble != null)
            {
                wobble.UpdateBaseScale(new Vector3(_liquidDiameter, 1f, _liquidDiameter));
                wobble.UpdateBasePosition(Vector3.zero);
            }
        }
    }

    // ============================================================
    //  PHASE 3-4: ACID INJECTION (DCS)
    // ============================================================

    public void IncreaseAcidRatio()
    {
        if (!_slurryArrivedAtAutoclave) return;
        if (GameLevelManager.Instance != null &&
            GameLevelManager.Instance.CurrentLevel != GameLevelManager.GameLevel.Level6_AcidInjection) return;

        _acidRatioCurrent = Mathf.Clamp(_acidRatioCurrent + _acidStepPerClick, 0f, _acidRatioMax);
        OnAcidRatioChanged();
    }

    public void DecreaseAcidRatio()
    {
        if (!_slurryArrivedAtAutoclave) return;
        if (GameLevelManager.Instance != null &&
            GameLevelManager.Instance.CurrentLevel != GameLevelManager.GameLevel.Level6_AcidInjection) return;

        _acidRatioCurrent = Mathf.Clamp(_acidRatioCurrent - _acidStepPerClick, 0f, _acidRatioMax);
        OnAcidRatioChanged();
    }

    private void OnAcidRatioChanged()
    {
        float t = Mathf.Clamp01(_acidRatioCurrent / _acidRatioTarget);
        _phCurrent = Mathf.Lerp(_phStart, _phTarget, t);

        if (GameLevelManager.Instance != null)
        {
            GameLevelManager.Instance.SetAcidRatio(_acidRatioCurrent);
            GameLevelManager.Instance.SetPH(_phCurrent);
        }

        UpdateAcidDisplay();
        UpdateBeakerColor();
        UpdateAcidAudio();
        CheckAcidQuest();
    }

    private void UpdateAcidDisplay()
    {
        if (_displayAcidRatio != null)
            _displayAcidRatio.text = _acidRatioCurrent.ToString("F0") + " kg/ton";
        if (_displayPH != null)
            _displayPH.text = "pH " + _phCurrent.ToString("F1");
        if (_displayStatus != null)
        {
            bool onTarget = Mathf.Abs(_acidRatioCurrent - _acidRatioTarget) <= _acidRatioTolerance;
            _displayStatus.text = onTarget ? "TARGET SOP" : (_acidRatioCurrent < _acidRatioTarget ? "Increase dose" : "Decrease dose");
            _displayStatus.color = onTarget ? Color.green : Color.yellow;
        }
    }

    private void UpdateBeakerColor()
    {
        if (_beakerRenderer == null) return;
        Color c;
        if (_phCurrent >= 4f) c = Color.Lerp(_colorPH4, _colorPH7, Mathf.InverseLerp(4f, 7f, _phCurrent));
        else if (_phCurrent >= 2f) c = Color.Lerp(_colorPH2, _colorPH4, Mathf.InverseLerp(2f, 4f, _phCurrent));
        else c = Color.Lerp(_colorPH1, _colorPH2, Mathf.InverseLerp(1f, 2f, _phCurrent));

        _beakerRenderer.GetPropertyBlock(_mpb);
        _mpb.SetColor("_BaseColor", c);
        _mpb.SetColor("_EmissionColor", c * 1.2f);
        _beakerRenderer.SetPropertyBlock(_mpb);
    }

    private void CheckAcidQuest()
    {
        if (_acidQuestComplete) return;
        if (Mathf.Abs(_acidRatioCurrent - _acidRatioTarget) <= _acidRatioTolerance && _phCurrent <= 1.1f)
        {
            _acidQuestComplete = true;
            if (_hud != null) _hud.ShowNotifPublic(_msgAcidComplete);
            Debug.Log("[Level6] Acid injection target reached. Ratio=" + _acidRatioCurrent + " pH=" + _phCurrent);
        }
    }

    // ============================================================
    //  LIQUID FILL OBJECTS
    // ============================================================

    private void EnsureLiquidFillObjects()
    {
        if (_liquidFillObjects != null && _liquidFillObjects.Length > 0) return;
        if (_pipeSegments == null) return;

        _liquidFillObjects = new GameObject[_pipeSegments.Length];
        // Get slurry material from tank (purple)
        Material slurryMat = GetSlurryMaterial();

        for (int i = 0; i < _pipeSegments.Length; i++)
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            go.name = "L6_LiquidFill_" + i;
            var col = go.GetComponent<Collider>();
            if (col != null) DestroyImmediate(col);
            go.GetComponent<MeshRenderer>().sharedMaterial = slurryMat;
            go.AddComponent<PipeFlowAnimator>();
            go.SetActive(false);
            _liquidFillObjects[i] = go;
        }
    }

    private Material GetSlurryMaterial()
    {
        // Try to get from Slurry_Fill in scene (purple material)
        var tankFill = GameObject.Find("Mesin Utama/Slurry Tank/Slurry_Fill");
        if (tankFill != null)
        {
            var mr = tankFill.GetComponent<MeshRenderer>();
            if (mr != null && mr.sharedMaterial != null) return mr.sharedMaterial;
        }
        // Fallback: create purple slurry material
        var mat = new Material(Shader.Find("Universal Render Pipeline/Lit"));
        mat.SetColor("_BaseColor", new Color(0.42f, 0.18f, 0.55f, 0.95f));
        mat.SetFloat("_Smoothness", 0.7f);
        mat.EnableKeyword("_EMISSION");
        mat.SetColor("_EmissionColor", new Color(0.42f, 0.18f, 0.55f) * 0.5f);
        return mat;
    }

    private void HideLiquidFills()
    {
        if (_liquidFillObjects == null) return;
        foreach (var go in _liquidFillObjects)
            if (go != null) go.SetActive(false);
    }

    // ============================================================
    //  BEAKER HOLOGRAM
    // ============================================================

    private void ShowBeaker()
    {
        EnsureBeaker();
        if (_beakerRuntime != null) _beakerRuntime.SetActive(true);
        UpdateBeakerColor();
    }

    private void HideBeaker()
    {
        if (_beakerRuntime != null) _beakerRuntime.SetActive(false);
    }

    private void EnsureBeaker()
    {
        if (_beakerRenderer != null) return;
        if (!_autoCreateBeaker) return;

        _beakerRuntime = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        _beakerRuntime.name = "L6_AcidBeaker";
        var col = _beakerRuntime.GetComponent<Collider>();
        if (col != null) DestroyImmediate(col);

        var dcsCanvas = GameObject.Find("DCS_Monitor_Canvas");
        Vector3 pos = dcsCanvas != null
            ? dcsCanvas.transform.position + new Vector3(1.5f, -0.5f, 0f)
            : new Vector3(-0.5f, 10f, 17.5f);
        _beakerRuntime.transform.position = pos;
        _beakerRuntime.transform.localScale = new Vector3(0.25f, 0.4f, 0.25f);

        var mat = new Material(Shader.Find("Universal Render Pipeline/Lit"));
        mat.SetFloat("_Surface", 1f);
        mat.SetOverrideTag("RenderType", "Transparent");
        mat.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.SrcAlpha);
        mat.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
        mat.SetInt("_ZWrite", 0);
        mat.renderQueue = 3000;
        mat.SetColor("_BaseColor", _colorPH7);
        mat.EnableKeyword("_EMISSION");
        mat.SetColor("_EmissionColor", _colorPH7 * 1.2f);
        _beakerRuntime.GetComponent<MeshRenderer>().sharedMaterial = mat;
        _beakerRenderer = _beakerRuntime.GetComponent<MeshRenderer>();
        _beakerRuntime.SetActive(false);
    }

    // ============================================================
    //  AUDIO
    // ============================================================

    private void EnsureLiquidFlowAudio()
    {
        if (_liquidFlowAudio != null) return;
        _liquidFlowAudio = gameObject.AddComponent<AudioSource>();
        _liquidFlowAudio.spatialBlend = 0f;
        _liquidFlowAudio.loop = true;
        _liquidFlowAudio.playOnAwake = false;
        _liquidFlowAudio.volume = 0f;
        _liquidFlowAudio.clip = GenerateFlowSound(4f, 22050);
    }

    private void UpdateAcidAudio()
    {
        if (_acidPumpAudio == null)
        {
            _acidPumpAudio = gameObject.AddComponent<AudioSource>();
            _acidPumpAudio.spatialBlend = 0f;
            _acidPumpAudio.loop = true;
            _acidPumpAudio.playOnAwake = false;
            _acidPumpAudio.clip = GenerateAcidPumpSound(3f, 22050);
        }
        float t = Mathf.Clamp01(_acidRatioCurrent / _acidRatioMax);
        if (t > 0.01f)
        {
            _acidPumpAudio.volume = _acidPumpVolume * t;
            _acidPumpAudio.pitch = Mathf.Lerp(0.7f, 1.1f, t);
            if (!_acidPumpAudio.isPlaying) _acidPumpAudio.Play();
        }
        else
        {
            _acidPumpAudio.volume = 0f;
        }
    }

    private void StartAudio(AudioSource src, float volume)
    {
        if (src == null) return;
        src.volume = volume;
        src.Play();
    }

    private void StopAudio(AudioSource src)
    {
        if (src != null && src.isPlaying) src.Stop();
    }

    private AudioClip GenerateFlowSound(float duration, int sampleRate)
    {
        int total = Mathf.CeilToInt(duration * sampleRate);
        float[] data = new float[total];
        System.Random rnd = new System.Random(55);
        float lp = 0f;
        for (int i = 0; i < total; i++)
        {
            float noise = ((float)rnd.NextDouble() - 0.5f) * 2f;
            lp += 0.06f * (noise - lp);
            float bass = Mathf.Sin(2f * Mathf.PI * 60f * i / sampleRate) * 0.2f;
            data[i] = (lp * 0.5f + bass) * 0.4f;
        }
        int fade = Mathf.Min(2000, total / 20);
        for (int i = 0; i < fade; i++) { float f = (float)i / fade; data[i] *= f; data[total - 1 - i] *= f; }
        var clip = AudioClip.Create("FlowSound", total, 1, sampleRate, false);
        clip.SetData(data, 0);
        return clip;
    }

    private AudioClip GenerateAcidPumpSound(float duration, int sampleRate)
    {
        int total = Mathf.CeilToInt(duration * sampleRate);
        float[] data = new float[total];
        System.Random rnd = new System.Random(42);
        float phase = 0f;
        for (int i = 0; i < total; i++)
        {
            phase += 2f * Mathf.PI * 120f / sampleRate;
            float sine = Mathf.Sin(phase) * 0.3f;
            float noise = ((float)rnd.NextDouble() - 0.5f) * 0.3f;
            float bubble = Mathf.Abs(Mathf.Sin(phase * 0.07f)) * 0.5f + 0.5f;
            data[i] = (sine + noise * 0.5f) * bubble * 0.35f;
        }
        int fade = Mathf.Min(1500, total / 20);
        for (int i = 0; i < fade; i++) { float f = (float)i / fade; data[i] *= f; data[total - 1 - i] *= f; }
        var clip = AudioClip.Create("AcidPumpSound", total, 1, sampleRate, false);
        clip.SetData(data, 0);
        return clip;
    }

    // ============================================================
    //  TELEPORT (uses XROrigin API)
    // ============================================================

    private void TeleportPlayer(Transform target)
    {
        if (target == null) return;
        if (_playerRigRoot == null) AutoFindReferences();
        if (_playerRigRoot == null) return;

        var xrOrigin = _playerRigRoot.GetComponent<Unity.XR.CoreUtils.XROrigin>();
        var cc = _playerRigRoot.GetComponent<CharacterController>();
        bool ccEnabled = cc != null && cc.enabled;
        if (ccEnabled) cc.enabled = false;

        if (xrOrigin != null)
        {
            Vector3 camTarget = target.position + Vector3.up * xrOrigin.CameraYOffset;
            xrOrigin.MoveCameraToWorldLocation(camTarget);
            xrOrigin.MatchOriginUpCameraForward(Vector3.up, target.forward);
        }
        else
        {
            _playerRigRoot.SetPositionAndRotation(target.position, target.rotation);
        }

        if (ccEnabled) cc.enabled = true;
    }

    // ============================================================
    //  AUTO-FIND
    // ============================================================

    private void AutoFindReferences()
    {
        if (_playerRigRoot == null)
        {
            var rig = GameObject.Find("XR Origin (XR Rig)") ?? GameObject.FindWithTag("Player");
            if (rig != null) _playerRigRoot = rig.transform;
        }
        if (_teleportTargetField == null)
        {
            var go = GameObject.Find("SpawnPoint_Lvl6");
            if (go != null) _teleportTargetField = go.transform;
        }
        if (_teleportTargetDcs == null)
        {
            var go = GameObject.Find("SpawnPoint_DCS");
            if (go != null) _teleportTargetDcs = go.transform;
        }
        if (_btnAcidPlus == null)
        {
            var go = GameObject.Find("Btn_AcidPlus");
            if (go != null) _btnAcidPlus = go.GetComponent<XRSimpleInteractable>();
        }
        if (_btnAcidMinus == null)
        {
            var go = GameObject.Find("Btn_AcidMinus");
            if (go != null) _btnAcidMinus = go.GetComponent<XRSimpleInteractable>();
        }
    }

    private void SetupAcidButtons()
    {
        if (_btnAcidPlus != null)
            _btnAcidPlus.selectEntered.AddListener(_ => IncreaseAcidRatio());
        if (_btnAcidMinus != null)
            _btnAcidMinus.selectEntered.AddListener(_ => DecreaseAcidRatio());
    }

    // ============================================================
    //  PUBLIC API
    // ============================================================

    public float AcidRatioCurrent => _acidRatioCurrent;
    public float PHCurrent => _phCurrent;
    public bool SlurryArrivedAtAutoclave => _slurryArrivedAtAutoclave;
    public bool AcidQuestComplete => _acidQuestComplete;
}
