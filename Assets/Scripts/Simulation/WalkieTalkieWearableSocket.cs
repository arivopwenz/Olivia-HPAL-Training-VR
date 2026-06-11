using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit.Interactables;

[DisallowMultipleComponent]
public sealed class WalkieTalkieWearableSocket : MonoBehaviour
{
    [SerializeField] private string walkieObjectName = "Walkie Talkie";
    [SerializeField] private Vector3 dockLocalPosition = Vector3.zero;
    [SerializeField] private Vector3 dockLocalEuler = new Vector3(8f, -18f, -6f);
    [SerializeField] private Vector3 dockLocalScale = Vector3.one * 0.163f;
    [SerializeField] private bool requireWalkieTaken = true;
    [SerializeField] private bool markTakenOnGrab = true;
    [SerializeField] private bool hideDockVisuals = true;

    private Transform _walkie;
    private XRGrabInteractable _grab;
    private Rigidbody _rigidbody;
    private ApdDisplayItemStabilizer _displayStabilizer;
    private bool _wasSelected;

    private void Awake()
    {
        HideDockVisuals();
        ResolveWalkie();
    }

    private void LateUpdate()
    {
        ResolveWalkie();
        if (_walkie == null)
            return;

        // HT dianggap "diambil player" HANYA jika dipegang interactor tangan/ray,
        // BUKAN oleh XRSocketInteractor dada (socket yang menahannya saat idle).
        bool heldByHand = false;
        if (_grab != null && _grab.isSelected)
        {
            foreach (var itr in _grab.interactorsSelecting)
            {
                if (!(itr is UnityEngine.XR.Interaction.Toolkit.Interactors.XRSocketInteractor))
                {
                    heldByHand = true;
                    break;
                }
            }
        }

        if (heldByHand && !_wasSelected && markTakenOnGrab)
            PhaseManager.Instance?.OnWalkieTalkieTaken();

        if (_displayStabilizer != null)
            _displayStabilizer.enabled = false;

        if (heldByHand)
        {
            // Sedang dipegang player: biarkan dibawa & dipakai lapor. JANGAN dock.
            if (_rigidbody != null)
                _rigidbody.useGravity = false;
        }
        else if (_wasSelected)
        {
            // BARU dilepas frame ini -> kembalikan ke dock dada SEKALI saja (bukan tiap frame).
            bool taken = !requireWalkieTaken || (PhaseManager.Instance != null && PhaseManager.Instance.isWalkieTalkieTaken);
            if (taken)
                DockNow();
        }
        // Saat idle & tidak dipegang: biarkan XRSocketInteractor dada yang menahan HT
        // (seperti masker). TIDAK ada DockNow paksa per-frame -> HT tetap bisa di-grab.

        _wasSelected = heldByHand;
    }

    public void DockNow()
    {
        if (_walkie == null)
            ResolveWalkie();
        if (_walkie == null)
            return;

        if (_grab != null && _grab.isSelected && _grab.interactionManager != null)
        {
            var selecting = new System.Collections.Generic.List<UnityEngine.XR.Interaction.Toolkit.Interactors.IXRSelectInteractor>(_grab.interactorsSelecting);
            foreach (var interactor in selecting)
                _grab.interactionManager.SelectExit(interactor, _grab);
        }

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
        {
            if (renderer == null) continue;
            PhaseManager.PaksaRendererApdSelaluTerlihat(renderer);
        }

        // HT harus tetap bisa di-grab manual oleh player saat ter-dock di dada.
        if (_grab != null) _grab.enabled = true;
        foreach (Collider col in _walkie.GetComponentsInChildren<Collider>(true))
            if (col != null) col.enabled = true;
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

    private void HideDockVisuals()
    {
        if (!hideDockVisuals)
            return;

        foreach (Renderer renderer in GetComponentsInChildren<Renderer>(true))
        {
            if (renderer != null && renderer.transform != transform)
                renderer.enabled = false;
        }
    }
}
