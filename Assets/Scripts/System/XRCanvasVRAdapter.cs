using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using UnityEngine.XR.Interaction.Toolkit.Interactables;


/// <summary>
/// Converts desktop overlay HUD canvases into head-locked world-space canvases
/// so they are rendered by both eyes in a VR headset.
/// </summary>
public static class XRCanvasVRAdapter
{
    private static readonly Vector3 CanvasLocalPosition = new Vector3(0f, -0.112f, 0.615f);
    private static readonly Vector2 CanvasSize = new Vector2(1671.062f, 1240.888f);
    private const float CanvasScale = 0.00075f;

    private static readonly string[] CanvasNames =
    {
        "Player_HUD_Canvas",
        "Pause_Menu_Canvas"
    };

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void Initialize()
    {
        SceneManager.sceneLoaded -= OnSceneLoaded;
        SceneManager.sceneLoaded += OnSceneLoaded;
        ConfigureSceneCanvases();
    }

    private static void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        ConfigureSceneCanvases();
    }

    private static void ConfigureSceneCanvases()
    {
        Camera xrCamera = Camera.main;
        if (xrCamera == null)
            xrCamera = Object.FindFirstObjectByType<Camera>();

        if (xrCamera == null)
        {
            Debug.LogWarning("[XRCanvasVRAdapter] Kamera pemain tidak ditemukan.");
            return;
        }

        for (int i = 0; i < CanvasNames.Length; i++)
        {
            GameObject canvasObject = GameObject.Find(CanvasNames[i]);
            if (canvasObject == null)
                continue;

            Canvas canvas = canvasObject.GetComponent<Canvas>();
            RectTransform rect = canvasObject.GetComponent<RectTransform>();
            if (canvas == null || rect == null)
                continue;

            canvas.renderMode = RenderMode.WorldSpace;
            canvas.worldCamera = xrCamera;
            canvas.sortingOrder = CanvasNames[i] == "Pause_Menu_Canvas" ? 200 : 100;

            rect.SetParent(xrCamera.transform, false);
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.zero;
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.sizeDelta = CanvasSize;
            rect.localPosition = CanvasLocalPosition;
            rect.localRotation = Quaternion.identity;
            rect.localScale = Vector3.one * CanvasScale;

            if (CanvasNames[i] == "Pause_Menu_Canvas")
                SetupXrButtons(canvasObject);

        }
    }


    private static void SetupXrButtons(GameObject canvasObject)
    {
        Button[] buttons = canvasObject.GetComponentsInChildren<Button>(true);
        if (buttons.Length == 0) return;

        Canvas.ForceUpdateCanvases();

        for (int b = 0; b < buttons.Length; b++)
        {
            Button btn = buttons[b];
            RectTransform btnRect = btn.GetComponent<RectTransform>();
            if (btnRect == null) continue;

            float w = btnRect.rect.width;
            float h = btnRect.rect.height;
            if (w < 1f || h < 1f)
            {
                RectTransform canvasRect = canvasObject.GetComponent<RectTransform>();
                float pw = canvasRect != null ? canvasRect.rect.width : CanvasSize.x;
                float ph = canvasRect != null ? canvasRect.rect.height : CanvasSize.y;
                w = Mathf.Max(w, (btnRect.anchorMax.x - btnRect.anchorMin.x) * pw);
                h = Mathf.Max(h, (btnRect.anchorMax.y - btnRect.anchorMin.y) * ph);
            }

            BoxCollider bc = btn.GetComponent<BoxCollider>();
            if (bc == null) bc = btn.gameObject.AddComponent<BoxCollider>();
            bc.isTrigger = true;
            bc.size = new Vector3(Mathf.Max(w, 10f), Mathf.Max(h, 10f), 20f);
            bc.center = Vector3.zero;

            XRSimpleInteractable simple = btn.GetComponent<XRSimpleInteractable>();
            if (simple == null) simple = btn.gameObject.AddComponent<XRSimpleInteractable>();
            simple.colliders.Clear();
            simple.colliders.Add(bc);

            Button capturedBtn = btn;
            simple.selectEntered.AddListener(_ => capturedBtn.onClick.Invoke());
        }
    }
}
