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
///   Tahap 1 PRA-NETRALISASI  : LIMESTONE/LIME      pH 1.35->2.5  buang Fe (endapan coklat)
///   Tahap 2 POLISHING        : KAPUR Ca(OH)2       pH 2.5->4.0   buang Al/Cr/sisa Fe
///   Tahap 3 PRESIPITASI MHP  : MAGNESIA MgO        pH 4.0->7.0   endap Ni(OH)2+Co(OH)2 (hijau)
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
        new Stage{ reagent="LIMESTONE / LIME SLURRY", formula="CaCO3 / Ca(OH)2", removes="Fe3+ turun sebagai Fe(OH)3 coklat; Ni-Co tetap larut",
            reaction="Fe3+ + 3OH- -> Fe(OH)3(s)", pHFrom=1.35f, pHTo=2.5f,
            liquidColor=new Color(0.45f,0.27f,0.12f), skidPrefix="Neutralization_Reagent_Dosing_Skid" },
        new Stage{ reagent="KAPUR / SLAKED LIME", formula="Ca(OH)2", removes="Al/Cr dan sisa Fe turun; Ni-Co masih dijaga larut",
            reaction="Al3+ + 3OH- -> Al(OH)3(s)", pHFrom=2.5f, pHTo=4.0f,
            liquidColor=new Color(0.34f,0.45f,0.42f), skidPrefix="Lime_Dosing_Skid" },
        new Stage{ reagent="MAGNESIA / MgO slurry", formula="MgO", removes="Ni & Co diendapkan jadi MHP (hijau)",
            reaction="NiSO4 + MgO + H2O -> Ni(OH)2(s) + MgSO4   (Co serupa)", pHFrom=4.0f, pHTo=7.0f,
            liquidColor=new Color(0.18f,0.62f,0.40f), skidPrefix="MGO_Dosing_Skid" },
    };

    private PlayerHUD _hud;
    private GameLevelManager _glm;
    private Coroutine _seq;
    private bool _levelActive, _processStarted;
    private const int PhaseInitialSample = -10;
    private const int PhaseFeDosing = 0;
    private const int PhaseFeSeparation = 10;
    private const int PhaseAlCrDosing = 1;
    private const int PhaseValidationSample = 11;
    private const int PhaseTransferValve = 12;
    private const int PhaseMhpDosing = 2;
    private const int PhaseFilterProduct = 13;
    private const int PhaseEvaluation = 4;
    private const int PhaseWarehouse = 5;
    private int _stageIndex;
    private bool _dosing;
    private float _pHCurrent = 1.5f, _mhpQuality;
    private bool _stage1, _stage2, _stage3, _sampleTaken, _labAccepted, _questComplete;
    private bool _initialSampleAnalyzed, _feSeparated, _validationSampleTaken, _transferValveOpen, _filterProductDone;
    private float _feConcentration, _alConcentration, _niConcentration, _coConcentration;
    private float _reagentFlow, _tankLevel, _turbidity;

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

    // ===== NEW: HT-GATED 3-TANK FLOW (limestone -> lime -> MgO) =====
    private readonly Renderer[] _tankBody = new Renderer[3];     // LiquidGhost (badan cairan, di-rise dari dasar)
    private readonly Renderer[] _tankSurface = new Renderer[3];  // Liquid_Surface (permukaan atas)
    private readonly Vector3[] _tankBodyBaseScale = new Vector3[3];
    private readonly Vector3[] _tankBodyBaseLocalPos = new Vector3[3];
    private readonly Vector3[] _tankSurfBaseLocalPos = new Vector3[3];
    private readonly TankFluidColumn[] _tankFluid = new TankFluidColumn[3]; // cairan TERANG naik dari dasar (1 volume, no ghost)
    private bool _rotorOn; // rotor/agitator baru berputar setelah HT#2 (dosing)
    private bool _mhpRefsReady;
    private static readonly string[] _tankNames = { "Neutralization_Purification_Tank", "Polishing_Tank", "MHP_Precipitation_Tank" };
    // Warna riset (chemistry-verified): PLS asam hijau -> per tahap.
    private static readonly Color _colPlsAcidGreen = new Color(0.52f, 0.60f, 0.20f);  // input CCD PLS (asam, Ni hijau + Fe amber)
    private static readonly Color _colAfterLimestone = new Color(0.50f, 0.30f, 0.11f); // Fe/Al hydroxide coklat-oranye
    private static readonly Color _colAfterLime = new Color(0.22f, 0.52f, 0.50f);       // teal (Ni jernih, Al/Cr buang)
    private static readonly Color _colAfterMgO = new Color(0.18f, 0.62f, 0.40f);        // MHP hijau-kebiruan

    private void EnsureMhpTankRefs()
    {
        if (_mhpRefsReady) return;
        for (int i = 0; i < 3; i++)
        {
            _tankBody[i] = FindSceneRenderer(_tankNames[i] + "_LiquidGhost");
            _tankSurface[i] = FindSceneRenderer(_tankNames[i] + "_Liquid_Surface");
            if (_tankBody[i] != null) { _tankBodyBaseScale[i] = _tankBody[i].transform.localScale; _tankBodyBaseLocalPos[i] = _tankBody[i].transform.localPosition; }
            if (_tankSurface[i] != null) _tankSurfBaseLocalPos[i] = _tankSurface[i].transform.localPosition;
            // SATU cairan TERANG (shader L7SlurryFill, naik dari dasar) pada mesh volume Ghost.
            if (_tankBody[i] != null)
            {
                var fc = _tankBody[i].GetComponent<TankFluidColumn>();
                if (fc == null) fc = _tankBody[i].gameObject.AddComponent<TankFluidColumn>();
                Color sh = _fillColor[i];
                fc.Setup(_tankBody[i], sh, sh * 0.55f, sh * 0.30f);
                fc.SetLevel01(0f);
                fc.Hide();
                _tankFluid[i] = fc;
            }
            // disc Surface tipis TIDAK dipakai (shader sudah punya surface band) -> sembunyikan permanen.
            if (_tankSurface[i] != null) _tankSurface[i].gameObject.SetActive(false);
        }
        _mhpRefsReady = true;
    }

    private Renderer FindSceneRenderer(string n)
    {
        foreach (var t in Resources.FindObjectsOfTypeAll<Transform>())
            if (t.name == n && t.gameObject.scene.IsValid()) return t.GetComponent<Renderer>();
        return null;
    }

    // Kosongkan SEMUA tangki Level 10 (awal level: tidak ada cairan).
    private void HideAllTankLiquids()
    {
        EnsureMhpTankRefs();
        _rotorOn = false;
        for (int i = 0; i < 3; i++)
        {
            if (_tankFluid[i] != null) { _tankFluid[i].SetLevel01(0f); _tankFluid[i].SetSwirl(0f); _tankFluid[i].Hide(); }
            if (_tankBody[i] != null) _tankBody[i].gameObject.SetActive(false);
            if (_tankSurface[i] != null) _tankSurface[i].gameObject.SetActive(false);
        }
        if (_neutralToPolishLiquid != null) _neutralToPolishLiquid.SetActive(false);
        if (_polishToMhpLiquid != null) _polishToMhpLiquid.SetActive(false);
        if (_feedLiquid != null) _feedLiquid.SetActive(false);
        if (_reagentLiquid != null) _reagentLiquid.SetActive(false);
    }

    // ===== STATE & FLOW =====
    private bool _mhpFlowActive, _mhpBusy;
    private int _mhpTank, _mhpAwait; // _mhpAwait: 0 none, 1 fill, 2 dose, 3 final report
    private readonly Transform[] _tankStand = new Transform[3];
    private GameObject _mhpStream; private ParticleSystem _mhpBubbles;
    private static readonly Color[] _fillColor = {
        new Color(0.52f,0.60f,0.20f),  // T1 PLS asam hijau (dari CCD)
        new Color(0.40f,0.52f,0.28f),  // T2 jernih pucat (Fe/Al sudah dibuang)
        new Color(0.22f,0.52f,0.50f),  // T3 teal (dari polishing)
    };
    private static readonly Color[] _doseColorOut = {
        new Color(0.50f,0.30f,0.11f),  // T1 setelah limestone: Fe/Al hydroxide coklat-oranye
        new Color(0.22f,0.52f,0.50f),  // T2 setelah lime: teal jernih
        new Color(0.18f,0.62f,0.40f),  // T3 setelah MgO: MHP hijau-kebiruan
    };
    private static readonly string[] _reagentName = { "LIMESTONE (CaCO3)", "KAPUR Ca(OH)2", "MAGNESIA MgO" };
    private static readonly float[] _phAfter = { 3.5f, 5.0f, 7.5f };

    private Transform GetTankStand(int idx)
    {
        if (_tankStand[idx] != null) return _tankStand[idx];
        EnsureMhpTankRefs();
        // posisi tangki: T1 x73.2, T2 x62.6, T3 x52.0, semua z~121.2
        float[] tx = { 73.2f, 62.6f, 52.0f };
        var go = new GameObject("L10_TankStand_" + idx);
        Vector3 tankCenter = new Vector3(tx[idx], 6.5f, 121.2f);
        Vector3 standPos = new Vector3(tx[idx], 1.0f, 128.6f); // berdiri di sisi akses (z lebih besar)
        go.transform.position = standPos;
        Vector3 look = tankCenter - standPos; look.y = 0f;
        go.transform.rotation = look.sqrMagnitude > 0.001f ? Quaternion.LookRotation(look.normalized, Vector3.up) : Quaternion.identity;
        _tankStand[idx] = go.transform;
        return go.transform;
    }

    // Dipanggil saat PTT (HT) dilepas — gate utama flow Level 10.
    private void OnMhpHtReleased()
    {
        if (!_mhpFlowActive || _mhpBusy) return;
        if (_mhpAwait == 1) { _mhpAwait = 0; _seq = StartCoroutine(FillTankRoutine(_mhpTank)); }
        else if (_mhpAwait == 2) { _mhpAwait = 0; _seq = StartCoroutine(DoseTankRoutine(_mhpTank)); }
        else if (_mhpAwait == 3) { _mhpAwait = 0; FinishMhp(); }
    }

    private void StartMhpFlow()
    {
        HideAllTankLiquids();
        _mhpFlowActive = true; _mhpBusy = false; _mhpTank = 0; _mhpAwait = 1;
        PlayAudio(_agitatorAudio, 0.30f);
        TeleportPlayer(GetTankStand(0));
        if (_hud != null) _hud.ShowNotifPublic("TANGKI 1 (Netralisasi). Lapor HT (tahan T) untuk membuka pipa PLS dari CCD turun ke tangki.", 8f);
    }

    // Cairan TURUN dari atas (pipa) + NAIK dari dasar tangki.
    private IEnumerator FillTankRoutine(int idx)
    {
        _mhpBusy = true;
        EnsureMhpTankRefs();
        var body = _tankBody[idx]; var surf = _tankSurface[idx];
        Color col = _fillColor[idx];
        // anchor stream: T1 dari PLS_Overflow_Pipe_Flange_End (1), lainnya dari atas tangki.
        float[] tx = { 73.2f, 62.6f, 52.0f };
        Vector3 tankTop = new Vector3(tx[idx], 10.6f, 121.2f);
        Vector3 streamTop = idx == 0 ? FindWorldPos("PLS_Overflow_Pipe_Flange_End (1)", new Vector3(71.4f,10.7f,119.3f)) : tankTop + Vector3.up * 3.2f;
        ShowFillStream(streamTop, tankTop, col, true);
        if (_hud != null) _hud.ShowNotifPublic("Cairan PLS mengalir turun ke tangki, level naik dari dasar...", 6f);

        var fluid = _tankFluid[idx];
        if (fluid != null)
        {
            fluid.Show(); fluid.SetColors(col, col * 0.55f, col * 0.30f);
            if (surf != null) surf.gameObject.SetActive(false); // pakai 1 volume terang saja, no disc/ghost ganda
            float dur = 6f, t = 0f;
            fluid.SetLevel01(0f);
            while (t < dur)
            {
                t += Time.deltaTime; float p = Smooth(t / dur);
                fluid.SetLevel01(p); // permukaan NAIK dari dasar ke atas (world-Y clip)
                yield return null;
            }
            fluid.SetLevel01(1f);
        }
        else yield return new WaitForSeconds(4f);

        ShowFillStream(Vector3.zero, Vector3.zero, col, false);
        _mhpBusy = false; _mhpAwait = 2;
        if (_hud != null) _hud.ShowNotifPublic($"Tangki {idx + 1} terisi PLS. Lapor HT (tahan T) untuk dosing {_reagentName[idx]}.", 8f);
    }

    // Dosing reagen: gelembung reaksi + warna berubah sesuai kimia + pH naik.
    private IEnumerator DoseTankRoutine(int idx)
    {
        _mhpBusy = true;
        var body = _tankBody[idx]; var surf = _tankSurface[idx];
        float[] tx = { 73.2f, 62.6f, 52.0f };
        Vector3 tankCenter = new Vector3(tx[idx], 6.8f, 121.2f);
        ShowBubbles(tankCenter, true);
        PlayAudio(_doseAudio, 0.32f);
        // HT#2 (dosing): rotor/agitator MULAI berputar + cairan ikut berputar (swirl)
        _rotorOn = true;
        if (_tankFluid[idx] != null) _tankFluid[idx].SetSwirl(1.2f);
        if (_hud != null) _hud.ShowNotifPublic($"Dosing {_reagentName[idx]}: reaksi berlangsung, larutan berubah warna...", 6f);
        Color from = _fillColor[idx], to = _doseColorOut[idx];
        float pHFrom = _pHCurrent, pHTo = _phAfter[idx];
        float dur = 6f, t = 0f;
        while (t < dur)
        {
            t += Time.deltaTime; float p = Smooth(t / dur);
            Color c = Color.Lerp(from, to, p);
            if (_tankFluid[idx] != null) _tankFluid[idx].SetColors(c, c * 0.55f, c * 0.30f);
            _pHCurrent = Mathf.Lerp(pHFrom, pHTo, p); PushPH();
            if (idx == 2) _mhpQuality = Mathf.Lerp(0f, 92f, p);
            yield return null;
        }
        ShowBubbles(Vector3.zero, false);
        if (idx == 0) _stage1 = true; else if (idx == 1) _stage2 = true; else _stage3 = true;
        _mhpBusy = false;

        if (idx < 2)
        {
            yield return StartCoroutine(FadeToTank(idx + 1));
        }
        else
        {
            _sampleTaken = true; _labAccepted = true; // tak ada misi sample/lab/gudang lagi
            // STAGE AKHIR: filter press mencetak cairan MHP jadi cake padat (keluar satu per satu).
            yield return StartCoroutine(FilterPressFinaleRoutine());
            _mhpAwait = 3;
            if (_hud != null) _hud.ShowNotifPublic("MHP cake tercetak lengkap! Lapor HT (tahan T): 'MHP terbentuk'.", 9f);
        }
    }

    private IEnumerator FadeToTank(int idx)
    {
        if (_hud != null) _hud.PlayManualFade(_fadeDuration);
        yield return new WaitForSeconds(_fadeDuration * 0.5f);
        TeleportPlayer(GetTankStand(idx));
        _mhpTank = idx; _mhpAwait = 1;
        yield return new WaitForSeconds(_fadeDuration * 0.5f);
        string[] tn = { "Netralisasi", "Polishing", "Presipitasi MHP" };
        if (_hud != null) _hud.ShowNotifPublic($"TANGKI {idx + 1} ({tn[idx]}). Lapor HT (tahan T) untuk alirkan cairan ke tangki ini.", 8f);
        _seq = null;
    }

    // ============================================================ STAGE AKHIR: FILTER PRESS MHP
    // Cairan MHP dipompa ke filter press -> plate menekan -> cake padat keluar SATU PER SATU
    // dari hydraulic ram ke tray.
    private IEnumerator FilterPressFinaleRoutine()
    {
        if (_hud != null) _hud.PlayManualFade(_fadeDuration);
        yield return new WaitForSeconds(_fadeDuration * 0.5f);
        TeleportPlayer(GetFilterPressStand());
        yield return new WaitForSeconds(_fadeDuration * 0.5f);
        if (_hud != null) _hud.ShowNotifPublic("Cairan MHP dipompa ke FILTER PRESS. Plate menekan, air terperas, padatan jadi cake...", 7f);
        PlayAudio(_doseAudio, 0.30f);

        Transform ram = FindWorldTransform("FilterPress_HydraulicRam_Main");
        Transform cloud = FindWorldTransform("MHP_Precipitation_ProductCloud");
        Vector3 cloudBaseScale = cloud != null ? cloud.localScale : Vector3.one;

        var cakes = new List<Transform>();
        for (int i = 0; i < 16; i++)
        {
            var ct = FindWorldTransform("MHP_Cake_RoughChunk_Tray_" + i.ToString("00"));
            if (ct != null) cakes.Add(ct);
        }
        // Simpan posisi akhir + sembunyikan dulu (cake belum tercetak).
        var cakeEnd = new Vector3[cakes.Count];
        for (int i = 0; i < cakes.Count; i++) { cakeEnd[i] = cakes[i].position; cakes[i].gameObject.SetActive(false); }

        Vector3 ramExit = ram != null ? ram.position : new Vector3(47.6f, 2.74f, 134.96f);
        Vector3 ramBase = ram != null ? ram.localPosition : Vector3.zero;

        // Fase 1: hydraulic ram menekan (maju-mundur) 3x = filter press hidup.
        if (ram != null)
        {
            for (int p = 0; p < 3; p++)
            {
                float tt = 0f;
                while (tt < 0.55f)
                {
                    tt += Time.deltaTime;
                    float k = Mathf.Sin(tt / 0.55f * Mathf.PI);
                    ram.localPosition = ramBase + new Vector3(0.45f * k, 0f, 0f); // dorong ke arah plate frame
                    yield return null;
                }
            }
            ram.localPosition = ramBase;
        }
        PlayAudio(_readyAudio, 0.3f);

        // Fase 2: cake MHP keluar SATU PER SATU dari ram -> meluncur ke posisi tray.
        for (int i = 0; i < cakes.Count; i++)
        {
            var ck = cakes[i];
            ck.gameObject.SetActive(true);
            // sentakan ram tiap cake keluar
            if (ram != null) StartCoroutine(RamKick(ram, ramBase));
            float tt = 0f, dur = 0.42f;
            while (tt < dur)
            {
                tt += Time.deltaTime; float k = Smooth(tt / dur);
                ck.position = Vector3.Lerp(ramExit, cakeEnd[i], k);
                yield return null;
            }
            ck.position = cakeEnd[i];
            // product cloud (cairan MHP) menyusut seiring cake terbentuk.
            if (cloud != null) cloud.localScale = cloudBaseScale * Mathf.Max(0.04f, 1f - (i + 1f) / cakes.Count);
            yield return new WaitForSeconds(0.28f);
        }
        if (cloud != null) cloud.gameObject.SetActive(false);
        if (_hud != null) _hud.ShowNotifPublic("MHP cake padat (Ni-Co hidroksida) tercetak lengkap di tray.", 6f);
        yield return new WaitForSeconds(0.8f);
    }

    // Reset cake + cloud ke kondisi awal (cake disembunyikan, cloud full) supaya finale bisa diulang.
    private Vector3 _cloudBaseScale = Vector3.one; private bool _cloudBaseCaptured;
    private void ResetFilterPressFinale()
    {
        var cloud = FindWorldTransform("MHP_Precipitation_ProductCloud");
        if (cloud != null)
        {
            if (!_cloudBaseCaptured) { _cloudBaseScale = cloud.localScale; _cloudBaseCaptured = true; }
            cloud.localScale = _cloudBaseScale;
            cloud.gameObject.SetActive(true);
        }
        for (int i = 0; i < 16; i++)
        {
            var ct = FindWorldTransform("MHP_Cake_RoughChunk_Tray_" + i.ToString("00"));
            if (ct != null) ct.gameObject.SetActive(false); // cake muncul saat finale
        }
    }

    private IEnumerator RamKick(Transform ram, Vector3 ramBase)
    {
        float tt = 0f;
        while (tt < 0.25f)
        {
            tt += Time.deltaTime; float k = Mathf.Sin(tt / 0.25f * Mathf.PI);
            ram.localPosition = ramBase + new Vector3(0.30f * k, 0f, 0f);
            yield return null;
        }
        ram.localPosition = ramBase;
    }

    private Transform GetFilterPressStand()
    {
        var go = GameObject.Find("L10_FilterPressStand_Runtime") ?? new GameObject("L10_FilterPressStand_Runtime");
        // Area aman di luar envelope filter press. Posisi lama z=129.8 berada di antara
        // frame/guard sehingga camera dapat muncul di dalam mesin.
        Vector3 standPos = new Vector3(47.5f, 0.10f, 125.8f);
        Vector3 lookAt = new Vector3(47.5f, 2.5f, 135.5f);
        go.transform.position = standPos;
        Vector3 look = lookAt - standPos; look.y = 0f;
        go.transform.rotation = look.sqrMagnitude > 0.001f ? Quaternion.LookRotation(look.normalized, Vector3.up) : Quaternion.identity;
        return go.transform;
    }

    private void ProtectMhpFilterPressFromOcclusion()
    {
        Transform ram = FindWorldTransform("FilterPress_HydraulicRam_Main");
        Transform root = ram != null ? ram.parent : null;
        if (root == null)
            return;

        foreach (Renderer renderer in root.GetComponentsInChildren<Renderer>(true))
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

    private Transform FindWorldTransform(string n)
    {
        foreach (var t in Resources.FindObjectsOfTypeAll<Transform>())
            if (t.name == n && t.gameObject.scene.IsValid()) return t;
        return null;
    }

    private void FinishMhp()
    {
        _questComplete = true;
        _glm?.NotifyLevel11MHPComplete();
        if (_hud != null) _hud.ShowNotifPublic("MHP terbentuk & dilaporkan. Produk siap ke gudang. Lanjut level berikutnya.", 7f);
    }

    // Stream cairan jatuh (cylinder dari atas ke tangki).
    private void ShowFillStream(Vector3 top, Vector3 bottom, Color col, bool on)
    {
        if (!on) { if (_mhpStream != null) _mhpStream.SetActive(false); return; }
        if (_mhpStream == null)
        {
            _mhpStream = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            _mhpStream.name = "L10_FillStream_Runtime";
            var col0 = _mhpStream.GetComponent<Collider>(); if (col0 != null) Object.Destroy(col0);
            _mhpStream.transform.SetParent(transform, true);
        }
        _mhpStream.SetActive(true);
        var r = _mhpStream.GetComponent<Renderer>();
        var m = OpaqueMat(col); m.EnableKeyword("_EMISSION"); m.SetColor("_EmissionColor", col * 0.4f);
        r.sharedMaterial = m;
        Vector3 mid = (top + bottom) * 0.5f; Vector3 dir = (bottom - top);
        float len = Mathf.Max(0.5f, dir.magnitude);
        _mhpStream.transform.position = mid;
        _mhpStream.transform.up = dir.sqrMagnitude > 0.001f ? dir.normalized : Vector3.down;
        _mhpStream.transform.localScale = new Vector3(0.35f, len * 0.5f, 0.35f);
    }

    // Gelembung reaksi (ParticleSystem naik).
    // Uap reaksi REALISTIS: netralisasi asam+kapur eksotermik -> uap air tipis (transparan) + sedikit CO2.
    // Bukan asap putih tebal. Warna di-tint halus per tahap (vapor).
    private Color _vaporTint = new Color(0.85f, 0.88f, 0.92f, 0.22f);
    private void ShowBubbles(Vector3 pos, bool on)
    {
        if (!on) { if (_mhpBubbles != null) _mhpBubbles.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear); return; }
        if (_mhpBubbles == null)
        {
            var go = new GameObject("L10_ReactionVapor_Runtime");
            go.transform.SetParent(transform, true);
            _mhpBubbles = go.AddComponent<ParticleSystem>();
            var rend = _mhpBubbles.GetComponent<ParticleSystemRenderer>();
            rend.material = new Material(Shader.Find("Sprites/Default"));
            var main = _mhpBubbles.main;
            main.startSize = 0.9f;          // gumpalan uap besar tipis
            main.startSpeed = 0.7f;         // naik perlahan
            main.startLifetime = 2.6f;
            main.maxParticles = 80;
            main.playOnAwake = false;
            main.gravityModifier = -0.04f;  // sedikit naik (uap panas)
            var sh = _mhpBubbles.shape; sh.shapeType = ParticleSystemShapeType.Cone; sh.angle = 18f; sh.radius = 1.6f; sh.rotation = new Vector3(-90f, 0f, 0f);
            var em = _mhpBubbles.emission; em.rateOverTime = 14f; // tipis, tidak deras
            // fade out (alpha menurun) -> uap menghilang seperti asli
            var col = _mhpBubbles.colorOverLifetime; col.enabled = true;
            var grad = new Gradient();
            grad.SetKeys(
                new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
                new[] { new GradientAlphaKey(0f, 0f), new GradientAlphaKey(0.6f, 0.25f), new GradientAlphaKey(0f, 1f) });
            col.color = grad;
        }
        var m = _mhpBubbles.main; m.startColor = _vaporTint;
        _mhpBubbles.transform.position = pos + Vector3.up * 0.6f;
        _mhpBubbles.Play();
    }

    private void ApplyColor(Renderer r, Color c)
    {
        if (r == null) return;
        // PENTING: URP SRP Batcher mengabaikan MaterialPropertyBlock untuk _BaseColor.
        // Pakai material instance per-renderer supaya warna BENAR-BENAR ter-render.
        var m = r.material; // instance (dibuat sekali, lalu reuse)
        if (m == null) return;
        if (m.HasProperty(IdBase)) m.SetColor(IdBase, c);
        if (m.HasProperty(IdColor)) m.SetColor(IdColor, c);
        m.EnableKeyword("_EMISSION");
        if (m.HasProperty("_EmissionColor")) m.SetColor("_EmissionColor", c * 0.18f);
    }

    private Vector3 FindWorldPos(string n, Vector3 fallback)
    {
        foreach (var t in Resources.FindObjectsOfTypeAll<Transform>())
            if (t.name == n && t.gameObject.scene.IsValid()) return t.position;
        return fallback;
    }

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
        WalkieTalkieManager.OnPTTDilepas += OnMhpHtReleased;
    }

    private void OnDisable()
    {
        GameLevelManager.OnLevelStarted -= OnLevelStarted;
        GameLevelManager.OnDCSButtonPressed -= OnDcsButtonPressed;
        WalkieTalkieManager.OnPTTDilepas -= OnMhpHtReleased;
        if (_seq != null) StopCoroutine(_seq);
        Stop(_agitatorAudio); Stop(_doseAudio);
    }

    private void OnLevelStarted(GameLevelManager.GameLevel level)
    {
        _levelActive = level == GameLevelManager.GameLevel.Level11_MHP;
        if (!_levelActive) { SetProcessVisuals(false); ShowDoseButton(false); ShowInfoPanel(false); HideLab(); Stop(_agitatorAudio); HideDispatchStation(); SetFillStream(false); RestoreWarehouseHeaps(); return; }

        _glm = GameLevelManager.Instance;
        _processStarted = false; _stageIndex = PhaseInitialSample; _dosing = false;
        _mhpFlowActive = false; _mhpBusy = false; _mhpAwait = 0; _mhpTank = 0;
        _pHCurrent = _stages[0].pHFrom; _mhpQuality = 0f;
        _stage1 = _stage2 = _stage3 = _sampleTaken = _labAccepted = _questComplete = false;
        _initialSampleAnalyzed = _feSeparated = _validationSampleTaken = _transferValveOpen = _filterProductDone = false;
        _feConcentration = 3.8f; _alConcentration = 1.7f; _niConcentration = 5.1f; _coConcentration = 0.52f;
        _reagentFlow = 0f; _tankLevel = 62f; _turbidity = 95f;
        _warehouseStarted = _dispatching = _baggingDone = false; _dispatchProgress = 0f; HideDispatchStation(); SetFillStream(false); EnsureWarehouseRefs(); RestoreWarehouseHeaps();
        PushPH(); SetProcessVisuals(false); ShowDoseButton(false); ShowInfoPanel(false); HideLab();
        HideAllTankLiquids();
        ProtectMhpFilterPressFromOcclusion();
        ResetFilterPressFinale();
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
        StartMhpFlow(); // hide liquids, teleport tangki 1, tunggu HT
        yield return new WaitForSeconds(_fadeDuration * 0.5f);
        _seq = null;
    }

    private void Update()
    {
        if (!_levelActive || !_processStarted) return;
        AnimateAgitators();
        if (_mhpFlowActive) return; // flow baru di-drive event HT + coroutine
        if (_dosing) AnimateSkid();

        // Operator input (keyboard fallback): SPACE atau 1
        if (!_dosing && (Input.GetKeyDown(KeyCode.Space) || Input.GetKeyDown(KeyCode.Alpha1)))
            TryOperatorAction();

        // Legacy proximity fallback untuk sample MHP kalau player mendekati stasiun sample.
        if (_stageIndex == PhaseFilterProduct && !_filterProductDone) UpdateSamplingProximity();

        // Lab submit (L) + Accept (Enter) fallback
        if (_stageIndex == PhaseEvaluation && !_labAccepted)
        {
            if (_labCanvas == null && Input.GetKeyDown(KeyCode.L)) ShowLabCanvas();
            if (_labCanvas != null && _labCanvas.activeSelf && _pendingClick != null && Input.GetKeyDown(KeyCode.Return))
            { var a = _pendingClick; _pendingClick = null; a(); }
        }
        if (_stageIndex == PhaseWarehouse && !_baggingDone && !_dispatching && (Input.GetKeyDown(KeyCode.Space) || Input.GetKeyDown(KeyCode.Alpha1)))
            TryDispatch();
        if (_dispatching) UpdateFillStream();
        if (_warehouseStarted) UpdateWarehousePanel();
        UpdateInfoPanel();
    }

    // ============================================================ DOSING
    private void BeginOperatorStep()
    {
        ShowInfoPanel(true);
        ShowDoseButton(true);
        if (_hud != null)
            _hud.ShowNotifPublic(GetCurrentOperatorInstruction(), 7f);
    }

    private string GetCurrentOperatorInstruction()
    {
        switch (_stageIndex)
        {
            case PhaseInitialSample: return "Ambil sampel PLS outlet CCD, masukkan ke analyzer, lalu baca Fe/Al.";
            case PhaseFeDosing: return "Aktifkan dosing pump Fe removal. Target pH 2.5.";
            case PhaseFeSeparation: return "Pisahkan endapan Fe coklat. Pastikan overflow lebih jernih.";
            case PhaseAlCrDosing: return "Lanjut dosing Al/Cr removal. Target pH 4.0.";
            case PhaseValidationSample: return "Ambil sampel validasi. Fe/Al harus rendah, Ni-Co masih larut.";
            case PhaseTransferValve: return "Buka valve transfer ke MHP tank. Line tailing harus tertutup.";
            case PhaseMhpDosing: return "Naikkan pH sekitar 7.0 untuk mengendapkan Ni-Co menjadi MHP.";
            case PhaseFilterProduct: return "Filter slurry MHP menjadi wet cake produk.";
            case PhaseEvaluation: return "Baca evaluasi kualitas proses MHP.";
            default: return "Lanjutkan prosedur MHP.";
        }
    }

