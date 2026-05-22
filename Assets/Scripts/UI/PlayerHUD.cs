using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System;
using System.Collections;
using System.Collections.Generic;

/// <summary>
/// OLIVIA VR - PlayerHUD.cs
/// HUD quest utama, panel APD, hint laporan HT, checklist operasional, dan fade transisi.
/// </summary>
public class PlayerHUD : MonoBehaviour
{
    [Serializable]
    private class HudTextUmum
    {
        [TextArea(2, 4)] public string tutorial = "<b>SELAMAT DATANG!</b>\n<size=83%>- Gerak: WASD\n- Ambil: G\n- Bicara: tahan T untuk HT</size>";
        [TextArea(2, 4)] public string pakaiApd = "<b>MISI: Pakai APD!</b>\n<size=83%>Lengkapi APD wajib sebelum keluar area.</size>";
        [TextArea(2, 4)] public string pakaiApdProgress = "<b>MISI: Pakai APD!</b>\n<size=83%>Lengkapi semua APD wajib sebelum keluar area.\n<color=#FFCC00>Progress: {0}/{1} item terpasang</color></size>";
        [TextArea(2, 4)] public string masukPintu = "<b>MISI: Keluar Loker!</b>\n<size=83%>Sentuh pintu keluar untuk lanjut ke area berikutnya.</size>";
    }

    [Serializable]
    private class HudTextLevel
    {
        public GameLevelManager.GameLevel level;
        public string labelLevel;
        [TextArea(2, 4)] public string lihatDcs;
        [TextArea(2, 4)] public string mulaiMesin;
        [TextArea(2, 4)] public string laporHt;
        [TextArea(2, 4)] public string gunakanWt;
    }

    [Serializable]
    private class HudTextLevel3
    {
        [TextArea(2, 4)] public string klikTombolDcs3 = "<b>MISI: Klik tombol DCS 3</b>\n<size=83%>Mulai alur awal ore ke slurry dari panel DCS.</size>";
        [TextArea(2, 4)] public string laporanAwal = "<b>MISI: Kirim laporan HT awal</b>\n<size=83%>Setelah tombol 3 ditekan, kirim perintah radio untuk memulai alur ore ke slurry tank.</size>";
        [TextArea(2, 4)] public string tungguTransisi = "<b>MISI: Bersiap ke area crusher</b>\n<size=83%>Laporan awal diterima. Tunggu transisi ke area mesin.</size>";
        [TextArea(2, 4)] public string pakaiApdLapangan = "<b>MISI: Pakai APD lapangan</b>\n<size=83%>Sebelum turun ke crusher/slurry, pakai kacamata pelindung dan respirator. Walkie Talkie tetap dibawa.</size>";
        [TextArea(2, 4)] public string observasiOreAir = "<b>MISI: Amati ore dan air masuk</b>\n<size=83%>Tunggu ore/laterit benar-benar mencapai slurry tank, lalu amati pengisian tank.</size>";
        [TextArea(2, 4)] public string observasiSlurry = "<b>MISI: Amati slurry tank terisi</b>\n<size=83%>Ore sudah masuk. Perhatikan level cairan naik sampai menyentuh batas 50%.</size>";
        [TextArea(2, 4)] public string laporanAkhir = "<b>MISI: Kirim laporan HT akhir</b>\n<size=83%>Laporkan bahwa ore sudah masuk ke slurry tank dan level cairan mencapai 50%.</size>";
        [TextArea(2, 4)] public string kembaliKeDcs = "<b>MISI: Kembali ke DCS</b>\n<size=83%>Laporan akhir diterima. Bersiap untuk transisi ke Level 4.</size>";
    }

    [Header("=== Panel Header ===")]
    public TextMeshProUGUI txtLevelLabel;
    public TextMeshProUGUI txtQuestLabel;
    public Image bgHeader;

    [Header("=== Checklist APD (8 Item) ===")]
    public TextMeshProUGUI taskHelm;
    public TextMeshProUGUI taskRompi;
    public TextMeshProUGUI taskKacamata;
    public TextMeshProUGUI taskSepatu;
    public TextMeshProUGUI taskSarungTangan;
    public TextMeshProUGUI taskRespirator;
    public TextMeshProUGUI taskEarplug;
    public TextMeshProUGUI taskWalkieTalkie;

    [Header("=== Panel Operasional ===")]
    public GameObject panelOperasional;
    public TextMeshProUGUI txtParameterInfo;

    [Header("=== Panel Walkie Talkie Hint ===")]
    public GameObject panelWalkieTalkieHint;
    public TextMeshProUGUI txtHintKataKunci;

    [Header("=== Notifikasi Bawah ===")]
    public GameObject panelNotif;
    public TextMeshProUGUI txtNotif;
    public Image bgNotif;

    [Header("=== Teks Quest Dinamis (Bisa Kamu Edit) ===")]
    [SerializeField] private bool _gunakanTeksQuestDinamis = true;
    [SerializeField] private HudTextUmum _teksUmum = new HudTextUmum();
    [SerializeField] private List<HudTextLevel> _teksPerLevel = new List<HudTextLevel>();
    [SerializeField] private HudTextLevel3 _teksLevel3 = new HudTextLevel3();

    private readonly Color _cDone = new Color(0.2f, 0.95f, 0.45f);
    private readonly Color _cBlue = new Color(0.3f, 0.85f, 1f);

    private enum FaseQuest
    {
        Tutorial,
        PakaiAPD,
        MasukPintu,
        LihatDCS,
        MulaiMesin,
        LaporHT,
        GunakanWT
    }

