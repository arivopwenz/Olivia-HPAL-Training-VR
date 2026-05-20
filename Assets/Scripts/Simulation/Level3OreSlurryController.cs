using System.Collections;
using UnityEngine;

/// <summary>
/// Mengatur sub-sequence Level 3:
/// laporan HT awal, fade ke area crusher, observasi ore + air, slurry 25%, lalu siap laporan akhir.
/// </summary>
public class Level3OreSlurryController : MonoBehaviour
{
    [Header("=== Referensi Pemain ===")]
    [SerializeField] private Transform _playerRigRoot;
    [SerializeField] private CharacterController _playerCharacterController;

    [Header("=== Titik Teleport ===")]
    [SerializeField] private Transform _teleportTargetField;
    [SerializeField] private Transform _teleportTargetObservation;
    [SerializeField] private Transform _teleportTargetDcs;

    [Header("=== Visual Ore dan Air ===")]
    [SerializeField] private Transform _oreMover;
    [SerializeField] private Transform _oreStartPoint;
    [SerializeField] private Transform _oreMidPoint;
    [SerializeField] private Transform _oreEndPoint;
    [SerializeField] private GameObject _waterFx;
    [SerializeField] private GameObject[] _aktifSaatObservasi;
    [SerializeField] private bool _sembunyikanOreSetelahMasukTank = true;

    [Header("=== APD Area Crusher / Slurry ===")]
    [SerializeField] private bool _wajibKacamataRespiratorSebelumKeLapangan = true;

    [Header("=== Visual Level Slurry ===")]
    [SerializeField] private Transform _slurryFill;
    [SerializeField] private Transform _slurryBatas25;
    [SerializeField] private Collider _slurryTrigger25;
    [SerializeField] private Vector3 _slurryLocalScaleAwal = new Vector3(1f, 0.08f, 1f);
    [SerializeField] private Vector3 _slurryLocalScaleTarget25 = new Vector3(1f, 0.50f, 1f);
    [SerializeField] private Vector3 _slurryLocalPosAwal = new Vector3(0f, -0.45f, 0f);
    [SerializeField] private Vector3 _slurryLocalPosTarget25 = new Vector3(0f, 0.0f, 0f);
    [SerializeField] private bool _aktifkanSlurryFillSaatMulaiIsi = true;
    [SerializeField] private bool _pertahankanDiameterSlurryDariScene = true;
    [SerializeField] private bool _validasiSlurry25PakaiBatasFisik = false;

    [Header("=== Timing Sequence ===")]
    [SerializeField] private float _jedaSetelahLaporanAwal = 2.2f;
    [SerializeField] private float _durasiFadeKeField = 3.1f;
    [SerializeField] private float _jedaSebelumOreJalan = 0.7f;
    [SerializeField] private float _durasiGerakOre = 4.8f;
    [SerializeField] private float _jedaSetelahOreMasuk = 0.5f;
    [SerializeField] private float _durasiIsiSlurry = 5.5f;
    [SerializeField] private float _offsetAmanDiAtasLantai = 0.15f;
    [SerializeField] private float _jarakRaycastLantai = 40f;
    [SerializeField] private bool _gunakanRaycastLantaiSaatTeleport = false;
    [SerializeField] private bool _teleportKeTitikObservasi = true;

    [Header("=== Safety Runtime ===")]
    [SerializeField] private bool _buatPlatformObservasiOtomatis = true;
    [SerializeField] private Vector3 _ukuranPlatformObservasi = new Vector3(8f, 0.5f, 8f);
    [SerializeField] private bool _buatSafetyFloorOtomatis = true;
    [SerializeField] private Vector3 _ukuranSafetyFloor = new Vector3(24f, 1f, 24f);

    [Header("=== Masker di Field (Chest Grab Mode) ===")]
    [Tooltip("Socket di dada/baju player yang menampung masker. Saat masuk field, socket ini di-highlight supaya player tahu harus ambil masker dari sini.")]
    [SerializeField] private Transform _socketMaskerBaju;
    [Tooltip("Renderer pada masker yang akan diberi outline/glow saat menunggu dipakai. Boleh dikosongkan untuk auto-find dari child Socket_Respirator_Baju.")]
    [SerializeField] private Renderer _maskerRendererUntukGlow;
    [Tooltip("Warna glow saat masker menunggu diambil dari dada.")]
    [SerializeField] private Color _warnaGlowMasker = new Color(1f, 0.85f, 0.2f, 1f);
    [Tooltip("Intensitas emisi glow masker.")]
    [SerializeField, Range(0.1f, 6f)] private float _intensitasGlowMasker = 2.4f;
    [Tooltip("Pesan HUD saat player baru sampai field dan harus pakai masker.")]
    [TextArea(2, 4)]
    [SerializeField] private string _pesanInstruksiMasker =
        "Ambil masker di dadamu lalu arahkan ke wajah sebelum mendekati slurry tank.";

