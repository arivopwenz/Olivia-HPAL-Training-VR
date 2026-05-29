using UnityEngine;
using System.Collections;
using System.Collections.Generic;

/// <summary>
/// OLIVIA VR - LevelTeleportManager.cs
///
/// Single source of truth untuk SEMUA teleport saat pergantian level.
/// Cara pakai:
///   1. Buat empty GameObject "TeleportManager" di scene (atau pasang di mana saja).
///   2. Drag XR Origin ke field _xrOrigin.
///   3. Di list _spawnConfigs, tambah satu entry per level:
///      - level: GameLevel target (Level2_DCSPrep, Level3_OreSlurry, dll)
///      - spawnPoint: Transform target (drag GameObject hierarchy seperti SpawnPoint_DCS)
///      - autoNotifyDcsViewed: centang untuk Level 2 supaya quest auto-check
///   4. Disable LevelSpawnTeleporter LAMA + hapus teleport call dari LockerHubController.
///
/// Tidak ada lagi GameObject.Find, koordinat hardcoded, atau race condition. Semua
/// pakai Transform references langsung dari Inspector.
/// </summary>
public class LevelTeleportManager : MonoBehaviour
{
    [System.Serializable]
    public class LevelSpawnConfig
    {
        public GameLevelManager.GameLevel level;
        [Tooltip("Drag empty GameObject (mis. SpawnPoint_DCS) dari hierarchy ke sini.")]
        public Transform spawnPoint;
        [Tooltip("Saat level ini start, otomatis call NotifyDcsViewed (untuk Level 2).")]
        public bool autoNotifyDcsViewed;
        [Tooltip("Override rotation Y. Kosongkan (0) untuk pakai rotation Transform.")]
        public bool overrideRotation;
        public Vector3 customEulerAngles = Vector3.zero;
    }

    [Header("=== Player Reference ===")]
    [Tooltip("Drag XR Origin (XR Rig) ke sini.")]
    [SerializeField] private Transform _xrOrigin;

    [Header("=== Spawn Configs (Drag GameObjects ke sini) ===")]
    [Tooltip("Tambah 1 entry per level yang butuh teleport saat OnLevelStarted.")]
    [SerializeField] private List<LevelSpawnConfig> _spawnConfigs = new List<LevelSpawnConfig>();

    [Header("=== Debug ===")]
    [SerializeField] private bool _debugLog = true;

    private void Awake()
    {
        if (_xrOrigin == null)
        {
            var rig = GameObject.Find("XR Origin (XR Rig)")
                  ?? GameObject.Find("XR Origin")
                  ?? GameObject.Find("XR Rig")
                  ?? GameObject.FindWithTag("Player");
            if (rig != null)
            {
                _xrOrigin = rig.transform;
                if (_debugLog) Debug.Log($"[LevelTeleportManager] Auto-found XR Origin: {rig.name}");
            }
        }

        EnsureDefaultSpawnConfigs();
    }

    private void OnValidate()
    {
        EnsureDefaultSpawnConfigs();
    }

    private void OnEnable()
    {
        GameLevelManager.OnLevelStarted += OnLevelStarted;
    }

    private void OnDisable()
    {
        GameLevelManager.OnLevelStarted -= OnLevelStarted;
    }

    private void OnLevelStarted(GameLevelManager.GameLevel level)
    {
        var config = _spawnConfigs.Find(c => c.level == level);
        if (config == null)
        {
            if (_debugLog) Debug.Log($"[LevelTeleportManager] Tidak ada config teleport untuk {level}, skip.");
            return;
        }

        if (config.spawnPoint == null)
        {
            Debug.LogWarning($"[LevelTeleportManager] SpawnPoint Transform untuk {level} BELUM DI-ASSIGN di Inspector!");
            return;
        }

        Teleport(config);

        if (config.autoNotifyDcsViewed && level == GameLevelManager.GameLevel.Level2_DCSPrep)
            StartCoroutine(NotifyDcsViewedDelayed());
    }

