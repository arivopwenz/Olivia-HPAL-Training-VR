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
        SarungTangan,       // Chemical-resistant gloves (BARU)

        // === APD KHUSUS ZONA (Safety Gate Berjenjang) ===
        Respirator,         // Wajib area kimia / H2SO4 (BARU)
        EarProtection,      // Wajib area mesin / >85dB (BARU)

        // === ITEM OPERASIONAL ===
        RadioHT,            // Walkie-Talkie dari loker (Ganti Scanner)

        // === INTERAKSI LAPANGAN ===
        SteamValveOpen,     // Katup injeksi uap dibuka (Ganti Scanner)
        ESDButton,          // Tombol Emergency Shutdown ditekan
        IsolationValve,     // Isolation valve diputar manual (backup ESD)
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

    /// <summary>
    /// Panggil dari event 'Select Entered' di XR Grab Interactable di Inspector.
    /// </summary>
    public void NotifyGrab()
    {
        if (phaseManager == null) return;

        switch (tipeTugas)
        {
            // --- APD Dasar ---
            case TaskType.Helm: phaseManager.OnHelmetWorn(); break;
            case TaskType.Rompi: phaseManager.OnVestWorn(); break;
            case TaskType.Kacamata: phaseManager.OnGlassesWorn(); break;
            case TaskType.Sepatu: phaseManager.OnBootsWorn(); break;
            case TaskType.SarungTangan: phaseManager.OnGlovesWorn(); break;

            // --- APD Khusus Zona ---
            case TaskType.Respirator: phaseManager.OnRespiratiorWorn(); break;
            case TaskType.EarProtection: phaseManager.OnRespiratiorWorn(); break; // EarProtection mapped to Respirator APD slot

            // --- Item Operasional ---
            case TaskType.RadioHT: phaseManager.OnWalkieTalkieTaken(); break;

            // --- Interaksi Lapangan ---
            case TaskType.SteamValveOpen: 
                Debug.Log("[TaskTrigger] Steam Valve Opened. Check GameLevelManager Level 5.");
                break;
            case TaskType.ESDButton: 
                if (GameLevelManager.Instance != null && GameLevelManager.Instance.CurrentLevel == GameLevelManager.GameLevel.Level14_Emergency)
                    GameLevelManager.Instance.SelesaikanLevel(GameLevelManager.GameLevel.Level14_Emergency);
                break;
            case TaskType.IsolationValve: 
                Debug.Log("[TaskTrigger] Isolation Valve Closed. Used in Emergency.");
                break;
        }
    }
}
