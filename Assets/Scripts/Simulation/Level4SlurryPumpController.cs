using System.Collections;
using UnityEngine;

/// <summary>
/// OLIVIA VR - Level4SlurryPumpController.cs (v6.0 - Liquid Fill + Pump Sound)
///
/// FLOW LEVEL 4:
///   1. AturFlowRate           — di DCS, player tekan + sampai 450 m3/h
///   2. MenungguLaporanFlow    — di DCS, player lapor HT "slurry pump aktif" → FADE & TELEPORT ke field
///   3. ObservasiPump          — player di field, lihat liquid mengisi pipa pelan-pelan
///   4. ObservasiPreheater     — liquid sampai pre-heater, highlight + bell
///   5. MenungguLaporanAkhir   — player lapor HT akhir → tunggu balasan
///   6. KembaliKeDcs           — fade balik ke DCS untuk Level 5
///   7. Selesai
/// </summary>
public class Level4SlurryPumpController : MonoBehaviour
{
    [Header("=== Referensi Pemain ===")]
    [SerializeField] private Transform _playerRigRoot;
    [SerializeField] private CharacterController _playerCharacterController;

    [Header("=== Titik Teleport ===")]
    [SerializeField] private Transform _teleportTargetPump;
    [SerializeField] private Transform _teleportTargetPreheater;
    [SerializeField] private Transform _teleportTargetDcs;

    [Header("=== Auto-Find Reference ===")]
    [SerializeField] private GameObject _pumpReference;
    [SerializeField] private GameObject _preheaterReference;
    [SerializeField] private Vector3 _offsetObservasiPump = new Vector3(-2.5f, 1.6f, 3.5f);
    [SerializeField] private Vector3 _offsetObservasiPreheater = new Vector3(0f, 1.6f, 4.5f);

    [Header("=== Visual Liquid Fill (di dalam pipa) ===")]
    [Tooltip("Pipa utama yang akan diisi liquid (Pipe_FromPump).")]
    [SerializeField] private Transform _pipaUtama;
    [Tooltip("Material liquid (slurry orange).")]
    [SerializeField] private Material _liquidMaterial;
    [Tooltip("Durasi liquid mengisi pipa (detik).")]
    [SerializeField] private float _durasiLiquidMengalir = 12f;
    [SerializeField] private AnimationCurve _kurvaLiquid = AnimationCurve.EaseInOut(0f, 0f, 1f, 1f);
    [Tooltip("Diameter liquid relatif terhadap pipa (0.85 = sedikit lebih kecil supaya muat).")]
    [Range(0.5f, 0.99f)] [SerializeField] private float _diameterRelatif = 0.88f;
    [SerializeField] private bool _autoFindPipa = true;

    [Header("=== Process Pipe Network ===")]
    [SerializeField] private ProcessPipeNetwork _pipeNetwork;
    [SerializeField] private string[] _level4FlowRouteIds =
    {
        "Tank_To_Pump",
        "Pump_Internal_Logic",
        "Pump_To_Preheater",
        "Legacy_Level4_Local"
    };

    [Header("=== Slurry Tank Drain ===")]
    [Tooltip("Slurry_Fill di Slurry Tank yang akan berkurang sambil pipa terisi.")]
    [SerializeField] private Transform _slurryTankFill;
    [Tooltip("Saat Level 4 mulai, paksa slurry tank PENUH (basisnya 50% dari Level 3).")]
    [SerializeField] private bool _isiPenuhSaatLevel4Mulai = true;
    [Tooltip("LocalScale Y saat tank PENUH 100%.")]
    [SerializeField] private float _slurryTankScaleYPenuh = 1.84f;
    [Tooltip("LocalPos Y saat tank PENUH.")]
    [SerializeField] private float _slurryTankPosYPenuh = 0f;
    [Tooltip("LocalScale Y saat tank di akhir (sisa setelah ngalir ke preheater).")]
    [SerializeField] private float _slurryTankScaleYAkhir = 0.5f;
    [Tooltip("LocalPos Y saat tank di akhir.")]
    [SerializeField] private float _slurryTankPosYAkhir = -1.4f;

