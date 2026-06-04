using System.Collections;
using UnityEngine;
using Unity.XR.CoreUtils;
using UnityEngine.XR.Interaction.Toolkit.Interactables;
using UnityEngine.XR.Interaction.Toolkit.Interactors;

/// <summary>
/// OLIVIA VR - Level5SteamValveController.cs
///
/// FLOW LEVEL 5 — Steam Valve & Pre-Heater:
///   1. Player teleport ke area Pre-Heater (setelah Level 4 selesai)
///   2. Player grab Steam Valve handwheel → putar searah jarum jam
///   3. Setiap rotasi → suhu Pre-Heater naik proporsional (25°C → 200°C)
///   4. Steam particle FX intensitas naik seiring valve terbuka
///   5. Audio: suara mendesis steam makin keras
///   6. Saat suhu mencapai 180°C → quest tercentang
///   7. Player lapor HT: "katup steam terbuka"
///   8. Fade → teleport ke DCS untuk Level 6
///
/// Mekanik valve VR: XRGrabInteractable. Saat di-grab, akumulasi rotasi diambil
/// dari delta yaw tangan controller (atau interactor attachTransform).
/// 4 putaran penuh = 1440° = 100% open.
/// </summary>
public class Level5SteamValveController : MonoBehaviour
{
    [Header("=== Referensi Pemain ===")]
    [SerializeField] private Transform _playerRigRoot;

    [Header("=== Steam Valve (Handwheel) ===")]
    [Tooltip("Transform handwheel yang diputar player (visual yang ikut berputar).")]
    [SerializeField] private Transform _valveWheel;
    [Tooltip("XRGrabInteractable di valve wheel. Auto-find dari _valveWheel kalau kosong.")]
    [SerializeField] private XRGrabInteractable _valveGrab;
    [Tooltip("Sumbu rotasi valve dalam local space dari _valveWheel (default Y atas = handwheel diputar di bidang horizontal). Ganti ke (0,0,1) kalau handwheel disc menghadap player.")]
    [SerializeField] private Vector3 _sumbuRotasiValveLocal = Vector3.up;
    [Tooltip("Total derajat rotasi untuk valve 100% open (4 putaran = 1440°).")]
    [SerializeField] private float _totalDerajatFullOpen = 1440f;
    [Tooltip("Kecepatan rotasi simulasi keyboard (derajat per detik) untuk testing tanpa headset.")]
    [SerializeField] private float _kecepatanRotasiKeyboardSimulasi = 240f;
    [Tooltip("Skala respons rotasi VR (1 = 1:1 tangan ke valve, 2 = setiap 1° tangan = 2° valve).")]
    [SerializeField, Range(0.5f, 4f)] private float _skalaResponsRotasiVR = 1.5f;
    [Tooltip("Inverskan arah rotasi VR (kalau valve berputar berlawanan arah dari yang diharapkan).")]
    [SerializeField] private bool _balikkanArahRotasiVR = false;

    [Header("=== Suhu Pre-Heater ===")]
    [SerializeField] private float _suhuAwal = 25f;
    [SerializeField] private float _suhuTarget = 200f;
    [SerializeField] private float _suhuMinimumQuest = 180f;

    [Header("=== Steam Particle FX ===")]
    [SerializeField] private ParticleSystem _steamParticle;
    [SerializeField] private float _steamEmisiMax = 80f;
    [SerializeField] private bool _autoFindSteamFx = true;
    [Tooltip("Mesh plume Blender tambahan. Auto-find: Level5_SteamPlume_3D_Runtime / Level5_SteamPlume_3D.")]
    [SerializeField] private GameObject _steamMeshVisual;
    [SerializeField] private float _steamMeshScaleMin = 0.18f;
    [SerializeField] private float _steamMeshScaleMax = 1.15f;

    [Header("=== Audio Steam ===")]
    [SerializeField] private AudioSource _steamAudio;
    [Range(0f, 1f)] [SerializeField] private float _steamVolumeMax = 0.7f;
    [SerializeField] private float _steamPitchMin = 0.6f;
    [SerializeField] private float _steamPitchMax = 1.3f;

    [Header("=== Temperature Gauge (Visual) ===")]
    [SerializeField] private Transform _gaugeNeedle;
    [SerializeField] private float _gaugeAngleMin = 45f;
    [SerializeField] private float _gaugeAngleMax = -135f;
    [Tooltip("Sumbu lokal jarum gauge. Default Z untuk needle mesh/cylinder tipis di panel gauge.")]
    [SerializeField] private Vector3 _gaugeNeedleAxisLocal = Vector3.forward;
    [Tooltip("True = jarum gauge berputar searah jarum jam relatif rotasi awal scene.")]
    [SerializeField] private bool _gaugePutarSearahJarumJam = true;

    [Header("=== Teleport & Spawn ===")]
    [Tooltip("Spawn point di field Pre-Heater. Dipakai saat Level 5 mulai.")]
    [SerializeField] private Transform _teleportTargetField;
    [Tooltip("Spawn point di DCS. Dipakai saat Level 5 selesai (transisi ke Level 6).")]
    [SerializeField] private Transform _teleportTargetDcs;
    [SerializeField] private float _durasiFade = 2.5f;
    [SerializeField] private float _durasiFadeKeField = 4.0f;
    [SerializeField] private float _jedaSetelahLaporan = 2f;

    [Header("=== Arrow Indicator ===")]
    [SerializeField] private DirectionArrowIndicator _arrowIndicator;
    [SerializeField] private bool _gunakanArrowIndicator = true;

