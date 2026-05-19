using UnityEngine;
using System;
using TMPro;

/// <summary>
/// OLIVIA VR - DCSParameterControl.cs
/// Mengontrol SATU parameter DCS (Flow Rate, Acid Ratio, Suhu, Tekanan, atau RPM).
/// Dipasang pada setiap unit kontrol [Monitor Mini + Tombol + / -] di panel DCS.
/// 
/// Cara Pasang di Unity:
///   1. Buat GameObject "UnitKontrol_FlowRate" (misalnya)
///   2. Attach script ini
///   3. Set parameterType ke "FlowRate"
///   4. Assign TMP untuk monitorText
///   5. Assign tombol (+) dan (-) XR Simple Interactable ke tombolPlus / tombolMinus
/// </summary>
public class DCSParameterControl : MonoBehaviour
{
    // ============================================================
    //  TIPE PARAMETER
    // ============================================================
    public enum TipeParameter
    {
        FlowRate,       // m³/h — Level 4 (Target: 450)
        AcidRatio,      // kg/ton — Level 6 (Target: 350)
        Suhu,           // °C — Level 7-8 (Target: 250-255)
        Tekanan,        // atm — Level 7-8 (Target: 45-50)
        RPM,            // RPM — Level 7-8 (Target: 60)
        PH              // pH — Level 6, 13 (Target: 1.0 atau 8.5)
    }

    // ============================================================
    //  INSPECTOR
    // ============================================================
    [Header("=== Konfigurasi Parameter ===")]
    [SerializeField] private TipeParameter _tipeParameter = TipeParameter.FlowRate;
    [SerializeField] private float _nilaiAwal = 0f;
    [SerializeField] private float _nilaiMin = 0f;
    [SerializeField] private float _nilaiMax = 600f;
    [SerializeField] private float _stepPerTombol = 10f;   // Berapa naik/turun per klik [+] / [-]
    [SerializeField] private float _nilaiTarget = 450f;  // Target SOP Pabrik
    [SerializeField] private float _toleransiTarget = 10f; // ± berapa untuk dianggap "tercapai"

    [Header("=== Satuan & Label ===")]
    [SerializeField] private string _satuanLabel = "m³/h";
    [SerializeField] private string _namaParameter = "Flow Rate";

    [Header("=== Referensi UI ===")]
    [SerializeField] private TextMeshProUGUI _monitorText;         // Layar kecil penampil angka
    [SerializeField] private TextMeshProUGUI _statusText;          // Teks "TARGET TERCAPAI" / "Terlalu Rendah"

    [Header("=== Referensi Tombol XR ===")]
    [SerializeField] private UnityEngine.XR.Interaction.Toolkit.Interactables.XRSimpleInteractable _tombolPlus;     // Tombol [+]
    [SerializeField] private UnityEngine.XR.Interaction.Toolkit.Interactables.XRSimpleInteractable _tombolMinus;    // Tombol [-]

    [Header("=== Warna Monitor ===")]
    [SerializeField] private Color _warnaDefault = Color.white;
    [SerializeField] private Color _warnaTargetOK = Color.green;
    [SerializeField] private Color _warnaBahaya = Color.red;
    [SerializeField] private Color _warnaWarning = Color.yellow;

    // ============================================================
    //  STATE
    // ============================================================
    private float _nilaiSaatIni;
    private bool _targetTercapai = false;
    private bool _kontrolAktif = true;  // Bisa di-lock saat level tidak aktif

    // ============================================================
    //  EVENTS
    // ============================================================
    public event Action<float> OnNilaiBerubahe;   // Dipanggil tiap nilai berubah
    public event Action<float> OnTargetTercapai;  // Dipanggil saat nilai dalam range target

    // ============================================================
    //  LIFECYCLE
    // ============================================================
    private void Awake()
    {
        _nilaiSaatIni = _nilaiAwal;
        SetupTombol();
    }

    private void Start()
    {
        RefreshUI();
    }

    private void SetupTombol()
    {
        if (_tombolPlus != null)
            _tombolPlus.selectEntered.AddListener(_ => TambahNilai());

        if (_tombolMinus != null)
            _tombolMinus.selectEntered.AddListener(_ => KurangNilai());
    }

    // ============================================================
    //  AKSI TOMBOL
    // ============================================================
    public void TambahNilai()
    {
        if (!_kontrolAktif) return;
        _nilaiSaatIni = Mathf.Clamp(_nilaiSaatIni + _stepPerTombol, _nilaiMin, _nilaiMax);
        RefreshUI();
        KirimKeGameLevelManager();
        CekTarget();
    }

    public void KurangNilai()
    {
        if (!_kontrolAktif) return;
        _nilaiSaatIni = Mathf.Clamp(_nilaiSaatIni - _stepPerTombol, _nilaiMin, _nilaiMax);
        RefreshUI();
        KirimKeGameLevelManager();
        CekTarget();
    }

    // ============================================================
    //  SINKRONISASI KE GAMELEVELMANAGER
    // ============================================================
    private void KirimKeGameLevelManager()
    {
        if (GameLevelManager.Instance == null) return;

        switch (_tipeParameter)
        {
            case TipeParameter.FlowRate:
                GameLevelManager.Instance.SetFlowRate(_nilaiSaatIni);
                break;
            case TipeParameter.AcidRatio:
                GameLevelManager.Instance.SetAcidRatio(_nilaiSaatIni);
                break;
            case TipeParameter.Suhu:
                GameLevelManager.Instance.SetSuhu(_nilaiSaatIni);
                break;
            case TipeParameter.Tekanan:
                GameLevelManager.Instance.SetTekanan(_nilaiSaatIni);
                break;
            case TipeParameter.RPM:
                GameLevelManager.Instance.SetRPM(_nilaiSaatIni);
                break;
            case TipeParameter.PH:
                GameLevelManager.Instance.SetPH(_nilaiSaatIni);
                break;
        }

        OnNilaiBerubahe?.Invoke(_nilaiSaatIni);
    }

