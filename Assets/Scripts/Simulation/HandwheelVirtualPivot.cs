using UnityEngine;

/// <summary>
/// OLIVIA VR — HandwheelVirtualPivot.cs
///
/// Rotate sekumpulan mesh handwheel (Hub, Outer Ring, Spokes) tanpa mengubah hierarki scene.
/// Berguna ketika mesh part di-import dari Blender/FBX dan Unity meriset reparent setelah save.
///
/// Cara kerja:
///   1. Di Awake, simpan rest world position & rotation tiap mesh part relatif terhadap pivot.
///   2. Tiap frame, baca local rotation pivot ini → project ke world rotation di sumbu yang dipilih.
///   3. Set posisi & rotasi tiap mesh part = pivot world center + (rotasi × rest offset).
///
/// Pemakaian:
///   - Tempatkan GameObject "RealSteamValve_Pivot_Lvl5" di sibling parent yang sama dengan mesh parts.
///   - Set posisi pivot di pusat handwheel (rata-rata posisi semua mesh).
///   - Assign array _meshParts ke 6 part decorative (Hub, OuterRing, Spoke_00..03, dst).
///   - Set _sumbuRotasiLocal = Vector3.up (default) untuk handwheel flat horizontal,
///     atau Vector3.forward untuk handwheel vertical menghadap player.
///   - Saat Level5SteamValveController.UpdateVisuals() rotate pivot.localRotation,
///     script ini otomatis sync mesh parts.
/// </summary>
[DefaultExecutionOrder(50)] // Jalan setelah controller mengubah pivot.localRotation
public class HandwheelVirtualPivot : MonoBehaviour
{
    [Tooltip("Mesh parts yang akan ikut diputar bersama pivot. Isi via Inspector atau script.")]
    [SerializeField] private Transform[] _meshParts;

    [Tooltip("Sumbu rotasi dalam local space pivot (default Y up untuk handwheel horizontal).")]
    [SerializeField] private Vector3 _sumbuRotasiLocal = Vector3.up;

    [Tooltip("Auto-cache rest pose di Awake. Set false kalau mau panggil RecacheRestPose() manual.")]
    [SerializeField] private bool _autoCacheDiAwake = true;

    [Tooltip("Geser pivot runtime ke pusat mesh parts supaya handwheel muter di tempat seperti setir, bukan orbit seperti bola.")]
    [SerializeField] private bool _autoCenterPivotKeMesh = true;

    // Rest pose di world space, relatif terhadap pivot saat Awake.
    private Vector3[] _restWorldOffsets;
    private Quaternion[] _restWorldRotations;
    private Vector3 _restPivotPos;
    private Quaternion _restPivotRotInverse; // inverse rotasi pivot saat caching

    private void Awake()
    {
        if (_autoCacheDiAwake)
            RecacheRestPose();
    }

    /// <summary>
    /// Set ulang rest pose. Panggil setelah ubah _meshParts di runtime.
    /// </summary>
    public void RecacheRestPose()
    {
        if (_meshParts == null || _meshParts.Length == 0)
        {
            _restWorldOffsets = null;
            _restWorldRotations = null;
            return;
        }

        if (_autoCenterPivotKeMesh)
            CenterPivotToMeshParts();

        _restPivotPos = transform.position;
        _restPivotRotInverse = Quaternion.Inverse(transform.rotation);
        _restWorldOffsets = new Vector3[_meshParts.Length];
        _restWorldRotations = new Quaternion[_meshParts.Length];

        for (int i = 0; i < _meshParts.Length; i++)
        {
            if (_meshParts[i] == null) continue;
            // Offset world saat rest, sebelum pivot diputar.
            _restWorldOffsets[i] = _meshParts[i].position - _restPivotPos;
            _restWorldRotations[i] = _meshParts[i].rotation;
        }
    }

