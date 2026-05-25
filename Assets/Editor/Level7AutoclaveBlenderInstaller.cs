#if UNITY_EDITOR
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

public static class Level7AutoclaveBlenderInstaller
{
    private const string ScenePath = "Assets/Scenes/Level1.unity";
    private const string FolderPath = "Assets/Art/Level7AutoclaveBlender";
    private const string FbxPath = FolderPath + "/level7_autoclave_industrial_uv.fbx";
    private const string AtlasPath = FolderPath + "/level7_autoclave_uv_atlas.png";
    private const string MaterialPath = FolderPath + "/M_Level7_Autoclave_UVAtlas.mat";
    private const string InstanceName = "Level7_Autoclave_Blender_Industrial_UV_Auto";
    private const string AutoSessionKey = "OLIVIA_Level7_Autoclave_Blender_Industrial_v2_SAFE_SCOPE";

    [MenuItem("OLIVIA/7 - Install Level 7 Autoclave Blender Design")]
    public static void InstallFromMenu()
    {
        Install(openSceneIfNeeded: true, forceReplace: true);
    }

    public static bool RepairAndInstallFromAutoRunner()
    {
        return Install(openSceneIfNeeded: true, forceReplace: true);
    }

    private static bool Install(bool openSceneIfNeeded, bool forceReplace)
    {
        if (!File.Exists(FbxPath) || !File.Exists(AtlasPath))
        {
            Debug.LogWarning("[OLIVIA] Level 7 Autoclave FBX/atlas belum ditemukan. Installer dilewati.");
            return false;
        }

        AssetDatabase.ImportAsset(FolderPath, ImportAssetOptions.ImportRecursive | ImportAssetOptions.ForceUpdate);
        ConfigureModelImporter();

        Scene scene = SceneManager.GetSceneByPath(ScenePath);
        if (!scene.IsValid() || !scene.isLoaded)
        {
            if (!openSceneIfNeeded && SceneManager.GetActiveScene().path != ScenePath)
                return false;

            scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
        }

        GameObject autoclaveRoot = FindSceneObject("Autoclave_Field");
        if (autoclaveRoot == null)
        {
            Debug.LogError("[OLIVIA] Gagal install Autoclave: Autoclave_Field tidak ditemukan.");
            return false;
        }

        RestoreVisualsHiddenByPreviousInstaller();
        int removed = RemoveExistingLevel7BlenderAutoclaves();

        Bounds targetBounds = GetOriginalAutoclaveTargetBounds(autoclaveRoot, new Vector3(24f, 11.5f, 7.2f));
        GameObject model = AssetDatabase.LoadAssetAtPath<GameObject>(FbxPath);
        if (model == null)
        {
            Debug.LogError("[OLIVIA] Gagal load Autoclave FBX: " + FbxPath);
            return false;
        }

        Material atlasMaterial = EnsureAtlasMaterial();
        GameObject instance = PrefabUtility.InstantiatePrefab(model, scene) as GameObject;
        if (instance == null)
        {
            Debug.LogError("[OLIVIA] Gagal instantiate Autoclave FBX.");
            return false;
        }

        instance.name = InstanceName;
        instance.transform.SetParent(autoclaveRoot.transform, worldPositionStays: false);
        instance.transform.SetPositionAndRotation(targetBounds.center, Quaternion.Euler(-90f, 0f, 0f));
        instance.transform.localScale = Vector3.one;
        AssignMaterial(instance, atlasMaterial);
        FitToBounds(instance, targetBounds, fitHeight: targetBounds.size.y * 1.08f, fitLength: targetBounds.size.x * 1.00f);
        ShiftBaseTo(instance, new Vector3(targetBounds.center.x, targetBounds.min.y, targetBounds.center.z));

        DisableOriginalAutoclaveBodyVisuals(autoclaveRoot.transform, instance.transform);
        RewireLevel7Controller(autoclaveRoot, instance);
        AddSimpleGaugeColliders(instance);

        EditorUtility.SetDirty(autoclaveRoot);
        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
        AssetDatabase.SaveAssets();
        Selection.activeGameObject = instance;

        int rendererCount = instance.GetComponentsInChildren<Renderer>(true).Length;
        Debug.Log($"[OLIVIA] Level 7 Blender Autoclave repaired safely. RemovedLevel7Prefabs={removed}, Renderers={rendererCount}, only Autoclave body visuals replaced, scene saved.");
        return true;
    }

