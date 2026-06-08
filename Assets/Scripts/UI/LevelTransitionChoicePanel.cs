using System;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using UnityEngine.XR.Interaction.Toolkit.Interactables;

/// <summary>
/// OLIVIA VR - LevelTransitionChoicePanel.cs
///
/// Floating world-space panel di depan kamera dengan 2 tombol:
///   - "LANJUT" → trigger callback onLanjut (mis. fade out + transisi ke level berikutnya)
///   - "LIHAT PROSES" → trigger callback onLihat (panel tutup, player bebas eksplorasi)
///
/// Panel auto-build canvas + 2 button + label saat Awake. Tinggal Show(onLanjut, onLihat).
/// Posisi follow kamera dengan jarak fix di depan + smoothing.
/// </summary>
public class LevelTransitionChoicePanel : MonoBehaviour
{
    [Header("=== Posisi Panel di Depan Kamera ===")]
    [Tooltip("Jarak dari kamera (m).")]
    [SerializeField] private float _jarakDariKamera = 1.4f;
    [Tooltip("Offset Y dari titik tengah pandangan (negatif = di bawah).")]
    [SerializeField] private float _offsetY = -0.05f;
    [Tooltip("Smoothing posisi panel agar tidak nervous.")]
    [Range(0f, 0.5f)] [SerializeField] private float _smoothPos = 0.10f;
    [Tooltip("Smoothing rotasi panel.")]
    [Range(0f, 0.5f)] [SerializeField] private float _smoothRot = 0.10f;
    [Tooltip("Hanya ikut yaw kamera (tidak ikut nunduk/dongak).")]
    [SerializeField] private bool _ikutYawSaja = true;

    [Header("=== Ukuran Canvas ===")]
    [SerializeField] private Vector2 _ukuranCanvas = new Vector2(1.0f, 0.55f);
    [SerializeField] private float _scaleCanvas = 0.001f;

    [Header("=== Konten ===")]
    [TextArea(2, 4)]
    [SerializeField] private string _judulPesan = "Mesin pengaduk slurry sudah aktif";
    [TextArea(2, 4)]
    [SerializeField] private string _subPesan = "Pilih lanjutkan operasi atau amati proses dulu.";
    [SerializeField] private string _labelTombolLanjut = "LANJUT KE TAHAP BERIKUTNYA";
    [SerializeField] private string _labelTombolLihat = "LIHAT PROSES DULU";

    [Header("=== Warna ===")]
    [SerializeField] private Color _warnaPanelBg = new Color(0.05f, 0.10f, 0.18f, 0.95f);
    [SerializeField] private Color _warnaHeader = new Color(0.10f, 0.45f, 0.85f, 1f);
    [SerializeField] private Color _warnaTombolLanjut = new Color(0.20f, 0.75f, 0.30f, 1f);
    [SerializeField] private Color _warnaTombolLihat = new Color(0.85f, 0.55f, 0.15f, 1f);

    private Transform _camera;
    private Canvas _canvas;
    private GameObject _content;
    private Vector3 _smoothPosVel;
    private Quaternion _smoothRotCurrent = Quaternion.identity;

    private Action _callbackLanjut;
    private Action _callbackLihat;
    private bool _aktif;

    private void Awake()
    {
        BuatPanel();
        SetActiveContent(false);
    }

    private void OnEnable()
    {
        if (_camera == null && Camera.main != null)
            _camera = Camera.main.transform;
    }

    /// <summary>
    /// Tampilkan panel dengan callback. Panel auto-tutup saat tombol ditekan.
    /// </summary>
    public void Show(Action onLanjut, Action onLihat)
    {
        _callbackLanjut = onLanjut;
        _callbackLihat = onLihat;

        if (_camera == null && Camera.main != null)
            _camera = Camera.main.transform;

        _aktif = true;
        SetActiveContent(true);
        SyncronisasiInstan();
    }

    public void Hide()
    {
        _aktif = false;
        SetActiveContent(false);
        _callbackLanjut = null;
        _callbackLihat = null;
    }

