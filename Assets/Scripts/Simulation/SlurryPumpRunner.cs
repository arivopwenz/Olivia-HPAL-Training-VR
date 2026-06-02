using UnityEngine;

/// <summary>
/// OLIVIA VR - SlurryPumpRunner.cs
/// Slurry pump "hidup": rotor berputar + dengung motor + GETARAN khas mesin +
/// efek RADIASI/heat-haze (distorsi) + ASAP keluar dari cerobong atas pompa.
/// Pasang di root pump field (Pump_Skid_Industrial_Details).
/// </summary>
public class SlurryPumpRunner : MonoBehaviour
{
    [SerializeField] float _rpm = 240f;
    [SerializeField] float _humVolume = 0.35f;
    [SerializeField] float _vibAmp = 0.012f;

    Transform[] _rotors;
    Transform[] _pumps;
    Vector3[] _pumpBase;
    AudioSource _hum;
    ParticleSystem _smoke, _haze;
    float _et;
    bool _fxReady;

    void OnEnable()
    {
        var rl = new System.Collections.Generic.List<Transform>();
        var pl = new System.Collections.Generic.List<Transform>();
        foreach (var t in GetComponentsInChildren<Transform>(true))
        {
            if (t.name.IndexOf("ImpellerPivot", System.StringComparison.OrdinalIgnoreCase) >= 0) rl.Add(t);
            if (t.name.IndexOf("Level4_SlurryPump_Blender", System.StringComparison.OrdinalIgnoreCase) >= 0) pl.Add(t);
        }
        _rotors = rl.ToArray();
        _pumps = pl.ToArray();
        _pumpBase = new Vector3[_pumps.Length];
        for (int i = 0; i < _pumps.Length; i++) _pumpBase[i] = _pumps[i].localPosition;
        if (Application.isPlaying) { EnsureHum(); EnsureFx(); }
    }

    void EnsureHum()
    {
        if (_hum != null || _rotors == null || _rotors.Length == 0) return;
        var go = new GameObject("SlurryPump_Hum");
        go.transform.SetParent(transform, false);
        go.transform.position = _rotors[0].position;
        _hum = go.AddComponent<AudioSource>();
        _hum.clip = GenHum(2f, 44100);
        _hum.loop = true; _hum.spatialBlend = 1f; _hum.minDistance = 4f; _hum.maxDistance = 45f;
        _hum.volume = _humVolume; _hum.Play();
    }


    void EnsureFx()
    {
        if (_fxReady) return; _fxReady = true;
        Transform host = transform; Renderer hr = null; float best = 999f;
        foreach (var p in _pumps)
        {
            var r = p.GetComponentInChildren<Renderer>(true); if (r == null) continue;
            float d = Mathf.Abs(r.bounds.center.z - 55.7f) + Mathf.Abs(r.bounds.center.x - 62.8f);
            if (d < best) { best = d; host = p; hr = r; }
        }
        float topY = host.position.y + 4f; Vector3 c = host.position;
        if (hr != null)
        {
            c = hr.bounds.center; topY = c.y;
            foreach (var r in host.GetComponentsInChildren<Renderer>(true)) if (r.bounds.max.y > topY) topY = r.bounds.max.y;
            topY += 0.45f;
        }
        var tex = SoftTex();
        _smoke = BuildPS(host, "SlurryPump_Smoke", new Vector3(c.x, topY, c.z), tex,
                         new Color(0.45f, 0.45f, 0.47f, 0.55f), 1.1f, 0.45f, 2.6f, 0.18f);
        _haze = BuildPS(host, "SlurryPump_HeatHaze", new Vector3(c.x, topY - 0.7f, c.z), tex,
                        new Color(0.85f, 0.9f, 1f, 0.13f), 0.7f, 0.25f, 1.2f, 0.95f);
    }

