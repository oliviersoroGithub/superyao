using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using UnityEngine;
#if SUPERYAO_UGS
using Unity.Services.Authentication;
using Unity.Services.CloudSave;
using Unity.Services.Core;
using Unity.Services.Leaderboards;
#endif

namespace SuperYao
{
    /// <summary>
    /// Compte du joueur en ligne (Unity Gaming Services) : connexion, sauvegarde en ligne et classement.
    /// - Au lancement, le joueur est connecté tout seul : son compte de la dernière fois, ou un compte « invité » créé sur
    ///   l'appareil (il joue tout de suite, sans rien saisir).
    /// - Il peut ensuite relier son compte Google (Google Play Jeux), Facebook ou Apple : sa progression le suit sur un
    ///   autre téléphone. Si ce compte Google/Facebook/Apple a déjà une partie, on bascule dessus et on garde la partie
    ///   la plus avancée des deux.
    /// - Chaque fois que la partie est enregistrée (stage gagné, partie perdue), elle est envoyée en ligne, avec le score
    ///   du classement (somme des meilleurs scores de chaque stage).
    /// Tout est facultatif : sans Internet, ou tant que les services ne sont pas installés (SUPERYAO_UGS), le jeu marche
    /// comme avant, sur l'appareil seulement. Les connexions Google, Facebook et Apple demandent en plus leurs SDK
    /// (SUPERYAO_GPGS, SUPERYAO_FACEBOOK, SUPERYAO_APPLE, posés tout seuls par l'éditeur quand ils sont installés).
    /// </summary>
    public class OnlineAccount : MonoBehaviour
    {
        /// <summary>Identifiant du classement, à créer dans le tableau de bord Unity Cloud (Leaderboards).</summary>
        public const string LeaderboardId = "score_total";
        const string SaveKey = "partie";

        public enum Provider { Google, Facebook, Apple }

        public struct Entry
        {
            public int rank;
            public string name;
            public long score;
            public bool me;
        }

        static OnlineAccount instance;
        public static bool HasInstance => instance != null;
        /// <summary>Le compte (créé à la première demande, gardé d'une scène à l'autre).</summary>
        public static OnlineAccount Instance
        {
            get
            {
                if (instance == null)
                {
                    instance = new GameObject("Compte_En_Ligne").AddComponent<OnlineAccount>();
                    DontDestroyOnLoad(instance.gameObject);
                }
                return instance;
            }
        }

        /// <summary>L'état a changé (connexion, nom, message) : les écrans se mettent à jour.</summary>
        public event Action Changed;
        /// <summary>La partie de l'appareil vient d'être remplacée par celle du compte : l'écran se recharge.</summary>
        public event Action ProgressReplaced;

        public bool Busy { get; private set; }
        /// <summary>Dernier message à montrer au joueur (erreur, confirmation).</summary>
        public string Message { get; private set; } = "";
        Task starting;

#if SUPERYAO_UGS
        public bool Available { get; private set; }
        public bool SignedIn => Available && AuthenticationService.Instance.IsSignedIn;
        /// <summary>Connecté sans compte Google, Facebook ou Apple relié.</summary>
        public bool IsGuest => SignedIn && Linked().Count == 0;
        /// <summary>Pseudo affiché (sans le « #1234 » que les services ajoutent).</summary>
        public string DisplayName
        {
            get
            {
                if (!SignedIn) return "Invité";
                var n = AuthenticationService.Instance.PlayerName;
                if (string.IsNullOrEmpty(n)) return "Joueur";
                int hash = n.LastIndexOf('#');
                return hash > 0 ? n.Substring(0, hash) : n;
            }
        }

