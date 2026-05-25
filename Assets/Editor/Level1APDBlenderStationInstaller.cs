#if UNITY_EDITOR
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using TMPro;
using UnityEngine.XR.Interaction.Toolkit.Interactables;
using UnityEngine.XR.Interaction.Toolkit.Interactors;

[InitializeOnLoad]
public static class Level1APDBlenderStationInstaller
{
    private const string ScenePath = "Assets/Scenes/Level1.unity";
    private const string FolderPath = "Assets/Art/Level1APDStationBlender";
    private const string FbxPath = FolderPath + "/level1_apd_industrial_workbench_locker_uv.fbx";
    private const string AtlasPath = FolderPath + "/level1_apd_industrial_workbench_locker_atlas.png";
    private const string MaterialPath = FolderPath + "/M_Level1_APDStation_UVAtlas.mat";
    private const string ArrowFolderPath = "Assets/Art/TaskHintArrowBlender";
    private const string ArrowFbxPath = ArrowFolderPath + "/task_hint_arrow_uv.fbx";
    private const string ArrowAtlasPath = ArrowFolderPath + "/task_hint_arrow_atlas.png";
    private const string ArrowMaterialPath = ArrowFolderPath + "/M_TaskHint_Arrow_UVAtlas.mat";
    private const string InstanceName = "Level1_APD_Blender_Workbench_Locker_Auto";
    private const string PrimitiveRootName = "Level1_APD_Industrial_Workbench_Locker";
    private const string ApdRootName = "APD Level 2";
    private const string HintRootName = "Level1_APD_TaskHints";
    private const string AutoSessionKey = "OLIVIA_Level1_APD_Blender_Station_v2";

    static Level1APDBlenderStationInstaller()
    {
        EditorApplication.delayCall += AutoInstallOnce;
    }

    [MenuItem("OLIVIA/1 - Install Level 1 APD Blender Station")]
    public static void InstallFromMenu()
    {
        Install(openSceneIfNeeded: true, forceReplace: true);
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
        Install(openSceneIfNeeded: false, forceReplace: true);
    }

    private static void Install(bool openSceneIfNeeded, bool forceReplace)
    {
        if (!File.Exists(FbxPath) || !File.Exists(AtlasPath))
        {
            Debug.LogWarning("[OLIVIA] APD Blender FBX/atlas belum ada. Installer dilewati.");
            return;
        }

        AssetDatabase.ImportAsset(FolderPath, ImportAssetOptions.ImportRecursive | ImportAssetOptions.ForceUpdate);
        if (Directory.Exists(ArrowFolderPath))
            AssetDatabase.ImportAsset(ArrowFolderPath, ImportAssetOptions.ImportRecursive | ImportAssetOptions.ForceUpdate);

        Scene scene = SceneManager.GetSceneByPath(ScenePath);
        if (!scene.IsValid() || !scene.isLoaded)
        {
            if (!openSceneIfNeeded)
                return;
            scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
        }

        GameObject building = GameObject.Find("Building");
        if (building == null)
        {
            Debug.LogError("[OLIVIA] Building root tidak ditemukan untuk APD Blender station.");
            return;
        }

        GameObject existing = FindChildByName(building.transform, InstanceName);
        if (existing != null)
        {
            if (!forceReplace)
                return;
            Object.DestroyImmediate(existing);
        }

        GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(FbxPath);
        if (prefab == null)
        {
            Debug.LogError("[OLIVIA] Gagal load APD Blender FBX: " + FbxPath);
            return;
        }

        Material mat = EnsureAtlasMaterial();
        GameObject instance = PrefabUtility.InstantiatePrefab(prefab, scene) as GameObject;
        if (instance == null)
        {
            Debug.LogError("[OLIVIA] Gagal instantiate APD Blender FBX.");
            return;
        }

        instance.name = InstanceName;
        instance.transform.SetParent(building.transform, worldPositionStays: false);
        instance.transform.localPosition = new Vector3(-6.75f, -3.85f, 0.29f);
        instance.transform.localRotation = Quaternion.Euler(270f, 180f, 0f);
        instance.transform.localScale = Vector3.one * 100f;

        AssignMaterial(instance, mat);
        HideOldVisualRoots();
        AddSimpleCollider(instance);
        LayoutApdLevel2();
        InstallLabelsAndTaskHints(scene);

        EditorUtility.SetDirty(instance);
        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
        Selection.activeGameObject = instance;

        Debug.Log("[OLIVIA] Level 1 APD Blender workbench + locker installed, UV atlas applied, scene saved.");
    }

