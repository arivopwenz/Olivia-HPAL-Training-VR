using UnityEngine;

/// <summary>
/// OLIVIA VR - ProcessPipeFlowAnimator.cs
///
/// Menganimasikan "aliran" cairan/slurry di dalam pipa proses (inner flow tube hasil Blender).
/// Efek: tekstur slurry di-scroll sepanjang pipa (kesan fluida benar-benar mengalir) + emisi berdenyut.
/// Dipakai untuk pipa CCD->MHP, CCD->Filter Press, dan Slurry Tank->Preheater.
///
/// Cara pakai:
///   - Pasang di GameObject flow tube (mis. SlurryToPreheater_SlurryFlow).
///   - Panggil SetFlowing(true) saat proses mengalir, SetFlowing(false) saat berhenti.
///   - [ExecuteAlways] -> animasi ikut jalan di edit mode (scene view repaint dipaksa).
/// </summary>
[ExecuteAlways]
public class ProcessPipeFlowAnimator : MonoBehaviour
{
    [Tooltip("Renderer flow tube. Kosong = auto-pakai renderer di GameObject ini + anak-anaknya.")]
    [SerializeField] private Renderer[] _flowRenderers;

    [Tooltip("Warna fluida. Diisi otomatis dari material kalau dibiarkan transparan.")]
    [SerializeField] private Color _fluidColor = Color.clear;

    [Tooltip("Kecepatan gelombang emisi yang menjalar di pipa.")]
    [SerializeField] private float _waveSpeed = 1.8f;

    [Tooltip("Kecepatan scroll tekstur slurry sepanjang pipa (kesan mengalir).")]
    [SerializeField] private float _scrollSpeed = 0.5f;

    [Tooltip("Mulai dengan aliran aktif?")]
    [SerializeField] private bool _flowOnStart = false;

    private MaterialPropertyBlock _mpb;
    private bool _flowing;
    private float _phase;
    private float _scroll;
    private bool _initialized;
    private static readonly int IdEmission = Shader.PropertyToID("_EmissionColor");
    private static readonly int IdBaseColor = Shader.PropertyToID("_BaseColor");
    private static readonly int IdBaseMapST = Shader.PropertyToID("_BaseMap_ST");

    private void Awake()
    {
        EnsureInit();
        SetFlowing(_flowOnStart);
    }

    private void OnEnable()
    {
        EnsureInit();
        if (_flowOnStart) SetFlowing(true);
    }

    // Inisialisasi aman, idempotent. Bisa dipanggil dari edit mode (sebelum Awake) tanpa throw.
    private void EnsureInit()
    {
        if (_mpb == null) _mpb = new MaterialPropertyBlock();
        if (_initialized) return;
        if (_flowRenderers == null || _flowRenderers.Length == 0)
            _flowRenderers = GetComponentsInChildren<Renderer>(true);
        if (_flowRenderers == null)
            _flowRenderers = new Renderer[0];
        if (_mpb == null)
            _mpb = new MaterialPropertyBlock();

        if (_fluidColor == Color.clear && _flowRenderers.Length > 0 && _flowRenderers[0] != null && _flowRenderers[0].sharedMaterial != null)
        {
            var m = _flowRenderers[0].sharedMaterial;
            _fluidColor = m.HasProperty(IdBaseColor) ? m.GetColor(IdBaseColor) : m.color;
        }
        _initialized = true;
    }

    /// <summary>Nyalakan/matikan aliran. Saat mati, flow tube disembunyikan. Aman dipanggil kapan saja.</summary>
    public void SetFlowing(bool on)
    {
        EnsureInit();
        _flowing = on;
        foreach (var r in _flowRenderers)
            if (r != null) r.enabled = on;
        if (!on) return;
        foreach (var r in _flowRenderers)
        {
            if (r == null || r.sharedMaterial == null) continue;
            r.sharedMaterial.EnableKeyword("_EMISSION");
        }
    }

    public bool IsFlowing => _flowing;

    private void Update()
    {
        if (!_flowing) return;
        EnsureInit();
        // Di edit mode Time.deltaTime bisa 0; pakai langkah kecil agar tetap bergerak saat repaint.
        float dt = (Application.isPlaying && Time.deltaTime > 0f) ? Time.deltaTime : 0.016f;
        _phase += dt * _waveSpeed;
        _scroll += dt * _scrollSpeed;
        float off = -Mathf.Repeat(_scroll, 1f); // negatif = arah maju ke preheater

        for (int i = 0; i < _flowRenderers.Length; i++)
        {
            var r = _flowRenderers[i];
            if (r == null) continue;
            Color emis = _fluidColor * 0.95f; // emisi konstan, TIDAK berdenyut (no kelap-kelip)
            Vector4 st = (r.sharedMaterial != null && r.sharedMaterial.HasProperty(IdBaseMapST))
                ? r.sharedMaterial.GetVector(IdBaseMapST) : new Vector4(1f, 1f, 0f, 0f);
            r.GetPropertyBlock(_mpb);
            _mpb.SetColor(IdEmission, emis);
            _mpb.SetColor(IdBaseColor, _fluidColor);
            _mpb.SetVector(IdBaseMapST, new Vector4(st.x, st.y, 0f, off));
            r.SetPropertyBlock(_mpb);
        }
#if UNITY_EDITOR
        if (!Application.isPlaying)
            UnityEditor.SceneView.RepaintAll(); // paksa repaint agar animasi mulus di edit mode
#endif
    }
}
