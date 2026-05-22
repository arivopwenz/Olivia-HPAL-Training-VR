using UnityEngine;
using System.Collections.Generic;

/// <summary>
/// OLIVIA VR - PumpClusterAnimator.cs
///
/// Animator untuk cluster Mesin Pump (multiple pump units di satu area).
/// Auto-find semua child dengan nama mengandung "Mesin Pump" / "Impeller" / "Motor"
/// dan rotate sesuai flow rate.
///
/// Pemakaian: pasang script di parent "SlurryPump_Field" atau "Mesin Pump".
/// </summary>
public class PumpClusterAnimator : MonoBehaviour
{
    [Header("=== Sumber Flow ===")]
    [SerializeField] private FlowMode _mode = FlowMode.Auto;
    [SerializeField] private float _flowManual = 450f;
    [SerializeField] private float _flowMaksimumDesain = 600f;
    [SerializeField] private float _flowMinimumAktif = 5f;
    [Tooltip("Rotasi tetap jalan setelah Level 3 selesai (slurry mengalir terus).")]
    [SerializeField] private bool _alwaysOnSetelahLevel3 = true;

    [Header("=== Target Rotasi ===")]
    [Tooltip("Auto-find Transform child dengan nama match keyword.")]
    [SerializeField] private bool _autoFindByName = true;
    [SerializeField] private string[] _keywordNama = { "ImpellerPivot", "Impeller", "MotorHousing" };
    [SerializeField] private List<Transform> _customRotators = new List<Transform>();

    [Header("=== Rotasi Settings ===")]
    [Tooltip("RPM motor pada flow rate maksimum.")]
    [SerializeField] private float _rpmMaksimum = 600f;
    [Tooltip("Sumbu rotasi (default Y).")]
    [SerializeField] private Vector3 _sumbuRotasi = Vector3.up;
    [Tooltip("Variasi RPM antar pump (random factor 0-1) untuk efek 'tidak sinkron'.")]
    [Range(0f, 0.5f)] [SerializeField] private float _variasiRpm = 0.15f;

    public enum FlowMode { Auto, Manual, AlwaysOn }

    private List<Transform> _rotators = new List<Transform>();
    private List<float> _rpmFactors = new List<float>();
    private List<float> _currentAngles = new List<float>();
    private bool _initialized;
    private bool _level3SudahMencapaiSlurry;

    private void Awake()
    {
        Initialize();
    }

    private void OnEnable()
    {
        GameLevelManager.OnLevel3OreReachedSlurry += OnLevel3OreReachedSlurry;
    }

    private void OnDisable()
    {
        GameLevelManager.OnLevel3OreReachedSlurry -= OnLevel3OreReachedSlurry;
    }

    private void OnLevel3OreReachedSlurry()
    {
        _level3SudahMencapaiSlurry = true;
    }

    private void Initialize()
    {
        if (_initialized) return;
        _rotators.Clear();
        _rpmFactors.Clear();
        _currentAngles.Clear();

        foreach (var t in _customRotators)
        {
            if (t != null) AddRotator(t);
        }

        if (_autoFindByName)
        {
            var allTransforms = GetComponentsInChildren<Transform>(true);
            foreach (var t in allTransforms)
            {
                if (t == null) continue;
                string nm = t.name;
                foreach (var key in _keywordNama)
                {
                    if (nm.IndexOf(key, System.StringComparison.OrdinalIgnoreCase) >= 0)
                    {
                        AddRotator(t);
                        break;
                    }
                }
            }
        }

        _initialized = true;
    }

    private void AddRotator(Transform t)
    {
        if (_rotators.Contains(t)) return;
        _rotators.Add(t);
        // Variasi RPM: 1.0 ± _variasiRpm
        float v = Random.Range(1f - _variasiRpm, 1f + _variasiRpm);
        _rpmFactors.Add(v);
        _currentAngles.Add(Random.Range(0f, 360f));
    }

    private void Update()
    {
        if (_rotators.Count == 0) return;

        float flow = GetFlowRate();
        float t = Mathf.Clamp01(flow / Mathf.Max(1f, _flowMaksimumDesain));
        bool aktif = flow >= _flowMinimumAktif;

        if (!aktif && _alwaysOnSetelahLevel3 && _level3SudahMencapaiSlurry)
        {
            t = 0.4f;
            aktif = true;
        }

        if (!aktif) return;

        Vector3 axis = _sumbuRotasi.normalized;
        for (int i = 0; i < _rotators.Count; i++)
        {
            if (_rotators[i] == null) continue;
            float rpm = Mathf.Lerp(0f, _rpmMaksimum * _rpmFactors[i], t);
            float degPerSec = rpm * 6f; // RPM * 360/60
            _currentAngles[i] += degPerSec * Time.deltaTime;
            _rotators[i].localRotation = Quaternion.AngleAxis(_currentAngles[i], axis);
        }
    }

    private float GetFlowRate()
    {
        switch (_mode)
        {
            case FlowMode.Auto:
                return GameLevelManager.Instance != null ? GameLevelManager.Instance.FlowRate : 0f;
            case FlowMode.Manual:
                return _flowManual;
            case FlowMode.AlwaysOn:
                return _flowMaksimumDesain;
            default:
                return 0f;
        }
    }
}