    private static Material EnsureAtlasMaterial()
    {
        TextureImporter texImporter = AssetImporter.GetAtPath(AtlasPath) as TextureImporter;
        if (texImporter != null)
        {
            texImporter.wrapMode = TextureWrapMode.Clamp;
            texImporter.mipmapEnabled = true;
            texImporter.textureCompression = TextureImporterCompression.Compressed;
            texImporter.SaveAndReimport();
        }

        Texture2D atlas = AssetDatabase.LoadAssetAtPath<Texture2D>(AtlasPath);
        Material mat = AssetDatabase.LoadAssetAtPath<Material>(MaterialPath);
        if (mat == null)
        {
            Shader shader = Shader.Find("Universal Render Pipeline/Lit");
            if (shader == null)
                shader = Shader.Find("Standard");
            mat = new Material(shader);
            mat.name = "M_Level1_APDStation_UVAtlas";
            AssetDatabase.CreateAsset(mat, MaterialPath);
        }

        if (atlas != null)
        {
            if (mat.HasProperty("_BaseMap")) mat.SetTexture("_BaseMap", atlas);
            if (mat.HasProperty("_MainTex")) mat.SetTexture("_MainTex", atlas);
        }

        if (mat.HasProperty("_BaseColor")) mat.SetColor("_BaseColor", Color.white);
        if (mat.HasProperty("_Color")) mat.SetColor("_Color", Color.white);
        if (mat.HasProperty("_Metallic")) mat.SetFloat("_Metallic", 0.18f);
        if (mat.HasProperty("_Smoothness")) mat.SetFloat("_Smoothness", 0.36f);
        EditorUtility.SetDirty(mat);
        AssetDatabase.SaveAssets();
        return mat;
    }

    private static void AssignMaterial(GameObject root, Material mat)
    {
        if (mat == null)
            return;

        foreach (Renderer renderer in root.GetComponentsInChildren<Renderer>(true))
        {
            Material[] materials = renderer.sharedMaterials;
            if (materials == null || materials.Length == 0)
                materials = new Material[1];
            for (int i = 0; i < materials.Length; i++)
                materials[i] = mat;
            renderer.sharedMaterials = materials;
            renderer.enabled = true;
            EditorUtility.SetDirty(renderer);
        }
    }

    private static void HideOldVisualRoots()
    {
        string[] names =
        {
            "meja",
            "Loker",
            PrimitiveRootName
        };

        foreach (string name in names)
        {
            GameObject go = GameObject.Find("Building/" + name);
            if (go == null)
                go = GameObject.Find(name);
            if (go == null)
                continue;

            foreach (Renderer renderer in go.GetComponentsInChildren<Renderer>(true))
            {
                renderer.enabled = false;
                EditorUtility.SetDirty(renderer);
            }
        }
    }

    private static void AddSimpleCollider(GameObject root)
    {
        if (root.GetComponent<BoxCollider>() != null)
            return;

        Renderer[] renderers = root.GetComponentsInChildren<Renderer>(true);
        if (renderers.Length == 0)
            return;

        Bounds bounds = renderers[0].bounds;
        for (int i = 1; i < renderers.Length; i++)
            bounds.Encapsulate(renderers[i].bounds);

        BoxCollider collider = root.AddComponent<BoxCollider>();
        collider.center = root.transform.InverseTransformPoint(bounds.center);
        Vector3 localSize = root.transform.InverseTransformVector(bounds.size);
        collider.size = new Vector3(Mathf.Abs(localSize.x), Mathf.Abs(localSize.y), Mathf.Abs(localSize.z));
        collider.isTrigger = true;
        EditorUtility.SetDirty(collider);
    }

