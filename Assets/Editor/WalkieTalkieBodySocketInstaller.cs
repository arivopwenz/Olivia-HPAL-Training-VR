#if UNITY_EDITOR
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.XR.Interaction.Toolkit.Interactors;

[InitializeOnLoad]
public static class WalkieTalkieBodySocketInstaller
{
    private const string ScenePath = "Assets/Scenes/Level1.unity";
    private const string ChestSocketName = "Socket_WalkieTalkie";
    private const string MouthSocketName = "Socket_WalkieTalkie_Mouth";
    private const string WalkieName = "Walkie Talkie";
    private const string AutoSessionKey = "OLIVIA_WalkieTalkie_BodySocket_v1";

    static WalkieTalkieBodySocketInstaller()
    {
        EditorApplication.delayCall += AutoInstallOnce;
    }

    [MenuItem("OLIVIA/1 - Install Walkie Talkie Body Socket")]
    public static void InstallFromMenu()
    {
        Install(openSceneIfNeeded: true);
    }

    private static void AutoInstallOnce()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode)
            return;

        if (EditorApplication.isCompiling || EditorApplication.isUpdating)
        {
            EditorApplication.delayCall += AutoInstallOnce;
            return;
        }

        if (SessionState.GetBool(AutoSessionKey, false))
            return;

        SessionState.SetBool(AutoSessionKey, true);
        Install(openSceneIfNeeded: false);
    }

    private static void Install(bool openSceneIfNeeded)
    {
        Scene scene = SceneManager.GetSceneByPath(ScenePath);
        if (!scene.IsValid() || !scene.isLoaded)
        {
            if (!openSceneIfNeeded)
                return;
            scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
        }

        Camera mainCamera = Object.FindAnyObjectByType<Camera>();
        if (mainCamera == null)
        {
            Debug.LogWarning("[OLIVIA] Main Camera tidak ditemukan. Walkie body socket dilewati.");
            return;
        }

        Transform cameraTransform = mainCamera.transform;
        Transform torsoAnchor = EnsureTorsoAnchor(mainCamera);

        DestroyDuplicateChildren(ChestSocketName, torsoAnchor);
        GameObject chestSocket = EnsureChild(torsoAnchor, ChestSocketName);
        chestSocket.SetActive(true);
        chestSocket.transform.localPosition = new Vector3(-0.22f, -0.08f, 0.02f);
        chestSocket.transform.localRotation = Quaternion.Euler(8f, -16f, -8f);
        chestSocket.transform.localScale = Vector3.one;
        ConfigureBoxTrigger(chestSocket, new Vector3(0.22f, 0.26f, 0.12f));
        ConfigureSocketInteractor(chestSocket, false);
        EnsureComponent<WalkieTalkieWearableSocket>(chestSocket);
        RebuildChestDockVisual(chestSocket.transform);

        DestroyDuplicateChildren(MouthSocketName, cameraTransform);
        GameObject mouthSocket = EnsureChild(cameraTransform, MouthSocketName);
        mouthSocket.SetActive(true);
        mouthSocket.transform.localPosition = new Vector3(0.04f, -0.08f, 0.24f);
        mouthSocket.transform.localRotation = Quaternion.Euler(0f, 0f, 0f);
        mouthSocket.transform.localScale = Vector3.one;
        ConfigureBoxTrigger(mouthSocket, new Vector3(0.28f, 0.22f, 0.22f));
        ConfigureSocketInteractor(mouthSocket, false);
        EnsureComponent<WalkieTalkieMouthPttTrigger>(mouthSocket);
        HideRendererChildren(mouthSocket.transform);

        GameObject walkie = FindSceneObjectByName(WalkieName);
        if (walkie != null)
        {
            Rigidbody rb = walkie.GetComponent<Rigidbody>();
            if (rb != null)
            {
                rb.isKinematic = true;
                rb.useGravity = false;
                EditorUtility.SetDirty(rb);
            }
        }

        ConfigureWalkieManager(walkie);

        EditorUtility.SetDirty(chestSocket);
        EditorUtility.SetDirty(mouthSocket);
        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
        Selection.activeGameObject = chestSocket;

        Debug.Log("[OLIVIA] Walkie Talkie body socket installed: left chest dock + mouth PTT trigger.");
    }

    private static void DestroyDuplicateChildren(string name, Transform keepParent)
    {
        foreach (GameObject go in Resources.FindObjectsOfTypeAll<GameObject>())
        {
            if (go == null || go.name != name)
                continue;
            if (!go.scene.IsValid() || EditorUtility.IsPersistent(go))
                continue;
            if (go.transform.parent == keepParent)
                continue;

            Object.DestroyImmediate(go);
        }
    }

    private static Transform EnsureTorsoAnchor(Camera mainCamera)
    {
        GameObject existing = GameObject.Find("TorsoAnchor");
        Transform parent = mainCamera.transform.parent != null && mainCamera.transform.parent.parent != null
            ? mainCamera.transform.parent.parent
            : mainCamera.transform.root;

        GameObject anchor = existing != null ? existing : EnsureChild(parent, "TorsoAnchor");
        anchor.SetActive(true);

        TorsoChestAnchor torso = EnsureComponent<TorsoChestAnchor>(anchor);
        SerializedObject serialized = new SerializedObject(torso);
        SerializedProperty cameraProp = serialized.FindProperty("_camera");
        if (cameraProp != null)
            cameraProp.objectReferenceValue = mainCamera.transform;
        SetFloat(serialized, "_offsetY", -0.42f);
        SetFloat(serialized, "_offsetDepan", 0.17f);
        SetFloat(serialized, "_offsetSamping", 0f);
        SetBool(serialized, "_ikutYawKamera", true);
        SetFloat(serialized, "_smoothPos", 0.05f);
        SetFloat(serialized, "_smoothRot", 0.08f);
        serialized.ApplyModifiedPropertiesWithoutUndo();
        EditorUtility.SetDirty(torso);
        EditorUtility.SetDirty(anchor);

        return anchor.transform;
    }

    private static void SetFloat(SerializedObject serialized, string propertyName, float value)
    {
        SerializedProperty property = serialized.FindProperty(propertyName);
        if (property != null)
            property.floatValue = value;
    }

    private static void SetBool(SerializedObject serialized, string propertyName, bool value)
    {
        SerializedProperty property = serialized.FindProperty(propertyName);
        if (property != null)
            property.boolValue = value;
    }

    private static GameObject EnsureChild(Transform parent, string name)
    {
        Transform existing = parent.Find(name);
        if (existing != null)
            return existing.gameObject;

        GameObject go = new GameObject(name);
        go.transform.SetParent(parent, false);
        return go;
    }

    private static T EnsureComponent<T>(GameObject go) where T : Component
    {
        T component = go.GetComponent<T>();
        if (component == null)
            component = go.AddComponent<T>();
        EditorUtility.SetDirty(component);
        return component;
    }

    private static void ConfigureBoxTrigger(GameObject go, Vector3 size)
    {
        BoxCollider collider = go.GetComponent<BoxCollider>();
        if (collider == null)
            collider = go.AddComponent<BoxCollider>();
        collider.center = Vector3.zero;
        collider.size = size;
        collider.isTrigger = true;
        EditorUtility.SetDirty(collider);
    }

    private static void ConfigureSocketInteractor(GameObject go, bool active)
    {
        XRSocketInteractor socket = go.GetComponent<XRSocketInteractor>();
        if (socket == null)
            socket = go.AddComponent<XRSocketInteractor>();
        socket.socketActive = active;
        socket.enabled = active;
        socket.showInteractableHoverMeshes = false;
        socket.interactableHoverScale = 1f;
        EditorUtility.SetDirty(socket);
    }

    private static void RebuildChestDockVisual(Transform chestSocket)
    {
        Transform old = chestSocket.Find("WT_ChestDock_Visual");
        if (old != null)
            Object.DestroyImmediate(old.gameObject);

        GameObject root = new GameObject("WT_ChestDock_Visual");
        root.transform.SetParent(chestSocket, false);
        root.transform.localPosition = Vector3.zero;
        root.transform.localRotation = Quaternion.identity;
        root.transform.localScale = Vector3.one;

        Material dark = EnsureFlatMaterial("M_WalkieDock_DarkRubber", new Color(0.015f, 0.018f, 0.018f), 0.05f, 0.30f);
        Material yellow = EnsureFlatMaterial("M_WalkieDock_SafetyYellow", new Color(1.0f, 0.73f, 0.04f), 0.02f, 0.34f);
        Material cyan = EnsureFlatMaterial("M_WalkieDock_CyanLED", new Color(0.08f, 0.86f, 1.0f), 0.0f, 0.45f);

        Cube(root.transform, "Back_Plate", new Vector3(0f, 0f, 0.018f), new Vector3(0.115f, 0.18f, 0.018f), dark);
        Cube(root.transform, "Left_Clamp", new Vector3(-0.072f, 0f, -0.006f), new Vector3(0.018f, 0.18f, 0.035f), yellow);
        Cube(root.transform, "Right_Clamp", new Vector3(0.072f, 0f, -0.006f), new Vector3(0.018f, 0.18f, 0.035f), yellow);
        Cube(root.transform, "Bottom_Stop", new Vector3(0f, -0.102f, -0.006f), new Vector3(0.145f, 0.018f, 0.035f), yellow);
        Cube(root.transform, "Top_Strap", new Vector3(0f, 0.094f, -0.006f), new Vector3(0.12f, 0.014f, 0.028f), dark);
        Cube(root.transform, "Radio_LED", new Vector3(0.0f, 0.0f, -0.028f), new Vector3(0.026f, 0.026f, 0.008f), cyan);
    }

    private static void Cube(Transform parent, string name, Vector3 localPosition, Vector3 localScale, Material material)
    {
        GameObject go = GameObject.CreatePrimitive(PrimitiveType.Cube);
        go.name = name;
        go.transform.SetParent(parent, false);
        go.transform.localPosition = localPosition;
        go.transform.localScale = localScale;
        Renderer renderer = go.GetComponent<Renderer>();
        if (renderer != null)
            renderer.sharedMaterial = material;
        Collider collider = go.GetComponent<Collider>();
        if (collider != null)
            Object.DestroyImmediate(collider);
        EditorUtility.SetDirty(go);
    }

    private static void HideRendererChildren(Transform root)
    {
        foreach (Renderer renderer in root.GetComponentsInChildren<Renderer>(true))
        {
            renderer.enabled = false;
            EditorUtility.SetDirty(renderer);
        }
    }

    private static void ConfigureWalkieManager(GameObject walkie)
    {
        WalkieTalkieManager manager = Object.FindAnyObjectByType<WalkieTalkieManager>(FindObjectsInactive.Include);
        if (manager == null)
            return;

        SerializedObject serialized = new SerializedObject(manager);
        SerializedProperty walkieProp = serialized.FindProperty("_walkieTalkieInHand");
        if (walkieProp != null && walkie != null)
            walkieProp.objectReferenceValue = walkie;

        SerializedProperty autoShowProp = serialized.FindProperty("_autoShowOnPTT");
        if (autoShowProp != null)
            autoShowProp.boolValue = false;

        SerializedProperty hideProp = serialized.FindProperty("_hidePhysicalWalkieSaatPTTSelesai");
        if (hideProp != null)
            hideProp.boolValue = false;

        serialized.ApplyModifiedPropertiesWithoutUndo();
        EditorUtility.SetDirty(manager);
    }

    private static Material EnsureFlatMaterial(string name, Color color, float metallic, float smoothness)
    {
        const string folder = "Assets/Materials/Generated";
        if (!Directory.Exists(folder))
            Directory.CreateDirectory(folder);

        string path = folder + "/" + name + ".mat";
        Material mat = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (mat == null)
        {
            Shader shader = Shader.Find("Universal Render Pipeline/Lit");
            if (shader == null)
                shader = Shader.Find("Standard");
            mat = new Material(shader);
            mat.name = name;
            AssetDatabase.CreateAsset(mat, path);
        }

        if (mat.HasProperty("_BaseColor")) mat.SetColor("_BaseColor", color);
        if (mat.HasProperty("_Color")) mat.SetColor("_Color", color);
        if (mat.HasProperty("_Metallic")) mat.SetFloat("_Metallic", metallic);
        if (mat.HasProperty("_Smoothness")) mat.SetFloat("_Smoothness", smoothness);
        EditorUtility.SetDirty(mat);
        AssetDatabase.SaveAssets();
        return mat;
    }

    private static GameObject FindSceneObjectByName(string name)
    {
        foreach (GameObject go in Resources.FindObjectsOfTypeAll<GameObject>())
        {
            if (go.name != name)
                continue;
            if (!go.scene.IsValid())
                continue;
            if (EditorUtility.IsPersistent(go))
                continue;
            return go;
        }

        return null;
    }
}
#endif
