using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System.Collections;

public class DCSMonitorUI : MonoBehaviour
{
    [Header("=== Panel Header ===")]
    public TextMeshProUGUI txtJudulMonitor;
    public TextMeshProUGUI txtStatusFase;

    [Header("=== Parameter Reaktor (Panel Kiri) ===")]
    public TextMeshProUGUI txtSuhu;
    public TextMeshProUGUI txtTekanan;
    public TextMeshProUGUI txtPH;
    public TextMeshProUGUI txtFlowRate;
    public TextMeshProUGUI txtRPM;
    public TextMeshProUGUI txtScaleLevel;

    [Header("=== Output Reaktor (Panel Kanan) ===")]
    public TextMeshProUGUI txtKadarNikel;
    public TextMeshProUGUI txtEfisiensi;
    public TextMeshProUGUI txtKadarAsam;
    public TextMeshProUGUI txtWaktuProses;
    public TextMeshProUGUI txtStatusMesin;

    [Header("=== Task Fase Mesin ===")]
    public GameObject panelTaskMesin;
    public TextMeshProUGUI taskScannerDCS;
    public TextMeshProUGUI taskMesinDCS;

    [Header("=== Alarm Panel ===")]
    public GameObject panelAlarm;
    public TextMeshProUGUI txtAlarm;
    public Image bgAlarm;

    [Header("=== Warna ===")]
    public Color warnaHijau = new Color(0.2f, 0.9f, 0.4f);
    public Color warnaKuning = new Color(1f, 0.85f, 0.1f);
    public Color warnaMerah = new Color(0.95f, 0.2f, 0.2f);
    public Color warnaBlue = new Color(0.3f, 0.8f, 1f);

    private float _suhu = 248f;
    private float _tekanan = 49f;
    private float _pH = 0.9f;
    private float _flowRate = 12.2f;
    private float _rpm = 45f;
    private float _scaleLevel = 22f;
    private float _nikel = 85f;
    private float _efisiensi = 91f;
    private float _kadarAsam = 18.5f;
    private float _waktuProses = 0f;

    private bool _mesinAktif = false;
    private bool _alarmAktif = false;

    void Start()
    {
        PhaseManager.OnPhaseChanged += OnFaseBerubah;
        PhaseManager.OnScannerPickedUp += OnScannerDiambil;
        PhaseManager.OnScannerInstalled += OnScannerDipasang;

        if (panelAlarm != null) panelAlarm.SetActive(false);
        if (panelTaskMesin != null) panelTaskMesin.SetActive(false);

        StartCoroutine(SimulasiNilaiReaktor());
        StartCoroutine(KejapAlarm());
        UpdateTampilan();
    }

    void OnDestroy()
    {
        PhaseManager.OnPhaseChanged -= OnFaseBerubah;
        PhaseManager.OnScannerPickedUp -= OnScannerDiambil;
        PhaseManager.OnScannerInstalled -= OnScannerDipasang;
    }

    void OnFaseBerubah(PhaseManager.SimulationPhase fase)
    {
        if (txtStatusFase == null) return;

        switch (fase)
        {
            case PhaseManager.SimulationPhase.PreparasiAPD:
                txtStatusFase.text = "STATUS: STANDBY - MENUNGGU OPERATOR";
                txtStatusFase.color = warnaKuning;
                break;

            case PhaseManager.SimulationPhase.OperasionalAlat:
                txtStatusFase.text = "STATUS: SIAP OPERASIONAL";
                txtStatusFase.color = warnaHijau;
                if (panelTaskMesin != null) panelTaskMesin.SetActive(true);
                TriggerAlarm("OPERATOR SIAP - MULAI PROSEDUR SCANNER", false);
                break;

            case PhaseManager.SimulationPhase.AktifMesin:
                txtStatusFase.text = "STATUS: REAKTOR AKTIF";
                txtStatusFase.color = warnaBlue;
                _mesinAktif = true;
                SetTaskDone(taskScannerDCS);
                TriggerAlarm("SCANNER TERPASANG - AKTIFKAN MESIN HPAL", false);
                if (txtStatusMesin != null)
                {
                    txtStatusMesin.text = "AKTIF";
                    txtStatusMesin.color = warnaHijau;
                }
                break;

            case PhaseManager.SimulationPhase.Selesai:
                txtStatusFase.text = "STATUS: PROSES SELESAI";
                txtStatusFase.color = warnaHijau;
                SetTaskDone(taskMesinDCS);
                TriggerAlarm("PROSES HPAL SELESAI - LAPORAN SIAP", true);
                break;
        }
    }

