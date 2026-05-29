using System.Collections;
using TMPro;
using Unity.XR.CoreUtils;
using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit.Interactables;

/// <summary>
/// OLIVIA VR - Level8MonitoringController.cs
///
/// Level 8 turns the post-autoclave handoff into an active DCS stabilization task.
/// The player presses DCS button 8, watches unstable reactor parameters, corrects
/// temperature, pressure, and agitator RPM, then reports only after SOP is stable.
/// </summary>
public class Level8MonitoringController : MonoBehaviour
{
    [Header("=== Teleport ===")]
    [SerializeField] private Transform _playerRigRoot;
    [SerializeField] private Transform _teleportTargetDcs;
    [SerializeField] private float _teleportDelay = 0.4f;

    [Header("=== Starting Upset ===")]
    [SerializeField] private float _temperatureCurrent = 260f;
    [SerializeField] private float _pressureCurrent = 52.5f;
    [SerializeField] private float _rpmCurrent = 54f;

    [Header("=== SOP Target ===")]
    [SerializeField] private float _temperatureTarget = 252f;
    [SerializeField] private float _temperatureTolerance = 3f;
    [SerializeField] private float _pressureTarget = 47.5f;
    [SerializeField] private float _pressureTolerance = 2f;
    [SerializeField] private float _rpmTarget = 60f;
    [SerializeField] private float _rpmTolerance = 3f;
    [SerializeField] private float _stableHoldDuration = 5f;

    [Header("=== Correction Step ===")]
    [SerializeField] private float _temperatureStep = 1f;
    [SerializeField] private float _pressureStep = 0.5f;
    [SerializeField] private float _rpmStep = 2f;
    [SerializeField] private float _naturalDriftInterval = 1.2f;

    [Header("=== Optional XR Buttons ===")]
    [SerializeField] private XRSimpleInteractable _btnCoolerOpen;
    [SerializeField] private XRSimpleInteractable _btnSteamTrim;
    [SerializeField] private XRSimpleInteractable _btnVentOpen;
    [SerializeField] private XRSimpleInteractable _btnPressureTrim;
    [SerializeField] private XRSimpleInteractable _btnRpmUp;
    [SerializeField] private XRSimpleInteractable _btnRpmDown;

    [Header("=== DCS Readout ===")]
    [SerializeField] private TextMeshPro _readoutText;
    [SerializeField] private Renderer _statusLampRenderer;
    [SerializeField] private Material _lampWarningMaterial;
    [SerializeField] private Material _lampStableMaterial;
    [SerializeField] private Material _lampDangerMaterial;

    [Header("=== Audio ===")]
    [SerializeField] private AudioSource _alarmAudio;
    [SerializeField] private AudioSource _stableAudio;
    [Range(0f, 1f)] [SerializeField] private float _alarmVolume = 0.32f;
    [Range(0f, 1f)] [SerializeField] private float _stableVolume = 0.35f;

    [Header("=== HUD Messages ===")]
    [TextArea(2, 4)] [SerializeField] private string _msgStart =
        "Level 8: Autoclave mulai drift. Tekan tombol DCS 8 untuk masuk mode stabilisasi.";
    [TextArea(2, 4)] [SerializeField] private string _msgCorrection =
        "Stabilkan parameter: suhu 252 C, tekanan 47.5 atm, agitator 60 RPM.";
    [TextArea(2, 4)] [SerializeField] private string _msgStable =
        "Parameter stabil. Lapor HT: 'parameter stabil'.";

    private PlayerHUD _hud;
    private Coroutine _driftCoroutine;
    private float _stableTimer;
    private bool _levelActive;
    private bool _correctionActive;
    private bool _questComplete;

    private void Awake()
    {
        _hud = FindFirstObjectByType<PlayerHUD>();
        AutoFindReferences();
        SetupButtons();
        EnsureReadout();
        EnsureMaterials();
        EnsureAudio();
        SetReadoutVisible(false);
    }

    private void OnEnable()
    {
        GameLevelManager.OnLevelStarted += OnLevelStarted;
        GameLevelManager.OnDCSButtonPressed += OnDcsButtonPressed;
    }

    private void OnDisable()
    {
        GameLevelManager.OnLevelStarted -= OnLevelStarted;
        GameLevelManager.OnDCSButtonPressed -= OnDcsButtonPressed;
        StopDrift();
        StopAudio(_alarmAudio);
        StopAudio(_stableAudio);
    }