    private FaseQuest _faseSekarang = FaseQuest.Tutorial;
    private int _apdTerpasang;
    private const int TOTAL_APD = 8;
    private GameLevelManager.GameLevel _levelAktif = GameLevelManager.GameLevel.Level0_Tutorial;
    private bool _dcsDilihat;
    private bool _dcsTombolDitekan;
    private bool _voiceReportSelesai;
    private bool _level3LaporanAwalSelesai;
    private bool _level3OreSampaiSlurry;
    private bool _level3Slurry25Tercapai;

    private RectTransform _questRect;
    private RectTransform _operasionalRect;
    private RectTransform _walkieHintRect;
    private RectTransform _apdRect;
    private Image _transitionOverlay;
    private Coroutine _transitionCoroutine;

    private void OnValidate()
    {
        IsiTeksPerLevelDefaultJikaKosong();
    }

    private void Start()
    {
        IsiTeksPerLevelDefaultJikaKosong();

        GameLevelManager.OnLevelStarted += OnLevelMulai;
        GameLevelManager.OnLevelComplete += OnLevelSelesai;
        GameLevelManager.OnDCSViewConfirmed += OnDcsViewed;
        GameLevelManager.OnDCSButtonPressed += OnDcsButtonPressed;
        GameLevelManager.OnVoiceReportAccepted += OnVoiceReportAccepted;
        GameLevelManager.OnLevelTransitionRequested += OnLevelTransitionRequested;
        GameLevelManager.OnLevel3PhaseChanged += OnLevel3PhaseChanged;
        GameLevelManager.OnLevel3OreReachedSlurry += OnLevel3OreReachedSlurry;
        GameLevelManager.OnLevel4PhaseChanged += OnLevel4PhaseChanged;
        PhaseManager.OnApdItemWorn += OnSatuApdDipakai;
        PhaseManager.OnApdItemRemoved += OnSatuApdDilepas;
        PhaseManager.OnAPD7Lengkap += OnSemuaApdLengkap;
        WalkieTalkieManager.OnPTTDitekan += OnPTTPress;
        WalkieTalkieManager.OnPTTDilepas += OnPTTRelease;

        CacheAndFixLayout();
        SetFase(FaseQuest.Tutorial);

        if (GameLevelManager.Instance != null)
            OnLevelMulai(GameLevelManager.Instance.CurrentLevel);
    }

    private void OnDestroy()
    {
        GameLevelManager.OnLevelStarted -= OnLevelMulai;
        GameLevelManager.OnLevelComplete -= OnLevelSelesai;
        GameLevelManager.OnDCSViewConfirmed -= OnDcsViewed;
        GameLevelManager.OnDCSButtonPressed -= OnDcsButtonPressed;
        GameLevelManager.OnVoiceReportAccepted -= OnVoiceReportAccepted;
        GameLevelManager.OnLevelTransitionRequested -= OnLevelTransitionRequested;
        GameLevelManager.OnLevel3PhaseChanged -= OnLevel3PhaseChanged;
        GameLevelManager.OnLevel3OreReachedSlurry -= OnLevel3OreReachedSlurry;
        GameLevelManager.OnLevel4PhaseChanged -= OnLevel4PhaseChanged;
        PhaseManager.OnApdItemWorn -= OnSatuApdDipakai;
        PhaseManager.OnApdItemRemoved -= OnSatuApdDilepas;
        PhaseManager.OnAPD7Lengkap -= OnSemuaApdLengkap;
        WalkieTalkieManager.OnPTTDitekan -= OnPTTPress;
        WalkieTalkieManager.OnPTTDilepas -= OnPTTRelease;
    }

    private void OnLevelMulai(GameLevelManager.GameLevel level)
    {
        _levelAktif = level;
        int idx = (int)level;
        Color warnaHeader = level == GameLevelManager.GameLevel.Level14_Emergency ? new Color(1f, 0.2f, 0.2f) : _cBlue;
        SetLevelLabel(GetLabelLevel(level), warnaHeader);

        if (level == GameLevelManager.GameLevel.Level0_Tutorial)
        {
            SetFase(FaseQuest.Tutorial);
        }
        else if (level == GameLevelManager.GameLevel.Level1_APD)
        {
            SetFase(FaseQuest.PakaiAPD);
        }
        else
        {
            _dcsDilihat = false;
            _dcsTombolDitekan = false;
            _voiceReportSelesai = false;
            _level3LaporanAwalSelesai = false;
            _level3OreSampaiSlurry = false;
            _level3Slurry25Tercapai = false;
            SetFase(level == GameLevelManager.GameLevel.Level2_DCSPrep ? FaseQuest.LihatDCS : FaseQuest.MulaiMesin);
            UpdateOperasionalChecklist(level);
            if (level == GameLevelManager.GameLevel.Level3_OreSlurry)
                RefreshLevel3Hud();
        }

        ShowNotif($"Level {idx} dimulai!", false);
    }

    private void OnLevelSelesai(GameLevelManager.GameLevel level, int skor)
    {
        ShowNotif($"Level {(int)level} selesai! Skor: {skor}/100", true);
    }

