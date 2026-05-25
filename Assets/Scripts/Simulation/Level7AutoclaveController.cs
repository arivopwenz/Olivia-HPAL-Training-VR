using System.Collections;
using UnityEngine;

/// <summary>
/// OLIVIA VR - Level7AutoclaveController.cs
///
/// LEVEL 7 — Field: Autoclave Monitoring & X-Ray Inspection
///
/// FLOW:
///   1. Player teleports to Autoclave inspection platform
///   2. HUD: "Inspect the Autoclave. Activate X-Ray vision to see inside."
///   3. Player presses X-Ray button (controller) → Autoclave shell becomes transparent
///   4. Inside visible: agitator spinning, purple slurry swirling, hematite particles settling
///   5. Player reads 3 analog gauges on Autoclave body:
///      - Pressure: 50 atm (target 45-50)
///      - Temperature: 252°C (target 250-255)
///      - RPM display: 60 (target 60)
///   6. Player points controller at each gauge → readout appears in HUD
///   7. After all 3 gauges inspected → quest complete
///   8. Player reports via WT: "Autoclave normal, suhu 250, tekanan 50, agitator 60 RPM"
///   9. Fade → teleport to DCS for Level 8
///
/// KEY FEATURES:
///   - X-Ray Vision: shell material swap to transparent + inner components visible
///   - Agitator rotation animation (60 RPM)
///   - Fluid simulation shader (vertex displacement noise on slurry mesh)
///   - Hematite particle settling at bottom
///   - Gauge inspection via XR ray pointer
///   - Procedural audio: reactor hum + agitator whir
/// </summary>
public class Level7AutoclaveController : MonoBehaviour
{
    [Header("=== Player Reference ===")]
    [SerializeField] private Transform _playerRigRoot;

    [Header("=== Teleport ===")]
    [SerializeField] private Transform _teleportTargetAutoclave;
    [SerializeField] private Transform _teleportTargetDcs;
    [SerializeField] private Vector3 _offsetObservasiAutoclave = new Vector3(0f, 3f, 4f);

    [Header("=== Autoclave Reference ===")]
    [SerializeField] private GameObject _autoclaveField;
    [SerializeField] private Renderer _shellRenderer;
    [SerializeField] private Renderer[] _endCapRenderers;
    [SerializeField] private Transform _agitatorShaft;
    [SerializeField] private Transform[] _agitatorShafts;

    [Header("=== X-Ray Vision ===")]
    [Tooltip("Material to swap shell to when X-Ray is active (transparent ghost).")]
    [SerializeField] private Material _xrayMaterial;
    [Tooltip("Objects to show ONLY when X-Ray is active (inner components).")]
    [SerializeField] private GameObject[] _xrayOnlyObjects;
    [Tooltip("Key to toggle X-Ray (keyboard testing). VR uses controller button.")]
    [SerializeField] private KeyCode _xrayToggleKey = KeyCode.X;
    [SerializeField] private bool _xrayActive;

    [Header("=== Agitator Animation ===")]
    [Tooltip("RPM of agitator shaft rotation.")]
    [SerializeField] private float _agitatorRPM = 60f;
    [SerializeField] private Vector3 _agitatorAxis = Vector3.right;

    [Header("=== Inner Fluid Visual ===")]
    [Tooltip("Mesh representing slurry inside autoclave (cylinder). Auto-create if null.")]
    [SerializeField] private Transform _innerFluid;
    [Tooltip("Material for inner fluid (purple slurry with vertex noise).")]
    [SerializeField] private Material _innerFluidMaterial;

    [Header("=== Gauge Inspection ===")]
    [Tooltip("Transform of pressure gauge needle.")]
    [SerializeField] private Transform _pressureGaugeNeedle;
    [Tooltip("Transform of temperature gauge needle.")]
    [SerializeField] private Transform _temperatureGaugeNeedle;
    [SerializeField] private float _pressureValue = 50f;
    [SerializeField] private float _temperatureValue = 252f;
    [SerializeField] private float _rpmValue = 60f;
    [SerializeField] private int _gaugesInspected;
    [SerializeField] private bool _pressureInspected;
    [SerializeField] private bool _temperatureInspected;
    [SerializeField] private bool _rpmInspected;