    private void OnLevelStarted(GameLevelManager.GameLevel level)
    {
        _levelActive = level == GameLevelManager.GameLevel.Level8_Monitoring;
        if (!_levelActive)
        {
            _correctionActive = false;
            SetReadoutVisible(false);
            StopDrift();
            StopAudio(_alarmAudio);
            StopAudio(_stableAudio);
            return;
        }

        _temperatureCurrent = 260f;
        _pressureCurrent = 52.5f;
        _rpmCurrent = 54f;
        _stableTimer = 0f;
        _correctionActive = false;
        _questComplete = false;
        PushParametersToManager();
        SetReadoutVisible(true);
        UpdateReadout();
        SetLampDanger();

        if (_hud != null)
            _hud.ShowNotifPublic(_msgStart);

        StartCoroutine(TeleportToDcsDelayed());
    }

    private void Update()
    {
        if (!_levelActive || !_correctionActive || _questComplete)
            return;

        HandleKeyboardTestInput();
        UpdateStabilityTimer();
        UpdateReadout();
    }

    private void OnDcsButtonPressed(int number)
    {
        if (!_levelActive || number != 8)
            return;

        _correctionActive = true;
        StartDrift();
        StartAudio(_alarmAudio, _alarmVolume);

        if (_hud != null)
            _hud.ShowNotifPublic(_msgCorrection);

        UpdateReadout();
    }

    public void OpenCooler()
    {
        if (!CanCorrect()) return;
        _temperatureCurrent = Mathf.Clamp(_temperatureCurrent - _temperatureStep, 230f, 280f);
        ApplyCorrectionFeedback();
    }

    public void TrimSteam()
    {
        if (!CanCorrect()) return;
        _temperatureCurrent = Mathf.Clamp(_temperatureCurrent + _temperatureStep, 230f, 280f);
        ApplyCorrectionFeedback();
    }

    public void OpenVent()
    {
        if (!CanCorrect()) return;
        _pressureCurrent = Mathf.Clamp(_pressureCurrent - _pressureStep, 35f, 70f);
        ApplyCorrectionFeedback();
    }

    public void TrimPressure()
    {
        if (!CanCorrect()) return;
        _pressureCurrent = Mathf.Clamp(_pressureCurrent + _pressureStep, 35f, 70f);
        ApplyCorrectionFeedback();
    }

    public void IncreaseRpm()
    {
        if (!CanCorrect()) return;
        _rpmCurrent = Mathf.Clamp(_rpmCurrent + _rpmStep, 30f, 90f);
        ApplyCorrectionFeedback();
    }

    public void DecreaseRpm()
    {
        if (!CanCorrect()) return;
        _rpmCurrent = Mathf.Clamp(_rpmCurrent - _rpmStep, 30f, 90f);
        ApplyCorrectionFeedback();
    }

    private bool CanCorrect()
    {
        return _levelActive && _correctionActive && !_questComplete;
    }

    private void ApplyCorrectionFeedback()
    {
        PushParametersToManager();
        UpdateReadout();
    }

    private void HandleKeyboardTestInput()
    {
        if (Input.GetKeyDown(KeyCode.Alpha1)) OpenCooler();
        if (Input.GetKeyDown(KeyCode.Alpha2)) TrimSteam();
        if (Input.GetKeyDown(KeyCode.Alpha3)) OpenVent();
        if (Input.GetKeyDown(KeyCode.Alpha4)) TrimPressure();
        if (Input.GetKeyDown(KeyCode.Alpha5)) IncreaseRpm();
        if (Input.GetKeyDown(KeyCode.Alpha6)) DecreaseRpm();
    }

    private void UpdateStabilityTimer()
    {
        if (ParametersInsideSop())
        {
            _stableTimer += Time.deltaTime;
            SetLampStable();
        }
        else
        {
            _stableTimer = 0f;
            SetLampDanger();
        }

        if (_stableTimer >= _stableHoldDuration)
            CompleteQuest();
    }

    private void CompleteQuest()
    {
        if (_questComplete)
            return;

        _questComplete = true;
        _correctionActive = false;
        StopDrift();
        StopAudio(_alarmAudio);
        StartAudio(_stableAudio, _stableVolume);
        PushParametersToManager();
        UpdateReadout();
        SetLampStable();

        if (_hud != null)
            _hud.ShowNotifPublic(_msgStable);

        Debug.Log("[Level8] Monitoring parameters stable. Player can report via WT.");
    }

    private bool ParametersInsideSop()
    {
        bool tempOk = Mathf.Abs(_temperatureCurrent - _temperatureTarget) <= _temperatureTolerance;
        bool pressureOk = Mathf.Abs(_pressureCurrent - _pressureTarget) <= _pressureTolerance;
        bool rpmOk = Mathf.Abs(_rpmCurrent - _rpmTarget) <= _rpmTolerance;
        return tempOk && pressureOk && rpmOk;
    }

