using UnityEngine;


public class MachineActivationButton : MonoBehaviour
{
    private PhaseManager phaseManager;

    void Start()
    {
        phaseManager = Object.FindAnyObjectByType<PhaseManager>();
    }

    // Panggil fungsi ini melalui XR Simple Interactable (On Select Entered)
    // Atau bisa juga pakai Trigger Collider jika ingin ditekan pakai tangan langsung
    public void PressButton()
    {
        // Notifikasi GameLevelManager bahwa tombol mesin ditekan
        if (GameLevelManager.Instance != null)
        {
            // Tombol mesin fisik setara dengan DCS Tombol Level 7 (Autoclave)
            GameLevelManager.Instance.OnDCSTombolDitekan(7);
            Debug.Log("[MachineActivationButton] Mesin HPAL diaktifkan → GameLevelManager notified.");
        }
        else
        {
            Debug.LogError("[MachineActivationButton] GameLevelManager tidak ditemukan di Scene!");
        }
    }
    
    // Opsional: Jika menggunakan Trigger Collider untuk ditekan fisik
    private void OnTriggerEnter(Collider other)
    {
        if (other.CompareTag("PlayerHand") || other.GetComponent<UnityEngine.XR.Interaction.Toolkit.Interactors.XRDirectInteractor>() != null)
        {
            PressButton();
        }
    }
}