    private void OnSatuApdDipakai(string namaApd)
    {
        string n = namaApd.ToLowerInvariant();
        if (n.Contains("helm")) SetTaskDone(taskHelm);
        else if (n.Contains("rompi")) SetTaskDone(taskRompi);
        else if (n.Contains("kacamata")) SetTaskDone(taskKacamata);
        else if (n.Contains("sepatu")) SetTaskDone(taskSepatu);
        else if (n.Contains("sarung")) SetTaskDone(taskSarungTangan);
        else if (n.Contains("respirator")) SetTaskDone(taskRespirator);
        else if (n.Contains("earplug")) SetTaskDone(taskEarplug);
        else if (n.Contains("walkie") || n.Contains("ht")) SetTaskDone(taskWalkieTalkie);

        _apdTerpasang = Mathf.Clamp(_apdTerpasang + 1, 0, TOTAL_APD);
        ShowNotif($"{namaApd} terpasang! ({_apdTerpasang}/{TOTAL_APD})", false);

        if (_faseSekarang == FaseQuest.PakaiAPD)
            SetQuestLabel(FormatQuest(Teks(_teksUmum.pakaiApdProgress, "<b>MISI: Pakai APD!</b>\n<size=83%>Lengkapi semua APD wajib sebelum keluar area.\n<color=#FFCC00>Progress: {0}/{1} item terpasang</color></size>"), _apdTerpasang, TOTAL_APD));

        if (_levelAktif == GameLevelManager.GameLevel.Level3_OreSlurry)
        {
            RefreshLevel3Hud();
            UpdateOperasionalChecklist(_levelAktif);
        }
    }

    private void OnSatuApdDilepas(string namaApd)
    {
        string n = namaApd.ToLowerInvariant();
        if (n.Contains("respirator"))
            SetTaskPending(taskRespirator);

        _apdTerpasang = Mathf.Clamp(_apdTerpasang - 1, 0, TOTAL_APD);
        ShowNotif($"{namaApd} dilepas / disimpan.", false);

        if (_levelAktif == GameLevelManager.GameLevel.Level3_OreSlurry)
        {
            RefreshLevel3Hud();
            UpdateOperasionalChecklist(_levelAktif);
        }
    }

    private void OnSemuaApdLengkap()
    {
        // Hanya jalan saat Level 1 (APD). Saat masker re-equip ke dada di Level 2+,
        // callback ini bisa ter-trigger ulang dan menyebabkan HUD reset ke "Keluar Loker".
        var glmLevel = GameLevelManager.Instance != null ? GameLevelManager.Instance.CurrentLevel : _levelAktif;
        if (glmLevel != GameLevelManager.GameLevel.Level1_APD)
            return;
        if (_levelAktif != GameLevelManager.GameLevel.Level1_APD)
            return;

        SetTaskDone(taskHelm);
        SetTaskDone(taskRompi);
        SetTaskDone(taskKacamata);
        SetTaskDone(taskSepatu);
        SetTaskDone(taskSarungTangan);
        SetTaskDone(taskRespirator);
        SetTaskDone(taskEarplug);
        SetTaskDone(taskWalkieTalkie);
        SetFase(FaseQuest.MasukPintu);
        ShowNotif("Semua APD lengkap! Sekarang masuk ke pintu loker.", true);
    }

    private void OnPTTPress()
    {
        if (txtNotif != null)
            txtNotif.text = "BERBICARA... (lepas tombol setelah laporan selesai)";

        if (panelNotif != null)
            panelNotif.SetActive(true);

        if (bgNotif != null)
            bgNotif.color = new Color(0.5f, 0.1f, 0.05f, 0.95f);
    }

    private void OnPTTRelease()
    {
        StartCoroutine(HideNotifDelayed(2f));
    }

    private void OnDcsViewed(GameLevelManager.GameLevel level)
    {
        if (level != _levelAktif || level != GameLevelManager.GameLevel.Level2_DCSPrep)
            return;

        _dcsDilihat = true;
        SetFase(FaseQuest.MulaiMesin);
        UpdateOperasionalChecklist(level);
    }

    private void OnDcsButtonPressed(int nomorTombol)
    {
        if (_levelAktif <= GameLevelManager.GameLevel.Level1_APD || GameLevelManager.Instance == null)
            return;

        if (!GameLevelManager.Instance.TryGetLevelData(_levelAktif, out var data))
            return;

        if (data.nomorTombolDCS != nomorTombol)
            return;

        _dcsTombolDitekan = true;
        SetFase(FaseQuest.LaporHT);
        if (_levelAktif == GameLevelManager.GameLevel.Level3_OreSlurry)
            RefreshLevel3Hud();
        UpdateOperasionalChecklist(_levelAktif);
    }

    private void OnVoiceReportAccepted(string keyword)
    {
        if (_levelAktif <= GameLevelManager.GameLevel.Level1_APD)
            return;

        if (_levelAktif == GameLevelManager.GameLevel.Level3_OreSlurry)
        {
            RefreshLevel3Hud();
            UpdateOperasionalChecklist(_levelAktif);
            return;
        }

        _voiceReportSelesai = true;
        UpdateOperasionalChecklist(_levelAktif);
    }

    private void OnLevelTransitionRequested(GameLevelManager.GameLevel fromLevel, GameLevelManager.GameLevel toLevel, float duration)
    {
        if (_transitionOverlay == null)
            return;

        if (_transitionCoroutine != null)
            StopCoroutine(_transitionCoroutine);

        _transitionCoroutine = StartCoroutine(PlayTransitionFade(duration));
    }

