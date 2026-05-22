using System.Collections;
using Unity.XR.CoreUtils;
using UnityEngine;

/// <summary>
/// OLIVIA VR - Level11MHPController.cs
///
/// Level 11 bridges CCD overflow into neutralization/purification and MHP
/// precipitation. The player starts the stage from DCS, observes pH adjustment,
/// precipitation, and sample collection, then reports after the MHP sample is ready.
/// </summary>
public class Level11MHPController : MonoBehaviour
{
    [Header("=== Player & Teleport ===")]
    [SerializeField] private Transform _playerRigRoot;
    [SerializeField] private Transform _teleportTargetDcs;
    [SerializeField] private Transform _teleportTargetField;

    [Header("=== Machine References ===")]
    [SerializeField] private GameObject _mhpField;
    [SerializeField] private Transform[] _agitatorRoots;
    [SerializeField] private GameObject _feedLiquid;
    [SerializeField] private GameObject _reagentLiquid;
    [SerializeField] private GameObject _neutralToPolishLiquid;
    [SerializeField] private GameObject _polishToMhpLiquid;
    [SerializeField] private GameObject _mhpSampleFlow;
    [SerializeField] private GameObject _mhpSampleProduct;
    [SerializeField] private ParticleSystem _neutralizationFx;
    [SerializeField] private ParticleSystem _precipitationFx;

    [Header("=== Process Settings ===")]
    [SerializeField] private float _fadeDuration = 2.5f;
    [SerializeField] private float _fieldObservationDelay = 1f;
    [SerializeField] private float _neutralizationDuration = 8f;
    [SerializeField] private float _precipitationDuration = 9f;
    [SerializeField] private float _pHStart = 1.2f;
    [SerializeField] private float _pHTarget = 5.5f;
    [SerializeField] private float _pHTolerance = 0.3f;
    [SerializeField] private float _agitatorRpm = 35f;

    [Header("=== Runtime Status ===")]
    [SerializeField] private float _pHCurrent = 1.2f;
    [SerializeField] private float _mhpQualityCurrent;
    [SerializeField] private float _processProgress;

    [Header("=== Audio ===")]
    [SerializeField] private AudioSource _agitatorAudio;
    [SerializeField] private AudioSource _precipitationAudio;
    [SerializeField] private AudioSource _sampleReadyAudio;
    [Range(0f, 1f)] [SerializeField] private float _agitatorVolume = 0.34f;
    [Range(0f, 1f)] [SerializeField] private float _precipitationVolume = 0.28f;
    [Range(0f, 1f)] [SerializeField] private float _sampleReadyVolume = 0.3f;

    [Header("=== HUD Messages ===")]
    [TextArea(2, 4)] [SerializeField] private string _msgStart =
        "Level 11: Larutan hasil CCD masuk ke pemurnian. Tekan DCS 11 untuk mulai neutralization dan MHP sampling.";
    [TextArea(2, 4)] [SerializeField] private string _msgNeutralizing =
        "Reagen netralisasi masuk. Pantau pH naik menuju 5.5 dan agitator aktif.";
    [TextArea(2, 4)] [SerializeField] private string _msgPrecipitating =
        "Presipitasi MHP berjalan. Amati produk mengendap dan sampel mulai terbentuk.";
    [TextArea(2, 4)] [SerializeField] private string _msgComplete =
        "MHP terbentuk dan sampel siap. Lapor HT: 'mhp terbentuk'.";