    [Header("=== Validasi APD Lapangan ===")]
    [Tooltip("Cek APD lapangan (kacamata + respirator) sebelum lanjut. Kalau false, langsung skip validasi.")]
    [SerializeField] private bool _validasiApdLapangan = false;

    [Header("=== HUD ===")]
    [TextArea(2, 4)] [SerializeField] private string _pesanMulai =
        "Grab handwheel steam valve dengan grip controller (auto-buka), atau tekan R (buka) / F (tutup) di keyboard.";
    [TextArea(2, 4)] [SerializeField] private string _pesanSuhuTercapai =
        "Suhu Pre-Heater mencapai target! Tahan T dan lapor: 'katup steam terbuka'.";
    [TextArea(2, 4)] [SerializeField] private string _pesanValvePenuh =
        "Katup sudah terbuka penuh. Suhu pre-heater 200°C.";

    // Runtime state
    private float _rotasiAkumulasi;
    private float _suhuSaatIni;
    private float _valveOpenPercent;
    private bool _questTercapai;
    private bool _sedangDiGrab;
    private bool _valveHover;

    private bool _valvePenuhSudahDinotif;
    private bool _fieldSudahDibuka;
    private bool _fieldApdHintShown;
    private PlayerHUD _hud;
    private Transform _interactorAttach;
    private float _yawTanganLastFrame;
    private bool _yawTanganValid;
    private bool _listenerTerpasang;
    private UnityEngine.XR.Interaction.Toolkit.Interactables.XRSimpleInteractable _valveSimple;
    private GesturalHandwheel _valveGH;


    private Quaternion _valveWheelBaseLocalRotation = Quaternion.identity;
    private Quaternion _gaugeNeedleBaseLocalRotation = Quaternion.identity;
    private Transform _capturedValveWheel;
    private Transform _capturedGaugeNeedle;
    private HandwheelVirtualPivot _handwheelVirtualPivot;
    private Vector3 _steamMeshBaseLocalScale = Vector3.one;
    private bool _steamMeshBaseScaleCaptured;
    private Coroutine _teleportFieldCoroutine;
    private Material _steamTransparentRuntimeMaterial;
    private Material _steamParticleRuntimeMaterial;
    private Texture2D _steamParticleRuntimeTexture;

    private void Awake()
    {
        _hud = FindFirstObjectByType<PlayerHUD>();
        AutoFindReferences();
        CaptureInitialVisualRotations();
        EnsureSteamAudio();
        WireXRGrabListeners();
        _suhuSaatIni = _suhuAwal;
    }

    private void OnEnable()
    {
        GameLevelManager.OnLevelStarted += OnLevelStarted;
        GameLevelManager.OnVoiceReportAccepted += OnVoiceReportAccepted;
    }

    private void OnDisable()
    {
        GameLevelManager.OnLevelStarted -= OnLevelStarted;
        GameLevelManager.OnVoiceReportAccepted -= OnVoiceReportAccepted;
        // Cleanup FX & audio saat level pindah / scene unload.
        if (_steamParticle != null && _steamParticle.isPlaying) _steamParticle.Stop(true);
        if (_steamAudio != null && _steamAudio.isPlaying) _steamAudio.Stop();
        if (_teleportFieldCoroutine != null)
        {
            StopCoroutine(_teleportFieldCoroutine);
            _teleportFieldCoroutine = null;
        }
        UnwireXRGrabListeners();
    }

    private void OnLevelStarted(GameLevelManager.GameLevel level)
    {
        if (level == GameLevelManager.GameLevel.Level5_SteamValve)
        {
            AutoFindReferences();
            CaptureInitialVisualRotations();
            WireXRGrabListeners();
            ResetLevelState();
            // NOTE: Jangan teleport langsung. Player tetap di DCS sampai laporan AWAL diterima.
            // Teleport di-trigger dari OnVoiceReportAccepted.
            if (_hud != null) _hud.ShowNotifPublic(_pesanMulai);
            StopSteamFxPaksa();
        }
        else
        {
            // Level lain aktif → matikan FX/audio Level 5 supaya tidak bocor.
            _valveOpenPercent = 0f;
            UpdateVisuals();
            StopSteamFxPaksa();
            HideArrow();
        }
    }

    /// <summary>
    /// Trigger saat GameLevelManager menerima voice report.
    /// Untuk Level 5: laporan AWAL ("aktifkan pre-heater") = teleport ke field & tampilkan arrow valve.
    /// Laporan AKHIR ("katup steam terbuka") di-handle oleh GameLevelManager untuk transisi level.
    /// </summary>
    private void OnVoiceReportAccepted(string keyword)
    {
        if (GameLevelManager.Instance == null) return;
        if (GameLevelManager.Instance.CurrentLevel != GameLevelManager.GameLevel.Level5_SteamValve) return;

        // Teleport ke field HANYA SEKALI: kalau quest belum tercapai DAN belum pernah teleport.
        // Tanpa guard ini, laporan kedua (HT akhir) akan trigger teleport ulang → looping.
        if (!_questTercapai && !_fieldSudahDibuka)
        {
            MulaiTeleportFieldDenganFade();
        }
    }

    private void ResetLevelState()
    {
        _rotasiAkumulasi = 0f;
        _suhuSaatIni = _suhuAwal;
        _valveOpenPercent = 0f;
        _questTercapai = false;
        _valvePenuhSudahDinotif = false;
        _fieldSudahDibuka = false;
        _fieldApdHintShown = false;
        _sedangDiGrab = false;
        UpdateVisuals();
        StopSteamFxPaksa();
    }

    // ============================================================
    //  UPDATE LOOP
    // ============================================================