    [Header("=== Audio Pump (saat menyala) ===")]
    [Tooltip("Posisi pump untuk spatial audio. Auto-find dari _pumpReference jika kosong.")]
    [SerializeField] private Transform _pumpAudioPosition;
    [Tooltip("Audio clip motor pump (looping). Auto-generated kalau kosong.")]
    [SerializeField] private AudioClip _pumpMotorClip;
    [Range(0f, 1f)] [SerializeField] private float _pumpMotorVolumeTarget = 0.55f;
    [Tooltip("Durasi fade-in volume pump motor saat menyala.")]
    [SerializeField] private float _pumpFadeInDurasi = 1.5f;

    [Header("=== Timing ===")]
    [SerializeField] private float _jedaSetelahLaporanAwal = 1.5f;
    [SerializeField] private float _durasiFade = 2.5f;
    [SerializeField] private float _jedaDiPump = 1.5f;
    [SerializeField] private float _jedaSetelahLiquidSampai = 1.5f;
    [SerializeField] private float _durasiAudioBalasan = 3f;
    [SerializeField] private float _jedaSetelahKembaliDcs = 1.0f;

    [Header("=== HUD Pesan ===")]
    [TextArea(2, 4)] [SerializeField] private string _pesanObservasiPump =
        "Slurry mulai mengalir! Lihat cairan mengisi pipa menuju Pre-Heater.";
    [TextArea(2, 4)] [SerializeField] private string _pesanLiquidSampai =
        "Slurry telah mencapai Pre-Heater! Tahan T dan kirim laporan HT akhir.";
    [TextArea(2, 4)] [SerializeField] private string _pesanKembaliDcs =
        "Laporan diterima. Kembali ke DCS untuk Level 5: Autoclave.";
    [TextArea(2, 4)] [SerializeField] private string _pesanLaporanAwalDiterima =
        "Roger. Field, lapor HT awal diterima. Memantau aliran...";

    [Header("=== Audio Notification ===")]
    [SerializeField] private AudioClip _bellEnterField;
    [SerializeField] private AudioClip _bellLiquidSampai;
    [SerializeField] private AudioClip _bellKembaliDcs;
    [SerializeField] private AudioClip _audioBalasanNPC;
    [Range(0f, 1f)] [SerializeField] private float _volumeBell = 0.7f;

    [Header("=== Visual Highlight ===")]
    [SerializeField] private Renderer[] _pumpHighlightRenderers;
    [SerializeField] private Renderer[] _preheaterHighlightRenderers;
    [SerializeField] private Color _warnaHighlight = new Color(0.2f, 1f, 0.5f, 1f);
    [SerializeField] private float _intensitasHighlight = 1.6f;

    [Tooltip("Auto-find Slurry_Fill di Slurry Tank kalau kosong.")]
    [SerializeField] private bool _autoFindSlurryTank = true;

    private Vector3 _slurryTankScaleAwal;
    private Vector3 _slurryTankPosAwal;
    private bool _slurryTankCached;

    private PlayerHUD _hud;
    private Coroutine _seqCoroutine;
    private Coroutine _pumpFadeCoroutine;
    private MaterialPropertyBlock _mpb;
    private bool _highlightPumpAktif;
    private bool _highlightPreheaterAktif;
    private ProcessPipeFlowAnimator _slurryToPreheaterFlow;
    private AudioSource _audioSource;
    private AudioSource _pumpAudioSource;
    private GameObject _liquidFillRuntime;

    private void Awake()
    {
        _hud = FindObjectOfType<PlayerHUD>();
        AutoFindPlayerRig();
        AutoFindReferences();
        _mpb = new MaterialPropertyBlock();

        _audioSource = GetComponent<AudioSource>();
        if (_audioSource == null) _audioSource = gameObject.AddComponent<AudioSource>();
        _audioSource.spatialBlend = 0f;
        _audioSource.playOnAwake = false;
        _audioSource.volume = _volumeBell;

        if (_bellEnterField == null) _bellEnterField = BuatClipBell(440f, 880f, 0.45f, 22050);
        if (_bellLiquidSampai == null) _bellLiquidSampai = BuatClipBell(523f, 784f, 0.7f, 22050);
        if (_bellKembaliDcs == null) _bellKembaliDcs = BuatClipBell(660f, 990f, 0.5f, 22050);

        EnsurePumpAudio();
        EnsureLiquidFill();
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
        if (_seqCoroutine != null) { StopCoroutine(_seqCoroutine); _seqCoroutine = null; }
        SetLevel4PipeFlow(false);
        StopPumpSound();
    }

