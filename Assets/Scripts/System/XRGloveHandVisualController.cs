using System.Collections.Generic;
using UnityEngine;
using UnityEngine.XR;

[DisallowMultipleComponent]
public sealed class XRGloveHandVisualController : MonoBehaviour
{
    [Header("Rigged glove FBX")]
    [SerializeField] private GameObject leftGlovePrefab;
    [SerializeField] private GameObject rightGlovePrefab;
    [SerializeField] private Material runtimeGloveMaterial;
    [SerializeField] private AnimationClip leftGripClip;
    [SerializeField] private AnimationClip rightGripClip;

    [Header("Placement")]
    [SerializeField] private Vector3 leftLocalEuler = new Vector3(270f, 180f, 0f);
    [SerializeField] private Vector3 rightLocalEuler = new Vector3(270f, 180f, 0f);
    [SerializeField, Min(0.01f)] private float handLength = 0.20f;
    [SerializeField, Min(0.1f)] private float poseSpeed = 9f;

    private readonly HandVisual _left = new HandVisual(XRNode.LeftHand, "LeftHand");
    private readonly HandVisual _right = new HandVisual(XRNode.RightHand, "RightHand");
    private bool _lastWornState;
    private float _nextResolveTime;

    private void Start()
    {
        ResolveHands();
        ApplyWornState(IsGlovesWorn());
    }

    private void LateUpdate()
    {
        if (Time.unscaledTime >= _nextResolveTime)
        {
            _nextResolveTime = Time.unscaledTime + 1f;
            ResolveHands();
        }

        bool worn = IsGlovesWorn();
        if (worn != _lastWornState)
            ApplyWornState(worn);

        if (!worn)
            return;

        SetGloveVisible(_left, true);
        SetGloveVisible(_right, true);
        UpdatePose(_left, leftGripClip);
        UpdatePose(_right, rightGripClip);
    }

    private bool IsGlovesWorn()
    {
        return PhaseManager.Instance != null && PhaseManager.Instance.isGlovesWorn;
    }

    private void ResolveHands()
    {
        ResolveHand(_left, leftGlovePrefab, leftLocalEuler);
        ResolveHand(_right, rightGlovePrefab, rightLocalEuler);
    }

    private void ResolveHand(HandVisual hand, GameObject prefab, Vector3 localEuler)
    {
        if (hand.Anchor == null)
            hand.Anchor = FindSceneTransform(hand.AnchorName);

        if (hand.Anchor == null || hand.GloveInstance != null || prefab == null)
            return;

        hand.BaseRenderers.Clear();
        hand.BaseRenderers.AddRange(hand.Anchor.GetComponentsInChildren<Renderer>(true));

        Renderer referenceRenderer = null;
        foreach (Renderer renderer in hand.BaseRenderers)
        {
            if (renderer != null && renderer.enabled)
            {
                referenceRenderer = renderer;
                break;
            }
        }

        if (referenceRenderer != null)
        {
            hand.TargetCenterLocal = hand.Anchor.InverseTransformPoint(referenceRenderer.bounds.center);
            hand.TargetLength = Mathf.Max(
                referenceRenderer.bounds.size.x,
                referenceRenderer.bounds.size.y,
                referenceRenderer.bounds.size.z);
        }
        else
        {
            hand.TargetCenterLocal = Vector3.zero;
            hand.TargetLength = handLength;
        }

        GameObject root = new GameObject(hand.Node == XRNode.LeftHand
            ? "XR_Glove_Left_Visual"
            : "XR_Glove_Right_Visual");
        Transform visualParent = hand.Anchor.parent != null ? hand.Anchor.parent : hand.Anchor;
        root.transform.SetParent(visualParent, false);
        root.transform.localPosition = hand.Anchor.localPosition;
        root.transform.localRotation = hand.Anchor.localRotation;
        root.transform.localScale = hand.Anchor.localScale;

        hand.GloveInstance = Instantiate(prefab, root.transform);
        hand.GloveInstance.name = prefab.name + "_Runtime";
        hand.GloveInstance.transform.localPosition = Vector3.zero;
        hand.GloveInstance.transform.localRotation = Quaternion.Euler(localEuler);
        ApplyRuntimeMaterial(hand.GloveInstance);

        Bounds gloveBounds = GetRendererBounds(hand.GloveInstance);
        float gloveLength = Mathf.Max(gloveBounds.size.x, gloveBounds.size.y, gloveBounds.size.z);
        float targetLength = hand.TargetLength > 0.01f ? hand.TargetLength : handLength;
        float scale = gloveLength > 0.0001f ? targetLength / gloveLength : 1f;
        hand.GloveInstance.transform.localScale = Vector3.one * scale;

        AlignToTrackedHand(hand);
        SetGloveVisible(hand, IsGlovesWorn());
    }

