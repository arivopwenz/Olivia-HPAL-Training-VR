using System.Collections.Generic;
using UnityEngine;

public enum ProcessPipeMaterial
{
    Slurry,
    HeatedSlurry,
    Acid,
    LetdownSlurry,
    FlashUnderflow,
    CcdOverflow,
    MhpFeed,
    Tailing,
    Water,
    Vapor,
    Utility
}

[DisallowMultipleComponent]
public class ProcessPipeSegment : MonoBehaviour
{
    [Header("Network Identity")]
    public string routeId;
    public string fromNode;
    public string toNode;
    public int order;
    public ProcessPipeMaterial materialType = ProcessPipeMaterial.Slurry;

    [Header("Connectivity")]
    public ProcessPipeSegment previousSegment;
    public List<ProcessPipeSegment> nextSegments = new List<ProcessPipeSegment>();

    [Header("Visual Flow Hook")]
    public Renderer pipeRenderer;
    public GameObject flowVisual;
    public bool flowInitiallyActive;

    public bool IsFlowing { get; private set; }

    public Vector3 WorldCenter => transform.position;

    private void Reset()
    {
        CacheReferences();
    }

    private void Awake()
    {
        CacheReferences();
        SetFlowActive(flowInitiallyActive);
    }

    private void OnValidate()
    {
        if (pipeRenderer == null)
            CacheReferences();
    }

    public void CacheReferences()
    {
        if (pipeRenderer == null)
            pipeRenderer = GetComponent<Renderer>();

        if (flowVisual == null)
        {
            Transform liquid = transform.Find("Liquid_Inner");
            if (liquid == null)
                liquid = transform.Find("Liquid");
            if (liquid != null)
                flowVisual = liquid.gameObject;
        }
    }

    public void Configure(string newRouteId, string newFromNode, string newToNode, int newOrder, ProcessPipeMaterial newMaterialType)
    {
        routeId = newRouteId;
        fromNode = newFromNode;
        toNode = newToNode;
        order = newOrder;
        materialType = newMaterialType;
    }

    public void LinkNext(ProcessPipeSegment next)
    {
        if (next == null || next == this || nextSegments.Contains(next))
            return;

        nextSegments.Add(next);
        next.previousSegment = this;
    }

    public void ClearLinks()
    {
        previousSegment = null;
        nextSegments.Clear();
    }

    public void SetFlowActive(bool active)
    {
        IsFlowing = active;

        if (flowVisual != null)
            flowVisual.SetActive(active);

        PipeLiquidFlow liquidFlow = GetComponent<PipeLiquidFlow>();
        if (liquidFlow != null)
            liquidFlow.enabled = active;
    }
}
