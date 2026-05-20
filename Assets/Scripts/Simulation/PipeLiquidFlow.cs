using UnityEngine;

/// <summary>
/// OLIVIA VR - PipeLiquidFlow.cs
///
/// Visual realistik liquid mengalir di dalam pipa transparent.
/// Inner-liquid GameObject (cylinder lebih kecil dari pipa) di-scale & di-scroll
/// proporsional dengan GameLevelManager.FlowRate.
///
/// Pemakaian: pasang di GameObject pipa (cylinder mesh).
///   - Auto-detect outer pipe renderer + assign Pipe_Transparent material
///   - Auto-create inner cylinder yang scale-nya 0.85x dari outer
///   - Inner cylinder pakai Liquid_Slurry_Inner material + scroll UV
///   - Saat flow rate = 0 → inner liquid mengempis (scale Y → 0)
///   - Saat flow rate naik → inner liquid penuh + texture scroll cepat
/// </summary>
[ExecuteAlways]
public class PipeLiquidFlow : MonoBehaviour
{
    [Header("=== Sumber Flow ===")]
    [SerializeField] private float _flowMaksimum = 600f;
    [SerializeField] private float _flowMinimumAktif = 1f;

    [Header("=== Outer Pipe (transparent) ===")]
    [Tooltip("Material untuk pipa luar (transparent kuning). Auto-load Pipe_Transparent.mat jika kosong.")]
    [SerializeField] private Material _outerPipeMaterial;

    [Header("=== Inner Liquid ===")]
    [Tooltip("Material untuk liquid stream di dalam pipa. Auto-load Liquid_Slurry_Inner.mat.")]
    [SerializeField] private Material _innerLiquidMaterial;
    [Tooltip("Skala diameter inner liquid relatif terhadap outer pipe (0.85 = 85% dari outer).")]
    [Range(0.3f, 0.95f)] [SerializeField] private float _innerDiameterRatio = 0.78f;
    [Tooltip("Skala panjang inner liquid relatif terhadap outer pipe (1.0 = full panjang).")]
    [Range(0.5f, 1.0f)] [SerializeField] private float _innerLengthRatio = 0.96f;

    [Header("=== Scroll Animation ===")]
    [Tooltip("Multiplier kecepatan scroll. Flow 600 m³/h pada multiplier 1 = scroll 1 unit per detik.")]
    [SerializeField] private float _multiplierScroll = 2.5f;
    [Tooltip("Property name texture (URP=_BaseMap, Standard=_MainTex).")]
    [SerializeField] private string _texturePropertyName = "_BaseMap";
    [Tooltip("Smoothing antara nilai flow lama dan baru supaya animasi tidak jumpy.")]
    [SerializeField] private float _smoothFactor = 0.25f;

    [Header("=== Volume Sync ===")]
    [Tooltip("Saat flow 0, inner liquid mengempis ke scale Y minimum ini.")]
    [Range(0f, 0.6f)] [SerializeField] private float _emptyScaleY = 0.05f;
    [Tooltip("Aktifkan supaya saat flow rate rendah, inner liquid juga 'belum penuh'.")]
    [SerializeField] private bool _gunakanFillVolumeBerdasarkanFlow = true;

    private GameObject _innerLiquid;
    private Renderer _innerRenderer;
    private Material _innerMatInstance;
    private Renderer _outerRenderer;
    private MaterialPropertyBlock _innerMpb;
    private float _scrollY;
    private float _flowSmoothed;
    private float _flowVel;
    private Vector3 _outerOriginalScale;

    private void Awake()
    {
        _outerRenderer = GetComponent<Renderer>();
        _outerOriginalScale = transform.localScale;

        if (_outerPipeMaterial == null)
            _outerPipeMaterial = LoadMaterial("Assets/Materials/Color Utama/Pipe_Transparent.mat");
        if (_innerLiquidMaterial == null)
            _innerLiquidMaterial = LoadMaterial("Assets/Materials/Color Utama/Liquid_Slurry_Inner.mat");

        ApplyOuterMaterial();
        BuildInnerLiquid();
        _innerMpb = new MaterialPropertyBlock();
    }

