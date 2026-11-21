using System;
using System.Threading.Tasks;
using Unity.Services.LevelPlay;
using UnityEngine;

/// <summary>
/// Anuncios con recompensa de Unity LevelPlay. Este archivo solo se compila
/// cuando el paquete "Ads Mediation (LevelPlay)" está instalado (ver el .asmdef).
/// </summary>
public class LevelPlayRewardedProvider : IRewardedAdProvider
{
    private LevelPlayRewardedAd ad;
    private bool loaded;
    private Action<bool> pending;
    private bool rewarded;

    public bool IsReady => ad != null && loaded && ad.IsAdReady();
    public string Status { get; private set; } = "sin iniciar";

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void Register()
    {
        if (string.IsNullOrEmpty(AdsConfig.AndroidAppKey) || string.IsNullOrEmpty(AdsConfig.AndroidRewardedAdUnitId))
        {
            Debug.Log("Anuncios: faltan las claves de LevelPlay en AdsConfig.");
            return;
        }
        if (Application.isEditor) return; // en el editor se usa el anuncio de prueba del juego

        var provider = new LevelPlayRewardedProvider();
        RewardedAds.Provider = provider;
        provider.Init();
    }

    private void Init()
    {
        LevelPlay.OnInitSuccess += _ =>
        {
            Status = "iniciado";
            CreateAd();
        };
        LevelPlay.OnInitFailed += error =>
        {
            Status = "no se pudo iniciar: " + error;
            Debug.LogWarning("Anuncios: " + Status);
        };
        Status = "iniciando";
        LevelPlay.Init(AdsConfig.AndroidAppKey);
    }

    private void CreateAd()
    {
        ad = new LevelPlayRewardedAd(AdsConfig.AndroidRewardedAdUnitId);
        ad.OnAdLoaded += _ =>
        {
            loaded = true;
            Status = "listo";
        };
        ad.OnAdLoadFailed += error =>
        {
            loaded = false;
            Status = "no se pudo cargar: " + error;
            Debug.Log("Anuncios: no se cargó (" + error + "), se reintenta.");
            RetryLoad();
        };
        ad.OnAdRewarded += (_, __) =>
        {
            rewarded = true;
            Finish(true);
        };
        ad.OnAdClosed += _ =>
        {
            loaded = false;
            ad.LoadAd();
            FinishAfterClose();
        };
        Status = "cargando";
        ad.LoadAd();
    }

    public void Show(Action<bool> done)
    {
        if (!IsReady)
        {
            done?.Invoke(false);
            return;
        }
        pending = done;
        rewarded = false;
        ad.ShowAd();
    }

    private void Finish(bool success)
    {
        Action<bool> callback = pending;
        pending = null;
        callback?.Invoke(success);
    }

    /// <summary>A veces el premio llega justo después de cerrar: esperamos un poco antes de decir que no.</summary>
    private async void FinishAfterClose()
    {
        await Task.Delay(1500);
        if (pending != null) Finish(rewarded);
    }

    private async void RetryLoad()
    {
        await Task.Delay(30000);
        if (ad != null && !loaded) ad.LoadAd();
    }
}
