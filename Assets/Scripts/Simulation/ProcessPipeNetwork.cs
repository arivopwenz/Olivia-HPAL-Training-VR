using System;
using System.Collections.Generic;
using UnityEngine;

[DisallowMultipleComponent]
public class ProcessPipeNetwork : MonoBehaviour
{
    [Serializable]
    public class PipeRoute
    {
        public string routeId;
        public string sourceNode;
        public string destinationNode;
        public ProcessPipeMaterial materialType;
        public bool activeByDefault;
        public ProcessPipeSegment[] segments;
    }

    [Serializable]
    public class RouteLevelBinding
    {
        public GameLevelManager.GameLevel level;
        public string[] activeRouteIds;
    }

    [Header("Network")]
    public List<ProcessPipeSegment> allSegments = new List<ProcessPipeSegment>();
    public List<PipeRoute> routes = new List<PipeRoute>();

    [Header("Level Sync")]
    public bool autoSyncWithGameLevel;
    public RouteLevelBinding[] levelBindings;

    private readonly Dictionary<string, PipeRoute> _routeById = new Dictionary<string, PipeRoute>();

    private void Awake()
    {
        RefreshCache();
        ApplyDefaultRouteState();
    }

    private void OnEnable()
    {
        if (autoSyncWithGameLevel)
            GameLevelManager.OnLevelStarted += HandleLevelStarted;
    }

    private void OnDisable()
    {
        if (autoSyncWithGameLevel)
            GameLevelManager.OnLevelStarted -= HandleLevelStarted;
    }

    [ContextMenu("Refresh Cache")]
    public void RefreshCache()
    {
        allSegments.Clear();
        GetComponentsInChildren(true, allSegments);
        allSegments.Sort(CompareSegments);

        _routeById.Clear();
        for (int i = 0; i < routes.Count; i++)
        {
            PipeRoute route = routes[i];
            if (route == null || string.IsNullOrWhiteSpace(route.routeId))
                continue;

            if (route.segments == null || route.segments.Length == 0)
                route.segments = BuildRouteSegmentArray(route.routeId);

            _routeById[route.routeId] = route;
        }
    }

    public ProcessPipeSegment[] GetRouteSegments(string routeId)
    {
        if (string.IsNullOrWhiteSpace(routeId))
            return Array.Empty<ProcessPipeSegment>();

        RefreshRouteLookupIfNeeded();
        PipeRoute route;
        if (_routeById.TryGetValue(routeId, out route) && route.segments != null)
            return route.segments;

        return BuildRouteSegmentArray(routeId);
    }

    public bool HasRoute(string routeId)
    {
        RefreshRouteLookupIfNeeded();
        return !string.IsNullOrWhiteSpace(routeId) && _routeById.ContainsKey(routeId);
    }

    public void SetRouteFlowActive(string routeId, bool active)
    {
        ProcessPipeSegment[] segments = GetRouteSegments(routeId);
        for (int i = 0; i < segments.Length; i++)
            if (segments[i] != null)
                segments[i].SetFlowActive(active);
    }

    public void SetAllFlowActive(bool active)
    {
        RefreshCache();
        for (int i = 0; i < allSegments.Count; i++)
            if (allSegments[i] != null)
                allSegments[i].SetFlowActive(active);
    }

    public bool TryGetNext(ProcessPipeSegment segment, out ProcessPipeSegment next)
    {
        next = null;
        if (segment == null)
            return false;

        if (segment.nextSegments != null && segment.nextSegments.Count > 0)
        {
            next = segment.nextSegments[0];
            return next != null;
        }

        ProcessPipeSegment[] route = GetRouteSegments(segment.routeId);
        for (int i = 0; i < route.Length - 1; i++)
        {
            if (route[i] == segment)
            {
                next = route[i + 1];
                return next != null;
            }
        }

        return false;
    }

