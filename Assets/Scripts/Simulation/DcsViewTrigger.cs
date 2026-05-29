using UnityEngine;

/// <summary>
/// Trigger sederhana untuk menandai pemain sudah melihat area DCS.
/// Pakai OnTriggerStay supaya tetap fire kalau player ditelepor masuk
/// langsung ke dalam trigger zone (tidak melewati OnTriggerEnter).
/// </summary>
public class DcsViewTrigger : MonoBehaviour
{
    [Tooltip("Selang cek minimum supaya tidak panggil tiap frame.")]
    [SerializeField] private float _intervalCek = 0.2f;

    [Tooltip("Print debug log saat trigger fire (bantu diagnose).")]
    [SerializeField] private bool _debugLog = false;

    private float _waktuCekTerakhir = -999f;

    private void OnTriggerEnter(Collider other) => Cek(other, "Enter");
    private void OnTriggerStay(Collider other) => Cek(other, "Stay");

    private void Cek(Collider other, string source)
    {
        if (Time.time - _waktuCekTerakhir < _intervalCek) return;

        bool playerTag = other.CompareTag("Player");
        bool hasCamera = other.GetComponentInChildren<Camera>() != null;
        if (_debugLog && (playerTag || hasCamera))
            Debug.Log($"[DcsViewTrigger.{source}] other='{other.name}' tag={other.tag} playerTag={playerTag} hasCamera={hasCamera}");

        if (!playerTag && !hasCamera) return;

        _waktuCekTerakhir = Time.time;
        GameLevelManager.Instance?.NotifyDcsViewed();
    }
}
