using UnityEngine;
using UnityEngine.UI;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine.SceneManagement;

public class OliviaUIBuilder : EditorWindow
{
    [MenuItem("OLIVIA/1 - Build DCS Monitor (World Space)")]
    public static void BuildDCS()
    {
        var old = GameObject.Find("DCS_Monitor_Canvas");
        if (old != null) DestroyImmediate(old);

        Color cBg = new Color(0.05f, 0.07f, 0.12f);
        Color cPanel = new Color(0.07f, 0.10f, 0.18f);
        Color cHeader = new Color(0.05f, 0.14f, 0.28f);
        Color cBlue = new Color(0.3f, 0.85f, 1f);
        Color cGreen = new Color(0.2f, 0.95f, 0.4f);
        Color cYellow = new Color(1f, 0.85f, 0.1f);
        Color cGray = new Color(0.55f, 0.55f, 0.65f);

        var canvasGO = new GameObject("DCS_Monitor_Canvas");
        var canvas = canvasGO.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.WorldSpace;
        canvasGO.AddComponent<CanvasScaler>();
        canvasGO.AddComponent<GraphicRaycaster>();
        canvasGO.transform.position = new Vector3(0f, 1.4f, 2.5f);
        
        // Dibuat 2x lipat dimensinya agar font resolution naik drastis (tajam)
        canvasGO.transform.localScale = Vector3.one * 0.001f;
        canvasGO.GetComponent<RectTransform>().sizeDelta = new Vector2(1800, 1080);

        var bg = MkPanel("BG", canvasGO.transform, cBg, 0.97f);
        Stretch(bg);

        var header = MkPanel("Header", bg.transform, cHeader, 0.96f);
        AnchorTop(header, 120);

        var txtJudul = MkTMP("Txt_Judul", header.transform, "HPAL REACTOR MONITORING SYSTEM  v2.1", 30, cBlue, TextAlignmentOptions.Center);
        StretchWithPad(txtJudul.gameObject, 0, 36);

        var txtFase = MkTMP("Txt_StatusFase", header.transform, "STATUS: STANDBY", 18, cGray, TextAlignmentOptions.Right);
        AnchorBottomFull(txtFase.gameObject, 36, 28);

        var pAlarm = MkPanel("Panel_Alarm", bg.transform, new Color(0.06f, 0.18f, 0.42f), 0.92f);
        RectTransform pAlarmRT = pAlarm.GetComponent<RectTransform>();
        pAlarmRT.anchorMin = new Vector2(0, 1);
        pAlarmRT.anchorMax = new Vector2(1, 1);
        pAlarmRT.pivot = new Vector2(0.5f, 1f);
        pAlarmRT.sizeDelta = new Vector2(-40, 84);
        pAlarmRT.anchoredPosition = new Vector2(0, -124);
        pAlarm.SetActive(false);
        var txtAlarm = MkTMP("Txt_Alarm", pAlarm.transform, "NOTIFIKASI", 24, Color.white, TextAlignmentOptions.Center);
        Stretch(txtAlarm.gameObject);

        var pKiri = MkPanel("Panel_Reaktor", bg.transform, cPanel, 0.9f);
        RectTransform pKiriRT = pKiri.GetComponent<RectTransform>();
        pKiriRT.anchorMin = Vector2.zero;
        pKiriRT.anchorMax = Vector2.zero;
        pKiriRT.pivot = Vector2.zero;
        pKiriRT.sizeDelta = new Vector2(860, 780);
        pKiriRT.anchoredPosition = new Vector2(20, 20);

        MkTMPPivotTop("Lbl_Reaktor", pKiri.transform, "[ PARAMETER REAKTOR AUTOCLAVE ]", 20, cBlue, TextAlignmentOptions.Center, new Vector2(0, -16), new Vector2(-20, 52));

        string[] lblR = { "TEMPERATUR REAKTOR", "TEKANAN AUTOCLAVE", "TINGKAT KEASAMAN (pH)", "LAJU ALIRAN ASAM" };
        string[] valR = { "Val_Suhu", "Val_Tekanan", "Val_PH", "Val_Flow" };
        string[] defR = { "248.7 C", "49.2 Bar", "pH 0.93", "12.4 m3/h" };
        float[] szR = { 52, 52, 52, 40 };

        for (int i = 0; i < 4; i++)
        {
            float y = -92f + (i * -148f);
            MkTMPPivotTop("Lbl_" + i, pKiri.transform, lblR[i], 16, cGray, TextAlignmentOptions.Left, new Vector2(30, y), new Vector2(-40, 40));
            MkTMPPivotTop(valR[i], pKiri.transform, defR[i], szR[i], cGreen, TextAlignmentOptions.Left, new Vector2(30, y - 34), new Vector2(-40, 72));
        }

        MkTMPAnchorBottom("Val_RPM", pKiri.transform, "RPM: 45.2", 24, cBlue, new Vector2(0, 0), new Vector2(0.5f, 0), new Vector2(0, 0), new Vector2(30, 28), new Vector2(0, 56));
        MkTMPAnchorBottom("Val_Scale", pKiri.transform, "SCALE: 22.1%", 24, cGreen, new Vector2(0.5f, 0), new Vector2(1, 0), new Vector2(0, 0), new Vector2(0, 28), new Vector2(-20, 56));

        var pKanan = MkPanel("Panel_Mesin", bg.transform, cPanel, 0.9f);
        RectTransform pKananRT = pKanan.GetComponent<RectTransform>();
        pKananRT.anchorMin = new Vector2(1, 0);
        pKananRT.anchorMax = new Vector2(1, 0);
        pKananRT.pivot = new Vector2(1, 0);
        pKananRT.sizeDelta = new Vector2(880, 780);
        pKananRT.anchoredPosition = new Vector2(-20, 20);

        MkTMPPivotTop("Lbl_Mesin", pKanan.transform, "[ STATUS OUTPUT REAKTOR ]", 20, cBlue, TextAlignmentOptions.Center, new Vector2(0, -16), new Vector2(-20, 52));

        var bgStatus = MkPanel("BG_Status", pKanan.transform, new Color(0.04f, 0.06f, 0.1f), 0.85f);
        RectTransform bgStatusRT = bgStatus.GetComponent<RectTransform>();
        bgStatusRT.anchorMin = new Vector2(0, 1);
        bgStatusRT.anchorMax = new Vector2(1, 1);
        bgStatusRT.pivot = new Vector2(0.5f, 1f);
        bgStatusRT.sizeDelta = new Vector2(-32, 108);
        bgStatusRT.anchoredPosition = new Vector2(0, -84);

        var lblSM = MkTMP("Lbl_StatusMesin", bgStatus.transform, "STATUS MESIN", 18, cGray);
        RectTransform lblSMrt = lblSM.GetComponent<RectTransform>();
        lblSMrt.anchorMin = new Vector2(0, 0.5f);
        lblSMrt.anchorMax = new Vector2(0.5f, 0.5f);
        lblSMrt.pivot = new Vector2(0, 0.5f);
        lblSMrt.sizeDelta = new Vector2(0, 56);
        lblSMrt.anchoredPosition = new Vector2(20, 0);

        var txtStatusMesin = MkTMP("Txt_StatusMesin", bgStatus.transform, "STANDBY", 36, cYellow, TextAlignmentOptions.Right);
        RectTransform smRT = txtStatusMesin.GetComponent<RectTransform>();
        smRT.anchorMin = new Vector2(0.45f, 0.5f);
        smRT.anchorMax = new Vector2(1, 0.5f);
        smRT.pivot = new Vector2(1, 0.5f);
        smRT.sizeDelta = new Vector2(-20, 60);
        smRT.anchoredPosition = Vector2.zero;

        string[] lblO = { "KADAR NIKEL OUTPUT", "EFISIENSI LEACHING", "INPUT ASAM SULFAT", "WAKTU PROSES" };
        string[] valO = { "Txt_Nikel", "Txt_Efisiensi", "Txt_Asam", "Txt_Waktu" };
        string[] defO = { "-- %", "-- %", "-- %", "0.0 min" };

        for (int i = 0; i < 4; i++)
        {
            float y = -208f + (i * -136f);
            MkTMPPivotTop("Lbl_O" + i, pKanan.transform, lblO[i], 16, cGray, TextAlignmentOptions.Left, new Vector2(30, y), new Vector2(-40, 40));
            MkTMPPivotTop(valO[i], pKanan.transform, defO[i], 44, cBlue, TextAlignmentOptions.Left, new Vector2(30, y - 32), new Vector2(-40, 64));
        }

        var pTask = MkPanel("Panel_Task_Mesin", pKanan.transform, new Color(0.04f, 0.1f, 0.04f), 0.85f);
        RectTransform pTaskRT = pTask.GetComponent<RectTransform>();
        pTaskRT.anchorMin = new Vector2(0, 0);
        pTaskRT.anchorMax = new Vector2(1, 0);
        pTaskRT.pivot = new Vector2(0.5f, 0);
        pTaskRT.sizeDelta = new Vector2(-32, 116);
        pTaskRT.anchoredPosition = new Vector2(0, 20);
        pTask.SetActive(false);

        var lblTask = MkTMP("Lbl_Task", pTask.transform, "SOP AKTIF:", 18, cGray);
        RectTransform lblTaskRT = lblTask.GetComponent<RectTransform>();
        lblTaskRT.anchorMin = new Vector2(0, 0.55f);
        lblTaskRT.anchorMax = new Vector2(1, 1);
        lblTaskRT.sizeDelta = new Vector2(-20, 0);
        lblTaskRT.anchoredPosition = new Vector2(20, 0);

        var taskScanner = MkTMP("Task_Scanner", pTask.transform, "[ ] Pasang Scanner ke Reaktor", 22, new Color(0.65f, 0.65f, 0.65f));
        RectTransform tsRT = taskScanner.GetComponent<RectTransform>();
        tsRT.anchorMin = new Vector2(0, 0.52f);
        tsRT.anchorMax = new Vector2(1, 0.52f);
        tsRT.pivot = new Vector2(0, 0.5f);
        tsRT.sizeDelta = new Vector2(-20, 44);
        tsRT.anchoredPosition = new Vector2(20, 0);

        var taskMesin = MkTMP("Task_Mesin", pTask.transform, "[ ] Aktifkan Mesin HPAL", 22, new Color(0.65f, 0.65f, 0.65f));
        RectTransform tmRT = taskMesin.GetComponent<RectTransform>();
        tmRT.anchorMin = new Vector2(0, 0);
        tmRT.anchorMax = new Vector2(1, 0.52f);
        tmRT.pivot = new Vector2(0, 0.5f);
        tmRT.sizeDelta = new Vector2(-20, 44);
        tmRT.anchoredPosition = new Vector2(20, 0);

        var dcs = canvasGO.AddComponent<DCSMonitorUI>();
        dcs.txtJudulMonitor = txtJudul;
        dcs.txtStatusFase = txtFase;
        dcs.txtSuhu = pKiri.transform.Find("Val_Suhu").GetComponent<TextMeshProUGUI>();
        dcs.txtTekanan = pKiri.transform.Find("Val_Tekanan").GetComponent<TextMeshProUGUI>();
        dcs.txtPH = pKiri.transform.Find("Val_PH").GetComponent<TextMeshProUGUI>();
        dcs.txtFlowRate = pKiri.transform.Find("Val_Flow").GetComponent<TextMeshProUGUI>();
        dcs.txtRPM = pKiri.transform.Find("Val_RPM").GetComponent<TextMeshProUGUI>();
        dcs.txtScaleLevel = pKiri.transform.Find("Val_Scale").GetComponent<TextMeshProUGUI>();
        dcs.txtKadarNikel = pKanan.transform.Find("Txt_Nikel").GetComponent<TextMeshProUGUI>();
        dcs.txtEfisiensi = pKanan.transform.Find("Txt_Efisiensi").GetComponent<TextMeshProUGUI>();
        dcs.txtKadarAsam = pKanan.transform.Find("Txt_Asam").GetComponent<TMPro.TextMeshProUGUI>();
        dcs.txtWaktuProses = pKanan.transform.Find("Txt_Waktu").GetComponent<TMPro.TextMeshProUGUI>();
        dcs.txtStatusMesin = txtStatusMesin;
        dcs.panelTaskMesin = pTask;
        dcs.taskScannerDCS = taskScanner;
        dcs.taskMesinDCS = taskMesin;
        dcs.panelAlarm = pAlarm;
        dcs.txtAlarm = txtAlarm;
        dcs.bgAlarm = pAlarm.GetComponent<UnityEngine.UI.Image>();

        EditorUtility.SetDirty(canvasGO);
        EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene());
        Selection.activeGameObject = canvasGO;
        Debug.Log("[OLIVIA] DCS Monitor berhasil dibangun ulang dengan High-Res!");
    }

    [MenuItem("OLIVIA/2 - Build Player HUD (Screen Space)")]
    public static void BuildHUD()
    {
        var old = GameObject.Find("Player_HUD_Canvas");
        if (old != null) DestroyImmediate(old);

        Color cBgDark = new Color(0.05f, 0.07f, 0.1f);
        Color cPanel = new Color(0.06f, 0.09f, 0.15f);
        Color cBlue = new Color(0.3f, 0.85f, 1f);
        Color cYellow = new Color(1f, 0.85f, 0.1f);
        Color cGray = new Color(0.6f, 0.6f, 0.65f);

        var canvasGO = new GameObject("Player_HUD_Canvas");
        var canvas = canvasGO.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 10;
        var cs = canvasGO.AddComponent<CanvasScaler>();
        cs.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        cs.referenceResolution = new Vector2(1920, 1080);
        cs.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
        cs.matchWidthOrHeight = 0.5f;
        canvasGO.AddComponent<GraphicRaycaster>();

        // Ukuran di-scale 2x agar jelas dan tajam
        var pQuest = MkPanel("Panel_Quest", canvasGO.transform, cBgDark, 0.88f);
        RectTransform pQuestRT = pQuest.GetComponent<RectTransform>();
        pQuestRT.anchorMin = new Vector2(1, 1);
        pQuestRT.anchorMax = new Vector2(1, 1);
        pQuestRT.pivot = new Vector2(1, 1);
        pQuestRT.sizeDelta = new Vector2(560, 620);
        pQuestRT.anchoredPosition = new Vector2(-40, -40);

        var bgH = MkPanel("BG_Header", pQuest.transform, new Color(0.06f, 0.16f, 0.3f), 0.95f);
        bgH.AddComponent<Outline>().effectColor = new Color(0.3f, 0.7f, 1f, 0.3f);
        AnchorTop(bgH, 76);

        var txtFaseLabel = MkTMP("Txt_FaseLabel", bgH.transform, "FASE 1 : PEMAKAIAN APD", 20, cYellow, TextAlignmentOptions.Center);
        Stretch(txtFaseLabel.gameObject);

        var div = MkPanel("Divider", pQuest.transform, cBlue, 0.25f);
        RectTransform divRT = div.GetComponent<RectTransform>();
        divRT.anchorMin = new Vector2(0, 1);
        divRT.anchorMax = new Vector2(1, 1);
        divRT.pivot = new Vector2(0.5f, 1);
        divRT.sizeDelta = new Vector2(-20, 2);
        divRT.anchoredPosition = new Vector2(0, -76);

        string[] apdLabels = { "Helm K3", "Rompi Safety", "Kacamata Pelindung", "Sepatu Safety", "Sarung Tangan", "Respirator", "Walkie Talkie" };
        string[] apdIds = { "Task_Helm", "Task_Rompi", "Task_Kacamata", "Task_Sepatu", "Task_SarungTangan", "Task_Respirator", "Task_WalkieTalkie" };

        var bgApd = MkPanel("BG_APD", pQuest.transform, cPanel, 0.7f);
        RectTransform bgApdRT = bgApd.GetComponent<RectTransform>();
        bgApdRT.anchorMin = new Vector2(0, 1);
        bgApdRT.anchorMax = new Vector2(1, 1);
        bgApdRT.pivot = new Vector2(0.5f, 1);
        bgApdRT.sizeDelta = new Vector2(0, 304);
        bgApdRT.anchoredPosition = new Vector2(0, -80);

        var lblApd = MkTMP("Lbl_APD", bgApd.transform, "ALAT PELINDUNG DIRI", 16, cBlue);
        RectTransform lblApdRT = lblApd.GetComponent<RectTransform>();
        lblApdRT.anchorMin = new Vector2(0, 1);
        lblApdRT.anchorMax = new Vector2(1, 1);
        lblApdRT.pivot = new Vector2(0.5f, 1);
        lblApdRT.sizeDelta = new Vector2(-20, 40);
        lblApdRT.anchoredPosition = new Vector2(0, -12);

        TextMeshProUGUI[] taskTxts = new TextMeshProUGUI[7];
        for (int i = 0; i < 7; i++)
        {
            float yPos = -56f + (i * -42f); // Spasi lebih rapat untuk 7 item
            var t = MkTMP(apdIds[i], bgApd.transform, "[ ] " + apdLabels[i], 18, cGray);
            RectTransform trt = t.GetComponent<RectTransform>();
            trt.anchorMin = new Vector2(0, 1);
            trt.anchorMax = new Vector2(1, 1);
            trt.pivot = new Vector2(0, 1);
            trt.sizeDelta = new Vector2(-32, 40);
            trt.anchoredPosition = new Vector2(16, yPos);
            taskTxts[i] = t;
        }

        // Panel Walkie Talkie Hint (Baru)
        var pWTHint = MkPanel("Panel_WalkieTalkie", pQuest.transform, new Color(0, 0, 0, 0.5f), 1f);
        RectTransform pWTHintRT = pWTHint.GetComponent<RectTransform>();
        pWTHintRT.anchorMin = new Vector2(0, 0);
        pWTHintRT.anchorMax = new Vector2(1, 0);
        pWTHintRT.pivot = new Vector2(0.5f, 0);
        pWTHintRT.sizeDelta = new Vector2(-20, 80);
        pWTHintRT.anchoredPosition = new Vector2(0, 120);

        var txtHTHint = MkTMP("Txt_HTHint", pWTHint.transform, "Ucapkan: 'siap'", 18, cYellow, TextAlignmentOptions.Center);
        Stretch(txtHTHint.gameObject);

        // Deskripsi Quest (Baru)
        var txtQuest = MkTMP("Txt_Quest", pQuest.transform, "Quest: -", 16, Color.white);
        RectTransform txtQuestRT = txtQuest.GetComponent<RectTransform>();
        txtQuestRT.anchorMin = new Vector2(0, 1);
        txtQuestRT.anchorMax = new Vector2(1, 1);
        txtQuestRT.pivot = new Vector2(0.5f, 1);
        txtQuestRT.sizeDelta = new Vector2(-40, 60);
        txtQuestRT.anchoredPosition = new Vector2(0, -390); // Di bawah APD list

        var pOps = MkPanel("Panel_Operasional", pQuest.transform, cPanel, 0.7f);
        RectTransform pOpsRT = pOps.GetComponent<RectTransform>();
        pOpsRT.anchorMin = new Vector2(0, 0);
        pOpsRT.anchorMax = new Vector2(1, 0);
        pOpsRT.pivot = new Vector2(0.5f, 0);
        pOpsRT.sizeDelta = new Vector2(0, 192);
        pOpsRT.anchoredPosition = new Vector2(0, 8);
        pOps.SetActive(false);

        var lblOps = MkTMP("Lbl_Ops", pOps.transform, "OPERASIONAL", 16, cBlue);
        RectTransform lblOpsRT = lblOps.GetComponent<RectTransform>();
        lblOpsRT.anchorMin = new Vector2(0, 1);
        lblOpsRT.anchorMax = new Vector2(1, 1);
        lblOpsRT.pivot = new Vector2(0.5f, 1);
        lblOpsRT.sizeDelta = new Vector2(-20, 40);
        lblOpsRT.anchoredPosition = new Vector2(0, -12);

        var txtParam = MkTMP("Txt_ParamInfo", pOps.transform, "Parameter: -", 18, Color.white, TextAlignmentOptions.Left);
        RectTransform txtParamRT = txtParam.GetComponent<RectTransform>();
        txtParamRT.anchorMin = new Vector2(0, 0);
        txtParamRT.anchorMax = new Vector2(1, 1);
        txtParamRT.pivot = new Vector2(0.5f, 0.5f);
        txtParamRT.sizeDelta = new Vector2(-32, -60);
        txtParamRT.anchoredPosition = new Vector2(16, -20);

        var pNotif = MkPanel("Panel_Notif", canvasGO.transform, new Color(0.06f, 0.18f, 0.42f), 0.95f);
        RectTransform pNotifRT = pNotif.GetComponent<RectTransform>();
        pNotifRT.anchorMin = new Vector2(0.2f, 0);
        pNotifRT.anchorMax = new Vector2(0.8f, 0);
        pNotifRT.pivot = new Vector2(0.5f, 0);
        pNotifRT.sizeDelta = new Vector2(0, 92);
        pNotifRT.anchoredPosition = new Vector2(0, 24);
        pNotif.SetActive(false);

        var txtNotif = MkTMP("Txt_Notif", pNotif.transform, "", 26, Color.white, TextAlignmentOptions.Center);
        Stretch(txtNotif.gameObject);

        var hud = canvasGO.AddComponent<PlayerHUD>();
        hud.txtLevelLabel = txtFaseLabel;
        hud.txtQuestLabel = txtQuest;
        hud.bgHeader = bgH.GetComponent<Image>();
        hud.taskHelm = taskTxts[0];
        hud.taskRompi = taskTxts[1];
        hud.taskKacamata = taskTxts[2];
        hud.taskSepatu = taskTxts[3];
        hud.taskSarungTangan = taskTxts[4];
        hud.taskRespirator = taskTxts[5];
        hud.taskWalkieTalkie = taskTxts[6];
        hud.panelOperasional = pOps;
        hud.txtParameterInfo = txtParam;
        hud.panelWalkieTalkieHint = pWTHint;
        hud.txtHintKataKunci = txtHTHint;
        hud.panelNotif = pNotif;
        hud.txtNotif = txtNotif;
        hud.bgNotif = pNotif.GetComponent<Image>();

        EditorUtility.SetDirty(canvasGO);
        EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene());
        Selection.activeGameObject = canvasGO;
        Debug.Log("[OLIVIA] Player HUD berhasil dibangun ulang dengan High-Res!");
    }

    static GameObject MkPanel(string n, Transform p, Color c, float a)
    {
        var go = new GameObject(n);
        go.transform.SetParent(p, false);
        go.AddComponent<RectTransform>();
        var img = go.AddComponent<Image>();
        img.color = new Color(c.r, c.g, c.b, a);
        return go;
    }

    static void Stretch(GameObject go)
    {
        RectTransform rt = go.GetComponent<RectTransform>();
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.sizeDelta = Vector2.zero;
        rt.anchoredPosition = Vector2.zero;
    }

    static void StretchWithPad(GameObject go, float px, float py)
    {
        RectTransform rt = go.GetComponent<RectTransform>();
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.sizeDelta = new Vector2(-px * 2, -py * 2);
        rt.anchoredPosition = Vector2.zero;
    }

    static void AnchorTop(GameObject go, float h)
    {
        RectTransform rt = go.GetComponent<RectTransform>();
        rt.anchorMin = new Vector2(0, 1);
        rt.anchorMax = new Vector2(1, 1);
        rt.pivot = new Vector2(0.5f, 1);
        rt.sizeDelta = new Vector2(0, h);
        rt.anchoredPosition = Vector2.zero;
    }

    static void AnchorBottomFull(GameObject go, float h, float padX)
    {
        RectTransform rt = go.GetComponent<RectTransform>();
        rt.anchorMin = new Vector2(0, 0);
        rt.anchorMax = new Vector2(1, 0);
        rt.pivot = new Vector2(0.5f, 0);
        rt.sizeDelta = new Vector2(-padX * 2, h);
        rt.anchoredPosition = new Vector2(0, 4);
    }

    static TextMeshProUGUI MkTMP(string n, Transform p, string txt, float sz, Color col, TextAlignmentOptions align = TextAlignmentOptions.Left)
    {
        var go = new GameObject(n);
        go.transform.SetParent(p, false);
        go.AddComponent<RectTransform>();
        var tmp = go.AddComponent<TextMeshProUGUI>();
        tmp.text = txt;
        tmp.fontSize = sz;
        tmp.color = col;
        tmp.alignment = align;
        tmp.fontStyle = FontStyles.Bold;
        return tmp;
    }

    static TextMeshProUGUI MkTMPPivotTop(string n, Transform p, string txt, float sz, Color col, TextAlignmentOptions align, Vector2 pos, Vector2 size)
    {
        var t = MkTMP(n, p, txt, sz, col, align);
        RectTransform rt = t.GetComponent<RectTransform>();
        rt.anchorMin = new Vector2(0, 1);
        rt.anchorMax = new Vector2(1, 1);
        rt.pivot = new Vector2(0, 1);
        rt.sizeDelta = size;
        rt.anchoredPosition = pos;
        return t;
    }

    static void MkTMPAnchorBottom(string n, Transform p, string txt, float sz, Color col, Vector2 ancMin, Vector2 ancMax, Vector2 pivot, Vector2 pos, Vector2 size)
    {
        var t = MkTMP(n, p, txt, sz, col);
        RectTransform rt = t.GetComponent<RectTransform>();
        rt.anchorMin = ancMin;
        rt.anchorMax = ancMax;
        rt.pivot = pivot;
        rt.sizeDelta = size;
        rt.anchoredPosition = pos;
    }
}
