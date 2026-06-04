using System.Collections;
using System.Collections.Generic;
using Unity.XR.CoreUtils;
using UnityEngine;

/// <summary>
/// OLIVIA VR - Level13DryStackController.cs  (Display "Level 12" — Dry Stack Tailing)
///
/// Gameplay INTERAKTIF & INFORMATIF (HPAL nikel — pembuangan limbah AKHIR):
/// Cake tailing yang sudah dinetralkan (pH ~8.5) + di-dewater (moisture < 25%) dari Filter Press
/// dibuang ke DRY STACK TAILINGS FACILITY (DSTF): di-spread & DIPADATKAN dalam terraced lift di
/// atas GEOMEMBRANE LINER, membentuk timbunan UNSATURATED yang stabil (TANPA bendungan/kolam =
/// anti-jebol, beda dari wet tailings dam). Lalu di-CLOSURE (geomembrane cap + tanah + revegetasi)
/// + MONITORING piezometer & rembesan ke polishing pond -> WWTP.
///   Tahap 1 STACKING  : timbun + padatkan cake -> terraced bench naik (DryStack progress 0->100%)
///   Tahap 2 CLOSURE   : rehab cap (geomembrane/grass) + piezometer AMAN + rembesan jernih
/// Operator menekan tombol per tahap (XR ray/poke ATAU keyboard SPACE/1), lalu inspeksi (proximity)
/// -> Compliance QC pop-up -> ACCEPT -> lapor HT 'dry stack aman'.
/// </summary>
public class Level13DryStackController : MonoBehaviour
{
    [Header("=== Player & Teleport ===")]
    [SerializeField] private Transform _playerRigRoot;
    [SerializeField] private Transform _teleportTargetDcs;

    [Header("=== DSTF References ===")]
    [SerializeField] private GameObject _rig;                  // Level13_DryStack_BlenderRig
    [SerializeField] private GameObject _containPad;           // GDS_ContainPad (ground)
    [SerializeField] private GameObject _geomembrane;          // GDS_Geomembrane
    [SerializeField] private GameObject[] _dryStackPiles;      // DryStack_Pile_00..05
    [SerializeField] private GameObject _safeCover;            // DryStack_SafeCover (rehab cap)
    [SerializeField] private GameObject[] _piezoCaps;          // GDS_PiezoCap_0..3
    [SerializeField] private GameObject _polishPondWater;      // GDS_PolishPond_Water
    [SerializeField] private ParticleSystem _dustFx;           // DryStack_Dust_FX

    [Header("=== Settings ===")]
    [SerializeField] private float _fadeDuration = 2.5f;
    [SerializeField] private float _stackDuration = 7f;
    [SerializeField] private float _closureDuration = 6f;
    [SerializeField] private float _inspectRadius = 12f;

    [Header("=== Audio ===")]
    [SerializeField] private AudioSource _stackAudio, _readyAudio;

    private const float PhValue = 8.5f;        // sudah dinetralkan di Level 11
    private const float MoistValue = 22f;      // sudah di-dewater di Level 11 (< 25%)

    private PlayerHUD _hud;
    private GameLevelManager _glm;
    private Coroutine _seq;
    private bool _levelActive, _processStarted, _busy;
    private int _stage;                 // 0=stacking, 1=closure, 2=inspeksi, 3=compliance, 4=report
    private float _dryStackProgress;
    private bool _stackingDone, _closureDone, _inspected, _complianceAccepted, _questComplete;

    private GameObject _btn; private TextMesh _btnLabel;
    private GameObject _infoPanel; private TextMesh _infoText;
    private GameObject _qcCanvas; private System.Action _pendingClick;
    private MaterialPropertyBlock _mpb;
    private static readonly int IdBase = Shader.PropertyToID("_BaseColor");
    private static readonly int IdColor = Shader.PropertyToID("_Color");

    // ---- Public props for HUD ----
    public bool LevelActive => _levelActive;
    public bool StackingDone => _stackingDone;
    public bool ClosureDone => _closureDone;
    public bool Inspected => _inspected;
    public bool ComplianceAccepted => _complianceAccepted;
    public bool QuestComplete => _questComplete;
    public float DryStackProgress => _dryStackProgress;
    public float PHCurrent => PhValue;
    public float CakeMoistureCurrent => MoistValue;