    [Header("=== Audio ===")]
    [SerializeField] private AudioSource _reactorHumAudio;
    [SerializeField] private AudioSource _agitatorWhirAudio;
    [Range(0f, 1f)] [SerializeField] private float _reactorHumVolume = 0.5f;
    [Range(0f, 1f)] [SerializeField] private float _agitatorWhirVolume = 0.35f;

    [Header("=== Heat Recovery Vapor Route ===")]
    [SerializeField] private ProcessPipeNetwork _pipeNetwork;
    [SerializeField] private string[] _heatRecoveryRouteIds =
    {
        "Autoclave_Vapor_To_HeatReceiver",
        "HeatReceiver_Internal_Logic",
        "HeatReceiver_To_Preheater"
    };

    [Header("=== Heat Recovery Steam FX ===")]
    [SerializeField] private ParticleSystem[] _heatRecoverySteamFx;
    [SerializeField] private float _heatRecoverySteamEmission = 42f;
    [SerializeField] private bool _autoCreateHeatRecoverySteamFx = true;

    [Header("=== Timing ===")]
    [SerializeField] private float _fadeTransitionDuration = 2.5f;

    [Header("=== HUD Messages ===")]
    [TextArea(2, 4)] [SerializeField] private string _msgStart =
        "Inspect the Autoclave. Press X to activate X-Ray vision.";
    [TextArea(2, 4)] [SerializeField] private string _msgXrayOn =
        "X-Ray active! Look inside: agitator spinning, slurry reacting. Point at gauges to read.";
    [TextArea(2, 4)] [SerializeField] private string _msgGaugeRead =
        "Gauge read: {0}. ({1}/3 inspected)";
    [TextArea(2, 4)] [SerializeField] private string _msgAllInspected =
        "All parameters confirmed! Report via WT: 'Autoclave normal, suhu 250, tekanan 50, agitator 60 RPM'.";

    private PlayerHUD _hud;
    private Material _originalShellMaterial;
    private Material[] _originalEndCapMaterials;
    private float _agitatorAngle;
    private bool _questComplete;
    private bool _levelActive;

    private void Awake()
    {
        _hud = FindObjectOfType<PlayerHUD>();
        AutoFindReferences();
        EnsureXRayMaterial();
        EnsureInnerFluid();
        EnsureAudio();
        EnsureHeatRecoverySteamFx();
        SetHeatRecoverySteamFx(false);
        SetXRayObjectsVisible(false);
    }

    private void OnEnable()
    {
        GameLevelManager.OnLevelStarted += OnLevelStarted;
    }

    private void OnDisable()
    {
        GameLevelManager.OnLevelStarted -= OnLevelStarted;
        SetHeatRecoveryFlow(false);
    }

    private void OnLevelStarted(GameLevelManager.GameLevel level)
    {
        _levelActive = level == GameLevelManager.GameLevel.Level7_Autoclave;
        if (_levelActive)
        {
            _xrayActive = false;
            _questComplete = false;
            _gaugesInspected = 0;
            _pressureInspected = false;
            _temperatureInspected = false;
            _rpmInspected = false;
            SetXRayObjectsVisible(false);
            RestoreShellMaterial();
            SetHeatRecoveryFlow(true);
            StartReactorAudio();
            if (_hud != null) _hud.ShowNotifPublic(_msgStart);

            // Set GLM parameters
            if (GameLevelManager.Instance != null)
            {
                GameLevelManager.Instance.SetSuhu(_temperatureValue);
                GameLevelManager.Instance.SetTekanan(_pressureValue);
                GameLevelManager.Instance.SetRPM(_rpmValue);
            }
        }
        else
        {
            SetXRayObjectsVisible(false);
            RestoreShellMaterial();
            SetHeatRecoveryFlow(false);
            StopReactorAudio();
        }
    }

