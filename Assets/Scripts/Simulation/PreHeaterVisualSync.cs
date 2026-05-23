using UnityEngine;

/// <summary>
/// OLIVIA VR - PreHeaterVisualSync.cs
///
/// Mensinkronisasi visual + audio pre-heater dengan flow rate yang di-set di DCS
/// (via GameLevelManager.FlowRate).
///
/// Komponen yang disinkronkan:
///   - Material pipa scrolling (inlet + outlet)
///   - Steam particle emission (semakin tinggi flow → semakin banyak uap)
///   - Heating fin emission (glow merah-orange saat slurry mengalir)
///   - LED status indicator
///
/// Pemakaian: pasang di GameObject PreHeater_Field. Auto-find child:
///   - Pipe_FromPump, Pipe_OutletVertical, Pipe_OutletHorizontal → flow materials
///   - Steam_FX → particle system uap
///   - HeatingFin_1/2/3 → renderer fin (glow)
///   - LED_Preheater → indikator status
/// </summary>
public class PreHeaterVisualSync : MonoBehaviour
{
    [Header("=== Sumber Flow Rate ===")]
    [Tooltip("Flow rate maksimum desain pre-heater (m³/h).")]
    [SerializeField] private float _flowMaksimumDesain = 600f;
    [Tooltip("Threshold flow rate (m³/h) di bawah ini visual dianggap idle.")]
    [SerializeField] private float _flowMinimumAktif = 5f;

    [Header("=== Pipe Scroll ===")]
    [Tooltip("Material pipa yang di-scroll. Auto-collect dari child Pipe_* jika kosong.")]
    [SerializeField] private Material[] _flowMaterials;
    [SerializeField] private string _texturePropertyName = "_BaseMap";
    [SerializeField] private Vector2 _arahScroll = new Vector2(0f, 1f);
    [SerializeField] private float _multiplierScroll = 1f;

    [Header("=== Steam Particle ===")]
    [SerializeField] private ParticleSystem _steamParticles;
    [Tooltip("Emisi maksimum saat flow penuh.")]
    [SerializeField] private float _emisiSteamMaks = 35f;

    [Header("=== Heating Fin ===")]
    [Tooltip("Renderer dari fin/coil yang glow saat heating aktif.")]
    [SerializeField] private Renderer[] _finRenderers;
    [SerializeField] private Color _warnaPanasIdle = new Color(0.1f, 0.1f, 0.1f, 1f);
    [SerializeField] private Color _warnaPanasMaks = new Color(1f, 0.45f, 0.1f, 1f);
    [SerializeField] private float _intensitasPanas = 2.5f;

    [Header("=== LED Indicator ===")]
    [SerializeField] private Renderer _ledIndicator;
    [SerializeField] private Color _warnaIdle = new Color(0.4f, 0f, 0f, 1f);
    [SerializeField] private Color _warnaAktif = new Color(0f, 1f, 0.3f, 1f);
    [SerializeField] private Color _warnaTargetTercapai = new Color(0.2f, 1f, 1f, 1f);

    [Header("=== Audio (Hiss steam) ===")]
    [SerializeField] private AudioSource _steamAudio;
    [Range(0f, 1f)] [SerializeField] private float _volumeMaks = 0.45f;
    [SerializeField] private float _smoothAudio = 0.2f;

    private Vector2 _scrollOffset;
    private MaterialPropertyBlock _mpb;
    private float _flowSmoothed;
    private float _flowVelocity;

    private void Awake()
    {
        AutoCollectReferences();

        _steamAudio = GetComponent<AudioSource>();
        if (_steamAudio == null) _steamAudio = gameObject.AddComponent<AudioSource>();

        _steamAudio.loop = true;
        _steamAudio.spatialBlend = 1f;
        _steamAudio.maxDistance = 25f;
        _steamAudio.rolloffMode = AudioRolloffMode.Linear;
        _steamAudio.volume = 0f;
        _steamAudio.playOnAwake = false;
        if (_steamAudio.clip == null)
            _steamAudio.clip = BuatClipSteamHiss(durasi: 3f, sampleRate: 22050);

        _mpb = new MaterialPropertyBlock();
        SetLedColor(_warnaIdle);
        ApplyFinGlow(0f);
    }

    private void Update()
    {
        float flow = GameLevelManager.Instance != null ? GameLevelManager.Instance.FlowRate : 0f;
        _flowSmoothed = Mathf.SmoothDamp(_flowSmoothed, flow, ref _flowVelocity, _smoothAudio);

        float t = Mathf.Clamp01(_flowSmoothed / Mathf.Max(1f, _flowMaksimumDesain));
        bool aktif = _flowSmoothed >= _flowMinimumAktif;

        UpdateMaterialScroll(t, aktif);
        UpdateSteam(t, aktif);
        ApplyFinGlow(aktif ? t : 0f);
        UpdateAudio(t, aktif);
        UpdateLedIndicator(aktif, flow);
    }

