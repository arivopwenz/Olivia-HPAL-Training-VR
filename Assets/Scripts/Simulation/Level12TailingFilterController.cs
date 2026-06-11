using System.Collections;
using System.Collections.Generic;
using Unity.XR.CoreUtils;
using UnityEngine;

/// <summary>
/// OLIVIA VR - Level12TailingFilterController.cs  (Display "Level 11" — Tailing & Filter Press)
///
/// Gameplay INTERAKTIF & INFORMATIF (HPAL nikel — manajemen limbah):
/// Underflow CCD (tailing asam, leach residue + gypsum) dinetralkan lalu di-dewater
/// jadi cake kering untuk DRY STACK; filtrat jernih ke WWTP.
///   Tahap 1 NETRALISASI : LIMESTONE/KAPUR (CaCO3 / Ca(OH)2)  pH 2.3 -> 8.0
///                          buang asam sisa + endapkan logam berat (compliance lingkungan)
///   Tahap 2 FILTER PRESS : plate & frame dewatering, moisture cake 60% -> < 25% (stackable)
/// Operator menekan tombol per tahap (XR ray/poke ATAU keyboard SPACE/1), pH naik live
/// di panel + jarum gauge + beacon hijau, plat press menekan, filtrat mengalir, cake keluar
/// di konveyor. Lalu inspeksi cake (proximity) -> Compliance QC pop-up -> ACCEPT -> lapor HT.
/// </summary>
public class Level12TailingFilterController : MonoBehaviour
{
    [Header("=== Player & Teleport ===")]
    [SerializeField] private Transform _playerRigRoot;
    [SerializeField] private Transform _teleportTargetDcs;

    [Header("=== Machine References ===")]
    [SerializeField] private GameObject _rig;                 // Level13_DryStack_BlenderRig
    [SerializeField] private Transform _agitatorRoot;          // Polishing_Agitator_Root
    [SerializeField] private GameObject _limestonePour;        // Limestone_Pour_Stream
    [SerializeField] private GameObject _neutralizedSurface;   // Neutralized_Surface
    [SerializeField] private GameObject _filtrateChannel;      // Filtrate_Channel
    [SerializeField] private GameObject _polishedFlow;         // Polished_Tailing_Flow
    [SerializeField] private Transform _phNeedle;              // pH_Monitor_Needle
    [SerializeField] private GameObject _phStatusGreen, _phStatusRed;
    [SerializeField] private GameObject _beaconGreen, _beaconRed;
    [SerializeField] private GameObject[] _cakeBlocks;         // Cake_Block_00..05
    [SerializeField] private Transform[] _rollers;             // Conveyor_Roller_*
    private Transform[] _pressPlates;
    private Vector3[] _pressPlateBaseLocal;

    [Header("=== Process Settings ===")]
    [SerializeField] private float _fadeDuration = 2.5f;
    [SerializeField] private float _doseDuration = 6f;
    [SerializeField] private float _pressDuration = 8f;
    [SerializeField] private float _agitatorRpm = 26f;
    [SerializeField] private float _inspectRadius = 3.5f;

    [Header("=== Audio ===")]
    [SerializeField] private AudioSource _agitatorAudio, _pressAudio, _readyAudio;

    private const float PhStart = 2.3f, PhTarget = 8.0f;       // window aman 8-9 (compliance)
    private const float MoistStart = 60f, MoistTarget = 22f;   // cake stackable < 25%

    private PlayerHUD _hud;
    private GameLevelManager _glm;
    private Coroutine _seq;
    private bool _levelActive, _processStarted, _busy;
    private int _stage;                 // 0 = netralisasi, 1 = filter press, 2 = inspeksi, 3 = compliance, 4 = report
    private float _pHCurrent = PhStart, _moisture = MoistStart;
    private bool _neutralizeDone, _filterPressDone, _inspected, _complianceAccepted, _questComplete;

    private GameObject _btn; private TextMesh _btnLabel;
    private GameObject _infoPanel; private TextMesh _infoText;
    private GameObject _qcCanvas; private System.Action _pendingClick;
    private MaterialPropertyBlock _mpb;
    private static readonly int IdBase = Shader.PropertyToID("_BaseColor");
    private static readonly int IdColor = Shader.PropertyToID("_Color");
    private static readonly int IdEmis = Shader.PropertyToID("_EmissionColor");

    // ===== HT-GATED FLOW (lapor HT -> tailing naik -> lapor HT -> susu kapur) =====
    private int _await;                 // 0 none, 1 alirkan tailing, 2 dosing kapur, 3 filter press, 4 report akhir
    private GameObject _liquidBody;     // badan cairan tailing (volume penuh, level via shader _FillY)
    private Renderer _liquidBodyR; private Material _liquidBodyMat;
    private Vector3 _liqBaseScale, _liqBasePos;
    private Material _surfMat;          // material instance permukaan (Neutralized_Surface)
    // ---- Fluida gaya autoclave (shader Olivia/L7SlurryFill: world-Y clip + depth gradient + surface glow) ----
    private float _fillBottomY = 1.30f;  // dasar cairan (match FLOOR_Z tangki baru)
    private float _fillTopY = 5.15f;     // level penuh (tangki tinggi interior ~6.6, sisakan freeboard)
    private static readonly Color _fluidAcidShallow = new Color(0.46f, 0.29f, 0.13f);
    private static readonly Color _fluidAcidDeep    = new Color(0.26f, 0.15f, 0.06f);
    private static readonly Color _fluidAcidEmis    = new Color(0.20f, 0.10f, 0.03f);
    private static readonly Color _fluidNeutShallow = new Color(0.55f, 0.60f, 0.52f);
    private static readonly Color _fluidNeutDeep    = new Color(0.30f, 0.38f, 0.32f);
    private static readonly Color _fluidNeutEmis    = new Color(0.10f, 0.16f, 0.08f);
    private GameObject _limePourGo;     // runtime susu kapur (kalau Limestone_Pour_Stream tak ada)
    private Material _limePourMat;
    private ParticleSystem _bubbles;    // gelembung reaksi
    private bool _bubblesOn;
    private bool _liquidReady;
    // Warna riset: tailing asam coklat keruh -> setelah kapur abu-kehijauan netral (gypsum+hidroksida)
    private static readonly Color _colAcidTailing = new Color(0.40f, 0.26f, 0.13f);   // asam, Fe + gypsum, keruh coklat
    private static readonly Color _colNeutralTailing = new Color(0.52f, 0.56f, 0.50f); // netral, gypsum abu-kehijauan
    private static readonly Color _colLimeSlurry = new Color(0.90f, 0.92f, 0.88f);    // susu kapur putih
    // pusat tank netralisasi V3 (z~142.8)
    private static readonly Vector3 _neutTankCenter = new Vector3(39.1f, 2.3f, 142.8f);

