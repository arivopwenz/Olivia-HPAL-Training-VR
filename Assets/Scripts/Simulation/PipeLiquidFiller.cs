using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// OLIVIA VR - PipeLiquidFiller.cs
///
/// Animate liquid (cylinder) mengisi pipa secara progresif: dari panjang 0 sampai
/// panjang penuh pipa. Mirip "slurry naik di tank" tapi horizontal di sepanjang pipa.
///
/// Cara pakai:
///   1. Pasang script di empty GameObject parent
///   2. Set _pipeSegments: list pipa dalam urutan flow (Pipe_ToTank → Pipe_FromPump → ...)
///   3. Set _radiusInsideMultiplier (default 0.7 = 70% dari radius pipa, supaya pas masuk)
///   4. Panggil StartFill() untuk mulai animasi.
///
/// Liquid akan mengisi tiap segmen secara berurutan. Bisa diatur durasi total atau per-meter.
/// </summary>
public class PipeLiquidFiller : MonoBehaviour
{
    [System.Serializable]
    public class PipeSegment
    {
        [Tooltip("Transform pipa (cylinder primitive yang sudah di-rotate).")]
        public Transform pipeTransform;

        [Tooltip("Auto-detect arah flow dari pipa orientation. Atau override manual.")]
        public bool autoDetectDirection = true;

        [Tooltip("Override arah flow (lokal axis pipa). Default: -X (kalau pipa horizontal mengarah ke -X).")]
        public Vector3 flowDirection = new Vector3(-1f, 0f, 0f);

        [Tooltip("Override radius pipa. 0 = auto detect dari mesh bounds.")]
        public float pipeRadiusOverride = 0f;

        [Tooltip("Override panjang pipa. 0 = auto detect dari mesh bounds.")]
        public float pipeLengthOverride = 0f;

        [HideInInspector] public Transform liquidVisual;
        [HideInInspector] public float computedLength;
        [HideInInspector] public float computedRadius;
        [HideInInspector] public Vector3 computedFlowDir;
        [HideInInspector] public Vector3 startWorldPos;
    }

    [Header("=== Pipe Segments (urutan flow) ===")]
    [SerializeField] private List<PipeSegment> _pipeSegments = new List<PipeSegment>();

    [Header("=== Visual Liquid ===")]
    [Tooltip("Material liquid (auto-create kalau kosong).")]
    [SerializeField] private Material _liquidMaterial;
    [SerializeField] private Color _warnaLiquid = new Color(0.85f, 0.45f, 0.20f, 1f);
    [SerializeField] private float _emissionIntensity = 1.5f;
    [Tooltip("Multiplier radius liquid dari radius pipa. 0.7 = 70% (cocok untuk pipa hollow).")]
    [Range(0.3f, 1.0f)] [SerializeField] private float _radiusInsideMultiplier = 0.75f;

    [Header("=== Animasi ===")]
    [Tooltip("Durasi total fill (semua segmen). Akan dibagi proporsional ke panjang setiap segmen.")]
    [SerializeField] private float _durasiTotalFill = 12f;
    [Tooltip("Curva ease (smooth ramp).")]
    [SerializeField] private AnimationCurve _kurvaFill = AnimationCurve.EaseInOut(0f, 0f, 1f, 1f);

    [Header("=== Particle Bubble ===")]
    [Tooltip("Tambah particle bubble di liquid (auto-create kalau kosong).")]
    [SerializeField] private bool _enableBubbles = true;
    [SerializeField] private ParticleSystem _bubbleFx;

    private Coroutine _fillCoroutine;
    private bool _initialized;

    public bool IsFilling => _fillCoroutine != null;
    public float DurasiTotalFill { get => _durasiTotalFill; set => _durasiTotalFill = Mathf.Max(0.1f, value); }

    private void Awake()
    {
        EnsureMaterial();
        InitSegments();
    }

