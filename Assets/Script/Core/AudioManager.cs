using UnityEngine;

/// <summary>
/// AudioManager — kelola semua audio (BGM, SFX, ambient) dengan spatial audio support.
/// Singleton, persist antar scene.
/// </summary>
public class AudioManager : MonoBehaviour
{
    public static AudioManager Instance { get; private set; }

    [Header("Audio Sources")]
    [SerializeField] private AudioSource bgmSource;       // Background music
    [SerializeField] private AudioSource sfxSource;        // Sound effects (2D)
    [SerializeField] private AudioSource ambientSource;    // Ambient sounds

    [Header("Volume Settings")]
    [Range(0f, 1f)] public float masterVolume = 1f;
    [Range(0f, 1f)] public float bgmVolume = 0.5f;
    [Range(0f, 1f)] public float sfxVolume = 1f;
    [Range(0f, 1f)] public float ambientVolume = 0.7f;

    [Header("Audio Clips — BGM")]
    public AudioClip bgmControlRoom;    // BGM ruang kontrol (tenang)
    public AudioClip bgmPlantFloor;     // BGM lantai pabrik (industrial)
    public AudioClip bgmEmergency;      // BGM darurat (tegang)
    public AudioClip bgmResult;         // BGM hasil (calm)

    [Header("Audio Clips — SFX")]
    public AudioClip sfxButtonClick;    // Klik tombol
    public AudioClip sfxAlarm;          // Sirine alarm
    public AudioClip sfxValveTurn;      // Suara putar katup
    public AudioClip sfxGlassBreak;     // Suara kaca pecah (ESD)
    public AudioClip sfxExplosion;      // Ledakan (gagal)
    public AudioClip sfxSuccess;        // Suara berhasil
    public AudioClip sfxHeartbeat;      // Detak jantung (darurat)

    [Header("Audio Clips — Ambient")]
    public AudioClip ambientControlRoom;  // AC, beep monitor
    public AudioClip ambientFactory;      // Mesin berderu, pipa bergetar

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;
        DontDestroyOnLoad(gameObject);

        // Auto-create audio sources jika belum ada
        if (bgmSource == null)
        {
            bgmSource = gameObject.AddComponent<AudioSource>();
            bgmSource.loop = true;
            bgmSource.playOnAwake = false;
            bgmSource.spatialBlend = 0f; // 2D
        }

        if (sfxSource == null)
        {
            sfxSource = gameObject.AddComponent<AudioSource>();
            sfxSource.loop = false;
            sfxSource.playOnAwake = false;
            sfxSource.spatialBlend = 0f; // 2D
        }

        if (ambientSource == null)
        {
            ambientSource = gameObject.AddComponent<AudioSource>();
            ambientSource.loop = true;
            ambientSource.playOnAwake = false;
            ambientSource.spatialBlend = 0f; // 2D
        }
    }

    private void Update()
    {
        // Update volume real-time
        bgmSource.volume = bgmVolume * masterVolume;
        sfxSource.volume = sfxVolume * masterVolume;
        ambientSource.volume = ambientVolume * masterVolume;
    }

    // ========== BGM ==========

    /// <summary>
    /// Play background music dengan fade.
    /// </summary>
    public void PlayBGM(AudioClip clip)
    {
        if (clip == null) return;
        bgmSource.clip = clip;
        bgmSource.Play();
        Debug.Log($"[AudioManager] Playing BGM: {clip.name}");
    }

    /// <summary>
    /// Stop background music.
    /// </summary>
    public void StopBGM()
    {
        bgmSource.Stop();
    }

    // ========== SFX ==========

    /// <summary>
    /// Play sound effect sekali (one-shot, 2D).
    /// </summary>
    public void PlaySFX(AudioClip clip)
    {
        if (clip == null) return;
        sfxSource.PlayOneShot(clip, sfxVolume * masterVolume);
    }

    /// <summary>
    /// Play sound effect di posisi tertentu (3D spatial audio).
    /// Ideal untuk VR — suara datang dari arah sumber.
    /// </summary>
    public void PlaySFXAtPosition(AudioClip clip, Vector3 position)
    {
        if (clip == null) return;
        AudioSource.PlayClipAtPoint(clip, position, sfxVolume * masterVolume);
    }

    // ========== AMBIENT ==========

    /// <summary>
    /// Play ambient sound (loop).
    /// </summary>
    public void PlayAmbient(AudioClip clip)
    {
        if (clip == null) return;
        ambientSource.clip = clip;
        ambientSource.Play();
        Debug.Log($"[AudioManager] Playing Ambient: {clip.name}");
    }

    /// <summary>
    /// Stop ambient sound.
    /// </summary>
    public void StopAmbient()
    {
        ambientSource.Stop();
    }

    // ========== SHORTCUTS ==========

    /// <summary>
    /// Setup audio berdasarkan fase game saat ini.
    /// Dipanggil otomatis saat ganti fase.
    /// </summary>
    public void SetAudioForPhase(GameManager.GamePhase phase)
    {
        StopBGM();
        StopAmbient();

        switch (phase)
        {
            case GameManager.GamePhase.ControlRoom:
                PlayBGM(bgmControlRoom);
                PlayAmbient(ambientControlRoom);
                break;
            case GameManager.GamePhase.PlantFloor:
            case GameManager.GamePhase.APDCheck:
                PlayBGM(bgmPlantFloor);
                PlayAmbient(ambientFactory);
                break;
            case GameManager.GamePhase.Emergency:
                PlayBGM(bgmEmergency);
                PlayAmbient(ambientFactory);
                break;
            case GameManager.GamePhase.Result:
                PlayBGM(bgmResult);
                break;
        }
    }
}
