using MultiplayerAvatars.Avatars;
using SiraUtil.Extras;
using SiraUtil.Objects.Multiplayer;
using Zenject;

namespace MultiplayerAvatars.Installers
{
    internal class MpavLobbyInstaller : Installer
    {
        public override void InstallBindings()
        {
            Container.RegisterRedecorator(new LobbyAvatarRegistration(DecorateAvatar));
        }

        private MultiplayerLobbyAvatarController DecorateAvatar(MultiplayerLobbyAvatarController original)
        {
            var avatar = original.GetComponentInChildren<BeatSaber.AvatarCore.AvatarController>(true);
            if (avatar != null && avatar.GetComponent<CustomAvatarController>() == null)
                avatar.gameObject.AddComponent<CustomAvatarController>();
            return original;
        }
    }
}
