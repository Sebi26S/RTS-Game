using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class ResolutionSettings : MonoBehaviour
{
    [Header("UI")]
    [SerializeField] private TMP_Dropdown resolutionDropdown;
    [SerializeField] private Toggle fullscreenToggle;

    private Resolution[] availableResolutions;
    private List<Resolution> filteredResolutions = new();

    private const string ResolutionIndexKey = "ResolutionIndex";
    private const string FullscreenKey = "Fullscreen";

    private void Start()
    {
        SetupResolutions();
        SetupFullscreen();
    }

    private void SetupResolutions()
    {
        availableResolutions = Screen.resolutions;
        filteredResolutions.Clear();

        resolutionDropdown.ClearOptions();

        List<string> options = new List<string>();
        HashSet<string> added = new HashSet<string>();

        int currentResolutionIndex = 0;

        for (int i = 0; i < availableResolutions.Length; i++)
        {
            Resolution res = availableResolutions[i];

            string option = $"{res.width} x {res.height} @ {res.refreshRateRatio.value:F0}Hz";

            string uniqueKey = $"{res.width}x{res.height}";

            if (added.Contains(uniqueKey))
                continue;

            added.Add(uniqueKey);
            filteredResolutions.Add(res);
            options.Add($"{res.width} x {res.height}");

            if (res.width == Screen.currentResolution.width &&
                res.height == Screen.currentResolution.height)
            {
                currentResolutionIndex = filteredResolutions.Count - 1;
            }
        }

        resolutionDropdown.AddOptions(options);

        int savedIndex = PlayerPrefs.GetInt(ResolutionIndexKey, currentResolutionIndex);
        savedIndex = Mathf.Clamp(savedIndex, 0, filteredResolutions.Count - 1);

        resolutionDropdown.value = savedIndex;
        resolutionDropdown.RefreshShownValue();

        ApplyResolution(savedIndex);
    }

    private void SetupFullscreen()
    {
        bool savedFullscreen = PlayerPrefs.GetInt(FullscreenKey, Screen.fullScreen ? 1 : 0) == 1;

        fullscreenToggle.isOn = savedFullscreen;
        Screen.fullScreen = savedFullscreen;
    }

    public void OnResolutionChanged(int resolutionIndex)
    {
        ApplyResolution(resolutionIndex);
        PlayerPrefs.SetInt(ResolutionIndexKey, resolutionIndex);
        PlayerPrefs.Save();
    }

    public void OnFullscreenChanged(bool isFullscreen)
    {
        Screen.fullScreen = isFullscreen;
        PlayerPrefs.SetInt(FullscreenKey, isFullscreen ? 1 : 0);
        PlayerPrefs.Save();
    }

    private void ApplyResolution(int resolutionIndex)
    {
        if (resolutionIndex < 0 || resolutionIndex >= filteredResolutions.Count)
            return;

        Resolution resolution = filteredResolutions[resolutionIndex];
        Screen.SetResolution(resolution.width, resolution.height, Screen.fullScreen);
    }
}