    private void PushParametersToManager()
    {
        if (GameLevelManager.Instance == null)
            return;

        GameLevelManager.Instance.SetSuhu(_temperatureCurrent);
        GameLevelManager.Instance.SetTekanan(_pressureCurrent);
        GameLevelManager.Instance.SetRPM(_rpmCurrent);
    }

    private void StartDrift()
    {
        if (_driftCoroutine != null)
            StopCoroutine(_driftCoroutine);

        _driftCoroutine = StartCoroutine(DriftLoop());
    }

    private void StopDrift()
    {
        if (_driftCoroutine == null)
            return;

        StopCoroutine(_driftCoroutine);
        _driftCoroutine = null;
    }

    private IEnumerator DriftLoop()
    {
        while (_levelActive && _correctionActive && !_questComplete)
        {
            yield return new WaitForSeconds(_naturalDriftInterval);

            if (!ParametersInsideSop())
            {
                _temperatureCurrent = Mathf.Clamp(_temperatureCurrent + 0.18f, 230f, 280f);
                _pressureCurrent = Mathf.Clamp(_pressureCurrent + 0.08f, 35f, 70f);
                _rpmCurrent = Mathf.Clamp(_rpmCurrent - 0.12f, 30f, 90f);
                PushParametersToManager();
                UpdateReadout();
            }
        }

        _driftCoroutine = null;
    }

    private IEnumerator TeleportToDcsDelayed()
    {
        yield return new WaitForSeconds(_teleportDelay);
        TeleportPlayer(_teleportTargetDcs);
    }

    private void TeleportPlayer(Transform target)
    {
        if (target == null)
            return;

        if (_playerRigRoot == null)
            AutoFindReferences();

        if (_playerRigRoot == null)
            return;

        XROrigin xrOrigin = _playerRigRoot.GetComponent<XROrigin>();
        if (xrOrigin == null)
        {
            Debug.LogWarning("[Level8] XROrigin component not found. Teleport skipped to avoid tracker snapback.");
            return;
        }

        CharacterController controller = _playerRigRoot.GetComponent<CharacterController>();
        bool controllerWasEnabled = controller != null && controller.enabled;
        if (controllerWasEnabled)
            controller.enabled = false;

        Vector3 cameraTarget = target.position + Vector3.up * xrOrigin.CameraYOffset;
        xrOrigin.MoveCameraToWorldLocation(cameraTarget);
        xrOrigin.MatchOriginUpCameraForward(Vector3.up, target.forward);

        if (controllerWasEnabled)
            controller.enabled = true;
    }

    private void EnsureReadout()
    {
        if (_readoutText != null)
            return;

        GameObject anchor = GameObject.Find("DCS_Monitor_Canvas") ?? GameObject.Find("SpawnPoint_DCS");
        Vector3 position = anchor != null
            ? anchor.transform.position + anchor.transform.forward * 0.12f + Vector3.up * 0.45f
            : new Vector3(-2.12f, 9.4f, 16.9f);
        Quaternion rotation = anchor != null
            ? anchor.transform.rotation
            : Quaternion.Euler(0f, 180f, 0f);

        GameObject panel = new GameObject("Level8_Monitoring_Readout");
        panel.transform.SetPositionAndRotation(position, rotation);
        panel.transform.localScale = Vector3.one;

        GameObject bg = GameObject.CreatePrimitive(PrimitiveType.Quad);
        bg.name = "Readout_Backplate";
        bg.transform.SetParent(panel.transform, false);
        bg.transform.localPosition = new Vector3(0f, 0f, 0.01f);
        bg.transform.localScale = new Vector3(1.7f, 0.95f, 1f);
        Collider bgCollider = bg.GetComponent<Collider>();
        if (bgCollider != null)
            Destroy(bgCollider);

        Renderer bgRenderer = bg.GetComponent<Renderer>();
        Material bgMaterial = new Material(Shader.Find("Universal Render Pipeline/Lit"));
        bgMaterial.SetColor("_BaseColor", new Color(0.02f, 0.05f, 0.07f, 0.96f));
        bgMaterial.EnableKeyword("_EMISSION");
        bgMaterial.SetColor("_EmissionColor", new Color(0.0f, 0.08f, 0.12f));
        bgRenderer.sharedMaterial = bgMaterial;

        GameObject text = new GameObject("Readout_Text");
        text.transform.SetParent(panel.transform, false);
        text.transform.localPosition = new Vector3(-0.78f, 0.33f, 0f);
        text.transform.localRotation = Quaternion.identity;
        _readoutText = text.AddComponent<TextMeshPro>();
        _readoutText.fontSize = 0.105f;
        _readoutText.color = Color.white;
        _readoutText.alignment = TextAlignmentOptions.TopLeft;
        _readoutText.textWrappingMode = TextWrappingModes.Normal;
        _readoutText.rectTransform.sizeDelta = new Vector2(1.52f, 0.82f);

        GameObject lamp = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        lamp.name = "SOP_Status_Lamp";
        lamp.transform.SetParent(panel.transform, false);
        lamp.transform.localPosition = new Vector3(0.72f, -0.32f, -0.05f);
        lamp.transform.localScale = Vector3.one * 0.11f;
        Collider lampCollider = lamp.GetComponent<Collider>();
        if (lampCollider != null)
            Destroy(lampCollider);
        _statusLampRenderer = lamp.GetComponent<Renderer>();
    }

