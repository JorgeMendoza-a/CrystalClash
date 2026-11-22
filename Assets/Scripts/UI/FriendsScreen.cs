using System;
using System.Threading.Tasks;
using CrystalClash.Ranked.Logic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Amigos: tu ID para compartir, agregar por ID, solicitudes recibidas
/// (aceptar / rechazar), enviadas (cancelar) y la lista de amigos con su
/// foto, banner, rango y puntos. Se abre desde el Perfil.
/// </summary>
public class FriendsScreen : MonoBehaviour
{
    private static readonly Color RowColor = new Color(0.14f, 0.13f, 0.3f, 0.95f);

    private IRankedService service;
    private Transform content;
    private Transform list;
    private TMP_Text myIdText;
    private int myShortId;
    private bool busy;

    /// <summary>Reemplaza el contenido de la pantalla (Perfil) por la de amigos.</summary>
    public static void Open(Transform content)
    {
        for (int i = content.childCount - 1; i >= 0; i--) Destroy(content.GetChild(i).gameObject);
        var screen = content.gameObject.AddComponent<FriendsScreen>();
        screen.service = RankedServices.Current;
        screen.content = content;
        screen.Build();
        screen.Load();
    }

    // ------------------------------------------------------------------
    // Construcción
    // ------------------------------------------------------------------

    private void Build()
    {
        GameObject column = UiKit.Node(content, "Amigos");
        UiKit.Stretch((RectTransform)column.transform);
        UiKit.Vertical(column, 0, 16f);

        // Encabezado: volver, título, agregar
        GameObject header = UiKit.Node(column.transform, "Encabezado");
        UiKit.Size(header.transform, height: 130f);
        HorizontalLayoutGroup row = UiKit.Horizontal(header, 0, 16f);
        row.childForceExpandHeight = false;
        UiKit.Size(UiKit.Button(header.transform, "<", UiKit.ButtonStyle.Gray, () => MenuScreens.Show(MenuScreens.Screen.Profile), 60f),
            width: 120f, height: 120f);
        TMP_Text title = UiKit.Title(header.transform, "AMIGOS", 86f);
        UiKit.Size(title, flexibleWidth: 1f, height: 110f);
        UiKit.Size(UiKit.Button(header.transform, "+", UiKit.ButtonStyle.Green, AskForId, 70f), width: 120f, height: 120f);

        // Tu ID (para que te agreguen)
        Image idCard = UiKit.Card(column.transform, "TuId");
        UiKit.Size(idCard, height: 150f);
        HorizontalLayoutGroup idRow = UiKit.Horizontal(idCard.gameObject, 30, 16f);
        idRow.childForceExpandHeight = false;
        myIdText = UiKit.Text(idCard.transform, "Tu ID: ...", 44f, Color.white, FontStyles.Normal, TextAlignmentOptions.Left);
        UiKit.Outline(myIdText, 0.18f);
        UiKit.Size(myIdText, flexibleWidth: 1f, height: 90f);
        UiKit.Size(UiKit.Button(idCard.transform, "COPIAR", UiKit.ButtonStyle.Blue, CopyMyId, 36f), width: 230f, height: 96f);
        UiKit.Size(UiKit.Button(idCard.transform, "AGREGAR", UiKit.ButtonStyle.Green, AskForId, 36f), width: 260f, height: 96f);

        GameObject scrollHolder = UiKit.Node(column.transform, "Lista");
        scrollHolder.AddComponent<LayoutElement>().flexibleHeight = 1f;
        list = UiKit.ScrollArea(scrollHolder.transform, 14f);

        Message("Cargando...");
    }

    // ------------------------------------------------------------------
    // Datos
    // ------------------------------------------------------------------

    private async void Load()
    {
        try
        {
            Task<RankedProfile> profileTask = service.GetProfileAsync();
            FriendsList friends = await service.GetFriendsAsync();
            RankedProfile me = await profileTask;
            if (this == null) return;
            myShortId = me.ShortId;
            myIdText.text = me.ShortId > 0 ? $"Tu ID: <color=#FFD040>#{me.ShortId}</color>" : "Tu ID: ...";
            Show(friends);
        }
        catch (Exception e)
        {
            if (this == null) return;
            Debug.LogWarning("Amigos: " + e.Message);
            Clear();
            Message("No se pudo cargar.\nRevisa tu internet.");
        }
    }

    private void Show(FriendsList friends)
    {
        Clear();

        if (friends.Incoming.Count > 0)
        {
            Section($"SOLICITUDES ({friends.Incoming.Count})");
            foreach (FriendInfo f in friends.Incoming) IncomingRow(f);
        }

        Section($"MIS AMIGOS ({friends.Friends.Count})");
        if (friends.Friends.Count == 0)
        {
            Message("Todavía no tienes amigos.\nToca <b>AGREGAR</b> y escribe el ID de tu amigo\n(lo ve en su Perfil).");
        }
        foreach (FriendInfo f in friends.Friends) FriendRow(f);

        if (friends.Outgoing.Count > 0)
        {
            Section($"ENVIADAS ({friends.Outgoing.Count})");
            foreach (FriendInfo f in friends.Outgoing) OutgoingRow(f);
        }
    }