    private static void LayoutApdLevel2()
    {
        GameObject apdRoot = FindSceneObjectByName(ApdRootName);
        if (apdRoot == null)
        {
            Debug.LogWarning("[OLIVIA] APD Level 2 tidak ditemukan. Layout item APD dilewati.");
            return;
        }

        apdRoot.SetActive(true);
        apdRoot.transform.SetPositionAndRotation(Vector3.zero, Quaternion.identity);
        apdRoot.transform.localScale = Vector3.one;

        LayoutSocket(apdRoot, "Socket_Scanner_Glassess", new Vector3(-5.96f, 1.16f, -7.04f), new Vector3(0f, 18f, 0f), Vector3.one * 0.34f, Vector3.zero, Vector3.zero);
        LayoutSocket(apdRoot, "Socket_Scanner_Hat", new Vector3(-5.22f, 1.24f, -7.05f), new Vector3(0f, -8f, 0f), Vector3.one * 0.34f, Vector3.zero, Vector3.zero);
        LayoutSocket(apdRoot, "Socket_Scanner_Gloves", new Vector3(-4.46f, 1.15f, -7.04f), new Vector3(0f, 10f, 0f), Vector3.one * 0.32f, Vector3.zero, Vector3.zero);
        LayoutSocket(apdRoot, "Socket_Scanner_EarPlug", new Vector3(-3.90f, 1.13f, -7.05f), new Vector3(0f, 0f, 0f), Vector3.one * 0.22f, Vector3.zero, Vector3.zero);
        LayoutSocket(apdRoot, "Socket_Scanner_WalkieTalkie", new Vector3(-3.18f, 1.18f, -7.05f), new Vector3(0f, -12f, 0f), Vector3.one * 0.38f, Vector3.zero, Vector3.zero);
        LayoutSocket(apdRoot, "Socket_Scanner_RespiratorMask", new Vector3(-3.52f, 1.55f, -7.94f), new Vector3(0f, 180f, 0f), Vector3.one * 0.34f, Vector3.zero, Vector3.zero);
        LayoutSocket(apdRoot, "Socket_Scanner_Boots", new Vector3(-7.56f, 0.38f, -6.83f), new Vector3(0f, 180f, 0f), Vector3.one * 0.44f, Vector3.zero, Vector3.zero);
        LayoutSocket(apdRoot, "Socket_Scanner_Rompi", new Vector3(-7.15f, 1.46f, -6.72f), new Vector3(0f, 180f, 0f), Vector3.one * 0.42f, Vector3.zero, Vector3.zero);
        RebuildVestVisual(apdRoot);

        EditorUtility.SetDirty(apdRoot);
    }

    private static void RebuildVestVisual(GameObject apdRoot)
    {
        GameObject socket = FindChildByName(apdRoot.transform, "Socket_Scanner_Rompi");
        if (socket == null || socket.transform.childCount == 0)
            return;

        Transform vest = socket.transform.GetChild(0);
        foreach (Transform child in vest.GetComponentsInChildren<Transform>(true).ToArray())
        {
            if (child != vest && child.name == "Vest_Industrial_Visual")
                Object.DestroyImmediate(child.gameObject);
        }

        Renderer rootRenderer = vest.GetComponent<Renderer>();
        if (rootRenderer != null)
        {
            rootRenderer.enabled = false;
            EditorUtility.SetDirty(rootRenderer);
        }

        Material yellow = EnsureFlatMaterial("M_APD_Vest_SafetyYellow", new Color(1.0f, 0.86f, 0.02f), 0.03f, 0.38f);
        Material dark = EnsureFlatMaterial("M_APD_Vest_DarkTrim", new Color(0.02f, 0.025f, 0.022f), 0.06f, 0.32f);
        Material reflective = EnsureFlatMaterial("M_APD_Vest_ReflectiveTape", new Color(0.86f, 0.94f, 0.82f), 0.0f, 0.65f);

        GameObject visual = new GameObject("Vest_Industrial_Visual");
        visual.transform.SetParent(vest, false);
        visual.transform.localPosition = Vector3.zero;
        visual.transform.localRotation = Quaternion.identity;
        visual.transform.localScale = Vector3.one;

        VestBox(visual.transform, "Left_Panel", new Vector3(-0.34f, -0.03f, 0f), new Vector3(0.48f, 1.42f, 0.10f), yellow);
        VestBox(visual.transform, "Right_Panel", new Vector3(0.34f, -0.03f, 0f), new Vector3(0.48f, 1.42f, 0.10f), yellow);
        VestBox(visual.transform, "Bottom_Belt", new Vector3(0f, -0.67f, 0f), new Vector3(1.10f, 0.24f, 0.11f), yellow);
        VestBox(visual.transform, "Left_Shoulder", new Vector3(-0.24f, 0.78f, 0f), new Vector3(0.32f, 0.38f, 0.10f), yellow);
        VestBox(visual.transform, "Right_Shoulder", new Vector3(0.24f, 0.78f, 0f), new Vector3(0.32f, 0.38f, 0.10f), yellow);
        VestBox(visual.transform, "Center_Zip_Dark", new Vector3(0f, -0.05f, -0.065f), new Vector3(0.045f, 1.34f, 0.025f), dark);
        VestBox(visual.transform, "Left_Reflective_V", new Vector3(-0.34f, 0.18f, -0.075f), new Vector3(0.08f, 1.10f, 0.026f), reflective);
        VestBox(visual.transform, "Right_Reflective_V", new Vector3(0.34f, 0.18f, -0.075f), new Vector3(0.08f, 1.10f, 0.026f), reflective);
        VestBox(visual.transform, "Waist_Reflective", new Vector3(0f, -0.46f, -0.08f), new Vector3(1.05f, 0.075f, 0.026f), reflective);
        VestBox(visual.transform, "Bottom_Dark_Trim", new Vector3(0f, -0.81f, -0.07f), new Vector3(1.12f, 0.06f, 0.026f), dark);

        EditorUtility.SetDirty(visual);
        EditorUtility.SetDirty(vest);
    }