    [Header("=== FX Tambahan ===")]
    [Tooltip("Reference ke SlurryFXController (audio splash + bubble particle). Auto-add ke Slurry_Fill jika kosong.")]
    [SerializeField] private SlurryFXController _slurryFx;
    [Tooltip("Reference ke SlurryAgitator (pengaduk slurry tank). Auto-find di scene jika kosong.")]
    [SerializeField] private SlurryAgitator _slurryAgitator;
    [Tooltip("Aktifkan agitator saat slurry mencapai 50% (siap laporan akhir).")]
    [SerializeField] private bool _aktifkanAgitatorSaatSiapLapor = true;
    [Tooltip("Reference ke arrow indicator yang menunjuk ke slurry tank. Auto-create di runtime jika kosong.")]
    [SerializeField] private DirectionArrowIndicator _arrowIndicator;
    [Tooltip("Aktifkan arrow indicator saat sampai field menunggu APD + saat menuju observation point.")]
    [SerializeField] private bool _gunakanArrowIndicator = true;

    private PlayerHUD _hud;
    private Coroutine _sequenceCoroutine;
    private Coroutine _returnCoroutine;
    private bool _sequenceSudahDimulai;
    private bool _teleportSudahDimulai;
    private bool _slurry25SudahTriggered;
    private Collider _slurryFillCollider;
    private Vector3 _slurryScaleSceneAwal = Vector3.one;
    private GameObject _platformObservasiRuntime;
    private GameObject _safetyFloorRuntime;
    private MaterialPropertyBlock _glowMpb;
    private bool _glowMaskerAktif;

    private void Awake()
    {
        _hud = FindObjectOfType<PlayerHUD>();
        if (_playerRigRoot == null && Camera.main != null)
            _playerRigRoot = Camera.main.transform.root;

        if (_playerCharacterController == null && _playerRigRoot != null)
            _playerCharacterController = _playerRigRoot.GetComponent<CharacterController>();

        if (_slurryFill != null)
            _slurryScaleSceneAwal = _slurryFill.localScale;

        CacheColliders();
        SiapkanSafetyRuntime();
        ResetVisualState();
    }

    private void OnEnable()
    {
        GameLevelManager.OnLevelStarted += OnLevelStarted;
        GameLevelManager.OnVoiceReportAccepted += OnVoiceReportAccepted;
        GameLevelManager.OnLevelTransitionRequested += OnLevelTransitionRequested;
        GameLevelManager.OnLevel3PhaseChanged += OnLevel3PhaseChanged;
        PhaseManager.OnApdItemWorn += OnApdItemWorn;
    }

    private void OnDisable()
    {
        GameLevelManager.OnLevelStarted -= OnLevelStarted;
        GameLevelManager.OnVoiceReportAccepted -= OnVoiceReportAccepted;
        GameLevelManager.OnLevelTransitionRequested -= OnLevelTransitionRequested;
        GameLevelManager.OnLevel3PhaseChanged -= OnLevel3PhaseChanged;
        PhaseManager.OnApdItemWorn -= OnApdItemWorn;
    }

    private void OnLevelStarted(GameLevelManager.GameLevel level)
    {
        if (_sequenceCoroutine != null)
        {
            StopCoroutine(_sequenceCoroutine);
            _sequenceCoroutine = null;
        }

        if (_returnCoroutine != null)
        {
            StopCoroutine(_returnCoroutine);
            _returnCoroutine = null;
        }

        _sequenceSudahDimulai = false;
        _teleportSudahDimulai = false;
        _slurry25SudahTriggered = false;
        AktifkanGlowMaskerDiBaju(false);
        HideArrow();
        HentikanAgitator();
        CacheColliders();
        SiapkanSafetyRuntime();
        ResetVisualState();
    }

    private void OnVoiceReportAccepted(string keyword)
    {
        if (GameLevelManager.Instance == null)
            return;

        if (GameLevelManager.Instance.CurrentLevel != GameLevelManager.GameLevel.Level3_OreSlurry)
            return;

        var phase = GameLevelManager.Instance.CurrentLevel3Phase;

        if (phase == GameLevelManager.Level3Phase.LaporanAwalDiterima)
        {
            // Setelah laporan HT awal diterima, langsung jalankan teleport ke field.
            CobaMulaiTeleportKeField();
            return;
        }

        // Setelah laporan HT akhir diterima (slurry sudah 50%), MULAI mesin pengaduk
        // dengan ramp-up pelan dari 0 → kencang (akselerasi 8 deg/s²).
        if (phase == GameLevelManager.Level3Phase.SiapLaporanAkhir ||
            phase == GameLevelManager.Level3Phase.Selesai)
        {
            MulaiAgitatorJikaPerlu();
        }
    }

