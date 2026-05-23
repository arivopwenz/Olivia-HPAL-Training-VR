using System.Collections;
using Unity.XR.CoreUtils;
using UnityEngine;

/// <summary>
/// OLIVIA VR - Level14EmergencyController.cs
///
/// Final K3 scenario: the player enters an emergency drill, observes a leak and
/// critical pressure, reports the emergency, then activates ESD to secure the plant.
/// </summary>
public class Level14EmergencyController : MonoBehaviour
{
    [Header("=== Player & Teleport ===")]
    [SerializeField] private Transform _playerRigRoot;
    [SerializeField] private Transform _teleportTargetField;

    [Header("=== Emergency References ===")]
    [SerializeField] private GameObject _emergencyField;
    [SerializeField] private GameObject _leakJet;
    [SerializeField] private GameObject _redAlarmBeacon;
    [SerializeField] private GameObject _greenSafeBeacon;
    [SerializeField] private GameObject _evacuationArrow;
    [SerializeField] private GameObject _containmentBarrier;
    [SerializeField] private Transform _pressureNeedle;
    [SerializeField] private Transform _esdButton;
    [SerializeField] private Renderer[] _warningRenderers;
    [SerializeField] private ParticleSystem[] _leakParticles;

    [Header("=== Emergency Settings ===")]
    [SerializeField] private float _fieldObservationDelay = 0.7f;
    [SerializeField] private float _pressureRiseDuration = 8f;
    [SerializeField] private float _criticalPressure = 56f;
    [SerializeField] private float _safePressure = 1.5f;
    [SerializeField] private float _safeTemperature = 90f;
    [SerializeField] private float _buttonPressedDepth = 0.08f;

    [Header("=== Runtime Status ===")]
    [SerializeField] private float _pressureCurrent = 47.5f;
    [SerializeField] private bool _emergencyActive;
    [SerializeField] private bool _esdPressed;

    [Header("=== Audio ===")]
    [SerializeField] private AudioSource _alarmAudio;
    [SerializeField] private AudioSource _leakAudio;
    [SerializeField] private AudioSource _shutdownAudio;
    [Range(0f, 1f)] [SerializeField] private float _alarmVolume = 0.38f;
    [Range(0f, 1f)] [SerializeField] private float _leakVolume = 0.32f;
    [Range(0f, 1f)] [SerializeField] private float _shutdownVolume = 0.35f;

    [Header("=== HUD Messages ===")]
    [TextArea(2, 4)] [SerializeField] private string _msgStart =
        "Level 14: Emergency K3. Tekan DCS 14 untuk simulasi kebocoran dan pressure critical.";
    [TextArea(2, 4)] [SerializeField] private string _msgEmergency =
        "Kebocoran terdeteksi. Lapor HT: 'Emergency, emergency...' lalu tekan ESD merah.";
    [TextArea(2, 4)] [SerializeField] private string _msgShutdown =
        "ESD aktif. Valve isolasi tertutup, tekanan turun, ikuti jalur evakuasi.";

    private PlayerHUD _hud;
    private Coroutine _sequenceCoroutine;
    private Vector3 _esdButtonInitialLocalPosition;
    private bool _levelActive;
    private bool _drillStarted;

    private void Awake()
    {
        _hud = FindFirstObjectByType<PlayerHUD>();
        AutoFindReferences();
        EnsureAudio();
        if (_esdButton != null)
            _esdButtonInitialLocalPosition = _esdButton.localPosition;
        SetEmergencyVisuals(false);
        PushSafeParameters();
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
        StopSequence();
        StopAllAudio();
    }

    private void OnLevelStarted(GameLevelManager.GameLevel level)
    {
        _levelActive = level == GameLevelManager.GameLevel.Level14_Emergency;
        if (!_levelActive)
        {
            SetEmergencyVisuals(false);
            StopSequence();
            StopAllAudio();
            return;
        }

        _drillStarted = false;
        _esdPressed = false;
        _emergencyActive = false;
        _pressureCurrent = 47.5f;
        if (_esdButton != null)
            _esdButton.localPosition = _esdButtonInitialLocalPosition;

        SetEmergencyVisuals(false);
        PushProcessParameters(252f, _pressureCurrent, 60f);
        TeleportPlayer(_teleportTargetField);
        ShowHud(_msgStart);
    }

    private void OnDcsButtonPressed(int buttonNumber)
    {
        if (!_levelActive || buttonNumber != 14)
            return;

        StartEmergencyDrill();
    }

    public void StartEmergencyDrill()
    {
        if (!_levelActive || _drillStarted)
            return;

        _drillStarted = true;
        StopSequence();
        _sequenceCoroutine = StartCoroutine(RunEmergencySequence());
    }

    public void PressESD()
    {
        if (!_levelActive || !_emergencyActive || _esdPressed)
            return;

        _esdPressed = true;
        _emergencyActive = false;
        StopSequence();
        StartCoroutine(RunShutdownSequence());
    }