    // ---- Public props for HUD ----
    public bool LevelActive => _levelActive;
    public bool NeutralizeDone => _neutralizeDone;
    public bool FilterPressDone => _filterPressDone;
    public bool Inspected => _inspected;
    public bool ComplianceAccepted => _complianceAccepted;
    public bool QuestComplete => _questComplete;
    public float PHCurrent => _pHCurrent;
    public float CakeMoisture => _moisture;
    public int AwaitStage => _await;   // 0 none, 1 alirkan tailing, 2 dosing kapur, 3 filter press
    public int StageNow => _stage;     // 0 netralisasi, 1 filter press, 2 inspeksi, 3 compliance, 4 report

    private void Awake()
    {
        _hud = FindFirstObjectByType<PlayerHUD>();
        _glm = GameLevelManager.Instance;
        AutoFindReferences();
        EnsureAudio();
        SetProcessVisuals(false);
    }

    private void OnEnable()
    {
        GameLevelManager.OnLevelStarted += OnLevelStarted;
        GameLevelManager.OnDCSButtonPressed += OnDcsButtonPressed;
        WalkieTalkieManager.OnPTTDilepas += OnTailingHtReleased;
    }

    private void OnDisable()
    {
        GameLevelManager.OnLevelStarted -= OnLevelStarted;
        GameLevelManager.OnDCSButtonPressed -= OnDcsButtonPressed;
        WalkieTalkieManager.OnPTTDilepas -= OnTailingHtReleased;
        if (_seq != null) StopCoroutine(_seq);
        Stop(_agitatorAudio); Stop(_pressAudio);
    }

    private void OnLevelStarted(GameLevelManager.GameLevel level)
    {
        _levelActive = level == GameLevelManager.GameLevel.Level12_TailingDischarge;
        if (!_levelActive) { SetProcessVisuals(false); ShowButton(false); ShowInfo(false); HideQc(); Stop(_agitatorAudio); if (_liquidReady) HideTailingLiquid(); _await = 0; return; }
        _glm = GameLevelManager.Instance;
        AutoFindReferences();
        ProtectTailingEquipmentFromOcclusion();
        _processStarted = false; _busy = false; _stage = 0; _await = 0;
        _pHCurrent = PhStart; _moisture = MoistStart;
        _neutralizeDone = _filterPressDone = _inspected = _complianceAccepted = _questComplete = false;
        PushPH(); SetProcessVisuals(false); ShowButton(false); ShowInfo(false); HideQc();
        if (_liquidReady) HideTailingLiquid();
        if (_hud != null) _hud.ShowNotifPublic("Level 11: Tailing (underflow CCD) siap diolah. Tekan DCS 11 untuk mulai.");
        TeleportTo(_teleportTargetDcs != null ? _teleportTargetDcs.position : Vector3.zero, Vector3.forward, _teleportTargetDcs == null);
    }

    private void OnDcsButtonPressed(int number)
    {
        if (!_levelActive || number != 11 || _processStarted) return;
        _processStarted = true;
        _seq = StartCoroutine(StartFieldSequence());
    }

    private IEnumerator StartFieldSequence()
    {
        if (_hud != null) _hud.PlayManualFade(_fadeDuration);
        yield return new WaitForSeconds(_fadeDuration * 0.5f);
        // Berdiri di depan tank netralisasi V3 (x39, z142.8)
        Vector3 stand = new Vector3(32f, 0.10f, 138.5f);
        Vector3 fwd = (_neutTankCenter - stand); fwd.y = 0f;
        TeleportTo(stand, fwd, false);
        yield return new WaitForSeconds(_fadeDuration * 0.5f + 0.4f);
        PlayAudio(_agitatorAudio, 0.32f);
        EnsureLiquidBody();
        HideTailingLiquid();                 // awal: tank KOSONG (belum ada cairan)
        BuildOperatorStation();
        ShowButton(false); ShowInfo(true);
        _stage = 0; _await = 1;              // tunggu HT #1
        if (_hud != null) _hud.ShowNotifPublic("TAILING (underflow CCD) siap diolah. Lapor HT (tahan T) untuk alirkan tailing asam ke tangki netralisasi.", 9f);
        _seq = null;
    }

    private void Update()
    {
        if (!_levelActive || !_processStarted) return;
        if (_agitatorRoot != null) _agitatorRoot.Rotate(Vector3.up, _agitatorRpm * 6f * Time.deltaTime, Space.World);
        if (_busy) AnimateRollers();

        // Fallback keyboard utk HT gate (desktop): SPACE/1 = lapor HT
        if (!_busy && _await > 0 && (Input.GetKeyDown(KeyCode.Space) || Input.GetKeyDown(KeyCode.Alpha1))) OnTailingHtReleased();

        if (_stage == 2 && !_inspected) UpdateInspectProximity();
        if (_stage == 3 && !_complianceAccepted)
        {
            if (_qcCanvas == null && Input.GetKeyDown(KeyCode.L)) ShowQc();
            if (_qcCanvas != null && _qcCanvas.activeSelf && _pendingClick != null && Input.GetKeyDown(KeyCode.Return))
            { var a = _pendingClick; _pendingClick = null; a(); }
        }
        UpdateInfo();
    }

    // ============================================================ HT-GATED STAGES
    // Gate utama: dipanggil saat operator melepas PTT (HT) ATAU keyboard SPACE fallback.
    private void OnTailingHtReleased()
    {
        if (!_levelActive || !_processStarted || _busy) return;
        if (_await == 1) { _await = 0; _seq = StartCoroutine(FillTailingRoutine()); }
        else if (_await == 2) { _await = 0; _seq = StartCoroutine(DoseLimeRoutine()); }
        else if (_await == 3) { _await = 0; _seq = StartCoroutine(RunFilterPressRoutine()); }
        // _await == 4 (report akhir) ditangani lewat QC accept -> NotifyLevel12TailingFilterComplete
    }

