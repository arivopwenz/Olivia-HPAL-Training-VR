using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit;
using UnityEngine.XR.Interaction.Toolkit.Interactables;

public class TaskTrigger : MonoBehaviour
{
    private PhaseManager phaseManager;

    void Start()
    {
        // Cari Game_Manager di awal game
        phaseManager = GameObject.FindObjectOfType<PhaseManager>();
    }

    // Fungsi ini kita panggil lewat Event di Inspector
    public void NotifyGrab()
    {
        if (phaseManager != null)
        {
            phaseManager.OnScannerGrabbed();
        }
    }
}
