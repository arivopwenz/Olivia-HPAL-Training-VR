using TMPro;
using UnityEngine;

public sealed class Level11MHPImmersiveVisuals : MonoBehaviour
{
    [SerializeField] private Level11MHPController controller;
    [SerializeField] private Renderer[] precipitationLiquidRenderers;
    [SerializeField] private Renderer[] mgoFeedFlowRenderers;
    [SerializeField] private Renderer[] mhpDischargeFlowRenderers;
    [SerializeField] private Transform[] rotatingProcessParts;
    [SerializeField] private Transform wetCakeFill;
    [SerializeField] private ParticleSystem precipitationParticles;
    [SerializeField] private ParticleSystem filterPressMist;
    [SerializeField] private TMP_Text analyzerScreen;

    private static readonly int BaseColor = Shader.PropertyToID("_BaseColor");
    private static readonly int ColorId = Shader.PropertyToID("_Color");
    private static readonly int EmissionColor = Shader.PropertyToID("_EmissionColor");
    private MaterialPropertyBlock _block;

    private void Reset()
    {
        ResolveReferences();
    }

    private void Awake()
    {
        _block = new MaterialPropertyBlock();
        ResolveReferences();
    }

    private void Update()
    {
        if (controller == null) return;
        if (_block == null) _block = new MaterialPropertyBlock();

        float mhpProgress = Mathf.Clamp01(controller.MHPQualityCurrent / 92f);
        float processActive = controller.LevelActive ? 1f : 0.35f;
        float ph = controller.PHCurrent;

        AnimateRotatingParts(processActive, mhpProgress);
        UpdateLiquid(ph, mhpProgress);
        UpdateFlowRenderers(mgoFeedFlowRenderers, Mathf.Clamp01((ph - 4f) / 3f), new Color(0.86f, 0.88f, 0.62f, 1f));
        UpdateFlowRenderers(mhpDischargeFlowRenderers, mhpProgress, new Color(0.18f, 0.70f, 0.40f, 1f));
        UpdateWetCake(mhpProgress);
        UpdateParticles(mhpProgress);
        UpdateAnalyzerScreen(mhpProgress);
    }

    private void ResolveReferences()
    {
        if (controller == null)
        {
            controller = GetComponentInParent<Level11MHPController>();
            if (controller == null) controller = FindFirstObjectByType<Level11MHPController>();
        }

        if (precipitationLiquidRenderers == null || precipitationLiquidRenderers.Length == 0)
        {
            precipitationLiquidRenderers = FindRenderersByName("MHP_Precipitation_Tank_Liquid");
        }

        if (mgoFeedFlowRenderers == null || mgoFeedFlowRenderers.Length == 0)
        {
            mgoFeedFlowRenderers = FindRenderersByName("MGO_Visible_Discharge_Flow");
        }

        if (mhpDischargeFlowRenderers == null || mhpDischargeFlowRenderers.Length == 0)
        {
            mhpDischargeFlowRenderers = FindRenderersByName("MHP_Discharge_To_Filter_Flow");
        }

        if (rotatingProcessParts == null || rotatingProcessParts.Length == 0)
        {
            rotatingProcessParts = new[]
            {
                FindTransform("MGO_Dosing_Skid_ScrewFeeder"),
                FindTransform("MGO_Dosing_Skid_MixMotor"),
                FindTransform("FilterPress_FeedPump_Coupling"),
                FindTransform("MHP_Precipitation_Tank_DriveMotor")
            };
        }

        if (wetCakeFill == null) wetCakeFill = FindTransform("MHP_WetCake_TrayFill");
        if (precipitationParticles == null) precipitationParticles = FindComponent<ParticleSystem>("MHP_Precipitation_ProductCloud");
        if (filterPressMist == null) filterPressMist = FindComponent<ParticleSystem>("FilterPress_DewaterMist");
        if (analyzerScreen == null) analyzerScreen = FindComponent<TMP_Text>("MHP_Analyzer_ResultText");
    }

    private void AnimateRotatingParts(float active, float mhpProgress)
    {
        float speed = Mathf.Lerp(35f, 180f, mhpProgress) * active;
        foreach (Transform part in rotatingProcessParts)
        {
            if (part == null) continue;
            part.Rotate(Vector3.forward, speed * Time.deltaTime, Space.Self);
        }
    }

