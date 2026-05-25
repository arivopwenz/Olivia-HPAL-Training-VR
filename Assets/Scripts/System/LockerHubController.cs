using UnityEngine;
using System.Collections;

/// <summary>
/// OLIVIA VR - LockerHubController.cs (v7.0 - Walkie Talkie PTT exit)
///
/// FLOW BARU keluar loker:
///   1. Player pakai 8 APD lengkap → OnAPD7Lengkap fire
///   2. HUD prompt: "Tekan T untuk cek Walkie Talkie"
///   3. Player tekan T (PTT) → release → OnPTTDilepas fire
///   4. Tunggu ~2 detik untuk voice balasan NPC
///   5. Fade out screen (3 detik smooth)
///   6. Teleport ke SpawnPoint_DCS via LevelTeleportManager
///   7. SelesaikanLevel(Level1) → memicu transisi ke Level 2
///   8. Fade in screen
///
/// PintuTrigger sudah TIDAK dipakai untuk transisi ini.
/// </summary>
public class LockerHubController : MonoBehaviour
{
    [Header("=== Audio ===")]
    [Tooltip("Audio balasan NPC saat player cek HT (mis. 'Roger, lanjut ke ruang DCS').")]
    [SerializeField] private AudioClip _audioBalasanNPC;
    [SerializeField] private AudioSource _audioSource;
    [Range(0f, 1f)] [SerializeField] private float _volumeBalasan = 0.85f;

    [Header("=== Timing ===")]
    [Tooltip("Jeda setelah player lepas T sebelum voice balasan diputar.")]
    [SerializeField] private float _jedaSebelumBalasan = 0.5f;
    [Tooltip("Durasi voice balasan / jeda total sebelum fade out (gunakan kalau audio tidak di-assign).")]
    [SerializeField] private float _durasiVoiceBalasan = 2.5f;
    [Tooltip("Durasi fade out screen sebelum teleport.")]
    [SerializeField] private float _durasiFadeOut = 2.5f;
    [Tooltip("Durasi fade in screen setelah teleport.")]
    [SerializeField] private float _durasiFadeIn = 2.0f;
    [Tooltip("Jeda di tengah black screen sebelum teleport (supaya fade benar-benar full).")]
    [SerializeField] private float _jedaSaatBlackScreen = 0.5f;

    [Header("=== HUD ===")]
    [TextArea(2, 4)]
    [SerializeField] private string _pesanCekHT =
        "APD lengkap! Tahan T untuk cek Walkie Talkie sebelum berangkat ke ruang DCS.";
    [TextArea(2, 4)]
    [SerializeField] private string _pesanBalasan =
        "DCS, terdengar jelas. Silakan menuju ruang kontrol.";

    private PhaseManager _phaseManager;
    private PlayerHUD _hud;
    private LevelTeleportManager _teleportManager;
    private bool _apdLengkap;
    private bool _sedangProsesKeluar;

    private void Start()
    {
        _phaseManager = Object.FindAnyObjectByType<PhaseManager>();
        _hud = Object.FindAnyObjectByType<PlayerHUD>();
        _teleportManager = Object.FindAnyObjectByType<LevelTeleportManager>();
        _apdLengkap = _phaseManager != null && _phaseManager.APDLengkapSempurna;

        if (_audioSource == null)
        {
            _audioSource = GetComponent<AudioSource>();
            if (_audioSource == null) _audioSource = gameObject.AddComponent<AudioSource>();
        }
        _audioSource.spatialBlend = 0f;
        _audioSource.playOnAwake = false;
        _audioSource.volume = _volumeBalasan;
    }

    private void OnEnable()
    {
        PhaseManager.OnAPD7Lengkap += OnApdLengkap;
        GameLevelManager.OnVoiceReportAccepted += OnVoiceReportAccepted;
    }

    private void OnDisable()
    {
        PhaseManager.OnAPD7Lengkap -= OnApdLengkap;
        GameLevelManager.OnVoiceReportAccepted -= OnVoiceReportAccepted;
    }

    private void OnApdLengkap()
    {
        if (GameLevelManager.Instance == null) return;
        if (GameLevelManager.Instance.CurrentLevel != GameLevelManager.GameLevel.Level1_APD) return;

        _apdLengkap = true;
        Debug.Log("[LockerHub] ✓ APD lengkap. Player diminta cek HT (tahan T).");
        _hud?.ShowNotifPublic(_pesanCekHT);
    }

    private void OnVoiceReportAccepted(string keyword)
    {
        bool apdLengkapSekarang = _apdLengkap || (PhaseManager.Instance != null && PhaseManager.Instance.APDLengkapSempurna);
        if (!apdLengkapSekarang || _sedangProsesKeluar) return;
        if (GameLevelManager.Instance == null) return;
        if (GameLevelManager.Instance.CurrentLevel != GameLevelManager.GameLevel.Level1_APD) return;

        _apdLengkap = true;
        _sedangProsesKeluar = true;
        StartCoroutine(SequenceKeluarLoker());
    }