    private void SetReadoutVisible(bool visible)
    {
        if (_readoutText == null)
            return;

        _readoutText.transform.parent.gameObject.SetActive(visible);
    }

    private void UpdateReadout()
    {
        if (_readoutText == null)
            return;

        string tempStatus = StatusLine(_temperatureCurrent, _temperatureTarget, _temperatureTolerance, "cooler", "steam");
        string pressureStatus = StatusLine(_pressureCurrent, _pressureTarget, _pressureTolerance, "vent", "pressure");
        string rpmStatus = StatusLine(_rpmCurrent, _rpmTarget, _rpmTolerance, "rpm down", "rpm up");
        string stable = _questComplete
            ? "SOP STABLE - REPORT VIA HT"
            : ParametersInsideSop()
                ? "Hold stable: " + Mathf.CeilToInt(Mathf.Max(0f, _stableHoldDuration - _stableTimer)) + "s"
                : "CORRECT UNTIL ALL OK";

        _readoutText.text =
            "<b>LEVEL 8 - DCS STABILIZATION</b>\n" +
            "Suhu    : " + _temperatureCurrent.ToString("F1") + " C  [" + tempStatus + "]\n" +
            "Tekanan : " + _pressureCurrent.ToString("F1") + " atm [" + pressureStatus + "]\n" +
            "Agitator: " + _rpmCurrent.ToString("F0") + " RPM [" + rpmStatus + "]\n\n" +
            "Keyboard test: 1/2 suhu, 3/4 tekanan, 5/6 RPM\n" +
            stable;
    }

    private string StatusLine(float value, float target, float tolerance, string lowerAction, string upperAction)
    {
        if (Mathf.Abs(value - target) <= tolerance)
            return "OK";

        return value > target ? lowerAction : upperAction;
    }

    private void EnsureMaterials()
    {
        if (_lampWarningMaterial == null)
            _lampWarningMaterial = CreateLampMaterial("L8_LampWarning", new Color(1f, 0.78f, 0.15f));
        if (_lampStableMaterial == null)
            _lampStableMaterial = CreateLampMaterial("L8_LampStable", new Color(0.15f, 0.95f, 0.35f));
        if (_lampDangerMaterial == null)
            _lampDangerMaterial = CreateLampMaterial("L8_LampDanger", new Color(1f, 0.18f, 0.12f));
    }

    private Material CreateLampMaterial(string name, Color color)
    {
        Material material = new Material(Shader.Find("Universal Render Pipeline/Lit"));
        material.name = name;
        material.SetColor("_BaseColor", color);
        material.EnableKeyword("_EMISSION");
        material.SetColor("_EmissionColor", color * 1.6f);
        return material;
    }

    private void SetLampDanger()
    {
        if (_statusLampRenderer != null)
            _statusLampRenderer.sharedMaterial = _lampDangerMaterial;
    }

    private void SetLampStable()
    {
        if (_statusLampRenderer != null)
            _statusLampRenderer.sharedMaterial = _lampStableMaterial;
    }

    private void EnsureAudio()
    {
        if (_alarmAudio == null)
        {
            GameObject go = new GameObject("L8_MonitoringAlarm_Audio");
            go.transform.SetParent(transform, false);
            _alarmAudio = go.AddComponent<AudioSource>();
            _alarmAudio.loop = true;
            _alarmAudio.playOnAwake = false;
            _alarmAudio.spatialBlend = 0f;
            _alarmAudio.clip = GenerateAlarmClip(2f, 22050);
        }

        if (_stableAudio == null)
        {
            GameObject go = new GameObject("L8_StableConfirm_Audio");
            go.transform.SetParent(transform, false);
            _stableAudio = go.AddComponent<AudioSource>();
            _stableAudio.loop = false;
            _stableAudio.playOnAwake = false;
            _stableAudio.spatialBlend = 0f;
            _stableAudio.clip = GenerateStableClip(1.2f, 22050);
        }
    }