    // ============================================================
    //  CEK TARGET SOP
    // ============================================================
    private void CekTarget()
    {
        bool tercapaiSekarang = Mathf.Abs(_nilaiSaatIni - _nilaiTarget) <= _toleransiTarget;

        if (tercapaiSekarang && !_targetTercapai)
        {
            _targetTercapai = true;
            OnTargetTercapai?.Invoke(_nilaiSaatIni);
            Debug.Log($"<color=green>[DCS KONTROL] Target {_namaParameter} tercapai: {_nilaiSaatIni} {_satuanLabel} (Target: {_nilaiTarget})</color>");
        }
        else if (!tercapaiSekarang && _targetTercapai)
        {
            _targetTercapai = false; // Target terlewat lagi
        }
    }

    // ============================================================
    //  REFRESH UI (Monitor Mini)
    // ============================================================
    private void RefreshUI()
    {
        if (_monitorText != null)
        {
            _monitorText.text = $"{_nilaiSaatIni:F1} {_satuanLabel}";
            _monitorText.color = GetWarnaMonitor();
        }

        if (_statusText != null)
        {
            bool dalamTarget = Mathf.Abs(_nilaiSaatIni - _nilaiTarget) <= _toleransiTarget;

            if (dalamTarget)
            {
                _statusText.text = "✓ TARGET SOP";
                _statusText.color = _warnaTargetOK;
            }
            else if (_nilaiSaatIni < _nilaiTarget - _toleransiTarget)
            {
                _statusText.text = $"▲ Tambah {_namaParameter}";
                _statusText.color = _warnaWarning;
            }
            else
            {
                _statusText.text = $"▼ Kurangi {_namaParameter}";
                _statusText.color = _warnaBahaya;
            }
        }
    }

    private Color GetWarnaMonitor()
    {
        float jarak = Mathf.Abs(_nilaiSaatIni - _nilaiTarget);
        if (jarak <= _toleransiTarget) return _warnaTargetOK; // Hijau: Target pas
        if (jarak <= _toleransiTarget * 3f) return _warnaWarning;  // Kuning: Mendekati
        return _warnaDefault;                                          // Putih: Jauh dari target
    }

    // ============================================================
    //  API PUBLIK
    // ============================================================

    /// <summary>Aktifkan kontrol ini (hanya saat level yang relevan aktif).</summary>
    public void AktifkanKontrol(bool aktif)
    {
        _kontrolAktif = aktif;
        if (_tombolPlus != null) _tombolPlus.enabled = aktif;
        if (_tombolMinus != null) _tombolMinus.enabled = aktif;
    }

    /// <summary>Set nilai secara paksa dari luar (misal: reset setelah level baru).</summary>
    /// <summary>Set nilai secara paksa dari luar (misal: reset setelah level baru).</summary>
    public void SetNilaiLangsung(float nilai)
    {
        _nilaiSaatIni = Mathf.Clamp(nilai, _nilaiMin, _nilaiMax);
        _targetTercapai = false;
        RefreshUI();
        KirimKeGameLevelManager();
        CekTarget();
    }

    public float NilaiSaatIni => _nilaiSaatIni;
    public bool TargetTercapai => _targetTercapai;

    // ============================================================
    //  PRESET CEPAT (Context Menu di Inspector untuk testing)
    // ============================================================
#if UNITY_EDITOR
    [ContextMenu("Preset: Flow Rate 450 m³/h")]
    private void PresetFlowRate() { _tipeParameter = TipeParameter.FlowRate; _nilaiTarget = 450; _nilaiMax = 600; _satuanLabel = "m³/h"; }

    [ContextMenu("Preset: Acid Ratio 350 kg/ton")]
    private void PresetAcid() { _tipeParameter = TipeParameter.AcidRatio; _nilaiTarget = 350; _nilaiMax = 500; _satuanLabel = "kg/ton"; _stepPerTombol = 10; }

    [ContextMenu("Preset: Suhu Autoclave 252°C")]
    private void PresetSuhu() { _tipeParameter = TipeParameter.Suhu; _nilaiTarget = 252; _nilaiMin = 0; _nilaiMax = 300; _satuanLabel = "°C"; _stepPerTombol = 1; }

    [ContextMenu("Preset: Tekanan 47.5 atm")]
    private void PresetTekanan() { _tipeParameter = TipeParameter.Tekanan; _nilaiTarget = 47.5f; _nilaiMin = 0; _nilaiMax = 80; _satuanLabel = "atm"; _stepPerTombol = 0.5f; _toleransiTarget = 2.5f; }

    [ContextMenu("Preset: RPM Agitator 60")]
    private void PresetRPM() { _tipeParameter = TipeParameter.RPM; _nilaiTarget = 60; _nilaiMin = 0; _nilaiMax = 120; _satuanLabel = "RPM"; _stepPerTombol = 5; }

    [ContextMenu("Preset: pH 1.0 (Acid)")]
    private void PresetPHAsam() { _tipeParameter = TipeParameter.PH; _nilaiTarget = 1.0f; _nilaiMin = 0; _nilaiMax = 14; _satuanLabel = "pH"; _stepPerTombol = 0.1f; _toleransiTarget = 0.2f; }

    [ContextMenu("Preset: pH 8.5 (Tailing)")]
    private void PresetPHTailing() { _tipeParameter = TipeParameter.PH; _nilaiTarget = 8.5f; _nilaiMin = 0; _nilaiMax = 14; _satuanLabel = "pH"; _stepPerTombol = 0.1f; _toleransiTarget = 0.5f; }
#endif
}
