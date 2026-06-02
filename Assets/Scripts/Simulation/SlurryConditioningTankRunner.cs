using UnityEngine;

// Bikin Slurry Tank Level 3 jadi AGITATED CONDITIONING TANK ala HPAL nikel (bukan pajangan):
// agitator turbine berputar + kopling drive di atas, solid laterit tersuspensi berputar di
// dalam slurry (jaga ~38% solids tetap homogen), dan panel instrumen live. SEMUA additive
// (runtime, play-mode), TIDAK menyentuh pipa lurus yang sudah ada.
public class SlurryConditioningTankRunner : MonoBehaviour
{
    [SerializeField] float _rpm = 46f;          // putaran agitator
    [SerializeField] int _solidCount = 46;      // partikel ore tersuspensi
    Vector3 _c = new Vector3(91.41f, 0f, 55.14f); // pusat XZ tangki
    float _surfaceY = 1.78f, _bottomY = 0.45f, _radius = 5.7f;

    GameObject _root;
    Transform _turbine, _coupling;
    Transform[] _solids;
    float[] _ang, _rad, _baseY, _ph, _spin;
    TextMesh _txt; Transform _panel;
    Material _ore, _steel;
    float _t;

    void OnEnable() { if (Application.isPlaying) Build(); }
    void OnDisable() { if (_root != null) Destroy(_root); }

    Material Mat(Color c, float metal, float smooth)
    {
        var m = new Material(Shader.Find("Universal Render Pipeline/Lit"));
        m.SetColor("_BaseColor", c); m.SetFloat("_Metallic", metal); m.SetFloat("_Smoothness", smooth);
        return m;
    }

    GameObject Prim(PrimitiveType p, Transform parent, Material mat, Vector3 pos, Vector3 scl)
    {
        var g = GameObject.CreatePrimitive(p);
        var col = g.GetComponent<Collider>(); if (col) Destroy(col);
        g.transform.SetParent(parent, false);
        g.transform.localPosition = pos; g.transform.localScale = scl;
        g.GetComponent<Renderer>().sharedMaterial = mat;
        return g;
    }

    void Build()
    {
        if (transform.Find("L3_SlurryConditioning_Runtime") != null) return;
        _root = new GameObject("L3_SlurryConditioning_Runtime");
        _root.transform.SetParent(transform, false);
        _ore = Mat(new Color(0.40f, 0.27f, 0.16f), 0.0f, 0.35f);
        _steel = Mat(new Color(0.55f, 0.57f, 0.60f), 0.75f, 0.5f);

        // --- agitator turbine (pitched-blade) tercelup di slurry, poros ke gearbox atas ---
        var tg = new GameObject("Agitator_Turbine"); tg.transform.SetParent(_root.transform, false);
        tg.transform.position = new Vector3(_c.x, _bottomY + 0.7f, _c.z);
        _turbine = tg.transform;
        Prim(PrimitiveType.Cylinder, _turbine, _steel, Vector3.zero, new Vector3(0.55f, 0.18f, 0.55f)); // hub
        for (int i = 0; i < 5; i++)
        {
            var b = Prim(PrimitiveType.Cube, _turbine, _steel, Vector3.zero, new Vector3(2.7f, 0.08f, 0.55f));
            b.transform.localRotation = Quaternion.Euler(0, i * 72f, 22f);
            b.transform.localPosition = Quaternion.Euler(0, i * 72f, 0) * new Vector3(1.55f, 0, 0);
        }
        // poros vertikal turbine -> gearbox
        Prim(PrimitiveType.Cylinder, _root.transform, _steel,
            new Vector3(_c.x, (_bottomY + 8.0f) * 0.5f, _c.z), new Vector3(0.22f, (8.0f - _bottomY) * 0.5f, 0.22f));
        // kopling drive berputar tepat di bawah gearbox (bukti motor jalan)
        var cg = Prim(PrimitiveType.Cylinder, _root.transform, _steel, new Vector3(_c.x, 7.75f, _c.z), new Vector3(0.7f, 0.18f, 0.7f));
        _coupling = cg.transform;
        for (int i = 0; i < 4; i++)
        {
            var bolt = Prim(PrimitiveType.Cube, _coupling, _ore, Vector3.zero, new Vector3(0.14f, 0.3f, 0.14f));
            bolt.transform.localPosition = Quaternion.Euler(0, i * 90f, 0) * new Vector3(0.42f, 0, 0);
        }

        // --- solid laterit tersuspensi (fungsi inti: jaga solids homogen, tak mengendap) ---
        _solids = new Transform[_solidCount];
        _ang = new float[_solidCount]; _rad = new float[_solidCount];
        _baseY = new float[_solidCount]; _ph = new float[_solidCount]; _spin = new float[_solidCount];
        for (int i = 0; i < _solidCount; i++)
        {
            _ang[i] = Random.Range(0f, 360f);
            _rad[i] = Random.Range(0.6f, _radius);
            _baseY[i] = Random.Range(_bottomY + 0.3f, _surfaceY - 0.2f);
            _ph[i] = Random.Range(0f, 6.28f);
            _spin[i] = Random.Range(40f, 140f);
            float s = Random.Range(0.16f, 0.34f);
            var chunk = Prim(Random.value > 0.5f ? PrimitiveType.Cube : PrimitiveType.Sphere, _root.transform, _ore, Vector3.zero, new Vector3(s, s * 0.8f, s));
            chunk.transform.rotation = Random.rotation;
            _solids[i] = chunk.transform;
        }

        BuildPanel();
    }

