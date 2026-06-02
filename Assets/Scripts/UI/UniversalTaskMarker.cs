using UnityEngine;

/// <summary>
/// OLIVIA VR - UniversalTaskMarker.cs
///
/// Sistem universal yang menampilkan PANAH BESAR 3D + OUTLINE BOX di object target aktif
/// untuk SETIAP level dan task. Bekerja otomatis berdasarkan state GameLevelManager.
///
/// Fitur:
///   - Panah 3D besar berputar di atas target object
///   - Outline wireframe box (12 edges) di sekeliling target
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
    [SerializeField] private float outlinePadding = 0.06f;
    [SerializeField] private float outlinePulseSpeed = 2.2f;

    [Header("=== Polling ===")]
    [SerializeField] private float updateInterval = 0.2f;

    private GameObject _arrowRoot;
    private Material _arrowMat;
    private readonly LineRenderer[] _outlineEdges = new LineRenderer[12];
    private Material _lineMat;
    private Transform _currentTarget;
    private float _nextUpdate;
    private GameLevelManager _glm;

    private void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
        CreateArrow();
        CreateOutline();
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
            if (dist < 2.0f)
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

        // Outline
        DrawOutline(bounds, outlineColor * pulse);
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
                if (!_glm.SudahTekanTombolDcs) return FindDcsButton(4);
                if (!_glm.SudahLaporanHt) return FindWalkieTalkie();
                return null;

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
                if (!_glm.SudahTekanTombolDcs) return FindDcsButton(9);
                if (!_glm.SudahLaporanHt) return FindWalkieTalkie();
                return FindByName("CCD_BlenderRig", "CCD_Field");

            case GameLevelManager.GameLevel.Level11_MHP:
                if (!_glm.SudahTekanTombolDcs) return FindDcsButton(10);
                if (!_glm.SudahLaporanHt) return FindWalkieTalkie();
                return FindByName("Level11_PurificationMHP_BlenderRig", "MHP_SampleBottle");

            case GameLevelManager.GameLevel.Level12_TailingDischarge:
                if (!_glm.SudahTekanTombolDcs) return FindDcsButton(11);
                if (!_glm.SudahLaporanHt) return FindWalkieTalkie();
                return FindByName("Tailing_Neutralization_Tank", "FilterPress");

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

        if (!pm.isHelmetWorn) return FindByName("Socket_Scanner_Hat");
        if (!pm.isVestWorn) return FindByName("Socket_Scanner_Rompi");
        if (!pm.isGlassesWorn) return FindByName("Socket_Scanner_Glassess");
        if (!pm.isBootsWorn) return FindByName("Socket_Scanner_Boots");
        if (!pm.isGlovesWorn) return FindByName("Socket_Scanner_Gloves");
        if (!pm.isRespiratorWorn) return FindByName("Socket_Scanner_RespiratorMask");
        if (!pm.isEarplugWorn) return FindByName("Socket_Scanner_EarPlug");
        if (!pm.isWalkieTalkieTaken) return FindByName("Socket_Scanner_WalkieTalkie");
        return null; // semua APD lengkap
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
        if (!_glm.Level6SlurryMasukAutoclave)
            return FindByName("L6_SlurryValve_Pivot_Runtime", "L5_Condensate_Drain_Handwheel_Hub", "L6_SlurryRoute_ValveWheel_Runtime");
        if (!_glm.Level6SlurryReportDone) return FindWalkieTalkie();
        if (!_glm.Level6DcsAcidReady)
            return FindByName("Btn_AcidPlus", "Btn_AcidArm", "Btn_AcidStrokePlus");
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
            return FindByName("L7_InletValve_Pivot_Runtime", "L7_LiquidUnderflow_Handwheel_Hub");
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
            if (!l8.AllSamplesTaken())
                return FindByName("FV1_XRay_SlurryPool_Ghost", "FlashLetdown_SampleStation_Backplate");
            if (!l8.IsCompleted && !_glm.SudahLaporanHt)
                return FindWalkieTalkie();
            return null;
        }

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
    }

    private void DrawOutline(Bounds bounds, Color color)
    {
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

    private Bounds CalculateBounds(Transform target)
    {
        Renderer[] renderers = target.GetComponentsInChildren<Renderer>(true);
        bool has = false;
        Bounds bounds = new Bounds(target.position, Vector3.one * 0.2f);
        foreach (Renderer r in renderers)
        {
            if (r == null || !r.enabled) continue;
            if (r is LineRenderer) continue; // skip our own outlines
            if (!has) { bounds = r.bounds; has = true; }
            else bounds.Encapsulate(r.bounds);
        }
        if (!has) bounds = new Bounds(target.position, Vector3.one * 0.3f);
        return bounds;
    }

    private void SetVisible(bool visible)
    {
        if (_arrowRoot != null) _arrowRoot.SetActive(visible);
        for (int i = 0; i < 12; i++)
            if (_outlineEdges[i] != null) _outlineEdges[i].enabled = visible;
    }
}