    // Tombol konsol = fallback ekuivalen HT (disembunyikan secara default).
    private void TryAction() { OnTailingHtReleased(); }

    // Bangun badan cairan tailing: VOLUME PENUH, level diatur via shader _FillY (gaya fluida autoclave).
    private void EnsureLiquidBody()
    {
        if (_liquidReady) return;
        // Turunkan posisi & dimensi cairan dari SHELL tangki aktual (robust terhadap rebuild tangki).
        Vector3 cen = _neutTankCenter;
        float dia = 5.0f;
        var shellGo = GameObject.Find("TNT_Shell_Glass");
        Renderer shellR = shellGo != null ? shellGo.GetComponent<Renderer>() : null;
        if (shellR != null)
        {
            Bounds sb = shellR.bounds;
            cen = new Vector3(sb.center.x, sb.center.y, sb.center.z);
            _fillBottomY = sb.min.y + 0.20f;          // dasar cairan tepat di atas lantai tangki
            _fillTopY = sb.max.y - 0.55f;             // sisakan freeboard di bawah bibir
            dia = Mathf.Min(sb.extents.x, sb.extents.z) * 2f * 0.86f; // muat di dalam shell
        }

        _liquidBody = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        _liquidBody.name = "Tailing_Neut_LiquidBody";
        Object.Destroy(_liquidBody.GetComponent<Collider>());
        _liquidBody.transform.SetParent(transform, false);
        // volume selalu ukuran penuh; level visual via shader _FillY
        float cyTop = _fillTopY + 0.45f;
        float cyBot = _fillBottomY - 0.10f;
        float h = cyTop - cyBot;
        _liquidBody.transform.position = new Vector3(cen.x, cyBot + h * 0.5f, cen.z);
        // kompensasi lossyScale parent supaya diameter dunia = dia (di DALAM shell), tinggi=h
        Vector3 pls = transform.lossyScale;
        _liquidBody.transform.localScale = new Vector3(
            dia / Mathf.Max(0.0001f, pls.x),
            (h * 0.5f) / Mathf.Max(0.0001f, pls.y),
            dia / Mathf.Max(0.0001f, pls.z));
        _liqBaseScale = _liquidBody.transform.localScale;
        _liqBasePos = _liquidBody.transform.position;
        _liquidBodyR = _liquidBody.GetComponent<Renderer>();
        _liquidBodyMat = BuildTailingFluidMaterial();
        if (_liquidBodyMat.HasProperty("_SwirlAxisZ")) _liquidBodyMat.SetFloat("_SwirlAxisZ", cen.z);
        if (_liquidBodyMat.HasProperty("_DepthRange")) _liquidBodyMat.SetFloat("_DepthRange", Mathf.Max(2f, _fillTopY - _fillBottomY));
        _liquidBodyR.sharedMaterial = _liquidBodyMat;
        SetFluidColors(_fluidAcidShallow, _fluidAcidDeep, _fluidAcidEmis);
        SetFillY(-1000f); // kosong (ter-clip semua)
        if (_neutralizedSurface != null) _neutralizedSurface.SetActive(false); // shader punya surface band sendiri
        _liquidReady = true;
    }

    // Material fluida gaya autoclave (Olivia/L7SlurryFill: world-Y clip + depth gradient + glow + swirl).
    private Material BuildTailingFluidMaterial()
    {
        Shader sh = Shader.Find("Olivia/L7SlurryFill");
        if (sh == null) sh = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard");
        var m = new Material(sh) { name = "M_Tailing_Fluid_Runtime" };
        if (m.HasProperty("_EmissionIntensity")) m.SetFloat("_EmissionIntensity", 0.16f);
        if (m.HasProperty("_SurfaceGlow")) m.SetFloat("_SurfaceGlow", 2.2f);
        if (m.HasProperty("_SurfaceWidth")) m.SetFloat("_SurfaceWidth", 0.40f);
        if (m.HasProperty("_DepthRange")) m.SetFloat("_DepthRange", 4.0f);
        if (m.HasProperty("_Alpha")) m.SetFloat("_Alpha", 0.82f);
        if (m.HasProperty("_RippleStrength")) m.SetFloat("_RippleStrength", 0.05f);
        if (m.HasProperty("_SwirlSpeed")) m.SetFloat("_SwirlSpeed", 0.6f);
        if (m.HasProperty("_SwirlStrength")) m.SetFloat("_SwirlStrength", 0.30f);
        if (m.HasProperty("_SwirlAxisZ")) m.SetFloat("_SwirlAxisZ", _neutTankCenter.z);
        if (m.HasProperty("_SwirlSpacing")) m.SetFloat("_SwirlSpacing", 80f); // 1 poros (tank tunggal)
        m.EnableKeyword("_EMISSION");
        return m;
    }

    private void SetFillY(float y)
    {
        if (_liquidBodyMat != null && _liquidBodyMat.HasProperty("_FillY")) _liquidBodyMat.SetFloat("_FillY", y);
    }
    private void SetFluidColors(Color shallow, Color deep, Color emis)
    {
        if (_liquidBodyMat == null) return;
        if (_liquidBodyMat.HasProperty("_BaseColor")) _liquidBodyMat.SetColor("_BaseColor", shallow);
        if (_liquidBodyMat.HasProperty("_DeepColor")) _liquidBodyMat.SetColor("_DeepColor", deep);
        if (_liquidBodyMat.HasProperty("_EmissionColor")) _liquidBodyMat.SetColor("_EmissionColor", emis);
    }

    private void HideTailingLiquid()
    {
        EnsureLiquidBody();
        if (_liquidBody != null) _liquidBody.SetActive(false);
        if (_neutralizedSurface != null) _neutralizedSurface.SetActive(false);
        if (_limestonePour != null) _limestonePour.SetActive(false);
        if (_limePourGo != null) _limePourGo.SetActive(false);
    }

