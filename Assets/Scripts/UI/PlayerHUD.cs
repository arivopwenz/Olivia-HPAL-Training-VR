using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System.Collections;
using System.Collections.Generic;

/// <summary>
/// OLIVIA VR - PlayerHUD.cs
/// HUD quest utama, panel APD, hint laporan HT, checklist operasional, dan fade transisi.
/// </summary>
public class PlayerHUD : MonoBehaviour
{
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

    private RectTransform _questRect;
    private RectTransform _operasionalRect;
    private RectTransform _walkieHintRect;
    private RectTransform _apdRect;
    private Image _transitionOverlay;
    private Coroutine _transitionCoroutine;

    private void Start()
    {
        GameLevelManager.OnLevelStarted += OnLevelMulai;
        GameLevelManager.OnLevelComplete += OnLevelSelesai;
        GameLevelManager.OnDCSViewConfirmed += OnDcsViewed;
        GameLevelManager.OnDCSButtonPressed += OnDcsButtonPressed;
        GameLevelManager.OnVoiceReportAccepted += OnVoiceReportAccepted;
        GameLevelManager.OnLevelTransitionRequested += OnLevelTransitionRequested;
        PhaseManager.OnApdItemWorn += OnSatuApdDipakai;
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
        PhaseManager.OnApdItemWorn -= OnSatuApdDipakai;
        PhaseManager.OnAPD7Lengkap -= OnSemuaApdLengkap;
        WalkieTalkieManager.OnPTTDitekan -= OnPTTPress;
        WalkieTalkieManager.OnPTTDilepas -= OnPTTRelease;
    }

    private void OnLevelMulai(GameLevelManager.GameLevel level)
    {
        _levelAktif = level;
        int idx = (int)level;
        string[] namaLevel =
        {
            "LEVEL 0 - TUTORIAL", "LEVEL 1 - APD SAFETY", "LEVEL 2 - DCS PREPARATION",
            "LEVEL 3 - ORE & SLURRY", "LEVEL 4 - SLURRY PUMP", "LEVEL 5 - STEAM VALVE",
            "LEVEL 6 - ACID INJECTION", "LEVEL 7 - AUTOCLAVE", "LEVEL 8 - MONITORING DCS",
            "LEVEL 9 - FLASH VESSEL", "LEVEL 10 - CCD", "LEVEL 11 - MHP SAMPLING",
            "LEVEL 12 - TAILING DISCHARGE", "LEVEL 13 - TAILING WASTE", "LEVEL 14 - DARURAT K3"
        };

        Color warnaHeader = level == GameLevelManager.GameLevel.Level14_Emergency ? new Color(1f, 0.2f, 0.2f) : _cBlue;
        if (idx < namaLevel.Length)
            SetLevelLabel(namaLevel[idx], warnaHeader);

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
            SetFase(level == GameLevelManager.GameLevel.Level2_DCSPrep ? FaseQuest.LihatDCS : FaseQuest.MulaiMesin);
            UpdateOperasionalChecklist(level);
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
            SetQuestLabel($"<b>MISI: Pakai APD!</b>\n<size=83%>Lengkapi semua APD wajib sebelum keluar area.\n<color=#FFCC00>Progress: {_apdTerpasang}/{TOTAL_APD} item terpasang</color></size>");
    }

    private void OnSemuaApdLengkap()
    {
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
        UpdateOperasionalChecklist(_levelAktif);
    }

    private void OnVoiceReportAccepted(string keyword)
    {
        if (_levelAktif <= GameLevelManager.GameLevel.Level1_APD)
            return;

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
                SetQuestLabel("<b>SELAMAT DATANG!</b>\n<size=83%>- Gerak: WASD\n- Ambil: G\n- Bicara: tahan T untuk HT</size>");
                break;

            case FaseQuest.PakaiAPD:
                showApd = true;
                SetQuestArea(98f, 96f);
                SetApdLayout(true);
                SetHintLayout();
                SetQuestLabel("<b>MISI: Pakai APD!</b>\n<size=83%>Lengkapi APD wajib sebelum keluar area.</size>");
                break;

            case FaseQuest.MasukPintu:
                SetQuestArea(98f, 96f);
                SetApdLayout(false);
                SetHintLayout();
                SetQuestLabel("<b>MISI: Keluar Loker!</b>\n<size=83%>Sentuh pintu keluar untuk lanjut ke area berikutnya.</size>");
                break;

            case FaseQuest.LihatDCS:
                SetQuestArea(98f, 96f);
                SetApdLayout(false);
                SetHintLayout();
                SetQuestLabel("<b>MISI: Lihat mesin DCS</b>\n<size=83%>Dekati area DCS untuk memulai langkah berikutnya.</size>");
                break;

            case FaseQuest.MulaiMesin:
                SetQuestArea(98f, 96f);
                SetApdLayout(false);
                SetHintLayout();
                SetQuestLabel("<b>MISI: Klik tombol DCS aktif</b>\n<size=83%>Tekan tombol DCS yang menyala untuk memulai operasi.</size>");
                break;

            case FaseQuest.LaporHT:
                SetQuestArea(98f, 96f);
                SetApdLayout(false);
                SetHintLayout();
                SetQuestLabel("<b>MISI: Lapor lewat HT</b>\n<size=83%>Tahan <b>T</b>, sampaikan laporan lengkap, lalu lepas tombol untuk kirim.</size>");
                UpdateHintKataKunci(_levelAktif);
                break;

            case FaseQuest.GunakanWT:
                SetQuestArea(98f, 96f);
                SetApdLayout(false);
                SetHintLayout();
                SetQuestLabel("<b>MISI: Kirim laporan HT</b>\n<size=83%>Sampaikan laporan lengkap seperti komunikasi radio asli.</size>");
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

    public void NotifyMasukPintu()
    {
        if (_faseSekarang == FaseQuest.MasukPintu)
            SetFase(FaseQuest.GunakanWT);
    }

    public void ShowNotifPublic(string pesan)
    {
        ShowNotif(pesan, false);
    }

    private void UpdateOperasionalChecklist(GameLevelManager.GameLevel level)
    {
        if (txtParameterInfo == null || GameLevelManager.Instance == null)
            return;

        if (!GameLevelManager.Instance.TryGetLevelData(level, out var data))
            return;

        var lines = new List<string>();
        if (level == GameLevelManager.GameLevel.Level2_DCSPrep)
            lines.Add($"{Check(_dcsDilihat)} Lihat mesin DCS");

        if (data.nomorTombolDCS > 0)
            lines.Add($"{Check(_dcsTombolDitekan)} Klik tombol DCS {data.nomorTombolDCS}");

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