    ParticleSystem BuildPS(Transform parent, string name, Vector3 pos, Texture2D tex, Color col,
                           float life, float startSize, float endSize, float noise)
    {
        var go = new GameObject(name); go.transform.SetParent(parent, true); go.transform.position = pos;
        var ps = go.AddComponent<ParticleSystem>(); ps.Stop();
        var main = ps.main;
        main.startLifetime = life; main.startSpeed = 0.5f; main.startSize = startSize;
        main.startColor = col; main.maxParticles = 240;
        main.simulationSpace = ParticleSystemSimulationSpace.World;
        var em = ps.emission; em.rateOverTime = 0f;
        var sh = ps.shape; sh.shapeType = ParticleSystemShapeType.Circle; sh.radius = 0.16f;
        var vel = ps.velocityOverLifetime; vel.enabled = true;
        vel.space = ParticleSystemSimulationSpace.World; vel.y = new ParticleSystem.MinMaxCurve(0.7f, 1.5f);
        var sz = ps.sizeOverLifetime; sz.enabled = true;
        sz.size = new ParticleSystem.MinMaxCurve(1f, new AnimationCurve(new Keyframe(0, startSize / endSize), new Keyframe(1, 1f)));
        var nz = ps.noise; nz.enabled = true; nz.strength = noise; nz.frequency = 0.7f; nz.scrollSpeed = 0.4f;
        var clr = ps.colorOverLifetime; clr.enabled = true;
        var g = new Gradient();
        g.SetKeys(new[] { new GradientColorKey(Color.white, 0), new GradientColorKey(Color.white, 1) },
                  new[] { new GradientAlphaKey(0f, 0f), new GradientAlphaKey(1f, 0.25f), new GradientAlphaKey(0f, 1f) });
        clr.color = g;
        var rend = go.GetComponent<ParticleSystemRenderer>();
        rend.material = new Material(Shader.Find("Sprites/Default")) { mainTexture = tex };
        rend.sortMode = ParticleSystemSortMode.Distance;
        ps.Play();
        return ps;
    }

    static Texture2D _soft;
    Texture2D SoftTex()
    {
        if (_soft != null) return _soft;
        int n = 48; _soft = new Texture2D(n, n, TextureFormat.ARGB32, false);
        var c = new Color[n * n]; float cx = (n - 1) * 0.5f;
        for (int y = 0; y < n; y++) for (int x = 0; x < n; x++)
        {
            float d = Mathf.Sqrt((x - cx) * (x - cx) + (y - cx) * (y - cx)) / cx;
            float a = Mathf.Clamp01(1f - d); c[y * n + x] = new Color(1, 1, 1, a * a);
        }
        _soft.SetPixels(c); _soft.Apply(); _soft.wrapMode = TextureWrapMode.Clamp; return _soft;
    }


    AudioClip GenHum(float dur, int sr)
    {
        int n = (int)(dur * sr); var d = new float[n]; float ph = 0f;
        for (int i = 0; i < n; i++)
        {
            float t = (float)i / sr;
            float s = 0.6f * Mathf.Sin(2f * Mathf.PI * 72f * t)
                    + 0.25f * Mathf.Sin(2f * Mathf.PI * 144f * t)
                    + 0.1f * Mathf.Sin(2f * Mathf.PI * 216f * t);
            ph = ph * 0.5f + (Random.value - 0.5f) * 0.5f;
            d[i] = (s + ph * 0.12f) * 0.5f;
        }
        var c = AudioClip.Create("SlurryPumpHum", n, 1, sr, false); c.SetData(d, 0); return c;
    }

    void Update()
    {
        float dt = Time.deltaTime; if (dt <= 0f) dt = 0.016f;
        if (_rotors != null)
        {
            float deg = _rpm * 6f * dt;
            for (int i = 0; i < _rotors.Length; i++)
                if (_rotors[i] != null) _rotors[i].Rotate(Vector3.forward, deg, Space.World);
        }
        if (!Application.isPlaying) return;
        if (_pumps != null)
        {
            float tt = Time.time;
            for (int i = 0; i < _pumps.Length; i++)
            {
                if (_pumps[i] == null) continue;
                float p = i * 1.7f;
                _pumps[i].localPosition = _pumpBase[i] + new Vector3(
                    Mathf.Sin(tt * 91f + p), Mathf.Sin(tt * 77f + p * 2f), Mathf.Sin(tt * 103f + p)) * _vibAmp;
            }
        }
        _et += dt;
        if (_et >= 0.08f)
        {
            int k = Mathf.Max(1, Mathf.RoundToInt(_et / 0.08f));
            if (_smoke != null) _smoke.Emit(k);
            if (_haze != null) _haze.Emit(k * 2);
            _et = 0f;
        }
    }
}