    // HT #1: tailing asam mengalir & NAIK dari dasar tangki.
    private IEnumerator FillTailingRoutine()
    {
        _busy = true;
        EnsureLiquidBody();
        PlayAudio(_pressAudio, 0.12f);
        if (_hud != null) _hud.ShowNotifPublic("Valve dibuka. Tailing asam (pH 2.3) mengalir, level naik dari dasar...", 6f);
        _pHCurrent = PhStart; PushPH(); UpdatePhNeedle();
        if (_liquidBody != null) _liquidBody.SetActive(true);
        SetFluidColors(_fluidAcidShallow, _fluidAcidDeep, _fluidAcidEmis);
        float dur = 5.5f, t = 0f;
        SetFillY(_fillBottomY - 0.05f);
        while (t < dur)
        {
            t += Time.deltaTime; float p = Smooth(t / dur);
            SetFillY(Mathf.Lerp(_fillBottomY - 0.05f, _fillTopY, p)); // permukaan naik dari dasar (gaya autoclave)
            yield return null;
        }
        SetFillY(_fillTopY);
        Stop(_pressAudio);
        _busy = false; _await = 2;
        if (_hud != null) _hud.ShowNotifPublic("Tangki terisi tailing asam. Lapor HT (tahan T) untuk dosing SUSU KAPUR (lime slurry).", 9f);
    }

    // HT #2: susu kapur turun dari atas, reaksi netralisasi (warna + pH + gelembung).
    private IEnumerator DoseLimeRoutine()
    {
        _busy = true;
        EnsureBubbles();
        ShowLimePour(true);
        ShowBubbles(true);
        PlayAudio(_pressAudio, 0.28f);
        if (_hud != null) _hud.ShowNotifPublic("Susu kapur Ca(OH)2 didosing: H2SO4 sisa + kapur -> gypsum. Asam dinetralkan, logam berat mengendap...", 7f);
        float dur = 6f, t = 0f;
        while (t < dur)
        {
            t += Time.deltaTime; float e = Smooth(Mathf.Clamp01(t / dur));
            _pHCurrent = Mathf.Lerp(PhStart, PhTarget, e); PushPH(); UpdatePhNeedle();
            SetFluidColors(
                Color.Lerp(_fluidAcidShallow, _fluidNeutShallow, e),
                Color.Lerp(_fluidAcidDeep, _fluidNeutDeep, e),
                Color.Lerp(_fluidAcidEmis, _fluidNeutEmis, e));
            yield return null;
        }
        _pHCurrent = PhTarget; PushPH(); UpdatePhNeedle();
        ShowLimePour(false); ShowBubbles(false); Stop(_pressAudio);
        SetActive(_phStatusGreen, true); SetActive(_phStatusRed, false);
        SetActive(_beaconGreen, true); SetActive(_beaconRed, false);
        if (_polishedFlow != null) _polishedFlow.SetActive(true);
        _neutralizeDone = true; _busy = false; _await = 3;
        PlayAudio(_readyAudio, 0.3f);
        if (_hud != null) _hud.ShowNotifPublic("Tailing NETRAL (pH 8). Lapor HT (tahan T) untuk kirim ke FILTER PRESS (peras air jadi cake).", 9f);
    }

    // HT #3: pindah ke filter press lalu jalankan dewatering.
    private IEnumerator RunFilterPressRoutine()
    {
        _busy = true;
        if (_hud != null) _hud.PlayManualFade(_fadeDuration);
        yield return new WaitForSeconds(_fadeDuration * 0.5f);
        TeleportTo(new Vector3(33.5f, 1.5f, 152f), new Vector3(1f, 0f, 0.2f), false);
        yield return new WaitForSeconds(_fadeDuration * 0.5f + 0.3f);
        _stage = 1;
        yield return StartCoroutine(FilterPressRoutine());
    }

    private void EnsureBubbles()
    {
        if (_bubbles != null) return;
        var go = new GameObject("Tailing_Reaction_Bubbles");
        go.transform.SetParent(transform, false);
        go.transform.position = new Vector3(_neutTankCenter.x, 3.2f, _neutTankCenter.z);
        _bubbles = go.AddComponent<ParticleSystem>();
        var main = _bubbles.main; main.startLifetime = 1.4f; main.startSpeed = 0.8f; main.startSize = 0.25f;
        main.startColor = new Color(0.95f, 0.97f, 0.9f, 0.7f); main.maxParticles = 200; main.playOnAwake = false;
        var em = _bubbles.emission; em.rateOverTime = 0f;
        var sh = _bubbles.shape; sh.shapeType = ParticleSystemShapeType.Circle; sh.radius = 2.0f;
        var r = _bubbles.GetComponent<ParticleSystemRenderer>();
        r.sharedMaterial = new Material(Shader.Find("Sprites/Default")) { color = new Color(0.95f, 0.97f, 0.9f, 0.7f) };
        _bubbles.Stop();
    }

    private void ShowBubbles(bool on)
    {
        if (_bubbles == null) return;
        _bubblesOn = on;
        if (on) { _bubbles.Play(); StartCoroutine(EmitBubbles()); }
        else _bubbles.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
    }
    private IEnumerator EmitBubbles()
    {
        while (_bubblesOn && _bubbles != null) { _bubbles.Emit(4); yield return new WaitForSeconds(0.08f); }
    }

    private void ShowLimePour(bool on)
    {
        if (_limestonePour != null) { _limestonePour.SetActive(on); if (on) Tint(_limestonePour, _colLimeSlurry); return; }
        if (_limePourGo == null)
        {
            _limePourGo = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            _limePourGo.name = "Lime_Slurry_Pour_Runtime";
            Object.Destroy(_limePourGo.GetComponent<Collider>());
            _limePourGo.transform.SetParent(transform, false);
            _limePourGo.transform.position = new Vector3(_neutTankCenter.x, 4.6f, _neutTankCenter.z);
            _limePourGo.transform.localScale = new Vector3(0.18f, 1.6f, 0.18f);
            _limePourGo.GetComponent<Renderer>().sharedMaterial = OpaqueMat(_colLimeSlurry);
        }
        _limePourGo.SetActive(on);
    }

    private void SetMatColor(Material m, Color c)
    {
        if (m == null) return;
        if (m.HasProperty(IdBase)) m.SetColor(IdBase, c);
        if (m.HasProperty(IdColor)) m.SetColor(IdColor, c);
        m.color = c;
    }