    void OnScannerDiambil()
    {
        TriggerAlarm("SCANNER DIAMBIL - PASANG KE SILINDER MERAH", false);
    }

    void OnScannerDipasang()
    {
        SetTaskDone(taskScannerDCS);
    }

    void SetTaskDone(TextMeshProUGUI txt)
    {
        if (txt == null) return;
        string t = txt.text;
        if (t.StartsWith("[ ]")) txt.text = "[OK]" + t.Substring(3);
        txt.color = warnaHijau;
    }

    IEnumerator SimulasiNilaiReaktor()
    {
        while (true)
        {
            _suhu += Random.Range(-0.4f, 0.4f);
            _suhu = Mathf.Clamp(_suhu, 245f, 255f);

            _tekanan += Random.Range(-0.2f, 0.2f);
            _tekanan = Mathf.Clamp(_tekanan, 47f, 53f);

            _pH += Random.Range(-0.02f, 0.02f);
            _pH = Mathf.Clamp(_pH, 0.7f, 1.3f);

            _flowRate += Random.Range(-0.1f, 0.1f);
            _flowRate = Mathf.Clamp(_flowRate, 11f, 14f);

            _rpm += Random.Range(-0.3f, 0.3f);
            _rpm = Mathf.Clamp(_rpm, 43f, 47f);

            _scaleLevel += Random.Range(-0.05f, 0.1f);
            _scaleLevel = Mathf.Clamp(_scaleLevel, 20f, 40f);

            if (_mesinAktif)
            {
                _nikel += Random.Range(-0.2f, 0.3f);
                _nikel = Mathf.Clamp(_nikel, 83f, 95f);

                _efisiensi += Random.Range(-0.1f, 0.15f);
                _efisiensi = Mathf.Clamp(_efisiensi, 88f, 97f);

                _kadarAsam += Random.Range(-0.1f, 0.1f);
                _kadarAsam = Mathf.Clamp(_kadarAsam, 17f, 22f);

                _waktuProses += 1.2f / 60f;
            }

            UpdateTampilan();
            yield return new WaitForSeconds(1.2f);
        }
    }

    void UpdateTampilan()
    {
        if (txtSuhu != null)
        {
            txtSuhu.text = $"{_suhu:F1} C";
            txtSuhu.color = _suhu > 265f ? warnaMerah : (_suhu > 258f ? warnaKuning : warnaHijau);
        }

        if (txtTekanan != null)
        {
            txtTekanan.text = $"{_tekanan:F1} Bar";
            txtTekanan.color = _tekanan > 60f ? warnaMerah : (_tekanan > 55f ? warnaKuning : warnaHijau);
        }

        if (txtPH != null)
        {
            txtPH.text = $"pH {_pH:F2}";
            txtPH.color = _pH > 1.5f ? warnaKuning : warnaHijau;
        }

        if (txtFlowRate != null)
            txtFlowRate.text = $"{_flowRate:F1} m3/h";

        if (txtRPM != null)
            txtRPM.text = $"RPM: {_rpm:F1}";

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
    }

    void TriggerAlarm(string pesan, bool sukses)
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
        if (panelAlarm != null) panelAlarm.SetActive(false);
        _alarmAktif = false;
    }

    IEnumerator KejapAlarm()
    {
        while (true)
        {
            if (_alarmAktif && bgAlarm != null)
            {
                Color c = bgAlarm.color;
                c.a = c.a > 0.5f ? 0.3f : 0.92f;
                bgAlarm.color = c;
            }
            yield return new WaitForSeconds(0.6f);
        }
    }
}