    private static void ConfigureModelImporter()
    {
        ModelImporter importer = AssetImporter.GetAtPath(FbxPath) as ModelImporter;
        if (importer == null)
            return;

        importer.importCameras = false;
        importer.importLights = false;
        importer.materialImportMode = ModelImporterMaterialImportMode.None;
        importer.globalScale = 1f;
        importer.isReadable = false;
        importer.meshCompression = ModelImporterMeshCompression.Medium;
        importer.SaveAndReimport();
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
            mat.name = "M_Level7_Autoclave_UVAtlas";
            AssetDatabase.CreateAsset(mat, MaterialPath);
        }

        if (atlas != null)
        {
            if (mat.HasProperty("_BaseMap")) mat.SetTexture("_BaseMap", atlas);
            if (mat.HasProperty("_MainTex")) mat.SetTexture("_MainTex", atlas);
        }

        if (mat.HasProperty("_BaseColor")) mat.SetColor("_BaseColor", Color.white);
        if (mat.HasProperty("_Color")) mat.SetColor("_Color", Color.white);
        if (mat.HasProperty("_Metallic")) mat.SetFloat("_Metallic", 0.24f);
        if (mat.HasProperty("_Smoothness")) mat.SetFloat("_Smoothness", 0.42f);

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

    private static void FitToBounds(GameObject instance, Bounds targetBounds, float fitHeight, float fitLength)
    {
        if (!TryGetRendererBounds(instance, out Bounds currentBounds, null, enabledOnly: true))
            return;

        float scaleByHeight = fitHeight / Mathf.Max(0.001f, currentBounds.size.y);
        float scaleByLength = fitLength / Mathf.Max(0.001f, Mathf.Max(currentBounds.size.x, currentBounds.size.z));
        float scale = Mathf.Min(scaleByHeight, scaleByLength);
        instance.transform.localScale *= Mathf.Clamp(scale, 0.02f, 500f);

        if (TryGetRendererBounds(instance, out currentBounds, null, enabledOnly: true))
            instance.transform.position += targetBounds.center - currentBounds.center;
    }

    private static void ShiftBaseTo(GameObject instance, Vector3 targetBaseCenter)
    {
        if (!TryGetRendererBounds(instance, out Bounds bounds, null, enabledOnly: true))
            return;

        Vector3 currentBaseCenter = new Vector3(bounds.center.x, bounds.min.y, bounds.center.z);
        instance.transform.position += targetBaseCenter - currentBaseCenter;
    }

    private static Bounds GetTargetBounds(GameObject root, Vector3 fallbackSize)
    {
        if (root != null && TryGetRendererBounds(root, out Bounds bounds, null, enabledOnly: false))
            return bounds;

        Vector3 center = root != null ? root.transform.position : Vector3.zero;
        return new Bounds(center, fallbackSize);
    }

    private static Bounds GetAutoclaveTargetBounds(GameObject root, GameObject existing, Vector3 fallbackSize)
    {
        Vector3 rootPosition = root != null ? root.transform.position : Vector3.zero;
        Bounds fallback = new Bounds(rootPosition + Vector3.up * (fallbackSize.y * 0.5f), fallbackSize);
        if (root == null)
            return fallback;

        Bounds bounds = new Bounds(rootPosition, Vector3.zero);
        bool hasBounds = false;
        foreach (Renderer renderer in root.GetComponentsInChildren<Renderer>(true))
        {
            if (renderer == null)
                continue;
            if (existing != null && renderer.transform.IsChildOf(existing.transform))
                continue;
            if (renderer.transform.name.StartsWith("L7_XRay_"))
                continue;
            if (renderer.transform.name.StartsWith("Level7_Autoclave_Blender"))
                continue;

            if (!hasBounds)
            {
                bounds = renderer.bounds;
                hasBounds = true;
            }
            else
            {
                bounds.Encapsulate(renderer.bounds);
            }
        }

        if (!hasBounds)
            return fallback;

        bool badScale =
            bounds.size.x < 8f || bounds.size.x > 32f ||
            bounds.size.y < 2f || bounds.size.y > 12f ||
            bounds.size.z < 2f || bounds.size.z > 14f;

        return badScale ? fallback : bounds;
    }