    private IEnumerator FilterPressRoutine()
    {
        CapturePressPlateLayout();
        PlayAudio(_pressAudio, 0.42f);
        if (_hud != null) _hud.ShowNotifPublic("FILTER PRESS: hydraulic closing. Plate merapat sebelum slurry dipompa.", 5f);

        float closeTime = 0f;
        while (closeTime < 1.8f)
        {
            closeTime += Time.deltaTime;
            SetPressPlateCompression(Smooth(closeTime / 1.8f));
            yield return null;
        }
        SetPressPlateCompression(1f);

        if (_filtrateChannel != null) _filtrateChannel.SetActive(true);
        if (_hud != null) _hud.ShowNotifPublic("DEWATERING: tekanan naik, filtrat keluar ke WWTP, moisture cake turun menuju <25%.", 7f);
        float t = 0f;
        while (t < _pressDuration)
        {
            t += Time.deltaTime; float e = Smooth(Mathf.Clamp01(t / _pressDuration));
            _moisture = Mathf.Lerp(MoistStart, MoistTarget, e);
            yield return null;
        }
        _moisture = MoistTarget;
        if (_filtrateChannel != null) _filtrateChannel.SetActive(false);

        if (_hud != null) _hud.ShowNotifPublic("FILTRASI SELESAI. Plate membuka berurutan dan cake jatuh ke conveyor.", 6f);
        int cakeCount = _cakeBlocks != null ? _cakeBlocks.Length : 0;
        float openDuration = Mathf.Max(2.4f, cakeCount * 0.32f);
        float openTime = 0f;
        int shown = 0;
        while (openTime < openDuration)
        {
            openTime += Time.deltaTime;
            float p = Smooth(openTime / openDuration);
            SetPressPlateCompression(1f - p);
            int want = Mathf.RoundToInt(p * cakeCount);
            while (shown < want && shown < cakeCount)
            {
                GameObject cake = _cakeBlocks[shown++];
                if (cake != null)
                {
                    cake.SetActive(true);
                    Tint(cake, new Color(0.35f, 0.27f, 0.20f));
                }
            }
            AnimateRollers();
            yield return null;
        }
        SetPressPlateCompression(0f);

        Stop(_pressAudio);
        PlayAudio(_readyAudio, 0.3f);
        _filterPressDone = true; _stage = 2;
        ShowButton(false);
        yield return MovePlayerToCakeInspection();
        _busy = false;
        if (_hud != null) _hud.ShowNotifPublic("Cake terbentuk (moisture 22%). Dekati cake di conveyor dan lakukan inspeksi visual.", 8f);
        _seq = null;
    }

    private void CapturePressPlateLayout()
    {
        if (_pressPlates == null || _pressPlates.Length == 0)
            return;
        if (_pressPlateBaseLocal != null && _pressPlateBaseLocal.Length == _pressPlates.Length)
            return;

        _pressPlateBaseLocal = new Vector3[_pressPlates.Length];
        for (int i = 0; i < _pressPlates.Length; i++)
            _pressPlateBaseLocal[i] = _pressPlates[i] != null ? _pressPlates[i].localPosition : Vector3.zero;
    }

    private void SetPressPlateCompression(float amount)
    {
        if (_pressPlates == null || _pressPlateBaseLocal == null || _pressPlates.Length == 0)
            return;

        Vector3 min = _pressPlateBaseLocal[0];
        Vector3 max = min;
        Vector3 center = Vector3.zero;
        for (int i = 0; i < _pressPlateBaseLocal.Length; i++)
        {
            min = Vector3.Min(min, _pressPlateBaseLocal[i]);
            max = Vector3.Max(max, _pressPlateBaseLocal[i]);
            center += _pressPlateBaseLocal[i];
        }
        center /= _pressPlateBaseLocal.Length;
        Vector3 range = max - min;
        int axis = range.x >= range.y && range.x >= range.z ? 0 : range.y >= range.z ? 1 : 2;

        for (int i = 0; i < _pressPlates.Length; i++)
        {
            if (_pressPlates[i] == null) continue;
            Vector3 open = _pressPlateBaseLocal[i];
            Vector3 closed = open;
            if (axis == 0) closed.x = Mathf.Lerp(open.x, center.x, 0.52f);
            else if (axis == 1) closed.y = Mathf.Lerp(open.y, center.y, 0.52f);
            else closed.z = Mathf.Lerp(open.z, center.z, 0.52f);
            _pressPlates[i].localPosition = Vector3.Lerp(open, closed, Mathf.Clamp01(amount));
        }
    }

    private IEnumerator MovePlayerToCakeInspection()
    {
        Transform cake = ResolveCakeInspectionTarget();
        if (cake == null)
            yield break;

        if (_hud != null) _hud.PlayManualFade(_fadeDuration);
        yield return new WaitForSeconds(_fadeDuration * 0.5f);
        Vector3 target = cake.position;
        Vector3 away = new Vector3(-1f, 0f, -1f).normalized;
        Vector3 stand = target + away * 4.5f;
        stand.y = 0.10f;
        Vector3 forward = target - stand;
        forward.y = 0f;
        TeleportTo(stand, forward, false);
        yield return new WaitForSeconds(_fadeDuration * 0.5f);
    }

    private Transform ResolveCakeInspectionTarget()
    {
        if (_cakeBlocks != null)
        {
            foreach (GameObject cake in _cakeBlocks)
                if (cake != null && cake.activeInHierarchy)
                    return cake.transform;
        }
        return FindChild("Cake_On_Conveyor") ?? FindChild("Cake_Transfer_Conveyor");
    }

    private void AnimateRollers()
    {
        if (_rollers == null) return;
        float d = 300f * Time.deltaTime;
        foreach (var r in _rollers) if (r != null) r.Rotate(Vector3.forward, d, Space.Self);
    }

    private void UpdatePhNeedle()
    {
        if (_phNeedle == null) return;
        // pH 0..14 -> -80..+80 deg pada sumbu lokal Z
        float ang = Mathf.Lerp(-80f, 80f, Mathf.Clamp01(_pHCurrent / 14f));
        var e = _phNeedle.localEulerAngles; _phNeedle.localEulerAngles = new Vector3(e.x, e.y, ang);
    }