    private void Update()
    {
        if (GameLevelManager.Instance == null) return;
        if (GameLevelManager.Instance.CurrentLevel != GameLevelManager.GameLevel.Level5_SteamValve) return;
        if (!_fieldSudahDibuka)
        {
            StopSteamFxPaksa();
            return;
        }

        // Validasi APD lapangan: HANYA tampilkan hint, tidak block valve.
        // Player tetap bisa putar valve walau APD belum lengkap (warning saja).
        if (_validasiApdLapangan && PhaseManager.Instance != null && !PhaseManager.Instance.Level5FieldApdLengkap)
        {
            if (_hud != null && !_fieldApdHintShown)
            {
                var kurang = new System.Collections.Generic.List<string>();
                if (!PhaseManager.Instance.isGlovesWorn) kurang.Add("Sarung Tangan Kimia");
                if (!PhaseManager.Instance.isWalkieTalkieTaken) kurang.Add("Walkie Talkie / HT");
                if (!PhaseManager.Instance.isGlassesWorn) kurang.Add("Kacamata Pelindung");
                if (!PhaseManager.Instance.isRespiratorWorn) kurang.Add("Respirator / Masker Gas");
                string msg = kurang.Count > 0
                    ? "Pakai APD lapangan dulu: " + string.Join(", ", kurang)
                    : "Pakai APD lapangan dulu sebelum operasi katup steam.";
                _hud.ShowNotifPublic(msg);
                _fieldApdHintShown = true;
            }
            // Jangan return, biarkan valve tetap bisa diputar (hint saja).
        }

        bool berubah = false;
        if (_valveGH != null)
            { _rotasiAkumulasi = _valveGH.OpenPercent01 * _totalDerajatFullOpen; berubah = true; }

        if (!berubah)
            berubah = SimulateValveInputKeyboard();

        // Fallback: kalau valve di-grab tapi yaw tangan gak valid, auto-open perlahan
        // supaya player gak stuck. 4 detik untuk full open (1440° / 360 deg/s).
        if (false) // mekanisme lama auto-open dihapus: sekarang murni gestural seperti FV1
        {
            _rotasiAkumulasi = Mathf.Clamp(_rotasiAkumulasi + 360f * Time.deltaTime, 0f, _totalDerajatFullOpen);
            berubah = true;
        }

        if (berubah)
        {
            UpdateValveState();
            UpdateVisuals();
            CheckQuestCompletion();
        }
    }

    /// <summary>
    /// Track rotasi tangan player (atau interactor attach transform) saat valve di-grab.
    /// Pakai delta yaw di sumbu valve lokal supaya kalau player putar tangan searah jarum jam,
    /// valve juga ikut berputar searah.
    /// </summary>
    private bool TrackVRRotation()
    {
        if (_valveWheel == null) return false;
        Vector3 axisWorld = _valveWheel.parent != null
            ? _valveWheel.parent.TransformDirection(_sumbuRotasiValveLocal).normalized
            : _valveWheel.TransformDirection(_sumbuRotasiValveLocal).normalized;

        // BARU: ikut TWIST tangan pemain (controller.up diproyeksikan ke bidang disc), bukan .forward.
        Vector3 handVec = _interactorAttach.up;
        Vector3 projForward = Vector3.ProjectOnPlane(handVec, axisWorld);
        if (projForward.sqrMagnitude < 0.01f) { handVec = _interactorAttach.right; projForward = Vector3.ProjectOnPlane(handVec, axisWorld); }
        if (projForward.sqrMagnitude < 0.0001f) return false;
        projForward.Normalize();

        Vector3 refForward = Vector3.ProjectOnPlane(Vector3.up, axisWorld);
        if (refForward.sqrMagnitude < 0.0001f) refForward = Vector3.ProjectOnPlane(Vector3.right, axisWorld);
        refForward.Normalize();

        float yawSekarang = Vector3.SignedAngle(refForward, projForward, axisWorld);
        if (!_yawTanganValid)
        {
            _yawTanganLastFrame = yawSekarang;
            _yawTanganValid = true;
            return false;
        }

        float delta = Mathf.DeltaAngle(_yawTanganLastFrame, yawSekarang);
        _yawTanganLastFrame = yawSekarang;
        if (Mathf.Abs(delta) > 35f) return false;   // buang lompatan teleport tangan

        // gesturalGain: gerakan kecil tangan -> putaran besar (seperti FV1).
        float deltaValve = -delta * Mathf.Max(1f, _skalaResponsRotasiVR * 3f);
        if (_balikkanArahRotasiVR) deltaValve = -deltaValve;
        if (Mathf.Abs(deltaValve) < 0.001f) return false;

        _rotasiAkumulasi = Mathf.Clamp(_rotasiAkumulasi + deltaValve, 0f, _totalDerajatFullOpen);
        return true;
    }

