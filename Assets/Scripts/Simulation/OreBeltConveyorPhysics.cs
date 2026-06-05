using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Physics-based ore conveyor untuk Level 3.
/// Ore (Rigidbody + Collider) duduk DI ATAS collider belt (L2_V2_Wide_Inclined_Rubber_Ore_Belt)
/// memakai GRAVITASI, lalu didorong naik sepanjang belt sampai jatuh ke chute
/// (L3_SlurryTank_InclinedOreFeedChute) dan masuk slurry tank.
///
/// Dipasang ke root "Level3_Runtime_Ore_Belt_Flow". Semua child ore otomatis di-setup.
/// Aktif/nonaktif via SetRunning(bool).
/// </summary>
[DisallowMultipleComponent]
public class OreBeltConveyorPhysics : MonoBehaviour
{
    [Header("=== Belt Path (world X) ===")]
    [Tooltip("X ujung crusher/feed (low end). Ore mulai di sini.")]
    [SerializeField] private float _tailX = 138f;
    [Tooltip("X ujung discharge/head (high end, dekat chute).")]
    [SerializeField] private float _headX = 100f;
    [Tooltip("X di mana ore lepas dari belt dan jatuh ke chute/tank.")]
    [SerializeField] private float _dischargeX = 99f;

    [Header("=== Belt Surface (world Y per X) ===")]
    [SerializeField] private float _tailY = 3.6f;   // Y belt di tailX
    [SerializeField] private float _headY = 9.25f;  // Y belt di headX
    [SerializeField] private float _beltZ = 56.5f;

    [Header("=== Tank ===")]
    [Tooltip("Y permukaan/dasar tank. Ore di bawah ini dianggap sudah masuk tank.")]
    [SerializeField] private float _tankCatchY = 5.0f;
    [Tooltip("X pusat slurry tank (tujuan jatuh ore).")]
    [SerializeField] private float _tankCenterX = 91f;
    [Tooltip("Z pusat slurry tank.")]
    [SerializeField] private float _tankCenterZ = 55.1f;
    [Tooltip("Radius (XZ) di sekitar pusat tank. Ore yang masuk radius ini dianggap masuk tank.")]
    [SerializeField] private float _tankCatchRadius = 3.5f;

    [Header("=== Gerak ===")]
    [Tooltip("Kecepatan horizontal konveyor (m/s). Kecil = pelan.")]
    [SerializeField] private float _conveyorSpeed = 1.4f;
    [Tooltip("Layer mask untuk deteksi belt (default: semua).")]
    [SerializeField] private LayerMask _beltMask = ~0;

    private readonly List<Rigidbody> _ores = new List<Rigidbody>();
    private readonly List<Vector3> _spawnPositions = new List<Vector3>();
    private readonly HashSet<Rigidbody> _fellIntoTank = new HashSet<Rigidbody>();
    private readonly Dictionary<Rigidbody, float> _stallTimer = new Dictionary<Rigidbody, float>();
    private bool _running;
    private bool _setup;

    public int OreCount => _ores.Count;
    public int OreFellIntoTankCount => _fellIntoTank.Count;
    public bool SemuaOreMasukTank => _ores.Count > 0 && _fellIntoTank.Count >= _ores.Count;

    private void Awake()
    {
        SetupOres();
    }

    /// <summary>Setup setiap child ore: tambah Rigidbody + Collider, simpan posisi spawn.</summary>
    public void SetupOres()
    {
        if (_setup) return;
        _setup = true;

        _ores.Clear();
        _spawnPositions.Clear();

        foreach (Transform child in transform)
        {
            if (child == null) continue;
            string n = child.name.ToLowerInvariant();
            if (!n.Contains("ore")) continue;

            // Collider: convex MeshCollider (atau box fallback)
            var mc = child.GetComponent<MeshCollider>();
            if (mc == null)
            {
                var mf = child.GetComponent<MeshFilter>();
                if (mf != null && mf.sharedMesh != null)
                {
                    mc = child.gameObject.AddComponent<MeshCollider>();
                    mc.convex = true;
                }
                else
                {
                    child.gameObject.AddComponent<BoxCollider>();
                }
            }
            else
            {
                mc.convex = true;
            }

            // Rigidbody
            var rb = child.GetComponent<Rigidbody>();
            if (rb == null) rb = child.gameObject.AddComponent<Rigidbody>();
            rb.mass = 8f;
            rb.useGravity = true;
            rb.isKinematic = true;        // diam dulu sampai SetRunning(true)
            rb.interpolation = RigidbodyInterpolation.Interpolate;
            rb.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;

            _ores.Add(rb);
            _spawnPositions.Add(child.position);
        }
    }