    void BuildPanel()
    {
        _panel = new GameObject("Slurry_Instrument_Panel").transform;
        _panel.SetParent(_root.transform, false);
        _panel.position = new Vector3(_c.x - 2.0f, 5.6f, _c.z - _radius - 0.6f);
        var plate = Prim(PrimitiveType.Cube, _panel, Mat(new Color(0.07f, 0.09f, 0.11f), 0.2f, 0.4f), Vector3.zero, new Vector3(3.4f, 1.9f, 0.08f));
        var tg = new GameObject("Txt"); tg.transform.SetParent(_panel, false);
        tg.transform.localPosition = new Vector3(-1.55f, 0.78f, -0.06f);
        _txt = tg.AddComponent<TextMesh>();
        _txt.fontSize = 64; _txt.characterSize = 0.045f; _txt.color = new Color(0.5f, 1f, 0.7f);
        _txt.anchor = TextAnchor.UpperLeft;
        var f = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        if (f == null) f = Resources.GetBuiltinResource<Font>("Arial.ttf");
        if (f != null) { _txt.font = f; tg.GetComponent<MeshRenderer>().sharedMaterial = f.material; }
    }

    void Update()
    {
        if (!Application.isPlaying || _root == null) return;
        _t += Time.deltaTime;
        float w = _rpm * 6f; // deg/s
        if (_turbine) _turbine.Rotate(Vector3.up, w * Time.deltaTime, Space.World);
        if (_coupling) _coupling.Rotate(Vector3.up, w * Time.deltaTime, Space.World);

        if (_solids != null)
            for (int i = 0; i < _solids.Length; i++)
            {
                _ang[i] += (w * (0.5f + 0.5f * (_radius - _rad[i]) / _radius)) * Time.deltaTime; // dalam lebih cepat
                float a = _ang[i] * Mathf.Deg2Rad;
                float r = _rad[i] + 0.18f * Mathf.Sin(_t * 1.4f + _ph[i]);     // turbulensi radial
                float y = _baseY[i] + 0.22f * Mathf.Sin(_t * 1.1f + _ph[i] * 2f); // angkat-turun (suspensi)
                _solids[i].position = new Vector3(_c.x + Mathf.Cos(a) * r, y, _c.z + Mathf.Sin(a) * r);
                _solids[i].Rotate(Vector3.one, _spin[i] * Time.deltaTime, Space.Self);
            }

        if (_txt != null)
        {
            float dens = 38f + 0.6f * Mathf.Sin(_t * 0.7f);
            float lvl = 86f + 1.5f * Mathf.Sin(_t * 0.3f);
            float feed = 452f + 6f * Mathf.Sin(_t * 0.5f);
            _txt.text = "SLURRY CONDITIONING TANK (HPAL)\n" +
                        "Status   : SUSPENDED / HOMOGEN\n" +
                        "Density  : " + dens.ToString("F1") + " % solids\n" +
                        "Level    : " + lvl.ToString("F0") + " %\n" +
                        "Agitator : " + _rpm.ToString("F0") + " RPM\n" +
                        "Suhu     : 80 C\n" +
                        "Feed -> Pre-Heater : " + feed.ToString("F0") + " m3/h";
            var cam = Camera.main;
            if (cam != null && _panel != null)
            {
                _panel.rotation = Quaternion.LookRotation(_panel.position - cam.transform.position);
            }
        }
    }
}