    private void AutoCollectReferences()
    {
        // Auto-collect flow materials dari child bernama "Pipe_*"
        if (_flowMaterials == null || _flowMaterials.Length == 0)
        {
            var rends = GetComponentsInChildren<Renderer>(true);
            var found = new System.Collections.Generic.List<Material>();
            foreach (var r in rends)
            {
                if (r == null || r.sharedMaterial == null) continue;
                if (r.gameObject.name.StartsWith("Pipe_"))
                {
                    if (!found.Contains(r.sharedMaterial))
                        found.Add(r.sharedMaterial);
                }
            }
            _flowMaterials = found.ToArray();
        }

        if (_steamParticles == null)
        {
            var t = transform.Find("Steam_FX");
            if (t != null) _steamParticles = t.GetComponent<ParticleSystem>();
        }

        if (_finRenderers == null || _finRenderers.Length == 0)
        {
            var rends = GetComponentsInChildren<Renderer>(true);
            var found = new System.Collections.Generic.List<Renderer>();
            foreach (var r in rends)
            {
                if (r != null && r.gameObject.name.StartsWith("HeatingFin"))
                    found.Add(r);
            }
            _finRenderers = found.ToArray();
        }

        if (_ledIndicator == null)
        {
            var t = transform.Find("LED_Preheater");
            if (t != null) _ledIndicator = t.GetComponent<Renderer>();
        }
    }

    private void UpdateMaterialScroll(float t, bool aktif)
    {
        if (_flowMaterials == null || _flowMaterials.Length == 0 || !aktif) return;

        Vector2 delta = _arahScroll * (t * _multiplierScroll * Time.deltaTime);
        _scrollOffset += delta;
        _scrollOffset.x = Mathf.Repeat(_scrollOffset.x, 1f);
        _scrollOffset.y = Mathf.Repeat(_scrollOffset.y, 1f);

        for (int i = 0; i < _flowMaterials.Length; i++)
        {
            var m = _flowMaterials[i];
            if (m == null) continue;
            m.SetTextureOffset(_texturePropertyName, _scrollOffset);
        }
    }

    private void UpdateSteam(float t, bool aktif)
    {
        if (_steamParticles == null) return;

        if (GameLevelManager.Instance != null)
        {
            var level = GameLevelManager.Instance.CurrentLevel;
            if (level == GameLevelManager.GameLevel.Level5_SteamValve ||
                level == GameLevelManager.GameLevel.Level7_Autoclave)
                return;
        }

        var em = _steamParticles.emission;
        if (aktif)
        {
            em.rateOverTime = Mathf.Lerp(0f, _emisiSteamMaks, t);
            if (!_steamParticles.isPlaying) _steamParticles.Play(true);
        }
        else
        {
            em.rateOverTime = 0f;
            if (_steamParticles.isPlaying)
                _steamParticles.Stop(true, ParticleSystemStopBehavior.StopEmitting);
        }
    }

    private void ApplyFinGlow(float t)
    {
        if (_finRenderers == null) return;
        Color targetEmission = Color.Lerp(_warnaPanasIdle, _warnaPanasMaks, t) * _intensitasPanas;
        foreach (var r in _finRenderers)
        {
            if (r == null) continue;
            r.GetPropertyBlock(_mpb);
            _mpb.SetColor("_EmissionColor", targetEmission);
            r.SetPropertyBlock(_mpb);
            if (r.sharedMaterial != null)
                r.sharedMaterial.EnableKeyword("_EMISSION");
        }
    }

    private void UpdateAudio(float t, bool aktif)
    {
        if (_steamAudio == null) return;
        if (aktif)
        {
            if (!_steamAudio.isPlaying) _steamAudio.Play();
            _steamAudio.volume = Mathf.Lerp(0f, _volumeMaks, t);
            _steamAudio.pitch = Mathf.Lerp(0.9f, 1.1f, t);
        }
        else
        {
            _steamAudio.volume = Mathf.MoveTowards(_steamAudio.volume, 0f, Time.deltaTime * 0.6f);
            if (_steamAudio.volume <= 0.001f && _steamAudio.isPlaying)
                _steamAudio.Stop();
        }
    }

    private void UpdateLedIndicator(bool aktif, float flow)
    {
        if (_ledIndicator == null) return;
        Color c = _warnaIdle;
        if (aktif)
        {
            bool target = Mathf.Abs(flow - 450f) <= 10f;
            c = target ? _warnaTargetTercapai : _warnaAktif;
        }
        SetLedColor(c);
    }

    private void SetLedColor(Color color)
    {
        if (_ledIndicator == null || _mpb == null) return;
        _ledIndicator.GetPropertyBlock(_mpb);
        _mpb.SetColor("_EmissionColor", color * 2.5f);
        _mpb.SetColor("_BaseColor", color);
        _mpb.SetColor("_Color", color);
        _ledIndicator.SetPropertyBlock(_mpb);
        if (_ledIndicator.sharedMaterial != null)
            _ledIndicator.sharedMaterial.EnableKeyword("_EMISSION");
    }

    /// <summary>Generate steam hiss procedural (white noise band-pass).</summary>
    private AudioClip BuatClipSteamHiss(float durasi, int sampleRate)
    {
        int total = Mathf.CeilToInt(durasi * sampleRate);
        float[] data = new float[total];
        var rnd = new System.Random(7);
        float lp = 0f, hp = 0f;
        for (int i = 0; i < total; i++)
        {
            float n = (float)(rnd.NextDouble() * 2 - 1);
            lp = lp + 0.45f * (n - lp); // soft low pass
            hp = n - lp;                // high pass
            data[i] = (hp * 0.55f + lp * 0.18f) * 0.6f;
        }
        // Crossfade
        int fadeLen = Mathf.Min(2200, total / 30);
        for (int i = 0; i < fadeLen; i++)
        {
            float f = (float)i / fadeLen;
            data[i] *= f;
            data[total - 1 - i] *= f;
        }
        var clip = AudioClip.Create("ProcSteamHiss", total, 1, sampleRate, false);
        clip.SetData(data, 0);
        return clip;
    }
}