    /// <summary>Hitung Y permukaan belt pada X tertentu (interpolasi linear tail..head).</summary>
    private float BeltSurfaceY(float x)
    {
        float t = Mathf.InverseLerp(_tailX, _headX, x);
        return Mathf.Lerp(_tailY, _headY, t);
    }

    /// <summary>Arah konveyor 3D (mengikuti kemiringan belt, dari tail ke head/discharge).</summary>
    private Vector3 ConveyorDir()
    {
        Vector3 tail = new Vector3(_tailX, _tailY, _beltZ);
        Vector3 head = new Vector3(_headX, _headY, _beltZ);
        return (head - tail).normalized;
    }

    /// <summary>Mulai/berhenti konveyor. Saat mulai, ore di-reset ke posisi spawn di belt.</summary>
    public void SetRunning(bool run)
    {
        _running = run;
        if (run)
        {
            // PENTING: jadikan non-kinematic DULU sebelum set velocity (hindari warning).
            foreach (var rb in _ores)
            {
                if (rb == null) continue;
                rb.gameObject.SetActive(true);
                rb.isKinematic = false;
            }
            IgnoreUnwantedCollisions();
            ResetOreToBelt();
            _fellIntoTank.Clear();
            _stallTimer.Clear();
        }
        else
        {
            foreach (var rb in _ores)
            {
                if (rb == null) continue;
                if (!rb.isKinematic)
                {
                    rb.linearVelocity = Vector3.zero;
                    rb.angularVelocity = Vector3.zero;
                }
                rb.isKinematic = true;
            }
        }
    }

    /// <summary>
    /// Ore HANYA boleh collide dengan belt + chute. Abaikan collision dengan:
    /// - Safety floor player (Level3_SafetyFloor_Auto) yang bikin ore nyangkut
    /// - Sesama ore (hindari traffic jam / saling kunci)
    /// </summary>
    private void IgnoreUnwantedCollisions()
    {
        // Kumpulkan collider ore
        var oreColliders = new List<Collider>();
        foreach (var rb in _ores)
        {
            if (rb == null) continue;
            var c = rb.GetComponent<Collider>();
            if (c != null) oreColliders.Add(c);
        }

        // Ignore antar ore
        for (int a = 0; a < oreColliders.Count; a++)
            for (int b = a + 1; b < oreColliders.Count; b++)
                Physics.IgnoreCollision(oreColliders[a], oreColliders[b], true);

        // Ignore vs safety floor + collider lain yang mengganggu (pijakan/lantai player)
        string[] ignoreNames = { "SafetyFloor", "InvisibleFloor", "SpawnPoint" };
        foreach (var other in FindObjectsByType<Collider>(FindObjectsSortMode.None))
        {
            if (other == null) continue;
            bool match = false;
            foreach (var nm in ignoreNames)
                if (other.gameObject.name.IndexOf(nm, System.StringComparison.OrdinalIgnoreCase) >= 0) { match = true; break; }
            if (!match) continue;
            foreach (var oc in oreColliders)
                Physics.IgnoreCollision(oc, other, true);
        }
    }

    /// <summary>Sebar ore di sepanjang belt (tail -> mid), tepat di atas permukaan belt.</summary>
    public void ResetOreToBelt()
    {
        int count = _ores.Count;
        for (int i = 0; i < count; i++)
        {
            var rb = _ores[i];
            if (rb == null) continue;

            // Sebar di paruh bawah belt (tail..mid) supaya ada jarak tempuh panjang.
            float order = count <= 1 ? 0f : (float)i / (count - 1);
            float x = Mathf.Lerp(_tailX, Mathf.Lerp(_tailX, _headX, 0.45f), order);
            float surfaceY = BeltSurfaceY(x);
            float halfH = rb.transform.localScale.y * 0.3f;
            float z = _beltZ + Mathf.Lerp(-1.2f, 1.2f, Mathf.Repeat(i * 0.37f, 1f));

            rb.transform.position = new Vector3(x, surfaceY + halfH + 0.15f, z);
            rb.transform.rotation = Random.rotation;
            if (!rb.isKinematic)
            {
                rb.linearVelocity = Vector3.zero;
                rb.angularVelocity = Vector3.zero;
            }
        }
    }

