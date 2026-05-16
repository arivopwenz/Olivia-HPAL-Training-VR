using UnityEngine;

using System.Collections;
using TMPro;

/// <summary>
/// OLIVIA VR - DCSTombolPanel.cs
/// Satu tombol fisik di panel DCS. Saat level yang sesuai aktif, tombol ini
/// akan berkedip/menyala (highlight) sebagai petunjuk visual bagi pemain.
/// Saat ditekan, melaporkan ke GameLevelManager.
///
/// CARA SETUP DI UNITY:
///   1. Buat GameObject "Tombol_1" ... "Tombol_14" di panel DCS
///   2. Attach script ini ke masing-masing tombol
///   3. Set nomorTombol sesuai (1-14)
///   4. Assign XRSimpleInteractable (bisa grab/touch) ke xrInteractable
///   5. Assign MeshRenderer tombol ke meshRenderer
///   6. Set material Normal dan Highlight di Inspector
/// </summary>
public class DCSTombolPanel : MonoBehaviour
{
    // ============================================================
    //  INSPECTOR
    // ============================================================
    [Header("=== Konfigurasi Tombol ===")]
    [SerializeField] private int  _nomorTombol = 1;       // Nomor tombol (1-14)
    [SerializeField] private string _namaLabel = "Tombol DCS";

    [Header("=== Referensi Komponen ===")]
    [SerializeField] private UnityEngine.XR.Interaction.Toolkit.Interactables.XRSimpleInteractable _xrInteractable;
    [SerializeField] private MeshRenderer         _meshRenderer;
    [SerializeField] private TextMeshPro          _labelText;    // Label angka di permukaan tombol

    [Header("=== Material Tombol ===")]
    [SerializeField] private Material _materialNormal;     // Material default (abu-abu/hitam)
    [SerializeField] private Material _materialHighlight;  // Material saat aktif (bercahaya/kuning)
    [SerializeField] private Material _materialDitekan;    // Material saat baru ditekan (putih/flash)
    [SerializeField] private Material _materialSelesai;    // Material saat sudah selesai (hijau)

    [Header("=== Animasi Highlight ===")]
    [SerializeField] private float _kecepatanKedip = 1.5f; // Berapa kali kedip per detik

    [Header("=== Status (Read Only) ===")]
    [SerializeField] private bool _sedangHighlight = false;
    [SerializeField] private bool _sudahDitekan    = false;

    // ============================================================
    //  PRIVATE
    // ============================================================
    private Coroutine _coroutineKedip;
    private bool _blinkState = false;

    // ============================================================
    //  LIFECYCLE
    // ============================================================
    private void Awake()
    {
        AutoBindReferences();
        SetupInteractable();
        if (_labelText != null) _labelText.text = _nomorTombol.ToString();
        AturMaterial(_materialNormal);
    }

    private void OnEnable()
    {
        GameLevelManager.OnDCSButtonShouldHighlight += OnHighlightDiminta;
        GameLevelManager.OnLevelStarted             += OnLevelBaru;
    }

    private void OnDisable()
    {
        GameLevelManager.OnDCSButtonShouldHighlight -= OnHighlightDiminta;
        GameLevelManager.OnLevelStarted             -= OnLevelBaru;
    }

    private void SetupInteractable()
    {
        if (_xrInteractable == null) return;
        _xrInteractable.selectEntered.AddListener(_ => OnTombolDitekan());
    }

    private void AutoBindReferences()
    {
        if (_xrInteractable == null)
            _xrInteractable = GetComponent<UnityEngine.XR.Interaction.Toolkit.Interactables.XRSimpleInteractable>();

        if (_meshRenderer == null)
            _meshRenderer = GetComponentInChildren<MeshRenderer>();

        if (_labelText == null)
            _labelText = GetComponentInChildren<TextMeshPro>();
    }

    // ============================================================
    //  LISTENER EVENT DARI GameLevelManager
    // ============================================================
    private void OnHighlightDiminta(int nomorTombolDiminta)
    {
        if (nomorTombolDiminta == _nomorTombol)
            MulaiHighlight();
        else
            HentikanHighlight(); // Tombol lain tidak aktif
    }

    private void OnLevelBaru(GameLevelManager.GameLevel level)
    {
        // Reset semua tombol ke normal saat level baru mulai
        if (!_sedangHighlight && !_sudahDitekan)
            AturMaterial(_materialNormal);
    }

    // ============================================================
    //  AKSI TOMBOL DITEKAN
    // ============================================================
    private void OnTombolDitekan()
    {
        if (_sudahDitekan) return;

        // Flash singkat material "ditekan"
        StartCoroutine(FlashDitekan());

        // Laporan ke GameLevelManager
        GameLevelManager.Instance?.OnDCSTombolDitekan(_nomorTombol);

        Debug.Log($"<color=cyan>[DCS TOMBOL {_nomorTombol}]</color> '{_namaLabel}' ditekan!");
    }

    private IEnumerator FlashDitekan()
    {
        HentikanHighlight();
        AturMaterial(_materialDitekan);
        yield return new WaitForSeconds(0.3f);
        AturMaterial(_materialSelesai);
        _sudahDitekan = true;
    }

    // ============================================================
    //  HIGHLIGHT / KEDIP
    // ============================================================
    public void MulaiHighlight()
    {
        if (_sedangHighlight || _sudahDitekan) return;
        _sedangHighlight = true;
        if (_coroutineKedip != null) StopCoroutine(_coroutineKedip);
        _coroutineKedip = StartCoroutine(EfekKedip());
    }

    public void HentikanHighlight()
    {
        _sedangHighlight = false;
        if (_coroutineKedip != null)
        {
            StopCoroutine(_coroutineKedip);
            _coroutineKedip = null;
        }
        if (!_sudahDitekan) AturMaterial(_materialNormal);
    }

    private IEnumerator EfekKedip()
    {
        while (_sedangHighlight)
        {
            _blinkState = !_blinkState;
            AturMaterial(_blinkState ? _materialHighlight : _materialNormal);
            yield return new WaitForSeconds(1f / (_kecepatanKedip * 2f));
        }
    }

    // ============================================================
    //  UTILITIES
    // ============================================================
    private void AturMaterial(Material mat)
    {
        if (_meshRenderer == null || mat == null) return;
        _meshRenderer.material = mat;
    }

    /// <summary>Reset tombol ke kondisi awal (untuk replay atau restart level).</summary>
    public void Reset()
    {
        HentikanHighlight();
        _sudahDitekan = false;
        AturMaterial(_materialNormal);
    }

    public int  NomorTombol  => _nomorTombol;
    public bool SudahDitekan => _sudahDitekan;

#if UNITY_EDITOR
    [ContextMenu("DEBUG: Simulasi Highlight")]
    private void D_Highlight() => MulaiHighlight();

    [ContextMenu("DEBUG: Simulasi Tombol Ditekan")]
    private void D_Tekan()     => OnTombolDitekan();

    [ContextMenu("DEBUG: Reset Tombol")]
    private void D_Reset()     => Reset();
#endif
}