    // ------------------------------------------------------------------
    // Filas
    // ------------------------------------------------------------------

    private Transform Row(FriendInfo friend, out Transform buttons)
    {
        Image row = UiKit.Image(list, "Fila " + friend.Name, RowColor, UiKit.LoadSliced("UI/row", 24, 160f));
        row.type = Image.Type.Sliced;
        row.preserveAspect = false;
        UiKit.Size(row, height: 170f);
        Cosmetics.BannerBackground(row.transform, friend.Banner, 6f, 0.5f);

        // Tocar la fila abre su perfil.
        var open = row.gameObject.AddComponent<Button>();
        open.targetGraphic = row;
        Sound.AddClick(open);
        string playerId = friend.PlayerId;
        open.onClick.AddListener(() => PlayerProfilePopup.Show(playerId));

        HorizontalLayoutGroup layout = UiKit.Horizontal(row.gameObject, 18, 18f);
        layout.padding = new RectOffset(24, 24, 14, 14);
        layout.childForceExpandHeight = false;

        GameObject avatarHolder = UiKit.Node(row.transform, "Foto");
        UiKit.Size(avatarHolder.transform, width: 130f, height: 130f);
        ProfileAvatar.Create(avatarHolder.transform, friend.Avatar, 130f);

        RankTier tier = RankTiers.ForPoints(friend.Points);
        GameObject texts = UiKit.Node(row.transform, "Textos");
        UiKit.Size(texts.transform, flexibleWidth: 1f, height: 130f);
        UiKit.Vertical(texts, 0, 0f, TextAnchor.MiddleLeft);
        TMP_Text name = UiKit.Text(texts.transform, friend.Name, 44f, Color.white, FontStyles.Normal, TextAlignmentOptions.Left);
        UiKit.Outline(name, 0.2f);
        UiKit.Size(name, height: 58f);
        string color = ColorUtility.ToHtmlStringRGB(UiKit.LeagueColor(tier.LeagueIndex));
        TMP_Text info = UiKit.Text(texts.transform, $"#{friend.ShortId}  ·  <color=#{color}>{tier.Name}</color>  ·  {friend.Points} pts",
            30f, UiKit.Muted, FontStyles.Normal, TextAlignmentOptions.Left);
        UiKit.Outline(info, 0.15f);
        UiKit.Size(info, height: 44f);

        GameObject buttonHolder = UiKit.Node(row.transform, "Botones");
        HorizontalLayoutGroup buttonRow = UiKit.Horizontal(buttonHolder, 0, 10f);
        buttonRow.childForceExpandHeight = false;
        buttons = buttonHolder.transform;
        return row.transform;
    }

    private void FriendRow(FriendInfo friend)
    {
        Row(friend, out Transform buttons);
        RankTier tier = RankTiers.ForPoints(friend.Points);
        Image emblem = UiKit.Image(buttons, "Emblema", Color.white, UiKit.RankEmblem(tier));
        emblem.preserveAspect = true;
        UiKit.Size(emblem, width: 110f, height: 110f);
        UiKit.Size(UiKit.Button(buttons, "X", UiKit.ButtonStyle.Red, () => ConfirmRemove(friend), 40f), width: 90f, height: 90f);
        UiKit.Size(buttons.GetComponent<HorizontalLayoutGroup>(), width: 210f, height: 120f);
    }

    private void IncomingRow(FriendInfo friend)
    {
        Row(friend, out Transform buttons);
        UiKit.Size(UiKit.Button(buttons, "ACEPTAR", UiKit.ButtonStyle.Green, () => Act(service.RespondFriendRequestAsync(friend.PlayerId, true)), 30f),
            width: 210f, height: 96f);
        UiKit.Size(UiKit.Button(buttons, "X", UiKit.ButtonStyle.Red, () => Act(service.RespondFriendRequestAsync(friend.PlayerId, false)), 40f),
            width: 90f, height: 96f);
        UiKit.Size(buttons.GetComponent<HorizontalLayoutGroup>(), width: 310f, height: 120f);
    }

    private void OutgoingRow(FriendInfo friend)
    {
        Row(friend, out Transform buttons);
        TMP_Text waiting = UiKit.Text(buttons, "Pendiente", 30f, UiKit.Muted);
        UiKit.Size(waiting, width: 150f, height: 90f);
        UiKit.Size(UiKit.Button(buttons, "X", UiKit.ButtonStyle.Red, () => Act(service.RemoveFriendAsync(friend.PlayerId)), 40f),
            width: 90f, height: 90f);
        UiKit.Size(buttons.GetComponent<HorizontalLayoutGroup>(), width: 250f, height: 120f);
    }

