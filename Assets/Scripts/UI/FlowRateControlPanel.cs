using UnityEngine;
using TMPro;
using UnityEngine.XR.Interaction.Toolkit.Interactables;

/// <summary>
/// OLIVIA VR - FlowRateControlPanel.cs
///
/// Panel kontrol flow rate slurry pump di area DCS untuk Level 4.
/// Auto-generate world-space canvas + tombol [+] / [-] + display monitor + label status.
///
/// Pemakaian: pasang di GameObject empty (anchor), tentukan posisi dunia-nya.
/// Script ini akan:
///   1. Auto-create child Canvas world-space + UI elements
///   2. Auto-attach DCSParameterControl preset FlowRate
///   3. Auto-wire tombol XR ke DCSParameterControl
///
/// Cara cepat: cukup attach script ini ke empty GameObject "FlowRatePanel_DCS" di scene.
/// </summary>
public class FlowRateControlPanel : MonoBehaviour
{
    [Header("=== Konfigurasi Panel ===")]
    [SerializeField] private Vector2 _ukuranCanvas = new Vector2(0.6f, 0.4f);
    [SerializeField] private float _scaleCanvas = 0.001f;
    [Tooltip("Hanya tampilkan panel saat Level 4 aktif. Default true.")]
    [SerializeField] private bool _hanyaSaatLevel4 = true;

    [Header("=== Konfigurasi DCSParameterControl ===")]
    [SerializeField] private float _nilaiAwal = 0f;
    [SerializeField] private float _nilaiMin = 0f;
    [SerializeField] private float _nilaiMax = 600f;
    [SerializeField] private float _stepPerTombol = 25f;
    [SerializeField] private float _nilaiTarget = 450f;
    [SerializeField] private float _toleransiTarget = 10f;
    [SerializeField] private string _satuanLabel = "m³/h";
    [SerializeField] private string _namaParameter = "Flow Rate";

    [Header("=== Warna ===")]
    [SerializeField] private Color _warnaPanel = new Color(0.06f, 0.10f, 0.18f, 0.95f);
    [SerializeField] private Color _warnaHeader = new Color(0.10f, 0.45f, 0.85f, 1f);
    [SerializeField] private Color _warnaTombol = new Color(0.20f, 0.60f, 0.95f, 1f);
    [SerializeField] private Color _warnaTombolHover = new Color(0.45f, 0.85f, 1f, 1f);

    private Canvas _canvas;
    private DCSParameterControl _control;
    private TextMeshProUGUI _monitorText;
    private TextMeshProUGUI _statusText;
    private GameObject _content;

    private void Awake()
    {
        BuatPanel();
    }

    private void Start()
    {
        if (_hanyaSaatLevel4)
            UpdateVisibilitas(GameLevelManager.Instance != null
                ? GameLevelManager.Instance.CurrentLevel
                : GameLevelManager.GameLevel.Level0_Tutorial);

        GameLevelManager.OnLevelStarted += OnLevelStarted;
    }

    private void OnDestroy()
    {
        GameLevelManager.OnLevelStarted -= OnLevelStarted;
    }

    private void OnLevelStarted(GameLevelManager.GameLevel level)
    {
        if (_hanyaSaatLevel4)
            UpdateVisibilitas(level);
    }

    private void UpdateVisibilitas(GameLevelManager.GameLevel level)
    {
        bool aktif = level == GameLevelManager.GameLevel.Level4_SlurryPump;
        if (_content != null)
            _content.SetActive(aktif);
    }

