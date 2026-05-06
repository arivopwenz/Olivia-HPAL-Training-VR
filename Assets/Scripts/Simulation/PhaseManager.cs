using UnityEngine;

public class PhaseManager : MonoBehaviour
{
    [Header("Status Simulasi")]
    public int currentPhase = 1;
    public bool isScannerGrabbed = false;

    void Start()
    {
        Debug.Log("<color=green>OLIVIA SIMULATOR:</color> Selamat datang! Kita mulai dari Fase 1 (Persiapan).");
        Debug.Log("<color=yellow>TUGAS:</color> Silakan ambil Kotak Kuning (Scanner) di depanmu.");
    }

    // Fungsi ini akan dipanggil saat kita ambil kotak
    public void OnScannerGrabbed()
    {
        if (!isScannerGrabbed)
        {
            isScannerGrabbed = true;
            Debug.Log("<color=green>TUGAS SELESAI:</color> Kamu sudah mengambil Scanner!");
            Debug.Log("<color=yellow>TUGAS BERIKUTNYA:</color> Silakan teleport ke silinder merah untuk memulai operasional.");
        }
    }

    public void OnScannerPlaced()
    {
        Debug.Log("<color=green>FASE 1 SELESAI!</color> Alat sudah terpasang di tempatnya.");
        Debug.Log("<color=cyan>SOP:</color> Silakan tekan tombol utama untuk menyalakan mesin (Fase 2).");

        // Kita pindah ke fase 2
        currentPhase = 2;
    }

    public void OnHelmetWorn()
    {
        Debug.Log("<color=green>APD TERPASANG:</color> Helm sudah dipakai. Kepala terlindungi!");
        Debug.Log("<color=yellow>TUGAS:</color> Sekarang silakan ambil peralatan scanner kamu.");
    }

}