    private void OnEnable()
    {
        if (_innerLiquid == null && Application.isPlaying)
            BuildInnerLiquid();
    }

    private void Update()
    {
        if (_innerLiquid == null) return;

        float flowAktual = GameLevelManager.Instance != null ? GameLevelManager.Instance.FlowRate : 0f;
        _flowSmoothed = Mathf.SmoothDamp(_flowSmoothed, flowAktual, ref _flowVel, Mathf.Max(0.01f, _smoothFactor));
        float t = Mathf.Clamp01(_flowSmoothed / Mathf.Max(1f, _flowMaksimum));
        bool aktif = _flowSmoothed >= _flowMinimumAktif;

        UpdateInnerScale(t, aktif);
        UpdateInnerScroll(t, aktif);
    }

    private void UpdateInnerScale(float t, bool aktif)
    {
        if (!_gunakanFillVolumeBerdasarkanFlow)
            return;

        // Inner cylinder: scale Y di-bend dengan ratio + ditarik ke length ratio penuh kalau flow penuh.
        Vector3 baseScale = new Vector3(_innerDiameterRatio, _innerLengthRatio, _innerDiameterRatio);
        float volumeFactor = aktif
            ? Mathf.Lerp(0.45f, 1f, t)   // saat flow rendah inner masih cukup penuh (slurry konstan), kecepatan saja yang beda
            : _emptyScaleY;

        Vector3 target = new Vector3(baseScale.x, baseScale.y * volumeFactor, baseScale.z);
        _innerLiquid.transform.localScale = Vector3.Lerp(_innerLiquid.transform.localScale, target, Time.deltaTime * 4f);
    }

    private void UpdateInnerScroll(float t, bool aktif)
    {
        if (!aktif || _innerRenderer == null) return;

        // Scroll Y bertambah seiring waktu * t
        _scrollY += t * _multiplierScroll * Time.deltaTime;
        _scrollY = Mathf.Repeat(_scrollY, 1f);

        // Pakai property block agar tidak modify shared material
        _innerRenderer.GetPropertyBlock(_innerMpb);
        _innerMpb.SetVector(_texturePropertyName + "_ST", new Vector4(1f, 1f, 0f, _scrollY));
        _innerRenderer.SetPropertyBlock(_innerMpb);
    }

    private void ApplyOuterMaterial()
    {
        if (_outerRenderer == null || _outerPipeMaterial == null) return;
        if (_outerRenderer.sharedMaterial == _outerPipeMaterial) return;
        _outerRenderer.sharedMaterial = _outerPipeMaterial;
    }

    private void BuildInnerLiquid()
    {
        // Cek child existing
        var existing = transform.Find("Liquid_Inner");
        if (existing != null)
        {
            _innerLiquid = existing.gameObject;
        }
        else
        {
            _innerLiquid = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            _innerLiquid.name = "Liquid_Inner";
            _innerLiquid.transform.SetParent(transform, false);
            _innerLiquid.transform.localPosition = Vector3.zero;
            _innerLiquid.transform.localRotation = Quaternion.identity;
            _innerLiquid.transform.localScale = new Vector3(_innerDiameterRatio, _innerLengthRatio, _innerDiameterRatio);

            // Hapus collider supaya tidak bentrok
            var col = _innerLiquid.GetComponent<Collider>();
            if (col != null)
            {
                if (Application.isPlaying) Destroy(col);
                else DestroyImmediate(col);
            }
        }

        _innerRenderer = _innerLiquid.GetComponent<Renderer>();
        if (_innerRenderer != null && _innerLiquidMaterial != null)
            _innerRenderer.sharedMaterial = _innerLiquidMaterial;
    }

    private static Material LoadMaterial(string path)
    {
#if UNITY_EDITOR
        return UnityEditor.AssetDatabase.LoadAssetAtPath<Material>(path);
#else
        return null;
#endif
    }
}