    // ============================================================ INSPEKSI
    private void UpdateInspectProximity()
    {
        Transform cake = ResolveCakeInspectionTarget();
        Vector3 target = cake != null ? cake.position : new Vector3(14.58f, 1.67f, 146.12f);
        Vector3 head = GetPlayerHead();
        if (Vector2.Distance(new Vector2(head.x, head.z), new Vector2(target.x, target.z)) <= _inspectRadius)
        {
            _inspected = true; _stage = 3; PlayAudio(_readyAudio, 0.3f);
            if (_hud != null) _hud.ShowNotifPublic("Inspeksi cake OK. Tekan [L] untuk COMPLIANCE QC (cek pH/moisture/filtrat).", 7f);
        }
    }

    // ============================================================ COMPLIANCE QC
    private void ShowQc()
    {
        Vector3 head = GetPlayerHead();
        Transform cam = GetCam();
        Vector3 fwd = cam != null ? cam.forward : Vector3.forward; fwd.y = 0f; fwd.Normalize();
        Vector3 pos = head + fwd * 1.9f; pos.y = head.y - 0.05f;

        _qcCanvas = GameObject.CreatePrimitive(PrimitiveType.Quad);
        _qcCanvas.name = "L11_Tailing_ComplianceQC";
        Object.Destroy(_qcCanvas.GetComponent<Collider>());
        _qcCanvas.transform.position = pos; _qcCanvas.transform.localScale = new Vector3(1.8f, 1.1f, 1f);
        _qcCanvas.GetComponent<Renderer>().sharedMaterial = OpaqueMat(new Color(0.06f, 0.10f, 0.08f));

        var txt = MakeText(_qcCanvas.transform, new Vector3(0f, 0f, -0.02f), 0.05f, TextAnchor.MiddleCenter, new Color(0.85f, 1f, 0.9f));
        txt.text =
            "=== COMPLIANCE QC - TAILING DISCHARGE ===\n" +
            "pH tailing netral : 8.2   (baku mutu 6-9)\n" +
            "Moisture cake     : 22 %  (< 25% -> dry-stack OK)\n" +
            "Filtrat -> WWTP   : jernih, TSS rendah\n" +
            "Logam berat (Fe/Mn/Cr) : di bawah baku mutu\n" +
            "Gypsum + residu stabil | beacon HIJAU\n" +
            "VERDICT: AMAN LINGKUNGAN - cake siap ke DRY STACK";

        var btn = GameObject.CreatePrimitive(PrimitiveType.Cube);
        btn.name = "L11_QC_Accept";
        btn.transform.SetParent(_qcCanvas.transform, false);
        btn.transform.localPosition = new Vector3(0f, -0.42f, -0.05f);
        btn.transform.localScale = new Vector3(0.42f, 0.16f, 0.06f);
        btn.GetComponent<Renderer>().sharedMaterial = OpaqueMat(new Color(0.15f, 0.55f, 0.25f));
        var bt = MakeText(btn.transform, new Vector3(0f, 0f, -0.6f), 0.16f, TextAnchor.MiddleCenter, Color.white);
        bt.text = "ACCEPT [Enter]";
        StartCoroutine(AttachXrButton(btn, OnAccept));
        _pendingClick = OnAccept;
        BillboardTo(_qcCanvas.transform, head);
    }

    private void OnAccept()
    {
        if (_complianceAccepted) return;
        _complianceAccepted = true; _stage = 4; _questComplete = true;
        HideQc();
        _glm?.NotifyLevel12TailingFilterComplete();
        if (_hud != null) _hud.ShowNotifPublic("Tailing lulus compliance. Lapor HT (tahan T): 'limbah dialirkan'.", 8f);
    }

    private void HideQc() { if (_qcCanvas != null) { Object.Destroy(_qcCanvas); _qcCanvas = null; } _pendingClick = null; }

    // ============================================================ OPERATOR STATION
    private void BuildOperatorStation()
    {
        if (_btn != null) return;
        Vector3 consolePos = new Vector3(28f, 2.85f, 142.6f);
        _btn = GameObject.CreatePrimitive(PrimitiveType.Cube);
        _btn.name = "L11_ActionButton";
        _btn.transform.SetParent(transform, false);
        _btn.transform.position = consolePos;
        _btn.transform.localScale = new Vector3(0.7f, 0.3f, 0.16f);
        _btn.GetComponent<Renderer>().sharedMaterial = OpaqueMat(new Color(0.9f, 0.55f, 0.1f));
        _btnLabel = MakeText(_btn.transform, new Vector3(0f, 0f, -0.6f), 0.11f, TextAnchor.MiddleCenter, Color.black);
        _btnLabel.text = "MULAI";
        StartCoroutine(AttachXrButton(_btn, TryAction));

        _infoPanel = GameObject.CreatePrimitive(PrimitiveType.Quad);
        _infoPanel.name = "L11_InfoPanel";
        Object.Destroy(_infoPanel.GetComponent<Collider>());
        _infoPanel.transform.SetParent(transform, false);
        _infoPanel.transform.position = consolePos + new Vector3(0f, 1.15f, 0.1f);
        _infoPanel.transform.localScale = new Vector3(2.0f, 1.2f, 1f);
        _infoPanel.GetComponent<Renderer>().sharedMaterial = OpaqueMat(new Color(0.05f, 0.09f, 0.07f));
        _infoText = MakeText(_infoPanel.transform, new Vector3(0f, 0f, -0.02f), 0.05f, TextAnchor.MiddleCenter, new Color(0.82f, 1f, 0.9f));
        ShowButton(false); ShowInfo(false);
    }

private void UpdateInfo()
    {
        if (_infoText == null || _infoPanel == null || !_infoPanel.activeSelf) return;
        string body;
        if (_stage == 0)
            body = "LEVEL 11 - TAILING NEUTRALIZATION\n" +
                   (_await == 1
                       ? "AKSI SEKARANG: tahan HT untuk buka inlet tailing CCD.\nAmati slurry asam naik dari dasar tangki."
                       : _await == 2
                           ? "AKSI SEKARANG: tahan HT untuk dosing susu kapur.\nAmati perubahan warna, reaksi, dan pH."
                           : _await == 3
                               ? "AKSI SEKARANG: tahan HT untuk transfer ke filter press.\nInterlock: pH harus 8.0 sebelum dewatering."
                               : "Proses sedang berjalan. Amati indikator dan cairan.") +
                   "\nReaksi: H2SO4 + Ca(OH)2 -> CaSO4 + H2O\nTarget pH lingkungan: 6-9";
        else if (_stage == 1)
            body = "LEVEL 11 - FILTER PRESS CYCLE\n" +
                   "1 PLATE CLOSE -> 2 PUMP/FILTRATE -> 3 PLATE OPEN\n" +
                   "Filtrat jernih menuju WWTP; padatan tertahan sebagai cake.\n" +
                   "Target moisture: 60% -> <25% agar stackable";
        else
            body = "DEWATERING SELESAI\nCake kering -> DRY STACK (aman, anti-jebol)\nFiltrat -> WWTP. Lanjut: inspeksi + compliance QC";
        _infoText.text = body + $"\n--------------------------------\npH : {_pHCurrent:0.0}   |   MOISTURE CAKE : {_moisture:0} %";
        BillboardTo(_infoPanel.transform, GetPlayerHead());
    }

