using UnityEngine;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
using UnityEngine.XR.Interaction.Toolkit.Inputs;
#endif
using UnityEngine.XR.Interaction.Toolkit.Interactors;

/// <summary>
/// OLIVIA VR — XRInteractorRecovery.cs
///
/// Re-enable XR interactor (NearFar + Ray) di kedua controller setelah teleport manual
/// (XROrigin.MoveCameraToWorldLocation) yang tidak melalui TeleportationProvider.
///
/// Bug yang di-fix:
///   ControllerInputActionManager.OnStartTeleport() panggil
///   m_NearFarInteractor.gameObject.SetActive(false). Kalau teleport diselesaikan
///   manual (bukan via TeleportationProvider), OnCancelTeleport tidak fire,
///   sehingga NearFar Interactor stuck disabled. Akibat: ray klik UI hilang
///   di Level 2-14.
///
/// Pemakaian:
///   XRInteractorRecovery.PulihkanRayInteractor();
/// </summary>
public static class XRInteractorRecovery
{
    private static GameObject _rightController;
    private static GameObject _leftController;
    private static float _nextGlobalScan;

    /// <summary>
    /// Aktifkan kembali NearFar Interactor + Ray Interactor + Poke Interactor di
    /// kedua controller. Dipanggil setelah teleport manual yang tidak via TeleportationProvider.
    /// </summary>
    public static void PulihkanRayInteractor()
    {
        PulihkanGlobalXRInput();

        if (_rightController == null)
            _rightController = CariControllerTermasukInactive("Right Controller");
        if (_leftController == null)
            _leftController = CariControllerTermasukInactive("Left Controller");

        if (_rightController != null && !_rightController.scene.IsValid()) _rightController = null;
        if (_leftController != null && !_leftController.scene.IsValid()) _leftController = null;

        PulihkanController(_rightController);
        PulihkanController(_leftController);
        PulihkanTransparentHands();
    }

    public static void PulihkanTransparentHands()
    {
        PulihkanTransparentHand("OLIVIA_Left_TransparentHand", "Left Controller");
        PulihkanTransparentHand("OLIVIA_Right_TransparentHand", "Right Controller");
    }

    private static GameObject CariControllerTermasukInactive(string namaController)
    {
        GameObject aktif = GameObject.Find("XR Origin (XR Rig)/Camera Offset/" + namaController);
        if (aktif != null)
            return aktif;

        Transform[] all = Object.FindObjectsByType<Transform>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        for (int i = 0; i < all.Length; i++)
        {
            Transform t = all[i];
            if (t == null || t.name != namaController)
                continue;

            Transform parent = t.parent;
            Transform root = t.root;
            bool benarDiRig = parent != null && parent.name == "Camera Offset" &&
                              root != null && root.name.IndexOf("XR Origin", System.StringComparison.OrdinalIgnoreCase) >= 0;
            if (benarDiRig)
                return t.gameObject;
        }

        return null;
    }

    private static void PulihkanController(GameObject controller)
    {
        if (controller == null) return;

        if (!controller.activeSelf)
            controller.SetActive(true);

        MatikanAffordanceVisualRusak(controller);
        PulihkanControllerBehaviours(controller);

        var nearFar = controller.transform.Find("Near-Far Interactor");
        if (nearFar != null && !nearFar.gameObject.activeSelf)
            nearFar.gameObject.SetActive(true);

        var poke = controller.transform.Find("Poke Interactor");
        if (poke != null && !poke.gameObject.activeSelf)
            poke.gameObject.SetActive(true);

        var teleport = controller.transform.Find("Teleport Interactor");
        if (teleport != null)
        {
            // Jangan paksa visual teleport hidup. Yang dipulihkan di sini adalah ray default UI/select,
            // bukan garis teleport merah yang bikin rancu saat level pindah.
            HealRayVisuals(teleport.gameObject, false);
        }

        foreach (var interactor in controller.GetComponentsInChildren<NearFarInteractor>(true))
        {
            if (interactor == null) continue;
            if (!interactor.gameObject.activeSelf) interactor.gameObject.SetActive(true);
            if (!interactor.enabled) interactor.enabled = true;
            HealRayVisuals(interactor.gameObject, true);
        }

        foreach (var ray in controller.GetComponentsInChildren<XRRayInteractor>(true))
        {
            if (ray == null) continue;
            if (AdaAncestorBernama(ray.transform, "Teleport Interactor")) continue;
            if (!ray.gameObject.activeSelf) ray.gameObject.SetActive(true);
            if (!ray.enabled) ray.enabled = true;
            HealRayVisuals(ray.gameObject, true);
        }
    }

    private static void PulihkanGlobalXRInput()
    {
        if (Application.isPlaying && Time.unscaledTime < _nextGlobalScan)
            return;

        if (Application.isPlaying)
            _nextGlobalScan = Time.unscaledTime + 0.5f;

        foreach (var mb in Object.FindObjectsByType<MonoBehaviour>(FindObjectsInactive.Include, FindObjectsSortMode.None))
        {
            if (mb == null) continue;
            string tn = mb.GetType().Name;
            if (tn == "InputActionManager" || tn == "XRInputModalityManager" || tn == "XRInteractionManager")
            {
                if (!mb.gameObject.activeSelf) mb.gameObject.SetActive(true);
                if (!mb.enabled) mb.enabled = true;
            }
        }

#if ENABLE_INPUT_SYSTEM
        foreach (var manager in Object.FindObjectsByType<InputActionManager>(FindObjectsInactive.Include, FindObjectsSortMode.None))
        {
            if (manager == null) continue;
            if (!manager.gameObject.activeSelf) manager.gameObject.SetActive(true);
            if (!manager.enabled) manager.enabled = true;

            var assets = manager.actionAssets;
            if (assets == null) continue;
            for (int i = 0; i < assets.Count; i++)
                assets[i]?.Enable();
        }
#endif
    }