    private void Update()
    {
        if (!_levelActive) return;

        // Agitator rotation
        AnimateAgitator();

        // X-Ray toggle (keyboard for testing, VR via event)
        if (Input.GetKeyDown(_xrayToggleKey))
            ToggleXRay();

        // Inner fluid wobble (simple sine displacement on Y scale)
        AnimateInnerFluid();
    }

    // ============================================================
    //  X-RAY VISION
    // ============================================================

    public void ToggleXRay()
    {
        _xrayActive = !_xrayActive;
        if (_xrayActive)
        {
            ApplyXRayMaterial();
            SetXRayObjectsVisible(true);
            if (_hud != null) _hud.ShowNotifPublic(_msgXrayOn);
        }
        else
        {
            RestoreShellMaterial();
            SetXRayObjectsVisible(false);
        }
    }

    private void ApplyXRayMaterial()
    {
        if (_shellRenderer != null)
        {
            if (_originalShellMaterial == null)
                _originalShellMaterial = _shellRenderer.sharedMaterial;
            _shellRenderer.sharedMaterial = _xrayMaterial;
        }
        if (_endCapRenderers != null)
        {
            if (_originalEndCapMaterials == null)
            {
                _originalEndCapMaterials = new Material[_endCapRenderers.Length];
                for (int i = 0; i < _endCapRenderers.Length; i++)
                    if (_endCapRenderers[i] != null)
                        _originalEndCapMaterials[i] = _endCapRenderers[i].sharedMaterial;
            }
            foreach (var r in _endCapRenderers)
                if (r != null) r.sharedMaterial = _xrayMaterial;
        }
    }

    private void RestoreShellMaterial()
    {
        if (_shellRenderer != null && _originalShellMaterial != null)
            _shellRenderer.sharedMaterial = _originalShellMaterial;
        if (_endCapRenderers != null && _originalEndCapMaterials != null)
        {
            for (int i = 0; i < _endCapRenderers.Length; i++)
                if (_endCapRenderers[i] != null && i < _originalEndCapMaterials.Length)
                    _endCapRenderers[i].sharedMaterial = _originalEndCapMaterials[i];
        }
    }

    private void SetXRayObjectsVisible(bool visible)
    {
        if (_xrayOnlyObjects == null) return;
        foreach (var go in _xrayOnlyObjects)
            if (go != null) go.SetActive(visible);
        if (_innerFluid != null)
            _innerFluid.gameObject.SetActive(visible);
    }

    // ============================================================
    //  AGITATOR ANIMATION
    // ============================================================

    private void AnimateAgitator()
    {
        float degPerSec = _agitatorRPM * 6f; // RPM * 360/60
        _agitatorAngle += degPerSec * Time.deltaTime;

        if (_agitatorShafts != null && _agitatorShafts.Length > 0)
        {
            Quaternion rotation = Quaternion.AngleAxis(_agitatorAngle, _agitatorAxis);
            foreach (Transform shaft in _agitatorShafts)
            {
                if (shaft != null)
                    shaft.localRotation = rotation;
            }
            return;
        }

        if (_agitatorShaft != null)
            _agitatorShaft.localRotation = Quaternion.AngleAxis(_agitatorAngle, _agitatorAxis);
    }

    // ============================================================
    //  INNER FLUID ANIMATION
    // ============================================================

    private void AnimateInnerFluid()
    {
        if (_innerFluid == null) return;
        // Simple wobble: scale Y oscillates slightly (simulates turbulent fluid)
        float wobble = 1f + Mathf.Sin(Time.time * 2.5f) * 0.03f;
        Vector3 s = _innerFluid.localScale;
        s.y = 0.85f * wobble; // base scale 0.85 of shell
        _innerFluid.localScale = s;

        // Slow rotation to simulate swirling
        _innerFluid.Rotate(Vector3.up, 15f * Time.deltaTime, Space.Self);
    }

