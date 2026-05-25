using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit.Interactables;

[DisallowMultipleComponent]
public sealed class ApdDisplayItemStabilizer : MonoBehaviour
{
    [SerializeField] private Transform homeAnchor;
    [SerializeField] private bool returnToHomeWhenReleased = true;
    [SerializeField] private bool keepGravityOff = true;

    private Rigidbody _rigidbody;
    private XRGrabInteractable _grab;
    private bool _wasSelected;

    private void Awake()
    {
        _rigidbody = GetComponent<Rigidbody>();
        _grab = GetComponent<XRGrabInteractable>();
        ConfigureGrab();
        StabilizeBody();
    }

    private void LateUpdate()
    {
        bool selected = _grab != null && _grab.isSelected;

        if (!selected)
        {
            StabilizeBody();
            if (homeAnchor != null && (returnToHomeWhenReleased || !_wasSelected))
            {
                transform.SetPositionAndRotation(homeAnchor.position, homeAnchor.rotation);
            }
        }

        _wasSelected = selected;
    }

    public void SetHomeAnchor(Transform anchor)
    {
        homeAnchor = anchor;
        if (homeAnchor != null && !(_grab != null && _grab.isSelected))
            transform.SetPositionAndRotation(homeAnchor.position, homeAnchor.rotation);
    }

    private void ConfigureGrab()
    {
        if (_grab == null)
            return;

        _grab.movementType = XRBaseInteractable.MovementType.Kinematic;
        _grab.throwOnDetach = false;
        _grab.forceGravityOnDetach = false;
        _grab.retainTransformParent = true;
    }

    private void StabilizeBody()
    {
        if (_rigidbody == null)
            return;

        if (!_rigidbody.isKinematic)
        {
            _rigidbody.linearVelocity = Vector3.zero;
            _rigidbody.angularVelocity = Vector3.zero;
        }
        _rigidbody.isKinematic = true;
        if (keepGravityOff)
            _rigidbody.useGravity = false;
    }
}
