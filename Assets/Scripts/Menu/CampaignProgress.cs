using UnityEngine;

public static class CampaignProgress
{
    private const string HighestUnlockedLevelKey = "HighestUnlockedLevel";



    public static int GetHighestUnlockedLevel()
    {
        return PlayerPrefs.GetInt(HighestUnlockedLevelKey, 0);
    }

    public static void UnlockLevel(int levelIndex)
    {
        int currentHighest = GetHighestUnlockedLevel();

        if (levelIndex > currentHighest)
        {
            PlayerPrefs.SetInt(HighestUnlockedLevelKey, levelIndex);
            PlayerPrefs.Save();
        }
    }

    public static bool IsLevelUnlocked(int levelIndex)
    {
        return levelIndex <= GetHighestUnlockedLevel();
    }

    public static void ResetProgress()
    {
        PlayerPrefs.SetInt(HighestUnlockedLevelKey, 0);
        PlayerPrefs.Save();
    }
}