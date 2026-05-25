using System;
using UnityEngine;

[DisallowMultipleComponent]
public sealed class Level1ApdTaskHintDirector : MonoBehaviour
{
    private enum ApdTask
    {
        Helm,
        Rompi,
        Kacamata,
        Sepatu,
        SarungTangan,
        Respirator,
        Earplug,
        WalkieTalkie
    }

    [Serializable]
    private struct HintEntry
    {
        public ApdTask task;
        public string socketName;
        public Transform target;
        public Vector3 arrowOffset;
    }

    [SerializeField] private GameObject arrowObject;
    [SerializeField] private float baseArrowScale = 28f;
    [SerializeField] private Color hintColor = new Color(1f, 0.78f, 0.04f, 0.88f);
    [SerializeField] private Color doneColor = new Color(0.1f, 1f, 0.45f, 0.88f);
    [SerializeField] private float pulseSpeed = 2.4f;
    [SerializeField] private float outlinePadding = 0.045f;
    [SerializeField] private HintEntry[] entries =
    {
        new HintEntry { task = ApdTask.Helm, socketName = "Socket_Scanner_Hat", arrowOffset = new Vector3(0f, 0.58f, 0f) },
        new HintEntry { task = ApdTask.Rompi, socketName = "Socket_Scanner_Rompi", arrowOffset = new Vector3(0f, 0.55f, 0f) },
        new HintEntry { task = ApdTask.Kacamata, socketName = "Socket_Scanner_Glassess", arrowOffset = new Vector3(0f, 0.42f, 0f) },
        new HintEntry { task = ApdTask.Sepatu, socketName = "Socket_Scanner_Boots", arrowOffset = new Vector3(0f, 0.42f, 0f) },
        new HintEntry { task = ApdTask.SarungTangan, socketName = "Socket_Scanner_Gloves", arrowOffset = new Vector3(0f, 0.42f, 0f) },
        new HintEntry { task = ApdTask.Respirator, socketName = "Socket_Scanner_RespiratorMask", arrowOffset = new Vector3(0f, 0.46f, 0f) },
        new HintEntry { task = ApdTask.Earplug, socketName = "Socket_Scanner_EarPlug", arrowOffset = new Vector3(0f, 0.34f, 0f) },
        new HintEntry { task = ApdTask.WalkieTalkie, socketName = "Socket_Scanner_WalkieTalkie", arrowOffset = new Vector3(0f, 0.42f, 0f) },
    };

    private readonly LineRenderer[] _outlineEdges = new LineRenderer[12];
    private Material _lineMaterial;
    private Transform _activeTarget;

    private void Awake()
    {
        if (arrowObject == null)
        {
            Transform arrow = transform.Find("TaskHint_Arrow3D");
            if (arrow != null)
                arrowObject = arrow.gameObject;
        }

        NormalizeSerializedTuning();
        ConfigureArrowRenderers();
        CreateOutline();
        ResolveTargets();
    }

    private void LateUpdate()
    {
        ResolveTargets();
        HintEntry? activeEntry = FindFirstIncompleteEntry();
        if (!activeEntry.HasValue)
        {
            SetVisible(false);
            return;
        }

        HintEntry entry = activeEntry.Value;
        Transform target = entry.target;
        if (target == null)
        {
            SetVisible(false);
            return;
        }

        _activeTarget = target;
        Bounds bounds = CalculateBounds(target);
        float pulse = 0.5f + 0.5f * Mathf.Sin(Time.time * pulseSpeed);
        Color color = Color.Lerp(hintColor, doneColor, pulse * 0.22f);
        DrawOutline(bounds, color);

        if (arrowObject != null)
        {
            arrowObject.SetActive(true);
            Vector3 basePosition = bounds.center + Vector3.up * (bounds.extents.y + 0.54f);
            arrowObject.transform.position = basePosition + entry.arrowOffset;
            arrowObject.transform.rotation = Quaternion.Euler(-90f, Time.time * 45f, 0f);
            float scale = baseArrowScale * (0.92f + pulse * 0.16f);
            arrowObject.transform.localScale = Vector3.one * scale;
        }
    }

    private HintEntry? FindFirstIncompleteEntry()
    {
        for (int i = 0; i < entries.Length; i++)
        {
            if (!IsComplete(entries[i].task))
                return entries[i];
        }
        return null;
    }