    private void BuatPanel()
    {
        // Canvas world-space
        var canvasGo = new GameObject("FlowRateCanvas");
        canvasGo.transform.SetParent(transform, false);
        canvasGo.transform.localPosition = Vector3.zero;
        canvasGo.transform.localRotation = Quaternion.identity;

        _canvas = canvasGo.AddComponent<Canvas>();
        _canvas.renderMode = RenderMode.WorldSpace;
        canvasGo.AddComponent<UnityEngine.UI.CanvasScaler>();
        canvasGo.AddComponent<UnityEngine.UI.GraphicRaycaster>();

        var canvasRect = canvasGo.GetComponent<RectTransform>();
        canvasRect.sizeDelta = new Vector2(_ukuranCanvas.x / _scaleCanvas, _ukuranCanvas.y / _scaleCanvas);
        canvasRect.localScale = Vector3.one * _scaleCanvas;

        _content = canvasGo;

        // Background panel
        var bg = BuatImage("BG", canvasGo.transform, _warnaPanel);
        var bgRect = bg.GetComponent<RectTransform>();
        bgRect.anchorMin = Vector2.zero; bgRect.anchorMax = Vector2.one;
        bgRect.offsetMin = Vector2.zero; bgRect.offsetMax = Vector2.zero;

        // Header
        var header = BuatImage("Header", canvasGo.transform, _warnaHeader);
        var hRect = header.GetComponent<RectTransform>();
        hRect.anchorMin = new Vector2(0f, 0.82f); hRect.anchorMax = new Vector2(1f, 1f);
        hRect.offsetMin = Vector2.zero; hRect.offsetMax = Vector2.zero;

        BuatText("HeaderText", header.transform, "FLOW RATE - SLURRY PUMP", 36, TextAlignmentOptions.Center, Color.white)
            .GetComponent<RectTransform>().AdjustToParent();

        // Monitor display
        var monitor = BuatImage("MonitorBG", canvasGo.transform, new Color(0f, 0f, 0f, 0.85f));
        var mRect = monitor.GetComponent<RectTransform>();
        mRect.anchorMin = new Vector2(0.05f, 0.45f); mRect.anchorMax = new Vector2(0.95f, 0.78f);
        mRect.offsetMin = Vector2.zero; mRect.offsetMax = Vector2.zero;

        _monitorText = BuatText("MonitorText", monitor.transform, "0.0 " + _satuanLabel, 80,
            TextAlignmentOptions.Center, new Color(0.4f, 1f, 0.6f, 1f));
        _monitorText.GetComponent<RectTransform>().AdjustToParent();
        _monitorText.fontStyle = FontStyles.Bold;

        // Status text
        _statusText = BuatText("StatusText", canvasGo.transform, "Tekan + untuk mulai", 24,
            TextAlignmentOptions.Center, Color.yellow);
        var sRect = _statusText.GetComponent<RectTransform>();
        sRect.anchorMin = new Vector2(0.05f, 0.30f); sRect.anchorMax = new Vector2(0.95f, 0.42f);
        sRect.offsetMin = Vector2.zero; sRect.offsetMax = Vector2.zero;

        // Target hint
        var hintText = BuatText("TargetHint", canvasGo.transform,
            $"TARGET: {_nilaiTarget:F0} {_satuanLabel} (±{_toleransiTarget:F0})",
            22, TextAlignmentOptions.Center, new Color(0.6f, 0.8f, 1f, 1f));
        var hintRect = hintText.GetComponent<RectTransform>();
        hintRect.anchorMin = new Vector2(0.05f, 0.20f); hintRect.anchorMax = new Vector2(0.95f, 0.30f);
        hintRect.offsetMin = Vector2.zero; hintRect.offsetMax = Vector2.zero;

        // Tombol [-] di anchor X kecil (di-render kiri canvas; karena panel rotate Y=180,
        // visual player akan kelihatan di KANAN). Logika: untuk player VR, swap visual:
        // anchor 0.08-0.40 → after Y=180 rotation, terlihat di kanan player.
        var btnMinus = BuatTombol3D("Btn_Minus", canvasGo.transform, "−");
        var bmRect = btnMinus.transform as RectTransform;
        bmRect.anchorMin = new Vector2(0.08f, 0.03f); bmRect.anchorMax = new Vector2(0.40f, 0.18f);
        bmRect.offsetMin = Vector2.zero; bmRect.offsetMax = Vector2.zero;

        // Tombol [+] di anchor X besar
        var btnPlus = BuatTombol3D("Btn_Plus", canvasGo.transform, "+");
        var bpRect = btnPlus.transform as RectTransform;
        bpRect.anchorMin = new Vector2(0.60f, 0.03f); bpRect.anchorMax = new Vector2(0.92f, 0.18f);
        bpRect.offsetMin = Vector2.zero; bpRect.offsetMax = Vector2.zero;

        // Buat XRSimpleInteractable di tombol agar XR ray bisa klik
        var simplePlus = btnPlus.gameObject.AddComponent<XRSimpleInteractable>();
        var simpleMinus = btnMinus.gameObject.AddComponent<XRSimpleInteractable>();

        // Attach DCSParameterControl
        _control = gameObject.GetComponent<DCSParameterControl>();
        if (_control == null) _control = gameObject.AddComponent<DCSParameterControl>();

        // Set field via SerializedObject (di edit mode) atau via reflection (di runtime)
        var so = new UnityEngine.Object[] { _control };
        SetPrivateField(_control, "_tipeParameter", DCSParameterControl.TipeParameter.FlowRate);
        SetPrivateField(_control, "_nilaiAwal", _nilaiAwal);
        SetPrivateField(_control, "_nilaiMin", _nilaiMin);
        SetPrivateField(_control, "_nilaiMax", _nilaiMax);
        SetPrivateField(_control, "_stepPerTombol", _stepPerTombol);
        SetPrivateField(_control, "_nilaiTarget", _nilaiTarget);
        SetPrivateField(_control, "_toleransiTarget", _toleransiTarget);
        SetPrivateField(_control, "_satuanLabel", _satuanLabel);
        SetPrivateField(_control, "_namaParameter", _namaParameter);
        SetPrivateField(_control, "_monitorText", _monitorText);
        SetPrivateField(_control, "_statusText", _statusText);
        SetPrivateField(_control, "_tombolPlus", simplePlus);
        SetPrivateField(_control, "_tombolMinus", simpleMinus);

        // Subscribe ke event control supaya bisa update visual sendiri kalau perlu
    }