    private void Awake()
    {
        _hud = FindFirstObjectByType<PlayerHUD>();
        _glm = GameLevelManager.Instance;
        AutoFindReferences();
        EnsureAudio();
        SetStackVisuals(false);
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
        Stop(_stackAudio);
    }

    private void OnLevelStarted(GameLevelManager.GameLevel level)
    {
        _levelActive = level == GameLevelManager.GameLevel.Level13_TailingWaste;
        if (!_levelActive) { SetStackVisuals(false); ShowButton(false); ShowInfo(false); HideQc(); Stop(_stackAudio); return; }
        _glm = GameLevelManager.Instance;
        _processStarted = false; _busy = false; _stage = 0; _dryStackProgress = 0f;
        _stackingDone = _closureDone = _inspected = _complianceAccepted = _questComplete = false;
        SetStackVisuals(false); ShowButton(false); ShowInfo(false); HideQc();
        if (_hud != null) _hud.ShowNotifPublic("Level 12: Cake tailing siap dibuang ke DRY STACK. Tekan DCS 12 untuk mulai.");
        TeleportTo(_teleportTargetDcs != null ? _teleportTargetDcs.position : Vector3.zero, Vector3.forward, _teleportTargetDcs == null);
    }

    private void OnDcsButtonPressed(int number)
    {
        if (!_levelActive || number != 12 || _processStarted) return;
        _processStarted = true;
        _seq = StartCoroutine(StartFieldSequence());
    }

    private IEnumerator StartFieldSequence()
    {
        if (_hud != null) _hud.PlayManualFade(_fadeDuration);
        yield return new WaitForSeconds(_fadeDuration * 0.5f);
        EnsurePadGround();
        TeleportTo(new Vector3(20f, 0.9f, 207f), new Vector3(0f, 0f, 1f), false); // di DSTF, hadap timbunan
        yield return new WaitForSeconds(_fadeDuration * 0.5f + 0.5f);
        SetStackVisuals(true);
        BuildOperatorStation();
        _stage = 0;
        BeginStage();
        _seq = null;
    }

    private void Update()
    {
        if (!_levelActive || !_processStarted) return;
        if (_busy && _dustFx != null && !_dustFx.isPlaying) _dustFx.Play();

        if (!_busy && _stage <= 1 && (Input.GetKeyDown(KeyCode.Space) || Input.GetKeyDown(KeyCode.Alpha1))) TryAction();
        if (_stage == 2 && !_inspected) UpdateInspectProximity();
        if (_stage == 3 && !_complianceAccepted)
        {
            if (_qcCanvas == null && Input.GetKeyDown(KeyCode.L)) ShowQc();
            if (_qcCanvas != null && _qcCanvas.activeSelf && _pendingClick != null && Input.GetKeyDown(KeyCode.Return))
            { var a = _pendingClick; _pendingClick = null; a(); }
        }
        UpdateInfo();
    }

    // ============================================================ STAGES
    private void BeginStage()
    {
        ShowInfo(true); ShowButton(true);
        if (_stage == 0)
        {
            if (_btnLabel != null) _btnLabel.text = "STACK & COMPACT\n[ tekan / SPACE ]";
            if (_hud != null) _hud.ShowNotifPublic("TAHAP 1: timbun + padatkan cake kering di terraced lift (di atas geomembrane). Tekan tombol.", 6f);
        }
        else if (_stage == 1)
        {
            if (_btnLabel != null) _btnLabel.text = "CLOSURE / REHAB\n[ tekan / SPACE ]";
            if (_hud != null) _hud.ShowNotifPublic("TAHAP 2: tutup sel (geomembrane cap + revegetasi) + cek piezometer & rembesan. Tekan tombol.", 6f);
        }
    }

    private void TryAction()
    {
        if (_busy || _stage > 1) return;
        _busy = true; ShowButton(false);
        _seq = StartCoroutine(_stage == 0 ? StackRoutine() : ClosureRoutine());
    }

