using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit.Interactables;

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
            Debug.Log($"[AutoEquip] Player menyentuh {gameObject.name}, otomatis memakai {GetEffectiveTaskType()}");
            DoNotify();
        }
    }

    private void DoNotify()
    {
        if (phaseManager == null) return;

        TaskType effectiveType = GetEffectiveTaskType();
        DockApdKeSocketTubuh(effectiveType);

        switch (effectiveType)
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

    private TaskType GetEffectiveTaskType()
    {
        XRGrabInteractable grabRoot = GetComponentInParent<XRGrabInteractable>();
        if (grabRoot == null)
            return tipeTugas;

        TaskTrigger rootTrigger = grabRoot.GetComponent<TaskTrigger>();
        return rootTrigger != null ? rootTrigger.tipeTugas : tipeTugas;
    }

    private void DockApdKeSocketTubuh(TaskType type)
    {
        string socketName = SocketTubuhUntuk(type);
        if (string.IsNullOrEmpty(socketName))
            return;

        XRGrabInteractable grabRoot = GetComponentInParent<XRGrabInteractable>();
        Transform item = grabRoot != null ? grabRoot.transform : transform;
        GameObject socketObject = GameObject.Find(socketName);
        if (socketObject == null || item == null)
            return;

        if (grabRoot != null && grabRoot.isSelected && grabRoot.interactionManager != null)
        {
            var selecting = new System.Collections.Generic.List<UnityEngine.XR.Interaction.Toolkit.Interactors.IXRSelectInteractor>(grabRoot.interactorsSelecting);
            foreach (var interactor in selecting)
                grabRoot.interactionManager.SelectExit(interactor, grabRoot);
        }

        Rigidbody rb = item.GetComponent<Rigidbody>();
        if (rb != null)
        {
            rb.linearVelocity = Vector3.zero;
            rb.angularVelocity = Vector3.zero;
            rb.isKinematic = true;
            rb.useGravity = false;
        }

        ApdDisplayItemStabilizer stabilizer = item.GetComponent<ApdDisplayItemStabilizer>();
        if (stabilizer != null)
            stabilizer.enabled = false;

        Vector3 worldScale = item.lossyScale;
        item.SetParent(socketObject.transform, false);
        item.localPosition = Vector3.zero;
        item.localRotation = Quaternion.identity;
        SetWorldScale(item, worldScale);

        foreach (Renderer renderer in item.GetComponentsInChildren<Renderer>(true))
            if (renderer != null) renderer.enabled = true;
        foreach (Collider collider in item.GetComponentsInChildren<Collider>(true))
            if (collider != null) collider.enabled = true;
        if (grabRoot != null) grabRoot.enabled = true;

        item.gameObject.SetActive(true);
    }

    private string SocketTubuhUntuk(TaskType type)
    {
        switch (type)
        {
            case TaskType.Helm: return "Socket_Helmet";
            case TaskType.Rompi: return "Socket_Rompi";
            case TaskType.Kacamata: return "Socket_Glasess";
            case TaskType.Sepatu: return "Socket_Boots";
            case TaskType.SarungTangan: return "Socket_Gloves";
            case TaskType.Respirator: return "Socket_RespiratorMask";
            case TaskType.EarProtection: return "Socket_EarPlug";
            case TaskType.RadioHT: return "Socket_WalkieTalkie";
            default: return null;
        }
    }

    private void SetWorldScale(Transform target, Vector3 worldScale)
    {
        Vector3 parentScale = target.parent != null ? target.parent.lossyScale : Vector3.one;
        target.localScale = new Vector3(
            Mathf.Approximately(parentScale.x, 0f) ? worldScale.x : worldScale.x / parentScale.x,
            Mathf.Approximately(parentScale.y, 0f) ? worldScale.y : worldScale.y / parentScale.y,
            Mathf.Approximately(parentScale.z, 0f) ? worldScale.z : worldScale.z / parentScale.z
        );
    }
}
