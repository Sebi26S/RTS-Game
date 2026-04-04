using System.IO;
using UnityEngine;
using UnityEngine.SceneManagement;

public static class SaveSystem
{
    private static readonly string SavePath = Path.Combine(Application.persistentDataPath, "savegame.json");

    public static bool ShouldLoadOnSceneStart { get; set; }
    public static bool IsLoadingFromSave { get; set; }

    public static void Save(SaveData data)
    {
        string json = JsonUtility.ToJson(data, true);
        Debug.Log(json);
        File.WriteAllText(SavePath, json);
        Debug.Log("Save elmentve ide: " + SavePath);
    }

    public static SaveData Load()
    {
        if (!File.Exists(SavePath))
        {
            Debug.Log("Nincs save fájl.");
            return null;
        }

        string json = File.ReadAllText(SavePath);
        return JsonUtility.FromJson<SaveData>(json);
    }

    public static bool HasSave()
    {
        return File.Exists(SavePath);
    }

    public static void DeleteSave()
    {
        if (File.Exists(SavePath))
        {
            File.Delete(SavePath);
            Debug.Log("Save törölve.");
        }
    }

    public static void LoadSavedScene()
    {
        SaveData data = Load();
        if (data == null)
            return;

        IsLoadingFromSave = true;
        ShouldLoadOnSceneStart = true;
        SceneManager.LoadScene(data.sceneName);
    }
    
    public static void ResetRuntimeState()
    {
        ShouldLoadOnSceneStart = false;
        IsLoadingFromSave = false;
        DepletedResourceRegistry.Clear();
        RTS.Player.Supplies.ResetAll();
    }
}