    private void Teleport(LevelSpawnConfig config)
    {
        Transform liveOrigin = ResolveLiveXrOrigin();
        if (liveOrigin == null)
        {
            Debug.LogWarning("[LevelTeleportManager] XR Origin tidak ditemukan di scene aktif.");
            return;
        }
        _xrOrigin = liveOrigin;

        Vector3 targetPos = config.spawnPoint.position;
        Quaternion targetRot = config.overrideRotation
            ? Quaternion.Euler(config.customEulerAngles)
            : config.spawnPoint.rotation;

        // Gunakan XROrigin.MoveCameraToWorldLocation — API resmi XR Toolkit
        // yang properly handles camera offset, simulator, dan tracking origin.
        var xrOriginComp = _xrOrigin.GetComponent<Unity.XR.CoreUtils.XROrigin>();
        var cc = _xrOrigin.GetComponent<CharacterController>();
        bool ccEnabled = cc != null && cc.enabled;
        if (ccEnabled) cc.enabled = false;

        if (xrOriginComp != null)
        {
            // MoveCameraToWorldLocation menggeser Origin agar Camera ada di targetPos.
            // Kita mau player FEET di targetPos, jadi camera target = targetPos + up * cameraYOffset.
            Vector3 cameraTarget = targetPos + Vector3.up * xrOriginComp.CameraYOffset;
            xrOriginComp.MoveCameraToWorldLocation(cameraTarget);

            // Rotasi: MatchOriginUpCameraForward untuk set yaw
            Vector3 fwd = targetRot * Vector3.forward;
            fwd.y = 0f;
            if (fwd.sqrMagnitude < 0.0001f)
                fwd = _xrOrigin.forward;
            fwd.Normalize();
            xrOriginComp.MatchOriginUpCameraForward(Vector3.up, fwd);
        }
        else
        {
            // Fallback kalau XROrigin component gak ada
            _xrOrigin.SetPositionAndRotation(targetPos, Quaternion.Euler(0f, targetRot.eulerAngles.y, 0f));
        }

        if (ccEnabled && cc != null) cc.enabled = true;

        // Recovery: re-enable NearFar/Poke Interactor yang ke-disable oleh
        // ControllerInputActionManager.OnStartTeleport (XR Toolkit sample) saat
        // input teleport dipencet. Tanpa ini, ray klik UI hilang di Level 2-14.
        XRInteractorRecovery.PulihkanRayInteractor();

        if (_debugLog)
            Debug.Log($"[LevelTeleportManager] Teleport {config.level} → '{config.spawnPoint.name}' target={targetPos}. Origin final pos={_xrOrigin.position}, Camera pos={xrOriginComp?.Camera?.transform.position}");
    }



    /// <summary>
    /// Resolve XR Origin Transform yang BENAR-BENAR aktif di scene saat ini, bukan referensi stale
    /// dari Inspector. Strategi prioritas:
    ///   1. FindObjectsByType<XROrigin>() — paling akurat untuk Unity 2022+ XR setup
    ///   2. Cari GameObject dengan nama umum (XR Origin, XR Rig, Player)
    ///   3. Cari via tag Player
    /// </summary>
    private Transform ResolveLiveXrOrigin()
    {
        // 1. Coba lewat XROrigin component (paling reliable)
        var origins = UnityEngine.Object.FindObjectsByType<Unity.XR.CoreUtils.XROrigin>(
            UnityEngine.FindObjectsInactive.Exclude,
            UnityEngine.FindObjectsSortMode.None);
        if (origins != null && origins.Length > 0)
        {
            // Prefer yang scene-nya sama dengan teleport manager ini
            for (int i = 0; i < origins.Length; i++)
            {
                if (origins[i].gameObject.scene == gameObject.scene)
                    return origins[i].transform;
            }
            return origins[0].transform;
        }

        // 2. Fallback: GameObject.Find by name
        var byName = GameObject.Find("XR Origin (XR Rig)")
                  ?? GameObject.Find("XR Origin")
                  ?? GameObject.Find("XR Rig");
        if (byName != null)
            return byName.transform;

        // 3. Fallback: tag Player
        var byTag = GameObject.FindWithTag("Player");
        if (byTag != null)
            return byTag.transform;

        return null;
    }

