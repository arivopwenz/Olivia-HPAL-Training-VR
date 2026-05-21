using UnityEngine;
using TMPro;
using UnityEngine.XR.Interaction.Toolkit.Interactables;

/// <summary>
/// OLIVIA VR - FlowRateControlPanel.cs (v3.0 - Permanent UI)
///
/// Versi ini pakai GameObject PERMANENT yang sudah ada di scene:
///   - Btn_FlowPlus (capsule fisik di meja DCS, child Button DCS)
///   - Btn_FlowMinus (capsule fisik di meja DCS, child Button DCS)
///   - Widget_FlowRate (UI di DCS_Monitor_Canvas/BG/Component)
///
/// Tidak ada lagi runtime auto-create. UI selalu visible (permanent), tidak
/// disembunyikan per-level. Cuma tombol XR-nya yang di-enable/disable per Level
/// supaya hanya bisa diklik saat Level 4 aktif.
///
/// Tugas script: wire DCSParameterControl ke tombol existing + lock click di luar Level 4.
/// </summary>
public class FlowRateControlPanel : MonoBehaviour
{
    [Header("=== Konfigurasi DCSParameterControl ===")]
    [SerializeField] private float _nilaiAwal = 0f;
    [SerializeField] private float _nilaiMin = 0f;
    [SerializeField] private float _nilaiMax = 600f;
    [SerializeField] private float _stepPerTombol = 50f;
    [SerializeField] private float _nilaiTarget = 450f;
    [SerializeField] private float _toleransiTarget = 10f;
    [SerializeField] private string _satuanLabel = "m³/h";
    [SerializeField] private string _namaParameter = "Flow Rate";

    [Header("=== Referensi GameObject Permanent (auto-find) ===")]
    [SerializeField] private XRSimpleInteractable _btnPlus;
    [SerializeField] private XRSimpleInteractable _btnMinus;
    [SerializeField] private TextMeshProUGUI _displayMonitor;
    [SerializeField] private TextMeshProUGUI _displayStatus;

    [Header("=== Visibility ===")]
    [Tooltip("Lock klik tombol kalau bukan Level 4 (tombol tetap kelihatan, tapi tidak responsif).")]
    [SerializeField] private bool _hanyaSaatLevel4 = true;

    private DCSParameterControl _control;

    private void Awake()
    {
        AutoFindReferences();
        AttachDCSParameterControl();
    }

    private void Start()
    {
        WireTombolListeners();
        UpdateLockState(GameLevelManager.Instance != null
            ? GameLevelManager.Instance.CurrentLevel
            : GameLevelManager.GameLevel.Level0_Tutorial);
        GameLevelManager.OnLevelStarted += OnLevelStarted;
    }

    private void OnDestroy()
    {
        GameLevelManager.OnLevelStarted -= OnLevelStarted;
    }

    private void OnLevelStarted(GameLevelManager.GameLevel level)
    {
        UpdateLockState(level);
    }

    /// <summary>
    /// Cuma toggle tombol XR — display & objek tetap visible permanent di scene.
    /// </summary>
    private void UpdateLockState(GameLevelManager.GameLevel level)
    {
        bool aktif = !_hanyaSaatLevel4 || level == GameLevelManager.GameLevel.Level4_SlurryPump;
        if (_btnPlus != null) _btnPlus.enabled = aktif;
        if (_btnMinus != null) _btnMinus.enabled = aktif;
        if (_control != null) _control.AktifkanKontrol(aktif);
    }

    private void AutoFindReferences()
    {
        if (_btnPlus == null)
        {
            var go = GameObject.Find("Btn_FlowPlus");
            if (go != null) _btnPlus = go.GetComponent<XRSimpleInteractable>();
        }
        if (_btnMinus == null)
        {
            var go = GameObject.Find("Btn_FlowMinus");
            if (go != null) _btnMinus = go.GetComponent<XRSimpleInteractable>();
        }
        if (_displayMonitor == null || _displayStatus == null)
        {
            var widget = GameObject.Find("Widget_FlowRate");
            if (widget != null)
            {
                if (_displayMonitor == null)
                {
                    var m = widget.transform.Find("Monitor");
                    if (m != null) _displayMonitor = m.GetComponent<TextMeshProUGUI>();
                }
                if (_displayStatus == null)
                {
                    var s = widget.transform.Find("Status");
                    if (s != null) _displayStatus = s.GetComponent<TextMeshProUGUI>();
                }
            }
        }

        Debug.Log($"[FlowRateControlPanel] AutoFind: Plus={_btnPlus != null}, Minus={_btnMinus != null}, Monitor={_displayMonitor != null}, Status={_displayStatus != null}");
    }

    private void AttachDCSParameterControl()
    {
        _control = GetComponent<DCSParameterControl>();
        if (_control == null) _control = gameObject.AddComponent<DCSParameterControl>();

        SetPrivateField(_control, "_tipeParameter", DCSParameterControl.TipeParameter.FlowRate);
        SetPrivateField(_control, "_nilaiAwal", _nilaiAwal);
        SetPrivateField(_control, "_nilaiMin", _nilaiMin);
        SetPrivateField(_control, "_nilaiMax", _nilaiMax);
        SetPrivateField(_control, "_stepPerTombol", _stepPerTombol);
        SetPrivateField(_control, "_nilaiTarget", _nilaiTarget);
        SetPrivateField(_control, "_toleransiTarget", _toleransiTarget);
        SetPrivateField(_control, "_satuanLabel", _satuanLabel);
        SetPrivateField(_control, "_namaParameter", _namaParameter);
        SetPrivateField(_control, "_monitorText", _displayMonitor);
        SetPrivateField(_control, "_statusText", _displayStatus);
        // _tombolPlus/_tombolMinus SENGAJA tidak di-set — wire manual di Start()
    }

    private void WireTombolListeners()
    {
        if (_btnPlus != null && _control != null)
            _btnPlus.selectEntered.AddListener(_ => _control.TambahNilai());
        if (_btnMinus != null && _control != null)
            _btnMinus.selectEntered.AddListener(_ => _control.KurangNilai());

        Debug.Log($"[FlowRateControlPanel] Listener wired: Plus={_btnPlus != null}, Minus={_btnMinus != null}, Control={_control != null}");
    }

    private static void SetPrivateField(object obj, string fieldName, object value)
    {
        var field = obj.GetType().GetField(fieldName,
            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
        if (field != null) field.SetValue(obj, value);
    }
}
