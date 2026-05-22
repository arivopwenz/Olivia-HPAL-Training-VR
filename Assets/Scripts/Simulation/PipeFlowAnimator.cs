using UnityEngine;

/// <summary>
/// OLIVIA VR - PipeFlowAnimator.cs
///
/// Animator untuk Level4_LiquidFill (liquid yang mengisi pipa).
/// Menambahkan animasi terombang-ambing (wobble) supaya liquid kelihatan hidup
/// seperti slurry yang mengalir/teraduk:
///   - Texture UV scroll (texture bergerak ke arah aliran)
///   - Sinusoidal scale wobble (X/Z pulse halus)
///   - Sinusoidal position offset (terombang-ambing)
///   - Emission pulse (cairan glowing pelan)
///
/// Pasang di GameObject liquid yang sudah scaled-fill (cylinder primitive di pipa).
/// </summary>
public class PipeFlowAnimator : MonoBehaviour
{
    [Header("=== Texture Scroll ===")]
    [Tooltip("Kecepatan scroll UV texture liquid (arus mengalir).")]
    [SerializeField] private Vector2 _scrollSpeed = new Vector2(0f, 0.6f);
    [SerializeField] private string _texturePropertyName = "_BaseMap";

    [Header("=== Wobble (Terombang-Ambing) ===")]
    [Tooltip("Amplitude pulse scale X/Z (efek bernafas).")]
    [Range(0f, 0.15f)] [SerializeField] private float _amplitudoScale = 0.04f;
    [Tooltip("Frekuensi pulse scale (Hz).")]
    [Range(0.1f, 5f)] [SerializeField] private float _frekuensiScale = 1.6f;
    [Tooltip("Amplitude offset position X/Z (efek goyang).")]
    [Range(0f, 0.05f)] [SerializeField] private float _amplitudoGoyang = 0.012f;
    [Tooltip("Frekuensi goyang.")]
    [Range(0.1f, 5f)] [SerializeField] private float _frekuensiGoyang = 1.2f;

    [Header("=== Emission Pulse ===")]
    [SerializeField] private bool _emissionPulseAktif = true;
    [SerializeField] private Color _warnaEmission = new Color(0.55f, 0.20f, 0.70f, 1f);
    [Range(0f, 3f)] [SerializeField] private float _intensitasMin = 0.3f;
    [Range(0f, 3f)] [SerializeField] private float _intensitasMax = 0.9f;
    [Range(0.1f, 3f)] [SerializeField] private float _frekuensiEmission = 0.8f;

    private Renderer _renderer;
    private MaterialPropertyBlock _mpb;
    private Vector3 _scaleAwal;
    private Vector3 _posAwal;
    private Vector2 _uvOffset;
    private float _phaseScale;
    private float _phaseGoyang;
    private float _phaseEmission;

    private void Awake()
    {
        _renderer = GetComponent<Renderer>();
        _mpb = new MaterialPropertyBlock();
        _scaleAwal = transform.localScale;
        _posAwal = transform.localPosition;
    }

    private void OnEnable()
    {
        // Re-snapshot saat re-enable supaya base values up-to-date
        _scaleAwal = transform.localScale;
        _posAwal = transform.localPosition;
    }

    private void Update()
    {
        if (_renderer == null) return;

        float dt = Time.deltaTime;

        // 1. UV scroll
        _uvOffset += _scrollSpeed * dt;
        _uvOffset.x = Mathf.Repeat(_uvOffset.x, 1f);
        _uvOffset.y = Mathf.Repeat(_uvOffset.y, 1f);
        if (_renderer.sharedMaterial != null && _renderer.sharedMaterial.HasProperty(_texturePropertyName))
            _renderer.sharedMaterial.SetTextureOffset(_texturePropertyName, _uvOffset);

        // 2. Scale pulse (X/Z bernafas, Y tetap supaya fill stabil)
        _phaseScale += dt * _frekuensiScale * Mathf.PI * 2f;
        float pulseScale = 1f + Mathf.Sin(_phaseScale) * _amplitudoScale;
        Vector3 scale = _scaleAwal;
        scale.x *= pulseScale;
        scale.z *= pulseScale;
        transform.localScale = scale;

        // 3. Position goyang (X/Z bergerak halus)
        _phaseGoyang += dt * _frekuensiGoyang * Mathf.PI * 2f;
        Vector3 pos = _posAwal;
        pos.x += Mathf.Sin(_phaseGoyang) * _amplitudoGoyang;
        pos.z += Mathf.Cos(_phaseGoyang * 0.85f) * _amplitudoGoyang;
        transform.localPosition = pos;

        // 4. Emission pulse
        if (_emissionPulseAktif)
        {
            _phaseEmission += dt * _frekuensiEmission * Mathf.PI * 2f;
            float intensity = Mathf.Lerp(_intensitasMin, _intensitasMax,
                                         (Mathf.Sin(_phaseEmission) + 1f) * 0.5f);
            _renderer.GetPropertyBlock(_mpb);
            _mpb.SetColor("_EmissionColor", _warnaEmission * intensity);
            _renderer.SetPropertyBlock(_mpb);
        }
    }

    /// <summary>Update base scale (panggil saat parent script mengubah fill amount).</summary>
    public void UpdateBaseScale(Vector3 baseScale)
    {
        _scaleAwal = baseScale;
    }

    /// <summary>Update base position (panggil saat parent script mengubah fill anchor).</summary>
    public void UpdateBasePosition(Vector3 basePos)
    {
        _posAwal = basePos;
    }
}
