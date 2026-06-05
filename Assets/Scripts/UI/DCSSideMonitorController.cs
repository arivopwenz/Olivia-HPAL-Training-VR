using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// OLIVIA VR — DCSSideMonitorController.cs
///
/// Membangun DUA monitor samping (kiri & kanan) di video wall DCS control room
/// sebagai canvas world-space PORTRAIT (panjang ke bawah). Tiap level, panel
/// mesin level itu muncul di SISI yang sesuai posisi mesin di plant:
///
///   KIRI  (mesin di -X): L6 Acid, L7 Autoclave, L8 Flash, L14 Emergency
///   KANAN (mesin di +X): L2 DCS Prep, L3 Ore/Slurry, L4 Pump, L5 Pre-Heater,
///                        L9 CCD (enum Level10), L10 MHP (enum Level11),
///                        L11 Tailing (enum Level12), L12 Dry Stack (enum Level13)
///
/// Sisi yang sesuai level aktif akan MENYALA TERANG dan menampilkan panel level
/// lengkap (route, field state, checklist, setpoint live, alarm). Sisi lainnya
/// redup / STANDBY. Nilai setpoint dibaca live dari GameLevelManager.
///
/// Komponen ini auto-build UI saat runtime dan auto-posisikan diri ke layar
/// samping (VW_Side_L_Screen / VW_Side_R_Screen). Tidak perlu setup manual.
/// </summary>
[DisallowMultipleComponent]
public class DCSSideMonitorController : MonoBehaviour
{
    private enum Side { Left, Right }

    private class PanelCopy
    {
        public Side side;
        public bool hasPanel;
        public string title;
        public string status;
        public string route;
        public string field;
        public string actions;
        public string setpoints;
        public string alarm;
        public float flow01;
    }

    private class SidePanel
    {
        public Side side;
        public Canvas canvas;
        public Image bg;
        public Image headerBand;
        public Image alarmBand;
        public TextMeshProUGUI txtTitle;
        public TextMeshProUGUI txtSideTag;
        public TextMeshProUGUI txtStatus;
        public TextMeshProUGUI txtRoute;
        public TextMeshProUGUI txtField;
        public TextMeshProUGUI txtActions;
        public TextMeshProUGUI txtSetpoints;
        public TextMeshProUGUI txtAlarm;
        public Image flowBarBg;
        public Image flowBar;
    }

    [Header("Nama layar samping di video wall (auto-find)")]
    [SerializeField] private string _leftScreenName = "VW_Side_L_Screen";
    [SerializeField] private string _rightScreenName = "VW_Side_R_Screen";

    [Header("Fallback posisi layar (kalau auto-find gagal)")]
    [SerializeField] private Vector3 _leftCenterFallback = new Vector3(-5.67f, 10.42f, 20.02f);
    [SerializeField] private Vector3 _rightCenterFallback = new Vector3(1.43f, 10.42f, 20.02f);
    [SerializeField] private Vector2 _screenSizeFallback = new Vector2(1.0f, 1.5f);

    [Header("Layout canvas (portrait, pixel)")]
    [SerializeField] private float _canvasPxW = 800f;
    [SerializeField] private float _canvasPxH = 1200f;
    [SerializeField] private float _fillFrac = 0.92f;
    [SerializeField] private float _frontOffset = 0.04f;

    [Header("Warna")]
    [SerializeField] private Color _bgActive = new Color(0.015f, 0.045f, 0.06f, 0.96f);
    [SerializeField] private Color _bgIdle = new Color(0.01f, 0.018f, 0.022f, 0.92f);
    [SerializeField] private Color _headerActive = new Color(0.02f, 0.30f, 0.42f, 0.98f);
    [SerializeField] private Color _headerIdle = new Color(0.06f, 0.10f, 0.12f, 0.96f);
    [SerializeField] private Color _titleActive = new Color(0.35f, 0.92f, 1f);
    [SerializeField] private Color _titleIdle = new Color(0.30f, 0.42f, 0.48f);

    private SidePanel _left;
    private SidePanel _right;
    private int _lastDcsButton;
    private bool _built;

    private void Awake()
    {
        BuildIfNeeded();
    }