    private static Bounds GetOriginalAutoclaveTargetBounds(GameObject root, Vector3 fallbackSize)
    {
        Vector3 center = root != null ? root.transform.TransformPoint(new Vector3(0f, fallbackSize.y * 0.52f, 0f)) : Vector3.zero;
        Bounds fallback = new Bounds(center, fallbackSize);
        if (root == null)
            return fallback;

        string[] anchorNames =
        {
            "Shell",
            "EndCap_Left",
            "EndCap_Right",
            "SupportSaddle_1",
            "SupportSaddle_2",
            "Manway",
            "PressureGauge",
            "TemperatureGauge",
            "AgitatorMotor",
            "ReliefValve_Stack",
            "ReliefValve_Cap"
        };

        Bounds bounds = fallback;
        bool hasBounds = false;
        foreach (string name in anchorNames)
        {
            Transform child = root.transform.Find(name);
            if (child == null)
                continue;

            if (!TryGetRendererBounds(child.gameObject, out Bounds childBounds, null, enabledOnly: false))
                continue;

            if (!hasBounds)
            {
                bounds = childBounds;
                hasBounds = true;
            }
            else
            {
                bounds.Encapsulate(childBounds);
            }
        }

        if (!hasBounds)
            return fallback;

        if (bounds.size.x < 12f || bounds.size.x > 34f || bounds.size.y < 5f || bounds.size.y > 16f)
            return fallback;

        return bounds;
    }

    private static int RemoveExistingLevel7BlenderAutoclaves()
    {
        int removed = 0;
        HashSet<GameObject> roots = new HashSet<GameObject>();
        foreach (GameObject go in Resources.FindObjectsOfTypeAll<GameObject>())
        {
            if (go == null || !go.scene.IsValid())
                continue;

            GameObject root = PrefabUtility.GetOutermostPrefabInstanceRoot(go);
            if (root == null || root != go || roots.Contains(root))
                continue;

            Object source = PrefabUtility.GetCorrespondingObjectFromSource(root);
            string sourcePath = source != null ? AssetDatabase.GetAssetPath(source).Replace("\\", "/") : string.Empty;
            bool isLevel7Blender =
                root.name == InstanceName ||
                root.name.StartsWith("Level7_Autoclave_Blender") ||
                sourcePath == FbxPath ||
                sourcePath.Contains("/Level7AutoclaveBlender/");

            if (!isLevel7Blender)
                continue;

            roots.Add(root);
        }

        foreach (GameObject root in roots)
        {
            Object.DestroyImmediate(root);
            removed++;
        }

        return removed;
    }

    private static void DisableOriginalAutoclaveBodyVisuals(Transform autoclaveRoot, Transform keep)
    {
        if (autoclaveRoot == null)
            return;

        HashSet<string> replaceNames = new HashSet<string>
        {
            "Shell",
            "EndCap_Left",
            "EndCap_Right",
            "SupportSaddle_1",
            "SupportSaddle_2",
            "Manway",
            "PressureGauge",
            "TemperatureGauge",
            "AgitatorShaft",
            "AgitatorMotor",
            "ReliefValve_Stack",
            "ReliefValve_Cap"
        };

        foreach (Transform child in autoclaveRoot)
        {
            if (child == null)
                continue;
            if (keep != null && child.IsChildOf(keep))
                continue;
            if (child.name == "Level7Controller")
                continue;
            if (!replaceNames.Contains(child.name))
                continue;

            foreach (Renderer renderer in child.GetComponentsInChildren<Renderer>(true))
            {
                if (keep != null && renderer.transform.IsChildOf(keep))
                    continue;
                renderer.enabled = false;
                EditorUtility.SetDirty(renderer);
            }
        }
    }

