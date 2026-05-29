using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System.Collections;
using System.Collections.Generic;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

/// <summary>
/// DCS Monitor UI v3.0 — OLIVIA VR Simulator
/// 
/// Fully integrated with GameLevelManager (14-Level system).
/// Menampilkan parameter reaktor real-time, valve status, flow tracker,
/// alarm, dan ESD panel.
/// 
/// TIDAK lagi bergantung pada PhaseManager untuk alur level.
/// </summary>
public class DCSMonitorUI : MonoBehaviour
{
    // ============================================================
    //  PANEL HEADER
    // ============================================================
    [Header("=== Panel Header ===")]
    public TextMeshProUGUI txtJudulMonitor;
    public TextMeshProUGUI txtStatusFase;
    public TextMeshProUGUI txtWaktuShift;

    // ============================================================
    //  PANEL 1: PARAMETER REAKTOR
    // ============================================================
    [Header("=== Parameter Reaktor (Panel 1) ===")]
    public TextMeshProUGUI txtSuhu;
    public TextMeshProUGUI txtTekanan;
    public TextMeshProUGUI txtPH;
    public TextMeshProUGUI txtFlowRate;
    public TextMeshProUGUI txtRPM;
    public TextMeshProUGUI txtScaleLevel;
    public TextMeshProUGUI txtKadarNikel;
    public TextMeshProUGUI txtEfisiensi;
    public TextMeshProUGUI txtKadarAsam;
    public TextMeshProUGUI txtWaktuProses;
    public TextMeshProUGUI txtStatusMesin;

    // ============================================================
    //  PANEL TASK MESIN (untuk level tertentu)
    // ============================================================
    [Header("=== Task Mesin (DCS Display) ===")]
    public GameObject panelTaskMesin;
    public TextMeshProUGUI taskScannerDCS;
    public TextMeshProUGUI taskMesinDCS;

    // ============================================================
    //  PANEL 2: FLOW TRACKER
    // ============================================================
    [Header("=== Flow Tracker — Posisi Cairan (Panel 2) ===")]
    [Tooltip("Image berupa lingkaran kecil untuk setiap titik proses")]
    public Image[] flowStepIndicators;
    public TextMeshProUGUI txtFlowCurrentStep;
    public TextMeshProUGUI txtFlowProgress;

    [Header("Warna Flow Indicator")]
    public Color warnaNodeBelum = new Color(0.3f, 0.3f, 0.3f);
    public Color warnaNodeAktif = new Color(1f, 0.85f, 0.1f);
    public Color warnaNodeSelesai = new Color(0.2f, 0.9f, 0.4f);

    // ============================================================
    //  PANEL 3: VALVE STATUS
    // ============================================================
    [Header("=== Valve Status Tracker (Panel 3) ===")]
    public TextMeshProUGUI txtValveSteam;
    public TextMeshProUGUI txtValveAcidFeed;
    public TextMeshProUGUI txtValveSlurryFeed;
    public TextMeshProUGUI txtValveLetdown;
    public TextMeshProUGUI txtValveFlash;
    public TextMeshProUGUI txtValveIsolation;

    // ============================================================
    //  PANEL 4: ESD
    // ============================================================
    [Header("=== ESD Panel (Panel 4) ===")]
    public GameObject panelESD;
    public Button btnESD;
    public TextMeshProUGUI txtESDStatus;
    public Image btnESDBackground;
    public TextMeshProUGUI txtCountdown;
    public Image imgCountdownBar;

    // ============================================================
    //  PANEL 5: ALARM
    // ============================================================
    [Header("=== Alarm System (Panel 5) ===")]
    public GameObject panelAlarm;
    public TextMeshProUGUI txtAlarm;
    public Image bgAlarm;

    // ============================================================
    //  WARNA STANDARD
    // ============================================================
    [Header("=== Warna ===")]
    public Color warnaHijau = new Color(0.2f, 0.9f, 0.4f);
    public Color warnaKuning = new Color(1f, 0.85f, 0.1f);
    public Color warnaMerah = new Color(0.95f, 0.2f, 0.2f);
    public Color warnaBlue = new Color(0.3f, 0.8f, 1f);
    public Color warnaAbu = new Color(0.5f, 0.5f, 0.5f);

