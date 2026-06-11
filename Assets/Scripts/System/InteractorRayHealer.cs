using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit.Interactors;

/// <summary>
/// OLIVIA VR — InteractorRayHealer.cs
///
/// Force-enable interactor XR bawaan setiap frame agar ray kontroler tetap muncul
/// setelah Level 1 tanpa menyalakan visual debug/custom merah.
///
/// Bug:
///   ControllerInputActionManager (XR Toolkit Starter Asset) men-disable interactor
///   saat teleport mode aktif. Kalau gameplay pakai scripted teleport (XROrigin.Move...),
///   CIAM tidak fire teleport-deactivate event.
///
/// Strategi:
///   Force enable di LateUpdate. Cost: ~10 component lookups per frame. OK untuk VR rig.
/// </summary>
[DefaultExecutionOrder(32000)]
public class InteractorRayHealer : MonoBehaviour
{
    private static InteractorRayHealer _instance;
    private const string AutoObjectName = "XR_Interactor_Ray_Healer_Auto";

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStaticState()
    {
        _instance = null;
    }

    [Tooltip("Heal di LateUpdate setiap frame. Set false kalau mau pakai interval saja.")]
    [SerializeField] private bool _aggressiveHeal = true;

    [Tooltip("Interval (detik) untuk heal periodic kalau aggressive heal off.")]
    [SerializeField] private float _intervalDetik = 0.5f;

    private float _nextHeal;
    private NearFarInteractor[] _cachedNearFar;
    private float _nextRescan;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void AutoCreate()
    {
        if (_instance != null)
            return;

        var go = new GameObject(AutoObjectName);
        DontDestroyOnLoad(go);
        _instance = go.AddComponent<InteractorRayHealer>();
    }

    private void Awake()
    {
        if (_instance != null && _instance != this)
        {
            // Prefer the healer authored in the gameplay scene. The old code
            // destroyed this whole GameObject, which is the root "Script" object
            // and consequently removed every gameplay manager below it.
            if (_instance.gameObject.name == AutoObjectName)
                Destroy(_instance.gameObject);
            else
                Destroy(_instance);
        }

        _instance = this;
        if (gameObject.name == AutoObjectName)
            DontDestroyOnLoad(gameObject);
    }

    private void OnDestroy()
    {
        if (_instance == this)
            _instance = null;
    }

    private void Update()
    {
        if (!_aggressiveHeal && Time.unscaledTime >= _nextHeal)
        {
            _nextHeal = Time.unscaledTime + _intervalDetik;
            HealNow();
        }
    }

    private void LateUpdate()
    {
        if (_aggressiveHeal)
            HealNow();
    }

    public void HealNow()
    {
        // Cache list interactor; rescan tiap 2 detik supaya kalau ada interactor baru ditambah, ketemu.
        if (_cachedNearFar == null || Time.unscaledTime >= _nextRescan)
        {
            _cachedNearFar = UnityEngine.Object.FindObjectsByType<NearFarInteractor>(
                FindObjectsInactive.Include, FindObjectsSortMode.None);
            _nextRescan = Time.unscaledTime + 2f;
        }

        for (int i = 0; i < _cachedNearFar.Length; i++)
        {
            var nf = _cachedNearFar[i];
            if (nf == null) continue;
            if (!nf.gameObject.activeSelf) nf.gameObject.SetActive(true);
            if (!nf.enabled) nf.enabled = true;
            HealNearFarLineVisual(nf.gameObject);
        }

        XRInteractorRecovery.PulihkanRayInteractor();
    }

    private void HealNearFarLineVisual(GameObject root)
    {
        if (root == null) return;

        var behaviours = root.GetComponentsInChildren<MonoBehaviour>(true);
        for (int i = 0; i < behaviours.Length; i++)
        {
            var mb = behaviours[i];
            if (!IsBuiltInRayVisual(mb)) continue;
            if (!HasLineRenderableInHierarchy(mb.gameObject)) continue;

            if (!mb.gameObject.activeSelf)
                mb.gameObject.SetActive(true);
            if (!mb.enabled)
                mb.enabled = true;
        }

        foreach (var lr in root.GetComponentsInChildren<LineRenderer>(true))
        {
            if (lr == null) continue;
            if (!lr.gameObject.activeSelf)
                lr.gameObject.SetActive(true);
            if (!lr.enabled)
                lr.enabled = true;
        }
    }


    private bool IsBuiltInRayVisual(MonoBehaviour mb)
    {
        if (mb == null) return false;

        string tn = mb.GetType().Name;
        return tn == "XRInteractorLineVisual" || tn == "CurveVisualController";
    }

    private bool HasLineRenderableInHierarchy(GameObject go)
    {
        if (go == null) return false;
        return go.GetComponent<NearFarInteractor>() != null ||
               go.GetComponent<XRRayInteractor>() != null ||
               go.GetComponentInParent<NearFarInteractor>(true) != null ||
               go.GetComponentInParent<XRRayInteractor>(true) != null;
    }
}
