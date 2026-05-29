#if UNITY_EDITOR
using System;
using System.Collections;
using System.IO;
using System.Reflection;
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.XR.Interaction.Toolkit.Interactors;

[InitializeOnLoad]
public static class OLIVIAPlayModeSmokeRunner
{
    private const string RequestPath = "Assets/Editor/OLIVIA_PLAY_SMOKE_REQUEST.txt";
    private const string ResultPath = "Assets/Editor/OLIVIA_PLAY_SMOKE_RESULT.txt";
    private static bool _armed;
    private static double _enteredAt;

    static OLIVIAPlayModeSmokeRunner()
    {
        EditorApplication.update -= Tick;
        EditorApplication.update += Tick;
        EditorApplication.playModeStateChanged -= OnPlayModeStateChanged;
        EditorApplication.playModeStateChanged += OnPlayModeStateChanged;
        Debug.Log("[OLIVIA_SMOKE] runner loaded");
    }

    private static void Tick()
    {
        if (EditorApplication.isCompiling || EditorApplication.isUpdating)
            return;

        if (!_armed && File.Exists(RequestPath))
        {
            _armed = true;
            File.WriteAllText(ResultPath, "queued\n");
            if (!SceneManager.GetActiveScene().path.EndsWith("Level1.unity", StringComparison.OrdinalIgnoreCase))
                EditorSceneManager.OpenScene("Assets/Scenes/Level1.unity", OpenSceneMode.Single);
            EditorApplication.EnterPlaymode();
        }

        if (_armed && EditorApplication.isPlaying && EditorApplication.timeSinceStartup - _enteredAt > 1.5d)
        {
            _armed = false;
            new GameObject("OLIVIA_PlayMode_Smoke_Runtime").AddComponent<SmokeRuntime>();
        }
    }

    private static void OnPlayModeStateChanged(PlayModeStateChange state)
    {
        if (state == PlayModeStateChange.EnteredPlayMode)
            _enteredAt = EditorApplication.timeSinceStartup;
    }

    private sealed class SmokeRuntime : MonoBehaviour
    {
        private readonly StringBuilder _sb = new StringBuilder();
        private int _errors;

        private void Awake()
        {
            Application.logMessageReceived += OnLog;
            DontDestroyOnLoad(gameObject);
            StartCoroutine(Run());
        }

        private void OnDestroy()
        {
            Application.logMessageReceived -= OnLog;
        }

        private void OnLog(string condition, string stackTrace, LogType type)
        {
            if (type != LogType.Error && type != LogType.Exception && type != LogType.Assert)
                return;
            _errors++;
            _sb.AppendLine("LOG_" + type + ": " + condition);
        }

        private IEnumerator Run()
        {
            _sb.AppendLine("OLIVIA PlayMode Smoke Result");
            _sb.AppendLine("scene=" + SceneManager.GetActiveScene().path);

            var manager = FindFirstObjectByType<GameLevelManager>();
            var level6 = FindFirstObjectByType<Level6AcidInjectionController>(FindObjectsInactive.Include);
            _sb.AppendLine("GameLevelManager=" + (manager != null));
            _sb.AppendLine("Level6Controller=" + (level6 != null));

            if (manager != null && level6 != null)
            {
                SpeedUpLevel6(level6);
                manager.MulaiLevel(GameLevelManager.GameLevel.Level6_AcidInjection);
                yield return new WaitForSeconds(0.25f);
                XRInteractorRecovery.PulihkanRayInteractor();
                AppendXRState("after_start");
                AppendHands();

                _sb.AppendLine("DCS6=" + manager.TryOnDCSTombolDitekan(6));
                yield return null;
                _sb.AppendLine("VoiceOutlet=" + manager.OnVoiceKeywordTerdeteksi("outlet preheater dibuka"));
                yield return new WaitForSeconds(1.1f);

                ForceValve(level6, "_slurryValveDegrees", "_slurryFullOpenDegrees", "UpdateSlurryValveVisuals");
                yield return new WaitForSeconds(1.4f);
                _sb.AppendLine("SlurryArrived=" + level6.SlurryArrivedAtAutoclave);
                _sb.AppendLine("VoiceSlurry=" + manager.OnVoiceKeywordTerdeteksi("slurry masuk autoclave"));
                yield return new WaitForSeconds(1.1f);

                for (int i = 0; i < 35; i++)
                    level6.IncreaseAcidRatio();
                yield return new WaitForSeconds(1.2f);
                _sb.AppendLine("AcidRatio=" + level6.AcidRatioCurrent.ToString("F0"));
                _sb.AppendLine("PH=" + level6.PHCurrent.ToString("F1"));

                ForceValve(level6, "_acidValveDegrees", "_acidFullOpenDegrees", "UpdateAcidValveVisuals");
                yield return new WaitForSeconds(1.0f);
                _sb.AppendLine("AcidComplete=" + level6.AcidQuestComplete);
                _sb.AppendLine("VoiceFinal=" + manager.OnVoiceKeywordTerdeteksi("acid aktif, rasio 350 kilo, pH 1.0"));
                yield return new WaitForSeconds(0.25f);
                _sb.AppendLine("CurrentLevel=" + manager.CurrentLevel);
                AppendXRState("after_level6");
            }

            _sb.AppendLine("Errors=" + _errors);
            File.WriteAllText(ResultPath, _sb.ToString());
            Debug.Log("[OLIVIA_SMOKE] done\n" + _sb);
            try { if (File.Exists(RequestPath)) File.Delete(RequestPath); } catch { }
            yield return null;
            EditorApplication.ExitPlaymode();
        }

