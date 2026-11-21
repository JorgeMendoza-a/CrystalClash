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

    /// <summary>Se dispara cada vez que se guarda (para respaldar en la nube).</summary>
    public static event System.Action Saved;

    public static void Save()
    {
        Progress.savedUtc = System.DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        WriteLocal();
        Saved?.Invoke();
    }

    /// <summary>Reemplaza el progreso (p. ej. con el de la nube) y lo guarda en el dispositivo.</summary>
    public static void Replace(PlayerProgress newProgress)
    {
        if (newProgress == null) return;
        progress = newProgress;
        WriteLocal();
    }

    private static void WriteLocal()
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

        if (progress.createdUtc == 0)
        {
            progress.createdUtc = System.DateTimeOffset.UtcNow.ToUnixTimeSeconds();
            Save();
        }
    }
}