    private void OnEnable()
    {
        GameLevelManager.OnLevelStarted += HandleLevelStarted;
        GameLevelManager.OnDCSButtonPressed += HandleDcsPressed;
        Refresh();
    }

    private void OnDisable()
    {
        GameLevelManager.OnLevelStarted -= HandleLevelStarted;
        GameLevelManager.OnDCSButtonPressed -= HandleDcsPressed;
    }

    private void Update()
    {
        Refresh();
    }

    private void HandleLevelStarted(GameLevelManager.GameLevel level)
    {
        Refresh();
    }

    private void HandleDcsPressed(int button)
    {
        _lastDcsButton = button;
        Refresh();
    }

    private void BuildIfNeeded()
    {
        if (_built)
            return;

        Vector3 lCenter; Vector2 lSize;
        Vector3 rCenter; Vector2 rSize;
        ResolveScreen(_leftScreenName, _leftCenterFallback, out lCenter, out lSize);
        ResolveScreen(_rightScreenName, _rightCenterFallback, out rCenter, out rSize);

        _left = BuildPanel(Side.Left, lCenter, lSize);
        _right = BuildPanel(Side.Right, rCenter, rSize);
        _built = true;
    }

    private void ResolveScreen(string screenName, Vector3 fallbackCenter, out Vector3 center, out Vector2 size)
    {
        center = fallbackCenter;
        size = _screenSizeFallback;

        GameObject go = null;
        foreach (var t in Resources.FindObjectsOfTypeAll<Transform>())
        {
            if (t.name == screenName && t.gameObject.scene.IsValid())
            {
                go = t.gameObject;
                break;
            }
        }
        if (go == null)
            return;

        var rend = go.GetComponent<Renderer>();
        if (rend != null)
        {
            center = rend.bounds.center;
            size = new Vector2(rend.bounds.size.x, rend.bounds.size.y);
        }
        else
        {
            center = go.transform.position;
        }
    }