        private static void SpeedUpLevel6(Level6AcidInjectionController level6)
        {
            SetFloat(level6, "_durasiFade", 0.8f);
            SetFloat(level6, "_delaySetelahValveTerbuka", 0.05f);
            SetFloat(level6, "_durasiSlurryFlow", 0.2f);
            SetFloat(level6, "_durasiAutoclaveFill", 0.25f);
            SetFloat(level6, "_durasiAcidFlow", 0.2f);
        }

        private static void ForceValve(Level6AcidInjectionController level6, string degreesField, string maxField, string updateMethod)
        {
            float max = GetFloat(level6, maxField, 720f);
            SetFloat(level6, degreesField, max);
            Invoke(level6, updateMethod);
        }

        private void AppendXRState(string label)
        {
            XRInteractorRecovery.PulihkanRayInteractor();
            int nearReady = 0, nearTotal = 0, rayReady = 0, rayTotal = 0, lineReady = 0, lineTotal = 0;
            foreach (var nf in FindObjectsByType<NearFarInteractor>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            {
                nearTotal++;
                if (nf.enabled && nf.gameObject.activeInHierarchy) nearReady++;
            }
            foreach (var ray in FindObjectsByType<XRRayInteractor>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            {
                if (PathOf(ray.transform).Contains("Teleport Interactor")) continue;
                rayTotal++;
                if (ray.enabled && ray.gameObject.activeInHierarchy) rayReady++;
            }
            foreach (var lr in FindObjectsByType<LineRenderer>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            {
                string path = PathOf(lr.transform);
                if (!path.Contains("Controller") || path.Contains("Teleport Interactor")) continue;
                lineTotal++;
                if (lr.enabled && lr.gameObject.activeInHierarchy) lineReady++;
            }
            _sb.AppendLine(label + ": NearFar=" + nearReady + "/" + nearTotal + " XRRay=" + rayReady + "/" + rayTotal + " Line=" + lineReady + "/" + lineTotal);
        }

        private void AppendHands()
        {
            AppendHand("OLIVIA_Left_TransparentHand");
            AppendHand("OLIVIA_Right_TransparentHand");
        }

        private void AppendHand(string name)
        {
            Transform t = FindTransform(name);
            if (t == null)
            {
                _sb.AppendLine(name + "=MISSING");
                return;
            }
            _sb.AppendLine(name + " parent=" + (t.parent != null ? t.parent.name : "null") + " localEuler=" + t.localEulerAngles + " active=" + t.gameObject.activeInHierarchy);
        }

        private static Transform FindTransform(string name)
        {
            foreach (var t in FindObjectsByType<Transform>(FindObjectsInactive.Include, FindObjectsSortMode.None))
                if (t.name == name) return t;
            return null;
        }

        private static string PathOf(Transform t)
        {
            string path = t.name;
            while (t.parent != null)
            {
                t = t.parent;
                path = t.name + "/" + path;
            }
            return path;
        }

        private static float GetFloat(object target, string field, float fallback)
        {
            FieldInfo f = target.GetType().GetField(field, BindingFlags.Instance | BindingFlags.NonPublic);
            return f != null ? (float)f.GetValue(target) : fallback;
        }

        private static void SetFloat(object target, string field, float value)
        {
            FieldInfo f = target.GetType().GetField(field, BindingFlags.Instance | BindingFlags.NonPublic);
            if (f != null) f.SetValue(target, value);
        }

        private static void Invoke(object target, string method)
        {
            MethodInfo m = target.GetType().GetMethod(method, BindingFlags.Instance | BindingFlags.NonPublic);
            if (m != null) m.Invoke(target, null);
        }
    }
}
#endif
