using UnityEngine;
using UnityEngine.UI;

namespace SuperYao
{
    /// <summary>
    /// Fenêtres « Compte » et « Classement » de l'accueil (dans le style des réglages) :
    /// - Compte : pseudo (modifiable), invité ou comptes reliés, boutons Google / Facebook / Apple (ceux disponibles sur
    ///   cet appareil), déconnexion, message d'état ;
    /// - Classement : les 10 premiers et la place du joueur (score = somme des meilleurs scores de chaque stage).
    /// À poser à côté de TitleScreen ; Build() crée les deux fenêtres, cachées.
    /// </summary>
    public class AccountScreen : MonoBehaviour
    {
        static readonly Color Ink = UiKit.Hex("#3A2213"), Gold = UiKit.Hex("#FFD23F"), Green = UiKit.Hex("#2EAA3F");
        static readonly Color Cream = UiKit.Hex("#FFF6E3"), Brown = UiKit.Hex("#5D4037");

        RectTransform account, ranking, rows;
        Text status, message, rankingNote, buttonName;
        InputField nameField;
        Button[] providerButtons;
        Button signOut;
        OnlineAccount online;

        /// <summary>Crée les fenêtres et le bouton « Compte » / « Classement » de l'accueil.</summary>
        public void Build(RectTransform full, RectTransform stage)
        {
            online = OnlineAccount.Instance;
            online.Changed += Refresh;

            // Boutons en haut à gauche : le pseudo (ouvre le compte), le classement.
            var who = UiKit.Button(stage, "Compte", 25, 25, 300, 78, Ink, Gold, 4, 4, 16, () => Open(account));
            buttonName = UiKit.Label((RectTransform)who.transform.Find("Face"), "", 26, Gold);
            var top = UiKit.Button(stage, "Classement", 340, 25, 180, 78, Ink, Gold, 4, 4, 16, OpenRanking);
            UiKit.Label((RectTransform)top.transform.Find("Face"), "Classement", 24, Gold);

            BuildAccount(full);
            BuildRanking(full);
            Refresh();
            _ = online.StartAsync();
        }

        void OnDestroy()
        {
            if (online != null) online.Changed -= Refresh;
        }

        static RectTransform Window(RectTransform full, string name, float w, float h, string title, out RectTransform box)
        {
            var root = UiKit.Stretch(UiKit.Rect(full, name));
            root.gameObject.AddComponent<Image>().color = new Color(0f, 0f, 0f, 0.5f); // voile qui bloque l'accueil
            box = UiKit.Box(root, "Fenetre", 0, 0, w, h, Cream, Ink, 3, 18);
            box.anchorMin = box.anchorMax = box.pivot = new Vector2(0.5f, 0.5f);
            box.anchoredPosition = Vector2.zero;
            UiKit.Label(UiKit.At(UiKit.Rect(box, "Titre"), 0, 16, w, 50), title, 38, Ink);
            root.gameObject.SetActive(false);
            return root;
        }

        void BuildAccount(RectTransform full)
        {
            const float W = 640f;
            account = Window(full, "Fenetre_Compte", W, 640, "Mon compte", out var box);
            status = UiKit.Label(UiKit.At(UiKit.Rect(box, "Etat"), 30, 70, W - 60, 60), "", 22, Brown);

            // Pseudo du classement.
            var field = UiKit.Box(box, "Pseudo", 40, 140, 380, 66, Color.white, Ink, 3, 14);
            field.GetComponent<Image>().raycastTarget = true;
            var text = UiKit.Label(UiKit.Stretch(UiKit.Rect(field, "Zone"), 14), "", 28, Ink, TextAnchor.MiddleLeft);
            text.supportRichText = false;
            nameField = field.gameObject.AddComponent<InputField>();
            nameField.textComponent = text;
            nameField.characterLimit = 30;
            var save = UiKit.Button(box, "Valider", 432, 140, 168, 66, Brown, Ink, 3, 3, 26, () => _ = online.RenameAsync(nameField.text));
            UiKit.Label((RectTransform)save.transform.Find("Face"), "Changer", 24, Color.white);

            // Comptes à relier.
            var colors = new[] { UiKit.Hex("#DB4437"), UiKit.Hex("#1877F2"), UiKit.Hex("#111111") };
            providerButtons = new Button[3];
            for (int i = 0; i < 3; i++)
            {
                var p = (OnlineAccount.Provider)i;
                var b = UiKit.Button(box, $"Relier_{p}", 40, 226 + i * 80, W - 80, 68, colors[i], Ink, 3, 3, 30, () => _ = online.ConnectAsync(p));
                UiKit.Label((RectTransform)b.transform.Find("Face"), "", 26, Color.white);
                providerButtons[i] = b;
            }
            signOut = UiKit.Button(box, "Deconnexion", 40, 470, (W - 100) / 2f, 64, Brown, Ink, 3, 3, 28, () => _ = online.SignOutAsync());
            UiKit.Label((RectTransform)signOut.transform.Find("Face"), "Se déconnecter", 24, Color.white);
            var close = UiKit.Button(box, "Fermer", 60 + (W - 100) / 2f, 470, (W - 100) / 2f, 64, Green, Ink, 3, 3, 28, () => account.gameObject.SetActive(false));
            UiKit.Label((RectTransform)close.transform.Find("Face"), "Fermer", 26, Color.white);
            message = UiKit.Label(UiKit.At(UiKit.Rect(box, "Message"), 30, 548, W - 60, 70), "", 21, UiKit.Hex("#B3261E"));
        }