    [Header("=== Kontrol Flow Rate ===")]
    [SerializeField] private float _langkahFlowRate = 10f;
    [SerializeField] private bool _izinkanKeyboardFlowTest = true;

    // ============================================================
    //  DATA INTERNAL — PARAMETER REAKTOR
    // ============================================================
    private float _suhu = 25f;
    private float _tekanan = 1f;
    private float _pH = 7f;
    private float _flowRate = 0f;
    private float _rpm = 0f;
    private float _scaleLevel = 12f;
    private float _nikel = 0f;
    private float _efisiensi = 0f;
    private float _kadarAsam = 0f;
    private float _waktuProses = 0f;
    private float _waktuShift = 0f;

    // Target saat mesin aktif
    private float _targetSuhu = 250f;
    private float _targetTekanan = 47.5f;
    private float _targetRPM = 60f;
    private float _targetFlow = 450f;

    // ============================================================
    //  DATA INTERNAL — VALVE
    // ============================================================
    private bool _valveSteam = false;
    private bool _valveAcidFeed = false;
    private bool _valveSlurry = false;
    private bool _valveLetdown = false;
    private bool _valveFlash = false;
    private bool _valveIsolation = false;

    // ============================================================
    //  DATA INTERNAL — FLOW TRACKER
    // ============================================================
    private int _flowCurrentStep = 0;
    private readonly string[] _flowStepNames = {
        "IDLE", "Crusher", "Slurry Tank", "Pre-heater",
        "Acid Injection", "AUTOCLAVE", "Flash Vessel", "CCD Separator",
        "MHP Tank", "Tailing Neutralization", "Filter Press", "Dry Stack"
    };

    // ============================================================
    //  STATE FLAGS
    // ============================================================
    private bool _mesinAktif = false;
    private bool _daruratAktif = false;
    private bool _alarmAktif = false;
    private bool _esdSudahDitekan = false;
    private float _countdownSisa = 45f;

    // ============================================================
    //  LIFECYCLE
    // ============================================================
    void Start()
    {
        // Subscribe ke event sistem 14-level
        GameLevelManager.OnLevelStarted += OnLevelBerubah;
        GameLevelManager.OnDCSButtonPressed += OnDcsButtonPressed;

        // Setup tombol ESD
        if (btnESD != null)
            btnESD.onClick.AddListener(TekanESD);

        // Sembunyikan panel yang belum perlu
        if (panelAlarm != null) panelAlarm.SetActive(false);
        if (panelESD != null) panelESD.SetActive(false);
        if (panelTaskMesin != null) panelTaskMesin.SetActive(false);

        // Init valve semua TUTUP
        ResetSemuaValve();

        StartCoroutine(SimulasiReaktor());
        StartCoroutine(KejapAlarm());
        StartCoroutine(UpdateWaktuShift());

        UpdateSemuaTampilan();
        UpdateFlowTracker();
        UpdateValvePanel();
    }

    void Update()
    {
        if (!_izinkanKeyboardFlowTest || GameLevelManager.Instance == null)
            return;

        if (GameLevelManager.Instance.CurrentLevel != GameLevelManager.GameLevel.Level4_SlurryPump)
            return;

#if ENABLE_INPUT_SYSTEM
        var keyboard = Keyboard.current;
        if (keyboard == null)
            return;

        if (keyboard.equalsKey.wasPressedThisFrame || keyboard.numpadPlusKey.wasPressedThisFrame)
            TambahFlowRate();
        else if (keyboard.minusKey.wasPressedThisFrame || keyboard.numpadMinusKey.wasPressedThisFrame)
            KurangiFlowRate();
#elif ENABLE_LEGACY_INPUT_MANAGER
        if (Input.GetKeyDown(KeyCode.Equals) || Input.GetKeyDown(KeyCode.KeypadPlus))
            TambahFlowRate();
        else if (Input.GetKeyDown(KeyCode.Minus) || Input.GetKeyDown(KeyCode.KeypadMinus))
            KurangiFlowRate();
#endif
    }