    private void OnLevelStarted(GameLevelManager.GameLevel level)
    {
        if (_seqCoroutine != null) { StopCoroutine(_seqCoroutine); _seqCoroutine = null; }
        SetHighlight(_pumpHighlightRenderers, false, ref _highlightPumpAktif);
        SetHighlight(_preheaterHighlightRenderers, false, ref _highlightPreheaterAktif);
        HideLiquid();
        SetLevel4PipeFlow(false);
        StopPumpSound();

        // Saat Level 4 mulai → paksa slurry tank ke kondisi PENUH 100%
        if (level == GameLevelManager.GameLevel.Level4_SlurryPump && _isiPenuhSaatLevel4Mulai)
        {
            EnsureSlurryTankFill();
            if (_slurryTankFill != null)
            {
                Vector3 scale = _slurryTankFill.localScale;
                scale.y = _slurryTankScaleYPenuh;
                _slurryTankFill.localScale = scale;
                Vector3 pos = _slurryTankFill.localPosition;
                pos.y = _slurryTankPosYPenuh;
                _slurryTankFill.localPosition = pos;
                Debug.Log($"[Level4Controller] Slurry tank di-set PENUH: scale.y={_slurryTankScaleYPenuh}, pos.y={_slurryTankPosYPenuh}");
            }
        }
    }

    private void EnsureSlurryTankFill()
    {
        if (_slurryTankFill != null) return;
        var go = GameObject.Find("Mesin Utama/Slurry Tank/Slurry_Fill");
        if (go != null) _slurryTankFill = go.transform;
    }

    private void OnLevel4PhaseChanged(GameLevelManager.Level4Phase phase)
    {
        if (GameLevelManager.Instance == null ||
            GameLevelManager.Instance.CurrentLevel != GameLevelManager.GameLevel.Level4_SlurryPump)
            return;

        switch (phase)
        {
            case GameLevelManager.Level4Phase.MenungguLaporanFlow:
                if (_hud != null)
                    _hud.ShowNotifPublic("Flow rate 450 m³/h tercapai. Tahan T dan lapor: 'slurry pump aktif'.");
                // Mulai pump motor sound (player baru hidupkan pump dengan flow rate tepat)
                SetLevel4PipeFlow(true);
                StartPumpSound();
                break;

            case GameLevelManager.Level4Phase.ObservasiPump:
                SetLevel4PipeFlow(true);
                StartSequence(SeqTeleportKeFieldDanFillPipa());
                break;

            case GameLevelManager.Level4Phase.MenungguLaporanAkhir:
                if (_hud != null) _hud.ShowNotifPublic(_pesanLiquidSampai);
                PlayBell(_bellLiquidSampai);
                break;

            case GameLevelManager.Level4Phase.KembaliKeDcs:
                StartSequence(SeqKembaliKeDcs());
                break;
        }
    }

    private void StartSequence(IEnumerator seq)
    {
        if (_seqCoroutine != null) StopCoroutine(_seqCoroutine);
        _seqCoroutine = StartCoroutine(seq);
    }

    private IEnumerator SeqTeleportKeFieldDanFillPipa()
    {
        if (_audioBalasanNPC != null && _audioSource != null)
            _audioSource.PlayOneShot(_audioBalasanNPC, _volumeBell);
        if (_hud != null) _hud.ShowNotifPublic(_pesanLaporanAwalDiterima);
        yield return new WaitForSeconds(_jedaSetelahLaporanAwal);

        if (_hud != null) _hud.PlayManualFade(_durasiFade);
        yield return new WaitForSeconds(_durasiFade * 0.5f);

        TeleportPlayer(EnsureTeleportTarget(ref _teleportTargetPump, _pumpReference, _offsetObservasiPump, "L4_ObservasiPump"));
        SetHighlight(_pumpHighlightRenderers, true, ref _highlightPumpAktif);
        if (_hud != null) _hud.ShowNotifPublic(_pesanObservasiPump);
        PlayBell(_bellEnterField);

        // Start pump motor sound saat player sudah di field (biar kedengaran jelas)
        StartPumpSound();

        yield return new WaitForSeconds(_durasiFade * 0.5f);
        yield return new WaitForSeconds(_jedaDiPump);

        yield return StartCoroutine(AnimasikanLiquidFillPipa());

        SetHighlight(_pumpHighlightRenderers, false, ref _highlightPumpAktif);
        SetHighlight(_preheaterHighlightRenderers, true, ref _highlightPreheaterAktif);
        yield return new WaitForSeconds(_jedaSetelahLiquidSampai);

        GameLevelManager.Instance?.NotifyLevel4PhaseAdvance(GameLevelManager.Level4Phase.MenungguLaporanAkhir);
        _seqCoroutine = null;
    }