    private void LateUpdate()
    {
        if (_meshParts == null || _restWorldOffsets == null) return;

        // Hitung rotasi delta antara rest pose pivot dan pivot saat ini.
        // delta = currentPivotRot * inverse(restPivotRot)
        Quaternion deltaRot = transform.rotation * _restPivotRotInverse;

        // Project ke axis yang dipilih (filter rotasi cuma ke sumbu valve, abaikan tilt random).
        Vector3 axisWorld = transform.TransformDirection(_sumbuRotasiLocal).normalized;
        if (axisWorld.sqrMagnitude < 0.0001f) return;

        deltaRot = ProjectRotationToAxis(deltaRot, axisWorld);

        Vector3 pivotPos = transform.position;

        for (int i = 0; i < _meshParts.Length; i++)
        {
            var part = _meshParts[i];
            if (part == null) continue;

            // Posisi: pivotPos + (deltaRot * restOffset)
            part.position = pivotPos + deltaRot * _restWorldOffsets[i];
            // Rotasi: deltaRot * restRot
            part.rotation = deltaRot * _restWorldRotations[i];
        }
    }

    /// <summary>
    /// Filter quaternion cuma ke komponen rotasi di axis tertentu.
    /// Berguna supaya tilt/yaw kecil dari pivot tidak terbawa ke mesh.
    /// </summary>
    private static Quaternion ProjectRotationToAxis(Quaternion q, Vector3 axis)
    {
        q.ToAngleAxis(out float angle, out Vector3 qAxis);
        // Sudut search-of-rotation di axis target.
        float dot = Vector3.Dot(qAxis.normalized, axis);
        float projectedAngle = angle * dot;
        return Quaternion.AngleAxis(projectedAngle, axis);
    }

    /// <summary>
    /// Public setter untuk script lain (mis. controller).
    /// </summary>
    public void SetMeshParts(Transform[] parts)
    {
        _meshParts = parts;
        RecacheRestPose();
    }

    public void SetAxisLocal(Vector3 axis)
    {
        if (axis.sqrMagnitude <= 0.0001f)
            return;

        _sumbuRotasiLocal = axis.normalized;
    }

    public Vector3 InferAxisLocalFromMeshBounds()
    {
        Bounds bounds = default;
        bool hasBounds = false;

        if (_meshParts == null)
            return _sumbuRotasiLocal.sqrMagnitude > 0.0001f ? _sumbuRotasiLocal.normalized : Vector3.up;

        for (int i = 0; i < _meshParts.Length; i++)
        {
            var part = _meshParts[i];
            if (part == null) continue;

            var renderer = part.GetComponentInChildren<Renderer>(true);
            if (renderer == null) continue;

            if (!hasBounds)
            {
                bounds = renderer.bounds;
                hasBounds = true;
            }
            else
            {
                bounds.Encapsulate(renderer.bounds);
            }
        }

        if (!hasBounds)
            return _sumbuRotasiLocal.sqrMagnitude > 0.0001f ? _sumbuRotasiLocal.normalized : Vector3.up;

        Vector3 size = bounds.size;
        Vector3 worldAxis = Vector3.forward;
        if (size.x <= size.y && size.x <= size.z)
            worldAxis = Vector3.right;
        else if (size.y <= size.x && size.y <= size.z)
            worldAxis = Vector3.up;

        Vector3 localAxis = transform.InverseTransformDirection(worldAxis);
        return localAxis.sqrMagnitude > 0.0001f ? localAxis.normalized : Vector3.up;
    }

    public void CenterPivotToMeshParts()
    {
        if (_meshParts == null || _meshParts.Length == 0)
            return;

        Bounds bounds = default;
        bool hasBounds = false;

        for (int i = 0; i < _meshParts.Length; i++)
        {
            var part = _meshParts[i];
            if (part == null) continue;

            var renderer = part.GetComponentInChildren<Renderer>(true);
            if (renderer != null)
            {
                if (!hasBounds)
                {
                    bounds = renderer.bounds;
                    hasBounds = true;
                }
                else
                {
                    bounds.Encapsulate(renderer.bounds);
                }
            }
            else
            {
                if (!hasBounds)
                {
                    bounds = new Bounds(part.position, Vector3.zero);
                    hasBounds = true;
                }
                else
                {
                    bounds.Encapsulate(part.position);
                }
            }
        }

        if (hasBounds)
            transform.position = bounds.center;
    }

    public Transform[] MeshParts => _meshParts;
}