        /// <summary>Comptes reliés (Google, Facebook, Apple).</summary>
        public List<Provider> Linked()
        {
            var list = new List<Provider>();
            if (!SignedIn || AuthenticationService.Instance.PlayerInfo == null) return list;
            foreach (var id in AuthenticationService.Instance.PlayerInfo.Identities)
            {
                if (id.TypeId == "google-play-games" || id.TypeId == "google.com") list.Add(Provider.Google);
                else if (id.TypeId == "facebook.com") list.Add(Provider.Facebook);
                else if (id.TypeId == "apple.com") list.Add(Provider.Apple);
            }
            return list;
        }
#else
        public bool Available => false;
        public bool SignedIn => false;
        public bool IsGuest => true;
        public string DisplayName => "Invité";
        public List<Provider> Linked() => new List<Provider>();
#endif

        /// <summary>Ce fournisseur peut être utilisé ici (son SDK est installé, et Apple seulement sur iPhone).</summary>
        public static bool Supports(Provider p)
        {
            switch (p)
            {
#if SUPERYAO_GPGS && UNITY_ANDROID
                case Provider.Google: return true;
#endif
#if SUPERYAO_FACEBOOK
                case Provider.Facebook: return true;
#endif
#if SUPERYAO_APPLE && UNITY_IOS
                case Provider.Apple: return true;
#endif
                default: return false;
            }
        }

        public static string Label(Provider p) => p == Provider.Google ? "Google" : p == Provider.Facebook ? "Facebook" : "Apple";

        void Awake()
        {
            GameProgress.Saved += OnSaved;
        }

        void OnDestroy()
        {
            GameProgress.Saved -= OnSaved;
        }

        void OnSaved() => _ = PushAsync();

        void Say(string message)
        {
            Message = message ?? "";
            Changed?.Invoke();
        }

        /// <summary>Connexion au lancement (une seule fois) : compte de la dernière fois, ou nouvel invité.</summary>
        public Task StartAsync() => starting ??= StartInternal();

        async Task StartInternal()
        {
#if SUPERYAO_UGS
            try
            {
                Busy = true;
                if (UnityServices.State != ServicesInitializationState.Initialized) await UnityServices.InitializeAsync();
                Available = true;
                // Sans compte enregistré, un compte invité est créé ; sinon on retrouve celui de la dernière fois (même s'il
                // est relié à Google, Facebook ou Apple).
                if (!AuthenticationService.Instance.IsSignedIn) await AuthenticationService.Instance.SignInAnonymouslyAsync();
                await AuthenticationService.Instance.GetPlayerNameAsync();
                await SyncAsync();
                Busy = false;
                Say("");
            }
            catch (Exception e)
            {
                Busy = false;
                Debug.LogWarning("Super Yao : compte en ligne indisponible. Le jeu continue sur l'appareil.\n" + e);
                Say("Compte en ligne indisponible : " + Reason(e));
                starting = null; // on réessaiera à la prochaine ouverture de la fenêtre
            }
#else
            await Task.CompletedTask;
#endif
        }

        /// <summary>Relie un compte Google, Facebook ou Apple (ou bascule dessus s'il a déjà une partie).</summary>
        public async Task ConnectAsync(Provider provider)
        {
#if SUPERYAO_UGS
            if (Busy) return;
            await StartAsync();
            if (!Available) { Say("Pas de connexion à Internet."); return; }
            Busy = true;
            Say($"Connexion à {Label(provider)}...");
            try
            {
                string token = await ProviderToken(provider);
                if (string.IsNullOrEmpty(token)) { Busy = false; Say("Connexion annulée."); return; }
                try
                {
                    // Le compte actuel (invité ou non) garde sa partie : on y ajoute ce fournisseur.
                    await Link(provider, token);
                    await AuthenticationService.Instance.GetPlayerInfoAsync();
                    await PushAsync();
                    Busy = false;
                    Say($"Compte {Label(provider)} relié : ta partie est sauvegardée en ligne.");
                }
                catch (AuthenticationException e) when (e.ErrorCode == AuthenticationErrorCodes.AccountAlreadyLinked)
                {
                    // Ce compte Google/Facebook/Apple a déjà sa partie : on bascule dessus, en gardant la plus avancée.
                    var local = GameProgress.Export();
                    AuthenticationService.Instance.SignOut(true);
                    token = await ProviderToken(provider); // un jeton ne sert qu'une fois
                    await SignIn(provider, token);
                    await AuthenticationService.Instance.GetPlayerNameAsync();
                    await SyncAsync(local);
                    Busy = false;
                    Say($"Connecté avec {Label(provider)}.");
                }
            }
            catch (Exception e)
            {
                Busy = false;
                Debug.LogWarning($"Super Yao : connexion {Label(provider)} impossible : {e}");
                Say($"Connexion à {Label(provider)} impossible. Réessaie plus tard.");
            }
#else
            await Task.CompletedTask;
            Say("Les comptes en ligne ne sont pas encore installés dans ce jeu.");
#endif
        }

