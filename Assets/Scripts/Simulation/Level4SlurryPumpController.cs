using System.Collections;
using UnityEngine;

/// <summary>
/// OLIVIA VR - Level4SlurryPumpController.cs
///
/// Mengatur sub-sequence Level 4 (Slurry Pump):
///   1. AturFlowRate (di DCS)               — player tekan tombol +/- sampai 450
///   2. MenungguLaporanFlow                  — voice "slurry pump aktif"
///   3. ObservasiPump (auto-teleport)        — fade ke depan SlurryPump_Field, lihat aliran
///   4. MenungguLaporanAlir                  — voice "slurry mengalirkan air"
///   5. ObservasiPreheater (auto-teleport)   — fade ke depan PreHeater, observe
///   6. KembaliKeDcs (auto-teleport)         — fade balik ke DCS
///   7. Selesai                              — trigger Level 5
///
/// Pemakaian:
///   1. Buat empty GameObject "Level4Controller" di scene.
///   2. Attach script ini.
///   3. Assign _teleportTargetPump   = Transform titik observasi pump (depan SlurryPump_Field)
///       _teleportTargetPreheater   = Transform titik observasi preheater
///       _teleportTargetDcs         = SpawnPoint_Lvl4 (atau SpawnPoint_DCS)
///   4. Auto-find PlayerHUD + XR Origin (CharacterController).
/// </summary>
public class Level4SlurryPumpController : MonoBehaviour
{
    [Header("=== Referensi Pemain ===")]
    [Tooltip("Root XR Origin / XR Rig. Auto-find jika kosong.")]
    [SerializeField] private Transform _playerRigRoot;
    [SerializeField] private CharacterController _playerCharacterController;

    [Header("=== Titik Teleport ===")]
    [Tooltip("Posisi observasi pump (depan SlurryPump_Field). Auto-create kalau kosong.")]
    [SerializeField] private Transform _teleportTargetPump;
    [Tooltip("Posisi observasi pre-heater. Auto-create kalau kosong.")]
    [SerializeField] private Transform _teleportTargetPreheater;
    [Tooltip("Posisi spawn DCS untuk balik ke control room. Auto-find SpawnPoint_Lvl4 / SpawnPoint_DCS jika kosong.")]
    [SerializeField] private Transform _teleportTargetDcs;

    [Header("=== Auto-Find Reference ===")]
    [Tooltip("GameObject pump di scene (sumber observasi). Auto-find jika kosong.")]
    [SerializeField] private GameObject _pumpReference;
    [Tooltip("GameObject pre-heater di scene. Auto-find jika kosong.")]
    [SerializeField] private GameObject _preheaterReference;
    [Tooltip("Offset dari pump untuk titik observasi (dari pivot).")]
    [SerializeField] private Vector3 _offsetObservasiPump = new Vector3(-2.5f, 1.6f, 3.5f);
    [Tooltip("Offset dari preheater untuk titik observasi.")]
    [SerializeField] private Vector3 _offsetObservasiPreheater = new Vector3(0f, 1.6f, 4.5f);

    [Header("=== Timing ===")]
    [Tooltip("Jeda setelah laporan flow diterima sebelum auto-teleport ke pump.")]
    [SerializeField] private float _jedaSebelumKePump = 1.5f;
    [Tooltip("Durasi fade saat teleport.")]
    [SerializeField] private float _durasiFade = 1.8f;
    [Tooltip("Durasi observasi pump sebelum HUD minta laporan kedua.")]
    [SerializeField] private float _durasiObservasiPump = 6f;
    [Tooltip("Durasi observasi pre-heater sebelum auto-balik ke DCS.")]
    [SerializeField] private float _durasiObservasiPreheater = 6f;
    [Tooltip("Jeda setelah balik ke DCS sebelum level di-mark selesai.")]
    [SerializeField] private float _jedaSetelahKembaliDcs = 1.0f;

    [Header("=== HUD Pesan ===")]
    [TextArea(2, 4)] [SerializeField] private string _pesanObservasiPump =
        "Lihat slurry pump mengalirkan air ke pipa. Lapor: \"slurry mengalirkan air\".";
    [TextArea(2, 4)] [SerializeField] private string _pesanObservasiPreheater =
        "Pindah pandangan ke pre-heater. Pastikan slurry masuk ke unit pemanas.";
    [TextArea(2, 4)] [SerializeField] private string _pesanKembaliDcs =
        "Mantap. Kembali ke DCS untuk operasi berikutnya.";

