using UnityEngine;

/// <summary>
/// Merapikan collider socket agar tidak menghalangi grab.
/// Pasang di parent socket (contoh: Tools).
/// </summary>
public class SocketAutoTidy : MonoBehaviour
{
    [SerializeField] private string namePrefix = "Socket_";
    [SerializeField] private bool setTrigger = true;
    [SerializeField] private bool disableRigidbody = true;
    [SerializeField] private bool disableScannerSocketColliders = true;

    private void Awake()
    {
        var colliders = GetComponentsInChildren<Collider>(true);
        foreach (var col in colliders)
        {
            if (!col.gameObject.name.StartsWith(namePrefix))
                continue;

            if (disableScannerSocketColliders && col.gameObject.name.StartsWith("Socket_Scanner_"))
            {
                col.enabled = false;
                continue;
            }

            if (setTrigger)
                col.isTrigger = true;

            if (disableRigidbody)
            {
                var rb = col.GetComponent<Rigidbody>();
                if (rb != null)
                {
                    rb.isKinematic = true;
                    rb.useGravity = false;
                }
            }
        }
    }
}
