#if UNITY_EDITOR
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

[InitializeOnLoad]
public static class Level4SlurryPumpBlenderInstaller
{
    private const string ScenePath = "Assets/Scenes/Level1.unity";
    private const string FolderPath = "Assets/Art/Level4SlurryPumpBlender";
    private const string FbxPath = FolderPath + "/level4_slurry_pump_industrial_uv.fbx";
    private const string AtlasPath = FolderPath + "/level4_slurry_pump_uv_atlas.png";
    private const string MaterialPath = FolderPath + "/M_Level4_SlurryPump_UVAtlas.mat";
    private const string InstanceName = "Level4_SlurryPump_Blender_Industrial_UV_Auto";
    private const string AutoSessionKey = "OLIVIA_Level4_SlurryPump_Blender_Industrial_v1";

    static Level4SlurryPumpBlenderInstaller()
    {
        EditorApplication.delayCall += AutoInstallOnce;
    }

    [MenuItem("OLIVIA/4 - Install Level 4 Slurry Pump Blender Design")]
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
            Debug.LogWarning("[OLIVIA] Level 4 slurry pump FBX/atlas belum ditemukan. Installer dilewati.");
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

        GameObject pumpRoot = FindSceneObject("Pump_Skid_Industrial_Details");
        if (pumpRoot == null)
        {
            Debug.LogError("[OLIVIA] Gagal install slurry pump: Pump_Skid_Industrial_Details tidak ditemukan.");
            return;
        }

        Bounds oldBounds;
        bool hasOldBounds = TryGetRendererBounds(pumpRoot, out oldBounds, null, enabledOnly: false);
        if (!hasOldBounds)
            oldBounds = new Bounds(pumpRoot.transform.position, new Vector3(4f, 2.4f, 2f));

        GameObject existing = FindSceneObject(InstanceName);
        if (existing != null)
        {
            if (!forceReplace)
                return;
            Object.DestroyImmediate(existing);
        }

        GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(FbxPath);
        if (prefab == null)
        {
            Debug.LogError("[OLIVIA] Gagal load slurry pump FBX: " + FbxPath);
            return;
        }

        Material atlasMaterial = EnsureAtlasMaterial();
        GameObject instance = PrefabUtility.InstantiatePrefab(prefab, scene) as GameObject;
        if (instance == null)
        {
            Debug.LogError("[OLIVIA] Gagal instantiate slurry pump FBX.");
            return;
        }

        instance.name = InstanceName;
        instance.transform.SetPositionAndRotation(oldBounds.center, Quaternion.Euler(90f, 0f, 0f));
        instance.transform.localScale = Vector3.one;
        instance.transform.SetParent(pumpRoot.transform, worldPositionStays: true);

        AssignMaterial(instance, atlasMaterial);
        FitToOldPumpEnvelope(instance, oldBounds);
        DisableOldVisualRenderers(pumpRoot.transform, instance.transform);
        DisableLegacyPumpVisuals();
        AddSelectionCollider(instance);
        ConfigureRuntimeAnimation(instance);
        RewireLevel4Controller(instance);

        EditorUtility.SetDirty(instance);
        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
        AssetDatabase.SaveAssets();
        Selection.activeGameObject = instance;