    private void EnsureInnerFluid()
    {
        if (_innerFluid != null) return;
        if (_autoclaveField == null) return;

        var go = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        go.name = "Autoclave_InnerFluid";
        var col = go.GetComponent<Collider>();
        if (col != null) DestroyImmediate(col);
        go.transform.SetParent(_autoclaveField.transform, false);
        // Position inside shell (same as shell center)
        var shell = _autoclaveField.transform.Find("Shell");
        if (shell != null)
        {
            go.transform.position = shell.position;
            go.transform.rotation = shell.rotation;
            go.transform.localScale = new Vector3(0.85f, 0.85f, 0.85f); // slightly smaller than shell
        }

        if (_innerFluidMaterial == null)
        {
            // Purple slurry material (same as tank)
            var tankFill = GameObject.Find("Mesin Utama/Slurry Tank/Slurry_Fill");
            if (tankFill != null)
            {
                var mr = tankFill.GetComponent<MeshRenderer>();
                if (mr != null) _innerFluidMaterial = mr.sharedMaterial;
            }
            if (_innerFluidMaterial == null)
            {
                _innerFluidMaterial = new Material(Shader.Find("Universal Render Pipeline/Lit"));
                _innerFluidMaterial.SetColor("_BaseColor", new Color(0.42f, 0.18f, 0.55f, 0.9f));
                _innerFluidMaterial.SetFloat("_Smoothness", 0.8f);
                _innerFluidMaterial.EnableKeyword("_EMISSION");
                _innerFluidMaterial.SetColor("_EmissionColor", new Color(0.42f, 0.18f, 0.55f) * 0.8f);
            }
        }
        go.GetComponent<MeshRenderer>().sharedMaterial = _innerFluidMaterial;
        _innerFluid = go.transform;
        go.SetActive(false); // only visible in X-Ray mode
    }

    // ============================================================
    //  GAUGE INSPECTION
    // ============================================================

    /// <summary>
    /// Called when player points at and clicks a gauge. Pass gauge type: "pressure", "temperature", "rpm".
    /// Can be triggered by XR ray interactable on each gauge object.
    /// </summary>
    public void InspectGauge(string gaugeType)
    {
        if (!_levelActive || _questComplete) return;

        string readout = "";
        switch (gaugeType.ToLower())
        {
            case "pressure":
                if (!_pressureInspected) { _pressureInspected = true; _gaugesInspected++; }
                readout = "Pressure: " + _pressureValue.ToString("F0") + " atm (Target: 45-50)";
                break;
            case "temperature":
                if (!_temperatureInspected) { _temperatureInspected = true; _gaugesInspected++; }
                readout = "Temperature: " + _temperatureValue.ToString("F0") + "°C (Target: 250-255)";
                break;
            case "rpm":
                if (!_rpmInspected) { _rpmInspected = true; _gaugesInspected++; }
                readout = "Agitator RPM: " + _rpmValue.ToString("F0") + " (Target: 60)";
                break;
        }

        if (_hud != null && !string.IsNullOrEmpty(readout))
            _hud.ShowNotifPublic(string.Format(_msgGaugeRead, readout, _gaugesInspected));

        if (_gaugesInspected >= 3 && !_questComplete)
        {
            _questComplete = true;
            GameLevelManager.Instance?.NotifyLevel7AutoclaveInspectionComplete();
            if (_hud != null) _hud.ShowNotifPublic(_msgAllInspected);
            Debug.Log("[Level7] All 3 gauges inspected. Quest complete.");
        }
    }

    // ============================================================
    //  AUDIO
    // ============================================================

