using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit.Interactables;

[DisallowMultipleComponent]
public sealed class WalkieTalkieWearableSocket : MonoBehaviour
{
    [SerializeField] private string walkieObjectName = "Walkie Talkie";
    [SerializeField] private Vector3 dockLocalPosition = Vector3.zero;
    [SerializeField] private Vector3 dockLocalEuler = new Vector3(8f, -18f, -6f);
    [SerializeField] private Vector3 dockLocalScale = new Vector3(0.16f, 0.37f, 0.06f);
    [SerializeField] private bool requireWalkieTaken = true;
    [SerializeField] private bool markTakenOnGrab = true;

    private Transform _walkie;
    private XRGrabInteractable _grab;
    private Rigidbody _rigidbody;
    private ApdDisplayItemStabilizer _displayStabilizer;
    private bool _wasSelected;

    private void Awake()
    {
        ResolveWalkie();
    }

    private void LateUpdate()
    {
        ResolveWalkie();
        if (_walkie == null)
            return;

        bool selected = _grab != null && _grab.isSelected;
        if (selected && !_wasSelected && markTakenOnGrab)
            PhaseManager.Instance?.OnWalkieTalkieTaken();
        _wasSelected = selected;

        bool taken = !requireWalkieTaken || (PhaseManager.Instance != null && PhaseManager.Instance.isWalkieTalkieTaken);
        if (!taken)
            return;

        if (_displayStabilizer != null)
            _displayStabilizer.enabled = false;

        if (selected)
        {
            if (_rigidbody != null)
                _rigidbody.useGravity = false;
            return;
        }

        DockNow();
    }

    public void DockNow()
    {
        if (_walkie == null)
            ResolveWalkie();
        if (_walkie == null)
            return;

        if (_rigidbody != null)
        {
            if (!_rigidbody.isKinematic)
            {
                _rigidbody.linearVelocity = Vector3.zero;
                _rigidbody.angularVelocity = Vector3.zero;
            }
            _rigidbody.isKinematic = true;
            _rigidbody.useGravity = false;
        }

        _walkie.gameObject.SetActive(true);
        _walkie.SetParent(transform, false);
        _walkie.localPosition = dockLocalPosition;
        _walkie.localRotation = Quaternion.Euler(dockLocalEuler);
        _walkie.localScale = dockLocalScale;

        foreach (Renderer renderer in _walkie.GetComponentsInChildren<Renderer>(true))
            if (renderer != null) renderer.enabled = true;
    }

    private void ResolveWalkie()
    {
        if (_walkie != null)
            return;

        GameObject go = GameObject.Find(walkieObjectName);
        if (go == null)
        {
            foreach (GameObject candidate in Resources.FindObjectsOfTypeAll<GameObject>())
            {
                if (candidate.name == walkieObjectName && candidate.scene.IsValid())
                {
                    go = candidate;
                    break;
                }
            }
        }

        if (go == null)
            return;

        _walkie = go.transform;
        _grab = go.GetComponent<XRGrabInteractable>();
        _rigidbody = go.GetComponent<Rigidbody>();
        _displayStabilizer = go.GetComponent<ApdDisplayItemStabilizer>();
    }
}
