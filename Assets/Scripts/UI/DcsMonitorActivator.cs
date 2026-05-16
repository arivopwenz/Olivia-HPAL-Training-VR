using UnityEngine;

/// <summary>
/// Menyembunyikan DCS monitor di awal level, lalu menyalakan saat tombol power ditekan.
/// </summary>
public class DcsMonitorActivator : MonoBehaviour
{
    [Header("=== Referensi ===")]
    [SerializeField] private GameObject monitorRoot;

    [Header("=== Trigger Tombol ===")]
    [SerializeField] private int tombolPower = 2;
    [SerializeField] private GameLevelManager.GameLevel requiredLevel = GameLevelManager.GameLevel.Level2_DCSPrep;
    [SerializeField] private bool hideOnLevelStart = true;

    private void Awake()
    {
        if (monitorRoot != null)
            monitorRoot.SetActive(false);
    }

    private void OnEnable()
    {
        GameLevelManager.OnDCSButtonPressed += OnDcsPressed;
        GameLevelManager.OnLevelStarted += OnLevelStarted;
    }

    private void OnDisable()
    {
        GameLevelManager.OnDCSButtonPressed -= OnDcsPressed;
        GameLevelManager.OnLevelStarted -= OnLevelStarted;
    }

    private void OnLevelStarted(GameLevelManager.GameLevel level)
    {
        if (!hideOnLevelStart || monitorRoot == null)
            return;

        if (level == requiredLevel)
            monitorRoot.SetActive(false);
    }

    private void OnDcsPressed(int nomorTombol)
    {
        if (nomorTombol != tombolPower)
            return;

        if (GameLevelManager.Instance != null && GameLevelManager.Instance.CurrentLevel != requiredLevel)
            return;

        if (monitorRoot != null)
            monitorRoot.SetActive(true);
    }
}
