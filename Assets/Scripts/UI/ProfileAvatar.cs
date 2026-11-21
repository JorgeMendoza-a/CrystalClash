using CrystalClash.Ranked.Logic;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Foto de perfil: "a3" = avatar normal 3, "s1_oro" = insignia de temporada.
/// Se usa en el Perfil y en la pantalla de VS (también para la foto del rival).
/// </summary>
public static class ProfileAvatar
{
    /// <summary>La foto que eligió este jugador (guardada en el dispositivo).</summary>
    public static string LocalId
    {
        get
        {
            var p = SaveManager.Progress;
            return !string.IsNullOrEmpty(p.avatarBadge) ? p.avatarBadge : "a" + p.avatar;
        }
    }

    /// <summary>Dibuja la foto con su marco dorado, centrada en parent.</summary>
    public static RectTransform Create(Transform parent, string avatarId, float size)
    {
        Image frame = UiKit.Image(parent, "Foto", Color.white, UiKit.PanelSprite());
        frame.type = Image.Type.Sliced;
        frame.preserveAspect = false;
        frame.raycastTarget = false;
        RectTransform rect = frame.rectTransform;
        rect.sizeDelta = new Vector2(size, size);
        frame.gameObject.AddComponent<LayoutElement>().preferredHeight = size;

        if (SeasonRules.TryParseBadge(avatarId, out int season, out int league))
        {
            RectTransform badge = SeasonBadge.Create(frame.transform, season, league, size * 0.84f);
            badge.anchorMin = badge.anchorMax = new Vector2(0.5f, 0.5f);
            badge.anchoredPosition = Vector2.zero;
        }
        else
        {
            int index = 0;
            if (!string.IsNullOrEmpty(avatarId) && avatarId[0] == 'a') int.TryParse(avatarId.Substring(1), out index);
            Image image = UiKit.Image(frame.transform, "Imagen", Color.white, ProfileScreen.AvatarSprite(index));
            image.raycastTarget = false;
            UiKit.Stretch(image.rectTransform, size * 0.14f);
        }
        return rect;
    }

    /// <summary>Avisa al servidor la foto actual (para que la vea el rival). No bloquea.</summary>
    public static async void SyncToServer()
    {
        try
        {
            await RankedServices.Current.SetAvatarAsync(LocalId);
        }
        catch (System.Exception e)
        {
            Debug.LogWarning("Foto de perfil: " + e.Message);
        }
    }
}
