using UnityEngine;

public class Level14ESDButton : MonoBehaviour
{
    [SerializeField] private Level14EmergencyController _controller;

    private void Awake()
    {
        if (_controller == null)
            _controller = FindFirstObjectByType<Level14EmergencyController>();
    }

    public void PressButton()
    {
        if (_controller == null)
            _controller = FindFirstObjectByType<Level14EmergencyController>();

        _controller?.PressESD();
    }

    private void OnTriggerEnter(Collider other)
    {
        if (other.CompareTag("PlayerHand") ||
            other.GetComponent<UnityEngine.XR.Interaction.Toolkit.Interactors.XRDirectInteractor>() != null)
        {
            PressButton();
        }
    }
}
