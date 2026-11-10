using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.Build;

namespace SuperYao.EditorTools
{
    /// <summary>
    /// Active le code du compte en ligne selon ce qui est installé dans le projet : à chaque ouverture et recompilation,
    /// on cherche les SDK et on pose (ou retire) les symboles de compilation, pour Android, iPhone et PC :
    /// SUPERYAO_UGS (Unity Gaming Services : Authentication, Cloud Save, Leaderboards), SUPERYAO_GPGS (Google Play Games),
    /// SUPERYAO_FACEBOOK (Facebook SDK), SUPERYAO_APPLE (Sign in with Apple), SUPERYAO_LEVELPLAY (vidéos récompensées). Sans eux, le jeu reste jouable hors ligne.
    /// </summary>
    [InitializeOnLoad]
    static class OnlineDefines
    {
        static readonly (string define, string[] types)[] Sdks =
        {
            ("SUPERYAO_UGS", new[] { "Unity.Services.Authentication.AuthenticationService", "Unity.Services.CloudSave.CloudSaveService", "Unity.Services.Leaderboards.LeaderboardsService" }),
            ("SUPERYAO_GPGS", new[] { "GooglePlayGames.PlayGamesPlatform" }),
            ("SUPERYAO_FACEBOOK", new[] { "Facebook.Unity.FB" }),
            ("SUPERYAO_APPLE", new[] { "AppleAuth.AppleAuthManager" }),
            ("SUPERYAO_LEVELPLAY", new[] { "Unity.Services.LevelPlay.LevelPlay" }),
        };

        static OnlineDefines()
        {
            var types = new HashSet<string>();
            foreach (var asm in AppDomain.CurrentDomain.GetAssemblies())
            {
                if (asm.IsDynamic) continue;
                foreach (var sdk in Sdks)
                    foreach (var t in sdk.types)
                        if (!types.Contains(t) && asm.GetType(t, false) != null) types.Add(t);
            }
            var wanted = Sdks.Where(s => s.types.All(types.Contains)).Select(s => s.define).ToList();
            foreach (var target in new[] { NamedBuildTarget.Android, NamedBuildTarget.iOS, NamedBuildTarget.Standalone })
            {
                var current = PlayerSettings.GetScriptingDefineSymbols(target).Split(';').Where(d => d.Length > 0).ToList();
                var next = current.Where(d => !d.StartsWith("SUPERYAO_")).Concat(wanted).ToList();
                if (next.OrderBy(d => d).SequenceEqual(current.OrderBy(d => d))) continue;
                PlayerSettings.SetScriptingDefineSymbols(target, string.Join(";", next));
                UnityEngine.Debug.Log($"Super Yao : compte en ligne, symboles {target.TargetName} = [{string.Join(", ", wanted)}].");
            }
        }
    }
}
