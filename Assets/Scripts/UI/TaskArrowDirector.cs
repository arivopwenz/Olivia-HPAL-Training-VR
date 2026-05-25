using UnityEngine;

[DisallowMultipleComponent]
public sealed class TaskArrowDirector : MonoBehaviour
{
    [SerializeField] private DirectionArrowIndicator arrowIndicator;
    [SerializeField] private string arrowObjectName = "TaskHint_GlobalArrow";

    private int _activeDcsButton = -1;

    private void Awake()
    {
        EnsureArrow();
        Hide();
    }

    private void OnEnable()
    {
        GameLevelManager.OnLevelStarted += OnLevelStarted;
        GameLevelManager.OnDCSButtonShouldHighlight += OnDcsButtonShouldHighlight;
        GameLevelManager.OnDCSButtonPressed += OnDcsButtonPressed;
        GameLevelManager.OnLevelComplete += OnLevelComplete;
    }

    private void OnDisable()
    {
        GameLevelManager.OnLevelStarted -= OnLevelStarted;
        GameLevelManager.OnDCSButtonShouldHighlight -= OnDcsButtonShouldHighlight;
        GameLevelManager.OnDCSButtonPressed -= OnDcsButtonPressed;
        GameLevelManager.OnLevelComplete -= OnLevelComplete;
    }

    private void OnLevelStarted(GameLevelManager.GameLevel level)
    {
        _activeDcsButton = -1;
        Hide();
    }

    private void OnDcsButtonShouldHighlight(int nomorTombol)
    {
        if (GameLevelManager.Instance == null ||
            GameLevelManager.Instance.CurrentLevel <= GameLevelManager.GameLevel.Level1_APD)
        {
            Hide();
            return;
        }

        Transform target = FindDcsButton(nomorTombol);
        if (target == null)
        {
            Hide();
            return;
        }

        _activeDcsButton = nomorTombol;
        EnsureArrow();
        arrowIndicator.Show(target);
    }

    private void OnDcsButtonPressed(int nomorTombol)
    {
        if (nomorTombol == _activeDcsButton)
            Hide();
    }

    private void OnLevelComplete(GameLevelManager.GameLevel level, int skor)
    {
        Hide();
    }

    private void EnsureArrow()
    {
        if (arrowIndicator != null)
            return;

        DirectionArrowIndicator existing = FindFirstObjectByType<DirectionArrowIndicator>(FindObjectsInactive.Include);
        if (existing != null)
        {
            arrowIndicator = existing;
            return;
        }

        GameObject go = new GameObject(arrowObjectName);
        arrowIndicator = go.AddComponent<DirectionArrowIndicator>();
    }

    private Transform FindDcsButton(int nomorTombol)
    {
        foreach (DCSTombolPanel tombol in FindObjectsByType<DCSTombolPanel>(FindObjectsInactive.Include, FindObjectsSortMode.None))
        {
            if (tombol != null && tombol.NomorTombol == nomorTombol)
                return tombol.transform;
        }

        string[] names =
        {
            "Tombol_" + nomorTombol,
            "DCS_Button_" + nomorTombol,
            "Button_DCS_" + nomorTombol,
            "Tombol DCS " + nomorTombol
        };

        foreach (string name in names)
        {
            GameObject go = GameObject.Find(name);
            if (go != null)
                return go.transform;
        }

        return null;
    }

    private void Hide()
    {
        _activeDcsButton = -1;
        if (arrowIndicator != null)
            arrowIndicator.Hide();
    }
}
