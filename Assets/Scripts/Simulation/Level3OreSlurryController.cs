using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Mengatur sub-sequence Level 3:
/// laporan HT awal, fade ke area crusher, observasi ore + air, slurry 75%, lalu siap laporan akhir.
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
    [Tooltip("Aktifkan agitator saat slurry mencapai target (siap laporan akhir).")]
    [SerializeField] private bool _aktifkanAgitatorSaatSiapLapor = true;
    [Tooltip("Group FX aliran air dari pipa kiri (mesh stream + particle splash). Diaktifkan saat sequence isi air dan dimatikan setelah air mencapai target.")]
    [SerializeField] private GameObject _waterFlowFx;
    [Tooltip("Animasi belt + ore memakai batu coklat asli di scene, bukan spawn batu runtime.")]
    [SerializeField] private bool _pakaiOreAsliDariBelt = true;
    [Tooltip("Object belt ore utama. Auto-find: L2_V2_Wide_Inclined_Rubber_Ore_Belt.")]
    [SerializeField] private Transform _oreBeltVisual;
    [Tooltip("Kecepatan loop batu di belt runtime (cycle/detik).")]
    [SerializeField] private float _kecepatanOreBeltRuntime = 0.18f;
    [Tooltip("Jumlah batu runtime yang bergerak di atas belt lalu jatuh ke slurry tank.")]
    [SerializeField] private int _jumlahOreBatuRuntime = 34;
    [Tooltip("Jumlah cleat/garis belt runtime yang bergerak dari bawah ke atas.")]
    [SerializeField] private int _jumlahBeltCleatRuntime = 18;
    [Tooltip("Sebaran kiri-kanan batu di belt.")]
    [SerializeField] private float _lebarSebarOreRuntime = 1.15f;
    [Tooltip("Tinggi arc jatuh batu dari ujung belt ke slurry tank.")]
    [SerializeField] private float _tinggiJatuhOreRuntime = 1.35f;
    [Tooltip("Offset ketinggian seluruh jalur ore di belt (negatif = lebih rendah/mepet ke crusher).")]
    [SerializeField] private float _oreBeltHeightOffset = -0.55f;
    [Tooltip("Geser titik mid belt mendekat ke crusher (mepet). Positif = lebih dekat ke start.")]
    [SerializeField] private float _oreBeltStartSnug = 1.2f;
    [Tooltip("Sembunyikan conveyor/ore/escalator bekas di sekitar slurry tank saat runtime.")]
    [SerializeField] private bool _hapusOreConveyorBekasRuntime = true;
    [Tooltip("Kecepatan target visual blade agitator prefab Level3 (deg/s).")]
    [SerializeField] private float _kecepatanAgitatorVisibleDeg = 120f;
    [Tooltip("Akselerasi blade agitator prefab Level3 dari pelan ke kencang (deg/s2).")]
    [SerializeField] private float _akselerasiAgitatorVisible = 8f;
    [Tooltip("Reference ke arrow indicator yang menunjuk ke slurry tank. Auto-create di runtime jika kosong.")]
    [SerializeField] private DirectionArrowIndicator _arrowIndicator;
    [Tooltip("Aktifkan arrow indicator saat sampai field menunggu APD + saat menuju observation point.")]
    [SerializeField] private bool _gunakanArrowIndicator = true;

    [Header("=== Panel Pilihan Transisi ===")]
    [Tooltip("Panel pilihan 'Lanjut' / 'Lihat Proses'. Auto-create di runtime jika kosong.")]
    [SerializeField] private LevelTransitionChoicePanel _choicePanel;
    [Tooltip("Durasi fade out saat player pilih 'Lanjut' (detik).")]
    [SerializeField] private float _durasiFadeLanjut = 3.5f;    private PlayerHUD _hud;
    private Coroutine _sequenceCoroutine;
    private Coroutine _returnCoroutine;
    private bool _sequenceSudahDimulai;
    private bool _teleportSudahDimulai;
    private bool _slurry25SudahTriggered;
    private bool _agitatorSudahStartSetelahLaporan;
    private Collider _slurryFillCollider;
    private Vector3 _slurryScaleSceneAwal = Vector3.one;
    private GameObject _platformObservasiRuntime;
    private GameObject _safetyFloorRuntime;
    private MaterialPropertyBlock _glowMpb;
    private bool _glowMaskerAktif;
    private readonly List<Transform> _agitatorVisibleParts = new List<Transform>();
    private readonly List<Renderer> _slurrySurfaceRenderers = new List<Renderer>();
    private Transform _waterPipeOutlet;
    private GameObject _runtimeWaterStream;
    private Transform _runtimeWaterStreamTransform;
    private GameObject _runtimeWaterSplash;
    private Transform _runtimeWaterSplashTransform;
    private ParticleSystem _runtimeWaterDroplets;
    private GameObject _runtimeSwirlRoot;
    private Transform _runtimeSwirlRootTransform;
    private GameObject _runtimeTankLiquidVolume;
    private Transform _runtimeTankLiquidVolumeTransform;
    private Material _runtimeTankLiquidMaterial;
    private GameObject _runtimeOreConveyorRoot;
    private readonly List<RuntimeOrePiece> _runtimeOrePieces = new List<RuntimeOrePiece>();
    private readonly List<RuntimeBeltCleat> _runtimeBeltCleats = new List<RuntimeBeltCleat>();
    private readonly List<RuntimeOrePiece> _sceneOrePieces = new List<RuntimeOrePiece>();
    private readonly List<Renderer> _oreBeltRenderers = new List<Renderer>();
    private Transform _runtimeOreStartPoint;
    private Transform _runtimeOreMidPoint;
    private Transform _runtimeOreEndPoint;
    private Material _runtimeWaterMaterial;
    private Material _runtimeOreMaterial;
    private Material _runtimeBeltCleatMaterial;
    private Material _oreBeltMaterial;
    private Material _runtimeSwirlMaterial;
    private bool _runtimeWaterFlowAktif;
    private bool _runtimeOreConveyorAktif;
    private bool _runtimeAgitatorAktif;
    private float _runtimeAgitatorSpeed;
    private float _runtimeWaterOffset;
    private float _runtimeOreBeltOffset;
    private float _runtimeOreConveyorTime;
    private float _runtimeSlurryFillProgress;

    private sealed class RuntimeOrePiece
    {
        public Transform Transform;
        public float Offset;
        public float SpeedMul;
        public float Lateral;
        public Vector3 SpinAxis;
        public Vector3 BaseScale;
        public Vector3 OriginalPosition;
        public Quaternion OriginalRotation;
        public Vector3 OriginalLocalScale;
        public bool IsSceneOre;
        public bool FellIntoTank;
    }

    private sealed class RuntimeBeltCleat
    {
        public Transform Transform;
        public float Offset;
        public float Lateral;
    }

    private void Awake()
    {
        _hud = UnityEngine.Object.FindFirstObjectByType<PlayerHUD>();
        if (_playerRigRoot == null && Camera.main != null)
            _playerRigRoot = Camera.main.transform.root;

        if (_playerCharacterController == null && _playerRigRoot != null)
            _playerCharacterController = _playerRigRoot.GetComponent<CharacterController>();

        if (_slurryFill != null)
            _slurryScaleSceneAwal = _slurryFill.localScale;

        EnsureLevel3RuntimeVisuals();
        SembunyikanOreConveyorBekasRuntime();
        CacheColliders();
        SiapkanSafetyRuntime();
        ResetVisualState();
    }

    private void OnEnable()
    {
        GameLevelManager.OnLevelStarted += OnLevelStarted;
        GameLevelManager.OnVoiceReportAccepted += OnVoiceReportAccepted;
        GameLevelManager.OnLevel3LaporanAkhirDiterima += OnLevel3LaporanAkhirDiterima;
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
        GameLevelManager.OnLevel3LaporanAkhirDiterima -= OnLevel3LaporanAkhirDiterima;
        PhaseManager.OnApdItemWorn -= OnApdItemWorn;
    }

    private void Update()
    {
        float dt = Time.deltaTime;
        UpdateRuntimeOreConveyor(dt);
        UpdateRuntimeWaterFlow(dt);
        UpdateRuntimeAgitator(dt);        UpdateSlurryStir(dt);

        UpdateRuntimeTankLiquidVolume();
        UpdateRuntimeSlurrySurface(dt);
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

        // Sembunyikan panel pilihan "Lanjut/Lihat Proses" saat level berubah (fix canvas nyangkut).
        if (_choicePanel != null)
            _choicePanel.Hide();

        _sequenceSudahDimulai = false;
        _teleportSudahDimulai = false;
        _slurry25SudahTriggered = false;
        AktifkanGlowMaskerDiBaju(false);
        HideArrow();
        HentikanAgitator();
        _agitatorSudahStartSetelahLaporan = false;
        EnsureLevel3RuntimeVisuals();
        SembunyikanOreConveyorBekasRuntime();
        SetConveyorOreFxAktif(false);
        SetWaterFlowFxAktif(false);
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

        // Fallback: setelah laporan HT akhir diterima, pengaduk mulai ramp-up.
        if (phase == GameLevelManager.Level3Phase.SiapLaporanAkhir ||
            phase == GameLevelManager.Level3Phase.Selesai)
        {
            MulaiAgitatorSetelahLaporanAkhir();
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

        // Nyala mesin + sirine + eskalator (dorongan mundur lalu naik) DULU sebelum ore keluar dari black box.
        yield return StartCoroutine(StartupMesinDanEskalator());

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
            Debug.LogWarning("[Level3OreSlurryController] Slurry belum mencapai batas 75%, quest belum akan dicentang.");

        _sequenceCoroutine = null;
    }

    // Sekuens nyala mesin Ore Crusher: mesin nyala + sirine, eskalator dorongan MUNDUR sesaat lalu NAIK.
    private IEnumerator StartupMesinDanEskalator()
    {
        if (_oreBeltMaterial == null && _oreBeltVisual != null)
        {
            Renderer br = _oreBeltVisual.GetComponent<Renderer>();
            if (br != null) _oreBeltMaterial = Application.isPlaying ? br.material : br.sharedMaterial;
        }

        _crusherFxAktif = true;
        StartCoroutine(CrusherCrushFxLoop());

        System.Collections.Generic.List<Transform> spinners = new System.Collections.Generic.List<Transform>();
        foreach (Transform t in CariSemuaTransformTermasukInactive())
        {
            if (t == null) continue;
            string n = t.name.ToLowerInvariant();
            if (n.Contains("pulley")) spinners.Add(t);
        }

        GameObject audGo = new GameObject("Level3_Mesin_Siren_Audio");
        audGo.transform.SetParent(transform, false);
        AudioSource aud = audGo.AddComponent<AudioSource>();
        aud.spatialBlend = 0f; aud.volume = 0.85f; aud.loop = true;
        aud.clip = GenSirenClip(2.4f); aud.Play();

        // Phase 1: mesin nyala + dorongan MUNDUR sesaat (belt offset positif = mundur).
        float jerk = 0.5f, e = 0f;
        while (e < jerk)
        {
            e += Time.deltaTime;
            float k = e / jerk;
            SetBeltOffsetRuntime(0.07f * Mathf.Sin(k * Mathf.PI));
            for (int i = 0; i < spinners.Count; i++) if (spinners[i] != null) spinners[i].Rotate(Vector3.forward, -55f * Time.deltaTime, Space.Self);
            yield return null;
        }

        // Phase 2: eskalator NAIK perlahan, ramp-up smooth (belt offset negatif = maju).
        float ramp = 2.6f; e = 0f;
        while (e < ramp)
        {
            e += Time.deltaTime;
            float spd = Mathf.SmoothStep(0f, 1f, e / ramp);
            _runtimeOreBeltOffset = Mathf.Repeat(_runtimeOreBeltOffset + Time.deltaTime * spd * 0.9f, 1f);
            SetBeltOffsetRuntime(-_runtimeOreBeltOffset);
            for (int i = 0; i < spinners.Count; i++) if (spinners[i] != null) spinners[i].Rotate(Vector3.forward, 340f * spd * Time.deltaTime, Space.Self);
            yield return null;
        }

        aud.loop = false;
        if (aud != null) aud.Stop();
        Destroy(audGo, 0.2f);
    }

    // ===== Crusher jaw-crush + dust FX saat mesin nyala =====
    private bool _crusherFxAktif;
    private GameObject _crusherDustGo;

    private void EnsureCrusherDust(Vector3 worldPos)
    {
        if (_crusherDustGo != null) return;
        _crusherDustGo = new GameObject("Level3_Crusher_Dust_FX");
        _crusherDustGo.transform.SetParent(transform, false);
        _crusherDustGo.transform.position = worldPos;
        var ps = _crusherDustGo.AddComponent<ParticleSystem>();
        var main = ps.main;
        main.startLifetime = 1.4f; main.startSpeed = 0.6f; main.startSize = 0.55f;
        main.gravityModifier = -0.04f; main.maxParticles = 120;
        main.startColor = new Color(0.6f, 0.53f, 0.44f, 0.45f);
        var em = ps.emission; em.rateOverTime = 24f;
        var sh = ps.shape; sh.shapeType = ParticleSystemShapeType.Box; sh.scale = new Vector3(1.4f, 0.4f, 1.8f);
        var rend = ps.GetComponent<ParticleSystemRenderer>();
        Shader dsh = Shader.Find("Sprites/Default");
        if (rend != null && dsh != null) { var dm = new Material(dsh); dm.color = new Color(0.62f, 0.55f, 0.46f, 0.4f); rend.material = dm; }
        ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
    }

    private IEnumerator CrusherCrushFxLoop()
    {
        Transform jawL = null, jawR = null;
        System.Collections.Generic.List<Transform> fly = new System.Collections.Generic.List<Transform>();
        foreach (Transform t in CariSemuaTransformTermasukInactive())
        {
            if (t == null) continue;
            string n = t.name.ToLowerInvariant();
            if (n.Contains("left_smooth_jaw_liner")) jawL = t;
            else if (n.Contains("right_smooth_jaw_liner")) jawR = t;
            else if (n.Contains("flywheel")) fly.Add(t);
        }
        System.Collections.Generic.List<Vector3> flyCtr = new System.Collections.Generic.List<Vector3>();
        for (int i = 0; i < fly.Count; i++) { Renderer rr = fly[i] != null ? fly[i].GetComponent<Renderer>() : null; flyCtr.Add(rr != null ? rr.bounds.center : (fly[i] != null ? fly[i].position : Vector3.zero)); }
        Vector3 baseL = jawL != null ? jawL.position : Vector3.zero;
        Vector3 baseR = jawR != null ? jawR.position : Vector3.zero;
        EnsureCrusherDust(new Vector3(141.5f, 5.6f, 56.2f));
        ParticleSystem dust = _crusherDustGo != null ? _crusherDustGo.GetComponent<ParticleSystem>() : null;
        if (dust != null) dust.Play();
        float ph = 0f, dustAcc = 0f;
        while (_crusherFxAktif)
        {
            ph += Time.deltaTime * 9f; // ~1.4 Hz crush
            float squeeze = Mathf.Abs(Mathf.Sin(ph)) * 0.16f;
            if (jawL != null) jawL.position = baseL + new Vector3(0f, 0f, squeeze);
            if (jawR != null) jawR.position = baseR + new Vector3(0f, 0f, -squeeze);
            for (int i = 0; i < fly.Count; i++) if (fly[i] != null) fly[i].RotateAround(flyCtr[i], Vector3.forward, 430f * Time.deltaTime);
            dustAcc += Time.deltaTime; if (dust != null && dustAcc >= 0.07f) { dust.Emit(2); dustAcc = 0f; }
            yield return null;
        }
        if (jawL != null) jawL.position = baseL;
        if (jawR != null) jawR.position = baseR;
        if (dust != null) dust.Stop(true, ParticleSystemStopBehavior.StopEmitting);
    }

    private void SetBeltOffsetRuntime(float v)
    {
        if (_oreBeltMaterial == null) return;
        Vector2 off = new Vector2(0f, v);
        if (_oreBeltMaterial.HasProperty("_MainTex")) _oreBeltMaterial.SetTextureOffset("_MainTex", off);
        if (_oreBeltMaterial.HasProperty("_BaseMap")) _oreBeltMaterial.SetTextureOffset("_BaseMap", off);
    }

    private AudioClip GenSirenClip(float dur)
    {
        int sr = 44100;
        int n = Mathf.CeilToInt(dur * sr);
        float[] data = new float[n];
        float wailPhase = 0f, h2 = 0f, h3 = 0f, motorPhase = 0f;
        var rnd = new System.Random(99);
        for (int i = 0; i < n; i++)
        {
            float t = (float)i / sr;
            float p = (float)i / n;
            // 1. Sirine wail elektromekanis pabrik: fundamental naik-turun 380..780 Hz
            float wail = 380f + 200f * (1f + Mathf.Sin(2f * Mathf.PI * 0.55f * t));
            wailPhase += 2f * Mathf.PI * wail / sr;
            h2 += 2f * Mathf.PI * (wail * 2f) / sr;
            h3 += 2f * Mathf.PI * (wail * 3f) / sr;
            float horn = Mathf.Sin(wailPhase) * 0.6f + Mathf.Sin(h2) * 0.25f + Mathf.Sin(h3) * 0.12f;
            // 2. Motor spin-up: rumble naik pitch 34->88 Hz (mesin menyala)
            float motorRamp = Mathf.Clamp01(p / 0.45f);
            float motorHz = Mathf.Lerp(34f, 88f, motorRamp);
            motorPhase += 2f * Mathf.PI * motorHz / sr;
            float motor = (Mathf.Sin(motorPhase) + 0.5f * Mathf.Sin(motorPhase * 2f)) * 0.35f * motorRamp;
            // 3. Tekstur noise mekanis + saturasi lembut biar gritty/realistis
            float noise = ((float)rnd.NextDouble() * 2f - 1f) * 0.06f;
            float s = horn * 0.7f + motor + noise;
            s = (float)System.Math.Tanh(s * 1.6);
            // 4. Envelope attack/release halus
            float env = Mathf.Min(Mathf.Clamp01(p / 0.04f), Mathf.Clamp01((1f - p) / 0.12f));
            data[i] = s * env * 0.85f;
        }
        AudioClip clip = AudioClip.Create("Level3_Mesin_Siren", n, 1, sr, false);
        clip.SetData(data, 0);
        return clip;
    }

    private IEnumerator TeleportKeDcsSaatTransisi(float duration)
    {
        yield return new WaitForSeconds(HitungWaktuTeleport(duration));

        // Gunakan SpawnPoint_Lvl4 / SpawnPoint_DCS sebagai target teleport akhir Level 3,
        // supaya posisi konsisten dengan spawn awal Level 4 (di depan DCS, bukan di atap).
        Transform target = _teleportTargetDcs;
        var spawnGo = GameObject.Find("SpawnPoint_Lvl4") ?? GameObject.Find("SpawnPoint_DCS");
        if (spawnGo != null)
            target = spawnGo.transform;

        if (target != null)
            TeleportPlayer(target);

        _returnCoroutine = null;
    }

    private IEnumerator AnimasikanOreMasukKeTank()
    {
        EnsureOrePathRuntime();
        SetConveyorOreFxAktif(true);

        if (_oreMover != null && _oreStartPoint != null)
            _oreMover.position = _oreStartPoint.position;

        float elapsed = 0f;
        while (elapsed < _durasiGerakOre)
        {
            elapsed += Time.deltaTime;
            float oreT = _durasiGerakOre <= 0f ? 1f : Mathf.Clamp01(elapsed / _durasiGerakOre);
            if (_oreMover != null)
                _oreMover.position = HitungPosisiOre(oreT);
            yield return null;
        }

        if (_oreMover != null && _oreEndPoint != null)
            _oreMover.position = _oreEndPoint.position;

        SetConveyorOreFxAktif(false);
    }

    private IEnumerator AnimasikanIsiSlurrySampaiBatas()
    {
        SiapkanSlurryFillUntukIsi();

        // Rotor DIAM dulu (impeller dibangun). Mengaduk BARU setelah laporan HT akhir + sirine.
        if (_agitatorVisibleParts.Count == 0) CacheVisibleAgitatorParts();
        EnsureSlurryImpeller();

        // Mulai FX audio + bubble particle
        EnsureSlurryFx();
        if (_slurryFx != null)
        {
            _slurryFx.MulaiFx();
            _slurryFx.UpdatePosisiPermukaan(HitungWorldPosPermukaanSlurry());
        }

        // Mulai FX aliran air jatuh dari pipa kiri (mesh stream + droplet particle).
        SetWaterFlowFxAktif(false); SetWaterPourAktif(true); // air mancur realistis dari WaterInlet_Flange + cairan naik; (cyan lama mati)
        if (_runtimeSwirlRoot == null) BuatRuntimeSwirlSurface();
        if (_runtimeSwirlRoot != null) _runtimeSwirlRoot.SetActive(true); // ore mulai muncul bertahap seiring cairan naik - pakai cairan naik realistis (nikel/slurry)

        float elapsed = 0f;
        while (elapsed < _durasiIsiSlurry)
        {
            elapsed += Time.deltaTime;
            float slurryT = _durasiIsiSlurry <= 0f ? 1f : Mathf.Clamp01(elapsed / _durasiIsiSlurry);
            _runtimeSlurryFillProgress = slurryT;
            _slurryFill.localScale = Vector3.Lerp(GetSlurryScaleAwal(), GetSlurryScaleTarget25(), slurryT);
            _slurryFill.localPosition = Vector3.Lerp(_slurryLocalPosAwal, _slurryLocalPosTarget25, slurryT);
            UpdateRuntimeTankLiquidVolume();
            UpdateRuntimeSlurrySurface(0f);

            if (_slurryFx != null)
                _slurryFx.UpdatePosisiPermukaan(HitungWorldPosPermukaanSlurry());

            if (_validasiSlurry25PakaiBatasFisik && !_slurry25SudahTriggered && SlurrySudahMencapaiBatas25())
            {
                _slurry25SudahTriggered = true;
                GameLevelManager.Instance?.NotifyLevel3SlurryReady(75f);
                if (_slurryFx != null) _slurryFx.HentikanFx();
                yield break;
            }

            yield return null;
        }

        _slurryFill.localScale = GetSlurryScaleTarget25();
        _slurryFill.localPosition = _slurryLocalPosTarget25;
        _runtimeSlurryFillProgress = 1f;
        UpdateRuntimeTankLiquidVolume();
        UpdateRuntimeSlurrySurface(0f);

        if (_slurryFx != null)
            _slurryFx.HentikanFx();

        if (!_slurry25SudahTriggered && (!_validasiSlurry25PakaiBatasFisik || SlurrySudahMencapaiBatas25()))
        {
            _slurry25SudahTriggered = true;
            GameLevelManager.Instance?.NotifyLevel3SlurryReady(75f);
        }
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
            _runtimeSlurryFillProgress = 0f;
            if (_aktifkanSlurryFillSaatMulaiIsi)
                _slurryFill.gameObject.SetActive(true);

            _slurryFill.localScale = GetSlurryScaleAwal();
            _slurryFill.localPosition = _slurryLocalPosAwal;
            PaksaSlurryKelihatan();
            SetRuntimeTankLiquidVisible(false);
            UpdateRuntimeSlurrySurface(0f);
        }

        SetConveyorOreFxAktif(false);
        SetWaterFlowFxAktif(false);
        SetObservationObjects(false);
    }

    private void CacheColliders()
    {
        _slurryFillCollider = _slurryFill != null ? _slurryFill.GetComponent<Collider>() : null;
    }

    private bool RefsOreLengkap()
    {
        EnsureOrePathRuntime();
        return _oreStartPoint != null && _oreEndPoint != null;
    }

    private void SelesaikanOreMasukTank()
    {
        if (_oreMover == null || !_sembunyikanOreSetelahMasukTank)
            return;

        _oreMover.gameObject.SetActive(false);
        SetConveyorOreFxAktif(false);
    }

    private void SiapkanSlurryFillUntukIsi()
    {
        if (_slurryFill == null)
            return;

        if (_aktifkanSlurryFillSaatMulaiIsi)
            _slurryFill.gameObject.SetActive(true);

        _runtimeSlurryFillProgress = 0f;
        _slurryFill.localScale = GetSlurryScaleAwal();
        _slurryFill.localPosition = _slurryLocalPosAwal;
        PaksaSlurryKelihatan();
        SetRuntimeTankLiquidVisible(true);
        UpdateRuntimeTankLiquidVolume();
        UpdateRuntimeSlurrySurface(0f);
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
        {
            if (renderers[i] != null)
                renderers[i].enabled = true;
        }

        Renderer selfRenderer = _slurryFill.GetComponent<Renderer>();
        if (selfRenderer != null)
            selfRenderer.enabled = true;
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
            Vector3 p = Vector3.Lerp(_oreStartPoint.position, _oreMidPoint.position, localT);
            p.y += _oreBeltHeightOffset; // turunkan jalur belt biar mepet ke crusher
            return p;
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

        if (_runtimeTankLiquidVolumeTransform != null && _runtimeTankLiquidVolume != null && _runtimeTankLiquidVolume.activeSelf)
            return _runtimeTankLiquidVolumeTransform.position + Vector3.up * _runtimeTankLiquidVolumeTransform.localScale.y;

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
    private void MulaiAgitatorSetelahLaporanAkhir()
    {
        if (_agitatorSudahStartSetelahLaporan)
            return;

        _agitatorSudahStartSetelahLaporan = true;
        MainkanSirineMesinSlurry();
        SetWaterPourAktif(false);
        MulaiAgitatorJikaPerlu();
    }

    // Sirine penanda mesin slurry tank (rotor pengaduk) mulai bergerak setelah laporan HT.
    private void MainkanSirineMesinSlurry()
    {
        var go = new GameObject("Level3_SlurryStart_Siren");
        go.transform.SetParent(transform, false);
        var aud = go.AddComponent<AudioSource>();
        aud.spatialBlend = 0f; aud.volume = 0.9f; aud.loop = false;
        aud.clip = GenSirenClip(2.6f); aud.Play();
        Destroy(go, 3.0f);
    }

    private void MulaiAgitatorJikaPerlu()
    {
        if (!_aktifkanAgitatorSaatSiapLapor)
            return;

        EnsureLevel3RuntimeVisuals();
        bool sudahAktif = _runtimeAgitatorAktif || (_slurryAgitator != null && _slurryAgitator.Aktif);
        _runtimeAgitatorAktif = true;
        if (_runtimeSwirlRoot != null)
            _runtimeSwirlRoot.SetActive(true);

        if (_slurryAgitator == null)
            _slurryAgitator = UnityEngine.Object.FindFirstObjectByType<SlurryAgitator>();

        if (_slurryAgitator != null)
        {
            if (!_slurryAgitator.gameObject.activeSelf)
                _slurryAgitator.gameObject.SetActive(true);

            _slurryAgitator.Mulai();
        }
        else if (_agitatorVisibleParts.Count == 0)
        {
            Debug.LogWarning("[Level3OreSlurryController] Visual agitator Level3 tidak ditemukan. Cek nama object agitator di prefab.");
        }

        if (!sudahAktif && _hud != null)
            _hud.ShowNotifPublic("Mesin pengaduk aktif. Lihat ke slurry tank dan kirim laporan akhir.");
    }

    /// <summary>
    /// Hentikan pengaduk (dipanggil saat Level 3 reset / level lain mulai).
    /// </summary>
    private GameObject _slurryImpellerGo;    private readonly System.Collections.Generic.List<Transform> _slurryStirParts = new System.Collections.Generic.List<Transform>();
    private Vector3 _slurryStirPivot;
    private float _slurryStirSpeed;
    private bool _tankBoundsAda;
    private Vector3 _tankCenter;
    private float _tankRadius;
    private float _tankBottomY;
    private float _tankRimY;

    private void EnsureSlurryImpeller()
    {
        if (_slurryStirParts.Count > 0) return;
        Transform shaft = CariTransformNamaContains("AgitatorVerticalShaft");
        Vector3 c = shaft != null ? shaft.position : (_slurryFill != null ? _slurryFill.position : transform.position);
        _slurryStirPivot = new Vector3(c.x, 0f, c.z);
        foreach (var t in CariSemuaTransformTermasukInactive())
        {
            if (t == null) continue;
            string n = t.name;
            if (n.IndexOf("StirrerColumn_Static", System.StringComparison.OrdinalIgnoreCase) >= 0
                || n.IndexOf("StirrerHub_Static", System.StringComparison.OrdinalIgnoreCase) >= 0
                || n.IndexOf("StirrerBlade_Static", System.StringComparison.OrdinalIgnoreCase) >= 0)
            {
                if (!t.gameObject.activeSelf) t.gameObject.SetActive(true);
                _slurryStirParts.Add(t);
            }
        }
    }

    // Putar rakitan pengaduk statik (kolom+hub+bilah, geometri FBX baked di origin) mengelilingi poros tengah via RotateAround.
    private void UpdateSlurryStir(float dt)
    {
        float target = _runtimeAgitatorAktif ? Mathf.Abs(_kecepatanAgitatorVisibleDeg) : 0f;
        _slurryStirSpeed = Mathf.MoveTowards(_slurryStirSpeed, target, 60f * dt);
        if (_slurryStirSpeed <= 0.001f || _slurryStirParts.Count == 0) return;
        float angle = _slurryStirSpeed * dt * (_kecepatanAgitatorVisibleDeg < 0f ? -1f : 1f);
        for (int i = _slurryStirParts.Count - 1; i >= 0; i--)
        {
            Transform t = _slurryStirParts[i];
            if (t == null) { _slurryStirParts.RemoveAt(i); continue; }
            t.RotateAround(_slurryStirPivot, Vector3.up, angle);
        }
    }

    // Cache bounds tangki open-top yang sudah di-rebuild supaya cairan terisi tepat di tengah & seukuran tangki.
    private void EnsureTankBounds()
    {
        if (_tankBoundsAda) return;
        Transform shell = CariTransformNamaContains("OpenShell_SmoothSteel");
        Renderer r = shell != null ? shell.GetComponentInChildren<Renderer>(true) : null;
        if (r == null || r.bounds.size.sqrMagnitude < 0.01f) return;
        Bounds b = r.bounds;
        _tankCenter = b.center;
        _tankRadius = Mathf.Max(b.extents.x, b.extents.z) * 0.92f;
        _tankBottomY = b.min.y + 0.25f;
        _tankRimY = b.max.y;
        _tankBoundsAda = true;
    }

    private void HentikanAgitator()
    {
        _runtimeAgitatorAktif = false;
        _runtimeAgitatorSpeed = 0f;
        if (_runtimeSwirlRoot != null)
            _runtimeSwirlRoot.SetActive(false);

        if (_slurryAgitator != null)
        {
            _slurryAgitator.Hentikan();
            if (_agitatorSudahStartSetelahLaporan && _slurryAgitator.gameObject.activeSelf)
                _slurryAgitator.gameObject.SetActive(false);
        }
    }

    private void SetWaterFlowFxAktif(bool aktif)
    {
        EnsureLevel3RuntimeVisuals();
        _runtimeWaterFlowAktif = aktif;
        if (_runtimeWaterStream != null)
            _runtimeWaterStream.SetActive(aktif);
        if (_runtimeWaterSplash != null)
            _runtimeWaterSplash.SetActive(aktif);
        if (_runtimeWaterDroplets != null)
        {
            if (aktif) _runtimeWaterDroplets.Play(true);
            else _runtimeWaterDroplets.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
        }

        if (_waterFlowFx == null)
            return;

        if (_waterFlowFx.activeSelf != aktif)
            _waterFlowFx.SetActive(aktif);

        ParticleSystem[] particles = _waterFlowFx.GetComponentsInChildren<ParticleSystem>(true);
        for (int i = 0; i < particles.Length; i++)
        {
            ParticleSystem ps = particles[i];
            if (ps == null)
                continue;

            if (aktif) ps.Play(true);
            else ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
        }

        Renderer[] renderers = _waterFlowFx.GetComponentsInChildren<Renderer>(true);
        for (int i = 0; i < renderers.Length; i++)
        {
            if (renderers[i] != null)
                renderers[i].enabled = aktif;
        }
    }

    private void EnsureLevel3RuntimeVisuals()
    {
        if (_oreBeltVisual == null)
            _oreBeltVisual = CariTransformNamaContains("L2_V2_Wide_Inclined_Rubber_Ore_Belt") ??
                             CariTransformNamaContains("Wide_Inclined_Rubber_Ore_Belt") ??
                             CariTransformNamaContains("Rubber_Ore_Belt");

        if (_oreBeltVisual != null && _oreBeltRenderers.Count == 0)
            _oreBeltRenderers.AddRange(_oreBeltVisual.GetComponentsInChildren<Renderer>(true));

        if (_oreBeltMaterial == null && _oreBeltVisual != null)
        {
            Renderer beltRenderer = _oreBeltVisual.GetComponent<Renderer>();
            if (beltRenderer == null)
                beltRenderer = _oreBeltVisual.GetComponentInChildren<Renderer>(true);
            if (beltRenderer != null)
                _oreBeltMaterial = Application.isPlaying ? beltRenderer.material : beltRenderer.sharedMaterial;
        }

        if (_waterPipeOutlet == null)
            _waterPipeOutlet = CariTransformNamaContains("WaterPipe_TankOutlet_To_SpawnWater") ??
                               CariTransformNamaContains("SpawnWater") ??
                               CariTransformNamaContains("Pipe_ToTank") ??
                               CariTransformNamaContains("Steel_Discharge_Chute_Into_Inlet");

        if (_waterFlowFx == null)
        {
            GameObject waterAsset = GameObject.Find("Level3_WaterFountain_3D_Runtime") ??
                                    GameObject.Find("Level3_WaterFountain_3D") ??
                                    GameObject.Find("Level3_WaterFountain_3D(Clone)");
            if (waterAsset != null)
                _waterFlowFx = waterAsset;
        }

        CacheSceneOreOnBelt();

        if (_runtimeWaterStream == null)
            BuatRuntimeWaterStream();

        if (_runtimeTankLiquidVolume == null)
            BuatRuntimeTankLiquidVolume();

        if (_runtimeSwirlRoot == null)
            BuatRuntimeSwirlSurface();

        if (_agitatorVisibleParts.Count == 0)
            CacheVisibleAgitatorParts();

        CacheSlurrySurfaceRenderers();
    }

    private void EnsureOrePathRuntime()
    {
        if (_oreEndPoint == null || OreEndPointTerlaluJauhDariSlurry(_oreEndPoint))
        {
            _runtimeOreEndPoint = BuatRuntimePointJikaPerlu(_runtimeOreEndPoint, "Level3_Runtime_Ore_EndPoint", HitungFallbackOreEndPoint());
            _oreEndPoint = _runtimeOreEndPoint;
        }

        if (_oreMidPoint == null)
            _oreMidPoint = CariTransformNamaContains("Ore_Feed_Recessed_Inlet_Box") ??
                           CariTransformNamaContains("Steel_Discharge_Chute_Into_Inlet") ??
                           CariTransformNamaContains("Dark_Recessed_Ore_Inlet") ??
                           CariTransformNamaContains("Ore_Inlet_Rim_Back_Bar") ??
                           _oreEndPoint;

        // Inlet FBX baked (transform.position ~0, geometri di world lewat renderer) -> pakai pusat bounds renderer agar path ore tidak lewat origin.
        if (_oreMidPoint != null && _oreMidPoint != _oreEndPoint && _oreMidPoint.position.sqrMagnitude < 0.0025f)
        {
            Renderer rMid = _oreMidPoint.GetComponentInChildren<Renderer>(true);
            Vector3 midPos = (rMid != null && rMid.bounds.size.sqrMagnitude > 0.0001f) ? rMid.bounds.center : HitungFallbackOreMidPoint();
            _runtimeOreMidPoint = BuatRuntimePointJikaPerlu(_runtimeOreMidPoint, "Level3_Runtime_Ore_MidPoint", midPos);
            _oreMidPoint = _runtimeOreMidPoint;
        }

        if (_oreStartPoint == null)
        {
            Vector3 start = HitungFallbackOreStartPoint();
            _runtimeOreStartPoint = BuatRuntimePointJikaPerlu(_runtimeOreStartPoint, "Level3_Runtime_Ore_StartPoint", start);
            _oreStartPoint = _runtimeOreStartPoint;
        }

        if (_oreMidPoint == null)
        {
            Vector3 mid = HitungFallbackOreMidPoint();
            _runtimeOreMidPoint = BuatRuntimePointJikaPerlu(_runtimeOreMidPoint, "Level3_Runtime_Ore_MidPoint", mid);
            _oreMidPoint = _runtimeOreMidPoint;
        }

        if (_runtimeOreStartPoint != null && _oreStartPoint == _runtimeOreStartPoint)
            _runtimeOreStartPoint.position = HitungFallbackOreStartPoint();
        if (_runtimeOreMidPoint != null && _oreMidPoint == _runtimeOreMidPoint && _runtimeOreMidPoint.position.sqrMagnitude < 0.0025f)
            _runtimeOreMidPoint.position = HitungFallbackOreMidPoint();
        if (_runtimeOreEndPoint != null && _oreEndPoint == _runtimeOreEndPoint)
            _runtimeOreEndPoint.position = HitungFallbackOreEndPoint();

        // OVERRIDE: ore KELUAR dari Crusher_Discharge_BlackBox -> NAIK belt -> jatuh ke tangki rebuilt (bukan slurry lama 88,-0.3).
        PaksaOrePathBlackBoxKeTank();
    }

    private void PaksaOrePathBlackBoxKeTank()
    {
        EnsureTankBounds();
        if (!_tankBoundsAda) return;
        var blackBox = CariTransformNamaContains("Crusher_Discharge_BlackBox");
        if (blackBox != null)
        {
            Renderer rb = blackBox.GetComponentInChildren<Renderer>(true);
            Vector3 bbPos = (rb != null ? rb.bounds.center : blackBox.position) + Vector3.up * 0.25f;
            _runtimeOreStartPoint = BuatRuntimePointJikaPerlu(_runtimeOreStartPoint, "Level3_Runtime_Ore_StartPoint", bbPos);
            _runtimeOreStartPoint.position = bbPos;
            _oreStartPoint = _runtimeOreStartPoint;
        }
        Vector3 endPos = new Vector3(_tankCenter.x, _tankBottomY + 1.2f, _tankCenter.z);
        _runtimeOreEndPoint = BuatRuntimePointJikaPerlu(_runtimeOreEndPoint, "Level3_Runtime_Ore_EndPoint", endPos);
        _runtimeOreEndPoint.position = endPos;
        _oreEndPoint = _runtimeOreEndPoint;
        Vector3 midPos = new Vector3(_tankCenter.x + 8f, _tankRimY + 1.2f, _tankCenter.z);
        if (_oreBeltVisual != null)
        {
            Renderer rBelt = _oreBeltVisual.GetComponentInChildren<Renderer>(true);
            if (rBelt != null) midPos = new Vector3(rBelt.bounds.min.x + 1.5f, rBelt.bounds.max.y + 0.05f, rBelt.bounds.center.z);
        }
        // Geser mid mendekat ke start (crusher discharge) supaya ore mepet ke crusher, bukan menggantung jauh.
        if (_oreStartPoint != null)
            midPos = Vector3.Lerp(midPos, _oreStartPoint.position, Mathf.Clamp01(_oreBeltStartSnug * 0.18f));
        midPos.y += _oreBeltHeightOffset;
        _runtimeOreMidPoint = BuatRuntimePointJikaPerlu(_runtimeOreMidPoint, "Level3_Runtime_Ore_MidPoint", midPos);
        _runtimeOreMidPoint.position = midPos;
        _oreMidPoint = _runtimeOreMidPoint;
    }

    private bool OreEndPointTerlaluJauhDariSlurry(Transform point)
    {
        if (point == null || _slurryFill == null)
            return false;

        Vector3 delta = point.position - _slurryFill.position;
        delta.y = 0f;
        return delta.sqrMagnitude > 14f * 14f;
    }

    private Transform BuatRuntimePointJikaPerlu(Transform point, string nama, Vector3 posisi)
    {
        if (point == null)
        {
            GameObject go = new GameObject(nama);
            point = go.transform;
        }

        point.position = posisi;
        return point;
    }

    private Vector3 HitungFallbackOreStartPoint()
    {
        Vector3 tank = _slurryFill != null ? _slurryFill.position : transform.position;
        if (_oreBeltVisual != null)
        {
            Renderer r = _oreBeltVisual.GetComponent<Renderer>();
            if (r != null)
            {
                Bounds b = r.bounds;
                Vector3 jauhDariTank = b.center - tank;
                jauhDariTank.y = 0f;
                if (jauhDariTank.sqrMagnitude < 0.01f)
                    jauhDariTank = -_oreBeltVisual.forward;
                jauhDariTank.Normalize();
                return b.center + jauhDariTank * Mathf.Max(2.5f, b.extents.magnitude * 0.35f) - Vector3.up * Mathf.Max(0.4f, b.extents.y * 0.45f);
            }

            return _oreBeltVisual.position + (_oreBeltVisual.right * -4f) - Vector3.up * 0.9f;
        }

        return tank + new Vector3(7.5f, 3.2f, -1.5f);
    }

    private Vector3 HitungFallbackOreMidPoint()
    {
        Vector3 tank = _slurryFill != null ? _slurryFill.position : transform.position;
        Vector3 start = _oreStartPoint != null ? _oreStartPoint.position : HitungFallbackOreStartPoint();
        Vector3 dirKeTank = tank - start;
        dirKeTank.y = 0f;
        if (dirKeTank.sqrMagnitude < 0.01f)
            dirKeTank = Vector3.left;
        dirKeTank.Normalize();
        return tank - dirKeTank * 1.35f + Vector3.up * 2.5f;
    }

    private Vector3 HitungFallbackOreEndPoint()
    {
        if (_slurryFill == null)
            return transform.position + Vector3.up * 0.5f;

        return HitungWorldPosPermukaanSlurry() + Vector3.up * 0.18f;
    }

    private void SembunyikanOreConveyorBekasRuntime()
    {
        if (!_hapusOreConveyorBekasRuntime || _slurryFill == null)
            return;

        Transform[] all = CariSemuaTransformTermasukInactive();
        Vector3 center = _slurryFill.position;
        for (int i = 0; i < all.Length; i++)
        {
            Transform t = all[i];
            if (t == null || t == transform || t == _oreMover || (_oreMover != null && t.IsChildOf(_oreMover)))
                continue;

            if (AdalahBeltOreUtama(t))
                continue;

            if (!NamaOreConveyorBekas(t.name))
                continue;

            Vector3 delta = t.position - center;
            delta.y = 0f;
            if (delta.sqrMagnitude > 45f * 45f)
                continue;

            t.gameObject.SetActive(false);
        }
    }

    private bool NamaOreConveyorBekas(string nama)
    {
        if (string.IsNullOrEmpty(nama))
            return false;

        string n = nama.ToLowerInvariant();
        return n.Contains("conveyor") ||
               n.Contains("ore_tangga") ||
               n.Contains("moving_clean_laterite_ore") ||
               n.Contains("moving_laterite_ore") ||
               n.Contains("low_profile_ore_cleat") ||
               n.Contains("contained_hopper_ore") ||
               n.Contains("batch_ore_chunk");
    }

    private bool AdalahBeltOreUtama(Transform t)
    {
        if (t == null)
            return false;

        if (_oreBeltVisual != null && (t == _oreBeltVisual || t.IsChildOf(_oreBeltVisual)))
            return true;

        string n = t.name.ToLowerInvariant();
        return n.Contains("l2_v2_wide_inclined_rubber_ore_belt") ||
               n.Contains("wide_inclined_rubber_ore_belt") ||
               n.Contains("rubber_ore_belt");
    }

    private void CacheVisibleAgitatorParts()
    {
        _agitatorVisibleParts.Clear();
        Transform[] all = CariSemuaTransformTermasukInactive();
        Vector3 center = _slurryFill != null ? _slurryFill.position : transform.position;
        List<Transform> candidates = new List<Transform>();

        for (int i = 0; i < all.Length; i++)
        {
            Transform t = all[i];
            if (t == null || !t.gameObject.activeInHierarchy)
                continue;

            string n = t.name.ToLowerInvariant();
            bool looksAgitator = (n.Contains("agitator") &&
                                  (n.Contains("blade") || n.Contains("shaft") || n.Contains("verticalshaft") || n.Contains("vertical_shaft"))) ||
                                 n.Contains("impellerblade");
            bool fixedPart = n.Contains("bridge") || n.Contains("motor") || n.Contains("gearbox") || n.Contains("endcap");
            if (!looksAgitator || fixedPart)
                continue;

            Vector3 delta = t.position - center;
            delta.y = 0f;
            if (delta.sqrMagnitude > 20f * 20f)
                continue;

            candidates.Add(t);
        }

        for (int i = 0; i < candidates.Count; i++)
        {
            Transform candidate = candidates[i];
            bool ancestorAlreadySelected = false;
            for (int j = 0; j < candidates.Count; j++)
            {
                if (i == j)
                    continue;

                Transform other = candidates[j];
                if (candidate.IsChildOf(other))
                {
                    ancestorAlreadySelected = true;
                    break;
                }
            }

            if (!ancestorAlreadySelected && !_agitatorVisibleParts.Contains(candidate))
                _agitatorVisibleParts.Add(candidate);
        }
    }

    private void CacheSlurrySurfaceRenderers()
    {
        _slurrySurfaceRenderers.Clear();
        if (_slurryFill == null)
            return;

        _slurrySurfaceRenderers.AddRange(_slurryFill.GetComponentsInChildren<Renderer>(true));
    }

    private Transform CariTransformNamaContains(string potonganNama)
    {
        Transform[] all = CariSemuaTransformTermasukInactive();
        for (int i = 0; i < all.Length; i++)
        {
            Transform t = all[i];
            if (t != null && t.name.IndexOf(potonganNama, System.StringComparison.OrdinalIgnoreCase) >= 0)
                return t;
        }

        return null;
    }

    private Transform[] CariSemuaTransformTermasukInactive()
    {
        return UnityEngine.Object.FindObjectsByType<Transform>(FindObjectsInactive.Include, FindObjectsSortMode.None);
    }

    private void CacheSceneOreOnBelt()
    {
        if (!_pakaiOreAsliDariBelt)
            return;

        if (_sceneOrePieces.Count > 0)
        {
            bool masihValid = false;
            for (int i = 0; i < _sceneOrePieces.Count; i++)
            {
                if (_sceneOrePieces[i] != null && _sceneOrePieces[i].Transform != null)
                {
                    masihValid = true;
                    break;
                }
            }

            if (masihValid)
                return;
        }

        Transform[] all = CariSemuaTransformTermasukInactive();
        List<Transform> candidates = new List<Transform>();
        for (int i = 0; i < all.Length; i++)
        {
            Transform t = all[i];
            if (t == null || t == transform || (_runtimeOreConveyorRoot != null && t.IsChildOf(_runtimeOreConveyorRoot.transform)))
                continue;

            if (!NamaOreAsliDiBelt(t.name))
                continue;

            Renderer r = t.GetComponentInChildren<Renderer>(true);
            if (r == null)
                continue;

            bool duplicate = false;
            for (int j = 0; j < candidates.Count; j++)
            {
                if (candidates[j] == t || t.IsChildOf(candidates[j]) || candidates[j].IsChildOf(t))
                {
                    duplicate = true;
                    break;
                }
            }

            if (!duplicate)
                candidates.Add(t);
        }

        if (candidates.Count == 0)
            return;

        Vector3 start = _oreStartPoint != null ? _oreStartPoint.position : HitungFallbackOreStartPoint();
        Vector3 mid = _oreMidPoint != null ? _oreMidPoint.position : HitungFallbackOreMidPoint();
        Vector3 dir = mid - start;
        dir.y = 0f;
        if (dir.sqrMagnitude < 0.01f && _slurryFill != null)
            dir = _slurryFill.position - start;
        dir.y = 0f;
        if (dir.sqrMagnitude < 0.01f)
            dir = _oreBeltVisual != null ? _oreBeltVisual.right : Vector3.right;
        dir.Normalize();

        candidates.Sort((a, b) => HitungProyeksiOreDiBelt(a, start, dir).CompareTo(HitungProyeksiOreDiBelt(b, start, dir)));

        _sceneOrePieces.Clear();
        int count = candidates.Count;
        for (int i = 0; i < count; i++)
        {
            Transform ore = candidates[i];
            if (ore == null)
                continue;

            float order = count <= 1 ? 0f : (float)i / (count - 1);
            float lateral = HitungLateralOreDiBelt(ore.position, start, dir);
            if (Mathf.Abs(lateral) < 0.01f)
                lateral = Mathf.Lerp(-_lebarSebarOreRuntime, _lebarSebarOreRuntime, Mathf.Repeat(i * 0.381f, 1f));

            _sceneOrePieces.Add(new RuntimeOrePiece
            {
                Transform = ore,
                Offset = Mathf.Lerp(0.04f, 0.62f, order),
                SpeedMul = Mathf.Lerp(0.92f, 1.18f, Mathf.Repeat(i * 0.217f, 1f)),
                Lateral = Mathf.Clamp(lateral, -_lebarSebarOreRuntime, _lebarSebarOreRuntime),
                SpinAxis = new Vector3(0.41f + order, 0.72f, 0.33f + Mathf.Repeat(i * 0.173f, 1f)).normalized,
                BaseScale = ore.localScale,
                OriginalPosition = ore.position,
                OriginalRotation = ore.rotation,
                OriginalLocalScale = ore.localScale,
                IsSceneOre = true,
                FellIntoTank = false
            });
        }
    }

    private bool NamaOreAsliDiBelt(string nama)
    {
        if (string.IsNullOrEmpty(nama))
            return false;

        string n = nama.ToLowerInvariant();
        if (n.Contains("runtime") || n.Contains("moving_ore") || n.Contains("batch_ore"))
            return false;

        return n.Contains("rounded_ore_rock_on_belt") ||
               n.Contains("ore_rock_on_belt") ||
               (n.Contains("ore") && n.Contains("on_belt"));
    }

    private float HitungProyeksiOreDiBelt(Transform ore, Vector3 start, Vector3 dir)
    {
        if (ore == null)
            return 0f;

        return Vector3.Dot(ore.position - start, dir);
    }

    private float HitungLateralOreDiBelt(Vector3 pos, Vector3 start, Vector3 dir)
    {
        Vector3 side = Vector3.Cross(Vector3.up, dir);
        if (side.sqrMagnitude < 0.01f)
            side = _oreBeltVisual != null ? _oreBeltVisual.right : Vector3.right;

        return Vector3.Dot(pos - start, side.normalized);
    }

    private void ResetSceneOreToBeltStartPose()
    {
        if (!_pakaiOreAsliDariBelt)
            return;

        for (int i = 0; i < _sceneOrePieces.Count; i++)
        {
            RuntimeOrePiece ore = _sceneOrePieces[i];
            if (ore == null || ore.Transform == null)
                continue;

            ore.Transform.gameObject.SetActive(true);
            ore.Transform.position = ore.OriginalPosition;
            ore.Transform.rotation = ore.OriginalRotation;
            ore.Transform.localScale = ore.OriginalLocalScale;
            ore.FellIntoTank = false;
        }
    }

    private void HideSceneOreOnBelt()
    {
        for (int i = 0; i < _sceneOrePieces.Count; i++)
        {
            RuntimeOrePiece ore = _sceneOrePieces[i];
            if (ore != null && ore.Transform != null)
                ore.Transform.gameObject.SetActive(false);
        }
    }

    private void UpdateSceneOreOnBelt(float dt, float speed)
    {
        if (_sceneOrePieces.Count == 0)
            CacheSceneOreOnBelt();

        if (_sceneOrePieces.Count == 0)
            return;

        float globalT = _durasiGerakOre <= 0.01f ? 1f : Mathf.Clamp01(_runtimeOreConveyorTime / _durasiGerakOre);
        for (int i = 0; i < _sceneOrePieces.Count; i++)
        {
            RuntimeOrePiece ore = _sceneOrePieces[i];
            if (ore == null || ore.Transform == null)
                continue;

            if (!ore.Transform.gameObject.activeSelf)
                ore.Transform.gameObject.SetActive(true);

            float pathT = Mathf.Clamp01(ore.Offset + globalT * speed * ore.SpeedMul * 5.25f);
            Vector3 dir = HitungArahOreRuntime(pathT);
            ore.Transform.position = HitungPosisiOreRuntime(pathT, ore.Lateral, 0.04f);
            if (dir.sqrMagnitude > 0.001f)
                ore.Transform.rotation = Quaternion.LookRotation(dir, Vector3.up);

            ore.Transform.Rotate(ore.SpinAxis, 310f * dt * Mathf.Max(0.1f, ore.SpeedMul), Space.Self);

            float scaleMul = pathT > 0.90f ? Mathf.Lerp(1f, 0.72f, Mathf.InverseLerp(0.90f, 1f, pathT)) : 1f;
            ore.Transform.localScale = ore.OriginalLocalScale * scaleMul;

            if (pathT >= 0.995f)
                ore.FellIntoTank = true;
        }
    }

    private GameObject _waterPourGo;
    private Material _waterPourMat;
    private bool _waterPourCoRunning;

    // Air mancur REALISTIS dari L3_SlurryTank_WaterInlet_Flange (stream translucent + UV scroll), bukan partikel cyan.
    private void SetWaterPourAktif(bool aktif)
    {
        if (!aktif) { if (_waterPourGo != null) _waterPourGo.SetActive(false); return; }
        EnsureTankBounds();
        Transform flange = CariTransformNamaContains("SlurryTank_WaterInlet_Flange") ?? CariTransformNamaContains("WaterInlet_Flange");
        Vector3 top = flange != null ? flange.position : new Vector3(89f, 6.7f, 60.4f);
        float cx = _tankBoundsAda ? _tankCenter.x : top.x;
        float cz = _tankBoundsAda ? _tankCenter.z : top.z;
        Vector3 bottom = new Vector3(Mathf.Lerp(top.x, cx, 0.4f), (_tankBoundsAda ? _tankBottomY : 1.5f) + 1.0f, Mathf.Lerp(top.z, cz, 0.4f));
        if (_waterPourGo == null)
        {
            _waterPourGo = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            _waterPourGo.name = "Level3_RealWaterPour";
            var col = _waterPourGo.GetComponent<Collider>(); if (col != null) Destroy(col);
            _waterPourMat = new Material(Shader.Find("Universal Render Pipeline/Lit"));
            _waterPourMat.SetFloat("_Surface", 1f);
            _waterPourMat.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.SrcAlpha);
            _waterPourMat.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
            _waterPourMat.SetInt("_ZWrite", 0);
            _waterPourMat.renderQueue = 3000;
            _waterPourMat.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
            Color wc = new Color(0.60f, 0.78f, 0.95f, 0.55f);
            _waterPourMat.color = wc;
            if (_waterPourMat.HasProperty("_BaseColor")) _waterPourMat.SetColor("_BaseColor", wc);
            if (_waterPourMat.HasProperty("_Smoothness")) _waterPourMat.SetFloat("_Smoothness", 0.92f);
            _waterPourMat.mainTexture = BuatTeksturAliranAir();
            _waterPourGo.GetComponent<Renderer>().sharedMaterial = _waterPourMat;
        }
        _waterPourGo.SetActive(true);
        Vector3 dir = bottom - top; float len = Mathf.Max(0.6f, dir.magnitude);
        _waterPourGo.transform.position = (top + bottom) * 0.5f;
        _waterPourGo.transform.up = dir.sqrMagnitude > 0.0001f ? dir.normalized : Vector3.up;
        _waterPourGo.transform.localScale = new Vector3(0.5f, len * 0.5f, 0.5f);
        if (_waterPourMat != null) _waterPourMat.mainTextureScale = new Vector2(1f, Mathf.Max(2f, len));
        if (!_waterPourCoRunning) { _waterPourCoRunning = true; StartCoroutine(AnimasiAliranAir()); }
    }

    private Texture2D BuatTeksturAliranAir()
    {
        int h = 64; var tex = new Texture2D(2, h); tex.wrapMode = TextureWrapMode.Repeat;
        for (int y = 0; y < h; y++)
        {
            float v = 0.65f + 0.35f * Mathf.Sin(y * 0.7f);
            Color c = new Color(0.7f * v, 0.85f * v, 1f * v, 1f);
            tex.SetPixel(0, y, c); tex.SetPixel(1, y, c);
        }
        tex.Apply(); return tex;
    }

    private System.Collections.IEnumerator AnimasiAliranAir()
    {
        float o = 0f;
        while (_waterPourGo != null && _waterPourGo.activeSelf)
        {
            o = Mathf.Repeat(o + Time.deltaTime * 1.8f, 1f);
            if (_waterPourMat != null) _waterPourMat.mainTextureOffset = new Vector2(0f, -o * 4f);
            yield return null;
        }
        _waterPourCoRunning = false;
    }

    private Mesh _meshBatuAsli;
    private Material _matBatuAsli;

    // Siapkan 1 mesh batu ORE ASLI, di-center ke origin (mesh FBX ter-bake di world coords)
    // supaya bisa dipakai jadi batu BERGERAK di atas belt (bukan kubus primitif).
    private void SiapkanMeshBatuAsli()
    {
        if (_meshBatuAsli != null) return;
        foreach (var t in CariSemuaTransformTermasukInactive())
        {
            if (t == null) continue;
            if (t.name.IndexOf("Rounded_Ore_Rock", System.StringComparison.OrdinalIgnoreCase) < 0) continue;
            var mf = t.GetComponent<MeshFilter>();
            var r = t.GetComponent<Renderer>();
            if (mf == null || mf.sharedMesh == null) continue;
            var src = mf.sharedMesh;
            var verts = src.vertices;
            Vector3 ctr = src.bounds.center;
            for (int i = 0; i < verts.Length; i++) verts[i] -= ctr;
            var m = new Mesh { name = "Level3_OreRock_Centered" };
            m.vertices = verts;
            m.triangles = src.triangles;
            m.normals = src.normals;
            m.uv = src.uv;
            m.RecalculateBounds();
            _meshBatuAsli = m;
            if (r != null) _matBatuAsli = r.sharedMaterial;
            break;
        }
    }

    private AudioSource _conveyorAudio;

    // Suara batu + motor eskalator saat ore berjalan di belt (loop), nyala saat ore berangkat.
    private void SetConveyorAudioAktif(bool aktif)
    {
        if (!aktif) { if (_conveyorAudio != null) _conveyorAudio.Stop(); return; }
        if (_conveyorAudio == null)
        {
            var go = new GameObject("Level3_ConveyorRock_Audio");
            go.transform.SetParent(transform, false);
            if (_oreBeltVisual != null)
            {
                Renderer rb = _oreBeltVisual.GetComponentInChildren<Renderer>(true);
                go.transform.position = rb != null ? rb.bounds.center : _oreBeltVisual.position;
            }
            _conveyorAudio = go.AddComponent<AudioSource>();
            _conveyorAudio.spatialBlend = 0.55f; _conveyorAudio.volume = 0.6f; _conveyorAudio.loop = true;
            _conveyorAudio.maxDistance = 70f; _conveyorAudio.rolloffMode = AudioRolloffMode.Linear;
            _conveyorAudio.clip = GenConveyorRockClip(3.2f);
        }
        if (!_conveyorAudio.isPlaying) _conveyorAudio.Play();
    }

    private AudioClip GenConveyorRockClip(float dur)
    {
        int sr = 44100; int n = Mathf.CeilToInt(dur * sr); var data = new float[n];
        var rnd = new System.Random(7); float hum = 0f; float lp = 0f;
        for (int i = 0; i < n; i++)
        {
            hum += 2f * Mathf.PI * 58f / sr;
            float motor = (Mathf.Sin(hum) + 0.4f * Mathf.Sin(hum * 2f)) * 0.18f;
            float ns = (float)rnd.NextDouble() * 2f - 1f;
            lp += 0.08f * (ns - lp);
            float clatter = lp * 0.5f;
            float knock = ((float)rnd.NextDouble() < 0.0045f) ? ((float)rnd.NextDouble() * 2f - 1f) * 0.55f : 0f;
            data[i] = Mathf.Clamp(motor + clatter + knock, -1f, 1f) * 0.85f;
        }
        int f = Mathf.Min(2200, n / 20);
        for (int i = 0; i < f; i++) { float k = (float)i / f; data[i] *= k; data[n - 1 - i] *= k; }
        var c = AudioClip.Create("Level3_ConveyorRock", n, 1, sr, false); c.SetData(data, 0); return c;
    }

    private void BuatRuntimeOreConveyorFx()
    {
        if (_pakaiOreAsliDariBelt)
            return;

        _runtimeOreConveyorRoot = new GameObject("Level3_Runtime_Ore_Belt_Flow");
        _runtimeOreConveyorRoot.SetActive(false);

        _runtimeOreMaterial = BuatRuntimeMaterial("Level3_Runtime_Laterite_Ore_Material", new Color(0.23f, 0.17f, 0.12f, 1f), false);
        _runtimeBeltCleatMaterial = BuatRuntimeMaterial("Level3_Runtime_Belt_Moving_Cleat_Material", new Color(0.08f, 0.08f, 0.075f, 1f), false);

        SiapkanMeshBatuAsli();
        int oreCount = Mathf.Clamp(_jumlahOreBatuRuntime, 6, 90);
        for (int i = 0; i < oreCount; i++)
        {
            GameObject ore;
            if (_meshBatuAsli != null)
            {
                ore = new GameObject("Level3_Runtime_Moving_Ore_" + i.ToString("00"));
                ore.transform.SetParent(_runtimeOreConveyorRoot.transform, true);
                ore.AddComponent<MeshFilter>().sharedMesh = _meshBatuAsli;
                ore.AddComponent<MeshRenderer>().sharedMaterial = _matBatuAsli != null ? _matBatuAsli : _runtimeOreMaterial;
            }
            else
            {
                PrimitiveType type = i % 3 == 0 ? PrimitiveType.Sphere : PrimitiveType.Cube;
                ore = GameObject.CreatePrimitive(type);
                ore.name = "Level3_Runtime_Moving_Ore_" + i.ToString("00");
                ore.transform.SetParent(_runtimeOreConveyorRoot.transform, true);
                Renderer r = ore.GetComponent<Renderer>();
                if (r != null) r.sharedMaterial = _runtimeOreMaterial;
                Collider c = ore.GetComponent<Collider>();
                if (c != null) Destroy(c);
            }

            float seedA = Mathf.Repeat(i * 0.371f, 1f);
            float seedB = Mathf.Repeat(i * 0.619f, 1f);
            float size = Mathf.Lerp(0.16f, 0.34f, seedA);
            _runtimeOrePieces.Add(new RuntimeOrePiece
            {
                Transform = ore.transform,
                Offset = (float)i / oreCount,
                SpeedMul = Mathf.Lerp(0.88f, 1.18f, seedB),
                Lateral = Mathf.Lerp(-_lebarSebarOreRuntime, _lebarSebarOreRuntime, seedA),
                SpinAxis = new Vector3(0.35f + seedA, 0.55f, 0.25f + seedB).normalized,
                BaseScale = new Vector3(size * Mathf.Lerp(0.75f, 1.35f, seedB), size * Mathf.Lerp(0.55f, 1.05f, seedA), size * Mathf.Lerp(0.75f, 1.25f, 1f - seedB))
            });
        }

        int cleatCount = Mathf.Clamp(_jumlahBeltCleatRuntime, 4, 48);
        for (int i = 0; i < cleatCount; i++)
        {
            GameObject cleat = GameObject.CreatePrimitive(PrimitiveType.Cube);
            cleat.name = "Level3_Runtime_Belt_Moving_Cleat_" + i.ToString("00");
            cleat.transform.SetParent(_runtimeOreConveyorRoot.transform, true);

            Renderer r = cleat.GetComponent<Renderer>();
            if (r != null)
                r.sharedMaterial = _runtimeBeltCleatMaterial;

            Collider c = cleat.GetComponent<Collider>();
            if (c != null)
                Destroy(c);

            _runtimeBeltCleats.Add(new RuntimeBeltCleat
            {
                Transform = cleat.transform,
                Offset = (float)i / cleatCount * 0.72f,
                Lateral = 0f
            });
        }
    }

    private void SetConveyorOreFxAktif(bool aktif)
    {
        EnsureOrePathRuntime();

        if (_runtimeOreConveyorRoot != null && _pakaiOreAsliDariBelt)
        {
            Destroy(_runtimeOreConveyorRoot);
            _runtimeOreConveyorRoot = null;
            _runtimeOrePieces.Clear();
            _runtimeBeltCleats.Clear();
        }

        CacheSceneOreOnBelt();

        if (!_pakaiOreAsliDariBelt && _runtimeOreConveyorRoot == null)
            BuatRuntimeOreConveyorFx();

        if (!aktif) _crusherFxAktif = false;
        _runtimeOreConveyorAktif = aktif;
        SetConveyorAudioAktif(aktif);
        if (aktif)
        {
            _runtimeOreConveyorTime = 0f;
            ResetSceneOreToBeltStartPose();
        }
        else if (!_sequenceSudahDimulai)
        {
            ResetSceneOreToBeltStartPose();
        }

        if (_oreBeltVisual != null && aktif && !_oreBeltVisual.gameObject.activeSelf)
            _oreBeltVisual.gameObject.SetActive(true);

        if (_runtimeOreConveyorRoot != null && !_pakaiOreAsliDariBelt)
            _runtimeOreConveyorRoot.SetActive(aktif);

        if (!aktif && _pakaiOreAsliDariBelt)
            HideSceneOreOnBelt();

        if (aktif)
            UpdateRuntimeOreConveyor(0f);
    }

    // DEBUG: klik kanan komponen ini untuk uji aliran ore di belt (Level3_Runtime_Ore_Belt_Flow).
    [ContextMenu("DEBUG: Level3 Ore Belt Flow ON")]
    private void DebugOreBeltFlowOn() { SetConveyorOreFxAktif(true); }

    [ContextMenu("DEBUG: Level3 Ore Belt Flow OFF")]
    private void DebugOreBeltFlowOff() { SetConveyorOreFxAktif(false); }


    private void UpdateRuntimeOreConveyor(float dt)
    {
        if (!_runtimeOreConveyorAktif || _oreStartPoint == null || _oreEndPoint == null)
            return;

        float speed = Mathf.Max(0.02f, _kecepatanOreBeltRuntime);
        _runtimeOreConveyorTime += dt;
        UpdateOreBeltMaterial(dt, speed);

        if (_pakaiOreAsliDariBelt)
        {
            UpdateSceneOreOnBelt(dt, speed);
            return;
        }

        if (_runtimeOreConveyorRoot == null)
            return;

        for (int i = 0; i < _runtimeBeltCleats.Count; i++)
        {
            RuntimeBeltCleat cleat = _runtimeBeltCleats[i];
            if (cleat == null || cleat.Transform == null)
                continue;

            float t = Mathf.Repeat((_runtimeOreConveyorTime * speed) + cleat.Offset, 0.72f);
            Vector3 dir = HitungArahOreRuntime(t);
            cleat.Transform.position = HitungPosisiOreRuntime(t, cleat.Lateral, 0.045f);
            cleat.Transform.rotation = Quaternion.LookRotation(dir, Vector3.up);
            cleat.Transform.localScale = new Vector3(Mathf.Max(1f, _lebarSebarOreRuntime * 2.25f), 0.035f, 0.14f);
        }

        for (int i = 0; i < _runtimeOrePieces.Count; i++)
        {
            RuntimeOrePiece ore = _runtimeOrePieces[i];
            if (ore == null || ore.Transform == null)
                continue;

            float t = Mathf.Clamp01((_runtimeOreConveyorTime * speed * ore.SpeedMul) + ore.Offset * 0.72f);
            ore.Transform.position = HitungPosisiOreRuntime(t, ore.Lateral, 0.06f);
            ore.Transform.localScale = ore.BaseScale * (t > 0.72f ? Mathf.Lerp(1f, 0.72f, Mathf.InverseLerp(0.72f, 1f, t)) : 1f);
            ore.Transform.Rotate(ore.SpinAxis, 260f * dt * ore.SpeedMul, Space.Self);
        }
    }

    private void UpdateOreBeltMaterial(float dt, float speed)
    {
        if (_oreBeltMaterial == null)
            return;

        _runtimeOreBeltOffset = Mathf.Repeat(_runtimeOreBeltOffset + dt * speed * 1.4f, 1f);
        Vector2 offset = new Vector2(0f, -_runtimeOreBeltOffset);
        if (_oreBeltMaterial.HasProperty("_MainTex"))
            _oreBeltMaterial.SetTextureOffset("_MainTex", offset);
        if (_oreBeltMaterial.HasProperty("_BaseMap"))
            _oreBeltMaterial.SetTextureOffset("_BaseMap", offset);
    }

    private Vector3 HitungPosisiOreRuntime(float t, float lateral, float lift)
    {
        const float split = 0.72f;
        Vector3 p;
        float sideFade = 1f;
        if (t > split && _oreMidPoint != null && _oreEndPoint != null)
        {
            float dropT = Mathf.InverseLerp(split, 1f, t);
            p = Vector3.Lerp(_oreMidPoint.position, _oreEndPoint.position, dropT);
            p.y += Mathf.Sin(dropT * Mathf.PI) * _tinggiJatuhOreRuntime;
            sideFade = 1f - dropT;
        }
        else
        {
            p = HitungPosisiOre(t);
        }

        Vector3 dir = HitungArahOreRuntime(t);
        Vector3 side = Vector3.Cross(Vector3.up, dir);
        if (side.sqrMagnitude < 0.001f)
            side = _oreBeltVisual != null ? _oreBeltVisual.right : Vector3.right;

        return p + side.normalized * (lateral * sideFade) + Vector3.up * lift;
    }

    private Vector3 HitungArahOreRuntime(float t)
    {
        float aT = Mathf.Clamp01(t - 0.015f);
        float bT = Mathf.Clamp01(t + 0.015f);
        Vector3 a = HitungPosisiOre(aT);
        Vector3 b = HitungPosisiOre(bT);
        Vector3 dir = b - a;
        if (dir.sqrMagnitude < 0.001f)
            dir = _oreEndPoint != null && _oreStartPoint != null ? _oreEndPoint.position - _oreStartPoint.position : Vector3.forward;
        if (dir.sqrMagnitude < 0.001f)
            dir = Vector3.forward;
        return dir.normalized;
    }

    private void BuatRuntimeWaterStream()
    {
        _runtimeWaterStream = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        _runtimeWaterStream.name = "Level3_Runtime_Falling_Water_Stream";
        _runtimeWaterStreamTransform = _runtimeWaterStream.transform;
        _runtimeWaterMaterial = BuatRuntimeMaterial("Level3_Runtime_Water_Material", new Color(0.35f, 0.9f, 1f, 0.72f), true);
        Renderer streamRenderer = _runtimeWaterStream.GetComponent<Renderer>();
        if (streamRenderer != null)
            streamRenderer.sharedMaterial = _runtimeWaterMaterial;

        Collider streamCollider = _runtimeWaterStream.GetComponent<Collider>();
        if (streamCollider != null)
            Destroy(streamCollider);

        GameObject droplets = new GameObject("Level3_Runtime_Water_Droplets");
        droplets.transform.SetParent(_runtimeWaterStreamTransform, false);
        _runtimeWaterDroplets = droplets.AddComponent<ParticleSystem>();
        var main = _runtimeWaterDroplets.main;
        main.loop = true;
        main.simulationSpace = ParticleSystemSimulationSpace.World;
        main.startLifetime = new ParticleSystem.MinMaxCurve(0.35f, 0.85f);
        main.startSpeed = new ParticleSystem.MinMaxCurve(3.8f, 7.2f);
        main.startSize = new ParticleSystem.MinMaxCurve(0.06f, 0.16f);
        main.gravityModifier = new ParticleSystem.MinMaxCurve(1.15f);
        main.maxParticles = 900;
        var emission = _runtimeWaterDroplets.emission;
        emission.rateOverTime = 420f;
        var shape = _runtimeWaterDroplets.shape;
        shape.enabled = true;
        shape.shapeType = ParticleSystemShapeType.Cone;
        shape.angle = 11f;
        shape.radius = 0.18f;
        shape.length = 0.45f;

        ParticleSystemRenderer dropletRenderer = _runtimeWaterDroplets.GetComponent<ParticleSystemRenderer>();
        if (dropletRenderer != null)
            dropletRenderer.sharedMaterial = _runtimeWaterMaterial;

        _runtimeWaterSplash = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        _runtimeWaterSplash.name = "Level3_Runtime_Water_Impact_Splash";
        _runtimeWaterSplashTransform = _runtimeWaterSplash.transform;
        Renderer splashRenderer = _runtimeWaterSplash.GetComponent<Renderer>();
        if (splashRenderer != null)
            splashRenderer.sharedMaterial = _runtimeWaterMaterial;
        Collider splashCollider = _runtimeWaterSplash.GetComponent<Collider>();
        if (splashCollider != null)
            Destroy(splashCollider);

        _runtimeWaterStream.SetActive(false);
        _runtimeWaterSplash.SetActive(false);
    }

    private void BuatRuntimeTankLiquidVolume()
    {
        _runtimeTankLiquidVolume = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        _runtimeTankLiquidVolume.name = "Level3_Runtime_Tank_Liquid_Rising_75";
        _runtimeTankLiquidVolumeTransform = _runtimeTankLiquidVolume.transform;
        _runtimeTankLiquidMaterial = BuatRuntimeMaterial("Level3_Runtime_Tank_Liquid_Material", new Color(0.48f, 0.26f, 0.14f, 0.78f), true);

        Renderer liquidRenderer = _runtimeTankLiquidVolume.GetComponent<Renderer>();
        if (liquidRenderer != null)
            liquidRenderer.sharedMaterial = _runtimeTankLiquidMaterial;

        Collider liquidCollider = _runtimeTankLiquidVolume.GetComponent<Collider>();
        if (liquidCollider != null)
            Destroy(liquidCollider);

        SetRuntimeTankLiquidVisible(false);
        UpdateRuntimeTankLiquidVolume();
    }

    private void SetRuntimeTankLiquidVisible(bool visible)
    {
        if (_runtimeTankLiquidVolume == null)
            return;

        if (_runtimeTankLiquidVolume.activeSelf != visible)
            _runtimeTankLiquidVolume.SetActive(visible);
    }

    private void UpdateRuntimeTankLiquidVolume()
    {
        if (_runtimeTankLiquidVolumeTransform == null || _slurryFill == null)
            return;

        Vector3 center = HitungCenterSlurryTank();
        float fillT = HitungProgressSlurryFill();
        float radius = HitungRadiusSlurryTankLiquid();
        float bottomY = HitungBottomYSlurryTankLiquid();
        float topTargetY = HitungTopTargetYSlurryTankLiquid();
        float topY = Mathf.Lerp(bottomY + 0.08f, topTargetY, fillT);
        float height = Mathf.Max(0.08f, topY - bottomY);

        _runtimeTankLiquidVolumeTransform.position = new Vector3(center.x, bottomY + height * 0.5f, center.z);
        _runtimeTankLiquidVolumeTransform.rotation = Quaternion.identity;
        _runtimeTankLiquidVolumeTransform.localScale = new Vector3(radius * 2f, height * 0.5f, radius * 2f);

        if (_runtimeTankLiquidMaterial != null)
        {
            float pulse = 0.72f + Mathf.Sin(Time.time * 4.5f) * 0.06f;
            Color color = new Color(0.48f, 0.26f, 0.14f, pulse);
            _runtimeTankLiquidMaterial.color = color;
            if (_runtimeTankLiquidMaterial.HasProperty("_BaseColor"))
                _runtimeTankLiquidMaterial.SetColor("_BaseColor", color);
            if (_runtimeTankLiquidMaterial.HasProperty("_Color"))
                _runtimeTankLiquidMaterial.SetColor("_Color", color);
        }
    }

    private Vector3 HitungCenterSlurryTank()
    {
        EnsureTankBounds();
        if (_tankBoundsAda) return _tankCenter;
        if (_slurryFill == null)
            return transform.position;

        Vector3 center = _slurryFill.position;
        Transform parent = _slurryFill.parent;
        if (parent != null)
            center = new Vector3(parent.position.x, center.y, parent.position.z);

        return center;
    }

    private float HitungProgressSlurryFill()
    {
        if (_slurryFill == null)
            return 0f;

        float byPos = Mathf.InverseLerp(_slurryLocalPosAwal.y, _slurryLocalPosTarget25.y, _slurryFill.localPosition.y);
        float byScale = Mathf.InverseLerp(Mathf.Max(0.001f, _slurryLocalScaleAwal.y), Mathf.Max(0.002f, _slurryLocalScaleTarget25.y), _slurryFill.localScale.y);
        return Mathf.Clamp01(Mathf.Max(_runtimeSlurryFillProgress, byPos, byScale));
    }

    private float HitungRadiusSlurryTankLiquid()
    {
        EnsureTankBounds();
        if (_tankBoundsAda) return _tankRadius;
        if (_slurryFill != null)
            return Mathf.Clamp(Mathf.Min(_slurryFill.lossyScale.x, _slurryFill.lossyScale.z) * 0.36f, 2.5f, 7.25f);

        return 5.5f;
    }

    private float HitungBottomYSlurryTankLiquid()
    {
        EnsureTankBounds();
        if (_tankBoundsAda) return _tankBottomY;
        if (_slurryFill != null && _slurryFill.parent != null)
            return _slurryFill.parent.position.y - 3.05f;

        return _slurryFill != null ? _slurryFill.position.y - 0.1f : transform.position.y;
    }

    private float HitungTopTargetYSlurryTankLiquid()
    {
        EnsureTankBounds();
        if (_tankBoundsAda) return _tankRimY - 1.2f;
        if (_slurryFill != null && _slurryFill.parent != null)
            return _slurryFill.parent.position.y + 3.35f;

        return _slurryFill != null ? _slurryFill.position.y + 3.2f : transform.position.y + 3.2f;
    }

    private Vector3 HitungTargetAirMasukTank()
    {
        Vector3 center = HitungCenterSlurryTank();
        Vector3 surface = HitungWorldPosPermukaanSlurry();
        if (_runtimeTankLiquidVolumeTransform != null && _runtimeTankLiquidVolume != null && _runtimeTankLiquidVolume.activeSelf)
            surface = _runtimeTankLiquidVolumeTransform.position + Vector3.up * _runtimeTankLiquidVolumeTransform.localScale.y;

        return new Vector3(center.x, surface.y + 0.08f, center.z);
    }

    private void BuatRuntimeSwirlSurface()
    {
        SiapkanMeshBatuAsli();
        _runtimeSwirlRoot = new GameObject("Level3_Runtime_Slurry_Surface_Swirl");
        _runtimeSwirlRootTransform = _runtimeSwirlRoot.transform;
        var rnd = new System.Random(31);
        int n = 28; // berton-ton ore chunk di dalam slurry
        for (int i = 0; i < n; i++)
        {
            float ang = (float)rnd.NextDouble() * Mathf.PI * 2f;
            float radius = Mathf.Sqrt((float)rnd.NextDouble()) * 1.45f;
            GameObject chunk;
            if (_meshBatuAsli != null)
            {
                chunk = new GameObject("Level3_Runtime_Slurry_Ore_" + i.ToString("00"));
                chunk.transform.SetParent(_runtimeSwirlRootTransform, false);
                chunk.AddComponent<MeshFilter>().sharedMesh = _meshBatuAsli;
                chunk.AddComponent<MeshRenderer>().sharedMaterial = _matBatuAsli != null ? _matBatuAsli : _runtimeOreMaterial;
            }
            else
            {
                chunk = GameObject.CreatePrimitive(PrimitiveType.Cube);
                chunk.name = "Level3_Runtime_Slurry_Ore_" + i.ToString("00");
                chunk.transform.SetParent(_runtimeSwirlRootTransform, false);
                var cc0 = chunk.GetComponent<Collider>(); if (cc0 != null) Destroy(cc0);
                var rr0 = chunk.GetComponent<Renderer>(); if (rr0 != null) rr0.sharedMaterial = _runtimeOreMaterial;
            }
            chunk.transform.localPosition = new Vector3(Mathf.Cos(ang) * radius, -0.05f - (float)rnd.NextDouble() * 0.75f, Mathf.Sin(ang) * radius);
            chunk.transform.localRotation = Quaternion.Euler((float)rnd.NextDouble() * 360f, (float)rnd.NextDouble() * 360f, (float)rnd.NextDouble() * 360f);
            float s = Mathf.Lerp(0.45f, 0.95f, (float)rnd.NextDouble());
            chunk.transform.localScale = new Vector3(s, s, s);
        }
        _runtimeSwirlRoot.SetActive(false);
        UpdateRuntimeSlurrySurface(0f);
    }

    private Material BuatRuntimeMaterial(string nama, Color warna, bool transparan = true)
    {
        Shader shader = Shader.Find("Universal Render Pipeline/Lit");
        if (shader == null)
            shader = Shader.Find("Standard");

        Material mat = new Material(shader);
        mat.name = nama;
        mat.color = warna;
        if (mat.HasProperty("_BaseColor"))
            mat.SetColor("_BaseColor", warna);
        if (mat.HasProperty("_Color"))
            mat.SetColor("_Color", warna);

        if (transparan)
        {
            if (mat.HasProperty("_Surface"))
                mat.SetFloat("_Surface", 1f);
            if (mat.HasProperty("_Blend"))
                mat.SetFloat("_Blend", 0f);
            if (mat.HasProperty("_SrcBlend"))
                mat.SetFloat("_SrcBlend", (float)UnityEngine.Rendering.BlendMode.SrcAlpha);
            if (mat.HasProperty("_DstBlend"))
                mat.SetFloat("_DstBlend", (float)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
            if (mat.HasProperty("_ZWrite"))
                mat.SetFloat("_ZWrite", 0f);
            mat.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
            mat.EnableKeyword("_ALPHABLEND_ON");
            if (mat.HasProperty("_Mode"))
                mat.SetFloat("_Mode", 3f);
            mat.renderQueue = 3000;
        }
        else
        {
            mat.renderQueue = 2000;
        }

        return mat;
    }

    private void UpdateRuntimeWaterFlow(float dt)
    {
        if (!_runtimeWaterFlowAktif || _runtimeWaterStreamTransform == null || _slurryFill == null)
            return;

        Vector3 start = HitungPosisiOutletAir();
        Vector3 end = HitungTargetAirMasukTank();
        if (start.y <= end.y + 0.05f)
        {
            Vector3 center = HitungCenterSlurryTank();
            end = new Vector3(center.x, HitungTopTargetYSlurryTankLiquid() + 0.45f, center.z);
        }

        Vector3 delta = end - start;
        float length = Mathf.Max(0.15f, delta.magnitude);
        Vector3 mid = (start + end) * 0.5f;
        _runtimeWaterStreamTransform.position = mid;
        _runtimeWaterStreamTransform.rotation = Quaternion.FromToRotation(Vector3.up, delta.normalized);
        float pulse = 1f + Mathf.Sin(Time.time * 18f) * 0.12f;
        _runtimeWaterStreamTransform.localScale = new Vector3(0.18f * pulse, length * 0.5f, 0.18f * pulse);

        if (_runtimeWaterDroplets != null)
        {
            _runtimeWaterDroplets.transform.position = start;
            _runtimeWaterDroplets.transform.rotation = _runtimeWaterStreamTransform.rotation;
        }

        if (_runtimeWaterSplashTransform != null)
        {
            float splashPulse = 1.25f + Mathf.Sin(Time.time * 23f) * 0.28f;
            _runtimeWaterSplashTransform.position = end + Vector3.up * 0.012f;
            _runtimeWaterSplashTransform.rotation = Quaternion.identity;
            _runtimeWaterSplashTransform.localScale = new Vector3(splashPulse, 0.012f, splashPulse);
        }

        UpdateBlenderWaterFlowFx(start, end, length);

        _runtimeWaterOffset = Mathf.Repeat(_runtimeWaterOffset + dt * 1.6f, 1f);
        float alphaPulse = 0.58f + Mathf.Sin(Time.time * 12f) * 0.14f;
        Color color = new Color(0.35f, 0.9f, 1f, alphaPulse);
        if (_runtimeWaterMaterial != null)
        {
            _runtimeWaterMaterial.color = color;
            if (_runtimeWaterMaterial.HasProperty("_BaseColor"))
                _runtimeWaterMaterial.SetColor("_BaseColor", color);
            if (_runtimeWaterMaterial.HasProperty("_MainTex"))
                _runtimeWaterMaterial.SetTextureOffset("_MainTex", new Vector2(0f, -_runtimeWaterOffset));
            if (_runtimeWaterMaterial.HasProperty("_BaseMap"))
                _runtimeWaterMaterial.SetTextureOffset("_BaseMap", new Vector2(0f, -_runtimeWaterOffset));
        }
    }

    private void UpdateBlenderWaterFlowFx(Vector3 start, Vector3 end, float length)
    {
        if (_waterFlowFx == null)
            return;

        Transform fx = _waterFlowFx.transform;
        Vector3 horizontal = start - end;
        horizontal.y = 0f;
        if (horizontal.sqrMagnitude < 0.01f)
            horizontal = Vector3.left;

        Quaternion faceOutlet = Quaternion.FromToRotation(Vector3.left, horizontal.normalized);
        float scale = Mathf.Clamp(length / 3.25f, 0.55f, 2.65f);
        float pulse = 0.96f + Mathf.Sin(Time.time * 16f) * 0.035f;
        fx.position = end;
        fx.rotation = faceOutlet;
        fx.localScale = Vector3.one * scale * pulse;
    }

    private Vector3 HitungPosisiOutletAir()
    {
        if (_waterPipeOutlet == null)
            return _slurryFill != null ? HitungWorldPosPermukaanSlurry() + new Vector3(-2.2f, 3.2f, 0f) : transform.position + Vector3.up * 2f;

        if (_waterPipeOutlet.name.IndexOf("SpawnWater", System.StringComparison.OrdinalIgnoreCase) >= 0)
            return _waterPipeOutlet.position;

        Vector3 a = _waterPipeOutlet.position + _waterPipeOutlet.up * Mathf.Max(0.15f, _waterPipeOutlet.lossyScale.y);
        Vector3 b = _waterPipeOutlet.position - _waterPipeOutlet.up * Mathf.Max(0.15f, _waterPipeOutlet.lossyScale.y);
        return a.y < b.y ? a : b;
    }

    private void UpdateRuntimeAgitator(float dt)
    {
        float target = _runtimeAgitatorAktif ? Mathf.Abs(_kecepatanAgitatorVisibleDeg) : 0f;
        float accel = Mathf.Max(0.01f, Mathf.Abs(_akselerasiAgitatorVisible));
        _runtimeAgitatorSpeed = Mathf.MoveTowards(_runtimeAgitatorSpeed, target, accel * dt);
        if (_runtimeAgitatorSpeed <= 0.001f)
            return;

        if (_agitatorVisibleParts.Count == 0)
            CacheVisibleAgitatorParts();

        float direction = _kecepatanAgitatorVisibleDeg < 0f ? -1f : 1f;
        float angle = _runtimeAgitatorSpeed * dt * direction;
        Vector3 center = HitungPusatAgitatorVisible();
        for (int i = _agitatorVisibleParts.Count - 1; i >= 0; i--)
        {
            Transform t = _agitatorVisibleParts[i];
            if (t == null || !t.gameObject.activeInHierarchy)
            {
                _agitatorVisibleParts.RemoveAt(i);
                continue;
            }

            t.RotateAround(center, Vector3.up, angle);
            t.Rotate(Vector3.up, angle, Space.World);
        }

        if (_runtimeSwirlRootTransform != null && _runtimeSwirlRoot.activeSelf)
            _runtimeSwirlRootTransform.Rotate(Vector3.up, angle * 0.85f, Space.World);
    }

    private Vector3 HitungPusatAgitatorVisible()
    {
        if (_agitatorVisibleParts.Count == 0)
            return _slurryFill != null ? _slurryFill.position : transform.position;

        Vector3 sum = Vector3.zero;
        int count = 0;
        for (int i = 0; i < _agitatorVisibleParts.Count; i++)
        {
            Transform t = _agitatorVisibleParts[i];
            if (t == null)
                continue;

            sum += t.position;
            count++;
        }

        if (count == 0)
            return _slurryFill != null ? _slurryFill.position : transform.position;

        return sum / count;
    }

    private void UpdateRuntimeSlurrySurface(float dt)
    {
        if (_runtimeSwirlRootTransform == null || _slurryFill == null)
            return;

        Vector3 surface = HitungWorldPosPermukaanSlurry() + Vector3.up * 0.05f;
        _runtimeSwirlRootTransform.position = surface;
        // Kecilkan swirl supaya tidak keluar dari slurry tank: pakai 0.30 dari radius tank
        // dan basis scatter 1.45 (bukan 2.65) agar chunk tetap di dalam dinding tank.
        float radius = Mathf.Max(0.25f, Mathf.Min(_slurryFill.lossyScale.x, _slurryFill.lossyScale.z) * 0.30f);
        float scale = radius / 1.45f;
        _runtimeSwirlRootTransform.localScale = new Vector3(scale, 1f, scale);

        // Ore muncul BERTAHAP seiring cairan naik (berton-ton ore terakumulasi di dalam slurry).
        int cc = _runtimeSwirlRootTransform.childCount;
        float prog = Mathf.Clamp01(_runtimeSlurryFillProgress);
        for (int i = 0; i < cc; i++)
        {
            bool show = prog >= ((i + 0.5f) / cc) * 0.92f;
            var ch = _runtimeSwirlRootTransform.GetChild(i);
            if (ch.gameObject.activeSelf != show) ch.gameObject.SetActive(show);
        }
    }


    /// <summary>
    /// Saat slurry mencapai 75% (SiapLaporanAkhir): tampilkan arrow ke agitator + notif HUD.
    /// </summary>
    private void OnLevel3PhaseChanged(GameLevelManager.Level3Phase phase)
    {
        if (phase != GameLevelManager.Level3Phase.SiapLaporanAkhir)
            return;

        // Arrow nunjuk ke agitator supaya pemain wajib melihat mesin pengaduk.
        EnsureLevel3RuntimeVisuals();
        if (_agitatorVisibleParts.Count > 0 && _agitatorVisibleParts[0] != null)
            ShowArrowKe(_agitatorVisibleParts[0]);
        else if (_slurryAgitator != null)
            ShowArrowKe(_slurryAgitator.transform);
        else if (_slurryFill != null)
            ShowArrowKe(_slurryFill);

        if (_hud != null)
            _hud.ShowNotifPublic("Slurry mencapai 75%. Lihat mesin pengaduk lalu kirim laporan HT akhir.");
    }


    /// <summary>
    /// Dipanggil saat laporan HT akhir Level 3 diterima.
    /// Tunda transisi otomatis dan tampilkan panel pilihan.
    /// </summary>
    private void OnLevel3LaporanAkhirDiterima()
    {
        if (GameLevelManager.Instance == null) return;
        if (GameLevelManager.Instance.CurrentLevel != GameLevelManager.GameLevel.Level3_OreSlurry) return;

        MulaiAgitatorSetelahLaporanAkhir();

        // Tunda transisi otomatis
        GameLevelManager.Instance.TundaTransisiLevel3(true);

        // Tampilkan panel pilihan
        EnsureChoicePanel();
        if (_choicePanel != null)
        {
            _choicePanel.Show(
                onLanjut: OnPilihLanjut,
                onLihat: OnPilihLihat
            );
        }
    }

    private void OnPilihLanjut()
    {
        // Player pilih lanjut: fade out pelan, lalu lanjutkan transisi ke Level 4
        if (_hud != null)
            _hud.PlayManualFade(_durasiFadeLanjut);

        // Lanjutkan transisi yang ditunda
        GameLevelManager.Instance?.LanjutkanTransisiLevel3();
    }

    private void OnPilihLihat()
    {
        // Player pilih lihat proses: biarkan di area slurry, transisi tetap ditunda.
        // Player bisa explore sesuka hati. Nanti kalau mau lanjut, bisa PTT lagi
        // atau kita bisa show panel lagi setelah beberapa detik.
        if (_hud != null)
            _hud.ShowNotifPublic("Amati proses slurry. Tekan T (PTT) lagi saat siap lanjut.");

        // Subscribe ke PTT release berikutnya untuk show panel lagi
        StartCoroutine(TungguPttUntukLanjut());
    }

    private System.Collections.IEnumerator TungguPttUntukLanjut()
    {
        // Tunggu 3 detik dulu biar player sempat lihat-lihat
        yield return new WaitForSeconds(3f);

        // Tunggu sampai player tekan PTT lagi
        bool pttDitekan = false;
        System.Action onPtt = () => pttDitekan = true;
        WalkieTalkieManager.OnPTTDilepas += onPtt;

        while (!pttDitekan)
            yield return null;

        WalkieTalkieManager.OnPTTDilepas -= onPtt;

        // Show panel lagi
        EnsureChoicePanel();
        _choicePanel?.Show(onLanjut: OnPilihLanjut, onLihat: OnPilihLihat);
    }

    private void EnsureChoicePanel()
    {
        if (_choicePanel != null) return;

        _choicePanel = UnityEngine.Object.FindFirstObjectByType<LevelTransitionChoicePanel>();
        if (_choicePanel != null) return;

        // Auto-create
        var go = new GameObject("Level3_ChoicePanel_Auto");
        _choicePanel = go.AddComponent<LevelTransitionChoicePanel>();
    }
}
