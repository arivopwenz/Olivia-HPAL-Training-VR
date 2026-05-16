using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System.Collections;
using UnityEngine.SceneManagement;

/// <summary>
/// OLIVIA VR - LoadingScreenManager.cs
/// 
/// Mengelola tampilan Loading Screen antar level.
/// Pasang skrip ini ke Canvas "LoadingScreen" yang ada di scene
/// LoadingScreen (atau sebagai GameObject persistent DontDestroyOnLoad).
/// 
/// Fitur:
///   - Loading bar progres asli (dari AsyncOperation Unity)
///   - Fade In / Fade Out layar hitam
///   - Teks deskripsi level yang akan dimuat
///   - Tips / Fakta industri HPAL yang berganti otomatis
///   - Nama dan nomor level yang muncul saat loading
/// </summary>
public class LoadingScreenManager : MonoBehaviour
{
    // ============================================================
    //  SINGLETON
    // ============================================================
    public static LoadingScreenManager Instance { get; private set; }

    // ============================================================
    //  UI REFERENCES
    // ============================================================
    [Header("=== Panel Utama ===")]
    public GameObject panelLoading;         // Panel fullscreen hitam
    public CanvasGroup canvasGroup;         // Untuk efek Fade

    [Header("=== Header Level ===")]
    public TextMeshProUGUI txtNomorLevel;   // Contoh: "LEVEL 3"
    public TextMeshProUGUI txtNamaLevel;    // Contoh: "ORE TO SLURRY PROCESS"
    public TextMeshProUGUI txtDeskripsiLevel; // Deskripsi singkat level

    [Header("=== Loading Bar ===")]
    public Slider sliderProgress;           // Slider sebagai progress bar
    public Image fillBar;                   // Image fill untuk warna animasi
    public TextMeshProUGUI txtPersen;       // Contoh: "Loading... 75%"

    [Header("=== Tips Panel ===")]
    public TextMeshProUGUI txtTips;         // Tips/fakta industri
    public TextMeshProUGUI txtTipsLabel;    // Label "TAHUKAH KAMU?"

    [Header("=== Branding ===")]
    public TextMeshProUGUI txtBranding;     // "OLIVIA VR Simulator"
    public Image imgLogoOlivia;

    [Header("=== Warna ===")]
    public Color warnaPrimary  = new Color(0.05f, 0.15f, 0.3f);
    public Color warnaAccent   = new Color(0.2f, 0.7f, 1f);
    public Color warnaSuccess  = new Color(0.2f, 0.9f, 0.4f);

    // ============================================================
    //  TIPS DATABASE (Fakta HPAL untuk loading screen)
    // ============================================================
    private readonly string[] _tipsList = {
        "💡 HPAL (High Pressure Acid Leach) menggunakan asam sulfat pada suhu 250°C untuk mengekstrak nikel dari bijih laterit.",
        "⚠️ Tekanan dalam reaktor autoclave bisa mencapai 50 atm — setara dengan beban 500 kg di setiap cm² permukaan.",
        "🌡️ Menjaga suhu di kisaran 248-252°C adalah kunci efisiensi ekstraksi nikel yang optimal.",
        "🧪 H₂SO₄ (Asam Sulfat) yang digunakan dalam proses HPAL bersifat sangat korosif — selalu pastikan APD lengkap sebelum memasuki area kimia.",
        "💧 Proses CCD (Counter Current Decantation) memisahkan larutan PLS kaya nikel dari padatan tailing.",
        "🔩 Katup isolasi (Isolation Valve) adalah garis pertahanan terakhir jika terjadi kegagalan sistem otomatis.",
        "📊 Target efisiensi produksi nikel dalam proses HPAL modern dapat mencapai lebih dari 95%.",
        "🏭 Mixed Hydroxide Precipitate (MHP) adalah produk akhir proses HPAL yang kemudian diolah menjadi bahan baterai kendaraan listrik.",
        "🌍 Indonesia memiliki cadangan nikel terbesar di dunia — lebih dari 21 juta ton nikel terkandung.",
        "🔐 Emergency Shutdown (ESD) adalah prosedur wajib yang harus dikuasai setiap operator HPAL.",
    };

    // ============================================================
    //  DATA LEVEL
    // ============================================================
    private readonly string[] _namaLevel = {
        "ORIENTASI & TUTORIAL",
        "PERSIAPAN APD",
        "INISIALISASI SISTEM DCS",
        "PROSES ORE KE SLURRY",
        "SLURRY PUMP & TRANSFER",
        "PEMANASAN AWAL (PRE-HEATING)",
        "INJEKSI ASAM SULFAT",
        "OPERASI AUTOCLAVE",
        "MONITORING KETAT",
        "FLASH VESSEL & DEPRESSURIZATION",
        "SEPARASI CCD",
        "PRESIPITASI MHP",
        "PEMBUANGAN TAILING",
        "PENYELESAIAN OPERASI",
        "⚠ SKENARIO DARURAT (ESD)"
    };

