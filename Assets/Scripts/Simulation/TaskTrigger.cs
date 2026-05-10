using UnityEngine;

public class TaskTrigger : MonoBehaviour
{
    public enum TaskType { Helm, Rompi, Kacamata, Sepatu, ScannerAmbil }
    
    [Header("=== SOP Task Configuration ===")]
    [Tooltip("Pilih jenis APD atau tugas yang akan dilaporkan saat objek ini diinteraksi.")]
    public TaskType tipeTugas;

    private PhaseManager phaseManager;

    void Start()
    {
        phaseManager = GameObject.FindObjectOfType<PhaseManager>();
        if (phaseManager == null)
        {
            Debug.LogError($"[TaskTrigger] PhaseManager tidak ditemukan di scene! Pastikan ada objek PhaseManager.");
        }
    }

    /// <summary>
    /// Panggil fungsi ini dari event 'Select Entered' di XR Grab Interactable 
    /// atau event interaksi lainnya di Inspector.
    /// </summary>
    public void NotifyGrab()
    {
        if (phaseManager == null) return;

        switch (tipeTugas)
        {
            case TaskType.Helm: 
                phaseManager.OnHelmetWorn(); 
                break;
            case TaskType.Rompi: 
                phaseManager.OnVestWorn(); 
                break;
            case TaskType.Kacamata: 
                phaseManager.OnGlassesWorn(); 
                break;
            case TaskType.Sepatu: 
                phaseManager.OnBootsWorn(); 
                break;
            case TaskType.ScannerAmbil: 
                phaseManager.OnScannerGrabbed(); 
                break;
        }
    }
}