    private void OnApdItemWorn(string namaApd)
    {
        // Saat APD masker dipakai DI FIELD, matikan glow di socket baju supaya tidak menyilaukan.
        if (_glowMaskerAktif &&
            (namaApd != null &&
             (namaApd.IndexOf("masker", System.StringComparison.OrdinalIgnoreCase) >= 0 ||
              namaApd.IndexOf("respirator", System.StringComparison.OrdinalIgnoreCase) >= 0)))
        {
            AktifkanGlowMaskerDiBaju(false);
        }

        // Saat APD masker/kacamata dipakai DI FIELD, lanjutkan sequence ore.
        CobaLanjutkanSequenceSetelahApdField();
    }

    /// <summary>
    /// Memulai teleport ke area field tanpa cek APD lapangan.
    /// Cek APD pindah ke fase setelah teleport (CobaLanjutkanSequenceSetelahApdField).
    /// </summary>
    private void CobaMulaiTeleportKeField()
    {
        if (GameLevelManager.Instance == null)
            return;

        if (GameLevelManager.Instance.CurrentLevel != GameLevelManager.GameLevel.Level3_OreSlurry)
            return;

        if (GameLevelManager.Instance.CurrentLevel3Phase != GameLevelManager.Level3Phase.LaporanAwalDiterima)
            return;

        if (_teleportSudahDimulai || _sequenceSudahDimulai)
            return;

        _teleportSudahDimulai = true;
        _sequenceCoroutine = StartCoroutine(MainkanTeleportKeFieldLalu_TungguApd());
    }

    /// <summary>
    /// Setelah teleport selesai, tunggu player memakai masker + kacamata.
    /// Saat lengkap, lanjutkan sequence ore + slurry.
    /// </summary>
    private void CobaLanjutkanSequenceSetelahApdField()
    {
        if (GameLevelManager.Instance == null)
            return;

        if (GameLevelManager.Instance.CurrentLevel != GameLevelManager.GameLevel.Level3_OreSlurry)
            return;

        if (GameLevelManager.Instance.CurrentLevel3Phase != GameLevelManager.Level3Phase.ObservasiLapangan)
            return;

        if (_sequenceSudahDimulai)
            return;

        if (!ApdLapanganSiap())
            return;

        _sequenceSudahDimulai = true;
        if (_sequenceCoroutine != null)
            StopCoroutine(_sequenceCoroutine);
        _sequenceCoroutine = StartCoroutine(MainkanSequenceOreSlurry());
    }

    private bool ApdLapanganSiap()
    {
        if (!_wajibKacamataRespiratorSebelumKeLapangan)
            return true;

        if (PhaseManager.Instance == null)
        {
            Debug.LogWarning("[Level3OreSlurryController] PhaseManager tidak ditemukan, validasi APD lapangan dilewati.");
            return true;
        }

        if (PhaseManager.Instance.Level3FieldApdLengkap)
            return true;

        string kurang = PhaseManager.Instance.CaraApdLevel3FieldYangKurang();
        string pesan = $"Pakai APD lapangan dulu sebelum ke crusher/slurry: {kurang}.";
        Debug.LogWarning($"[Level3OreSlurryController] {pesan}");
        if (_hud != null)
            _hud.ShowNotifPublic(pesan);

        return false;
    }

    private void OnLevelTransitionRequested(GameLevelManager.GameLevel fromLevel, GameLevelManager.GameLevel toLevel, float duration)
    {
        if (fromLevel != GameLevelManager.GameLevel.Level3_OreSlurry || toLevel != GameLevelManager.GameLevel.Level4_SlurryPump)
            return;

        if (_returnCoroutine != null)
            StopCoroutine(_returnCoroutine);

        _returnCoroutine = StartCoroutine(TeleportKeDcsSaatTransisi(duration));
    }

