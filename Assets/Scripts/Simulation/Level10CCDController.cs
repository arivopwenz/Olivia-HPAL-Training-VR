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

    // ----- Real industrial model drivers (CCDIndustrialUVRedesign) -----
    // Rake bridges berputar pelan mengelilingi sumbu vertikal tiap thickener.
    private readonly Vector3[] _rakeTankAxis = new Vector3[3];   // titik pusat tank (world)
    private readonly float[] _rakeAngle = new float[3];          // akumulasi sudut
    private Transform[] _driveMotors;                            // motor di drive head (spin cepat)
    private Transform[] _flocAgitators;                          // agitator skid flokulan
    private Transform[] _underflowPumpMotors;                    // motor pompa underflow
    private Renderer[] _clearPlsSurfaces = new Renderer[3];      // permukaan PLS jernih (overflow)
    private Renderer[] _feedwellCores = new Renderer[3];         // inti slurry feedwell (keruh)
    private Renderer[] _settlingZones = new Renderer[3];         // zona pengendapan (x-ray)
    private Renderer[] _underflowPools = new Renderer[3];        // lumpur underflow di dasar
    private MaterialPropertyBlock _mpb;
    private readonly Color _turbidSlurry = new Color(0.42f, 0.30f, 0.20f, 0.92f);  // coklat keruh awal
    private readonly Color _clearPls = new Color(0.30f, 0.62f, 0.70f, 0.70f);      // PLS jernih kehijauan

    [Header("=== Process Timing ===")]
    [SerializeField] private float _fadeDuration = 2.5f;
    [SerializeField] private float _fieldObservationDelay = 1.0f;
    [SerializeField] private float _separationDuration = 18f;
    [Tooltip("RPM rake bridge (real thickener ~0.1-0.3 RPM; dipercepat dikit untuk visibilitas).")]
    [SerializeField] private float _rakeRpm = 1.2f;

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
        "Level 9 - CCD: Tekan tombol DCS 9 untuk menjalankan rangkaian Counter-Current Decantation.";
    [TextArea(2, 4)] [SerializeField] private string _msgObserve =
        "CCD aktif. Slurry masuk ke feedwell, rake bridge berputar pelan, padatan mengendap, dan PLS jernih meluap ke launder.";
    [TextArea(2, 4)] [SerializeField] private string _msgComplete =
        "CCD stabil. Pemisahan padat-cair berjalan. Ambil 3 sample PLS overflow, submit Lab QC, lalu lapor HT.";

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
        AutoFindReferences();   // re-resolve real model refs (recover NULL dari scene lama)
        FixBakedLabels();       // ganti teks baked yang ter-cermin dengan overlay readable
        SetProcessVisuals(false);

        if (_hud != null)
            _hud.ShowNotifPublic(_msgStart);

        TeleportPlayer(_teleportTargetDcs);
    }

    private void Update()
    {
        if (!_levelActive)
            return;

        // Overlay label billboard jalan sejak level mulai (sebelum DCS ditekan juga).
        BillboardOverlayLabels();

        if (!_ccdStarted)
            return;

        AnimateRakeArms();
        AnimateRotatingMachinery();

        // Setelah CCD stabil: aktifkan flow sampling PLS + lab QC.
        Update_PLSSampling();
    }

    private void OnDcsButtonPressed(int number)
    {
        if (!_levelActive || number != 9 || _ccdStarted)
            return;

        _ccdStarted = true;
        _sequenceCoroutine = StartCoroutine(RunCCDSequence());
    }

    private IEnumerator RunCCDSequence()
    {
        if (_hud != null)
            _hud.PlayManualFade(_fadeDuration);

        yield return new WaitForSeconds(_fadeDuration * 0.5f);
        TeleportPlayer(ResolveFieldStandSpot());
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

        // Setelah CCD stabil, bangun 3 sample station (overflow PLS) + gedung lab QC.
        BeginPLSSamplingFlow();

        _sequenceCoroutine = null;
    }

    private float SmoothStep(float t)
    {
        t = Mathf.Clamp01(t);
        return t * t * (3f - 2f * t);
    }

    private bool _bakedLabelsFixed;
    // Beberapa label teks pada FBX CCD ter-bake ter-cermin (UV/scale negatif dari Blender).
    // Daripada mengutak-atik mesh, kita sembunyikan renderer teks baked yang menghadap player
    // lalu pasang overlay TextMesh yang terbaca benar + selalu menghadap player (billboard).
    private readonly System.Collections.Generic.List<Transform> _overlayLabels = new System.Collections.Generic.List<Transform>();
    private void FixBakedLabels()
    {
        if (_bakedLabelsFixed) return;
        _bakedLabelsFixed = true;

        // Map: nama mesh baked -> (teks benar, warna). Hanya yang paling kelihatan oleh player.
        var map = new System.Collections.Generic.Dictionary<string, (string text, Color col)>
        {
            { "CCD_ProcessLegend_Wash",      ("WASH WATER \u2192 COUNTER-CURRENT", new Color(0.35f,0.6f,1f)) },
            { "CCD_ProcessLegend_Overflow",  ("OVERFLOW PLS \u2192 PURIFICATION", new Color(0.4f,0.85f,0.7f)) },
            { "CCD_ProcessLegend_Underflow", ("UNDERFLOW \u2192 PUMP STATION", new Color(0.95f,0.6f,0.3f)) },
            { "CCD1_TankLabel", ("CCD-1", new Color(0.9f,0.95f,1f)) },
            { "CCD2_TankLabel", ("CCD-2", new Color(0.9f,0.95f,1f)) },
            { "CCD3_TankLabel", ("CCD-3", new Color(0.9f,0.95f,1f)) },
        };

        foreach (var kv in map)
        {
            Transform baked = FindAnywhere(kv.Key);
            if (baked == null) continue;

            // Sembunyikan teks baked yang ter-cermin.
            var rend = baked.GetComponent<Renderer>();
            if (rend != null) rend.enabled = false;

            // Overlay TextMesh readable di posisi yang sama, sedikit diangkat.
            var go = new GameObject("L9_OverlayLabel_" + kv.Key);
            go.transform.SetParent(transform, false);
            go.transform.position = baked.position + Vector3.up * 0.15f;
            var tm = go.AddComponent<TextMesh>();
            tm.text = kv.Value.text;
            tm.color = kv.Value.col;
            tm.fontSize = 64;
            tm.characterSize = 0.06f;
            tm.anchor = TextAnchor.MiddleCenter;
            tm.alignment = TextAlignment.Center;
            tm.fontStyle = FontStyle.Bold;
            _overlayLabels.Add(go.transform);
        }
    }

    private void BillboardOverlayLabels()
    {
        if (_overlayLabels.Count == 0) return;
        Vector3 head = GetPlayerHead();
        for (int i = 0; i < _overlayLabels.Count; i++)
        {
            if (_overlayLabels[i] == null) continue;
            Vector3 dir = _overlayLabels[i].position - head;
            dir.y = 0f;
            if (dir.sqrMagnitude < 0.001f) continue;
            _overlayLabels[i].rotation = Quaternion.LookRotation(dir.normalized, Vector3.up);
        }
    }

    private Transform FindAnywhere(string name)
    {
        foreach (var t in GameObject.FindObjectsByType<Transform>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            if (t != null && t.name == name && t.gameObject.scene.IsValid())
                return t;
        return null;
    }

    private void AnimateRakeArms()
    {
        if (_rakeArmRoots == null)
            return;
        // Rake bridge berputar mengelilingi sumbu VERTIKAL di pusat tank (bukan pivot lokal mesh,
        // yang bisa offset). RotateAround menjaga rake tetap konsentris walau pivot tidak center.
        float degPerSecond = _rakeRpm * 6f; // RPM -> deg/s
        float step = degPerSecond * Time.deltaTime;
        for (int i = 0; i < _rakeArmRoots.Length; i++)
        {
            if (_rakeArmRoots[i] == null)
                continue;
            Vector3 axisPoint = (i < _rakeTankAxis.Length && _rakeTankAxis[i] != Vector3.zero)
                ? _rakeTankAxis[i]
                : new Vector3(_rakeArmRoots[i].position.x, _rakeArmRoots[i].position.y, _rakeArmRoots[i].position.z);
            _rakeArmRoots[i].RotateAround(axisPoint, Vector3.up, step);
        }
    }

    // Motor drive head, agitator flokulan, dan motor pompa underflow ikut berputar -> pabrik "hidup".
    private void AnimateRotatingMachinery()
    {
        float dt = Time.deltaTime;
        if (_driveMotors != null)
            foreach (var m in _driveMotors)
                if (m != null) m.Rotate(Vector3.up, 220f * dt, Space.Self);
        if (_flocAgitators != null)
            foreach (var a in _flocAgitators)
                if (a != null) a.Rotate(Vector3.up, 360f * dt, Space.Self);
        if (_underflowPumpMotors != null)
            foreach (var p in _underflowPumpMotors)
                if (p != null) p.Rotate(Vector3.forward, 480f * dt, Space.Self);
    }

    // t: 0 (mulai, slurry keruh penuh) -> 1 (stabil, padatan mengendap, PLS jernih).
    // Mensimulasikan pemisahan: feedwell core keruh menyusut, zona pengendapan turun,
    // permukaan PLS makin jernih (warna lerp turbid->clear), lumpur underflow naik di dasar.
    private void UpdateMudLayers(float t)
    {
        if (_mpb == null) _mpb = new MaterialPropertyBlock();
        float settle = SmoothStep(t);
        float clarity = SmoothStep(Mathf.Clamp01((t - 0.15f) / 0.85f));

        // 1) Permukaan PLS jernih: warna keruh -> jernih (selalu tampil, hanya warnanya berubah).
        for (int i = 0; i < 3; i++)
        {
            if (_clearPlsSurfaces[i] != null)
            {
                if (!_clearPlsSurfaces[i].enabled) _clearPlsSurfaces[i].enabled = true;
                Color c = Color.Lerp(_turbidSlurry, _clearPls, clarity);
                ApplyTint(_clearPlsSurfaces[i], c);
            }
            // 2) Feedwell core (slurry keruh masuk): menyusut sedikit saat padatan turun.
            if (_feedwellCores[i] != null)
            {
                float coreScale = Mathf.Lerp(1f, 0.55f, settle);
                var tr = _feedwellCores[i].transform;
                Vector3 baseS = _feedwellBaseScale[i];
                tr.localScale = new Vector3(baseS.x * coreScale, baseS.y, baseS.z * coreScale);
            }
            // 3) Lumpur underflow di dasar: naik tinggi seiring padatan mengendap.
            if (_underflowPools[i] != null)
            {
                var tr = _underflowPools[i].transform;
                Vector3 baseS = _underflowBaseScale[i];
                float h = Mathf.Lerp(0.25f, 1.0f, settle);
                tr.localScale = new Vector3(baseS.x, baseS.y * h, baseS.z);
            }
        }
    }

    private readonly Vector3[] _feedwellBaseScale = new Vector3[3];
    private readonly Vector3[] _underflowBaseScale = new Vector3[3];

    private void ApplyTint(Renderer r, Color c)
    {
        if (r == null) return;
        r.GetPropertyBlock(_mpb);
        _mpb.SetColor("_BaseColor", c);
        _mpb.SetColor("_Color", c);
        r.SetPropertyBlock(_mpb);
    }

    private void SetProcessVisuals(bool active)
    {
        // JANGAN aktifkan stub bar lama (Feed_Inlet_FromFlash_Liquid / Overflow_ToPurification_Liquid):
        // itu peninggalan model CCD lama yang muncul sebagai batang melayang aneh. Biarkan nonaktif.
        if (_feedLiquid != null && _feedLiquid.activeSelf) _feedLiquid.SetActive(false);
        if (_overflowLiquid != null && _overflowLiquid.activeSelf) _overflowLiquid.SetActive(false);

        if (_mpb == null) _mpb = new MaterialPropertyBlock();

        // State awal: PLS surface keruh (coklat slurry) tapi TETAP tampil, core feedwell penuh,
        // lumpur underflow rendah. Saat aktif, sequence menganimasikan keruh->jernih.
        for (int i = 0; i < 3; i++)
        {
            if (_clearPlsSurfaces[i] != null)
            {
                if (!_clearPlsSurfaces[i].enabled) _clearPlsSurfaces[i].enabled = true;
                if (active) ApplyTint(_clearPlsSurfaces[i], _turbidSlurry);
            }
        }
        if (!active)
            UpdateMudLayers(0f);

        if (active)
            BuildOverflowFx();

        if (_separationFx == null)
            return;

        EnsureFxMaterial(_separationFx);
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

    // Pastikan particle system punya material valid (FX model kadang null -> render magenta).
    private void EnsureFxMaterial(ParticleSystem ps)
    {
        if (ps == null) return;
        var r = ps.GetComponent<ParticleSystemRenderer>();
        if (r == null) return;
        if (r.sharedMaterial == null)
        {
            var sh = Shader.Find("Universal Render Pipeline/Particles/Unlit")
                  ?? Shader.Find("Legacy Shaders/Particles/Alpha Blended Premultiply")
                  ?? Shader.Find("Sprites/Default");
            if (sh != null)
            {
                var m = new Material(sh);
                m.color = new Color(0.75f, 0.85f, 0.95f, 0.5f);
                r.sharedMaterial = m;
            }
        }
    }

    // Bangun trickle FX kecil di tiap overflow launder (CCD overflow PLS jernih meluap).
    private ParticleSystem[] _overflowFx;
    private void BuildOverflowFx()
    {
        if (_overflowFx != null) return;
        // Titik overflow tiap thickener (header overflow ke arah purification / wash).
        Vector3[] pts = {
            new Vector3(19.0f, 6.7f, 107.7f),  // CCD1 overflow header
            new Vector3(8.2f, 6.5f, 108.6f),   // CCD2 wash overflow
            new Vector3(-4.9f, 6.5f, 108.6f)   // CCD3 wash overflow
        };
        _overflowFx = new ParticleSystem[pts.Length];
        for (int i = 0; i < pts.Length; i++)
        {
            var go = new GameObject($"L9_OverflowTrickle_{i}");
            go.transform.SetParent(transform, false);
            go.transform.position = pts[i];
            var ps = go.AddComponent<ParticleSystem>();
            var main = ps.main;
            main.startLifetime = 1.1f;
            main.startSpeed = 1.4f;
            main.startSize = 0.10f;
            main.gravityModifier = 1.2f;
            main.startColor = new Color(0.55f, 0.78f, 0.85f, 0.7f);
            main.maxParticles = 120;
            var em = ps.emission; em.rateOverTime = 28f;
            var sh = ps.shape; sh.shapeType = ParticleSystemShapeType.Cone; sh.angle = 8f; sh.radius = 0.12f;
            ps.transform.rotation = Quaternion.Euler(90f, 0, 0); // arahkan ke bawah
            EnsureFxMaterial(ps);
            _overflowFx[i] = ps;
            ps.Play();
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

    /// <summary>
    /// Hitung titik berdiri yang nyaman untuk MENGAMATI train CCD: di depan deretan tank,
    /// jaraknya proporsional dengan tinggi tank supaya seluruh train kelihatan (bukan
    /// nempel ke dinding tank seperti bug sebelumnya). Kalau bounds gagal dihitung, pakai
    /// _teleportTargetField yang di-assign manual.
    /// </summary>
    private Transform ResolveFieldStandSpot()
    {
        if (_ccdField == null)
            AutoFindReferences();
        if (_ccdField == null)
            return _teleportTargetField;

        var renderers = _ccdField.GetComponentsInChildren<Renderer>();
        if (renderers == null || renderers.Length == 0)
            return _teleportTargetField;

        Bounds b = renderers[0].bounds;
        for (int i = 1; i < renderers.Length; i++)
            b.Encapsulate(renderers[i].bounds);

        // Berdiri di sisi -Z train (arah datang player), jarak = setengah kedalaman + offset
        // skala tinggi tank supaya seluruh train muat di FOV.
        float standBack = b.extents.z + Mathf.Max(8f, b.size.y * 0.9f);
        Vector3 pos = new Vector3(b.center.x, 0.1f, b.min.z - standBack);

        var existing = GameObject.Find("SpawnPoint_Lvl10_Observe_Runtime");
        var sp = existing != null ? existing : new GameObject("SpawnPoint_Lvl10_Observe_Runtime");
        sp.transform.position = pos;
        Vector3 look = new Vector3(b.center.x - pos.x, 0f, b.center.z - pos.z);
        sp.transform.rotation = look.sqrMagnitude > 0.001f
            ? Quaternion.LookRotation(look.normalized, Vector3.up)
            : Quaternion.identity;
        return sp.transform;
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

        if (_separationFx == null)
        {
            Transform fx = FindDeepChild(root, "CCD_Separation_FX");
            if (fx != null)
                _separationFx = fx.GetComponent<ParticleSystem>();
        }

        ResolveIndustrialModelRefs(rigRoot);
    }

    // Resolusi objek model industrial baru (CCDIndustrialUVRedesign). Dipanggil ulang setiap
    // level start supaya referensi NULL (dari scene lama) ter-recover otomatis.
    private void ResolveIndustrialModelRefs(Transform rigRoot)
    {
        if (rigRoot == null) return;

        // --- Rake bridges: 3 thickener. Cari root + hitung sumbu vertikal (pakai bounds XZ). ---
        var rakeList = new System.Collections.Generic.List<Transform>();
        FindDeepChildren(rigRoot, "Rake_Arm_Root", rakeList);
        bool rakesValid = _rakeArmRoots != null && _rakeArmRoots.Length > 0;
        if (rakesValid)
            foreach (var r in _rakeArmRoots) if (r == null) { rakesValid = false; break; }
        if (!rakesValid && rakeList.Count > 0)
            _rakeArmRoots = rakeList.ToArray();

        if (_rakeArmRoots != null)
        {
            for (int i = 0; i < _rakeArmRoots.Length && i < _rakeTankAxis.Length; i++)
            {
                if (_rakeArmRoots[i] == null) continue;
                var rends = _rakeArmRoots[i].GetComponentsInChildren<Renderer>();
                if (rends.Length > 0)
                {
                    Bounds b = rends[0].bounds;
                    for (int k = 1; k < rends.Length; k++) b.Encapsulate(rends[k].bounds);
                    _rakeTankAxis[i] = new Vector3(b.center.x, _rakeArmRoots[i].position.y, b.center.z);
                }
                else _rakeTankAxis[i] = _rakeArmRoots[i].position;
            }
        }

        // --- Per-tank visual surfaces (CCD1/CCD2/CCD3) ---
        for (int i = 0; i < 3; i++)
        {
            string p = "CCD" + (i + 1);
            _clearPlsSurfaces[i] = GetRenderer(rigRoot, p + "_ClearPLS_Surface");
            _feedwellCores[i] = GetRenderer(rigRoot, p + "_Feedwell_SlurryCore");
            _settlingZones[i] = GetRenderer(rigRoot, p + "_SettlingZone_XRayColumn");
            _underflowPools[i] = GetRenderer(rigRoot, p + "_ThickUnderflow_BottomPool");
            if (_feedwellCores[i] != null) _feedwellBaseScale[i] = _feedwellCores[i].transform.localScale;
            if (_underflowPools[i] != null) _underflowBaseScale[i] = _underflowPools[i].transform.localScale;
        }

        // --- Rotating machinery (motors, agitator, pumps) ---
        var motors = new System.Collections.Generic.List<Transform>();
        for (int i = 1; i <= 3; i++) { var m = FindDeepChild(rigRoot, "CCD" + i + "_DriveMotor"); if (m != null) motors.Add(m); }
        _driveMotors = motors.ToArray();

        var aggs = new System.Collections.Generic.List<Transform>();
        var fa = FindDeepChild(rigRoot, "FlocculantSkid_AgitatorMotor"); if (fa != null) aggs.Add(fa);
        var dp = FindDeepChild(rigRoot, "FlocculantSkid_DosingPump"); if (dp != null) aggs.Add(dp);
        _flocAgitators = aggs.ToArray();

        var pumps = new System.Collections.Generic.List<Transform>();
        var p1 = FindDeepChild(rigRoot, "UnderflowPump_1_Motor"); if (p1 != null) pumps.Add(p1);
        var p2 = FindDeepChild(rigRoot, "UnderflowPump_2_Motor"); if (p2 != null) pumps.Add(p2);
        _underflowPumpMotors = pumps.ToArray();
    }

    private Renderer GetRenderer(Transform rigRoot, string name)
    {
        var t = FindDeepChild(rigRoot, name);
        return t != null ? t.GetComponent<Renderer>() : null;
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

    // ============================================================
    //  PLS SAMPLING + LAB QC (Opsi A: dipindah dari Level 8 ke sini)
    //  Real-world HPAL: sample PLS untuk lab QC diambil dari OVERFLOW CCD
    //  setelah solid-cair dipisah; bukan dari flash vessel discharge.
    // ============================================================

    [Header("=== Sample Station + Lab QC ===")]
    [SerializeField] private GameObject _qcLabFbxOverride; // optional: assign FBX manually

    private GameObject[] _ccdSampleStations = new GameObject[3];
    private Transform[] _ccdStationFillLiquid = new Transform[3];
    private float[] _ccdBottleFillProgress = new float[3];
    private bool[] _ccdBottleFilling = new bool[3];
    private bool[] _ccdSampleTaken = new bool[3];
    private bool _ccdStationsBuilt;
    private float _ccdSampleProximityRadius = 2.8f;

    private GameObject _ccdLabBuilding;
    private Transform[] _ccdLabSlotLiquids = new Transform[3];
    private readonly float[] _ccdLabSlotBaseY = new float[3] { 1.7f, 1.7f, 1.7f };
    private Transform _ccdLabAnalyzerRotor;
    private Transform _ccdLabResultScreen;
    private TextMesh _ccdLabScreenText;
    private GameObject _ccdLabQcCanvas;
    private bool _ccdLabBuilt;
    private bool _ccdLabSubmitted;

    // Warna PLS per sample point. PLS HPAL real = larutan sulfat hijau-kekuningan (Ni/Co),
    // makin ke wash overflow makin encer/bening.
    private static readonly Color[] _ccdSampleColors = {
        new Color(0.42f, 0.62f, 0.30f),   // Th-1: PLS pekat (Ni/Co tinggi, hijau zaitun)
        new Color(0.55f, 0.70f, 0.45f),   // Th-3: PLS lebih encer
        new Color(0.60f, 0.72f, 0.68f)    // Th-5: wash overflow (Ni rendah, hampir bening)
    };

    private void BeginPLSSamplingFlow()
    {
        if (_hud != null)
            _hud.ShowNotifPublic("CCD stabil. Ambil 3 sample PLS dari overflow tiap thickener (dekati pedestal botol). Lalu masuk LAB QC.", 10f);
        BuildCCDSampleStations();
        BuildCCDLabBuilding();
    }

    private void Update_PLSSampling()
    {
        if (!_ccdStartedFlag()) return;
        UpdateCCDProximity();
        UpdateCCDBottleFill();
        BillboardStationLabels();
        // Manual submit: tekan L kalau semua sample terambil.
        if (Input.GetKeyDown(KeyCode.L) && AllPLSSamplesTaken() && !_ccdLabSubmitted)
            SubmitPLSToLab();
        // Fallback keyboard untuk tombol ACCEPT canvas hasil lab (Enter), selain klik ray XR.
        if (_pendingAcceptAction != null && _ccdLabQcCanvas != null && _ccdLabQcCanvas.activeInHierarchy
            && (Input.GetKeyDown(KeyCode.Return) || Input.GetKeyDown(KeyCode.KeypadEnter)))
        {
            var act = _pendingAcceptAction;
            _pendingAcceptAction = null;
            act.Invoke();
        }
    }

    // Label sample station selalu menghadap player (billboard) supaya teks tidak terbalik/miring.
    private void BillboardStationLabels()
    {
        Vector3 head = GetPlayerHead();
        for (int i = 0; i < 3; i++)
        {
            if (_ccdStationLabels[i] == null) continue;
            Vector3 dir = _ccdStationLabels[i].position - head;
            dir.y = 0f;
            if (dir.sqrMagnitude < 0.001f) continue;
            _ccdStationLabels[i].rotation = Quaternion.LookRotation(dir.normalized, Vector3.up);
        }
    }

    private bool _ccdStartedFlag() => _questComplete; // shorthand: hanya aktif setelah CCD stable

    private void BuildCCDSampleStations()
    {
        if (_ccdStationsBuilt) return;
        _ccdStationsBuilt = true;

        // Tempatkan 3 pedestal sample TEPAT DI DEPAN tiap thickener (sisi -Z, arah player),
        // di atas tanah/walkway supaya bisa didekati. Pakai sumbu X tiap tank (CCD1/2/3),
        // bukan di tengah tank (yang bikin botol nyangkut di dalam tank — bug lama).
        float[] tankX = { 15.0f, 1.6f, -11.7f };  // CCD1, CCD2, CCD3 center X
        float frontZ = 105.0f;                      // sedikit di depan dinding tank (z~108)
        float groundY = 0.0f;

        if (_ccdField != null)
        {
            var rends = _ccdField.GetComponentsInChildren<Renderer>();
            if (rends.Length > 0)
            {
                var b = rends[0].bounds; foreach (var r in rends) b.Encapsulate(r.bounds);
                frontZ = b.min.z - 1.5f;   // 1.5m di depan muka tank
                groundY = Mathf.Max(0f, b.min.y + 0.05f);
            }
        }

        for (int i = 0; i < 3; i++)
        {
            Vector3 pos = new Vector3(tankX[i], groundY, frontZ);
            _ccdSampleStations[i] = BuildCCDStationVisual(i, pos);
        }
    }

    private GameObject BuildCCDStationVisual(int idx, Vector3 worldPos)
    {
        int thNo = idx == 0 ? 1 : idx == 1 ? 3 : 5;
        var root = new GameObject($"L9_PLS_SampleStation_Th{thNo}");
        root.transform.SetParent(transform, false);
        root.transform.position = worldPos;
        // Hadapkan station ke arah player (sisi -Z) supaya spout & label menghadap pemain.
        root.transform.rotation = Quaternion.LookRotation(Vector3.back, Vector3.up);

        // --- Base cabinet (steel) ---
        var cabinet = GameObject.CreatePrimitive(PrimitiveType.Cube);
        cabinet.name = "SampleCabinet";
        cabinet.transform.SetParent(root.transform, false);
        cabinet.transform.localPosition = new Vector3(0, 0.55f, 0);
        cabinet.transform.localScale = new Vector3(0.55f, 1.1f, 0.45f);
        var cc0 = cabinet.GetComponent<Collider>(); if (cc0 != null) Destroy(cc0);
        ApplySimpleMat(cabinet.GetComponent<Renderer>(), new Color(0.30f, 0.33f, 0.38f));

        // --- Hazard stripe band on cabinet front ---
        var band = GameObject.CreatePrimitive(PrimitiveType.Cube);
        band.name = "HazardBand";
        band.transform.SetParent(root.transform, false);
        band.transform.localPosition = new Vector3(0, 0.95f, -0.24f);
        band.transform.localScale = new Vector3(0.56f, 0.12f, 0.02f);
        var bc0 = band.GetComponent<Collider>(); if (bc0 != null) Destroy(bc0);
        ApplySimpleMat(band.GetComponent<Renderer>(), new Color(0.95f, 0.62f, 0.05f));

        // --- Sloped sampling spout (where PLS drips into bottle) ---
        var spout = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        spout.name = "SampleSpout";
        spout.transform.SetParent(root.transform, false);
        spout.transform.localPosition = new Vector3(0, 1.5f, -0.18f);
        spout.transform.localRotation = Quaternion.Euler(60f, 0, 0);
        spout.transform.localScale = new Vector3(0.06f, 0.18f, 0.06f);
        var sc0 = spout.GetComponent<Collider>(); if (sc0 != null) Destroy(sc0);
        ApplySimpleMat(spout.GetComponent<Renderer>(), new Color(0.55f, 0.57f, 0.62f));

        // --- Small sampling valve handwheel on cabinet ---
        var valve = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        valve.name = "SampleValve";
        valve.transform.SetParent(root.transform, false);
        valve.transform.localPosition = new Vector3(0.18f, 1.25f, -0.22f);
        valve.transform.localRotation = Quaternion.Euler(90f, 0, 0);
        valve.transform.localScale = new Vector3(0.16f, 0.03f, 0.16f);
        var vc0 = valve.GetComponent<Collider>(); if (vc0 != null) Destroy(vc0);
        ApplySimpleMat(valve.GetComponent<Renderer>(), new Color(0.7f, 0.15f, 0.12f));

        // --- Glass sample bottle on cabinet top ---
        var bottle = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        bottle.name = "Bottle";
        bottle.transform.SetParent(root.transform, false);
        bottle.transform.localPosition = new Vector3(0, 1.30f, 0);
        bottle.transform.localScale = new Vector3(0.16f, 0.22f, 0.16f);
        var bc = bottle.GetComponent<Collider>(); if (bc != null) Destroy(bc);
        var glassMat = new Material(Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard"));
        ApplyTransparent(glassMat, new Color(0.8f, 0.85f, 0.9f, 0.25f));
        bottle.GetComponent<Renderer>().sharedMaterial = glassMat;

        var liquid = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        liquid.name = "Liquid";
        liquid.transform.SetParent(bottle.transform, false);
        liquid.transform.localScale = new Vector3(0.82f, 0.001f, 0.82f);
        liquid.transform.localPosition = new Vector3(0, -0.95f, 0);
        var lc = liquid.GetComponent<Collider>(); if (lc != null) Destroy(lc);
        var lm = new Material(Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard"));
        lm.color = _ccdSampleColors[idx];
        lm.EnableKeyword("_EMISSION");
        if (lm.HasProperty("_EmissionColor")) lm.SetColor("_EmissionColor", _ccdSampleColors[idx] * 1.2f);
        liquid.GetComponent<Renderer>().sharedMaterial = lm;
        _ccdStationFillLiquid[idx] = liquid.transform;

        // --- Floating label (billboarded each frame toward player) ---
        var labelGO = new GameObject("Label");
        labelGO.transform.SetParent(root.transform, false);
        labelGO.transform.localPosition = new Vector3(0, 2.05f, 0);
        var tm = labelGO.AddComponent<TextMesh>();
        tm.text = $"PLS Th-{thNo}\n[ ambil sample ]";
        tm.fontSize = 48; tm.characterSize = 0.022f; tm.anchor = TextAnchor.MiddleCenter;
        tm.alignment = TextAlignment.Center; tm.color = new Color(0.4f, 1f, 0.7f);
        _ccdStationLabels[idx] = labelGO.transform;
        return root;
    }

    private readonly Transform[] _ccdStationLabels = new Transform[3];

    private void UpdateCCDProximity()
    {
        if (!_ccdStationsBuilt) return;
        Vector3 head = GetPlayerHead();
        for (int i = 0; i < 3; i++)
        {
            if (_ccdSampleTaken[i] || _ccdBottleFilling[i]) continue;
            if (_ccdSampleStations[i] == null) continue;
            Vector3 a = head; a.y = 0f;
            Vector3 b = _ccdSampleStations[i].transform.position; b.y = 0f;
            if (Vector3.Distance(a, b) <= _ccdSampleProximityRadius)
            {
                _ccdBottleFilling[i] = true;
                if (_hud != null) _hud.ShowNotifPublic($"Mengambil sample PLS Thickener {(i == 0 ? 1 : i == 1 ? 3 : 5)}...", 3f);
            }
        }
    }

    private void UpdateCCDBottleFill()
    {
        for (int i = 0; i < 3; i++)
        {
            if (!_ccdBottleFilling[i] || _ccdSampleTaken[i]) continue;
            _ccdBottleFillProgress[i] += Time.deltaTime / 2f;
            float t = Mathf.Clamp01(_ccdBottleFillProgress[i]);
            if (_ccdStationFillLiquid[i] != null)
            {
                float h = Mathf.Lerp(0.001f, 1.7f, t);
                _ccdStationFillLiquid[i].localScale = new Vector3(0.82f, h, 0.82f);
                _ccdStationFillLiquid[i].localPosition = new Vector3(0, -0.95f + h * 0.5f, 0);
            }
            if (t >= 1f)
            {
                _ccdSampleTaken[i] = true;
                _ccdBottleFilling[i] = false;
                if (_ccdStationLabels[i] != null)
                {
                    var tm = _ccdStationLabels[i].GetComponent<TextMesh>();
                    if (tm != null) { tm.text = $"PLS Th-{(i == 0 ? 1 : i == 1 ? 3 : 5)}\n✓ TERAMBIL"; tm.color = new Color(0.5f, 1f, 0.5f); }
                }
                if (_hud != null)
                    _hud.ShowNotifPublic($"Sample PLS Th-{(i == 0 ? 1 : i == 1 ? 3 : 5)} terkumpul ({CountPLSSamples()}/3).", 3f);
                if (AllPLSSamplesTaken() && _hud != null)
                    _hud.ShowNotifPublic("3 sample PLS terkumpul. Masuk LAB QC, tekan [L] untuk submit analisa.", 8f);
            }
        }
    }

    private bool AllPLSSamplesTaken() { foreach (var s in _ccdSampleTaken) if (!s) return false; return true; }
    private int CountPLSSamples() { int c = 0; foreach (var s in _ccdSampleTaken) if (s) c++; return c; }

    private void BuildCCDLabBuilding()
    {
        if (_ccdLabBuilt) return;
        _ccdLabBuilt = true;

        // Posisi lab: DI DEPAN train CCD (sisi player, -Z), agak ke kiri supaya tidak menutupi
        // pedestal sample. Reachable jalan kaki dari titik observasi. Menghadap ke tank (+Z).
        Vector3 labOrigin = transform.position + new Vector3(-10f, 0f, -8f);
        if (_ccdField != null)
        {
            var rends = _ccdField.GetComponentsInChildren<Renderer>();
            if (rends.Length > 0)
            {
                var b = rends[0].bounds; foreach (var r in rends) b.Encapsulate(r.bounds);
                // Sudut depan-kiri train: x = tepi kiri tank (CCD3 ~ -11.7) geser kiri lagi,
                // z = di depan muka tank supaya sejajar pedestal sample.
                float frontZ = b.min.z - 4.0f;
                labOrigin = new Vector3(-20f, 0f, frontZ);
            }
        }

        GameObject fbxPrefab = _qcLabFbxOverride;
#if UNITY_EDITOR
        // Prioritaskan CCDLab.fbx (lab khusus Level 9, lebih detail) baru fallback ke QCLab.fbx
        if (fbxPrefab == null)
            fbxPrefab = UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Art/Lab/CCDLab.fbx");
        if (fbxPrefab == null)
            fbxPrefab = UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Art/Lab/QCLab.fbx");
#endif
        if (fbxPrefab == null) fbxPrefab = Resources.Load<GameObject>("CCDLab");
        if (fbxPrefab == null) fbxPrefab = Resources.Load<GameObject>("QCLab");

        if (fbxPrefab != null)
        {
            var inst = Instantiate(fbxPrefab);
            inst.name = "L9_LabBuilding";
            inst.transform.SetParent(transform, false);
            inst.transform.position = labOrigin;
            inst.transform.rotation = Quaternion.identity;
            _ccdLabBuilding = inst;

            // Coba nama child versi CCDLab.fbx (baru) DULU; fallback ke nama lama (QCLab.fbx).
            _ccdLabAnalyzerRotor = FindDeepChild(inst.transform, "CCDLab_Spectrometer_Rotor")
                                ?? FindDeepChild(inst.transform, "Lab_Analyzer_Rotor");
            _ccdLabResultScreen = FindDeepChild(inst.transform, "CCDLab_ResultScreen")
                                ?? FindDeepChild(inst.transform, "Lab_ResultScreen");
            _ccdLabSlotLiquids[0] = FindDeepChild(inst.transform, "CCDLab_InletLiquid_1")
                                  ?? FindDeepChild(inst.transform, "Lab_SlotLiquid_1");
            _ccdLabSlotLiquids[1] = FindDeepChild(inst.transform, "CCDLab_InletLiquid_2")
                                  ?? FindDeepChild(inst.transform, "Lab_SlotLiquid_2");
            _ccdLabSlotLiquids[2] = FindDeepChild(inst.transform, "CCDLab_InletLiquid_3")
                                  ?? FindDeepChild(inst.transform, "Lab_SlotLiquid_3");
            for (int i = 0; i < 3; i++)
            {
                if (_ccdLabSlotLiquids[i] != null)
                {
                    var s = _ccdLabSlotLiquids[i].localScale;
                    _ccdLabSlotBaseY[i] = s.y;
                    _ccdLabSlotLiquids[i].localScale = new Vector3(s.x, s.y * 0.02f, s.z);
                }
            }
            if (_ccdLabResultScreen != null)
            {
                var st = new GameObject("ScreenText");
                st.transform.SetParent(_ccdLabResultScreen, false);
                st.transform.localPosition = new Vector3(0, 0, 0.7f);
                st.transform.localScale = Vector3.one * 0.6f;
                var tm = st.AddComponent<TextMesh>();
                tm.text = "QC LAB\nStandby...";
                tm.fontSize = 40; tm.characterSize = 0.05f; tm.anchor = TextAnchor.MiddleCenter;
                tm.alignment = TextAlignment.Center; tm.color = new Color(0.4f, 0.9f, 0.7f);
                _ccdLabScreenText = tm;
            }
            var sign = new GameObject("Lab_Sign");
            sign.transform.SetParent(inst.transform, false);
            sign.transform.localPosition = new Vector3(0, 3.9f, 3.5f);
            var stm = sign.AddComponent<TextMesh>();
            stm.text = "LAB QC PLS";
            stm.fontSize = 60; stm.characterSize = 0.04f; stm.anchor = TextAnchor.MiddleCenter;
            stm.alignment = TextAlignment.Center; stm.color = new Color(0.2f, 0.9f, 1f);
            Debug.Log("[Level9 CCD] Lab QC dari Blender FBX ter-load.");
        }
        else
        {
            Debug.LogWarning("[Level9 CCD] QCLab.fbx tidak ditemukan. Lab QC akan pakai canvas pop-up saja.");
        }
    }

    private void SubmitPLSToLab()
    {
        if (_ccdLabSubmitted) return;
        _ccdLabSubmitted = true;
        StartCoroutine(LabAnalysisCoroutineL10());
    }

    private IEnumerator LabAnalysisCoroutineL10()
    {
        if (_hud != null) _hud.ShowNotifPublic("Sample PLS dimasukkan ke analyzer. Analisa berjalan...", 6f);

        for (int i = 0; i < 3; i++)
        {
            if (_ccdLabSlotLiquids[i] == null) continue;
            var baseScale = _ccdLabSlotLiquids[i].localScale;
            var basePos = _ccdLabSlotLiquids[i].localPosition;
            float fullY = _ccdLabSlotBaseY[i];
            float t = 0f;
            while (t < 0.6f)
            {
                t += Time.deltaTime;
                float p = Mathf.Clamp01(t / 0.6f);
                float h = Mathf.Lerp(fullY * 0.02f, fullY, p);
                _ccdLabSlotLiquids[i].localScale = new Vector3(baseScale.x, h, baseScale.z);
                _ccdLabSlotLiquids[i].localPosition = basePos + new Vector3(0, (h - fullY * 0.02f) * 0.5f, 0);
                yield return null;
            }
        }

        float dur = 5f, e = 0f;
        while (e < dur)
        {
            e += Time.deltaTime;
            if (_ccdLabAnalyzerRotor != null)
                _ccdLabAnalyzerRotor.Rotate(Vector3.up, 360f * Time.deltaTime, Space.Self);
            if (_ccdLabScreenText != null)
            {
                int pct = Mathf.RoundToInt(Mathf.Clamp01(e / dur) * 100f);
                int bars = Mathf.RoundToInt(pct / 10f);
                _ccdLabScreenText.text = "ANALISA QC PLS\n[" + new string('#', bars) + new string('-', 10 - bars) + "] " + pct + "%";
            }
            yield return null;
        }
        if (_ccdLabScreenText != null) _ccdLabScreenText.text = "QC SELESAI\nPLS dalam SOP ✓\nNi 5.2  Co 0.45\nFree acid 18.0 g/L";

        ShowL10LabResultCanvas();
        if (_hud != null) _hud.ShowNotifPublic("Hasil QC keluar: PLS dalam SOP. Klik tombol ACCEPT (atau tekan Enter) untuk lanjut.", 8f);
    }

    private void ShowL10LabResultCanvas()
    {
        if (_ccdLabQcCanvas != null) { _ccdLabQcCanvas.SetActive(true); return; }
        var canvasGO = new GameObject("L9_LabQC_Canvas");
        canvasGO.transform.SetParent(transform, false);
        var canvas = canvasGO.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.WorldSpace;
        canvasGO.AddComponent<UnityEngine.UI.CanvasScaler>();
        canvasGO.AddComponent<UnityEngine.UI.GraphicRaycaster>();
        Vector3 head = GetPlayerHead();
        Vector3 fwd = GetPlayerForward(); fwd.y = 0f;
        if (fwd.sqrMagnitude < 0.001f) fwd = Vector3.forward;
        fwd.Normalize();
        // Posisikan setinggi mata, ~2.2m di depan, supaya tidak nabrak meja lab & terbaca penuh.
        canvasGO.transform.position = new Vector3(head.x, head.y + 0.1f, head.z) + fwd * 2.2f;
        canvasGO.transform.rotation = Quaternion.LookRotation(fwd, Vector3.up);
        var rect = canvasGO.GetComponent<RectTransform>();
        rect.sizeDelta = new Vector2(2.0f, 1.4f);
        canvasGO.transform.localScale = Vector3.one * 0.85f;

        AddPanel(canvasGO.transform, "BG", new Color(0.04f, 0.08f, 0.13f, 0.98f), Vector2.zero, Vector2.one);
        AddPanel(canvasGO.transform, "TitleBar", new Color(0.10f, 0.30f, 0.45f, 1f), new Vector2(0f, 0.85f), new Vector2(1f, 1f));
        AddText(canvasGO.transform, "Title", "LABORATORY QC — PLS OVERFLOW CCD",
            new Color(0.7f, 1f, 0.85f), 30, FontStyle.Bold, TextAnchor.MiddleCenter,
            new Vector2(0, 0.85f), new Vector2(1, 1f));
        string[] data = {
            "Th-1 PLS:  Free acid 18.0 g/L | Ni 5.2 g/L | Co 0.45 | Fe 0.8  ✓",
            "Th-3 PLS:  Free acid 16.5 g/L | Ni 4.6 g/L | Co 0.41 | Fe 0.6  ✓",
            "Th-5 PLS:  Free acid 6.2 g/L  | Ni 1.1 g/L | Co 0.10 | Fe 0.2  ✓"
        };
        for (int i = 0; i < 3; i++)
        {
            float yMin = 0.55f - i * 0.13f;
            AddText(canvasGO.transform, $"S{i}", data[i], Color.white, 17, FontStyle.Normal,
                TextAnchor.MiddleLeft, new Vector2(0.05f, yMin), new Vector2(0.95f, yMin + 0.12f));
        }
        AddText(canvasGO.transform, "Verdict",
            "VERDICT: PLS dalam SOP. Wash efficiency CCD ≈ 95%. Siap ke neutralisasi.",
            new Color(0.6f, 1f, 0.7f), 18, FontStyle.Italic, TextAnchor.MiddleCenter,
            new Vector2(0.05f, 0.16f), new Vector2(0.95f, 0.32f));
        AddButton(canvasGO.transform, "ACCEPT & LANJUT",
            new Vector2(0.3f, 0.04f), new Vector2(0.7f, 0.14f),
            new Color(0.2f, 0.6f, 0.3f), () => OnL10LabAccepted());
        _ccdLabQcCanvas = canvasGO;
    }

    private void OnL10LabAccepted()
    {
        if (_ccdLabQcCanvas != null) _ccdLabQcCanvas.SetActive(false);
        GameLevelManager.Instance?.NotifyLevel10SamplePLSAccepted();
        if (_hud != null) _hud.ShowNotifPublic("Lab QC PLS lulus. Lapor HT (tahan T): 'CCD aktif, PLS lulus QC'.", 8f);
    }

    private Vector3 GetPlayerHead()
    {
        if (_playerRigRoot == null) return Vector3.zero;
        var origin = _playerRigRoot.GetComponent<XROrigin>();
        if (origin != null && origin.Camera != null) return origin.Camera.transform.position;
        var cam = _playerRigRoot.GetComponentInChildren<Camera>();
        return cam != null ? cam.transform.position : _playerRigRoot.position + Vector3.up * 1.6f;
    }
    private Vector3 GetPlayerForward()
    {
        if (_playerRigRoot == null) return Vector3.forward;
        var origin = _playerRigRoot.GetComponent<XROrigin>();
        if (origin != null && origin.Camera != null) return origin.Camera.transform.forward;
        var cam = _playerRigRoot.GetComponentInChildren<Camera>();
        return cam != null ? cam.transform.forward : _playerRigRoot.forward;
    }

    private void ApplySimpleMat(Renderer r, Color c)
    {
        if (r == null) return;
        var m = new Material(Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard"));
        if (m.HasProperty("_BaseColor")) m.SetColor("_BaseColor", c);
        if (m.HasProperty("_Color")) m.SetColor("_Color", c);
        r.sharedMaterial = m;
    }

    private void ApplyTransparent(Material m, Color c)
    {
        m.color = c;
        if (m.HasProperty("_BaseColor")) m.SetColor("_BaseColor", c);
        m.SetFloat("_Mode", 3f);
        m.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.SrcAlpha);
        m.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
        m.SetInt("_ZWrite", 0);
        m.DisableKeyword("_SURFACE_TYPE_OPAQUE");
        m.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
        m.renderQueue = 3000;
    }

    private void AddPanel(Transform parent, string name, Color c, Vector2 amin, Vector2 amax)
    {
        var go = new GameObject(name); go.transform.SetParent(parent, false);
        var img = go.AddComponent<UnityEngine.UI.Image>(); img.color = c;
        var rt = go.GetComponent<RectTransform>();
        rt.anchorMin = amin; rt.anchorMax = amax; rt.offsetMin = Vector2.zero; rt.offsetMax = Vector2.zero;
    }
    private void AddText(Transform parent, string name, string text, Color c, int fontSize, FontStyle style,
                         TextAnchor anchor, Vector2 amin, Vector2 amax)
    {
        var go = new GameObject(name); go.transform.SetParent(parent, false);
        var t = go.AddComponent<UnityEngine.UI.Text>();
        t.text = text; t.color = c; t.fontSize = fontSize; t.fontStyle = style; t.alignment = anchor;
        t.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        t.horizontalOverflow = HorizontalWrapMode.Wrap;
        var rt = go.GetComponent<RectTransform>();
        rt.anchorMin = amin; rt.anchorMax = amax; rt.offsetMin = Vector2.zero; rt.offsetMax = Vector2.zero;
    }
    private void AddButton(Transform parent, string label, Vector2 amin, Vector2 amax, Color c, System.Action onClick)
    {
        var go = new GameObject("Btn_" + label);
        go.transform.SetParent(parent, false);
        var img = go.AddComponent<UnityEngine.UI.Image>(); img.color = c;
        var rt = go.GetComponent<RectTransform>();
        rt.anchorMin = amin; rt.anchorMax = amax; rt.offsetMin = Vector2.zero; rt.offsetMax = Vector2.zero;
        var btn = go.AddComponent<UnityEngine.UI.Button>();
        btn.targetGraphic = img;
        btn.onClick.AddListener(() => onClick?.Invoke());
        var txtGo = new GameObject("Text"); txtGo.transform.SetParent(go.transform, false);
        var txt = txtGo.AddComponent<UnityEngine.UI.Text>();
        txt.text = label; txt.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        txt.fontSize = 20; txt.color = Color.white; txt.alignment = TextAnchor.MiddleCenter; txt.fontStyle = FontStyle.Bold;
        var trt = txtGo.GetComponent<RectTransform>();
        trt.anchorMin = Vector2.zero; trt.anchorMax = Vector2.one; trt.offsetMin = Vector2.zero; trt.offsetMax = Vector2.zero;

        // PENTING (VR): UnityEngine.UI.Button + GraphicRaycaster saja TIDAK bisa diklik ray XR.
        // Tambahkan XRSimpleInteractable + BoxCollider supaya XR ray/poke bisa men-trigger.
        // Pakai keyboard [Enter] juga sebagai fallback (handled di Update_PLSSampling).
        StartCoroutine(AttachXrButtonNextFrame(go, rt, onClick));
        _pendingAcceptAction = onClick; // fallback keyboard
    }

    private System.Action _pendingAcceptAction;

    // Tunggu 1 frame supaya layout RectTransform sudah final, baru pasang collider seukuran tombol.
    private IEnumerator AttachXrButtonNextFrame(GameObject go, RectTransform rt, System.Action onClick)
    {
        yield return null;
        if (go == null) yield break;
        var rect = rt.rect;
        float w = Mathf.Max(0.2f, Mathf.Abs(rect.width));
        float h = Mathf.Max(0.2f, Mathf.Abs(rect.height));
        var bc = go.GetComponent<BoxCollider>();
        if (bc == null) bc = go.AddComponent<BoxCollider>();
        bc.isTrigger = true;
        bc.size = new Vector3(w, h, 6f);
        bc.center = Vector3.zero;
        var simple = go.GetComponent<UnityEngine.XR.Interaction.Toolkit.Interactables.XRSimpleInteractable>();
        if (simple == null) simple = go.AddComponent<UnityEngine.XR.Interaction.Toolkit.Interactables.XRSimpleInteractable>();
        simple.selectEntered.AddListener(_ => onClick?.Invoke());
    }
}