    private SidePanel BuildPanel(Side side, Vector3 screenCenter, Vector2 screenSize)
    {
        var panel = new SidePanel { side = side };

        var go = new GameObject("DCS_SideMonitor_" + side, typeof(RectTransform), typeof(Canvas));
        go.transform.SetParent(transform, false);

        var canvas = go.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.WorldSpace;
        panel.canvas = canvas;

        var rt = go.GetComponent<RectTransform>();
        rt.sizeDelta = new Vector2(_canvasPxW, _canvasPxH);
        rt.pivot = new Vector2(0.5f, 0.5f);

        // Operator menghadap +Z, jadi canvas sedikit di depan layar (z lebih kecil).
        Vector3 worldPos = new Vector3(screenCenter.x, screenCenter.y, screenCenter.z - _frontOffset);
        go.transform.position = worldPos;
        go.transform.rotation = Quaternion.identity;

        float targetW = screenSize.x * _fillFrac;
        float targetH = screenSize.y * _fillFrac;
        float scale = Mathf.Min(targetW / _canvasPxW, targetH / _canvasPxH);
        if (scale < 1e-6f) scale = 0.00115f;
        go.transform.localScale = Vector3.one * scale;

        // Background
        var bgImg = go.AddComponent<Image>();
        bgImg.color = _bgIdle;
        panel.bg = bgImg;

        // Header band (atas)
        panel.headerBand = AddBand(rt, "HeaderBand", new Vector2(0f, 540f), new Vector2(_canvasPxW, 96f), _headerIdle);
        panel.txtTitle = AddText(rt, "Txt_Title", new Vector2(0f, 558f), new Vector2(_canvasPxW - 40f, 46f), 30, FontStyles.Bold, _titleIdle, TextAlignmentOptions.Center);
        panel.txtSideTag = AddText(rt, "Txt_SideTag", new Vector2(0f, 512f), new Vector2(_canvasPxW - 40f, 32f), 18, FontStyles.Bold, new Color(0.6f, 0.8f, 0.9f), TextAlignmentOptions.Center);

        // Status
        panel.txtStatus = AddText(rt, "Txt_Status", new Vector2(0f, 462f), new Vector2(_canvasPxW - 60f, 40f), 19, FontStyles.Bold, new Color(0.65f, 0.95f, 1f), TextAlignmentOptions.Center);

        // Stack vertikal: Route, Field, Actions, Setpoints
        panel.txtRoute = AddText(rt, "Txt_Route", new Vector2(0f, 372f), new Vector2(_canvasPxW - 70f, 130f), 19, FontStyles.Bold, new Color(0.90f, 0.96f, 1f), TextAlignmentOptions.TopLeft);
        panel.txtField = AddText(rt, "Txt_Field", new Vector2(0f, 222f), new Vector2(_canvasPxW - 70f, 140f), 19, FontStyles.Bold, new Color(0.56f, 0.95f, 0.66f), TextAlignmentOptions.TopLeft);
        panel.txtActions = AddText(rt, "Txt_Actions", new Vector2(0f, 30f), new Vector2(_canvasPxW - 70f, 230f), 19, FontStyles.Bold, Color.white, TextAlignmentOptions.TopLeft);
        panel.txtSetpoints = AddText(rt, "Txt_Setpoints", new Vector2(0f, -200f), new Vector2(_canvasPxW - 70f, 180f), 19, FontStyles.Bold, new Color(1f, 0.86f, 0.38f), TextAlignmentOptions.TopLeft);

        // Flow bar
        panel.flowBarBg = AddBand(rt, "Flow_Bar_Bg", new Vector2(0f, -320f), new Vector2(_canvasPxW - 80f, 30f), new Color(0.05f, 0.08f, 0.09f, 0.96f));
        var barGo = new GameObject("Flow_Bar_Fill", typeof(RectTransform), typeof(Image));
        barGo.transform.SetParent(panel.flowBarBg.rectTransform, false);
        var barRt = barGo.GetComponent<RectTransform>();
        barRt.anchorMin = new Vector2(0f, 0f);
        barRt.anchorMax = new Vector2(0f, 1f);
        barRt.pivot = new Vector2(0f, 0.5f);
        barRt.anchoredPosition = Vector2.zero;
        barRt.sizeDelta = new Vector2(1f, 0f);
        panel.flowBar = barGo.GetComponent<Image>();
        panel.flowBar.color = new Color(0.22f, 0.80f, 0.45f, 0.98f);

        // Alarm band (bawah)
        panel.alarmBand = AddBand(rt, "AlarmBand", new Vector2(0f, -540f), new Vector2(_canvasPxW, 110f), new Color(0.08f, 0.06f, 0.02f, 0.96f));
        panel.txtAlarm = AddText(rt, "Txt_Alarm", new Vector2(0f, -540f), new Vector2(_canvasPxW - 50f, 96f), 17, FontStyles.Bold, new Color(1f, 0.86f, 0.2f), TextAlignmentOptions.Center);

        return panel;
    }

    private static Image AddBand(RectTransform parent, string name, Vector2 pos, Vector2 size, Color color)
    {
        var go = new GameObject(name, typeof(RectTransform), typeof(Image));
        go.transform.SetParent(parent, false);
        var rt = go.GetComponent<RectTransform>();
        rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
        rt.anchoredPosition = pos;
        rt.sizeDelta = size;
        var img = go.GetComponent<Image>();
        img.color = color;
        return img;
    }

    private static TextMeshProUGUI AddText(RectTransform parent, string name, Vector2 pos, Vector2 size, int fontSize, FontStyles style, Color color, TextAlignmentOptions align)
    {
        var go = new GameObject(name, typeof(RectTransform), typeof(TextMeshProUGUI));
        go.transform.SetParent(parent, false);
        var rt = go.GetComponent<RectTransform>();
        rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
        rt.anchoredPosition = pos;
        rt.sizeDelta = size;
        var text = go.GetComponent<TextMeshProUGUI>();
        text.fontSize = fontSize;
        text.fontStyle = style;
        text.color = color;
        text.alignment = align;
        text.enableWordWrapping = true;
        text.overflowMode = TextOverflowModes.Truncate;
        return text;
    }

