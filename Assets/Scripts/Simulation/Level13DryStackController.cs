using System.Collections;
using Unity.XR.CoreUtils;
using UnityEngine;

/// <summary>
/// OLIVIA VR - Level13DryStackController.cs
///
/// Final tailing showcase: the player starts local tailing polishing, watches
/// limestone dosing lift pH to the environmental range, final filter press
/// lowers moisture below 25%, and tailing cake is secured in dry stack storage.
/// </summary>
public class Level13DryStackController : MonoBehaviour
{
    [Header("=== Player & Teleport ===")]
    [SerializeField] private Transform _playerRigRoot;
    [SerializeField] private Transform _teleportTargetField;

    [Header("=== Machine References ===")]
    [SerializeField] private GameObject _dryStackField;
    [SerializeField] private Transform _agitatorRoot;
    [SerializeField] private Transform _limestoneBag;
    [SerializeField] private GameObject _limestonePourStream;
    [SerializeField] private GameObject _polishedTailingFlow;
    [SerializeField] private GameObject _filtrateChannel;
    [SerializeField] private Transform _phMonitorNeedle;
    [SerializeField] private GameObject _phStatusGreen;
    [SerializeField] private GameObject _phStatusRed;
    [SerializeField] private GameObject _environmentalBeaconGreen;
    [SerializeField] private GameObject _environmentalBeaconRed;
    [SerializeField] private GameObject _dryStackSafeCover;
    [SerializeField] private GameObject[] _cakeBlocks;
    [SerializeField] private GameObject[] _dryStackPiles;
    [SerializeField] private Transform[] _filterPlates;
    [SerializeField] private Transform[] _conveyorRollers;
    [SerializeField] private ParticleSystem _limestoneDustFx;
    [SerializeField] private ParticleSystem _dryStackDustFx;

    [Header("=== Process Settings ===")]
    [SerializeField] private float _fadeDuration = 2.0f;
    [SerializeField] private float _fieldObservationDelay = 0.8f;
    [SerializeField] private float _neutralizationDuration = 7.5f;
    [SerializeField] private float _filterPressDuration = 8.5f;
    [SerializeField] private float _stackingDuration = 6.5f;
    [SerializeField] private float _pHStart = 7.5f;
    [SerializeField] private float _pHTarget = 8.5f;
    [SerializeField] private float _pHTolerance = 0.5f;
    [SerializeField] private float _moistureStart = 34f;
    [SerializeField] private float _moistureTarget = 22f;
    [SerializeField] private float _agitatorRpm = 24f;

    [Header("=== Runtime Status ===")]
    [SerializeField] private float _pHCurrent = 7.5f;
    [SerializeField] private float _cakeMoistureCurrent = 34f;
    [SerializeField] private float _dryStackProgress;

    [Header("=== Audio ===")]
    [SerializeField] private AudioSource _limeAudio;
    [SerializeField] private AudioSource _pressAudio;
    [SerializeField] private AudioSource _conveyorAudio;
    [SerializeField] private AudioSource _completeAudio;
    [Range(0f, 1f)] [SerializeField] private float _limeVolume = 0.30f;
    [Range(0f, 1f)] [SerializeField] private float _pressVolume = 0.38f;
    [Range(0f, 1f)] [SerializeField] private float _conveyorVolume = 0.34f;
    [Range(0f, 1f)] [SerializeField] private float _completeVolume = 0.30f;