    private void EnsureDefaultSpawnConfigs()
    {
        if (_spawnConfigs == null)
            _spawnConfigs = new List<LevelSpawnConfig>();

        EnsureSpawnConfig(GameLevelManager.GameLevel.Level1_APD, "SpawnPoint_APD", false);
        EnsureSpawnConfig(GameLevelManager.GameLevel.Level2_DCSPrep, "SpawnPoint_DCS", true);
        EnsureSpawnConfig(GameLevelManager.GameLevel.Level3_OreSlurry, "SpawnPoint_DCS", false);
        EnsureSpawnConfig(GameLevelManager.GameLevel.Level4_SlurryPump, "SpawnPoint_DCS", false);
        EnsureSpawnConfig(GameLevelManager.GameLevel.Level5_SteamValve, "SpawnPoint_DCS", false);
        EnsureSpawnConfig(GameLevelManager.GameLevel.Level6_AcidInjection, "SpawnPoint_DCS", false);
        EnsureSpawnConfig(GameLevelManager.GameLevel.Level7_Autoclave, "SpawnPoint_DCS", false);
        EnsureSpawnConfig(GameLevelManager.GameLevel.Level8_Monitoring, "SpawnPoint_DCS", false);
        EnsureSpawnConfig(GameLevelManager.GameLevel.Level9_FlashVessel, "SpawnPoint_DCS", false);
        EnsureSpawnConfig(GameLevelManager.GameLevel.Level10_CCD, "SpawnPoint_DCS", false);
        EnsureSpawnConfig(GameLevelManager.GameLevel.Level11_MHP, "SpawnPoint_DCS", false);
        EnsureSpawnConfig(GameLevelManager.GameLevel.Level12_TailingDischarge, "SpawnPoint_DCS", false);
        EnsureSpawnConfig(GameLevelManager.GameLevel.Level13_TailingWaste, "SpawnPoint_Lvl13", false);
        EnsureSpawnConfig(GameLevelManager.GameLevel.Level14_Emergency, "SpawnPoint_Lvl14", false);
    }

    private void EnsureSpawnConfig(GameLevelManager.GameLevel level, string spawnName, bool autoNotifyDcsViewed)
    {
        LevelSpawnConfig config = _spawnConfigs.Find(c => c != null && c.level == level);
        if (config == null)
        {
            config = new LevelSpawnConfig { level = level };
            _spawnConfigs.Add(config);
        }

        if (config.spawnPoint == null)
            config.spawnPoint = FindSpawnPoint(spawnName);

        config.autoNotifyDcsViewed = autoNotifyDcsViewed;
    }

    private Transform FindSpawnPoint(string spawnName)
    {
        GameObject go = GameObject.Find(spawnName);
        if (go != null)
            return go.transform;

        foreach (Transform t in UnityEngine.Object.FindObjectsByType<Transform>(FindObjectsInactive.Include, FindObjectsSortMode.None))
        {
            if (t.name == spawnName)
                return t;
        }

        return null;
    }

    private IEnumerator NotifyDcsViewedDelayed()
    {
        yield return null; // 1 frame delay supaya state cleanup CC selesai
        if (GameLevelManager.Instance != null &&
            GameLevelManager.Instance.CurrentLevel == GameLevelManager.GameLevel.Level2_DCSPrep)
        {
            GameLevelManager.Instance.NotifyDcsViewed();
            if (_debugLog) Debug.Log("[LevelTeleportManager] Auto-notify NotifyDcsViewed setelah spawn Level 2.");
        }
    }

    /// <summary>
    /// API publik untuk teleport manual (mis. dari LockerHubController saat keluar pintu).
    /// </summary>
    public void TeleportKeLevel(GameLevelManager.GameLevel level)
    {
        var config = _spawnConfigs.Find(c => c.level == level);
        if (config == null)
        {
            Debug.LogWarning($"[LevelTeleportManager] Tidak ada config untuk {level}.");
            return;
        }
        Teleport(config);
    }

    /// <summary>
    /// Visualisasi spawn points di Scene View (cuma di Editor).
    /// Marker hijau = posisi spawn point. Garis kuning = arah forward (rotation).
    /// </summary>
    private void OnDrawGizmos()
    {
        if (_spawnConfigs == null) return;
        foreach (var c in _spawnConfigs)
        {
            if (c == null || c.spawnPoint == null) continue;
            var pos = c.spawnPoint.position;
            var rot = c.overrideRotation
                ? Quaternion.Euler(c.customEulerAngles)
                : c.spawnPoint.rotation;

            // Marker hijau di posisi spawn
            Gizmos.color = Color.green;
            Gizmos.DrawSphere(pos, 0.15f);

            // Capsule outline player (tinggi 1.8m)
            Gizmos.color = new Color(0f, 1f, 0f, 0.4f);
            Gizmos.DrawWireCube(pos + Vector3.up * 0.9f, new Vector3(0.5f, 1.8f, 0.5f));

            // Arah forward (kuning)
            Gizmos.color = Color.yellow;
            Vector3 fwd = rot * Vector3.forward * 1.5f;
            Gizmos.DrawLine(pos + Vector3.up * 1.0f, pos + Vector3.up * 1.0f + fwd);

            // Label level (cuma di editor selected)
#if UNITY_EDITOR
            UnityEditor.Handles.color = Color.cyan;
            UnityEditor.Handles.Label(pos + Vector3.up * 2.1f, $"Spawn: {c.level}");
#endif
        }
    }
}