    private void LateUpdate()
    {
        if (!_aktif) return;
        if (_camera == null)
        {
            if (Camera.main != null) _camera = Camera.main.transform;
            else return;
        }

        Vector3 fwd = _camera.forward;
        Vector3 up = Vector3.up;
        if (_ikutYawSaja)
        {
            fwd.y = 0f;
            if (fwd.sqrMagnitude < 0.0001f) fwd = Vector3.forward;
            fwd.Normalize();
        }

        Vector3 targetPos = _camera.position + fwd * _jarakDariKamera + up * _offsetY;
        if (_smoothPos <= 0.0001f)
            transform.position = targetPos;
        else
            transform.position = Vector3.SmoothDamp(transform.position, targetPos, ref _smoothPosVel, _smoothPos);

        Quaternion targetRot = Quaternion.LookRotation(fwd, up);
        if (_smoothRot <= 0.0001f)
            transform.rotation = targetRot;
        else
        {
            _smoothRotCurrent = Quaternion.Slerp(_smoothRotCurrent, targetRot, 1f - Mathf.Exp(-Time.deltaTime / _smoothRot));
            transform.rotation = _smoothRotCurrent;
        }
    }

    private void SyncronisasiInstan()
    {
        if (_camera == null) return;
        Vector3 fwd = _camera.forward;
        if (_ikutYawSaja) { fwd.y = 0f; fwd.Normalize(); }
        transform.position = _camera.position + fwd * _jarakDariKamera + Vector3.up * _offsetY;
        Quaternion rot = Quaternion.LookRotation(fwd, Vector3.up);
        transform.rotation = rot;
        _smoothRotCurrent = rot;
        _smoothPosVel = Vector3.zero;
    }

    private void SetActiveContent(bool aktif)
    {
        if (_content != null) _content.SetActive(aktif);
    }

    private void BuatPanel()
    {
        var canvasGo = new GameObject("ChoicePanel_Canvas");
        canvasGo.transform.SetParent(transform, false);
        canvasGo.transform.localPosition = Vector3.zero;
        canvasGo.transform.localRotation = Quaternion.identity;

        _canvas = canvasGo.AddComponent<Canvas>();
        _canvas.renderMode = RenderMode.WorldSpace;
        _canvas.sortingOrder = 50;
        canvasGo.AddComponent<CanvasScaler>();
        canvasGo.AddComponent<GraphicRaycaster>();

        var rect = canvasGo.GetComponent<RectTransform>();
        rect.sizeDelta = new Vector2(_ukuranCanvas.x / _scaleCanvas, _ukuranCanvas.y / _scaleCanvas);
        rect.localScale = Vector3.one * _scaleCanvas;

        _content = canvasGo;

        // Panel BG
        var bg = BuatImage("BG", canvasGo.transform, _warnaPanelBg);
        FillParent(bg.GetComponent<RectTransform>());

        // Header strip
        var header = BuatImage("Header", canvasGo.transform, _warnaHeader);
        var hr = header.GetComponent<RectTransform>();
        hr.anchorMin = new Vector2(0f, 0.78f); hr.anchorMax = new Vector2(1f, 1f);
        hr.offsetMin = Vector2.zero; hr.offsetMax = Vector2.zero;
        var hText = BuatText("HeaderText", header.transform, _judulPesan, 50,
            TextAlignmentOptions.Center, Color.white);
        FillParent(hText.GetComponent<RectTransform>());
        hText.fontStyle = FontStyles.Bold;

        // Sub message
        var sub = BuatText("SubPesan", canvasGo.transform, _subPesan, 38,
            TextAlignmentOptions.Center, new Color(0.85f, 0.92f, 1f, 1f));
        var sr = sub.GetComponent<RectTransform>();
        sr.anchorMin = new Vector2(0.05f, 0.50f); sr.anchorMax = new Vector2(0.95f, 0.74f);
        sr.offsetMin = Vector2.zero; sr.offsetMax = Vector2.zero;

        // Tombol LANJUT (kiri)
        var btnLanjut = BuatTombol("Btn_Lanjut", canvasGo.transform, _labelTombolLanjut, _warnaTombolLanjut);
        var blr = (RectTransform)btnLanjut.transform;
        blr.anchorMin = new Vector2(0.05f, 0.08f); blr.anchorMax = new Vector2(0.48f, 0.42f);
        blr.offsetMin = Vector2.zero; blr.offsetMax = Vector2.zero;

        // Tombol LIHAT (kanan)
        var btnLihat = BuatTombol("Btn_Lihat", canvasGo.transform, _labelTombolLihat, _warnaTombolLihat);
        var blhr = (RectTransform)btnLihat.transform;
        blhr.anchorMin = new Vector2(0.52f, 0.08f); blhr.anchorMax = new Vector2(0.95f, 0.42f);
        blhr.offsetMin = Vector2.zero; blhr.offsetMax = Vector2.zero;

        // Button click
        var uiBtnLanjut = btnLanjut.GetComponent<Button>();
        if (uiBtnLanjut == null) uiBtnLanjut = btnLanjut.AddComponent<Button>();
        uiBtnLanjut.onClick.AddListener(OnTombolLanjutDitekan);

        var uiBtnLihat = btnLihat.GetComponent<Button>();
        if (uiBtnLihat == null) uiBtnLihat = btnLihat.AddComponent<Button>();
        uiBtnLihat.onClick.AddListener(OnTombolLihatDitekan);

        // XR Simple Interactable di body button supaya XR ray bisa hit
        AttachXrSimpleInteractable(btnLanjut, OnTombolLanjutDitekan);
        AttachXrSimpleInteractable(btnLihat, OnTombolLihatDitekan);
    }

