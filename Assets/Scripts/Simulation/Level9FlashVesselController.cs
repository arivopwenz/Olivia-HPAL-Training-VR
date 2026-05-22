using System.Collections;
using Unity.XR.CoreUtils;
using UnityEngine;

/// <summary>
/// OLIVIA VR - Level9FlashVesselController.cs
///
/// Level 9 handles the flash / letdown vessel after Autoclave operation.
/// The player starts the letdown sequence from DCS button 9, observes the
/// pressure drop at the field vessel, then reports once pressure is stable.
/// </summary>
public class Level9FlashVesselController : MonoBehaviour
{
    [Header("=== Player & Teleport ===")]
    [SerializeField] private Transform _playerRigRoot;
    [SerializeField] private Transform _teleportTargetDcs;
    [SerializeField] private Transform _teleportTargetField;

    [Header("=== Machine References ===")]
    [SerializeField] private GameObject _flashVesselField;
    [SerializeField] private Transform _letdownValveHandwheel;
    [SerializeField] private Transform _pressureGaugeNeedle;
    [SerializeField] private ParticleSystem _vaporFx;
    [SerializeField] private GameObject _inletLiquid;
    [SerializeField] private GameObject _outletLiquid;

    [Header("=== Pressure Profile ===")]
    [SerializeField] private float _pressureStart = 47.5f;
    [SerializeField] private float _pressureTarget = 12f;
    [SerializeField] private float _pressureTolerance = 1.2f;
    [SerializeField] private float _letdownDuration = 12f;
    [SerializeField] private AnimationCurve _pressureDropCurve = AnimationCurve.EaseInOut(0f, 0f, 1f, 1f);

    [Header("=== Timing ===")]
    [SerializeField] private float _fadeDuration = 2.5f;
    [SerializeField] private float _fieldObservationDelay = 1.2f;

    [Header("=== Audio ===")]
    [SerializeField] private AudioSource _depressurizeAudio;
    [SerializeField] private AudioSource _stableAudio;
    [Range(0f, 1f)] [SerializeField] private float _depressurizeVolume = 0.48f;
    [Range(0f, 1f)] [SerializeField] private float _stableVolume = 0.32f;

    [Header("=== HUD Messages ===")]
    [TextArea(2, 4)] [SerializeField] private string _msgStart =
        "Level 9: Siapkan flash vessel. Tekan tombol DCS 9 untuk membuka letdown valve.";
    [TextArea(2, 4)] [SerializeField] private string _msgObserve =
        "Letdown valve terbuka. Amati tekanan turun cepat di Flash Vessel.";
    [TextArea(2, 4)] [SerializeField] private string _msgStable =
        "Tekanan flash vessel stabil di 12 atm. Lapor HT: 'flash vessel normal'.";

    private PlayerHUD _hud;
    private Coroutine _sequenceCoroutine;
    private float _pressureCurrent;
    private float _valveSpinAngle;
    private bool _levelActive;
    private bool _letdownStarted;
    private bool _questComplete;

    private void Awake()
    {
        _hud = FindFirstObjectByType<PlayerHUD>();
        AutoFindReferences();
        EnsureAudio();
        SetProcessVisuals(false);
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
        StopAudio(_depressurizeAudio);
        StopAudio(_stableAudio);
    }

    private void OnLevelStarted(GameLevelManager.GameLevel level)
    {
        _levelActive = level == GameLevelManager.GameLevel.Level9_FlashVessel;
        if (!_levelActive)
        {
            SetProcessVisuals(false);
            StopSequence();
            StopAudio(_depressurizeAudio);
            return;
        }

        _pressureCurrent = _pressureStart;
        _letdownStarted = false;
        _questComplete = false;
        _valveSpinAngle = 0f;
        PushPressureToManager();
        UpdateGaugeNeedle();
        SetProcessVisuals(false);

        if (_hud != null)
            _hud.ShowNotifPublic(_msgStart);

        TeleportPlayer(_teleportTargetDcs);
    }