    /// <summary>
    /// Keyboard fallback untuk testing tanpa headset:
    /// - Tahan R: putar valve CW (buka)
    /// - Tahan F: putar valve CCW (tutup)
    /// </summary>
    private bool SimulateValveInputKeyboard()
    {
#if ENABLE_INPUT_SYSTEM
        var kb = UnityEngine.InputSystem.Keyboard.current;
        if (kb == null) return false;
        float delta = 0f;
        if (kb.rKey.isPressed) delta += _kecepatanRotasiKeyboardSimulasi * Time.deltaTime;
        if (kb.fKey.isPressed) delta -= _kecepatanRotasiKeyboardSimulasi * Time.deltaTime;
        if (Mathf.Abs(delta) < 0.001f) return false;
        _rotasiAkumulasi = Mathf.Clamp(_rotasiAkumulasi + delta, 0f, _totalDerajatFullOpen);
        return true;
#else
        if (Input.GetKey(KeyCode.R))
        {
            _rotasiAkumulasi += _kecepatanRotasiKeyboardSimulasi * Time.deltaTime;
            _rotasiAkumulasi = Mathf.Clamp(_rotasiAkumulasi, 0f, _totalDerajatFullOpen);
            return true;
        }
        if (Input.GetKey(KeyCode.F))
        {
            _rotasiAkumulasi -= _kecepatanRotasiKeyboardSimulasi * Time.deltaTime;
            _rotasiAkumulasi = Mathf.Clamp(_rotasiAkumulasi, 0f, _totalDerajatFullOpen);
            return true;
        }
        return false;
#endif
    }

    private void UpdateValveState()
    {
        _valveOpenPercent = Mathf.Clamp01(_rotasiAkumulasi / _totalDerajatFullOpen);
        _suhuSaatIni = Mathf.Lerp(_suhuAwal, _suhuTarget, _valveOpenPercent);

        if (GameLevelManager.Instance != null)
            GameLevelManager.Instance.SetSuhu(_suhuSaatIni);
    }

    private void UpdateVisuals()
    {
        if (_valveWheel == null || _gaugeNeedle == null)
        {
            AutoFindReferences();
            CaptureInitialVisualRotations();
            WireXRGrabListeners();
        }

        if (_valveWheel != null)
        {
            // putaran wheel ditangani GesturalHandwheel (persis Level 8)
            ; // (rotasi wheel via GesturalHandwheel)
        }

        if (_steamParticle != null)
        {
            ConfigureSteamParticleRenderer();
            var emission = _steamParticle.emission;
            bool bolehSteam = _fieldSudahDibuka && _valveOpenPercent > 0.01f;
            emission.rateOverTime = bolehSteam ? _steamEmisiMax * _valveOpenPercent : 0f;
            if (bolehSteam)
            {
                if (!_steamParticle.gameObject.activeSelf)
                    _steamParticle.gameObject.SetActive(true);
                if (!_steamParticle.isPlaying)
                    _steamParticle.Play(true);
            }
            else
            {
                if (_steamParticle.isPlaying)
                    _steamParticle.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
                if (_steamParticle.gameObject.activeSelf)
                    _steamParticle.gameObject.SetActive(false);
            }
        }

        UpdateSteamMeshVisual();

        if (_steamAudio != null)
        {
            _steamAudio.volume = _steamVolumeMax * _valveOpenPercent;
            _steamAudio.pitch = Mathf.Lerp(_steamPitchMin, _steamPitchMax, _valveOpenPercent);
            bool bolehSteamAudio = _fieldSudahDibuka && _valveOpenPercent > 0.01f;
            if (bolehSteamAudio && !_steamAudio.isPlaying)
                _steamAudio.Play();
            else if (!bolehSteamAudio && _steamAudio.isPlaying)
                _steamAudio.Stop();
            if (!bolehSteamAudio)
                _steamAudio.volume = 0f;
        }

        if (_gaugeNeedle != null)
        {
            float t = Mathf.InverseLerp(_suhuAwal, _suhuTarget, _suhuSaatIni);
            float angleRange = Mathf.Max(1f, Mathf.Abs(_gaugeAngleMax - _gaugeAngleMin));
            float angle = Mathf.Lerp(0f, angleRange, t);
            Vector3 gaugeAxis = SafeAxis(_gaugeNeedleAxisLocal, Vector3.forward);
            if (_gaugePutarSearahJarumJam)
                gaugeAxis = -gaugeAxis;
            _gaugeNeedle.localRotation = _gaugeNeedleBaseLocalRotation * Quaternion.AngleAxis(angle, gaugeAxis);
        }
    }

    private void CheckQuestCompletion()
    {
        if (!_questTercapai && _suhuSaatIni >= _suhuMinimumQuest)
        {
            _questTercapai = true;
            GameLevelManager.Instance?.NotifyLevel5PreheaterReady();
            Debug.Log($"[Level5] Suhu Pre-Heater {_suhuSaatIni:F0}°C. Quest tercapai.");
            if (_hud != null) _hud.ShowNotifPublic(_pesanSuhuTercapai);
            HideArrow();
        }

        if (!_valvePenuhSudahDinotif && _valveOpenPercent >= 0.999f)
        {
            _valvePenuhSudahDinotif = true;
            if (_hud != null) _hud.ShowNotifPublic(_pesanValvePenuh);
            PlayValveClickSound();
            SendHapticToGrabber();
        }
    }

    // ============================================================
    //  XR GRAB WIRING
    // ============================================================

    private void WireXRGrabListeners()
    {
        if (_listenerTerpasang || _valveWheel == null) return;

        // Putaran PERSIS seperti Level 8 Flash Vessel: GesturalHandwheel menangani interaksi
        // (XRSimpleInteractable hover+grab) + rotasi part mengelilingi pivot world.
        var oldGrab = _valveWheel.GetComponent<XRGrabInteractable>();
        if (oldGrab != null) Destroy(oldGrab);
        var oldRb = _valveWheel.GetComponent<Rigidbody>();
        if (oldRb != null) Destroy(oldRb);
        _valveGrab = null;

        _valveGH = _valveWheel.GetComponent<GesturalHandwheel>();
        if (_valveGH == null) _valveGH = _valveWheel.gameObject.AddComponent<GesturalHandwheel>();
        _valveGH.fullOpenDegrees = _totalDerajatFullOpen;
        _valveGH.Setup(_valveWheel, null);
        _listenerTerpasang = true;
    }

