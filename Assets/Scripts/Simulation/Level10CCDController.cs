using System.Collections;
using Unity.XR.CoreUtils;
using UnityEngine;

/// <summary>
/// OLIVIA VR - Level10CCDController.cs
///
/// Level 10 activates the CCD solid-liquid separation train after flash/letdown.
/// The player starts the system from DCS, observes slurry entering the CCD tanks,
/// rake arms rotating, solids settling, and clarified overflow moving onward.
/// </summary>
public class Level10CCDController : MonoBehaviour
{
    [Header("=== Player & Teleport ===")]
    [SerializeField] private Transform _playerRigRoot;
    [SerializeField] private Transform _teleportTargetDcs;
    [SerializeField] private Transform _teleportTargetField;

    [Header("=== CCD References ===")]
    [SerializeField] private GameObject _ccdField;
    [SerializeField] private Transform[] _rakeArmRoots;
    [SerializeField] private GameObject _feedLiquid;
    [SerializeField] private GameObject _overflowLiquid;
    [SerializeField] private GameObject[] _settledMudLayers;
    [SerializeField] private ParticleSystem _separationFx;

    [Header("=== Process Timing ===")]
    [SerializeField] private float _fadeDuration = 2.5f;
    [SerializeField] private float _fieldObservationDelay = 1.0f;
    [SerializeField] private float _separationDuration = 14f;
    [SerializeField] private float _rakeRpm = 4f;

    [Header("=== Process Quality ===")]
    [SerializeField] private float _solidsSettlingTarget = 92f;
    [SerializeField] private float _clarityTarget = 88f;
    [SerializeField] private float _progressCurrent;
    [SerializeField] private float _solidsSettlingCurrent;
    [SerializeField] private float _clarityCurrent;

    [Header("=== Audio ===")]
    [SerializeField] private AudioSource _driveAudio;
    [SerializeField] private AudioSource _separationCompleteAudio;
    [Range(0f, 1f)] [SerializeField] private float _driveVolume = 0.35f;
    [Range(0f, 1f)] [SerializeField] private float _completeVolume = 0.3f;

    [Header("=== HUD Messages ===")]
    [TextArea(2, 4)] [SerializeField] private string _msgStart =
        "Level 10: CCD siap. Tekan tombol DCS 10 untuk mulai pemisahan padat-cair.";
    [TextArea(2, 4)] [SerializeField] private string _msgObserve =
        "CCD aktif. Amati slurry masuk, rake arm berputar, dan padatan mengendap.";
    [TextArea(2, 4)] [SerializeField] private string _msgComplete =
        "CCD stabil. Pemisahan padat-cair sudah berjalan. Lapor HT: 'ccd aktif'.";

