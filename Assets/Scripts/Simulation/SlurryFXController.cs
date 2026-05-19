using UnityEngine;

/// <summary>
/// OLIVIA VR - SlurryFXController.cs
///
/// Visual + audio FX khusus saat slurry tank diisi di Level 3:
///   - Bubble particle naik dari permukaan slurry sambil fill animasi
///   - Audio "splash/whoosh" yang di-generate prosedural (tidak butuh asset audio)
///   - Optional: ripple/swirl di permukaan
///
/// Pemakaian:
///   1. Pasang script ini di GameObject Slurry_Fill (atau parent Slurry Tank).
///   2. Atau biarkan Level3OreSlurryController membuat instance otomatis lewat helper.
/// </summary>
[RequireComponent(typeof(AudioSource))]
public class SlurryFXController : MonoBehaviour
{
    [Header("=== Audio (Procedural Splash) ===")]
    [Tooltip("Volume saat slurry mulai mengisi.")]
    [Range(0f, 1f)] [SerializeField] private float _volumeSplashAwal = 0.65f;
    [Tooltip("Volume looping bubbling saat slurry mengisi.")]
    [Range(0f, 1f)] [SerializeField] private float _volumeBubbling = 0.30f;
    [Tooltip("Pitch bubbling.")]
    [Range(0.4f, 1.5f)] [SerializeField] private float _pitchBubbling = 0.85f;

    [Header("=== Bubble Particle ===")]
    [SerializeField] private bool _aktifkanBubble = true;
    [SerializeField] private Color _warnaBubble = new Color(0.55f, 0.4f, 0.2f, 0.6f);
    [SerializeField] [Range(1f, 200f)] private float _emisiPerDetik = 35f;
    [Tooltip("Tinggi maksimum bubble naik dari permukaan (m).")]
    [SerializeField] private float _tinggiNaikBubble = 1.2f;
    [Tooltip("Radius spawn bubble di permukaan (m).")]
    [SerializeField] private float _radiusSpawn = 1.8f;

    private AudioSource _audioSource;
    private ParticleSystem _bubbleParticle;
    private AudioClip _clipSplashAwal;
    private AudioClip _clipBubbling;

    private void Awake()
    {
        _audioSource = GetComponent<AudioSource>();
        _audioSource.playOnAwake = false;
        _audioSource.spatialBlend = 1f; // 3D
        _audioSource.maxDistance = 30f;
        _audioSource.rolloffMode = AudioRolloffMode.Linear;

        _clipSplashAwal = BuatClipSplash(durasi: 1.4f, freqAwal: 320f, freqAkhir: 80f, sampleRate: 22050);
        _clipBubbling = BuatClipBubbling(durasi: 4f, sampleRate: 22050);

        if (_aktifkanBubble)
            _bubbleParticle = BuatBubbleParticle();
    }

    /// <summary>
    /// Mulai FX saat slurry mulai diisi. Mainkan splash sekali + looping bubbling + emit bubbles.
    /// </summary>
    public void MulaiFx()
    {
        if (_audioSource != null && _clipSplashAwal != null)
            _audioSource.PlayOneShot(_clipSplashAwal, _volumeSplashAwal);

        if (_audioSource != null && _clipBubbling != null)
        {
            _audioSource.clip = _clipBubbling;
            _audioSource.loop = true;
            _audioSource.volume = _volumeBubbling;
            _audioSource.pitch = _pitchBubbling;
            _audioSource.Play();
        }

        if (_bubbleParticle != null)
        {
            var emission = _bubbleParticle.emission;
            emission.rateOverTime = _emisiPerDetik;
            _bubbleParticle.Play(true);
        }
    }

    /// <summary>
    /// Hentikan FX saat slurry mencapai 25%.
    /// </summary>
    public void HentikanFx()
    {
        if (_audioSource != null && _audioSource.isPlaying)
        {
            _audioSource.loop = false;
            _audioSource.Stop();
        }

        if (_bubbleParticle != null)
            _bubbleParticle.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
    }

    /// <summary>
    /// Pindahkan emitter bubble ke posisi permukaan slurry (di-call setiap frame oleh controller).
    /// </summary>
    public void UpdatePosisiPermukaan(Vector3 worldPosPermukaan)
    {
        if (_bubbleParticle != null)
            _bubbleParticle.transform.position = worldPosPermukaan;
    }

