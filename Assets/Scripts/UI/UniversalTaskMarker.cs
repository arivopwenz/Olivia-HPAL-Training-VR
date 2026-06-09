using UnityEngine;

/// <summary>
/// OLIVIA VR - UniversalTaskMarker.cs
///
/// Sistem universal yang menampilkan PANAH BESAR 3D + OUTLINE DINAMIS di object target aktif
/// untuk SETIAP level dan task. Bekerja otomatis berdasarkan state GameLevelManager.
///
/// Fitur:
///   - Panah 3D besar berputar di atas target object
///   - Outline wireframe dinamis: box / sphere-rings / capsule-rings sesuai bentuk target
///   - Auto-resolve target berdasarkan level + phase
///   - Pulse animation (scale + color)
///   - Auto-hide saat task selesai, lalu pindah ke target berikutnya
///
/// Pemakaian:
///   Taruh di scene (1 instance). Semua otomatis.
/// </summary>
[DisallowMultipleComponent]
public sealed class UniversalTaskMarker : MonoBehaviour
{
    public static UniversalTaskMarker Instance { get; private set; }

    [Header("=== Arrow Visual ===")]
    [SerializeField] private float arrowScale = 0.45f;
    [SerializeField] private float arrowBobSpeed = 2.5f;
    [SerializeField] private float arrowBobHeight = 0.15f;
    [SerializeField] private float arrowSpinSpeed = 90f;
    [SerializeField] private float arrowHeightAboveTarget = 0.7f;
    [SerializeField] private Color arrowColor = new Color(1f, 0.85f, 0.1f, 1f);
    [SerializeField] private float arrowEmissionIntensity = 4f;

    [Header("=== Outline ===")]
    [SerializeField] private Color outlineColor = new Color(1f, 0.85f, 0.1f, 0.9f);
    [SerializeField] private float outlineWidth = 0.012f;
    [SerializeField] private float outlinePadding = 0.025f;
    [SerializeField] private float outlinePulseSpeed = 2.2f;

    [Header("=== Polling ===")]
    [SerializeField] private float updateInterval = 0.2f;
    [SerializeField] private float minimumBoundsSize = 0.18f;
    [SerializeField] private float ccdCloseHideDistance = 0.35f;

    private GameObject _arrowRoot;
    private Material _arrowMat;
    private readonly LineRenderer[] _outlineEdges = new LineRenderer[12];
    private readonly LineRenderer[] _roundedLines = new LineRenderer[7];
    private Material _lineMat;
    private Transform _currentTarget;
    private float _nextUpdate;
    private GameLevelManager _glm;
    private enum MarkerShape { Box, Sphere, Capsule }
    private struct MarkerShapeInfo
    {
        public Bounds bounds;
        public MarkerShape shape;
    }

