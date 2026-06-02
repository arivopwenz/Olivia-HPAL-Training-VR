using System.Collections.Generic;
using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit.Interactables;

/// <summary>
/// Putaran handwheel PERSIS seperti Level 8 Flash Vessel:
///  - Input gestural: twist tangan (interactor.up, fallback .right) diproyeksikan ke bidang disc x gesturalGain.
///  - Hover & grab sama-sama memutar; XRSimpleInteractable -> objek TIDAK ketarik mengikuti tangan.
///  - Rotasi: SEMUA part (hub+ring+spoke) diputar mengelilingi pivot world pada axis world
///    (Quaternion.AngleAxis(degrees, axisWorld)) -> identik dengan ApplyHandwheelRotation Level 8.
/// Controller cukup baca OpenPercent01.
/// </summary>
[DisallowMultipleComponent]
public class GesturalHandwheel : MonoBehaviour
{
    public float fullOpenDegrees = 1440f;
    public float gesturalGain = 5f;          // sama default seperti Level 8 (_gesturalGain).
    public KeyCode debugKey = KeyCode.R;
    [SerializeField] bool _autoSetupOnStart = false;   // true = setup sendiri saat Start (selalu bisa diputar tangan, tak perlu controller).
    public float OpenPercent01 { get; private set; }

    Transform[] _parts; Quaternion[] _baseRot; Vector3[] _basePos;
    Vector3 _pivot, _axis = Vector3.right;
    float _deg; bool _active, _yawValid; float _yawLast; Transform _attach; bool _ready;

    void Start()
    {
        if (_autoSetupOnStart && !_ready) AutoSetup();
    }

    // Setup otomatis dari struktur objek: root ber-anak mesh -> putar root (anak ikut);
    // part terpisah (Hub/OuterRing/Spoke_NN sebagai sibling) -> kumpulkan se-prefix.
    public void AutoSetup()
    {
        bool hasChildMesh = false;
        foreach (Transform c in transform) if (c.GetComponentInChildren<Renderer>() != null) { hasChildMesh = true; break; }
        if (!hasChildMesh && name.EndsWith("_Hub") && transform.parent != null)
        {
            var parts = new List<Transform>();
            int us = name.LastIndexOf('_');
            string prefix = us > 0 ? name.Substring(0, us) : name;
            foreach (Transform sib in transform.parent)
                if (sib != transform && sib.name.StartsWith(prefix)) parts.Add(sib);
            Setup(transform, parts);
            return;
        }
        Setup(transform, null);   // root ber-anak mesh ATAU handwheel satu-mesh -> putar diri sendiri.
    }
    public void Setup(Transform hub, IList<Transform> parts)
    {
        var list = new List<Transform>();
        if (hub != null) list.Add(hub);
        if (parts != null) foreach (var p in parts) if (p != null && !list.Contains(p)) list.Add(p);
        if (list.Count == 0) return;
        _parts = list.ToArray();
        _baseRot = new Quaternion[_parts.Length];
        _basePos = new Vector3[_parts.Length];

        // pivot = pusat bounds gabungan; axis = arah tertipis bounds (= normal disc) di world.
        Bounds b = default; bool has = false;
        foreach (var p in _parts)
            foreach (var r in p.GetComponentsInChildren<Renderer>())
            { if (!has) { b = r.bounds; has = true; } else b.Encapsulate(r.bounds); }
        _pivot = has ? b.center : (hub != null ? hub.position : _parts[0].position);
        Vector3 s = has ? b.size : Vector3.one;
        _axis = (s.x <= s.y && s.x <= s.z) ? Vector3.right : (s.y <= s.z ? Vector3.up : Vector3.forward);

        for (int i = 0; i < _parts.Length; i++) { _baseRot[i] = _parts[i].rotation; _basePos[i] = _parts[i].position; }
        _deg = 0f; OpenPercent01 = 0f; _ready = true;
        EnsureInteractable(hub != null ? hub : _parts[0]);
    }

    void EnsureInteractable(Transform t)
    {
        var go = t.gameObject;
        var grab = t.GetComponent<XRGrabInteractable>(); if (grab) Destroy(grab);
        var rb = t.GetComponent<Rigidbody>(); if (rb) Destroy(rb);
        if (t.GetComponent<Collider>() == null) { var sc = go.AddComponent<SphereCollider>(); sc.radius = 0.7f; sc.isTrigger = false; }
        var si = t.GetComponent<XRSimpleInteractable>(); if (si == null) si = go.AddComponent<XRSimpleInteractable>();
        si.colliders.Clear();
        foreach (var c in go.GetComponents<Collider>()) if (c != null) si.colliders.Add(c);
        si.enabled = false; si.enabled = true;
        si.selectEntered.RemoveAllListeners(); si.selectExited.RemoveAllListeners();
        si.hoverEntered.RemoveAllListeners(); si.hoverExited.RemoveAllListeners();
        si.selectEntered.AddListener(a => { _active = true; _attach = a.interactorObject != null ? a.interactorObject.transform : null; _yawValid = false; });
        si.selectExited.AddListener(a => { _active = false; _attach = null; _yawValid = false; });
        si.hoverEntered.AddListener(a => { _active = true; _attach = a.interactorObject != null ? a.interactorObject.transform : _attach; });
        si.hoverExited.AddListener(a => { _active = false; _yawValid = false; });
    }

    void Update()
    {
        if (!_ready) return;
        float d = 0f;
        if (Input.GetKey(debugKey)) d += 360f * Time.deltaTime;
        if (_active && _attach != null)
        {
            Vector3 hv = _attach.up;
            Vector3 p = Vector3.ProjectOnPlane(hv, _axis);
            if (p.sqrMagnitude < 0.01f) { hv = _attach.right; p = Vector3.ProjectOnPlane(hv, _axis); }
            if (p.sqrMagnitude > 0.0001f)
            {
                p.Normalize();
                Vector3 r = Vector3.ProjectOnPlane(Vector3.up, _axis);
                if (r.sqrMagnitude < 0.0001f) r = Vector3.ProjectOnPlane(Vector3.right, _axis);
                r.Normalize();
                float yaw = Vector3.SignedAngle(r, p, _axis);
                if (!_yawValid) { _yawLast = yaw; _yawValid = true; }
                else { float dy = Mathf.DeltaAngle(_yawLast, yaw); _yawLast = yaw; if (Mathf.Abs(dy) > 35f) dy = 0f; d += dy * Mathf.Max(1f, gesturalGain); }
            }
        }
        else _yawValid = false;

        if (Mathf.Abs(d) < 0.0001f) return;
        _deg = Mathf.Clamp(_deg + d, 0f, fullOpenDegrees);
        Apply();
        OpenPercent01 = Mathf.Clamp01(_deg / fullOpenDegrees);
    }

    void Apply()
    {
        Quaternion delta = Quaternion.AngleAxis(_deg, _axis);
        for (int i = 0; i < _parts.Length; i++)
        {
            if (_parts[i] == null) continue;
            _parts[i].rotation = delta * _baseRot[i];
            _parts[i].position = _pivot + delta * (_basePos[i] - _pivot);
        }
    }
}