    private void EnsureMaterial()
    {
        if (_liquidMaterial != null) return;

        var shader = Shader.Find("Universal Render Pipeline/Lit");
        if (shader == null) shader = Shader.Find("Standard");
        _liquidMaterial = new Material(shader);
        _liquidMaterial.SetColor("_BaseColor", _warnaLiquid);
        _liquidMaterial.SetColor("_Color", _warnaLiquid);
        _liquidMaterial.SetFloat("_Smoothness", 0.65f);
        _liquidMaterial.SetFloat("_Metallic", 0.05f);
        _liquidMaterial.EnableKeyword("_EMISSION");
        _liquidMaterial.SetColor("_EmissionColor", _warnaLiquid * _emissionIntensity);
    }

    /// <summary>
    /// Initialize all segments: create child cylinder liquid, compute dimensi pipa.
    /// </summary>
    private void InitSegments()
    {
        if (_initialized) return;

        for (int i = 0; i < _pipeSegments.Count; i++)
        {
            var seg = _pipeSegments[i];
            if (seg.pipeTransform == null) continue;

            ComputeSegmentParams(seg);
            CreateLiquidVisualForSegment(seg, i);
        }

        _initialized = true;
    }

    private void ComputeSegmentParams(PipeSegment seg)
    {
        if (seg.pipeTransform == null) return;

        // Auto-detect dari mesh bounds (untuk Cylinder primitive yang di-rotate via transform)
        var mr = seg.pipeTransform.GetComponent<MeshRenderer>();
        if (mr == null)
        {
            seg.computedLength = seg.pipeLengthOverride > 0 ? seg.pipeLengthOverride : 1f;
            seg.computedRadius = seg.pipeRadiusOverride > 0 ? seg.pipeRadiusOverride : 0.4f;
            seg.computedFlowDir = seg.flowDirection.normalized;
            seg.startWorldPos = seg.pipeTransform.position;
            return;
        }

        Bounds b = mr.bounds; // world space
        // Tentukan axis paling panjang sebagai panjang pipa
        Vector3 ext = b.extents;
        float lenX = ext.x, lenY = ext.y, lenZ = ext.z;
        float maxLen = Mathf.Max(lenX, Mathf.Max(lenY, lenZ));
        float radius = (lenX + lenY + lenZ - maxLen) * 0.5f; // average dari 2 axis lain

        seg.computedLength = seg.pipeLengthOverride > 0 ? seg.pipeLengthOverride : maxLen * 2f;
        seg.computedRadius = seg.pipeRadiusOverride > 0 ? seg.pipeRadiusOverride : radius;

        // Auto-detect flow direction: ambil axis terpanjang dari pipa di world space
        if (seg.autoDetectDirection)
        {
            // Cari axis dengan extent terbesar
            Vector3 absExt = new Vector3(Mathf.Abs(ext.x), Mathf.Abs(ext.y), Mathf.Abs(ext.z));
            Vector3 dir;
            if (absExt.x >= absExt.y && absExt.x >= absExt.z)
                dir = Vector3.right;
            else if (absExt.y >= absExt.z)
                dir = Vector3.up;
            else
                dir = Vector3.forward;
            seg.computedFlowDir = dir;
        }
        else
        {
            seg.computedFlowDir = seg.flowDirection.normalized;
        }

        // Default: liquid dimulai dari ujung yang sesuai (kalau dir = -X, mulai dari sisi +X)
        Vector3 center = b.center;
        seg.startWorldPos = center - seg.computedFlowDir * (seg.computedLength * 0.5f);
    }