    private void UnwireXRGrabListeners()
    {
        if (!_listenerTerpasang || _valveGrab == null) return;
        _valveGrab.selectEntered.RemoveListener(OnValveSelectEntered);
        _valveGrab.selectExited.RemoveListener(OnValveSelectExited);
        _listenerTerpasang = false;
    }

    private void OnValveSelectEntered(UnityEngine.XR.Interaction.Toolkit.SelectEnterEventArgs args)
    {
        _sedangDiGrab = true;
        _interactorAttach = args.interactorObject != null
            ? args.interactorObject.transform
            : null;
        _yawTanganValid = false; // baseline akan diambil di frame Update berikutnya
    }

    private void OnValveSelectExited(UnityEngine.XR.Interaction.Toolkit.SelectExitEventArgs args)
    {
        _sedangDiGrab = false;
        _interactorAttach = null;
        _yawTanganValid = false;
    }

    // ============================================================
    //  TELEPORT & ARROW
    // ============================================================

    private void TeleportPlayerKeField()
    {
        if (_teleportTargetField == null)
        {
            Debug.LogWarning("[Level5] _teleportTargetField belum di-assign. Player tidak diteleport ke field.");
            return;
        }
        if (_playerRigRoot == null)
        {
            var rig = GameObject.Find("XR Origin (XR Rig)") ?? GameObject.FindWithTag("Player");
            if (rig != null) _playerRigRoot = rig.transform;
        }
        if (_playerRigRoot == null) return;

        var origin = _playerRigRoot.GetComponent<XROrigin>();
        if (origin != null)
        {
            // Pattern XR Rig: pakai MoveCameraToWorldLocation + MatchOriginUpCameraForward
            origin.MoveCameraToWorldLocation(_teleportTargetField.position);
            origin.MatchOriginUpCameraForward(Vector3.up, _teleportTargetField.forward);
        }
        else
        {
            // Non-XR fallback
            _playerRigRoot.SetPositionAndRotation(_teleportTargetField.position, _teleportTargetField.rotation);
        }
    }

    private void MulaiTeleportFieldDenganFade()
    {
        if (_teleportFieldCoroutine != null)
            StopCoroutine(_teleportFieldCoroutine);

        _teleportFieldCoroutine = StartCoroutine(TeleportFieldDenganFadeCoroutine());
    }

    private IEnumerator TeleportFieldDenganFadeCoroutine()
    {
        float durasi = Mathf.Max(2.8f, _durasiFadeKeField);
        if (_hud != null)
            _hud.PlayManualFade(durasi);

        yield return new WaitForSeconds(durasi * 0.50f);
        TeleportPlayerKeField();
        XRInteractorRecovery.PulihkanRayInteractor();
        _fieldSudahDibuka = true;
        TampilkanArrowKeValve();

        yield return new WaitForSeconds(durasi * 0.50f);
        XRInteractorRecovery.PulihkanRayInteractor();
        _teleportFieldCoroutine = null;
    }

    private void TampilkanArrowKeValve()
    {
        if (!_gunakanArrowIndicator || _arrowIndicator == null || _valveWheel == null) return;
        _arrowIndicator.Show(_valveWheel);
    }

    private void HideArrow()
    {
        if (_arrowIndicator != null)
            _arrowIndicator.Hide();
    }

    private void StopSteamFxPaksa()
    {
        if (_steamParticle != null)
        {
            var emission = _steamParticle.emission;
            emission.rateOverTime = 0f;
            if (_steamParticle.isPlaying)
                _steamParticle.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            if (_steamParticle.gameObject.activeSelf)
                _steamParticle.gameObject.SetActive(false);
        }

        if (_steamAudio != null)
        {
            _steamAudio.volume = 0f;
            if (_steamAudio.isPlaying)
                _steamAudio.Stop();
        }

        if (_steamMeshVisual != null && _steamMeshVisual.activeSelf)
            _steamMeshVisual.SetActive(false);
    }

    private void UpdateSteamMeshVisual()
    {
        if (_steamMeshVisual == null && _autoFindSteamFx)
            AutoFindSteamMeshVisual();

        if (_steamMeshVisual == null)
            return;

        if (!_steamMeshBaseScaleCaptured)
        {
            _steamMeshBaseLocalScale = _steamMeshVisual.transform.localScale;
            if (_steamMeshBaseLocalScale.sqrMagnitude < 0.0001f)
                _steamMeshBaseLocalScale = Vector3.one;
            _steamMeshBaseScaleCaptured = true;
        }

        bool bolehSteam = _fieldSudahDibuka && _valveOpenPercent > 0.01f;
        if (!_steamMeshVisual.activeSelf && bolehSteam)
            _steamMeshVisual.SetActive(true);

        float scaleT = Mathf.Lerp(_steamMeshScaleMin, _steamMeshScaleMax, Mathf.Clamp01(_valveOpenPercent));
        float pulse = 1f + Mathf.Sin(Time.time * 2.8f) * 0.035f;
        _steamMeshVisual.transform.localScale = _steamMeshBaseLocalScale * scaleT * pulse;
        _steamMeshVisual.transform.Rotate(Vector3.up, Time.deltaTime * Mathf.Lerp(7f, 22f, _valveOpenPercent), Space.World);

        Renderer[] renderers = _steamMeshVisual.GetComponentsInChildren<Renderer>(true);
        for (int i = 0; i < renderers.Length; i++)
        {
            Renderer renderer = renderers[i];
            if (renderer == null)
                continue;

            renderer.enabled = bolehSteam;
            renderer.sharedMaterial = GetSteamTransparentRuntimeMaterial();
        }

        if (!bolehSteam && _steamMeshVisual.activeSelf)
            _steamMeshVisual.SetActive(false);
    }

