using System.Collections;
using Unity.XR.CoreUtils;
using UnityEngine;

/// <summary>
/// OLIVIA VR - Level12TailingFilterController.cs
///
/// Level 12 connects the MHP/purification train to tailing treatment. The player
/// starts tailing discharge from DCS, observes neutralization, filter press
/// dewatering, and filter cake output before reporting.
/// </summary>
public class Level12TailingFilterController : MonoBehaviour
{
    [Header("=== Player & Teleport ===")]
    [SerializeField] private Transform _playerRigRoot;
    [SerializeField] private Transform _teleportTargetDcs;
    [SerializeField] private Transform _teleportTargetField;

    [Header("=== Machine References ===")]
    [SerializeField] private GameObject _tailingField;
    [SerializeField] private Transform _agitatorRoot;
    [SerializeField] private GameObject _tailingFeedLiquid;
    [SerializeField] private GameObject _limeDosingFlow;
    [SerializeField] private GameObject _neutralizedSurface;
    [SerializeField] private GameObject _pressFeedLiquid;
    [SerializeField] private GameObject _filtrateLiquid;
    [SerializeField] private GameObject _cakeBinProduct;
    [SerializeField] private GameObject[] _cakeBlocks;
    [SerializeField] private Transform[] _filterPlates;
    [SerializeField] private ParticleSystem _neutralizationFx;
    [SerializeField] private ParticleSystem _dewaterFx;

    [Header("=== Process Settings ===")]
    [SerializeField] private float _fadeDuration = 2.5f;
    [SerializeField] private float _fieldObservationDelay = 1f;
    [SerializeField] private float _neutralizationDuration = 8f;
    [SerializeField] private float _filterPressDuration = 10f;
    [SerializeField] private float _pHStart = 5.5f;
    [SerializeField] private float _pHTarget = 7.5f;
    [SerializeField] private float _pHTolerance = 0.4f;
    [SerializeField] private float _agitatorRpm = 28f;

    [Header("=== Runtime Status ===")]
    [SerializeField] private float _pHCurrent = 5.5f;
    [SerializeField] private float _cakeDrynessCurrent;
    [SerializeField] private float _processProgress;

    [Header("=== Audio ===")]
    [SerializeField] private AudioSource _neutralizationAudio;
    [SerializeField] private AudioSource _filterPressAudio;
    [SerializeField] private AudioSource _completeAudio;
    [Range(0f, 1f)] [SerializeField] private float _neutralizationVolume = 0.34f;
    [Range(0f, 1f)] [SerializeField] private float _filterPressVolume = 0.42f;
    [Range(0f, 1f)] [SerializeField] private float _completeVolume = 0.30f;

