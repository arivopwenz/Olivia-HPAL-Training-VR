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

    // ---- Public props for HUD ----
    public bool LevelActive => _levelActive;
    public bool NeutralizeDone => _neutralizeDone;
    public bool FilterPressDone => _filterPressDone;
    public bool Inspected => _inspected;
    public bool ComplianceAccepted => _complianceAccepted;
    public bool QuestComplete => _questComplete;
    public float PHCurrent => _pHCurrent;
    public float CakeMoisture => _moisture;

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
        Stop(_agitatorAudio); Stop(_pressAudio);
    }

    private void OnLevelStarted(GameLevelManager.GameLevel level)
    {
        _levelActive = level == GameLevelManager.GameLevel.Level12_TailingDischarge;
        if (!_levelActive) { SetProcessVisuals(false); ShowButton(false); ShowInfo(false); HideQc(); Stop(_agitatorAudio); return; }
        _glm = GameLevelManager.Instance;
        _processStarted = false; _busy = false; _stage = 0;
        _pHCurrent = PhStart; _moisture = MoistStart;
        _neutralizeDone = _filterPressDone = _inspected = _complianceAccepted = _questComplete = false;
        PushPH(); SetProcessVisuals(false); ShowButton(false); ShowInfo(false); HideQc();
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
        // Field stand spot di depan neutralization tank + filter press (z~146)
        TeleportTo(new Vector3(28f, 1.5f, 140f), new Vector3(0f, 0f, 1f), false);
        yield return new WaitForSeconds(_fadeDuration * 0.5f + 0.5f);
        SetProcessVisuals(true);
        Start(_agitatorAudio, 0.32f);
        BuildOperatorStation();
        _stage = 0;
        BeginStage();
        _seq = null;
    }

    private void Update()
    {
        if (!_levelActive || !_processStarted) return;
        if (_agitatorRoot != null) _agitatorRoot.Rotate(Vector3.up, _agitatorRpm * 6f * Time.deltaTime, Space.World);
        if (_busy) AnimateRollers();

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
            _pHCurrent = PhStart; PushPH();
            if (_btnLabel != null) _btnLabel.text = "DOSING LIMESTONE\n[ tekan / SPACE ]";
            if (_hud != null) _hud.ShowNotifPublic("TAHAP 1: dosing LIMESTONE/KAPUR untuk netralkan tailing asam. Tekan tombol DOSING.", 6f);
        }
        else if (_stage == 1)
        {
            if (_btnLabel != null) _btnLabel.text = "RUN FILTER PRESS\n[ tekan / SPACE ]";
            if (_hud != null) _hud.ShowNotifPublic("TAHAP 2: jalankan FILTER PRESS untuk dewater tailing jadi cake kering. Tekan tombol.", 6f);
        }
    }

    private void TryAction()
    {
        if (_busy || _stage > 1) return;
        _busy = true; ShowButton(false);
        _seq = StartCoroutine(_stage == 0 ? NeutralizeRoutine() : FilterPressRoutine());
    }

    private IEnumerator NeutralizeRoutine()
    {
        Start(_pressAudio, 0.0f);
        if (_limestonePour != null) _limestonePour.SetActive(true);
        float t = 0f;
        while (t < _doseDuration)
        {
            t += Time.deltaTime; float e = Smooth(Mathf.Clamp01(t / _doseDuration));
            _pHCurrent = Mathf.Lerp(PhStart, PhTarget, e); PushPH();
            UpdatePhNeedle();
            Tint(_neutralizedSurface, Color.Lerp(new Color(0.42f, 0.30f, 0.16f), new Color(0.55f, 0.58f, 0.5f), e)); // coklat asam -> abu netral
            yield return null;
        }
        _pHCurrent = PhTarget; PushPH(); UpdatePhNeedle();
        SetActive(_phStatusGreen, true); SetActive(_phStatusRed, false);
        SetActive(_beaconGreen, true); SetActive(_beaconRed, false);
        if (_limestonePour != null) _limestonePour.SetActive(false);
        if (_polishedFlow != null) _polishedFlow.SetActive(true);
        _neutralizeDone = true; _busy = false; _stage = 1;
        BeginStage();
        _seq = null;
    }

    private IEnumerator FilterPressRoutine()
    {
        Start(_pressAudio, 0.42f);
        if (_filtrateChannel != null) _filtrateChannel.SetActive(true);
        int shown = 0;
        float t = 0f;
        while (t < _pressDuration)
        {
            t += Time.deltaTime; float e = Smooth(Mathf.Clamp01(t / _pressDuration));
            _moisture = Mathf.Lerp(MoistStart, MoistTarget, e);
            // reveal cake blocks progresif + tint makin kering (gelap)
            if (_cakeBlocks != null)
            {
                int want = Mathf.RoundToInt(e * _cakeBlocks.Length);
                for (; shown < want && shown < _cakeBlocks.Length; shown++)
                { if (_cakeBlocks[shown] != null) { _cakeBlocks[shown].SetActive(true); Tint(_cakeBlocks[shown], new Color(0.35f, 0.27f, 0.2f)); } }
            }
            yield return null;
        }
        _moisture = MoistTarget;
        Stop(_pressAudio); Start(_readyAudio, 0.3f);
        _filterPressDone = true; _busy = false; _stage = 2;
        ShowButton(false);
        if (_hud != null) _hud.ShowNotifPublic("Cake terbentuk (moisture 22%). Jalan ke KONVEYOR CAKE untuk inspeksi.", 7f);
        _seq = null;
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
        Transform cake = FindChild("Cake_On_Conveyor") ?? FindChild("Cake_Block_00") ?? FindChild("Cake_Transfer_Conveyor");
        Vector3 target = cake != null ? cake.position : new Vector3(14.58f, 1.67f, 146.12f);
        Vector3 head = GetPlayerHead();
        if (Vector2.Distance(new Vector2(head.x, head.z), new Vector2(target.x, target.z)) <= _inspectRadius)
        {
            _inspected = true; _stage = 3; Start(_readyAudio, 0.3f);
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
            "=== COMPLIANCE QC — TAILING DISCHARGE ===\n" +
            "pH tailing netral : 8.2   (baku mutu 6-9)\n" +
            "Moisture cake     : 22 %  (< 25% -> dry-stack OK)\n" +
            "Filtrat -> WWTP   : jernih, TSS rendah\n" +
            "Logam berat (Fe/Mn/Cr) : di bawah baku mutu\n" +
            "Gypsum + residu stabil | beacon HIJAU\n" +
            "VERDICT: AMAN LINGKUNGAN — cake siap ke DRY STACK";

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
            body = "PENGOLAHAN LIMBAH HPAL — TAHAP 1/2 NETRALISASI\n" +
                   "Reagen : LIMESTONE CaCO3 / KAPUR Ca(OH)2\n" +
                   "Reaksi : H2SO4 sisa + CaCO3 -> CaSO4 + H2O + CO2\n" +
                   "Fungsi : netralkan asam + endapkan logam berat\n" +
                   "Target pH : 2.3 -> 8.0 (baku mutu lingkungan 6-9)";
        else if (_stage == 1)
            body = "TAHAP 2/2 — FILTER PRESS (plate & frame)\n" +
                   "Dewatering tailing: tekan slurry, filtrat keluar\n" +
                   "Filtrat jernih -> WWTP ; padatan jadi CAKE\n" +
                   "Target moisture cake : 60% -> < 25% (stackable)";
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
    private void Start(AudioSource s, float v) { if (s == null) return; s.volume = v; if (!s.isPlaying) s.Play(); }
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
        if (_rig == null) _rig = GameObject.Find("Level13_DryStack_BlenderRig") ?? GameObject.Find("Final_FilterPress_Unit");
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
    }

    private GameObject Child(string name) { var t = FindChild(name); return t != null ? t.gameObject : null; }
    private Transform FindChild(string name)
    {
        if (_rig == null) return null;
        foreach (Transform t in _rig.GetComponentsInChildren<Transform>(true)) if (t.name == name) return t;
        return null;
    }
}