    private IEnumerator StackRoutine()
    {
        PlayAudio(_stackAudio, 0.34f);
        if (_dustFx != null) _dustFx.Play();
        int shown = 0;
        float t = 0f;
        while (t < _stackDuration)
        {
            t += Time.deltaTime; float e = Smooth(Mathf.Clamp01(t / _stackDuration));
            _dryStackProgress = e * 100f;
            if (_dryStackPiles != null)
            {
                int want = Mathf.RoundToInt(e * _dryStackPiles.Length);
                for (; shown < want && shown < _dryStackPiles.Length; shown++)
                { if (_dryStackPiles[shown] != null) { _dryStackPiles[shown].SetActive(true); Tint(_dryStackPiles[shown], new Color(0.46f, 0.39f, 0.29f)); } }
            }
            yield return null;
        }
        _dryStackProgress = 100f;
        if (_dustFx != null) _dustFx.Stop();
        Stop(_stackAudio); PlayAudio(_readyAudio, 0.3f);
        _stackingDone = true; _busy = false; _stage = 1;
        BeginStage();
        _seq = null;
    }

    private IEnumerator ClosureRoutine()
    {
        PlayAudio(_stackAudio, 0.22f);
        float t = 0f;
        while (t < _closureDuration)
        {
            t += Time.deltaTime; float e = Smooth(Mathf.Clamp01(t / _closureDuration));
            if (_polishPondWater != null) Tint(_polishPondWater, Color.Lerp(new Color(0.4f, 0.35f, 0.22f), new Color(0.22f, 0.46f, 0.5f), e)); // keruh -> jernih
            yield return null;
        }
        if (_safeCover != null) { _safeCover.SetActive(true); Tint(_safeCover, new Color(0.32f, 0.5f, 0.22f)); } // rehab grass cap
        if (_piezoCaps != null) foreach (var p in _piezoCaps) Tint(p, new Color(0.15f, 0.65f, 0.2f)); // piezometer AMAN
        Stop(_stackAudio); PlayAudio(_readyAudio, 0.3f);
        _closureDone = true; _busy = false; _stage = 2;
        ShowButton(false);
        if (_hud != null) _hud.ShowNotifPublic("Sel tertutup + revegetasi. Jalan ke TIMBUNAN untuk inspeksi akhir.", 7f);
        _seq = null;
    }

