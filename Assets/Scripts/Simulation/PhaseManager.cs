using UnityEngine;
using System;
using System.Collections.Generic;

/// <summary>
/// OLIVIA VR - PhaseManager.cs (Refactored v3.0)
/// Sub-sistem APD & Safety Gate yang dikendalikan oleh GameLevelManager.
/// 
/// TANGGUNG JAWAB PhaseManager:
///   - Tracking 7 item APD (equipped / not equipped)
///   - Validasi akses area (kimia, mesin, tailing)
///   - Membuka Safety Gate saat APD lengkap
/// 
/// PhaseManager TIDAK lagi mengatur level atau alur game secara langsung.
/// Semua alur level sekarang dikendalikan oleh GameLevelManager.cs
/// </summary>
public class PhaseManager : MonoBehaviour
{
    // ============================================================
    //  SINGLETON
    // ============================================================
    public static PhaseManager Instance { get; private set; }

    // ============================================================
    //  EVENTS
    // ============================================================
    public static event Action<string> OnApdItemWorn;       // Satu item APD dipakai
    public static event Action         OnAPD7Lengkap;       // Semua 7 APD lengkap → buka Safety Gate
    public static event Action         OnAPDTidakLengkap;   // Pemain mencoba akses tanpa APD

    // ============================================================
    //  MODEL APD
    // ============================================================
    [Serializable]
    public class ApdItem
    {
        public string namaApd;
        public bool   sudahDipakai = false;
        public ApdItem(string nama) { namaApd = nama; }
    }

    // ============================================================
    //  INSPECTOR — 7 APD WAJIB
    // ============================================================
    [Header("=== APD Dasar (5 Item) ===")]
    [SerializeField] private ApdItem _helm         = new ApdItem("Helm K3");
    [SerializeField] private ApdItem _rompi        = new ApdItem("Rompi Safety");
    [SerializeField] private ApdItem _kacamata     = new ApdItem("Kacamata Pelindung");
    [SerializeField] private ApdItem _sepatuBots   = new ApdItem("Sepatu Safety");
    [SerializeField] private ApdItem _sarungTangan = new ApdItem("Sarung Tangan Kimia");

    [Header("=== APD Khusus (2 Item Wajib Tambahan) ===")]
    [SerializeField] private ApdItem _respirator   = new ApdItem("Respirator / Masker Gas");
    [SerializeField] private ApdItem _walikieTalkie = new ApdItem("Walkie Talkie / HT");

    // ============================================================
    //  PROPERTIES
    // ============================================================
    public bool ApdDasarLengkap =>
        _helm.sudahDipakai         &&
        _rompi.sudahDipakai        &&
        _kacamata.sudahDipakai     &&
        _sepatuBots.sudahDipakai   &&
        _sarungTangan.sudahDipakai;

    public bool RespiratiorTerpasang   => _respirator.sudahDipakai;
    public bool WalkieTalkieDiambil    => _walikieTalkie.sudahDipakai;

    /// <summary>True jika semua 7 item APD sudah dipakai/diambil.</summary>
    public bool APD7Lengkap =>
        ApdDasarLengkap            &&
        RespiratiorTerpasang       &&
        WalkieTalkieDiambil;

    public int JumlahAPDTerpasang
    {
        get
        {
            int count = 0;
            foreach (var apd in SemuaAPD()) if (apd.sudahDipakai) count++;
            return count;
        }
    }

