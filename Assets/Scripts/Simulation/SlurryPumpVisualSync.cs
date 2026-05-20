using UnityEngine;

/// <summary>
/// OLIVIA VR - SlurryPumpVisualSync.cs
///
/// Mensinkronisasi visual + audio pump slurry di lapangan dengan flow rate yang di-set
/// di DCS (via GameLevelManager.FlowRate).
///
/// Komponen yang disinkronkan:
///   - Material pipe scrolling (texture offset / _MainTex_ST) — kecepatan aliran
///   - ParticleSystem fluid trail di pipe
///   - Pump motor rotation (mesh berputar)
///   - AudioSource pump motor (pitch & volume scaling)
///   - Optional: emisi glow indikator status (off/idle/aktif)
///
/// Pemakaian: pasang di GameObject pump utama. Assign:
///   _flowMaterials  = material pipe yang di-scroll
///   _agitatorMesh   = mesh motor/agitator yang berputar
///   _pumpAudio      = AudioSource pump
///   _flowParticles  = ParticleSystem yang menjadi trail aliran
/// </summary>
public class SlurryPumpVisualSync : MonoBehaviour
{
    [Header("=== Sumber Flow Rate ===")]
    [Tooltip("Flow rate maksimum desain pompa (m³/h). Visual scroll di-normalize ke nilai ini.")]
    [SerializeField] private float _flowMaksimumDesain = 600f;
    [Tooltip("Threshold flow rate (m³/h) di bawah ini visual + audio dianggap idle/off.")]
    [SerializeField] private float _flowMinimumAktif = 5f;

    [Header("=== Material Pipe (Scroll Texture) ===")]
    [Tooltip("Daftar material yang punya texture pipe slurry. Texture akan di-offset (_MainTex_ST atau _BaseMap_ST) tiap frame berdasarkan flow rate.")]
    [SerializeField] private Material[] _flowMaterials;
    [Tooltip("Property name untuk texture utama. URP-Lit pakai _BaseMap, Standard pakai _MainTex.")]
    [SerializeField] private string _texturePropertyName = "_BaseMap";
    [Tooltip("Arah scroll texture dalam UV space. (0,1) = ke atas, (1,0) = ke kanan.")]
    [SerializeField] private Vector2 _arahScroll = new Vector2(0f, 1f);
    [Tooltip("Multiplier kecepatan scroll. Flow 450 m³/h pada multiplier 1 = 0.75 UV/detik.")]
    [SerializeField] private float _multiplierScroll = 1f;

    [Header("=== Motor / Agitator Rotation ===")]
    [Tooltip("Transform yang akan diputar saat pump aktif (impeller/motor shaft).")]
    [SerializeField] private Transform _motorTransform;
    [Tooltip("Sumbu rotasi (default Y).")]
    [SerializeField] private Vector3 _sumbuRotasi = Vector3.up;
    [Tooltip("RPM motor pada flow rate maksimum (visual). 60 RPM = 1 rotasi/detik, sangat pelan dan visible.")]
    [SerializeField] private float _rpmMaksimum = 60f;
    [Tooltip("Aktifkan rotasi HANYA saat Level 4 sudah lewat fase lapor HT (ramp-up pelan ke kenceng).")]
    [SerializeField] private bool _rotasiHanyaSetelahLapor = true;
    [Tooltip("Durasi ramp-up dari 0 ke RPM max (detik). Lebih lama = lebih dramatis.")]
    [SerializeField] private float _durasiRampUpRotasi = 8.0f;

    [Header("=== Audio Pump ===")]
    [SerializeField] private AudioSource _pumpAudio;
    [Tooltip("Pitch saat pump idle (flow ~ 0).")]
    [SerializeField] private float _pitchMinimum = 0.4f;
    [Tooltip("Pitch saat pump pada flow maksimum.")]
    [SerializeField] private float _pitchMaksimum = 1.15f;
    [Tooltip("Volume saat pump idle.")]
    [Range(0f, 1f)] [SerializeField] private float _volumeMinimum = 0.05f;
    [Tooltip("Volume saat pump pada flow maksimum.")]
    [Range(0f, 1f)] [SerializeField] private float _volumeMaksimum = 0.65f;
    [Tooltip("Smoothing audio supaya tidak jumpy saat flow rate berubah cepat.")]
    [Range(0f, 0.5f)] [SerializeField] private float _smoothAudio = 0.15f;

    [Header("=== Particle Trail ===")]
    [SerializeField] private ParticleSystem _flowParticles;
    [Tooltip("Emisi maksimum saat flow rate maksimum.")]
    [SerializeField] private float _emisiMaksimum = 80f;

    [Header("=== Status Indicator ===")]
    [Tooltip("Renderer indikator status (lampu LED kecil di pump). Akan di-set warna emission.")]
    [SerializeField] private Renderer _ledIndicator;
    [SerializeField] private Color _warnaIdle = new Color(0.4f, 0f, 0f, 1f);
    [SerializeField] private Color _warnaAktif = new Color(0f, 1f, 0.3f, 1f);
    [SerializeField] private Color _warnaTargetTercapai = new Color(0.2f, 1f, 1f, 1f);

