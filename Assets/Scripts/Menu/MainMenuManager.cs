using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public class MainMenuManager : MonoBehaviour
{
    [Header("Panels")]
    [SerializeField] private GameObject mainPanel;
    [SerializeField] private GameObject settingsPanel;
    [SerializeField] private GameObject levelSelectPanel;

    [Header("Buttons")]
    [SerializeField] private Button continueButton;
    [SerializeField] private Button mediumButton;
    [SerializeField] private Button smallButton;
    [SerializeField] private Button bigButton;

    private readonly string[] campaignScenes = { "map medium", "map small", "map big" };

    private void Start()
    {
        ShowMainPanel();
        RefreshContinueButton();
        RefreshLevelButtons();
    }

    public void StartNewGame()
    {
        SaveSystem.ResetRuntimeState();
        SceneManager.LoadScene(campaignScenes[0]);
    }

    public void ContinueGame()
    {
        if (!SaveSystem.HasSave())
            return;

        SaveSystem.LoadSavedScene();
    }

    public void OpenSettings()
    {
        if (mainPanel != null)
            mainPanel.SetActive(false);

        if (settingsPanel != null)
            settingsPanel.SetActive(true);

        if (levelSelectPanel != null)
            levelSelectPanel.SetActive(false);
    }

    public void BackFromSettings()
    {
        ShowMainPanel();
    }

    public void OpenLevelSelect()
    {
        if (mainPanel != null)
            mainPanel.SetActive(false);

        if (settingsPanel != null)
            settingsPanel.SetActive(false);

        if (levelSelectPanel != null)
            levelSelectPanel.SetActive(true);

        RefreshLevelButtons();
    }

    public void BackFromLevelSelect()
    {
        ShowMainPanel();
    }

    public void LoadMedium()
    {
        LoadLevelIfUnlocked(0);
    }

    public void LoadSmall()
    {
        LoadLevelIfUnlocked(1);
    }

    public void LoadBig()
    {
        LoadLevelIfUnlocked(2);
    }

    private void LoadLevelIfUnlocked(int levelIndex)
    {
        if (!CampaignProgress.IsLevelUnlocked(levelIndex))
            return;

        SaveSystem.ResetRuntimeState();
        SceneManager.LoadScene(campaignScenes[levelIndex]);
    }

    public void ExitGame()
    {
        Application.Quit();

#if UNITY_EDITOR
        UnityEditor.EditorApplication.isPlaying = false;
#endif
    }

    private void ShowMainPanel()
    {
        if (mainPanel != null)
            mainPanel.SetActive(true);

        if (settingsPanel != null)
            settingsPanel.SetActive(false);

        if (levelSelectPanel != null)
            levelSelectPanel.SetActive(false);
    }

    private void RefreshContinueButton()
    {
        if (continueButton != null)
            continueButton.interactable = SaveSystem.HasSave();
    }

    private void RefreshLevelButtons()
    {
        if (mediumButton != null)
            mediumButton.interactable = CampaignProgress.IsLevelUnlocked(0);

        if (smallButton != null)
            smallButton.interactable = CampaignProgress.IsLevelUnlocked(1);

        if (bigButton != null)
            bigButton.interactable = CampaignProgress.IsLevelUnlocked(2);
    }
}