    private void EnsureAudio()
    {
        if (_reactorHumAudio == null)
        {
            var go = new GameObject("ReactorHum_Audio");
            go.transform.SetParent(transform, false);
            _reactorHumAudio = go.AddComponent<AudioSource>();
            _reactorHumAudio.spatialBlend = 0.4f;
            _reactorHumAudio.loop = true;
            _reactorHumAudio.playOnAwake = false;
            _reactorHumAudio.volume = 0f;
            _reactorHumAudio.clip = GenerateReactorHum(5f, 22050);
        }
        if (_agitatorWhirAudio == null)
        {
            var go = new GameObject("AgitatorWhir_Audio");
            go.transform.SetParent(transform, false);
            _agitatorWhirAudio = go.AddComponent<AudioSource>();
            _agitatorWhirAudio.spatialBlend = 0.4f;
            _agitatorWhirAudio.loop = true;
            _agitatorWhirAudio.playOnAwake = false;
            _agitatorWhirAudio.volume = 0f;
            _agitatorWhirAudio.clip = GenerateAgitatorWhir(4f, 22050);
        }
    }

    private void StartReactorAudio()
    {
        if (_reactorHumAudio != null) { _reactorHumAudio.volume = _reactorHumVolume; _reactorHumAudio.Play(); }
        if (_agitatorWhirAudio != null) { _agitatorWhirAudio.volume = _agitatorWhirVolume; _agitatorWhirAudio.Play(); }
    }

    private void StopReactorAudio()
    {
        if (_reactorHumAudio != null) _reactorHumAudio.Stop();
        if (_agitatorWhirAudio != null) _agitatorWhirAudio.Stop();
    }

    private void SetHeatRecoveryFlow(bool active)
    {
        EnsureHeatRecoverySteamFx();

        if (_pipeNetwork == null)
        {
            var mesinUtama = GameObject.Find("Mesin Utama");
            if (mesinUtama != null)
                _pipeNetwork = mesinUtama.GetComponent<ProcessPipeNetwork>();
        }

        if (_pipeNetwork == null || _heatRecoveryRouteIds == null)
            return;

        for (int i = 0; i < _heatRecoveryRouteIds.Length; i++)
        {
            string routeId = _heatRecoveryRouteIds[i];
            if (!string.IsNullOrWhiteSpace(routeId))
                _pipeNetwork.SetRouteFlowActive(routeId, active);
        }

        SetHeatRecoverySteamFx(active);
    }

    private void EnsureHeatRecoverySteamFx()
    {
        if (_heatRecoverySteamFx != null && _heatRecoverySteamFx.Length > 0)
            return;

        var found = new System.Collections.Generic.List<ParticleSystem>();
        foreach (var ps in Resources.FindObjectsOfTypeAll<ParticleSystem>())
        {
            if (ps == null || !ps.gameObject.scene.IsValid()) continue;
            string n = ps.gameObject.name;
            if (n.StartsWith("HR_Steam") || n == "HeatRecovery_SteamFX_Group")
                found.Add(ps);
        }

        if (found.Count == 0 && _autoCreateHeatRecoverySteamFx)
        {
            var parent = GameObject.Find("Autoclave_HeatRecovery_System");
            Transform root = parent != null ? parent.transform : transform;
            found.Add(CreateHeatRecoverySteamFx("HR_Steam_Riser_FX", new Vector3(17.125f, 7.05f, 34.597f), root));
            found.Add(CreateHeatRecoverySteamFx("HR_Steam_Run_FX", new Vector3(24.2f, 7.05f, 40.4f), root));
            found.Add(CreateHeatRecoverySteamFx("HR_Steam_PreheaterTieIn_FX", new Vector3(34.05f, 5.95f, 46.10f), root));
        }

        _heatRecoverySteamFx = found.ToArray();
    }

    private ParticleSystem CreateHeatRecoverySteamFx(string name, Vector3 position, Transform parent)
    {
        var go = new GameObject(name);
        if (parent != null) go.transform.SetParent(parent, true);
        go.transform.position = position;
        go.transform.rotation = Quaternion.Euler(-35f, 0f, 0f);
        var ps = go.AddComponent<ParticleSystem>();
        ConfigureHeatRecoverySteamFx(ps);
        return ps;
    }

    private void ConfigureHeatRecoverySteamFx(ParticleSystem ps)
    {
        if (ps == null) return;
        ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);