    private static void RestoreVisualsHiddenByPreviousInstaller()
    {
        EnableVisualRoot("Autoclave_Clear_Catwalk_Rail_Redesign");
        EnableVisualRoot("Autoclave_TieIn_Guard_Rail");
        EnableVisualRoot("Pipe_AutoclaveToFlash_Transparent");
        EnableVisualRoot("Pipe_AutoclaveToFlash_Liquid");
        EnableVisualRoot("LetdownValve_Assembly");
        EnableVisualRoot("AcidLine_ToAutoclave_Rerouted_Clearance");
        EnableVisualRoot("Acid_Autoclave_TieIn_Spool");
        EnableVisualRoot("Acid_Autoclave_Run_Rail_L");
        EnableVisualRoot("Acid_Autoclave_Run_Post_L_03");
        EnableVisualRoot("Acid_Autoclave_Run_Post_R_00");
        EnableVisualRoot("Acid_Autoclave_Run_Post_R_01");
        EnableVisualRoot("Acid_Autoclave_Run_Post_R_05");
        EnableVisualRoot("Acid_Autoclave_Run_Post_R_06");
        EnableVisualRoot("Acid_Autoclave_Run_Rail_R");
        EnableVisualRoot("Acid_Joint");
        EnableVisualRoot("M1_Autoclave_Bolted_Nozzle_Redesign");
        EnableVisualRoot("Autoclave_Flange_Bolts_Details");
        EnableChildrenWithPrefixes("Process_Pipes_Repaired", "Autoclave_To_Flash_");
        EnableChildrenWithPrefixes("Pipe_Connection_Adjustments", "Acid_Autoclave_", "Autoclave_", "Preheater_To_Autoclave_");
        EnableChildrenWithPrefixes("Industrial_Stairs_Catwalks", "Autoclave_", "Catwalk_");
        EnableProcessRouteVisuals("Autoclave_To_Flash");
        EnableProcessRouteVisuals("Legacy_Autoclave_To_Flash_Local");
        EnableProcessRouteVisuals("Autoclave_Vapor_To_HeatReceiver");
        EnableLocalAutoclaveRouteVisuals("Acid_To_Autoclave");
    }

    private static void RemoveLegacyAutoclavePipeClutter()
    {
        DisableVisualRoot("Autoclave_Clear_Catwalk_Rail_Redesign");
        DisableVisualRoot("Autoclave_TieIn_Guard_Rail");
        DisableVisualRoot("Pipe_AutoclaveToFlash_Transparent");
        DisableVisualRoot("Pipe_AutoclaveToFlash_Liquid");
        DisableVisualRoot("LetdownValve_Assembly");
        DisableVisualRoot("AcidLine_ToAutoclave_Rerouted_Clearance");
        DisableVisualRoot("Acid_Autoclave_TieIn_Spool");
        DisableVisualRoot("Acid_Autoclave_Run_Rail_L");
        DisableVisualRoot("Acid_Autoclave_Run_Post_L_03");
        DisableVisualRoot("Acid_Autoclave_Run_Post_R_00");
        DisableVisualRoot("Acid_Autoclave_Run_Post_R_01");
        DisableVisualRoot("Acid_Autoclave_Run_Post_R_05");
        DisableVisualRoot("Acid_Autoclave_Run_Post_R_06");
        DisableVisualRoot("Acid_Autoclave_Run_Rail_R");
        DisableVisualRoot("Acid_Joint");
        DisableVisualRoot("M1_Autoclave_Bolted_Nozzle_Redesign");
        DisableVisualRoot("Autoclave_Flange_Bolts_Details");
        DisableChildrenWithPrefixes("Process_Pipes_Repaired", "Autoclave_To_Flash_");
        DisableChildrenWithPrefixes("Pipe_Connection_Adjustments", "Acid_Autoclave_", "Autoclave_", "Preheater_To_Autoclave_");
        DisableChildrenWithPrefixes("Industrial_Stairs_Catwalks", "Autoclave_", "Catwalk_");

        DisableProcessRouteVisuals("Autoclave_To_Flash");
        DisableProcessRouteVisuals("Legacy_Autoclave_To_Flash_Local");
        DisableProcessRouteVisuals("Autoclave_Vapor_To_HeatReceiver");
        DisableLocalAutoclaveRouteVisuals("Acid_To_Autoclave");

        GameObject yellowGuards = FindSceneObject("Yellow_Pipe_Guards");
        if (yellowGuards != null)
        {
            foreach (Transform child in yellowGuards.transform)
            {
                if (child == null)
                    continue;
                if (!child.name.StartsWith("Autoclave_To_Flash_") &&
                    !child.name.StartsWith("Acid_Autoclave_"))
                    continue;

                DisableVisualTree(child.gameObject, disableColliders: true);
            }
        }
    }