    /// <summary>
    /// Sequence: jeda → voice balasan → fade out → teleport → SelesaikanLevel → fade in.
    /// </summary>
    private IEnumerator SequenceKeluarLoker()
    {
        Debug.Log("[LockerHub] Sequence keluar loker dimulai (jeda → voice → fade → teleport).");

        // 1. Jeda kecil sebelum voice balasan
        yield return new WaitForSeconds(_jedaSebelumBalasan);

        // 2. Putar voice balasan NPC
        if (_audioBalasanNPC != null && _audioSource != null)
        {
            _audioSource.PlayOneShot(_audioBalasanNPC, _volumeBalasan);
            _hud?.ShowNotifPublic(_pesanBalasan);
            yield return new WaitForSeconds(Mathf.Max(_audioBalasanNPC.length, _durasiVoiceBalasan));
        }
        else
        {
            _hud?.ShowNotifPublic(_pesanBalasan);
            yield return new WaitForSeconds(_durasiVoiceBalasan);
        }

        // 3. Fade out screen pelan
        Debug.Log("[LockerHub] Fade out screen.");
        yield return StartCoroutine(FadeOverlay(0f, 1f, _durasiFadeOut));

        // 4. Jeda di black screen (supaya teleport tidak terlihat instant)
        yield return new WaitForSeconds(_jedaSaatBlackScreen);

        // 5. Teleport via LevelTeleportManager (target Level 2 spawn config = SpawnPoint_DCS)
        if (_teleportManager != null)
        {
            _teleportManager.TeleportKeLevel(GameLevelManager.GameLevel.Level2_DCSPrep);
            Debug.Log("[LockerHub] Teleport ke SpawnPoint_DCS via LevelTeleportManager.");
        }
        else
        {
            Debug.LogWarning("[LockerHub] LevelTeleportManager null, teleport gagal.");
        }

        // 6. Selesaikan Level 1 (akan memicu MulaiLevel(Level2) setelah delay default GLM)
        GameLevelManager.Instance.SelesaikanLevel(GameLevelManager.GameLevel.Level1_APD);

        // 7. Notify HUD untuk geser fase (UI lock-in)
        _hud?.NotifyMasukPintu();

        // 8. Fade in screen
        Debug.Log("[LockerHub] Fade in screen.");
        yield return StartCoroutine(FadeOverlay(1f, 0f, _durasiFadeIn));

        Debug.Log("[LockerHub] Sequence keluar loker selesai.");
    }

    /// <summary>
    /// Smooth fade overlay menggunakan PlayerHUD's _transitionOverlay (auto-create kalau null).
    /// </summary>
    private IEnumerator FadeOverlay(float fromAlpha, float toAlpha, float duration)
    {
        var overlay = GetOrCreateFadeOverlay();
        if (overlay == null)
        {
            yield return new WaitForSeconds(duration);
            yield break;
        }

        overlay.gameObject.SetActive(true);
        var c = overlay.color;
        c.a = fromAlpha;
        overlay.color = c;

        float elapsed = 0f;
        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            c.a = Mathf.Lerp(fromAlpha, toAlpha, Mathf.Clamp01(elapsed / duration));
            overlay.color = c;
            yield return null;
        }

        c.a = toAlpha;
        overlay.color = c;
        if (toAlpha <= 0.01f) overlay.gameObject.SetActive(false);
    }

    /// <summary>
    /// Cari overlay fade. Coba pakai PlayerHUD reflection dulu, kalau tidak ada buat sendiri.
    /// </summary>
    private UnityEngine.UI.Image _ownFadeOverlay;
    private UnityEngine.UI.Image GetOrCreateFadeOverlay()
    {
        // Reuse existing one if any
        if (_ownFadeOverlay != null) return _ownFadeOverlay;

        // Try to use PlayerHUD's overlay via reflection
        if (_hud != null)
        {
            var f = typeof(PlayerHUD).GetField("_transitionOverlay",
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            if (f != null)
            {
                var existing = f.GetValue(_hud) as UnityEngine.UI.Image;
                if (existing != null)
                {
                    _ownFadeOverlay = existing;
                    return _ownFadeOverlay;
                }
            }
        }

        // Fallback: create our own overlay on a dedicated canvas (sortingOrder tinggi)
        var canvasGo = new GameObject("LockerHubFadeCanvas");
        var canvas = canvasGo.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 9999;
        canvasGo.AddComponent<UnityEngine.UI.CanvasScaler>();

        var overlayGo = new GameObject("FadeOverlay", typeof(RectTransform));
        overlayGo.transform.SetParent(canvasGo.transform, false);
        var rect = overlayGo.GetComponent<RectTransform>();
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;
        var img = overlayGo.AddComponent<UnityEngine.UI.Image>();
        img.color = new Color(0f, 0f, 0f, 0f);
        overlayGo.SetActive(false);

        _ownFadeOverlay = img;
        return _ownFadeOverlay;
    }
}
