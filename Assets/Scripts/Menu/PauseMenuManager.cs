using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.InputSystem;

public class PauseMenuManager : MonoBehaviour
{
    [Header("UI")]
    [SerializeField] private GameObject pausePanel;
    [SerializeField] private GameObject settingsPanel;

    [Header("Optional References")]
    [SerializeField] private DisplaySettingsTMP displaySettingsTMP;

    [Header("Scene Names")]
    [SerializeField] private string mainMenuSceneName = "Main Menu";

    [Header("References")]
    [SerializeField] private GameSaveManager gameSaveManager;

    private bool isPaused;
    private bool canPause = true;
    private bool isInSettings;

    private void Start()
    {
        ResumeGame();
    }

    private void Update()
    {
        if (!canPause)
            return;

        if (Keyboard.current != null && Keyboard.current.escapeKey.wasPressedThisFrame)
        {
            if (isPaused)
            {
                if (isInSettings)
                    BackFromSettings();
                else
                    ResumeGame();
            }
            else
            {
                PauseGame();
            }
        }
    }

    public void PauseGame()
    {
        if (!canPause)
            return;

        isPaused = true;
        isInSettings = false;

        if (pausePanel != null)
            pausePanel.SetActive(true);

        if (settingsPanel != null)
            settingsPanel.SetActive(false);

        Time.timeScale = 0f;
    }

    public void ResumeGame()
    {
        isPaused = false;
        isInSettings = false;

        if (pausePanel != null)
            pausePanel.SetActive(false);

        if (settingsPanel != null)
            settingsPanel.SetActive(false);

        Time.timeScale = 1f;
    }

    public void OpenSettings()
    {
        if (!isPaused)
            return;

        isInSettings = true;

        if (pausePanel != null)
            pausePanel.SetActive(false);

        if (settingsPanel != null)
            settingsPanel.SetActive(true);

        if (displaySettingsTMP != null)
            displaySettingsTMP.RefreshFromCurrentSettings();
    }

    public void BackFromSettings()
    {
        if (!isPaused)
            return;

        isInSettings = false;

        if (pausePanel != null)
            pausePanel.SetActive(true);

        if (settingsPanel != null)
            settingsPanel.SetActive(false);
    }

    public void SaveGame()
    {
        if (gameSaveManager == null)
        {
            Debug.LogError("PauseMenuManager: gameSaveManager reference is NULL.");
            return;
        }

        Debug.Log("PauseMenuManager: Save gomb megnyomva.");
        gameSaveManager.SaveGame();
    }

    public void LoadLastSave()
    {
        if (!SaveSystem.HasSave())
        {
            Debug.Log("Nincs mentés.");
            return;
        }

        Time.timeScale = 1f;
        isPaused = false;
        isInSettings = false;

        SaveSystem.ResetRuntimeState();
        SaveSystem.LoadSavedScene();
    }

    public void ExitToMainMenu()
    {
        Time.timeScale = 1f;
        isPaused = false;
        isInSettings = false;
        SceneManager.LoadScene(mainMenuSceneName);
    }

    public void DisablePause()
    {
        canPause = false;
        isPaused = false;
        isInSettings = false;

        if (pausePanel != null)
            pausePanel.SetActive(false);

        if (settingsPanel != null)
            settingsPanel.SetActive(false);

        Time.timeScale = 1f;
    }

    public void EnablePause()
    {
        canPause = true;
    }

    
}