    private static void DisableChildrenWithPrefixes(string rootName, params string[] prefixes)
    {
        GameObject root = FindSceneObject(rootName);
        if (root == null || prefixes == null || prefixes.Length == 0)
            return;

        foreach (Transform child in root.transform)
        {
            if (child == null)
                continue;
            if (!prefixes.Any(prefix => child.name.StartsWith(prefix)))
                continue;

            DisableVisualTree(child.gameObject, disableColliders: true);
        }
    }

    private static void EnableChildrenWithPrefixes(string rootName, params string[] prefixes)
    {
        GameObject root = FindSceneObject(rootName);
        if (root == null || prefixes == null || prefixes.Length == 0)
            return;

        foreach (Transform child in root.transform)
        {
            if (child == null)
                continue;
            if (!prefixes.Any(prefix => child.name.StartsWith(prefix)))
                continue;

            EnableVisualTree(child.gameObject, enableColliders: true);
        }
    }

    private static void DisableProcessRouteVisuals(string routeId)
    {
        if (string.IsNullOrWhiteSpace(routeId))
            return;

        foreach (ProcessPipeSegment segment in Resources.FindObjectsOfTypeAll<ProcessPipeSegment>())
        {
            if (segment == null || segment.routeId != routeId || !segment.gameObject.scene.IsValid())
                continue;

            DisableVisualTree(segment.gameObject, disableColliders: true);
            if (segment.pipeRenderer != null)
            {
                segment.pipeRenderer.enabled = false;
                EditorUtility.SetDirty(segment.pipeRenderer);
            }

            if (segment.flowVisual != null)
                DisableVisualTree(segment.flowVisual, disableColliders: true);

            EditorUtility.SetDirty(segment);
        }
    }

    private static void EnableProcessRouteVisuals(string routeId)
    {
        if (string.IsNullOrWhiteSpace(routeId))
            return;

        foreach (ProcessPipeSegment segment in Resources.FindObjectsOfTypeAll<ProcessPipeSegment>())
        {
            if (segment == null || segment.routeId != routeId || !segment.gameObject.scene.IsValid())
                continue;

            EnableVisualTree(segment.gameObject, enableColliders: true);
            if (segment.pipeRenderer != null)
            {
                segment.pipeRenderer.enabled = true;
                EditorUtility.SetDirty(segment.pipeRenderer);
            }

            if (segment.flowVisual != null)
                EnableVisualTree(segment.flowVisual, enableColliders: false);

            EditorUtility.SetDirty(segment);
        }
    }

    private static void DisableLocalAutoclaveRouteVisuals(string routeId)
    {
        foreach (ProcessPipeSegment segment in Resources.FindObjectsOfTypeAll<ProcessPipeSegment>())
        {
            if (segment == null || segment.routeId != routeId || !segment.gameObject.scene.IsValid())
                continue;

            bool local = segment.name.Contains("Autoclave") ||
                         (segment.fromNode ?? string.Empty).Contains("Autoclave") ||
                         (segment.toNode ?? string.Empty).Contains("Autoclave") ||
                         segment.order >= 6;
            if (!local)
                continue;

            DisableVisualTree(segment.gameObject, disableColliders: true);
            if (segment.pipeRenderer != null)
            {
                segment.pipeRenderer.enabled = false;
                EditorUtility.SetDirty(segment.pipeRenderer);
            }
            EditorUtility.SetDirty(segment);
        }
    }

    private static void EnableLocalAutoclaveRouteVisuals(string routeId)
    {
        foreach (ProcessPipeSegment segment in Resources.FindObjectsOfTypeAll<ProcessPipeSegment>())
        {
            if (segment == null || segment.routeId != routeId || !segment.gameObject.scene.IsValid())
                continue;

            bool local = segment.name.Contains("Autoclave") ||
                         (segment.fromNode ?? string.Empty).Contains("Autoclave") ||
                         (segment.toNode ?? string.Empty).Contains("Autoclave") ||
                         segment.order >= 6;
            if (!local)
                continue;

            EnableVisualTree(segment.gameObject, enableColliders: true);
            if (segment.pipeRenderer != null)
            {
                segment.pipeRenderer.enabled = true;
                EditorUtility.SetDirty(segment.pipeRenderer);
            }
            EditorUtility.SetDirty(segment);
        }
    }