    private void ShowButton(bool on) { if (_btn != null) _btn.SetActive(on); }
    private void ShowInfo(bool on) { if (_infoPanel != null) _infoPanel.SetActive(on); }

    // ============================================================ HELPERS
    private void SetProcessVisuals(bool active)
    {
        if (_limestonePour != null) _limestonePour.SetActive(false);
        if (_filtrateChannel != null) _filtrateChannel.SetActive(false);
        if (_polishedFlow != null) _polishedFlow.SetActive(false);
        SetActive(_phStatusGreen, false); SetActive(_phStatusRed, active);
        SetActive(_beaconGreen, false); SetActive(_beaconRed, active);
        if (_cakeBlocks != null) foreach (var c in _cakeBlocks) SetActive(c, false);
    }

    private void SetActive(GameObject go, bool on) { if (go != null) go.SetActive(on); }
    private float Smooth(float t) { t = Mathf.Clamp01(t); return t * t * (3f - 2f * t); }
    private void PushPH() { _glm?.SetPH(_pHCurrent); }

    private void Tint(GameObject go, Color c)
    {
        if (go == null) return;
        if (_mpb == null) _mpb = new MaterialPropertyBlock();
        foreach (var r in go.GetComponentsInChildren<Renderer>(true))
        { r.GetPropertyBlock(_mpb); _mpb.SetColor(IdBase, c); _mpb.SetColor(IdColor, c); r.SetPropertyBlock(_mpb); }
    }

    private void TeleportTo(Vector3 pos, Vector3 fwd, bool skip)
    {
        if (skip) return;
        if (_playerRigRoot == null) AutoFindReferences();
        if (_playerRigRoot == null) return;
        var xr = _playerRigRoot.GetComponent<XROrigin>();
        if (xr == null) return;
        var cc = _playerRigRoot.GetComponent<CharacterController>();
        bool en = cc != null && cc.enabled; if (en) cc.enabled = false;
        xr.MoveCameraToWorldLocation(pos + Vector3.up * xr.CameraYOffset);
        xr.MatchOriginUpCameraForward(Vector3.up, fwd.sqrMagnitude > 0.001f ? fwd : Vector3.forward);
        if (en) cc.enabled = true;
    }

    private Transform GetCam() { var xr = _playerRigRoot != null ? _playerRigRoot.GetComponent<XROrigin>() : null; return xr != null && xr.Camera != null ? xr.Camera.transform : (Camera.main != null ? Camera.main.transform : null); }
    private Vector3 GetPlayerHead() { var c = GetCam(); return c != null ? c.position : new Vector3(28f, 3.1f, 140f); }
    private void BillboardTo(Transform t, Vector3 head) { if (t == null) return; Vector3 d = t.position - head; d.y = 0f; if (d.sqrMagnitude > 0.001f) t.rotation = Quaternion.LookRotation(d.normalized, Vector3.up); }

    private TextMesh MakeText(Transform parent, Vector3 local, float size, TextAnchor anchor, Color col)
    {
        var go = new GameObject("Txt");
        go.transform.SetParent(parent, false);
        go.transform.localPosition = local; go.transform.localRotation = Quaternion.identity;
        go.transform.localScale = Vector3.one * (1f / Mathf.Max(0.01f, parent.lossyScale.x));
        var tm = go.AddComponent<TextMesh>();
        tm.fontSize = 64; tm.characterSize = size; tm.anchor = anchor; tm.alignment = TextAlignment.Center; tm.color = col;
        var font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf") ?? Resources.GetBuiltinResource<Font>("Arial.ttf");
        if (font != null) { tm.font = font; var mr = go.GetComponent<MeshRenderer>(); if (mr != null) mr.sharedMaterial = font.material; }
        return tm;
    }

    private Material OpaqueMat(Color c)
    {
        var sh = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard");
        var m = new Material(sh); m.color = c; if (m.HasProperty(IdBase)) m.SetColor(IdBase, c); return m;
    }

    private IEnumerator AttachXrButton(GameObject go, System.Action onClick)
    {
        yield return null;
        var bc = go.GetComponent<BoxCollider>() ?? go.AddComponent<BoxCollider>(); bc.isTrigger = false;
        var si = go.GetComponent<UnityEngine.XR.Interaction.Toolkit.Interactables.XRSimpleInteractable>()
              ?? go.AddComponent<UnityEngine.XR.Interaction.Toolkit.Interactables.XRSimpleInteractable>();
        si.colliders.Clear(); si.colliders.Add(bc); si.enabled = false; si.enabled = true;
        si.selectEntered.AddListener(_ => onClick());
        si.hoverEntered.AddListener(_ => { _pendingClick = onClick; });
    }

