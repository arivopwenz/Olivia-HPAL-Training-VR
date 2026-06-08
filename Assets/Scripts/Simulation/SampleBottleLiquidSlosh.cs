using UnityEngine;

/// <summary>
/// Lightweight visual slosh for grabbed sample bottles.
/// The liquid tilts opposite acceleration and wobbles back to level, enough to
/// communicate "real liquid" without needing expensive fluid simulation.
/// </summary>
[DisallowMultipleComponent]
public class SampleBottleLiquidSlosh : MonoBehaviour
{
    [SerializeField] private Transform _liquidRoot;
    [SerializeField] private float _maxTiltDegrees = 14f;
    [SerializeField] private float _velocityGain = 8f;
    [SerializeField] private float _returnSpeed = 7f;
    [SerializeField] private float _surfacePulse = 0.035f;

    private Vector3 _lastPosition;
    private Vector3 _smoothedLocalVelocity;
    private Quaternion _baseLocalRotation;
    private Vector3 _baseLocalScale;
    private bool _initialized;

    public void Setup(Transform liquidRoot)
    {
        _liquidRoot = liquidRoot;
        CacheBasePose();
    }

    private void Awake()
    {
        if (_liquidRoot == null)
        {
            Transform direct = transform.Find("Liquid") ?? transform.Find("BottleLiquid");
            if (direct != null) _liquidRoot = direct;
        }
        CacheBasePose();
    }

    private void CacheBasePose()
    {
        if (_liquidRoot == null) return;
        _lastPosition = transform.position;
        _baseLocalRotation = _liquidRoot.localRotation;
        _baseLocalScale = _liquidRoot.localScale;
        _initialized = true;
    }

    private void LateUpdate()
    {
        if (_liquidRoot == null)
            return;
        if (!_initialized)
            CacheBasePose();

        float dt = Mathf.Max(Time.deltaTime, 0.0001f);
        Vector3 velocity = (transform.position - _lastPosition) / dt;
        _lastPosition = transform.position;

        Vector3 localVelocity = transform.InverseTransformDirection(velocity);
        _smoothedLocalVelocity = Vector3.Lerp(_smoothedLocalVelocity, localVelocity, 1f - Mathf.Exp(-dt * _returnSpeed));

        float tiltX = Mathf.Clamp(_smoothedLocalVelocity.z * _velocityGain, -_maxTiltDegrees, _maxTiltDegrees);
        float tiltZ = Mathf.Clamp(-_smoothedLocalVelocity.x * _velocityGain, -_maxTiltDegrees, _maxTiltDegrees);
        float wobble = Mathf.Sin(Time.time * 9.5f) * Mathf.Clamp(_smoothedLocalVelocity.magnitude * 0.18f, 0f, 1f);

        _liquidRoot.localRotation = _baseLocalRotation * Quaternion.Euler(tiltX + wobble, 0f, tiltZ - wobble);

        float pulse = 1f + Mathf.Sin(Time.time * 7f) * _surfacePulse * Mathf.Clamp01(_smoothedLocalVelocity.magnitude * 0.25f);
        Vector3 currentScale = _liquidRoot.localScale;
        _liquidRoot.localScale = new Vector3(_baseLocalScale.x * pulse, currentScale.y, _baseLocalScale.z / Mathf.Max(0.85f, pulse));
    }
}