    [Header("=== Visual Indicator ===")]
    [Tooltip("Highlight pump saat fase ObservasiPump (toggle emission).")]
    [SerializeField] private Renderer[] _pumpHighlightRenderers;
    [Tooltip("Highlight preheater saat fase ObservasiPreheater.")]
    [SerializeField] private Renderer[] _preheaterHighlightRenderers;
    [SerializeField] private Color _warnaHighlight = new Color(0.2f, 1f, 0.5f, 1f);
    [SerializeField] private float _intensitasHighlight = 1.6f;

    private PlayerHUD _hud;
    private Coroutine _seqCoroutine;
    private MaterialPropertyBlock _mpb;
    private bool _highlightPumpAktif;
    private bool _highlightPreheaterAktif;

    private void Awake()
    {
        _hud = FindObjectOfType<PlayerHUD>();
        AutoFindPlayerRig();
        AutoFindReferences();
        _mpb = new MaterialPropertyBlock();
    }

    private void OnEnable()
    {
        GameLevelManager.OnLevelStarted += OnLevelStarted;
        GameLevelManager.OnLevel4PhaseChanged += OnLevel4PhaseChanged;
    }

    private void OnDisable()
    {
        GameLevelManager.OnLevelStarted -= OnLevelStarted;
        GameLevelManager.OnLevel4PhaseChanged -= OnLevel4PhaseChanged;

        if (_seqCoroutine != null)
        {
            StopCoroutine(_seqCoroutine);
            _seqCoroutine = null;
        }
    }

    private void OnLevelStarted(GameLevelManager.GameLevel level)
    {
        // Reset semua state saat level berubah.
        if (_seqCoroutine != null)
        {
            StopCoroutine(_seqCoroutine);
            _seqCoroutine = null;
        }
        SetHighlight(_pumpHighlightRenderers, false, ref _highlightPumpAktif);
        SetHighlight(_preheaterHighlightRenderers, false, ref _highlightPreheaterAktif);
    }

    private void OnLevel4PhaseChanged(GameLevelManager.Level4Phase phase)
    {
        if (GameLevelManager.Instance == null ||
            GameLevelManager.Instance.CurrentLevel != GameLevelManager.GameLevel.Level4_SlurryPump)
            return;

        switch (phase)
        {
            case GameLevelManager.Level4Phase.MenungguLaporanFlow:
                // Player masih di DCS. HUD sudah handle quest update via OnLevel4PhaseChanged.
                break;

            case GameLevelManager.Level4Phase.ObservasiPump:
                // Voice flow diterima. Auto-teleport ke depan pump.
                StartSequence(SeqObservasiPump());
                break;

            case GameLevelManager.Level4Phase.MenungguLaporanAlir:
                // Player sudah di pump, tinggal voice "slurry mengalirkan air".
                if (_hud != null) _hud.ShowNotifPublic(_pesanObservasiPump);
                break;

            case GameLevelManager.Level4Phase.ObservasiPreheater:
                // Voice diterima. Auto-teleport ke preheater.
                StartSequence(SeqObservasiPreheater());
                break;

            case GameLevelManager.Level4Phase.KembaliKeDcs:
                // Auto-balik ke DCS dan tutup level.
                StartSequence(SeqKembaliKeDcs());
                break;
        }
    }

    private void StartSequence(IEnumerator seq)
    {
        if (_seqCoroutine != null) StopCoroutine(_seqCoroutine);
        _seqCoroutine = StartCoroutine(seq);
    }

    private IEnumerator SeqObservasiPump()
    {
        yield return new WaitForSeconds(_jedaSebelumKePump);

        if (_hud != null) _hud.PlayManualFade(_durasiFade);
        yield return new WaitForSeconds(_durasiFade * 0.45f);

        TeleportPlayer(EnsureTeleportTarget(ref _teleportTargetPump, _pumpReference, _offsetObservasiPump, "L4_ObservasiPump"));

        SetHighlight(_pumpHighlightRenderers, true, ref _highlightPumpAktif);
        if (_hud != null) _hud.ShowNotifPublic(_pesanObservasiPump);

        // Tunggu durasi observasi sebelum minta laporan kedua.
        yield return new WaitForSeconds(_durasiObservasiPump);

        // Promote phase ke MenungguLaporanAlir agar voice handler bisa accept.
        GameLevelManager.Instance?.NotifyLevel4PhaseAdvance(GameLevelManager.Level4Phase.MenungguLaporanAlir);
        _seqCoroutine = null;
    }

    private IEnumerator SeqObservasiPreheater()
    {
        SetHighlight(_pumpHighlightRenderers, false, ref _highlightPumpAktif);

        if (_hud != null) _hud.PlayManualFade(_durasiFade);
        yield return new WaitForSeconds(_durasiFade * 0.45f);

        TeleportPlayer(EnsureTeleportTarget(ref _teleportTargetPreheater, _preheaterReference, _offsetObservasiPreheater, "L4_ObservasiPreheater"));

        SetHighlight(_preheaterHighlightRenderers, true, ref _highlightPreheaterAktif);
        if (_hud != null) _hud.ShowNotifPublic(_pesanObservasiPreheater);

        yield return new WaitForSeconds(_durasiObservasiPreheater);

        // Auto-promote ke KembaliKeDcs.
        GameLevelManager.Instance?.NotifyLevel4PhaseAdvance(GameLevelManager.Level4Phase.KembaliKeDcs);
        _seqCoroutine = null;
    }

