using System;
using Unity.Services.Authentication;
using Unity.Services.Authentication.PlayerAccounts;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// Vincular la cuenta del juego con Google (o Apple / correo) usando
/// Unity Player Accounts. Así el progreso y la clasificatoria se recuperan
/// en otro celular o al reinstalar.
/// </summary>
public static class AccountLink
{
    private const string LinkedKey = "crystalclash.account.linked";

    /// <summary>True si la cuenta ya está vinculada (se recuerda para mostrarlo sin internet).</summary>
    public static bool IsLinked => PlayerPrefs.GetInt(LinkedKey, 0) == 1;

    private static bool linking;

    /// <summary>Consulta al servidor si la cuenta tiene un inicio de sesión vinculado.</summary>
    public static async void RefreshStatus()
    {
        try
        {
            await UgsRankedService.EnsureSignedInAsync();
            PlayerInfo info = await AuthenticationService.Instance.GetPlayerInfoAsync();
            SetLinked(info != null && !string.IsNullOrEmpty(info.GetUnityId()));
        }
        catch (Exception e)
        {
            Debug.Log("Cuenta: estado no disponible (" + e.Message + ")");
        }
    }

    /// <summary>Botón "Vincular con Google": abre el inicio de sesión de Unity (Google, Apple o correo).</summary>
    public static async void StartLink()
    {
        if (linking) return;
        linking = true;
        try
        {
            await UgsRankedService.EnsureSignedInAsync();

            PlayerAccountService.Instance.SignedIn -= OnPlayerAccountSignedIn;
            PlayerAccountService.Instance.SignedIn += OnPlayerAccountSignedIn;

            if (PlayerAccountService.Instance.IsSignedIn)
            {
                OnPlayerAccountSignedIn();
                return;
            }

            StoryPopup.Show("Vincular cuenta", "Se abrirá la página de inicio de sesión.\nElige <b>Continuar con Google</b> y vuelve al juego.",
                new StoryPopup.PopupButton("Cerrar", null));
            await PlayerAccountService.Instance.StartSignInAsync();
            linking = false;
        }
        catch (Exception e)
        {
            linking = false;
            Debug.LogWarning("Vincular cuenta: " + e.Message);
            StoryPopup.Show("No se pudo vincular", "Revisa tu conexión e inténtalo otra vez.", new StoryPopup.PopupButton("Cerrar", null));
        }
    }

    private static async void OnPlayerAccountSignedIn()
    {
        PlayerAccountService.Instance.SignedIn -= OnPlayerAccountSignedIn;
        string token = PlayerAccountService.Instance.AccessToken;

        try
        {
            await AuthenticationService.Instance.LinkWithUnityAsync(token);
            SetLinked(true);
            await CloudSync.UploadNow();
            StoryPopup.Show("¡Cuenta vinculada!",
                "Tu progreso y tu clasificatoria quedan guardados en la nube.\nEn otro celular, toca <b>Vincular con Google</b> con la misma cuenta.",
                new StoryPopup.PopupButton("Genial", MenuScreens.Refresh));
        }
        catch (AuthenticationException e) when (e.ErrorCode == AuthenticationErrorCodes.AccountAlreadyLinked)
        {
            // Esa cuenta de Google ya tiene una partida guardada (p. ej. de otro celular).
            StoryPopup.Show("Ya tienes una partida guardada",
                "Esa cuenta ya tiene progreso en la nube.\n¿Cargarla en este dispositivo?\n\n<size=80%>El progreso actual de este dispositivo se reemplaza.</size>",
                new StoryPopup.PopupButton("Cargar partida", () => SwitchToAccount(token)),
                new StoryPopup.PopupButton("Cancelar", null));
        }
        catch (Exception e)
        {
            Debug.LogWarning("Vincular cuenta: " + e.Message);
            StoryPopup.Show("No se pudo vincular", "Inténtalo otra vez en un momento.", new StoryPopup.PopupButton("Cerrar", null));
        }
        finally
        {
            linking = false;
        }
    }

    /// <summary>Cambia este dispositivo a la cuenta vinculada y carga su progreso.</summary>
    private static async void SwitchToAccount(string token)
    {
        StoryPopup.Show("Cargando tu partida...", "");
        try
        {
            AuthenticationService.Instance.SignOut(true);
            UgsRankedService.ResetSession();
            await AuthenticationService.Instance.SignInWithUnityAsync(token);
            SetLinked(true);
            await CloudSync.ReplaceWithCloud();

            // Volver a empezar la escena con el progreso cargado.
            RankedMatchState.Current = null;
            AppNavigation.PendingScreen = AppNavigation.Screen.Home;
            StoryPopup.Close();
            SceneManager.LoadScene(SceneManager.GetActiveScene().name);
        }
        catch (Exception e)
        {
            Debug.LogWarning("Cambiar de cuenta: " + e.Message);
            StoryPopup.Show("No se pudo cargar la partida", "Inténtalo otra vez.", new StoryPopup.PopupButton("Cerrar", null));
        }
    }

    private static void SetLinked(bool linked)
    {
        PlayerPrefs.SetInt(LinkedKey, linked ? 1 : 0);
        PlayerPrefs.Save();
    }
}