    private ParticleSystem BuatBubbleParticle()
    {
        var go = new GameObject("Slurry_Bubbles_FX");
        go.transform.SetParent(transform, false);
        go.transform.localPosition = Vector3.zero;

        var ps = go.AddComponent<ParticleSystem>();
        var main = ps.main;
        main.duration = 5f;
        main.loop = true;
        main.startLifetime = new ParticleSystem.MinMaxCurve(1.5f, 3.5f);
        main.startSpeed = new ParticleSystem.MinMaxCurve(0.4f, 1.1f);
        main.startSize = new ParticleSystem.MinMaxCurve(0.06f, 0.18f);
        main.startColor = _warnaBubble;
        main.gravityModifier = -0.3f; // negatif = naik
        main.simulationSpace = ParticleSystemSimulationSpace.World;
        main.maxParticles = 200;

        var emission = ps.emission;
        emission.enabled = true;
        emission.rateOverTime = 0f; // mulai 0, di-set saat MulaiFx

        var shape = ps.shape;
        shape.enabled = true;
        shape.shapeType = ParticleSystemShapeType.Circle;
        shape.radius = _radiusSpawn;
        shape.rotation = new Vector3(90f, 0f, 0f); // hadap atas

        var velocityOverLifetime = ps.velocityOverLifetime;
        velocityOverLifetime.enabled = true;
        velocityOverLifetime.space = ParticleSystemSimulationSpace.World;
        velocityOverLifetime.y = new ParticleSystem.MinMaxCurve(0.4f, _tinggiNaikBubble * 0.5f);

        var sizeOverLifetime = ps.sizeOverLifetime;
        sizeOverLifetime.enabled = true;
        AnimationCurve sizeCurve = new AnimationCurve(
            new Keyframe(0f, 0.5f),
            new Keyframe(0.3f, 1f),
            new Keyframe(1f, 0.2f)
        );
        sizeOverLifetime.size = new ParticleSystem.MinMaxCurve(1f, sizeCurve);

        var colorOverLifetime = ps.colorOverLifetime;
        colorOverLifetime.enabled = true;
        Gradient grad = new Gradient();
        grad.SetKeys(
            new GradientColorKey[] {
                new GradientColorKey(_warnaBubble, 0f),
                new GradientColorKey(_warnaBubble, 1f)
            },
            new GradientAlphaKey[] {
                new GradientAlphaKey(0f, 0f),
                new GradientAlphaKey(_warnaBubble.a, 0.2f),
                new GradientAlphaKey(_warnaBubble.a * 0.7f, 0.8f),
                new GradientAlphaKey(0f, 1f)
            }
        );
        colorOverLifetime.color = grad;

        // Renderer dengan default particle material
        var psRenderer = ps.GetComponent<ParticleSystemRenderer>();
        Shader sprShader = Shader.Find("Universal Render Pipeline/Particles/Unlit");
        if (sprShader == null) sprShader = Shader.Find("Particles/Standard Unlit");
        if (sprShader == null) sprShader = Shader.Find("Sprites/Default");
        Material mat = new Material(sprShader);
        mat.color = _warnaBubble;
        // Gunakan tekstur lingkaran default Unity
        Texture2D tex = Resources.GetBuiltinResource<Texture2D>("Default-Particle.psd");
        if (tex != null) mat.mainTexture = tex;
        psRenderer.material = mat;
        psRenderer.renderMode = ParticleSystemRenderMode.Billboard;

        ps.Stop();
        return ps;
    }

    /// <summary>
    /// Generate AudioClip "splash" prosedural — sweep frekuensi dari tinggi ke rendah dengan envelope cepat.
    /// </summary>
    private AudioClip BuatClipSplash(float durasi, float freqAwal, float freqAkhir, int sampleRate)
    {
        int totalSamples = Mathf.CeilToInt(durasi * sampleRate);
        float[] data = new float[totalSamples];
        System.Random rnd = new System.Random(42);
        float phase = 0f;

        for (int i = 0; i < totalSamples; i++)
        {
            float t = (float)i / totalSamples;
            // Frekuensi sweep eksponensial
            float freq = Mathf.Lerp(freqAwal, freqAkhir, t);
            phase += 2f * Mathf.PI * freq / sampleRate;
            // Suara dasar: sine + noise (turbulence air)
            float sine = Mathf.Sin(phase) * 0.4f;
            float noise = ((float)rnd.NextDouble() - 0.5f) * 0.7f;
            // Envelope: attack cepat, decay panjang
            float env = t < 0.05f ? t / 0.05f : Mathf.Exp(-3f * (t - 0.05f));
            data[i] = (sine + noise) * env * 0.7f;
        }

        AudioClip clip = AudioClip.Create("ProcSplash", totalSamples, 1, sampleRate, false);
        clip.SetData(data, 0);
        return clip;
    }

    /// <summary>
    /// Generate AudioClip bubbling looping — noise + low-frequency rumble.
    /// </summary>
    private AudioClip BuatClipBubbling(float durasi, int sampleRate)
    {
        int totalSamples = Mathf.CeilToInt(durasi * sampleRate);
        float[] data = new float[totalSamples];
        System.Random rnd = new System.Random(7);
        float phaseLow = 0f;

        // Low-pass filter state
        float lpPrev = 0f;
        float lpAlpha = 0.05f;

        for (int i = 0; i < totalSamples; i++)
        {
            // Noise
            float noise = ((float)rnd.NextDouble() - 0.5f) * 2f;
            // Low-pass untuk bikin "boom" rendah
            lpPrev = lpPrev + lpAlpha * (noise - lpPrev);

            // Bass rumble 50Hz
            phaseLow += 2f * Mathf.PI * 55f / sampleRate;
            float bass = Mathf.Sin(phaseLow) * 0.3f;

            data[i] = (lpPrev * 0.6f + bass * 0.4f) * 0.5f;
        }

        // Smooth loop edges
        int fadeLen = Mathf.Min(2000, totalSamples / 20);
        for (int i = 0; i < fadeLen; i++)
        {
            float fade = (float)i / fadeLen;
            data[i] *= fade;
            data[totalSamples - 1 - i] *= fade;
        }

        AudioClip clip = AudioClip.Create("ProcBubbling", totalSamples, 1, sampleRate, false);
        clip.SetData(data, 0);
        return clip;
    }
}