    private void FixedUpdate()
    {
        if (!_running) return;

        Vector3 convDir = ConveyorDir();
        Vector3 horiz = new Vector3(convDir.x, 0f, convDir.z).normalized;

        for (int i = 0; i < _ores.Count; i++)
        {
            var rb = _ores[i];
            if (rb == null) continue;
            if (_fellIntoTank.Contains(rb)) continue;

            Vector3 pos = rb.position;

            // Masuk tank? Terdeteksi via: (a) jatuh di bawah ambang Y, ATAU
            // (b) sudah dekat pusat tank secara horizontal (XZ) — supaya tidak menumpuk
            // di bibir tank kalau ada collider yang menahan.
            float distXZ = new Vector2(pos.x - _tankCenterX, pos.z - _tankCenterZ).magnitude;
            if (pos.y <= _tankCatchY || distXZ <= _tankCatchRadius)
            {
                _fellIntoTank.Add(rb);
                rb.transform.gameObject.SetActive(false); // slurry menggantikan ore di tank
                continue;
            }

            // Sudah lewat head belt (di chute / jatuh ke tank)?
            // Dorong PELAN ke arah pusat tank supaya ore meluncur turun dari chute masuk tank,
            // tetapi tidak overshoot. Gravitasi tetap aktif menjatuhkan ore ke dalam tank.
            if (pos.x <= _headX)
            {
                Vector3 toTank = new Vector3(_tankCenterX - pos.x, 0f, _tankCenterZ - pos.z);
                if (toTank.sqrMagnitude > 0.01f) toTank.Normalize();
                Vector3 v = rb.linearVelocity;
                float chuteSpeed = _conveyorSpeed * 0.45f; // pelan, biar tidak melayang
                v.x = toTank.x * chuteSpeed;
                v.z = toTank.z * chuteSpeed;
                rb.linearVelocity = v;
                continue;
            }

            // Masih di belt: dorong naik mengikuti kemiringan belt.
            Vector3 vel = rb.linearVelocity;
            vel.x = horiz.x * _conveyorSpeed;
            vel.z = horiz.z * _conveyorSpeed;
            // Bantu naik incline: jaga komponen Y minimum mengikuti slope saat bergerak maju.
            float slope = (_headY - _tailY) / Mathf.Max(0.01f, Mathf.Abs(_headX - _tailX));
            float climbY = _conveyorSpeed * slope;
            if (vel.y < climbY) vel.y = Mathf.Max(vel.y, climbY * 0.5f);
            rb.linearVelocity = vel;

            // ANTI-STALL: kalau ore di belt macet (kecepatan horizontal sangat kecil)
            // padahal seharusnya bergerak, beri dorongan ekstra + angkat sedikit
            // supaya lolos dari obstacle (penyangga belt / saling terkunci).
            float horizSpeed = new Vector2(rb.linearVelocity.x, rb.linearVelocity.z).magnitude;
            float prev; _stallTimer.TryGetValue(rb, out prev);
            if (horizSpeed < _conveyorSpeed * 0.25f)
            {
                prev += Time.fixedDeltaTime;
                _stallTimer[rb] = prev;
                if (prev > 0.8f)
                {
                    // Nudge: angkat sedikit + dorong maju sepanjang belt.
                    rb.position += new Vector3(horiz.x, 0.6f, horiz.z) * 0.5f;
                    rb.linearVelocity = new Vector3(horiz.x * _conveyorSpeed * 1.5f, 1.0f, horiz.z * _conveyorSpeed * 1.5f);
                    _stallTimer[rb] = 0f;
                }
            }
            else
            {
                _stallTimer[rb] = 0f;
            }
        }
    }
}