    /// <summary>
    /// Step 1: Tunggu sebentar setelah laporan HT awal, fade screen, lalu teleport player ke area field.
    /// Setelah sampai field, fase pindah ke ObservasiLapangan, dan tinggal menunggu APD lapangan dipakai.
    /// </summary>
    private IEnumerator MainkanTeleportKeFieldLalu_TungguApd()
    {
        yield return new WaitForSeconds(_jedaSetelahLaporanAwal);

        if (_hud != null)
            _hud.PlayManualFade(_durasiFadeKeField);

        yield return new WaitForSeconds(HitungWaktuTeleport(_durasiFadeKeField));
        TeleportPlayer(_teleportTargetField);
        GameLevelManager.Instance?.NotifyLevel3FieldSequenceStarted();

        // Lepas masker dari socket baju saat masuk field, supaya player wajib pasang sendiri.
        TampilkanMaskerDiFieldUntukDipakai();

        if (_hud != null)
        {
            string kurang = PhaseManager.Instance != null
                ? PhaseManager.Instance.CaraApdLevel3FieldYangKurang()
                : "masker dan kacamata";
            _hud.ShowNotifPublic($"Pakai APD lapangan dulu sebelum lihat slurry tank: {kurang}.");
        }

        float sisaFade = Mathf.Max(0f, _durasiFadeKeField - HitungWaktuTeleport(_durasiFadeKeField));
        if (sisaFade > 0f)
            yield return new WaitForSeconds(sisaFade);

        // Setelah fase ObservasiLapangan aktif, panggilan dari OnApdItemWorn akan memicu sequence ore.
        // Coba sekali lagi (untuk kasus player sudah memakai masker sebelum sampai sini, walaupun jarang terjadi).
        CobaLanjutkanSequenceSetelahApdField();

        _sequenceCoroutine = null;
    }

    /// <summary>
    /// Step 2: Player sudah pakai APD lapangan. Jalankan animasi ore + slurry fill.
    /// </summary>
    /// <summary>
    /// Step 2: Player sudah pakai APD lapangan. Jalankan animasi ore + slurry fill.
    /// </summary>
    private IEnumerator MainkanSequenceOreSlurry()
    {
        yield return new WaitForSeconds(_jedaSebelumOreJalan);

        SetObservationObjects(true);
        if (!RefsOreLengkap())
        {
            Debug.LogWarning("[Level3OreSlurryController] Ore mover/start/end belum lengkap. Sequence Level 3 dihentikan agar quest tidak auto-centang.");
            _sequenceCoroutine = null;
            yield break;
        }

        if (_slurryFill == null)
        {
            Debug.LogWarning("[Level3OreSlurryController] SlurryFill belum di-assign. Sequence Level 3 dihentikan agar quest tidak auto-centang.");
            _sequenceCoroutine = null;
            yield break;
        }

        // Arrow tetap aktif menunjuk ke slurry tank selama ore bergerak.
        if (_slurryFill != null)
            ShowArrowKe(_slurryFill);

        yield return StartCoroutine(AnimasikanOreMasukKeTank());
        SelesaikanOreMasukTank();
        GameLevelManager.Instance?.NotifyLevel3OreReachedSlurry();

        if (_teleportKeTitikObservasi && _teleportTargetObservation != null)
            TeleportPlayer(_teleportTargetObservation);

        if (_jedaSetelahOreMasuk > 0f)
            yield return new WaitForSeconds(_jedaSetelahOreMasuk);

        // Setelah teleport ke observation, hide arrow karena player sudah dekat tank.
        HideArrow();

        yield return StartCoroutine(AnimasikanIsiSlurrySampaiBatas());

        if (!_slurry25SudahTriggered)
            Debug.LogWarning("[Level3OreSlurryController] Slurry belum mencapai batas 25%, quest belum akan dicentang.");

        _sequenceCoroutine = null;
    }

    private IEnumerator TeleportKeDcsSaatTransisi(float duration)
    {
        yield return new WaitForSeconds(HitungWaktuTeleport(duration));
        TeleportPlayer(_teleportTargetDcs);
        _returnCoroutine = null;
    }

    private IEnumerator AnimasikanOreMasukKeTank()
    {
        if (_oreMover != null && _oreStartPoint != null)
            _oreMover.position = _oreStartPoint.position;

        float elapsed = 0f;
        while (elapsed < _durasiGerakOre)
        {
            elapsed += Time.deltaTime;
            float oreT = _durasiGerakOre <= 0f ? 1f : Mathf.Clamp01(elapsed / _durasiGerakOre);
            _oreMover.position = HitungPosisiOre(oreT);
            yield return null;
        }

        _oreMover.position = _oreEndPoint.position;
    }

