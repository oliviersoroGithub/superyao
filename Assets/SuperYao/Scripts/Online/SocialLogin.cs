using System.Threading.Tasks;
using UnityEngine;
#if SUPERYAO_GPGS && UNITY_ANDROID
using GooglePlayGames;
using GooglePlayGames.BasicApi;
#endif
#if SUPERYAO_FACEBOOK
using System.Collections.Generic;
using Facebook.Unity;
#endif
#if SUPERYAO_APPLE && UNITY_IOS
using AppleAuth;
using AppleAuth.Enums;
using AppleAuth.Interfaces;
using AppleAuth.Native;
#endif

namespace SuperYao
{
    /// <summary>
    /// Fenêtres de connexion des fournisseurs, qui donnent le jeton transmis au compte en ligne (OnlineAccount) :
    /// - Google : Google Play Jeux (plugin « Google Play Games for Unity », SUPERYAO_GPGS), Android seulement ;
    /// - Facebook : « Facebook SDK for Unity » (SUPERYAO_FACEBOOK) ;
    /// - Apple : « Sign in with Apple Unity Plugin » (com.lupidan.apple-signin-unity, SUPERYAO_APPLE), iPhone seulement.
    /// Sans le SDK, la méthode renvoie null (le bouton n'est pas proposé).
    /// </summary>
    public static class SocialLogin
    {
        public static Task<string> GoogleAsync()
        {
#if SUPERYAO_GPGS && UNITY_ANDROID
            var done = new TaskCompletionSource<string>();
            PlayGamesPlatform.Activate();
            PlayGamesPlatform.Instance.ManuallyAuthenticate(status =>
            {
                if (status != SignInStatus.Success) { done.TrySetResult(null); return; }
                // Code à usage unique, échangé par Unity contre l'identité Google Play Jeux du joueur.
                PlayGamesPlatform.Instance.RequestServerSideAccess(true, code => done.TrySetResult(code));
            });
            return done.Task;
#else
            return Task.FromResult<string>(null);
#endif
        }

        public static Task<string> FacebookAsync()
        {
#if SUPERYAO_FACEBOOK
            var done = new TaskCompletionSource<string>();
            void LogIn() => FB.LogInWithReadPermissions(new List<string> { "public_profile" }, result =>
            {
                bool ok = result != null && string.IsNullOrEmpty(result.Error) && !result.Cancelled && FB.IsLoggedIn;
                done.TrySetResult(ok ? AccessToken.CurrentAccessToken.TokenString : null);
            });
            if (FB.IsInitialized) LogIn();
            else FB.Init(() => { FB.ActivateApp(); LogIn(); });
            return done.Task;
#else
            return Task.FromResult<string>(null);
#endif
        }

        public static Task<string> AppleAsync(MonoBehaviour host)
        {
#if SUPERYAO_APPLE && UNITY_IOS
            var done = new TaskCompletionSource<string>();
            var manager = new AppleAuthManager(new PayloadDeserializer());
            var pump = host.gameObject.AddComponent<ApplePump>();
            pump.manager = manager;
            manager.LoginWithAppleId(new AppleAuthLoginArgs(LoginOptions.IncludeFullName), credential =>
            {
                Object.Destroy(pump);
                var apple = credential as IAppleIDCredential;
                done.TrySetResult(apple != null && apple.IdentityToken != null ? System.Text.Encoding.UTF8.GetString(apple.IdentityToken) : null);
            }, error =>
            {
                Object.Destroy(pump);
                done.TrySetResult(null);
            });
            return done.Task;
#else
            return Task.FromResult<string>(null);
#endif
        }

#if SUPERYAO_APPLE && UNITY_IOS
        /// <summary>Le plugin Apple doit être appelé à chaque image pour recevoir la réponse.</summary>
        class ApplePump : MonoBehaviour
        {
            public AppleAuthManager manager;
            void Update() => manager?.Update();
        }
#endif
    }
}