    private static void VestBox(Transform parent, string name, Vector3 localPosition, Vector3 localScale, Material material)
    {
        GameObject go = GameObject.CreatePrimitive(PrimitiveType.Cube);
        go.name = name;
        go.transform.SetParent(parent, false);
        go.transform.localPosition = localPosition;
        go.transform.localRotation = Quaternion.identity;
        go.transform.localScale = localScale;
        Renderer renderer = go.GetComponent<Renderer>();
        if (renderer != null)
            renderer.sharedMaterial = material;
        Collider collider = go.GetComponent<Collider>();
        if (collider != null)
            Object.DestroyImmediate(collider);
        EditorUtility.SetDirty(go);
    }

    private static void LayoutSocket(GameObject apdRoot, string socketName, Vector3 worldPosition, Vector3 worldEuler, Vector3 localScale, Vector3 itemLocalPosition, Vector3 itemLocalEuler)
    {
        GameObject socket = FindChildByName(apdRoot.transform, socketName);
        if (socket == null)
            return;

        socket.SetActive(true);
        socket.transform.SetPositionAndRotation(worldPosition, Quaternion.Euler(worldEuler));
        socket.transform.localScale = localScale;

        foreach (Collider collider in socket.GetComponents<Collider>())
        {
            collider.isTrigger = true;
            EditorUtility.SetDirty(collider);
        }

        XRSocketInteractor socketInteractor = socket.GetComponent<XRSocketInteractor>();
        if (socketInteractor != null)
        {
            socketInteractor.showInteractableHoverMeshes = false;
            socketInteractor.interactableHoverScale = 1f;
            EditorUtility.SetDirty(socketInteractor);
        }

        if (socket.transform.childCount > 0)
        {
            Transform item = socket.transform.GetChild(0);
            item.gameObject.SetActive(true);
            item.localPosition = itemLocalPosition;
            item.localRotation = Quaternion.Euler(itemLocalEuler);

            foreach (Rigidbody rb in item.GetComponentsInChildren<Rigidbody>(true))
            {
                if (!rb.isKinematic)
                {
                    rb.linearVelocity = Vector3.zero;
                    rb.angularVelocity = Vector3.zero;
                }
                rb.isKinematic = true;
                rb.useGravity = false;
                EditorUtility.SetDirty(rb);
            }

            XRGrabInteractable grab = item.GetComponent<XRGrabInteractable>();
            if (grab != null)
            {
                grab.movementType = XRBaseInteractable.MovementType.Kinematic;
                grab.throwOnDetach = false;
                grab.forceGravityOnDetach = false;
                grab.retainTransformParent = true;
                EditorUtility.SetDirty(grab);
            }

            ApdDisplayItemStabilizer stabilizer = item.GetComponent<ApdDisplayItemStabilizer>();
            if (stabilizer == null)
                stabilizer = item.gameObject.AddComponent<ApdDisplayItemStabilizer>();
            stabilizer.SetHomeAnchor(socket.transform);
            EditorUtility.SetDirty(stabilizer);

            foreach (Renderer renderer in item.GetComponentsInChildren<Renderer>(true))
            {
                renderer.enabled = true;
                EditorUtility.SetDirty(renderer);
            }

            EditorUtility.SetDirty(item);
        }

        EditorUtility.SetDirty(socket);
    }

