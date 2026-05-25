#if UNITY_EDITOR
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// Installs the Blender-built Level 3 slurry/water tank visual into Level1 without
/// deleting gameplay objects referenced by the existing controllers.
/// </summary>
[InitializeOnLoad]
public static class Level3SlurryWaterTankSceneInstaller
{
    private const string ScenePath = "Assets/Scenes/Level1.unity";
    private const string FolderPath = "Assets/Art/Level3SlurryWaterTanksBlender";
    private const string FbxPath = FolderPath + "/level3_slurry_water_tanks_industrial_uv.fbx";
    private const string AtlasPath = FolderPath + "/level3_slurry_water_tanks_uv_atlas.png";
    private const string MatPath = FolderPath + "/M_Level3_SlurryWaterTanks_UVAtlas.mat";
    private const string InstanceName = "Level3_SlurryWaterTanks_Industrial_UV_Auto";
    private const string OverlayName = "Level3_SlurryWaterTanks_UnityIndustrialOverlay";
    private const string AutoSessionKey = "OLIVIA_Level3_SlurryWaterTank_Installed_This_Session_v4";
    private const string GeneratedMaterialFolder = "Assets/Materials/Generated";

    static Level3SlurryWaterTankSceneInstaller()
    {
        EditorApplication.delayCall += AutoInstallOnce;
    }