    private IEnumerator AnimasikanLiquidFillPipa()
    {
        if (_pipaUtama == null)
        {
            Debug.LogWarning("[Level4Controller] _pipaUtama belum ditentukan. Animasi liquid skip.");
            yield break;
        }

        EnsureLiquidFill();
        EnsureSlurryTankFill();
        if (_liquidFillRuntime == null) yield break;

        // Match pipa transform: parent ke pipa
        _liquidFillRuntime.transform.SetParent(_pipaUtama, false);

        // Cylinder primitive: half-height = 1, radius = 0.5
        // Anchor di bottom (Y=-1 local), grow Y dari 0 → 1
        _liquidFillRuntime.transform.localPosition = new Vector3(0f, -1f, 0f);
        _liquidFillRuntime.transform.localRotation = Quaternion.identity;
        _liquidFillRuntime.transform.localScale = new Vector3(_diameterRelatif, 0.001f, _diameterRelatif);
        _liquidFillRuntime.SetActive(true);

        // Wobble animator: kasih tau base values
        var wobble = _liquidFillRuntime.GetComponent<PipeFlowAnimator>();

        // Snapshot tank fill state awal (penuh) untuk drain animation
        Vector3 tankScaleAwal = _slurryTankFill != null ? _slurryTankFill.localScale : Vector3.one;
        Vector3 tankPosAwal = _slurryTankFill != null ? _slurryTankFill.localPosition : Vector3.zero;

        float elapsed = 0f;
        while (elapsed < _durasiLiquidMengalir)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.Clamp01(elapsed / _durasiLiquidMengalir);
            float curveT = _kurvaLiquid.Evaluate(t);

            // Pipa fill (mengalir masuk ke pipa)
            float pipaScaleY = Mathf.Lerp(0.001f, 1f, curveT);
            Vector3 baseScale = new Vector3(_diameterRelatif, pipaScaleY, _diameterRelatif);
            Vector3 basePos = new Vector3(0f, -1f + pipaScaleY, 0f);
            _liquidFillRuntime.transform.localScale = baseScale;
            _liquidFillRuntime.transform.localPosition = basePos;

            if (wobble != null)
            {
                wobble.UpdateBaseScale(baseScale);
                wobble.UpdateBasePosition(basePos);
            }

            // Tank drain — proporsional terhadap progress (terbalik: tank PENUH → tank AKHIR)
            if (_slurryTankFill != null)
            {
                float tankScaleYNow = Mathf.Lerp(_slurryTankScaleYPenuh, _slurryTankScaleYAkhir, curveT);
                float tankPosYNow = Mathf.Lerp(_slurryTankPosYPenuh, _slurryTankPosYAkhir, curveT);
                Vector3 tScale = tankScaleAwal;
                tScale.y = tankScaleYNow;
                _slurryTankFill.localScale = tScale;
                Vector3 tPos = tankPosAwal;
                tPos.y = tankPosYNow;
                _slurryTankFill.localPosition = tPos;
            }

            yield return null;
        }