    private static void InstallLabelsAndTaskHints(Scene scene)
    {
        GameObject oldHintRoot = FindSceneObjectByName(HintRootName);
        if (oldHintRoot != null)
            Object.DestroyImmediate(oldHintRoot);

        GameObject hintRoot = new GameObject(HintRootName);
        hintRoot.transform.SetPositionAndRotation(Vector3.zero, Quaternion.identity);
        Material labelPlateMaterial = EnsureFlatMaterial("M_Level1_APD_LabelPlate", new Color(0.035f, 0.055f, 0.055f), 0.15f, 0.42f);
        Material arrowMaterial = EnsureArrowMaterial();

        CreateLabel(hintRoot.transform, "APD_Label_Helm", "HELM K3", new Vector3(-5.22f, 1.56f, -6.62f), labelPlateMaterial);
        CreateLabel(hintRoot.transform, "APD_Label_Rompi", "ROMPI", new Vector3(-7.15f, 1.86f, -6.48f), labelPlateMaterial);
        CreateLabel(hintRoot.transform, "APD_Label_Kacamata", "KACAMATA", new Vector3(-5.96f, 1.45f, -6.62f), labelPlateMaterial);
        CreateLabel(hintRoot.transform, "APD_Label_Sepatu", "SEPATU", new Vector3(-7.56f, 0.80f, -6.48f), labelPlateMaterial);
        CreateLabel(hintRoot.transform, "APD_Label_SarungTangan", "SARUNG TANGAN", new Vector3(-4.46f, 1.41f, -6.62f), labelPlateMaterial);
        CreateLabel(hintRoot.transform, "APD_Label_Respirator", "RESPIRATOR", new Vector3(-3.52f, 1.86f, -7.46f), labelPlateMaterial);
        CreateLabel(hintRoot.transform, "APD_Label_Earplug", "EARPLUG", new Vector3(-3.90f, 1.36f, -6.62f), labelPlateMaterial);
        CreateLabel(hintRoot.transform, "APD_Label_Walkie", "WALKIE TALKIE", new Vector3(-3.18f, 1.47f, -6.62f), labelPlateMaterial);

        GameObject arrowPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(ArrowFbxPath);
        if (arrowPrefab != null)
        {
            GameObject arrow = PrefabUtility.InstantiatePrefab(arrowPrefab, scene) as GameObject;
            if (arrow != null)
            {
                arrow.name = "TaskHint_Arrow3D";
                arrow.transform.SetParent(hintRoot.transform, worldPositionStays: true);
                arrow.transform.position = new Vector3(-5.22f, 2.14f, -7.02f);
                arrow.transform.rotation = Quaternion.Euler(-90f, 0f, 0f);
                arrow.transform.localScale = Vector3.one * 28f;
                AssignMaterial(arrow, arrowMaterial);
                EditorUtility.SetDirty(arrow);
            }
        }

        Level1ApdTaskHintDirector director = hintRoot.GetComponent<Level1ApdTaskHintDirector>();
        if (director == null)
            director = hintRoot.AddComponent<Level1ApdTaskHintDirector>();

        SerializedObject serialized = new SerializedObject(director);
        SetFloat(serialized, "baseArrowScale", 28f);
        SetFloat(serialized, "pulseSpeed", 2.4f);
        SetFloat(serialized, "outlinePadding", 0.045f);
        serialized.ApplyModifiedPropertiesWithoutUndo();

        EditorUtility.SetDirty(hintRoot);
    }

    private static void SetFloat(SerializedObject serialized, string propertyName, float value)
    {
        SerializedProperty property = serialized.FindProperty(propertyName);
        if (property != null)
            property.floatValue = value;
    }