    private static void PulihkanControllerBehaviours(GameObject controller)
    {
        foreach (var mb in controller.GetComponentsInChildren<MonoBehaviour>(true))
        {
            if (mb == null) continue;
            string tn = mb.GetType().Name;
            if (!HarusHidupUntukRayXR(tn)) continue;
            if (!mb.gameObject.activeSelf) mb.gameObject.SetActive(true);
            if (!mb.enabled) mb.enabled = true;
        }
    }

    private static bool HarusHidupUntukRayXR(string typeName)
    {
        return typeName == "ControllerInputActionManager" ||
               typeName == "ActionBasedController" ||
               typeName == "XRController" ||
               typeName == "TrackedPoseDriver" ||
               typeName == "XRInteractionGroup" ||
               typeName == "NearFarInteractor" ||
               typeName == "XRRayInteractor" ||
               typeName == "XRDirectInteractor" ||
               typeName == "XRPokeInteractor" ||
               typeName == "CurveVisualController";
    }

    private static void PulihkanTransparentHand(string handName, string controllerName)
    {
        Transform hand = CariTransformTermasukInactive(handName);
        Transform controller = CariControllerTermasukInactive(controllerName)?.transform;
        if (hand == null || controller == null) return;

        if (hand.parent != controller)
            hand.SetParent(controller, false);

        hand.localPosition = new Vector3(0f, -0.015f, 0.055f);
        hand.localRotation = Quaternion.identity;
        if (!hand.gameObject.activeSelf) hand.gameObject.SetActive(true);

        foreach (Renderer r in hand.GetComponentsInChildren<Renderer>(true))
            if (r != null && !r.enabled) r.enabled = true;
    }

    private static Transform CariTransformTermasukInactive(string nama)
    {
        GameObject aktif = GameObject.Find(nama);
        if (aktif != null) return aktif.transform;

        Transform[] all = Object.FindObjectsByType<Transform>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        for (int i = 0; i < all.Length; i++)
            if (all[i] != null && all[i].name == nama)
                return all[i];

        return null;
    }

    private static bool AdaAncestorBernama(Transform t, string nama)
    {
        while (t != null)
        {
            if (t.name == nama)
                return true;
            t = t.parent;
        }
        return false;
    }


    private static void MatikanAffordanceVisualRusak(GameObject controller)
    {
        foreach (var mb in controller.GetComponentsInChildren<MonoBehaviour>(true))
        {
            if (mb == null) continue;
            string tn = mb.GetType().Name;
            if (tn.IndexOf("Affordance", System.StringComparison.OrdinalIgnoreCase) < 0)
                continue;

            if (mb.enabled)
                mb.enabled = false;
        }
    }

    private static void HealRayVisuals(GameObject root, bool activateGameObjects)
    {
        if (root == null) return;

        if (!activateGameObjects)
        {
            foreach (var ray in root.GetComponentsInChildren<XRRayInteractor>(true))
                if (ray != null) ray.enabled = false;

            foreach (var line in root.GetComponentsInChildren<LineRenderer>(true))
                if (line != null) line.enabled = false;

            foreach (var mb in root.GetComponentsInChildren<MonoBehaviour>(true))
                if (IsRayVisualController(mb)) mb.enabled = false;

            return;
        }

        foreach (var ray in root.GetComponentsInChildren<XRRayInteractor>(true))
        {
            if (ray == null) continue;
            if (activateGameObjects && !ray.gameObject.activeSelf)
                ray.gameObject.SetActive(true);
            if (!ray.enabled)
                ray.enabled = true;
        }

        foreach (var line in root.GetComponentsInChildren<LineRenderer>(true))
        {
            if (line == null) continue;
            if (activateGameObjects && !line.gameObject.activeSelf)
                line.gameObject.SetActive(true);
            if (!line.enabled)
                line.enabled = true;
        }

        foreach (var mb in root.GetComponentsInChildren<MonoBehaviour>(true))
        {
            if (!IsRayVisualController(mb)) continue;
            if (!HasLineRenderableInHierarchy(mb.gameObject))
                continue;
            if (activateGameObjects && !mb.gameObject.activeSelf)
                mb.gameObject.SetActive(true);
            if (!mb.enabled)
                mb.enabled = true;
        }
    }

    private static bool IsRayVisualController(MonoBehaviour mb)
    {
        if (mb == null) return false;

        string tn = mb.GetType().Name;
        return tn == "XRInteractorLineVisual" || tn == "CurveVisualController";
    }

    private static bool HasLineRenderableInHierarchy(GameObject go)
    {
        if (go == null) return false;
        return go.GetComponent<NearFarInteractor>() != null ||
               go.GetComponent<XRRayInteractor>() != null ||
               go.GetComponentInParent<NearFarInteractor>(true) != null ||
               go.GetComponentInParent<XRRayInteractor>(true) != null;
    }
}