    private Vector2 _scrollOffset;
    private float _currentRotation;
    private float _rampRotasiT;
    private MaterialPropertyBlock _ledMpb;
    private bool _audioWasIdle = true;

    private float _flowSaatIni;
    private float _flowSmoothed;
    private float _flowVelocity;

    private void Awake()
    {
        // Auto-create AudioSource kalau belum ada
        if (_pumpAudio == null)
        {
            _pumpAudio = GetComponent<AudioSource>();
            if (_pumpAudio == null)
                _pumpAudio = gameObject.AddComponent<AudioSource>();
        }

        _pumpAudio.loop = true;
        _pumpAudio.spatialBlend = 1f;
        _pumpAudio.maxDistance = 25f;
        _pumpAudio.rolloffMode = AudioRolloffMode.Linear;
        _pumpAudio.volume = 0f;
        _pumpAudio.playOnAwake = false;

        if (_pumpAudio.clip == null)
            _pumpAudio.clip = BuatClipMotorPump(durasi: 3f, sampleRate: 22050);

        _ledMpb = new MaterialPropertyBlock();
        SetLedColor(_warnaIdle);
    }

    private void OnEnable()
    {
        _scrollOffset = Vector2.zero;
    }

    private void Update()
    {
        // Ambil flow rate dari GameLevelManager. Jika tidak ada, fallback ke 0.
        _flowSaatIni = GameLevelManager.Instance != null ? GameLevelManager.Instance.FlowRate : 0f;
        _flowSmoothed = Mathf.SmoothDamp(_flowSmoothed, _flowSaatIni, ref _flowVelocity, _smoothAudio);

        float t = Mathf.Clamp01(_flowSmoothed / Mathf.Max(1f, _flowMaksimumDesain));
        bool aktif = _flowSmoothed >= _flowMinimumAktif;

        UpdateMaterialScroll(t, aktif);
        UpdateMotorRotation(t, aktif);
        UpdateAudio(t, aktif);
        UpdateParticles(t, aktif);
        UpdateLedIndicator(aktif);
    }

    private void UpdateMaterialScroll(float t, bool aktif)
    {
        if (_flowMaterials == null || _flowMaterials.Length == 0)
            return;

        if (!aktif)
            return;

        Vector2 deltaScroll = _arahScroll * (t * _multiplierScroll * Time.deltaTime);
        _scrollOffset += deltaScroll;
        _scrollOffset.x = Mathf.Repeat(_scrollOffset.x, 1f);
        _scrollOffset.y = Mathf.Repeat(_scrollOffset.y, 1f);

        for (int i = 0; i < _flowMaterials.Length; i++)
        {
            var mat = _flowMaterials[i];
            if (mat == null) continue;
            mat.SetTextureOffset(_texturePropertyName, _scrollOffset);
        }
    }

    private void UpdateMotorRotation(float t, bool aktif)
    {
        if (_motorTransform == null)
            return;

        // Cek apakah Level 4 sudah lewat fase MenungguLaporanFlow (artinya sudah lapor HT).
        bool sudahLaporHt = false;
        if (GameLevelManager.Instance != null &&
            GameLevelManager.Instance.CurrentLevel == GameLevelManager.GameLevel.Level4_SlurryPump)
        {
            var phase = GameLevelManager.Instance.CurrentLevel4Phase;
            sudahLaporHt = phase == GameLevelManager.Level4Phase.KembaliKeDcs ||
                           phase == GameLevelManager.Level4Phase.Selesai;
        }

        if (_rotasiHanyaSetelahLapor && !sudahLaporHt)
        {
            // Belum lapor HT → diam.
            _rampRotasiT = 0f;
            return;
        }

        if (!aktif) return;

        // Ramp-up: saat baru di-trigger, mulai dari 0 dan naik perlahan ke RPM max.
        _rampRotasiT = Mathf.Min(1f, _rampRotasiT + Time.deltaTime / Mathf.Max(0.1f, _durasiRampUpRotasi));
        // Ease-in cubic supaya start very slow → kenceng natural.
        float ramp = _rampRotasiT * _rampRotasiT * _rampRotasiT;
        float rpm = Mathf.Lerp(0f, _rpmMaksimum, t) * ramp;
        // RPM -> degrees per second = rpm * 6
        float degPerSec = rpm * 6f;
        _currentRotation += degPerSec * Time.deltaTime;
        _motorTransform.localRotation = Quaternion.AngleAxis(_currentRotation, _sumbuRotasi.normalized);
    }