    [Header("=== HUD Messages ===")]
    [TextArea(2, 4)] [SerializeField] private string _msgStart =
        "Level 12: Tailing treatment siap. Tekan DCS 12 untuk alirkan tailing ke netralisasi dan filter press.";
    [TextArea(2, 4)] [SerializeField] private string _msgNeutralizing =
        "Tailing masuk neutralization tank. Lime dosing aktif, pH naik menuju aman.";
    [TextArea(2, 4)] [SerializeField] private string _msgFiltering =
        "Tailing netral masuk filter press. Amati dewatering dan cake terbentuk.";
    [TextArea(2, 4)] [SerializeField] private string _msgComplete =
        "Tailing sudah dinetralkan dan filter press selesai. Lapor HT: 'limbah dialirkan'.";

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
        StopAudio(_neutralizationAudio);
        StopAudio(_filterPressAudio);
        StopAudio(_completeAudio);
    }

    private void OnLevelStarted(GameLevelManager.GameLevel level)
    {
        _levelActive = level == GameLevelManager.GameLevel.Level12_TailingDischarge;
        if (!_levelActive)
        {
            SetProcessVisuals(false);
            StopSequence();
            StopAudio(_neutralizationAudio);
            StopAudio(_filterPressAudio);
            return;
        }

        _processStarted = false;
        _questComplete = false;
        _pHCurrent = _pHStart;
        _cakeDrynessCurrent = 0f;
        _processProgress = 0f;
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

        if (_agitatorRoot != null)
            _agitatorRoot.Rotate(Vector3.up, _agitatorRpm * 6f * Time.deltaTime, Space.World);
    }

    private void OnDcsButtonPressed(int number)
    {
        if (!_levelActive || number != 12 || _processStarted)
            return;

        _processStarted = true;
        _sequenceCoroutine = StartCoroutine(RunTailingSequence());
    }

    private IEnumerator RunTailingSequence()
    {
        if (_hud != null)
            _hud.PlayManualFade(_fadeDuration);

        yield return new WaitForSeconds(_fadeDuration * 0.5f);
        TeleportPlayer(_teleportTargetField);
        yield return new WaitForSeconds(_fadeDuration * 0.5f + _fieldObservationDelay);

        if (_hud != null)
            _hud.ShowNotifPublic(_msgNeutralizing);

        SetProcessVisuals(true);
        StartAudio(_neutralizationAudio, _neutralizationVolume);

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
        StopAudio(_neutralizationAudio);
        if (_pressFeedLiquid != null) _pressFeedLiquid.SetActive(true);
        if (_filtrateLiquid != null) _filtrateLiquid.SetActive(true);
        StartAudio(_filterPressAudio, _filterPressVolume);

        if (_hud != null)
            _hud.ShowNotifPublic(_msgFiltering);

        elapsed = 0f;
        while (elapsed < _filterPressDuration)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.Clamp01(elapsed / _filterPressDuration);
            _cakeDrynessCurrent = Mathf.Lerp(0f, 88f, SmoothStep(t));
            _processProgress = Mathf.Lerp(45f, 100f, t);
            AnimateFilterPlates(t);
            UpdateCakeOutput(t);
            UpdateDewaterFx(t);
            yield return null;
        }

        _cakeDrynessCurrent = 88f;
        _processProgress = 100f;
        UpdateCakeOutput(1f);
        if (_cakeBinProduct != null) _cakeBinProduct.SetActive(true);
        StopAudio(_filterPressAudio);
        StartAudio(_completeAudio, _completeVolume);
        _questComplete = Mathf.Abs(_pHCurrent - _pHTarget) <= _pHTolerance && _cakeDrynessCurrent >= 85f;
        GameLevelManager.Instance?.NotifyLevel12TailingFilterComplete();

        if (_hud != null)
            _hud.ShowNotifPublic(_msgComplete);

        Debug.Log("[Level12] Tailing neutralization and filter press complete. Player can report via WT.");
        _sequenceCoroutine = null;
    }

    private float SmoothStep(float t)
    {
        t = Mathf.Clamp01(t);
        return t * t * (3f - 2f * t);
    }

    private void SetProcessVisuals(bool active)
    {
        if (_tailingFeedLiquid != null) _tailingFeedLiquid.SetActive(active);
        if (_limeDosingFlow != null) _limeDosingFlow.SetActive(active);
        if (_neutralizedSurface != null) _neutralizedSurface.SetActive(active);
        if (_pressFeedLiquid != null) _pressFeedLiquid.SetActive(false);
        if (_filtrateLiquid != null) _filtrateLiquid.SetActive(false);
        if (_cakeBinProduct != null) _cakeBinProduct.SetActive(false);

        if (_cakeBlocks != null)
        {
            for (int i = 0; i < _cakeBlocks.Length; i++)
                if (_cakeBlocks[i] != null)
                    _cakeBlocks[i].SetActive(false);
        }

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

        if (_dewaterFx != null)
            _dewaterFx.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
    }

    private void AnimateFilterPlates(float t)
    {
        if (_filterPlates == null)
            return;

        float squeeze = Mathf.Lerp(1f, 0.62f, SmoothStep(t));
        for (int i = 0; i < _filterPlates.Length; i++)
        {
            if (_filterPlates[i] == null)
                continue;

            Vector3 local = _filterPlates[i].localPosition;
            float centerIndex = (_filterPlates.Length - 1) * 0.5f;
            local.x = (i - centerIndex) * 0.36f * squeeze;
            _filterPlates[i].localPosition = local;
        }
    }

    private void UpdateCakeOutput(float t)
    {
        if (_cakeBlocks == null)
            return;

        for (int i = 0; i < _cakeBlocks.Length; i++)
        {
            if (_cakeBlocks[i] == null)
                continue;

            _cakeBlocks[i].SetActive(t > 0.22f + i * 0.075f);
        }
    }

    private void UpdateNeutralizationFx(float intensity)
    {
        if (_neutralizationFx == null)
            return;

        var emission = _neutralizationFx.emission;
        emission.rateOverTime = Mathf.Lerp(10f, 62f, Mathf.Clamp01(intensity));
    }

    private void UpdateDewaterFx(float intensity)
    {
        if (_dewaterFx == null)
            return;

        if (!_dewaterFx.isPlaying)
            _dewaterFx.Play();

        var emission = _dewaterFx.emission;
        emission.rateOverTime = Mathf.Lerp(8f, 52f, Mathf.Clamp01(intensity));
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
            Debug.LogWarning("[Level12] XROrigin component not found. Teleport skipped to avoid tracker snapback.");
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
        if (_neutralizationAudio == null)
        {
            GameObject go = new GameObject("L12_Neutralization_Audio");
            go.transform.SetParent(transform, false);
            _neutralizationAudio = go.AddComponent<AudioSource>();
            _neutralizationAudio.loop = true;
            _neutralizationAudio.playOnAwake = false;
            _neutralizationAudio.spatialBlend = 0.25f;
            _neutralizationAudio.clip = GenerateNeutralizationClip(3f, 22050);
        }

        if (_filterPressAudio == null)
        {
            GameObject go = new GameObject("L12_FilterPress_Audio");
            go.transform.SetParent(transform, false);
            _filterPressAudio = go.AddComponent<AudioSource>();
            _filterPressAudio.loop = true;
            _filterPressAudio.playOnAwake = false;
            _filterPressAudio.spatialBlend = 0.28f;
            _filterPressAudio.clip = GenerateFilterPressClip(4f, 22050);
        }

        if (_completeAudio == null)
        {
            GameObject go = new GameObject("L12_Complete_Audio");
            go.transform.SetParent(transform, false);
            _completeAudio = go.AddComponent<AudioSource>();
            _completeAudio.loop = false;
            _completeAudio.playOnAwake = false;
            _completeAudio.spatialBlend = 0.15f;
            _completeAudio.clip = GenerateCompleteClip(1.2f, 22050);
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

    private AudioClip GenerateNeutralizationClip(float duration, int sampleRate)
    {
        int total = Mathf.CeilToInt(duration * sampleRate);
        float[] data = new float[total];
        System.Random random = new System.Random(1201);
        float phase = 0f;
        float filter = 0f;

        for (int i = 0; i < total; i++)
        {
            phase += 2f * Mathf.PI * 72f / sampleRate;
            float motor = Mathf.Sin(phase) * 0.28f;
            float noise = ((float)random.NextDouble() - 0.5f) * 0.25f;
            filter += 0.06f * (noise - filter);
            data[i] = (motor + filter) * 0.45f;
        }

        AudioClip clip = AudioClip.Create("Level12Neutralization", total, 1, sampleRate, false);
        clip.SetData(data, 0);
        return clip;
    }

    private AudioClip GenerateFilterPressClip(float duration, int sampleRate)
    {
        int total = Mathf.CeilToInt(duration * sampleRate);
        float[] data = new float[total];
        System.Random random = new System.Random(1202);
        float phase = 0f;

        for (int i = 0; i < total; i++)
        {
            float time = (float)i / sampleRate;
            phase += 2f * Mathf.PI * 48f / sampleRate;
            float hydraulic = Mathf.Sin(phase) * 0.36f;
            float pumpPulse = Mathf.Abs(Mathf.Sin(2f * Mathf.PI * 1.1f * time));
            float noise = ((float)random.NextDouble() - 0.5f) * 0.18f;
            data[i] = (hydraulic * (0.65f + pumpPulse * 0.35f) + noise) * 0.45f;
        }

        AudioClip clip = AudioClip.Create("Level12FilterPress", total, 1, sampleRate, false);
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
            float tone = Mathf.Sin(2f * Mathf.PI * 450f * time) * 0.20f;
            float harmonic = Mathf.Sin(2f * Mathf.PI * 675f * time) * 0.13f;
            data[i] = (tone + harmonic) * envelope;
        }

        AudioClip clip = AudioClip.Create("Level12Complete", total, 1, sampleRate, false);
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
            GameObject field = GameObject.Find("SpawnPoint_Lvl12");
            if (field != null)
                _teleportTargetField = field.transform;
        }

        if (_tailingField == null)
            _tailingField = GameObject.Find("Mesin Utama/Level12_TailingFilter_Field") ?? GameObject.Find("Level12_TailingFilter_Field");

        if (_tailingField == null)
            return;

        Transform root = _tailingField.transform;
        if (_agitatorRoot == null) _agitatorRoot = root.Find("Tailing_Neutralization_Tank/Neutralizer_Agitator_Root");
        if (_tailingFeedLiquid == null) _tailingFeedLiquid = FindChild(root, "Tailing_Feed_From_MHP_Liquid");
        if (_limeDosingFlow == null) _limeDosingFlow = FindChild(root, "Lime_Dosing_Flow");
        if (_neutralizedSurface == null) _neutralizedSurface = FindChild(root, "Tailing_Neutralization_Tank/Neutralized_Surface");
        if (_pressFeedLiquid == null) _pressFeedLiquid = FindChild(root, "Neutralized_To_FilterPress_Liquid");
        if (_filtrateLiquid == null) _filtrateLiquid = FindChild(root, "Filtrate_Outlet_Liquid");
        if (_cakeBinProduct == null) _cakeBinProduct = FindChild(root, "Cake_Bin_Product");

        if (_neutralizationFx == null)
        {
            Transform fx = root.Find("TailingNeutralization_FX");
            if (fx != null) _neutralizationFx = fx.GetComponent<ParticleSystem>();
        }

        if (_dewaterFx == null)
        {
            Transform fx = root.Find("FilterPress_Dewater_FX");
            if (fx != null) _dewaterFx = fx.GetComponent<ParticleSystem>();
        }

        if (_cakeBlocks == null || _cakeBlocks.Length == 0)
        {
            Transform cakeRoot = root.Find("FilterPress_Unit/FilterCake_Output");
            if (cakeRoot != null)
            {
                _cakeBlocks = new GameObject[cakeRoot.childCount];
                for (int i = 0; i < cakeRoot.childCount; i++)
                    _cakeBlocks[i] = cakeRoot.GetChild(i).gameObject;
            }
        }

        if (_filterPlates == null || _filterPlates.Length == 0)
        {
            Transform press = root.Find("FilterPress_Unit");
            if (press != null)
            {
                System.Collections.Generic.List<Transform> plates = new System.Collections.Generic.List<Transform>();
                for (int i = 0; i < press.childCount; i++)
                {
                    Transform child = press.GetChild(i);
                    if (child.name.StartsWith("FilterPlate_"))
                        plates.Add(child);
                }
                _filterPlates = plates.ToArray();
            }
        }
    }

    private GameObject FindChild(Transform root, string path)
    {
        Transform child = root.Find(path);
        return child != null ? child.gameObject : null;
    }

    public bool QuestComplete => _questComplete;
    public float PHCurrent => _pHCurrent;
    public float CakeDrynessCurrent => _cakeDrynessCurrent;
    public float ProcessProgress => _processProgress;
}
