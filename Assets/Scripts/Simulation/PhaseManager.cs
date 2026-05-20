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
    [SerializeField] private bool _otomatisSimpanRespiratorSaatLevel2 = true;
    [SerializeField] private Transform _respiratorObject;
    [SerializeField] private Transform _socketRespiratorBaju;
    [SerializeField] private Vector3 _posisiLokalRespiratorDiBaju = Vector3.zero;
    [SerializeField] private Vector3 _rotasiLokalRespiratorDiBaju = Vector3.zero;
    [SerializeField] private Vector3 _skalaDuniaRespiratorDiBaju = new Vector3(0.2f, 0.095f, 0.12f);

    [Header("=== Masker Auto-Transparent ===")]
    [Tooltip("Setelah masker dipakai, fade material ke transparent supaya tidak menutupi pandangan.")]
    [SerializeField] private bool _maskerAutoTransparentSaatDipakai = true;
    [Tooltip("Jeda detik setelah masker dipakai sebelum mulai fade.")]
    [SerializeField] private float _delayMaskerFade = 2f;
    [Tooltip("Durasi fade dari opaque ke alpha target.")]
    [SerializeField] private float _durasiMaskerFade = 1.0f;
    [Tooltip("Alpha akhir masker setelah fade. 0 = invisible, 1 = solid.")]
    [Range(0.05f, 1f)] [SerializeField] private float _alphaMaskerSetelahFade = 0.30f;

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
        GameLevelManager.OnLevelStarted += OnLevelStarted;
        Log("APD SYSTEM", $"Siap! Pakai {TOTAL_APD} APD sebelum keluar loker.", "yellow");
    }

    private void OnDestroy()
    {
        GameLevelManager.OnLevelStarted -= OnLevelStarted;
    }

    public void OnHelmetWorn()        { PakaiApd(_helm);          }
    public void OnVestWorn()          { PakaiApd(_rompi);         }
    public void OnGlassesWorn()       { PakaiApd(_kacamata);      }
    public void OnBootsWorn()         { PakaiApd(_sepatuBots);    }
    public void OnGlovesWorn()        { PakaiApd(_sarungTangan);  }
    public void OnRespiratiorWorn()   { PakaiApd(_respirator); MulaiFadeMaskerSetelahDipakai(); }
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
        if (level == GameLevelManager.GameLevel.Level2_DCSPrep && _otomatisSimpanRespiratorSaatLevel2)
            PindahkanRespiratorKeSocketBaju();
    }

    private void PindahkanRespiratorKeSocketBaju()
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
        {
            Log("RESPIRATOR AUTO", "RespiratorMask atau Socket_Respirator_Baju belum ditemukan.", "orange");
            return;
        }

        Rigidbody rb = _respiratorObject.GetComponent<Rigidbody>();
        if (rb != null)
        {
            rb.linearVelocity = Vector3.zero;
            rb.angularVelocity = Vector3.zero;
        }

        _respiratorObject.SetParent(_socketRespiratorBaju, false);
        _respiratorObject.localPosition = _posisiLokalRespiratorDiBaju;
        _respiratorObject.localRotation = Quaternion.Euler(_rotasiLokalRespiratorDiBaju);
        SetWorldScale(_respiratorObject, _skalaDuniaRespiratorDiBaju);

        OnRespiratorStored();
        Log("RESPIRATOR AUTO", "Level 2 dimulai: respirator dipindahkan ke socket baju agar bisa dilihat saat menunduk.", "cyan");
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
    public void PastikanMaskerAdaDiSocketBaju()
    {
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

        // 1. Force-deselect masker dari interactor / socket manapun (face socket, hand grab, dll).
        var grab = _respiratorObject.GetComponent<UnityEngine.XR.Interaction.Toolkit.Interactables.XRGrabInteractable>();
        if (grab != null && grab.isSelected && grab.interactionManager != null)
        {
            // CancelInteractableSelection: force release dari semua interactor.
            var selectingList = new System.Collections.Generic.List<UnityEngine.XR.Interaction.Toolkit.Interactors.IXRSelectInteractor>(grab.interactorsSelecting);
            foreach (var interactor in selectingList)
            {
                grab.interactionManager.SelectExit(interactor, grab);
            }
        }

        // 2. Stabilkan rigidbody supaya tidak terbang atau jatuh setelah deselect.
        Rigidbody rb = _respiratorObject.GetComponent<Rigidbody>();
        if (rb != null)
        {
            rb.linearVelocity = Vector3.zero;
            rb.angularVelocity = Vector3.zero;
            rb.isKinematic = true; // sementara kinematic; XRGrabInteractable akan switch saat di-grab
            rb.useGravity = false;
        }

        // 3. Reparent ke socket baju dengan worldPositionStays=false agar local transform reset ke 0/identitas otomatis.
        _respiratorObject.SetParent(_socketRespiratorBaju, false);
        _respiratorObject.localPosition = _posisiLokalRespiratorDiBaju;
        _respiratorObject.localRotation = Quaternion.Euler(_rotasiLokalRespiratorDiBaju);
        SetWorldScale(_respiratorObject, _skalaDuniaRespiratorDiBaju);

        _respiratorObject.gameObject.SetActive(true);

        // 4. Pastikan renderer & collider on supaya bisa di-grab.
        Renderer mr = _respiratorObject.GetComponentInChildren<Renderer>(true);
        if (mr != null) mr.enabled = true;
        Collider col = _respiratorObject.GetComponent<Collider>();
        if (col != null) col.enabled = true;

        Log("RESPIRATOR", $"Masker dipastikan ada di socket baju di pos {_respiratorObject.position}, scale={_respiratorObject.lossyScale}, siap di-grab player.", "cyan");
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

    private void MulaiFadeMaskerSetelahDipakai()
    {
        if (!_maskerAutoTransparentSaatDipakai) return;
        if (_coroutineFadeMasker != null)
        {
            StopCoroutine(_coroutineFadeMasker);
            _coroutineFadeMasker = null;
        }
        _coroutineFadeMasker = StartCoroutine(FadeMaskerCoroutine());
    }

    private IEnumerator FadeMaskerCoroutine()
    {
        yield return new WaitForSeconds(_delayMaskerFade);

        var renderers = AmbilSemuaRendererMasker();
        if (renderers == null || renderers.Count == 0)
        {
            _coroutineFadeMasker = null;
            yield break;
        }

        // Sebelum fade, set semua material ke transparent mode (URP/Standard).
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

        // Pastikan ke alpha final
        foreach (var r in renderers)
            SetRendererAlpha(r, _alphaMaskerSetelahFade);

        Log("MASKER", $"Masker fade ke alpha {_alphaMaskerSetelahFade:F2} selesai.", "cyan");
        _coroutineFadeMasker = null;
    }

    private List<Renderer> AmbilSemuaRendererMasker()
    {
        var list = new List<Renderer>();
        if (_respiratorObject == null)
        {
            GameObject respirator = GameObject.Find("RespiratorMask");
            if (respirator != null) _respiratorObject = respirator.transform;
        }
        if (_respiratorObject == null) return list;
        foreach (var r in _respiratorObject.GetComponentsInChildren<Renderer>(true))
            if (r != null) list.Add(r);
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
