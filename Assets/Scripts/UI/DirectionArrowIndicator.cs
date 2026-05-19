using UnityEngine;

/// <summary>
/// OLIVIA VR - DirectionArrowIndicator.cs
///
/// Panah 3D yang melayang di depan kamera dan selalu menunjuk ke target world.
/// Auto-hide kalau player sudah dekat target atau sudah memandang ke target.
///
/// Pemakaian:
///   1. Buat empty GameObject sebagai child Main Camera atau XR Rig.
///   2. Pasang script ini, assign _camera + _target.
///   3. Panggil Show() / Hide() dari controller saat sequence butuh.
/// </summary>
public class DirectionArrowIndicator : MonoBehaviour
{
    [Header("=== Referensi ===")]
    [Tooltip("Kamera player. Panah di-render relatif ke kamera ini.")]
    [SerializeField] private Transform _camera;
    [Tooltip("Target world yang ditunjuk panah.")]
    [SerializeField] private Transform _target;

    [Header("=== Posisi Panah ===")]
    [Tooltip("Jarak panah dari kamera (m).")]
    [SerializeField] private float _jarakDariKamera = 1.4f;
    [Tooltip("Offset Y dari titik tengah pandangan (negatif = di bawah view).")]
    [SerializeField] private float _offsetY = -0.30f;
    [Tooltip("Smoothing posisi panah agar tidak nervous.")]
    [Range(0f, 0.5f)] [SerializeField] private float _smoothPos = 0.1f;
    [Tooltip("Smoothing rotasi panah.")]
    [Range(0f, 0.5f)] [SerializeField] private float _smoothRot = 0.08f;

    [Header("=== Visual ===")]
    [Tooltip("Warna panah utama.")]
    [SerializeField] private Color _warnaPanah = new Color(1f, 0.85f, 0.15f, 1f);
    [Tooltip("Intensitas glow emisi.")]
    [Range(0.5f, 8f)] [SerializeField] private float _glowIntensity = 3.5f;
    [Tooltip("Pulse: panah berdenyut pelan-pelan agar mudah ditangkap mata.")]
    [SerializeField] private bool _pulse = true;
    [SerializeField] private float _pulsePeriod = 1.2f;

    [Header("=== Auto-Hide ===")]
    [Tooltip("Sembunyikan panah otomatis kalau player sudah dekat target.")]
    [SerializeField] private bool _autoHideSaatDekatTarget = true;
    [SerializeField] private float _jarakAutoHide = 6f;
    [Tooltip("Sembunyikan panah otomatis kalau player sudah memandang ke target (dot > threshold).")]
    [SerializeField] private bool _autoHideSaatMemandang = true;
    [SerializeField] private float _dotMemandang = 0.85f;

    private MeshRenderer _renderer;
    private Material _material;
    private Vector3 _smoothPosVel;
    private Quaternion _currentRot = Quaternion.identity;
    private bool _aktif;

    private void Awake()
    {
        if (_camera == null && Camera.main != null)
            _camera = Camera.main.transform;

        BuatVisual();
        gameObject.SetActive(false);
    }

    public void Show(Transform target)
    {
        _target = target;
        Show();
    }

    public void Show()
    {
        _aktif = true;
        gameObject.SetActive(true);
        if (_camera == null && Camera.main != null)
            _camera = Camera.main.transform;
    }

    public void Hide()
    {
        _aktif = false;
        gameObject.SetActive(false);
    }

    private void LateUpdate()
    {
        if (!_aktif || _camera == null || _target == null)
            return;

        // Auto-hide checks
        Vector3 toTarget = _target.position - _camera.position;
        float dist = toTarget.magnitude;
        if (_autoHideSaatDekatTarget && dist <= _jarakAutoHide)
        {
            // Hanya sembunyikan render, tetap aktif logic-nya supaya bisa muncul lagi kalau menjauh
            if (_renderer != null) _renderer.enabled = false;
            return;
        }

        if (_autoHideSaatMemandang)
        {
            float dot = Vector3.Dot(_camera.forward.normalized, toTarget.normalized);
            if (dot >= _dotMemandang)
            {
                if (_renderer != null) _renderer.enabled = false;
                return;
            }
        }

        if (_renderer != null) _renderer.enabled = true;

        // Posisi panah: di depan kamera
        Vector3 targetPos = _camera.position + _camera.forward * _jarakDariKamera + _camera.up * _offsetY;
        if (_smoothPos <= 0.0001f)
            transform.position = targetPos;
        else
            transform.position = Vector3.SmoothDamp(transform.position, targetPos, ref _smoothPosVel, _smoothPos);

        // Rotasi panah: hadap target
        Vector3 dir = (_target.position - transform.position).normalized;
        if (dir.sqrMagnitude > 0.0001f)
        {
            Quaternion rotTarget = Quaternion.LookRotation(dir, Vector3.up);
            if (_smoothRot <= 0.0001f)
                _currentRot = rotTarget;
            else
                _currentRot = Quaternion.Slerp(_currentRot, rotTarget, 1f - Mathf.Exp(-Time.deltaTime / _smoothRot));
            transform.rotation = _currentRot;
        }

        // Pulse
        if (_pulse && _material != null)
        {
            float t = Mathf.PingPong(Time.time / _pulsePeriod, 1f);
            float scale = Mathf.Lerp(0.85f, 1.15f, t);
            transform.localScale = new Vector3(scale, scale, scale) * _baseScale;
            float intensity = Mathf.Lerp(_glowIntensity * 0.6f, _glowIntensity, t);
            _material.SetColor("_EmissionColor", _warnaPanah * intensity);
        }
    }

    private float _baseScale = 0.18f;

    private void BuatVisual()
    {
        // Buat panah dari Cube primitive yang di-stretch jadi bentuk panah (body + head)
        // Sederhana: cube body + cube head terpotong (atau pyramid). Cukup dengan capsule + cone visual.

        // Body: cube scaled
        var body = GameObject.CreatePrimitive(PrimitiveType.Cube);
        body.name = "ArrowBody";
        body.transform.SetParent(transform, false);
        body.transform.localPosition = new Vector3(0f, 0f, -0.4f);
        body.transform.localScale = new Vector3(0.25f, 0.25f, 0.8f);
        DestroyImmediate(body.GetComponent<Collider>());

        // Head: cube rotated 45 deg → pseudo-arrow point
        var head = GameObject.CreatePrimitive(PrimitiveType.Cube);
        head.name = "ArrowHead";
        head.transform.SetParent(transform, false);
        head.transform.localPosition = new Vector3(0f, 0f, 0.3f);
        head.transform.localRotation = Quaternion.Euler(0f, 45f, 45f);
        head.transform.localScale = new Vector3(0.5f, 0.5f, 0.5f);
        DestroyImmediate(head.GetComponent<Collider>());

        Shader shader = Shader.Find("Universal Render Pipeline/Lit");
        if (shader == null) shader = Shader.Find("Standard");
        _material = new Material(shader);
        _material.color = _warnaPanah;
        _material.EnableKeyword("_EMISSION");
        _material.SetColor("_EmissionColor", _warnaPanah * _glowIntensity);

        body.GetComponent<MeshRenderer>().sharedMaterial = _material;
        head.GetComponent<MeshRenderer>().sharedMaterial = _material;

        _renderer = body.GetComponent<MeshRenderer>();
        transform.localScale = Vector3.one * _baseScale;
    }
}
