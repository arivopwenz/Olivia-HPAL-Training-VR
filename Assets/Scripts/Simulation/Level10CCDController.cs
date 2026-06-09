using System.Collections;
using System.Collections.Generic;
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

    // Cairan tabung TERANG (satu volume) yang permukaannya NAIK DARI DASAR via shader _FillY.
    // Menggantikan layer disc/pool lama (yang melebar dari tengah & double-liquid).
    private TankFluidColumn[] _ccdFluid = new TankFluidColumn[3];
    private bool _ccdRotorOn;                                     // rotor/rake swirl aktif (sesudah cairan naik)
    // Warna PLS CCD: keruh ungu-coklat (awal) -> ungu jernih kebiruan (sesudah pemisahan).
    private readonly Color _ccdTurbidShallow = new Color(0.46f, 0.32f, 0.52f, 1f);
    private readonly Color _ccdTurbidDeep    = new Color(0.30f, 0.18f, 0.40f, 1f);
    private readonly Color _ccdTurbidEmis    = new Color(0.20f, 0.10f, 0.28f, 1f);
    private readonly Color _ccdClearShallow  = new Color(0.46f, 0.42f, 0.70f, 1f);
    private readonly Color _ccdClearDeep     = new Color(0.30f, 0.34f, 0.58f, 1f);
    private readonly Color _ccdClearEmis     = new Color(0.18f, 0.18f, 0.36f, 1f);

    // Pipa proses (CCD -> MHP / Filter Press) yang dibuat di Blender. Flow tube di-animasikan
    // (emissive pulse + scroll) untuk menunjukkan PLS & slurry mengalir keluar dari CCD.
    private Renderer _plsFlowPipe;        // PLS overflow -> Level 10 Pemurnian (hijau)
    private Renderer _underflowFlowPipe;  // underflow solids -> Tailing Filter Press (coklat)
    private MaterialPropertyBlock _flowMpb;
    private float _flowPhase;
    private bool _pipeFlowsActive;
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

    [Header("=== Field Observation ===")]
    private readonly Dictionary<string, Material> _runtimeMats = new Dictionary<string, Material>();
    private bool _awaitingCcdStartupReport;
    private bool _separationRunning;

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
        GameLevelManager.OnLevel10CCDStartAuthorized += OnLevel10CCDStartAuthorized;
    }

    private void OnDisable()
    {
        GameLevelManager.OnLevelStarted -= OnLevelStarted;
        GameLevelManager.OnDCSButtonPressed -= OnDcsButtonPressed;
        GameLevelManager.OnLevel10CCDStartAuthorized -= OnLevel10CCDStartAuthorized;
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
            StopProcessPipeFlows();
            StopSequence();
            StopAudio(_driveAudio);
            return;
        }

        _ccdStarted = false;
        _questComplete = false;
        _awaitingCcdStartupReport = false;
        _separationRunning = false;
        ResetCcdSamplingAndLabState();
        _progressCurrent = 0f;
        _solidsSettlingCurrent = 0f;
        _clarityCurrent = 0f;
        AutoFindReferences();   // re-resolve real model refs (recover NULL dari scene lama)
        HideLegacyCcdValveArtifacts();
        StopProcessPipeFlows(); // flow tube tersembunyi sampai pemisahan CCD selesai
        FixBakedLabels();       // ganti teks baked yang ter-cermin dengan overlay readable
        SetProcessVisuals(false);

        if (_hud != null)
            _hud.ShowNotifPublic(_msgStart);

        TeleportPlayer(_teleportTargetDcs);
    }

    private void ResetCcdSamplingAndLabState()
    {
        if (_sampleTeleportCoroutine != null)
        {
            StopCoroutine(_sampleTeleportCoroutine);
            _sampleTeleportCoroutine = null;
        }

        for (int i = 0; i < 3; i++)
        {
            _ccdBottleFillProgress[i] = 0f;
            _ccdBottleFilling[i] = false;
            _ccdSampleTaken[i] = false;
            _ccdSampleReadyForInventory[i] = false;
            _ccdSampleStoredInInventory[i] = false;
            _sampleInventoryBottles[i] = null;

            if (_ccdSampleBottles[i] != null)
                _ccdSampleBottles[i].SetActive(true);

            if (_ccdStationFillLiquid[i] != null)
            {
                _ccdStationFillLiquid[i].localScale = new Vector3(0.82f, 1.15f, 0.82f);
                _ccdStationFillLiquid[i].localPosition = new Vector3(0f, -0.35f, 0f);
            }

            if (_ccdStationLabels[i] != null)
            {
                var tm = _ccdStationLabels[i].GetComponent<TextMesh>();
                if (tm != null)
                {
                    int thNo = i == 0 ? 1 : i == 1 ? 3 : 5;
                    tm.text = $"PLS Th-{thNo}\n[ ambil sample ]";
                    tm.color = Color.white;
                }
            }

            if (_ccdLabSlotLiquids[i] != null)
            {
                Vector3 s = _ccdLabSlotLiquids[i].localScale;
                float fullY = Mathf.Abs(_ccdLabSlotBaseY[i]) > 0.0001f ? _ccdLabSlotBaseY[i] : s.y;
                _ccdLabSlotLiquids[i].localScale = new Vector3(s.x, fullY * 0.02f, s.z);
            }
        }

        for (int i = 0; i < _ccdLabStepDone.Length; i++)
        {
            _ccdLabStepDone[i] = false;
            if (_ccdLabStepStations[i] != null)
                SetLabStepVisual(i, false, false);
        }
        _ccdLabActiveStep = -1;
        _ccdLabStepConfirmed = false;

        if (_sampleInventoryRoot != null)
        {
            Destroy(_sampleInventoryRoot.gameObject);
            _sampleInventoryRoot = null;
        }

        if (_ccdLabQcCanvas != null)
        {
            Destroy(_ccdLabQcCanvas);
            _ccdLabQcCanvas = null;
        }

        _pendingAcceptAction = null;
        _ccdLabSubmitted = false;
        _ccdLabSequenceStarted = false;
        if (_ccdLabScreenText != null)
            _ccdLabScreenText.text = "QC LAB\nStandby...";
    }

    private void Update()
    {
        if (!_levelActive)
            return;

        // Overlay label billboard jalan sejak level mulai (sebelum DCS ditekan juga).
        BillboardOverlayLabels();

        if (!_ccdStarted)
            return;

        if (_separationRunning)
        {
            AnimateRakeArms();
            AnimateCcdLiquidMotion();
            AnimateRotatingMachinery();
        }

        AnimateProcessPipeFlow(Time.deltaTime);

        // Setelah CCD stabil: aktifkan flow sampling PLS + lab QC.
        Update_PLSSampling();
    }

    private void OnDcsButtonPressed(int number)
    {
        if (!_levelActive || number != 9 || _ccdStarted)
            return;

        _ccdStarted = true;
        _sequenceCoroutine = StartCoroutine(RunCcdFieldReportPrompt());
    }

    private IEnumerator RunCcdFieldReportPrompt()
    {
        _awaitingCcdStartupReport = true;
        if (_hud != null)
            _hud.PlayManualFade(_fadeDuration);

        yield return new WaitForSeconds(_fadeDuration * 0.5f);
        TeleportPlayer(ResolveFieldStandSpot());
        yield return new WaitForSeconds(_fadeDuration * 0.5f + 0.15f);

        if (_hud != null)
            _hud.ShowNotifPublic("Di area CCD. Lapor HT awal: 'CCD siap, alirkan cairan dari flash vessel'.", 8f);

        _sequenceCoroutine = null;
    }

    private void OnLevel10CCDStartAuthorized()
    {
        if (!_levelActive || !_ccdStarted || _questComplete || _separationRunning)
            return;
        if (!_awaitingCcdStartupReport)
            return;

        if (_sequenceCoroutine != null)
            StopCoroutine(_sequenceCoroutine);
        _sequenceCoroutine = StartCoroutine(RunCCDSequence());
    }

    private IEnumerator RunCCDSequence()
    {
        _awaitingCcdStartupReport = false;

        if (_hud != null)
            _hud.ShowNotifPublic("Flash vessel discharge dibuka. Slurry mulai masuk CCD dari dasar thickener.", 6f);
        TeleportPlayer(ResolveFieldStandSpot());
        yield return new WaitForSeconds(_fieldObservationDelay);

        if (_hud != null)
            _hud.ShowNotifPublic("Cairan CCD naik dari dasar. Tunggu feed column penuh sebelum rake arm mulai mengaduk.", 6f);

        SetProcessVisuals(true);
        PrepareCcdLiquidAtBottom();
        StartSettlingParticleFx();
        yield return AnimateCcdLiquidRise(4.2f);

        _separationRunning = true;
        _ccdRotorOn = true;   // rotor/rake mulai mengaduk SETELAH cairan naik penuh
        if (_hud != null)
            _hud.ShowNotifPublic(_msgObserve);
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

        // Pemisahan selesai → 2 aliran mulai: PLS jernih ke Pemurnian/MHP, padatan underflow ke Filter Press.
        StartProcessPipeFlows();

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

    private void HideLegacyCcdValveArtifacts()
    {
        Transform[] all = FindObjectsByType<Transform>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        for (int i = 0; i < all.Length; i++)
        {
            Transform t = all[i];
            if (t == null) continue;

            string n = t.name;
            bool isSourceL5 =
                n.Equals("L5_Condensate_Drain_Handwheel_StirRedesign", System.StringComparison.OrdinalIgnoreCase) ||
                IsChildOfNamed(t, "L5_Condensate_Drain_Handwheel_StirRedesign");
            if (isSourceL5)
                continue;

            bool legacy =
                n.IndexOf("L9_CCD_FieldControl_", System.StringComparison.OrdinalIgnoreCase) >= 0 ||
                n.IndexOf("Underflow_KnifeValve", System.StringComparison.OrdinalIgnoreCase) >= 0 ||
                n.IndexOf("Handwheel_L5_StirRedesign_CCD", System.StringComparison.OrdinalIgnoreCase) >= 0 ||
                n.IndexOf("L9_CCD_L5_Condensate_Drain_Handwheel", System.StringComparison.OrdinalIgnoreCase) >= 0;
            if (!legacy)
                continue;

            if (t.parent != null && legacy)
                t.gameObject.SetActive(false);
        }
    }

    private bool IsChildOfNamed(Transform t, string namePrefix)
    {
        while (t != null)
        {
            if (t.name.StartsWith(namePrefix, System.StringComparison.OrdinalIgnoreCase))
                return true;
            t = t.parent;
        }
        return false;
    }

    private Material RuntimeMat(string name, Color color, float metallic)
    {
        if (!_runtimeMats.TryGetValue(name, out Material mat) || mat == null)
        {
            Shader shader = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard");
            mat = new Material(shader);
            mat.name = name;
            _runtimeMats[name] = mat;
        }
        if (mat.HasProperty("_BaseColor")) mat.SetColor("_BaseColor", color);
        if (mat.HasProperty("_Color")) mat.SetColor("_Color", color);
        if (mat.HasProperty("_Metallic")) mat.SetFloat("_Metallic", metallic);
        return mat;
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

    private Transform FindAnywhereContains(string token)
    {
        foreach (var t in GameObject.FindObjectsByType<Transform>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            if (t != null && t.gameObject.scene.IsValid() && t.name.IndexOf(token, System.StringComparison.OrdinalIgnoreCase) >= 0)
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

    private void AnimateCcdLiquidMotion()
    {
        // Cairan satu volume berputar via shader swirl (rotor) HANYA saat rotor aktif.
        float swirl = _ccdRotorOn ? Mathf.Max(0.2f, _rakeRpm) : 0f;
        for (int i = 0; i < 3; i++)
            if (_ccdFluid[i] != null) _ccdFluid[i].SetSwirl(swirl);
    }

    private void RotateLiquidLayer(Renderer renderer, Vector3 axisPoint, float degrees)
    {
        if (renderer == null || axisPoint == Vector3.zero)
            return;
        renderer.transform.RotateAround(axisPoint, Vector3.up, degrees);
    }

    private void PulseLiquidTint(Renderer renderer, Color color)
    {
        if (renderer == null)
            return;
        if (_mpb == null) _mpb = new MaterialPropertyBlock();
        renderer.GetPropertyBlock(_mpb);
        _mpb.SetColor("_BaseColor", color);
        _mpb.SetColor("_Color", color);
        renderer.SetPropertyBlock(_mpb);
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

    // Aktifkan 2 aliran pipa keluar CCD: PLS jernih -> Pemurnian/MHP, padatan underflow -> Filter Press.
    // Flow tube default disembunyikan; di sini kita tampilkan + nyalakan animasi.
    private void StartProcessPipeFlows()
    {
        _pipeFlowsActive = true;
        if (_plsFlowPipe != null)
        {
            _plsFlowPipe.enabled = true;
            if (_plsFlowPipe.sharedMaterial != null) _plsFlowPipe.sharedMaterial.EnableKeyword("_EMISSION");
        }
        if (_underflowFlowPipe != null)
        {
            _underflowFlowPipe.enabled = true;
            if (_underflowFlowPipe.sharedMaterial != null) _underflowFlowPipe.sharedMaterial.EnableKeyword("_EMISSION");
        }
    }

    private void StopProcessPipeFlows()
    {
        _pipeFlowsActive = false;
        if (_plsFlowPipe != null) _plsFlowPipe.enabled = false;
        if (_underflowFlowPipe != null) _underflowFlowPipe.enabled = false;
    }

    // Pulsa emissive flow tube pipa untuk menunjukkan PLS (hijau) mengalir ke Pemurnian
    // dan slurry underflow (coklat) mengalir ke Filter Press. Aktif saat CCD jalan.
    private void AnimateProcessPipeFlow(float dt)
    {
        if (!_pipeFlowsActive) return;
        if (_plsFlowPipe == null && _underflowFlowPipe == null) return;
        if (_flowMpb == null) _flowMpb = new MaterialPropertyBlock();
        _flowPhase += dt * 2.2f;
        float pulse = 0.55f + 0.45f * Mathf.Sin(_flowPhase);              // 0.1..1.0
        // PLS hijau
        if (_plsFlowPipe != null)
        {
            _plsFlowPipe.GetPropertyBlock(_flowMpb);
            Color c = new Color(0.32f, 0.60f, 0.26f) * (0.6f + pulse);
            _flowMpb.SetColor("_EmissionColor", c);
            _flowMpb.SetColor("_BaseColor", new Color(0.42f, 0.62f, 0.30f, 1f));
            _plsFlowPipe.SetPropertyBlock(_flowMpb);
        }
        // Slurry coklat (fase berbeda biar tidak sinkron)
        if (_underflowFlowPipe != null)
        {
            float pulse2 = 0.55f + 0.45f * Mathf.Sin(_flowPhase + 1.6f);
            _underflowFlowPipe.GetPropertyBlock(_flowMpb);
            Color c = new Color(0.32f, 0.20f, 0.12f) * (0.5f + pulse2);
            _flowMpb.SetColor("_EmissionColor", c);
            _flowMpb.SetColor("_BaseColor", new Color(0.34f, 0.22f, 0.15f, 1f));
            _underflowFlowPipe.SetPropertyBlock(_flowMpb);
        }
    }

    // t: 0 (mulai, slurry keruh penuh) -> 1 (stabil, padatan mengendap, PLS jernih).
    // Mensimulasikan pemisahan: feedwell core keruh menyusut, zona pengendapan turun,
    // permukaan PLS makin jernih (warna lerp turbid->clear), lumpur underflow naik di dasar.
    private void UpdateMudLayers(float t)
    {
        float clarity = SmoothStep(Mathf.Clamp01((t - 0.15f) / 0.85f));
        // Cairan satu volume: warna lerp keruh ungu-coklat -> ungu jernih kebiruan seiring pemisahan.
        for (int i = 0; i < 3; i++)
        {
            if (_ccdFluid[i] == null) continue;
            _ccdFluid[i].SetColors(
                Color.Lerp(_ccdTurbidShallow, _ccdClearShallow, clarity),
                Color.Lerp(_ccdTurbidDeep, _ccdClearDeep, clarity),
                Color.Lerp(_ccdTurbidEmis, _ccdClearEmis, clarity));
            _ccdFluid[i].SetLevel01(1f);
        }
    }

    // Pasang TankFluidColumn (cairan TERANG satu volume) pada zona settling tiap thickener,
    // dan SEMBUNYIKAN layer disc/pool lama (clearPLS surface, feedwell core, underflow pool) supaya
    // tidak ada double-liquid & tidak ada animasi "melebar dari tengah". Idempotent.
    private void EnsureCcdFluidColumns()
    {
        for (int i = 0; i < 3; i++)
        {
            if (_ccdFluid[i] != null) continue;
            Renderer vol = _settlingZones[i];
            if (vol == null) continue;
            // Pastikan transform settling zone di skala/posisi penuh sebelum bounds di-capture.
            Transform tr = vol.transform;
            if (_settlingBaseScale[i] != Vector3.zero) tr.localScale = _settlingBaseScale[i];
            tr.localPosition = _settlingBaseLocalPosition[i];
            vol.enabled = true;
            var col = vol.GetComponent<TankFluidColumn>();
            if (col == null) col = vol.gameObject.AddComponent<TankFluidColumn>();
            col.Setup(vol, _ccdTurbidShallow, _ccdTurbidDeep, _ccdTurbidEmis);
            _ccdFluid[i] = col;
            // Sembunyikan layer lama agar hanya satu volume cairan terang yang tampil.
            if (_clearPlsSurfaces[i] != null) _clearPlsSurfaces[i].enabled = false;
            if (_feedwellCores[i] != null) _feedwellCores[i].enabled = false;
            if (_underflowPools[i] != null) _underflowPools[i].enabled = false;
        }
    }

    private void PrepareCcdLiquidAtBottom()
    {
        EnsureCcdFluidColumns();
        _ccdRotorOn = false;
        for (int i = 0; i < 3; i++)
        {
            if (_ccdFluid[i] == null) continue;
            _ccdFluid[i].Show();
            _ccdFluid[i].SetColors(_ccdTurbidShallow, _ccdTurbidDeep, _ccdTurbidEmis);
            _ccdFluid[i].SetSwirl(0f);
            _ccdFluid[i].SetLevel01(0f);   // mulai KOSONG, permukaan di dasar
        }
    }

    private IEnumerator AnimateCcdLiquidRise(float duration)
    {
        duration = Mathf.Max(0.1f, duration);
        EnsureFeedInflowStreams();
        SetFeedInflowActive(true);
        float elapsed = 0f;
        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            float t = SmoothStep(Mathf.Clamp01(elapsed / duration));
            // Permukaan cairan NAIK DARI DASAR ke atas (shader _FillY), bukan melebar dari tengah.
            for (int i = 0; i < 3; i++)
                if (_ccdFluid[i] != null) _ccdFluid[i].SetLevel01(t);
            UpdateFeedInflow();
            yield return null;
        }
        for (int i = 0; i < 3; i++)
            if (_ccdFluid[i] != null) _ccdFluid[i].SetLevel01(1f);
    }

    // ============================================================
    //  FEED INFLOW — cairan slurry TURUN dari atas masuk feedwell tiap thickener.
    //  Inilah yang "menyebabkan" cairan settling zone naik (umpan dari flash vessel).
    // ============================================================
    private GameObject[] _feedInflow = new GameObject[3];
    private ParticleSystem[] _feedSplashFx = new ParticleSystem[3];
    private Material _feedInflowMat;
    private float _feedInflowPhase;

    private void EnsureFeedInflowStreams()
    {
        if (_feedInflowMat == null)
        {
            Shader sh = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard");
            _feedInflowMat = new Material(sh) { name = "M_CCD_FeedInflow_Runtime" };
            Color feed = new Color(0.34f, 0.22f, 0.12f); // slurry umpan coklat (dari flash vessel)
            if (_feedInflowMat.HasProperty("_BaseColor")) _feedInflowMat.SetColor("_BaseColor", feed);
            if (_feedInflowMat.HasProperty("_Color")) _feedInflowMat.SetColor("_Color", feed);
            _feedInflowMat.EnableKeyword("_EMISSION");
            if (_feedInflowMat.HasProperty("_EmissionColor")) _feedInflowMat.SetColor("_EmissionColor", feed * 0.5f);
            if (_feedInflowMat.HasProperty("_Smoothness")) _feedInflowMat.SetFloat("_Smoothness", 0.75f);
            if (_feedInflowMat.HasProperty("_Metallic")) _feedInflowMat.SetFloat("_Metallic", 0.0f);
        }
        for (int i = 0; i < 3; i++)
        {
            if (_settlingZones[i] == null || _feedInflow[i] != null) continue;
            var go = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            go.name = "CCD_FeedInflow_" + i;
            var col = go.GetComponent<Collider>(); if (col != null) Destroy(col);
            go.transform.SetParent(transform, true);
            var r = go.GetComponent<Renderer>(); if (r != null) r.sharedMaterial = _feedInflowMat;
            go.SetActive(false);
            _feedInflow[i] = go;

            // Splash particle di titik jatuh (cipratan slurry masuk).
            var splashGo = new GameObject("CCD_FeedSplash_" + i);
            splashGo.transform.SetParent(transform, true);
            var ps = splashGo.AddComponent<ParticleSystem>();
            ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            var main = ps.main; main.startLifetime = 0.5f; main.startSpeed = 1.2f;
            main.startSize = 0.18f; main.gravityModifier = 1.1f; main.maxParticles = 60;
            main.startColor = new Color(0.34f, 0.22f, 0.12f, 0.9f);
            var em = ps.emission; em.rateOverTime = 28f;
            var sh2 = ps.shape; sh2.shapeType = ParticleSystemShapeType.Cone; sh2.angle = 28f; sh2.radius = 0.25f;
            var pr = ps.GetComponent<ParticleSystemRenderer>();
            var pmat = new Material(Shader.Find("Universal Render Pipeline/Particles/Unlit") ?? Shader.Find("Sprites/Default"));
            if (pmat.HasProperty("_BaseColor")) pmat.SetColor("_BaseColor", new Color(0.34f, 0.22f, 0.12f, 0.9f));
            pr.sharedMaterial = pmat;
            _feedSplashFx[i] = ps;
        }
    }

    private void SetFeedInflowActive(bool on)
    {
        for (int i = 0; i < 3; i++)
        {
            if (_feedInflow[i] != null) _feedInflow[i].SetActive(on);
            if (_feedSplashFx[i] != null)
            {
                if (on) _feedSplashFx[i].Play();
                else _feedSplashFx[i].Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            }
        }
        if (on) UpdateFeedInflow();
    }

    private void UpdateFeedInflow()
    {
        _feedInflowPhase += Time.deltaTime * 2.5f;
        for (int i = 0; i < 3; i++)
        {
            if (_feedInflow[i] == null || _settlingZones[i] == null) continue;
            var b = _settlingZones[i].bounds;
            float surfaceY = b.max.y;               // permukaan cairan yang sedang naik
            float feedTopY = b.center.y + b.size.y * 0.5f + 2.4f; // titik umpan di atas kolom
            if (feedTopY <= surfaceY + 0.05f) { _feedInflow[i].SetActive(false); continue; }
            if (!_feedInflow[i].activeSelf) _feedInflow[i].SetActive(true);
            Vector3 top = new Vector3(b.center.x, feedTopY, b.center.z);
            Vector3 bot = new Vector3(b.center.x, surfaceY, b.center.z);
            Vector3 mid = (top + bot) * 0.5f;
            float len = (top - bot).magnitude;
            var tr = _feedInflow[i].transform;
            tr.position = mid;
            tr.up = Vector3.up;
            // gelombang lebar tipis-tebal supaya terlihat mengalir
            float wob = 0.16f + 0.03f * Mathf.Sin(_feedInflowPhase * 3f + i);
            tr.localScale = new Vector3(wob, len * 0.5f, wob);
            // splash di permukaan
            if (_feedSplashFx[i] != null) _feedSplashFx[i].transform.position = bot;
        }
    }




    private void StartSettlingParticleFx()
    {
        if (_brownSettlingFx == null)
            BuildSettlingParticleFx();

        if (_brownSettlingFx == null)
            return;

        for (int i = 0; i < _brownSettlingFx.Length; i++)
        {
            if (_brownSettlingFx[i] == null)
                continue;
            if (!_brownSettlingFx[i].isPlaying)
                _brownSettlingFx[i].Play();
        }
    }

    private void StopSettlingParticleFx()
    {
        if (_brownSettlingFx == null)
            return;
        for (int i = 0; i < _brownSettlingFx.Length; i++)
        {
            if (_brownSettlingFx[i] != null)
                _brownSettlingFx[i].Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
        }
    }

    private void BuildSettlingParticleFx()
    {
        _brownSettlingFx = new ParticleSystem[3];
        Shader shader = Shader.Find("Universal Render Pipeline/Particles/Unlit")
                     ?? Shader.Find("Legacy Shaders/Particles/Alpha Blended Premultiply")
                     ?? Shader.Find("Sprites/Default");
        Material mat = shader != null ? new Material(shader) : null;
        if (mat != null)
            mat.color = new Color(0.34f, 0.20f, 0.11f, 0.78f);

        for (int i = 0; i < 3; i++)
        {
            Renderer zone = _settlingZones[i];
            if (zone == null)
                continue;

            GameObject go = new GameObject("CCD_BrownSolids_FallingFX_" + (i + 1));
            go.transform.SetParent(zone.transform, false);
            go.transform.localPosition = Vector3.up * 0.15f;
            ParticleSystem ps = go.AddComponent<ParticleSystem>();
            var main = ps.main;
            main.loop = true;
            main.startLifetime = new ParticleSystem.MinMaxCurve(1.8f, 3.2f);
            main.startSpeed = new ParticleSystem.MinMaxCurve(0.18f, 0.45f);
            main.startSize = new ParticleSystem.MinMaxCurve(0.035f, 0.085f);
            main.startColor = new Color(0.38f, 0.22f, 0.12f, 0.85f);
            main.gravityModifier = 0.35f;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.maxParticles = 260;

            var emission = ps.emission;
            emission.rateOverTime = 55f;

            var shape = ps.shape;
            shape.shapeType = ParticleSystemShapeType.Box;
            Bounds b = zone.bounds;
            shape.scale = new Vector3(Mathf.Max(0.5f, b.size.x * 0.55f), Mathf.Max(0.4f, b.size.y * 0.35f), Mathf.Max(0.5f, b.size.z * 0.55f));

            var velocity = ps.velocityOverLifetime;
            velocity.enabled = true;
            velocity.y = new ParticleSystem.MinMaxCurve(-0.55f, -0.25f);

            var renderer = ps.GetComponent<ParticleSystemRenderer>();
            if (renderer != null && mat != null)
                renderer.sharedMaterial = mat;

            ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            _brownSettlingFx[i] = ps;
        }
    }

    private readonly Vector3[] _feedwellBaseScale = new Vector3[3];
    private readonly Vector3[] _underflowBaseScale = new Vector3[3];
    private readonly Vector3[] _settlingBaseScale = new Vector3[3];
    private readonly Vector3[] _settlingBaseLocalPosition = new Vector3[3];
    private ParticleSystem[] _brownSettlingFx;

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

        // Cairan satu volume terang: sembunyikan saat tidak aktif (cairan ungu tak tampil di awal).
        if (!active)
        {
            _ccdRotorOn = false;
            for (int i = 0; i < 3; i++)
                if (_ccdFluid[i] != null) { _ccdFluid[i].SetSwirl(0f); _ccdFluid[i].Hide(); }
        }

        // State awal: PLS surface keruh (coklat slurry) tapi TETAP tampil, core feedwell penuh,
        // lumpur underflow rendah. Saat aktif, sequence menganimasikan keruh->jernih.
        for (int i = 0; i < 3; i++)
        {
            if (_clearPlsSurfaces[i] != null)
            {
                // Layer disc lama selalu disembunyikan — diganti volume TankFluidColumn.
                if (_ccdFluid[i] != null) _clearPlsSurfaces[i].enabled = false;
                else if (!_clearPlsSurfaces[i].enabled) _clearPlsSurfaces[i].enabled = true;
            }
        }
        if (!active && _ccdFluid[0] == null)
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
            StopSettlingParticleFx();
            SetFeedInflowActive(false);
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
        if (_sequenceCoroutine != null)
        {
            StopCoroutine(_sequenceCoroutine);
            _sequenceCoroutine = null;
        }

        if (_sampleTeleportCoroutine != null)
        {
            StopCoroutine(_sampleTeleportCoroutine);
            _sampleTeleportCoroutine = null;
        }
    }

    /// <summary>
    /// Titik observasi CCD harus memakai SpawnPoint_Lvl9. Bounds fallback hanya dipakai
    /// kalau scene lama belum punya spawn point tersebut.
    /// </summary>
    private Transform ResolveFieldStandSpot()
    {
        GameObject lvl9 = GameObject.Find("SpawnPoint_Lvl9");
        if (lvl9 != null)
            return lvl9.transform;

        if (_teleportTargetField != null)
            return _teleportTargetField;

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

        var existing = GameObject.Find("SpawnPoint_Lvl9_Observe_Runtime");
        var sp = existing != null ? existing : new GameObject("SpawnPoint_Lvl9_Observe_Runtime");
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
            GameObject field = GameObject.Find("SpawnPoint_Lvl9") ?? GameObject.Find("SpawnPoint_Lvl10");
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
            if (_settlingZones[i] != null)
            {
                _settlingBaseScale[i] = _settlingZones[i].transform.localScale;
                _settlingBaseLocalPosition[i] = _settlingZones[i].transform.localPosition;
            }
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

        // --- Pipa proses Blender (CCD -> MHP / Filter Press): cari flow tube untuk animasi ---
        var pipesRoot = GameObject.Find("CCD_Process_Pipes");
        if (pipesRoot != null)
        {
            var plsFlow = FindDeepChild(pipesRoot.transform, "PLS_Flow");
            var underFlow = FindDeepChild(pipesRoot.transform, "Underflow_Flow");
            _plsFlowPipe = plsFlow != null ? plsFlow.GetComponent<Renderer>() : null;
            _underflowFlowPipe = underFlow != null ? underFlow.GetComponent<Renderer>() : null;
        }
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

    public bool AllPLSSamplesTakenForMarker => CountPLSSamples() >= 3;
    public bool LabSubmittedForMarker => _ccdLabSubmitted;

    public Transform GetCurrentTaskMarkerTarget()
    {
        if (!_questComplete)
            return ResolveCcdFieldMarkerTarget();

        if (CountPLSSamples() < 3)
            return ResolveNextSampleMarkerTarget();

        if (!_ccdLabSubmitted)
            return ResolveLabStepMarkerTarget();

        if (_ccdLabQcCanvas != null && _ccdLabQcCanvas.activeInHierarchy)
            return _ccdLabQcCanvas.transform;

        return ResolveLabMarkerTarget();
    }

    private Transform ResolveLabStepMarkerTarget()
    {
        if (_ccdLabActiveStep >= 0
            && _ccdLabActiveStep < _ccdLabStepButtons.Length
            && _ccdLabStepButtons[_ccdLabActiveStep] != null
            && _ccdLabStepButtons[_ccdLabActiveStep].gameObject.activeInHierarchy)
            return _ccdLabStepButtons[_ccdLabActiveStep];

        if (_ccdLabStepButtons[0] != null && _ccdLabStepButtons[0].gameObject.activeInHierarchy)
            return _ccdLabStepButtons[0];

        return ResolveLabMarkerTarget();
    }

    private Transform ResolveCcdFieldMarkerTarget()
    {
        if (_ccdField != null && _ccdField.activeInHierarchy)
        {
            Transform rigRoot = FindDeepChild(_ccdField.transform, "CCD_BlenderRig");
            return rigRoot != null && rigRoot.gameObject.activeInHierarchy ? rigRoot : _ccdField.transform;
        }

        GameObject found = GameObject.Find("CCD_BlenderRig") ?? GameObject.Find("CCD_Field");
        return found != null ? found.transform : transform;
    }

    private Transform ResolveNextSampleMarkerTarget()
    {
        for (int i = 0; i < _ccdSampleTaken.Length; i++)
        {
            if (_ccdSampleTaken[i]) continue;

            if (_ccdSampleStations[i] != null && _ccdSampleStations[i].activeInHierarchy)
                return _ccdSampleStations[i].transform;

            if (_ccdSampleBottles[i] != null && _ccdSampleBottles[i].activeInHierarchy)
                return _ccdSampleBottles[i].transform;
        }

        int[] thNos = { 1, 3, 5 };
        for (int i = 0; i < thNos.Length; i++)
        {
            GameObject station = GameObject.Find($"L9_PLS_SampleStation_Th{thNos[i]}");
            if (station != null && station.activeInHierarchy)
                return station.transform;
        }

        return ResolveCcdFieldMarkerTarget();
    }

    private Transform ResolveLabMarkerTarget()
    {
        if (_ccdLabResultScreen != null && _ccdLabResultScreen.gameObject.activeInHierarchy)
            return _ccdLabResultScreen;

        if (_ccdLabBuilding != null && _ccdLabBuilding.activeInHierarchy)
            return _ccdLabBuilding.transform;

        GameObject lab = GameObject.Find("L9_LabBuilding");
        return lab != null ? lab.transform : ResolveCcdFieldMarkerTarget();
    }

    // ============================================================
    //  PLS SAMPLING + LAB QC (Opsi A: dipindah dari Level 8 ke sini)
    //  Real-world HPAL: sample PLS untuk lab QC diambil dari OVERFLOW CCD
    //  setelah solid-cair dipisah; bukan dari flash vessel discharge.
    // ============================================================

    [Header("=== Sample Station + Lab QC ===")]
    [SerializeField] private GameObject _qcLabFbxOverride; // optional: assign FBX manually

    private GameObject[] _ccdSampleStations = new GameObject[3];
    private GameObject[] _ccdSampleBottles = new GameObject[3];
    private Transform[] _ccdStationFillLiquid = new Transform[3];
    private float[] _ccdBottleFillProgress = new float[3];
    private bool[] _ccdBottleFilling = new bool[3];
    private bool[] _ccdSampleTaken = new bool[3];
    private bool[] _ccdSampleReadyForInventory = new bool[3];
    private bool[] _ccdSampleStoredInInventory = new bool[3];
    private bool _ccdStationsBuilt;
    private float _ccdSampleInteractRadius = 4f;
    [SerializeField] private float _sampleInventoryTouchRadius = 0.75f;
    [SerializeField] private float _sampleTeleportFadeDuration = 2.0f;
    [SerializeField] private float _sampleSuccessPause = 1.0f;
    [SerializeField] private float _sampleStandDistance = 1.65f;
    private Coroutine _sampleTeleportCoroutine;
    private Transform _sampleInventoryRoot;
    private GameObject[] _sampleInventoryBottles = new GameObject[3];

    private GameObject _ccdLabBuilding;
    private Transform[] _ccdLabSlotLiquids = new Transform[3];
    private readonly float[] _ccdLabSlotBaseY = new float[3] { 1.7f, 1.7f, 1.7f };
    private Transform _ccdLabAnalyzerRotor;
    private Transform _ccdLabResultScreen;
    private TextMesh _ccdLabScreenText;
    private GameObject _ccdLabQcCanvas;
    private bool _ccdLabBuilt;
    private bool _ccdLabSubmitted;
    private bool _ccdLabSequenceStarted;
    private readonly GameObject[] _ccdLabStepStations = new GameObject[5];
    private readonly Transform[] _ccdLabStepButtons = new Transform[5];
    private readonly Transform[] _ccdLabStepLabels = new Transform[5];
    private readonly bool[] _ccdLabStepDone = new bool[5];
    private int _ccdLabActiveStep = -1;
    private bool _ccdLabStepConfirmed;

    // Warna PLS per sample point. PLS HPAL real = larutan sulfat hijau-kekuningan (Ni/Co),
    // makin ke wash overflow makin encer/bening.
    private static readonly Color[] _ccdSampleColors = {
        new Color(0.42f, 0.62f, 0.30f),   // Th-1: PLS pekat (Ni/Co tinggi, hijau zaitun)
        new Color(0.55f, 0.70f, 0.45f),   // Th-3: PLS lebih encer
        new Color(0.60f, 0.72f, 0.68f)    // Th-5: wash overflow (Ni rendah, hampir bening)
    };

    public void DebugEnterLabQCFromGameLevelManager(bool startLabSequence)
    {
        _levelActive = true;
        _ccdStarted = true;
        _questComplete = false;
        _separationRunning = false;
        _progressCurrent = 100f;
        _solidsSettlingCurrent = _solidsSettlingTarget;
        _clarityCurrent = _clarityTarget;

        AutoFindReferences();
        SetProcessVisuals(true);
        StartProcessPipeFlows();
        BuildCCDSampleStations();
        BuildCCDLabBuilding();
        EnsureSampleInventoryVisual();

        for (int i = 0; i < 3; i++)
        {
            _ccdBottleFillProgress[i] = 1f;
            _ccdBottleFilling[i] = false;
            _ccdSampleReadyForInventory[i] = false;
            _ccdSampleStoredInInventory[i] = true;
            _ccdSampleTaken[i] = true;

            if (_ccdSampleBottles[i] != null)
                _ccdSampleBottles[i].SetActive(false);

            CreateInventoryBottle(i);

            if (_ccdStationFillLiquid[i] != null)
            {
                _ccdStationFillLiquid[i].localScale = new Vector3(0.82f, 1.7f, 0.82f);
                _ccdStationFillLiquid[i].localPosition = new Vector3(0f, -0.10f, 0f);
            }

            if (_ccdStationLabels[i] != null)
            {
                TextMesh tm = _ccdStationLabels[i].GetComponent<TextMesh>();
                if (tm != null)
                {
                    int thNo = i == 0 ? 1 : i == 1 ? 3 : 5;
                    tm.text = $"PLS Th-{thNo}\nOK INVENTORY";
                    tm.color = new Color(0.5f, 1f, 0.5f);
                }
            }
        }

        GameLevelManager.Instance?.NotifyLevel10CCDComplete();
        TeleportPlayer(CreateLabStandSpot());

        if (_hud != null)
            _hud.ShowNotifPublic(startLabSequence
                ? "DEBUG: 3 sample PLS sudah masuk lab. Lab QC dimulai dari chain-of-custody."
                : "DEBUG: 3 sample PLS sudah siap di Lab QC. Tekan L/G/Y untuk mulai analisa.", 8f);

        if (startLabSequence)
            SubmitPLSToLab();
    }

    public void DebugAcceptLabQCFromGameLevelManager()
    {
        _ccdLabSequenceStarted = false;
        _ccdLabSubmitted = true;
        _ccdLabActiveStep = -1;
        for (int i = 0; i < _ccdLabStepDone.Length; i++)
        {
            _ccdLabStepDone[i] = true;
            SetLabStepVisual(i, false, true);
        }

        if (_ccdLabScreenText != null)
            _ccdLabScreenText.text = "QC SELESAI\nCCD OVERFLOW PASS\nNi 5.1 g/L | Co 0.52 g/L\nTSS 180 mg/L | Free acid 22 g/L";

        if (_ccdLabQcCanvas != null)
            _ccdLabQcCanvas.SetActive(false);

        GameLevelManager.Instance?.NotifyLevel10SamplePLSAccepted();
        if (_hud != null)
            _hud.ShowNotifPublic("DEBUG: Lab QC PLS diterima. Lapor HT: 'CCD aktif, PLS lulus QC'.", 7f);
    }

    private void BeginPLSSamplingFlow()
    {
        if (_hud != null)
            _hud.ShowNotifPublic("CCD stabil. Ambil 3 sample PLS overflow. Grab botol, isi dari sample port, lalu sentuhkan ke dada/inventory.", 10f);
        BuildCCDSampleStations();
        BuildCCDLabBuilding();
        TeleportToSampleOrLabAfterDelay(0, 0.15f);
    }

    private void Update_PLSSampling()
    {
        if (!_ccdStartedFlag()) return;
        FollowLabResultCanvas();
        UpdateCCDProximity();
        UpdateCCDBottleFill();
        UpdateSampleInventoryTouch();
        BillboardStationLabels();
        BillboardLabStepLabels();
        // Keyboard fallback G/Y: ambil sample aktif (untuk desktop simulator tanpa VR controller).
        if ((Input.GetKeyDown(KeyCode.G) || Input.GetKeyDown(KeyCode.Y)) && _ccdStationsBuilt)
        {
            if (!TryStoreReadySampleByInput())
                TryStartNearestSampleByInput();
        }
        // Manual submit: tekan L/G/Y kalau semua sample sudah tersimpan dan player ada di lab.
        if ((Input.GetKeyDown(KeyCode.L) || Input.GetKeyDown(KeyCode.G) || Input.GetKeyDown(KeyCode.Y))
            && CountPLSSamples() >= 3 && !_ccdLabSubmitted)
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

    private void BillboardLabStepLabels()
    {
        Vector3 head = GetPlayerHead();
        for (int i = 0; i < _ccdLabStepLabels.Length; i++)
        {
            Transform label = _ccdLabStepLabels[i];
            if (label == null || !label.gameObject.activeInHierarchy) continue;
            Vector3 dir = label.position - head;
            dir.y = 0f;
            if (dir.sqrMagnitude < 0.001f) continue;
            label.rotation = Quaternion.LookRotation(dir.normalized, Vector3.up);
        }
    }

    private bool _ccdStartedFlag() => _questComplete; // shorthand: hanya aktif setelah CCD stable

    private void BuildCCDSampleStations()
    {
        if (_ccdStationsBuilt) return;
        _ccdStationsBuilt = true;

        // Cari sample station yang sudah permanen di scene (pre-placed).
        // Nama: L9_PLS_SampleStation_Th1, Th3, Th5.
        int[] thNos = { 1, 3, 5 };
        bool allFound = true;
        for (int i = 0; i < 3; i++)
        {
            string goName = $"L9_PLS_SampleStation_Th{thNos[i]}";
            var existing = GameObject.Find(goName);
            if (existing == null)
            {
                // Cari termasuk inactive
                foreach (var g in Resources.FindObjectsOfTypeAll<GameObject>())
                {
                    if (g.name == goName && g.scene.IsValid()) { existing = g; break; }
                }
            }
            if (existing != null)
            {
                existing.SetActive(true);
                _ccdSampleStations[i] = existing;
                // Cari bottle dan pasang XRGrabInteractable
                var bottleTf = existing.transform.Find("Bottle");
                if (bottleTf != null)
                {
                    _ccdSampleBottles[i] = bottleTf.gameObject;
                    var liq = EnsureBottleLiquid(existing.transform, bottleTf, i);
                    if (liq != null) _ccdStationFillLiquid[i] = liq;
                    EnsureBottleGrabbable(bottleTf.gameObject);
                    AttachBottleSlosh(bottleTf.gameObject, i);
                }
                // Cari label child
                var lbl = existing.transform.Find("StationLabel");
                if (lbl == null)
                {
                    // Cari TextMesh child (label floating)
                    var tm = existing.GetComponentInChildren<TextMesh>();
                    if (tm != null) _ccdStationLabels[i] = tm.transform;
                }
                else _ccdStationLabels[i] = lbl;
            }
            else
            {
                allFound = false;
            }
        }

        // Fallback: kalau station belum ada di scene, build runtime (backward compat)
        if (!allFound)
        {
            float[] tankX = { 15.0f, 1.6f, -11.7f };
            float frontZ = 108.0f;
            float groundY = 0.05f;

            for (int i = 0; i < 3; i++)
            {
                if (_ccdSampleStations[i] != null) continue; // sudah found
                Vector3 pos = new Vector3(tankX[i], groundY, frontZ);
                _ccdSampleStations[i] = BuildCCDStationVisual(i, pos);
            }
        }
    }

    private Transform EnsureBottleLiquid(Transform stationRoot, Transform bottle, int sampleIndex)
    {
        if (bottle == null) return null;

        Transform liquid = bottle.Find("Liquid") ?? bottle.Find("BottleLiquid");
        if (liquid == null && stationRoot != null)
        {
            liquid = stationRoot.Find("BottleLiquid");
            if (liquid != null)
            {
                liquid.SetParent(bottle, true);
                liquid.name = "Liquid";
            }
        }

        if (liquid == null)
        {
            GameObject go = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            go.name = "Liquid";
            go.transform.SetParent(bottle, false);
            go.transform.localScale = new Vector3(0.82f, 1.15f, 0.82f);
            go.transform.localPosition = new Vector3(0f, -0.35f, 0f);
            var col = go.GetComponent<Collider>();
            if (col != null) Destroy(col);
            ApplySimpleMat(go.GetComponent<Renderer>(), _ccdSampleColors[Mathf.Clamp(sampleIndex, 0, _ccdSampleColors.Length - 1)]);
            liquid = go.transform;
        }

        return liquid;
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
        liquid.transform.localScale = new Vector3(0.82f, 1.15f, 0.82f);
        liquid.transform.localPosition = new Vector3(0, -0.35f, 0);
        var lc = liquid.GetComponent<Collider>(); if (lc != null) Destroy(lc);
        var lm = new Material(Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard"));
        lm.color = _ccdSampleColors[idx];
        lm.EnableKeyword("_EMISSION");
        if (lm.HasProperty("_EmissionColor")) lm.SetColor("_EmissionColor", _ccdSampleColors[idx] * 1.2f);
        liquid.GetComponent<Renderer>().sharedMaterial = lm;
        _ccdStationFillLiquid[idx] = liquid.transform;
        AttachBottleSlosh(bottle, idx);

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

    /// <summary>
    /// Pasang XRGrabInteractable ke botol sample supaya bisa di-grab tangan VR.
    /// Botol isKinematic=false, useGravity=false (melayang di tangan).
    /// Saat di-grab → sample terambil → botol menghilang (atau attach ke player).
    /// </summary>
    private void EnsureBottleGrabbable(GameObject bottle)
    {
        if (bottle == null) return;

        // Collider (wajib untuk XR grab)
        var col = bottle.GetComponent<Collider>();
        if (col == null)
        {
            var sc = bottle.AddComponent<SphereCollider>();
            sc.radius = 0.5f; // radius relatif terhadap scale botol
            sc.isTrigger = false;
        }

        // Rigidbody (wajib untuk XRGrabInteractable)
        var rb = bottle.GetComponent<Rigidbody>();
        if (rb == null)
        {
            rb = bottle.AddComponent<Rigidbody>();
        }
        rb.isKinematic = true;
        rb.useGravity = false;

        // XRGrabInteractable
        var grab = bottle.GetComponent<UnityEngine.XR.Interaction.Toolkit.Interactables.XRGrabInteractable>();
        if (grab == null)
        {
            grab = bottle.AddComponent<UnityEngine.XR.Interaction.Toolkit.Interactables.XRGrabInteractable>();
        }
        grab.throwOnDetach = false;
        grab.movementType = UnityEngine.XR.Interaction.Toolkit.Interactables.XRBaseInteractable.MovementType.Instantaneous;
    }

    private void AttachBottleSlosh(GameObject bottle, int sampleIndex)
    {
        if (bottle == null) return;
        Transform liquid = sampleIndex >= 0 && sampleIndex < _ccdStationFillLiquid.Length
            ? _ccdStationFillLiquid[sampleIndex]
            : null;
        if (liquid == null)
            liquid = bottle.transform.Find("Liquid") ?? bottle.transform.Find("BottleLiquid");
        if (liquid == null) return;

        var slosh = bottle.GetComponent<SampleBottleLiquidSlosh>();
        if (slosh == null) slosh = bottle.AddComponent<SampleBottleLiquidSlosh>();
        slosh.Setup(liquid);
    }

    private void UpdateCCDProximity()
    {
        // Sekarang pakai GRAB mechanic — cek apakah botol sudah di-grab pemain.
        if (!_ccdStationsBuilt) return;
        for (int i = 0; i < 3; i++)
        {
            if (_ccdSampleTaken[i] || _ccdBottleFilling[i] || _ccdSampleReadyForInventory[i]) continue;
            if (_ccdSampleBottles[i] == null) continue;

            // Cek apakah botol sudah di-grab (isSelected = sedang dipegang)
            var grab = _ccdSampleBottles[i].GetComponent<UnityEngine.XR.Interaction.Toolkit.Interactables.XRGrabInteractable>();
            bool grabbed = grab != null && grab.isSelected;

            if (grabbed)
                StartSampleFill(i);
        }
    }

    private void TryStartNearestSampleByInput()
    {
        Vector3 head = GetPlayerHead(); head.y = 0f;
        int best = -1;
        float bestDistance = float.MaxValue;

        for (int i = 0; i < 3; i++)
        {
            if (_ccdSampleTaken[i] || _ccdBottleFilling[i]) continue;
            if (_ccdSampleStations[i] == null) continue;

            Vector3 sPos = _ccdSampleStations[i].transform.position;
            sPos.y = 0f;
            float distance = Vector3.Distance(head, sPos);
            if (distance < bestDistance)
            {
                best = i;
                bestDistance = distance;
            }
        }

        if (best < 0) return;

        if (bestDistance > _ccdSampleInteractRadius)
        {
            if (_hud != null)
                _hud.ShowNotifPublic("Dekati pedestal sample dulu, lalu tekan G/Y atau grab botol.", 3f);
            return;
        }

        StartSampleFill(best);
    }

    private bool TryStoreReadySampleByInput()
    {
        Vector3 head = GetPlayerHead(); head.y = 0f;
        int best = -1;
        float bestDistance = float.MaxValue;
        for (int i = 0; i < 3; i++)
        {
            if (!_ccdSampleReadyForInventory[i] || _ccdSampleStoredInInventory[i]) continue;
            if (_ccdSampleStations[i] == null) continue;
            Vector3 sPos = _ccdSampleStations[i].transform.position;
            sPos.y = 0f;
            float distance = Vector3.Distance(head, sPos);
            if (distance < bestDistance)
            {
                best = i;
                bestDistance = distance;
            }
        }

        if (best < 0 || bestDistance > _ccdSampleInteractRadius)
            return false;

        StoreSampleInInventory(best);
        return true;
    }

    private void StartSampleFill(int sampleIndex)
    {
        if (sampleIndex < 0 || sampleIndex >= _ccdSampleTaken.Length) return;
        if (_ccdSampleTaken[sampleIndex] || _ccdBottleFilling[sampleIndex]) return;
        if (_ccdSampleReadyForInventory[sampleIndex]) return;

        _ccdSampleReadyForInventory[sampleIndex] = true;
        if (_hud != null)
            _hud.ShowNotifPublic($"Sample PLS Th-{(sampleIndex == 0 ? 1 : sampleIndex == 1 ? 3 : 5)} diambil.", 2f);
        StoreSampleInInventory(sampleIndex);
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
                _ccdBottleFilling[i] = false;
                _ccdSampleReadyForInventory[i] = true;
                if (_ccdStationLabels[i] != null)
                {
                    var tm = _ccdStationLabels[i].GetComponent<TextMesh>();
                    if (tm != null)
                    {
                        tm.text = $"PLS Th-{(i == 0 ? 1 : i == 1 ? 3 : 5)}\nTEMPEL KE INVENTORY";
                        tm.color = new Color(1f, 0.88f, 0.25f);
                    }
                }
                if (_hud != null)
                    _hud.ShowNotifPublic($"Botol PLS Th-{(i == 0 ? 1 : i == 1 ? 3 : 5)} penuh. Sentuhkan botol ke dada/inventory.", 5f);
                continue;
            }
        }
    }

    private void UpdateSampleInventoryTouch()
    {
        if (!_ccdStationsBuilt) return;
        Vector3 chest = GetInventoryChestPosition();
        for (int i = 0; i < 3; i++)
        {
            if (!_ccdSampleReadyForInventory[i] || _ccdSampleStoredInInventory[i]) continue;
            if (_ccdSampleBottles[i] == null) continue;

            GameObject bottle = _ccdSampleBottles[i];
            var grab = bottle.GetComponent<UnityEngine.XR.Interaction.Toolkit.Interactables.XRGrabInteractable>();
            bool selected = grab != null && grab.isSelected;
            if (selected && Vector3.Distance(bottle.transform.position, chest) <= _sampleInventoryTouchRadius)
                StoreSampleInInventory(i);
        }
    }

    private Vector3 GetInventoryChestPosition()
    {
        return GetPlayerHead() + Vector3.down * 0.45f;
    }

    private void StoreSampleInInventory(int sampleIndex)
    {
        if (sampleIndex < 0 || sampleIndex >= 3) return;
        if (_ccdSampleStoredInInventory[sampleIndex]) return;

        _ccdSampleStoredInInventory[sampleIndex] = true;
        _ccdSampleTaken[sampleIndex] = true;

        if (_ccdSampleBottles[sampleIndex] != null)
            _ccdSampleBottles[sampleIndex].SetActive(false);

        EnsureSampleInventoryVisual();
        CreateInventoryBottle(sampleIndex);

        if (_ccdStationLabels[sampleIndex] != null)
        {
            var tm = _ccdStationLabels[sampleIndex].GetComponent<TextMesh>();
            if (tm != null)
            {
                tm.text = $"PLS Th-{(sampleIndex == 0 ? 1 : sampleIndex == 1 ? 3 : 5)}\nOK INVENTORY";
                tm.color = new Color(0.5f, 1f, 0.5f);
            }
        }

        if (_hud != null)
            _hud.ShowNotifPublic($"Sample PLS Th-{(sampleIndex == 0 ? 1 : sampleIndex == 1 ? 3 : 5)} masuk inventory ({CountPLSSamples()}/3).", 4f);

        if (CountPLSSamples() >= 3)
        {
            if (_hud != null)
                _hud.ShowNotifPublic("3 sample PLS tersimpan. Pindah ke Lab QC untuk analisa Ni-Co.", 6f);
            TeleportToSampleOrLabAfterDelay(3, _sampleSuccessPause);
        }
        else
        {
            TeleportToSampleOrLabAfterDelay(sampleIndex + 1, _sampleSuccessPause);
        }
    }

    private void EnsureSampleInventoryVisual()
    {
        if (_sampleInventoryRoot != null) return;
        GameObject go = new GameObject("L9_CCD_SampleInventory_Runtime");
        if (_playerRigRoot != null)
            go.transform.SetParent(_playerRigRoot, false);
        go.transform.localPosition = new Vector3(0.32f, 1.08f, 0.28f);
        _sampleInventoryRoot = go.transform;
    }

    private void CreateInventoryBottle(int sampleIndex)
    {
        if (_sampleInventoryRoot == null || _sampleInventoryBottles[sampleIndex] != null)
            return;

        GameObject bottle = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        bottle.name = $"Inventory_PLS_Bottle_{sampleIndex + 1}";
        bottle.transform.SetParent(_sampleInventoryRoot, false);
        bottle.transform.localPosition = new Vector3((sampleIndex - 1) * 0.13f, 0f, 0f);
        bottle.transform.localScale = new Vector3(0.035f, 0.105f, 0.035f);
        var col = bottle.GetComponent<Collider>(); if (col != null) Destroy(col);
        Material glass = new Material(Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard"));
        ApplyTransparent(glass, new Color(0.78f, 0.9f, 1f, 0.32f));
        bottle.GetComponent<Renderer>().sharedMaterial = glass;

        GameObject liquid = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        liquid.name = "Liquid";
        liquid.transform.SetParent(bottle.transform, false);
        liquid.transform.localPosition = new Vector3(0f, -0.15f, 0f);
        liquid.transform.localScale = new Vector3(0.78f, 0.68f, 0.78f);
        var lcol = liquid.GetComponent<Collider>(); if (lcol != null) Destroy(lcol);
        ApplySimpleMat(liquid.GetComponent<Renderer>(), _ccdSampleColors[sampleIndex]);
        _sampleInventoryBottles[sampleIndex] = bottle;
    }

    private void TeleportToSampleOrLabAfterDelay(int nextSampleIndex, float delay)
    {
        if (_sampleTeleportCoroutine != null)
            StopCoroutine(_sampleTeleportCoroutine);

        _sampleTeleportCoroutine = StartCoroutine(TeleportToSampleOrLabCoroutine(nextSampleIndex, delay));
    }

    private IEnumerator TeleportToSampleOrLabCoroutine(int nextSampleIndex, float delay)
    {
        if (delay > 0f)
            yield return new WaitForSeconds(delay);

        float fadeDuration = Mathf.Max(2.0f, _sampleTeleportFadeDuration);
        if (_hud != null)
            _hud.PlayManualFade(fadeDuration);

        yield return new WaitForSeconds(fadeDuration * 0.5f);

        if (nextSampleIndex < 3)
        {
            TeleportPlayer(CreateSampleStandSpot(nextSampleIndex));
            int thNo = nextSampleIndex == 0 ? 1 : nextSampleIndex == 1 ? 3 : 5;
            if (_hud != null)
                _hud.ShowNotifPublic($"Ambil sample PLS Th-{thNo}. Grab botol sample atau tekan G/Y.", 5f);
        }
        else
        {
            TeleportPlayer(CreateLabStandSpot());
            if (_hud != null)
                _hud.ShowNotifPublic("Semua sample PLS masuk lab. Dekati meja QC, tekan G/Y/L untuk mulai chain-of-custody dan analisa.", 7f);
        }

        yield return new WaitForSeconds(fadeDuration * 0.5f);
        _sampleTeleportCoroutine = null;
    }

    private Transform CreateSampleStandSpot(int sampleIndex)
    {
        Transform station = null;
        if (sampleIndex >= 0 && sampleIndex < _ccdSampleStations.Length && _ccdSampleStations[sampleIndex] != null)
            station = _ccdSampleStations[sampleIndex].transform;
        if (station == null)
            return ResolveFieldStandSpot();

        string name = $"SpawnPoint_L9_PLS_Sample_{sampleIndex + 1}_Runtime";
        GameObject existing = GameObject.Find(name);
        GameObject sp = existing != null ? existing : new GameObject(name);

        Vector3 forward = ResolveSampleFrontDirection(station);
        Vector3 pos = station.position + forward * _sampleStandDistance;
        pos.y = 0.1f;
        Vector3 look = station.position - pos;
        look.y = 0f;

        sp.transform.position = pos;
        sp.transform.rotation = look.sqrMagnitude > 0.001f
            ? Quaternion.LookRotation(look.normalized, Vector3.up)
            : Quaternion.identity;
        return sp.transform;
    }

    private Vector3 ResolveSampleFrontDirection(Transform station)
    {
        Vector3 a = station != null ? station.forward : Vector3.back;
        a.y = 0f;
        if (a.sqrMagnitude < 0.001f)
            a = Vector3.back;
        a.Normalize();

        Vector3 b = -a;
        Vector3 fieldCenter = _ccdField != null ? GetRendererBoundsCenter(_ccdField) : Vector3.zero;
        if (fieldCenter != Vector3.zero && station != null)
        {
            Vector3 pa = station.position + a * _sampleStandDistance;
            Vector3 pb = station.position + b * _sampleStandDistance;
            pa.y = pb.y = fieldCenter.y = 0f;
            return Vector3.Distance(pa, fieldCenter) >= Vector3.Distance(pb, fieldCenter) ? a : b;
        }

        return a;
    }

    private Vector3 GetRendererBoundsCenter(GameObject go)
    {
        if (go == null) return Vector3.zero;
        Renderer[] renderers = go.GetComponentsInChildren<Renderer>();
        if (renderers == null || renderers.Length == 0)
            return go.transform.position;

        Bounds bounds = renderers[0].bounds;
        for (int i = 1; i < renderers.Length; i++)
            bounds.Encapsulate(renderers[i].bounds);
        return bounds.center;
    }

    private Transform CreateLabStandSpot()
    {
        Transform lab = ResolveLabMarkerTarget();
        if (lab == null)
            return ResolveFieldStandSpot();

        string name = "SpawnPoint_L9_LabQC_Runtime";
        GameObject existing = GameObject.Find(name);
        GameObject sp = existing != null ? existing : new GameObject(name);

        Vector3 forward = lab.forward;
        forward.y = 0f;
        if (forward.sqrMagnitude < 0.001f)
            forward = Vector3.back;
        forward.Normalize();

        Vector3 pos = lab.position - forward * 4.2f;
        pos.y = 0.1f;
        Vector3 look = lab.position - pos;
        look.y = 0f;

        sp.transform.position = pos;
        sp.transform.rotation = look.sqrMagnitude > 0.001f
            ? Quaternion.LookRotation(look.normalized, Vector3.up)
            : Quaternion.identity;
        return sp.transform;
    }

    private int CountPLSSamples() { int c = 0; foreach (var s in _ccdSampleTaken) if (s) c++; return c; }

    private void BuildCCDLabBuilding()
    {
        if (_ccdLabBuilt) return;
        _ccdLabBuilt = true;

        // Cari lab building yang sudah permanen di scene (pre-placed)
        var existing = GameObject.Find("L9_LabBuilding");
        if (existing == null)
        {
            foreach (var g in Resources.FindObjectsOfTypeAll<GameObject>())
                if (g.name == "L9_LabBuilding" && g.scene.IsValid()) { existing = g; break; }
        }

        if (existing != null)
        {
            existing.SetActive(true);
            _ccdLabBuilding = existing;
        }
        else
        {
            // Fallback: instantiate dari FBX kalau tidak ada di scene
            Vector3 labOrigin = new Vector3(-20f, 0f, 104f);
            GameObject fbxPrefab = _qcLabFbxOverride;
#if UNITY_EDITOR
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
            }
        }

        if (_ccdLabBuilding == null)
        {
            Debug.LogWarning("[Level9 CCD] Lab QC tidak ditemukan di scene dan FBX gagal load.");
            return;
        }

        // Resolve references dari lab building
        _ccdLabAnalyzerRotor = FindDeepChild(_ccdLabBuilding.transform, "CCDLab_Spectrometer_Rotor")
                            ?? FindDeepChild(_ccdLabBuilding.transform, "Lab_Analyzer_Rotor");
        _ccdLabResultScreen = FindDeepChild(_ccdLabBuilding.transform, "CCDLab_ResultScreen")
                            ?? FindDeepChild(_ccdLabBuilding.transform, "Lab_ResultScreen");
        _ccdLabSlotLiquids[0] = FindDeepChild(_ccdLabBuilding.transform, "CCDLab_InletLiquid_1")
                              ?? FindDeepChild(_ccdLabBuilding.transform, "Lab_SlotLiquid_1");
        _ccdLabSlotLiquids[1] = FindDeepChild(_ccdLabBuilding.transform, "CCDLab_InletLiquid_2")
                              ?? FindDeepChild(_ccdLabBuilding.transform, "Lab_SlotLiquid_2");
        _ccdLabSlotLiquids[2] = FindDeepChild(_ccdLabBuilding.transform, "CCDLab_InletLiquid_3")
                              ?? FindDeepChild(_ccdLabBuilding.transform, "Lab_SlotLiquid_3");
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
        sign.transform.SetParent(_ccdLabBuilding.transform, false);
        sign.transform.localPosition = new Vector3(0, 3.9f, 3.5f);
        var stm = sign.AddComponent<TextMesh>();
        stm.text = "LAB QC PLS";
        stm.fontSize = 60; stm.characterSize = 0.04f; stm.anchor = TextAnchor.MiddleCenter;
        stm.alignment = TextAlignment.Center; stm.color = new Color(0.2f, 0.9f, 1f);
        BuildInteractiveLabStations();
        Debug.Log("[Level9 CCD] Lab QC resolved (scene atau fallback FBX).");
    }

    private void BuildInteractiveLabStations()
    {
        if (_ccdLabBuilding == null)
            return;

        Transform existing = _ccdLabBuilding.transform.Find("L9_LabInteractiveStations_Runtime");
        if (existing != null && existing.childCount > 0)
        {
            CacheExistingLabStations(existing);
            EnsureLabSampleBottleVisuals(existing);
            return;
        }

        GameObject root = existing != null ? existing.gameObject : new GameObject("L9_LabInteractiveStations_Runtime");
        if (existing == null)
        {
            root.transform.SetParent(_ccdLabBuilding.transform, false);
            root.transform.localPosition = new Vector3(0f, 1.15f, -0.65f);
            root.transform.localRotation = Quaternion.identity;
        }

        string[] titles =
        {
            "1 SAMPLE LOGIN",
            "2 FILTER / TSS",
            "3 pH + FREE ACID",
            "4 ICP-OES METALS",
            "5 VALIDASI CCD"
        };
        string[] subtitles =
        {
            "scan seal + ID",
            "0.45 um filter",
            "probe + titrasi",
            "vial ke analyzer",
            "pass/fail window"
        };

        for (int i = 0; i < 5; i++)
        {
            GameObject station = new GameObject("LabStep_" + (i + 1));
            station.transform.SetParent(root.transform, false);
            station.transform.localPosition = new Vector3((i - 2) * 0.72f, 0f, 0f);
            _ccdLabStepStations[i] = station;

            CreateLabCube(station.transform, "BenchPad", new Vector3(0f, -0.04f, 0f), new Vector3(0.56f, 0.08f, 0.46f), new Color(0.12f, 0.16f, 0.18f));

            if (i == 0)
            {
                CreateLabCube(station.transform, "BarcodeScanner", new Vector3(-0.12f, 0.11f, 0.02f), new Vector3(0.18f, 0.08f, 0.25f), new Color(0.04f, 0.05f, 0.06f));
                CreateLabCube(station.transform, "SampleLogbook", new Vector3(0.13f, 0.09f, 0.02f), new Vector3(0.22f, 0.035f, 0.30f), new Color(0.10f, 0.18f, 0.30f));
            }
            else if (i == 1)
            {
                CreateLabCylinder(station.transform, "FilterFunnel", new Vector3(0f, 0.18f, 0f), new Vector3(0.13f, 0.18f, 0.13f), new Color(0.85f, 0.95f, 1f, 0.45f), true);
                CreateLabCylinder(station.transform, "FilterFlask", new Vector3(0f, 0.02f, 0f), new Vector3(0.18f, 0.11f, 0.18f), new Color(0.50f, 0.72f, 0.74f, 0.55f), true);
            }
            else if (i == 2)
            {
                CreateLabCube(station.transform, "PHMeter", new Vector3(-0.12f, 0.12f, 0.03f), new Vector3(0.20f, 0.15f, 0.12f), new Color(0.04f, 0.08f, 0.10f));
                CreateLabCylinder(station.transform, "Burette", new Vector3(0.14f, 0.22f, 0.02f), new Vector3(0.035f, 0.30f, 0.035f), new Color(0.85f, 0.95f, 1f, 0.35f), true);
            }
            else if (i == 3)
            {
                CreateLabCube(station.transform, "ICPTray", new Vector3(-0.02f, 0.07f, 0f), new Vector3(0.36f, 0.08f, 0.24f), new Color(0.10f, 0.12f, 0.16f));
                for (int v = 0; v < 3; v++)
                    CreateLabSampleBottle(station.transform, "Vial_" + v, new Vector3(-0.12f + v * 0.12f, 0.18f, 0.02f), 0.10f, _ccdSampleColors[Mathf.Clamp(v, 0, 2)]);
            }
            else
            {
                CreateLabCube(station.transform, "ValidationConsole", new Vector3(0f, 0.12f, 0f), new Vector3(0.38f, 0.16f, 0.20f), new Color(0.02f, 0.14f, 0.10f));
                CreateLabCube(station.transform, "PassLamp", new Vector3(0f, 0.25f, -0.02f), new Vector3(0.16f, 0.05f, 0.05f), new Color(0.05f, 0.85f, 0.25f));
            }

            GameObject button = CreateLabCylinder(station.transform, "ACTION_BUTTON", new Vector3(0f, 0.13f, -0.28f), new Vector3(0.13f, 0.045f, 0.13f), new Color(0.05f, 0.85f, 0.25f), false);
            _ccdLabStepButtons[i] = button.transform;
            int captured = i;
            WireLabStepButton(button, captured);

            GameObject labelGo = new GameObject("StepLabel");
            labelGo.transform.SetParent(station.transform, false);
            labelGo.transform.localPosition = new Vector3(0f, 0.42f, -0.30f);
            TextMesh tm = labelGo.AddComponent<TextMesh>();
            tm.text = titles[i] + "\n" + subtitles[i];
            tm.fontSize = 34;
            tm.characterSize = 0.010f;
            tm.anchor = TextAnchor.MiddleCenter;
            tm.alignment = TextAlignment.Center;
            tm.color = Color.white;
            _ccdLabStepLabels[i] = labelGo.transform;

            SetLabStepVisual(i, false, false);
        }

        EnsureLabSampleBottleVisuals(root.transform);
    }

    private void CacheExistingLabStations(Transform root)
    {
        for (int i = 0; i < 5; i++)
        {
            Transform station = root.Find("LabStep_" + (i + 1));
            if (station == null && i < root.childCount)
                station = root.GetChild(i);
            if (station == null)
                continue;

            _ccdLabStepStations[i] = station.gameObject;

            Transform button = FindDeepChild(station, "ACTION_BUTTON");
            if (button == null)
                button = FindDeepChild(station, "ActionButton");
            if (button != null)
            {
                _ccdLabStepButtons[i] = button;
                WireLabStepButton(button.gameObject, i);
            }

            Transform label = FindDeepChild(station, "StepLabel");
            if (label == null)
            {
                TextMesh tm = station.GetComponentInChildren<TextMesh>();
                if (tm != null) label = tm.transform;
            }
            _ccdLabStepLabels[i] = label;
            SetLabStepVisual(i, false, false);
        }
    }

    private void EnsureLabSampleBottleVisuals(Transform root)
    {
        if (root == null)
            return;

        Transform rack = root.Find("Runtime_PLS_SampleBottles");
        if (rack != null)
            return;

        GameObject rackGo = new GameObject("Runtime_PLS_SampleBottles");
        rackGo.transform.SetParent(root, false);
        rackGo.transform.localPosition = new Vector3(1.58f, 0.19f, 0.16f);
        rackGo.transform.localRotation = Quaternion.identity;
        for (int i = 0; i < 3; i++)
            CreateLabSampleBottle(rackGo.transform, "PLS_LabBottle_Th" + (i == 0 ? 1 : i == 1 ? 3 : 5), new Vector3(i * 0.16f, 0f, 0f), 0.18f, _ccdSampleColors[i]);
    }

    private GameObject CreateLabSampleBottle(Transform parent, string name, Vector3 localPosition, float height, Color liquidColor)
    {
        GameObject bottle = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        bottle.name = name;
        bottle.transform.SetParent(parent, false);
        bottle.transform.localPosition = localPosition;
        bottle.transform.localScale = new Vector3(height * 0.28f, height, height * 0.28f);
        Renderer br = bottle.GetComponent<Renderer>();
        Material glass = new Material(Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard"));
        ApplyTransparent(glass, new Color(0.78f, 0.90f, 1f, 0.26f));
        br.sharedMaterial = glass;
        var bc = bottle.GetComponent<Collider>(); if (bc != null) Destroy(bc);

        GameObject liquid = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        liquid.name = "Liquid";
        liquid.transform.SetParent(bottle.transform, false);
        liquid.transform.localPosition = new Vector3(0f, -0.18f, 0f);
        liquid.transform.localScale = new Vector3(0.76f, 0.58f, 0.76f);
        Renderer lr = liquid.GetComponent<Renderer>();
        Material lm = new Material(Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard"));
        ApplyTransparent(lm, new Color(liquidColor.r, liquidColor.g, liquidColor.b, 0.72f));
        lr.sharedMaterial = lm;
        var lc = liquid.GetComponent<Collider>(); if (lc != null) Destroy(lc);
        return bottle;
    }

    private GameObject CreateLabCube(Transform parent, string name, Vector3 localPosition, Vector3 localScale, Color color)
    {
        GameObject go = GameObject.CreatePrimitive(PrimitiveType.Cube);
        go.name = name;
        go.transform.SetParent(parent, false);
        go.transform.localPosition = localPosition;
        go.transform.localScale = localScale;
        ApplySimpleMat(go.GetComponent<Renderer>(), color);
        var col = go.GetComponent<Collider>();
        if (col != null) Destroy(col);
        return go;
    }

    private GameObject CreateLabCylinder(Transform parent, string name, Vector3 localPosition, Vector3 localScale, Color color, bool transparent)
    {
        GameObject go = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        go.name = name;
        go.transform.SetParent(parent, false);
        go.transform.localPosition = localPosition;
        go.transform.localScale = localScale;
        Renderer r = go.GetComponent<Renderer>();
        if (transparent)
        {
            Material m = new Material(Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard"));
            ApplyTransparent(m, color);
            r.sharedMaterial = m;
        }
        else
        {
            ApplySimpleMat(r, color);
        }
        return go;
    }

    private void WireLabStepButton(GameObject button, int stepIndex)
    {
        if (button == null) return;
        var col = button.GetComponent<Collider>();
        if (col == null) col = button.AddComponent<SphereCollider>();
        col.isTrigger = false;

        var si = button.GetComponent<UnityEngine.XR.Interaction.Toolkit.Interactables.XRSimpleInteractable>();
        if (si == null) si = button.AddComponent<UnityEngine.XR.Interaction.Toolkit.Interactables.XRSimpleInteractable>();
        si.colliders.Clear();
        foreach (var c in button.GetComponents<Collider>())
            if (c != null) si.colliders.Add(c);
        si.selectEntered.RemoveAllListeners();
        si.selectEntered.AddListener(_ => ConfirmLabStep(stepIndex));
    }

    private void ConfirmLabStep(int stepIndex)
    {
        if (!_ccdLabSequenceStarted || stepIndex != _ccdLabActiveStep || _ccdLabStepDone[stepIndex])
            return;

        _ccdLabStepConfirmed = true;
        if (_hud != null)
            _hud.ShowNotifPublic("Tahap lab dikonfirmasi. Analisa berjalan...", 3f);
    }

    private void SetLabStepVisual(int stepIndex, bool active, bool done)
    {
        if (stepIndex < 0 || stepIndex >= _ccdLabStepStations.Length)
            return;

        Color pad = done ? new Color(0.05f, 0.35f, 0.12f) : active ? new Color(0.30f, 0.24f, 0.02f) : new Color(0.12f, 0.16f, 0.18f);
        Transform station = _ccdLabStepStations[stepIndex] != null ? _ccdLabStepStations[stepIndex].transform : null;
        Transform bench = station != null ? station.Find("BenchPad") : null;
        if (bench != null && bench.TryGetComponent(out Renderer br))
            ApplySimpleMat(br, pad);

        Transform button = _ccdLabStepButtons[stepIndex];
        if (button != null && button.TryGetComponent(out Renderer rr))
            ApplySimpleMat(rr, done ? new Color(0.08f, 0.45f, 0.15f) : active ? new Color(1f, 0.86f, 0.1f) : new Color(0.05f, 0.85f, 0.25f));

        if (_ccdLabStepLabels[stepIndex] != null)
        {
            TextMesh tm = _ccdLabStepLabels[stepIndex].GetComponent<TextMesh>();
            if (tm != null)
                tm.color = done ? new Color(0.55f, 1f, 0.55f) : active ? new Color(1f, 0.92f, 0.35f) : Color.white;
        }
    }

    private void SubmitPLSToLab()
    {
        if (_ccdLabSubmitted || _ccdLabSequenceStarted) return;
        _ccdLabSequenceStarted = true;
        StartCoroutine(ImmersiveNickelPlsLabCoroutine());
    }

    private IEnumerator ImmersiveNickelPlsLabCoroutine()
    {
        if (_hud != null)
            _hud.ShowNotifPublic("Lab menerima 3 botol PLS. Mulai chain-of-custody, filtrasi, titrasi, dan ICP-OES.", 7f);

        string[] steps =
        {
            "01 SAMPLE LOGIN\nLabel Th-1/Th-3/Th-5, seal, volume, waktu sampling OK",
            "02 FILTRASI / TSS\nFilter 0.45 um + gravimetri padatan tersuspensi",
            "03 pH + FREE ACID\npH meter terkalibrasi, titrasi NaOH untuk H2SO4 bebas",
            "04 ICP-OES METALS\nDilusi asam, baca Ni/Co/Fe/Al/Mg/Mn",
            "05 VALIDASI CCD\nNi-Co sesuai target, TSS rendah, acid/impurities siap route neutralisasi-MHP"
        };

        string[] prompts =
        {
            "Scan label sample dan cek chain-of-custody.",
            "Pasang filter 0.45 um untuk TSS/clarity.",
            "Celup probe pH dan mulai titrasi free acid.",
            "Masukkan vial ke ICP-OES untuk metals assay.",
            "Bandingkan hasil dengan window proses CCD."
        };

        for (int i = 0; i < 3; i++)
        {
            if (_ccdLabSlotLiquids[i] == null) continue;
            Vector3 baseScale = _ccdLabSlotLiquids[i].localScale;
            Vector3 basePos = _ccdLabSlotLiquids[i].localPosition;
            float fullY = Mathf.Abs(_ccdLabSlotBaseY[i]) > 0.0001f ? _ccdLabSlotBaseY[i] : 1.7f;
            float elapsed = 0f;
            if (_ccdLabScreenText != null)
                _ccdLabScreenText.text = $"LOAD SAMPLE Th-{(i == 0 ? 1 : i == 1 ? 3 : 5)}\nBottle ID verified";
            while (elapsed < 0.65f)
            {
                elapsed += Time.deltaTime;
                float p = Mathf.Clamp01(elapsed / 0.65f);
                float h = Mathf.Lerp(fullY * 0.02f, fullY, p);
                _ccdLabSlotLiquids[i].localScale = new Vector3(baseScale.x, h, baseScale.z);
                _ccdLabSlotLiquids[i].localPosition = basePos + new Vector3(0f, (h - fullY * 0.02f) * 0.5f, 0f);
                yield return null;
            }
        }

        for (int step = 0; step < steps.Length; step++)
        {
            yield return WaitForLabStepInput(step, prompts[step]);
            float duration = step == 3 ? 2.0f : 1.35f;
            float elapsed = 0f;
            while (elapsed < duration)
            {
                elapsed += Time.deltaTime;
                if (_ccdLabAnalyzerRotor != null)
                    _ccdLabAnalyzerRotor.Rotate(Vector3.up, (step == 3 ? 520f : 260f) * Time.deltaTime, Space.Self);
                if (_ccdLabScreenText != null)
                {
                    int pct = Mathf.RoundToInt(Mathf.Clamp01(elapsed / duration) * 100f);
                    int bars = Mathf.RoundToInt(pct / 10f);
                    _ccdLabScreenText.text = steps[step] + "\n[" + new string('#', bars) + new string('-', 10 - bars) + "] " + pct + "%";
                }
                yield return null;
            }

            _ccdLabStepDone[step] = true;
            SetLabStepVisual(step, false, true);
        }

        _ccdLabActiveStep = -1;
        _ccdLabSubmitted = true;
        if (_ccdLabScreenText != null)
            _ccdLabScreenText.text = "QC SELESAI\nCCD OVERFLOW PASS\nNi 5.1 g/L | Co 0.52 g/L\nTSS 180 mg/L | Free acid 22 g/L";

        ShowImmersiveL10LabResultCanvas();
        if (_hud != null)
            _hud.ShowNotifPublic("Hasil QC keluar: PLS memenuhi window proses. Klik ACCEPT atau tekan Enter untuk lanjut.", 8f);
    }

    private IEnumerator WaitForLabStepInput(int stepIndex, string prompt)
    {
        _ccdLabActiveStep = stepIndex;
        _ccdLabStepConfirmed = false;
        for (int i = 0; i < _ccdLabStepStations.Length; i++)
            SetLabStepVisual(i, i == stepIndex, _ccdLabStepDone[i]);

        if (_ccdLabScreenText != null)
            _ccdLabScreenText.text = prompt + "\nTekan tombol kuning alat lab";
        if (_hud != null)
            _hud.ShowNotifPublic(prompt + " Tekan tombol kuning pada station lab yang ditandai.", 7f);

        yield return null;
        while (!_ccdLabStepConfirmed)
        {
            if (Input.GetKeyDown(KeyCode.G) || Input.GetKeyDown(KeyCode.Y) || Input.GetKeyDown(KeyCode.L))
                ConfirmLabStep(stepIndex);

            if (_ccdLabAnalyzerRotor != null)
                _ccdLabAnalyzerRotor.Rotate(Vector3.up, 35f * Time.deltaTime, Space.Self);
            yield return null;
        }
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

    private void ShowImmersiveL10LabResultCanvas()
    {
        if (_ccdLabQcCanvas != null)
        {
            _ccdLabQcCanvas.SetActive(true);
            _labResultCanvasFollowPlayer = true;
            PositionLabResultCanvas(_ccdLabQcCanvas.transform);
            return;
        }

        var canvasGO = new GameObject("L9_LabQC_Canvas_Immersive");
        canvasGO.transform.SetParent(transform, false);
        var canvas = canvasGO.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.WorldSpace;
        canvasGO.AddComponent<UnityEngine.UI.CanvasScaler>();
        canvasGO.AddComponent<UnityEngine.UI.GraphicRaycaster>();

        canvasGO.transform.localScale = Vector3.one * 0.85f;
        var rect = canvasGO.GetComponent<RectTransform>();
        rect.sizeDelta = new Vector2(2.15f, 1.55f);
        _labResultCanvasFollowPlayer = true;
        PositionLabResultCanvas(canvasGO.transform);

        AddPanel(canvasGO.transform, "BG", new Color(0.035f, 0.07f, 0.12f, 0.98f), Vector2.zero, Vector2.one);
        AddPanel(canvasGO.transform, "TitleBar", new Color(0.08f, 0.28f, 0.42f, 1f), new Vector2(0f, 0.86f), new Vector2(1f, 1f));
        AddText(canvasGO.transform, "Title", "LAB QC - CCD OVERFLOW PLS",
            new Color(0.7f, 1f, 0.85f), 30, FontStyle.Bold, TextAnchor.MiddleCenter,
            new Vector2(0f, 0.86f), new Vector2(1f, 1f));

        string[] rows =
        {
            "Chain-of-custody: 3 bottle sealed | ID Th-1/Th-3/Th-5 | volume OK",
            "ICP-OES metals: Ni 5.1 g/L | Co 0.52 g/L | Fe 3.2 | Al 1.4 | Mg 16.8",
            "Wet chemistry: pH 1.35 | free H2SO4 22 g/L | ORP stable",
            "Solids/clarity: TSS 180 mg/L | turbidity low | no coarse carryover",
            "CCD validation: soluble Ni/Co recovery 96% | overflow ready to purification/MHP route"
        };
        for (int i = 0; i < rows.Length; i++)
        {
            float yMin = 0.61f - i * 0.10f;
            AddText(canvasGO.transform, "QCRow" + i, rows[i], Color.white, 16, FontStyle.Normal,
                TextAnchor.MiddleLeft, new Vector2(0.05f, yMin), new Vector2(0.95f, yMin + 0.095f));
        }

        AddText(canvasGO.transform, "Verdict",
            "VERDICT: PASS. Training window: Ni 3-6 g/L, Co 0.2-0.8 g/L, free acid 10-60 g/L, TSS <500 mg/L, recovery >=95%.",
            new Color(0.6f, 1f, 0.7f), 17, FontStyle.Italic, TextAnchor.MiddleCenter,
            new Vector2(0.05f, 0.13f), new Vector2(0.95f, 0.30f));
        AddButton(canvasGO.transform, "ACCEPT & LANJUT",
            new Vector2(0.3f, 0.04f), new Vector2(0.7f, 0.13f),
            new Color(0.2f, 0.6f, 0.3f), () => OnL10LabAccepted());

        _ccdLabQcCanvas = canvasGO;
    }

    private void ShowL10LabResultCanvas()
    {
        if (_ccdLabQcCanvas != null)
        {
            _ccdLabQcCanvas.SetActive(true);
            _labResultCanvasFollowPlayer = true;
            PositionLabResultCanvas(_ccdLabQcCanvas.transform);
            return;
        }
        var canvasGO = new GameObject("L9_LabQC_Canvas");
        canvasGO.transform.SetParent(transform, false);
        var canvas = canvasGO.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.WorldSpace;
        canvasGO.AddComponent<UnityEngine.UI.CanvasScaler>();
        canvasGO.AddComponent<UnityEngine.UI.GraphicRaycaster>();
        var rect = canvasGO.GetComponent<RectTransform>();
        rect.sizeDelta = new Vector2(2.0f, 1.4f);
        canvasGO.transform.localScale = Vector3.one * 0.85f;
        _labResultCanvasFollowPlayer = true;
        PositionLabResultCanvas(canvasGO.transform);

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
        _labResultCanvasFollowPlayer = false;
        GameLevelManager.Instance?.NotifyLevel10SamplePLSAccepted();
        if (_hud != null) _hud.ShowNotifPublic("Lab QC PLS lulus. Lapor HT (tahan T): 'CCD aktif, PLS lulus QC'.", 8f);
    }

    private void FollowLabResultCanvas()
    {
        if (!_labResultCanvasFollowPlayer || _ccdLabQcCanvas == null || !_ccdLabQcCanvas.activeInHierarchy)
            return;

        PositionLabResultCanvas(_ccdLabQcCanvas.transform);
    }

    private void PositionLabResultCanvas(Transform canvasTransform)
    {
        if (canvasTransform == null) return;

        Vector3 head = GetPlayerHead();
        Vector3 fwd = GetPlayerForward();
        fwd.y = 0f;
        if (fwd.sqrMagnitude < 0.001f) fwd = Vector3.forward;
        fwd.Normalize();

        Vector3 right = Vector3.Cross(Vector3.up, fwd);
        if (right.sqrMagnitude < 0.001f) right = Vector3.right;
        right.Normalize();

        // DI DEPAN player (bukan menyamping) supaya jelas terbaca + tombol ACCEPT mudah diklik.
        Vector3 pos = head + fwd * 1.7f + Vector3.down * 0.05f;
        canvasTransform.position = pos;

        Vector3 face = pos - head;
        face.y = 0f;
        if (face.sqrMagnitude < 0.001f) face = fwd;
        canvasTransform.rotation = Quaternion.LookRotation(face.normalized, Vector3.up);
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
    private bool _labResultCanvasFollowPlayer;

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