    private bool IsComplete(ApdTask task)
    {
        PhaseManager phase = PhaseManager.Instance != null ? PhaseManager.Instance : FindAnyObjectByType<PhaseManager>();
        if (phase == null)
            return false;

        switch (task)
        {
            case ApdTask.Helm: return phase.isHelmetWorn;
            case ApdTask.Rompi: return phase.isVestWorn;
            case ApdTask.Kacamata: return phase.isGlassesWorn;
            case ApdTask.Sepatu: return phase.isBootsWorn;
            case ApdTask.SarungTangan: return phase.isGlovesWorn;
            case ApdTask.Respirator: return phase.isRespiratorWorn;
            case ApdTask.Earplug: return phase.isEarplugWorn;
            case ApdTask.WalkieTalkie: return phase.isWalkieTalkieTaken;
            default: return false;
        }
    }

    private void ResolveTargets()
    {
        for (int i = 0; i < entries.Length; i++)
        {
            if (entries[i].target != null)
                continue;

            GameObject socket = GameObject.Find(entries[i].socketName);
            if (socket == null)
                continue;

            Transform target = socket.transform.childCount > 0 ? socket.transform.GetChild(0) : socket.transform;
            entries[i].target = target;
        }
    }

    private void CreateOutline()
    {
        _lineMaterial = new Material(Shader.Find("Sprites/Default"));
        _lineMaterial.name = "M_Runtime_TaskHint_Line";

        for (int i = 0; i < _outlineEdges.Length; i++)
        {
            GameObject edge = new GameObject("TaskHint_OutlineEdge_" + i.ToString("00"));
            edge.transform.SetParent(transform, false);
            LineRenderer lr = edge.AddComponent<LineRenderer>();
            lr.useWorldSpace = true;
            lr.positionCount = 2;
            lr.widthMultiplier = 0.010f;
            lr.material = _lineMaterial;
            lr.enabled = false;
            _outlineEdges[i] = lr;
        }
    }

    private void NormalizeSerializedTuning()
    {
        if (baseArrowScale > 34f || baseArrowScale < 12f)
            baseArrowScale = 28f;
        if (outlinePadding > 0.07f || outlinePadding < 0.015f)
            outlinePadding = 0.045f;
        if (pulseSpeed > 3f || pulseSpeed < 1f)
            pulseSpeed = 2.4f;
    }

    private void ConfigureArrowRenderers()
    {
        if (arrowObject == null)
            return;

        foreach (Renderer renderer in arrowObject.GetComponentsInChildren<Renderer>(true))
        {
            renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            renderer.receiveShadows = false;
        }
    }

    private void DrawOutline(Bounds sourceBounds, Color color)
    {
        Bounds bounds = sourceBounds;
        bounds.Expand(outlinePadding);

        Vector3 min = bounds.min;
        Vector3 max = bounds.max;
        Vector3[] c =
        {
            new Vector3(min.x, min.y, min.z),
            new Vector3(max.x, min.y, min.z),
            new Vector3(max.x, min.y, max.z),
            new Vector3(min.x, min.y, max.z),
            new Vector3(min.x, max.y, min.z),
            new Vector3(max.x, max.y, min.z),
            new Vector3(max.x, max.y, max.z),
            new Vector3(min.x, max.y, max.z),
        };

        SetEdge(0, c[0], c[1], color);
        SetEdge(1, c[1], c[2], color);
        SetEdge(2, c[2], c[3], color);
        SetEdge(3, c[3], c[0], color);
        SetEdge(4, c[4], c[5], color);
        SetEdge(5, c[5], c[6], color);
        SetEdge(6, c[6], c[7], color);
        SetEdge(7, c[7], c[4], color);
        SetEdge(8, c[0], c[4], color);
        SetEdge(9, c[1], c[5], color);
        SetEdge(10, c[2], c[6], color);
        SetEdge(11, c[3], c[7], color);
    }

    private void SetEdge(int index, Vector3 a, Vector3 b, Color color)
    {
        LineRenderer lr = _outlineEdges[index];
        lr.enabled = true;
        lr.startColor = color;
        lr.endColor = color;
        lr.SetPosition(0, a);
        lr.SetPosition(1, b);
    }

    private Bounds CalculateBounds(Transform target)
    {
        Renderer[] renderers = target.GetComponentsInChildren<Renderer>(true);
        bool hasBounds = false;
        Bounds bounds = new Bounds(target.position, Vector3.one * 0.15f);
        foreach (Renderer renderer in renderers)
        {
            if (!renderer.enabled)
                continue;

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
            bounds = new Bounds(target.position, Vector3.one * 0.22f);
        return bounds;
    }

    private void SetVisible(bool visible)
    {
        if (arrowObject != null)
            arrowObject.SetActive(visible);
        for (int i = 0; i < _outlineEdges.Length; i++)
            _outlineEdges[i].enabled = visible;
        _activeTarget = visible ? _activeTarget : null;
    }
}