    private void OnLevel3PhaseChanged(GameLevelManager.Level3Phase phase)
    {
        if (_levelAktif != GameLevelManager.GameLevel.Level3_OreSlurry)
            return;

        _level3LaporanAwalSelesai = phase >= GameLevelManager.Level3Phase.LaporanAwalDiterima;
        if (phase < GameLevelManager.Level3Phase.ObservasiLapangan)
            _level3OreSampaiSlurry = false;
        _level3Slurry25Tercapai = phase >= GameLevelManager.Level3Phase.SiapLaporanAkhir;
        _voiceReportSelesai = phase >= GameLevelManager.Level3Phase.Selesai;

        RefreshLevel3Hud();
        UpdateOperasionalChecklist(_levelAktif);
    }

    private void OnLevel3OreReachedSlurry()
    {
        if (_levelAktif != GameLevelManager.GameLevel.Level3_OreSlurry)
            return;

        _level3OreSampaiSlurry = true;
        RefreshLevel3Hud();
        UpdateOperasionalChecklist(_levelAktif);
    }

    private void OnLevel4PhaseChanged(GameLevelManager.Level4Phase phase)
    {
        if (_levelAktif != GameLevelManager.GameLevel.Level4_SlurryPump)
            return;

        // Refresh checklist setiap phase change supaya item ke-centang
        UpdateOperasionalChecklist(_levelAktif);

        switch (phase)
        {
            case GameLevelManager.Level4Phase.MenungguTombolDcs:
                SetFase(FaseQuest.MulaiMesin);
                SetQuestLabel("<b>MISI: Klik tombol DCS 4</b>\n<size=83%>Tekan tombol DCS 4 yang menyala untuk menyalakan slurry pump.</size>");
                break;

            case GameLevelManager.Level4Phase.AturFlowRate:
                SetFase(FaseQuest.MulaiMesin);
                SetQuestLabel("<b>MISI: Atur flow rate 450 m³/h</b>\n<size=83%>Gunakan tombol [+] / [−] di panel Flow Rate sampai mencapai 450 m³/h.</size>");
                ShowNotif("Slurry pump menyala. Atur flow rate ke 450 m³/h.", false);
                break;

            // Setelah flow=450 tercapai → minta lapor HT AWAL di DCS
            case GameLevelManager.Level4Phase.MenungguLaporanFlow:
                SetFase(FaseQuest.LaporHT);
                SetQuestLabel("<b>MISI: Lapor HT awal</b>\n<size=83%>Flow rate 450 m³/h tercapai. Tahan T dan ucapkan: \"slurry pump aktif\".</size>");
                ShowNotif("Flow tercapai! Lapor HT 'slurry pump aktif'.", true);
                break;

            // Lapor awal diterima → teleport ke field, animasi liquid mengalir
            case GameLevelManager.Level4Phase.ObservasiPump:
                SetFase(FaseQuest.MulaiMesin);
                SetQuestLabel("<b>MISI: Amati aliran slurry</b>\n<size=83%>Lihat slurry mengalir dari Slurry Tank menuju Pre-Heater.</size>");
                ShowNotif("Memantau aliran slurry...", false);
                break;

            case GameLevelManager.Level4Phase.ObservasiPreheater:
                SetFase(FaseQuest.MulaiMesin);
                SetQuestLabel("<b>MISI: Slurry sampai Pre-Heater</b>\n<size=83%>Cairan telah mencapai unit pre-heater. Tunggu prompt untuk lapor HT akhir.</size>");
                break;

            // Liquid sudah sampai preheater → minta lapor HT AKHIR
            case GameLevelManager.Level4Phase.MenungguLaporanAkhir:
                SetFase(FaseQuest.LaporHT);
                SetQuestLabel("<b>MISI: Lapor HT akhir</b>\n<size=83%>Slurry telah mencapai pre-heater. Tahan T dan ucapkan: \"cairan sudah di preheater\".</size>");
                ShowNotif("Slurry sampai Pre-Heater! Lapor HT akhir.", true);
                break;

            case GameLevelManager.Level4Phase.KembaliKeDcs:
                SetFase(FaseQuest.MulaiMesin);
                SetQuestLabel("<b>MISI: Kembali ke DCS</b>\n<size=83%>Lapor diterima. Kembali ke ruang DCS untuk Level 5: Autoclave.</size>");
                ShowNotif("Mantap. Kembali ke DCS untuk Level 5.", true);
                break;

            case GameLevelManager.Level4Phase.Selesai:
                ShowNotif("Level 4 selesai! Slurry pump dan pre-heater operasional.", true);
                break;
        }
    }