    private void SetPrivateField(object obj, string fieldName, object value)
    {
        var field = obj.GetType().GetField(fieldName,
            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
        if (field != null)
            field.SetValue(obj, value);
    }

    private static UnityEngine.UI.Image BuatImage(string name, Transform parent, Color color)
    {
        var go = new GameObject(name, typeof(RectTransform));
        go.transform.SetParent(parent, false);
        var img = go.AddComponent<UnityEngine.UI.Image>();
        img.color = color;
        return img;
    }

    private static TextMeshProUGUI BuatText(string name, Transform parent, string text, float size,
        TextAlignmentOptions align, Color color)
    {
        var go = new GameObject(name, typeof(RectTransform));
        go.transform.SetParent(parent, false);
        var tmp = go.AddComponent<TextMeshProUGUI>();
        tmp.text = text;
        tmp.fontSize = size;
        tmp.alignment = align;
        tmp.color = color;
        tmp.enableWordWrapping = true;
        return tmp;
    }

    private GameObject BuatTombol3D(string name, Transform parent, string label)
    {
        var btnGo = new GameObject(name, typeof(RectTransform));
        btnGo.transform.SetParent(parent, false);

        var img = btnGo.AddComponent<UnityEngine.UI.Image>();
        img.color = _warnaTombol;

        var labelGo = new GameObject("Label", typeof(RectTransform));
        labelGo.transform.SetParent(btnGo.transform, false);
        var rectLabel = labelGo.GetComponent<RectTransform>();
        rectLabel.AdjustToParent();
        var tmp = labelGo.AddComponent<TextMeshProUGUI>();
        tmp.text = label;
        tmp.fontSize = 100;
        tmp.alignment = TextAlignmentOptions.Center;
        tmp.color = Color.white;
        tmp.fontStyle = FontStyles.Bold;

        // Tambah BoxCollider agar XR ray bisa hit. Auto-fit ke rect tiap frame.
        var bc = btnGo.AddComponent<BoxCollider>();
        bc.isTrigger = true;
        var fitter = btnGo.AddComponent<RectColliderAutoFit>();
        fitter.SetTarget(bc);

        return btnGo;
    }
}

/// <summary>
/// Resize BoxCollider supaya selalu match RectTransform tombol UI (dipakai untuk
/// XR ray-hitting tombol world-space). Canvas anchor-stretched bikin sizeDelta = 0
/// dan rect baru valid setelah layout pass, jadi kita hitung ulang tiap frame.
/// </summary>
[DisallowMultipleComponent]
[RequireComponent(typeof(RectTransform))]
public class RectColliderAutoFit : MonoBehaviour
{
    private BoxCollider _box;
    private RectTransform _rt;

    public void SetTarget(BoxCollider bc) => _box = bc;

    private void Awake()
    {
        _rt = transform as RectTransform;
        if (_box == null) _box = GetComponent<BoxCollider>();
    }

    private void LateUpdate()
    {
        if (_box == null || _rt == null) return;
        var rect = _rt.rect;
        if (rect.width <= 0f || rect.height <= 0f) return;
        // Pivot offset agar collider center align dengan rect center
        Vector3 size = new Vector3(rect.width, rect.height, 0.5f);
        Vector3 center = new Vector3(
            rect.width * (0.5f - _rt.pivot.x),
            rect.height * (0.5f - _rt.pivot.y),
            0f);
        _box.size = size;
        _box.center = center;
    }
}

/// <summary>
/// Helper extension untuk RectTransform agar fill parent.
/// </summary>
internal static class FlowRateControlPanelRectExt
{
    public static void AdjustToParent(this RectTransform rt)
    {
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.offsetMin = Vector2.zero;
        rt.offsetMax = Vector2.zero;
    }
}