        var main = ps.main;
        main.playOnAwake = false;
        main.loop = true;
        main.duration = 4f;
        main.startLifetime = new ParticleSystem.MinMaxCurve(0.7f, 1.6f);
        main.startSpeed = new ParticleSystem.MinMaxCurve(0.35f, 1.1f);
        main.startSize = new ParticleSystem.MinMaxCurve(0.18f, 0.5f);
        main.startColor = new Color(1f, 0.92f, 0.72f, 0.42f);
        main.simulationSpace = ParticleSystemSimulationSpace.World;
        main.maxParticles = 160;

        var emission = ps.emission;
        emission.enabled = true;
        emission.rateOverTime = 0f;

        var shape = ps.shape;
        shape.enabled = true;
        shape.shapeType = ParticleSystemShapeType.Cone;
        shape.angle = 18f;
        shape.radius = 0.22f;
        shape.length = 0.55f;

        var velocity = ps.velocityOverLifetime;
        velocity.enabled = true;
        velocity.space = ParticleSystemSimulationSpace.World;
        velocity.x = new ParticleSystem.MinMaxCurve(0f, 0f);
        velocity.y = new ParticleSystem.MinMaxCurve(0.15f, 0.55f);
        velocity.z = new ParticleSystem.MinMaxCurve(0f, 0f);

        var color = ps.colorOverLifetime;
        color.enabled = true;
        var gradient = new Gradient();
        gradient.SetKeys(
            new[] { new GradientColorKey(new Color(1f, 0.86f, 0.55f), 0f), new GradientColorKey(Color.white, 1f) },
            new[] { new GradientAlphaKey(0f, 0f), new GradientAlphaKey(0.42f, 0.18f), new GradientAlphaKey(0f, 1f) });
        color.color = gradient;

