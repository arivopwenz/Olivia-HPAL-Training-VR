#if UNITY_EDITOR
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

[InitializeOnLoad]
public static class Level5PreHeaterBlenderInstaller
{
    private const string ScenePath = "Assets/Scenes/Level1.unity";
    private const string FolderPath = "Assets/Art/Level5PreHeaterBlender";
    private const string FbxPath = FolderPath + "/level5_preheater_industrial_uv.fbx";
    private const string AtlasPath = FolderPath + "/level5_preheater_uv_atlas.png";
    private const string MaterialPath = FolderPath + "/M_Level5_PreHeater_UVAtlas.mat";
    private const string FunctionalInstanceName = "Level5_PreHeater_Blender_Industrial_UV_Auto";
    private const string OverviewInstanceName = "Level5_PreHeater_Blender_Industrial_UV_Overview";
    private const string AutoSessionKey = "OLIVIA_Level5_PreHeater_Blender_Industrial_v1";

    static Level5PreHeaterBlenderInstaller()
    {
        EditorApplication.delayCall += AutoInstallOnce;
    }

    [MenuItem("OLIVIA/5 - Install Level 5 PreHeater Blender Design")]
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
        Install(openSceneIfNeeded: true, forceReplace: true);
    }

    private static void Install(bool openSceneIfNeeded, bool forceReplace)
    {
        if (!File.Exists(FbxPath) || !File.Exists(AtlasPath))
        {
            Debug.LogWarning("[OLIVIA] Level 5 PreHeater FBX/atlas belum ditemukan. Installer dilewati.");
            return;
        }

        AssetDatabase.ImportAsset(FbxPath, ImportAssetOptions.ForceUpdate);
        AssetDatabase.ImportAsset(AtlasPath, ImportAssetOptions.ForceUpdate);
        ConfigureModelImporter();

        Scene scene = SceneManager.GetSceneByPath(ScenePath);
        if (!scene.IsValid() || !scene.isLoaded)
        {
            if (!openSceneIfNeeded && SceneManager.GetActiveScene().path != ScenePath)
                return;

            scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
        }

        GameObject preheaterRoot = FindSceneObject("PreHeater_Field_1");
        if (preheaterRoot == null)
        {
            Debug.LogError("[OLIVIA] Gagal install PreHeater: PreHeater_Field_1 tidak ditemukan.");
            return;
        }

        GameObject oldFunctionalVisual = FindSceneObject("Preheater_TripleTrain_Redesign");
        Bounds functionalBounds = GetTargetBounds(oldFunctionalVisual, preheaterRoot, new Vector3(12.8f, 7.1f, 9.8f));

        GameObject model = AssetDatabase.LoadAssetAtPath<GameObject>(FbxPath);
        if (model == null)
        {
            Debug.LogError("[OLIVIA] Gagal load PreHeater FBX: " + FbxPath);
            return;
        }

        Material atlasMaterial = EnsureAtlasMaterial();
        GameObject functional = ReplaceInstance(model, scene, preheaterRoot.transform, FunctionalInstanceName, forceReplace);
        if (functional == null)
            return;

        functional.transform.SetPositionAndRotation(functionalBounds.center, Quaternion.Euler(90f, 0f, 0f));
        functional.transform.localScale = Vector3.one;
        functional.transform.SetParent(preheaterRoot.transform, worldPositionStays: true);
        AssignMaterial(functional, atlasMaterial);
        FitToBounds(functional, functionalBounds, fitHeight: 4.75f, fitLength: Mathf.Min(10.8f, functionalBounds.size.x * 0.90f));
        ShiftBaseTo(functional, new Vector3(functionalBounds.center.x, functionalBounds.min.y, functionalBounds.center.z));

        GameObject oldOverviewRoot = FindSceneObject("Preheater_Industrial_Details");
        GameObject overview = null;
        if (oldOverviewRoot != null)
        {
            Bounds overviewBounds = GetTargetBounds(oldOverviewRoot, oldOverviewRoot, new Vector3(9.8f, 5.8f, 3.2f));
            overview = ReplaceInstance(model, scene, oldOverviewRoot.transform, OverviewInstanceName, forceReplace);
            if (overview != null)
            {
                overview.transform.SetPositionAndRotation(overviewBounds.center, Quaternion.Euler(90f, 0f, 0f));
                overview.transform.localScale = Vector3.one;
                overview.transform.SetParent(oldOverviewRoot.transform, worldPositionStays: true);
                AssignMaterial(overview, atlasMaterial);
                FitToBounds(overview, overviewBounds, fitHeight: 4.75f, fitLength: Mathf.Min(9.0f, overviewBounds.size.x * 0.92f));
                ShiftBaseTo(overview, new Vector3(overviewBounds.center.x, overviewBounds.min.y, overviewBounds.center.z));
            }
        }

        DisableOldPreheaterVisuals(preheaterRoot.transform, functional != null ? functional.transform : null, overview != null ? overview.transform : null);
        RemoveWeirdPipeClarifier();
        RemoveLegacyPipeClutter();
        RewireVisualSync(preheaterRoot, functional);
        RewireLevel4PreheaterHighlight(functional);

        EditorUtility.SetDirty(preheaterRoot);
        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
        AssetDatabase.SaveAssets();
        Selection.activeGameObject = functional;

        int rendererCount = functional.GetComponentsInChildren<Renderer>(true).Length;
        int overviewRenderers = overview != null ? overview.GetComponentsInChildren<Renderer>(true).Length : 0;
        Debug.Log($"[OLIVIA] Level 5 Blender PreHeater installed. Functional={rendererCount} renderers, overview={overviewRenderers}, weird pipe hidden, scene saved.");
    }

    private static GameObject ReplaceInstance(GameObject model, Scene scene, Transform parent, string name, bool forceReplace)
    {
        GameObject existing = FindSceneObject(name);
        if (existing != null)
        {
            if (!forceReplace)
                return existing;
            Object.DestroyImmediate(existing);
        }

        GameObject instance = PrefabUtility.InstantiatePrefab(model, scene) as GameObject;
        if (instance == null)
        {
            Debug.LogError("[OLIVIA] Gagal instantiate PreHeater FBX.");
            return null;
        }

        instance.name = name;
        instance.transform.SetParent(parent, worldPositionStays: false);
        return instance;
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
            mat.name = "M_Level5_PreHeater_UVAtlas";
            AssetDatabase.CreateAsset(mat, MaterialPath);
        }

        if (atlas != null)
        {
            if (mat.HasProperty("_BaseMap")) mat.SetTexture("_BaseMap", atlas);
            if (mat.HasProperty("_MainTex")) mat.SetTexture("_MainTex", atlas);
        }

        if (mat.HasProperty("_BaseColor")) mat.SetColor("_BaseColor", Color.white);
        if (mat.HasProperty("_Color")) mat.SetColor("_Color", Color.white);
        if (mat.HasProperty("_Metallic")) mat.SetFloat("_Metallic", 0.20f);
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

    private static Bounds GetTargetBounds(GameObject preferred, GameObject fallback, Vector3 fallbackSize)
    {
        if (preferred != null && TryGetRendererBounds(preferred, out Bounds preferredBounds, null, enabledOnly: false))
            return preferredBounds;

        if (fallback != null && TryGetRendererBounds(fallback, out Bounds fallbackBounds, null, enabledOnly: true))
            return fallbackBounds;

        Vector3 center = fallback != null ? fallback.transform.position : Vector3.zero;
        return new Bounds(center, fallbackSize);
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

    private static void DisableOldPreheaterVisuals(Transform preheaterRoot, Transform functionalKeep, Transform overviewKeep)
    {
        string[] legacyFieldChildren =
        {
            "Foundation",
            "SupportLeg_1",
            "SupportLeg_2",
            "SupportLeg_3",
            "SupportLeg_4",
            "Vessel",
            "BottomCap",
            "TopCap",
            "HeatingFin_1",
            "HeatingFin_2",
            "HeatingFin_3",
            "HeatingFin_4",
            "FlangeInlet",
            "SteamValve_Handwheel",
            "TempGauge"
        };

        foreach (string rootName in legacyFieldChildren)
        {
            GameObject root = preheaterRoot != null ? FindDirectChild(preheaterRoot, rootName) : null;
            if (root == null)
                continue;
            DisableRenderers(root, functionalKeep, overviewKeep);
        }

        DisableRenderers(FindSceneObject("Preheater_TripleTrain_Redesign"), functionalKeep, overviewKeep);
        DisableRenderers(FindSceneObject("Preheater_Industrial_Details"), functionalKeep, overviewKeep);
    }

    private static void DisableRenderers(GameObject root, Transform functionalKeep, Transform overviewKeep)
    {
        if (root == null)
            return;

        foreach (Renderer renderer in root.GetComponentsInChildren<Renderer>(true))
        {
            if (renderer == null)
                continue;
            if (functionalKeep != null && renderer.transform.IsChildOf(functionalKeep))
                continue;
            if (overviewKeep != null && renderer.transform.IsChildOf(overviewKeep))
                continue;

            renderer.enabled = false;
            EditorUtility.SetDirty(renderer);
        }
    }

    private static void RemoveWeirdPipeClarifier()
    {
        GameObject clarifier = FindSceneObject("L1_UserRequested_Pipe_Direction_Clarifier");
        if (clarifier == null)
            return;

        foreach (Collider collider in clarifier.GetComponentsInChildren<Collider>(true))
        {
            collider.enabled = false;
            EditorUtility.SetDirty(collider);
        }

        foreach (Renderer renderer in clarifier.GetComponentsInChildren<Renderer>(true))
        {
            renderer.enabled = false;
            EditorUtility.SetDirty(renderer);
        }

        clarifier.SetActive(false);
        EditorUtility.SetDirty(clarifier);
    }

    private static void RemoveLegacyPipeClutter()
    {
        DisableVisualRoot("Steam_Accumulator_TripleBank_Redesign");
        DisableVisualRoot("Preheater_Walkway_Stair_Continuation_Redesign");
        DisableVisualRoot("Raised_Preheater_Autoclave_PipeRack_Redesign");
        DisableVisualRoot("Pipe_PreheaterToAutoclave");
        DisableChildrenWithPrefixes("Process_Pipes_Repaired", "Pump_To_Preheater_");
        DisableProcessRouteVisuals("Pump_To_Preheater");
        DisableProcessRouteVisuals("HeatReceiver_To_Preheater");
        DisableProcessRouteVisuals("Preheater_To_Autoclave");

        GameObject yellowGuards = FindSceneObject("Yellow_Pipe_Guards");
        if (yellowGuards != null)
        {
            foreach (Transform child in yellowGuards.transform)
            {
                if (child == null)
                    continue;
                if (!child.name.StartsWith("Preheater_To_Autoclave_") &&
                    !child.name.StartsWith("Pump_To_Preheater_"))
                    continue;

                DisableVisualTree(child.gameObject);
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

            DisableVisualTree(child.gameObject);
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

            DisableVisualTree(segment.gameObject);
            if (segment.pipeRenderer != null)
            {
                segment.pipeRenderer.enabled = false;
                EditorUtility.SetDirty(segment.pipeRenderer);
            }

            if (segment.flowVisual != null)
                DisableVisualTree(segment.flowVisual);

            EditorUtility.SetDirty(segment);
        }
    }

    private static void DisableVisualRoot(string name)
    {
        GameObject root = FindSceneObject(name);
        if (root == null)
            return;

        DisableVisualTree(root);
    }

    private static void DisableVisualTree(GameObject root)
    {
        if (root == null)
            return;

        foreach (Collider collider in root.GetComponentsInChildren<Collider>(true))
        {
            collider.enabled = false;
            EditorUtility.SetDirty(collider);
        }

        foreach (Renderer renderer in root.GetComponentsInChildren<Renderer>(true))
        {
            renderer.enabled = false;
            EditorUtility.SetDirty(renderer);
        }
    }

    private static void RewireVisualSync(GameObject preheaterRoot, GameObject functionalVisual)
    {
        if (preheaterRoot == null || functionalVisual == null)
            return;

        PreHeaterVisualSync sync = preheaterRoot.GetComponent<PreHeaterVisualSync>();
        if (sync == null)
            return;

        Renderer[] fins = functionalVisual.GetComponentsInChildren<Renderer>(true)
            .Where(r => r != null && r.enabled && r.gameObject.name.StartsWith("HeatingFin"))
            .ToArray();

        Renderer led = null;
        GameObject ledObject = FindSceneObject("LED_Preheater");
        if (ledObject != null)
            led = ledObject.GetComponent<Renderer>();

        SerializedObject serialized = new SerializedObject(sync);
        SerializedProperty finProp = serialized.FindProperty("_finRenderers");
        if (finProp != null)
        {
            finProp.arraySize = fins.Length;
            for (int i = 0; i < fins.Length; i++)
                finProp.GetArrayElementAtIndex(i).objectReferenceValue = fins[i];
        }

        SerializedProperty ledProp = serialized.FindProperty("_ledIndicator");
        if (ledProp != null && led != null)
            ledProp.objectReferenceValue = led;

        serialized.ApplyModifiedPropertiesWithoutUndo();
        EditorUtility.SetDirty(sync);
    }

    private static void RewireLevel4PreheaterHighlight(GameObject functionalVisual)
    {
        if (functionalVisual == null)
            return;

        Level4SlurryPumpController controller = Resources.FindObjectsOfTypeAll<Level4SlurryPumpController>()
            .FirstOrDefault(c => c != null && c.gameObject.scene.IsValid());
        if (controller == null)
            return;

        Renderer[] renderers = functionalVisual.GetComponentsInChildren<Renderer>(true)
            .Where(r => r != null && r.enabled)
            .ToArray();

        SerializedObject serialized = new SerializedObject(controller);
        SerializedProperty preheaterReference = serialized.FindProperty("_preheaterReference");
        GameObject preheaterRoot = FindSceneObject("PreHeater_Field_1");
        if (preheaterReference != null && preheaterRoot != null)
            preheaterReference.objectReferenceValue = preheaterRoot;

        SerializedProperty highlightRenderers = serialized.FindProperty("_preheaterHighlightRenderers");
        if (highlightRenderers != null)
        {
            highlightRenderers.arraySize = renderers.Length;
            for (int i = 0; i < renderers.Length; i++)
                highlightRenderers.GetArrayElementAtIndex(i).objectReferenceValue = renderers[i];
        }

        serialized.ApplyModifiedPropertiesWithoutUndo();
        EditorUtility.SetDirty(controller);
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

    private static GameObject FindDirectChild(Transform parent, string name)
    {
        if (parent == null)
            return null;

        foreach (Transform child in parent)
        {
            if (child != null && child.name == name)
                return child.gameObject;
        }

        return null;
    }
}
#endif
