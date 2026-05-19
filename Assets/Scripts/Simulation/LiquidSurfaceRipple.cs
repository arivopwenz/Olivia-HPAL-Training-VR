using UnityEngine;

/// <summary>
/// OLIVIA VR - LiquidSurfaceRipple.cs
///
/// Animasi ripple sederhana pada permukaan liquid saat agitator aktif.
/// Pakai vertex displacement (bukan compute shader) — performant untuk mesh sedikit vertex
/// seperti Cylinder primitive (top cap punya banyak vertex melingkar).
///
/// Cara kerja:
///   - Saat agitator aktif: permukaan top dari mesh di-modify dengan sin-wave radial
///   - Saat tidak aktif: vertex direstore ke posisi awal smoothly
///
/// Pemakaian: pasang di GameObject Slurry_Fill, assign reference SlurryAgitator.
/// </summary>
[RequireComponent(typeof(MeshFilter))]
public class LiquidSurfaceRipple : MonoBehaviour
{
    [Header("=== Referensi Agitator ===")]
    [Tooltip("Agitator yang trigger ripple. Auto-find di scene jika kosong.")]
    [SerializeField] private SlurryAgitator _agitator;

    [Header("=== Ripple Parameters ===")]
    [Tooltip("Amplitude maksimum gelombang (m, di local mesh space).")]
    [Range(0f, 0.5f)] [SerializeField] private float _amplitudo = 0.08f;
    [Tooltip("Frekuensi gelombang spasial (banyak puncak per radius).")]
    [Range(0.5f, 20f)] [SerializeField] private float _frekuensiSpatial = 4f;
    [Tooltip("Kecepatan animasi waktu.")]
    [Range(0.1f, 10f)] [SerializeField] private float _kecepatanWaktu = 2.5f;
    [Tooltip("Hanya modifikasi vertex yang Y positif (top cap dari cylinder).")]
    [SerializeField] private bool _hanyaTopCap = true;

    [Header("=== Smoothing ===")]
    [Range(0f, 5f)] [SerializeField] private float _ramp = 1.2f;

    private Mesh _meshClone;
    private Vector3[] _vertexAwal;
    private Vector3[] _vertexKerja;
    private float _intensitasSekarang;

    private void Awake()
    {
        if (_agitator == null)
            _agitator = UnityEngine.Object.FindFirstObjectByType<SlurryAgitator>();

        var mf = GetComponent<MeshFilter>();
        // Clone mesh agar tidak modifikasi shared mesh asset
        _meshClone = Instantiate(mf.sharedMesh);
        _meshClone.name = mf.sharedMesh.name + "_Rippled";
        _meshClone.MarkDynamic();
        mf.mesh = _meshClone;

        _vertexAwal = _meshClone.vertices;
        _vertexKerja = new Vector3[_vertexAwal.Length];
        System.Array.Copy(_vertexAwal, _vertexKerja, _vertexAwal.Length);
    }

    private void Update()
    {
        bool aktif = _agitator != null && _agitator.Aktif;
        float target = aktif ? 1f : 0f;
        _intensitasSekarang = _ramp <= 0.0001f
            ? target
            : Mathf.MoveTowards(_intensitasSekarang, target, Time.deltaTime / _ramp);

        if (_intensitasSekarang <= 0.0001f && !aktif)
        {
            // Restore plain vertex tanpa update mesh setiap frame
            return;
        }

        ApplyRipple();
    }

    private void ApplyRipple()
    {
        float t = Time.time * _kecepatanWaktu;
        float amp = _amplitudo * _intensitasSekarang;

        for (int i = 0; i < _vertexAwal.Length; i++)
        {
            Vector3 v = _vertexAwal[i];
            // Cylinder primitive: top cap vertices Y ≈ +1, bottom cap Y ≈ -1, side mid Y in between.
            if (_hanyaTopCap && v.y < 0.95f)
            {
                _vertexKerja[i] = v;
                continue;
            }

            // Radial distance dari pusat (X-Z plane)
            float r = Mathf.Sqrt(v.x * v.x + v.z * v.z);
            // Sudut radial untuk pola swirl
            float angle = Mathf.Atan2(v.z, v.x);
            // Sin-wave spatial (gelombang menyebar dari pusat) + swirl
            float wave = Mathf.Sin(r * _frekuensiSpatial - t) * Mathf.Cos(angle * 2f + t * 0.5f);
            _vertexKerja[i] = v + new Vector3(0f, wave * amp, 0f);
        }

        _meshClone.vertices = _vertexKerja;
        _meshClone.RecalculateNormals();
        _meshClone.RecalculateBounds();
    }

    private void OnDestroy()
    {
        if (_meshClone != null)
        {
            if (Application.isPlaying) Destroy(_meshClone);
            else DestroyImmediate(_meshClone);
        }
    }
}
