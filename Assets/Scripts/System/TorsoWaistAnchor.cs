using UnityEngine;

/// <summary>
/// OLIVIA VR - TorsoWaistAnchor.cs
/// Membuat anchor pinggang mengikuti posisi + yaw kamera.
/// Offset lebih rendah dari TorsoChestAnchor (pinggang).
/// </summary>
[DefaultExecutionOrder(-99)]
public class TorsoWaistAnchor : MonoBehaviour
{
    [SerializeField] private Transform _camera;
    [SerializeField] private float _offsetY = -0.65f;
    [SerializeField] private float _offsetDepan = 0.25f;
    [SerializeField] private float _offsetSamping = 0.22f;
    [SerializeField] private bool _ikutYawKamera = true;
    [SerializeField] private float _smoothPos = 0.06f;
    [SerializeField] private float _smoothRot = 0.10f;
    private Vector3 _velocityPos;
    private float _yawSekarang;
    private float _yawVelocity;

    private void Reset() { if (_camera == null && Camera.main != null) _camera = Camera.main.transform; }
    private void OnEnable() { ForceSyncNow(); }
    private void Start() { ForceSyncNow(); }

    private void LateUpdate()
    {
        if (_camera == null) { if (Camera.main != null) _camera = Camera.main.transform; else return; }
        if (_offsetY > -0.40f) _offsetY = -0.45f;
        if (_offsetY < -0.85f) _offsetY = -0.80f;
        if (_offsetDepan < 0.15f) _offsetDepan = 0.20f;
        Vector3 forwardYaw = HitungForwardYaw();
        Vector3 rightYaw = Vector3.Cross(Vector3.up, forwardYaw).normalized;
        Vector3 targetPos = _camera.position + Vector3.up * _offsetY + forwardYaw * _offsetDepan + rightYaw * _offsetSamping;
        if (_smoothPos <= 0.0001f) transform.position = targetPos;
        else transform.position = Vector3.SmoothDamp(transform.position, targetPos, ref _velocityPos, _smoothPos);
        float yawTarget = Mathf.Atan2(forwardYaw.x, forwardYaw.z) * Mathf.Rad2Deg;
        if (_smoothRot <= 0.0001f) _yawSekarang = yawTarget;
        else _yawSekarang = Mathf.SmoothDampAngle(_yawSekarang, yawTarget, ref _yawVelocity, _smoothRot);
        transform.rotation = Quaternion.Euler(0f, _yawSekarang, 0f);
    }

    private Vector3 HitungForwardYaw()
    {
        if (_ikutYawKamera && _camera != null)
        { Vector3 fwd = _camera.forward; fwd.y = 0f; if (fwd.sqrMagnitude > 0.0001f) return fwd.normalized; }
        if (transform.parent != null)
        { Vector3 fwd = transform.parent.forward; fwd.y = 0f; if (fwd.sqrMagnitude > 0.0001f) return fwd.normalized; }
        return Vector3.forward;
    }

    public void ForceSyncNow()
    {
        if (_camera == null && Camera.main != null) _camera = Camera.main.transform;
        if (_offsetY > -0.40f) _offsetY = -0.45f;
        if (_offsetY < -0.85f) _offsetY = -0.80f;
        if (_offsetDepan < 0.15f) _offsetDepan = 0.20f;
        if (_camera == null) return;
        Vector3 forwardYaw = HitungForwardYaw();
        Vector3 rightYaw = Vector3.Cross(Vector3.up, forwardYaw).normalized;
        transform.position = _camera.position + Vector3.up * _offsetY + forwardYaw * _offsetDepan + rightYaw * _offsetSamping;
        _yawSekarang = Mathf.Atan2(forwardYaw.x, forwardYaw.z) * Mathf.Rad2Deg;
        transform.rotation = Quaternion.Euler(0f, _yawSekarang, 0f);
        _velocityPos = Vector3.zero; _yawVelocity = 0f;
    }
}
