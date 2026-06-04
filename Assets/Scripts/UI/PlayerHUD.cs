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
        [TextArea(2, 4)] public string observasiSlurry = "<b>MISI: Amati slurry tank terisi</b>\n<size=83%>Air mengalir dari pipa kiri. Perhatikan level cairan naik sampai menyentuh batas 75%.</size>";
        [TextArea(2, 4)] public string laporanAkhir = "<b>MISI: Kirim laporan HT akhir</b>\n<size=83%>Laporkan bahwa ore sudah masuk ke slurry tank dan level cairan mencapai 75%.</size>";
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
    private bool _level5LaporanAwalDone;

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
            _level5LaporanAwalDone = false;
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
        SetFase(FaseQuest.GunakanWT);
        ShowNotif("Semua APD lengkap! Lapor lewat WT untuk lanjut ke Level 2.", true);
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

        // Level 5 punya 2 laporan: awal ("aktifkan pre-heater") dan akhir ("katup steam terbuka").
        // Jangan set _voiceReportSelesai untuk laporan awal supaya task akhir tidak ikut kecentang.
        if (_levelAktif == GameLevelManager.GameLevel.Level5_SteamValve)
        {
            bool preheaterReady = GameLevelManager.Instance != null && GameLevelManager.Instance.Level5PreheaterReady;
            if (preheaterReady)
                _voiceReportSelesai = true;   // laporan AKHIR
            else
                _level5LaporanAwalDone = true; // laporan AWAL
        }
        else if (_levelAktif == GameLevelManager.GameLevel.Level6_AcidInjection)
        {
            // Level 6 punya 3 laporan: outlet, slurry masuk, dan acid aktif (final).
            // _voiceReportSelesai hanya boleh true saat laporan AKHIR (acid complete).
            bool acidComplete = GameLevelManager.Instance != null && GameLevelManager.Instance.Level6AcidComplete;
            if (acidComplete) _voiceReportSelesai = true;
            // Laporan intermediate: jangan set _voiceReportSelesai. GLM akan handle internal flag.
        }
        else
        {
            _voiceReportSelesai = true;
        }
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
                    ? Teks(_teksLevel3.observasiSlurry, "<b>MISI: Amati slurry tank terisi</b>\n<size=83%>Air mengalir dari pipa kiri. Perhatikan level cairan naik sampai menyentuh batas 75%.</size>")
                    : Teks(_teksLevel3.observasiOreAir, "<b>MISI: Amati ore dan air masuk</b>\n<size=83%>Tunggu ore/laterit benar-benar mencapai slurry tank, lalu amati pengisian tank.</size>"));
                break;

            case GameLevelManager.Level3Phase.SiapLaporanAkhir:
                SetFase(FaseQuest.LaporHT);
                SetQuestLabel(Teks(_teksLevel3.laporanAkhir, "<b>MISI: Kirim laporan HT akhir</b>\n<size=83%>Laporkan bahwa ore sudah masuk ke slurry tank dan level cairan mencapai 75%.</size>"));
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

    public void ShowNotifPublic(string pesan, float duration)
    {
        ShowNotif(pesan, false, duration);
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
            "LEVEL 6 - ACID INJECTION", "LEVEL 7 - AUTOCLAVE", "LEVEL 8 - FLASH VESSEL & LETDOWN",
            "LEVEL 9 - (digabung ke Level 8)", "LEVEL 9 - CCD", "LEVEL 10 - MHP SAMPLING",
            "LEVEL 11 - TAILING & FILTER PRESS", "LEVEL 12 - DRY STACK TAILING", "LEVEL 13 - DARURAT K3"
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
            lines.Add($"{Check(_level3Slurry25Tercapai)} Pastikan slurry 75%");
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

        // Level 5: Steam Valve — detail per-step
        if (level == GameLevelManager.GameLevel.Level5_SteamValve)
        {
            bool preheaterReady = GameLevelManager.Instance.Level5PreheaterReady;
            bool sudahLaporAwal = _level5LaporanAwalDone || preheaterReady;
            // Lapor HT akhir hanya valid kalau preheater sudah ready DAN voice report final accepted.
            bool laporAkhirOk = preheaterReady && _voiceReportSelesai;
            lines.Add($"{Check(sudahLaporAwal)} Lapor HT: 'aktifkan pre-heater'");
            lines.Add($"{Check(preheaterReady)} Putar katup steam (suhu >= 180C)");
            lines.Add($"{Check(laporAkhirOk)} Lapor HT akhir: 'katup steam terbuka'");
            txtParameterInfo.text = string.Join("\n", lines);
            return;
        }

        // Level 6: Acid Injection — detail per-step
        if (level == GameLevelManager.GameLevel.Level6_AcidInjection)
        {
            bool outletDone = GameLevelManager.Instance.Level6OutletReportDone;
            bool slurryMasuk = GameLevelManager.Instance.Level6SlurryMasukAutoclave;
            bool slurryReport = GameLevelManager.Instance.Level6SlurryReportDone;
            bool dcsAcidReady = GameLevelManager.Instance.Level6DcsAcidReady;
            bool acidComplete = GameLevelManager.Instance.Level6AcidComplete;

            lines.Add($"{Check(outletDone)} Lapor HT: 'outlet preheater dibuka'");
            lines.Add($"{Check(slurryMasuk)} Putar valve preheater (cairan masuk autoclave)");
            lines.Add($"{Check(slurryReport)} Lapor HT: 'slurry masuk autoclave'");
            lines.Add($"{Check(dcsAcidReady)} DCS: set acid 350 + stroke 70% + ARM");
            lines.Add($"{Check(acidComplete)} Field acid skid: tekan LOCAL START + LEAK OK");
            // Lapor akhir hanya ter-check kalau acid complete + final voice accepted.
            bool laporAkhirAcidOk = acidComplete && _voiceReportSelesai;
            lines.Add($"{Check(laporAkhirAcidOk)} Lapor HT akhir: 'acid aktif, pH 1.0'");
            txtParameterInfo.text = string.Join("\n", lines);
            return;
        }

        // Level 7: Autoclave (REBUILT - simpler 4-step flow)
        if (level == GameLevelManager.GameLevel.Level7_Autoclave)
        {
            var glm = GameLevelManager.Instance;
            // Pakai flag granular: GaugesLogged = fill 100%, Xray = X-Ray seen, SafetyDrillDone = drill done.
            bool dcsDone = _dcsTombolDitekan;
            bool valveOpened = glm != null && glm.Level7GaugesLogged; // Slurry fill 100%
            bool xrayDone = glm != null && glm.Level7XrayActivated;
            bool safetyDone = glm != null && glm.Level7SafetyDrillDone;
            bool inspected = glm != null && glm.Level7AutoclaveInspected;
            bool laporOk = inspected && _voiceReportSelesai;

            lines.Add($"{Check(dcsDone)} Klik tombol DCS 7 (start autoclave route)");
            lines.Add($"{Check(valveOpened)} Putar valve inlet → cairan masuk autoclave");
            lines.Add($"{Check(xrayDone)} Aktifkan X-Ray (X) untuk monitor cairan + agitator");
            lines.Add($"{Check(safetyDone)} Konfirmasi safety drill 4 step (S): PSV/ESD/Quench/Exit");
            lines.Add($"{Check(laporOk)} Lapor HT: 'autoclave normal, suhu 250, tekanan 50, agitator 60 RPM'");
            txtParameterInfo.text = string.Join("\n", lines);
            return;
        }
        // Level 8: Flash Vessel & Letdown (Opsi A — sample dipindah ke Level 9 CCD)
        if (level == GameLevelManager.GameLevel.Level8_Monitoring)
        {
            var l8 = FindFirstObjectByType<Level8FlashTrainController>(FindObjectsInactive.Exclude);
            if (l8 != null && l8.LevelActive)
            {
                lines.Add($"{Check(_dcsTombolDitekan)} Klik tombol DCS 8");
                lines.Add($"{Check(l8.AutoclaveValveOpened)} Buka valve letdown Autoclave → slurry mengalir ke Flash Vessel");
                lines.Add($"{Check(l8.AutoclaveValveOpened)} Lapor HT: 'Autoclave dibuka menuju Flash Vessel'");
                lines.Add($"{Check(l8.Fv1Stable)} Lapor + tutup uap FV1 (putar handwheel)");
                lines.Add($"{Check(l8.Fv2Stable)} Lapor + tutup uap FV2");
                lines.Add($"{Check(l8.Fv3Stable)} Lapor + tutup uap FV3");
                lines.Add($"{Check(_voiceReportSelesai)} Lapor HT akhir: 'flash train stable'");
            }
            else
            {
                lines.Add($"{Check(_dcsTombolDitekan)} Klik tombol DCS 8");
                lines.Add("[ ] Buka letdown FV1, FV2, FV3 berurutan");
                lines.Add("[ ] Lapor HT");
            }
            txtParameterInfo.text = string.Join("\n", lines);
            return;
        }

        

        // Level 9 lama (Flash Vessel) DIPENSIUNKAN — digabung ke Level 8. Tidak ada checklist.

        // Level 9 (display) = CCD (enum Level10_CCD)
        if (level == GameLevelManager.GameLevel.Level10_CCD)
        {
            var glm = GameLevelManager.Instance;
            bool ccdStable = glm != null && glm.Level10CCDComplete;
            bool plsLulus = glm != null && glm.Level10SamplePLSAccepted;
            lines.Add($"{Check(_dcsTombolDitekan)} Klik tombol DCS 9");
            lines.Add($"{Check(ccdStable)} Aktifkan CCD separator + amati pemisahan");
            lines.Add($"{Check(plsLulus)} Ambil 3 sample PLS overflow + submit lab QC");
            lines.Add($"{Check(_voiceReportSelesai)} Lapor HT: 'CCD aktif, PLS lulus QC'");
            txtParameterInfo.text = string.Join("\n", lines);
            return;
        }

        // Level 10 (display) = MHP (enum Level11_MHP)
        if (level == GameLevelManager.GameLevel.Level11_MHP)
        {
            var l10 = FindFirstObjectByType<Level11MHPController>(FindObjectsInactive.Exclude);
            lines.Add($"{Check(_dcsTombolDitekan)} Klik tombol DCS 10");
            if (l10 != null && l10.LevelActive)
            {
                lines.Add($"{Check(l10.Stage1Done)} Dosing LIMESTONE (CaCO3) -> pH 3.5 (buang Fe/Al)");
                lines.Add($"{Check(l10.Stage2Done)} Dosing KAPUR Ca(OH)2 -> pH 5.0 (buang Al/Cr)");
                lines.Add($"{Check(l10.Stage3Done)} Dosing MgO -> pH 7.5 (endap MHP Ni-Co hijau)");
                lines.Add($"{Check(l10.SampleTaken)} Ambil sampel MHP di stasiun sampling");
                lines.Add($"{Check(l10.LabAccepted)} Lab QC: assay Ni/Co lulus SOP (ACCEPT)");
                lines.Add($"{Check(l10.BaggingDone)} Bagging & dispatch produk MHP ke refinery");
            }
            lines.Add($"{Check(_voiceReportSelesai)} Lapor HT: 'MHP terbentuk'");
            txtParameterInfo.text = string.Join("\n", lines);
            return;
        }

        // Level 11 (display) = Tailing Discharge (enum Level12_TailingDischarge)
        if (level == GameLevelManager.GameLevel.Level12_TailingDischarge)
        {
            var l11 = FindFirstObjectByType<Level12TailingFilterController>(FindObjectsInactive.Exclude);
            lines.Add($"{Check(_dcsTombolDitekan)} Klik tombol DCS 11");
            if (l11 != null && l11.LevelActive)
            {
                lines.Add($"{Check(l11.NeutralizeDone)} Dosing LIMESTONE/KAPUR -> pH 8.0 (netralkan asam)");
                lines.Add($"{Check(l11.FilterPressDone)} Jalankan FILTER PRESS -> cake moisture < 25%");
                lines.Add($"{Check(l11.Inspected)} Inspeksi cake di konveyor");
                lines.Add($"{Check(l11.ComplianceAccepted)} Compliance QC: pH/moisture/filtrat lulus (ACCEPT)");
            }
            lines.Add($"{Check(_voiceReportSelesai)} Lapor HT: 'limbah dialirkan'");
            txtParameterInfo.text = string.Join("\n", lines);
            return;
        }

        // Level 12 (display) = Dry Stack (enum Level13_TailingWaste)
        if (level == GameLevelManager.GameLevel.Level13_TailingWaste)
        {
                        var l12 = FindFirstObjectByType<Level13DryStackController>(FindObjectsInactive.Exclude);
            lines.Add($"{Check(_dcsTombolDitekan)} Klik tombol DCS 12");
            if (l12 != null && l12.LevelActive)
            {
                lines.Add($"{Check(l12.StackingDone)} Timbun + padatkan cake di terraced lift");
                lines.Add($"{Check(l12.ClosureDone)} Closure: rehab cap + piezometer aman");
                lines.Add($"{Check(l12.Inspected)} Inspeksi DSTF");
                lines.Add($"{Check(l12.ComplianceAccepted)} Compliance QC: geomembrane/piezo/rembesan (ACCEPT)");
            }
            lines.Add($"{Check(_voiceReportSelesai)} Lapor HT: 'dry stack aman, pH 8.5'");
            txtParameterInfo.text = string.Join("\n", lines);
            return;
        }

        // Level 13 (display) = Emergency (enum Level14_Emergency)
        if (level == GameLevelManager.GameLevel.Level14_Emergency)
        {
            lines.Add("[ ] Deteksi alarm gas / kebocoran");
            lines.Add("[ ] Lapor HT: 'Emergency! Evakuasi!'");
            lines.Add("[ ] Tekan tombol ESD (merah)");
            lines.Add($"{Check(_voiceReportSelesai)} Shutdown berhasil");
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
        ShowNotif(pesan, sukses, 4.5f);
    }

    private void ShowNotif(string pesan, bool sukses, float duration)
    {
        if (panelNotif == null)
            return;

        panelNotif.SetActive(true);
        // Pastikan banner render PALING DEPAN (di atas semua panel & transition overlay).
        var notifRt = panelNotif.GetComponent<RectTransform>();
        if (notifRt != null) notifRt.SetAsLastSibling();
        if (txtNotif != null)
            txtNotif.text = pesan;

        if (bgNotif != null)
            bgNotif.color = sukses ? new Color(0.08f, 0.45f, 0.12f, 0.95f) : new Color(0.06f, 0.18f, 0.42f, 0.95f);

        if (_hideNotifCo != null) StopCoroutine(_hideNotifCo);
        _hideNotifCo = StartCoroutine(HideNotifAfter(duration));
    }

    private Coroutine _hideNotifCo;

    private IEnumerator HideNotifAfter(float seconds)
    {
        yield return new WaitForSeconds(seconds);
        if (panelNotif != null) panelNotif.SetActive(false);
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

        // ===================================================================
        // REDESAIN PANEL TASK v3 — stack vertikal bersih, tidak overlap.
        // Layout (anchor semua dari TOP, panel pivot kanan-atas):
        //   [0      .. -PAD]            margin atas
        //   HEADER  (judul level)       tinggi HEADER_H
        //   MISI    (quest)             tinggi MISI_H
        //   LAPORAN HT (kata kunci)     tinggi HT_H
        //   OPERASIONAL / APD checklist sisa ruang ke bawah
        // ===================================================================
        const float PANEL_W = 500f;
        const float PANEL_H = 800f;
        const float PAD = 14f;          // padding dalam panel
        const float HEADER_H = 56f;
        const float MISI_H = 128f;
        const float HT_H = 150f;
        const float GAP = 10f;

        float yHeader = -PAD;
        float yMisi = yHeader - HEADER_H - GAP;
        float yHt = yMisi - MISI_H - GAP;
        float yChecklist = yHt - HT_H - GAP;
        float checklistH = PANEL_H + yChecklist - PAD; // sisa sampai bawah (yChecklist negatif)

        Color cPanel = new Color(0.05f, 0.07f, 0.11f, 0.95f);
        Color cBand = new Color(0.09f, 0.13f, 0.20f, 0.96f);
        Color cBandDark = new Color(0.03f, 0.05f, 0.08f, 0.97f);
        Color cHeaderBand = new Color(0.06f, 0.34f, 0.52f, 1f);

        RectTransform panelRect = null;
        if (_questRect != null && _questRect.parent != null)
            panelRect = _questRect.parent as RectTransform; // Panel_Quest
        if (panelRect != null)
        {
            panelRect.anchorMin = new Vector2(1f, 1f);
            panelRect.anchorMax = new Vector2(1f, 1f);
            panelRect.pivot = new Vector2(1f, 1f);
            panelRect.sizeDelta = new Vector2(PANEL_W, PANEL_H);
            panelRect.anchoredPosition = new Vector2(-24f, -24f);
            var pImg = panelRect.GetComponent<Image>();
            if (pImg != null) pImg.color = cPanel;

            // Header band penuh lebar
            var headerTr = panelRect.Find("BG_Header") as RectTransform;
            if (headerTr != null)
            {
                headerTr.anchorMin = new Vector2(0f, 1f);
                headerTr.anchorMax = new Vector2(1f, 1f);
                headerTr.pivot = new Vector2(0.5f, 1f);
                headerTr.sizeDelta = new Vector2(0f, HEADER_H);
                headerTr.anchoredPosition = new Vector2(0f, -PAD);
                var hbImg = headerTr.GetComponent<Image>();
                if (hbImg != null) hbImg.color = cHeaderBand;
            }

            // Divider lama (garis) tidak dipakai di layout v3 -> sembunyikan.
            var dividerTr = panelRect.Find("Divider");
            if (dividerTr != null) dividerTr.gameObject.SetActive(false);
        }

        // HEADER label
        if (txtLevelLabel != null)
        {
            txtLevelLabel.fontSize = 23f;
            txtLevelLabel.fontStyle = FontStyles.Bold;
            txtLevelLabel.alignment = TextAlignmentOptions.Center;
            txtLevelLabel.textWrappingMode = TextWrappingModes.Normal;
            txtLevelLabel.overflowMode = TextOverflowModes.Truncate;
            txtLevelLabel.color = Color.white;
        }

        // MISI (quest) band
        if (_questRect != null)
        {
            _questRect.anchorMin = new Vector2(0f, 1f);
            _questRect.anchorMax = new Vector2(1f, 1f);
            _questRect.pivot = new Vector2(0.5f, 1f);
            _questRect.sizeDelta = new Vector2(-(PAD * 2f), MISI_H);
            _questRect.anchoredPosition = new Vector2(0f, yMisi);
            _questRect.gameObject.SetActive(true);
            var qImg = _questRect.GetComponent<Image>();
            if (qImg != null) qImg.color = cBand;
            StyleBody(txtQuestLabel, 17f, TextAlignmentOptions.TopLeft);
        }

        // APD checklist — isi area checklist (hanya Level 1)
        if (_apdRect != null)
        {
            _apdRect.anchorMin = new Vector2(0f, 1f);
            _apdRect.anchorMax = new Vector2(1f, 1f);
            _apdRect.pivot = new Vector2(0.5f, 1f);
            _apdRect.sizeDelta = new Vector2(-(PAD * 2f), checklistH);
            _apdRect.anchoredPosition = new Vector2(0f, yChecklist);
        }

        // LAPORAN HT band
        if (_walkieHintRect != null)
        {
            _walkieHintRect.anchorMin = new Vector2(0f, 1f);
            _walkieHintRect.anchorMax = new Vector2(1f, 1f);
            _walkieHintRect.pivot = new Vector2(0.5f, 1f);
            _walkieHintRect.sizeDelta = new Vector2(-(PAD * 2f), HT_H);
            _walkieHintRect.anchoredPosition = new Vector2(0f, yHt);
            var hImg = _walkieHintRect.GetComponent<Image>();
            if (hImg != null) hImg.color = cBandDark;
        }
        if (txtHintKataKunci != null)
        {
            txtHintKataKunci.fontSize = 16f;
            txtHintKataKunci.enableAutoSizing = true;
            txtHintKataKunci.fontSizeMin = 12f;
            txtHintKataKunci.fontSizeMax = 17f;
            txtHintKataKunci.textWrappingMode = TextWrappingModes.Normal;
            txtHintKataKunci.overflowMode = TextOverflowModes.Truncate;
            txtHintKataKunci.alignment = TextAlignmentOptions.TopLeft;
            txtHintKataKunci.margin = new Vector4(14f, 34f, 14f, 10f);
            txtHintKataKunci.color = new Color(1f, 0.88f, 0.34f);
            var htRt = txtHintKataKunci.GetComponent<RectTransform>();
            if (htRt != null)
            {
                htRt.anchorMin = new Vector2(0f, 0f);
                htRt.anchorMax = new Vector2(1f, 1f);
                htRt.offsetMin = Vector2.zero;
                htRt.offsetMax = Vector2.zero;
            }
            // Header "LAPORAN HT" di band walkie (buat sekali).
            if (_walkieHintRect != null && _walkieHintRect.Find("Lbl_HT") == null)
            {
                var lblGo = new GameObject("Lbl_HT", typeof(RectTransform));
                var lblRt = lblGo.GetComponent<RectTransform>();
                lblRt.SetParent(_walkieHintRect, false);
                lblRt.anchorMin = new Vector2(0f, 1f);
                lblRt.anchorMax = new Vector2(1f, 1f);
                lblRt.pivot = new Vector2(0.5f, 1f);
                lblRt.sizeDelta = new Vector2(-20f, 28f);
                lblRt.anchoredPosition = new Vector2(0f, -6f);
                var lbl = lblGo.AddComponent<TextMeshProUGUI>();
                lbl.text = "LAPORAN HT";
                lbl.fontSize = 15f;
                lbl.fontStyle = FontStyles.Bold;
                lbl.color = new Color(0.45f, 0.85f, 1f);
                lbl.alignment = TextAlignmentOptions.TopLeft;
                lbl.margin = new Vector4(14f, 4f, 4f, 0f);
            }
        }

        // OPERASIONAL checklist — isi area checklist (anchor TOP, di bawah LAPORAN HT)
        if (_operasionalRect != null)
        {
            _operasionalRect.anchorMin = new Vector2(0f, 1f);
            _operasionalRect.anchorMax = new Vector2(1f, 1f);
            _operasionalRect.pivot = new Vector2(0.5f, 1f);
            _operasionalRect.sizeDelta = new Vector2(-(PAD * 2f), checklistH);
            _operasionalRect.anchoredPosition = new Vector2(0f, yChecklist);
            var oImg = _operasionalRect.GetComponent<Image>();
            if (oImg != null) oImg.color = cBand;
        }
        if (txtParameterInfo != null)
        {
            txtParameterInfo.fontSize = 17f;
            txtParameterInfo.enableAutoSizing = true;
            txtParameterInfo.fontSizeMin = 12f;
            txtParameterInfo.fontSizeMax = 18f;
            txtParameterInfo.textWrappingMode = TextWrappingModes.Normal;
            txtParameterInfo.overflowMode = TextOverflowModes.Truncate;
            txtParameterInfo.alignment = TextAlignmentOptions.TopLeft;
            txtParameterInfo.lineSpacing = 14f;
            var prt = txtParameterInfo.GetComponent<RectTransform>();
            if (prt != null)
            {
                prt.anchorMin = new Vector2(0f, 0f);
                prt.anchorMax = new Vector2(1f, 1f);
                prt.offsetMin = new Vector2(16f, 14f);
                prt.offsetMax = new Vector2(-16f, -44f);
            }
        }

        EnsureTransitionOverlay();
        PositionNotifTop();
    }
    private void PositionNotifTop()
    {
        if (panelNotif == null) return;
        var rt = panelNotif.GetComponent<RectTransform>();
        if (rt != null)
        {
            rt.anchorMin = new Vector2(0.5f, 1f);
            rt.anchorMax = new Vector2(0.5f, 1f);
            rt.pivot = new Vector2(0.5f, 1f);
            rt.sizeDelta = new Vector2(900f, 84f);
            rt.anchoredPosition = new Vector2(0f, -26f);
            // render paling depan: jadikan child terakhir di canvas root.
            rt.SetAsLastSibling();
        }
        if (txtNotif != null)
        {
            txtNotif.fontSize = 22f;
            txtNotif.enableAutoSizing = true;
            txtNotif.fontSizeMin = 14f;
            txtNotif.fontSizeMax = 24f;
            txtNotif.fontStyle = FontStyles.Bold;
            txtNotif.alignment = TextAlignmentOptions.Center;
            txtNotif.textWrappingMode = TextWrappingModes.Normal;
            txtNotif.overflowMode = TextOverflowModes.Truncate;
            txtNotif.margin = new Vector4(20f, 6f, 20f, 6f);
        }
    }

    private void StyleBody(TextMeshProUGUI t, float size, TextAlignmentOptions align)
    {
        if (t == null) return;
        t.fontSize = size;
        t.enableAutoSizing = true;
        t.fontSizeMin = 12f;
        t.fontSizeMax = size + 2f;
        t.textWrappingMode = TextWrappingModes.Normal;
        t.overflowMode = TextOverflowModes.Truncate;
        t.alignment = align;
        t.margin = new Vector4(12f, 6f, 12f, 6f);
    }

    // Layout band sekarang ditangani sekali di CacheAndFixLayout() (v3 stack vertikal).
    // Method2 ini sengaja dibuat no-op supaya tidak menimpa posisi band saat ganti fase.
    private void SetQuestArea(float topOffset, float height)
    {
        if (_questRect != null) _questRect.gameObject.SetActive(true);
    }

    private void SetApdLayout(bool active)
    {
        // no-op: posisi APD diatur di CacheAndFixLayout.
    }

    private void SetHintLayout()
    {
        // no-op: posisi band LAPORAN HT diatur di CacheAndFixLayout.
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
