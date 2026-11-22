using System;
using CrystalClash.Ranked.Logic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Perfil de otro jugador (al tocarlo en la tabla de posiciones o en amigos):
/// banner, foto, nombre, ID, rango, récord, insignias y botón para agregarlo.
/// </summary>
public static class PlayerProfilePopup
{
    private static bool loading;

    public static async void Show(string playerId)
    {
        if (string.IsNullOrEmpty(playerId) || loading) return;
        loading = true;
        StoryPopup.Show("Cargando perfil...", "");
        try
        {
            PublicProfile profile = await RankedServices.Current.GetPublicProfileAsync(playerId);
            if (profile == null) throw new Exception("perfil vacío");
            Build(profile);
        }
        catch (Exception e)
        {
            Debug.LogWarning("Perfil: " + e.Message);
            StoryPopup.Show("No se pudo cargar", "Revisa tu conexión e inténtalo otra vez.", new StoryPopup.PopupButton("Cerrar", null));
        }
        finally
        {
            loading = false;
        }
    }

    private static void Build(PublicProfile profile)
    {
        RankTier tier = RankTiers.ForPoints(profile.Points);
        StoryPopup.PopupButton? action = ActionButton(profile);
        var close = new StoryPopup.PopupButton("Cerrar", null);

        StoryPopup.ShowCustom(profile.Name, panel =>
        {
            // Cabecera: banner de fondo, foto, ID y rango
            GameObject header = UiKit.Node(panel, "Cabecera");
            UiKit.Size(header.transform, height: 280f);
            Cosmetics.BannerBackground(header.transform, profile.Banner, 0f, 0.35f);
            HorizontalLayoutGroup row = UiKit.Horizontal(header, 20, 20f);
            row.childForceExpandHeight = false;

            GameObject avatarHolder = UiKit.Node(header.transform, "Foto");
            UiKit.Size(avatarHolder.transform, width: 220f, height: 220f);
            ProfileAvatar.Create(avatarHolder.transform, profile.Avatar, 220f);

            GameObject info = UiKit.Node(header.transform, "Info");
            UiKit.Size(info.transform, flexibleWidth: 1f, height: 220f);
            UiKit.Vertical(info, 0, 4f, TextAnchor.MiddleCenter);
            Image emblem = UiKit.Image(info.transform, "Emblema", Color.white, UiKit.RankEmblem(tier));
            emblem.preserveAspect = true;
            UiKit.Size(emblem, height: 130f);
            TMP_Text rank = UiKit.Text(info.transform, $"{tier.Name}  ·  {profile.Points} pts", 38f, UiKit.LeagueColor(tier.LeagueIndex));
            UiKit.Outline(rank, 0.2f);
            UiKit.Size(rank, height: 48f);
            TMP_Text id = UiKit.Text(info.transform, profile.ShortId > 0 ? $"ID: #{profile.ShortId}" : "", 32f, Color.white);
            UiKit.Outline(id, 0.18f);
            UiKit.Size(id, height: 40f);

            // Récord
            int played = profile.Wins + profile.Losses + profile.Draws;
            string rate = played > 0 ? $"{100f * profile.Wins / played:0}%" : "-";
            TMP_Text record = UiKit.Text(panel,
                $"Partidas <b>{played}</b>   ·   Victorias <b>{profile.Wins}</b>   ·   % victorias <b>{rate}</b>", 34f, Color.white);
            UiKit.Outline(record, 0.15f);
            UiKit.Size(record, height: 60f);

            // Insignias de temporada
            if (profile.Badges != null && profile.Badges.Count > 0)
            {
                GameObject badges = UiKit.Node(panel, "Insignias");
                UiKit.Size(badges.transform, height: 110f);
                HorizontalLayoutGroup badgeRow = UiKit.Horizontal(badges, 0, 8f);
                badgeRow.childForceExpandHeight = false;
                foreach (string badge in profile.Badges)
                {
                    if (!SeasonRules.TryParseBadge(badge, out int season, out int league)) continue;
                    GameObject holder = UiKit.Node(badges.transform, "Insignia");
                    UiKit.Size(holder.transform, width: 100f, height: 100f);
                    RectTransform b = SeasonBadge.Create(holder.transform, season, league, 96f);
                    b.anchorMin = b.anchorMax = new Vector2(0.5f, 0.5f);
                    b.anchoredPosition = Vector2.zero;
                }
            }
        }, action.HasValue ? new[] { action.Value, close } : new[] { close });
    }

    private static StoryPopup.PopupButton? ActionButton(PublicProfile profile)
    {
        switch (profile.FriendStatus)
        {
            case "self": return null;
            case "friend": return new StoryPopup.PopupButton("Ya son amigos", null, false);
            case "sent": return new StoryPopup.PopupButton("Solicitud enviada", null, false);
            case "incoming":
                return new StoryPopup.PopupButton("Aceptar solicitud",
                    () => Act(RankedServices.Current.RespondFriendRequestAsync(profile.PlayerId, true), profile.Name));
            default:
                return new StoryPopup.PopupButton("Agregar amigo",
                    () => Act(RankedServices.Current.SendFriendRequestToPlayerAsync(profile.PlayerId), profile.Name));
        }
    }

    private static async void Act(System.Threading.Tasks.Task<FriendsList> action, string name)
    {
        StoryPopup.Show("Un momento...", "");
        try
        {
            FriendsList result = await action;
            switch (result.Result)
            {
                case "friends":
                    Sound.Play("star");
                    StoryPopup.Show("¡Ahora son amigos!", $"<b>{name}</b> está en tu lista de amigos.", new StoryPopup.PopupButton("Genial", null));
                    break;
                case "sent":
                    Sound.Play("coin");
                    StoryPopup.Show("¡Solicitud enviada!", $"Cuando <b>{name}</b> la acepte, aparecerá en tu lista.", new StoryPopup.PopupButton("Genial", null));
                    break;
                case "already_friends":
                    StoryPopup.Show("Ya son amigos", $"<b>{name}</b> ya está en tu lista.", new StoryPopup.PopupButton("Entendido", null));
                    break;
                case "already_sent":
                    StoryPopup.Show("Solicitud pendiente", $"Ya le enviaste una solicitud a <b>{name}</b>.", new StoryPopup.PopupButton("Entendido", null));
                    break;
                case "full":
                    StoryPopup.Show("Lista llena", "Tú o ese jugador ya tienen el máximo de amigos o solicitudes.", new StoryPopup.PopupButton("Entendido", null));
                    break;
                case "not_found":
                    StoryPopup.Show("No encontrado", "Ese jugador ya no existe.", new StoryPopup.PopupButton("Entendido", null));
                    break;
                default:
                    StoryPopup.Close();
                    break;
            }
        }
        catch (Exception e)
        {
            Debug.LogWarning("Agregar amigo: " + e.Message);
            StoryPopup.Show("No se pudo", "Revisa tu conexión e inténtalo otra vez.", new StoryPopup.PopupButton("Cerrar", null));
        }
    }
}
