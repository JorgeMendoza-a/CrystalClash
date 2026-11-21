using System;
using CrystalClash.Story.Logic;
using UnityEngine;

/// <summary>
/// Vidas por anuncios: cada anuncio visto completo da +1 vida.
/// Máximo AdsPerWindow anuncios cada WindowHours horas (para que no se abuse).
/// </summary>
public static class LifeAds
{
    public const int AdsPerWindow = 5;
    public const int WindowHours = 4;

    private const string CountKey = "crystalclash.ads.lives.count";
    private const string WindowKey = "crystalclash.ads.lives.window";

    private static bool showing;

    /// <summary>Anuncios que todavía puedes ver en este período.</summary>
    public static int Remaining
    {
        get
        {
            int used = WindowOver ? 0 : PlayerPrefs.GetInt(CountKey, 0);
            return Mathf.Max(0, AdsPerWindow - used);
        }
    }

    /// <summary>Segundos hasta que se renueven los anuncios disponibles.</summary>
    public static long SecondsToReset
    {
        get
        {
            long start = long.TryParse(PlayerPrefs.GetString(WindowKey, "0"), out long s) ? s : 0;
            return Math.Max(0, start + WindowHours * 3600L - GameClock.NowUtc);
        }
    }

    /// <summary>Texto para los botones: "Ver anuncio (+1 vida)".</summary>
    public const string ButtonLabel = "Ver anuncio  +1 vida";

    /// <summary>
    /// Muestra un anuncio y, si se ve completo, da una vida. onDone se llama al
    /// final (haya funcionado o no) para refrescar la pantalla.
    /// </summary>
    public static void WatchForLife(Action onDone = null)
    {
        if (showing) return;
        PlayerProgress p = SaveManager.Progress;

        if (p.GetLives(GameClock.NowUtc) >= PlayerProgress.MaxLives)
        {
            StoryPopup.Show("Vidas llenas", "Ya tienes todas tus vidas.", new StoryPopup.PopupButton("Genial", onDone));
            return;
        }
        if (Remaining <= 0)
        {
            TimeSpan wait = TimeSpan.FromSeconds(SecondsToReset);
            StoryPopup.Show("Sin anuncios por ahora",
                $"Ya viste {AdsPerWindow} anuncios.\nPodrás ver más en <b>{(int)wait.TotalHours}h {wait.Minutes}m</b>.",
                new StoryPopup.PopupButton("Entendido", onDone));
            return;
        }
        if (Application.internetReachability == NetworkReachability.NotReachable)
        {
            StoryPopup.Show("Sin internet", "Los anuncios necesitan conexión.", new StoryPopup.PopupButton("Entendido", onDone));
            return;
        }

        IRewardedAdProvider provider = RewardedAds.Provider;
        if (provider == null && Application.isEditor)
        {
            ShowEditorTestAd(onDone);
            return;
        }
        if (provider == null || !provider.IsReady)
        {
            StoryPopup.Show("Anuncio no disponible", "Todavía no hay un anuncio listo.\nInténtalo en unos segundos.",
                new StoryPopup.PopupButton("Entendido", onDone));
            return;
        }

        showing = true;
        StoryPopup.Close();
        provider.Show(success =>
        {
            showing = false;
            if (success) GiveLife(onDone);
            else StoryPopup.Show("Sin premio", "Mira el anuncio completo para ganar la vida.", new StoryPopup.PopupButton("Entendido", onDone));
        });
    }

    private static void GiveLife(Action onDone)
    {
        ResetWindowIfOver();
        PlayerPrefs.SetInt(CountKey, PlayerPrefs.GetInt(CountKey, 0) + 1);
        PlayerPrefs.Save();

        PlayerProgress p = SaveManager.Progress;
        p.AddLife(GameClock.NowUtc);
        SaveManager.Save();
        StoryPopup.Show("¡+1 vida!", $"Ahora tienes <b>{p.GetLives(GameClock.NowUtc)}</b> vidas.",
            new StoryPopup.PopupButton("¡A jugar!", onDone));
    }

    /// <summary>En el editor no hay anuncios reales: se simula uno para probar el flujo.</summary>
    private static async void ShowEditorTestAd(Action onDone)
    {
        showing = true;
        StoryPopup.Show("Anuncio de prueba", "(En el celular aquí sale un anuncio real)");
        await System.Threading.Tasks.Task.Delay(2000);
        showing = false;
        GiveLife(onDone);
    }

    /// <summary>El período empieza con el primer anuncio visto y dura WindowHours.</summary>
    private static bool WindowOver
    {
        get
        {
            long now = GameClock.NowUtc;
            long start = long.TryParse(PlayerPrefs.GetString(WindowKey, "0"), out long s) ? s : 0;
            return start == 0 || now - start >= WindowHours * 3600L || now < start;
        }
    }

    private static void ResetWindowIfOver()
    {
        long now = GameClock.NowUtc;
        if (WindowOver)
        {
            PlayerPrefs.SetString(WindowKey, now.ToString());
            PlayerPrefs.SetInt(CountKey, 0);
            PlayerPrefs.Save();
        }
    }
}