    void OnDestroy()
    {
        GameLevelManager.OnLevelStarted -= OnLevelBerubah;
        GameLevelManager.OnDCSButtonPressed -= OnDcsButtonPressed;

        if (btnESD != null)
            btnESD.onClick.RemoveListener(TekanESD);
    }

    // ============================================================
    //  EVENT HANDLER: LEVEL BERUBAH
    // ============================================================
    private void OnLevelBerubah(GameLevelManager.GameLevel level)
    {
        if (txtStatusFase == null) return;

        _flowCurrentStep = Mathf.Clamp((int)level, 0, _flowStepNames.Length - 1);

        // 1. Update teks status sesuai level
        switch (level)
        {
            case GameLevelManager.GameLevel.Level0_Tutorial:
                txtStatusFase.text = "STATUS: ORIENTASI AWAL";
                txtStatusFase.color = warnaKuning;
                break;

            case GameLevelManager.GameLevel.Level1_APD:
                txtStatusFase.text = "STATUS: PERSIAPAN APD";
                txtStatusFase.color = warnaKuning;
                TriggerAlarm("OPERATOR — LENGKAPI APD SEBELUM MASUK AREA", false);
                break;

            case GameLevelManager.GameLevel.Level2_DCSPrep:
                txtStatusFase.text = "STATUS: INISIALISASI DCS";
                txtStatusFase.color = warnaBlue;
                TriggerAlarm("DCS AKTIF — SIAPKAN AREA OPERASIONAL", false);
                break;

            case GameLevelManager.GameLevel.Level3_OreSlurry:
                txtStatusFase.text = "STATUS: ORE → SLURRY";
                txtStatusFase.color = warnaBlue;
                break;

            case GameLevelManager.GameLevel.Level4_SlurryPump:
                txtStatusFase.text = "STATUS: SLURRY PUMP READY";
                txtStatusFase.color = warnaBlue;
                _targetFlow = 450f;
                break;

            case GameLevelManager.GameLevel.Level5_SteamValve:
                txtStatusFase.text = "STATUS: PEMANASAN AWAL";
                txtStatusFase.color = warnaBlue;
                SetValve(ref _valveSteam, true);
                _targetSuhu = 190f;
                break;

            case GameLevelManager.GameLevel.Level6_AcidInjection:
                txtStatusFase.text = "STATUS: INJEKSI ASAM SULFAT";
                txtStatusFase.color = warnaKuning;
                SetValve(ref _valveAcidFeed, false);
                TriggerAlarm("PERHATIAN — INJEKSI H₂SO₄ DIMULAI!", false);
                break;

            case GameLevelManager.GameLevel.Level7_Autoclave:
                txtStatusFase.text = "STATUS: AUTOCLAVE AKTIF";
                txtStatusFase.color = warnaHijau;
                _mesinAktif = true;
                _targetSuhu = 252f;
                _targetTekanan = 47.5f;
                _targetRPM = 60f;
                if (panelTaskMesin != null) panelTaskMesin.SetActive(true);
                if (txtStatusMesin != null) { txtStatusMesin.text = "AKTIF"; txtStatusMesin.color = warnaHijau; }
                TriggerAlarm("REAKTOR AUTOCLAVE BEROPERASI — PANTAU PARAMETER!", false);
                break;

            case GameLevelManager.GameLevel.Level8_Monitoring:
                txtStatusFase.text = "STATUS: MONITORING KETAT";
                txtStatusFase.color = warnaHijau;
                break;

            case GameLevelManager.GameLevel.Level9_FlashVessel:
                txtStatusFase.text = "STATUS: FLASH VESSEL";
                txtStatusFase.color = warnaBlue;
                _targetTekanan = 12f;
                _targetFlow = 430f;
                _flowCurrentStep = 6;
                SetValve(ref _valveLetdown, true);
                SetValve(ref _valveFlash, true);
                TriggerAlarm("LETDOWN VALVE TERBUKA - TEKANAN MENUJU FLASH VESSEL", false);
                break;

            case GameLevelManager.GameLevel.Level10_CCD:
                txtStatusFase.text = "STATUS: SEPARASI CCD";
                txtStatusFase.color = warnaBlue;
                _flowCurrentStep = 7;
                _targetFlow = 420f;
                _targetRPM = 4f;
                TriggerAlarm("CCD AKTIF - PEMISAHAN PADAT-CAIR DIMULAI", false);
                break;

            case GameLevelManager.GameLevel.Level11_MHP:
                txtStatusFase.text = "STATUS: NETRALISASI & MHP";
                txtStatusFase.color = warnaHijau;
                _flowCurrentStep = 8;
                _targetRPM = 35f;
                TriggerAlarm("MHP TRAIN AKTIF - NETRALISASI DAN PRESIPITASI BERJALAN", false);
                break;

            case GameLevelManager.GameLevel.Level12_TailingDischarge:
                txtStatusFase.text = "STATUS: TAILING FILTER PRESS";
                txtStatusFase.color = warnaKuning;
                _flowCurrentStep = 10;
                _targetFlow = 360f;
                _targetRPM = 28f;
                TriggerAlarm("TAILING TREATMENT AKTIF - NETRALISASI DAN FILTER PRESS", false);
                break;

            case GameLevelManager.GameLevel.Level13_TailingWaste:
                txtStatusFase.text = "STATUS: DRY STACK TAILING";
                txtStatusFase.color = warnaKuning;
                _flowCurrentStep = 11;
                _targetFlow = 260f;
                _targetRPM = 0f;
                _pH = 7.5f;
                TriggerAlarm("DRY STACK AKTIF - CEK pH 8.5 DAN MOISTURE CAKE", false);
                break;

            case GameLevelManager.GameLevel.Level14_Emergency:
                txtStatusFase.text = "STATUS: DARURAT!";
                txtStatusFase.color = warnaMerah;
                OnDaruratDimulai();
                break;
        }

        if (level == GameLevelManager.GameLevel.Level6_AcidInjection)
            TriggerAlarm("LEVEL 6 - TEKAN DCS 6 UNTUK AUTHORIZE JALUR PRE-HEATER KE AUTOCLAVE", false);

        // 2. Update flow step berdasarkan level
        _flowCurrentStep = Mathf.Clamp(_flowCurrentStep, 0, _flowStepNames.Length - 1);
        UpdateFlowTracker();
    }

