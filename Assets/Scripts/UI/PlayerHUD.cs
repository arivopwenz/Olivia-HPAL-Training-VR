using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System.Collections;

public class PlayerHUD : MonoBehaviour
{
    [Header("=== Panel Header ===")]
    public TextMeshProUGUI txtFaseLabel;
    public Image bgHeader;

    [Header("=== Task APD ===")]
    public TextMeshProUGUI taskHelm;
    public TextMeshProUGUI taskRompi;
    public TextMeshProUGUI taskKacamata;
    public TextMeshProUGUI taskSepatu;

    [Header("=== Task Operasional ===")]
    public GameObject panelOperasional;
    public TextMeshProUGUI taskScannerAmbil;
    public TextMeshProUGUI taskScannerPasang;
    public TextMeshProUGUI taskMesinAktif;

    [Header("=== Notifikasi Bawah ===")]
    public GameObject panelNotif;
    public TextMeshProUGUI txtNotif;
    public Image bgNotif;

    private Color _cDone = new Color(0.2f, 0.95f, 0.45f);
    private Color _cTodo = new Color(0.65f, 0.65f, 0.65f);
    private Color _cActive = new Color(1f, 0.88f, 0.15f);
    private Color _cBlue = new Color(0.3f, 0.85f, 1f);

    void Start()
    {
        PhaseManager.OnPhaseChanged += OnFaseBerubah;
        PhaseManager.OnAllApdEquipped += OnSemuaApdSelesai;
        PhaseManager.OnApdItemWorn += OnSatuApdDipakai;
        PhaseManager.OnScannerPickedUp += OnScannerDiambil;
        PhaseManager.OnScannerInstalled += OnScannerDipasang;

        if (panelNotif != null) panelNotif.SetActive(false);
        if (panelOperasional != null) panelOperasional.SetActive(false);

        SetFaseLabel("FASE 1 : PEMAKAIAN APD", _cActive);
    }

    void OnDestroy()
    {
        PhaseManager.OnPhaseChanged -= OnFaseBerubah;
        PhaseManager.OnAllApdEquipped -= OnSemuaApdSelesai;
        PhaseManager.OnApdItemWorn -= OnSatuApdDipakai;
        PhaseManager.OnScannerPickedUp -= OnScannerDiambil;
        PhaseManager.OnScannerInstalled -= OnScannerDipasang;
    }

    void OnFaseBerubah(PhaseManager.SimulationPhase fase)
    {
        switch (fase)
        {
            case PhaseManager.SimulationPhase.OperasionalAlat:
                SetFaseLabel("FASE 2 : OPERASIONAL ALAT", _cBlue);
                if (panelOperasional != null) panelOperasional.SetActive(true);
                ShowNotif("APD Lengkap! Sekarang ambil Scanner.", false);
                break;

            case PhaseManager.SimulationPhase.AktifMesin:
                SetFaseLabel("FASE 3 : AKTIFKAN MESIN HPAL", _cBlue);
                ShowNotif("Scanner terpasang! Tekan tombol untuk aktifkan mesin.", false);
                break;

            case PhaseManager.SimulationPhase.Selesai:
                SetFaseLabel("SIMULASI SELESAI!", _cDone);
                SetTaskDone(taskMesinAktif);
                ShowNotif("Kerja bagus! Simulasi berhasil diselesaikan.", true);
                break;
        }
    }

    void OnSatuApdDipakai(string namaApd)
    {
        string n = namaApd.ToLower();
        if (n.Contains("helm")) SetTaskDone(taskHelm);
        else if (n.Contains("rompi")) SetTaskDone(taskRompi);
        else if (n.Contains("kacamata")) SetTaskDone(taskKacamata);
        else if (n.Contains("sepatu") || n.Contains("boots")) SetTaskDone(taskSepatu);
    }

    void OnSemuaApdSelesai()
    {
        SetTaskDone(taskHelm);
        SetTaskDone(taskRompi);
        SetTaskDone(taskKacamata);
        SetTaskDone(taskSepatu);
    }

    void OnScannerDiambil()
    {
        SetTaskDone(taskScannerAmbil);
        ShowNotif("Scanner diambil! Bawa ke silinder merah.", false);
    }

    void OnScannerDipasang()
    {
        SetTaskDone(taskScannerPasang);
    }

    void SetFaseLabel(string teks, Color warna)
    {
        if (txtFaseLabel == null) return;
        txtFaseLabel.text = teks;
        txtFaseLabel.color = warna;
        if (bgHeader != null)
            bgHeader.color = new Color(warna.r * 0.15f, warna.g * 0.15f, warna.b * 0.15f, 0.95f);
    }

    void SetTaskDone(TextMeshProUGUI txt)
    {
        if (txt == null) return;
        string t = txt.text;
        if (t.StartsWith("[ ]")) txt.text = "[OK]" + t.Substring(3);
        txt.color = _cDone;
    }

    void ShowNotif(string pesan, bool sukses)
    {
        if (panelNotif == null) return;
        StopCoroutine("HideNotif");
        panelNotif.SetActive(true);
        if (txtNotif != null) txtNotif.text = pesan;
        if (bgNotif != null)
            bgNotif.color = sukses
                ? new Color(0.08f, 0.45f, 0.12f, 0.95f)
                : new Color(0.06f, 0.18f, 0.42f, 0.95f);
        StartCoroutine("HideNotif");
    }

    IEnumerator HideNotif()
    {
        yield return new WaitForSeconds(4.5f);
        if (panelNotif != null) panelNotif.SetActive(false);
    }
}
