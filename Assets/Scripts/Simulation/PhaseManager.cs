using UnityEngine;
using System.Collections.Generic;
using System;

public class PhaseManager : MonoBehaviour
{
    public enum SimulationPhase
    {
        Idle = 0,
        PreparasiAPD = 1,
        OperasionalAlat = 2,
        AktifMesin = 3,
        Selesai = 4
    }

    [Serializable]
    public class ApdItem
    {
        public string namaApd;
        public bool sudahDipakai = false;

        public ApdItem(string nama)
        {
            namaApd = nama;
        }
    }

    public static event Action<SimulationPhase> OnPhaseChanged;
    public static event Action OnAllApdEquipped;
    public static event Action<string> OnApdItemWorn;
    public static event Action OnScannerPickedUp;
    public static event Action OnScannerInstalled;

    [Header("=== Status Fase Saat Ini ===")]
    [SerializeField] private SimulationPhase _currentPhase = SimulationPhase.Idle;

    [Header("=== Status APD ===")]
    [SerializeField] private ApdItem _helm = new ApdItem("Helm K3");
    [SerializeField] private ApdItem _rompi = new ApdItem("Rompi Safety");
    [SerializeField] private ApdItem _kacamata = new ApdItem("Kacamata Pelindung");
    [SerializeField] private ApdItem _sepatuBots = new ApdItem("Sepatu Boots");

    [Header("=== Status Operasional ===")]
    [SerializeField] private bool _scannerSudahDiambil = false;
    [SerializeField] private bool _scannerSudahDipasang = false;

    public SimulationPhase CurrentPhase => _currentPhase;
    public bool ScannerSudahDiambil => _scannerSudahDiambil;

    private void Start()
    {
        MulaiSimulasi();
    }

    public void OnHelmetWorn()
    {
        CatatApdDipakai(_helm);
        CekSemuaApdLengkap();
    }

    public void OnVestWorn()
    {
        CatatApdDipakai(_rompi);
        CekSemuaApdLengkap();
    }

    public void OnGlassesWorn()
    {
        CatatApdDipakai(_kacamata);
        CekSemuaApdLengkap();
    }

    public void OnBootsWorn()
    {
        CatatApdDipakai(_sepatuBots);
        CekSemuaApdLengkap();
    }

    public void OnScannerGrabbed()
    {
        if (_scannerSudahDiambil) return;

        if (_currentPhase != SimulationPhase.OperasionalAlat)
        {
            Log("PERINGATAN", "Scanner belum bisa diambil. Lengkapi APD terlebih dahulu!", "orange");
            return;
        }

        _scannerSudahDiambil = true;
        OnScannerPickedUp?.Invoke();
        Log("TUGAS SELESAI", "Scanner berhasil diambil!", "green");
        Log("TUGAS BERIKUTNYA", "Bawa scanner ke silinder merah dan tempatkan di sana.", "yellow");
    }

    public void OnScannerPlaced()
    {
        if (_scannerSudahDipasang) return;

        _scannerSudahDipasang = true;
        OnScannerInstalled?.Invoke();
        Log("FASE 2 SELESAI", "Scanner terpasang! Sistem siap diaktifkan.", "green");
        Log("SOP SELANJUTNYA", "Tekan tombol utama untuk menyalakan mesin HPAL (Fase 3).", "cyan");

        UbahFase(SimulationPhase.AktifMesin);
    }

    public void OnMachineActivated()
    {
        if (_currentPhase != SimulationPhase.AktifMesin)
        {
            Log("PERINGATAN", "Mesin belum siap. Pastikan Scanner sudah terpasang!", "orange");
            return;
        }

        Log("FASE 3 AKTIF", "Mesin HPAL berhasil dinyalakan! Proses dimulai.", "green");
        UbahFase(SimulationPhase.Selesai);
    }

    private void MulaiSimulasi()
    {
        Log("OLIVIA SIMULATOR", "Selamat Datang! Ikuti SOP dengan benar.", "white");
        UbahFase(SimulationPhase.PreparasiAPD);
    }

    private void CatatApdDipakai(ApdItem apd)
    {
        if (apd.sudahDipakai) return;
        apd.sudahDipakai = true;
        OnApdItemWorn?.Invoke(apd.namaApd);
        Log("APD TERPASANG", $"{apd.namaApd} berhasil dipakai. Bagus!", "green");
    }

    private void CekSemuaApdLengkap()
    {
        var daftarApd = new List<ApdItem> { _helm, _rompi, _kacamata, _sepatuBots };

        ApdItem sisaApd = daftarApd.Find(a => !a.sudahDipakai);
        if (sisaApd != null)
        {
            Log("TUGAS", $"Masih perlu memakai: <b>{sisaApd.namaApd}</b>", "yellow");
            return;
        }

        Log("FASE 1 SELESAI", "Semua APD terpasang! Kamu siap masuk ke area kerja HPAL.", "green");
        Log("TUGAS BERIKUTNYA", "Ambil Scanner untuk memulai operasional.", "cyan");

        OnAllApdEquipped?.Invoke();
        UbahFase(SimulationPhase.OperasionalAlat);
    }

    private void UbahFase(SimulationPhase faseBaru)
    {
        _currentPhase = faseBaru;
        Log("FASE BERUBAH", $"Sekarang masuk ke fase: <b>{faseBaru}</b>", "cyan");
        OnPhaseChanged?.Invoke(faseBaru);
    }

    private void Log(string label, string pesan, string warna = "white")
    {
        Debug.Log($"<color={warna}>[{label}]</color> {pesan}");
    }
}