    private void SetFase(FaseQuest fase)
    {
        _faseSekarang = fase;
        GameObject apdContainer = taskHelm != null ? taskHelm.transform.parent.gameObject : null;
        bool showApd = false;
        bool showOps = fase == FaseQuest.LihatDCS || fase == FaseQuest.MulaiMesin || fase == FaseQuest.LaporHT;
        bool showHint = fase == FaseQuest.LaporHT || fase == FaseQuest.GunakanWT;

        if (panelOperasional != null) panelOperasional.SetActive(showOps);
        if (panelWalkieTalkieHint != null) panelWalkieTalkieHint.SetActive(showHint);

        switch (fase)
        {
            case FaseQuest.Tutorial:
                SetQuestArea(98f, 110f);
                SetApdLayout(false);
                SetHintLayout();
                SetQuestLabel(Teks(_teksUmum.tutorial, "<b>SELAMAT DATANG!</b>\n<size=83%>- Gerak: WASD\n- Ambil: G\n- Bicara: tahan T untuk HT</size>"));
                break;

            case FaseQuest.PakaiAPD:
                showApd = true;
                SetQuestArea(98f, 96f);
                SetApdLayout(true);
                SetHintLayout();
                SetQuestLabel(Teks(_teksUmum.pakaiApd, "<b>MISI: Pakai APD!</b>\n<size=83%>Lengkapi APD wajib sebelum keluar area.</size>"));
                break;

            case FaseQuest.MasukPintu:
                SetQuestArea(98f, 96f);
                SetApdLayout(false);
                SetHintLayout();
                SetQuestLabel(Teks(_teksUmum.masukPintu, "<b>MISI: Keluar Loker!</b>\n<size=83%>Sentuh pintu keluar untuk lanjut ke area berikutnya.</size>"));
                break;

            case FaseQuest.LihatDCS:
                SetQuestArea(98f, 96f);
                SetApdLayout(false);
                SetHintLayout();
                SetQuestLabel(GetTeksFase(_levelAktif, fase, "<b>MISI: Lihat mesin DCS</b>\n<size=83%>Dekati area DCS untuk memulai langkah berikutnya.</size>"));
                break;

            case FaseQuest.MulaiMesin:
                SetQuestArea(98f, 96f);
                SetApdLayout(false);
                SetHintLayout();
                SetQuestLabel(GetTeksFase(_levelAktif, fase, "<b>MISI: Klik tombol DCS aktif</b>\n<size=83%>Tekan tombol DCS yang menyala untuk memulai operasi.</size>"));
                break;

            case FaseQuest.LaporHT:
                SetQuestArea(98f, 96f);
                SetApdLayout(false);
                SetHintLayout();
                SetQuestLabel(GetTeksFase(_levelAktif, fase, "<b>MISI: Lapor lewat HT</b>\n<size=83%>Tahan <b>T</b>, sampaikan laporan lengkap, lalu lepas tombol untuk kirim.</size>"));
                UpdateHintKataKunci(_levelAktif);
                break;

            case FaseQuest.GunakanWT:
                SetQuestArea(98f, 96f);
                SetApdLayout(false);
                SetHintLayout();
                SetQuestLabel(GetTeksFase(_levelAktif, fase, "<b>MISI: Kirim laporan HT</b>\n<size=83%>Sampaikan laporan lengkap seperti komunikasi radio asli.</size>"));
                UpdateHintKataKunci(_levelAktif);
                break;
        }

        if (apdContainer != null)
            apdContainer.SetActive(showApd);
    }

    private void UpdateHintKataKunci(GameLevelManager.GameLevel level)
    {
        if (txtHintKataKunci == null)
            return;

        string laporan = GameLevelManager.Instance != null
            ? GameLevelManager.Instance.GetLaporanVoiceDisplay(level)
            : string.Empty;

        txtHintKataKunci.text = string.IsNullOrWhiteSpace(laporan)
            ? "Laporan HT belum tersedia."
            : $"<b>LAPORAN HT:</b>\n\"{laporan}\"";

        txtHintKataKunci.textWrappingMode = TextWrappingModes.Normal;
        txtHintKataKunci.overflowMode = TextOverflowModes.Overflow;
        txtHintKataKunci.alignment = TextAlignmentOptions.TopLeft;
    }

    private void RefreshLevel3Hud()
    {
        if (_levelAktif != GameLevelManager.GameLevel.Level3_OreSlurry || GameLevelManager.Instance == null)
            return;

        switch (GameLevelManager.Instance.CurrentLevel3Phase)
        {
            case GameLevelManager.Level3Phase.MenungguTombolDcs:
                SetFase(FaseQuest.MulaiMesin);
                SetQuestLabel(Teks(_teksLevel3.klikTombolDcs3, "<b>MISI: Klik tombol DCS 3</b>\n<size=83%>Mulai alur awal ore ke slurry dari panel DCS.</size>"));
                break;

            case GameLevelManager.Level3Phase.MenungguLaporanAwal:
                SetFase(FaseQuest.LaporHT);
                SetQuestLabel(Teks(_teksLevel3.laporanAwal, "<b>MISI: Kirim laporan HT awal</b>\n<size=83%>Setelah tombol 3 ditekan, kirim perintah radio untuk memulai alur ore ke slurry tank.</size>"));
                break;

            case GameLevelManager.Level3Phase.LaporanAwalDiterima:
                SetFase(FaseQuest.LaporHT);
                if (panelWalkieTalkieHint != null)
                    panelWalkieTalkieHint.SetActive(false);
                bool apdLapanganSiap = PhaseManager.Instance == null || PhaseManager.Instance.Level3FieldApdLengkap;
                SetQuestLabel(apdLapanganSiap
                    ? Teks(_teksLevel3.tungguTransisi, "<b>MISI: Bersiap ke area crusher</b>\n<size=83%>Laporan awal diterima. Tunggu transisi ke area mesin.</size>")
                    : Teks(_teksLevel3.pakaiApdLapangan, "<b>MISI: Pakai APD lapangan</b>\n<size=83%>Sebelum turun ke crusher/slurry, pakai kacamata pelindung dan respirator. Walkie Talkie tetap dibawa.</size>"));
                break;

            case GameLevelManager.Level3Phase.ObservasiLapangan:
                SetFase(FaseQuest.MulaiMesin);
                if (panelWalkieTalkieHint != null)
                    panelWalkieTalkieHint.SetActive(false);
                SetQuestLabel(_level3OreSampaiSlurry
                    ? Teks(_teksLevel3.observasiSlurry, "<b>MISI: Amati slurry tank terisi</b>\n<size=83%>Ore sudah masuk. Perhatikan level cairan naik sampai menyentuh batas 50%.</size>")
                    : Teks(_teksLevel3.observasiOreAir, "<b>MISI: Amati ore dan air masuk</b>\n<size=83%>Tunggu ore/laterit benar-benar mencapai slurry tank, lalu amati pengisian tank.</size>"));
                break;

            case GameLevelManager.Level3Phase.SiapLaporanAkhir:
                SetFase(FaseQuest.LaporHT);
                SetQuestLabel(Teks(_teksLevel3.laporanAkhir, "<b>MISI: Kirim laporan HT akhir</b>\n<size=83%>Laporkan bahwa ore sudah masuk ke slurry tank dan level cairan mencapai 50%.</size>"));
                break;

            case GameLevelManager.Level3Phase.Selesai:
                SetFase(FaseQuest.LaporHT);
                if (panelWalkieTalkieHint != null)
                    panelWalkieTalkieHint.SetActive(false);
                SetQuestLabel(Teks(_teksLevel3.kembaliKeDcs, "<b>MISI: Kembali ke DCS</b>\n<size=83%>Laporan akhir diterima. Bersiap untuk transisi ke Level 4.</size>"));
                break;
        }
    }