    private void UpdateLiquid(float ph, float mhpProgress)
    {
        Color pls = new Color(0.22f, 0.48f, 0.52f, 0.68f);
        Color reacting = new Color(0.34f, 0.58f, 0.40f, 0.76f);
        Color mhpSlurry = new Color(0.20f, 0.62f, 0.32f, 0.88f);
        float phBlend = Mathf.Clamp01((ph - 4f) / 3f);
        Color c = Color.Lerp(Color.Lerp(pls, reacting, phBlend), mhpSlurry, mhpProgress);

        foreach (Renderer r in precipitationLiquidRenderers)
        {
            if (r == null) continue;
            r.GetPropertyBlock(_block);
            _block.SetColor(BaseColor, c);
            _block.SetColor(ColorId, c);
            _block.SetColor(EmissionColor, new Color(0.10f, 0.45f, 0.22f, 1f) * Mathf.Lerp(0.25f, 1.4f, mhpProgress));
            r.SetPropertyBlock(_block);
        }
    }

    private void UpdateFlowRenderers(Renderer[] renderers, float progress, Color color)
    {
        float pulse = progress <= 0.01f ? 0f : Mathf.Lerp(0.45f, 1.2f, 0.5f + 0.5f * Mathf.Sin(Time.time * 7f));
        Color c = new Color(color.r, color.g, color.b, Mathf.Lerp(0.12f, 0.92f, progress));

        foreach (Renderer r in renderers)
        {
            if (r == null) continue;
            r.enabled = progress > 0.01f;
            r.GetPropertyBlock(_block);
            _block.SetColor(BaseColor, c);
            _block.SetColor(ColorId, c);
            _block.SetColor(EmissionColor, color * pulse);
            r.SetPropertyBlock(_block);
        }
    }

    private void UpdateWetCake(float progress)
    {
        if (wetCakeFill == null) return;
        wetCakeFill.gameObject.SetActive(progress > 0.05f);
        Vector3 scale = wetCakeFill.localScale;
        scale.y = Mathf.Lerp(0.02f, 0.28f, progress);
        wetCakeFill.localScale = scale;
    }

    private void UpdateParticles(float progress)
    {
        SetEmission(precipitationParticles, Mathf.Lerp(0f, 55f, progress));
        SetEmission(filterPressMist, Mathf.Lerp(0f, 14f, Mathf.Clamp01((progress - 0.55f) / 0.45f)));
    }

    private void UpdateAnalyzerScreen(float mhpProgress)
    {
        if (analyzerScreen == null) return;
        if (!controller.Stage1Done)
        {
            analyzerScreen.text = "PLS ANALYZER\nFe 3.80 g/L HIGH\nAl 1.70 g/L HIGH\nNi 5.10 g/L\nCo 0.52 g/L\nACTION: Fe removal pH 2.5";
        }
        else if (!controller.Stage2Done)
        {
            analyzerScreen.text = "AFTER Fe REMOVAL\nFe trending down\nAl still HIGH\nNi-Co retained\nACTION: Al/Cr removal pH 4.0";
        }
        else if (!controller.Stage3Done)
        {
            analyzerScreen.text = "VALIDATION SAMPLE\nFe LOW\nAl LOW\nNi-Co still in solution\nOPEN VALVE TO MHP";
        }
        else
        {
            analyzerScreen.text = "MHP PRECIPITATION\npH " + controller.PHCurrent.ToString("0.0") +
                                  "\nNi-Co precipitate " + Mathf.RoundToInt(mhpProgress * 100f) +
                                  "%\nFILTER WET CAKE";
        }
    }

    private void SetEmission(ParticleSystem ps, float rate)
    {
        if (ps == null) return;
        ParticleSystem.EmissionModule emission = ps.emission;
        emission.rateOverTime = rate;
        if (rate > 0.1f)
        {
            if (!ps.isPlaying) ps.Play();
        }
        else if (ps.isPlaying)
        {
            ps.Stop(true, ParticleSystemStopBehavior.StopEmitting);
        }
    }

    private Renderer[] FindRenderersByName(string contains)
    {
        var list = new System.Collections.Generic.List<Renderer>();
        foreach (Transform t in GetComponentsInChildren<Transform>(true))
        {
            if (!t.name.Contains(contains)) continue;
            Renderer r = t.GetComponent<Renderer>();
            if (r != null) list.Add(r);
        }
        return list.ToArray();
    }

    private Transform FindTransform(string contains)
    {
        foreach (Transform t in GetComponentsInChildren<Transform>(true))
        {
            if (t.name.Contains(contains)) return t;
        }
        return null;
    }

    private T FindComponent<T>(string contains) where T : Component
    {
        Transform t = FindTransform(contains);
        return t == null ? null : t.GetComponent<T>();
    }
}