    private void Refresh()
    {
        BuildIfNeeded();

        GameLevelManager glm = GameLevelManager.Instance;
        GameLevelManager.GameLevel level = glm != null ? glm.CurrentLevel : GameLevelManager.GameLevel.Level1_APD;

        PanelCopy copy = BuildCopy(level, glm);

        // Saat Level 6, panel fisik "L6_DCS_AcidControlPanel_Runtime" menempati layar KIRI.
        // Sembunyikan side-monitor kiri supaya tidak dobel/overlap.
        bool hideLeft = level == GameLevelManager.GameLevel.Level6_AcidInjection
                        && GameObject.Find("L6_DCS_AcidControlPanel_Runtime") != null;

        ApplyToSide(_left, copy, hideLeft);
        ApplyToSide(_right, copy, false);
    }

    private void ApplyToSide(SidePanel panel, PanelCopy copy, bool forceHide)
    {
        if (panel == null)
            return;

        if (panel.canvas != null && panel.canvas.gameObject.activeSelf == forceHide)
            panel.canvas.gameObject.SetActive(!forceHide);
        if (forceHide)
            return;

        ApplyToSideInner(panel, copy);
    }

    private void ApplyToSideInner(SidePanel panel, PanelCopy copy)
    {
        if (panel == null)
            return;

        bool active = copy.hasPanel && copy.side == panel.side;

        panel.bg.color = active ? _bgActive : _bgIdle;
        panel.headerBand.color = active ? _headerActive : _headerIdle;
        panel.txtTitle.color = active ? _titleActive : _titleIdle;

        string sideTag = panel.side == Side.Left ? "MONITOR KIRI" : "MONITOR KANAN";

        if (active)
        {
            panel.txtTitle.text = copy.title;
            panel.txtSideTag.text = sideTag + "  •  AKTIF";
            panel.txtStatus.text = copy.status;
            panel.txtRoute.text = copy.route;
            panel.txtField.text = copy.field;
            panel.txtActions.text = copy.actions;
            panel.txtSetpoints.text = copy.setpoints;
            panel.txtAlarm.text = copy.alarm;

            panel.txtStatus.color = new Color(0.65f, 0.95f, 1f);
            panel.txtRoute.color = new Color(0.90f, 0.96f, 1f);
            panel.txtField.color = new Color(0.56f, 0.95f, 0.66f);
            panel.txtActions.color = Color.white;
            panel.txtSetpoints.color = new Color(1f, 0.86f, 0.38f);
            panel.txtAlarm.color = new Color(1f, 0.86f, 0.2f);

            panel.flowBarBg.gameObject.SetActive(true);
            float w = Mathf.Lerp(6f, _canvasPxW - 80f, Mathf.Clamp01(copy.flow01));
            panel.flowBar.rectTransform.sizeDelta = new Vector2(w, panel.flowBar.rectTransform.sizeDelta.y);
        }
        else
        {
            string tag = panel.side == Side.Left ? "SISI KIRI" : "SISI KANAN";
            panel.txtTitle.text = "DCS " + tag;
            panel.txtSideTag.text = sideTag + "  •  STANDBY";
            panel.txtStatus.text = "Tidak ada task di sisi ini untuk level aktif.";
            panel.txtRoute.text = "PROCESS\nMonitor siaga. Panel level akan muncul di sisi sesuai posisi mesin.";
            panel.txtField.text = "";
            panel.txtActions.text = "";
            panel.txtSetpoints.text = "";
            panel.txtAlarm.text = "STANDBY";

            Color dim = new Color(0.35f, 0.42f, 0.46f);
            panel.txtStatus.color = dim;
            panel.txtRoute.color = dim;
            panel.txtField.color = dim;
            panel.txtActions.color = dim;
            panel.txtSetpoints.color = dim;
            panel.txtAlarm.color = dim;

            panel.flowBarBg.gameObject.SetActive(false);
        }
    }