    public void NotifyMasukPintu()
    {
        if (_faseSekarang == FaseQuest.MasukPintu)
            SetFase(FaseQuest.GunakanWT);
    }

    public void ShowNotifPublic(string pesan)
    {
        ShowNotif(pesan, false);
    }

    public void PlayManualFade(float totalDuration)
    {
        if (_transitionOverlay == null)
            return;

        if (_transitionCoroutine != null)
            StopCoroutine(_transitionCoroutine);

        _transitionCoroutine = StartCoroutine(PlayTransitionFade(totalDuration));
    }

    private void IsiTeksPerLevelDefaultJikaKosong()
    {
        if (_teksUmum == null)
            _teksUmum = new HudTextUmum();

        if (_teksLevel3 == null)
            _teksLevel3 = new HudTextLevel3();

        if (_teksPerLevel == null)
            _teksPerLevel = new List<HudTextLevel>();

        if (_teksPerLevel.Count > 0)
            return;

        for (int i = 0; i <= 14; i++)
        {
            var level = (GameLevelManager.GameLevel)i;
            _teksPerLevel.Add(new HudTextLevel
            {
                level = level,
                labelLevel = GetLabelLevelDefault(level),
                lihatDcs = "<b>MISI: Lihat mesin DCS</b>\n<size=83%>Dekati area DCS untuk memulai langkah berikutnya.</size>",
                mulaiMesin = $"<b>MISI: Klik tombol DCS {i}</b>\n<size=83%>Tekan tombol DCS yang menyala untuk memulai operasi.</size>",
                laporHt = "<b>MISI: Lapor lewat HT</b>\n<size=83%>Tahan <b>T</b>, sampaikan laporan lengkap, lalu lepas tombol untuk kirim.</size>",
                gunakanWt = "<b>MISI: Kirim laporan HT</b>\n<size=83%>Sampaikan laporan lengkap seperti komunikasi radio asli.</size>"
            });
        }
    }

    private string GetLabelLevel(GameLevelManager.GameLevel level)
    {
        HudTextLevel teksLevel = GetTeksLevel(level);
        return Teks(teksLevel != null ? teksLevel.labelLevel : string.Empty, GetLabelLevelDefault(level));
    }

    private string GetLabelLevelDefault(GameLevelManager.GameLevel level)
    {
        string[] namaLevel =
        {
            "LEVEL 0 - TUTORIAL", "LEVEL 1 - APD SAFETY", "LEVEL 2 - DCS PREPARATION",
            "LEVEL 3 - ORE & SLURRY", "LEVEL 4 - SLURRY PUMP", "LEVEL 5 - STEAM VALVE",
            "LEVEL 6 - ACID INJECTION", "LEVEL 7 - AUTOCLAVE", "LEVEL 8 - MONITORING DCS",
            "LEVEL 9 - FLASH VESSEL", "LEVEL 10 - CCD", "LEVEL 11 - MHP SAMPLING",
            "LEVEL 12 - TAILING DISCHARGE", "LEVEL 13 - TAILING WASTE", "LEVEL 14 - DARURAT K3"
        };

        int idx = (int)level;
        return idx >= 0 && idx < namaLevel.Length ? namaLevel[idx] : level.ToString();
    }

    private HudTextLevel GetTeksLevel(GameLevelManager.GameLevel level)
    {
        if (_teksPerLevel == null)
            return null;

        for (int i = 0; i < _teksPerLevel.Count; i++)
        {
            if (_teksPerLevel[i] != null && _teksPerLevel[i].level == level)
                return _teksPerLevel[i];
        }

        return null;
    }

    private string GetTeksFase(GameLevelManager.GameLevel level, FaseQuest fase, string fallback)
    {
        HudTextLevel teksLevel = GetTeksLevel(level);
        if (teksLevel == null)
            return fallback;

        switch (fase)
        {
            case FaseQuest.LihatDCS:
                return Teks(teksLevel.lihatDcs, fallback);
            case FaseQuest.MulaiMesin:
                return Teks(teksLevel.mulaiMesin, fallback);
            case FaseQuest.LaporHT:
                return Teks(teksLevel.laporHt, fallback);
            case FaseQuest.GunakanWT:
                return Teks(teksLevel.gunakanWt, fallback);
            default:
                return fallback;
        }
    }

    private string Teks(string teksInspector, string fallback)
    {
        if (!_gunakanTeksQuestDinamis || string.IsNullOrWhiteSpace(teksInspector))
            return fallback;

        return teksInspector;
    }

