using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit.Interactables;

[DisallowMultipleComponent]
public sealed class WalkieTalkieMouthPttTrigger : MonoBehaviour
{
    [SerializeField] private string walkieObjectName = "Walkie Talkie";
    [SerializeField] private bool requireGrabbed = true;
    [SerializeField] private bool requireWalkieTaken = true;

    private XRGrabInteractable _activeGrab;

    private void OnDisable()
    {
        EndPTT();
    }

    private void OnTriggerEnter(Collider other)
    {
        TryBeginPTT(other);
    }

    private void OnTriggerStay(Collider other)
    {
        if (WalkieTalkieManager.Instance == null || !WalkieTalkieManager.Instance.PTTAktif)
            TryBeginPTT(other);
    }

    private void OnTriggerExit(Collider other)
    {
        XRGrabInteractable grab = other.GetComponentInParent<XRGrabInteractable>();
        if (grab != null && grab == _activeGrab)
            EndPTT();
    }

    private void TryBeginPTT(Collider other)
    {
        if (requireWalkieTaken && !(PhaseManager.Instance != null && PhaseManager.Instance.isWalkieTalkieTaken))
            return;

        XRGrabInteractable grab = other.GetComponentInParent<XRGrabInteractable>();
        if (grab == null || grab.gameObject.name != walkieObjectName)
            return;

        if (requireGrabbed && !grab.isSelected)
            return;

        _activeGrab = grab;
        WalkieTalkieManager.Instance?.BeginPhysicalWalkiePTT();
    }

    private void EndPTT()
    {
        if (_activeGrab == null)
            return;

        _activeGrab = null;
        WalkieTalkieManager.Instance?.EndPhysicalWalkiePTT();
    }
}