    private IEnumerator AnimasikanIsiSlurrySampaiBatas()
    {
        SiapkanSlurryFillUntukIsi();

        // Mulai FX audio + bubble particle
        EnsureSlurryFx();
        if (_slurryFx != null)
        {
            _slurryFx.MulaiFx();
            _slurryFx.UpdatePosisiPermukaan(HitungWorldPosPermukaanSlurry());
        }

        float elapsed = 0f;
        while (elapsed < _durasiIsiSlurry)
        {
            elapsed += Time.deltaTime;
            float slurryT = _durasiIsiSlurry <= 0f ? 1f : Mathf.Clamp01(elapsed / _durasiIsiSlurry);
            _slurryFill.localScale = Vector3.Lerp(GetSlurryScaleAwal(), GetSlurryScaleTarget25(), slurryT);
            _slurryFill.localPosition = Vector3.Lerp(_slurryLocalPosAwal, _slurryLocalPosTarget25, slurryT);

            if (_slurryFx != null)
                _slurryFx.UpdatePosisiPermukaan(HitungWorldPosPermukaanSlurry());

            if (_validasiSlurry25PakaiBatasFisik && !_slurry25SudahTriggered && SlurrySudahMencapaiBatas25())
            {
                _slurry25SudahTriggered = true;
                GameLevelManager.Instance?.NotifyLevel3SlurryReady(50f);
                if (_slurryFx != null) _slurryFx.HentikanFx();
                // NOTE: Agitator JANGAN dimulai di sini. Trigger di OnVoiceReportAccepted
                // (setelah laporan HT akhir diterima) supaya alur: slurry penuh → lapor HT → mesin baru hidup.
                yield break;
            }

            yield return null;
        }

        _slurryFill.localScale = GetSlurryScaleTarget25();
        _slurryFill.localPosition = _slurryLocalPosTarget25;

        if (_slurryFx != null)
            _slurryFx.HentikanFx();

        if (!_slurry25SudahTriggered && (!_validasiSlurry25PakaiBatasFisik || SlurrySudahMencapaiBatas25()))
        {
            _slurry25SudahTriggered = true;
            GameLevelManager.Instance?.NotifyLevel3SlurryReady(50f);
        }

        // NOTE: Agitator akan dimulai di OnVoiceReportAccepted setelah player lapor HT akhir.
    }

    private void TeleportPlayer(Transform target)
    {
        if (target == null)
        {
            Debug.LogWarning("[Level3OreSlurryController] Target teleport belum di-assign.");
            return;
        }

        if (_playerRigRoot == null && Camera.main != null)
            _playerRigRoot = Camera.main.transform.root;

        if (_playerRigRoot == null)
        {
            Debug.LogWarning("[Level3OreSlurryController] Player rig root tidak ditemukan.");
            return;
        }

        bool restoreController = _playerCharacterController != null && _playerCharacterController.enabled;
        if (restoreController)
            _playerCharacterController.enabled = false;

        Vector3 posisiAman = CariPosisiAmanDiLantai(target.position);
        _playerRigRoot.SetPositionAndRotation(posisiAman, target.rotation);

        if (restoreController)
            _playerCharacterController.enabled = true;
    }

    private void ResetVisualState()
    {
        if (_oreMover != null && _oreStartPoint != null)
            _oreMover.position = _oreStartPoint.position;

        if (_slurryFill != null)
        {
            if (_aktifkanSlurryFillSaatMulaiIsi)
                _slurryFill.gameObject.SetActive(true);

            _slurryFill.localScale = GetSlurryScaleAwal();
            _slurryFill.localPosition = _slurryLocalPosAwal;
            PaksaSlurryKelihatan();
        }

        SetObservationObjects(false);
    }

    private void CacheColliders()
    {
        _slurryFillCollider = _slurryFill != null ? _slurryFill.GetComponent<Collider>() : null;
    }

    private bool RefsOreLengkap()
    {
        return _oreMover != null && _oreStartPoint != null && _oreEndPoint != null;
    }

    private void SelesaikanOreMasukTank()
    {
        if (_oreMover == null || !_sembunyikanOreSetelahMasukTank)
            return;

        _oreMover.gameObject.SetActive(false);
    }

    private void SiapkanSlurryFillUntukIsi()
    {
        if (_slurryFill == null)
            return;

        if (_aktifkanSlurryFillSaatMulaiIsi)
            _slurryFill.gameObject.SetActive(true);

        _slurryFill.localScale = GetSlurryScaleAwal();
        _slurryFill.localPosition = _slurryLocalPosAwal;
        PaksaSlurryKelihatan();
    }

    private Vector3 GetSlurryScaleAwal()
    {
        Vector3 scale = _slurryLocalScaleAwal;
        if (_pertahankanDiameterSlurryDariScene)
        {
            scale.x = Mathf.Max(_slurryScaleSceneAwal.x, 0.01f);
            scale.z = Mathf.Max(_slurryScaleSceneAwal.z, 0.01f);
        }

        return scale;
    }

    private Vector3 GetSlurryScaleTarget25()
    {
        Vector3 scale = _slurryLocalScaleTarget25;
        if (_pertahankanDiameterSlurryDariScene)
        {
            scale.x = Mathf.Max(_slurryScaleSceneAwal.x, 0.01f);
            scale.z = Mathf.Max(_slurryScaleSceneAwal.z, 0.01f);
        }

        return scale;
    }