    private string FormatQuest(string format, params object[] args)
    {
        try
        {
            return string.Format(format, args);
        }
        catch (FormatException)
        {
            return format;
        }
    }

    private void UpdateOperasionalChecklist(GameLevelManager.GameLevel level)
    {
        if (txtParameterInfo == null || GameLevelManager.Instance == null)
            return;

        if (!GameLevelManager.Instance.TryGetLevelData(level, out var data))
            return;

        var lines = new List<string>();
        if (level == GameLevelManager.GameLevel.Level3_OreSlurry)
        {
            bool apdLapanganSiap = PhaseManager.Instance == null || PhaseManager.Instance.Level3FieldApdLengkap;
            lines.Add($"{Check(_dcsTombolDitekan)} Klik tombol DCS 3");
            lines.Add($"{Check(_level3LaporanAwalSelesai)} Lapor HT awal");
            lines.Add($"{Check(apdLapanganSiap)} Pakai kacamata + respirator");
            lines.Add($"{Check(_level3OreSampaiSlurry)} Ore/laterit sampai ke slurry tank");
            lines.Add($"{Check(_level3Slurry25Tercapai)} Pastikan slurry 50%");
            lines.Add($"{Check(_voiceReportSelesai)} Lapor HT akhir");
            txtParameterInfo.text = string.Join("\n", lines);
            return;
        }

        if (level == GameLevelManager.GameLevel.Level2_DCSPrep)
            lines.Add($"{Check(_dcsDilihat)} Lihat mesin DCS");

        if (data.nomorTombolDCS > 0)
            lines.Add($"{Check(_dcsTombolDitekan)} Klik tombol DCS {data.nomorTombolDCS}");

        // Level 4 punya step ekstra: atur flow rate ke 450 m³/h sebelum lapor HT.
        if (level == GameLevelManager.GameLevel.Level4_SlurryPump)
        {
            var p4 = GameLevelManager.Instance != null
                ? GameLevelManager.Instance.CurrentLevel4Phase
                : GameLevelManager.Level4Phase.Idle;

            // Flow tercapai = phase sudah ke MenungguLaporanFlow atau lebih lanjut
            bool flowRateSudahTerset = p4 == GameLevelManager.Level4Phase.MenungguLaporanFlow
                                    || p4 == GameLevelManager.Level4Phase.ObservasiPump
                                    || p4 == GameLevelManager.Level4Phase.ObservasiPreheater
                                    || p4 == GameLevelManager.Level4Phase.MenungguLaporanAkhir
                                    || p4 == GameLevelManager.Level4Phase.KembaliKeDcs
                                    || p4 == GameLevelManager.Level4Phase.Selesai;
            lines.Add($"{Check(flowRateSudahTerset)} Atur flow rate 450 m³/h");

            // Lapor HT awal
            bool laporAwalSelesai = p4 == GameLevelManager.Level4Phase.ObservasiPump
                                 || p4 == GameLevelManager.Level4Phase.ObservasiPreheater
                                 || p4 == GameLevelManager.Level4Phase.MenungguLaporanAkhir
                                 || p4 == GameLevelManager.Level4Phase.KembaliKeDcs
                                 || p4 == GameLevelManager.Level4Phase.Selesai;
            lines.Add($"{Check(laporAwalSelesai)} Lapor HT awal: 'slurry pump aktif'");

            // Slurry sampai preheater
            bool slurrySampaiPreheater = p4 == GameLevelManager.Level4Phase.ObservasiPreheater
                                      || p4 == GameLevelManager.Level4Phase.MenungguLaporanAkhir
                                      || p4 == GameLevelManager.Level4Phase.KembaliKeDcs
                                      || p4 == GameLevelManager.Level4Phase.Selesai;
            lines.Add($"{Check(slurrySampaiPreheater)} Slurry mencapai Pre-Heater");

            // Lapor HT akhir
            bool laporAkhirSelesai = p4 == GameLevelManager.Level4Phase.KembaliKeDcs
                                  || p4 == GameLevelManager.Level4Phase.Selesai;
            lines.Add($"{Check(laporAkhirSelesai)} Lapor HT akhir: 'cairan sudah di preheater'");

            txtParameterInfo.text = string.Join("\n", lines);
            return;
        }

        if (data.butuhVoiceReport)
            lines.Add($"{Check(_voiceReportSelesai)} Lapor HT lengkap");

        txtParameterInfo.text = string.Join("\n", lines);
    }

    private string Check(bool done) => done ? "[OK]" : "[ ]";

    private void SetLevelLabel(string teks, Color warna)
    {
        if (txtLevelLabel == null)
            return;

        txtLevelLabel.text = teks;
        txtLevelLabel.color = warna;
    }

    private void SetQuestLabel(string teks)
    {
        if (txtQuestLabel == null)
            return;

        txtQuestLabel.text = teks;
        txtQuestLabel.textWrappingMode = TextWrappingModes.Normal;
        txtQuestLabel.overflowMode = TextOverflowModes.Overflow;
        txtQuestLabel.alignment = TextAlignmentOptions.TopLeft;
    }

    private void SetTaskDone(TextMeshProUGUI txt)
    {
        if (txt == null)
            return;

        string t = txt.text;
        if (t.StartsWith("[ ]"))
            txt.text = "[OK]" + t.Substring(3);

        txt.color = _cDone;
    }

    private void SetTaskPending(TextMeshProUGUI txt)
    {
        if (txt == null)
            return;

        string t = txt.text;
        if (t.StartsWith("[OK]"))
            txt.text = "[ ]" + t.Substring(4);

        txt.color = Color.white;
    }