private string GetPhaseName()
    {
        switch (_stageIndex)
        {
            case PhaseInitialSample: return "Sample PLS outlet CCD + analyzer";
            case PhaseFeDosing: return "Fe removal - naikkan pH ke 2.5";
            case PhaseFeSeparation: return "Pemisahan endapan Fe";
            case PhaseAlCrDosing: return "Al/Cr removal - naikkan pH ke 4.0";
            case PhaseValidationSample: return "Sample validasi impurity";
            case PhaseTransferValve: return "Transfer valve ke MHP tank";
            case PhaseMhpDosing: return "Presipitasi MHP - pH sekitar 7";
            case PhaseFilterProduct: return "Filter produk MHP";
            case PhaseEvaluation: return "Evaluasi kualitas proses";
            case PhaseWarehouse: return "Bagging & dispatch MHP";
            default: return "Purification / MHP";
        }
    }

private string GetActionButtonLabel()
    {
        switch (_stageIndex)
        {
            case PhaseInitialSample: return "AMBIL SAMPLE\n+ ANALYZER";
            case PhaseFeDosing: return "DOSING Fe\npH 2.5";
            case PhaseFeSeparation: return "PISAHKAN\nENDAPAN Fe";
            case PhaseAlCrDosing: return "DOSING Al/Cr\npH 4.0";
            case PhaseValidationSample: return "SAMPLE\nVALIDASI";
            case PhaseTransferValve: return "BUKA VALVE\nKE MHP";
            case PhaseMhpDosing: return "DOSING MHP\npH 7";
            case PhaseFilterProduct: return "FILTER\nMHP CAKE";
            case PhaseEvaluation: return "BACA\nEVALUASI";
            default: return "AKSI\nOPERATOR";
        }
    }



    private void TryOperatorAction()
    {
        if (_dosing) return;

        switch (_stageIndex)
        {
            case PhaseInitialSample:
                _initialSampleAnalyzed = true;
                _stageIndex = PhaseFeDosing;
                if (_hud != null) _hud.ShowNotifPublic("Analyzer: Fe dan Al masih tinggi. Lakukan Fe removal pH 2.5.", 6f);
                BeginDoseStage();
                break;
            case PhaseFeDosing:
            case PhaseAlCrDosing:
            case PhaseMhpDosing:
                TryDose();
                break;
            case PhaseFeSeparation:
                _feSeparated = true;
                _turbidity = 45f;
                _stageIndex = PhaseAlCrDosing;
                if (_hud != null) _hud.ShowNotifPublic("Endapan Fe dipisahkan. Overflow Ni-Co lanjut ke Al/Cr removal.", 6f);
                BeginDoseStage();
                break;
            case PhaseValidationSample:
                _validationSampleTaken = true;
                _sampleTaken = true;
                _stageIndex = PhaseTransferValve;
                if (_hud != null) _hud.ShowNotifPublic("Validasi OK: Fe/Al rendah, Ni-Co masih tinggi. Buka valve ke MHP tank.", 6f);
                BeginOperatorStep();
                break;
            case PhaseTransferValve:
                _transferValveOpen = true;
                _stageIndex = PhaseMhpDosing;
                if (_polishToMhpLiquid != null) _polishToMhpLiquid.SetActive(true);
                if (_hud != null) _hud.ShowNotifPublic("Valve ke MHP tank terbuka. Mulai presipitasi Ni-Co.", 6f);
                BeginDoseStage();
                break;
            case PhaseFilterProduct:
                _filterProductDone = true;
                _stageIndex = PhaseEvaluation;
                ShowDoseButton(false);
                if (_mhpSampleProduct != null) { _mhpSampleProduct.SetActive(true); Tint(_mhpSampleProduct, new Color(0.15f, 0.6f, 0.38f)); }
                if (_hud != null) _hud.ShowNotifPublic("Filter press selesai. Wet cake MHP terbentuk. Tekan L untuk evaluasi kualitas.", 6f);
                break;
        }
    }

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
        if (_dosing || _stageIndex < 0 || _stageIndex > 2) return;
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
            _reagentFlow = Mathf.Lerp(0f, idx == 2 ? 18f : 12f, e);
            _tankLevel = Mathf.Lerp(_tankLevel, idx == 2 ? 78f : 68f, Time.deltaTime * 0.35f);
            if (idx == 0) _feConcentration = Mathf.Lerp(3.8f, 0.22f, e);
            if (idx == 1) { _feConcentration = Mathf.Lerp(0.22f, 0.06f, e); _alConcentration = Mathf.Lerp(1.7f, 0.08f, e); _turbidity = Mathf.Lerp(45f, 18f, e); }
            if (idx == 2) { _niConcentration = Mathf.Lerp(5.1f, 0.20f, e); _coConcentration = Mathf.Lerp(0.52f, 0.03f, e); _turbidity = Mathf.Lerp(18f, 88f, e); }
            PushPH();
            TintStageLiquid(idx, e);
            if (idx == 2) _mhpQuality = Mathf.Lerp(0f, 92f, e);
            UpdateFx(idx, e);
            yield return null;
        }
        _pHCurrent = s.pHTo; PushPH(); Stop(_doseAudio); _dosing = false;

        _reagentFlow = 0f;
        if (idx == 0) { _stage1 = true; if (_neutralToPolishLiquid != null) _neutralToPolishLiquid.SetActive(true); }
        else if (idx == 1) { _stage2 = true; if (_polishToMhpLiquid != null) _polishToMhpLiquid.SetActive(true); }
        else if (idx == 2)
        {
            _stage3 = true; _mhpQuality = 92f;
            if (_mhpSampleFlow != null) _mhpSampleFlow.SetActive(true);
            PlayAudio(_readyAudio, 0.32f);
        }

        if (idx == 0)
        {
            _stageIndex = PhaseFeSeparation;
            if (_hud != null) _hud.ShowNotifPublic("Fe removal selesai. Pisahkan endapan Fe coklat sebelum lanjut.", 7f);
            BeginOperatorStep();
        }
        else if (idx == 1)
        {
            _stageIndex = PhaseValidationSample;
            if (_hud != null) _hud.ShowNotifPublic("Al/Cr removal selesai. Ambil sampel validasi.", 7f);
            BeginOperatorStep();
        }
        else
        {
            _stageIndex = PhaseFilterProduct;
            if (_hud != null) _hud.ShowNotifPublic("Ni-Co mengendap jadi MHP. Jalankan filter press produk.", 7f);
            BeginOperatorStep();
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
            TryOperatorAction();
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
        _labCanvas.name = "L10_MHP_ProcessEvaluation";
        Object.Destroy(_labCanvas.GetComponent<Collider>());
        _labCanvas.transform.position = pos;
        _labCanvas.transform.localScale = new Vector3(1.85f, 1.18f, 1f);
        var qr = _labCanvas.GetComponent<Renderer>();
        qr.sharedMaterial = OpaqueMat(new Color(0.05f, 0.09f, 0.13f));

        _labText = MakeText(_labCanvas.transform, new Vector3(0f, 0f, -0.02f), 0.044f, TextAnchor.MiddleCenter, new Color(0.85f, 1f, 0.9f));
        _labText.text =
            "=== EVALUASI PROSES PURIFICATION - MHP ===\n" +
            "Sample awal PLS : Fe/Al tinggi, pH sangat asam\n" +
            "Fe removal      : pH 2.5, Fe turun ke " + _feConcentration.ToString("0.00") + " g/L\n" +
            "Al/Cr removal   : pH 4.0, Al turun ke " + _alConcentration.ToString("0.00") + " g/L\n" +
            "Transfer MHP    : valve OPEN, Ni-Co solution masuk tank\n" +
            "Presipitasi     : pH " + _pHCurrent.ToString("0.0") + ", Ni-Co mengendap sebagai MHP\n" +
            "Filter product  : wet cake MHP terbentuk\n" +
            "Ni recovery 94% | Co recovery 92% | Fe/Al impurity LOW\n" +
            "VERDICT: PASS - proses siap bagging dan dispatch.";

        var btn = GameObject.CreatePrimitive(PrimitiveType.Cube);
        btn.name = "L10_Evaluation_Accept";
        btn.transform.SetParent(_labCanvas.transform, false);
        btn.transform.localPosition = new Vector3(0f, -0.44f, -0.05f);
        btn.transform.localScale = new Vector3(0.44f, 0.16f, 0.06f);
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

        _doseButton = GameObject.CreatePrimitive(PrimitiveType.Cube);
        _doseButton.name = "L10_OperatorActionButton";
        _doseButton.transform.SetParent(transform, false);
        _doseButton.transform.position = consolePos;
        _doseButton.transform.localScale = new Vector3(0.74f, 0.30f, 0.18f);
        _doseButton.GetComponent<Renderer>().sharedMaterial = OpaqueMat(new Color(0.9f, 0.55f, 0.1f));
        _doseLabel = MakeText(_doseButton.transform, new Vector3(0f, 0f, -0.6f), 0.105f, TextAnchor.MiddleCenter, Color.black);
        _doseLabel.text = "AKSI OPERATOR";
        StartCoroutine(AttachXrButton(_doseButton, TryOperatorAction));

        _infoPanel = GameObject.CreatePrimitive(PrimitiveType.Quad);
        _infoPanel.name = "L10_Purification_DCS_Panel";
        Object.Destroy(_infoPanel.GetComponent<Collider>());
        _infoPanel.transform.SetParent(transform, false);
        _infoPanel.transform.position = consolePos + new Vector3(0f, 1.05f, 0.1f);
        _infoPanel.transform.localScale = new Vector3(2.2f, 1.45f, 1f);
        _infoPanel.GetComponent<Renderer>().sharedMaterial = OpaqueMat(new Color(0.05f, 0.08f, 0.12f));
        _infoText = MakeText(_infoPanel.transform, new Vector3(0f, 0f, -0.02f), 0.041f, TextAnchor.MiddleCenter, new Color(0.8f, 0.95f, 1f));
        ShowDoseButton(false); ShowInfoPanel(false);
    }