        var renderer = ps.GetComponent<ParticleSystemRenderer>();
        renderer.renderMode = ParticleSystemRenderMode.Billboard;
        var shader = Shader.Find("Universal Render Pipeline/Particles/Unlit");
        if (shader == null) shader = Shader.Find("Particles/Standard Unlit");
        if (shader == null) shader = Shader.Find("Sprites/Default");
        if (shader != null)
        {
            var mat = new Material(shader);
            mat.name = "HR_Steam_FX_Material";
            mat.color = new Color(1f, 0.9f, 0.65f, 0.38f);
            if (mat.HasProperty("_BaseColor")) mat.SetColor("_BaseColor", mat.color);
            if (mat.HasProperty("_Color")) mat.SetColor("_Color", mat.color);
            renderer.sharedMaterial = mat;
        }
    }

    private void SetHeatRecoverySteamFx(bool active)
    {
        if (_heatRecoverySteamFx == null) return;
        for (int i = 0; i < _heatRecoverySteamFx.Length; i++)
        {
            var ps = _heatRecoverySteamFx[i];
            if (ps == null) continue;
            ConfigureHeatRecoverySteamFx(ps);
            var emission = ps.emission;
            emission.rateOverTime = active ? _heatRecoverySteamEmission : 0f;
            ps.gameObject.SetActive(true);
            if (active) ps.Play(true);
            else ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
        }
    }

    private AudioClip GenerateReactorHum(float duration, int sampleRate)
    {
        int total = Mathf.CeilToInt(duration * sampleRate);
        float[] data = new float[total];
        float p1 = 0f, p2 = 0f;
        for (int i = 0; i < total; i++)
        {
            p1 += 2f * Mathf.PI * 50f / sampleRate;  // 50Hz deep hum
            p2 += 2f * Mathf.PI * 100f / sampleRate; // 100Hz harmonic
            data[i] = (Mathf.Sin(p1) * 0.6f + Mathf.Sin(p2) * 0.3f) * 0.35f;
        }
        int fade = Mathf.Min(2000, total / 20);
        for (int i = 0; i < fade; i++) { float f = (float)i / fade; data[i] *= f; data[total - 1 - i] *= f; }
        var clip = AudioClip.Create("ReactorHum", total, 1, sampleRate, false);
        clip.SetData(data, 0);
        return clip;
    }

    private AudioClip GenerateAgitatorWhir(float duration, int sampleRate)
    {
        int total = Mathf.CeilToInt(duration * sampleRate);
        float[] data = new float[total];
        System.Random rnd = new System.Random(99);
        float phase = 0f;
        for (int i = 0; i < total; i++)
        {
            phase += 2f * Mathf.PI * 180f / sampleRate; // mid-frequency whir
            float sine = Mathf.Sin(phase) * 0.3f;
            float noise = ((float)rnd.NextDouble() - 0.5f) * 0.15f;
            // Periodic amplitude modulation (blade passing frequency)
            float bladePass = Mathf.Abs(Mathf.Sin(phase * 0.25f));
            data[i] = (sine + noise) * bladePass * 0.4f;
        }
        int fade = Mathf.Min(2000, total / 20);
        for (int i = 0; i < fade; i++) { float f = (float)i / fade; data[i] *= f; data[total - 1 - i] *= f; }
        var clip = AudioClip.Create("AgitatorWhir", total, 1, sampleRate, false);
        clip.SetData(data, 0);
        return clip;
    }

    // ============================================================
    //  X-RAY MATERIAL
    // ============================================================

    private void EnsureXRayMaterial()
    {
        if (_xrayMaterial != null) return;
        _xrayMaterial = new Material(Shader.Find("Universal Render Pipeline/Lit"));
        _xrayMaterial.SetFloat("_Surface", 1f); // Transparent
        _xrayMaterial.SetOverrideTag("RenderType", "Transparent");
        _xrayMaterial.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.SrcAlpha);
        _xrayMaterial.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
        _xrayMaterial.SetInt("_ZWrite", 0);
        _xrayMaterial.renderQueue = 3000;
        _xrayMaterial.SetColor("_BaseColor", new Color(0.3f, 0.7f, 1f, 0.12f)); // light blue ghost
        _xrayMaterial.EnableKeyword("_EMISSION");
        _xrayMaterial.SetColor("_EmissionColor", new Color(0.2f, 0.5f, 0.9f) * 0.8f); // blue glow outline
    }

    // ============================================================
    //  AUTO-FIND
    // ============================================================

    private void AutoFindReferences()
    {
        if (_playerRigRoot == null)
        {
            var rig = GameObject.Find("XR Origin (XR Rig)") ?? GameObject.FindWithTag("Player");
            if (rig != null) _playerRigRoot = rig.transform;
        }
        if (_autoclaveField == null)
            _autoclaveField = GameObject.Find("Mesin Utama/Autoclave_Field");
        if (_autoclaveField != null)
        {
            if (_shellRenderer == null)
            {
                var shell = _autoclaveField.transform.Find("Shell");
                if (shell != null) _shellRenderer = shell.GetComponent<Renderer>();
            }
            if (_endCapRenderers == null || _endCapRenderers.Length == 0)
            {
                var ecL = _autoclaveField.transform.Find("EndCap_Left");
                var ecR = _autoclaveField.transform.Find("EndCap_Right");
                _endCapRenderers = new Renderer[] {
                    ecL != null ? ecL.GetComponent<Renderer>() : null,
                    ecR != null ? ecR.GetComponent<Renderer>() : null
                };
            }
            if (_agitatorShaft == null)
            {
                var shaft = _autoclaveField.transform.Find("AgitatorShaft");
                if (shaft != null) _agitatorShaft = shaft;
            }
        }
        if (_teleportTargetDcs == null)
        {
            var go = GameObject.Find("SpawnPoint_DCS");
            if (go != null) _teleportTargetDcs = go.transform;
        }
        if (_pipeNetwork == null)
        {
            var mesinUtama = GameObject.Find("Mesin Utama");
            if (mesinUtama != null)
                _pipeNetwork = mesinUtama.GetComponent<ProcessPipeNetwork>();
        }
    }

    // ============================================================
    //  PUBLIC
    // ============================================================

    public bool XRayActive => _xrayActive;
    public bool QuestComplete => _questComplete;
    public int GaugesInspected => _gaugesInspected;
}