    private PlayerHUD _hud;
    private Coroutine _sequenceCoroutine;
    private bool _levelActive;
    private bool _ccdStarted;
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
        StopAudio(_driveAudio);
        StopAudio(_separationCompleteAudio);
    }

    private void OnLevelStarted(GameLevelManager.GameLevel level)
    {
        _levelActive = level == GameLevelManager.GameLevel.Level10_CCD;
        if (!_levelActive)
        {
            SetProcessVisuals(false);
            StopSequence();
            StopAudio(_driveAudio);
            return;
        }

        _ccdStarted = false;
        _questComplete = false;
        _progressCurrent = 0f;
        _solidsSettlingCurrent = 0f;
        _clarityCurrent = 0f;
        SetProcessVisuals(false);

        if (_hud != null)
            _hud.ShowNotifPublic(_msgStart);

        TeleportPlayer(_teleportTargetDcs);
    }

    private void Update()
    {
        if (!_levelActive || !_ccdStarted)
            return;

        AnimateRakeArms();
    }

    private void OnDcsButtonPressed(int number)
    {
        if (!_levelActive || number != 10 || _ccdStarted)
            return;

        _ccdStarted = true;
        _sequenceCoroutine = StartCoroutine(RunCCDSequence());
    }

    private IEnumerator RunCCDSequence()
    {
        if (_hud != null)
            _hud.PlayManualFade(_fadeDuration);

        yield return new WaitForSeconds(_fadeDuration * 0.5f);
        TeleportPlayer(_teleportTargetField);
        yield return new WaitForSeconds(_fadeDuration * 0.5f + _fieldObservationDelay);

        if (_hud != null)
            _hud.ShowNotifPublic(_msgObserve);

        SetProcessVisuals(true);
        StartAudio(_driveAudio, _driveVolume);

        float elapsed = 0f;
        while (elapsed < _separationDuration)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.Clamp01(elapsed / _separationDuration);
            _progressCurrent = t * 100f;
            _solidsSettlingCurrent = Mathf.Lerp(0f, _solidsSettlingTarget, SmoothStep(t));
            _clarityCurrent = Mathf.Lerp(0f, _clarityTarget, SmoothStep(Mathf.Clamp01(t - 0.15f) / 0.85f));
            if (_overflowLiquid != null && _clarityCurrent >= _clarityTarget * 0.45f)
                _overflowLiquid.SetActive(true);
            UpdateMudLayers(t);
            UpdateSeparationFx(t);
            yield return null;
        }

        _progressCurrent = 100f;
        _solidsSettlingCurrent = _solidsSettlingTarget;
        _clarityCurrent = _clarityTarget;
        UpdateMudLayers(1f);
        UpdateSeparationFx(0.18f);
        StopAudio(_driveAudio);
        StartAudio(_separationCompleteAudio, _completeVolume);
        _questComplete = true;
        GameLevelManager.Instance?.NotifyLevel10CCDComplete();

        if (_hud != null)
            _hud.ShowNotifPublic(_msgComplete);

        Debug.Log("[Level10] CCD separation stable. Player can report via WT.");
        _sequenceCoroutine = null;
    }

    private float SmoothStep(float t)
    {
        t = Mathf.Clamp01(t);
        return t * t * (3f - 2f * t);
    }

    private void AnimateRakeArms()
    {
        if (_rakeArmRoots == null)
            return;

        float degPerSecond = _rakeRpm * 6f;
        for (int i = 0; i < _rakeArmRoots.Length; i++)
        {
            if (_rakeArmRoots[i] != null)
                _rakeArmRoots[i].Rotate(Vector3.up, degPerSecond * Time.deltaTime, Space.World);
        }
    }

    private void UpdateMudLayers(float t)
    {
        if (_settledMudLayers == null)
            return;

        for (int i = 0; i < _settledMudLayers.Length; i++)
        {
            GameObject mud = _settledMudLayers[i];
            if (mud == null)
                continue;

            mud.SetActive(t > 0.18f + i * 0.08f);
            float scaleY = Mathf.Lerp(0.04f, 0.18f + i * 0.025f, Mathf.Clamp01((t - 0.18f) / 0.82f));
            Vector3 scale = mud.transform.localScale;
            scale.y = scaleY;
            mud.transform.localScale = scale;
        }
    }

    private void SetProcessVisuals(bool active)
    {
        if (_feedLiquid != null)
            _feedLiquid.SetActive(active);
        if (_overflowLiquid != null)
            _overflowLiquid.SetActive(active && _clarityCurrent >= _clarityTarget * 0.45f);

        if (_settledMudLayers != null)
        {
            for (int i = 0; i < _settledMudLayers.Length; i++)
                if (_settledMudLayers[i] != null)
                    _settledMudLayers[i].SetActive(false);
        }

        if (_separationFx == null)
            return;

        if (active)
        {
            UpdateSeparationFx(1f);
            if (!_separationFx.isPlaying)
                _separationFx.Play();
        }
        else
        {
            _separationFx.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
        }
    }

    private void UpdateSeparationFx(float intensity)
    {
        if (_separationFx == null)
            return;

        var emission = _separationFx.emission;
        emission.rateOverTime = Mathf.Lerp(8f, 60f, Mathf.Clamp01(intensity));
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
            Debug.LogWarning("[Level10] XROrigin component not found. Teleport skipped to avoid tracker snapback.");
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
        if (_driveAudio == null)
        {
            GameObject go = new GameObject("L10_CCDDrive_Audio");
            go.transform.SetParent(transform, false);
            _driveAudio = go.AddComponent<AudioSource>();
            _driveAudio.loop = true;
            _driveAudio.playOnAwake = false;
            _driveAudio.spatialBlend = 0.25f;
            _driveAudio.clip = GenerateDriveClip(4f, 22050);
        }

        if (_separationCompleteAudio == null)
        {
            GameObject go = new GameObject("L10_CCDComplete_Audio");
            go.transform.SetParent(transform, false);
            _separationCompleteAudio = go.AddComponent<AudioSource>();
            _separationCompleteAudio.loop = false;
            _separationCompleteAudio.playOnAwake = false;
            _separationCompleteAudio.spatialBlend = 0.15f;
            _separationCompleteAudio.clip = GenerateCompleteClip(1.2f, 22050);
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

    private AudioClip GenerateDriveClip(float duration, int sampleRate)
    {
        int total = Mathf.CeilToInt(duration * sampleRate);
        float[] data = new float[total];
        System.Random random = new System.Random(10010);
        float phaseA = 0f;
        float filter = 0f;

        for (int i = 0; i < total; i++)
        {
            phaseA += 2f * Mathf.PI * 58f / sampleRate;
            float motor = Mathf.Sin(phaseA) * 0.34f;
            float noise = ((float)random.NextDouble() - 0.5f) * 0.22f;
            filter += 0.05f * (noise - filter);
            float rakePulse = 0.75f + Mathf.Abs(Mathf.Sin(phaseA * 0.08f)) * 0.25f;
            data[i] = (motor + filter) * rakePulse * 0.45f;
        }

        AudioClip clip = AudioClip.Create("Level10CCDDrive", total, 1, sampleRate, false);
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
            float tone = Mathf.Sin(2f * Mathf.PI * 480f * time) * 0.22f;
            float harmonic = Mathf.Sin(2f * Mathf.PI * 720f * time) * 0.14f;
            data[i] = (tone + harmonic) * envelope;
        }

        AudioClip clip = AudioClip.Create("Level10CCDComplete", total, 1, sampleRate, false);
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
            GameObject field = GameObject.Find("SpawnPoint_Lvl10");
            if (field != null)
                _teleportTargetField = field.transform;
        }

        if (_ccdField == null)
            _ccdField = GameObject.Find("Mesin Utama/CCD_Field") ?? GameObject.Find("CCD_Field");

        if (_ccdField == null)
            return;

        Transform root = _ccdField.transform;
        Transform rigRoot = FindDeepChild(root, "CCD_BlenderRig") ?? root;
        if (_feedLiquid == null)
        {
            Transform liquid = FindDeepChild(rigRoot, "Feed_Inlet_FromFlash_Liquid");
            if (liquid != null)
                _feedLiquid = liquid.gameObject;
        }

        if (_overflowLiquid == null)
        {
            Transform liquid = FindDeepChild(rigRoot, "Overflow_ToPurification_Liquid");
            if (liquid != null)
                _overflowLiquid = liquid.gameObject;
        }

        if (_separationFx == null)
        {
            Transform fx = FindDeepChild(root, "CCD_Separation_FX");
            if (fx != null)
                _separationFx = fx.GetComponent<ParticleSystem>();
        }

        if (_rakeArmRoots == null || _rakeArmRoots.Length == 0)
        {
            System.Collections.Generic.List<Transform> rakeRoots = new System.Collections.Generic.List<Transform>();
            FindDeepChildren(rigRoot, "Rake_Arm_Root", rakeRoots);
            _rakeArmRoots = rakeRoots.ToArray();
        }

        if (_settledMudLayers == null || _settledMudLayers.Length == 0)
        {
            System.Collections.Generic.List<Transform> mudLayers = new System.Collections.Generic.List<Transform>();
            FindDeepChildren(rigRoot, "Settled_Underflow_Mud", mudLayers);
            _settledMudLayers = new GameObject[mudLayers.Count];
            for (int i = 0; i < mudLayers.Count; i++)
                _settledMudLayers[i] = mudLayers[i].gameObject;
        }
    }

    private Transform FindDeepChild(Transform root, string childName)
    {
        if (root == null || string.IsNullOrEmpty(childName))
            return null;

        foreach (Transform child in root)
        {
            if (child.name == childName)
                return child;

            Transform nested = FindDeepChild(child, childName);
            if (nested != null)
                return nested;
        }

        return null;
    }

    private void FindDeepChildren(Transform root, string childName, System.Collections.Generic.List<Transform> results)
    {
        if (root == null || string.IsNullOrEmpty(childName) || results == null)
            return;

        foreach (Transform child in root)
        {
            if (child.name == childName || child.name.StartsWith(childName + ".", System.StringComparison.Ordinal))
                results.Add(child);

            FindDeepChildren(child, childName, results);
        }
    }

    public bool QuestComplete => _questComplete;
    public float ProgressCurrent => _progressCurrent;
    public float SolidsSettlingCurrent => _solidsSettlingCurrent;
    public float ClarityCurrent => _clarityCurrent;
}