    private void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
        arrowScale = Mathf.Max(arrowScale, 0.35f);
        arrowHeightAboveTarget = Mathf.Max(arrowHeightAboveTarget, 0.45f);
        outlineWidth = Mathf.Max(outlineWidth, 0.012f);
        outlinePadding = Mathf.Clamp(outlinePadding, 0.01f, 0.08f);
        CreateArrow();
        SetVisible(false);
    }

    private void OnEnable()
    {
        GameLevelManager.OnLevelStarted += OnLevelStarted;
        GameLevelManager.OnLevelComplete += OnLevelComplete;
        GameLevelManager.OnDCSButtonPressed += OnDcsPressed;
        GameLevelManager.OnVoiceReportAccepted += OnVoiceReport;
    }

    private void OnDisable()
    {
        GameLevelManager.OnLevelStarted -= OnLevelStarted;
        GameLevelManager.OnLevelComplete -= OnLevelComplete;
        GameLevelManager.OnDCSButtonPressed -= OnDcsPressed;
        GameLevelManager.OnVoiceReportAccepted -= OnVoiceReport;
    }

    private void OnLevelStarted(GameLevelManager.GameLevel l) { _currentTarget = null; _nextUpdate = Time.time + 0.6f; }
    private void OnLevelComplete(GameLevelManager.GameLevel l, int s) { SetVisible(false); _currentTarget = null; }
    private void OnDcsPressed(int n) { _nextUpdate = Time.time; }
    private void OnVoiceReport(string k) { _nextUpdate = Time.time; }

    private void Update()
    {
        if (Time.time < _nextUpdate) return;
        _nextUpdate = Time.time + updateInterval;

        if (_glm == null) _glm = GameLevelManager.Instance;
        if (_glm == null) { SetVisible(false); return; }

        Transform target = ResolveTarget();
        if (target != _currentTarget)
        {
            _currentTarget = target;
            if (target == null) SetVisible(false);
        }
    }

    private void LateUpdate()
    {
        if (_currentTarget == null || !_currentTarget.gameObject.activeInHierarchy)
        {
            SetVisible(false);
            return;
        }

        // Jangan tampilkan marker kalau target adalah child dari player rig (walkie di tangan, dll)
        if (IsChildOfPlayer(_currentTarget))
        {
            SetVisible(false);
            return;
        }

        // Auto-hide kalau player sudah dekat target (< 2m)
        Transform cam = Camera.main != null ? Camera.main.transform : null;
        if (cam != null)
        {
            float dist = Vector3.Distance(cam.position, _currentTarget.position);
            float hideDistance = IsCcdLevel() ? ccdCloseHideDistance : 2.0f;
            if (dist < hideDistance)
            {
                SetVisible(false);
                return;
            }
        }

        SetVisible(true);
        Bounds bounds = CalculateBounds(_currentTarget);

        // Arrow position + animation
        float bob = Mathf.Sin(Time.time * arrowBobSpeed) * arrowBobHeight;
        Vector3 arrowPos = bounds.center + Vector3.up * (bounds.extents.y + arrowHeightAboveTarget + bob);
        _arrowRoot.transform.position = arrowPos;
        _arrowRoot.transform.Rotate(Vector3.up, arrowSpinSpeed * Time.deltaTime, Space.World);

        float pulse = 0.85f + 0.15f * Mathf.Sin(Time.time * outlinePulseSpeed);
        _arrowRoot.transform.localScale = Vector3.one * arrowScale * pulse;

        DisableOutline();
    }

    /// <summary>
    /// Bisa dipanggil dari controller manapun untuk override target secara manual.
    /// </summary>
    public void SetTarget(Transform target)
    {
        _currentTarget = target;
    }

    public void ClearTarget()
    {
        _currentTarget = null;
        SetVisible(false);
    }

    // ============================================================
    //  TARGET RESOLUTION PER LEVEL
    // ============================================================

    private Transform ResolveTarget()
    {
        var level = _glm.CurrentLevel;

        switch (level)
        {
            case GameLevelManager.GameLevel.Level0_Tutorial:
                return null; // Tutorial punya mekanisme sendiri

            case GameLevelManager.GameLevel.Level1_APD:
                return ResolveLevel1Target(); // UniversalTaskMarker handle Level 1 juga

            case GameLevelManager.GameLevel.Level2_DCSPrep:
                if (!_glm.SudahLihatDcs) return FindByName("DCS_Monitor", "Monitor_DCS", "DCSPanel");
                if (!_glm.SudahTekanTombolDcs) return FindDcsButton(_glm.NomorTombolDcsLevelIni);
                if (!_glm.SudahLaporanHt) return FindWalkieTalkie();
                return null;

            case GameLevelManager.GameLevel.Level3_OreSlurry:
                if (!_glm.SudahTekanTombolDcs) return FindDcsButton(3);
                if (!_glm.SudahLaporanHt) return FindWalkieTalkie();
                return null; // panah/marker Level 3 dimatikan (ganggu observasi)

            case GameLevelManager.GameLevel.Level4_SlurryPump:
                return ResolveLevel4Target();

            case GameLevelManager.GameLevel.Level5_SteamValve:
                return ResolveLevel5Target();

            case GameLevelManager.GameLevel.Level6_AcidInjection:
                return ResolveLevel6Target();

            case GameLevelManager.GameLevel.Level7_Autoclave:
                return ResolveLevel7Target();

            case GameLevelManager.GameLevel.Level8_Monitoring:
                return ResolveLevel8Target();

            case GameLevelManager.GameLevel.Level9_FlashVessel:
                // DIPENSIUNKAN: digabung ke Level 8. Tidak ada target.
                return null;

            case GameLevelManager.GameLevel.Level10_CCD:
                return ResolveLevel10CcdTarget();

            case GameLevelManager.GameLevel.Level11_MHP:
                if (!_glm.SudahTekanTombolDcs) return FindDcsButton(10);
                if (!_glm.SudahLaporanHt) return FindWalkieTalkie();
                return FindByName("Level11_PurificationMHP_BlenderRig", "MHP_SampleBottle");

            case GameLevelManager.GameLevel.Level12_TailingDischarge:
                return ResolveLevel12TailingTarget();

            case GameLevelManager.GameLevel.Level13_TailingWaste:
                if (!_glm.SudahTekanTombolDcs) return FindDcsButton(12);
                if (!_glm.SudahLaporanHt) return FindWalkieTalkie();
                return FindByName("Level13_DryStack_BlenderRig", "Level13_DryStack_StorageArea_BlenderRig");

            case GameLevelManager.GameLevel.Level14_Emergency:
                return FindByName("ESD_Button", "Btn_ESD", "ESD_Panel");

            default:
                return null;
        }
    }

    // ============================================================
    //  LEVEL-SPECIFIC TARGET RESOLVERS
    // ============================================================

    private Transform ResolveLevel1Target()
    {
        PhaseManager pm = PhaseManager.Instance;
        if (pm == null) return null;

        if (!pm.isHelmetWorn) return FindByName("Helmet", "Socket_Scanner_Hat");
        if (!pm.isVestWorn) return FindByName("Vest", "Socket_Scanner_Rompi");
        if (!pm.isGlassesWorn) return FindByName("Glassess", "Glasses", "Socket_Scanner_Glassess");
        if (!pm.isBootsWorn) return FindByName("Boots", "Socket_Scanner_Boots");
        if (!pm.isGlovesWorn) return FindByName("Gloves", "Socket_Scanner_Gloves");
        if (!pm.isRespiratorWorn) return FindByName("RespiratorMask", "Socket_Scanner_RespiratorMask");
        if (!pm.isEarplugWorn) return FindByName("EarPlug", "Socket_Scanner_EarPlug");
        if (!pm.isWalkieTalkieTaken) return FindByName("Walkie Talkie", "Socket_Scanner_WalkieTalkie");
        return null; // semua APD lengkap
    }

    private Transform ResolveLevel4Target()
    {
        // Marker per task Level 4 (Slurry Pump), digerakkan oleh fase.
        switch (_glm.CurrentLevel4Phase)
        {
            case GameLevelManager.Level4Phase.Idle:
            case GameLevelManager.Level4Phase.MenungguTombolDcs:
                // Task 1: klik tombol DCS 4
                return FindDcsButton(4);

            case GameLevelManager.Level4Phase.AturFlowRate:
                // Task 2: atur flow rate 450 m3/h -> arahkan ke tombol [+] flow di meja DCS
                return FindByName("Btn_FlowPlus", "Widget_FlowRate", "A_PARAM_Flow_PLUS");

            case GameLevelManager.Level4Phase.MenungguLaporanFlow:
                // Task 3: lapor HT awal "slurry pump aktif"
                return FindWalkieTalkie();

            case GameLevelManager.Level4Phase.ObservasiPump:
                // Observasi: arahkan ke slurry pump
                return FindByName("SlurryPump_Field", "PumpMotor_Audio", "SlurryPump");

            case GameLevelManager.Level4Phase.ObservasiPreheater:
                // Task 4: slurry mencapai Pre-Heater -> arahkan ke pre-heater (instance dekat pump, z~56)
                return FindByName(
                    "Level5_PreHeater_Blender_Industrial_UV_Overview (1)",
                    "Level5_PreHeater_Blender_Industrial_UV_Overview",
                    "PreHeater_Field_1", "PreHeater_Field");

            case GameLevelManager.Level4Phase.MenungguLaporanAkhir:
                // Task 5: lapor HT akhir "cairan sudah di preheater"
                return FindWalkieTalkie();

            default:
                // KembaliKeDcs / Selesai: tidak ada marker
                return null;
        }
    }

    private Transform ResolveLevel5Target()
    {
        if (!_glm.SudahTekanTombolDcs) return FindDcsButton(5);
        bool preheaterReady = _glm.Level5PreheaterReady;
        if (!preheaterReady)
            return FindByName("RealSteamValve_Pivot_Lvl5", "SteamValve_Handwheel", "LetdownValve_Handwheel", "Level5_PreHeater_Blender_Industrial_UV_Overview");
        if (!_glm.SudahLaporanHt) return FindWalkieTalkie();
        return null;
    }

    private Transform ResolveLevel6Target()
    {
        if (!_glm.SudahTekanTombolDcs) return FindDcsButton(6);
        if (!_glm.Level6OutletReportDone) return FindWalkieTalkie();
        // Setelah lapor outlet & sampai di lapangan: WAJIB pakai masker dulu sebelum operasi valve.
        if (PhaseManager.Instance != null && !PhaseManager.Instance.isRespiratorWorn)
            return FindByName("RespiratorMask", "Socket_Respirator_Baju", "Socket_Scanner_RespiratorMask");
        if (!_glm.Level6SlurryMasukAutoclave)
            return FindByName("L6_SlurryValve_Pivot_Runtime", "L5_Condensate_Drain_Handwheel_Hub", "L6_SlurryRoute_ValveWheel_Runtime");
        if (!_glm.Level6SlurryReportDone) return FindWalkieTalkie();
        if (!_glm.Level6DcsAcidReady)
        {
            if (!_glm.Level6AcidDoseReady)
                return FindByName("PS_AcidRatio_pr", "Btn_AcidPlus", "A_PARAM_AcidRatio_PLUS");
            if (!_glm.Level6PumpStrokeReady)
                return FindByName("PS_AcidStroke_pr", "Btn_AcidStrokePlus", "A_PARAM_AcidStroke_PLUS");
            if (!_glm.Level6PhLeachReady)
                return FindByName("PS_pH_mr", "Btn_pH_Minus", "A_PARAM_pH_MINUS");
        }
        if (!_glm.Level6AcidComplete)
            return FindByName("L6_AcidSkid_BtnLocalStart_Runtime", "L6_AcidSkid_BtnLeakOk_Runtime", "Transparent_CalibrationColumn");
        if (!_glm.SudahLaporanHt) return FindWalkieTalkie();
        return null;
    }

    private Transform ResolveLevel7Target()
    {
        if (!_glm.SudahTekanTombolDcs) return FindDcsButton(7);
        // Step 1: Player belum buka valve → arahkan ke handwheel inlet
        if (!_glm.Level7GaugesLogged) // GaugesLogged = slurry fill 100%
        {
            return FindByName(
                "L7_L5_Condensate_Drain_Handwheel_StirRedesign_Scene",
                "L7_L5_Condensate_Drain_Handwheel_StirRedesign_Runtime",
                "L7_InletValve_Pivot_Runtime",
                "L7_LiquidUnderflow_Handwheel_Hub");
        }
        // Step 2: Setelah valve dibuka & cairan penuh → arahkan ke autoclave shell (X-Ray target)
        if (!_glm.Level7XrayActivated)
        {
            return FindByName("Autoclave_Field", "L7_XRay_InnerSlurry_Surface");
        }
        // Step 3: X-Ray sudah aktif → safety drill (PSV target)
        if (!_glm.Level7SafetyDrillDone)
        {
            return FindByName("L7_PSV", "Autoclave_PSV", "PSV_Marker", "Autoclave_Field");
        }
        // Step 4: Inspeksi selesai → lapor HT
        if (!_glm.SudahLaporanHt) return FindWalkieTalkie();
        return null;
    }
    private Transform ResolveLevel8Target()
    {
        if (!_glm.SudahTekanTombolDcs) return FindDcsButton(8);

        var l8 = FindFirstObjectByType<Level8FlashTrainController>(FindObjectsInactive.Exclude);
        if (l8 != null && l8.LevelActive)
        {
            if (!l8.Fv1Stable)
                return FindByName("FV1_To_FV2_InterstageLetdownValve_BypassHandwheel");
            if (!l8.Fv2Stable)
                return FindByName("FV2_To_FV3_InterstageLetdownValve_BypassHandwheel");
            if (!l8.Fv3Stable)
                return FindByName("FV3_SteamValve_Handwheel");
            if (!l8.IsCompleted && !_glm.SudahLaporanHt)
                return FindWalkieTalkie();
            return null;
        }

        if (!_glm.SudahLaporanHt) return FindWalkieTalkie();
        return null;
    }

    private Transform ResolveLevel10CcdTarget()
    {
        if (!_glm.SudahTekanTombolDcs) return FindDcsButton(9);

        Level10CCDController ccd = FindFirstObjectByType<Level10CCDController>(FindObjectsInactive.Include);
        if (!_glm.Level10CCDComplete)
            return ccd != null ? ccd.GetCurrentTaskMarkerTarget() : FindByName("CCD_BlenderRig", "CCD_Field");

        if (!_glm.Level10SamplePLSAccepted)
            return ccd != null ? ccd.GetCurrentTaskMarkerTarget() : FindByName("L9_PLS_SampleStation_Th1", "L9_LabBuilding", "CCD_Field");

        if (!_glm.SudahLaporanHt) return FindWalkieTalkie();
        return null;
    }

    private Transform ResolveLevel12TailingTarget()
    {
        // Task 1: tekan DCS 11
        if (!_glm.SudahTekanTombolDcs) return FindDcsButton(11);

        var tail = FindFirstObjectByType<Level12TailingFilterController>(FindObjectsInactive.Include);
        if (tail == null)
        {
            if (!_glm.SudahLaporanHt) return FindWalkieTalkie();
            return null;
        }

        // HT-gate: await 1 (alirkan tailing), 2 (dosing kapur), 3 (filter press) -> semua lapor HT (tahan T)
        if (tail.AwaitStage == 1 || tail.AwaitStage == 2 || tail.AwaitStage == 3)
            return FindWalkieTalkie();

        // stage 2 = inspeksi cake (jalan ke konveyor cake)
        if (tail.StageNow == 2 && !tail.Inspected)
            return FindByName("Cake_On_Conveyor", "Cake_Block_00", "Cake_Transfer_Conveyor", "Final_FilterPress_Unit");

        // stage 3 = Compliance QC pop-up (tombol ACCEPT di canvas, marker tak perlu)
        if (tail.StageNow == 3 && !tail.ComplianceAccepted)
            return null;

        // task akhir: lapor HT "limbah dialirkan"
        if (!_glm.SudahLaporanHt) return FindWalkieTalkie();
        return null;
    }

    

    // ============================================================
    //  HELPERS
    // ============================================================

    private Transform FindDcsButton(int nomor)
    {
        if (nomor <= 0) return null;
        foreach (var tombol in FindObjectsByType<DCSTombolPanel>(FindObjectsInactive.Include, FindObjectsSortMode.None))
        {
            if (tombol != null && tombol.NomorTombol == nomor)
                return tombol.transform;
        }
        return FindByName("Tombol_" + nomor, "DCS_Button_" + nomor);
    }

    private Transform FindWalkieTalkie()
    {
        // Cari walkie talkie yang BUKAN child dari player rig (yang di meja/rak, bukan di tangan)
        WalkieTalkieManager wtm = FindFirstObjectByType<WalkieTalkieManager>(FindObjectsInactive.Include);
        if (wtm != null)
        {
            Transform t = wtm.WalkieTalkieInHandTransform;
            if (t != null && t.gameObject.activeInHierarchy && !IsChildOfPlayer(t)) return t;
        }
        Transform found = FindByName("WalkieTalkie", "Walkie_Talkie", "WT_Body");
        if (found != null && !IsChildOfPlayer(found)) return found;
        return null;
    }

    private Transform FindByName(params string[] names)
    {
        // First try active GameObject.Find
        foreach (string name in names)
        {
            GameObject go = GameObject.Find(name);
            if (go != null) return go.transform;
        }
        // Deep search including inactive
        Transform[] all = FindObjectsByType<Transform>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        foreach (string name in names)
        {
            foreach (Transform t in all)
            {
                if (t != null && t.name == name && t.gameObject.scene.IsValid())
                {
                    // Walk up to find first active ancestor; if anywhere inactive, skip
                    if (!t.gameObject.activeInHierarchy)
                    {
                        // Skip; can't display marker on inactive object
                        continue;
                    }
                    return t;
                }
            }
        }
        return null;
    }

    private bool IsChildOfPlayer(Transform target)
    {
        if (target == null) return false;
        Transform t = target;
        while (t != null)
        {
            string n = t.name;
            if (n.Contains("XR Origin") || n.Contains("XR Rig") || n.Contains("PlayerRig") || t.CompareTag("Player"))
                return true;
            t = t.parent;
        }
        return false;
    }

    // ============================================================
    //  VISUALS
    // ============================================================

    private void CreateArrow()
    {
        _arrowRoot = new GameObject("UniversalTaskMarker_Arrow");
        _arrowRoot.transform.SetParent(transform, false);

        Shader shader = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard");
        _arrowMat = new Material(shader);
        _arrowMat.color = arrowColor;
        _arrowMat.EnableKeyword("_EMISSION");
        if (_arrowMat.HasProperty("_EmissionColor"))
            _arrowMat.SetColor("_EmissionColor", arrowColor * arrowEmissionIntensity);
        if (_arrowMat.HasProperty("_BaseColor"))
            _arrowMat.SetColor("_BaseColor", arrowColor);

        // Arrow body (cylinder pointing down)
        GameObject body = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        body.name = "Arrow_Body";
        body.transform.SetParent(_arrowRoot.transform, false);
        body.transform.localPosition = new Vector3(0f, 0.25f, 0f);
        body.transform.localScale = new Vector3(0.12f, 0.25f, 0.12f);
        Object.Destroy(body.GetComponent<Collider>());
        body.GetComponent<Renderer>().sharedMaterial = _arrowMat;
        body.GetComponent<Renderer>().shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;

        // Arrow head (cone = scaled sphere squished, or use cube rotated)
        GameObject head = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        head.name = "Arrow_Head";
        head.transform.SetParent(_arrowRoot.transform, false);
        head.transform.localPosition = new Vector3(0f, -0.05f, 0f);
        head.transform.localScale = new Vector3(0.4f, 0.22f, 0.4f);
        Object.Destroy(head.GetComponent<Collider>());
        head.GetComponent<Renderer>().sharedMaterial = _arrowMat;
        head.GetComponent<Renderer>().shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;

        // Tip
        GameObject tip = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        tip.name = "Arrow_Tip";
        tip.transform.SetParent(_arrowRoot.transform, false);
        tip.transform.localPosition = new Vector3(0f, -0.22f, 0f);
        tip.transform.localScale = new Vector3(0.15f, 0.25f, 0.15f);
        Object.Destroy(tip.GetComponent<Collider>());
        tip.GetComponent<Renderer>().sharedMaterial = _arrowMat;
        tip.GetComponent<Renderer>().shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;

        _arrowRoot.SetActive(false);
    }

    private void CreateOutline()
    {
        _lineMat = new Material(Shader.Find("Sprites/Default") ?? Shader.Find("UI/Default"));
        _lineMat.name = "M_UniversalTaskMarker_Outline";

        for (int i = 0; i < 12; i++)
        {
            GameObject edge = new GameObject("UTM_Edge_" + i);
            edge.transform.SetParent(transform, false);
            LineRenderer lr = edge.AddComponent<LineRenderer>();
            lr.useWorldSpace = true;
            lr.positionCount = 2;
            lr.widthMultiplier = outlineWidth;
            lr.material = _lineMat;
            lr.startColor = outlineColor;
            lr.endColor = outlineColor;
            lr.enabled = false;
            _outlineEdges[i] = lr;
        }

        for (int i = 0; i < _roundedLines.Length; i++)
        {
            GameObject line = new GameObject("UTM_Rounded_" + i);
            line.transform.SetParent(transform, false);
            LineRenderer lr = line.AddComponent<LineRenderer>();
            lr.useWorldSpace = true;
            lr.positionCount = 0;
            lr.widthMultiplier = outlineWidth;
            lr.material = _lineMat;
            lr.startColor = outlineColor;
            lr.endColor = outlineColor;
            lr.loop = true;
            lr.enabled = false;
            _roundedLines[i] = lr;
        }
    }

    private void DrawOutline(MarkerShapeInfo shape, Color color)
    {
        if (shape.shape == MarkerShape.Sphere)
        {
            DrawSphereOutline(shape.bounds, color);
            return;
        }

        if (shape.shape == MarkerShape.Capsule)
        {
            DrawCapsuleOutline(shape.bounds, color);
            return;
        }

        DrawBoxOutline(shape.bounds, color);
    }

    private void DrawBoxOutline(Bounds bounds, Color color)
    {
        SetRoundedLinesVisible(0);
        Bounds b = bounds;
        b.Expand(outlinePadding);

        Vector3 mn = b.min, mx = b.max;
        Vector3[] c = {
            new Vector3(mn.x, mn.y, mn.z), new Vector3(mx.x, mn.y, mn.z),
            new Vector3(mx.x, mn.y, mx.z), new Vector3(mn.x, mn.y, mx.z),
            new Vector3(mn.x, mx.y, mn.z), new Vector3(mx.x, mx.y, mn.z),
            new Vector3(mx.x, mx.y, mx.z), new Vector3(mn.x, mx.y, mx.z),
        };

        int[,] edges = { {0,1},{1,2},{2,3},{3,0},{4,5},{5,6},{6,7},{7,4},{0,4},{1,5},{2,6},{3,7} };
        for (int i = 0; i < 12; i++)
        {
            LineRenderer lr = _outlineEdges[i];
            if (lr == null) continue;
            lr.enabled = true;
            lr.startColor = color;
            lr.endColor = color;
            lr.SetPosition(0, c[edges[i, 0]]);
            lr.SetPosition(1, c[edges[i, 1]]);
        }
    }

    private void DrawSphereOutline(Bounds bounds, Color color)
    {
        SetBoxEdgesVisible(false);
        Bounds b = bounds;
        b.Expand(outlinePadding);
        float radius = Mathf.Max(b.extents.x, b.extents.y, b.extents.z);
        DrawCircle(_roundedLines[0], b.center, Vector3.up, radius, color);
        DrawCircle(_roundedLines[1], b.center, Vector3.right, radius, color);
        DrawCircle(_roundedLines[2], b.center, Vector3.forward, radius, color);
        SetRoundedLinesVisible(3);
    }

    private void DrawCapsuleOutline(Bounds bounds, Color color)
    {
        SetBoxEdgesVisible(false);
        Bounds b = bounds;
        b.Expand(outlinePadding);
        float radius = Mathf.Max(0.06f, Mathf.Min(b.extents.x, b.extents.z));
        float topY = b.center.y + Mathf.Max(0f, b.extents.y - radius);
        float botY = b.center.y - Mathf.Max(0f, b.extents.y - radius);
        Vector3 top = new Vector3(b.center.x, topY, b.center.z);
        Vector3 bot = new Vector3(b.center.x, botY, b.center.z);
        DrawCircle(_roundedLines[0], top, Vector3.up, radius, color);
        DrawCircle(_roundedLines[1], bot, Vector3.up, radius, color);
        DrawCircle(_roundedLines[2], b.center, Vector3.right, Mathf.Max(radius, b.extents.y), color);
        DrawCircle(_roundedLines[3], b.center, Vector3.forward, Mathf.Max(radius, b.extents.y), color);
        SetRoundedLinesVisible(4);
    }

    private void DrawCircle(LineRenderer lr, Vector3 center, Vector3 normal, float radius, Color color)
    {
        if (lr == null) return;
        const int segments = 64;
        lr.positionCount = segments;
        lr.loop = true;
        lr.enabled = true;
        lr.startColor = color;
        lr.endColor = color;

        Vector3 axisA = Vector3.Cross(normal, Vector3.up);
        if (axisA.sqrMagnitude < 0.0001f)
            axisA = Vector3.Cross(normal, Vector3.right);
        axisA.Normalize();
        Vector3 axisB = Vector3.Cross(normal, axisA).normalized;

        for (int i = 0; i < segments; i++)
        {
            float a = (i / (float)segments) * Mathf.PI * 2f;
            lr.SetPosition(i, center + (axisA * Mathf.Cos(a) + axisB * Mathf.Sin(a)) * radius);
        }
    }

    private MarkerShapeInfo CalculateShape(Transform target)
    {
        Bounds bounds = CalculateBounds(target);
        MarkerShape markerShape = DetectShape(target, bounds);
        return new MarkerShapeInfo { bounds = bounds, shape = markerShape };
    }

    private Bounds CalculateBounds(Transform target)
    {
        bool targetKecilApd = IsSmallWearableTarget(target);
        Collider[] colliders = target.GetComponentsInChildren<Collider>(true);
        bool hasColliderBounds = false;
        Bounds colliderBounds = new Bounds(target.position, Vector3.one * 0.1f);
        if (!targetKecilApd)
        {
            foreach (Collider c in colliders)
            {
                if (c == null || !c.enabled) continue;
                if (ShouldIgnoreBoundsObject(c.transform)) continue;
                if (!hasColliderBounds) { colliderBounds = c.bounds; hasColliderBounds = true; }
                else colliderBounds.Encapsulate(c.bounds);
            }
        }

        Renderer[] renderers = target.GetComponentsInChildren<Renderer>(true);
        bool has = false;
        Bounds bounds = new Bounds(target.position, Vector3.one * 0.2f);
        foreach (Renderer r in renderers)
        {
            if (r == null || !r.enabled) continue;
            if (r is LineRenderer) continue; // skip our own outlines
            if (ShouldIgnoreBoundsObject(r.transform)) continue;
            if (!has) { bounds = r.bounds; has = true; }
            else bounds.Encapsulate(r.bounds);
        }
        if (!has && hasColliderBounds) bounds = colliderBounds;
        else if (!has) bounds = new Bounds(target.position, Vector3.one * minimumBoundsSize);
        Vector3 size = bounds.size;
        float minSize = targetKecilApd ? 0.08f : minimumBoundsSize;
        size.x = Mathf.Max(size.x, minSize);
        size.y = Mathf.Max(size.y, minSize);
        size.z = Mathf.Max(size.z, minSize);
        bounds.size = size;
        return bounds;
    }

    private bool IsSmallWearableTarget(Transform target)
    {
        if (target == null) return false;
        string n = target.name.ToLowerInvariant();
        return n.Contains("respirator")
            || n.Contains("mask")
            || n.Contains("helmet")
            || n.Contains("earplug")
            || n.Contains("walkie")
            || n.Contains("glove")
            || n.Contains("boot")
            || n.Contains("glass")
            || n.Contains("vest");
    }

    private bool ShouldIgnoreBoundsObject(Transform t)
    {
        while (t != null)
        {
            string n = t.name.ToLowerInvariant();
            if (n.Contains("label") || n.Contains("text") || n.Contains("canvas") ||
                n.Contains("socket") || n.Contains("scanner") || n.Contains("marker") ||
                n.Contains("taskhint") || n.Contains("arrow") || n.Contains("outline") ||
                n.Contains("line") || n.Contains("guide"))
                return true;
            t = t.parent;
        }
        return false;
    }

    private MarkerShape DetectShape(Transform target, Bounds bounds)
    {
        string targetName = target != null ? target.name.ToLowerInvariant() : string.Empty;
        if (targetName.Contains("respirator") || targetName.Contains("mask") || targetName.Contains("helmet"))
            return MarkerShape.Sphere;
        if (targetName.Contains("earplug"))
            return MarkerShape.Capsule;

        Collider[] colliders = target.GetComponentsInChildren<Collider>(true);
        foreach (Collider c in colliders)
        {
            if (c == null || !c.enabled) continue;
            if (c is SphereCollider) return MarkerShape.Sphere;
            if (c is CapsuleCollider) return MarkerShape.Capsule;
            if (c is BoxCollider) return MarkerShape.Box;
        }

        Vector3 s = bounds.size;
        float max = Mathf.Max(s.x, s.y, s.z);
        float min = Mathf.Max(0.0001f, Mathf.Min(s.x, s.y, s.z));
        if (max <= 0.9f && max / min < 1.55f)
            return MarkerShape.Sphere;
        if (s.y > Mathf.Max(s.x, s.z) * 1.8f && Mathf.Abs(s.x - s.z) <= Mathf.Max(s.x, s.z) * 0.35f)
            return MarkerShape.Capsule;
        return MarkerShape.Box;
    }

    private void SetBoxEdgesVisible(bool visible)
    {
        for (int i = 0; i < 12; i++)
            if (_outlineEdges[i] != null) _outlineEdges[i].enabled = visible;
    }

    private void SetRoundedLinesVisible(int visibleCount)
    {
        for (int i = 0; i < _roundedLines.Length; i++)
        {
            if (_roundedLines[i] == null) continue;
            bool visible = i < visibleCount;
            _roundedLines[i].enabled = visible;
            if (!visible) _roundedLines[i].positionCount = 0;
        }
    }

    private bool IsCcdLevel()
    {
        return _glm != null && _glm.CurrentLevel == GameLevelManager.GameLevel.Level10_CCD;
    }

    private void SetVisible(bool visible)
    {
        if (_arrowRoot != null) _arrowRoot.SetActive(visible);
        for (int i = 0; i < 12; i++)
            if (_outlineEdges[i] != null) _outlineEdges[i].enabled = false;
        SetRoundedLinesVisible(0);
    }

    private void DisableOutline()
    {
        SetBoxEdgesVisible(false);
        SetRoundedLinesVisible(0);
    }
}