        int rendererCount = instance.GetComponentsInChildren<Renderer>(true).Length;
        Debug.Log($"[OLIVIA] Level 4 Blender slurry pump installed. New={InstanceName}, renderers={rendererCount}, scene saved.");
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
            mat.name = "M_Level4_SlurryPump_UVAtlas";
            AssetDatabase.CreateAsset(mat, MaterialPath);
        }

        if (atlas != null)
        {
            if (mat.HasProperty("_BaseMap")) mat.SetTexture("_BaseMap", atlas);
            if (mat.HasProperty("_MainTex")) mat.SetTexture("_MainTex", atlas);
        }

        if (mat.HasProperty("_BaseColor")) mat.SetColor("_BaseColor", Color.white);
        if (mat.HasProperty("_Color")) mat.SetColor("_Color", Color.white);
        if (mat.HasProperty("_Metallic")) mat.SetFloat("_Metallic", 0.22f);
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

    private static void FitToOldPumpEnvelope(GameObject instance, Bounds oldBounds)
    {
        if (!TryGetRendererBounds(instance, out Bounds newBounds, null, enabledOnly: true))
            return;

        float desiredHeight = Mathf.Clamp(oldBounds.size.y * 1.02f, 1.75f, 2.25f);
        if (newBounds.size.y > 0.001f)
        {
            float scale = desiredHeight / newBounds.size.y;
            instance.transform.localScale *= Mathf.Clamp(scale, 0.02f, 500f);
        }

        ShiftBaseTo(instance, new Vector3(oldBounds.center.x, oldBounds.min.y, oldBounds.center.z));
    }

    private static void ShiftBaseTo(GameObject instance, Vector3 targetBaseCenter)
    {
        if (!TryGetRendererBounds(instance, out Bounds bounds, null, enabledOnly: true))
            return;

        Vector3 currentBaseCenter = new Vector3(bounds.center.x, bounds.min.y, bounds.center.z);
        instance.transform.position += targetBaseCenter - currentBaseCenter;
    }

    private static void DisableOldVisualRenderers(Transform oldRoot, Transform keepRoot)
    {
        foreach (Renderer renderer in oldRoot.GetComponentsInChildren<Renderer>(true))
        {
            if (renderer == null || renderer.transform.IsChildOf(keepRoot))
                continue;

            renderer.enabled = false;
            EditorUtility.SetDirty(renderer);
        }
    }

    private static void DisableLegacyPumpVisuals()
    {
        string[] names =
        {
            "L2_Clean_Slurry_Pump_Station_Redesign",
            "Mesin Pump",
            "Level4_SlurryPump_UnityIndustrialOverlay",
            "Level4_SlurryPump_Primitive_Redesign"
        };

        foreach (string name in names)
        {
            GameObject go = FindSceneObject(name);
            if (go == null || go.name == InstanceName)
                continue;

            foreach (Renderer renderer in go.GetComponentsInChildren<Renderer>(true))
            {
                renderer.enabled = false;
                EditorUtility.SetDirty(renderer);
            }
        }
    }

    private static void AddSelectionCollider(GameObject root)
    {
        if (!TryGetRendererBounds(root, out Bounds bounds, null, enabledOnly: true))
            return;

        BoxCollider collider = root.GetComponent<BoxCollider>();
        if (collider == null)
            collider = root.AddComponent<BoxCollider>();

        collider.isTrigger = false;
        collider.center = root.transform.InverseTransformPoint(bounds.center);
        Vector3 scale = root.transform.lossyScale;
        collider.size = new Vector3(
            bounds.size.x / Mathf.Max(0.001f, Mathf.Abs(scale.x)),
            bounds.size.y / Mathf.Max(0.001f, Mathf.Abs(scale.y)),
            bounds.size.z / Mathf.Max(0.001f, Mathf.Abs(scale.z))
        );
        EditorUtility.SetDirty(collider);
    }

    private static void ConfigureRuntimeAnimation(GameObject instance)
    {
        PumpClusterAnimator animator = instance.GetComponent<PumpClusterAnimator>();
        if (animator == null)
            animator = instance.AddComponent<PumpClusterAnimator>();

        SerializedObject serialized = new SerializedObject(animator);
        SetEnum(serialized, "_mode", 0);
        SetBool(serialized, "_autoFindByName", true);
        SetVector(serialized, "_sumbuRotasi", Vector3.right);
        SetFloat(serialized, "_rpmMaksimum", 520f);
        SetFloat(serialized, "_flowMaksimumDesain", 600f);
        SetFloat(serialized, "_flowMinimumAktif", 5f);
        SetFloat(serialized, "_variasiRpm", 0f);
        serialized.ApplyModifiedPropertiesWithoutUndo();
        EditorUtility.SetDirty(animator);
    }

    private static void RewireLevel4Controller(GameObject newPumpVisual)
    {
        Level4SlurryPumpController controller = Resources.FindObjectsOfTypeAll<Level4SlurryPumpController>()
            .FirstOrDefault(c => c != null && c.gameObject.scene.IsValid());
        if (controller == null)
            return;

        Renderer[] renderers = newPumpVisual.GetComponentsInChildren<Renderer>(true)
            .Where(r => r != null && r.enabled)
            .ToArray();

        SerializedObject serialized = new SerializedObject(controller);
        SerializedProperty pumpReference = serialized.FindProperty("_pumpReference");
        if (pumpReference != null)
            pumpReference.objectReferenceValue = newPumpVisual;

        SerializedProperty pumpAudioPosition = serialized.FindProperty("_pumpAudioPosition");
        if (pumpAudioPosition != null)
            pumpAudioPosition.objectReferenceValue = newPumpVisual.transform;

        SerializedProperty highlightRenderers = serialized.FindProperty("_pumpHighlightRenderers");
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

    private static void SetFloat(SerializedObject serialized, string name, float value)
    {
        SerializedProperty property = serialized.FindProperty(name);
        if (property != null)
            property.floatValue = value;
    }

    private static void SetBool(SerializedObject serialized, string name, bool value)
    {
        SerializedProperty property = serialized.FindProperty(name);
        if (property != null)
            property.boolValue = value;
    }

    private static void SetVector(SerializedObject serialized, string name, Vector3 value)
    {
        SerializedProperty property = serialized.FindProperty(name);
        if (property != null)
            property.vector3Value = value;
    }

    private static void SetEnum(SerializedObject serialized, string name, int value)
    {
        SerializedProperty property = serialized.FindProperty(name);
        if (property != null)
            property.enumValueIndex = value;
    }
}
#endif
