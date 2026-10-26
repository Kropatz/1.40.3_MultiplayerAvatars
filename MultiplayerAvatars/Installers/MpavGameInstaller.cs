using BeatSaber.AvatarCore;
using MultiplayerAvatars.Avatars;
using SiraUtil.Extras;
using SiraUtil.Objects.Multiplayer;
using UnityEngine;
using Zenject;

namespace MultiplayerAvatars.Installers
{
    internal class MpavGameInstaller : Installer
    {
        public override void InstallBindings()
        {
            Container.RegisterRedecorator(new ConnectedPlayerRegistration(DecorateConnectedPlayerFacade));
            Container.RegisterRedecorator(new ConnectedPlayerDuelRegistration(DecorateConnectedPlayerFacade));
        }

        private MultiplayerConnectedPlayerFacade DecorateConnectedPlayerFacade(MultiplayerConnectedPlayerFacade original)
        {
            var avatar = original.GetComponentInChildren<AvatarController>(true);
            if (avatar != null && avatar.GetComponent<CustomAvatarController>() == null)
                avatar.gameObject.AddComponent<CustomAvatarController>();
            return original;
        }
    }
}