        /// <summary>Déconnexion : le téléphone garde la partie, et un nouveau compte invité est créé.</summary>
        public async Task SignOutAsync()
        {
#if SUPERYAO_UGS
            if (!SignedIn || Busy) return;
            Busy = true;
            try
            {
                await PushAsync();
                AuthenticationService.Instance.SignOut(true);
                await AuthenticationService.Instance.SignInAnonymouslyAsync();
                await AuthenticationService.Instance.GetPlayerNameAsync();
                await PushAsync();
                Busy = false;
                Say("Déconnecté. Tu joues maintenant en invité.");
            }
            catch (Exception e)
            {
                Busy = false;
                Debug.LogWarning("Super Yao : déconnexion : " + e.Message);
                Say("Déconnecté.");
            }
#else
            await Task.CompletedTask;
#endif
        }

        /// <summary>Change le pseudo affiché dans le classement (lettres, chiffres, sans espace, 50 caractères au plus).</summary>
        public async Task RenameAsync(string name)
        {
#if SUPERYAO_UGS
            name = (name ?? "").Trim().Replace(' ', '_');
            if (name.Length < 2) { Say("Le pseudo doit faire au moins 2 caractères."); return; }
            if (name.Length > 30) name = name.Substring(0, 30);
            await StartAsync();
            if (!SignedIn || Busy) return;
            Busy = true;
            try
            {
                await AuthenticationService.Instance.UpdatePlayerNameAsync(name);
                Busy = false;
                Say("Pseudo changé.");
            }
            catch (Exception e)
            {
                Busy = false;
                Debug.LogWarning("Super Yao : pseudo refusé : " + e.Message);
                Say("Ce pseudo n'est pas accepté (lettres et chiffres seulement).");
            }
#else
            await Task.CompletedTask;
#endif
        }

        /// <summary>Le classement : les premiers, puis la place du joueur s'il n'y est pas.</summary>
        public async Task<List<Entry>> TopAsync(int count)
        {
            var list = new List<Entry>();
#if SUPERYAO_UGS
            await StartAsync();
            if (!SignedIn) return list;
            try
            {
                var page = await LeaderboardsService.Instance.GetScoresAsync(LeaderboardId, new GetScoresOptions { Limit = count });
                string me = AuthenticationService.Instance.PlayerId;
                bool found = false;
                foreach (var e in page.Results)
                {
                    bool mine = e.PlayerId == me;
                    found |= mine;
                    list.Add(new Entry { rank = e.Rank + 1, name = Clean(e.PlayerName), score = (long)e.Score, me = mine });
                }
                if (!found)
                {
                    try
                    {
                        var mine = await LeaderboardsService.Instance.GetPlayerScoreAsync(LeaderboardId);
                        list.Add(new Entry { rank = mine.Rank + 1, name = Clean(mine.PlayerName), score = (long)mine.Score, me = true });
                    }
                    catch (Exception) { } // pas encore de score
                }
            }
            catch (Exception e)
            {
                Debug.LogWarning("Super Yao : classement indisponible : " + e.Message);
                Say("Classement indisponible pour l'instant.");
            }
#else
            await Task.CompletedTask;
#endif
            return list;
        }

