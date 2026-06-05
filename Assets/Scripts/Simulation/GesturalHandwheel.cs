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
    [Tooltip("Faktor amplifikasi twist tangan ke wheel. 1.0 = 1:1 (paling natural). >1 untuk wheel besar yang harus lebih sensitif.")]
    public float gesturalGain = 1.0f;
    [Tooltip("Cap kecepatan rotasi (deg/s). Naikkan kalau tangan player diputar cepat agar wheel tidak ketinggalan.")]
    [SerializeField] private float _maxDegreesPerSecond = 720f;
    [Tooltip("Smoothing rotasi tampilan (detik). 0 = instant follow tangan (paling riil), 0.05-0.1 sedikit dampened. 0.22 lama terasa laggy.")]
    [SerializeField] private float _smoothTime = 0.04f;
    public KeyCode debugKey = KeyCode.R;
    [SerializeField] bool _autoSetupOnStart = false;   // true = setup sendiri saat Start (selalu bisa diputar tangan, tak perlu controller).
    public float OpenPercent01 { get; private set; }

    Transform[] _parts; Quaternion[] _baseRot; Vector3[] _basePos;
    Vector3 _pivot, _axis = Vector3.right;
    float _ringRadius = 0.5f;
    float _targetDeg, _displayDeg, _smoothVelocity;
    bool _active, _yawValid; float _yawLast; Transform _attach; bool _ready;
    Transform _activeHand;
    static readonly List<Transform> s_handCandidates = new List<Transform>();
    static float s_nextHandScanTime;
    static readonly string[] s_knownHandNames =
    {
        "OLIVIA_Left_TransparentHand",
        "OLIVIA_Right_TransparentHand",
        "LeftHand",
        "RightHand",
        "Left Controller",
        "Right Controller",
        "[Left InteractionAttachController] Attach",
        "[Right InteractionAttachController] Attach",
        "[Left InteractionAttachController] Attach Child",
        "[Right InteractionAttachController] Attach Child",
        "Left Controller Stabilized Attach",
        "Right Controller Stabilized Attach"
    };

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
        // Pastikan nilai responsif & konsisten walau instance lama punya serialized value lama.
        if (_smoothTime > 0.1f) _smoothTime = 0.04f;
        if (_maxDegreesPerSecond < 360f) _maxDegreesPerSecond = 720f;
        // Twist-based: gain lama (5) bikin terlalu cepat. Clamp ke 1..1.8 (sedikit
        // amplifikasi, tetap terkendali & konsisten).
        gesturalGain = Mathf.Clamp(gesturalGain, 1f, 1.8f);
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
            {
                if (r == null || !IsFinite(r.bounds.center) || !IsFinite(r.bounds.size))
                    continue;
                if (!has) { b = r.bounds; has = true; } else b.Encapsulate(r.bounds);
            }
        _pivot = has ? b.center : (hub != null ? hub.position : _parts[0].position);
        if (!IsFinite(_pivot))
            _pivot = hub != null && IsFinite(hub.position) ? hub.position : transform.position;
        Vector3 s = has ? b.size : Vector3.one;
        _axis = (s.x <= s.y && s.x <= s.z) ? Vector3.right : (s.y <= s.z ? Vector3.up : Vector3.forward);
        _ringRadius = Mathf.Max(0.12f, Mathf.Max(s.x, s.y, s.z) * 0.5f);

        for (int i = 0; i < _parts.Length; i++)
        {
            _baseRot[i] = IsFinite(_parts[i].rotation) ? _parts[i].rotation : Quaternion.identity;
            _basePos[i] = IsFinite(_parts[i].position) ? _parts[i].position : _pivot;
        }
        _targetDeg = 0f; _displayDeg = 0f; _smoothVelocity = 0f; OpenPercent01 = 0f; _ready = true;
        EnsureInteractable(hub != null ? hub : _parts[0]);
    }

    void EnsureInteractable(Transform t)
    {
        var go = t.gameObject;
        var grab = t.GetComponent<XRGrabInteractable>(); if (grab) Destroy(grab);
        var rb = t.GetComponent<Rigidbody>(); if (rb) Destroy(rb);
        var sc = t.GetComponent<SphereCollider>();
        if (sc == null) sc = go.AddComponent<SphereCollider>();
        sc.radius = LocalRadiusForWorld(t, 0.7f);
        sc.isTrigger = false;
        var si = t.GetComponent<XRSimpleInteractable>(); if (si == null) si = go.AddComponent<XRSimpleInteractable>();
        si.colliders.Clear();
        foreach (var c in go.GetComponents<Collider>()) if (c != null) si.colliders.Add(c);
        si.enabled = false; si.enabled = true;
        si.selectEntered.RemoveAllListeners(); si.selectExited.RemoveAllListeners();
        si.hoverEntered.RemoveAllListeners(); si.hoverExited.RemoveAllListeners();
        si.selectEntered.AddListener(a => { _active = true; _attach = a.interactorObject != null ? a.interactorObject.transform : null; _yawValid = false; });
        si.selectExited.AddListener(a => { _active = false; _attach = null; _yawValid = false; });
        si.hoverEntered.AddListener(a => { _active = true; _attach = a.interactorObject != null ? a.interactorObject.transform : _attach; _yawValid = false; });
        si.hoverExited.AddListener(a => { _active = false; _yawValid = false; });
    }

    static float LocalRadiusForWorld(Transform t, float worldRadius)
    {
        Vector3 s = t != null ? t.lossyScale : Vector3.one;
        float maxAxis = Mathf.Max(Mathf.Abs(s.x), Mathf.Abs(s.y), Mathf.Abs(s.z), 0.0001f);
        return worldRadius / maxAxis;
    }

    void Update()
    {
        if (!_ready) return;

        float d = 0f;
        Transform directHand = ResolveDirectHandTracker();
        if (directHand != null)
        {
            d += GetPositionDelta(directHand);
        }
        else if (_active && _attach != null)
        {
            Vector3 handVec = _attach.up;
            Vector3 p = Vector3.ProjectOnPlane(handVec, _axis);
            if (p.sqrMagnitude < 0.02f)
            {
                handVec = _attach.right;
                p = Vector3.ProjectOnPlane(handVec, _axis);
            }

            if (p.sqrMagnitude > 0.0001f)
            {
                p.Normalize();
                Vector3 r = Vector3.ProjectOnPlane(Vector3.up, _axis);
                if (r.sqrMagnitude < 0.0001f)
                    r = Vector3.ProjectOnPlane(Vector3.right, _axis);
                r.Normalize();

                float yaw = Vector3.SignedAngle(r, p, _axis);
                if (!_yawValid)
                {
                    _yawLast = yaw;
                    _yawValid = true;
                }
                else
                {
                    float dy = Mathf.DeltaAngle(_yawLast, yaw);
                    _yawLast = yaw;
                    if (Mathf.Abs(dy) > 60f) dy = 0f;
                    d += dy * Mathf.Max(1f, gesturalGain);
                }
            }
        }
        else
        {
            _yawValid = false;
        }

        if (!float.IsFinite(d) || Mathf.Abs(d) < 0.0001f) return;

        _targetDeg = Mathf.Clamp(_targetDeg + d, 0f, fullOpenDegrees);
        if (!float.IsFinite(_targetDeg))
            _targetDeg = _displayDeg;
        _displayDeg = _targetDeg;
        Apply(_displayDeg);
        OpenPercent01 = Mathf.Clamp01(_displayDeg / fullOpenDegrees);
    }

    void Apply(float degrees)
    {
        if (!float.IsFinite(degrees) || !IsFinite(_pivot) || !IsFinite(_axis))
            return;

        Quaternion delta = Quaternion.AngleAxis(degrees, _axis);
        if (!IsFinite(delta))
            return;

        for (int i = 0; i < _parts.Length; i++)
        {
            if (_parts[i] == null) continue;
            Quaternion nextRotation = delta * _baseRot[i];
            Vector3 nextPosition = _pivot + delta * (_basePos[i] - _pivot);
            if (!IsFinite(nextRotation) || !IsFinite(nextPosition))
                continue;

            _parts[i].rotation = nextRotation;
            _parts[i].position = nextPosition;
        }
    }

    static bool IsFinite(Vector3 v)
    {
        return float.IsFinite(v.x) && float.IsFinite(v.y) && float.IsFinite(v.z);
    }

    static bool IsFinite(Quaternion q)
    {
        return float.IsFinite(q.x) && float.IsFinite(q.y) && float.IsFinite(q.z) && float.IsFinite(q.w);
    }


    Transform ResolveDirectHandTracker()
    {
        RefreshSharedHandCandidates();

        Transform best = null;
        float bestScore = float.MaxValue;
        for (int i = 0; i < s_handCandidates.Count; i++)
            ScoreHandCandidate(s_handCandidates[i], ref best, ref bestScore);

        if (best == null)
        {
            _activeHand = null;
            _yawValid = false;
            return null;
        }

        if (_activeHand != best)
        {
            _activeHand = best;
            _yawValid = false;
        }

        return best;
    }

    static void RefreshSharedHandCandidates()
    {
        if (Time.time < s_nextHandScanTime && s_handCandidates.Count > 0)
            return;

        s_nextHandScanTime = Time.time + 0.5f;
        s_handCandidates.Clear();

        for (int i = 0; i < s_knownHandNames.Length; i++)
        {
            GameObject go = GameObject.Find(s_knownHandNames[i]);
            if (go == null) continue;

            Transform t = go.transform;
            if (t == null || !t.gameObject.activeInHierarchy) continue;
            if (!s_handCandidates.Contains(t))
                s_handCandidates.Add(t);
        }
    }

    void ScoreHandCandidate(Transform candidate, ref Transform best, ref float bestScore)
    {
        if (candidate == null || !candidate.gameObject.activeInHierarchy) return;
        if (!IsFinite(candidate.position)) return;

        Vector3 fromCenter = candidate.position - _pivot;
        float axial = Mathf.Abs(Vector3.Dot(fromCenter, _axis.normalized));
        Vector3 inPlane = Vector3.ProjectOnPlane(fromCenter, _axis);
        float radial = inPlane.magnitude;
        if (!float.IsFinite(axial) || !float.IsFinite(radial)) return;

        float minRadius = Mathf.Max(0.05f, _ringRadius * 0.18f);
        float maxRadius = Mathf.Max(0.65f, _ringRadius * 2.2f);
        float maxAxial = Mathf.Max(0.75f, _ringRadius * 1.8f);
        if (radial < minRadius || radial > maxRadius || axial > maxAxial)
            return;

        float score = Mathf.Abs(radial - _ringRadius) + axial * 0.35f;
        if (score < bestScore)
        {
            bestScore = score;
            best = candidate;
        }
    }

    float GetPositionDelta(Transform hand)
    {
        if (hand == null || !IsFinite(hand.position) || !IsFinite(_pivot) || !IsFinite(_axis))
        {
            _yawValid = false;
            return 0f;
        }

        Vector3 inPlane = Vector3.ProjectOnPlane(hand.position - _pivot, _axis);
        if (!IsFinite(inPlane) || inPlane.sqrMagnitude < 0.0001f)
        {
            _yawValid = false;
            return 0f;
        }

        Vector3 reference = Vector3.ProjectOnPlane(Vector3.up, _axis);
        if (reference.sqrMagnitude < 0.0001f)
            reference = Vector3.ProjectOnPlane(Vector3.right, _axis);
        reference.Normalize();
        if (!IsFinite(reference))
        {
            _yawValid = false;
            return 0f;
        }

        float yaw = Vector3.SignedAngle(reference, inPlane.normalized, _axis);
        if (!float.IsFinite(yaw))
        {
            _yawValid = false;
            return 0f;
        }
        if (!_yawValid)
        {
            _yawLast = yaw;
            _yawValid = true;
            return 0f;
        }

        float delta = Mathf.DeltaAngle(_yawLast, yaw);
        _yawLast = yaw;
        if (!float.IsFinite(delta) || Mathf.Abs(delta) < 0.04f || Mathf.Abs(delta) > 75f)
            return 0f;

        return -delta * Mathf.Max(1f, gesturalGain);
    }

    static bool PathContains(Transform t, string token)
    {
        while (t != null)
        {
            if (t.name.IndexOf(token, System.StringComparison.OrdinalIgnoreCase) >= 0)
                return true;
            t = t.parent;
        }
        return false;
    }
}
