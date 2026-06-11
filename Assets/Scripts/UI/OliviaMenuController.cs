using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using UnityEngine.XR;

[DisallowMultipleComponent]
public sealed class OliviaMenuController : MonoBehaviour
{
    [Header("Scene")]
    [SerializeField] private string gameplayScene = "Level1_MainBroken";
    [SerializeField] private string mainMenuScene = "Main Menu";

    [Header("Panels")]
    [SerializeField] private GameObject pauseBackdrop;
    [SerializeField] private GameObject pausePanel;
    [SerializeField] private GameObject settingsPanel;
    [SerializeField] private Slider volumeSlider;
    [SerializeField] private TextMeshProUGUI volumeValueText;

    [Header("Mode")]
    [SerializeField] private bool enablePauseInput;

    private bool _paused;
    private bool _leftMenuWasPressed;
    private bool _rightMenuWasPressed;

    private void Awake()
    {
        Time.timeScale = 1f;
        AudioListener.pause = false;

        float volume = PlayerPrefs.GetFloat("Olivia.MasterVolume", 1f);
        AudioListener.volume = volume;
        if (volumeSlider != null)
        {
            volumeSlider.SetValueWithoutNotify(volume);
            volumeSlider.onValueChanged.AddListener(SetVolume);
        }
        UpdateVolumeText(volume);

        if (pausePanel != null)
            pausePanel.SetActive(false);
        if (pauseBackdrop != null)
            pauseBackdrop.SetActive(false);
        if (settingsPanel != null)
            settingsPanel.SetActive(false);
    }

    private void Update()
    {
        if (!enablePauseInput)
            return;

        bool keyboardPressed = Input.GetKeyDown(KeyCode.Escape) || Input.GetKeyDown(KeyCode.P);
        bool xrPressed = ReadMenuButton(XRNode.LeftHand, ref _leftMenuWasPressed) |
                         ReadMenuButton(XRNode.RightHand, ref _rightMenuWasPressed);
        if (keyboardPressed || xrPressed)
            TogglePause();
    }

    private static bool ReadMenuButton(XRNode node, ref bool wasPressed)
    {
        InputDevice device = InputDevices.GetDeviceAtXRNode(node);
        bool pressed = false;
        device.TryGetFeatureValue(CommonUsages.menuButton, out pressed);
        bool pressedThisFrame = pressed && !wasPressed;
        wasPressed = pressed;
        return pressedThisFrame;
    }

    public void StartGame()
    {
        ResumeTime();
        SceneManager.LoadScene(gameplayScene);
    }

    public void TogglePause()
    {
        if (_paused)
            ResumeGame();
        else
            PauseGame();
    }

    public void PauseGame()
    {
        _paused = true;
        Time.timeScale = 0f;
        AudioListener.pause = true;
        if (pausePanel != null)
            pausePanel.SetActive(true);
        if (pauseBackdrop != null)
            pauseBackdrop.SetActive(true);
        if (settingsPanel != null)
            settingsPanel.SetActive(false);
    }

    public void ResumeGame()
    {
        ResumeTime();
        if (pausePanel != null)
            pausePanel.SetActive(false);
        if (pauseBackdrop != null)
            pauseBackdrop.SetActive(false);
        if (settingsPanel != null)
            settingsPanel.SetActive(false);
    }

    public void OpenSettings()
    {
        if (settingsPanel != null)
            settingsPanel.SetActive(true);
    }

    public void CloseSettings()
    {
        if (settingsPanel != null)
            settingsPanel.SetActive(false);
    }

    public void ReturnToMainMenu()
    {
        ResumeTime();
        SceneManager.LoadScene(mainMenuScene);
    }

    public void QuitGame()
    {
#if UNITY_EDITOR
        UnityEditor.EditorApplication.isPlaying = false;
#else
        Application.Quit();
#endif
    }

    public void SetVolume(float value)
    {
        value = Mathf.Clamp01(value);
        AudioListener.volume = value;
        PlayerPrefs.SetFloat("Olivia.MasterVolume", value);
        PlayerPrefs.Save();
        UpdateVolumeText(value);
    }

    private void UpdateVolumeText(float value)
    {
        if (volumeValueText != null)
            volumeValueText.text = Mathf.RoundToInt(value * 100f) + "%";
    }

    private void ResumeTime()
    {
        _paused = false;
        Time.timeScale = 1f;
        AudioListener.pause = false;
    }

    private void OnDestroy()
    {
        if (volumeSlider != null)
            volumeSlider.onValueChanged.RemoveListener(SetVolume);

        if (_paused)
            ResumeTime();
    }
}