    private static void CreateLabel(Transform parent, string name, string text, Vector3 worldPosition, Material plateMaterial)
    {
        GameObject root = new GameObject(name, typeof(RectTransform));
        root.transform.SetParent(parent, worldPositionStays: true);
        root.transform.position = worldPosition;
        root.transform.rotation = Quaternion.identity;
        root.transform.localScale = Vector3.one * 0.00115f;
        root.AddComponent<WorldSpaceBillboard>();

        RectTransform rootRect = root.GetComponent<RectTransform>();
        rootRect.sizeDelta = new Vector2(Mathf.Clamp(260f + text.Length * 18f, 360f, 560f), 92f);

        Canvas canvas = root.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.WorldSpace;
        canvas.sortingOrder = 40;
        root.AddComponent<CanvasScaler>();
        root.AddComponent<GraphicRaycaster>();

        Image background = root.AddComponent<Image>();
        background.color = new Color(0.015f, 0.025f, 0.030f, 0.88f);

        GameObject line = new GameObject("AccentLine", typeof(RectTransform));
        line.transform.SetParent(root.transform, false);
        RectTransform lineRect = line.GetComponent<RectTransform>();
        lineRect.anchorMin = new Vector2(0f, 0f);
        lineRect.anchorMax = new Vector2(1f, 0f);
        lineRect.pivot = new Vector2(0.5f, 0f);
        lineRect.sizeDelta = new Vector2(0f, 8f);
        lineRect.anchoredPosition = Vector2.zero;
        Image lineImage = line.AddComponent<Image>();
        lineImage.color = new Color(1f, 0.72f, 0.05f, 1f);

        GameObject textObject = new GameObject("Text", typeof(RectTransform));
        textObject.transform.SetParent(root.transform, false);
        RectTransform textRect = textObject.GetComponent<RectTransform>();
        textRect.anchorMin = Vector2.zero;
        textRect.anchorMax = Vector2.one;
        textRect.sizeDelta = new Vector2(-28f, -8f);
        textRect.anchoredPosition = new Vector2(0f, 5f);
        TextMeshProUGUI tmp = textObject.AddComponent<TextMeshProUGUI>();
        tmp.text = text;
        tmp.fontSize = 32f;
        tmp.enableAutoSizing = true;
        tmp.fontSizeMin = 18f;
        tmp.fontSizeMax = 34f;
        tmp.alignment = TextAlignmentOptions.Center;
        tmp.fontStyle = FontStyles.Bold;
        tmp.color = new Color(0.72f, 0.95f, 1f, 1f);
        EditorUtility.SetDirty(root);
    }

    private static Material EnsureArrowMaterial()
    {
        TextureImporter texImporter = AssetImporter.GetAtPath(ArrowAtlasPath) as TextureImporter;
        if (texImporter != null)
        {
            texImporter.wrapMode = TextureWrapMode.Clamp;
            texImporter.mipmapEnabled = true;
            texImporter.textureCompression = TextureImporterCompression.Compressed;
            texImporter.SaveAndReimport();
        }

        Material mat = AssetDatabase.LoadAssetAtPath<Material>(ArrowMaterialPath);
        Shader shader = Shader.Find("Universal Render Pipeline/Unlit");
        if (shader == null)
            shader = Shader.Find("Unlit/Texture");
        if (shader == null)
            shader = Shader.Find("Universal Render Pipeline/Lit");
        if (shader == null)
            shader = Shader.Find("Standard");
        if (mat == null)
        {
            mat = new Material(shader);
            mat.name = "M_TaskHint_Arrow_UVAtlas";
            AssetDatabase.CreateAsset(mat, ArrowMaterialPath);
        }
        else
        {
            mat.shader = shader;
        }

        Texture2D atlas = AssetDatabase.LoadAssetAtPath<Texture2D>(ArrowAtlasPath);
        if (atlas != null)
        {
            if (mat.HasProperty("_BaseMap")) mat.SetTexture("_BaseMap", atlas);
            if (mat.HasProperty("_MainTex")) mat.SetTexture("_MainTex", atlas);
        }
        if (mat.HasProperty("_BaseColor")) mat.SetColor("_BaseColor", Color.white);
        if (mat.HasProperty("_Color")) mat.SetColor("_Color", Color.white);
        if (mat.HasProperty("_EmissionColor")) mat.SetColor("_EmissionColor", new Color(1f, 0.68f, 0.04f));
        EditorUtility.SetDirty(mat);
        AssetDatabase.SaveAssets();
        return mat;
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

    private static GameObject FindChildByName(Transform root, string name)
    {
        foreach (Transform child in root.GetComponentsInChildren<Transform>(true))
        {
            if (child.name == name)
                return child.gameObject;
        }
        return null;
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
