using UnityEngine;

public class IndustrialOreWaterAnimator : MonoBehaviour
{
    [Header("Ore conveyor")]
    public Renderer[] beltRenderers;
    public Transform[] rollers;
    public Transform[] oreChunks;
    public Transform oreStart;
    public Transform oreMid;
    public Transform oreEnd;
    public float oreSpeed = 0.12f;
    public float rollerSpeed = 260f;

    [Header("Water line")]
    public Renderer[] waterRenderers;
    public ParticleSystem[] waterParticles;
    public Transform[] waterPulseObjects;
    public float waterScrollSpeed = 0.85f;
    public float waterPulseAmplitude = 0.025f;
    public float waterPulseFrequency = 1.8f;

    private float _beltOffset;
    private float _waterOffset;
    private Vector3[] _waterBaseScales;

    private void Awake()
    {
        CacheWaterScales();
        PlayParticles(true);
    }

    private void OnEnable()
    {
        CacheWaterScales();
        PlayParticles(true);
    }

    private void OnDisable()
    {
        PlayParticles(false);
    }

    private void Update()
    {
        float dt = Time.deltaTime;
        AnimateBelt(dt);
        AnimateRollers(dt);
        AnimateOreChunks(dt);
        AnimateWater(dt);
        KeepActiveParticlesPlaying();
    }

    private void CacheWaterScales()
    {
        if (waterPulseObjects == null)
            return;

        _waterBaseScales = new Vector3[waterPulseObjects.Length];
        for (int i = 0; i < waterPulseObjects.Length; i++)
            _waterBaseScales[i] = waterPulseObjects[i] != null ? waterPulseObjects[i].localScale : Vector3.one;
    }

    private void AnimateBelt(float dt)
    {
        if (beltRenderers == null)
            return;

        _beltOffset = Mathf.Repeat(_beltOffset + dt * oreSpeed * 2.4f, 1f);
        for (int i = 0; i < beltRenderers.Length; i++)
            ApplyTextureOffset(beltRenderers[i], new Vector2(0f, -_beltOffset));
    }

    private void AnimateRollers(float dt)
    {
        if (rollers == null)
            return;

        float angle = rollerSpeed * dt;
        for (int i = 0; i < rollers.Length; i++)
        {
            if (rollers[i] != null)
                rollers[i].Rotate(Vector3.right, angle, Space.Self);
        }
    }

    private void AnimateOreChunks(float dt)
    {
        if (oreChunks == null || oreStart == null || oreMid == null || oreEnd == null)
            return;

        for (int i = 0; i < oreChunks.Length; i++)
        {
            Transform chunk = oreChunks[i];
            if (chunk == null)
                continue;

            float t = Mathf.Repeat(Time.time * oreSpeed + i / Mathf.Max(1f, oreChunks.Length), 1f);
            chunk.position = Bezier(oreStart.position, oreMid.position, oreEnd.position, t);
            chunk.Rotate(new Vector3(37f, 49f, 23f), dt * 65f, Space.Self);
        }
    }

    private void AnimateWater(float dt)
    {
        if (waterRenderers != null)
        {
            _waterOffset = Mathf.Repeat(_waterOffset + dt * waterScrollSpeed, 1f);
            for (int i = 0; i < waterRenderers.Length; i++)
                ApplyTextureOffset(waterRenderers[i], new Vector2(0f, -_waterOffset));
        }

        if (waterPulseObjects == null || _waterBaseScales == null)
            return;

        float pulse = 1f + Mathf.Sin(Time.time * waterPulseFrequency * Mathf.PI * 2f) * waterPulseAmplitude;
        for (int i = 0; i < waterPulseObjects.Length; i++)
        {
            Transform t = waterPulseObjects[i];
            if (t == null)
                continue;

            Vector3 baseScale = i < _waterBaseScales.Length ? _waterBaseScales[i] : t.localScale;
            t.localScale = new Vector3(baseScale.x * pulse, baseScale.y, baseScale.z * pulse);
        }
    }

    private void ApplyTextureOffset(Renderer renderer, Vector2 offset)
    {
        if (renderer == null)
            return;

        Material mat = renderer.material;
        if (mat == null)
            return;

        if (mat.HasProperty("_BaseMap"))
            mat.SetTextureOffset("_BaseMap", offset);
        if (mat.HasProperty("_MainTex"))
            mat.SetTextureOffset("_MainTex", offset);
    }

    private void PlayParticles(bool play)
    {
        if (waterParticles == null)
            return;

        for (int i = 0; i < waterParticles.Length; i++)
        {
            ParticleSystem ps = waterParticles[i];
            if (ps == null)
                continue;

            if (play) ps.Play(true);
            else ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
        }
    }

    private void KeepActiveParticlesPlaying()
    {
        if (waterParticles == null)
            return;

        for (int i = 0; i < waterParticles.Length; i++)
        {
            ParticleSystem ps = waterParticles[i];
            if (ps != null && ps.gameObject.activeInHierarchy && !ps.isPlaying)
                ps.Play(true);
        }
    }

    private Vector3 Bezier(Vector3 a, Vector3 b, Vector3 c, float t)
    {
        float u = 1f - t;
        return u * u * a + 2f * u * t * b + t * t * c;
    }
}