    private void OnDcsButtonPressed(int nomorTombol)
    {
        if (GameLevelManager.Instance == null)
            return;

        if (GameLevelManager.Instance.CurrentLevel == GameLevelManager.GameLevel.Level3_OreSlurry && nomorTombol == 3)
        {
            _mesinAktif = true;
            _flowCurrentStep = 2;
            _targetFlow = 120f;
            _rpm = Mathf.Max(_rpm, 18f);
            _flowRate = Mathf.Max(_flowRate, 90f);
            _nikel = Mathf.Max(_nikel, 12f);
            _efisiensi = Mathf.Max(_efisiensi, 8f);
            _kadarAsam = Mathf.Max(_kadarAsam, 5f);
            _waktuProses = 0f;
            SetValve(ref _valveSlurry, true);

            if (txtStatusMesin != null)
            {
                txtStatusMesin.text = "STARTING";
                txtStatusMesin.color = warnaBlue;
            }

            if (panelTaskMesin != null)
                panelTaskMesin.SetActive(true);

            SetTaskDone(taskMesinDCS);
            TriggerAlarm("Crusher dan slurry tank mulai beroperasi. Tunggu laporan HT operator.", false);
            UpdateSemuaTampilan();
            UpdateFlowTracker();
            return;
        }

        if (GameLevelManager.Instance.CurrentLevel == GameLevelManager.GameLevel.Level4_SlurryPump && nomorTombol == 4)
        {
            _mesinAktif = true;
            _flowCurrentStep = 3;
            _targetFlow = 450f;
            _rpm = Mathf.Max(_rpm, 28f);
            _flowRate = Mathf.Max(_flowRate, 180f);
            _nikel = Mathf.Max(_nikel, 18f);
            _efisiensi = Mathf.Max(_efisiensi, 14f);
            _kadarAsam = Mathf.Max(_kadarAsam, 6f);
            _waktuProses = 0f;
            SetValve(ref _valveSlurry, true);

            if (txtStatusMesin != null)
            {
                txtStatusMesin.text = "SLURRY PUMP ON";
                txtStatusMesin.color = warnaHijau;
            }

            if (panelTaskMesin != null)
                panelTaskMesin.SetActive(true);

            SetTaskDone(taskMesinDCS);
            TriggerAlarm("Slurry pump aktif. Atur flow rate ke 450 m3/h lalu kirim laporan HT.", false);
            SyncFlowRateKeLevelManager();
            UpdateSemuaTampilan();
            UpdateFlowTracker();
            return;
        }

        if (GameLevelManager.Instance.CurrentLevel == GameLevelManager.GameLevel.Level6_AcidInjection && nomorTombol == 6)
        {
            _mesinAktif = true;
            _flowCurrentStep = 5;
            _targetFlow = 430f;
            _rpm = Mathf.Max(_rpm, 18f);
            _flowRate = Mathf.Max(_flowRate, 220f);
            _kadarAsam = Mathf.Max(_kadarAsam, 8f);
            _waktuProses = 0f;
            SetValve(ref _valveSlurry, true);
            SetValve(ref _valveAcidFeed, false);

            if (txtStatusMesin != null)
            {
                txtStatusMesin.text = "ROUTE AUTHORIZED";
                txtStatusMesin.color = warnaKuning;
            }

            if (panelTaskMesin != null)
                panelTaskMesin.SetActive(true);

            SetTaskDone(taskMesinDCS);
            TriggerAlarm("Outlet pre-heater authorized. Lapor HT lalu buka valve field ke autoclave.", false);
            UpdateSemuaTampilan();
            UpdateFlowTracker();
        }
    }

