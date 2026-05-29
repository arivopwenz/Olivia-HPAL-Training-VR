using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Visual-only helper for the redesigned Level 13 dry-stack rig.
/// Gameplay state remains owned by Level13DryStackController.
/// </summary>
public sealed class Level13DryStackVisualAnimator : MonoBehaviour
{
    [SerializeField] private float _liquidScrollSpeed = 0.42f;
    [SerializeField] private float _beltScrollSpeed = 0.72f;
    [SerializeField] private float _pumpRpm = 55f;
    [SerializeField] private float _dustPulse = 0.08f;

    private readonly List<Renderer> _liquidRenderers = new List<Renderer>();
    private readonly List<Renderer> _beltRenderers = new List<Renderer>();
    private readonly List<Transform> _rotors = new List<Transform>();
    private readonly List<Transform> _dustPuffs = new List<Transform>();
    private readonly List<Vector3> _dustBaseScales = new List<Vector3>();

    private void Awake()
    {
        CollectVisualParts();
    }

    private void Update()
    {
        float dt = Time.deltaTime;
        float spin = _pumpRpm * 6f * dt;
        for (int i = 0; i < _rotors.Count; i++)
        {
            if (_rotors[i] != null)
                _rotors[i].Rotate(Vector3.right, spin, Space.Self);
        }

        float t = Time.timeSinceLevelLoad;
        ScrollMaterials(_liquidRenderers, new Vector2(t * _liquidScrollSpeed, t * _liquidScrollSpeed * 0.35f));
        ScrollMaterials(_beltRenderers, new Vector2(t * _beltScrollSpeed, 0f));

        for (int i = 0; i < _dustPuffs.Count; i++)
        {
            if (_dustPuffs[i] == null)
                continue;

            float pulse = 1f + Mathf.Sin(t * 2.4f + i * 0.73f) * _dustPulse;
            _dustPuffs[i].localScale = _dustBaseScales[i] * pulse;
        }
    }

    private void CollectVisualParts()
    {
        _liquidRenderers.Clear();
        _beltRenderers.Clear();
        _rotors.Clear();
        _dustPuffs.Clear();
        _dustBaseScales.Clear();

        Transform[] children = GetComponentsInChildren<Transform>(true);
        for (int i = 0; i < children.Length; i++)
        {
            Transform tr = children[i];
            string name = tr.name;

            if (name.Contains("Pump_Rotor") || name.Contains("Dosing_Valve_Handwheel") || name.Contains("Isolation_Handwheel") || name.Contains("Tension_Handwheel"))
                _rotors.Add(tr);

            Renderer renderer = tr.GetComponent<Renderer>();
            if (renderer != null)
            {
                if (name.Contains("Flow") || name.Contains("Filtrate") || name.Contains("Slurry_Surface") || name.Contains("Water_In_Sump") || name.Contains("Underdrain"))
                    _liquidRenderers.Add(renderer);

                if (name.Contains("Conveyor_Belt_Surface"))
                    _beltRenderers.Add(renderer);
            }

            if (name.StartsWith("DryStack_Dust_Puff_Mesh_"))
            {
                _dustPuffs.Add(tr);
                _dustBaseScales.Add(tr.localScale);
            }
        }
    }

    private static void ScrollMaterials(List<Renderer> renderers, Vector2 offset)
    {
        for (int i = 0; i < renderers.Count; i++)
        {
            Renderer renderer = renderers[i];
            if (renderer == null)
                continue;

            Material[] materials = renderer.materials;
            for (int m = 0; m < materials.Length; m++)
            {
                Material material = materials[m];
                if (material == null)
                    continue;

                if (material.HasProperty("_BaseMap"))
                    material.SetTextureOffset("_BaseMap", offset);
                if (material.HasProperty("_MainTex"))
                    material.SetTextureOffset("_MainTex", offset);
            }
        }
    }
}