    private void StartAudio(AudioSource source, float volume)
    {
        if (source == null)
            return;

        source.volume = volume;
        if (!source.isPlaying)
            source.Play();
    }

    private void StopAudio(AudioSource source)
    {
        if (source != null && source.isPlaying)
            source.Stop();
    }

    private AudioClip GenerateAlarmClip(float duration, int sampleRate)
    {
        int total = Mathf.CeilToInt(duration * sampleRate);
        float[] data = new float[total];
        for (int i = 0; i < total; i++)
        {
            float time = (float)i / sampleRate;
            float pulse = Mathf.PingPong(time * 3f, 1f) > 0.45f ? 1f : 0f;
            float tone = Mathf.Sin(2f * Mathf.PI * 740f * time) * 0.4f;
            data[i] = tone * pulse;
        }

        AudioClip clip = AudioClip.Create("Level8MonitoringAlarm", total, 1, sampleRate, false);
        clip.SetData(data, 0);
        return clip;
    }

    private AudioClip GenerateStableClip(float duration, int sampleRate)
    {
        int total = Mathf.CeilToInt(duration * sampleRate);
        float[] data = new float[total];
        for (int i = 0; i < total; i++)
        {
            float time = (float)i / sampleRate;
            float envelope = Mathf.Clamp01(1f - time / duration);
            float toneA = Mathf.Sin(2f * Mathf.PI * 520f * time);
            float toneB = Mathf.Sin(2f * Mathf.PI * 780f * time) * 0.55f;
            data[i] = (toneA + toneB) * 0.25f * envelope;
        }

        AudioClip clip = AudioClip.Create("Level8StableConfirm", total, 1, sampleRate, false);
        clip.SetData(data, 0);
        return clip;
    }

    private void AutoFindReferences()
    {
        if (_playerRigRoot == null)
        {
            GameObject rig = GameObject.Find("XR Origin (XR Rig)")
                         ?? GameObject.Find("XR Origin")
                         ?? GameObject.Find("XR Rig")
                         ?? GameObject.FindWithTag("Player");
            if (rig != null)
                _playerRigRoot = rig.transform;
        }

        if (_teleportTargetDcs == null)
        {
            GameObject spawn = GameObject.Find("SpawnPoint_DCS");
            if (spawn != null)
                _teleportTargetDcs = spawn.transform;
        }

        if (_btnCoolerOpen == null) _btnCoolerOpen = FindInteractable("Btn_L8_CoolerOpen");
        if (_btnSteamTrim == null) _btnSteamTrim = FindInteractable("Btn_L8_SteamTrim");
        if (_btnVentOpen == null) _btnVentOpen = FindInteractable("Btn_L8_VentOpen");
        if (_btnPressureTrim == null) _btnPressureTrim = FindInteractable("Btn_L8_PressureTrim");
        if (_btnRpmUp == null) _btnRpmUp = FindInteractable("Btn_L8_RpmUp");
        if (_btnRpmDown == null) _btnRpmDown = FindInteractable("Btn_L8_RpmDown");
    }

    private XRSimpleInteractable FindInteractable(string objectName)
    {
        GameObject go = GameObject.Find(objectName);
        return go != null ? go.GetComponent<XRSimpleInteractable>() : null;
    }

    private void SetupButtons()
    {
        if (_btnCoolerOpen != null) _btnCoolerOpen.selectEntered.AddListener(_ => OpenCooler());
        if (_btnSteamTrim != null) _btnSteamTrim.selectEntered.AddListener(_ => TrimSteam());
        if (_btnVentOpen != null) _btnVentOpen.selectEntered.AddListener(_ => OpenVent());
        if (_btnPressureTrim != null) _btnPressureTrim.selectEntered.AddListener(_ => TrimPressure());
        if (_btnRpmUp != null) _btnRpmUp.selectEntered.AddListener(_ => IncreaseRpm());
        if (_btnRpmDown != null) _btnRpmDown.selectEntered.AddListener(_ => DecreaseRpm());
    }

    public bool QuestComplete => _questComplete;
    public bool CorrectionActive => _correctionActive;
    public float TemperatureCurrent => _temperatureCurrent;
    public float PressureCurrent => _pressureCurrent;
    public float RpmCurrent => _rpmCurrent;
}