    private void CreateLiquidVisualForSegment(PipeSegment seg, int index)
    {
        if (seg.pipeTransform == null) return;
        if (seg.liquidVisual != null) return;

        // Buat cylinder liquid sebagai child seg.pipeTransform.
        // Tapi untuk simplicity, parent ke `this` agar tidak terpengaruh transform pipa yang sudah di-rotate.
        var liquidGo = new GameObject($"Liquid_Segment_{index}");
        liquidGo.transform.SetParent(transform, false);

        // Buat mesh cylinder (Unity primitive). Cylinder default panjang 2, radius 0.5.
        var meshGo = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        meshGo.name = "Mesh";
        var col = meshGo.GetComponent<Collider>();
        if (col != null) DestroyImmediate(col);
        meshGo.transform.SetParent(liquidGo.transform, false);

        // Mesh cylinder default: axis Y (vertical). Length=2, radius=0.5.
        // Liquid: scale Y = length (panjang pipa) / 2, scale X & Z = radius_inside / 0.5.
        // Rotation cylinder agar align dengan flow direction.
        Quaternion rotToFlow = Quaternion.FromToRotation(Vector3.up, seg.computedFlowDir);
        meshGo.transform.localRotation = rotToFlow;

        float radiusInside = seg.computedRadius * _radiusInsideMultiplier;
        float scaleXZ = radiusInside / 0.5f; // default cylinder radius is 0.5
        meshGo.transform.localScale = new Vector3(scaleXZ, 0.001f, scaleXZ); // start dengan length~0

        var mr = meshGo.GetComponent<MeshRenderer>();
        mr.sharedMaterial = _liquidMaterial;
        mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;

        liquidGo.transform.position = seg.startWorldPos;
        liquidGo.SetActive(false);

        seg.liquidVisual = liquidGo.transform;
    }

    /// <summary>
    /// Mulai animasi fill (semua segmen secara berurutan).
    /// </summary>
    public void StartFill()
    {
        if (_fillCoroutine != null) StopCoroutine(_fillCoroutine);
        _fillCoroutine = StartCoroutine(FillCoroutine());
    }

    public void ResetFill()
    {
        if (_fillCoroutine != null) { StopCoroutine(_fillCoroutine); _fillCoroutine = null; }
        for (int i = 0; i < _pipeSegments.Count; i++)
        {
            var seg = _pipeSegments[i];
            if (seg.liquidVisual != null) seg.liquidVisual.gameObject.SetActive(false);
        }
    }

    private IEnumerator FillCoroutine()
    {
        // Re-compute segments untuk handle perubahan transform pipa dinamis
        for (int i = 0; i < _pipeSegments.Count; i++)
        {
            ComputeSegmentParams(_pipeSegments[i]);
            UpdateLiquidVisualPlacement(_pipeSegments[i]);
        }

        // Start bubble effect
        if (_enableBubbles && _bubbleFx == null)
            _bubbleFx = CreateBubbleFx();
        if (_bubbleFx != null) _bubbleFx.Play(true);

        // Total panjang
        float totalLen = 0f;
        for (int i = 0; i < _pipeSegments.Count; i++)
        {
            if (_pipeSegments[i].pipeTransform == null) continue;
            totalLen += _pipeSegments[i].computedLength;
        }
        if (totalLen <= 0.01f) yield break;

        // Animate per segmen, durasi proporsional dengan panjang
        for (int i = 0; i < _pipeSegments.Count; i++)
        {
            var seg = _pipeSegments[i];
            if (seg.pipeTransform == null || seg.liquidVisual == null) continue;

            seg.liquidVisual.gameObject.SetActive(true);
            float segLen = seg.computedLength;
            float segDuration = (segLen / totalLen) * _durasiTotalFill;
            yield return StartCoroutine(FillSegment(seg, segDuration));
        }

        // Stop bubble setelah selesai (atau biarkan tetap jalan jika mau)
        if (_bubbleFx != null) _bubbleFx.Stop(true, ParticleSystemStopBehavior.StopEmitting);

        _fillCoroutine = null;
    }

    private void UpdateLiquidVisualPlacement(PipeSegment seg)
    {
        if (seg.liquidVisual == null) return;
        seg.liquidVisual.position = seg.startWorldPos;

        // Reset mesh scale ke 0
        var meshT = seg.liquidVisual.Find("Mesh");
        if (meshT != null)
        {
            float radiusInside = seg.computedRadius * _radiusInsideMultiplier;
            float scaleXZ = radiusInside / 0.5f;
            meshT.localScale = new Vector3(scaleXZ, 0.001f, scaleXZ);
            meshT.localRotation = Quaternion.FromToRotation(Vector3.up, seg.computedFlowDir);
            meshT.localPosition = Vector3.zero;
        }
    }