    // ============================================================
    //  SIMULASI REAKTOR (Coroutine)
    // ============================================================
    IEnumerator SimulasiReaktor()
    {
        while (true)
        {
            yield return new WaitForSeconds(1.2f);

            if (_mesinAktif && !_daruratAktif)
            {
                // Parameter berfluktuasi mendekati target SOP
                _suhu = Mathf.Clamp(_suhu + Random.Range(-0.4f, 0.4f), _targetSuhu - 5f, _targetSuhu + 5f);
                _tekanan = Mathf.Clamp(_tekanan + Random.Range(-0.2f, 0.2f), _targetTekanan - 2f, _targetTekanan + 2f);
                _rpm = Mathf.Clamp(_rpm + Random.Range(-0.5f, 0.5f), _targetRPM - 3f, _targetRPM + 3f);
                if (GameLevelManager.Instance != null &&
                    GameLevelManager.Instance.CurrentLevel == GameLevelManager.GameLevel.Level4_SlurryPump)
                {
                    _flowRate = Mathf.Clamp(_flowRate, 0f, 600f);
                }
                else
                {
                    _flowRate = Mathf.Clamp(_flowRate + Random.Range(-2f, 2f), _targetFlow - 20f, _targetFlow + 20f);
                }
                if (GameLevelManager.Instance != null &&
                    (GameLevelManager.Instance.CurrentLevel == GameLevelManager.GameLevel.Level11_MHP ||
                     GameLevelManager.Instance.CurrentLevel == GameLevelManager.GameLevel.Level12_TailingDischarge ||
                     GameLevelManager.Instance.CurrentLevel == GameLevelManager.GameLevel.Level13_TailingWaste))
                {
                    _pH = Mathf.MoveTowards(_pH, GameLevelManager.Instance.PH, 0.35f);
                }
                else
                {
                    _pH = Mathf.Clamp(_pH + Random.Range(-0.02f, 0.02f), 0.7f, 1.3f);
                }
                _scaleLevel = Mathf.Clamp(_scaleLevel + Random.Range(-0.05f, 0.1f), 10f, 40f);

                _nikel = Mathf.Clamp(_nikel + Random.Range(-0.2f, 0.3f), 83f, 95f);
                _efisiensi = Mathf.Clamp(_efisiensi + Random.Range(-0.1f, 0.15f), 88f, 97f);
                _kadarAsam = Mathf.Clamp(_kadarAsam + Random.Range(-0.1f, 0.1f), 17f, 22f);
                _waktuProses += 1.2f / 60f;

                // Flow tracker: majukan setiap ~10 detik simulasi
                if (Time.frameCount % 500 == 0 && _flowCurrentStep < 8)
                    _flowCurrentStep++;

                // Trigger darurat otomatis saat Scale > 40% DAN tekanan > 65 Bar
                if (_scaleLevel > 40f && _tekanan > 65f && !_daruratAktif)
                {
                    if (GameLevelManager.Instance != null)
                        GameLevelManager.Instance.TriggerEmergency();
                }
            }
            else if (_daruratAktif)
            {
                // Saat darurat: tekanan terus naik tidak terkendali sampai ESD ditekan
                _tekanan += Random.Range(0.3f, 0.8f);
                _scaleLevel += Random.Range(0.2f, 0.5f);
            }
            else
            {
                // Mesin belum aktif — nilai idle
                _suhu = Mathf.MoveTowards(_suhu, 25f, 0.2f);
                _tekanan = Mathf.MoveTowards(_tekanan, 1f, 0.05f);
                _rpm = Mathf.MoveTowards(_rpm, 0f, 0.3f);
                // Saat Level 4, flow rate dikontrol DCSParameterControl — jangan decay
                if (GameLevelManager.Instance == null ||
                    GameLevelManager.Instance.CurrentLevel != GameLevelManager.GameLevel.Level4_SlurryPump)
                {
                    _flowRate = Mathf.MoveTowards(_flowRate, 0f, 1f);
                }
            }

            UpdateSemuaTampilan();
            UpdateValvePanel();
            UpdateFlowTracker();

            // Countdown ESD saat darurat
            if (_daruratAktif && !_esdSudahDitekan)
                UpdateCountdown();
        }
    }

