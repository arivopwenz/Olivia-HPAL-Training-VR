using UnityEngine;

[DisallowMultipleComponent]
public sealed class WorldSpaceBillboard : MonoBehaviour
{
    [SerializeField] private bool lockY;
    [SerializeField] private Vector3 eulerOffset;

    private void LateUpdate()
    {
        Camera camera = Camera.main;
        if (camera == null)
            return;

        Vector3 direction = transform.position - camera.transform.position;
        if (lockY)
            direction.y = 0f;
        if (direction.sqrMagnitude < 0.0001f)
            return;

        transform.rotation = Quaternion.LookRotation(direction.normalized, Vector3.up) * Quaternion.Euler(eulerOffset);
    }
}