    private static void RewireLevel7Controller(GameObject autoclaveRoot, GameObject visual)
    {
        Level7AutoclaveController controller = Resources.FindObjectsOfTypeAll<Level7AutoclaveController>()
            .FirstOrDefault(c => c != null && c.gameObject.scene.IsValid());
        if (controller == null || visual == null)
            return;

        Renderer shell = FindRenderer(visual, "L7_Autoclave_PressureShell");
        Renderer leftCap = FindRenderer(visual, "L7_Autoclave_EndCap_Left");
        Renderer rightCap = FindRenderer(visual, "L7_Autoclave_EndCap_Right");
        Transform[] agitators = visual.GetComponentsInChildren<Transform>(true)
            .Where(t => t != null && IsAgitatorRotor(t.gameObject.name))
            .OrderBy(t => t.gameObject.name)
            .ToArray();
        Transform agitator = agitators.Length > 0 ? agitators[0] : FindTransform(visual, "L7_XRay_AgitatorShaft");
        Transform innerFluid = FindTransform(visual, "L7_XRay_InnerSlurry_Surface");
        Transform pressureNeedle = FindTransform(visual, "L7_PressureGauge_Needle");
        Transform tempNeedle = FindTransform(visual, "L7_TemperatureGauge_Needle");
        GameObject[] xrayObjects = visual.GetComponentsInChildren<Transform>(true)
            .Where(t => t != null && t.gameObject.name.StartsWith("L7_XRay_"))
            .Select(t => t.gameObject)
            .Distinct()
            .ToArray();

        // Keep the cutaway internals visible in the authored scene preview.
        // Runtime Awake() still hides these until X-Ray mode is toggled.
        foreach (GameObject obj in xrayObjects)
            obj.SetActive(true);

        SerializedObject serialized = new SerializedObject(controller);
        SetObject(serialized, "_autoclaveField", autoclaveRoot);
        SetObject(serialized, "_shellRenderer", shell);
        SerializedProperty endCaps = serialized.FindProperty("_endCapRenderers");
        if (endCaps != null)
        {
            endCaps.arraySize = 2;
            endCaps.GetArrayElementAtIndex(0).objectReferenceValue = leftCap;
            endCaps.GetArrayElementAtIndex(1).objectReferenceValue = rightCap;
        }
        SetObject(serialized, "_agitatorShaft", agitator);
        SetObjectArray(serialized, "_agitatorShafts", agitators);
        SetVector(serialized, "_agitatorAxis", Vector3.forward);
        SetObject(serialized, "_innerFluid", innerFluid);
        SetObject(serialized, "_pressureGaugeNeedle", pressureNeedle);
        SetObject(serialized, "_temperatureGaugeNeedle", tempNeedle);

        SerializedProperty xrayProp = serialized.FindProperty("_xrayOnlyObjects");
        if (xrayProp != null)
        {
            xrayProp.arraySize = xrayObjects.Length;
            for (int i = 0; i < xrayObjects.Length; i++)
                xrayProp.GetArrayElementAtIndex(i).objectReferenceValue = xrayObjects[i];
        }

        serialized.ApplyModifiedPropertiesWithoutUndo();
        EditorUtility.SetDirty(controller);
    }

    private static void AddSimpleGaugeColliders(GameObject visual)
    {
        string[] gaugeNames =
        {
            "L7_PressureGauge_Housing",
            "L7_TemperatureGauge_Housing",
            "L7_RpmGauge_Housing"
        };

        foreach (string gaugeName in gaugeNames)
        {
            Transform t = FindTransform(visual, gaugeName);
            if (t == null || t.GetComponent<Collider>() != null)
                continue;

            BoxCollider collider = t.gameObject.AddComponent<BoxCollider>();
            collider.size = Vector3.one * 0.65f;
            EditorUtility.SetDirty(t.gameObject);
        }
    }