    [MenuItem("OLIVIA/3 - Install Level 3 Slurry Water Tank Design")]
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
            Debug.LogWarning("[OLIVIA] Level 3 tank FBX/atlas belum ditemukan. Installer dilewati.");
            return;
        }

        AssetDatabase.ImportAsset(FolderPath, ImportAssetOptions.ImportRecursive | ImportAssetOptions.ForceUpdate);

        Scene scene = SceneManager.GetSceneByPath(ScenePath);
        if (!scene.IsValid() || !scene.isLoaded)
        {
            if (!openSceneIfNeeded && SceneManager.GetActiveScene().path != ScenePath)
            {
                Debug.Log("[OLIVIA] Level1 tidak sedang aktif. Jalankan menu OLIVIA/3 untuk install visual tank.");
                return;
            }

            scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
        }

        GameObject mesinUtama = GameObject.Find("Mesin Utama");
        if (mesinUtama == null)
        {
            Debug.LogError("[OLIVIA] Gagal install tank: GameObject 'Mesin Utama' tidak ditemukan.");
            return;
        }

        GameObject oldTankRoot = FindGameplaySlurryTankRoot(mesinUtama.transform);
        if (oldTankRoot == null)
        {
            Debug.LogError("[OLIVIA] Gagal install tank: root 'Mesin Utama/Slurry Tank' tidak ditemukan.");
            return;
        }

        GameObject existing = FindChildByName(mesinUtama.transform, InstanceName);
        if (existing != null)
        {
            if (!forceReplace)
                return;
            Object.DestroyImmediate(existing);
        }

        GameObject model = AssetDatabase.LoadAssetAtPath<GameObject>(FbxPath);
        if (model == null)
        {
            Debug.LogError("[OLIVIA] Gagal load FBX: " + FbxPath);
            return;
        }

        Material atlasMaterial = EnsureAtlasMaterial();
        GameObject instance = PrefabUtility.InstantiatePrefab(model, scene) as GameObject;
        if (instance == null)
        {
            Debug.LogError("[OLIVIA] Gagal instantiate FBX tank.");
            return;
        }

        instance.name = InstanceName;
        instance.transform.SetParent(mesinUtama.transform, worldPositionStays: false);
        instance.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
        instance.transform.localScale = Vector3.one;
        instance.transform.localPosition = oldTankRoot.transform.localPosition;

        AssignMaterial(instance, atlasMaterial);
        FitToExistingTank(instance, oldTankRoot);
        HideImportedLayoutClutter(instance);
        DisableOldTankRenderers(mesinUtama.transform, oldTankRoot.transform, instance.transform);
        PreserveRuntimeReferences(oldTankRoot);
        RemoveVisibleUnityDetailOverlay(mesinUtama.transform);
        AddSimpleSelectionCollider(instance);

        EditorUtility.SetDirty(instance);
        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
        Selection.activeGameObject = instance;

        int rendererCount = instance.GetComponentsInChildren<Renderer>(true).Length;
        Debug.Log($"[OLIVIA] Level 3 slurry/water tank visual installed: {InstanceName}, overlay={OverlayName}, renderers={rendererCount}, scene saved.");
    }

    private static void RemoveVisibleUnityDetailOverlay(Transform mesinUtama)
    {
        GameObject existing = FindChildByName(mesinUtama, OverlayName);
        if (existing != null)
            Object.DestroyImmediate(existing);
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
        Material mat = AssetDatabase.LoadAssetAtPath<Material>(MatPath);
        if (mat == null)
        {
            Shader shader = Shader.Find("Universal Render Pipeline/Lit");
            if (shader == null) shader = Shader.Find("Standard");
            mat = new Material(shader);
            mat.name = "M_Level3_SlurryWaterTanks_UVAtlas";
            AssetDatabase.CreateAsset(mat, MatPath);
        }

        if (atlas != null)
        {
            if (mat.HasProperty("_BaseMap")) mat.SetTexture("_BaseMap", atlas);
            if (mat.HasProperty("_MainTex")) mat.SetTexture("_MainTex", atlas);
        }

        if (mat.HasProperty("_BaseColor")) mat.SetColor("_BaseColor", Color.white);
        if (mat.HasProperty("_Color")) mat.SetColor("_Color", Color.white);
        if (mat.HasProperty("_Metallic")) mat.SetFloat("_Metallic", 0.18f);
        if (mat.HasProperty("_Smoothness")) mat.SetFloat("_Smoothness", 0.34f);

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

    private static void HideImportedLayoutClutter(GameObject root)
    {
        string[] names =
        {
            "L3_TankArea_ConcretePad"
        };

        foreach (Renderer renderer in root.GetComponentsInChildren<Renderer>(true))
        {
            if (!names.Contains(renderer.gameObject.name))
                continue;

            renderer.enabled = false;
            EditorUtility.SetDirty(renderer);
        }
    }

    private static void FitToExistingTank(GameObject instance, GameObject oldTankRoot)
    {
        Renderer oldShell = oldTankRoot.GetComponentsInChildren<Renderer>(true)
            .Where(r => r.gameObject.name == "Slurry Tank")
            .OrderByDescending(r => FlatSize(r.bounds))
            .FirstOrDefault();

        if (oldShell == null)
        {
            oldShell = oldTankRoot.GetComponentsInChildren<Renderer>(true)
                .Where(r => r.gameObject.name != "Slurry_Fill")
                .OrderByDescending(r => FlatSize(r.bounds))
                .FirstOrDefault();
        }

        Renderer newShell = instance.GetComponentsInChildren<Renderer>(true)
            .Where(r => r.gameObject.name.Contains("L3_SlurryTank_OpenShell") ||
                        (r.gameObject.name.Contains("SlurryTank") && r.gameObject.name.Contains("Shell")))
            .OrderByDescending(r => SafeFlatSize(r.bounds))
            .FirstOrDefault();

        if (newShell == null)
        {
            newShell = instance.GetComponentsInChildren<Renderer>(true)
                .OrderByDescending(r => SafeFlatSize(r.bounds))
                .FirstOrDefault();
        }

        if (oldShell == null || newShell == null)
            return;

        float oldDiameter = EstimateFlatDiameter(oldShell.transform);
        float newDiameter = EstimateFlatDiameter(newShell.transform);
        if (oldDiameter > 0.1f && newDiameter > 0.001f)
        {
            float scale = Mathf.Clamp(oldDiameter / newDiameter, 0.25f, 500f);
            instance.transform.localScale = Vector3.one * scale;
        }

        newShell = instance.GetComponentsInChildren<Renderer>(true)
            .Where(r => r.gameObject.name.Contains("L3_SlurryTank_OpenShell") ||
                        (r.gameObject.name.Contains("SlurryTank") && r.gameObject.name.Contains("Shell")))
            .OrderByDescending(r => SafeFlatSize(r.bounds))
            .FirstOrDefault();

        if (newShell == null)
        {
            newShell = instance.GetComponentsInChildren<Renderer>(true)
                .OrderByDescending(r => SafeFlatSize(r.bounds))
                .FirstOrDefault();
        }

        if (newShell != null)
        {
            Vector3 delta = EstimateWorldCenter(oldShell.transform) - EstimateWorldCenter(newShell.transform);
            instance.transform.position += delta;
        }
    }

    private static void DisableOldTankRenderers(Transform mesinUtama, Transform oldTankRoot, Transform newInstance)
    {
        string[] prefixes =
        {
            "Slurry_Tank_",
            "Water_Tank_",
            "Vertical_Makeup_Water_Tank",
            "Lime_Slurry_Tank"
        };

        string[] exactNames =
        {
            "Water_Tank_Blue_Liquid_Surface",
            "Water_Tank_Internal_Blue_Surface",
            "Water_Tank_Label",
            "Water_Tank_Label_Text",
            "Water_Tank_Nameplate"
        };

        foreach (Renderer renderer in mesinUtama.GetComponentsInChildren<Renderer>(true))
        {
            if (renderer.transform.IsChildOf(newInstance))
                continue;

            string n = renderer.gameObject.name;
            bool hide = renderer.transform.IsChildOf(oldTankRoot) || prefixes.Any(n.StartsWith) || exactNames.Contains(n);
            if (n == "Slurry Tank" && renderer.transform.parent != null && renderer.transform.parent.name == "Slurry Tank")
                hide = true;

            if (!hide)
                continue;

            renderer.enabled = false;
            EditorUtility.SetDirty(renderer);
        }
    }

    private static void PreserveRuntimeReferences(GameObject oldTankRoot)
    {
        Transform slurryFill = FindChildByName(oldTankRoot.transform, "Slurry_Fill")?.transform;
        if (slurryFill != null)
        {
            slurryFill.gameObject.SetActive(true);
            Renderer fillRenderer = slurryFill.GetComponent<Renderer>();
            if (fillRenderer != null)
            {
                fillRenderer.enabled = false;
                EditorUtility.SetDirty(fillRenderer);
            }
        }

        GameObject agitator = FindChildByName(oldTankRoot.transform, "Agitator");
        if (agitator != null)
            EditorUtility.SetDirty(agitator);
    }

    private static void AddSimpleSelectionCollider(GameObject instance)
    {
        if (instance.GetComponent<BoxCollider>() != null)
            return;

        Bounds bounds = CalculateBounds(instance);
        if (bounds.size == Vector3.zero)
            return;

        BoxCollider box = instance.AddComponent<BoxCollider>();
        box.center = instance.transform.InverseTransformPoint(bounds.center);
        Vector3 localSize = instance.transform.InverseTransformVector(bounds.size);
        box.size = new Vector3(Mathf.Abs(localSize.x), Mathf.Abs(localSize.y), Mathf.Abs(localSize.z));
        EditorUtility.SetDirty(box);
    }

    private static void EnsureVisibleUnityDetailOverlay(Transform mesinUtama, Transform oldTankRoot)
    {
        GameObject existing = FindChildByName(mesinUtama, OverlayName);
        if (existing != null)
            Object.DestroyImmediate(existing);

        GameObject overlay = new GameObject(OverlayName);
        overlay.transform.SetParent(mesinUtama, worldPositionStays: false);
        overlay.transform.localPosition = Vector3.zero;
        overlay.transform.localRotation = Quaternion.identity;
        overlay.transform.localScale = Vector3.one;

        Renderer oldShell = oldTankRoot.GetComponentsInChildren<Renderer>(true)
            .Where(r => r.gameObject.name == "Slurry Tank")
            .OrderByDescending(r => EstimateFlatDiameter(r.transform))
            .FirstOrDefault();

        Transform basis = oldShell != null ? oldShell.transform : oldTankRoot;
        float slurryRadius = Mathf.Max(3.0f, EstimateFlatDiameter(basis) * 0.5f);
        float slurryHeight = Mathf.Max(4.6f, Mathf.Abs(basis.lossyScale.y) * 1.75f);
        Vector3 slurryCenter = EstimateWorldCenter(basis);
        float bottomY = slurryCenter.y - slurryHeight * 0.5f;
        float topY = slurryCenter.y + slurryHeight * 0.5f;

        Material steel = OverlayMaterial("M_L3_Overlay_WarmGalvanizedSteel", new Color(0.58f, 0.66f, 0.62f), 0.18f, 0.38f);
        Material dark = OverlayMaterial("M_L3_Overlay_DarkPipeSteel", new Color(0.035f, 0.045f, 0.045f), 0.25f, 0.42f);
        Material yellow = OverlayMaterial("M_L3_Overlay_SafetyYellow", new Color(1.0f, 0.64f, 0.06f), 0.02f, 0.32f);
        Material blue = OverlayMaterial("M_L3_Overlay_IndustrialBlue", new Color(0.03f, 0.13f, 0.32f), 0.05f, 0.30f);
        Material slurry = OverlayMaterial("M_L3_Overlay_SlurryPurple", new Color(0.46f, 0.18f, 0.50f), 0.0f, 0.55f);
        Material water = OverlayMaterial("M_L3_Overlay_ProcessWater", new Color(0.18f, 0.50f, 0.80f), 0.0f, 0.55f);
        Material concrete = OverlayMaterial("M_L3_Overlay_ConcretePad", new Color(0.42f, 0.40f, 0.36f), 0.0f, 0.25f);

        BuildTankBands(overlay.transform, "Slurry", slurryCenter, slurryRadius, bottomY, topY, steel, dark, yellow, slurry);
        BuildCircularSafetyRail(overlay.transform, "SlurryTop", slurryCenter, slurryRadius + 0.55f, topY + 0.10f, 1.05f, yellow, dark, 32);
        BuildLadder(overlay.transform, "SlurryFront", slurryCenter, slurryRadius + 0.25f, -145f, bottomY + 0.35f, topY + 0.75f, yellow, dark);
        BuildNameplate(overlay.transform, "SLURRY TANK", slurryCenter + new Vector3(0f, slurryHeight * 0.08f, -slurryRadius - 0.42f), Vector3.back, blue, Color.white, 3.3f);
        BuildGaugeStack(overlay.transform, slurryCenter + new Vector3(-slurryRadius * 0.55f, 0f, -slurryRadius - 0.38f), dark);

        float waterRadius = slurryRadius * 0.43f;
        float waterHeight = slurryHeight * 1.05f;
        Vector3 waterCenter = slurryCenter + new Vector3(slurryRadius + waterRadius + 5.30f, 0.35f, 0.30f);
        BuildWaterTank(overlay.transform, waterCenter, waterRadius, waterHeight, steel, dark, yellow, water, blue);

        Vector3 pipeA = waterCenter + new Vector3(waterRadius, waterHeight * 0.40f, 0f);
        Vector3 pipeB = slurryCenter + new Vector3(-slurryRadius * 0.55f, slurryHeight * 0.50f, slurryRadius * 0.18f);
        BuildPolylinePipe(overlay.transform, "MakeupWaterPipe", new[]
        {
            pipeA,
            pipeA + Vector3.right * (slurryRadius * 0.55f),
            pipeB + Vector3.left * (slurryRadius * 0.35f),
            pipeB
        }, 0.16f, steel);
        BuildValve(overlay.transform, "MakeupWaterValve", Vector3.Lerp(pipeA, pipeB, 0.52f), Vector3.right, 0.42f, dark, yellow);

        Vector3 outlet0 = slurryCenter + new Vector3(slurryRadius + 0.20f, -slurryHeight * 0.25f, slurryRadius * 0.18f);
        Vector3 outlet1 = outlet0 + new Vector3(slurryRadius * 1.05f, 0f, 0f);
        CylinderBetween(overlay.transform, "SlurryOutlet_HeavyPipe", outlet0, outlet1, 0.24f, steel);
        BuildValve(overlay.transform, "SlurryOutlet_Valve", Vector3.Lerp(outlet0, outlet1, 0.48f), Vector3.right, 0.55f, dark, yellow);
        Box(overlay.transform, "SlurryOutlet_ConcreteSupport", outlet1 + new Vector3(-0.6f, -0.55f, 0f), new Vector3(0.75f, 0.8f, 0.75f), Quaternion.identity, concrete);

        BuildCatwalk(overlay.transform, slurryCenter, waterCenter, slurryRadius, waterRadius, topY, dark, yellow);

        EditorUtility.SetDirty(overlay);
    }

    private static void BuildTankBands(Transform parent, string prefix, Vector3 center, float radius, float bottomY, float topY, Material steel, Material dark, Material yellow, Material liquid)
    {
        float height = topY - bottomY;
        Ring(parent, prefix + "_BottomReinforcementRing", center, radius + 0.08f, bottomY + height * 0.18f, 0.055f, dark, 40);
        Ring(parent, prefix + "_MidReinforcementRing", center, radius + 0.10f, bottomY + height * 0.52f, 0.052f, dark, 40);
        Ring(parent, prefix + "_TopRimPipe", center, radius + 0.13f, topY + 0.02f, 0.085f, steel, 48);

        for (int i = 0; i < 16; i++)
        {
            float angle = Mathf.PI * 2f * i / 16f;
            Vector3 radial = new Vector3(Mathf.Cos(angle), 0f, Mathf.Sin(angle));
            Vector3 pos = center + radial * (radius + 0.06f);
            CylinderBetween(parent, prefix + "_VerticalRib_" + i.ToString("00"), new Vector3(pos.x, bottomY + 0.45f, pos.z), new Vector3(pos.x, topY - 0.30f, pos.z), 0.045f, dark);
        }

        Cylinder(parent, prefix + "_VisibleLiquidSurface", new Vector3(center.x, bottomY + height * 0.50f, center.z), radius * 0.78f, 0.035f, liquid);
    }

    private static void BuildWaterTank(Transform parent, Vector3 center, float radius, float height, Material steel, Material dark, Material yellow, Material water, Material blue)
    {
        Cylinder(parent, "WaterTank_VisibleSteelShell", center, radius, height, steel);
        Cylinder(parent, "WaterTank_BlueWaterSurface", center + Vector3.up * (height * 0.42f), radius * 0.82f, 0.04f, water);
        Ring(parent, "WaterTank_BottomRing", center, radius + 0.05f, center.y - height * 0.48f, 0.055f, dark, 32);
        Ring(parent, "WaterTank_TopRim", center, radius + 0.06f, center.y + height * 0.50f, 0.065f, steel, 32);
        BuildCircularSafetyRail(parent, "WaterTankTop", center, radius + 0.35f, center.y + height * 0.55f, 0.82f, yellow, dark, 22);
        BuildLadder(parent, "WaterTankSide", center, radius + 0.15f, -110f, center.y - height * 0.48f, center.y + height * 0.95f, yellow, dark);
        BuildNameplate(parent, "WATER TANK", center + new Vector3(0f, height * 0.16f, -radius - 0.30f), Vector3.back, blue, Color.white, 2.35f);
    }

    private static void BuildCircularSafetyRail(Transform parent, string prefix, Vector3 center, float radius, float y, float height, Material yellow, Material dark, int segments)
    {
        for (int i = 0; i < segments; i++)
        {
            float a0 = Mathf.PI * 2f * i / segments;
            float a1 = Mathf.PI * 2f * (i + 1) / segments;
            Vector3 p0 = center + new Vector3(Mathf.Cos(a0) * radius, y + height, Mathf.Sin(a0) * radius);
            Vector3 p1 = center + new Vector3(Mathf.Cos(a1) * radius, y + height, Mathf.Sin(a1) * radius);
            Vector3 m0 = center + new Vector3(Mathf.Cos(a0) * radius, y + height * 0.52f, Mathf.Sin(a0) * radius);
            Vector3 m1 = center + new Vector3(Mathf.Cos(a1) * radius, y + height * 0.52f, Mathf.Sin(a1) * radius);
            Material mat = (i % 2 == 0) ? yellow : dark;
            CylinderBetween(parent, prefix + "_TopRail_" + i.ToString("00"), p0, p1, 0.055f, mat);
            CylinderBetween(parent, prefix + "_MidRail_" + i.ToString("00"), m0, m1, 0.042f, mat);
            CylinderBetween(parent, prefix + "_Post_" + i.ToString("00"), new Vector3(p0.x, y, p0.z), p0, 0.045f, yellow);
        }
    }

    private static void BuildLadder(Transform parent, string prefix, Vector3 center, float radius, float angleDeg, float bottomY, float topY, Material yellow, Material dark)
    {
        float angle = angleDeg * Mathf.Deg2Rad;
        Vector3 radial = new Vector3(Mathf.Cos(angle), 0f, Mathf.Sin(angle));
        Vector3 tangent = new Vector3(-radial.z, 0f, radial.x);
        Vector3 basePoint = center + radial * radius;
        Vector3 left = basePoint - tangent * 0.28f;
        Vector3 right = basePoint + tangent * 0.28f;
        CylinderBetween(parent, prefix + "_LadderRail_L", new Vector3(left.x, bottomY, left.z), new Vector3(left.x, topY, left.z), 0.045f, yellow);
        CylinderBetween(parent, prefix + "_LadderRail_R", new Vector3(right.x, bottomY, right.z), new Vector3(right.x, topY, right.z), 0.045f, yellow);

        int rungCount = Mathf.Max(5, Mathf.RoundToInt((topY - bottomY) / 0.45f));
        for (int i = 0; i <= rungCount; i++)
        {
            float y = Mathf.Lerp(bottomY + 0.25f, topY - 0.20f, i / (float)rungCount);
            CylinderBetween(parent, prefix + "_Rung_" + i.ToString("00"), new Vector3(left.x, y, left.z), new Vector3(right.x, y, right.z), 0.032f, dark);
        }
    }

    private static void BuildGaugeStack(Transform parent, Vector3 basePosition, Material dark)
    {
        for (int i = 0; i < 3; i++)
            Box(parent, "SlurryLevelMarker_" + i.ToString("00"), basePosition + Vector3.up * (i * 0.85f), new Vector3(0.62f, 0.22f, 0.055f), Quaternion.identity, dark);
    }

    private static void BuildCatwalk(Transform parent, Vector3 slurryCenter, Vector3 waterCenter, float slurryRadius, float waterRadius, float y, Material dark, Material yellow)
    {
        Vector3 mid = Vector3.Lerp(slurryCenter, waterCenter, 0.5f) + Vector3.up * (y - slurryCenter.y + 0.28f);
        float length = Vector3.Distance(new Vector3(slurryCenter.x, 0f, slurryCenter.z), new Vector3(waterCenter.x, 0f, waterCenter.z)) - slurryRadius * 0.55f;
        Quaternion rot = Quaternion.LookRotation((slurryCenter - waterCenter).normalized, Vector3.up);
        Box(parent, "InterTank_ServiceCatwalk_Grating", mid, new Vector3(1.45f, 0.12f, Mathf.Max(2.0f, length)), rot, dark);
        CylinderBetween(parent, "InterTank_Catwalk_LeftRail", mid + rot * new Vector3(-0.78f, 0.70f, -length * 0.48f), mid + rot * new Vector3(-0.78f, 0.70f, length * 0.48f), 0.04f, yellow);
        CylinderBetween(parent, "InterTank_Catwalk_RightRail", mid + rot * new Vector3(0.78f, 0.70f, -length * 0.48f), mid + rot * new Vector3(0.78f, 0.70f, length * 0.48f), 0.04f, yellow);
    }

    private static void BuildPolylinePipe(Transform parent, string prefix, Vector3[] points, float radius, Material mat)
    {
        for (int i = 0; i < points.Length - 1; i++)
            CylinderBetween(parent, prefix + "_Seg_" + i.ToString("00"), points[i], points[i + 1], radius, mat);
    }

    private static void BuildValve(Transform parent, string name, Vector3 position, Vector3 axis, float size, Material body, Material wheel)
    {
        CylinderBetween(parent, name + "_Body", position - axis.normalized * size * 0.5f, position + axis.normalized * size * 0.5f, size * 0.22f, body);
        Cylinder(parent, name + "_Handwheel", position + Vector3.up * (size * 0.75f), size * 0.34f, size * 0.08f, wheel);
    }

    private static void BuildNameplate(Transform parent, string text, Vector3 position, Vector3 normal, Material plate, Color textColor, float width)
    {
        Box(parent, text.Replace(" ", "_") + "_NameplateBack", position, new Vector3(width, 0.38f, 0.08f), Quaternion.identity, plate);

        GameObject label = new GameObject(text.Replace(" ", "_") + "_Text");
        label.transform.position = position + normal.normalized * 0.08f + Vector3.down * 0.13f;
        label.transform.rotation = Quaternion.LookRotation(-normal.normalized, Vector3.up);
        label.transform.SetParent(parent, worldPositionStays: true);
        TextMesh mesh = label.AddComponent<TextMesh>();
        mesh.text = text;
        mesh.anchor = TextAnchor.MiddleCenter;
        mesh.alignment = TextAlignment.Center;
        mesh.characterSize = 0.20f;
        mesh.fontSize = 80;
        mesh.color = textColor;
    }

    private static void Ring(Transform parent, string name, Vector3 center, float radius, float y, float pipeRadius, Material mat, int segments)
    {
        for (int i = 0; i < segments; i++)
        {
            float a0 = Mathf.PI * 2f * i / segments;
            float a1 = Mathf.PI * 2f * (i + 1) / segments;
            Vector3 p0 = center + new Vector3(Mathf.Cos(a0) * radius, y - center.y, Mathf.Sin(a0) * radius);
            Vector3 p1 = center + new Vector3(Mathf.Cos(a1) * radius, y - center.y, Mathf.Sin(a1) * radius);
            CylinderBetween(parent, name + "_Seg_" + i.ToString("00"), p0, p1, pipeRadius, mat);
        }
    }

    private static void Cylinder(Transform parent, string name, Vector3 position, float radius, float height, Material mat)
    {
        GameObject go = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        go.name = name;
        go.transform.position = position;
        go.transform.localScale = new Vector3(radius * 2f, height * 0.5f, radius * 2f);
        go.transform.SetParent(parent, worldPositionStays: true);
        AssignPrimitiveMaterial(go, mat);
    }

    private static void CylinderBetween(Transform parent, string name, Vector3 a, Vector3 b, float radius, Material mat)
    {
        Vector3 delta = b - a;
        if (delta.sqrMagnitude < 0.0001f)
            return;

        GameObject go = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        go.name = name;
        go.transform.position = (a + b) * 0.5f;
        go.transform.rotation = Quaternion.FromToRotation(Vector3.up, delta.normalized);
        go.transform.localScale = new Vector3(radius * 2f, delta.magnitude * 0.5f, radius * 2f);
        go.transform.SetParent(parent, worldPositionStays: true);
        AssignPrimitiveMaterial(go, mat);
    }

    private static void Box(Transform parent, string name, Vector3 position, Vector3 size, Quaternion rotation, Material mat)
    {
        GameObject go = GameObject.CreatePrimitive(PrimitiveType.Cube);
        go.name = name;
        go.transform.position = position;
        go.transform.rotation = rotation;
        go.transform.localScale = size;
        go.transform.SetParent(parent, worldPositionStays: true);
        AssignPrimitiveMaterial(go, mat);
    }

    private static void AssignPrimitiveMaterial(GameObject go, Material mat)
    {
        Renderer renderer = go.GetComponent<Renderer>();
        if (renderer != null)
            renderer.sharedMaterial = mat;

        Collider collider = go.GetComponent<Collider>();
        if (collider != null)
            Object.DestroyImmediate(collider);
    }

    private static Material OverlayMaterial(string name, Color color, float metallic, float smoothness)
    {
        if (!Directory.Exists(GeneratedMaterialFolder))
            Directory.CreateDirectory(GeneratedMaterialFolder);

        string path = GeneratedMaterialFolder + "/" + name + ".mat";
        Material mat = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (mat == null)
        {
            Shader shader = Shader.Find("Universal Render Pipeline/Lit");
            if (shader == null) shader = Shader.Find("Standard");
            mat = new Material(shader);
            mat.name = name;
            AssetDatabase.CreateAsset(mat, path);
        }

        if (mat.HasProperty("_BaseColor")) mat.SetColor("_BaseColor", color);
        if (mat.HasProperty("_Color")) mat.SetColor("_Color", color);
        if (mat.HasProperty("_Metallic")) mat.SetFloat("_Metallic", metallic);
        if (mat.HasProperty("_Smoothness")) mat.SetFloat("_Smoothness", smoothness);
        EditorUtility.SetDirty(mat);
        return mat;
    }

    private static Bounds CalculateBounds(GameObject root)
    {
        Renderer[] renderers = root.GetComponentsInChildren<Renderer>(true);
        if (renderers.Length == 0)
            return new Bounds(root.transform.position, Vector3.zero);

        Bounds bounds = renderers[0].bounds;
        for (int i = 1; i < renderers.Length; i++)
            bounds.Encapsulate(renderers[i].bounds);
        return bounds;
    }

    private static float FlatSize(Bounds bounds)
    {
        return Mathf.Max(bounds.size.x, bounds.size.z);
    }

    private static float SafeFlatSize(Bounds bounds)
    {
        float size = FlatSize(bounds);
        return float.IsNaN(size) ? 0f : size;
    }

    private static float EstimateFlatDiameter(Transform transform)
    {
        Renderer renderer = transform.GetComponent<Renderer>();
        if (renderer != null && SafeFlatSize(renderer.bounds) > 0.001f)
            return SafeFlatSize(renderer.bounds);

        Collider collider = transform.GetComponent<Collider>();
        if (collider != null && SafeFlatSize(collider.bounds) > 0.001f)
            return SafeFlatSize(collider.bounds);

        Vector3 scale = transform.lossyScale;
        return Mathf.Max(Mathf.Abs(scale.x), Mathf.Abs(scale.z));
    }

    private static Vector3 EstimateWorldCenter(Transform transform)
    {
        Renderer renderer = transform.GetComponent<Renderer>();
        if (renderer != null && renderer.bounds.size != Vector3.zero)
            return renderer.bounds.center;

        Collider collider = transform.GetComponent<Collider>();
        if (collider != null && collider.bounds.size != Vector3.zero)
            return collider.bounds.center;

        return transform.position;
    }

    private static GameObject FindGameplaySlurryTankRoot(Transform mesinUtama)
    {
        foreach (Transform child in mesinUtama.GetComponentsInChildren<Transform>(true))
        {
            if (child.name != "Slurry Tank")
                continue;

            if (FindChildByName(child, "Slurry_Fill") != null)
                return child.gameObject;
        }

        return FindChildByName(mesinUtama, "Slurry Tank");
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
}
#endif