    public void TambahFlowRate()
    {
        _flowRate = Mathf.Clamp(_flowRate + _langkahFlowRate, 0f, 600f);
        SyncFlowRateKeLevelManager();
        UpdateSemuaTampilan();
    }

    public void KurangiFlowRate()
    {
        _flowRate = Mathf.Clamp(_flowRate - _langkahFlowRate, 0f, 600f);
        SyncFlowRateKeLevelManager();
        UpdateSemuaTampilan();
    }

    // ============================================================
    //  DARURAT DIMULAI
    // ============================================================
    private void OnDaruratDimulai()
    {
        _daruratAktif = true;
        _countdownSisa = 45f;

        if (panelESD != null) panelESD.SetActive(true);
        if (panelAlarm != null) panelAlarm.SetActive(true);

        if (txtESDStatus != null)
        {
            txtESDStatus.text = "DARURAT! TEKAN ESD SEGERA!";
            txtESDStatus.color = warnaMerah;
        }

        if (txtAlarm != null) txtAlarm.text = "KONDISI DARURAT - TEKANAN KRITIS! SEGERA TEKAN ESD!";
        if (bgAlarm != null) bgAlarm.color = new Color(0.5f, 0.05f, 0.05f, 0.95f);
        _alarmAktif = true;
    }

    // ============================================================
    //  COUNTDOWN ESD
    // ============================================================
    private void UpdateCountdown()
    {
        _countdownSisa -= 1.2f;
        if (_countdownSisa < 0) _countdownSisa = 0f;

        if (txtCountdown != null)
            txtCountdown.text = $"WAKTU TERSISA: {Mathf.CeilToInt(_countdownSisa)}s";

        if (imgCountdownBar != null)
            imgCountdownBar.fillAmount = _countdownSisa / 45f;
    }

    // ============================================================
    //  ESD BUTTON
    // ============================================================
    public void TekanESD()
    {
        if (_esdSudahDitekan) return;
        _esdSudahDitekan = true;
        _daruratAktif = false;

        // Tutup semua valve saat emergency
        SetValve(ref _valveAcidFeed, false);
        SetValve(ref _valveSlurry, false);
        SetValve(ref _valveSteam, false);
        SetValve(ref _valveIsolation, true);  // Isolation valve BUKA untuk bypass tekanan

        if (txtESDStatus != null)
        {
            txtESDStatus.text = "ESD AKTIF - REAKTOR SHUTDOWN";
            txtESDStatus.color = warnaHijau;
        }

        if (btnESDBackground != null)
            btnESDBackground.color = warnaHijau;

        TriggerAlarm("EMERGENCY SHUTDOWN BERHASIL — Semua pompa berhenti. Reaktor aman.", true);

        // Selesaikan Level 14 (Emergency) via GameLevelManager
        if (GameLevelManager.Instance != null &&
            GameLevelManager.Instance.CurrentLevel == GameLevelManager.GameLevel.Level14_Emergency)
        {
            GameLevelManager.Instance.NotifyLevel14EsdPressed();
        }
    }

