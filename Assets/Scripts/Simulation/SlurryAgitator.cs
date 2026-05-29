using UnityEngine;

/// <summary>
/// OLIVIA VR - SlurryAgitator.cs
///
/// Mengontrol rotasi pengaduk slurry tank (bentuk + / cross dengan tiang vertikal).
/// Animasi:
///   - Aktif: pengaduk muter di sumbu Y dengan kecepatan deg/s
///   - Permukaan slurry mendapat ripple distortion proporsional
///   - Audio motor mengaduk (synthesized) loop saat aktif
///
/// Pemakaian: pasang di GameObject Pengaduk (parent untuk shaft + blade), assign Slurry_Fill renderer untuk efek ripple opsional.
/// </summary>
[RequireComponent(typeof(AudioSource))]
public class SlurryAgitator : MonoBehaviour
{
    [Header("=== Rotasi ===")]
    [Tooltip("Kecepatan putar (derajat per detik). Negatif = arah berlawanan.")]
    [SerializeField] private float _rpmDeg = 45f;
    [Tooltip("Sumbu rotasi lokal. Default Y (vertikal).")]
    [SerializeField] private Vector3 _sumbuRotasi = Vector3.up;
    [Tooltip("Akselerasi/deselerasi saat start/stop (deg/s²). Pelan supaya ramp-up natural.")]
    [SerializeField] private float _akselerasi = 8f;

    [Header("=== Audio Motor ===")]
    [Range(0f, 1f)] [SerializeField] private float _volume = 0.35f;
    [Range(0.5f, 2f)] [SerializeField] private float _pitch = 1.0f;

    [Header("=== Status (Read Only) ===")]
    [SerializeField] private bool _aktif;
    [SerializeField] private float _kecepatanSekarang;

    private AudioSource _audio;
    private AudioClip _clipMotor;

    public bool Aktif => _aktif;

    private void Awake()
    {
        SiapkanAudio();
    }

    private void SiapkanAudio()
    {
        _audio = GetComponent<AudioSource>();
        _audio.playOnAwake = false;
        _audio.loop = true;
        _audio.spatialBlend = 1f;
        _audio.volume = _volume;
        _audio.pitch = _pitch;
        _audio.maxDistance = 25f;
        _audio.rolloffMode = AudioRolloffMode.Linear;

        if (_clipMotor == null)
            _clipMotor = BuatClipMotor(durasi: 4f, sampleRate: 22050);

        if (_audio.clip == null)
            _audio.clip = _clipMotor;
    }

    public void Mulai()
    {
        SiapkanAudio();
        _aktif = true;
        if (_audio != null && !_audio.isPlaying)
            _audio.Play();
    }

    public void Hentikan()
    {
        _aktif = false;
        // Audio akan stop saat _kecepatanSekarang turun ke 0 lewat Update.
    }

    private void Update()
    {
        float target = _aktif ? _rpmDeg : 0f;
        if (_akselerasi <= 0f)
            _kecepatanSekarang = target;
        else
            _kecepatanSekarang = Mathf.MoveTowards(_kecepatanSekarang, target, _akselerasi * Time.deltaTime);

        if (Mathf.Abs(_kecepatanSekarang) > 0.01f)
        {
            transform.Rotate(_sumbuRotasi.normalized, _kecepatanSekarang * Time.deltaTime, Space.Self);
        }
        else if (_audio != null && _audio.isPlaying)
        {
            _audio.Stop();
        }

        // Update audio params dinamis (pitch ikut kecepatan)
        if (_audio != null && _audio.isPlaying)
        {
            float t = Mathf.Abs(_kecepatanSekarang) / Mathf.Max(1f, Mathf.Abs(_rpmDeg));
            _audio.pitch = Mathf.Lerp(0.6f, _pitch, t);
            _audio.volume = _volume * Mathf.Clamp01(t);
        }
    }

    /// <summary>
    /// Generate clip motor: low-frequency hum + sedikit harmonik.
    /// </summary>
    private AudioClip BuatClipMotor(float durasi, int sampleRate)
    {
        int totalSamples = Mathf.CeilToInt(durasi * sampleRate);
        float[] data = new float[totalSamples];
        float phase1 = 0f;
        float phase2 = 0f;
        System.Random rnd = new System.Random(123);

        for (int i = 0; i < totalSamples; i++)
        {
            // Hum 60Hz fundamental + 120Hz harmonik
            phase1 += 2f * Mathf.PI * 60f / sampleRate;
            phase2 += 2f * Mathf.PI * 120f / sampleRate;
            float hum = Mathf.Sin(phase1) * 0.4f + Mathf.Sin(phase2) * 0.2f;
            // Tiny noise (vibration mekanis)
            float noise = ((float)rnd.NextDouble() - 0.5f) * 0.15f;
            data[i] = hum + noise;
        }

        // Smooth loop edges
        int fadeLen = Mathf.Min(2000, totalSamples / 20);
        for (int i = 0; i < fadeLen; i++)
        {
            float fade = (float)i / fadeLen;
            data[i] *= fade;
            data[totalSamples - 1 - i] *= fade;
        }

        AudioClip clip = AudioClip.Create("ProcMotor", totalSamples, 1, sampleRate, false);
        clip.SetData(data, 0);
        return clip;
    }

#if UNITY_EDITOR
    [ContextMenu("DEBUG: Mulai")]
    private void D_Mulai() => Mulai();

    [ContextMenu("DEBUG: Hentikan")]
    private void D_Hentikan() => Hentikan();
#endif
}
