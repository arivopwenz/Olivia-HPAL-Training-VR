using UnityEngine;

/// <summary>
/// OLIVIA VR - AutoclaveSlurryFlowDriver.cs
///
/// Menggerakkan "plug" slurry panas (AutoclaveToFlash_SlurryPlug_xx) menyusuri pipa
/// dari Autoclave -> Flash Vessel. Unity-driven (bukan baked) supaya bebas masalah
/// konversi sumbu FBX.
///
/// Logic baru:
///  - AWAL: plug DISEMBUNYIKAN, aliran TIDAK jalan (FlowActive=false).
///  - StartFlow(): plug muncul mulai dari ujung Autoclave lalu bergerak ke Flash Vessel.
///  - FrontProgress01: 0..1 posisi plug TERDEPAN sepanjang pipa (1 = sudah sampai Flash Vessel).
/// </summary>
[DisallowMultipleComponent]
public class AutoclaveSlurryFlowDriver : MonoBehaviour
{
    [SerializeField] private string _plugPrefix = "AutoclaveToFlash_SlurryPlug";
    [Tooltip("Kecepatan aliran (m/detik). Lambat = cairan kental/padat.")]
    [SerializeField] private float _speed = 1.6f;
    [Tooltip("Rapatkan slug supaya terlihat padat (kecil = makin rapat).")]
    [SerializeField] private float _packing = 0.42f;

    // Waypoint WORLD pipa autoclave -> FV1 (route resmi, sama dgn build script).
    private static readonly Vector3[] _route = new Vector3[]
    {
        new Vector3(-35.2f, 3.0f, 84.0f),
        new Vector3(-48.0f, 3.0f, 88.0f),
        new Vector3(-60.0f, 4.6f, 92.5f),
        new Vector3(-66.5f, 6.4f, 96.8f),
    };

    private Transform[] _plugs;
    private float[] _arc;
    private float[] _segLen;
    private float _total;
    private bool _ready;
    private float _spacing;

    public bool FlowActive { get; private set; }
    public float PathTotal => _total;

    /// <summary>Posisi plug TERDEPAN dinormalisasi 0..1 (1 = sampai Flash Vessel).</summary>
    public float FrontProgress01
    {
        get
        {
            if (!_ready || _total <= 0.0001f) return 0f;
            float maxArc = 0f;
            for (int i = 0; i < _arc.Length; i++) if (_arc[i] > maxArc) maxArc = _arc[i];
            return Mathf.Clamp01(maxArc / _total);
        }
    }

    private void Awake()
    {
        BuildPath();
        CollectPlugs();
        _ready = _plugs != null && _plugs.Length > 0;
        // AWAL: sembunyikan + jangan mengalir.
        SetVisible(false);
        FlowActive = false;
    }

    private void BuildPath()
    {
        _segLen = new float[_route.Length - 1];
        _total = 0f;
        for (int i = 0; i < _route.Length - 1; i++)
        {
            _segLen[i] = Vector3.Distance(_route[i], _route[i + 1]);
            _total += _segLen[i];
        }
    }

    private void CollectPlugs()
    {
        var list = new System.Collections.Generic.List<Transform>();
        foreach (var t in GetComponentsInChildren<Transform>(true))
        {
            // HANYA group node slug (AutoclaveToFlash_SlurryPlug_NN), BUKAN child (_Liquid/_Cap/_Rock).
            string n = t.name;
            if (!n.StartsWith(_plugPrefix)) continue;
            // child slug mengandung suffix "_Liquid"/"_Cap"/"_Rock" -> skip.
            if (n.Contains("_Liquid") || n.Contains("_Cap") || n.Contains("_Rock")) continue;
            list.Add(t);
        }
        list.Sort((a, b) => string.CompareOrdinal(a.name, b.name));
        _plugs = list.ToArray();
        _arc = new float[_plugs.Length];
        _spacing = _plugs.Length > 0 ? (_total * _packing) / _plugs.Length : 0f;
    }

    public void SetVisible(bool on)
    {
        if (_plugs == null) return;
        foreach (var t in _plugs)
        {
            if (t == null) continue;
            foreach (var r in t.GetComponentsInChildren<Renderer>(true))
                r.enabled = on;
        }
    }

    /// <summary>Mulai aliran: plug muncul, mulai BERURUTAN dari ujung autoclave (arc negatif -> masuk pipa bertahap).</summary>
    public void StartFlow()
    {
        if (!_ready) return;
        for (int i = 0; i < _plugs.Length; i++)
        {
            // plug ke-0 mulai di awal pipa, sisanya antri di belakang (arc negatif) -> wave bergerak maju.
            _arc[i] = -_spacing * i;
            PlacePlug(i);
        }
        SetVisible(true);
        FlowActive = true;
    }

    public void StopFlow(bool hide)
    {
        FlowActive = false;
        if (hide) SetVisible(false);
    }

    private Vector3 PathPoint(float s)
    {
        if (_total <= 0.0001f) return _route[0];
        s = Mathf.Clamp(s, 0f, _total);   // tidak loop: clamp di ujung (slurry ngumpul di flash vessel)
        float acc = 0f;
        for (int i = 0; i < _segLen.Length; i++)
        {
            if (s <= acc + _segLen[i])
            {
                float f = (s - acc) / Mathf.Max(0.0001f, _segLen[i]);
                return Vector3.Lerp(_route[i], _route[i + 1], f);
            }
            acc += _segLen[i];
        }
        return _route[_route.Length - 1];
    }

    private void PlacePlug(int i)
    {
        if (_plugs[i] == null) return;
        float s = _arc[i];
        // plug yang belum masuk pipa (arc<0) disembunyikan sampai masuk.
        bool vis = FlowActive && s >= 0f;
        foreach (var r in _plugs[i].GetComponentsInChildren<Renderer>(true))
            r.enabled = vis;
        float sc = Mathf.Clamp(s, 0f, _total);
        Vector3 p = PathPoint(sc);
        Vector3 ahead = PathPoint(sc + 0.6f);
        _plugs[i].position = p;
        Vector3 dir = ahead - p;
        if (dir.sqrMagnitude > 0.0001f)
            _plugs[i].rotation = Quaternion.LookRotation(dir.normalized, Vector3.up);
    }

    private void Update()
    {
        if (!_ready || !FlowActive) return;
        float ds = _speed * Time.deltaTime;
        for (int i = 0; i < _plugs.Length; i++)
        {
            _arc[i] += ds;
            // setelah sampai ujung, recycle ke belakang supaya aliran kontinu (slurry terus mengalir).
            if (_arc[i] > _total) _arc[i] -= _total + _spacing;
            PlacePlug(i);
        }
        // Heat-haze HALUS (radiasi panas) - shimmer SMOOTH, BUKAN kelap-kelip.
        // Skala lebar slug naik-turun pelan (sin kontinu) + sedikit beda fase per slug.
        float t = Time.time;
        for (int i = 0; i < _plugs.Length; i++)
        {
            if (_plugs[i] == null) continue;
            float wob = 1f + 0.04f * Mathf.Sin(t * 1.7f + i * 0.9f);   // ±4% halus
            _plugs[i].localScale = new Vector3(wob, wob, 1f);
        }
    }
}