    private void OnTombolLanjutDitekan()
    {
        var cb = _callbackLanjut;
        Hide();
        cb?.Invoke();
    }

    private void OnTombolLihatDitekan()
    {
        var cb = _callbackLihat;
        Hide();
        cb?.Invoke();
    }

    private void AttachXrSimpleInteractable(GameObject btn, Action onSelect)
    {
        var rect = btn.GetComponent<RectTransform>();

        // Pastikan layout canvas terhitung supaya ukuran rect valid.
        // PENTING: untuk button anchor-stretch, rect.sizeDelta = ~0 (bukan ukuran asli),
        // sehingga collider lama nyaris tak berukuran -> ray VR tidak pernah kena.
        Canvas.ForceUpdateCanvases();
        float w = rect.rect.width;
        float h = rect.rect.height;
        if (w < 1f || h < 1f)
        {
            // Fallback: hitung dari fraksi anchor x ukuran canvas parent.
            var parentRect = _canvas != null ? _canvas.GetComponent<RectTransform>() : null;
            float pw = parentRect != null ? parentRect.rect.width : (_ukuranCanvas.x / _scaleCanvas);
            float ph = parentRect != null ? parentRect.rect.height : (_ukuranCanvas.y / _scaleCanvas);
            w = Mathf.Max(w, (rect.anchorMax.x - rect.anchorMin.x) * pw);
            h = Mathf.Max(h, (rect.anchorMax.y - rect.anchorMin.y) * ph);
        }

        var bc = btn.GetComponent<BoxCollider>();
        if (bc == null) bc = btn.AddComponent<BoxCollider>();
        bc.isTrigger = true;
        bc.size = new Vector3(Mathf.Max(w, 10f), Mathf.Max(h, 10f), 20f);
        bc.center = Vector3.zero;

        var simple = btn.GetComponent<XRSimpleInteractable>();
        if (simple == null) simple = btn.AddComponent<XRSimpleInteractable>();
        // Daftarkan collider secara eksplisit supaya XR ray pasti meng-hit interactable ini.
        if (!simple.colliders.Contains(bc))
        {
            simple.colliders.Clear();
            simple.colliders.Add(bc);
        }
        simple.selectEntered.AddListener(_ => onSelect?.Invoke());
        // Hover juga memicu (beberapa rig pakai hover+trigger), tapi select sudah cukup.
    }

    private static Image BuatImage(string name, Transform parent, Color color)
    {
        var go = new GameObject(name, typeof(RectTransform));
        go.transform.SetParent(parent, false);
        var img = go.AddComponent<Image>();
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

    private GameObject BuatTombol(string name, Transform parent, string label, Color warna)
    {
        var btn = new GameObject(name, typeof(RectTransform));
        btn.transform.SetParent(parent, false);
        var img = btn.AddComponent<Image>();
        img.color = warna;

        var labelGo = new GameObject("Label", typeof(RectTransform));
        labelGo.transform.SetParent(btn.transform, false);
        var rectLabel = labelGo.GetComponent<RectTransform>();
        FillParent(rectLabel);
        var tmp = labelGo.AddComponent<TextMeshProUGUI>();
        tmp.text = label;
        tmp.fontSize = 42;
        tmp.alignment = TextAlignmentOptions.Center;
        tmp.color = Color.white;
        tmp.fontStyle = FontStyles.Bold;
        tmp.enableWordWrapping = true;

        return btn;
    }

    private static void FillParent(RectTransform rt)
    {
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.offsetMin = Vector2.zero;
        rt.offsetMax = Vector2.zero;
    }
}