    private PlayerHUD _hud;
    private Coroutine _sequenceCoroutine;
    private bool _levelActive;
    private bool _processStarted;
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
        StopAudio(_agitatorAudio);
        StopAudio(_precipitationAudio);
        StopAudio(_sampleReadyAudio);
    }

    private void OnLevelStarted(GameLevelManager.GameLevel level)
    {
        _levelActive = level == GameLevelManager.GameLevel.Level11_MHP;
        if (!_levelActive)
        {
            SetProcessVisuals(false);
            StopSequence();
            StopAudio(_agitatorAudio);
            StopAudio(_precipitationAudio);
            return;
        }

        _processStarted = false;
        _questComplete = false;
        _processProgress = 0f;
        _mhpQualityCurrent = 0f;
        _pHCurrent = _pHStart;
        PushPHToManager();
        SetProcessVisuals(false);

        if (_hud != null)
            _hud.ShowNotifPublic(_msgStart);

        TeleportPlayer(_teleportTargetDcs);
    }

    private void Update()
    {
        if (!_levelActive || !_processStarted)
            return;

        AnimateAgitators();
    }

    private void OnDcsButtonPressed(int number)
    {
        if (!_levelActive || number != 11 || _processStarted)
            return;

        _processStarted = true;
        _sequenceCoroutine = StartCoroutine(RunMHPSequence());
    }

    private IEnumerator RunMHPSequence()
    {
        if (_hud != null)
            _hud.PlayManualFade(_fadeDuration);

        yield return new WaitForSeconds(_fadeDuration * 0.5f);
        TeleportPlayer(_teleportTargetField);
        yield return new WaitForSeconds(_fadeDuration * 0.5f + _fieldObservationDelay);

        if (_hud != null)
            _hud.ShowNotifPublic(_msgNeutralizing);

        SetProcessVisuals(true);
        StartAudio(_agitatorAudio, _agitatorVolume);

        float elapsed = 0f;
        while (elapsed < _neutralizationDuration)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.Clamp01(elapsed / _neutralizationDuration);
            _pHCurrent = Mathf.Lerp(_pHStart, _pHTarget, SmoothStep(t));
            _processProgress = t * 45f;
            PushPHToManager();
            UpdateNeutralizationFx(t);
            yield return null;
        }

        _pHCurrent = _pHTarget;
        PushPHToManager();
        if (_neutralToPolishLiquid != null) _neutralToPolishLiquid.SetActive(true);
        if (_polishToMhpLiquid != null) _polishToMhpLiquid.SetActive(true);
        StartAudio(_precipitationAudio, _precipitationVolume);

        if (_hud != null)
            _hud.ShowNotifPublic(_msgPrecipitating);

        elapsed = 0f;
        while (elapsed < _precipitationDuration)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.Clamp01(elapsed / _precipitationDuration);
            _mhpQualityCurrent = Mathf.Lerp(0f, 92f, SmoothStep(t));
            _processProgress = Mathf.Lerp(45f, 100f, t);
            UpdatePrecipitationFx(t);
            yield return null;
        }

        _mhpQualityCurrent = 92f;
        _processProgress = 100f;
        if (_mhpSampleFlow != null) _mhpSampleFlow.SetActive(true);
        yield return new WaitForSeconds(1.2f);
        if (_mhpSampleProduct != null) _mhpSampleProduct.SetActive(true);

        StopAudio(_precipitationAudio);
        StartAudio(_sampleReadyAudio, _sampleReadyVolume);
        _questComplete = Mathf.Abs(_pHCurrent - _pHTarget) <= _pHTolerance && _mhpQualityCurrent >= 90f;
        GameLevelManager.Instance?.NotifyLevel11MHPComplete();

        if (_hud != null)
            _hud.ShowNotifPublic(_msgComplete);

        Debug.Log("[Level11] MHP sample ready. Player can report via WT.");
        _sequenceCoroutine = null;
    }

    private float SmoothStep(float t)
    {
        t = Mathf.Clamp01(t);
        return t * t * (3f - 2f * t);
    }

    private void AnimateAgitators()
    {
        if (_agitatorRoots == null)
            return;

        float degreesPerSecond = _agitatorRpm * 6f;
        for (int i = 0; i < _agitatorRoots.Length; i++)
        {
            if (_agitatorRoots[i] != null)
                _agitatorRoots[i].Rotate(Vector3.up, degreesPerSecond * Time.deltaTime, Space.World);
        }
    }

    private void SetProcessVisuals(bool active)
    {
        if (_feedLiquid != null) _feedLiquid.SetActive(active);
        if (_reagentLiquid != null) _reagentLiquid.SetActive(active);
        if (_neutralToPolishLiquid != null) _neutralToPolishLiquid.SetActive(false);
        if (_polishToMhpLiquid != null) _polishToMhpLiquid.SetActive(false);
        if (_mhpSampleFlow != null) _mhpSampleFlow.SetActive(false);
        if (_mhpSampleProduct != null) _mhpSampleProduct.SetActive(false);

        if (_neutralizationFx != null)
        {
            if (active)
            {
                UpdateNeutralizationFx(0.45f);
                _neutralizationFx.Play();
            }
            else
            {
                _neutralizationFx.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            }
        }

        if (_precipitationFx != null)
        {
            if (active)
            {
                UpdatePrecipitationFx(0f);
                _precipitationFx.Play();
            }
            else
            {
                _precipitationFx.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            }
        }
    }

    private void UpdateNeutralizationFx(float intensity)
    {
        if (_neutralizationFx == null)
            return;

        var emission = _neutralizationFx.emission;
        emission.rateOverTime = Mathf.Lerp(10f, 55f, Mathf.Clamp01(intensity));
    }

    private void UpdatePrecipitationFx(float intensity)
    {
        if (_precipitationFx == null)
            return;

        var emission = _precipitationFx.emission;
        emission.rateOverTime = Mathf.Lerp(0f, 70f, Mathf.Clamp01(intensity));
    }

    private void PushPHToManager()
    {
        if (GameLevelManager.Instance != null)
            GameLevelManager.Instance.SetPH(_pHCurrent);
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
            Debug.LogWarning("[Level11] XROrigin component not found. Teleport skipped to avoid tracker snapback.");
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
        if (_agitatorAudio == null)
        {
            GameObject go = new GameObject("L11_Agitator_Audio");
            go.transform.SetParent(transform, false);
            _agitatorAudio = go.AddComponent<AudioSource>();
            _agitatorAudio.loop = true;
            _agitatorAudio.playOnAwake = false;
            _agitatorAudio.spatialBlend = 0.25f;
            _agitatorAudio.clip = GenerateAgitatorClip(4f, 22050);
        }

        if (_precipitationAudio == null)
        {
            GameObject go = new GameObject("L11_Precipitation_Audio");
            go.transform.SetParent(transform, false);
            _precipitationAudio = go.AddComponent<AudioSource>();
            _precipitationAudio.loop = true;
            _precipitationAudio.playOnAwake = false;
            _precipitationAudio.spatialBlend = 0.25f;
            _precipitationAudio.clip = GeneratePrecipitationClip(3f, 22050);
        }

        if (_sampleReadyAudio == null)
        {
            GameObject go = new GameObject("L11_SampleReady_Audio");
            go.transform.SetParent(transform, false);
            _sampleReadyAudio = go.AddComponent<AudioSource>();
            _sampleReadyAudio.loop = false;
            _sampleReadyAudio.playOnAwake = false;
            _sampleReadyAudio.spatialBlend = 0.15f;
            _sampleReadyAudio.clip = GenerateSampleReadyClip(1.2f, 22050);
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

    private AudioClip GenerateAgitatorClip(float duration, int sampleRate)
    {
        int total = Mathf.CeilToInt(duration * sampleRate);
        float[] data = new float[total];
        System.Random random = new System.Random(1101);
        float phase = 0f;
        float filter = 0f;

        for (int i = 0; i < total; i++)
        {
            phase += 2f * Mathf.PI * 68f / sampleRate;
            float motor = Mathf.Sin(phase) * 0.32f;
            float noise = ((float)random.NextDouble() - 0.5f) * 0.22f;
            filter += 0.055f * (noise - filter);
            data[i] = (motor + filter) * 0.42f;
        }

        AudioClip clip = AudioClip.Create("Level11Agitator", total, 1, sampleRate, false);
        clip.SetData(data, 0);
        return clip;
    }

    private AudioClip GeneratePrecipitationClip(float duration, int sampleRate)
    {
        int total = Mathf.CeilToInt(duration * sampleRate);
        float[] data = new float[total];
        System.Random random = new System.Random(1102);
        float bubblePhase = 0f;

        for (int i = 0; i < total; i++)
        {
            bubblePhase += 2f * Mathf.PI * 5.4f / sampleRate;
            float noise = ((float)random.NextDouble() - 0.5f) * 0.45f;
            float bubble = Mathf.Abs(Mathf.Sin(bubblePhase));
            data[i] = (noise * 0.35f + bubble * 0.08f) * 0.55f;
        }

        AudioClip clip = AudioClip.Create("Level11Precipitation", total, 1, sampleRate, false);
        clip.SetData(data, 0);
        return clip;
    }

    private AudioClip GenerateSampleReadyClip(float duration, int sampleRate)
    {
        int total = Mathf.CeilToInt(duration * sampleRate);
        float[] data = new float[total];

        for (int i = 0; i < total; i++)
        {
            float time = (float)i / sampleRate;
            float envelope = Mathf.Clamp01(1f - time / duration);
            float tone = Mathf.Sin(2f * Mathf.PI * 540f * time) * 0.22f;
            float harmonic = Mathf.Sin(2f * Mathf.PI * 810f * time) * 0.14f;
            data[i] = (tone + harmonic) * envelope;
        }

        AudioClip clip = AudioClip.Create("Level11SampleReady", total, 1, sampleRate, false);
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
            GameObject field = GameObject.Find("SpawnPoint_Lvl11");
            if (field != null)
                _teleportTargetField = field.transform;
        }

        if (_mhpField == null)
            _mhpField = GameObject.Find("Mesin Utama/Level11_MHP_Field") ?? GameObject.Find("Level11_MHP_Field");

        if (_mhpField == null)
            return;

        Transform root = _mhpField.transform;
        if (_feedLiquid == null) _feedLiquid = FindChild(root, "Feed_From_CCD_Liquid");
        if (_reagentLiquid == null) _reagentLiquid = FindChild(root, "Reagent_Liquid_Line");
        if (_neutralToPolishLiquid == null) _neutralToPolishLiquid = FindChild(root, "Neutralization_To_Polishing_Liquid");
        if (_polishToMhpLiquid == null) _polishToMhpLiquid = FindChild(root, "Polishing_To_MHP_Liquid");
        if (_mhpSampleFlow == null) _mhpSampleFlow = FindChild(root, "MHP_Sampling_Station/MHP_Sample_Flow");
        if (_mhpSampleProduct == null) _mhpSampleProduct = FindChild(root, "MHP_Sampling_Station/MHP_Sample_Product");

        if (_neutralizationFx == null)
        {
            Transform fx = root.Find("Neutralization_FX");
            if (fx != null)
                _neutralizationFx = fx.GetComponent<ParticleSystem>();
        }

        if (_precipitationFx == null)
        {
            Transform fx = root.Find("MHP_Precipitation_FX");
            if (fx != null)
                _precipitationFx = fx.GetComponent<ParticleSystem>();
        }

        if (_agitatorRoots == null || _agitatorRoots.Length == 0)
        {
            _agitatorRoots = new Transform[]
            {
                root.Find("Neutralization_Purification_Tank/Agitator_Root"),
                root.Find("Polishing_Tank/Agitator_Root"),
                root.Find("MHP_Precipitation_Tank/Agitator_Root")
            };
        }
    }

    private GameObject FindChild(Transform root, string path)
    {
        Transform child = root.Find(path);
        return child != null ? child.gameObject : null;
    }

    public bool QuestComplete => _questComplete;
    public float PHCurrent => _pHCurrent;
    public float MHPQualityCurrent => _mhpQualityCurrent;
    public float ProcessProgress => _processProgress;
}