    private IEnumerator FillSegment(PipeSegment seg, float duration)
    {
        var meshT = seg.liquidVisual.Find("Mesh");
        if (meshT == null) yield break;

        float totalLen = seg.computedLength;
        float radiusInside = seg.computedRadius * _radiusInsideMultiplier;
        float scaleXZ = radiusInside / 0.5f;

        float elapsed = 0f;
        Vector3 flowDir = seg.computedFlowDir;
        Vector3 startPos = seg.startWorldPos;

        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.Clamp01(elapsed / Mathf.Max(0.001f, duration));
            float curveT = _kurvaFill.Evaluate(t);
            float currentLen = totalLen * curveT;

            // Mesh cylinder default panjang 2 (Y axis), kita scale Y = currentLen / 2
            float scaleY = Mathf.Max(0.001f, currentLen / 2f);
            meshT.localScale = new Vector3(scaleXZ, scaleY, scaleXZ);

            // Position: liquid grows DARI startPos KE arah flow.
            // Pivot mesh cylinder default di tengah, jadi posisi parent harus = startPos + flowDir * (currentLen/2)
            Vector3 newPos = startPos + flowDir * (currentLen * 0.5f);
            seg.liquidVisual.position = newPos;

            yield return null;
        }

        // Final state: full length
        meshT.localScale = new Vector3(scaleXZ, totalLen / 2f, scaleXZ);
        seg.liquidVisual.position = startPos + flowDir * (totalLen * 0.5f);
    }

    private ParticleSystem CreateBubbleFx()
    {
        if (_pipeSegments.Count == 0 || _pipeSegments[0].pipeTransform == null) return null;

        var go = new GameObject("BubbleFx");
        go.transform.SetParent(transform, false);

        var ps = go.AddComponent<ParticleSystem>();
        var main = ps.main;
        main.duration = 6f;
        main.loop = true;
        main.startLifetime = new ParticleSystem.MinMaxCurve(0.6f, 1.8f);
        main.startSpeed = new ParticleSystem.MinMaxCurve(0.3f, 0.8f);
        main.startSize = new ParticleSystem.MinMaxCurve(0.04f, 0.12f);
        main.startColor = new Color(1f, 0.85f, 0.5f, 0.9f);
        main.simulationSpace = ParticleSystemSimulationSpace.World;
        main.maxParticles = 200;
        main.gravityModifier = 0f;

        var emission = ps.emission;
        emission.rateOverTime = 25f;

        var shape = ps.shape;
        shape.shapeType = ParticleSystemShapeType.Box;
        shape.scale = new Vector3(0.2f, 0.2f, 0.2f);
        // Position emitter at middle of last segment liquid
        var lastSeg = _pipeSegments[_pipeSegments.Count - 1];
        if (lastSeg.pipeTransform != null)
            go.transform.position = lastSeg.pipeTransform.position;

        var psRenderer = ps.GetComponent<ParticleSystemRenderer>();
        Shader sprShader = Shader.Find("Universal Render Pipeline/Particles/Unlit");
        if (sprShader == null) sprShader = Shader.Find("Sprites/Default");
        var psMat = new Material(sprShader);
        psMat.color = new Color(1f, 0.85f, 0.5f, 0.9f);
        psRenderer.material = psMat;

        ps.Stop();
        return ps;
    }

    /// <summary>
    /// Bisa dipanggil dari controller untuk override durasi sebelum start.
    /// </summary>
    public void SetDurasiTotalFill(float durasi) => _durasiTotalFill = Mathf.Max(0.5f, durasi);

    /// <summary>
    /// Build segment list otomatis dari nama pipa di scene.
    /// </summary>
    public void BuildSegmentsFromNames(string[] pipeNames)
    {
        _pipeSegments.Clear();
        foreach (var n in pipeNames)
        {
            var go = GameObject.Find(n);
            if (go == null)
            {
                foreach (var item in Resources.FindObjectsOfTypeAll<GameObject>())
                    if (item != null && item.scene.IsValid() && item.name == n)
                    {
                        go = item;
                        break;
                    }
            }
            if (go != null)
            {
                _pipeSegments.Add(new PipeSegment { pipeTransform = go.transform, autoDetectDirection = true });
            }
        }
        _initialized = false;
    }
}