    private void Update()
    {
        if (!_levelActive || !_letdownStarted || _questComplete)
            return;

        if (_letdownValveHandwheel != null)
        {
            _valveSpinAngle += 180f * Time.deltaTime;
            _letdownValveHandwheel.localRotation = Quaternion.Euler(0f, _valveSpinAngle, 0f);
        }
    }

    private void OnDcsButtonPressed(int number)
    {
        if (!_levelActive || number != 9 || _letdownStarted)
            return;

        _letdownStarted = true;
        _sequenceCoroutine = StartCoroutine(RunLetdownSequence());
    }

    private IEnumerator RunLetdownSequence()
    {
        if (_hud != null)
            _hud.PlayManualFade(_fadeDuration);

        yield return new WaitForSeconds(_fadeDuration * 0.5f);
        TeleportPlayer(_teleportTargetField);
        yield return new WaitForSeconds(_fadeDuration * 0.5f + _fieldObservationDelay);

        if (_hud != null)
            _hud.ShowNotifPublic(_msgObserve);

        SetProcessVisuals(true);
        StartAudio(_depressurizeAudio, _depressurizeVolume);

        float elapsed = 0f;
        while (elapsed < _letdownDuration)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.Clamp01(elapsed / _letdownDuration);
            float curveT = _pressureDropCurve.Evaluate(t);
            _pressureCurrent = Mathf.Lerp(_pressureStart, _pressureTarget, curveT);
            PushPressureToManager();
            UpdateGaugeNeedle();
            UpdateVaporEmission(1f - curveT);
            yield return null;
        }

        _pressureCurrent = _pressureTarget;
        PushPressureToManager();
        UpdateGaugeNeedle();
        UpdateVaporEmission(0.18f);
        StopAudio(_depressurizeAudio);
        StartAudio(_stableAudio, _stableVolume);
        _questComplete = Mathf.Abs(_pressureCurrent - _pressureTarget) <= _pressureTolerance;

        if (_hud != null)
            _hud.ShowNotifPublic(_msgStable);