    private Material GetSteamTransparentRuntimeMaterial()
    {
        if (_steamTransparentRuntimeMaterial != null)
            return _steamTransparentRuntimeMaterial;

        Shader shader = Shader.Find("Universal Render Pipeline/Lit");
        if (shader == null)
            shader = Shader.Find("Standard");

        _steamTransparentRuntimeMaterial = new Material(shader);
        _steamTransparentRuntimeMaterial.name = "M_Level5_Steam_Transparent_Runtime";
        Color color = new Color(0.92f, 0.96f, 1f, 0.22f);
        _steamTransparentRuntimeMaterial.color = color;
        if (_steamTransparentRuntimeMaterial.HasProperty("_BaseColor"))
            _steamTransparentRuntimeMaterial.SetColor("_BaseColor", color);
        if (_steamTransparentRuntimeMaterial.HasProperty("_Color"))
            _steamTransparentRuntimeMaterial.SetColor("_Color", color);
        if (_steamTransparentRuntimeMaterial.HasProperty("_Surface"))
            _steamTransparentRuntimeMaterial.SetFloat("_Surface", 1f);
        if (_steamTransparentRuntimeMaterial.HasProperty("_Blend"))
            _steamTransparentRuntimeMaterial.SetFloat("_Blend", 0f);
        if (_steamTransparentRuntimeMaterial.HasProperty("_SrcBlend"))
            _steamTransparentRuntimeMaterial.SetFloat("_SrcBlend", (float)UnityEngine.Rendering.BlendMode.SrcAlpha);
        if (_steamTransparentRuntimeMaterial.HasProperty("_DstBlend"))
            _steamTransparentRuntimeMaterial.SetFloat("_DstBlend", (float)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
        if (_steamTransparentRuntimeMaterial.HasProperty("_ZWrite"))
            _steamTransparentRuntimeMaterial.SetFloat("_ZWrite", 0f);
        _steamTransparentRuntimeMaterial.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
        _steamTransparentRuntimeMaterial.EnableKeyword("_ALPHABLEND_ON");
        _steamTransparentRuntimeMaterial.renderQueue = 3000;
        return _steamTransparentRuntimeMaterial;
    }

    private void ConfigureSteamParticleRenderer()
    {
        if (_steamParticle == null)
            return;

        ParticleSystemRenderer renderer = _steamParticle.GetComponent<ParticleSystemRenderer>();
        if (renderer != null)
            renderer.sharedMaterial = GetSteamParticleRuntimeMaterial();

        var main = _steamParticle.main;
        main.startColor = new ParticleSystem.MinMaxGradient(new Color(0.9f, 0.95f, 1f, 0.18f));
        main.startSize = new ParticleSystem.MinMaxCurve(0.42f, 1.15f);
        main.startLifetime = new ParticleSystem.MinMaxCurve(1.1f, 2.4f);

        var noise = _steamParticle.noise;
        noise.enabled = true;
        noise.strength = 0.42f;
        noise.frequency = 0.65f;
        noise.scrollSpeed = 0.22f;
    }

    private Material GetSteamParticleRuntimeMaterial()
    {
        if (_steamParticleRuntimeMaterial != null)
            return _steamParticleRuntimeMaterial;

        Shader shader = Shader.Find("Universal Render Pipeline/Particles/Unlit");
        if (shader == null)
            shader = Shader.Find("Particles/Standard Unlit");
        if (shader == null)
            shader = Shader.Find("Sprites/Default");

        _steamParticleRuntimeMaterial = new Material(shader);
        _steamParticleRuntimeMaterial.name = "M_Level5_Steam_SoftParticle_Runtime";
        _steamParticleRuntimeTexture = BuatSteamRadialTexture();
        if (_steamParticleRuntimeMaterial.HasProperty("_BaseMap"))
            _steamParticleRuntimeMaterial.SetTexture("_BaseMap", _steamParticleRuntimeTexture);
        if (_steamParticleRuntimeMaterial.HasProperty("_MainTex"))
            _steamParticleRuntimeMaterial.SetTexture("_MainTex", _steamParticleRuntimeTexture);
        Color color = new Color(0.92f, 0.96f, 1f, 0.22f);
        if (_steamParticleRuntimeMaterial.HasProperty("_BaseColor"))
            _steamParticleRuntimeMaterial.SetColor("_BaseColor", color);
        if (_steamParticleRuntimeMaterial.HasProperty("_Color"))
            _steamParticleRuntimeMaterial.SetColor("_Color", color);
        _steamParticleRuntimeMaterial.renderQueue = 3000;
        return _steamParticleRuntimeMaterial;
    }

    private Texture2D BuatSteamRadialTexture()
    {
        const int size = 64;
        Texture2D tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
        tex.name = "T_Level5_Steam_SoftRadial_Runtime";
        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                float u = (x + 0.5f) / size * 2f - 1f;
                float v = (y + 0.5f) / size * 2f - 1f;
                float d = Mathf.Sqrt(u * u + v * v);
                float alpha = Mathf.Clamp01(1f - d);
                alpha = alpha * alpha * alpha;
                tex.SetPixel(x, y, new Color(1f, 1f, 1f, alpha));
            }
        }
        tex.Apply(false, true);
        return tex;
    }

    // ============================================================
    //  AUTO-FIND & SETUP
    // ============================================================

    private void CaptureInitialVisualRotations()
    {
        if (_valveWheel != null && _capturedValveWheel != _valveWheel)
        {
            _valveWheelBaseLocalRotation = _valveWheel.localRotation;
            _capturedValveWheel = _valveWheel;
        }

        if (_gaugeNeedle != null && _capturedGaugeNeedle != _gaugeNeedle)
        {
            _gaugeNeedleBaseLocalRotation = _gaugeNeedle.localRotation;
            _capturedGaugeNeedle = _gaugeNeedle;
        }
    }

    private Vector3 SafeAxis(Vector3 axis, Vector3 fallback)
    {
        return axis.sqrMagnitude > 0.0001f ? axis.normalized : fallback;
    }

    private void AutoFindReferences()
    {
        if (_playerRigRoot == null)
        {
            var rig = GameObject.Find("XR Origin (XR Rig)") ?? GameObject.FindWithTag("Player");
            if (rig != null) _playerRigRoot = rig.transform;
        }

        if (_valveWheel == null)
        {
            _valveWheel = FindBestTransformNear(
                new[] { "RealSteamValve_Pivot_Lvl5", "SteamValve_Handwheel", "ValveWheel", "Handwheel" },
                null);
        }

        if (_valveGrab == null && _valveWheel != null)
        {
            _valveGrab = _valveWheel.GetComponent<XRGrabInteractable>()
                      ?? _valveWheel.GetComponentInChildren<XRGrabInteractable>(true)
                      ?? _valveWheel.GetComponentInParent<XRGrabInteractable>();
        }

        if (_handwheelVirtualPivot == null && _valveWheel != null)
            _handwheelVirtualPivot = _valveWheel.GetComponent<HandwheelVirtualPivot>();
        if (_handwheelVirtualPivot != null)
        {
            _sumbuRotasiValveLocal = _handwheelVirtualPivot.InferAxisLocalFromMeshBounds();
            _handwheelVirtualPivot.SetAxisLocal(SafeAxis(_sumbuRotasiValveLocal, Vector3.forward));
            _handwheelVirtualPivot.CenterPivotToMeshParts();
            _handwheelVirtualPivot.RecacheRestPose();
        }

        if (_gaugeNeedle == null)
        {
            _gaugeNeedle = FindBestTransformNear(
                new[] { "Gauge_Needle", "TemperatureGauge_Needle", "TempGauge_Needle", "PressureGauge_Needle" },
                _valveWheel);
        }

        if (_gaugeNeedle != null)
            _gaugeNeedleAxisLocal = InferGaugeNeedleAxisLocal(_gaugeNeedle);

        if (_steamParticle == null && _autoFindSteamFx)
        {
            var go = GameObject.Find("Mesin Utama/PreHeater_Field_1/Steam_FX")
                  ?? GameObject.Find("Steam_FX_Level5");
            if (go != null) _steamParticle = go.GetComponent<ParticleSystem>()
                                          ?? go.GetComponentInChildren<ParticleSystem>();
        }

        if (_steamMeshVisual == null && _autoFindSteamFx)
            AutoFindSteamMeshVisual();

        if (_teleportTargetDcs == null)
        {
            var go = GameObject.Find("SpawnPoint_DCS");
            if (go != null) _teleportTargetDcs = go.transform;
        }

        if (_teleportTargetField == null)
        {
            var go = GameObject.Find("SpawnPoint_Lvl5_PreHeater")
                  ?? GameObject.Find("SpawnPoint_Lvl4_Preheater");
            if (go != null) _teleportTargetField = go.transform;
        }
    }

    private void AutoFindSteamMeshVisual()
    {
        GameObject go = GameObject.Find("Level5_SteamPlume_3D_Runtime") ??
                        GameObject.Find("Level5_SteamPlume_3D") ??
                        GameObject.Find("Level5_SteamPlume_3D(Clone)");
        if (go == null)
            return;

        _steamMeshVisual = go;
        _steamMeshBaseLocalScale = go.transform.localScale.sqrMagnitude < 0.0001f ? Vector3.one : go.transform.localScale;
        _steamMeshBaseScaleCaptured = true;
        if (go.activeSelf)
            go.SetActive(false);
    }

    private Transform FindBestTransformNear(string[] names, Transform near)
    {
        Transform best = null;
        float bestScore = float.MaxValue;
        var all = UnityEngine.Object.FindObjectsByType<Transform>(FindObjectsInactive.Include, FindObjectsSortMode.None);

        foreach (var t in all)
        {
            if (t == null) continue;

            float score = 0f;
            bool match = false;
            for (int i = 0; i < names.Length; i++)
            {
                string targetName = names[i];
                if (string.Equals(t.name, targetName, System.StringComparison.OrdinalIgnoreCase))
                {
                    score -= 1000f;
                    match = true;
                    break;
                }

                if (t.name.IndexOf(targetName, System.StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    score -= 500f;
                    match = true;
                    break;
                }
            }

            if (!match) continue;

            if (TransformPathContains(t, "Level5")) score -= 220f;
            if (TransformPathContains(t, "PreHeater") || TransformPathContains(t, "Preheater")) score -= 180f;
            if (near != null) score += Vector3.SqrMagnitude(t.position - near.position);

            if (score < bestScore)
            {
                bestScore = score;
                best = t;
            }
        }

        return best;
    }

    private Vector3 InferGaugeNeedleAxisLocal(Transform needle)
    {
        if (needle == null)
            return SafeAxis(_gaugeNeedleAxisLocal, Vector3.forward);

        Renderer referenceRenderer = FindNearestGaugeDialRenderer(needle) ?? needle.GetComponentInChildren<Renderer>(true);
        if (referenceRenderer == null)
            return SafeAxis(_gaugeNeedleAxisLocal, Vector3.forward);

        Vector3 size = referenceRenderer.bounds.size;
        Vector3 worldAxis = Vector3.forward;
        if (size.x <= size.y && size.x <= size.z)
            worldAxis = Vector3.right;
        else if (size.y <= size.x && size.y <= size.z)
            worldAxis = Vector3.up;

        Vector3 localAxis = needle.InverseTransformDirection(worldAxis);
        return localAxis.sqrMagnitude > 0.0001f ? localAxis.normalized : Vector3.forward;
    }

    private Renderer FindNearestGaugeDialRenderer(Transform needle)
    {
        Renderer best = null;
        float bestDist = float.MaxValue;
        var all = UnityEngine.Object.FindObjectsByType<Transform>(FindObjectsInactive.Include, FindObjectsSortMode.None);

        foreach (var t in all)
        {
            if (t == null) continue;
            if (t.name.IndexOf("Dial", System.StringComparison.OrdinalIgnoreCase) < 0 &&
                t.name.IndexOf("Gauge", System.StringComparison.OrdinalIgnoreCase) < 0)
                continue;

            if (t.name.IndexOf("Needle", System.StringComparison.OrdinalIgnoreCase) >= 0)
                continue;

            var renderer = t.GetComponentInChildren<Renderer>(true);
            if (renderer == null) continue;

            float dist = Vector3.SqrMagnitude(renderer.bounds.center - needle.position);
            if (dist < bestDist)
            {
                bestDist = dist;
                best = renderer;
            }
        }

        return best;
    }

    private bool TransformPathContains(Transform t, string text)
    {
        while (t != null)
        {
            if (t.name.IndexOf(text, System.StringComparison.OrdinalIgnoreCase) >= 0)
                return true;
            t = t.parent;
        }
        return false;
    }

    private void EnsureSteamAudio()
    {
        if (_steamAudio != null) return;

        var steamFx = GameObject.Find("Mesin Utama/PreHeater_Field_1/Steam_FX")
                   ?? GameObject.Find("Steam_FX_Level5");
        if (steamFx != null)
        {
            _steamAudio = steamFx.GetComponent<AudioSource>();
            if (_steamAudio == null) _steamAudio = steamFx.AddComponent<AudioSource>();
        }
        else
        {
            _steamAudio = gameObject.AddComponent<AudioSource>();
        }

        _steamAudio.spatialBlend = 0.5f;
        _steamAudio.maxDistance = 40f;
        _steamAudio.loop = true;
        _steamAudio.playOnAwake = false;
        _steamAudio.volume = 0f;
        _steamAudio.priority = 48;

        if (_steamAudio.clip == null)
            _steamAudio.clip = BuatClipSteamHiss(durasi: 4f, sampleRate: 22050);
    }

    private AudioClip BuatClipSteamHiss(float durasi, int sampleRate)
    {
        int total = Mathf.CeilToInt(durasi * sampleRate);
        float[] data = new float[total];
        System.Random rnd = new System.Random(77);
        float hpPrev = 0f;

        for (int i = 0; i < total; i++)
        {
            float noise = ((float)rnd.NextDouble() - 0.5f) * 2f;
            float hp = noise - hpPrev;
            hpPrev = noise * 0.92f;
            float t = (float)i / total;
            float mod = 1f + 0.3f * Mathf.Sin(t * Mathf.PI * 2f * 3f);
            data[i] = hp * 0.35f * mod;
        }

        int fadeLen = Mathf.Min(2000, total / 20);
        for (int i = 0; i < fadeLen; i++)
        {
            float fade = (float)i / fadeLen;
            data[i] *= fade;
            data[total - 1 - i] *= fade;
        }

        var clip = AudioClip.Create("ProcSteamHiss", total, 1, sampleRate, false);
        clip.SetData(data, 0);
        return clip;
    }

    private void PlayValveClickSound()
    {
        if (_steamAudio != null)
        {
            var clickClip = GenerateClickClip("ValveFullClick", 0.15f, 22050);
            _steamAudio.PlayOneShot(clickClip, 1f);
        }
    }

    private void SendHapticToGrabber()
    {
        if (_valveGrab == null || !_valveGrab.isSelected) return;
        foreach (var interactor in _valveGrab.interactorsSelecting)
        {
            var xrInteractor = interactor as UnityEngine.XR.Interaction.Toolkit.Interactors.XRBaseInputInteractor;
            if (xrInteractor != null)
            {
                xrInteractor.SendHapticImpulse(0.6f, 0.2f);
            }
        }
    }

    private AudioClip GenerateClickClip(string name, float duration, int sampleRate)
    {
        int total = Mathf.CeilToInt(duration * sampleRate);
        float[] data = new float[total];
        for (int i = 0; i < total; i++)
        {
            float t = (float)i / sampleRate;
            float env = Mathf.Exp(-t * 40f);
            float freq = 2200f + 800f * Mathf.Exp(-t * 20f);
            data[i] = Mathf.Sin(2f * Mathf.PI * freq * t) * env * 0.7f;
        }
        var clip = AudioClip.Create(name, total, 1, sampleRate, false);
        clip.SetData(data, 0);
        return clip;
    }

    // ============================================================
    //  PUBLIC API
    // ============================================================

    public float SuhuSaatIni => _suhuSaatIni;
    public float ValveOpenPercent => _valveOpenPercent;
    public bool QuestTercapai => _questTercapai;
}
