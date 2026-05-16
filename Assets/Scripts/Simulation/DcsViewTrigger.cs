using UnityEngine;

/// <summary>
/// Trigger sederhana untuk menandai pemain sudah melihat area DCS.
/// </summary>
public class DcsViewTrigger : MonoBehaviour
{
    private void OnTriggerEnter(Collider other)
    {
        if (other.CompareTag("Player") || other.GetComponentInChildren<Camera>() != null)
            GameLevelManager.Instance?.NotifyDcsViewed();
    }
}
