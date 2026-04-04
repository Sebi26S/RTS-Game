using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class DisplaySettingsTMP : MonoBehaviour
{
    [Header("UI")]
    [SerializeField] private TMP_Dropdown resolutionDropdown;
    [SerializeField] private Toggle fullscreenToggle;
    [SerializeField] private Button applyButton;

    private readonly List<Vector2Int> resolutions = new()
    {
        new Vector2Int(1280, 720),
        new Vector2Int(1366, 768),
        new Vector2Int(1600, 900),
        new Vector2Int(1920, 1080),
        new Vector2Int(2560, 1440)
    };

    private int selectedResolutionIndex;
    private bool selectedFullscreen;

    private const string ResolutionIndexKey = "ResolutionIndex";
    private const string FullscreenKey = "Fullscreen";

    private void Start()
    {
        SetupResolutionDropdown();
        LoadSavedSettings();
        UpdateUI();

        if (fullscreenToggle != null)
            fullscreenToggle.onValueChanged.AddListener(OnFullscreenChanged);

        if (applyButton != null)
            applyButton.onClick.AddListener(ApplySettings);
    }

    private void SetupResolutionDropdown()
    {
        if (resolutionDropdown == null)
            return;

        resolutionDropdown.ClearOptions();

        List<string> options = new();
        for (int i = 0; i < resolutions.Count; i++)
        {
            options.Add($"{resolutions[i].x} x {resolutions[i].y}");
        }

        resolutionDropdown.AddOptions(options);
        resolutionDropdown.onValueChanged.AddListener(OnResolutionChanged);
    }

    private void LoadSavedSettings()
    {
        int defaultIndex = GetClosestCurrentResolutionIndex();

        selectedResolutionIndex = PlayerPrefs.GetInt(ResolutionIndexKey, defaultIndex);
        selectedResolutionIndex = Mathf.Clamp(selectedResolutionIndex, 0, resolutions.Count - 1);

        selectedFullscreen = PlayerPrefs.GetInt(FullscreenKey, 1) == 1;
    }

    private int GetClosestCurrentResolutionIndex()
    {
        int currentWidth = Screen.width;
        int currentHeight = Screen.height;

        int bestIndex = 0;
        int bestScore = int.MaxValue;

        for (int i = 0; i < resolutions.Count; i++)
        {
            int score = Mathf.Abs(resolutions[i].x - currentWidth) + Mathf.Abs(resolutions[i].y - currentHeight);
            if (score < bestScore)
            {
                bestScore = score;
                bestIndex = i;
            }
        }

        return bestIndex;
    }

    private void UpdateUI()
    {
        if (resolutionDropdown != null)
        {
            resolutionDropdown.SetValueWithoutNotify(selectedResolutionIndex);
            resolutionDropdown.RefreshShownValue();
        }

        if (fullscreenToggle != null)
        {
            fullscreenToggle.SetIsOnWithoutNotify(selectedFullscreen);
        }
    }

    public void OnResolutionChanged(int index)
    {
        selectedResolutionIndex = Mathf.Clamp(index, 0, resolutions.Count - 1);
        Debug.Log($"Selected resolution index: {selectedResolutionIndex}");
    }

    public void OnFullscreenChanged(bool isFullscreen)
    {
        selectedFullscreen = isFullscreen;
        Debug.Log($"Selected fullscreen: {selectedFullscreen}");
    }

    public void ApplySettings()
    {
        if (selectedResolutionIndex < 0 || selectedResolutionIndex >= resolutions.Count)
            return;

        Vector2Int res = resolutions[selectedResolutionIndex];

        FullScreenMode mode = selectedFullscreen
            ? FullScreenMode.FullScreenWindow
            : FullScreenMode.Windowed;

        Screen.SetResolution(res.x, res.y, mode);

        PlayerPrefs.SetInt(ResolutionIndexKey, selectedResolutionIndex);
        PlayerPrefs.SetInt(FullscreenKey, selectedFullscreen ? 1 : 0);
        PlayerPrefs.Save();

        Debug.Log($"Applied: {res.x}x{res.y}, mode: {mode}");
        Debug.Log($"Screen after apply: {Screen.width}x{Screen.height}");
    }

    public void RefreshFromCurrentSettings()
    {
        LoadSavedSettings();
        UpdateUI();
    }
}