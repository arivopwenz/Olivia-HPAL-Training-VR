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
    [SerializeField] private bool _panahDinonaktifkan = false;
    [Tooltip("Kamera player. Panah di-render relatif ke kamera ini.")]
    [SerializeField] private Transform _camera;
    [Tooltip("Target world yang ditunjuk panah.")]
    [SerializeField] private Transform _target;

    [Header("=== Posisi Panah ===")]
    [Tooltip("Jarak panah dari kamera (m).")]
    [SerializeField] private float _jarakDariKamera = 1.75f;
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
    private Renderer[] _renderers;
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
        if (_panahDinonaktifkan)
        {
            Hide();
            return;
        }

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
            SetVisualVisible(false);
            return;
        }

        if (_autoHideSaatMemandang)
        {
            float dot = Vector3.Dot(_camera.forward.normalized, toTarget.normalized);
            if (dot >= _dotMemandang)
            {
                SetVisualVisible(false);
                return;
            }
        }

        SetVisualVisible(true);

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
            float scale = Mathf.Lerp(0.9f, 1.12f, t);
            ApplyStableScale(scale);
            float intensity = Mathf.Lerp(_glowIntensity * 0.6f, _glowIntensity, t);
            _material.SetColor("_EmissionColor", _warnaPanah * intensity);
        }
    }

    [SerializeField] private float _baseScale = 0.30f;

    private void BuatVisual()
    {
        if (CloneLevel1ArrowVisual())
        {
            ApplyStableScale(1f);
            return;
        }

        // Buat panah dari Cube primitive yang di-stretch jadi bentuk panah (body + head)
        // Sederhana: cube body + cube head terpotong (atau pyramid). Cukup dengan capsule + cone visual.

        // Body: cube scaled
        var body = GameObject.CreatePrimitive(PrimitiveType.Cube);
        body.name = "ArrowBody";
        body.transform.SetParent(transform, false);
        body.transform.localPosition = new Vector3(0f, 0f, -0.32f);
        body.transform.localScale = new Vector3(0.18f, 0.12f, 0.58f);
        DestroyImmediate(body.GetComponent<Collider>());

        // Head: cube rotated 45 deg → pseudo-arrow point
        var head = GameObject.CreatePrimitive(PrimitiveType.Cube);
        head.name = "ArrowHead";
        head.transform.SetParent(transform, false);
        head.transform.localPosition = new Vector3(0f, 0f, 0.22f);
        head.transform.localRotation = Quaternion.Euler(0f, 45f, 45f);
        head.transform.localScale = new Vector3(0.38f, 0.38f, 0.38f);
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
        _renderers = GetComponentsInChildren<Renderer>(true);
        foreach (Renderer renderer in _renderers)
        {
            renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            renderer.receiveShadows = false;
        }
        ApplyStableScale(1f);
    }

    private bool CloneLevel1ArrowVisual()
    {
        Transform source = null;
        foreach (Transform t in Resources.FindObjectsOfTypeAll<Transform>())
        {
            if (t == null || !t.gameObject.scene.IsValid() || t == transform)
                continue;

            if (t.name == "TaskHint_Arrow3D" && t.GetComponentInChildren<Renderer>(true) != null)
            {
                source = t;
                break;
            }
        }

        if (source == null)
            return false;

        GameObject clone = Instantiate(source.gameObject, transform);
        clone.name = "TaskHint_Arrow3D_Runtime";
        clone.transform.localPosition = Vector3.zero;
        clone.transform.localRotation = Quaternion.identity;
        clone.transform.localScale = source.localScale;

        foreach (Collider collider in clone.GetComponentsInChildren<Collider>(true))
            DestroyImmediate(collider);

        _renderers = clone.GetComponentsInChildren<Renderer>(true);
        foreach (Renderer renderer in _renderers)
        {
            renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            renderer.receiveShadows = false;
        }

        _renderer = _renderers.Length > 0 ? _renderers[0] as MeshRenderer : null;
        if (_renderers.Length > 0)
            _material = _renderers[0].material;

        return true;
    }

    private void ApplyStableScale(float pulseScale)
    {
        float parentScale = 1f;
        if (transform.parent != null)
        {
            Vector3 lossy = transform.parent.lossyScale;
            parentScale = Mathf.Max(0.001f, Mathf.Max(Mathf.Abs(lossy.x), Mathf.Abs(lossy.y), Mathf.Abs(lossy.z)));
        }

        float localScale = Mathf.Clamp(_baseScale * pulseScale / parentScale, 0.04f, 0.42f);
        transform.localScale = Vector3.one * localScale;
    }

    private void SetVisualVisible(bool visible)
    {
        if (_renderers == null || _renderers.Length == 0)
            _renderers = GetComponentsInChildren<Renderer>(true);

        for (int i = 0; i < _renderers.Length; i++)
            if (_renderers[i] != null)
                _renderers[i].enabled = visible;
    }
}