    // ------------------------------------------------------------------
    // Acciones
    // ------------------------------------------------------------------

    private void AskForId()
    {
        StoryPopup.ShowInput("Agregar amigo", "Escribe el ID de tu amigo.\n<size=80%>Lo encuentra en su Perfil, debajo de su nombre.</size>",
            "", 10, "Enviar", text =>
            {
                string digits = "";
                foreach (char c in text ?? "") if (char.IsDigit(c)) digits += c;
                if (!int.TryParse(digits, out int shortId) || shortId <= 0)
                {
                    StoryPopup.Show("ID inválido", "Escribe solo el número, por ejemplo <b>100002</b>.", new StoryPopup.PopupButton("Entendido", AskForId));
                    return;
                }
                if (shortId == myShortId)
                {
                    StoryPopup.Show("Ese eres tú", "Escribe el ID de tu amigo, no el tuyo.", new StoryPopup.PopupButton("Entendido", null));
                    return;
                }
                Act(service.SendFriendRequestAsync(shortId), shortId);
            });
    }

    private void ConfirmRemove(FriendInfo friend)
    {
        StoryPopup.Show("¿Quitar amigo?", $"<b>{friend.Name}</b> dejará de estar en tu lista de amigos.",
            StoryPopup.PopupButton.Danger("Quitar", () => Act(service.RemoveFriendAsync(friend.PlayerId))),
            new StoryPopup.PopupButton("Cancelar", null));
    }

    /// <summary>Hace una acción en el servidor, muestra el resultado y refresca la lista.</summary>
    private async void Act(Task<FriendsList> action, int shortId = 0)
    {
        if (busy) return;
        busy = true;
        StoryPopup.Show("Un momento...", "");
        try
        {
            FriendsList result = await action;
            if (this == null) return;
            StoryPopup.Close();
            Show(result);
            ShowResultMessage(result, shortId);
        }
        catch (Exception e)
        {
            Debug.LogWarning("Amigos: " + e.Message);
            if (this != null)
            {
                StoryPopup.Show("No se pudo", "Revisa tu conexión e inténtalo otra vez.", new StoryPopup.PopupButton("Cerrar", null));
            }
        }
        finally
        {
            busy = false;
        }
    }

    private static void ShowResultMessage(FriendsList result, int shortId)
    {
        string name = $"#{shortId}";
        FriendInfo found = result.Outgoing.Find(f => f.ShortId == shortId) ?? result.Friends.Find(f => f.ShortId == shortId);
        if (found != null) name = found.Name;

        switch (result.Result)
        {
            case "sent":
                Sound.Play("coin");
                StoryPopup.Show("¡Solicitud enviada!", $"Cuando <b>{name}</b> la acepte, aparecerá en tu lista.", new StoryPopup.PopupButton("Genial", null));
                break;
            case "friends":
                Sound.Play("star");
                StoryPopup.Show("¡Ahora son amigos!", $"<b>{name}</b> está en tu lista de amigos.", new StoryPopup.PopupButton("Genial", null));
                break;
            case "already_friends":
                StoryPopup.Show("Ya son amigos", $"<b>{name}</b> ya está en tu lista.", new StoryPopup.PopupButton("Entendido", null));
                break;
            case "already_sent":
                StoryPopup.Show("Solicitud pendiente", $"Ya le enviaste una solicitud a <b>{name}</b>.", new StoryPopup.PopupButton("Entendido", null));
                break;
            case "not_found":
                StoryPopup.Show("No encontrado", $"No existe ningún jugador con el ID <b>#{shortId}</b>.", new StoryPopup.PopupButton("Entendido", null));
                break;
            case "self":
                StoryPopup.Show("Ese eres tú", "Escribe el ID de tu amigo, no el tuyo.", new StoryPopup.PopupButton("Entendido", null));
                break;
            case "full":
                StoryPopup.Show("Lista llena", "Tú o tu amigo ya tienen el máximo de amigos o solicitudes.", new StoryPopup.PopupButton("Entendido", null));
                break;
        }
    }

    private void CopyMyId()
    {
        if (myShortId <= 0) return;
        GUIUtility.systemCopyBuffer = myShortId.ToString();
        StoryPopup.Show("¡Copiado!", $"Tu ID <b>#{myShortId}</b> está copiado.\nMándaselo a tu amigo para que te agregue.",
            new StoryPopup.PopupButton("Genial", null));
    }

    // ------------------------------------------------------------------

    private void Section(string text)
    {
        TMP_Text title = UiKit.Title(list, text, 52f);
        UiKit.Size(title, height: 80f);
    }

    private void Clear()
    {
        for (int i = list.childCount - 1; i >= 0; i--) Destroy(list.GetChild(i).gameObject);
    }

    private void Message(string text)
    {
        TMP_Text message = UiKit.Text(list, text, 36f, Color.white);
        UiKit.Outline(message, 0.15f);
        UiKit.Size(message, height: 180f);
    }
}
