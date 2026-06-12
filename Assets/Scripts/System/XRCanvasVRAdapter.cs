using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// Converts desktop overlay HUD canvases into head-locked world-space canvases
/// so they are rendered by both eyes in a VR headset.
/// </summary>
public static class XRCanvasVRAdapter
{
    private static readonly Vector3 CanvasLocalPosition = new Vector3(0f, -0.103f, 0.62f);
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
        }
    }
}