    // ============================================================ INSPEKSI
    private void UpdateInspectProximity()
    {
        Transform stack = FindChild("DryStack_Storage") ?? FindChild("DryStack_Pile_00");
        Vector3 target = stack != null ? stack.position : new Vector3(20f, 0.4f, 230f);
        Vector3 head = GetPlayerHead();
        if (Vector2.Distance(new Vector2(head.x, head.z), new Vector2(target.x, target.z)) <= _inspectRadius)
        {
            _inspected = true; _stage = 3; PlayAudio(_readyAudio, 0.3f);
            if (_hud != null) _hud.ShowNotifPublic("Inspeksi DSTF OK. Tekan [L] untuk COMPLIANCE QC (geomembrane/piezometer/rembesan).", 7f);
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
        _qcCanvas.name = "L12_DryStack_ComplianceQC";
        Object.Destroy(_qcCanvas.GetComponent<Collider>());
        _qcCanvas.transform.position = pos; _qcCanvas.transform.localScale = new Vector3(1.9f, 1.18f, 1f);
        _qcCanvas.GetComponent<Renderer>().sharedMaterial = OpaqueMat(new Color(0.06f, 0.09f, 0.07f));

        var txt = MakeText(_qcCanvas.transform, new Vector3(0f, 0f, -0.02f), 0.048f, TextAnchor.MiddleCenter, new Color(0.85f, 1f, 0.9f));
        txt.text =
            "=== COMPLIANCE QC — DRY STACK TAILINGS FACILITY ===\n" +
            "Moisture cake : 22 %  -> UNSATURATED (stabil, anti-jebol)\n" +
            "pH tailing    : 8.5   (baku mutu 6-9)\n" +
            "Geomembrane liner : INTACT (cegah seepage ke tanah)\n" +
            "Piezometer (4 titik) : pore pressure RENDAH -> AMAN\n" +
            "Rembesan -> Polishing Pond -> WWTP : jernih\n" +
            "Closure cap + revegetasi : SELESAI\n" +
            "VERDICT: DSTF AMAN LINGKUNGAN — TANPA bendungan/kolam";

        var btn = GameObject.CreatePrimitive(PrimitiveType.Cube);
        btn.name = "L12_QC_Accept";
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
        _glm?.NotifyLevel13DryStackComplete();
        if (_hud != null) _hud.ShowNotifPublic("DSTF lulus compliance. Lapor HT (tahan T): 'dry stack aman, pH 8.5'.", 8f);
    }

    private void HideQc() { if (_qcCanvas != null) { Object.Destroy(_qcCanvas); _qcCanvas = null; } _pendingClick = null; }

    // ============================================================ OPERATOR STATION
    private void BuildOperatorStation()
    {
        if (_btn != null) return;
        Vector3 consolePos = new Vector3(20f, 2.4f, 209.2f);
        _btn = GameObject.CreatePrimitive(PrimitiveType.Cube);
        _btn.name = "L12_ActionButton";
        _btn.transform.SetParent(transform, false);
        _btn.transform.position = consolePos;
        _btn.transform.localScale = new Vector3(0.7f, 0.3f, 0.16f);
        _btn.GetComponent<Renderer>().sharedMaterial = OpaqueMat(new Color(0.55f, 0.42f, 0.18f));
        _btnLabel = MakeText(_btn.transform, new Vector3(0f, 0f, -0.6f), 0.11f, TextAnchor.MiddleCenter, Color.black);
        _btnLabel.text = "MULAI";
        StartCoroutine(AttachXrButton(_btn, TryAction));

        _infoPanel = GameObject.CreatePrimitive(PrimitiveType.Quad);
        _infoPanel.name = "L12_InfoPanel";
        Object.Destroy(_infoPanel.GetComponent<Collider>());
        _infoPanel.transform.SetParent(transform, false);
        _infoPanel.transform.position = consolePos + new Vector3(0f, 1.2f, 0.1f);
        _infoPanel.transform.localScale = new Vector3(2.0f, 1.2f, 1f);
        _infoPanel.GetComponent<Renderer>().sharedMaterial = OpaqueMat(new Color(0.05f, 0.08f, 0.06f));
        _infoText = MakeText(_infoPanel.transform, new Vector3(0f, 0f, -0.02f), 0.05f, TextAnchor.MiddleCenter, new Color(0.82f, 1f, 0.9f));
        ShowButton(false); ShowInfo(false);
    }

    private void UpdateInfo()
    {
        if (_infoText == null || _infoPanel == null || !_infoPanel.activeSelf) return;
        string body;
        if (_stage == 0)
            body = "PEMBUANGAN LIMBAH AKHIR — TAHAP 1/2 DRY STACKING\n" +
                   "Cake kering (moisture < 25%) di-spread + DIPADATKAN\n" +
                   "dalam terraced lift di atas GEOMEMBRANE LINER\n" +
                   "Hasil: timbunan UNSATURATED -> stabil, anti-jebol\n" +
                   "(beda dari wet tailings dam yang berisiko)";
        else if (_stage == 1)
            body = "TAHAP 2/2 — CLOSURE & MONITORING\n" +
                   "Penutupan sel: geomembrane cap + tanah + REVEGETASI\n" +
                   "Monitoring: PIEZOMETER (pore pressure rendah)\n" +
                   "Rembesan dikumpulkan -> Polishing Pond -> WWTP";
        else
            body = "DRY STACK SELESAI\nTimbunan stabil + sel tertutup + revegetasi\nLanjut: inspeksi + compliance QC";
        _infoText.text = body + $"\n--------------------------------\nPROGRESS : {_dryStackProgress:0} %   |   pH : {PhValue:0.0}   |   MOISTURE : {MoistValue:0} %";
        BillboardTo(_infoPanel.transform, GetPlayerHead());
    }

    private void ShowButton(bool on) { if (_btn != null) _btn.SetActive(on); }
    private void ShowInfo(bool on) { if (_infoPanel != null) _infoPanel.SetActive(on); }

    // ============================================================ HELPERS
    private void SetStackVisuals(bool active)
    {
        if (_dryStackPiles != null) foreach (var p in _dryStackPiles) SetActive(p, false);
        SetActive(_safeCover, false);
        if (_dustFx != null) _dustFx.Stop();
    }

    private void SetActive(GameObject go, bool on) { if (go != null) go.SetActive(on); }
    private float Smooth(float t) { t = Mathf.Clamp01(t); return t * t * (3f - 2f * t); }

    private void Tint(GameObject go, Color c)
    {
        if (go == null) return;
        if (_mpb == null) _mpb = new MaterialPropertyBlock();
        foreach (var r in go.GetComponentsInChildren<Renderer>(true))
        { r.GetPropertyBlock(_mpb); _mpb.SetColor(IdBase, c); _mpb.SetColor(IdColor, c); r.SetPropertyBlock(_mpb); }
    }

    private void EnsurePadGround()
    {
        if (_containPad == null) return;
        if (_containPad.GetComponent<Collider>() == null)
        {
            var mf = _containPad.GetComponentInChildren<MeshFilter>();
            var bc = _containPad.AddComponent<BoxCollider>();
            if (mf != null && mf.sharedMesh != null) { bc.center = mf.sharedMesh.bounds.center; bc.size = mf.sharedMesh.bounds.size; }
        }
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
    private Vector3 GetPlayerHead() { var c = GetCam(); return c != null ? c.position : new Vector3(20f, 2.5f, 207f); }
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
        if (_stackAudio == null) _stackAudio = MakeAudio("L12_StackAudio", true, 0f, GenNoise(3f, 70f, 1401));
        if (_readyAudio == null) _readyAudio = MakeAudio("L12_ReadyAudio", false, 0f, GenChime(1f, 1402));
    }
    private AudioSource MakeAudio(string n, bool loop, float vol, AudioClip clip)
    { var go = new GameObject(n); go.transform.SetParent(transform, false); var a = go.AddComponent<AudioSource>(); a.loop = loop; a.playOnAwake = false; a.spatialBlend = 0.2f; a.volume = vol; a.clip = clip; return a; }
    private void PlayAudio(AudioSource s, float v) { if (s == null) return; s.volume = v; if (!s.isPlaying) s.Play(); }
    private void Stop(AudioSource s) { if (s != null && s.isPlaying) s.Stop(); }
    private AudioClip GenNoise(float dur, float hz, int seed)
    {
        int sr = 22050, n = Mathf.CeilToInt(dur * sr); var d = new float[n]; var r = new System.Random(seed); float ph = 0f, f = 0f;
        for (int i = 0; i < n; i++) { ph += 2f * Mathf.PI * hz / sr; float mo = Mathf.Sin(ph) * 0.28f; float no = ((float)r.NextDouble() - 0.5f) * 0.22f; f += 0.05f * (no - f); d[i] = (mo + f) * 0.4f; }
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
        if (_rig == null) _rig = GameObject.Find("Level13_DryStack_BlenderRig");
        if (_rig == null) return;
        if (_containPad == null) _containPad = Child("GDS_ContainPad");
        if (_geomembrane == null) _geomembrane = Child("GDS_Geomembrane");
        if (_safeCover == null) _safeCover = Child("DryStack_SafeCover");
        if (_polishPondWater == null) _polishPondWater = Child("GDS_PolishPond_Water");
        if (_dryStackPiles == null || _dryStackPiles.Length == 0)
            _dryStackPiles = CollectChildren("DryStack_Pile_");
        if (_piezoCaps == null || _piezoCaps.Length == 0)
            _piezoCaps = CollectChildren("GDS_PiezoCap_");
        if (_dustFx == null) { var t = FindChild("DryStack_Dust_FX"); if (t != null) _dustFx = t.GetComponent<ParticleSystem>(); }
    }

    private GameObject[] CollectChildren(string prefix)
    {
        var list = new List<GameObject>();
        foreach (Transform t in _rig.GetComponentsInChildren<Transform>(true)) if (t.name.StartsWith(prefix)) list.Add(t.gameObject);
        return list.ToArray();
    }
    private GameObject Child(string name) { var t = FindChild(name); return t != null ? t.gameObject : null; }
    private Transform FindChild(string name)
    {
        if (_rig == null) return null;
        foreach (Transform t in _rig.GetComponentsInChildren<Transform>(true)) if (t.name == name) return t;
        return null;
    }
}
