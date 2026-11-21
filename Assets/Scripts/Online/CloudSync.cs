using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using CrystalClash.Story.Logic;
using UnityEngine;

/// <summary>
/// Respaldo del progreso en la nube (con el servidor de Cloud Code):
/// - Al abrir el juego baja la copia de la nube y la junta con la del dispositivo.
/// - Unos segundos después de cada guardado sube la copia actual.
/// Sin internet no pasa nada: se juega igual y se sincroniza después.
/// </summary>
public static class CloudSync
{
    private const int UploadDelayMs = 4000;

    [Serializable]
    private class StoryProgressDto
    {
        public bool Exists;
        public string Json;
        public long SavedUtc;
    }

    private static bool started;
    private static int uploadVersion;
    private static bool uploading;

    /// <summary>Llamar una vez al iniciar: sincroniza y luego respalda cada guardado.</summary>
    public static async void StartSync()
    {
        if (started) return;
        started = true;
        SaveManager.Saved += ScheduleUpload;
        CloudSyncRunner.Ensure();

        try
        {
            await UgsRankedService.EnsureSignedInAsync();
            bool changed = await DownloadAndMerge();
            await Upload();
            AccountLink.RefreshStatus();
            if (changed) MenuScreens.Refresh();
        }
        catch (Exception e)
        {
            Debug.Log("Nube: sin sincronizar por ahora (" + e.Message + ")");
        }
    }

    /// <summary>Baja la copia de la nube y la junta con la local. True si cambió algo.</summary>
    public static async Task<bool> DownloadAndMerge()
    {
        StoryProgressDto cloud = await UgsRankedService.CallModule<StoryProgressDto>("LoadProgress");
        if (cloud == null || !cloud.Exists || string.IsNullOrEmpty(cloud.Json)) return false;

        PlayerProgress cloudProgress = JsonUtility.FromJson<PlayerProgress>(cloud.Json);
        if (cloudProgress == null) return false;

        PlayerProgress local = SaveManager.Progress;
        string before = JsonUtility.ToJson(local);
        PlayerProgress merged = PlayerProgress.Merge(local, cloudProgress);
        SaveManager.Replace(merged);
        return JsonUtility.ToJson(merged) != before;
    }

    /// <summary>Reemplaza el progreso del dispositivo por el de la nube (al entrar a otra cuenta).</summary>
    public static async Task<bool> ReplaceWithCloud()
    {
        StoryProgressDto cloud = await UgsRankedService.CallModule<StoryProgressDto>("LoadProgress");
        if (cloud == null || !cloud.Exists || string.IsNullOrEmpty(cloud.Json)) return false;

        PlayerProgress cloudProgress = JsonUtility.FromJson<PlayerProgress>(cloud.Json);
        if (cloudProgress == null) return false;
        SaveManager.Replace(cloudProgress);
        return true;
    }

    public static async Task Upload()
    {
        if (uploading) return;
        uploading = true;
        try
        {
            PlayerProgress p = SaveManager.Progress;
            await UgsRankedService.CallModule<bool>("SaveProgress", new Dictionary<string, object>
            {
                { "json", JsonUtility.ToJson(p) },
                { "savedUtc", p.savedUtc }
            });
        }
        finally
        {
            uploading = false;
        }
    }

    private static async void ScheduleUpload()
    {
        int version = ++uploadVersion;
        await Task.Delay(UploadDelayMs);
        if (version != uploadVersion) return; // hubo otro guardado: esperamos a ese
        await UploadNow();
    }

    /// <summary>Sube ya (p. ej. cuando el juego pasa a segundo plano en el celular).</summary>
    public static async Task UploadNow()
    {
        try
        {
            await UgsRankedService.EnsureSignedInAsync();
            await Upload();
        }
        catch (Exception e)
        {
            Debug.Log("Nube: no se pudo respaldar (" + e.Message + ")");
        }
    }
}

/// <summary>Sube el progreso cuando la app se va a segundo plano o se cierra.</summary>
public class CloudSyncRunner : MonoBehaviour
{
    private static CloudSyncRunner instance;

    public static void Ensure()
    {
        if (instance != null) return;
        var go = new GameObject("CloudSync");
        DontDestroyOnLoad(go);
        instance = go.AddComponent<CloudSyncRunner>();
    }

    private void OnApplicationPause(bool paused)
    {
        if (paused) _ = CloudSync.UploadNow();
    }

    private void OnApplicationQuit()
    {
        _ = CloudSync.UploadNow();
    }
}