    public bool TryFindNodePath(string sourceNode, string destinationNode, out List<ProcessPipeSegment> path)
    {
        path = new List<ProcessPipeSegment>();
        if (string.IsNullOrWhiteSpace(sourceNode) || string.IsNullOrWhiteSpace(destinationNode))
            return false;

        RefreshCache();

        Dictionary<string, List<ProcessPipeSegment>> outgoing = new Dictionary<string, List<ProcessPipeSegment>>();
        for (int i = 0; i < allSegments.Count; i++)
        {
            ProcessPipeSegment segment = allSegments[i];
            if (segment == null || string.IsNullOrWhiteSpace(segment.fromNode) || string.IsNullOrWhiteSpace(segment.toNode))
                continue;

            List<ProcessPipeSegment> list;
            if (!outgoing.TryGetValue(segment.fromNode, out list))
            {
                list = new List<ProcessPipeSegment>();
                outgoing.Add(segment.fromNode, list);
            }

            list.Add(segment);
        }

        Queue<string> frontier = new Queue<string>();
        HashSet<string> visited = new HashSet<string>();
        Dictionary<string, ProcessPipeSegment> edgeToNode = new Dictionary<string, ProcessPipeSegment>();

        frontier.Enqueue(sourceNode);
        visited.Add(sourceNode);

        while (frontier.Count > 0)
        {
            string node = frontier.Dequeue();
            if (node == destinationNode)
                break;

            List<ProcessPipeSegment> nextSegments;
            if (!outgoing.TryGetValue(node, out nextSegments))
                continue;

            for (int i = 0; i < nextSegments.Count; i++)
            {
                ProcessPipeSegment segment = nextSegments[i];
                if (segment == null || visited.Contains(segment.toNode))
                    continue;

                visited.Add(segment.toNode);
                edgeToNode[segment.toNode] = segment;
                frontier.Enqueue(segment.toNode);
            }
        }

        if (!visited.Contains(destinationNode))
            return false;

        string cursor = destinationNode;
        while (cursor != sourceNode)
        {
            ProcessPipeSegment segment;
            if (!edgeToNode.TryGetValue(cursor, out segment) || segment == null)
            {
                path.Clear();
                return false;
            }

            path.Add(segment);
            cursor = segment.fromNode;
        }

        path.Reverse();
        return true;
    }

    public bool HasNodePath(string sourceNode, string destinationNode)
    {
        List<ProcessPipeSegment> path;
        return TryFindNodePath(sourceNode, destinationNode, out path);
    }

    public bool ValidateNetwork(out string report)
    {
        RefreshCache();
        List<string> issues = new List<string>();

        for (int i = 0; i < routes.Count; i++)
        {
            PipeRoute route = routes[i];
            if (route == null || string.IsNullOrWhiteSpace(route.routeId))
            {
                issues.Add("Route missing routeId at index " + i);
                continue;
            }

            ProcessPipeSegment[] segments = route.segments != null && route.segments.Length > 0
                ? route.segments
                : BuildRouteSegmentArray(route.routeId);

            if (segments.Length == 0)
            {
                issues.Add(route.routeId + " has no segments");
                continue;
            }

            for (int s = 0; s < segments.Length; s++)
            {
                ProcessPipeSegment seg = segments[s];
                if (seg == null)
                {
                    issues.Add(route.routeId + " has null segment at " + s);
                    continue;
                }

                if (seg.routeId != route.routeId)
                    issues.Add(seg.name + " routeId mismatch");

                if (string.IsNullOrWhiteSpace(seg.fromNode) || string.IsNullOrWhiteSpace(seg.toNode))
                    issues.Add(seg.name + " missing fromNode/toNode");

                if (s > 0 && segments[s - 1] != null && segments[s - 1].toNode != seg.fromNode)
                    issues.Add(route.routeId + " chain break: " + segments[s - 1].name + " -> " + seg.name);
            }
        }

        report = issues.Count == 0 ? "Process pipe network OK" : string.Join("\n", issues.ToArray());
        return issues.Count == 0;
    }

    private void HandleLevelStarted(GameLevelManager.GameLevel level)
    {
        SetAllFlowActive(false);
        if (levelBindings == null)
            return;

        for (int i = 0; i < levelBindings.Length; i++)
        {
            RouteLevelBinding binding = levelBindings[i];
            if (binding == null || binding.level != level || binding.activeRouteIds == null)
                continue;

            for (int r = 0; r < binding.activeRouteIds.Length; r++)
                SetRouteFlowActive(binding.activeRouteIds[r], true);
        }
    }

    private void ApplyDefaultRouteState()
    {
        for (int i = 0; i < routes.Count; i++)
        {
            PipeRoute route = routes[i];
            if (route != null && route.activeByDefault)
                SetRouteFlowActive(route.routeId, true);
        }
    }

    private void RefreshRouteLookupIfNeeded()
    {
        if (_routeById.Count == routes.Count)
            return;
        RefreshCache();
    }

    private ProcessPipeSegment[] BuildRouteSegmentArray(string routeId)
    {
        List<ProcessPipeSegment> matches = new List<ProcessPipeSegment>();
        if (allSegments.Count == 0)
            GetComponentsInChildren(true, allSegments);

        for (int i = 0; i < allSegments.Count; i++)
        {
            ProcessPipeSegment seg = allSegments[i];
            if (seg != null && seg.routeId == routeId)
                matches.Add(seg);
        }

        matches.Sort(CompareSegments);
        return matches.ToArray();
    }

    private static int CompareSegments(ProcessPipeSegment a, ProcessPipeSegment b)
    {
        if (a == b) return 0;
        if (a == null) return -1;
        if (b == null) return 1;

        int routeCompare = string.CompareOrdinal(a.routeId, b.routeId);
        if (routeCompare != 0)
            return routeCompare;

        int orderCompare = a.order.CompareTo(b.order);
        if (orderCompare != 0)
            return orderCompare;

        return string.CompareOrdinal(a.name, b.name);
    }
}