    private void EnsureAudio()
    {
        if (_agitatorAudio == null) _agitatorAudio = MakeAudio("L11_AgitatorAudio", true, 0.25f, GenNoise(3f, 55f, 1301));
        if (_pressAudio == null) _pressAudio = MakeAudio("L11_PressAudio", true, 0f, GenNoise(2f, 90f, 1302));
        if (_readyAudio == null) _readyAudio = MakeAudio("L11_ReadyAudio", false, 0f, GenChime(1f, 1303));
    }
    private AudioSource MakeAudio(string n, bool loop, float vol, AudioClip clip)
    { var go = new GameObject(n); go.transform.SetParent(transform, false); var a = go.AddComponent<AudioSource>(); a.loop = loop; a.playOnAwake = false; a.spatialBlend = 0.2f; a.volume = vol; a.clip = clip; return a; }
    private void PlayAudio(AudioSource s, float v) { if (s == null) return; s.volume = v; if (!s.isPlaying) s.Play(); }
    private void Stop(AudioSource s) { if (s != null && s.isPlaying) s.Stop(); }
    private AudioClip GenNoise(float dur, float hz, int seed)
    {
        int sr = 22050, n = Mathf.CeilToInt(dur * sr); var d = new float[n]; var r = new System.Random(seed); float ph = 0f, f = 0f;
        for (int i = 0; i < n; i++) { ph += 2f * Mathf.PI * hz / sr; float mo = Mathf.Sin(ph) * 0.3f; float no = ((float)r.NextDouble() - 0.5f) * 0.25f; f += 0.05f * (no - f); d[i] = (mo + f) * 0.4f; }
        var c = AudioClip.Create("n" + seed, n, 1, sr, false); c.SetData(d, 0); return c;
    }
    private AudioClip GenChime(float dur, int seed)
    {
        int sr = 22050, n = Mathf.CeilToInt(dur * sr); var d = new float[n];
        for (int i = 0; i < n; i++) { float t = (float)i / sr; float env = Mathf.Clamp01(1f - t / dur); d[i] = (Mathf.Sin(2 * Mathf.PI * 450 * t) * 0.2f + Mathf.Sin(2 * Mathf.PI * 675 * t) * 0.13f) * env; }
        var c = AudioClip.Create("c" + seed, n, 1, sr, false); c.SetData(d, 0); return c;
    }

    private void AutoFindReferences()
    {
        if (_playerRigRoot == null)
        {
            var rig = GameObject.Find("XR Origin (XR Rig)") ?? GameObject.Find("XR Origin") ?? GameObject.Find("XR Rig") ?? GameObject.FindWithTag("Player");
            if (rig != null) _playerRigRoot = rig.transform;
        }
        if (_teleportTargetDcs == null) { var g = GameObject.Find("SpawnPoint_DCS"); if (g != null) _teleportTargetDcs = g.transform; }
        if (_rig == null) _rig = GameObject.Find("Level12_13_Tailing_IndustrialUV_BlenderRig_V3")
            ?? GameObject.Find("Final_FilterPress_Unit")
            ?? GameObject.Find("Level13_DryStack_BlenderRig");
        if (_rig == null) return;
        if (_agitatorRoot == null) { var t = FindChild("Polishing_Agitator_Root"); if (t != null) _agitatorRoot = t; }
        if (_limestonePour == null) _limestonePour = Child("Limestone_Pour_Stream");
        if (_neutralizedSurface == null) _neutralizedSurface = Child("Neutralized_Surface");
        if (_filtrateChannel == null) _filtrateChannel = Child("Filtrate_Channel");
        if (_polishedFlow == null) _polishedFlow = Child("Polished_Tailing_Flow");
        if (_phNeedle == null) { var t = FindChild("pH_Monitor_Needle"); if (t != null) _phNeedle = t; }
        if (_phStatusGreen == null) _phStatusGreen = Child("pH_Status_Green");
        if (_phStatusRed == null) _phStatusRed = Child("pH_Status_Red");
        if (_beaconGreen == null) _beaconGreen = Child("Environmental_Beacon_Green");
        if (_beaconRed == null) _beaconRed = Child("Environmental_Beacon_Red");
        if (_cakeBlocks == null || _cakeBlocks.Length == 0)
        {
            var list = new List<GameObject>();
            foreach (Transform t in _rig.GetComponentsInChildren<Transform>(true)) if (t.name.StartsWith("Cake_Block_")) list.Add(t.gameObject);
            _cakeBlocks = list.ToArray();
        }
        if (_rollers == null || _rollers.Length == 0)
        {
            var list = new List<Transform>();
            foreach (Transform t in _rig.GetComponentsInChildren<Transform>(true)) if (t.name.StartsWith("Conveyor_Roller_")) list.Add(t);
            _rollers = list.ToArray();
        }
        if (_pressPlates == null || _pressPlates.Length == 0)
        {
            var list = new List<Transform>();
            foreach (Transform t in _rig.GetComponentsInChildren<Transform>(true))
                if (t.name.StartsWith("PressPlate_")
                    || t.name.Contains("FilterPlate_")) list.Add(t);
            list.Sort((a, b) => string.CompareOrdinal(a.name, b.name));
            _pressPlates = list.ToArray();
            _pressPlateBaseLocal = null;
        }
    }

    private void ProtectTailingEquipmentFromOcclusion()
    {
        if (_rig == null)
            return;

        foreach (Transform t in _rig.GetComponentsInChildren<Transform>(true))
        {
            string n = t.name;
            bool critical = n.Contains("Cake_On_Conveyor")
                || n.StartsWith("Cake_Block_")
                || n.StartsWith("PressPlate_")
                || n.Contains("FilterPlate_")
                || n.StartsWith("Conveyor_Roller_")
                || n.Contains("Filtrate")
                || n.Contains("FilterPress");
            if (!critical)
                continue;

            if (n == "Cake_On_Conveyor")
                t.gameObject.SetActive(true);

            foreach (Renderer renderer in t.GetComponentsInChildren<Renderer>(true))
            {
                renderer.enabled = true;
                renderer.forceRenderingOff = false;
                renderer.allowOcclusionWhenDynamic = false;
#if UNITY_EDITOR
                var flags = UnityEditor.GameObjectUtility.GetStaticEditorFlags(renderer.gameObject);
                flags &= ~(UnityEditor.StaticEditorFlags.OccludeeStatic | UnityEditor.StaticEditorFlags.OccluderStatic);
                UnityEditor.GameObjectUtility.SetStaticEditorFlags(renderer.gameObject, flags);
#endif
            }
        }
    }

    private GameObject Child(string name) { var t = FindChild(name); return t != null ? t.gameObject : null; }
    private Transform FindChild(string name)
    {
        if (_rig == null) return null;
        foreach (Transform t in _rig.GetComponentsInChildren<Transform>(true)) if (t.name == name) return t;
        return null;
    }
}