    private void PaksaSlurryKelihatan()
    {
        if (_slurryFill == null)
            return;

        Renderer[] renderers = _slurryFill.GetComponentsInChildren<Renderer>(true);
        for (int i = 0; i < renderers.Length; i++)
            renderers[i].enabled = true;
    }

    private Vector3 HitungPosisiOre(float t)
    {
        if (_oreStartPoint == null || _oreEndPoint == null)
            return _oreMover != null ? _oreMover.position : Vector3.zero;

        if (_oreMidPoint == null)
            return Vector3.Lerp(_oreStartPoint.position, _oreEndPoint.position, t);

        const float split = 0.72f;
        if (t <= split)
        {
            float localT = Mathf.InverseLerp(0f, split, t);
            return Vector3.Lerp(_oreStartPoint.position, _oreMidPoint.position, localT);
        }

        float dropT = Mathf.InverseLerp(split, 1f, t);
        return Vector3.Lerp(_oreMidPoint.position, _oreEndPoint.position, dropT);
    }

    private bool SlurrySudahMencapaiBatas25()
    {
        if (_slurryFill == null)
            return false;

        if (_slurryTrigger25 != null)
        {
            if (_slurryFillCollider == null)
            {
                Debug.LogWarning("[Level3OreSlurryController] SlurryTrigger25 sudah diisi, tapi SlurryFill belum punya collider. Fallback ke marker/posisi.");
            }
            else
            {
                return _slurryFillCollider.bounds.Intersects(_slurryTrigger25.bounds);
            }
        }

        if (_slurryBatas25 == null)
            return Vector3.Distance(_slurryFill.localPosition, _slurryLocalPosTarget25) <= 0.02f;

        return _slurryFill.position.y >= _slurryBatas25.position.y;
    }

    private Vector3 CariPosisiAmanDiLantai(Vector3 posisiTarget)
    {
        if (!_gunakanRaycastLantaiSaatTeleport)
            return posisiTarget;

        Vector3 asal = posisiTarget + Vector3.up * (_jarakRaycastLantai * 0.5f);
        if (Physics.Raycast(asal, Vector3.down, out RaycastHit hit, _jarakRaycastLantai, ~0, QueryTriggerInteraction.Ignore))
        {
            float tinggiController = _playerCharacterController != null ? Mathf.Max(0f, _playerCharacterController.height * 0.5f) : 0.9f;
            return new Vector3(posisiTarget.x, hit.point.y + tinggiController + _offsetAmanDiAtasLantai, posisiTarget.z);
        }

        return posisiTarget + Vector3.up * _offsetAmanDiAtasLantai;
    }

    private void SiapkanSafetyRuntime()
    {
        if (_buatPlatformObservasiOtomatis && _teleportTargetObservation != null)
            SiapkanPlatformObservasi();

        if (_buatSafetyFloorOtomatis)
            SiapkanSafetyFloor();
    }

    private void SiapkanPlatformObservasi()
    {
        if (_platformObservasiRuntime == null)
        {
            _platformObservasiRuntime = GameObject.CreatePrimitive(PrimitiveType.Cube);
            _platformObservasiRuntime.name = "Level3_ObservationPlatform_Auto";
            var renderer = _platformObservasiRuntime.GetComponent<MeshRenderer>();
            if (renderer != null)
                renderer.enabled = false;
        }

        Vector3 posisi = _teleportTargetObservation.position;
        _platformObservasiRuntime.transform.position = posisi + Vector3.down * 1.15f;
        _platformObservasiRuntime.transform.rotation = Quaternion.identity;
        _platformObservasiRuntime.transform.localScale = _ukuranPlatformObservasi;
    }

    private void SiapkanSafetyFloor()
    {
        if (_safetyFloorRuntime == null)
        {
            _safetyFloorRuntime = GameObject.CreatePrimitive(PrimitiveType.Cube);
            _safetyFloorRuntime.name = "Level3_SafetyFloor_Auto";
            var renderer = _safetyFloorRuntime.GetComponent<MeshRenderer>();
            if (renderer != null)
                renderer.enabled = false;
        }

        // Cover dua titik: spawn field DAN observation point (slurry tank).
        // Hitung center + extents agar safety floor cukup besar untuk mencakup keduanya plus margin.
        Vector3 fieldPos = _teleportTargetField != null ? _teleportTargetField.position : transform.position;
        Vector3 obsPos = _teleportTargetObservation != null ? _teleportTargetObservation.position : fieldPos;

        Vector3 center = (fieldPos + obsPos) * 0.5f;
        float marginXZ = 12f; // margin di kedua sisi
        float deltaX = Mathf.Abs(fieldPos.x - obsPos.x) + marginXZ * 2f;
        float deltaZ = Mathf.Abs(fieldPos.z - obsPos.z) + marginXZ * 2f;
        // Pakai max dengan _ukuranSafetyFloor agar tidak lebih kecil dari yang di-config user.
        float scaleX = Mathf.Max(_ukuranSafetyFloor.x, deltaX);
        float scaleZ = Mathf.Max(_ukuranSafetyFloor.z, deltaZ);

        // Y target: 3m di bawah titik field (titik teleport awal player) supaya player landing aman.
        float targetY = fieldPos.y - 3f;
        _safetyFloorRuntime.transform.position = new Vector3(center.x, targetY, center.z);
        _safetyFloorRuntime.transform.rotation = Quaternion.identity;
        _safetyFloorRuntime.transform.localScale = new Vector3(scaleX, _ukuranSafetyFloor.y, scaleZ);
    }

