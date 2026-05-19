using UnityEngine;

/// <summary>
/// OLIVIA VR - TorsoChestAnchor.cs
///
/// Membuat anchor "dada" yang mengikuti posisi horizontal + yaw kamera, tapi TIDAK
/// ikut pitch/roll. Dengan ini, socket APD (masker, walkie talkie, dst) yang di-parent
/// ke anchor ini akan tetap berada di area dada player meskipun kepala didongakkan
/// atau ditundukkan, sehingga masih kelihatan saat player nunduk untuk lihat dada.
///
/// Pemakaian:
///   1. Buat empty GameObject sebagai child XR Origin (XR Rig) bernama "TorsoAnchor".
///   2. Pasang script ini, assign field _camera ke Main Camera.
///   3. Pindahkan socket APD (mis. Socket_Respirator_Baju) jadi child TorsoAnchor.
/// </summary>
public class TorsoChestAnchor : MonoBehaviour
{
    [Header("=== Referensi Kamera ===")]
    [Tooltip("Main Camera dari XR Rig. Posisi & yaw kamera ini yang akan diikuti.")]
    [SerializeField] private Transform _camera;

    [Header("=== Offset dari Kamera ===")]
    [Tooltip("Jarak vertikal dari posisi kamera (negatif = di bawah kamera). Default -0.40m kira-kira posisi dada.")]
    [SerializeField] private float _offsetY = -0.40f;

    [Tooltip("Jarak ke depan dari kamera, mengikuti yaw kamera (di depan dada).")]
    [SerializeField] private float _offsetDepan = 0.18f;

    [Tooltip("Jarak ke samping kamera (positif = kanan).")]
    [SerializeField] private float _offsetSamping = 0.0f;

    [Header("=== Behaviour ===")]
    [Tooltip("Jika true, anchor ikut yaw kamera (rotasi horizontal). Jika false, anchor mengikuti yaw rig (parent).")]
    [SerializeField] private bool _ikutYawKamera = true;

    [Tooltip("Smoothing posisi anchor agar tidak nervous mengikuti gerakan kepala. 0 = instan, 0.1-0.2 = halus.")]
    [Range(0f, 0.5f)]
    [SerializeField] private float _smoothPos = 0.08f;

    [Tooltip("Smoothing rotasi anchor (yaw only).")]
    [Range(0f, 0.5f)]
    [SerializeField] private float _smoothRot = 0.12f;

    private Vector3 _velocityPos;
    private float _yawSekarang;
    private float _yawVelocity;

    private void Reset()
    {
        if (_camera == null && Camera.main != null)
            _camera = Camera.main.transform;
    }

    private void OnEnable()
    {
        if (_camera == null && Camera.main != null)
            _camera = Camera.main.transform;

        SyncronisasiInstan();
    }

    private void LateUpdate()
    {
        if (_camera == null)
        {
            if (Camera.main != null)
                _camera = Camera.main.transform;
            else
                return;
        }

        Vector3 forwardYaw = HitungForwardYaw();
        Vector3 rightYaw = Vector3.Cross(Vector3.up, forwardYaw).normalized;

        Vector3 targetPos = _camera.position
            + Vector3.up * _offsetY
            + forwardYaw * _offsetDepan
            + rightYaw * _offsetSamping;

        if (_smoothPos <= 0.0001f)
            transform.position = targetPos;
        else
            transform.position = Vector3.SmoothDamp(transform.position, targetPos, ref _velocityPos, _smoothPos);

        float yawTarget = Mathf.Atan2(forwardYaw.x, forwardYaw.z) * Mathf.Rad2Deg;
        if (_smoothRot <= 0.0001f)
            _yawSekarang = yawTarget;
        else
            _yawSekarang = Mathf.SmoothDampAngle(_yawSekarang, yawTarget, ref _yawVelocity, _smoothRot);

        transform.rotation = Quaternion.Euler(0f, _yawSekarang, 0f);
    }

    private Vector3 HitungForwardYaw()
    {
        if (_ikutYawKamera && _camera != null)
        {
            Vector3 fwd = _camera.forward;
            fwd.y = 0f;
            if (fwd.sqrMagnitude > 0.0001f)
                return fwd.normalized;
        }

        // Fallback: pakai yaw rig (parent)
        if (transform.parent != null)
        {
            Vector3 fwd = transform.parent.forward;
            fwd.y = 0f;
            if (fwd.sqrMagnitude > 0.0001f)
                return fwd.normalized;
        }

        return Vector3.forward;
    }

    private void SyncronisasiInstan()
    {
        if (_camera == null)
            return;

        Vector3 forwardYaw = HitungForwardYaw();
        Vector3 rightYaw = Vector3.Cross(Vector3.up, forwardYaw).normalized;
        transform.position = _camera.position + Vector3.up * _offsetY + forwardYaw * _offsetDepan + rightYaw * _offsetSamping;
        _yawSekarang = Mathf.Atan2(forwardYaw.x, forwardYaw.z) * Mathf.Rad2Deg;
        transform.rotation = Quaternion.Euler(0f, _yawSekarang, 0f);
        _velocityPos = Vector3.zero;
        _yawVelocity = 0f;
    }
}
