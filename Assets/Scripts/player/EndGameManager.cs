using System.Collections;
using TMPro;
using RTS.Player;
using RTS.Units;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public class EndGameManager : MonoBehaviour
{
    [Header("Owners")]
    [SerializeField] private Owner playerOwner = Owner.Player1;
    [SerializeField] private Owner aiOwner = Owner.AI1;

    [Header("UI")]
    [SerializeField] private GameObject endGamePanel;
    [SerializeField] private TextMeshProUGUI resultText;
    [SerializeField] private Button nextMapButton;
    [SerializeField] private Button restartButton;
    [SerializeField] private Button exitButton;

    [Header("Other UI To Disable")]
    [SerializeField] private GameObject[] uiToDisableOnEndGame;

    [Header("References")]
    [SerializeField] private PauseMenuManager pauseMenuManager;
    [SerializeField] private PlayerInput playerInput;

    [Header("Scene Names")]
    [SerializeField] private string mainMenuSceneName = "Main Menu";
    [SerializeField] private string[] campaignScenes = { "map medium", "map small", "map big" };

    private BaseBuilding playerCommandPost;
    private BaseBuilding aiCommandPost;

    private bool gameEnded;
    private bool commandPostsInitialized;

    private IEnumerator Start()
    {
        Time.timeScale = 1f;
        gameEnded = false;
        commandPostsInitialized = false;

        if (playerInput == null)
            playerInput = FindFirstObjectByType<PlayerInput>();

        if (endGamePanel != null)
            endGamePanel.SetActive(false);

        if (nextMapButton != null)
            nextMapButton.onClick.AddListener(LoadNextMap);

        if (restartButton != null)
            restartButton.onClick.AddListener(RestartCurrentMap);

        if (exitButton != null)
            exitButton.onClick.AddListener(ExitToMainMenu);

        yield return StartCoroutine(FallbackFindCommandPostsRoutine());
    }

    private IEnumerator FallbackFindCommandPostsRoutine()
    {
        float timeout = 5f;
        float elapsed = 0f;

        while (!commandPostsInitialized && elapsed < timeout)
        {
            BaseBuilding[] allBuildings = FindObjectsByType<BaseBuilding>(FindObjectsSortMode.None);

            if (playerCommandPost == null)
                playerCommandPost = FindBuildingForOwner(allBuildings, playerOwner);

            if (aiCommandPost == null)
                aiCommandPost = FindBuildingForOwner(allBuildings, aiOwner);

            if (playerCommandPost != null && aiCommandPost != null)
            {
                commandPostsInitialized = true;
                yield break;
            }

            elapsed += 0.2f;
            yield return new WaitForSeconds(0.2f);
        }

        if (!commandPostsInitialized)
            Debug.LogWarning("EndGameManager: Could not find both command posts.");
    }

    private BaseBuilding FindBuildingForOwner(BaseBuilding[] allBuildings, Owner owner)
    {
        for (int i = 0; i < allBuildings.Length; i++)
        {
            BaseBuilding building = allBuildings[i];

            if (building == null)
                continue;

            if (building.Owner != owner)
                continue;

            return building;
        }

        return null;
    }

    public void RegisterCommandPost(BaseBuilding building)
    {
        if (building == null)
            return;

        if (building.Owner == playerOwner)
            playerCommandPost = building;
        else if (building.Owner == aiOwner)
            aiCommandPost = building;

        if (playerCommandPost != null && aiCommandPost != null)
            commandPostsInitialized = true;
    }

    private void Update()
    {
        if (gameEnded || !commandPostsInitialized)
            return;

        if (aiCommandPost == null || !aiCommandPost.gameObject.activeInHierarchy)
        {
            ShowEndGame(true);
            return;
        }

        if (playerCommandPost == null || !playerCommandPost.gameObject.activeInHierarchy)
        {
            ShowEndGame(false);
        }
    }

    private void ShowEndGame(bool playerWon)
    {
        if (gameEnded)
            return;

        if (playerWon)
            UnlockNextLevel();

        gameEnded = true;

        if (playerInput != null)
            playerInput.enabled = false;

        if (pauseMenuManager != null)
            pauseMenuManager.DisablePause();

        DisableOtherUI();

        Time.timeScale = 0f;

        if (endGamePanel != null)
            endGamePanel.SetActive(true);

        if (resultText != null)
        {
            if (playerWon)
            {
                if (!HasNextMap())
                    resultText.text = "CAMPAIGN COMPLETE!\nTHANK YOU FOR PLAYING!";
                else
                    resultText.text = "YOU WON";
            }
            else
            {
                resultText.text = "YOU LOST";
            }
        }

        if (nextMapButton != null)
        {
            bool hasNextMap = playerWon && HasNextMap();
            nextMapButton.gameObject.SetActive(hasNextMap);
            nextMapButton.interactable = hasNextMap;
        }
    }

    private void DisableOtherUI()
    {
        if (uiToDisableOnEndGame == null)
            return;

        for (int i = 0; i < uiToDisableOnEndGame.Length; i++)
        {
            if (uiToDisableOnEndGame[i] != null)
                uiToDisableOnEndGame[i].SetActive(false);
        }
    }

    private bool HasNextMap()
    {
        string currentScene = SceneManager.GetActiveScene().name;
        int currentIndex = System.Array.IndexOf(campaignScenes, currentScene);

        return currentIndex >= 0 && currentIndex < campaignScenes.Length - 1;
    }

    public void LoadNextMap()
    {
        Time.timeScale = 1f;

        string currentScene = SceneManager.GetActiveScene().name;
        int currentIndex = System.Array.IndexOf(campaignScenes, currentScene);

        if (currentIndex >= 0 && currentIndex < campaignScenes.Length - 1)
            SceneManager.LoadScene(campaignScenes[currentIndex + 1]);
        else
            SceneManager.LoadScene(mainMenuSceneName);
    }

    private void UnlockNextLevel()
    {
        string currentScene = SceneManager.GetActiveScene().name;
        int currentIndex = System.Array.IndexOf(campaignScenes, currentScene);

        if (currentIndex < 0)
            return;

        int nextIndex = currentIndex + 1;

        if (nextIndex < campaignScenes.Length)
            CampaignProgress.UnlockLevel(nextIndex);
    }

    public void RestartCurrentMap()
    {
        Time.timeScale = 1f;
        SceneManager.LoadScene(SceneManager.GetActiveScene().name);
    }

    public void ExitToMainMenu()
    {
        Time.timeScale = 1f;
        SceneManager.LoadScene(mainMenuSceneName);
    }

    private void FullRuntimeReset()
    {
        SaveSystem.ResetRuntimeState();
    }
}