    private void ApplyWornState(bool worn)
    {
        _lastWornState = worn;
        SetGloveVisible(_left, worn);
        SetGloveVisible(_right, worn);
    }

    private static void SetGloveVisible(HandVisual hand, bool worn)
    {
        if (hand.GloveInstance != null)
            hand.GloveInstance.transform.parent.gameObject.SetActive(worn);

        if (hand.Anchor != null)
            hand.Anchor.gameObject.SetActive(!worn);

        foreach (Renderer renderer in hand.BaseRenderers)
            if (renderer != null)
                renderer.enabled = !worn;
    }

    private void ApplyRuntimeMaterial(GameObject gloveRoot)
    {
        if (gloveRoot == null || runtimeGloveMaterial == null)
            return;

        foreach (Renderer renderer in gloveRoot.GetComponentsInChildren<Renderer>(true))
        {
            if (renderer == null)
                continue;

            Material[] materials = renderer.sharedMaterials;
            int slotCount = materials != null && materials.Length > 0 ? materials.Length : 1;
            materials = new Material[slotCount];
            for (int i = 0; i < materials.Length; i++)
                materials[i] = runtimeGloveMaterial;

            renderer.sharedMaterials = materials;
            renderer.enabled = true;
        }
    }

    private void UpdatePose(HandVisual hand, AnimationClip clip)
    {
        if (hand.GloveInstance == null || clip == null)
            return;

        InputDevice device = InputDevices.GetDeviceAtXRNode(hand.Node);
        float grip = 0f;
        float trigger = 0f;
        device.TryGetFeatureValue(CommonUsages.grip, out grip);
        device.TryGetFeatureValue(CommonUsages.trigger, out trigger);

        float targetPose = Mathf.Clamp01(Mathf.Max(grip, trigger));
        hand.Pose = Mathf.MoveTowards(hand.Pose, targetPose, poseSpeed * Time.unscaledDeltaTime);
        clip.SampleAnimation(hand.GloveInstance, hand.Pose * clip.length * 0.5f);
        AlignToTrackedHand(hand);
    }

    private static void AlignToTrackedHand(HandVisual hand)
    {
        if (hand.Anchor == null || hand.GloveInstance == null)
            return;

        Bounds bounds = GetRendererBounds(hand.GloveInstance);
        Vector3 targetCenter = hand.Anchor.TransformPoint(hand.TargetCenterLocal);
        hand.GloveInstance.transform.position += targetCenter - bounds.center;
    }

    private static Bounds GetRendererBounds(GameObject root)
    {
        Renderer[] renderers = root.GetComponentsInChildren<Renderer>(true);
        if (renderers.Length == 0)
            return new Bounds(root.transform.position, Vector3.one);

        Bounds bounds = renderers[0].bounds;
        for (int i = 1; i < renderers.Length; i++)
            bounds.Encapsulate(renderers[i].bounds);
        return bounds;
    }

    private static Transform FindSceneTransform(string objectName)
    {
        foreach (GameObject candidate in Resources.FindObjectsOfTypeAll<GameObject>())
        {
            if (candidate != null && candidate.scene.IsValid() && candidate.name == objectName)
                return candidate.transform;
        }
        return null;
    }

    private sealed class HandVisual
    {
        public readonly XRNode Node;
        public readonly string AnchorName;
        public readonly List<Renderer> BaseRenderers = new List<Renderer>();
        public Transform Anchor;
        public GameObject GloveInstance;
        public Vector3 TargetCenterLocal;
        public float TargetLength;
        public float Pose;

        public HandVisual(XRNode node, string anchorName)
        {
            Node = node;
            AnchorName = anchorName;
        }
    }
}
