using UnityEngine;
using System;
using System.Collections;
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
    public static event Action<string> OnApdItemRemoved;
    public static event Action         OnAPD7Lengkap;
    public static event Action         OnAPDTidakLengkap;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStaticState()
    {
        Instance = null;
        ClearStaticEvents();
    }

    private static void ClearStaticEvents()
    {
        OnApdItemWorn = null;
        OnApdItemRemoved = null;
        OnAPD7Lengkap = null;
        OnAPDTidakLengkap = null;
    }

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

    [Header("=== Auto Socket Respirator ===")]
    [Tooltip("Kalau true, setiap start level respirator dipasang di socket dada kanan, bukan di mulut/field.")]
    [SerializeField] private bool _otomatisSimpanRespiratorSaatLevel2 = true;
    [SerializeField] private Transform _respiratorObject;
    [SerializeField] private Transform _socketRespiratorBaju;
    [SerializeField] private Vector3 _posisiLokalRespiratorDiBaju = Vector3.zero;
    [SerializeField] private Vector3 _rotasiLokalRespiratorDiBaju = Vector3.zero;
    [SerializeField] private Vector3 _skalaDuniaRespiratorDiBaju = new Vector3(0.2f, 0.095f, 0.12f);
    [SerializeField] private bool _paksaPosisiMaskerDadaKananRuntime = true;

    [Header("=== Masker Auto-Transparent ===")]
    [Tooltip("Setelah masker dipakai, fade material ke transparent supaya tidak menutupi pandangan.")]
    [SerializeField] private bool _maskerAutoTransparentSaatDipakai = true;
    [Tooltip("Jeda detik setelah masker dipakai sebelum mulai fade.")]
    [SerializeField] private float _delayMaskerFade = 2f;
    [Tooltip("Durasi fade dari opaque ke alpha target.")]
    [SerializeField] private float _durasiMaskerFade = 1.0f;
    [Tooltip("Alpha akhir masker setelah fade. 0 = invisible, 1 = solid.")]
    [Range(0.05f, 1f)] [SerializeField] private float _alphaMaskerSetelahFade = 0.30f;

    private bool _levelStartedSubscribed;
    private float _pastikanMaskerDadaSampai;
    private float _nextMaskerDadaEnsure;
    private float _pastikanWalkieDadaSampai;
    private float _nextWalkieDadaEnsure;
    private float _nextRayRecovery;

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

    public bool Level3FieldApdLengkap =>
        isWalkieTalkieTaken &&
        isGlassesWorn &&
        isRespiratorWorn;

    public bool Level5FieldApdLengkap =>
        isGlovesWorn &&
        isWalkieTalkieTaken &&
        isGlassesWorn &&
        isRespiratorWorn;

    [Header("=== Auto Socket Sarung Tangan Pinggang ===")]
    [Tooltip("Kalau true, sarung tangan otomatis dipindah ke socket pinggang sebelum masuk field.")]
    [SerializeField] private bool _otomatisSimpanGloveSaatLevelField = true;
    [SerializeField] private Transform _glovesObject;
    [SerializeField] private Transform _socketGlovesPinggang;
    [SerializeField] private Vector3 _posisiLokalGloveDiPinggang = new Vector3(0f, -0.02f, 0.05f);
    [SerializeField] private Vector3 _rotasiLokalGloveDiPinggang = new Vector3(-15f, 0f, 0f);
    [SerializeField] private Vector3 _skalaDuniaGloveDiPinggang = new Vector3(0.18f, 0.08f, 0.12f);
    private float _pastikanGlovePinggangSampai;
    private float _nextGlovePinggangEnsure;

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
        // PhaseManager is scene-local and shares Game_Manager with other critical
        // components. Never destroy the whole GameObject because of a stale static
        // reference left by Enter Play Mode Options or a scene transition.
        Instance = this;
    }

    private void OnEnable()
    {
        SubscribeLevelStarted();
        if (GameLevelManager.Instance != null)
            OnLevelStarted(GameLevelManager.Instance.CurrentLevel);
    }

    private void Start()
    {
        SubscribeLevelStarted();
        if (GameLevelManager.Instance != null)
            OnLevelStarted(GameLevelManager.Instance.CurrentLevel);
        Log("APD SYSTEM", $"Siap! Pakai {TOTAL_APD} APD sebelum keluar loker.", "yellow");
    }

    private void Update()
    {
        if (Time.unscaledTime >= _nextRayRecovery)
        {
            _nextRayRecovery = Time.unscaledTime + 0.1f;
            XRInteractorRecovery.PulihkanRayInteractor();
        }

        bool diLevel1 = GameLevelManager.Instance != null &&
                        GameLevelManager.Instance.CurrentLevel == GameLevelManager.GameLevel.Level1_APD;
        bool levelSetelahApd = GameLevelManager.Instance != null &&
                               GameLevelManager.Instance.CurrentLevel > GameLevelManager.GameLevel.Level1_APD;

        if (levelSetelahApd && isWalkieTalkieTaken && !WalkieSedangDipegangPlayer() &&
            (WalkieBelumDiSocketDada() || Time.unscaledTime <= _pastikanWalkieDadaSampai) &&
            Time.unscaledTime >= _nextWalkieDadaEnsure)
        {
            _nextWalkieDadaEnsure = Time.unscaledTime + 0.25f;
            PastikanWalkieTalkieAdaDiSocketDada();
        }

        if (!_otomatisSimpanRespiratorSaatLevel2) return;

        // Khusus Level 1: kalau masker belum dipakai, biarkan di rak APD (Socket_Scanner_RespiratorMask),
        // jangan paksa pindah ke dada.
        if (diLevel1 && !isRespiratorWorn) return;

        bool maskerBelumDiDada = MaskerBelumDiSocketDada();
        bool jagaMaskerDadaTerus = levelSetelahApd && maskerBelumDiDada;
        if (!jagaMaskerDadaTerus && Time.unscaledTime > _pastikanMaskerDadaSampai) return;
        if (Time.unscaledTime < _nextMaskerDadaEnsure) return;

        _nextMaskerDadaEnsure = Time.unscaledTime + 0.25f;
        PastikanMaskerAdaDiSocketBaju(false);
        OnRespiratorStored();
    }

    private bool MaskerBelumDiSocketDada()
    {
        if (_respiratorObject == null)
        {
            GameObject respirator = GameObject.Find("RespiratorMask");
            if (respirator != null)
                _respiratorObject = respirator.transform;
        }

        if (_socketRespiratorBaju == null)
        {
            GameObject socketBaju = GameObject.Find("Socket_Respirator_Baju");
            if (socketBaju != null)
                _socketRespiratorBaju = socketBaju.transform;
        }

        if (_respiratorObject == null || _socketRespiratorBaju == null)
            return false;

        if (isRespiratorWorn && _respiratorObject.parent != null && _respiratorObject.parent.name == "Socket_RespiratorMask")
            return false;

        if (_respiratorObject.parent != _socketRespiratorBaju)
            return true;

        if (!_respiratorObject.gameObject.activeInHierarchy)
            return true;

        foreach (Renderer r in _respiratorObject.GetComponentsInChildren<Renderer>(true))
            if (r != null && !r.enabled) return true;

        return false;
    }

    private void OnDisable()
    {
        UnsubscribeLevelStarted();
    }

    private void OnDestroy()
    {
        UnsubscribeLevelStarted();
        if (Instance == this)
        {
            Instance = null;
            ClearStaticEvents();
        }
    }

    private void SubscribeLevelStarted()
    {
        if (_levelStartedSubscribed) return;
        GameLevelManager.OnLevelStarted += OnLevelStarted;
        _levelStartedSubscribed = true;
    }

    private void UnsubscribeLevelStarted()
    {
        if (!_levelStartedSubscribed) return;
        GameLevelManager.OnLevelStarted -= OnLevelStarted;
        _levelStartedSubscribed = false;
    }

    public void OnHelmetWorn()        { PakaiApd(_helm); MulaiFadeApdSetelahDipakai("Helmet"); }
    public void OnVestWorn()          { PakaiApd(_rompi);         }
    public void OnGlassesWorn()       { PakaiApd(_kacamata); MulaiFadeApdSetelahDipakai("Glassess"); }
    public void OnBootsWorn()         { PakaiApd(_sepatuBots);    }
    public void OnGlovesWorn()        { PakaiApd(_sarungTangan);  }
    public void OnRespiratiorWorn()   { PakaiApd(_respirator); MulaiFadeApdSetelahDipakai("RespiratorMask"); }
    public void OnRespiratorRemoved() { LepasApd(_respirator);    }
    public void OnRespiratorStored()  { LepasApd(_respirator);    }
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

    public string CaraApdLevel3FieldYangKurang()
    {
        var kurang = new List<string>();
        if (!isWalkieTalkieTaken) kurang.Add(_walkieTalkie.namaApd);
        if (!isGlassesWorn) kurang.Add(_kacamata.namaApd);
        if (!isRespiratorWorn) kurang.Add(_respirator.namaApd);
        return kurang.Count > 0 ? string.Join(", ", kurang) : "-";
    }

    private void PakaiApd(ApdItem apd)
    {
        // Walkie Talkie = alat yang dipegang & dipakai berulang. JANGAN dikunci/disembunyikan supaya tetap bisa di-grab manual.
        bool isWalkie = apd == _walkieTalkie;
        if (apd.sudahDipakai)
        {
            if (!isWalkie) SembunyikanApdDiMeja(apd.namaApd);
            return;
        }
        apd.sudahDipakai = true;
        OnApdItemWorn?.Invoke(apd.namaApd);
        Log("APD", $"\u2713 <b>{apd.namaApd}</b> terpasang! ({JumlahAPDTerpasang}/{TOTAL_APD})", "green");

        if (!isWalkie)
        {
            // Disable grab pada object APD ini supaya tidak bisa diambil ulang.
            KuncIGrabAPD(apd.namaApd, true);
            SembunyikanApdDiMeja(apd.namaApd);
        }

        if (APDLengkapSempurna)
        {
            bool diLevel1 = GameLevelManager.Instance != null &&
                            GameLevelManager.Instance.CurrentLevel == GameLevelManager.GameLevel.Level1_APD;
            if (diLevel1 && !_apd7LengkapSudahPernahTrigger)
            {
                _apd7LengkapSudahPernahTrigger = true;
                Log("APD LENGKAP", $"Semua {TOTAL_APD} APD terpasang sempurna!", "green");
                OnAPD7Lengkap?.Invoke();
            }
        }
    }

    private bool _apd7LengkapSudahPernahTrigger = false;

    private void SembunyikanApdDiMeja(string namaApd)
    {
        string[] objectNames = MapApdKeObjectNames(namaApd);
        if (objectNames == null || objectNames.Length == 0)
            return;

        foreach (Transform t in UnityEngine.Object.FindObjectsByType<Transform>(FindObjectsInactive.Include, FindObjectsSortMode.None))
        {
            if (t == null || !t.gameObject.scene.IsValid() || !IsUnderScannerSocket(t))
                continue;

            bool match = false;
            foreach (string objectName in objectNames)
            {
                if (t.name == objectName)
                {
                    match = true;
                    break;
                }
            }

            if (!match)
                continue;

            foreach (Renderer renderer in t.GetComponentsInChildren<Renderer>(true))
                if (renderer != null) renderer.enabled = false;
            foreach (Collider collider in t.GetComponentsInChildren<Collider>(true))
                if (collider != null) collider.enabled = false;

            var grab = t.GetComponent<UnityEngine.XR.Interaction.Toolkit.Interactables.XRGrabInteractable>();
            if (grab != null) grab.enabled = false;

            var stabilizer = t.GetComponent<ApdDisplayItemStabilizer>();
            if (stabilizer != null) stabilizer.enabled = false;
        }
    }

    private bool IsUnderScannerSocket(Transform t)
    {
        while (t != null)
        {
            if (t.name.StartsWith("Socket_Scanner_"))
                return true;
            t = t.parent;
        }
        return false;
    }

    private string[] MapApdKeObjectNames(string namaApd)
    {
        if (string.IsNullOrEmpty(namaApd)) return null;
        string n = namaApd.ToLowerInvariant();
        if (n.Contains("helm")) return new[] { "Helmet" };
        if (n.Contains("rompi") || n.Contains("vest")) return new[] { "Vest" };
        if (n.Contains("kacamata") || n.Contains("glass")) return new[] { "Glassess" };
        if (n.Contains("sepatu") || n.Contains("boot")) return new[] { "Boots" };
        if (n.Contains("sarung tangan") || n.Contains("glove")) return new[] { "Gloves" };
        if (n.Contains("respirator") || n.Contains("masker")) return new[] { "RespiratorMask" };
        if (n.Contains("earplug") || n.Contains("ear protection")) return new[] { "EarPlug" };
        if (n.Contains("walkie") || n.Contains("ht")) return new[] { "Walkie Talkie" };
        return null;
    }

    /// <summary>
    /// Lock atau unlock XRGrabInteractable pada semua child di socket APD bersangkutan.
    /// Saat true: APD tidak bisa di-grab (sudah dipakai).
    /// </summary>
    private void KuncIGrabAPD(string namaApd, bool kunci)
    {
        if (!string.IsNullOrEmpty(namaApd))
        {
            string lowered = namaApd.ToLowerInvariant();
            if (lowered.Contains("walkie") || lowered.Contains("ht") ||
                lowered.Contains("respirator") || lowered.Contains("masker"))
                return;
        }

        string[] socketNames = MapApdKeSockets(namaApd);
        var handled = new List<UnityEngine.XR.Interaction.Toolkit.Interactables.XRGrabInteractable>();

        var sockets = UnityEngine.Object.FindObjectsByType<Transform>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        foreach (var t in sockets)
        {
            bool socketMatch = false;
            if (socketNames != null)
            {
                foreach (string socketName in socketNames)
                {
                    if (t.name == socketName)
                    {
                        socketMatch = true;
                        break;
                    }
                }
            }

            if (!socketMatch) continue;
            foreach (var grab in t.GetComponentsInChildren<UnityEngine.XR.Interaction.Toolkit.Interactables.XRGrabInteractable>(true))
            {
                if (grab == null) continue;
                SetGrabLock(grab, kunci, handled);
            }
        }

        foreach (TaskTrigger trigger in UnityEngine.Object.FindObjectsByType<TaskTrigger>(FindObjectsInactive.Include, FindObjectsSortMode.None))
        {
            if (trigger == null || !TaskMatchesApdName(namaApd, trigger.tipeTugas))
                continue;

            foreach (var grab in trigger.GetComponentsInChildren<UnityEngine.XR.Interaction.Toolkit.Interactables.XRGrabInteractable>(true))
                SetGrabLock(grab, kunci, handled);

            var ownGrab = trigger.GetComponent<UnityEngine.XR.Interaction.Toolkit.Interactables.XRGrabInteractable>();
            SetGrabLock(ownGrab, kunci, handled);
        }
    }

    private void SetGrabLock(UnityEngine.XR.Interaction.Toolkit.Interactables.XRGrabInteractable grab, bool kunci, List<UnityEngine.XR.Interaction.Toolkit.Interactables.XRGrabInteractable> handled)
    {
        if (grab == null || handled.Contains(grab))
            return;

        handled.Add(grab);
        grab.enabled = !kunci;
        foreach (var col in grab.GetComponentsInChildren<Collider>(true))
            col.enabled = !kunci;
    }

    private bool TaskMatchesApdName(string namaApd, TaskTrigger.TaskType type)
    {
        if (string.IsNullOrEmpty(namaApd))
            return false;

        string n = namaApd.ToLowerInvariant();
        switch (type)
        {
            case TaskTrigger.TaskType.Helm: return n.Contains("helm");
            case TaskTrigger.TaskType.Rompi: return n.Contains("rompi") || n.Contains("vest");
            case TaskTrigger.TaskType.Kacamata: return n.Contains("kacamata") || n.Contains("glass");
            case TaskTrigger.TaskType.Sepatu: return n.Contains("sepatu") || n.Contains("boot");
            case TaskTrigger.TaskType.SarungTangan: return n.Contains("sarung tangan") || n.Contains("glove");
            case TaskTrigger.TaskType.EarProtection: return n.Contains("earplug") || n.Contains("ear protection");
            default: return false;
        }
    }

    private string[] MapApdKeSockets(string namaApd)
    {
        if (string.IsNullOrEmpty(namaApd)) return null;
        string n = namaApd.ToLowerInvariant();
        if (n.Contains("helm")) return new[] { "Socket_Helmet", "Socket_Scanner_Hat" };
        if (n.Contains("rompi") || n.Contains("vest")) return new[] { "Socket_Rompi", "Socket_Scanner_Rompi" };
        if (n.Contains("kacamata") || n.Contains("glass")) return new[] { "Socket_Glasess", "Socket_Scanner_Glassess" };
        if (n.Contains("sepatu") || n.Contains("boot")) return new[] { "Socket_Boots", "Socket_Scanner_Boots" };
        if (n.Contains("sarung tangan") || n.Contains("glove")) return new[] { "Socket_Gloves", "Socket_Scanner_Gloves" };
        if (n.Contains("respirator") || n.Contains("masker")) return new[] { "Socket_RespiratorMask", "Socket_Scanner_RespiratorMask" };
        if (n.Contains("earplug") || n.Contains("ear protection")) return new[] { "Socket_EarPlug", "Socket_Scanner_EarPlug" };
        if (n.Contains("walkie") || n.Contains("ht")) return new[] { "Socket_WalkieTalkie", "Socket_Scanner_WalkieTalkie" };
        return null;
    }

    private void LepasApd(ApdItem apd)
    {
        if (!apd.sudahDipakai) return;
        apd.sudahDipakai = false;
        OnApdItemRemoved?.Invoke(apd.namaApd);
        Log("APD", $"<b>{apd.namaApd}</b> dilepas / disimpan.", "orange");
    }

    public void ResetKeAreaDcsSaja()
    {
        bool walkieTetapDibawa = _walkieTalkie.sudahDipakai;
        foreach (var apd in SemuaAPD())
            apd.sudahDipakai = false;

        _walkieTalkie.sudahDipakai = walkieTetapDibawa;
        Log("RESET AREA", "APD lapangan dilepas untuk area DCS. Walkie Talkie tetap dibawa.", "cyan");
    }

    private void OnLevelStarted(GameLevelManager.GameLevel level)
    {
        if (level == GameLevelManager.GameLevel.Level1_APD)
        {
            // Di Level 1 masker WAJIB tetap di rak APD (Socket_Scanner_RespiratorMask) sampai player ambil & pakai.
            if (!isRespiratorWorn) PindahkanRespiratorKeRakApd();
            return;
        }

        if (_otomatisSimpanRespiratorSaatLevel2)
        {
            _pastikanMaskerDadaSampai = Time.unscaledTime + 4f;
            _nextMaskerDadaEnsure = 0f;
            PindahkanRespiratorKeSocketBaju();
            StartCoroutine(PastikanMaskerDadaBeberapaFrame());
        }

        // HT: tandai sudah diambil supaya WalkieTalkieWearableSocket auto-dock ke dada (tetap bisa di-grab manual).
        OnWalkieTalkieTaken();
        _pastikanWalkieDadaSampai = Time.unscaledTime + 4f;
        _nextWalkieDadaEnsure = 0f;
        PastikanWalkieTalkieAdaDiSocketDada();
        StartCoroutine(PastikanWalkieTalkieDadaBeberapaFrame());
        // Kacamata: otomatis terpasang di wajah & ikut player tiap naik level (Level 2+).
        PindahkanKacamataKeWajah();

        // Auto-move gloves to waist socket before Level 5+ field levels
        if (_otomatisSimpanGloveSaatLevelField && !isGlovesWorn
            && level >= GameLevelManager.GameLevel.Level5_SteamValve)
        {
            _pastikanGlovePinggangSampai = Time.unscaledTime + 4f;
            _nextGlovePinggangEnsure = 0f;
            PindahkanGloveKeSocketPinggang();
            StartCoroutine(PastikanGlovePinggangBeberapaFrame());
        }
    }

    private void PindahkanGloveKeSocketPinggang()
    {
        if (_glovesObject == null)
        {
            GameObject gloves = GameObject.Find("Gloves");
            if (gloves == null)
            {
                foreach (var go in Resources.FindObjectsOfTypeAll<GameObject>())
                    if (go.name == "Gloves" && go.scene.IsValid()) { gloves = go; break; }
            }
            if (gloves != null) _glovesObject = gloves.transform;
        }

        if (_socketGlovesPinggang == null)
        {
            GameObject socketWaist = GameObject.Find("Socket_Gloves_Waist");
            if (socketWaist != null) _socketGlovesPinggang = socketWaist.transform;
        }

        if (_glovesObject == null) return;

        if (_socketGlovesPinggang == null)
        {
            var waistAnchor = FindFirstObjectByType<TorsoWaistAnchor>(FindObjectsInactive.Include);
            if (waistAnchor != null)
            {
                var waistGo = new GameObject("Socket_Gloves_Waist");
                waistGo.transform.SetParent(waistAnchor.transform, false);
                _socketGlovesPinggang = waistGo.transform;
            }
            else
            {
                var xrOrigin = FindFirstObjectByType<Unity.XR.CoreUtils.XROrigin>(FindObjectsInactive.Include);
                if (xrOrigin != null)
                {
                    var waistGo = new GameObject("Socket_Gloves_Waist");
                    waistGo.transform.SetParent(xrOrigin.transform, false);
                    var anchor = waistGo.AddComponent<TorsoWaistAnchor>();
                    _socketGlovesPinggang = waistGo.transform;
                }
            }
        }

        if (_socketGlovesPinggang == null) return;

        var grab = _glovesObject.GetComponent<UnityEngine.XR.Interaction.Toolkit.Interactables.XRGrabInteractable>();
        if (grab != null && grab.interactionManager != null && grab.isSelected)
        {
            foreach (var interactor in new System.Collections.Generic.List<UnityEngine.XR.Interaction.Toolkit.Interactors.IXRSelectInteractor>(grab.interactorsSelecting))
                grab.interactionManager.SelectExit(interactor, grab);
        }

        _glovesObject.SetParent(_socketGlovesPinggang, false);
        _glovesObject.localPosition = _posisiLokalGloveDiPinggang;
        _glovesObject.localRotation = Quaternion.Euler(_rotasiLokalGloveDiPinggang);

        if (_skalaDuniaGloveDiPinggang.sqrMagnitude > 0.0001f)
        {
            Vector3 parentLossy = _socketGlovesPinggang.lossyScale;
            if (parentLossy.sqrMagnitude > 0.0001f)
                _glovesObject.localScale = new Vector3(
                    _skalaDuniaGloveDiPinggang.x / parentLossy.x,
                    _skalaDuniaGloveDiPinggang.y / parentLossy.y,
                    _skalaDuniaGloveDiPinggang.z / parentLossy.z);
        }

        _glovesObject.gameObject.SetActive(true);
        if (grab != null) grab.enabled = true;

        if (!isGlovesWorn)
            Log("GLOVES WAIST", "Gloves moved to waist socket. Grab before entering Pre-Heater area.", "cyan");
    }

    private IEnumerator PastikanWalkieTalkieDadaBeberapaFrame()
    {
        yield return null;
        PastikanWalkieTalkieAdaDiSocketDada();
        yield return new WaitForSecondsRealtime(0.25f);
        PastikanWalkieTalkieAdaDiSocketDada();
        yield return new WaitForSecondsRealtime(0.75f);
        PastikanWalkieTalkieAdaDiSocketDada();
    }

    private bool WalkieBelumDiSocketDada()
    {
        Transform walkie = CariTransformScene("Walkie Talkie");
        Transform socketTransform = CariTransformScene("Socket_WalkieTalkie");
        if (walkie == null || socketTransform == null) return true;
        if (!walkie.gameObject.activeInHierarchy) return true;
        if (walkie.parent != socketTransform && !walkie.IsChildOf(socketTransform)) return true;

        Renderer[] renderers = walkie.GetComponentsInChildren<Renderer>(true);
        if (renderers == null || renderers.Length == 0) return true;
        foreach (Renderer r in renderers)
        {
            if (r == null) continue;
            if (!r.enabled || !r.gameObject.activeInHierarchy) return true;
        }
        return false;
    }

    // True jika HT sedang dipegang player (interactor tangan/ray, bukan socket dada).
    // Dipakai untuk MENCEGAH dock-paksa berkala saat player sedang membawa HT.
    private bool WalkieSedangDipegangPlayer()
    {
        Transform walkie = CariTransformScene("Walkie Talkie");
        if (walkie == null) return false;
        var grab = walkie.GetComponent<UnityEngine.XR.Interaction.Toolkit.Interactables.XRGrabInteractable>();
        if (grab == null || !grab.isSelected) return false;
        foreach (var itr in grab.interactorsSelecting)
            if (!(itr is UnityEngine.XR.Interaction.Toolkit.Interactors.XRSocketInteractor))
                return true;
        return false;
    }

    public void PastikanWalkieTalkieAdaDiSocketDada()
    {
        OnWalkieTalkieTaken();

        WalkieTalkieWearableSocket socket = UnityEngine.Object.FindFirstObjectByType<WalkieTalkieWearableSocket>(FindObjectsInactive.Include);
        if (socket != null)
        {
            socket.DockNow();
            Log("HT", "Walkie Talkie dipastikan dock di Socket_WalkieTalkie dada.", "cyan");
            return;
        }

        Transform walkie = CariTransformScene("Walkie Talkie");
        Transform socketTransform = CariTransformScene("Socket_WalkieTalkie");
        if (walkie == null || socketTransform == null)
        {
            Log("HT", "Tidak bisa dock HT: object Walkie Talkie atau Socket_WalkieTalkie belum ketemu.", "orange");
            return;
        }

        var grab = walkie.GetComponent<UnityEngine.XR.Interaction.Toolkit.Interactables.XRGrabInteractable>();
        if (grab != null && grab.isSelected && grab.interactionManager != null)
        {
            var selecting = new List<UnityEngine.XR.Interaction.Toolkit.Interactors.IXRSelectInteractor>(grab.interactorsSelecting);
            foreach (var interactor in selecting)
                grab.interactionManager.SelectExit(interactor, grab);
        }

        Rigidbody rb = walkie.GetComponent<Rigidbody>();
        if (rb != null)
        {
            if (!rb.isKinematic)
            {
                rb.linearVelocity = Vector3.zero;
                rb.angularVelocity = Vector3.zero;
            }
            rb.isKinematic = true;
            rb.useGravity = false;
        }

        walkie.gameObject.SetActive(true);
        walkie.SetParent(socketTransform, false);
        walkie.localPosition = Vector3.zero;
        walkie.localRotation = Quaternion.Euler(8f, -18f, -6f);
        walkie.localScale = Vector3.one * 0.163f;

        foreach (Renderer r in walkie.GetComponentsInChildren<Renderer>(true))
        {
            if (r == null) continue;
            PaksaRendererApdSelaluTerlihat(r);
        }
        foreach (Collider c in walkie.GetComponentsInChildren<Collider>(true))
            if (c != null) c.enabled = true;
        if (grab != null) grab.enabled = true;
    }

    private IEnumerator PastikanGlovePinggangBeberapaFrame()
    {
        yield return null;
        PindahkanGloveKeSocketPinggang();
        yield return new WaitForSeconds(0.15f);
        PindahkanGloveKeSocketPinggang();
    }

    private void PindahkanRespiratorKeRakApd()
    {
        if (_respiratorObject == null)
        {
            GameObject respirator = GameObject.Find("RespiratorMask");
            if (respirator == null)
            {
                foreach (var go in Resources.FindObjectsOfTypeAll<GameObject>())
                    if (go.name == "RespiratorMask" && go.scene.IsValid()) { respirator = go; break; }
            }
            if (respirator != null) _respiratorObject = respirator.transform;
        }
        if (_respiratorObject == null) return;

        GameObject scannerSocketGo = GameObject.Find("Socket_Scanner_RespiratorMask");
        if (scannerSocketGo == null)
        {
            foreach (var go in Resources.FindObjectsOfTypeAll<GameObject>())
                if (go.name == "Socket_Scanner_RespiratorMask" && go.scene.IsValid()) { scannerSocketGo = go; break; }
        }
        if (scannerSocketGo == null) return;

        Transform scannerSocket = scannerSocketGo.transform;
        if (_respiratorObject.parent == scannerSocket) return;

        // Lepaskan dari interactor manapun supaya boleh dipindah parent.
        var grab = _respiratorObject.GetComponent<UnityEngine.XR.Interaction.Toolkit.Interactables.XRGrabInteractable>();
        if (grab != null && grab.interactionManager != null && grab.isSelected)
        {
            foreach (var interactor in new System.Collections.Generic.List<UnityEngine.XR.Interaction.Toolkit.Interactors.IXRSelectInteractor>(grab.interactorsSelecting))
                grab.interactionManager.SelectExit(interactor, grab);
        }

        // Ukuran target di rak: world scale proporsional dengan masker respirator.
        // Parent scanner socket lossyScale = 0.34, localScale target = (0.55, 0.38, 0.5).
        Vector3 targetLocalScale = new Vector3(0.55f, 0.38f, 0.5f);

        _respiratorObject.SetParent(scannerSocket, false);
        _respiratorObject.localPosition = new Vector3(0f, -0.05f, 0.1f);
        _respiratorObject.localRotation = Quaternion.identity;
        _respiratorObject.localScale = targetLocalScale;

        _respiratorObject.gameObject.SetActive(true);
        foreach (Renderer r in _respiratorObject.GetComponentsInChildren<Renderer>(true))
        {
            if (r == null) continue;
            PaksaRendererApdSelaluTerlihat(r);
            SetRendererAlpha(r, 1f);
            BuatMaterialSolid(r);
        }
        foreach (Collider c in _respiratorObject.GetComponentsInChildren<Collider>(true))
            if (c != null) c.enabled = true;
        if (grab != null) grab.enabled = true;

        // Disable stabilizer supaya tidak fight posisi
        var stabilizer = _respiratorObject.GetComponent<ApdDisplayItemStabilizer>();
        if (stabilizer != null) stabilizer.enabled = true;

        Log("RESPIRATOR LV1", "Masker dipindahkan ke rak APD (Socket_Scanner_RespiratorMask) untuk Level 1.", "yellow");
    }

    private IEnumerator PastikanMaskerDadaBeberapaFrame()
    {
        yield return null;
        PindahkanRespiratorKeSocketBaju();
        yield return new WaitForSeconds(0.15f);
        PindahkanRespiratorKeSocketBaju();
    }

    private void PindahkanRespiratorKeSocketBaju()
    {
        PastikanMaskerAdaDiSocketBaju();
        OnRespiratorStored();
        Log("RESPIRATOR AUTO", "Respirator dipindahkan ke socket dada kanan agar terlihat bersama socket Walkie Talkie.", "cyan");
    }

    private void PindahkanKacamataKeWajah()
    {
        Transform glasses = CariTransformScene("Glassess");
        Transform socket = CariTransformScene("Socket_Glasess");
        if (glasses == null || socket == null) return;

        var grab = glasses.GetComponent<UnityEngine.XR.Interaction.Toolkit.Interactables.XRGrabInteractable>();
        if (grab != null && grab.isSelected && grab.interactionManager != null)
            foreach (var it in new System.Collections.Generic.List<UnityEngine.XR.Interaction.Toolkit.Interactors.IXRSelectInteractor>(grab.interactorsSelecting))
                grab.interactionManager.SelectExit(it, grab);

        var stab = glasses.GetComponent<ApdDisplayItemStabilizer>();
        if (stab != null) stab.enabled = false;

        Vector3 ws = glasses.lossyScale;
        glasses.SetParent(socket, false);
        glasses.localPosition = Vector3.zero;
        glasses.localRotation = Quaternion.identity;
        SetWorldScale(glasses, ws);
        glasses.gameObject.SetActive(true);
        foreach (Renderer r in glasses.GetComponentsInChildren<Renderer>(true)) if (r != null) r.enabled = true;
        if (grab != null) grab.enabled = false;
        foreach (Collider c in glasses.GetComponentsInChildren<Collider>(true)) if (c != null) c.enabled = false;

        if (!isGlassesWorn) OnGlassesWorn();
        else MulaiFadeApdSetelahDipakai("Glassess");
    }

    private Transform CariTransformScene(string nama)
    {
        var go = GameObject.Find(nama);
        if (go == null)
            foreach (var g in Resources.FindObjectsOfTypeAll<GameObject>())
                if (g.name == nama && g.scene.IsValid()) { go = g; break; }
        return go != null ? go.transform : null;
    }

    private void SetWorldScale(Transform target, Vector3 worldScale)
    {
        if (target == null || worldScale == Vector3.zero)
            return;

        Vector3 parentScale = target.parent != null ? target.parent.lossyScale : Vector3.one;
        target.localScale = new Vector3(
            parentScale.x == 0f ? worldScale.x : worldScale.x / parentScale.x,
            parentScale.y == 0f ? worldScale.y : worldScale.y / parentScale.y,
            parentScale.z == 0f ? worldScale.z : worldScale.z / parentScale.z
        );
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


    /// <summary>
    /// Lepaskan masker dari socket baju (atau parent manapun) dan tempatkan di posisi dunia tertentu
    /// agar player bisa mengambilnya kembali dengan tangannya. Status APD respirator otomatis di-reset.
    /// </summary>
    /// <returns>True jika masker berhasil dilepas dan ditempatkan; false jika referensi masker belum ada.</returns>
    /// <summary>
    /// Pastikan masker secara fisik berada di socket baju (chest), tidak di tangan / world.
    /// Dipakai saat masuk Level 3 field — supaya socket baju siap di-grab oleh player.
    /// </summary>
    /// <summary>
    /// Pastikan masker secara fisik berada di socket baju (chest), tidak di tangan / face socket / world.
    /// Force-deselect dari interactor manapun terlebih dahulu, lalu reparent + reset transform.
    /// </summary>
    public void PastikanMaskerAdaDiSocketBaju(bool paksaLepasDariInteractor = true)
    {
        SyncTorsoChestAnchorNow();

        if (_respiratorObject == null)
        {
            GameObject respirator = GameObject.Find("RespiratorMask");
            if (respirator == null)
            {
                foreach (var go in Resources.FindObjectsOfTypeAll<GameObject>())
                    if (go.name == "RespiratorMask" && go.scene.IsValid()) { respirator = go; break; }
            }
            if (respirator != null)
                _respiratorObject = respirator.transform;
        }

        if (_socketRespiratorBaju == null)
        {
            GameObject socketBaju = GameObject.Find("Socket_Respirator_Baju");
            if (socketBaju == null)
            {
                foreach (var go in Resources.FindObjectsOfTypeAll<GameObject>())
                    if (go.name == "Socket_Respirator_Baju" && go.scene.IsValid()) { socketBaju = go; break; }
            }
            if (socketBaju != null)
                _socketRespiratorBaju = socketBaju.transform;
        }

        if (_respiratorObject == null || _socketRespiratorBaju == null)
        {
            Log("RESPIRATOR", "Tidak bisa memastikan masker di socket baju: referensi belum lengkap.", "orange");
            return;
        }

        var grab = _respiratorObject.GetComponent<UnityEngine.XR.Interaction.Toolkit.Interactables.XRGrabInteractable>();
        if (grab != null && grab.isSelected)
        {
            if (!paksaLepasDariInteractor)
                return;

            if (grab.interactionManager == null)
                return;

            var selectingList = new System.Collections.Generic.List<UnityEngine.XR.Interaction.Toolkit.Interactors.IXRSelectInteractor>(grab.interactorsSelecting);
            foreach (var interactor in selectingList)
                grab.interactionManager.SelectExit(interactor, grab);
        }

        Rigidbody rb = _respiratorObject.GetComponent<Rigidbody>();
        if (rb != null)
        {
            if (!rb.isKinematic)
            {
                rb.linearVelocity = Vector3.zero;
                rb.angularVelocity = Vector3.zero;
            }
            rb.isKinematic = true;
            rb.useGravity = false;
        }

        _respiratorObject.SetParent(_socketRespiratorBaju, false);

        ApdDisplayItemStabilizer stabilizer = _respiratorObject.GetComponent<ApdDisplayItemStabilizer>();
        if (stabilizer != null)
            stabilizer.enabled = false;

        Vector3 localPos = _paksaPosisiMaskerDadaKananRuntime
            ? new Vector3(0.04f, 0.14f, 0.12f)
            : _posisiLokalRespiratorDiBaju;
        Vector3 localRot = _paksaPosisiMaskerDadaKananRuntime
            ? new Vector3(8f, -10f, 0f)
            : _rotasiLokalRespiratorDiBaju;
        Vector3 worldScale = _paksaPosisiMaskerDadaKananRuntime
            ? new Vector3(0.26f, 0.17f, 0.22f)
            : _skalaDuniaRespiratorDiBaju;
        _respiratorObject.localPosition = localPos;
        _respiratorObject.localRotation = Quaternion.Euler(localRot);
        SetWorldScale(_respiratorObject, worldScale);

        _respiratorObject.gameObject.SetActive(true);

        // PENTING: hentikan coroutine fade masker (dari Level 1 saat dipakai di wajah) supaya
        // tidak men-transparankan ulang masker yang baru ditaruh di dada. Bug "masker hilang"
        // di gameplay normal (Level1->2->3) berasal dari fade ini; debug-skip tidak fade.
        if (_coroutineFadeMasker != null)
        {
            StopCoroutine(_coroutineFadeMasker);
            _coroutineFadeMasker = null;
        }

        foreach (Renderer mr in _respiratorObject.GetComponentsInChildren<Renderer>(true))
        {
            if (mr == null) continue;
            PaksaRendererApdSelaluTerlihat(mr);
            SetRendererAlpha(mr, 1f);
            BuatMaterialSolid(mr);
        }

        foreach (Collider col in _respiratorObject.GetComponentsInChildren<Collider>(true))
            if (col != null) col.enabled = true;

        var grabReady = _respiratorObject.GetComponent<UnityEngine.XR.Interaction.Toolkit.Interactables.XRGrabInteractable>();
        if (grabReady != null) grabReady.enabled = true;

        Log("RESPIRATOR", $"Masker dipastikan ada di socket baju di pos {_respiratorObject.position}, scale={_respiratorObject.lossyScale}, siap di-grab player.", "cyan");
    }

    private void SyncTorsoChestAnchorNow()
    {
        var torso = UnityEngine.Object.FindFirstObjectByType<TorsoChestAnchor>(FindObjectsInactive.Include);
        if (torso != null)
            torso.ForceSyncNow();
    }

    private void BuatMaterialSolid(Renderer r)
    {
        if (r == null) return;
        var mats = Application.isPlaying ? r.materials : r.sharedMaterials;
        for (int i = 0; i < mats.Length; i++)
        {
            var m = mats[i];
            if (m == null) continue;

            if (m.HasProperty("_BaseColor"))
            {
                Color c = m.GetColor("_BaseColor");
                c.a = 1f;
                m.SetColor("_BaseColor", c);
            }
            if (m.HasProperty("_Color"))
            {
                Color c = m.GetColor("_Color");
                c.a = 1f;
                m.SetColor("_Color", c);
            }
            if (m.HasProperty("_Surface"))
            {
                m.SetFloat("_Surface", 0f);
                m.SetFloat("_ZWrite", 1f);
                m.SetOverrideTag("RenderType", "Opaque");
                m.renderQueue = -1;
                m.DisableKeyword("_SURFACE_TYPE_TRANSPARENT");
            }
            if (m.HasProperty("_Cull"))
                m.SetFloat("_Cull", 0f);
        }
        if (Application.isPlaying)
            r.materials = mats;
        else
            r.sharedMaterials = mats;
    }

    public static void PaksaRendererApdSelaluTerlihat(Renderer r)
    {
        if (r == null) return;

        r.enabled = true;
        r.forceRenderingOff = false;
        r.allowOcclusionWhenDynamic = false;
        r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;

        var mats = Application.isPlaying ? r.materials : r.sharedMaterials;
        for (int i = 0; i < mats.Length; i++)
        {
            Material m = mats[i];
            if (m == null) continue;
            if (m.HasProperty("_Cull"))
                m.SetFloat("_Cull", 0f);
        }

        if (Application.isPlaying)
            r.materials = mats;
        else
            r.sharedMaterials = mats;
    }

    /// <summary>
    /// Akses renderer masker untuk efek glow / outline runtime.
    /// </summary>
    public Renderer GetRespiratorRenderer()
    {
        if (_respiratorObject == null)
        {
            GameObject respirator = GameObject.Find("RespiratorMask");
            if (respirator != null)
                _respiratorObject = respirator.transform;
        }

        return _respiratorObject != null ? _respiratorObject.GetComponentInChildren<Renderer>(true) : null;
    }

    // ============================================================
    //  MASKER AUTO TRANSPARENT
    // ============================================================
    private Coroutine _coroutineFadeMasker;

    private void MulaiFadeApdSetelahDipakai(string objectName)
    {
        if (!_maskerAutoTransparentSaatDipakai) return;
        // Khusus masker: simpan handle coroutine supaya bisa dihentikan saat masker
        // dipindah ke dada (mencegah bug "masker hilang" karena fade transparan).
        if (objectName == "RespiratorMask")
        {
            if (_coroutineFadeMasker != null) StopCoroutine(_coroutineFadeMasker);
            _coroutineFadeMasker = StartCoroutine(FadeApdCoroutine(objectName));
        }
        else
        {
            StartCoroutine(FadeApdCoroutine(objectName));
        }
    }

    private IEnumerator FadeApdCoroutine(string objectName)
    {
        yield return new WaitForSeconds(_delayMaskerFade);

        var renderers = AmbilRendererByName(objectName);
        if (renderers == null || renderers.Count == 0)
            yield break;

        foreach (var r in renderers)
            SetMaterialTransparent(r);

        float elapsed = 0f;
        while (elapsed < _durasiMaskerFade)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.Clamp01(elapsed / _durasiMaskerFade);
            float alpha = Mathf.Lerp(1f, _alphaMaskerSetelahFade, t);
            foreach (var r in renderers)
                SetRendererAlpha(r, alpha);
            yield return null;
        }

        foreach (var r in renderers)
            SetRendererAlpha(r, _alphaMaskerSetelahFade);
    }

    private List<Renderer> AmbilRendererByName(string objectName)
    {
        var list = new List<Renderer>();
        var go = GameObject.Find(objectName);
        if (go == null)
        {
            // Cari di child XR Origin (socket APD)
            var allT = UnityEngine.Object.FindObjectsByType<Transform>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            foreach (var t in allT)
            {
                if (t.name == objectName || t.name.Contains(objectName))
                {
                    foreach (var r in t.GetComponentsInChildren<Renderer>(true))
                        if (r != null && !list.Contains(r)) list.Add(r);
                    break;
                }
            }
        }
        else
        {
            foreach (var r in go.GetComponentsInChildren<Renderer>(true))
                if (r != null) list.Add(r);
        }
        return list;
    }

    private void SetMaterialTransparent(Renderer r)
    {
        if (r == null) return;
        // Use instance materials supaya tidak modify shared
        var mats = r.materials;
        for (int i = 0; i < mats.Length; i++)
        {
            var m = mats[i];
            if (m == null) continue;

            if (m.HasProperty("_Surface"))
            {
                m.SetFloat("_Surface", 1f); // URP Lit Transparent
                m.SetFloat("_Blend", 0f);
                m.SetFloat("_ZWrite", 0f);
                m.SetFloat("_SrcBlend", (float)UnityEngine.Rendering.BlendMode.SrcAlpha);
                m.SetFloat("_DstBlend", (float)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
                m.SetOverrideTag("RenderType", "Transparent");
                m.renderQueue = 3000;
                m.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
            }
            else
            {
                // Standard shader
                m.SetFloat("_Mode", 3f); // Transparent
                m.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.SrcAlpha);
                m.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
                m.SetInt("_ZWrite", 0);
                m.DisableKeyword("_ALPHATEST_ON");
                m.EnableKeyword("_ALPHABLEND_ON");
                m.DisableKeyword("_ALPHAPREMULTIPLY_ON");
                m.renderQueue = 3000;
            }
        }
        r.materials = mats;
    }

    private void SetRendererAlpha(Renderer r, float alpha)
    {
        if (r == null) return;
        var mats = r.materials;
        for (int i = 0; i < mats.Length; i++)
        {
            var m = mats[i];
            if (m == null) continue;
            if (m.HasProperty("_BaseColor"))
            {
                Color c = m.GetColor("_BaseColor");
                c.a = alpha;
                m.SetColor("_BaseColor", c);
            }
            if (m.HasProperty("_Color"))
            {
                Color c = m.GetColor("_Color");
                c.a = alpha;
                m.SetColor("_Color", c);
            }
        }
    }
}