    private static void SetObject(SerializedObject serialized, string propertyName, Object value)
    {
        SerializedProperty property = serialized.FindProperty(propertyName);
        if (property != null)
            property.objectReferenceValue = value;
    }

    private static bool IsAgitatorRotor(string name)
    {
        const string prefix = "L7_XRay_AgitatorRotor_";
        if (string.IsNullOrEmpty(name) || !name.StartsWith(prefix) || name.Length != prefix.Length + 2)
            return false;

        return char.IsDigit(name[prefix.Length]) && char.IsDigit(name[prefix.Length + 1]);
    }

    private static void SetObjectArray(SerializedObject serialized, string propertyName, Object[] values)
    {
        SerializedProperty property = serialized.FindProperty(propertyName);
        if (property == null)
            return;

        property.arraySize = values != null ? values.Length : 0;
        for (int i = 0; i < property.arraySize; i++)
            property.GetArrayElementAtIndex(i).objectReferenceValue = values[i];
    }

    private static void SetVector(SerializedObject serialized, string propertyName, Vector3 value)
    {
        SerializedProperty property = serialized.FindProperty(propertyName);
        if (property != null)
            property.vector3Value = value;
    }

    private static Renderer FindRenderer(GameObject root, string name)
    {
        Transform t = FindTransform(root, name);
        return t != null ? t.GetComponent<Renderer>() : null;
    }

    private static Transform FindTransform(GameObject root, string name)
    {
        if (root == null)
            return null;

        foreach (Transform child in root.GetComponentsInChildren<Transform>(true))
        {
            if (child != null && child.name == name)
                return child;
        }

        return null;
    }

    private static void DisableVisualRoot(string name)
    {
        GameObject root = FindSceneObject(name);
        if (root == null)
            return;

        DisableVisualTree(root, disableColliders: true);
    }

    private static void EnableVisualRoot(string name)
    {
        GameObject root = FindSceneObject(name);
        if (root == null)
            return;

        EnableVisualTree(root, enableColliders: true);
    }

    private static void DisableVisualTree(GameObject root, bool disableColliders)
    {
        if (root == null)
            return;

        if (disableColliders)
        {
            foreach (Collider collider in root.GetComponentsInChildren<Collider>(true))
            {
                collider.enabled = false;
                EditorUtility.SetDirty(collider);
            }
        }

        foreach (Renderer renderer in root.GetComponentsInChildren<Renderer>(true))
        {
            renderer.enabled = false;
            EditorUtility.SetDirty(renderer);
        }
    }

    private static void EnableVisualTree(GameObject root, bool enableColliders)
    {
        if (root == null)
            return;

        if (enableColliders)
        {
            foreach (Collider collider in root.GetComponentsInChildren<Collider>(true))
            {
                collider.enabled = true;
                EditorUtility.SetDirty(collider);
            }
        }

        foreach (Renderer renderer in root.GetComponentsInChildren<Renderer>(true))
        {
            renderer.enabled = true;
            EditorUtility.SetDirty(renderer);
        }
    }

    private static bool TryGetRendererBounds(GameObject root, out Bounds bounds, Transform excludeRoot, bool enabledOnly)
    {
        bounds = new Bounds(root.transform.position, Vector3.zero);
        bool hasBounds = false;

        foreach (Renderer renderer in root.GetComponentsInChildren<Renderer>(true))
        {
            if (renderer == null)
                continue;
            if (excludeRoot != null && renderer.transform.IsChildOf(excludeRoot))
                continue;
            if (enabledOnly && !renderer.enabled)
                continue;

            if (!hasBounds)
            {
                bounds = renderer.bounds;
                hasBounds = true;
            }
            else
            {
                bounds.Encapsulate(renderer.bounds);
            }
        }

        return hasBounds;
    }

    private static GameObject FindSceneObject(string name)
    {
        GameObject go = GameObject.Find(name);
        if (go != null)
            return go;

        foreach (GameObject candidate in Resources.FindObjectsOfTypeAll<GameObject>())
        {
            if (candidate == null || candidate.name != name)
                continue;
            if (!candidate.scene.IsValid())
                continue;
            return candidate;
        }

        return null;
    }
}
#endif