    private readonly string[] _deskripsiLevel = {
        "Pelajari lingkungan plant dan kontrol dasar operator.",
        "Pastikan 7 APD wajib sudah terpasang sebelum memasuki area.",
        "Aktifkan sistem DCS dan lakukan inisialisasi parameter awal.",
        "Mulai proses pencampuran bijih nikel dengan air menjadi slurry.",
        "Aktifkan pompa slurry dan pastikan flow rate mencapai 450 m³/jam.",
        "Buka katup steam untuk memanaskan slurry hingga 190°C.",
        "Injeksikan H₂SO₄ secara terkontrol ke dalam aliran slurry.",
        "Reaktor autoclave beroperasi. Pantau suhu 250°C dan tekanan 47 atm.",
        "Lakukan monitoring ketat selama proses leaching berlangsung.",
        "Turunkan tekanan melalui flash vessel secara bertahap.",
        "Pisahkan PLS dari tailing menggunakan proses CCD.",
        "Presipitasikan nikel dan kobalt menjadi MHP.",
        "Kelola dan buang tailing sesuai prosedur lingkungan.",
        "Selesaikan operasi dan buat laporan shift.",
        "Tangani kondisi darurat. Tekan ESD segera!",
    };

    // ============================================================
    //  INTERNAL STATE
    // ============================================================
    private float _fadeDuration = 1.15f;
    private int _tipsIndex = 0;
    private Coroutine _tipsCoroutine;

    // ============================================================
    //  LIFECYCLE
    // ============================================================
    void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;
        DontDestroyOnLoad(gameObject);

        // Sembunyikan loading screen di awal
        if (panelLoading != null) panelLoading.SetActive(false);
        if (canvasGroup != null) canvasGroup.alpha = 0f;
    }

    // ============================================================
    //  API PUBLIK: Dipanggil dari LevelLoader
    // ============================================================

    /// <summary>
    /// Tampilkan loading screen dan mulai load scene secara async.
    /// </summary>
    public void LoadLevel(int levelIndex, string sceneName)
    {
        StartCoroutine(SequenceLoading(levelIndex, sceneName));
    }

    // ============================================================
    //  COROUTINE UTAMA: Urutan Loading
    // ============================================================
    private IEnumerator SequenceLoading(int levelIndex, string sceneName)
    {
        // 1. Tampilkan Panel Loading
        if (panelLoading != null) panelLoading.SetActive(true);

        // 2. Fade IN (layar muncul dari transparan ke penuh)
        yield return StartCoroutine(Fade(0f, 1f));

        // 3. Set konten level yang akan dimuat
        SetKontenLevel(levelIndex);

        // 4. Mulai animasi tips berganti
        _tipsCoroutine = StartCoroutine(GantiTipsOtomatis());

        // 5. Reset progress bar ke 0
        SetProgress(0f);

        // 6. Mulai loading scene secara ASYNC (ini yang NYATA)
        AsyncOperation asyncLoad = SceneManager.LoadSceneAsync(sceneName);
        asyncLoad.allowSceneActivation = false; // Tahan dulu di 90%

        // 7. Update progress bar dari data ASLI Unity
        while (!asyncLoad.isDone)
        {
            // Unity progress 0-0.9 = loading, 0.9-1.0 = activating
            float progressNyata = Mathf.Clamp01(asyncLoad.progress / 0.9f);
            SetProgress(progressNyata);

            // Jika loading sudah 90% (siap), beri jeda sebentar agar pemain sempat membaca
            if (asyncLoad.progress >= 0.9f)
            {
                SetProgress(1f);
                yield return new WaitForSeconds(2.2f); // Jeda agar transisi terasa lebih natural
                asyncLoad.allowSceneActivation = true;  // Sekarang benar-benar pindah scene
            }

            yield return null;
        }

        // 8. Hentikan animasi tips
        if (_tipsCoroutine != null) StopCoroutine(_tipsCoroutine);

        // 9. Fade OUT (layar menghilang, scene baru terlihat)
        yield return StartCoroutine(Fade(1f, 0f));

        // 10. Sembunyikan panel
        if (panelLoading != null) panelLoading.SetActive(false);
    }

    // ============================================================
    //  HELPERS: Set Konten UI
    // ============================================================
    private void SetKontenLevel(int index)
    {
        if (index < 0 || index >= _namaLevel.Length) return;

        if (txtNomorLevel != null)
            txtNomorLevel.text = $"LEVEL {index}";

        if (txtNamaLevel != null)
            txtNamaLevel.text = _namaLevel[index];

        if (txtDeskripsiLevel != null)
            txtDeskripsiLevel.text = _deskripsiLevel[index];

        // Acak tips awal
        _tipsIndex = Random.Range(0, _tipsList.Length);
        if (txtTips != null) txtTips.text = _tipsList[_tipsIndex];
    }

    private void SetProgress(float value)
    {
        if (sliderProgress != null)
            sliderProgress.value = value;

        if (txtPersen != null)
            txtPersen.text = value >= 1f ? "Siap Masuk..." : $"Memuat... {Mathf.RoundToInt(value * 100f)}%";

        // Ubah warna bar saat mendekati selesai
        if (fillBar != null)
            fillBar.color = Color.Lerp(warnaAccent, warnaSuccess, value);
    }

    // ============================================================
    //  COROUTINE: Ganti Tips Otomatis
    // ============================================================
    private IEnumerator GantiTipsOtomatis()
    {
        while (true)
        {
            yield return new WaitForSeconds(4f);
            _tipsIndex = (_tipsIndex + 1) % _tipsList.Length;
            if (txtTips != null) txtTips.text = _tipsList[_tipsIndex];
        }
    }

    // ============================================================
    //  COROUTINE: Fade In / Fade Out
    // ============================================================
    private IEnumerator Fade(float from, float to)
    {
        if (canvasGroup == null) yield break;

        float elapsed = 0f;
        canvasGroup.alpha = from;

        while (elapsed < _fadeDuration)
        {
            elapsed += Time.deltaTime;
            canvasGroup.alpha = Mathf.Lerp(from, to, elapsed / _fadeDuration);
            yield return null;
        }

        canvasGroup.alpha = to;
    }
}