    [Header("=== HUD Messages ===")]
    [TextArea(2, 4)] [SerializeField] private string _msgStart =
        "Level 13: Area dry stack tailing. Tekan tombol 13/local start untuk polishing pH dan pengamanan limbah B3.";
    [TextArea(2, 4)] [SerializeField] private string _msgNeutralizing =
        "Kapur/limestone masuk. Pantau pH naik ke rentang aman 8.0 sampai 9.0.";
    [TextArea(2, 4)] [SerializeField] private string _msgFiltering =
        "Final filter press aktif. Plat merapat, filtrate keluar, moisture cake turun di bawah 25%.";
    [TextArea(2, 4)] [SerializeField] private string _msgStacking =
        "Cake tailing bergerak ke dry stack storage. Area B3 terkunci dan containment aman.";
    [TextArea(2, 4)] [SerializeField] private string _msgComplete =
        "Dry stack aman. Lapor HT: 'tailing aman'.";

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
        UpdatePHVisuals(0f);
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
        StopAllProcessAudio();
    }

    private void OnLevelStarted(GameLevelManager.GameLevel level)
    {
        _levelActive = level == GameLevelManager.GameLevel.Level13_TailingWaste;
        if (!_levelActive)
        {
            SetProcessVisuals(false);
            StopSequence();
            StopAllProcessAudio();
            return;
        }

        _processStarted = false;
        _questComplete = false;
        _pHCurrent = _pHStart;
        _cakeMoistureCurrent = _moistureStart;
        _dryStackProgress = 0f;
        PushPHToManager();
        SetProcessVisuals(false);
        UpdatePHVisuals(0f);

        if (_hud != null)
            _hud.ShowNotifPublic(_msgStart);

        TeleportPlayer(_teleportTargetField);
    }

    private void Update()
    {
        if (!_levelActive || !_processStarted)
            return;

        if (_agitatorRoot != null)
            _agitatorRoot.Rotate(Vector3.up, _agitatorRpm * 6f * Time.deltaTime, Space.World);

        if (_conveyorRollers == null)
            return;

        for (int i = 0; i < _conveyorRollers.Length; i++)
        {
            if (_conveyorRollers[i] != null)
                _conveyorRollers[i].Rotate(Vector3.right, 220f * Time.deltaTime, Space.Self);
        }
    }

    private void OnDcsButtonPressed(int number)
    {
        if (!_levelActive || number != 12 || _processStarted)
            return;

        _processStarted = true;
        _sequenceCoroutine = StartCoroutine(RunDryStackSequence());
    }

    private IEnumerator RunDryStackSequence()
    {
        if (_hud != null)
            _hud.PlayManualFade(_fadeDuration);

        yield return new WaitForSeconds(_fadeDuration * 0.5f);
        TeleportPlayer(_teleportTargetField);
        yield return new WaitForSeconds(_fadeDuration * 0.5f + _fieldObservationDelay);

        if (_hud != null)
            _hud.ShowNotifPublic(_msgNeutralizing);

        SetProcessVisuals(true);
        StartAudio(_limeAudio, _limeVolume);

        float elapsed = 0f;
        while (elapsed < _neutralizationDuration)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.Clamp01(elapsed / _neutralizationDuration);
            float smooth = SmoothStep(t);
            _pHCurrent = Mathf.Lerp(_pHStart, _pHTarget, smooth);
            _dryStackProgress = Mathf.Lerp(0f, 32f, t);
            PushPHToManager();
            UpdatePHVisuals(t);
            UpdateLimestoneDosing(t);
            yield return null;
        }

        _pHCurrent = _pHTarget;
        PushPHToManager();
        UpdatePHVisuals(1f);
        if (_limestonePourStream != null) _limestonePourStream.SetActive(false);
        StopDust(_limestoneDustFx);
        StopAudio(_limeAudio);

        if (_hud != null)
            _hud.ShowNotifPublic(_msgFiltering);

        if (_polishedTailingFlow != null) _polishedTailingFlow.SetActive(true);
        if (_filtrateChannel != null) _filtrateChannel.SetActive(true);
        StartAudio(_pressAudio, _pressVolume);

        elapsed = 0f;
        while (elapsed < _filterPressDuration)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.Clamp01(elapsed / _filterPressDuration);
            _cakeMoistureCurrent = Mathf.Lerp(_moistureStart, _moistureTarget, SmoothStep(t));
            _dryStackProgress = Mathf.Lerp(32f, 70f, t);
            AnimateFilterPlates(t);
            UpdateCakeBlocks(t * 0.75f);
            UpdateDust(_dryStackDustFx, 0.2f + t * 0.35f);
            yield return null;
        }

        _cakeMoistureCurrent = _moistureTarget;
        StopAudio(_pressAudio);

        if (_hud != null)
            _hud.ShowNotifPublic(_msgStacking);

        StartAudio(_conveyorAudio, _conveyorVolume);

        elapsed = 0f;
        while (elapsed < _stackingDuration)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.Clamp01(elapsed / _stackingDuration);
            _dryStackProgress = Mathf.Lerp(70f, 100f, t);
            UpdateCakeBlocks(0.75f + t * 0.25f);
            UpdateDryStackPiles(t);
            UpdateDust(_dryStackDustFx, 0.45f + t * 0.35f);
            yield return null;
        }

        _dryStackProgress = 100f;
        UpdateCakeBlocks(1f);
        UpdateDryStackPiles(1f);
        if (_dryStackSafeCover != null) _dryStackSafeCover.SetActive(true);
        if (_environmentalBeaconRed != null) _environmentalBeaconRed.SetActive(false);
        if (_environmentalBeaconGreen != null) _environmentalBeaconGreen.SetActive(true);
        StopAudio(_conveyorAudio);
        StopDust(_dryStackDustFx);

        _questComplete = Mathf.Abs(_pHCurrent - _pHTarget) <= _pHTolerance && _cakeMoistureCurrent <= 25f;
        GameLevelManager.Instance?.NotifyLevel13DryStackComplete();
        StartAudio(_completeAudio, _completeVolume);

        if (_hud != null)
            _hud.ShowNotifPublic(_msgComplete);

        Debug.Log("[Level13] Dry stack tailing secured. pH=" + _pHCurrent.ToString("F1") +
                  " moisture=" + _cakeMoistureCurrent.ToString("F0") + "%");
        _sequenceCoroutine = null;
    }

    private float SmoothStep(float t)
    {
        t = Mathf.Clamp01(t);
        return t * t * (3f - 2f * t);
    }

    private void SetProcessVisuals(bool active)
    {
        if (_limestonePourStream != null) _limestonePourStream.SetActive(active);
        if (_polishedTailingFlow != null) _polishedTailingFlow.SetActive(false);
        if (_filtrateChannel != null) _filtrateChannel.SetActive(false);
        if (_dryStackSafeCover != null) _dryStackSafeCover.SetActive(false);
        if (_phStatusGreen != null) _phStatusGreen.SetActive(false);
        if (_phStatusRed != null) _phStatusRed.SetActive(true);
        if (_environmentalBeaconGreen != null) _environmentalBeaconGreen.SetActive(false);
        if (_environmentalBeaconRed != null) _environmentalBeaconRed.SetActive(true);

        if (_cakeBlocks != null)
        {
            for (int i = 0; i < _cakeBlocks.Length; i++)
                if (_cakeBlocks[i] != null)
                    _cakeBlocks[i].SetActive(false);
        }

        if (_dryStackPiles != null)
        {
            for (int i = 0; i < _dryStackPiles.Length; i++)
                if (_dryStackPiles[i] != null)
                    _dryStackPiles[i].SetActive(false);
        }

        StopDust(_limestoneDustFx);
        StopDust(_dryStackDustFx);
    }

    private void UpdatePHVisuals(float t)
    {
        if (_phMonitorNeedle != null)
        {
            Vector3 euler = _phMonitorNeedle.localEulerAngles;
            euler.z = Mathf.Lerp(-35f, 35f, SmoothStep(t));
            _phMonitorNeedle.localEulerAngles = euler;
        }

        bool safe = _pHCurrent >= 8f && _pHCurrent <= 9f;
        if (_phStatusGreen != null) _phStatusGreen.SetActive(safe);
        if (_phStatusRed != null) _phStatusRed.SetActive(!safe);
    }

    private void UpdateLimestoneDosing(float t)
    {
        if (_limestoneBag != null)
        {
            Vector3 euler = _limestoneBag.localEulerAngles;
            euler.z = Mathf.Lerp(-12f, -34f, Mathf.Sin(t * Mathf.PI));
            _limestoneBag.localEulerAngles = euler;
        }

        UpdateDust(_limestoneDustFx, 0.35f + t * 0.5f);
    }

    private void AnimateFilterPlates(float t)
    {
        if (_filterPlates == null || _filterPlates.Length == 0)
            return;

        float squeeze = Mathf.Lerp(1f, 0.55f, SmoothStep(t));
        float center = (_filterPlates.Length - 1) * 0.5f;
        for (int i = 0; i < _filterPlates.Length; i++)
        {
            if (_filterPlates[i] == null)
                continue;

            Vector3 local = _filterPlates[i].localPosition;
            local.x = (i - center) * 0.21f * squeeze;
            _filterPlates[i].localPosition = local;
        }
    }

    private void UpdateCakeBlocks(float t)
    {
        if (_cakeBlocks == null)
            return;

        for (int i = 0; i < _cakeBlocks.Length; i++)
        {
            if (_cakeBlocks[i] == null)
                continue;

            bool visible = t > 0.12f + i * 0.06f;
            _cakeBlocks[i].SetActive(visible);
            if (!visible)
                continue;

            Transform tr = _cakeBlocks[i].transform;
            Vector3 local = tr.localPosition;
            float travel = Mathf.Clamp01((t - 0.12f - i * 0.06f) / 0.65f);
            local.x = Mathf.Lerp(-2.15f + i * 0.55f, 2.25f, travel);
            local.y = Mathf.Lerp(0.92f, 0.84f, travel);
            tr.localPosition = local;
        }
    }

    private void UpdateDryStackPiles(float t)
    {
        if (_dryStackPiles == null)
            return;

        for (int i = 0; i < _dryStackPiles.Length; i++)
        {
            if (_dryStackPiles[i] == null)
                continue;

            bool visible = t > i * 0.12f;
            _dryStackPiles[i].SetActive(visible);
            if (visible)
            {
                float pop = Mathf.Clamp01((t - i * 0.12f) / 0.2f);
                Vector3 scale = _dryStackPiles[i].transform.localScale;
                scale.y = Mathf.Lerp(0.05f, 0.35f, SmoothStep(pop));
                _dryStackPiles[i].transform.localScale = scale;
            }
        }
    }

    private void UpdateDust(ParticleSystem particle, float intensity)
    {
        if (particle == null)
            return;

        if (!particle.isPlaying)
            particle.Play();

        var emission = particle.emission;
        emission.rateOverTime = Mathf.Lerp(0f, 55f, Mathf.Clamp01(intensity));
    }

    private void StopDust(ParticleSystem particle)
    {
        if (particle != null)
            particle.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
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
            Debug.LogWarning("[Level13] XROrigin component not found. Teleport skipped.");
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
        if (_limeAudio == null)
            _limeAudio = CreateAudioSource("L13_Lime_Dosing_Audio", GenerateNoiseMotorClip("Level13LimeDosing", 3f, 22050, 95f, 0.18f), true, 0.25f);

        if (_pressAudio == null)
            _pressAudio = CreateAudioSource("L13_FinalPress_Audio", GenerateNoiseMotorClip("Level13FinalPress", 4f, 22050, 46f, 0.30f), true, 0.30f);

        if (_conveyorAudio == null)
            _conveyorAudio = CreateAudioSource("L13_Conveyor_Audio", GenerateNoiseMotorClip("Level13Conveyor", 3f, 22050, 62f, 0.24f), true, 0.35f);

        if (_completeAudio == null)
            _completeAudio = CreateAudioSource("L13_Complete_Audio", GenerateCompleteClip(1.3f, 22050), false, 0.15f);
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

    private void StopAllProcessAudio()
    {
        StopAudio(_limeAudio);
        StopAudio(_pressAudio);
        StopAudio(_conveyorAudio);
        StopAudio(_completeAudio);
    }

    private AudioClip GenerateNoiseMotorClip(string name, float duration, int sampleRate, float frequency, float noiseAmount)
    {
        int total = Mathf.CeilToInt(duration * sampleRate);
        float[] data = new float[total];
        System.Random random = new System.Random(name.GetHashCode());
        float phase = 0f;
        float filter = 0f;

        for (int i = 0; i < total; i++)
        {
            float time = (float)i / sampleRate;
            phase += 2f * Mathf.PI * frequency / sampleRate;
            float motor = Mathf.Sin(phase) * 0.28f;
            float pulse = Mathf.Abs(Mathf.Sin(2f * Mathf.PI * 1.25f * time));
            float noise = ((float)random.NextDouble() - 0.5f) * noiseAmount;
            filter += 0.05f * (noise - filter);
            data[i] = (motor * (0.65f + pulse * 0.35f) + filter) * 0.50f;
        }

        AudioClip clip = AudioClip.Create(name, total, 1, sampleRate, false);
        clip.SetData(data, 0);
        return clip;
    }

    private AudioClip GenerateCompleteClip(float duration, int sampleRate)
    {
        int total = Mathf.CeilToInt(duration * sampleRate);
        float[] data = new float[total];

        for (int i = 0; i < total; i++)
        {
            float time = (float)i / sampleRate;
            float envelope = Mathf.Clamp01(1f - time / duration);
            float tone = Mathf.Sin(2f * Mathf.PI * 520f * time) * 0.18f;
            float harmonic = Mathf.Sin(2f * Mathf.PI * 780f * time) * 0.11f;
            data[i] = (tone + harmonic) * envelope;
        }

        AudioClip clip = AudioClip.Create("Level13Complete", total, 1, sampleRate, false);
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
            GameObject field = GameObject.Find("SpawnPoint_Lvl13");
            if (field != null)
                _teleportTargetField = field.transform;
        }

        if (_dryStackField == null)
            _dryStackField = GameObject.Find("Mesin Utama/Level13_DryStack_Field") ?? GameObject.Find("Level13_DryStack_Field");

        if (_dryStackField == null)
            return;

        Transform root = _dryStackField.transform;
        if (_agitatorRoot == null) _agitatorRoot = root.Find("Final_Neutralization_Tank/Polishing_Agitator_Root");
        if (_limestoneBag == null) _limestoneBag = root.Find("Final_Neutralization_Tank/Limestone_Bag");
        if (_limestonePourStream == null) _limestonePourStream = FindChild(root, "Final_Neutralization_Tank/Limestone_Pour_Stream");
        if (_polishedTailingFlow == null) _polishedTailingFlow = FindChild(root, "Polished_Tailing_Flow");
        if (_filtrateChannel == null) _filtrateChannel = FindChild(root, "Final_FilterPress_Unit/Filtrate_Channel");
        if (_phMonitorNeedle == null) _phMonitorNeedle = root.Find("pH_Monitor_Panel/pH_Monitor_Needle");
        if (_phStatusGreen == null) _phStatusGreen = FindChild(root, "pH_Monitor_Panel/pH_Status_Green");
        if (_phStatusRed == null) _phStatusRed = FindChild(root, "pH_Monitor_Panel/pH_Status_Red");
        if (_environmentalBeaconGreen == null) _environmentalBeaconGreen = FindChild(root, "Environmental_Beacon_Green");
        if (_environmentalBeaconRed == null) _environmentalBeaconRed = FindChild(root, "Environmental_Beacon_Red");
        if (_dryStackSafeCover == null) _dryStackSafeCover = FindChild(root, "DryStack_Storage/DryStack_SafeCover");

        if (_limestoneDustFx == null)
        {
            Transform fx = root.Find("Limestone_Dust_FX");
            if (fx != null) _limestoneDustFx = fx.GetComponent<ParticleSystem>();
        }

        if (_dryStackDustFx == null)
        {
            Transform fx = root.Find("DryStack_Dust_FX");
            if (fx != null) _dryStackDustFx = fx.GetComponent<ParticleSystem>();
        }

        if (_cakeBlocks == null || _cakeBlocks.Length == 0)
            _cakeBlocks = GetChildren(root.Find("Cake_Transfer_Conveyor/Cake_On_Conveyor"), "Cake_Block_");

        if (_dryStackPiles == null || _dryStackPiles.Length == 0)
            _dryStackPiles = GetChildren(root.Find("DryStack_Storage"), "DryStack_Pile_");

        if (_filterPlates == null || _filterPlates.Length == 0)
            _filterPlates = GetChildTransforms(root.Find("Final_FilterPress_Unit"), "PressPlate_");

        if (_conveyorRollers == null || _conveyorRollers.Length == 0)
            _conveyorRollers = GetChildTransforms(root.Find("Cake_Transfer_Conveyor"), "Conveyor_Roller_");
    }

    private GameObject[] GetChildren(Transform parent, string prefix)
    {
        if (parent == null)
            return new GameObject[0];

        System.Collections.Generic.List<GameObject> results = new System.Collections.Generic.List<GameObject>();
        for (int i = 0; i < parent.childCount; i++)
        {
            Transform child = parent.GetChild(i);
            if (child.name.StartsWith(prefix))
                results.Add(child.gameObject);
        }

        results.Sort((a, b) => string.CompareOrdinal(a.name, b.name));
        return results.ToArray();
    }

    private Transform[] GetChildTransforms(Transform parent, string prefix)
    {
        if (parent == null)
            return new Transform[0];

        System.Collections.Generic.List<Transform> results = new System.Collections.Generic.List<Transform>();
        for (int i = 0; i < parent.childCount; i++)
        {
            Transform child = parent.GetChild(i);
            if (child.name.StartsWith(prefix))
                results.Add(child);
        }

        results.Sort((a, b) => string.CompareOrdinal(a.name, b.name));
        return results.ToArray();
    }

    private GameObject FindChild(Transform root, string path)
    {
        Transform child = root.Find(path);
        return child != null ? child.gameObject : null;
    }

    public bool QuestComplete => _questComplete;
    public float PHCurrent => _pHCurrent;
    public float CakeMoistureCurrent => _cakeMoistureCurrent;
    public float DryStackProgress => _dryStackProgress;
}
