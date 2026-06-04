using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Builds a single industrial operations panel inside DCS_Monitor_Canvas.
/// The goal is to keep level instructions, field state, and DCS setpoints on one monitor.
/// </summary>
[DisallowMultipleComponent]
public class DCSUnifiedOperationsPanel : MonoBehaviour
{
    [Header("Runtime Layout")]
    [SerializeField] private RectTransform _root;
    [SerializeField] private TextMeshProUGUI _txtTitle;
    [SerializeField] private TextMeshProUGUI _txtStatus;
    [SerializeField] private TextMeshProUGUI _txtRoute;
    [SerializeField] private TextMeshProUGUI _txtField;
    [SerializeField] private TextMeshProUGUI _txtActions;
    [SerializeField] private TextMeshProUGUI _txtSetpoints;
    [SerializeField] private TextMeshProUGUI _txtAlarm;
    [SerializeField] private Image _flowBar;

    private int _lastDcsButton;

    private void Awake()
    {
        EnsureLayout();
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

    private void EnsureLayout()
    {
        if (_root != null)
            return;

        Transform parent = transform.Find("BG/Component");
        if (parent == null)
            parent = transform;

        GameObject rootGo = new GameObject("Unified_Operations_Panel", typeof(RectTransform), typeof(Image));
        rootGo.transform.SetParent(parent, false);
        _root = rootGo.GetComponent<RectTransform>();
        _root.anchorMin = new Vector2(0.5f, 0.5f);
        _root.anchorMax = new Vector2(0.5f, 0.5f);
        _root.pivot = new Vector2(0.5f, 0.5f);
        _root.anchoredPosition = new Vector2(420f, -22f);
        _root.sizeDelta = new Vector2(640f, 690f);

        Image bg = rootGo.GetComponent<Image>();
        bg.color = new Color(0.015f, 0.035f, 0.045f, 0.92f);

        AddBand(_root, "HeaderBand", new Vector2(0f, 300f), new Vector2(640f, 74f), new Color(0.02f, 0.22f, 0.34f, 0.98f));
        AddBand(_root, "AlarmBand", new Vector2(0f, -292f), new Vector2(590f, 74f), new Color(0.08f, 0.06f, 0.02f, 0.96f));

        _txtTitle = AddText(_root, "Txt_OpsTitle", new Vector2(0f, 308f), new Vector2(600f, 34f), 24, FontStyles.Bold, new Color(0.30f, 0.88f, 1f));
        _txtStatus = AddText(_root, "Txt_OpsStatus", new Vector2(0f, 270f), new Vector2(590f, 34f), 15, FontStyles.Bold, new Color(0.62f, 0.95f, 1f));
        _txtRoute = AddText(_root, "Txt_ProcessRoute", new Vector2(-145f, 170f), new Vector2(310f, 150f), 18, FontStyles.Bold, new Color(0.90f, 0.96f, 1f));
        _txtField = AddText(_root, "Txt_FieldState", new Vector2(165f, 170f), new Vector2(260f, 150f), 17, FontStyles.Bold, new Color(0.56f, 0.95f, 0.62f));
        _txtActions = AddText(_root, "Txt_OperatorActions", new Vector2(-150f, -22f), new Vector2(310f, 205f), 17, FontStyles.Bold, Color.white);
        _txtSetpoints = AddText(_root, "Txt_Setpoints", new Vector2(168f, -22f), new Vector2(260f, 205f), 17, FontStyles.Bold, new Color(1f, 0.86f, 0.35f));
        _txtAlarm = AddText(_root, "Txt_OpsAlarm", new Vector2(0f, -292f), new Vector2(560f, 54f), 17, FontStyles.Bold, new Color(1f, 0.86f, 0.18f));

        GameObject barBgGo = new GameObject("Flow_Bar_Background", typeof(RectTransform), typeof(Image));
        barBgGo.transform.SetParent(_root, false);
        RectTransform barBg = barBgGo.GetComponent<RectTransform>();
        barBg.anchorMin = barBg.anchorMax = new Vector2(0.5f, 0.5f);
        barBg.anchoredPosition = new Vector2(0f, -205f);
        barBg.sizeDelta = new Vector2(560f, 26f);
        barBgGo.GetComponent<Image>().color = new Color(0.05f, 0.08f, 0.09f, 0.96f);

        GameObject barGo = new GameObject("Flow_Bar_Fill", typeof(RectTransform), typeof(Image));
        barGo.transform.SetParent(barBgGo.transform, false);
        RectTransform bar = barGo.GetComponent<RectTransform>();
        bar.anchorMin = new Vector2(0f, 0f);
        bar.anchorMax = new Vector2(0f, 1f);
        bar.pivot = new Vector2(0f, 0.5f);
        bar.anchoredPosition = Vector2.zero;
        bar.sizeDelta = new Vector2(1f, 0f);
        _flowBar = barGo.GetComponent<Image>();
        _flowBar.color = new Color(0.22f, 0.78f, 0.42f, 0.98f);
    }

    private static void AddBand(RectTransform parent, string name, Vector2 pos, Vector2 size, Color color)
    {
        GameObject go = new GameObject(name, typeof(RectTransform), typeof(Image));
        go.transform.SetParent(parent, false);
        RectTransform rt = go.GetComponent<RectTransform>();
        rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
        rt.anchoredPosition = pos;
        rt.sizeDelta = size;
        go.GetComponent<Image>().color = color;
    }

    private static TextMeshProUGUI AddText(RectTransform parent, string name, Vector2 pos, Vector2 size, int fontSize, FontStyles style, Color color)
    {
        GameObject go = new GameObject(name, typeof(RectTransform), typeof(TextMeshProUGUI));
        go.transform.SetParent(parent, false);
        RectTransform rt = go.GetComponent<RectTransform>();
        rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
        rt.anchoredPosition = pos;
        rt.sizeDelta = size;
        TextMeshProUGUI text = go.GetComponent<TextMeshProUGUI>();
        text.fontSize = fontSize;
        text.fontStyle = style;
        text.color = color;
        text.alignment = TextAlignmentOptions.TopLeft;
        text.enableWordWrapping = true;
        text.overflowMode = TextOverflowModes.Ellipsis;
        return text;
    }

    private void Refresh()
    {
        EnsureLayout();

        GameLevelManager glm = GameLevelManager.Instance;
        GameLevelManager.GameLevel level = glm != null ? glm.CurrentLevel : GameLevelManager.GameLevel.Level1_APD;
        float flow = glm != null ? glm.FlowRate : 0f;
        float temp = glm != null ? glm.Suhu : 25f;
        float pressure = glm != null ? glm.Tekanan : 1f;
        float ph = glm != null ? glm.PH : 7f;

        string title;
        string status;
        string route;
        string field;
        string actions;
        string setpoints;
        string alarm;

        BuildCopy(level, flow, temp, pressure, ph, out title, out status, out route, out field, out actions, out setpoints, out alarm);

        if (_txtTitle != null) _txtTitle.text = title;
        if (_txtStatus != null) _txtStatus.text = status;
        if (_txtRoute != null) _txtRoute.text = route;
        if (_txtField != null) _txtField.text = field;
        if (_txtActions != null) _txtActions.text = actions;
        if (_txtSetpoints != null) _txtSetpoints.text = setpoints;
        if (_txtAlarm != null) _txtAlarm.text = alarm;

        if (_flowBar != null)
        {
            RectTransform rt = _flowBar.rectTransform;
            float width = Mathf.Lerp(8f, 560f, Mathf.Clamp01(flow / 600f));
            rt.sizeDelta = new Vector2(width, rt.sizeDelta.y);
        }
    }

    private void BuildCopy(GameLevelManager.GameLevel level, float flow, float temp, float pressure, float ph,
        out string title, out string status, out string route, out string field, out string actions, out string setpoints, out string alarm)
    {
        title = "HPAL DCS - OPERATIONS OVERVIEW";
        status = "MODE: STANDBY | LAST DCS BUTTON: " + (_lastDcsButton <= 0 ? "-" : _lastDcsButton.ToString());
        route = "PROCESS ROUTE\nCrusher -> Slurry Tank -> Pump -> Pre-Heater -> Autoclave";
        field = "FIELD STATE\nWaiting for operator confirmation.";
        actions = "OPERATOR CHECKLIST\n[ ] Tekan tombol DCS level aktif\n[ ] Verifikasi status proses\n[ ] Lapor HT sesuai instruksi";
        setpoints = $"SETPOINTS\nFlow: {flow:F0} m3/h\nTemp: {temp:F1} C\nPressure: {pressure:F1} atm\npH: {ph:F2}";
        alarm = "DCS ready. Follow SOP and verify field condition.";

        switch (level)
        {
            case GameLevelManager.GameLevel.Level2_DCSPrep:
                status = "MODE: DCS PREPARATION";
                route = "CONTROL ROOM STARTUP\nPower DCS -> Check interlock -> Confirm field ready";
                field = "FIELD STATE\nCrusher area standby.\nNo flow command active.";
                actions = "OPERATOR CHECKLIST\n[ ] Klik tombol DCS 2\n[ ] Cek status monitor\n[ ] Lapor HT persiapan area";
                setpoints = "SETPOINTS\nDCS Power: ON\nInterlock: CHECK\nArea: CRUSHER READY";
                alarm = "Industrial note: verify DCS before energizing equipment.";
                break;

            case GameLevelManager.GameLevel.Level3_OreSlurry:
                status = "MODE: LEVEL 3 - ORE & SLURRY INTEGRATED";
                route = "PROCESS ROUTE\nCrusher discharge -> Slurry tank\nTarget tank level: 75%";
                field = "FIELD STATE\nOre feed and slurry tank are monitored from this same canvas.\nNo separate slurry panel needed.";
                actions = "OPERATOR CHECKLIST\n[ ] Klik tombol DCS 3\n[ ] Lapor HT awal\n[ ] Pastikan ore masuk slurry tank\n[ ] Lapor HT akhir";
                setpoints = $"SLURRY MONITOR\nTank Level: 75%\nAgitator: 46 RPM\nFlow Prep: {Mathf.Max(flow, 120f):F0} m3/h";
                alarm = "DCS note: Level 3 and slurry preparation are consolidated on this monitor.";
                break;

            case GameLevelManager.GameLevel.Level4_SlurryPump:
                status = "MODE: LEVEL 4 - SLURRY PUMP CONTROL";
                route = "PROCESS ROUTE\nSlurry tank -> Pump suction -> Discharge pipe -> Pre-Heater";
                field = "FIELD STATE\nSlurry should travel gradually along the pipe.\nDo not show full pipe instantly.";
                actions = "OPERATOR CHECKLIST\n[ ] Klik tombol DCS 4\n[ ] Atur flow rate 450 m3/h\n[ ] Lapor 'slurry pump aktif'\n[ ] Observe field flow\n[ ] Lapor 'cairan sudah di preheater'";
                setpoints = $"PUMP SETPOINT\nTarget Flow: 450 m3/h\nCurrent Flow: {flow:F0} m3/h\nPump Status: {(flow >= 440f ? "ON TARGET" : "ADJUSTING")}";
                alarm = "DCS note: flow control and field observation stay in one canvas.";
                break;

            case GameLevelManager.GameLevel.Level5_SteamValve:
                status = "MODE: LEVEL 5 - PRE-HEATER STEAM VALVE";
                route = "PROCESS ROUTE\nSteam header -> Manual valve -> Pre-Heater shell";
                field = "FIELD STATE\nHandwheel is smoothed and rate-limited for industrial valve feel.";
                actions = "OPERATOR CHECKLIST\n[ ] Klik tombol DCS 5\n[ ] Lapor aktifkan pre-heater\n[ ] Putar steam handwheel perlahan\n[ ] Lapor katup steam terbuka";
                setpoints = $"PRE-HEATER\nTarget Temp: 180-200 C\nCurrent Temp: {temp:F1} C\nValve Motion: SLOW INDUSTRIAL";
                alarm = "Operate valve slowly. Watch temperature rise, not just hand motion.";
                break;

            case GameLevelManager.GameLevel.Level7_Autoclave:
                status = "MODE: AUTOCLAVE SPECIAL MONITOR";
                route = "AUTOCLAVE\nDedicated reactor monitoring remains on primary reactor panels.";
                field = "FIELD STATE\nAutoclave inspection uses specialist workflow.";
                actions = "OPERATOR CHECKLIST\nUse autoclave-specific panel and SOP.";
                setpoints = $"AUTOCLAVE\nTemp: {temp:F1} C\nPressure: {pressure:F1} atm\npH: {ph:F2}";
                alarm = "Autoclave is intentionally kept as a specialist monitor section.";
                break;
        }
    }
}