    private IEnumerator RunEmergencySequence()
    {
        yield return new WaitForSeconds(_fieldObservationDelay);
        _emergencyActive = true;
        SetEmergencyVisuals(true);
        ShowHud(_msgEmergency);
        StartAudio(_alarmAudio, _alarmVolume);
        StartAudio(_leakAudio, _leakVolume);

        float timer = 0f;
        while (_emergencyActive && !_esdPressed)
        {
            timer += Time.deltaTime;
            float t = Mathf.Clamp01(timer / _pressureRiseDuration);
            _pressureCurrent = Mathf.Lerp(47.5f, _criticalPressure, SmoothStep(t));
            UpdatePressureNeedle(t);
            PulseWarnings();
            PushProcessParameters(252f + t * 10f, _pressureCurrent, 60f);
            yield return null;
        }

        _sequenceCoroutine = null;
    }

    private IEnumerator RunShutdownSequence()
    {
        ShowHud(_msgShutdown);
        StopAudio(_leakAudio);
        StopAudio(_alarmAudio);
        StartAudio(_shutdownAudio, _shutdownVolume);

        if (_esdButton != null)
            _esdButton.localPosition = _esdButtonInitialLocalPosition + Vector3.down * _buttonPressedDepth;

        float startPressure = _pressureCurrent;
        float timer = 0f;
        while (timer < 3.5f)
        {
            timer += Time.deltaTime;
            float t = Mathf.Clamp01(timer / 3.5f);
            _pressureCurrent = Mathf.Lerp(startPressure, _safePressure, SmoothStep(t));
            UpdatePressureNeedle(1f - t);
            PushProcessParameters(Mathf.Lerp(252f, _safeTemperature, t), _pressureCurrent, 0f);
            yield return null;
        }

        SetSafeVisuals();
        PushSafeParameters();
        GameLevelManager.Instance?.NotifyLevel14EsdPressed();
    }

    private void SetEmergencyVisuals(bool active)
    {
        if (_leakJet != null) _leakJet.SetActive(active);
        if (_redAlarmBeacon != null) _redAlarmBeacon.SetActive(active);
        if (_greenSafeBeacon != null) _greenSafeBeacon.SetActive(false);
        if (_evacuationArrow != null) _evacuationArrow.SetActive(active);
        if (_containmentBarrier != null) _containmentBarrier.SetActive(false);

        if (_leakParticles != null)
        {
            for (int i = 0; i < _leakParticles.Length; i++)
            {
                if (_leakParticles[i] == null)
                    continue;

                if (active) _leakParticles[i].Play();
                else _leakParticles[i].Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            }
        }
    }

    private void SetSafeVisuals()
    {
        SetEmergencyVisuals(false);
        if (_greenSafeBeacon != null) _greenSafeBeacon.SetActive(true);
        if (_containmentBarrier != null) _containmentBarrier.SetActive(true);
    }

    private void UpdatePressureNeedle(float t)
    {
        if (_pressureNeedle == null)
            return;

        Vector3 euler = _pressureNeedle.localEulerAngles;
        euler.z = Mathf.Lerp(-40f, 42f, SmoothStep(t));
        _pressureNeedle.localEulerAngles = euler;
    }

    private void PulseWarnings()
    {
        if (_warningRenderers == null)
            return;

        float pulse = 0.55f + Mathf.Sin(Time.time * 8f) * 0.35f;
        Color color = new Color(1f, pulse * 0.12f, 0.05f, 1f);
        for (int i = 0; i < _warningRenderers.Length; i++)
        {
            if (_warningRenderers[i] != null)
                _warningRenderers[i].material.color = color;
        }
    }

    private float SmoothStep(float t)
    {
        t = Mathf.Clamp01(t);
        return t * t * (3f - 2f * t);
    }

    private void PushProcessParameters(float temperature, float pressure, float rpm)
    {
        if (GameLevelManager.Instance == null)
            return;

        GameLevelManager.Instance.SetSuhu(temperature);
        GameLevelManager.Instance.SetTekanan(pressure);
        GameLevelManager.Instance.SetRPM(rpm);
    }

    private void PushSafeParameters()
    {
        PushProcessParameters(_safeTemperature, _safePressure, 0f);
    }

    private void ShowHud(string message)
    {
        if (_hud != null)
            _hud.ShowNotifPublic(message);
    }