        Debug.Log("[Level9] Flash vessel pressure stable at 12 atm. Player can report via WT.");
        _sequenceCoroutine = null;
    }

    private void StopSequence()
    {
        if (_sequenceCoroutine == null)
            return;

        StopCoroutine(_sequenceCoroutine);
        _sequenceCoroutine = null;
    }

    private void SetProcessVisuals(bool active)
    {
        if (_inletLiquid != null)
            _inletLiquid.SetActive(active);
        if (_outletLiquid != null)
            _outletLiquid.SetActive(active);

        if (_vaporFx == null)
            return;

        if (active)
        {
            UpdateVaporEmission(1f);
            if (!_vaporFx.isPlaying)
                _vaporFx.Play();
        }
        else
        {
            _vaporFx.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
        }
    }

    private void UpdateVaporEmission(float intensity)
    {
        if (_vaporFx == null)
            return;

        var emission = _vaporFx.emission;
        emission.rateOverTime = Mathf.Lerp(10f, 80f, Mathf.Clamp01(intensity));

        var main = _vaporFx.main;
        main.startSpeed = Mathf.Lerp(0.45f, 1.8f, Mathf.Clamp01(intensity));
    }

    private void UpdateGaugeNeedle()
    {
        if (_pressureGaugeNeedle == null)
            return;

        float t = Mathf.InverseLerp(_pressureStart, _pressureTarget, _pressureCurrent);
        float angle = Mathf.Lerp(-35f, 125f, t);
        _pressureGaugeNeedle.localRotation = Quaternion.Euler(0f, 0f, angle);
    }

    private void PushPressureToManager()
    {
        if (GameLevelManager.Instance != null)
            GameLevelManager.Instance.SetTekanan(_pressureCurrent);
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
            Debug.LogWarning("[Level9] XROrigin component not found. Teleport skipped to avoid tracker snapback.");
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
        if (_depressurizeAudio == null)
        {
            GameObject go = new GameObject("L9_Depressurize_Audio");
            go.transform.SetParent(transform, false);
            _depressurizeAudio = go.AddComponent<AudioSource>();
            _depressurizeAudio.loop = true;
            _depressurizeAudio.playOnAwake = false;
            _depressurizeAudio.spatialBlend = 0.25f;
            _depressurizeAudio.clip = GenerateDepressurizeClip(3f, 22050);
        }

        if (_stableAudio == null)
        {
            GameObject go = new GameObject("L9_Stable_Audio");
            go.transform.SetParent(transform, false);
            _stableAudio = go.AddComponent<AudioSource>();
            _stableAudio.loop = false;
            _stableAudio.playOnAwake = false;
            _stableAudio.spatialBlend = 0.15f;
            _stableAudio.clip = GenerateStableClip(1.4f, 22050);
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

    private AudioClip GenerateDepressurizeClip(float duration, int sampleRate)
    {
        int total = Mathf.CeilToInt(duration * sampleRate);
        float[] data = new float[total];
        System.Random random = new System.Random(904);
        float filter = 0f;

        for (int i = 0; i < total; i++)
        {
            float time = (float)i / sampleRate;
            float noise = ((float)random.NextDouble() - 0.5f) * 2f;
            filter += 0.14f * (noise - filter);
            float rumble = Mathf.Sin(2f * Mathf.PI * 82f * time) * 0.22f;
            float hissPulse = Mathf.Abs(Mathf.Sin(2f * Mathf.PI * 2.4f * time));
            data[i] = (filter * 0.65f + rumble) * (0.65f + hissPulse * 0.35f) * 0.55f;
        }

        AudioClip clip = AudioClip.Create("Level9Depressurize", total, 1, sampleRate, false);
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
            float tone = Mathf.Sin(2f * Mathf.PI * 420f * time) * 0.22f;
            float harmonic = Mathf.Sin(2f * Mathf.PI * 630f * time) * 0.12f;
            data[i] = (tone + harmonic) * envelope;
        }

        AudioClip clip = AudioClip.Create("Level9Stable", total, 1, sampleRate, false);
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
            GameObject dcs = GameObject.Find("SpawnPoint_DCS");
            if (dcs != null)
                _teleportTargetDcs = dcs.transform;
        }

        if (_teleportTargetField == null)
        {
            GameObject field = GameObject.Find("SpawnPoint_Lvl9");
            if (field != null)
                _teleportTargetField = field.transform;
        }

        if (_flashVesselField == null)
            _flashVesselField = GameObject.Find("Mesin Utama/FlashVessel_Field") ?? GameObject.Find("FlashVessel_Field");

        if (_flashVesselField == null)
            return;

        Transform root = _flashVesselField.transform;
        if (_letdownValveHandwheel == null)
            _letdownValveHandwheel = root.Find("LetdownValve_Assembly/LetdownValve_Handwheel");
        if (_pressureGaugeNeedle == null)
            _pressureGaugeNeedle = root.Find("PressureGauge_Needle");
        if (_vaporFx == null)
        {
            Transform fx = root.Find("Vapor_FX");
            if (fx != null)
                _vaporFx = fx.GetComponent<ParticleSystem>();
        }
        if (_inletLiquid == null)
        {
            Transform inlet = root.Find("Pipe_AutoclaveToFlash_Liquid");
            if (inlet != null)
                _inletLiquid = inlet.gameObject;
        }
        if (_outletLiquid == null)
        {
            Transform outlet = root.Find("Pipe_FlashToCCD_Liquid");
            if (outlet != null)
                _outletLiquid = outlet.gameObject;
        }
    }

    public bool QuestComplete => _questComplete;
    public float PressureCurrent => _pressureCurrent;
}
