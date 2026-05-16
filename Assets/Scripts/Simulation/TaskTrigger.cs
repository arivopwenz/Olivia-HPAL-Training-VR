using UnityEngine;

/// <summary>
/// Adapter universal yang menghubungkan interaksi XR Grab/Socket
/// dengan PhaseManager. Pilih TaskType di Inspector sesuai objek.
/// </summary>
public class TaskTrigger : MonoBehaviour
{
    public enum TaskType
    {
        // === APD DASAR (Wajib Semua Area) ===
        Helm,
        Rompi,
        Kacamata,
        Sepatu,
        SarungTangan,

        // === APD KHUSUS ZONA ===
        Respirator,
        EarProtection,

        // === ITEM OPERASIONAL ===
        RadioHT,

        // === INTERAKSI LAPANGAN ===
        SteamValveOpen,
        ESDButton,
        IsolationValve,

        // === DCS AREA ===
        LihatDCS,
    }

    [Header("=== SOP Task Configuration ===")]
    [Tooltip("Pilih jenis APD atau tugas yang akan dilaporkan saat objek ini diinteraksi.")]
    public TaskType tipeTugas;

    private PhaseManager phaseManager;

    void Start()
    {
        phaseManager = Object.FindAnyObjectByType<PhaseManager>();
        if (phaseManager == null)
        {
            Debug.LogError($"[TaskTrigger] PhaseManager tidak ditemukan di scene!");
        }
    }

    public void NotifyGrab()
    {
        DoNotify();
    }

    private void OnTriggerEnter(Collider other)
    {
        if (other.CompareTag("Player") || other.GetComponentInChildren<Camera>() != null)
        {
            Debug.Log($"[AutoEquip] Player menyentuh {gameObject.name}, otomatis memakai {tipeTugas}");
            DoNotify();
        }
    }

    private void DoNotify()
    {
        if (phaseManager == null) return;

        switch (tipeTugas)
        {
            case TaskType.Helm:         phaseManager.OnHelmetWorn();        break;
            case TaskType.Rompi:        phaseManager.OnVestWorn();          break;
            case TaskType.Kacamata:     phaseManager.OnGlassesWorn();       break;
            case TaskType.Sepatu:       phaseManager.OnBootsWorn();         break;
            case TaskType.SarungTangan: phaseManager.OnGlovesWorn();        break;

            case TaskType.Respirator:   phaseManager.OnRespiratiorWorn();   break;
            case TaskType.EarProtection:phaseManager.OnEarplugWorn();       break;

            case TaskType.RadioHT:      phaseManager.OnWalkieTalkieTaken(); break;

            case TaskType.SteamValveOpen: 
                Debug.Log("[TaskTrigger] Steam Valve Opened.");
                break;
            case TaskType.ESDButton: 
                if (GameLevelManager.Instance != null && GameLevelManager.Instance.CurrentLevel == GameLevelManager.GameLevel.Level14_Emergency)
                    GameLevelManager.Instance.SelesaikanLevel(GameLevelManager.GameLevel.Level14_Emergency);
                break;
            case TaskType.IsolationValve: 
                Debug.Log("[TaskTrigger] Isolation Valve Closed.");
                break;
            case TaskType.LihatDCS:
                GameLevelManager.Instance?.NotifyDcsViewed();
                break;
        }
    }
}
