using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Small local machine animator for the Level 11 purification/MHP rig.
/// It reads active process-liquid objects from Level11MHPController and adds
/// continuous mechanical motion without owning quest logic.
/// </summary>
public class Level11PurificationRigAnimator : MonoBehaviour
{
    [SerializeField] private Transform[] _rotators;
    [SerializeField] private Transform[] _pulseObjects;
    [SerializeField] private GameObject[] _processLiquids;
    [SerializeField] private float _handwheelDegreesPerSecond = 150f;
    [SerializeField] private float _screwDegreesPerSecond = 260f;
    [SerializeField] private float _flowPulseAmount = 0.075f;

    private Vector3[] _basePulseScales;

    private void Awake()
    {
        AutoFindReferences();
        CacheBaseScales();
    }

    private void Update()
    {
        bool processRunning = AnyActive(_processLiquids);
        if (!processRunning)
            return;

        RotateMechanics();
        PulseActiveLiquids();
    }

    private void AutoFindReferences()
    {
        if (_rotators == null || _rotators.Length == 0)
        {
            List<Transform> rotators = new List<Transform>();
            AddIfFound(rotators, "Reagent_Dosing_Handwheel");
            AddIfFound(rotators, "MGO_Dosing_Handwheel");
            AddIfFound(rotators, "MGO_Screw_Feeder_Root");
            AddIfFound(rotators, "MHP_Sample_Valve_Handwheel");
            _rotators = rotators.ToArray();
        }

        if (_processLiquids == null || _processLiquids.Length == 0)
        {
            List<GameObject> liquids = new List<GameObject>();
            AddIfFound(liquids, "Feed_From_CCD_Liquid");
            AddIfFound(liquids, "Reagent_Liquid_Line");
            AddIfFound(liquids, "Neutralization_To_Polishing_Liquid");
            AddIfFound(liquids, "Polishing_To_MHP_Liquid");
            AddIfFound(liquids, "MHP_Sample_Flow");
            AddIfFound(liquids, "MHP_Sample_Product");
            _processLiquids = liquids.ToArray();
        }

        if (_pulseObjects == null || _pulseObjects.Length == 0)
        {
            List<Transform> pulses = new List<Transform>();
            AddIfFound(pulses, "Feed_From_CCD_Liquid");
            AddIfFound(pulses, "Reagent_Liquid_Line");
            AddIfFound(pulses, "Neutralization_To_Polishing_Liquid");
            AddIfFound(pulses, "Polishing_To_MHP_Liquid");
            AddIfFound(pulses, "MHP_Sample_Flow");
            AddIfFound(pulses, "MHP_Sample_Product");
            _pulseObjects = pulses.ToArray();
        }
    }

    private void CacheBaseScales()
    {
        if (_pulseObjects == null)
            return;

        _basePulseScales = new Vector3[_pulseObjects.Length];
        for (int i = 0; i < _pulseObjects.Length; i++)
            _basePulseScales[i] = _pulseObjects[i] != null ? _pulseObjects[i].localScale : Vector3.one;
    }

    private void RotateMechanics()
    {
        if (_rotators == null)
            return;

        for (int i = 0; i < _rotators.Length; i++)
        {
            Transform rotator = _rotators[i];
            if (rotator == null)
                continue;

            bool screw = rotator.name.Contains("Screw");
            Vector3 axis = screw ? Vector3.right : Vector3.up;
            float speed = screw ? _screwDegreesPerSecond : _handwheelDegreesPerSecond;
            rotator.Rotate(axis, speed * Time.deltaTime, Space.Self);
        }
    }

    private void PulseActiveLiquids()
    {
        if (_pulseObjects == null || _basePulseScales == null)
            return;

        float pulse = 1f + Mathf.Sin(Time.time * 5.5f) * _flowPulseAmount;
        for (int i = 0; i < _pulseObjects.Length; i++)
        {
            Transform item = _pulseObjects[i];
            if (item == null || !item.gameObject.activeInHierarchy)
                continue;

            Vector3 scale = _basePulseScales[i];
            scale.z *= pulse;
            item.localScale = scale;
        }
    }

    private bool AnyActive(GameObject[] objects)
    {
        if (objects == null)
            return false;

        for (int i = 0; i < objects.Length; i++)
        {
            if (objects[i] != null && objects[i].activeInHierarchy)
                return true;
        }

        return false;
    }

    private void AddIfFound(List<Transform> list, string name)
    {
        Transform child = FindDeepChild(transform, name);
        if (child != null)
            list.Add(child);
    }

    private void AddIfFound(List<GameObject> list, string name)
    {
        Transform child = FindDeepChild(transform, name);
        if (child != null)
            list.Add(child.gameObject);
    }

    private Transform FindDeepChild(Transform root, string name)
    {
        foreach (Transform child in root)
        {
            if (child.name == name || child.name.StartsWith(name + "."))
                return child;

            Transform nested = FindDeepChild(child, name);
            if (nested != null)
                return nested;
        }

        return null;
    }
}
