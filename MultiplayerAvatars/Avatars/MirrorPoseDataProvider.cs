using BeatSaber.AvatarCore;
using System;
using System.Reflection;

namespace MultiplayerAvatars.Avatars
{
    // Soft integration with MultiplayerMirror's MirrorAvatarPoseDataProvider, without referencing the mod
    internal static class MirrorPoseDataProvider
    {
        private const string TypeName = "MultiplayerMirror.Core.Scripts.MirrorAvatarPoseDataProvider";

        // Returns whether hands should currently be swapped, or null if the provider isn't a mirror
        public static Func<bool>? GetSwapHands(IAvatarPoseDataProvider? provider)
        {
            if (provider == null || provider.GetType().FullName != TypeName)
                return null;
            var getter = provider.GetType().GetProperty("EnableMirror", BindingFlags.Public | BindingFlags.Instance)?.GetGetMethod();
            if (getter == null || getter.ReturnType != typeof(bool))
                return null;
            return (Func<bool>)Delegate.CreateDelegate(typeof(Func<bool>), provider, getter);
        }
    }
}
