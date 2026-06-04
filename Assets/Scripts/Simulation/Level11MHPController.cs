using System.Collections;
using System.Collections.Generic;
using Unity.XR.CoreUtils;
using UnityEngine;

/// <summary>
/// OLIVIA VR - Level11MHPController.cs  (Display "Level 10" — Pemurnian/MHP)
///
/// Gameplay INTERAKTIF & INFORMATIF (HPAL nikel):
/// PLS jernih dari CCD dimurnikan 3 tahap dosing reagen oleh operator, lalu MHP
/// (Mixed Hydroxide Precipitate, Ni-Co) di-sampling + lulus Lab QC, lalu lapor HT.
///   Tahap 1 PRA-NETRALISASI  : LIMESTONE (CaCO3)  pH 1.5->3.5  buang Fe/Al (endapan coklat)
///   Tahap 2 POLISHING        : KAPUR Ca(OH)2      pH 3.5->5.0  buang Al/Cr/sisa Fe
///   Tahap 3 PRESIPITASI MHP  : MAGNESIA MgO        pH 5.0->7.5  endap Ni(OH)2+Co(OH)2 (hijau)
/// Operator menekan tombol dosing tiap tahap (XR ray/poke ATAU keyboard SPACE/1),
/// pH naik live di panel info, larutan berubah warna + skid dosing beranimasi.
/// Lalu jalan ke stasiun sampling (proximity) -> Lab QC pop-up assay Ni/Co -> ACCEPT -> lapor HT.
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
    [SerializeField] private float _doseDuration = 5f;
    [SerializeField] private float _agitatorRpm = 35f;
    [SerializeField] private float _sampleRadius = 3.2f;

    [Header("=== Audio ===")]
    [SerializeField] private AudioSource _agitatorAudio;
    [SerializeField] private AudioSource _doseAudio;
    [SerializeField] private AudioSource _readyAudio;

    // ---- Stage data (research-accurate) ----
    private class Stage
    {
        public string reagent, formula, reaction, removes;
        public float pHFrom, pHTo;
        public Color liquidColor;
        public string skidPrefix; // real dosing skid in machine to animate
    }
    private readonly Stage[] _stages = new[]
    {
        new Stage{ reagent="LIMESTONE (CaCO3)", formula="CaCO3", removes="Fe3+ / Al3+ (endapan coklat + gypsum CaSO4)",
            reaction="Fe2(SO4)3 + 3CaCO3 + 3H2O -> 2Fe(OH)3 + 3CaSO4 + 3CO2", pHFrom=1.5f, pHTo=3.5f,
            liquidColor=new Color(0.45f,0.27f,0.12f), skidPrefix="Neutralization_Reagent_Dosing_Skid" },
        new Stage{ reagent="KAPUR / SLAKED LIME (Ca(OH)2)", formula="Ca(OH)2", removes="Al, Cr, sisa Fe (polishing)",
            reaction="2Al3+ + 3Ca(OH)2 -> 2Al(OH)3 + 3Ca2+   (impurity removal)", pHFrom=3.5f, pHTo=5.0f,
            liquidColor=new Color(0.34f,0.45f,0.42f), skidPrefix="Lime_Dosing_Skid" },
        new Stage{ reagent="MAGNESIA (MgO) slurry", formula="MgO", removes="Ni & Co diendapkan jadi MHP (hijau)",
            reaction="NiSO4 + MgO + H2O -> Ni(OH)2(s) + MgSO4   (Co serupa)", pHFrom=5.0f, pHTo=7.5f,
            liquidColor=new Color(0.18f,0.62f,0.40f), skidPrefix="MGO_Dosing_Skid" },
    };

    private PlayerHUD _hud;
    private GameLevelManager _glm;
    private Coroutine _seq;
    private bool _levelActive, _processStarted;
    private int _stageIndex;          // 0..2 = dosing, 3 = sampling, 4 = lab, 5 = report
    private bool _dosing;
    private float _pHCurrent = 1.5f, _mhpQuality;
    private bool _stage1, _stage2, _stage3, _sampleTaken, _labAccepted, _questComplete;

    private readonly List<Transform> _skidMotors = new List<Transform>();
    private GameObject _doseButton; private TextMesh _doseLabel;
    private GameObject _infoPanel; private TextMesh _infoText;
    private GameObject _labCanvas; private TextMesh _labText;
    private System.Action _pendingClick;
    private MaterialPropertyBlock _mpb;
    private static readonly int IdBase = Shader.PropertyToID("_BaseColor");
    private static readonly int IdColor = Shader.PropertyToID("_Color");
    private float _doseProgress;
    private GameObject _warehouseRig, _warehouseFloor; private Transform _baggingHeap; private Transform[] _exportHeaps;
    private GameObject _dispatchButton; private TextMesh _dispatchLabel;
    private GameObject _whPanel; private TextMesh _whText;
    private GameObject _weighDisplay; private TextMesh _weighText;
    private GameObject _fillStream; private Material _fillStreamMat;
    private bool _warehouseStarted, _dispatching, _baggingDone;
    private float _dispatchProgress; private float _dispatchDuration = 8f;
    private Vector3 _baggingHeapBase; private Vector3[] _exportHeapBase;

    // ---- Public props for HUD ----
    public bool LevelActive => _levelActive;
    public bool Stage1Done => _stage1;
    public bool Stage2Done => _stage2;
    public bool Stage3Done => _stage3;
    public bool SampleTaken => _sampleTaken;
    public bool LabAccepted => _labAccepted;
    public bool QuestComplete => _questComplete;
    public float PHCurrent => _pHCurrent;
    public float MHPQualityCurrent => _mhpQuality;
    public bool BaggingDone => _baggingDone;
    public float DispatchProgress => _dispatchProgress;

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
    }

    private void OnDisable()
    {
        GameLevelManager.OnLevelStarted -= OnLevelStarted;
        GameLevelManager.OnDCSButtonPressed -= OnDcsButtonPressed;
        if (_seq != null) StopCoroutine(_seq);
        Stop(_agitatorAudio); Stop(_doseAudio);
    }

    private void OnLevelStarted(GameLevelManager.GameLevel level)
    {
        _levelActive = level == GameLevelManager.GameLevel.Level11_MHP;
        if (!_levelActive) { SetProcessVisuals(false); ShowDoseButton(false); ShowInfoPanel(false); HideLab(); Stop(_agitatorAudio); HideDispatchStation(); SetFillStream(false); RestoreWarehouseHeaps(); return; }

        _glm = GameLevelManager.Instance;
        _processStarted = false; _stageIndex = 0; _dosing = false;
        _pHCurrent = _stages[0].pHFrom; _mhpQuality = 0f;
        _stage1 = _stage2 = _stage3 = _sampleTaken = _labAccepted = _questComplete = false;
        _warehouseStarted = _dispatching = _baggingDone = false; _dispatchProgress = 0f; HideDispatchStation(); SetFillStream(false); EnsureWarehouseRefs(); RestoreWarehouseHeaps();
        PushPH(); SetProcessVisuals(false); ShowDoseButton(false); ShowInfoPanel(false); HideLab();
        if (_hud != null) _hud.ShowNotifPublic("Level 10: Larutan PLS dari CCD masuk pemurnian. Tekan DCS 10 untuk mulai.");
        TeleportPlayer(_teleportTargetDcs);
    }

    private void OnDcsButtonPressed(int number)
    {
        if (!_levelActive || number != 10 || _processStarted) return;
        _processStarted = true;
        _seq = StartCoroutine(StartFieldSequence());
    }

    private IEnumerator StartFieldSequence()
    {
        if (_hud != null) _hud.PlayManualFade(_fadeDuration);
        yield return new WaitForSeconds(_fadeDuration * 0.5f);
        TeleportPlayer(_teleportTargetField);
        yield return new WaitForSeconds(_fadeDuration * 0.5f + 0.5f);

        SetProcessVisuals(true);
        if (_feedLiquid != null) _feedLiquid.SetActive(true);
        PlayAudio(_agitatorAudio, 0.34f);
        BuildOperatorStation();
        _stageIndex = 0;
        BeginDoseStage();
        _seq = null;
    }

    private void Update()
    {
        if (!_levelActive || !_processStarted) return;
        AnimateAgitators();
        if (_dosing) AnimateSkid();

        // Dosing input (keyboard fallback): SPACE atau 1
        if (!_dosing && _stageIndex <= 2 && (Input.GetKeyDown(KeyCode.Space) || Input.GetKeyDown(KeyCode.Alpha1)))
            TryDose();

        // Sampling: proximity ke stasiun sampling
        if (_stageIndex == 3 && !_sampleTaken) UpdateSamplingProximity();

        // Lab submit (L) + Accept (Enter) fallback
        if (_stageIndex == 4 && !_labAccepted)
        {
            if (_labCanvas == null && Input.GetKeyDown(KeyCode.L)) ShowLabCanvas();
            if (_labCanvas != null && _labCanvas.activeSelf && _pendingClick != null && Input.GetKeyDown(KeyCode.Return))
            { var a = _pendingClick; _pendingClick = null; a(); }
        }
        if (_stageIndex == 5 && !_baggingDone && !_dispatching && (Input.GetKeyDown(KeyCode.Space) || Input.GetKeyDown(KeyCode.Alpha1)))
            TryDispatch();
        if (_dispatching) UpdateFillStream();
        if (_warehouseStarted) UpdateWarehousePanel();
        UpdateInfoPanel();
    }

    // ============================================================ DOSING
    private void BeginDoseStage()
    {
        var s = _stages[_stageIndex];
        _pHCurrent = s.pHFrom; PushPH();
        ShowInfoPanel(true);
        ShowDoseButton(true);
        if (_doseLabel != null) _doseLabel.text = $"DOSING {_stages[_stageIndex].formula}\n[ tekan / SPACE ]";
        if (_hud != null) _hud.ShowNotifPublic($"TAHAP {_stageIndex + 1}: tambahkan {s.reagent}. Tekan tombol DOSING (atau SPACE).", 6f);
    }

    private void TryDose()
    {
        if (_dosing || _stageIndex > 2) return;
        _dosing = true;
        ShowDoseButton(false);
        _seq = StartCoroutine(DoseRoutine(_stageIndex));
    }

    private IEnumerator DoseRoutine(int idx)
    {
        var s = _stages[idx];
        PlayAudio(_doseAudio, 0.4f);
        if (_reagentLiquid != null) _reagentLiquid.SetActive(true);
        float t = 0f;
        while (t < _doseDuration)
        {
            t += Time.deltaTime; _doseProgress = Mathf.Clamp01(t / _doseDuration);
            float e = Smooth(_doseProgress);
            _pHCurrent = Mathf.Lerp(s.pHFrom, s.pHTo, e);
            PushPH();
            TintStageLiquid(idx, e);
            if (idx == 2) _mhpQuality = Mathf.Lerp(0f, 92f, e);
            UpdateFx(idx, e);
            yield return null;
        }
        _pHCurrent = s.pHTo; PushPH(); Stop(_doseAudio); _dosing = false;

        if (idx == 0) { _stage1 = true; if (_neutralToPolishLiquid != null) _neutralToPolishLiquid.SetActive(true); }
        else if (idx == 1) { _stage2 = true; if (_polishToMhpLiquid != null) _polishToMhpLiquid.SetActive(true); }
        else if (idx == 2)
        {
            _stage3 = true; _mhpQuality = 92f;
            if (_mhpSampleFlow != null) _mhpSampleFlow.SetActive(true);
            PlayAudio(_readyAudio, 0.32f);
        }

        if (idx < 2) { _stageIndex = idx + 1; BeginDoseStage(); }
        else
        {
            _stageIndex = 3; ShowDoseButton(false);
            if (_hud != null) _hud.ShowNotifPublic("MHP terbentuk (hijau). Jalan ke STASIUN SAMPLING (depan kanan) untuk ambil sampel.", 7f);
        }
        _seq = null;
    }

    private void TintStageLiquid(int idx, float t)
    {
        Color from = idx == 0 ? new Color(0.55f, 0.50f, 0.20f) : _stages[idx - 1].liquidColor;
        Color c = Color.Lerp(from, _stages[idx].liquidColor, t);
        Tint(_feedLiquid, c); Tint(_reagentLiquid, c);
        if (idx >= 1) Tint(_neutralToPolishLiquid, c);
        if (idx >= 2) { Tint(_polishToMhpLiquid, c); Tint(_mhpSampleFlow, c); }
        var ls = FindChild(_mhpField ? _mhpField.transform : null, "Liquid_Surface");
        if (ls != null) Tint(ls.gameObject, c);
    }

    private void Tint(GameObject go, Color c)
    {
        if (go == null) return;
        if (_mpb == null) _mpb = new MaterialPropertyBlock();
        foreach (var r in go.GetComponentsInChildren<Renderer>(true))
        {
            r.GetPropertyBlock(_mpb);
            _mpb.SetColor(IdBase, c); _mpb.SetColor(IdColor, c);
            r.SetPropertyBlock(_mpb);
        }
    }

    private void AnimateSkid()
    {
        float dt = Time.deltaTime;
        foreach (var m in _skidMotors) if (m != null) m.Rotate(Vector3.up, 360f * dt, Space.Self);
    }

    private void ResolveSkidMotors(int idx)
    {
        _skidMotors.Clear();
        if (_mhpField == null) return;
        string p = _stages[idx].skidPrefix;
        foreach (Transform t in _mhpField.GetComponentsInChildren<Transform>(true))
            if (t.name.StartsWith(p) && (t.name.Contains("Feeder") || t.name.Contains("Screw") || t.name.Contains("DosingPump") || t.name.Contains("Mix")))
                _skidMotors.Add(t);
    }

    // ============================================================ SAMPLING
    private void UpdateSamplingProximity()
    {
        var cup = FindChild(_mhpField ? _mhpField.transform : null, "MHP_Sample_Cup")
               ?? FindChild(_mhpField ? _mhpField.transform : null, "MHP_Sample_Line");
        Vector3 target = cup != null ? cup.position : new Vector3(78.56f, 1.05f, 100.91f);
        Vector3 head = GetPlayerHead();
        Vector2 a = new Vector2(head.x, head.z), b = new Vector2(target.x, target.z);
        if (Vector2.Distance(a, b) <= _sampleRadius)
        {
            _sampleTaken = true;
            if (_mhpSampleProduct != null) { _mhpSampleProduct.SetActive(true); Tint(_mhpSampleProduct, new Color(0.15f, 0.6f, 0.38f)); }
            PlayAudio(_readyAudio, 0.3f);
            _stageIndex = 4;
            if (_hud != null) _hud.ShowNotifPublic("Sampel MHP diambil. Tekan [L] untuk submit ke LAB QC (assay Ni/Co).", 7f);
        }
    }

    // ============================================================ LAB QC
    private void ShowLabCanvas()
    {
        Vector3 head = GetPlayerHead();
        Transform cam = GetCam();
        Vector3 fwd = cam != null ? cam.forward : Vector3.forward; fwd.y = 0f; fwd.Normalize();
        Vector3 pos = head + fwd * 1.9f; pos.y = head.y - 0.05f;

        _labCanvas = GameObject.CreatePrimitive(PrimitiveType.Quad);
        _labCanvas.name = "L10_MHP_LabQC";
        Object.Destroy(_labCanvas.GetComponent<Collider>());
        _labCanvas.transform.position = pos;
        _labCanvas.transform.localScale = new Vector3(1.7f, 1.05f, 1f);
        var qr = _labCanvas.GetComponent<Renderer>();
        qr.sharedMaterial = OpaqueMat(new Color(0.05f, 0.09f, 0.13f));

        _labText = MakeText(_labCanvas.transform, new Vector3(0f, 0f, -0.02f), 0.052f, TextAnchor.MiddleCenter, new Color(0.85f, 1f, 0.9f));
        _labText.text =
            "=== LAB QC — MIXED HYDROXIDE PRECIPITATE ===\n" +
            "pH akhir presipitasi : 7.5  (window 6.0-8.4)\n" +
            "Ni grade (kering)    : 41 %    Co grade : 3.6 %\n" +
            "Ni recovery 94 %     Co recovery 92 %\n" +
            "Fe / Al / Cr         : < 0.1 % (sudah dibuang)\n" +
            "Mn ditekan (N2 sparge) | Moisture cake ~48 %\n" +
            "VERDICT: DALAM SOP — MHP siap ke refinery (baterai EV)";

        var btn = GameObject.CreatePrimitive(PrimitiveType.Cube);
        btn.name = "L10_Lab_Accept";
        btn.transform.SetParent(_labCanvas.transform, false);
        btn.transform.localPosition = new Vector3(0f, -0.42f, -0.05f);
        btn.transform.localScale = new Vector3(0.42f, 0.16f, 0.06f);
        btn.GetComponent<Renderer>().sharedMaterial = OpaqueMat(new Color(0.15f, 0.55f, 0.25f));
        var bt = MakeText(btn.transform, new Vector3(0f, 0f, -0.6f), 0.16f, TextAnchor.MiddleCenter, Color.white);
        bt.text = "ACCEPT [Enter]";
        StartCoroutine(AttachXrButton(btn, OnLabAccept));
        _pendingClick = OnLabAccept;
        BillboardTo(_labCanvas.transform, head);
    }

    private void OnLabAccept()
    {
        if (_labAccepted) return;
        _labAccepted = true; _stageIndex = 5;
        HideLab();
        if (_hud != null) _hud.ShowNotifPublic("MHP lulus QC. Mengangkut produk ke GUDANG untuk bagging & dispatch...", 6f);
        _seq = StartCoroutine(StartWarehouseSequence());
    }

    private void HideLab()
    {
        if (_labCanvas != null) { Object.Destroy(_labCanvas); _labCanvas = null; }
        _pendingClick = null;
    }
    // ============================================================ STAGE AKHIR: GUDANG PRODUK MHP
    private IEnumerator StartWarehouseSequence()
    {
        ShowDoseButton(false); ShowInfoPanel(false); Stop(_agitatorAudio);
        EnsureWarehouseRefs();
        if (_hud != null) _hud.PlayManualFade(_fadeDuration);
        yield return new WaitForSeconds(_fadeDuration * 0.5f);
        EnsureWarehouseGround();
        TeleportTo(new Vector3(104f, 2.0f, 150f), new Vector3(0f, 0f, 1f));
        yield return new WaitForSeconds(_fadeDuration * 0.5f + 0.5f);
        ResetWarehouseHeaps();
        BuildDispatchStation();
        _warehouseStarted = true; _stageIndex = 5;
        if (_hud != null) _hud.ShowNotifPublic("TAHAP AKHIR: kemas MHP ke FIBC bulk bag & dispatch ke refinery. Tekan tombol (atau SPACE).", 7f);
        _seq = null;
    }

    private void TryDispatch()
    {
        if (_dispatching || _baggingDone || _stageIndex != 5) return;
        _dispatching = true; ShowDispatchButton(false);
        _seq = StartCoroutine(DispatchRoutine());
    }

    private IEnumerator DispatchRoutine()
    {
        SetFillStream(true); PlayAudio(_doseAudio, 0.32f);
        float t = 0f;
        while (t < _dispatchDuration)
        {
            t += Time.deltaTime;
            _dispatchProgress = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(t / _dispatchDuration)) * 100f;
            ApplyBaggingFill(_dispatchProgress / 100f);
            yield return null;
        }
        _dispatchProgress = 100f; ApplyBaggingFill(1f);
        SetFillStream(false); PlayAudio(_readyAudio, 0.3f); _dispatching = false;
        _baggingDone = true; _stageIndex = 6; _questComplete = true;
        _glm?.NotifyLevel11MHPComplete();
        if (_hud != null) _hud.ShowNotifPublic("Produk MHP dikemas (FIBC) & siap dispatch ke refinery. Lapor HT (tahan T): 'MHP terbentuk'.", 8f);
        _seq = null;
    }

    private void ApplyBaggingFill(float p)
    {
        if (_baggingHeap != null && _baggingHeapBase != Vector3.zero)
        {
            float a = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(p / 0.3f));
            _baggingHeap.localScale = Vector3.Lerp(_baggingHeapBase * 0.04f, _baggingHeapBase, a);
        }
        if (_exportHeaps != null && _exportHeapBase != null)
        {
            int n = _exportHeaps.Length;
            for (int i = 0; i < n; i++)
            {
                if (_exportHeaps[i] == null || i >= _exportHeapBase.Length) continue;
                float lo = 0.3f + 0.65f * i / Mathf.Max(1, n);
                float hi = 0.3f + 0.65f * (i + 1) / Mathf.Max(1, n);
                float e = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01((p - lo) / Mathf.Max(0.0001f, hi - lo)));
                _exportHeaps[i].localScale = Vector3.Lerp(_exportHeapBase[i] * 0.04f, _exportHeapBase[i], e);
            }
        }
        if (_weighText != null) _weighText.text = Mathf.RoundToInt(Mathf.Lerp(0f, 1500f, Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(p / 0.3f)))) + " kg";
    }

    private void ResetWarehouseHeaps()
    {
        if (_baggingHeap != null && _baggingHeapBase != Vector3.zero) _baggingHeap.localScale = _baggingHeapBase * 0.04f;
        if (_exportHeaps != null && _exportHeapBase != null)
            for (int i = 0; i < _exportHeaps.Length; i++) if (_exportHeaps[i] != null && i < _exportHeapBase.Length) _exportHeaps[i].localScale = _exportHeapBase[i] * 0.04f;
    }

    private void RestoreWarehouseHeaps()
    {
        if (_baggingHeap != null && _baggingHeapBase != Vector3.zero) _baggingHeap.localScale = _baggingHeapBase;
        if (_exportHeaps != null && _exportHeapBase != null)
            for (int i = 0; i < _exportHeaps.Length; i++) if (_exportHeaps[i] != null && i < _exportHeapBase.Length) _exportHeaps[i].localScale = _exportHeapBase[i];
    }

    private void UpdateWarehousePanel()
    {
        if (_whText == null || _whPanel == null || !_whPanel.activeSelf) return;
        _whText.text =
            "AREA PRODUK MHP - KEMAS & DISPATCH\n" +
            "Produk: MHP (Ni-Co hidroksida) ~40% Ni, 3-4% Co\n" +
            "Moisture cake ~48% -> dikemas FIBC bulk bag 1-2 ton\n" +
            "Ditimbang -> staging -> dispatch ke REFINERY\n" +
            "--------------------------------\n" +
            "DISPATCH PROGRESS : " + _dispatchProgress.ToString("0") + " %";
        BillboardTo(_whPanel.transform, GetPlayerHead());
    }

    private void SetFillStream(bool on)
    {
        if (!on) { if (_fillStream != null) _fillStream.SetActive(false); return; }
        if (_fillStream == null)
        {
            _fillStream = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            _fillStream.name = "L10_FillStream";
            Object.Destroy(_fillStream.GetComponent<Collider>());
            _fillStream.transform.SetParent(transform, false);
            _fillStream.transform.position = new Vector3(106.41f, 2.95f, 155.01f);
            _fillStream.transform.localScale = new Vector3(0.16f, 0.32f, 0.16f);
            _fillStreamMat = OpaqueMat(new Color(0.25f, 0.55f, 0.32f));
            _fillStreamMat.EnableKeyword("_EMISSION");
            _fillStream.GetComponent<Renderer>().sharedMaterial = _fillStreamMat;
        }
        _fillStream.SetActive(true);
    }

    private void UpdateFillStream()
    {
        if (_fillStreamMat == null) return;
        _fillStreamMat.mainTextureOffset = new Vector2(0f, -Time.time * 1.6f);
        float pulse = 0.6f + 0.25f * Mathf.Sin(Time.time * 6f);
        _fillStreamMat.SetColor("_EmissionColor", new Color(0.2f, 0.7f, 0.35f) * pulse);
    }

    private void EnsureWarehouseGround()
    {
        if (_warehouseFloor == null) return;
        if (_warehouseFloor.GetComponent<Collider>() == null)
        {
            var mf = _warehouseFloor.GetComponentInChildren<MeshFilter>();
            var bc = _warehouseFloor.AddComponent<BoxCollider>();
            if (mf != null && mf.sharedMesh != null) { bc.center = mf.sharedMesh.bounds.center; bc.size = mf.sharedMesh.bounds.size; }
        }
    }

    private void BuildDispatchStation()
    {
        if (_dispatchButton != null) { ShowDispatchButton(true); ShowWhPanel(true); if (_weighDisplay != null) _weighDisplay.SetActive(true); return; }
        Vector3 consolePos = new Vector3(104f, 2.7f, 152.5f);
        _dispatchButton = GameObject.CreatePrimitive(PrimitiveType.Cube);
        _dispatchButton.name = "L10_DispatchButton";
        _dispatchButton.transform.SetParent(transform, false);
        _dispatchButton.transform.position = consolePos;
        _dispatchButton.transform.localScale = new Vector3(0.7f, 0.3f, 0.16f);
        _dispatchButton.GetComponent<Renderer>().sharedMaterial = OpaqueMat(new Color(0.2f, 0.6f, 0.3f));
        _dispatchLabel = MakeText(_dispatchButton.transform, new Vector3(0f, 0f, -0.6f), 0.1f, TextAnchor.MiddleCenter, Color.black);
        _dispatchLabel.text = "BAGGING & DISPATCH\n[ tekan / SPACE ]";
        StartCoroutine(AttachXrButton(_dispatchButton, TryDispatch));

        _whPanel = GameObject.CreatePrimitive(PrimitiveType.Quad);
        _whPanel.name = "L10_WarehousePanel";
        Object.Destroy(_whPanel.GetComponent<Collider>());
        _whPanel.transform.SetParent(transform, false);
        _whPanel.transform.position = consolePos + new Vector3(0f, 1.2f, 0.1f);
        _whPanel.transform.localScale = new Vector3(2.0f, 1.2f, 1f);
        _whPanel.GetComponent<Renderer>().sharedMaterial = OpaqueMat(new Color(0.05f, 0.09f, 0.07f));
        _whText = MakeText(_whPanel.transform, new Vector3(0f, 0f, -0.02f), 0.05f, TextAnchor.MiddleCenter, new Color(0.82f, 1f, 0.9f));

        _weighDisplay = GameObject.CreatePrimitive(PrimitiveType.Quad);
        _weighDisplay.name = "L10_WeighDisplay";
        Object.Destroy(_weighDisplay.GetComponent<Collider>());
        _weighDisplay.transform.SetParent(transform, false);
        _weighDisplay.transform.position = new Vector3(106.41f, 3.95f, 154.2f);
        _weighDisplay.transform.localScale = new Vector3(0.9f, 0.4f, 1f);
        _weighDisplay.GetComponent<Renderer>().sharedMaterial = OpaqueMat(new Color(0.02f, 0.05f, 0.02f));
        _weighText = MakeText(_weighDisplay.transform, new Vector3(0f, 0f, -0.02f), 0.09f, TextAnchor.MiddleCenter, new Color(0.4f, 1f, 0.5f));
        _weighText.text = "0 kg";
    }

    private void ShowDispatchButton(bool on) { if (_dispatchButton != null) _dispatchButton.SetActive(on); }
    private void ShowWhPanel(bool on) { if (_whPanel != null) _whPanel.SetActive(on); }
    private void HideDispatchStation() { ShowDispatchButton(false); ShowWhPanel(false); if (_weighDisplay != null) _weighDisplay.SetActive(false); }

    private void EnsureWarehouseRefs()
    {
        if (_warehouseRig == null) _warehouseRig = GameObject.Find("MHP_ProductWarehouse_BlenderRig");
        if (_warehouseRig == null) return;
        Transform wr = _warehouseRig.transform;
        if (_warehouseFloor == null) _warehouseFloor = Child(wr, "MHP_Yard_Pad");
        if (_baggingHeap == null) _baggingHeap = FindChild(wr, "Bagging_ActiveBag_MHP_TopHeap");
        if (!HasAny(_exportHeaps))
        {
            var list = new List<Transform>();
            foreach (Transform t in wr.GetComponentsInChildren<Transform>(true))
                if (t.name.StartsWith("ExportBag_") && t.name.EndsWith("_MHP_TopHeap")) list.Add(t);
            _exportHeaps = list.ToArray();
        }
        if (_baggingHeap != null && _baggingHeapBase == Vector3.zero) _baggingHeapBase = _baggingHeap.localScale;
        if (_exportHeaps != null && (_exportHeapBase == null || _exportHeapBase.Length != _exportHeaps.Length))
        {
            _exportHeapBase = new Vector3[_exportHeaps.Length];
            for (int i = 0; i < _exportHeaps.Length; i++) _exportHeapBase[i] = _exportHeaps[i] != null ? _exportHeaps[i].localScale : Vector3.one;
        }
    }

    private void TeleportTo(Vector3 pos, Vector3 fwd)
    {
        if (_playerRigRoot == null) return;
        var xr = _playerRigRoot.GetComponent<XROrigin>();
        if (xr == null) return;
        var cc = _playerRigRoot.GetComponent<CharacterController>();
        bool en = cc != null && cc.enabled; if (en) cc.enabled = false;
        xr.MoveCameraToWorldLocation(pos + Vector3.up * xr.CameraYOffset);
        xr.MatchOriginUpCameraForward(Vector3.up, fwd.sqrMagnitude > 0.001f ? fwd : Vector3.forward);
        if (en) cc.enabled = true;
    }


    // ============================================================ INFO PANEL / DOSE BUTTON
    private void BuildOperatorStation()
    {
        if (_doseButton != null) return;
        Vector3 basePos = _teleportTargetField != null ? _teleportTargetField.position : new Vector3(74.11f, 0f, 93.51f);
        Vector3 consolePos = basePos + new Vector3(0f, 1.35f, 2.6f);

        // Dose button (cube)
        _doseButton = GameObject.CreatePrimitive(PrimitiveType.Cube);
        _doseButton.name = "L10_DoseButton";
        _doseButton.transform.SetParent(transform, false);
        _doseButton.transform.position = consolePos;
        _doseButton.transform.localScale = new Vector3(0.6f, 0.28f, 0.16f);
        _doseButton.GetComponent<Renderer>().sharedMaterial = OpaqueMat(new Color(0.9f, 0.55f, 0.1f));
        _doseLabel = MakeText(_doseButton.transform, new Vector3(0f, 0f, -0.6f), 0.12f, TextAnchor.MiddleCenter, Color.black);
        _doseLabel.text = "DOSING";
        StartCoroutine(AttachXrButton(_doseButton, TryDose));

        // Info panel
        _infoPanel = GameObject.CreatePrimitive(PrimitiveType.Quad);
        _infoPanel.name = "L10_InfoPanel";
        Object.Destroy(_infoPanel.GetComponent<Collider>());
        _infoPanel.transform.SetParent(transform, false);
        _infoPanel.transform.position = consolePos + new Vector3(0f, 1.05f, 0.1f);
        _infoPanel.transform.localScale = new Vector3(1.9f, 1.15f, 1f);
        _infoPanel.GetComponent<Renderer>().sharedMaterial = OpaqueMat(new Color(0.05f, 0.08f, 0.12f));
        _infoText = MakeText(_infoPanel.transform, new Vector3(0f, 0f, -0.02f), 0.05f, TextAnchor.MiddleCenter, new Color(0.8f, 0.95f, 1f));
        ShowDoseButton(false); ShowInfoPanel(false);
    }

    private void UpdateInfoPanel()
    {
        if (_infoText == null || _infoPanel == null || !_infoPanel.activeSelf) return;
        string body;
        if (_stageIndex <= 2)
        {
            var s = _stages[_stageIndex];
            body = $"PEMURNIAN HPAL — TAHAP {_stageIndex + 1}/3\n" +
                   $"Reagen : {s.reagent}\n" +
                   $"Reaksi : {s.reaction}\n" +
                   $"Fungsi : {s.removes}\n" +
                   $"Target pH : {s.pHFrom:0.0} -> {s.pHTo:0.0}";
        }
        else
        {
            body = "PRESIPITASI MHP SELESAI\nNi(OH)2 + Co(OH)2 (hijau) = bahan baku baterai EV\nLanjut: sampling + Lab QC";
        }
        _infoText.text = body + $"\n--------------------------------\npH SEKARANG : {_pHCurrent:0.0}   |   MHP : {_mhpQuality:0} %";
        BillboardTo(_infoPanel.transform, GetPlayerHead());
    }

    private void ShowDoseButton(bool on)
    {
        if (_doseButton != null) _doseButton.SetActive(on);
        if (on && _stageIndex <= 2) ResolveSkidMotors(_stageIndex);
        if (on && _doseLabel != null) _doseLabel.text = $"DOSING {_stages[Mathf.Clamp(_stageIndex, 0, 2)].formula}\n[ tekan / SPACE ]";
    }
    private void ShowInfoPanel(bool on) { if (_infoPanel != null) _infoPanel.SetActive(on); }

    // ============================================================ HELPERS
    private void AnimateAgitators()
    {
        if (_agitatorRoots == null) return;
        float d = _agitatorRpm * 6f * Time.deltaTime;
        foreach (var a in _agitatorRoots) if (a != null) a.Rotate(Vector3.up, d, Space.World);
    }

    private void SetProcessVisuals(bool active)
    {
        if (_feedLiquid != null) _feedLiquid.SetActive(active);
        if (_reagentLiquid != null) _reagentLiquid.SetActive(false);
        if (_neutralToPolishLiquid != null) _neutralToPolishLiquid.SetActive(false);
        if (_polishToMhpLiquid != null) _polishToMhpLiquid.SetActive(false);
        if (_mhpSampleFlow != null) _mhpSampleFlow.SetActive(false);
        if (_mhpSampleProduct != null) _mhpSampleProduct.SetActive(false);
        if (_neutralizationFx != null) { if (active) _neutralizationFx.Play(); else _neutralizationFx.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear); }
        if (_precipitationFx != null) { if (active) _precipitationFx.Play(); else _precipitationFx.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear); }
    }

    private void UpdateFx(int idx, float t)
    {
        if (idx < 2 && _neutralizationFx != null) { var e = _neutralizationFx.emission; e.rateOverTime = Mathf.Lerp(10f, 55f, t); }
        if (idx == 2 && _precipitationFx != null) { var e = _precipitationFx.emission; e.rateOverTime = Mathf.Lerp(0f, 70f, t); }
    }

    private float Smooth(float t) { t = Mathf.Clamp01(t); return t * t * (3f - 2f * t); }
    private void PushPH() { _glm?.SetPH(_pHCurrent); }

    private void TeleportPlayer(Transform target)
    {
        if (target == null || _playerRigRoot == null) { if (_playerRigRoot == null) AutoFindReferences(); if (target == null || _playerRigRoot == null) return; }
        var xr = _playerRigRoot.GetComponent<XROrigin>();
        if (xr == null) return;
        var cc = _playerRigRoot.GetComponent<CharacterController>();
        bool en = cc != null && cc.enabled; if (en) cc.enabled = false;
        xr.MoveCameraToWorldLocation(target.position + Vector3.up * xr.CameraYOffset);
        xr.MatchOriginUpCameraForward(Vector3.up, target.forward);
        if (en) cc.enabled = true;
    }

    private Transform GetCam() { var xr = _playerRigRoot != null ? _playerRigRoot.GetComponent<XROrigin>() : null; return xr != null && xr.Camera != null ? xr.Camera.transform : (Camera.main != null ? Camera.main.transform : null); }
    private Vector3 GetPlayerHead() { var c = GetCam(); return c != null ? c.position : (_teleportTargetField != null ? _teleportTargetField.position + Vector3.up * 1.6f : Vector3.zero); }
    private void BillboardTo(Transform t, Vector3 head) { if (t == null) return; Vector3 d = t.position - head; d.y = 0f; if (d.sqrMagnitude > 0.001f) t.rotation = Quaternion.LookRotation(d.normalized, Vector3.up); }

    private TextMesh MakeText(Transform parent, Vector3 local, float size, TextAnchor anchor, Color col)
    {
        var go = new GameObject("Txt");
        go.transform.SetParent(parent, false);
        go.transform.localPosition = local;
        go.transform.localRotation = Quaternion.identity;
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
        var m = new Material(sh); m.color = c;
        if (m.HasProperty(IdBase)) m.SetColor(IdBase, c);
        return m;
    }

    private IEnumerator AttachXrButton(GameObject go, System.Action onClick)
    {
        yield return null;
        var bc = go.GetComponent<BoxCollider>() ?? go.AddComponent<BoxCollider>();
        bc.isTrigger = false;
        var si = go.GetComponent<UnityEngine.XR.Interaction.Toolkit.Interactables.XRSimpleInteractable>()
              ?? go.AddComponent<UnityEngine.XR.Interaction.Toolkit.Interactables.XRSimpleInteractable>();
        si.colliders.Clear(); si.colliders.Add(bc);
        si.enabled = false; si.enabled = true;
        si.selectEntered.AddListener(_ => onClick());
        si.hoverEntered.AddListener(_ => { _pendingClick = onClick; });
    }

    private void EnsureAudio()
    {
        if (_agitatorAudio == null) _agitatorAudio = MakeAudio("L10_AgitatorAudio", true, 0.25f, GenNoise(3f, 60f, 1201));
        if (_doseAudio == null) _doseAudio = MakeAudio("L10_DoseAudio", true, 0.0f, GenNoise(2f, 110f, 1202));
        if (_readyAudio == null) _readyAudio = MakeAudio("L10_ReadyAudio", false, 0.0f, GenChime(1.0f, 1203));
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
        for (int i = 0; i < n; i++) { float t = (float)i / sr; float env = Mathf.Clamp01(1f - t / dur); d[i] = (Mathf.Sin(2 * Mathf.PI * 540 * t) * 0.22f + Mathf.Sin(2 * Mathf.PI * 810 * t) * 0.14f) * env; }
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
        if (_teleportTargetField == null) { var g = GameObject.Find("SpawnPoint_Lvl11"); if (g != null) _teleportTargetField = g.transform; }
        if (_mhpField == null) _mhpField = GameObject.Find("Mesin Utama/Level11_MHP_Field") ?? GameObject.Find("Level11_MHP_Field");
        if (_mhpField == null) return;
        Transform root = _mhpField.transform;
        if (_feedLiquid == null) _feedLiquid = Child(root, "Feed_From_CCD_Liquid");
        if (_reagentLiquid == null) _reagentLiquid = Child(root, "Reagent_Liquid_Line");
        if (_neutralToPolishLiquid == null) _neutralToPolishLiquid = Child(root, "Neutralization_To_Polishing_Liquid");
        if (_polishToMhpLiquid == null) _polishToMhpLiquid = Child(root, "Polishing_To_MHP_Liquid");
        if (_mhpSampleFlow == null) _mhpSampleFlow = Child(root, "MHP_Sample_Flow");
        if (_mhpSampleProduct == null) _mhpSampleProduct = Child(root, "MHP_Sample_Product");
        if (_neutralizationFx == null) { var f = FindChild(root, "Neutralization_FX"); if (f != null) _neutralizationFx = f.GetComponent<ParticleSystem>(); }
        if (_precipitationFx == null) { var f = FindChild(root, "MHP_Precipitation_FX"); if (f != null) _precipitationFx = f.GetComponent<ParticleSystem>(); }
        if (!HasAny(_agitatorRoots))
        {
            var list = new List<Transform>();
            foreach (Transform t in root.GetComponentsInChildren<Transform>(true))
                if (t.name == "Agitator_Root" || t.name.StartsWith("AGI_") && t.name.Contains("Agitator_Shaft")) list.Add(t);
            _agitatorRoots = list.ToArray();
        }
    }

    private GameObject Child(Transform root, string name) { var t = FindChild(root, name); return t != null ? t.gameObject : null; }
    private Transform FindChild(Transform root, string name)
    {
        if (root == null) return null;
        foreach (Transform t in root.GetComponentsInChildren<Transform>(true)) if (t.name == name) return t;
        return null;
    }
    private bool HasAny(Transform[] a) { if (a == null) return false; foreach (var t in a) if (t != null) return true; return false; }
}