    private void StopSequence()
    {
        if (_sequenceCoroutine == null)
            return;

        StopCoroutine(_sequenceCoroutine);
        _sequenceCoroutine = null;
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
            Debug.LogWarning("[Level14] XROrigin component not found. Teleport skipped.");
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

    private void EnsureAudio()
    {
        if (_alarmAudio == null)
            _alarmAudio = CreateAudioSource("L14_Alarm_Audio", GenerateToneClip("Level14Alarm", 2f, 22050, 880f, 0.22f), true, 0.2f);

        if (_leakAudio == null)
            _leakAudio = CreateAudioSource("L14_Leak_Audio", GenerateNoiseClip("Level14Leak", 3f, 22050), true, 0.55f);

        if (_shutdownAudio == null)
            _shutdownAudio = CreateAudioSource("L14_Shutdown_Audio", GenerateToneClip("Level14Shutdown", 1.4f, 22050, 420f, 0.24f), false, 0.25f);
    }

    private AudioSource CreateAudioSource(string name, AudioClip clip, bool loop, float spatialBlend)
    {
        GameObject go = new GameObject(name);
        go.transform.SetParent(transform, false);
        AudioSource source = go.AddComponent<AudioSource>();
        source.clip = clip;
        source.loop = loop;
        source.playOnAwake = false;
        source.spatialBlend = spatialBlend;
        return source;
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

    private void StopAllAudio()
    {
        StopAudio(_alarmAudio);
        StopAudio(_leakAudio);
        StopAudio(_shutdownAudio);
    }

    private AudioClip GenerateToneClip(string name, float duration, int sampleRate, float frequency, float amplitude)
    {
        int total = Mathf.CeilToInt(duration * sampleRate);
        float[] data = new float[total];

        for (int i = 0; i < total; i++)
        {
            float time = (float)i / sampleRate;
            float gate = Mathf.PingPong(time * 3f, 1f) > 0.45f ? 1f : 0.25f;
            data[i] = Mathf.Sin(2f * Mathf.PI * frequency * time) * amplitude * gate;
        }

        AudioClip clip = AudioClip.Create(name, total, 1, sampleRate, false);
        clip.SetData(data, 0);
        return clip;
    }

    private AudioClip GenerateNoiseClip(string name, float duration, int sampleRate)
    {
        int total = Mathf.CeilToInt(duration * sampleRate);
        float[] data = new float[total];
        System.Random random = new System.Random(name.GetHashCode());
        float filter = 0f;

        for (int i = 0; i < total; i++)
        {
            float noise = ((float)random.NextDouble() - 0.5f) * 0.65f;
            filter += 0.12f * (noise - filter);
            data[i] = filter;
        }

        AudioClip clip = AudioClip.Create(name, total, 1, sampleRate, false);
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

        if (_teleportTargetField == null)
        {
            GameObject field = GameObject.Find("SpawnPoint_Lvl14");
            if (field != null)
                _teleportTargetField = field.transform;
        }

        if (_emergencyField == null)
            _emergencyField = GameObject.Find("Mesin Utama/Level14_Emergency_Field") ?? GameObject.Find("Level14_Emergency_Field");

        if (_emergencyField == null)
            return;

        Transform root = _emergencyField.transform;
        if (_leakJet == null) _leakJet = FindChild(root, "Autoclave_Leak_Zone/Acid_Steam_Leak_Jet");
        if (_redAlarmBeacon == null) _redAlarmBeacon = FindChild(root, "Emergency_Red_Beacon");
        if (_greenSafeBeacon == null) _greenSafeBeacon = FindChild(root, "Emergency_Green_Beacon");
        if (_evacuationArrow == null) _evacuationArrow = FindChild(root, "Evacuation_Route_Arrow");
        if (_containmentBarrier == null) _containmentBarrier = FindChild(root, "Containment_Barrier_Safe");
        if (_pressureNeedle == null) _pressureNeedle = root.Find("Emergency_Pressure_Panel/Pressure_Needle");
        if (_esdButton == null) _esdButton = root.Find("ESD_Station/ESD_Button");

        if (_leakParticles == null || _leakParticles.Length == 0)
        {
            ParticleSystem particle = null;
            Transform fx = root.Find("Autoclave_Leak_Zone/Acid_Steam_Leak_FX");
            if (fx != null)
                particle = fx.GetComponent<ParticleSystem>();
            _leakParticles = particle != null ? new[] { particle } : new ParticleSystem[0];
        }

        if (_warningRenderers == null || _warningRenderers.Length == 0)
            _warningRenderers = GetChildRenderers(root, "Warning_Stripe_");
    }

    private GameObject FindChild(Transform root, string path)
    {
        Transform child = root.Find(path);
        return child != null ? child.gameObject : null;
    }

    private Renderer[] GetChildRenderers(Transform parent, string prefix)
    {
        if (parent == null)
            return new Renderer[0];

        System.Collections.Generic.List<Renderer> results = new System.Collections.Generic.List<Renderer>();
        Renderer[] renderers = parent.GetComponentsInChildren<Renderer>(true);
        for (int i = 0; i < renderers.Length; i++)
        {
            if (renderers[i].name.StartsWith(prefix))
                results.Add(renderers[i]);
        }
        return results.ToArray();
    }

    public bool ESDPressed => _esdPressed;
    public float PressureCurrent => _pressureCurrent;
}
