using CrystalClash.Story.Logic;
using UnityEngine;

/// <summary>Guarda el progreso del modo historia en el dispositivo (PlayerPrefs, como JSON).</summary>
public static class SaveManager
{
    private const string ProgressKey = "crystalclash.progress.v1";

    private static PlayerProgress progress;

    public static PlayerProgress Progress
    {
        get
        {
            if (progress == null) Load();
            return progress;
        }
    }

    public static void Save()
    {
        PlayerPrefs.SetString(ProgressKey, JsonUtility.ToJson(Progress));
        PlayerPrefs.Save();
    }

    /// <summary>Borra todo el progreso (útil para probar).</summary>
    public static void ResetProgress()
    {
        progress = new PlayerProgress();
        PlayerPrefs.DeleteKey(ProgressKey);
        PlayerPrefs.Save();
    }

    private static void Load()
    {
        string json = PlayerPrefs.GetString(ProgressKey, "");
        progress = string.IsNullOrEmpty(json) ? null : JsonUtility.FromJson<PlayerProgress>(json);
        if (progress == null) progress = new PlayerProgress();
    }
}
