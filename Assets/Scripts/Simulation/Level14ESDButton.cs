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
        if (AdalahTanganPlayer(other))
        {
            PressButton();
        }
    }

    private bool AdalahTanganPlayer(Collider other)
    {
        if (other == null)
            return false;

        if (other.GetComponent<UnityEngine.XR.Interaction.Toolkit.Interactors.XRDirectInteractor>() != null)
            return true;

        string n = other.name.ToLowerInvariant();
        string p = other.transform.parent != null ? other.transform.parent.name.ToLowerInvariant() : string.Empty;
        return n.Contains("playerhand") || n.Contains("transparenthand") || n.Contains("controller") ||
               p.Contains("playerhand") || p.Contains("transparenthand") || p.Contains("controller");
    }
}