    private void SetObservationObjects(bool active)
    {
        if (_oreMover != null)
            _oreMover.gameObject.SetActive(active);

        if (_waterFx != null)
            _waterFx.SetActive(active);

        if (_aktifSaatObservasi == null)
            return;

        for (int i = 0; i < _aktifSaatObservasi.Length; i++)
        {
            if (_aktifSaatObservasi[i] != null)
                _aktifSaatObservasi[i].SetActive(active);
        }
    }

    private float HitungWaktuTeleport(float totalDuration)
    {
        float fadeIn = Mathf.Clamp(totalDuration * 0.35f, 0.8f, 1.6f);
        float hold = Mathf.Max(0.15f, totalDuration - fadeIn - fadeIn);
        return fadeIn + (hold * 0.5f);
    }


    /// <summary>
    /// Lepaskan masker dari socket baju dan tempatkan di dekat titik teleport field,
    /// supaya player wajib mengambil + memasangnya sendiri ke wajah.
    /// Aman dipanggil meskipun masker belum pernah dipindahkan ke socket baju.
    /// </summary>
    /// <summary>
    /// Mode chest-grab: masker tetap di socket baju, tapi:
    ///  - status APD respirator di-reset (player wajib pakai ulang setelah lepas dari dada)
    ///  - socket baju + masker di-highlight glow + tampilkan instruksi HUD
    /// Dipanggil persis setelah teleport sampai di field.
    /// </summary>
    /// <summary>
    /// Mode chest-grab: masker tetap di socket baju, tapi:
    ///  - status APD respirator di-reset (player wajib pakai ulang setelah lepas dari dada)
    ///  - socket baju + masker di-highlight glow + tampilkan instruksi HUD
    /// Dipanggil persis setelah teleport sampai di field.
    /// </summary>
    private void TampilkanMaskerDiFieldUntukDipakai()
    {
        if (PhaseManager.Instance == null)
            return;

        // Reset status APD lapangan agar player wajib pakai ulang masker dan kacamata di field.
        if (PhaseManager.Instance.isRespiratorWorn)
            PhaseManager.Instance.OnRespiratorRemoved();

        // Pastikan masker secara fisik ada di socket baju (kalau belum).
        PhaseManager.Instance.PastikanMaskerAdaDiSocketBaju();

        AktifkanGlowMaskerDiBaju(true);

        if (_hud != null && !string.IsNullOrWhiteSpace(_pesanInstruksiMasker))
            _hud.ShowNotifPublic(_pesanInstruksiMasker);

        // Tampilkan arrow ke slurry tank supaya player tahu ke mana harus lihat setelah pakai APD.
        Transform arrowTarget = _teleportTargetObservation != null ? _teleportTargetObservation : _slurryFill;
        if (arrowTarget != null)
            ShowArrowKe(arrowTarget);
    }

    private void AktifkanGlowMaskerDiBaju(bool aktif)
    {
        Renderer rend = ResolveMaskerRenderer();
        if (rend == null)
        {
            _glowMaskerAktif = false;
            return;
        }

        if (_glowMpb == null)
            _glowMpb = new MaterialPropertyBlock();

        rend.GetPropertyBlock(_glowMpb);
        if (aktif)
        {
            // Aktifkan emisi via property block. Material harus mendukung _EmissionColor (Standard / URP-Lit OK).
            Color emisi = _warnaGlowMasker * Mathf.Max(0.01f, _intensitasGlowMasker);
            _glowMpb.SetColor("_EmissionColor", emisi);
        }
        else
        {
            _glowMpb.SetColor("_EmissionColor", Color.black);
        }
        rend.SetPropertyBlock(_glowMpb);

        // Pastikan keyword EMISSION aktif di material runtime untuk Standard shader.
        if (rend.sharedMaterial != null)
        {
            if (aktif) rend.sharedMaterial.EnableKeyword("_EMISSION");
        }

        _glowMaskerAktif = aktif;
    }