    // ============================================================
    //  VALVE HELPERS
    // ============================================================
    private void SetValve(ref bool valveState, bool buka)
    {
        valveState = buka;
        UpdateValvePanel();
    }

    private void ResetSemuaValve()
    {
        _valveSteam = false;
        _valveAcidFeed = false;
        _valveSlurry = false;
        _valveLetdown = false;
        _valveFlash = false;
        _valveIsolation = false;
    }

    void UpdateValvePanel()
    {
        SetValveText(txtValveSteam, "V-01 STEAM INJECT ", _valveSteam);
        SetValveText(txtValveAcidFeed, "V-02 ACID FEED    ", _valveAcidFeed);
        SetValveText(txtValveSlurryFeed, "V-03 SLURRY PUMP  ", _valveSlurry);
        SetValveText(txtValveLetdown, "V-04 LETDOWN      ", _valveLetdown);
        SetValveText(txtValveFlash, "V-05 FLASH VENT   ", _valveFlash);
        SetValveText(txtValveIsolation, "V-06 ISOLATION    ", _valveIsolation);
    }

    private void SetValveText(TextMeshProUGUI txt, string label, bool buka)
    {
        if (txt == null) return;
        txt.text = $"{label}  {(buka ? "[BUKA]" : "[TUTUP]")}";
        txt.color = buka ? warnaHijau : warnaAbu;
    }

    // ============================================================
    //  UPDATE FLOW TRACKER
    // ============================================================
    void UpdateFlowTracker()
    {
        if (flowStepIndicators == null) return;

        for (int i = 0; i < flowStepIndicators.Length; i++)
        {
            if (flowStepIndicators[i] == null) continue;
            int nodeIndex = i + 1;
            if (nodeIndex < _flowCurrentStep)
                flowStepIndicators[i].color = warnaNodeSelesai;
            else if (nodeIndex == _flowCurrentStep)
                flowStepIndicators[i].color = warnaNodeAktif;
            else
                flowStepIndicators[i].color = warnaNodeBelum;
        }

        if (txtFlowCurrentStep != null)
            txtFlowCurrentStep.text = $"CAIRAN DI: {(_flowCurrentStep < _flowStepNames.Length ? _flowStepNames[_flowCurrentStep] : "—")}";

        if (txtFlowProgress != null)
            txtFlowProgress.text = $"PROGRESS: {_flowCurrentStep}/{_flowStepNames.Length - 1} Titik";
    }

    private Color GetPHDisplayColor()
    {
        if (GameLevelManager.Instance == null)
            return _pH > 1.5f ? warnaKuning : warnaHijau;

        switch (GameLevelManager.Instance.CurrentLevel)
        {
            case GameLevelManager.GameLevel.Level11_MHP:
                return _pH >= 5.2f && _pH <= 5.8f ? warnaHijau : warnaKuning;
            case GameLevelManager.GameLevel.Level12_TailingDischarge:
                return _pH >= 7.1f && _pH <= 7.9f ? warnaHijau : warnaKuning;
            case GameLevelManager.GameLevel.Level13_TailingWaste:
                return _pH >= 8.0f && _pH <= 9.0f ? warnaHijau : warnaKuning;
            default:
                return _pH > 1.5f ? warnaKuning : warnaHijau;
        }
    }