    private IEnumerator SeqKembaliKeDcs()
    {
        SetHighlight(_preheaterHighlightRenderers, false, ref _highlightPreheaterAktif);

        if (_hud != null) _hud.PlayManualFade(_durasiFade);
        yield return new WaitForSeconds(_durasiFade * 0.45f);

        var dcsTarget = EnsureDcsTarget();
        TeleportPlayer(dcsTarget);

        if (_hud != null) _hud.ShowNotifPublic(_pesanKembaliDcs);

        yield return new WaitForSeconds(_jedaSetelahKembaliDcs);

        // Mark Level 4 selesai → akan trigger transisi otomatis ke Level 5.
        GameLevelManager.Instance?.NotifyLevel4Selesai();
        _seqCoroutine = null;
    }

    // ============================================================
    //  HELPERS
    // ============================================================

    private void AutoFindPlayerRig()
    {
        if (_playerRigRoot == null)
        {
            var rig = GameObject.Find("XR Origin (XR Rig)")
                   ?? GameObject.Find("XR Origin")
                   ?? GameObject.Find("XR Rig")
                   ?? GameObject.FindWithTag("Player");
            if (rig != null) _playerRigRoot = rig.transform;
        }

        if (_playerCharacterController == null && _playerRigRoot != null)
            _playerCharacterController = _playerRigRoot.GetComponent<CharacterController>();
    }

    private void AutoFindReferences()
    {
        if (_pumpReference == null)
            _pumpReference = GameObject.Find("SlurryPump_Field");

        if (_preheaterReference == null)
            _preheaterReference = GameObject.Find("PreHeater_Field")
                                ?? GameObject.Find("Preheater_Field")
                                ?? GameObject.Find("PreHeater")
                                ?? GameObject.Find("Preheater");
    }

    private Transform EnsureTeleportTarget(ref Transform field, GameObject reference, Vector3 offset, string runtimeName)
    {
        if (field != null) return field;

        if (reference == null)
        {
            Debug.LogWarning($"[Level4Controller] Reference untuk titik teleport '{runtimeName}' tidak ditemukan.");
            return null;
        }

        var go = new GameObject(runtimeName);
        go.transform.SetParent(reference.transform.parent != null ? reference.transform.parent : null, false);
        go.transform.position = reference.transform.position + offset;
        // Hadap ke pump/preheater
        Vector3 lookDir = reference.transform.position - go.transform.position;
        lookDir.y = 0f;
        if (lookDir.sqrMagnitude > 0.0001f)
            go.transform.rotation = Quaternion.LookRotation(lookDir.normalized);

        field = go.transform;
        return field;
    }

    private Transform EnsureDcsTarget()
    {
        if (_teleportTargetDcs != null) return _teleportTargetDcs;

        var go = GameObject.Find("SpawnPoint_Lvl4")
              ?? GameObject.Find("SpawnPoint_DCS");
        if (go != null) _teleportTargetDcs = go.transform;
        return _teleportTargetDcs;
    }

    private void TeleportPlayer(Transform target)
    {
        if (target == null)
        {
            Debug.LogWarning("[Level4Controller] Teleport target null.");
            return;
        }
        if (_playerRigRoot == null) AutoFindPlayerRig();
        if (_playerRigRoot == null)
        {
            Debug.LogWarning("[Level4Controller] Player rig tidak ditemukan.");
            return;
        }

        bool ccEnabled = _playerCharacterController != null && _playerCharacterController.enabled;
        if (ccEnabled) _playerCharacterController.enabled = false;

        _playerRigRoot.position = target.position;
        _playerRigRoot.rotation = target.rotation;

        if (ccEnabled) _playerCharacterController.enabled = true;
    }

    private void SetHighlight(Renderer[] renderers, bool aktif, ref bool flag)
    {
        if (renderers == null) return;
        flag = aktif;
        foreach (var r in renderers)
        {
            if (r == null) continue;
            r.GetPropertyBlock(_mpb);
            Color emission = aktif ? _warnaHighlight * _intensitasHighlight : Color.black;
            _mpb.SetColor("_EmissionColor", emission);
            r.SetPropertyBlock(_mpb);
            if (aktif && r.sharedMaterial != null)
                r.sharedMaterial.EnableKeyword("_EMISSION");
        }
    }
}
