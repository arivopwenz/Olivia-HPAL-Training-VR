using UnityEngine;

/// <summary>
/// OLIVIA VR - TankFluidColumn.cs
/// Komponen reusable cairan tabung industri: SATU volume cairan TERANG (shader Olivia/L7SlurryFill)
/// yang permukaannya NAIK DARI DASAR ke atas via world-Y clip (_FillY) — BUKAN melebar dari tengah.
///
/// Dipakai pada mesh volume penuh yang sudah ada (mis. *_LiquidGhost full-cylinder). Material di-override
/// jadi shader terang dengan depth gradient + surface glow band. Mesh "surface" disc tipis terpisah
/// cukup DISEMBUNYIKAN — volume ini sudah punya permukaan glow sendiri.
///
/// API:
///   Setup(volumeRenderer, shallow, deep, emis)  -> pasang shader, hitung bottom/top + swirl center dari bounds
///   SetLevel01(0..1)   -> permukaan naik dari dasar (0=kosong) ke penuh (1)
///   SetColors(...)     -> ubah warna fasa proses (dinamis)
///   SetSwirl(speed)    -> cairan berputar mengikuti rotor (0 = diam); dinyalakan saat agitator jalan
///   Hide() / Show()
/// </summary>
[DisallowMultipleComponent]
public class TankFluidColumn : MonoBehaviour
{
    private Renderer _r;
    private Material _mat;
    private float _bottomY, _topY;
    private float _level01;

    public bool Ready { get; private set; }
    public float Level01 => _level01;

    public void Setup(Renderer volumeRenderer, Color shallow, Color deep, Color emis)
    {
        if (volumeRenderer == null) return;
        _r = volumeRenderer;
        Bounds b = _r.bounds;
        _bottomY = b.min.y;
        _topY = b.max.y;

        Shader sh = Shader.Find("Olivia/L7SlurryFill");
        if (sh == null) sh = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard");
        _mat = new Material(sh) { name = "M_TankFluid_" + volumeRenderer.gameObject.name };
        if (_mat.HasProperty("_EmissionIntensity")) _mat.SetFloat("_EmissionIntensity", 0.22f);
        if (_mat.HasProperty("_SurfaceGlow")) _mat.SetFloat("_SurfaceGlow", 2.6f);
        if (_mat.HasProperty("_SurfaceWidth")) _mat.SetFloat("_SurfaceWidth", 0.35f);
        if (_mat.HasProperty("_DepthRange")) _mat.SetFloat("_DepthRange", Mathf.Max(2f, _topY - _bottomY));
        if (_mat.HasProperty("_Alpha")) _mat.SetFloat("_Alpha", 0.90f);
        if (_mat.HasProperty("_RippleStrength")) _mat.SetFloat("_RippleStrength", 0.05f);
        if (_mat.HasProperty("_SwirlStrength")) _mat.SetFloat("_SwirlStrength", 0.32f);
        // swirl center auto dari bounds: spacing = pusat X tabung (round(x/cx)*cx ~= cx di sekitar tabung),
        // axis Z = pusat Z tabung -> vortex berputar di poros tabung ini.
        if (_mat.HasProperty("_SwirlAxisZ")) _mat.SetFloat("_SwirlAxisZ", b.center.z);
        if (_mat.HasProperty("_SwirlSpacing")) _mat.SetFloat("_SwirlSpacing", Mathf.Max(1f, Mathf.Abs(b.center.x)));
        if (_mat.HasProperty("_SwirlSpeed")) _mat.SetFloat("_SwirlSpeed", 0f);
        _mat.EnableKeyword("_EMISSION");
        _r.sharedMaterial = _mat;
        SetColors(shallow, deep, emis);
        _r.gameObject.SetActive(true);
        SetLevel01(0f); // mulai KOSONG (ter-clip semua)
        Ready = true;
    }

    public void SetColors(Color shallow, Color deep, Color emis)
    {
        if (_mat == null) return;
        if (_mat.HasProperty("_BaseColor")) _mat.SetColor("_BaseColor", shallow);
        if (_mat.HasProperty("_DeepColor")) _mat.SetColor("_DeepColor", deep);
        if (_mat.HasProperty("_EmissionColor")) _mat.SetColor("_EmissionColor", emis);
    }

    public void SetLevel01(float t)
    {
        _level01 = Mathf.Clamp01(t);
        if (_mat == null) return;
        float y = Mathf.Lerp(_bottomY - 0.05f, _topY, _level01);
        if (_mat.HasProperty("_FillY")) _mat.SetFloat("_FillY", y);
    }

    public void SetSwirl(float speed)
    {
        if (_mat != null && _mat.HasProperty("_SwirlSpeed")) _mat.SetFloat("_SwirlSpeed", Mathf.Max(0f, speed));
    }

    public void Hide() { if (_r != null) _r.gameObject.SetActive(false); }
    public void Show() { if (_r != null) _r.gameObject.SetActive(true); }
}