    private Renderer ResolveMaskerRenderer()
    {
        if (_maskerRendererUntukGlow != null)
            return _maskerRendererUntukGlow;

        if (PhaseManager.Instance != null)
        {
            Renderer rend = PhaseManager.Instance.GetRespiratorRenderer();
            if (rend != null)
            {
                _maskerRendererUntukGlow = rend;
                return rend;
            }
        }

        if (_socketMaskerBaju != null)
        {
            Renderer rend = _socketMaskerBaju.GetComponentInChildren<Renderer>(true);
            if (rend != null)
            {
                _maskerRendererUntukGlow = rend;
                return rend;
            }
        }

        return null;
    }


    /// <summary>
    /// Pastikan SlurryFXController ada di Slurry_Fill. Auto-add jika belum.
    /// </summary>
    private void EnsureSlurryFx()
    {
        if (_slurryFx != null)
            return;

        if (_slurryFill == null)
            return;

        _slurryFx = _slurryFill.GetComponent<SlurryFXController>();
        if (_slurryFx == null)
            _slurryFx = _slurryFill.gameObject.AddComponent<SlurryFXController>();
    }

    /// <summary>
    /// Hitung world position permukaan slurry (atas dari fill) berdasarkan transform saat ini.
    /// </summary>
    private Vector3 HitungWorldPosPermukaanSlurry()
    {
        if (_slurryFill == null)
            return transform.position;

        // Cylinder primitive: half-height = 1 unit di local mesh space.
        // World top = world position + (up * worldScale.y * 1.0f).
        Vector3 worldUp = _slurryFill.parent != null ? _slurryFill.parent.up : Vector3.up;
        return _slurryFill.position + worldUp * _slurryFill.lossyScale.y;
    }

    /// <summary>
    /// Pastikan DirectionArrowIndicator ada di scene. Auto-create child di XR Rig jika belum.
    /// </summary>
    private void EnsureArrowIndicator()
    {
        if (_arrowIndicator != null)
            return;

        _arrowIndicator = UnityEngine.Object.FindFirstObjectByType<DirectionArrowIndicator>();
        if (_arrowIndicator != null)
            return;

        if (_playerRigRoot == null)
            return;

        var go = new GameObject("DirectionArrow_Auto");
        go.transform.SetParent(_playerRigRoot, false);
        _arrowIndicator = go.AddComponent<DirectionArrowIndicator>();
    }

    /// <summary>
    /// Tampilkan arrow indicator menuju target tertentu (slurry tank, dst).
    /// </summary>
    private void ShowArrowKe(Transform target)
    {
        if (!_gunakanArrowIndicator || target == null)
            return;

        EnsureArrowIndicator();
        if (_arrowIndicator != null)
            _arrowIndicator.Show(target);
    }

    private void HideArrow()
    {
        if (_arrowIndicator != null)
            _arrowIndicator.Hide();
    }


    /// <summary>
    /// Cari + mulai pengaduk slurry. Auto-find di scene jika referensi belum di-assign.
    /// </summary>
    private void MulaiAgitatorJikaPerlu()
    {
        if (!_aktifkanAgitatorSaatSiapLapor)
            return;

        if (_slurryAgitator == null)
            _slurryAgitator = UnityEngine.Object.FindFirstObjectByType<SlurryAgitator>();

        if (_slurryAgitator == null)
        {
            Debug.LogWarning("[Level3OreSlurryController] SlurryAgitator tidak ditemukan di scene. Pengaduk tidak akan berputar.");
            return;
        }

        _slurryAgitator.Mulai();

        if (_hud != null)
            _hud.ShowNotifPublic("Mesin pengaduk aktif. Lihat ke slurry tank dan kirim laporan akhir.");
    }

    /// <summary>
    /// Hentikan pengaduk (dipanggil saat Level 3 reset / level lain mulai).
    /// </summary>
    private void HentikanAgitator()
    {
        if (_slurryAgitator != null)
            _slurryAgitator.Hentikan();
    }


    /// <summary>
    /// Saat slurry mencapai 50% (SiapLaporanAkhir): tampilkan arrow ke agitator + notif HUD.
    /// </summary>
    private void OnLevel3PhaseChanged(GameLevelManager.Level3Phase phase)
    {
        if (phase != GameLevelManager.Level3Phase.SiapLaporanAkhir)
            return;

        // Arrow nunjuk ke agitator supaya pemain wajib melihat mesin pengaduk.
        if (_slurryAgitator != null)
            ShowArrowKe(_slurryAgitator.transform);
        else if (_slurryFill != null)
            ShowArrowKe(_slurryFill);

        if (_hud != null)
            _hud.ShowNotifPublic("Slurry mencapai 50%. Lihat mesin pengaduk lalu kirim laporan HT akhir.");
    }
}