    // ============================================================
    //  LIFECYCLE
    // ============================================================
    private void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
    }

    private void Start()
    {
        Log("APD SYSTEM", "Siap! Pakai 7 APD sebelum masuk area plant.", "yellow");
    }

    // ============================================================
    //  HANDLER APD — Dipanggil dari XR Socket/Grab Events
    // ============================================================
    public void OnHelmetWorn()        { PakaiApd(_helm);          }
    public void OnVestWorn()          { PakaiApd(_rompi);         }
    public void OnGlassesWorn()       { PakaiApd(_kacamata);      }
    public void OnBootsWorn()         { PakaiApd(_sepatuBots);    }
    public void OnGlovesWorn()        { PakaiApd(_sarungTangan);  }
    public void OnRespiratiorWorn()   { PakaiApd(_respirator);    }
    public void OnWalkieTalkieTaken() { PakaiApd(_walikieTalkie); }

    // ============================================================
    //  VALIDASI AKSES AREA (Dipanggil dari SafetyGate.cs)
    // ============================================================

    /// <summary>Cek akses ke area utama plant (butuh semua 7 APD).</summary>
    public bool BolehMasukAreaPlant()
    {
        if (APD7Lengkap) return true;

        string namaKurang = CaraAPDYangKurang();
        Log("SAFETY GATE", $"AKSES DITOLAK! APD kurang: {namaKurang}", "red");
        OnAPDTidakLengkap?.Invoke();
        return false;
    }

    /// <summary>Cek akses area kimia khusus (butuh respirator).</summary>
    public bool BolehMasukAreaKimia()
    {
        if (ApdDasarLengkap && RespiratiorTerpasang) return true;
        Log("SAFETY GATE", "AKSES AREA KIMIA DITOLAK! Respirator belum terpasang.", "red");
        OnAPDTidakLengkap?.Invoke();
        return false;
    }

    /// <summary>Kembalikan nama APD pertama yang belum dipakai.</summary>
    public string CaraAPDYangKurang()
    {
        foreach (var apd in SemuaAPD())
            if (!apd.sudahDipakai) return apd.namaApd;
        return "—";
    }

    /// <summary>Kembalikan daftar semua nama APD yang belum dipakai.</summary>
    public List<string> DaftarAPDKurang()
    {
        var kurang = new List<string>();
        foreach (var apd in SemuaAPD())
            if (!apd.sudahDipakai) kurang.Add(apd.namaApd);
        return kurang;
    }

    // ============================================================
    //  INTERNAL
    // ============================================================
    private void PakaiApd(ApdItem apd)
    {
        if (apd.sudahDipakai) return;
        apd.sudahDipakai = true;
        OnApdItemWorn?.Invoke(apd.namaApd);
        Log("APD", $"✓ <b>{apd.namaApd}</b> terpasang! ({JumlahAPDTerpasang}/7)", "green");

        if (APD7Lengkap)
        {
            Log("APD LENGKAP", "Semua 7 APD terpasang! Safety Gate terbuka.", "green");
            OnAPD7Lengkap?.Invoke();
            // Notifikasi GameLevelManager bahwa Level 1 (APD) bisa diselesaikan
            GameLevelManager.Instance?.OnVoiceKeywordTerdeteksi("APD lengkap");
        }
        else
        {
            string kurang = CaraAPDYangKurang();
            Log("APD CHECKLIST", $"Masih perlu: <b>{kurang}</b>", "yellow");
        }
    }

    private IEnumerable<ApdItem> SemuaAPD()
    {
        yield return _helm;
        yield return _rompi;
        yield return _kacamata;
        yield return _sepatuBots;
        yield return _sarungTangan;
        yield return _respirator;
        yield return _walikieTalkie;
    }

    private void Log(string label, string pesan, string warna = "white")
        => Debug.Log($"<color={warna}>[APD-{label}]</color> {pesan}");

    // ============================================================
    //  DEBUG
    // ============================================================
#if UNITY_EDITOR
    [ContextMenu("DEBUG: Pakai Semua APD (Instant)")]
    private void D_PakaiSemuaAPD()
    {
        OnHelmetWorn(); OnVestWorn(); OnGlassesWorn();
        OnBootsWorn(); OnGlovesWorn(); OnRespiratiorWorn(); OnWalkieTalkieTaken();
    }

    [ContextMenu("DEBUG: Reset Semua APD")]
    private void D_ResetAPD()
    {
        foreach (var apd in SemuaAPD()) apd.sudahDipakai = false;
        Log("RESET", "Semua APD direset.", "orange");
    }

    [ContextMenu("DEBUG: Cek Status APD")]
    private void D_CekAPD()
    {
        Log("STATUS", $"APD Terpasang: {JumlahAPDTerpasang}/7 | Lengkap: {APD7Lengkap}", "cyan");
        foreach (var kurang in DaftarAPDKurang())
            Log("KURANG", kurang, "orange");
    }
#endif
}