        // Final state: pipa full, tank di posisi akhir
        Vector3 finalScale = new Vector3(_diameterRelatif, 1f, _diameterRelatif);
        Vector3 finalPos = Vector3.zero;
        _liquidFillRuntime.transform.localScale = finalScale;
        _liquidFillRuntime.transform.localPosition = finalPos;
        if (wobble != null)
        {
            wobble.UpdateBaseScale(finalScale);
            wobble.UpdateBasePosition(finalPos);
        }
        if (_slurryTankFill != null)
        {
            Vector3 tScale = _slurryTankFill.localScale;
            tScale.y = _slurryTankScaleYAkhir;
            _slurryTankFill.localScale = tScale;
            Vector3 tPos = _slurryTankFill.localPosition;
            tPos.y = _slurryTankPosYAkhir;
            _slurryTankFill.localPosition = tPos;
        }
    }

    private IEnumerator SeqKembaliKeDcs()
    {
        SetHighlight(_preheaterHighlightRenderers, false, ref _highlightPreheaterAktif);

        if (_audioBalasanNPC != null && _audioSource != null)
            _audioSource.PlayOneShot(_audioBalasanNPC, _volumeBell);
        yield return new WaitForSeconds(_durasiAudioBalasan);

        if (_hud != null) _hud.PlayManualFade(_durasiFade);
        yield return new WaitForSeconds(_durasiFade * 0.5f);

        // Stop pump sound saat fade out
        SetLevel4PipeFlow(false);
        StopPumpSound();

        var dcsTarget = EnsureDcsTarget();
        TeleportPlayer(dcsTarget);

        if (_hud != null) _hud.ShowNotifPublic(_pesanKembaliDcs);
        PlayBell(_bellKembaliDcs);

        yield return new WaitForSeconds(_durasiFade * 0.5f);
        yield return new WaitForSeconds(_jedaSetelahKembaliDcs);

        GameLevelManager.Instance?.NotifyLevel4Selesai();
        _seqCoroutine = null;
    }

    // ============================================================
    //  LIQUID FILL VISUAL
    // ============================================================

    private void EnsureLiquidFill()
    {
        if (_liquidFillRuntime != null) return;

        _liquidFillRuntime = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        _liquidFillRuntime.name = "Level4_LiquidFill";
        var col = _liquidFillRuntime.GetComponent<Collider>();
        if (col != null) DestroyImmediate(col);

        // Pakai material yang SAMA dengan Slurry_Fill di tank (ungu).
        // Caranya: ambil material runtime dari Slurry_Fill GameObject di scene.
        if (_liquidMaterial == null)
        {
            var tankFill = GameObject.Find("Mesin Utama/Slurry Tank/Slurry_Fill");
            if (tankFill == null)
            {
                foreach (var go in Resources.FindObjectsOfTypeAll<GameObject>())
                    if (go != null && go.scene.IsValid() && go.name == "Slurry_Fill") { tankFill = go; break; }
            }

            if (tankFill != null)
            {
                var tankMr = tankFill.GetComponent<MeshRenderer>();
                if (tankMr != null && tankMr.sharedMaterial != null)
                    _liquidMaterial = tankMr.sharedMaterial;
            }

            if (_liquidMaterial == null)
            {
                // Fallback: buat material slurry ungu (sama dengan tank)
                _liquidMaterial = new Material(Shader.Find("Universal Render Pipeline/Lit"));
                _liquidMaterial.SetColor("_BaseColor", new Color(0.42f, 0.18f, 0.55f, 0.95f));
                _liquidMaterial.SetColor("_Color", new Color(0.42f, 0.18f, 0.55f, 0.95f));
                _liquidMaterial.SetFloat("_Smoothness", 0.7f);
                _liquidMaterial.SetFloat("_Metallic", 0.05f);
                _liquidMaterial.EnableKeyword("_EMISSION");
                _liquidMaterial.SetColor("_EmissionColor", new Color(0.42f, 0.18f, 0.55f) * 0.6f);
                Debug.LogWarning("[Level4Controller] Fallback: bikin material slurry ungu sendiri (tidak ketemu Slurry_Fill di scene).");
            }
        }
        _liquidFillRuntime.GetComponent<MeshRenderer>().sharedMaterial = _liquidMaterial;

        // Tambah PipeFlowAnimator untuk efek wobble + UV scroll + emission pulse
        var anim = _liquidFillRuntime.GetComponent<PipeFlowAnimator>();
        if (anim == null) _liquidFillRuntime.AddComponent<PipeFlowAnimator>();

        _liquidFillRuntime.SetActive(false);
    }

    private void HideLiquid()
    {
        if (_liquidFillRuntime != null) _liquidFillRuntime.SetActive(false);
    }

    private void SetLevel4PipeFlow(bool active)
    {
        if (_slurryToPreheaterFlow == null)
        {
            var sf = GameObject.Find("SlurryToPreheater_SlurryFlow");
            if (sf != null) _slurryToPreheaterFlow = sf.GetComponent<ProcessPipeFlowAnimator>();
        }
        if (_slurryToPreheaterFlow != null) _slurryToPreheaterFlow.SetFlowing(active);

        if (_pipeNetwork == null)
        {
            if (!isActiveAndEnabled || !gameObject.activeInHierarchy)
                return;

            var mesinUtama = GameObject.Find("Mesin Utama");
            if (mesinUtama != null)
                _pipeNetwork = mesinUtama.GetComponent<ProcessPipeNetwork>();
        }

        if (_pipeNetwork == null || _level4FlowRouteIds == null)
            return;

        for (int i = 0; i < _level4FlowRouteIds.Length; i++)
        {
            string routeId = _level4FlowRouteIds[i];
            if (!string.IsNullOrWhiteSpace(routeId))
                _pipeNetwork.SetRouteFlowActive(routeId, active);
        }
    }

    // ============================================================
    //  PUMP MOTOR SOUND
    // ============================================================

    private void EnsurePumpAudio()
    {
        if (_pumpAudioSource != null) return;

        // Cari existing PumpMotor_Audio di scene (sudah dibuat permanent)
        var existingGo = GameObject.Find("PumpMotor_Audio");
        if (existingGo != null)
        {
            _pumpAudioSource = existingGo.GetComponent<AudioSource>();
            if (_pumpAudioSource == null)
                _pumpAudioSource = existingGo.AddComponent<AudioSource>();
        }
        else
        {
            // Fallback: bikin baru di pump reference
            if (_pumpReference == null) AutoFindReferences();
            if (_pumpReference == null) return;

            var go = new GameObject("PumpMotor_Audio");
            go.transform.SetParent(_pumpReference.transform, false);
            go.transform.localPosition = Vector3.zero;
            _pumpAudioSource = go.AddComponent<AudioSource>();
        }

        _pumpAudioSource.spatialBlend = 0f; // full 2D - SELALU kedengaran
        _pumpAudioSource.maxDistance = 100f;
        _pumpAudioSource.minDistance = 1f;
        _pumpAudioSource.rolloffMode = AudioRolloffMode.Linear;
        _pumpAudioSource.loop = true;
        _pumpAudioSource.playOnAwake = false;
        _pumpAudioSource.volume = 0f;
        _pumpAudioSource.priority = 32;

        if (_pumpMotorClip == null)
            _pumpMotorClip = BuatClipMotor(durasi: 4f, sampleRate: 22050);
        _pumpAudioSource.clip = _pumpMotorClip;
    }

    private void StartPumpSound()
    {
        if (!isActiveAndEnabled || !gameObject.activeInHierarchy) return;

        EnsurePumpAudio();
        if (_pumpAudioSource == null) return;
        if (_pumpAudioSource.isPlaying && _pumpAudioSource.volume > 0.1f) return;

        _pumpAudioSource.volume = 0f;
        _pumpAudioSource.Play();
        if (_pumpFadeCoroutine != null) StopCoroutine(_pumpFadeCoroutine);
        _pumpFadeCoroutine = StartCoroutine(FadeAudioVolume(_pumpAudioSource, _pumpMotorVolumeTarget, _pumpFadeInDurasi));
    }

    private void StopPumpSound()
    {
        if (_pumpAudioSource == null) return;
        if (_pumpFadeCoroutine != null)
        {
            StopCoroutine(_pumpFadeCoroutine);
            _pumpFadeCoroutine = null;
        }

        if (!isActiveAndEnabled || !gameObject.activeInHierarchy)
        {
            _pumpAudioSource.volume = 0f;
            _pumpAudioSource.Stop();
            return;
        }

        _pumpFadeCoroutine = StartCoroutine(FadeAudioVolumeAndStop(_pumpAudioSource, 0f, _pumpFadeInDurasi));
    }

    private IEnumerator FadeAudioVolume(AudioSource src, float target, float dur)
    {
        if (src == null) yield break;
        float start = src.volume;
        float t = 0f;
        while (t < dur && src != null)
        {
            t += Time.deltaTime;
            src.volume = Mathf.Lerp(start, target, t / dur);
            yield return null;
        }
        if (src != null) src.volume = target;
    }

    private IEnumerator FadeAudioVolumeAndStop(AudioSource src, float target, float dur)
    {
        yield return FadeAudioVolume(src, target, dur);
        if (src != null && target <= 0.001f) src.Stop();
    }

    // ============================================================
    //  PROCEDURAL AUDIO GENERATORS
    // ============================================================

    private AudioClip BuatClipBell(float fundamentalHz, float harmonicHz, float durasi, int sampleRate)
    {
        int total = Mathf.CeilToInt(durasi * sampleRate);
        float[] data = new float[total];
        for (int i = 0; i < total; i++)
        {
            float t = (float)i / sampleRate;
            float env = Mathf.Exp(-t * 4.5f);
            float fundamental = Mathf.Sin(2f * Mathf.PI * fundamentalHz * t);
            float harmonic = Mathf.Sin(2f * Mathf.PI * harmonicHz * t) * 0.55f;
            float detune = Mathf.Sin(2f * Mathf.PI * (harmonicHz * 1.012f) * t) * 0.25f;
            data[i] = (fundamental * 0.7f + harmonic + detune) * env * 0.45f;
        }
        var clip = AudioClip.Create("ProcBell", total, 1, sampleRate, false);
        clip.SetData(data, 0);
        return clip;
    }

    /// <summary>
    /// Generate motor pump sound: low-frequency rumble + sine harmonic + noise turbulence.
    /// </summary>
    private AudioClip BuatClipMotor(float durasi, int sampleRate)
    {
        int total = Mathf.CeilToInt(durasi * sampleRate);
        float[] data = new float[total];
        System.Random rnd = new System.Random(123);
        float phase1 = 0f, phase2 = 0f;
        float lpPrev = 0f;

        for (int i = 0; i < total; i++)
        {
            phase1 += 2f * Mathf.PI * 75f / sampleRate;   // Low rumble
            phase2 += 2f * Mathf.PI * 220f / sampleRate;  // Mid pitch motor
            float sine = Mathf.Sin(phase1) * 0.5f + Mathf.Sin(phase2) * 0.25f;
            float noise = ((float)rnd.NextDouble() - 0.5f) * 0.2f;
            lpPrev = lpPrev + 0.18f * (noise - lpPrev);
            data[i] = (sine * 0.7f + lpPrev * 0.6f) * 0.42f;
        }

        // Crossfade endpoints biar loop seamless
        int fadeLen = Mathf.Min(2200, total / 25);
        for (int i = 0; i < fadeLen; i++)
        {
            float fade = (float)i / fadeLen;
            data[i] *= fade;
            data[total - 1 - i] *= fade;
        }

        var clip = AudioClip.Create("ProcPumpMotor", total, 1, sampleRate, false);
        clip.SetData(data, 0);
        return clip;
    }

    // ============================================================
    //  HELPERS
    // ============================================================

    private void PlayBell(AudioClip clip)
    {
        if (clip == null || _audioSource == null) return;
        _audioSource.PlayOneShot(clip, _volumeBell);
    }

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
            _preheaterReference = GameObject.Find("PreHeater_Field_1")
                                ?? GameObject.Find("PreHeater_Field")
                                ?? GameObject.Find("PreHeater");

        if (_pipaUtama == null && _autoFindPipa)
        {
            var go = GameObject.Find("Mesin Utama/PreHeater_Field_1/Pipe_FromPump")
                  ?? GameObject.Find("Pipe_FromPump");
            if (go != null) _pipaUtama = go.transform;
        }

        if (_pipeNetwork == null)
        {
            var mesinUtama = GameObject.Find("Mesin Utama");
            if (mesinUtama != null)
                _pipeNetwork = mesinUtama.GetComponent<ProcessPipeNetwork>();
        }

        if (_pumpAudioPosition == null && _pumpReference != null)
            _pumpAudioPosition = _pumpReference.transform;
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
        go.transform.SetParent(reference.transform.parent, false);
        go.transform.position = reference.transform.position + offset;
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
        if (target == null) { Debug.LogWarning("[Level4Controller] Teleport target null."); return; }
        if (_playerRigRoot == null) AutoFindPlayerRig();
        if (_playerRigRoot == null) return;

        var xrOrigin = _playerRigRoot.GetComponent<Unity.XR.CoreUtils.XROrigin>();
        var cc = _playerCharacterController;
        bool ccEnabled = cc != null && cc.enabled;
        if (ccEnabled) cc.enabled = false;

        if (xrOrigin != null)
        {
            Vector3 cameraTarget = target.position + Vector3.up * xrOrigin.CameraYOffset;
            xrOrigin.MoveCameraToWorldLocation(cameraTarget);
            Vector3 fwd = target.rotation * Vector3.forward;
            xrOrigin.MatchOriginUpCameraForward(Vector3.up, fwd);
        }
        else
        {
            _playerRigRoot.SetPositionAndRotation(target.position, target.rotation);
        }

        if (ccEnabled) cc.enabled = true;
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