    private void UpdateAudio(float t, bool aktif)
    {
        if (_pumpAudio == null)
            return;

        if (aktif)
        {
            if (_audioWasIdle || !_pumpAudio.isPlaying)
            {
                _pumpAudio.Play();
                _audioWasIdle = false;
            }

            _pumpAudio.pitch = Mathf.Lerp(_pitchMinimum, _pitchMaksimum, t);
            _pumpAudio.volume = Mathf.Lerp(_volumeMinimum, _volumeMaksimum, t);
        }
        else
        {
            // Fade out volume saat idle
            _pumpAudio.volume = Mathf.MoveTowards(_pumpAudio.volume, 0f, Time.deltaTime * 0.8f);
            if (_pumpAudio.volume <= 0.001f && _pumpAudio.isPlaying)
            {
                _pumpAudio.Stop();
                _audioWasIdle = true;
            }
        }
    }

    private void UpdateParticles(float t, bool aktif)
    {
        if (_flowParticles == null)
            return;

        var emission = _flowParticles.emission;
        if (aktif)
        {
            emission.rateOverTime = Mathf.Lerp(0f, _emisiMaksimum, t);
            if (!_flowParticles.isPlaying)
                _flowParticles.Play(true);
        }
        else
        {
            emission.rateOverTime = 0f;
        }
    }

    private void UpdateLedIndicator(bool aktif)
    {
        if (_ledIndicator == null)
            return;

        Color targetColor = _warnaIdle;
        if (aktif)
        {
            // Cek target tercapai (Level 4 target = 450 ± 10)
            bool targetTercapai = GameLevelManager.Instance != null &&
                                   Mathf.Abs(_flowSaatIni - 450f) <= 10f;
            targetColor = targetTercapai ? _warnaTargetTercapai : _warnaAktif;
        }

        SetLedColor(targetColor);
    }

    private void SetLedColor(Color color)
    {
        if (_ledIndicator == null || _ledMpb == null)
            return;

        _ledIndicator.GetPropertyBlock(_ledMpb);
        _ledMpb.SetColor("_EmissionColor", color * 2.5f);
        _ledMpb.SetColor("_BaseColor", color);
        _ledMpb.SetColor("_Color", color);
        _ledIndicator.SetPropertyBlock(_ledMpb);

        if (_ledIndicator.sharedMaterial != null)
            _ledIndicator.sharedMaterial.EnableKeyword("_EMISSION");
    }

    /// <summary>
    /// Generate AudioClip motor pump prosedural. Mid-low frequency rumble + harmonics + pulse pump rhythm.
    /// Pulse rhythm = beat tiap ~0.55s yang naik amplitudo (efek "pumping").
    /// </summary>
    private AudioClip BuatClipMotorPump(float durasi, int sampleRate)
    {
        int totalSamples = Mathf.CeilToInt(durasi * sampleRate);
        float[] data = new float[totalSamples];
        System.Random rnd = new System.Random(123);

        float phase1 = 0f, phase2 = 0f, phase3 = 0f;
        float lpPrev = 0f;

        // Pump pulse: setiap 0.55 detik (≈ 110 BPM) ada "chuk" yang naik volumenya.
        float beatHz = 1.8f;            // 1.8 beat per second (≈ 108 BPM, mirip pompa industrial)
        float pulsePhase = 0f;

        for (int i = 0; i < totalSamples; i++)
        {
            // Tiga sine berlapis: fundamental + 2 harmonik (motor)
            phase1 += 2f * Mathf.PI * 90f / sampleRate;
            phase2 += 2f * Mathf.PI * 180f / sampleRate;
            phase3 += 2f * Mathf.PI * 270f / sampleRate;

            float sine = Mathf.Sin(phase1) * 0.5f
                       + Mathf.Sin(phase2) * 0.25f
                       + Mathf.Sin(phase3) * 0.12f;

            // Pulse pump (setiap beat, amplitudo membesar)
            pulsePhase += 2f * Mathf.PI * beatHz / sampleRate;
            float pulseRaw = Mathf.Sin(pulsePhase);
            // Map sine ke "kick" envelope (asimetris: spike cepat → decay)
            float pulseEnv = Mathf.Pow(Mathf.Max(0f, pulseRaw), 4f); // 0..1, asimetris
            // Sub-low "thump" 60 Hz dimodulasi pulse env
            float thump = Mathf.Sin(2f * Mathf.PI * 60f * i / sampleRate) * pulseEnv * 0.8f;

            // Slight noise untuk turbulensi mekanik
            float noise = ((float)rnd.NextDouble() - 0.5f) * 0.2f;
            // Low pass
            lpPrev = lpPrev + 0.15f * (noise - lpPrev);

            data[i] = (sine * 0.55f + lpPrev * 0.4f + thump * 0.55f) * 0.42f;
        }

        // Crossfade endpoints untuk seamless loop
        int fadeLen = Mathf.Min(2000, totalSamples / 30);
        for (int i = 0; i < fadeLen; i++)
        {
            float fade = (float)i / fadeLen;
            data[i] *= fade;
            data[totalSamples - 1 - i] *= fade;
        }

        AudioClip clip = AudioClip.Create("ProcPumpMotor", totalSamples, 1, sampleRate, false);
        clip.SetData(data, 0);
        return clip;
    }
}