        /// <summary>Raison lisible d'un échec (avec le message technique, pour comprendre ce qui bloque).</summary>
        static string Reason(Exception e)
        {
            var inner = e;
            while (inner.InnerException != null) inner = inner.InnerException;
            string m = inner.Message ?? "";
            string why = m.Contains("Forbidden") || m.Contains("403") || m.Contains("not enabled") || m.Contains("404")
                ? "le service n'est pas activé dans Unity Cloud. "
                : m.Contains("project") || m.Contains("Project") ? "le projet n'est pas relié à Unity Cloud. "
                : m.Contains("Network") || m.Contains("resolve") || m.Contains("connect") ? "pas d'Internet. " : "";
            return why + "(" + inner.GetType().Name + " : " + (m.Length > 160 ? m.Substring(0, 160) : m) + ")";
        }

        static string Clean(string n)
        {
            if (string.IsNullOrEmpty(n)) return "Joueur";
            int hash = n.LastIndexOf('#');
            return hash > 0 ? n.Substring(0, hash) : n;
        }

#if SUPERYAO_UGS
        /// <summary>Envoie la partie et le score du classement (sans bruit si hors ligne).</summary>
        async Task PushAsync()
        {
            if (!SignedIn) return;
            try
            {
                var snap = GameProgress.Export();
                await CloudSaveService.Instance.Data.Player.SaveAsync(new Dictionary<string, object> { { SaveKey, JsonUtility.ToJson(snap) } });
                if (snap.Total > 0) await LeaderboardsService.Instance.AddPlayerScoreAsync(LeaderboardId, snap.Total);
            }
            catch (Exception e)
            {
                Debug.LogWarning("Super Yao : sauvegarde en ligne impossible pour l'instant : " + e.Message);
            }
        }

        /// <summary>Compare la partie du compte et celle de l'appareil (ou <paramref name="local"/>) : garde la plus avancée,
        /// et met les deux à jour.</summary>
        async Task SyncAsync(GameProgress.Snapshot local = null)
        {
            local ??= GameProgress.Export();
            GameProgress.Snapshot cloud = null;
            try
            {
                var items = await CloudSaveService.Instance.Data.Player.LoadAsync(new HashSet<string> { SaveKey });
                if (items.TryGetValue(SaveKey, out var item)) cloud = JsonUtility.FromJson<GameProgress.Snapshot>(item.Value.GetAs<string>());
            }
            catch (Exception e)
            {
                Debug.LogWarning("Super Yao : lecture de la sauvegarde en ligne impossible : " + e.Message);
                return;
            }
            if (cloud != null && GameProgress.IsAhead(cloud, local) && cloud.started == 1)
            {
                GameProgress.Import(cloud);
                ProgressReplaced?.Invoke();
            }
            else
            {
                GameProgress.Import(local);
                await PushAsync();
            }
        }

        static Task Link(Provider p, string token) =>
            p == Provider.Google ? AuthenticationService.Instance.LinkWithGooglePlayGamesAsync(token)
            : p == Provider.Facebook ? AuthenticationService.Instance.LinkWithFacebookAsync(token)
            : AuthenticationService.Instance.LinkWithAppleAsync(token);

        static Task SignIn(Provider p, string token) =>
            p == Provider.Google ? AuthenticationService.Instance.SignInWithGooglePlayGamesAsync(token)
            : p == Provider.Facebook ? AuthenticationService.Instance.SignInWithFacebookAsync(token)
            : AuthenticationService.Instance.SignInWithAppleAsync(token);
#else
        Task PushAsync() => Task.CompletedTask;
#endif

        /// <summary>Jeton du fournisseur (sa propre fenêtre de connexion), ou null si annulé ou non disponible.</summary>
        Task<string> ProviderToken(Provider p)
        {
            switch (p)
            {
                case Provider.Google: return SocialLogin.GoogleAsync();
                case Provider.Facebook: return SocialLogin.FacebookAsync();
                default: return SocialLogin.AppleAsync(this);
            }
        }
    }
}