private void UpdateInfoPanel()
    {
        if (_infoText == null || _infoPanel == null || !_infoPanel.activeSelf) return;

        string body;
        if (_stageIndex >= 0 && _stageIndex <= 2)
        {
            var s = _stages[_stageIndex];
            body = "PURIFICATION / MHP - DCS PANEL\n" +
                   "Tahap : " + GetPhaseName() + "\n" +
                   "Reagen : " + s.reagent + "\n" +
                   "Target pH : " + s.pHFrom.ToString("0.0") + " -> " + s.pHTo.ToString("0.0") + "\n" +
                   "Fungsi : " + s.removes;
        }
        else
        {
            body = "PURIFICATION / MHP - DCS PANEL\nTahap : " + GetPhaseName() + "\nInstruksi : " + GetCurrentOperatorInstruction();
        }

        _infoText.text = body +
            "\n--------------------------------" +
            "\npH              : " + _pHCurrent.ToString("0.00") +
            "\nFe concentration: " + _feConcentration.ToString("0.00") + " g/L" +
            "\nAl concentration: " + _alConcentration.ToString("0.00") + " g/L" +
            "\nNi concentration: " + _niConcentration.ToString("0.00") + " g/L" +
            "\nCo concentration: " + _coConcentration.ToString("0.00") + " g/L" +
            "\nReagent flow    : " + _reagentFlow.ToString("0.0") + " m3/h" +
            "\nAgitator status : " + ((_agitatorAudio != null && _agitatorAudio.isPlaying) ? "RUNNING" : "STOP") +
            "\nTank level      : " + _tankLevel.ToString("0") + " %" +
            "\nTurbidity       : " + _turbidity.ToString("0") + " NTU" +
            "\nValve to MHP    : " + (_transferValveOpen ? "OPEN" : "CLOSED") +
            "\nMHP quality     : " + _mhpQuality.ToString("0") + " %";
        BillboardTo(_infoPanel.transform, GetPlayerHead());
    }

private void ShowDoseButton(bool on)
    {
        if (_doseButton != null) _doseButton.SetActive(on);
        if (on && _stageIndex >= 0 && _stageIndex <= 2) ResolveSkidMotors(_stageIndex);
        if (on && _doseLabel != null) _doseLabel.text = GetActionButtonLabel();
    }
    private void ShowInfoPanel(bool on) { if (_infoPanel != null) _infoPanel.SetActive(false); }

    // ============================================================ HELPERS
    private void AnimateAgitators()
    {
        if (!_rotorOn) return; // rotor baru berputar setelah HT#2 (dosing)
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
