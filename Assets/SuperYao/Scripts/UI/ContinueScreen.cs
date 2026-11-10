using UnityEngine;
using UnityEngine.UI;

namespace SuperYao
{
    /// <summary>
    /// Fin de partie dans un stage : « Plus de vies ! Regarde une vidéo pour continuer. » Vidéo regardée jusqu'au bout :
    /// une vie, Yao repart du dernier point de contrôle (une seule fois par stage). « Abandonner » : fin de partie comme
    /// avant. Ajouté par StageState ; s'affiche tant que StageState.OfferingContinue est vrai.
    /// </summary>
    public class ContinueScreen : MonoBehaviour
    {
        static readonly Color Ink = UiKit.Hex("#3A2213"), Green = UiKit.Hex("#2EAA3F"), Brown = UiKit.Hex("#5D4037");

        RectTransform root;
        Text note;
        Button watch, quit;
        bool waiting;

        void Update()
        {
            var state = StageState.Instance;
            bool show = state != null && state.OfferingContinue;
            if (show && root == null) Build();
            if (root != null && root.gameObject.activeSelf != show) root.gameObject.SetActive(show);
        }

        void Build()
        {
            var pause = FindAnyObjectByType<PauseScreen>();
            if (pause != null && pause.font != null) UiKit.Font = pause.font;
            var (full, s) = UiKit.Screen(transform, "Continuer", 80);
            root = full;
            var veil = UiKit.Stretch(UiKit.Rect(full, "Voile"));
            veil.gameObject.AddComponent<Image>().color = new Color(0f, 0f, 0f, 0.6f);
            veil.SetAsFirstSibling();
            var box = UiKit.Box(s, "Fenetre", 1672 / 2f - 330, 230, 660, 440, UiKit.Hex("#FFF6E3"), Ink, 4, 22);
            UiKit.Label(UiKit.At(UiKit.Rect(box, "Titre"), 0, 24, 660, 60), "Plus de vies !", 46, UiKit.Hex("#E8343A"));
            UiKit.Label(UiKit.At(UiKit.Rect(box, "Texte"), 40, 96, 580, 90),
                "Regarde une courte vidéo pour gagner une vie et repartir du dernier drapeau.", 26, Ink);
            watch = UiKit.Button(box, "Regarder", 60, 200, 540, 90, Green, Ink, 4, 8, 40, Watch);
            UiKit.Label((RectTransform)watch.transform.Find("Face"), "Regarder la vidéo  (+1 vie)", 30, Color.white, TextAnchor.MiddleCenter, new Color(0, 0, 0, 0.4f));
            quit = UiKit.Button(box, "Abandonner", 160, 306, 340, 70, Brown, Ink, 3, 4, 30, () => StageState.Instance?.DeclineContinue());
            UiKit.Label((RectTransform)quit.transform.Find("Face"), "Abandonner", 26, Color.white);
            note = UiKit.Label(UiKit.At(UiKit.Rect(box, "Note"), 20, 384, 620, 44), "", 21, UiKit.Hex("#B3261E"));
        }

        void Watch()
        {
            if (waiting) return;
            waiting = true;
            watch.interactable = quit.interactable = false;
            note.text = "";
            RewardedVideo.Instance.Show(ok =>
            {
                waiting = false;
                if (watch != null) watch.interactable = quit.interactable = true;
                if (ok) StageState.Instance?.AcceptContinue();
                else if (note != null) note.text = "Vidéo pas terminée ou indisponible : réessaie, ou abandonne.";
            });
        }
    }
}
