using UnityEngine;
using System;
using System.Collections.Generic;

/// <summary>
/// OLIVIA VR - PhaseManager.cs (v5.0 - Final Clean 8 Item)
/// 
/// APD WAJIB (8 item):
///   1. Helm K3
///   2. Rompi Safety
///   3. Kacamata Pelindung
///   4. Sepatu Safety
///   5. Sarung Tangan Kimia
///   6. Respirator / Masker Gas
///   7. Earplug
///   8. Walkie Talkie / HT
/// </summary>
public class PhaseManager : MonoBehaviour
{
    public static PhaseManager Instance { get; private set; }

    public static event Action<string> OnApdItemWorn;
    public static event Action         OnAPD7Lengkap;
    public static event Action         OnAPDTidakLengkap;

    [Serializable]
    public class ApdItem
    {
        public string namaApd;
        public bool   sudahDipakai = false;
        public ApdItem(string nama) { namaApd = nama; }
    }

    [Header("=== APD Wajib (8 Item) ===")]
    [SerializeField] private ApdItem _helm         = new ApdItem("Helm K3");
    [SerializeField] private ApdItem _rompi        = new ApdItem("Rompi Safety");
    [SerializeField] private ApdItem _kacamata     = new ApdItem("Kacamata Pelindung");
    [SerializeField] private ApdItem _sepatuBots   = new ApdItem("Sepatu Safety");
    [SerializeField] private ApdItem _sarungTangan = new ApdItem("Sarung Tangan Kimia");
    [SerializeField] private ApdItem _respirator   = new ApdItem("Respirator / Masker Gas");
    [SerializeField] private ApdItem _earplug      = new ApdItem("Ear Protection / Earplug");
    [SerializeField] private ApdItem _walkieTalkie = new ApdItem("Walkie Talkie / HT");

    public const int TOTAL_APD = 8;

    public bool ApdDasarLengkap =>
        _helm.sudahDipakai         &&
        _rompi.sudahDipakai        &&
        _kacamata.sudahDipakai     &&
        _sepatuBots.sudahDipakai   &&
        _sarungTangan.sudahDipakai;

    public bool isHelmetWorn       => _helm.sudahDipakai;
    public bool isVestWorn         => _rompi.sudahDipakai;
    public bool isGlassesWorn      => _kacamata.sudahDipakai;
    public bool isBootsWorn        => _sepatuBots.sudahDipakai;
    public bool isGlovesWorn       => _sarungTangan.sudahDipakai;
    public bool isRespiratorWorn   => _respirator.sudahDipakai;
    public bool isEarplugWorn      => _earplug.sudahDipakai;
    public bool isWalkieTalkieTaken => _walkieTalkie.sudahDipakai;

    public bool APDLengkapSempurna =>
        ApdDasarLengkap            &&
        isRespiratorWorn           &&
        isEarplugWorn              &&
        isWalkieTalkieTaken;

    public int JumlahAPDTerpasang
    {
        get
        {
            int count = 0;
            foreach (var apd in SemuaAPD()) if (apd.sudahDipakai) count++;
            return count;
        }
    }

    private void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
    }

    private void Start()
    {
        Log("APD SYSTEM", $"Siap! Pakai {TOTAL_APD} APD sebelum keluar loker.", "yellow");
    }

    public void OnHelmetWorn()        { PakaiApd(_helm);          }
    public void OnVestWorn()          { PakaiApd(_rompi);         }
    public void OnGlassesWorn()       { PakaiApd(_kacamata);      }
    public void OnBootsWorn()         { PakaiApd(_sepatuBots);    }
    public void OnGlovesWorn()        { PakaiApd(_sarungTangan);  }
    public void OnRespiratiorWorn()   { PakaiApd(_respirator);    }
    public void OnEarplugWorn()       { PakaiApd(_earplug);       }
    public void OnWalkieTalkieTaken() { PakaiApd(_walkieTalkie);  }

    public bool BolehMasukAreaPlant()
    {
        if (ApdDasarLengkap) return true;

        string namaKurang = CaraAPDYangKurang();
        Log("SAFETY GATE", $"AKSES DITOLAK! APD kurang: {namaKurang}", "red");
        OnAPDTidakLengkap?.Invoke();
        return false;
    }

    public string CaraAPDYangKurang()
    {
        var kurang = new List<string>();
        foreach (var apd in SemuaAPD())
            if (!apd.sudahDipakai) kurang.Add(apd.namaApd);
        return kurang.Count > 0 ? string.Join(", ", kurang) : "—";
    }

    public List<string> DaftarAPDKurang()
    {
        var kurang = new List<string>();
        foreach (var apd in SemuaAPD())
            if (!apd.sudahDipakai) kurang.Add(apd.namaApd);
        return kurang;
    }

    private void PakaiApd(ApdItem apd)
    {
        if (apd.sudahDipakai) return;
        apd.sudahDipakai = true;
        OnApdItemWorn?.Invoke(apd.namaApd);
        Log("APD", $"✓ <b>{apd.namaApd}</b> terpasang! ({JumlahAPDTerpasang}/{TOTAL_APD})", "green");

        if (APDLengkapSempurna)
        {
            Log("APD LENGKAP", $"Semua {TOTAL_APD} APD terpasang sempurna!", "green");
            OnAPD7Lengkap?.Invoke();
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
        yield return _earplug;
        yield return _walkieTalkie;
    }

    private void Log(string label, string pesan, string warna = "white")
        => Debug.Log($"<color={warna}>[APD-{label}]</color> {pesan}");

#if UNITY_EDITOR
    [ContextMenu("DEBUG: Pakai Semua APD (Instant)")]
    private void D_PakaiSemuaAPD()
    {
        OnHelmetWorn(); OnVestWorn(); OnGlassesWorn();
        OnBootsWorn(); OnGlovesWorn(); OnRespiratiorWorn();
        OnEarplugWorn(); OnWalkieTalkieTaken();
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
        Log("STATUS", $"APD Terpasang: {JumlahAPDTerpasang}/{TOTAL_APD} | Lengkap: {APDLengkapSempurna}", "cyan");
        foreach (var kurang in DaftarAPDKurang())
            Log("KURANG", kurang, "orange");
    }
#endif
}