    private PanelCopy BuildCopy(GameLevelManager.GameLevel level, GameLevelManager glm)
    {
        float flow = glm != null ? glm.FlowRate : 0f;
        float temp = glm != null ? glm.Suhu : 25f;
        float pressure = glm != null ? glm.Tekanan : 1f;
        float ph = glm != null ? glm.PH : 7f;
        float rpm = glm != null ? glm.RPM : 0f;
        float acid = glm != null ? glm.AcidRatio : 0f;
        float stroke = glm != null ? glm.AcidStroke : 0f;

        var c = new PanelCopy { hasPanel = true, side = Side.Right, flow01 = Mathf.Clamp01(flow / 600f) };
        string dcs = _lastDcsButton <= 0 ? "-" : _lastDcsButton.ToString();

        switch (level)
        {
            case GameLevelManager.GameLevel.Level2_DCSPrep:
                c.side = Side.Right;
                c.title = "DCS PREPARATION";
                c.status = "MODE: STARTUP  •  DCS " + dcs;
                c.route = "PROCESS ROUTE\nPower DCS -> Cek interlock -> Konfirmasi area crusher ready";
                c.field = "FIELD STATE\nArea crusher standby.\nBelum ada perintah flow.";
                c.actions = "CHECKLIST\n[ ] Tekan tombol DCS 2\n[ ] Cek status monitor\n[ ] Lapor HT 'siapkan area'";
                c.setpoints = "STATUS\nDCS Power: ON\nInterlock: CHECK\nArea: CRUSHER READY";
                c.alarm = "Verifikasi DCS sebelum energize equipment.";
                c.flow01 = 0f;
                break;

            case GameLevelManager.GameLevel.Level3_OreSlurry:
                c.side = Side.Right;
                c.title = "ORE -> SLURRY TANK";
                c.status = "MODE: LEVEL 3  •  DCS " + dcs;
                c.route = "PROCESS ROUTE\nCrusher discharge -> Slurry tank\nTarget level tank: 75%";
                c.field = "FIELD STATE\nOre feed dipantau dari monitor ini.\nAmati ore masuk slurry tank.";
                c.actions = "CHECKLIST\n[ ] Tekan DCS 3\n[ ] Lapor HT awal\n[ ] Pastikan ore masuk tank\n[ ] Lapor HT akhir";
                c.setpoints = $"SLURRY MONITOR\nTank Level: 75%\nAgitator: 46 RPM\nFlow Prep: {Mathf.Max(flow, 120f):F0} m3/h";
                c.alarm = "Pantau level tank, jangan overflow.";
                c.flow01 = 0.35f;
                break;

            case GameLevelManager.GameLevel.Level4_SlurryPump:
                c.side = Side.Right;
                c.title = "SLURRY PUMP CONTROL";
                c.status = "MODE: LEVEL 4  •  DCS " + dcs;
                c.route = "PROCESS ROUTE\nSlurry tank -> Pump -> Discharge pipe -> Pre-Heater";
                c.field = "FIELD STATE\nSlurry mengalir bertahap di pipa.\nAmati aliran sampai pre-heater.";
                c.actions = "CHECKLIST\n[ ] Tekan DCS 4\n[ ] Set flow 450 m3/h ([+]/[-] di meja)\n[ ] Lapor 'slurry pump aktif'\n[ ] Lapor 'cairan di preheater'";
                c.setpoints = $"PUMP SETPOINT\nTarget Flow: 450 m3/h\nFlow Now: {flow:F0} m3/h\nStatus: {(flow >= 440f ? "ON TARGET" : "ADJUSTING")}";
                c.alarm = "Atur flow rate dari tombol +/- console.";
                break;

            case GameLevelManager.GameLevel.Level5_SteamValve:
                c.side = Side.Right;
                c.title = "PRE-HEATER STEAM VALVE";
                c.status = "MODE: LEVEL 5  •  DCS " + dcs;
                c.route = "PROCESS ROUTE\nSteam header -> Manual valve -> Pre-Heater shell";
                c.field = "FIELD STATE\nTurun ke lapangan, putar handwheel steam perlahan.";
                c.actions = "CHECKLIST\n[ ] Tekan DCS 5\n[ ] Lapor 'aktifkan pre-heater'\n[ ] Putar steam handwheel\n[ ] Lapor 'katup steam terbuka'";
                c.setpoints = $"PRE-HEATER\nTarget: 180-200 C\nTemp Now: {temp:F1} C\nValve: SLOW INDUSTRIAL";
                c.alarm = "Putar valve pelan. Pantau kenaikan suhu.";
                c.flow01 = 0.5f;
                break;

            case GameLevelManager.GameLevel.Level6_AcidInjection:
                c.side = Side.Left;
                c.title = "AUTOCLAVE MONITORING";
                c.status = "MODE: LEVEL 6 - DCS " + dcs;
                c.route = "AUTOCLAVE PARAMETER\nPre-Heater outlet -> Autoclave\nPantau reactor setelah slurry masuk";
                c.field = "FIELD STATE\nCek parameter utama di layar DCS.\nPastikan suhu, tekanan, dan RPM stabil.";
                c.actions = "CHECKLIST\n[ ] Tekan DCS 6\n[ ] Lapor HT outlet preheater\n[ ] Putar valve slurry preheater\n[ ] Pantau suhu/tekanan/RPM\n[ ] Lapor parameter stabil";
                c.setpoints = $"AUTOCLAVE\nTemp: {temp:F1} C (TGT 252)\nPressure: {pressure:F1} atm (TGT 47.5)\nAgitator: {rpm:F0} RPM (TGT 60)\npH: {ph:F2}";
                c.alarm = "Pantau parameter autoclave seperti layout DCS awal.";
                c.flow01 = 0.55f;
                break;

            case GameLevelManager.GameLevel.Level7_Autoclave:
                c.side = Side.Left;
                c.title = "AUTOCLAVE INSPECTION";
                c.status = "MODE: LEVEL 7  •  DCS " + dcs;
                c.route = "AUTOCLAVE (HPAL CORE)\n250 C / 47.5 atm  •  agitator 60 RPM";
                c.field = "FIELD STATE\nBuka valve underflow, amati X-Ray slurry naik.\nAutoclave di sisi kiri plant.";
                c.actions = "CHECKLIST\n[ ] Tekan DCS 7\n[ ] Aktifkan X-Ray view\n[ ] Catat scale & gauge\n[ ] Safety drill\n[ ] Lapor suhu/tekanan/RPM";
                c.setpoints = $"AUTOCLAVE\nTemp: {temp:F1} C (TGT 252)\nPressure: {pressure:F1} atm (TGT 47.5)\nAgitator: {rpm:F0} RPM (TGT 60)";
                c.alarm = "Reaktor bertekanan tinggi. Patuhi SOP inspeksi.";
                c.flow01 = 0.6f;
                break;

            case GameLevelManager.GameLevel.Level8_Monitoring:
                c.side = Side.Left;
                c.title = "FLASH VESSEL & LETDOWN";
                c.status = "MODE: LEVEL 8  •  DCS " + dcs;
                c.route = "PROCESS ROUTE\nAutoclave -> FV1 -> FV2 -> FV3 (letdown bertahap)";
                c.field = "FIELD STATE\nFlash train di sisi kiri plant.\nPutar handwheel tiap vessel, recover steam.";
                c.actions = "CHECKLIST\n[ ] Tekan DCS 8\n[ ] Buka valve autoclave\n[ ] FV1 -> FV2 -> FV3 (lapor tiap fase)\n[ ] Lapor 'flash train stable'";
                c.setpoints = $"FLASH TRAIN\nTemp: {temp:F1} C (TGT 100)\nPressure: {pressure:F1} atm (TGT 1.0)\nSteam recovery: ON";
                c.alarm = "Turunkan tekanan bertahap. Awas semburan uap.";
                c.flow01 = 0.7f;
                break;

            case GameLevelManager.GameLevel.Level10_CCD:
                c.side = Side.Right;
                c.title = "CCD & PLS SAMPLING";
                c.status = "MODE: LEVEL 9 (CCD)  •  DCS " + dcs;
                c.route = "PROCESS ROUTE\nCCD1 -> CCD2 -> CCD3 (counter-current decantation)";
                c.field = "FIELD STATE\nThickener train di sisi kanan plant.\nAmbil 3 sample PLS overflow.";
                c.actions = "CHECKLIST\n[ ] Tekan DCS 9\n[ ] Aktifkan rake CCD\n[ ] Ambil 3 sample PLS\n[ ] Submit lab QC\n[ ] Lapor 'CCD aktif PLS lulus QC'";
                c.setpoints = "CCD MONITOR\nUnderflow density: OK\nWash ratio: OK\nPLS clarity: CHECK";
                c.alarm = "Pisahan padat-cair harus stabil sebelum sampling.";
                c.flow01 = 0.5f;
                break;

            case GameLevelManager.GameLevel.Level11_MHP:
                c.side = Side.Right;
                c.title = "NEUTRALIZATION & MHP";
                c.status = "MODE: LEVEL 10 (MHP)  •  DCS " + dcs;
                c.route = "PROCESS ROUTE\nPLS -> Neutralization -> Polishing -> MHP precip";
                c.field = "FIELD STATE\nTangki MHP di sisi kanan plant.\nBentuk MHP, ambil sampel produk.";
                c.actions = "CHECKLIST\n[ ] Tekan DCS 10\n[ ] Dosing reagent (pH naik)\n[ ] Bentuk MHP (Ni-Co hijau)\n[ ] Ambil sampel produk\n[ ] Lapor 'MHP terbentuk'";
                c.setpoints = $"NEUTRALIZATION\npH Now: {ph:F2} (TGT 5.5)\nReagent: MgO dosing\nProduk: MHP Ni-Co";
                c.alarm = "Jaga pH untuk presipitasi selektif Ni-Co.";
                c.flow01 = 0.4f;
                break;

            case GameLevelManager.GameLevel.Level12_TailingDischarge:
                c.side = Side.Right;
                c.title = "TAILING & FILTER PRESS";
                c.status = "MODE: LEVEL 11  •  DCS " + dcs;
                c.route = "PROCESS ROUTE\nTailing -> Neutralization -> Filter press -> Cake";
                c.field = "FIELD STATE\nArea tailing di sisi kanan plant.\nJalankan filter press, cek cake.";
                c.actions = "CHECKLIST\n[ ] Tekan DCS 11\n[ ] Netralisasi tailing\n[ ] Jalankan filter press\n[ ] Lapor 'limbah dialirkan'";
                c.setpoints = $"TAILING\npH Now: {ph:F2} (TGT 7.5)\nFilter press: RUN\nCake: SIAP DRY STACK";
                c.alarm = "Pastikan cake siap sebelum kirim ke dry stack.";
                c.flow01 = 0.45f;
                break;

            case GameLevelManager.GameLevel.Level13_TailingWaste:
                c.side = Side.Right;
                c.title = "DRY STACK TAILING";
                c.status = "MODE: LEVEL 12  •  DCS " + dcs;
                c.route = "PROCESS ROUTE\nCake -> Conveyor -> Dry stack facility (DSTF)";
                c.field = "FIELD STATE\nDry stack di sisi kanan/belakang plant.\nPolishing pH, padatkan cake.";
                c.actions = "CHECKLIST\n[ ] Tekan DCS 12\n[ ] Polishing pH ke 8.5\n[ ] Tekan cake moisture < 25%\n[ ] Lapor 'tailing aman'";
                c.setpoints = $"DRY STACK\npH Now: {ph:F2} (TGT 8.5)\nCake moisture: < 25%\nContainment: AMAN";
                c.alarm = "B3 area. Verifikasi netralisasi sebelum disposal.";
                c.flow01 = 0.3f;
                break;

            case GameLevelManager.GameLevel.Level14_Emergency:
                c.side = Side.Left;
                c.title = "EMERGENCY / ESD";
                c.status = "MODE: DARURAT  •  DCS " + dcs;
                c.route = "EMERGENCY\nKebocoran/tekanan kritis terdeteksi di sektor proses.";
                c.field = "FIELD STATE\nSumber bocor di sisi kiri plant.\nUap/asap menyembur, alarm aktif.";
                c.actions = "CHECKLIST\n[ ] Lapor HT 'emergency, evakuasi'\n[ ] Tekan tombol ESD merah\n[ ] Pastikan valve & pompa mati";
                c.setpoints = $"EMERGENCY\nPressure: {pressure:F1} atm\nTemp: {temp:F1} C\nESD: STANDBY";
                c.alarm = "TEKAN ESD! Matikan semua pompa & valve asam/steam.";
                c.flow01 = 1f;
                break;

            default:
                // Level 0/1/9: tidak ada panel mesin di sisi mana pun.
                c.hasPanel = false;
                break;
        }

        return c;
    }
}