    // ============================================================
    //  UPDATE SEMUA TAMPILAN
    // ============================================================
    void UpdateSemuaTampilan()
    {
        if (txtSuhu != null)
        {
            txtSuhu.text = $"{_suhu:F1} °C";
            txtSuhu.color = _suhu > 260f ? warnaMerah : (_suhu > 255f ? warnaKuning : warnaHijau);
        }

        if (txtTekanan != null)
        {
            txtTekanan.text = $"{_tekanan:F1} atm";
            txtTekanan.color = _tekanan > 55f ? warnaMerah : (_tekanan > 50f ? warnaKuning : warnaHijau);
        }

        if (txtPH != null)
        {
            txtPH.text = $"pH {_pH:F2}";
            txtPH.color = GetPHDisplayColor();
        }

        if (txtFlowRate != null)
            txtFlowRate.text = $"{_flowRate:F0} m³/h";

        if (txtRPM != null)
            txtRPM.text = $"RPM: {_rpm:F0}";

        if (txtScaleLevel != null)
        {
            txtScaleLevel.text = $"SCALE: {_scaleLevel:F1}%";
            txtScaleLevel.color = _scaleLevel > 35f ? warnaMerah : (_scaleLevel > 28f ? warnaKuning : warnaHijau);
        }

        if (txtKadarNikel != null)
            txtKadarNikel.text = _mesinAktif ? $"{_nikel:F1}%" : "-- %";

        if (txtEfisiensi != null)
        {
            txtEfisiensi.text = _mesinAktif ? $"{_efisiensi:F1}%" : "-- %";
            if (_mesinAktif)
                txtEfisiensi.color = _efisiensi > 90f ? warnaHijau : warnaKuning;
        }

        if (txtKadarAsam != null)
            txtKadarAsam.text = _mesinAktif ? $"{_kadarAsam:F1}%" : "-- %";

        if (txtWaktuProses != null)
            txtWaktuProses.text = _mesinAktif ? $"{_waktuProses:F1} min" : "0.0 min";

        if (txtStatusMesin != null && !_mesinAktif)
        {
            txtStatusMesin.text = "STANDBY";
            txtStatusMesin.color = warnaKuning;
        }

        if (txtWaktuShift != null)
            txtWaktuShift.text = $"SHIFT: {Mathf.FloorToInt(_waktuShift / 60f):00}:{Mathf.FloorToInt(_waktuShift % 60f):00}";

        SyncFlowRateKeLevelManager();
    }

    private void SyncFlowRateKeLevelManager()
    {
        if (GameLevelManager.Instance == null)
            return;

        if (GameLevelManager.Instance.CurrentLevel != GameLevelManager.GameLevel.Level4_SlurryPump)
            return;

        // Saat Level 4, DCSParameterControl yang mengontrol flow rate ke GLM.
        // DCSMonitorUI hanya membaca dari GLM untuk display, TIDAK override.
        _flowRate = GameLevelManager.Instance.FlowRate;
    }

    // ============================================================
    //  ALARM SYSTEM
    // ============================================================
    public void TriggerAlarm(string pesan, bool sukses)
    {
        if (panelAlarm == null) return;
        _alarmAktif = true;
        panelAlarm.SetActive(true);
        if (txtAlarm != null) txtAlarm.text = pesan;
        if (bgAlarm != null)
            bgAlarm.color = sukses
                ? new Color(0.08f, 0.42f, 0.12f, 0.92f)
                : new Color(0.06f, 0.18f, 0.42f, 0.92f);
        StartCoroutine(MatikanAlarm(5f));
    }

    IEnumerator MatikanAlarm(float delay)
    {
        yield return new WaitForSeconds(delay);
        if (!_daruratAktif && panelAlarm != null) panelAlarm.SetActive(false);
        if (!_daruratAktif) _alarmAktif = false;
    }

    IEnumerator KejapAlarm()
    {
        while (true)
        {
            if (_alarmAktif && bgAlarm != null)
            {
                Color c = bgAlarm.color;
                c.a = c.a > 0.5f ? 0.25f : 0.92f;
                bgAlarm.color = c;
            }
            yield return new WaitForSeconds(_daruratAktif ? 0.3f : 0.6f);
        }
    }

    IEnumerator UpdateWaktuShift()
    {
        while (true)
        {
            yield return new WaitForSeconds(1f);
            _waktuShift += 1f;
        }
    }

    // ============================================================
    //  TASK DONE HELPER
    // ============================================================
    public void SetTaskDone(TextMeshProUGUI txt)
    {
        if (txt == null) return;
        string t = txt.text;
        if (t.StartsWith("[ ]")) txt.text = "[OK]" + t.Substring(3);
        txt.color = warnaHijau;
    }
}
