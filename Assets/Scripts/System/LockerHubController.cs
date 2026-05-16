using UnityEngine;
using System.Collections.Generic;

/// <summary>
/// OLIVIA VR - LockerHubController.cs (v3.0 - Simplified 8 APD)
/// 
/// Mengontrol Ruang Loker:
///   - Cek 8 APD wajib (5 dasar + respirator + earplug + walkie talkie)
///   - Teleportasi pemain ke lokasi yang sesuai
///   - Tidak lagi mengurus Harness/Lanyard/JasHujan
/// </summary>
public class LockerHubController : MonoBehaviour
{
    [Header("=== Referensi Player ===")]
    [Tooltip("Tarik XR Origin (XR Rig) ke sini untuk teleport. Jika kosong, akan dicari otomatis.")]
    public Transform xrOrigin;

    [System.Serializable]
    public class LevelSpawnPoint
    {
        public GameLevelManager.GameLevel level;
        [Tooltip("Titik tujuan di lapangan")]
        public Transform spawnPoint;
    }

    [Header("=== Pengaturan Teleport per Level ===")]
    [Tooltip("Daftar titik teleport tiap level")]
    public List<LevelSpawnPoint> daftarSpawnPoint;

    private PhaseManager phaseManager;

    void Start()
    {
        phaseManager = Object.FindAnyObjectByType<PhaseManager>();

        // Auto-find XROrigin jika belum di-assign di inspector
        if (xrOrigin == null)
        {
            var xrRig = GameObject.Find("XR Origin (XR Rig)")
                     ?? GameObject.Find("XR Origin")
                     ?? GameObject.FindWithTag("Player");
            if (xrRig != null)
            {
                xrOrigin = xrRig.transform;
                Debug.Log($"[LockerHub] Auto-found XR Origin: {xrRig.name}");
            }
            else
            {
                Debug.LogError("[LockerHub] XR Origin tidak ditemukan! Assign manual di Inspector.");
            }
        }
    }

    /// <summary>
    /// Dipanggil saat pemain berinteraksi/menyentuh Pintu Keluar Loker.
    /// </summary>
    public void CobaKeluarLoker()
    {
        if (GameLevelManager.Instance == null || phaseManager == null)
        {
            Debug.LogError("[LockerHub] GameLevelManager atau PhaseManager belum ada di scene!");
            return;
        }

        GameLevelManager.GameLevel levelSekarang = GameLevelManager.Instance.CurrentLevel;
        LevelSpawnPoint config = daftarSpawnPoint.Find(x => x.level == levelSekarang);

        if (config == null || config.spawnPoint == null)
        {
            Debug.LogWarning($"[LockerHub] Belum ada SpawnPoint untuk level {levelSekarang}. Assign di Inspector!");
            ShowPesanGagal("⚠ Pintu belum dikonfigurasi di Inspector!");
            return;
        }

        // --- CEK 8 APD WAJIB ---
        if (!phaseManager.APDLengkapSempurna)
        {
            string kurang = phaseManager.CaraAPDYangKurang();
            ShowPesanGagal($"✗ Belum bisa keluar!\nAPD kurang: {kurang}");
            return;
        }

        // --- PROSES TELEPORT ---
        if (xrOrigin == null)
        {
            Debug.LogError("[LockerHub] xrOrigin NULL! Tidak bisa teleport.");
            return;
        }

        Debug.Log($"[LockerHub] ✓ APD Lengkap ({PhaseManager.TOTAL_APD}/{PhaseManager.TOTAL_APD})! Teleport ke: {config.spawnPoint.name}");
        xrOrigin.position = config.spawnPoint.position;
        xrOrigin.rotation = config.spawnPoint.rotation;

        // Beritahu PlayerHUD untuk geser ke fase berikutnya
        var hud = Object.FindAnyObjectByType<PlayerHUD>();
        hud?.NotifyMasukPintu();

        if (levelSekarang == GameLevelManager.GameLevel.Level1_APD)
            GameLevelManager.Instance.SelesaikanLevel(GameLevelManager.GameLevel.Level1_APD);
    }

    private void OnTriggerEnter(Collider other)
    {
        // Cek apakah yang masuk adalah Player (Main Camera atau XR Origin)
        if (other.CompareTag("MainCamera") || other.CompareTag("Player") || other.name.Contains("Camera"))
        {
            Debug.Log("[LockerHub] Trigger Pintu: Player mendeteksi area keluar.");
            CobaKeluarLoker();
        }
    }

    private void ShowPesanGagal(string pesan)
    {
        Debug.Log($"[LockerHub] GAGAL: {pesan}");
        var hud = Object.FindAnyObjectByType<PlayerHUD>();
        if (hud != null)
            hud.ShowNotifPublic(pesan);
    }
}