        void BuildRanking(RectTransform full)
        {
            const float W = 640f;
            ranking = Window(full, "Fenetre_Classement", W, 720, "Classement", out var box);
            rankingNote = UiKit.Label(UiKit.At(UiKit.Rect(box, "Note"), 30, 66, W - 60, 40), "", 20, Brown);
            rows = UiKit.At(UiKit.Rect(box, "Lignes"), 30, 112, W - 60, 500);
            var close = UiKit.Button(box, "Fermer", (W - 260) / 2f, 630, 260, 64, Green, Ink, 3, 3, 28, () => ranking.gameObject.SetActive(false));
            UiKit.Label((RectTransform)close.transform.Find("Face"), "Fermer", 26, Color.white);
        }

        void Open(RectTransform window)
        {
            if (!online.SignedIn && !online.Busy) _ = online.StartAsync(); // nouvel essai de connexion
            window.gameObject.SetActive(true);
            window.SetAsLastSibling();
            Refresh();
        }

        async void OpenRanking()
        {
            Open(ranking);
            for (int i = rows.childCount - 1; i >= 0; i--) Destroy(rows.GetChild(i).gameObject);
            rankingNote.text = "Chargement...";
            var list = await online.TopAsync(10);
            if (this == null || rows == null) return;
            rankingNote.text = list.Count == 0
                ? (online.SignedIn ? "Pas encore de score : gagne un stage !" : "Classement indisponible (pas de connexion).")
                : $"Ton score : {GameProgress.TotalScore:N0} (meilleur score de chaque stage, additionnés)".Replace(',', ' ');
            for (int i = 0; i < list.Count; i++)
            {
                var e = list[i];
                var row = UiKit.Box(rows, $"Ligne_{i}", 0, i * 45, rows.sizeDelta.x, 40, e.me ? Gold : UiKit.Hex("#F3E3C3"), Color.clear, 0, 10);
                UiKit.Label(UiKit.At(UiKit.Rect(row, "Rang"), 10, 0, 70, 40), $"{e.rank}", 22, Ink, TextAnchor.MiddleLeft);
                UiKit.Label(UiKit.At(UiKit.Rect(row, "Nom"), 80, 0, 300, 40), e.name, 22, Ink, TextAnchor.MiddleLeft);
                UiKit.Label(UiKit.At(UiKit.Rect(row, "Score"), 380, 0, rows.sizeDelta.x - 390, 40), e.score.ToString("N0").Replace(',', ' '), 22, Ink, TextAnchor.MiddleRight);
            }
        }

        void Refresh()
        {
            if (buttonName == null) return;
            buttonName.text = online.SignedIn ? online.DisplayName : "Compte";
            if (status == null) return;
            var linked = online.Linked();
            status.text = !online.Available ? "Hors ligne : ta partie est gardée sur ce téléphone."
                : !online.SignedIn ? "Connexion..."
                : linked.Count == 0 ? "Tu joues en invité. Relie un compte pour retrouver ta partie sur un autre téléphone."
                : "Partie sauvegardée en ligne (" + string.Join(", ", linked.ConvertAll(OnlineAccount.Label)) + ").";
            if (!nameField.isFocused) nameField.text = online.SignedIn ? online.DisplayName : "";
            nameField.interactable = online.SignedIn && !online.Busy;
            int shown = 0;
            for (int i = 0; i < providerButtons.Length; i++)
            {
                var p = (OnlineAccount.Provider)i;
                bool can = OnlineAccount.Supports(p);
                providerButtons[i].gameObject.SetActive(can);
                if (!can) continue;
                var rt = (RectTransform)providerButtons[i].transform;
                rt.anchoredPosition = new Vector2(rt.anchoredPosition.x, -(226 + shown++ * 80));
                bool done = linked.Contains(p);
                providerButtons[i].interactable = online.Available && !online.Busy && !done;
                providerButtons[i].GetComponentInChildren<Text>().text = done ? $"{OnlineAccount.Label(p)} : relié" : $"Se connecter avec {OnlineAccount.Label(p)}";
            }
            signOut.interactable = online.SignedIn && !online.IsGuest && !online.Busy;
            message.fontSize = online.Message.Length > 90 ? 16 : 21;
            message.text = shown == 0 && online.Available && string.IsNullOrEmpty(online.Message)
                ? "Les connexions Google, Facebook et Apple ne sont pas encore installées."
                : online.Message;
        }
    }
}