    private void ShowNotif(string pesan, bool sukses)
    {
        if (panelNotif == null)
            return;

        panelNotif.SetActive(true);
        if (txtNotif != null)
            txtNotif.text = pesan;

        if (bgNotif != null)
            bgNotif.color = sukses ? new Color(0.08f, 0.45f, 0.12f, 0.95f) : new Color(0.06f, 0.18f, 0.42f, 0.95f);

        StartCoroutine(HideNotif());
    }

    private IEnumerator HideNotif()
    {
        yield return new WaitForSeconds(4.5f);
        if (panelNotif != null)
            panelNotif.SetActive(false);
    }

    private IEnumerator HideNotifDelayed(float delay)
    {
        yield return new WaitForSeconds(delay);
        if (panelNotif != null)
            panelNotif.SetActive(false);
    }

    private IEnumerator PlayTransitionFade(float totalDuration)
    {
        _transitionOverlay.gameObject.SetActive(true);

        float fadeIn = Mathf.Clamp(totalDuration * 0.35f, 0.8f, 1.6f);
        float fadeOut = fadeIn;
        float hold = Mathf.Max(0.15f, totalDuration - fadeIn - fadeOut);

        yield return FadeOverlay(0f, 1f, fadeIn);
        yield return new WaitForSeconds(hold);
        yield return FadeOverlay(1f, 0f, fadeOut);

        _transitionOverlay.gameObject.SetActive(false);
        _transitionCoroutine = null;
    }

    private IEnumerator FadeOverlay(float from, float to, float duration)
    {
        float elapsed = 0f;
        Color c = _transitionOverlay.color;
        c.a = from;
        _transitionOverlay.color = c;

        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            c.a = Mathf.Lerp(from, to, elapsed / duration);
            _transitionOverlay.color = c;
            yield return null;
        }

        c.a = to;
        _transitionOverlay.color = c;
    }

    private void CacheAndFixLayout()
    {
        _questRect = txtQuestLabel != null ? txtQuestLabel.GetComponent<RectTransform>() : null;
        _operasionalRect = panelOperasional != null ? panelOperasional.GetComponent<RectTransform>() : null;
        _walkieHintRect = panelWalkieTalkieHint != null ? panelWalkieTalkieHint.GetComponent<RectTransform>() : null;
        _apdRect = taskHelm != null ? taskHelm.transform.parent.GetComponent<RectTransform>() : null;

        if (panelNotif != null) panelNotif.SetActive(false);
        if (panelOperasional != null) panelOperasional.SetActive(false);
        if (panelWalkieTalkieHint != null) panelWalkieTalkieHint.SetActive(false);

        if (_operasionalRect != null)
        {
            _operasionalRect.anchorMin = new Vector2(0f, 0f);
            _operasionalRect.anchorMax = new Vector2(1f, 0f);
            _operasionalRect.pivot = new Vector2(0.5f, 0f);
            _operasionalRect.sizeDelta = new Vector2(-20f, 190f);
            _operasionalRect.anchoredPosition = new Vector2(0f, 8f);
        }

        if (_walkieHintRect != null)
        {
            _walkieHintRect.anchorMin = new Vector2(0f, 0f);
            _walkieHintRect.anchorMax = new Vector2(1f, 0f);
            _walkieHintRect.pivot = new Vector2(0.5f, 0f);
            _walkieHintRect.sizeDelta = new Vector2(-20f, 124f);
            _walkieHintRect.anchoredPosition = new Vector2(0f, 206f);
        }

        if (_apdRect != null)
        {
            _apdRect.anchorMin = new Vector2(0f, 1f);
            _apdRect.anchorMax = new Vector2(1f, 1f);
            _apdRect.pivot = new Vector2(0.5f, 1f);
            _apdRect.sizeDelta = new Vector2(0f, 420f);
            _apdRect.anchoredPosition = new Vector2(0f, -184f);
        }

        SetQuestArea(98f, 110f);
        EnsureTransitionOverlay();
    }

    private void SetQuestArea(float topOffset, float height)
    {
        if (_questRect == null)
            return;

        _questRect.anchorMin = new Vector2(0f, 1f);
        _questRect.anchorMax = new Vector2(1f, 1f);
        _questRect.pivot = new Vector2(0.5f, 1f);
        _questRect.sizeDelta = new Vector2(-40f, height);
        _questRect.anchoredPosition = new Vector2(0f, -topOffset);
        _questRect.gameObject.SetActive(true);
    }

    private void SetApdLayout(bool active)
    {
        if (_apdRect == null)
            return;

        _apdRect.anchoredPosition = active ? new Vector2(0f, -184f) : new Vector2(0f, -184f);
    }

    private void SetHintLayout()
    {
        if (_walkieHintRect == null)
            return;

        _walkieHintRect.anchoredPosition = new Vector2(0f, 206f);
        _walkieHintRect.sizeDelta = new Vector2(-20f, 124f);
    }

    private void EnsureTransitionOverlay()
    {
        var rootRect = transform as RectTransform;
        if (rootRect == null)
            return;

        var overlayGO = new GameObject("TransitionOverlay");
        overlayGO.transform.SetParent(rootRect, false);
        overlayGO.transform.SetAsLastSibling();

        var rect = overlayGO.AddComponent<RectTransform>();
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;

        _transitionOverlay = overlayGO.AddComponent<Image>();
        _transitionOverlay.color = new Color(0f, 0f, 0f, 0f);
        overlayGO.SetActive(false);
    }
}
