using BeatSaber.AvatarCore;
using CustomAvatar.Avatar;
using IPA.Utilities;
using MultiplayerAvatars.Networking;
using MultiplayerAvatars.Providers;
using SiraUtil.Logging;
using System;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;
using Zenject;

namespace MultiplayerAvatars.Avatars
{
    // Lives next to the game's AvatarController and swaps the base game avatar for the player's custom avatar.
    // Dependencies are resolved optionally: a failure here must never break lobby/game setup.
    internal class CustomAvatarController : MonoBehaviour
    {
        private static readonly FieldAccessor<AvatarController, Transform>.Accessor ParentingTransform
            = FieldAccessor<AvatarController, Transform>.GetAccessor("_parentingTransform");

        private CustomAvatarPacket _avatarPacket = new();
        private SpawnedAvatar? _spawnedAvatar;
        private MultiplayerAvatarInput? _avatarInput;
        private Renderer[]? _hiddenRenderers;
        private int _loadVersion;
        private bool _started;

        private AvatarController _avatarController = null!;
        private AvatarSpawner _avatarSpawner = null!;
        private IConnectedPlayer _connectedPlayer = null!;
        private CustomAvatarManager _customAvatarManager = null!;
        private AvatarProviderService _avatarProvider = null!;
        private IAvatarPoseDataProvider _poseDataProvider = null!;
        private SiraLog? _logger;

        [Inject]
        public void Construct(DiContainer container)
        {
            _logger = container.TryResolve<SiraLog>();
            _avatarController = GetComponent<AvatarController>();
            _avatarSpawner = container.TryResolve<AvatarSpawner>();
            _connectedPlayer = container.TryResolve<IConnectedPlayer>();
            _customAvatarManager = container.TryResolve<CustomAvatarManager>();
            _avatarProvider = container.TryResolve<AvatarProviderService>();
            _poseDataProvider = container.TryResolve<IAvatarPoseDataProvider>();

            if (_avatarController == null || _avatarSpawner == null || _connectedPlayer == null
                || _customAvatarManager == null || _avatarProvider == null || _poseDataProvider == null)
            {
                Log($"Missing dependencies on '{name}', custom avatar disabled " +
                    $"(controller={_avatarController != null}, spawner={_avatarSpawner != null}, player={_connectedPlayer != null}, " +
                    $"manager={_customAvatarManager != null}, provider={_avatarProvider != null}, pose={_poseDataProvider != null})");
                enabled = false;
            }
        }

        public void Start()
        {
            if (!enabled)
                return;
            _started = true;
            _customAvatarManager.avatarReceived += HandleAvatarReceived;
            HandleAvatarReceived(_connectedPlayer, _customAvatarManager.GetPlayerAvatarPacket(_connectedPlayer.userId));
        }

        public void Update()
        {
            // The base game avatar loads asynchronously, so keep it hidden while ours is shown
            if (_spawnedAvatar != null && _hiddenRenderers == null && _avatarController.avatar != null)
            {
                _hiddenRenderers = _avatarController.avatar.GetComponentsInChildren<Renderer>(true);
                SetBaseAvatarVisible(false);
            }
        }

        public void OnDestroy()
        {
            if (_started)
                _customAvatarManager.avatarReceived -= HandleAvatarReceived;
            _loadVersion++;
            DestroyCustomAvatar();
        }

        private void HandleAvatarReceived(IConnectedPlayer player, CustomAvatarPacket packet)
        {
            if (player.userId != _connectedPlayer.userId)
                return;

            var previousHash = _avatarPacket.Hash;
            _avatarPacket = packet;

            if (packet.Hash == CustomAvatarPacket.NoAvatarHash)
            {
                _loadVersion++;
                DestroyCustomAvatar();
                return;
            }

            if (_spawnedAvatar != null && previousHash == packet.Hash)
            {
                ApplyScale();
                return;
            }

            _ = LoadAvatar(packet.Hash, ++_loadVersion);
        }

        private async Task LoadAvatar(string hash, int version)
        {
            try
            {
                var avatarPrefab = await _avatarProvider.GetAvatarByHash(hash, CancellationToken.None);
                if (version != _loadVersion || this == null)
                    return;
                if (avatarPrefab == null)
                {
                    _logger?.Warn($"Could not find avatar '{hash}' for player '{_connectedPlayer.userName}'");
                    return;
                }

                CreateAvatar(avatarPrefab);
            }
            catch (Exception ex)
            {
                _logger?.Error($"Failed to load avatar '{hash}' for player '{_connectedPlayer.userName}': {ex.Message}");
                _logger?.Debug(ex);
                DestroyCustomAvatar();
            }
        }

        private void CreateAvatar(AvatarPrefab avatar)
        {
            DestroyCustomAvatar();

            var parent = ParentingTransform(ref _avatarController) ?? _avatarController.transform;
            _avatarInput = new MultiplayerAvatarInput(_poseDataProvider, parent);
            _spawnedAvatar = _avatarSpawner.SpawnAvatar(avatar, _avatarInput, parent);
            var avatarIk = _spawnedAvatar.GetComponent<AvatarIK>();
            if (avatarIk != null)
                avatarIk.isLocomotionEnabled = true;
            ApplyScale();
            _logger?.Info($"Spawned avatar '{avatar.descriptor.name}' for player '{_connectedPlayer.userName}'");
        }

        private void ApplyScale()
        {
            if (_spawnedAvatar != null)
                _spawnedAvatar.transform.localScale = Vector3.one * (_avatarPacket.Scale > 0f ? _avatarPacket.Scale : 1f);
        }

        private void DestroyCustomAvatar()
        {
            if (_spawnedAvatar != null)
                Destroy(_spawnedAvatar.gameObject);
            _spawnedAvatar = null;
            _avatarInput?.Dispose();
            _avatarInput = null;
            SetBaseAvatarVisible(true);
            _hiddenRenderers = null;
        }

        private void SetBaseAvatarVisible(bool visible)
        {
            if (_hiddenRenderers == null)
                return;
            foreach (var renderer in _hiddenRenderers)
                if (renderer != null)
                    renderer.enabled = visible;
        }

        private void Log(string message)
        {
            if (_logger != null)
                _logger.Warn(message);
            else
                Plugin.Logger.Warn(message);
        }
    }
}
