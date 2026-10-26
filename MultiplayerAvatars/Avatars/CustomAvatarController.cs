using BeatSaber.AvatarCore;
using CustomAvatar.Avatar;
using IPA.Utilities;
using IPA.Utilities.Async;
using MultiplayerAvatars.Networking;
using MultiplayerAvatars.Providers;
using SiraUtil.Logging;
using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;
using Zenject;

namespace MultiplayerAvatars.Avatars
{
    internal class CustomAvatarController : MonoBehaviour
    {
        private AvatarSpawner _spawner = null!;
        private IConnectedPlayer _player = null!;
        private CustomAvatarManager _manager = null!;
        private AvatarProviderService _provider = null!;
        private SiraLog _logger = null!;
        private IAvatarPoseDataProvider? _poses;
        private AvatarController? _vanilla;
        private Transform? _avatarParent;
        private MultiplayerAvatarInput? _input;
        private SpawnedAvatar? _spawned;
        private readonly Dictionary<Renderer, bool> _hiddenRenderers = new();
        private bool _started;
        private bool _subscribed;
        private int _request;

        [Inject]
        public void Construct(AvatarSpawner spawner, IConnectedPlayer player,
            CustomAvatarManager manager, AvatarProviderService provider, SiraLog logger,
            [InjectOptional] IAvatarPoseDataProvider? poses)
        {
            _spawner = spawner;
            _player = player;
            _manager = manager;
            _provider = provider;
            _logger = logger;
            _poses = poses;
        }

        public void Start()
        {
            // Visual avatars are loaded asynchronously. Use their stable controller and parent.
            try
            {
                _vanilla = GetComponent<AvatarController>();
                if (_poses == null || _vanilla == null)
                {
                    _logger.Warn("Custom avatar unavailable: missing pose provider or AvatarController; retaining default avatar.");
                    return;
                }
                _avatarParent = _vanilla.GetField<Transform, AvatarController>("_parentingTransform");
                if (_avatarParent == null)
                {
                    _logger.Warn("Custom avatar unavailable: missing avatar parent; retaining default avatar.");
                    return;
                }
                _started = true;
                OnEnable();
            }
            catch (Exception ex)
            {
                _logger.Error($"Custom avatar setup failed; retaining default avatar: {ex}");
            }
        }

        public void OnEnable()
        {
            if (!_started || _subscribed) return;
            _input = new MultiplayerAvatarInput(_poses!, _avatarParent!);
            _manager.avatarReceived += HandleAvatarReceived;
            _subscribed = true;
            HandleAvatarReceived(_player, _manager.GetPlayerAvatarPacket(_player.userId));
        }

        public void OnDisable()
        {
            ++_request; // Invalidate any load still in flight.
            if (_subscribed) _manager.avatarReceived -= HandleAvatarReceived;
            _subscribed = false;
            ClearAvatar();
            _input?.Dispose();
            _input = null;
        }

        private void HandleAvatarReceived(IConnectedPlayer player, CustomAvatarPacket packet)
        {
            if (player.userId != _player.userId) return;
            int request = ++_request;
            ClearAvatar();
            if (packet.Hash == "FFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFF") return;
            _ = LoadAvatar(packet.Hash, packet.Scale, request);
        }

        private async Task LoadAvatar(string hash, float scale, int request)
        {
            try
            {
                var prefab = await _provider.GetAvatarByHash(hash, CancellationToken.None);
                await UnityMainThreadTaskScheduler.Factory.StartNew(() =>
                {
                    if (this == null || !isActiveAndEnabled || request != _request) return;
                    if (prefab == null)
                    {
                        _logger.Warn($"Failed to load avatar {hash}; retaining default avatar.");
                        return;
                    }
                    try
                    {
                        _spawned = _spawner.SpawnAvatar(prefab, _input!, _avatarParent!);
                        var ik = _spawned.GetComponent<AvatarIK>();
                        if (ik != null) ik.isLocomotionEnabled = true;
                        _spawned.transform.localScale = Vector3.one * scale;
                        LateUpdate();
                    }
                    catch
                    {
                        ClearAvatar();
                        throw;
                    }
                });
            }
            catch (Exception ex)
            {
                _logger.Error($"Custom avatar load failed for {hash}: {ex}");
            }
        }

        public void LateUpdate()
        {
            // The default visual may finish loading after our custom avatar.
            if (_spawned == null || _vanilla == null || _vanilla.avatar == null) return;
            foreach (var renderer in _vanilla.avatar.GetComponentsInChildren<Renderer>(true))
            {
                if (!_hiddenRenderers.ContainsKey(renderer))
                    _hiddenRenderers.Add(renderer, renderer.enabled);
                renderer.enabled = false;
            }
        }

        private void ClearAvatar()
        {
            if (_spawned != null)
            {
                _spawned.gameObject.SetActive(false);
                Destroy(_spawned.gameObject);
                _spawned = null;
            }
            foreach (var entry in _hiddenRenderers)
                if (entry.Key != null) entry.Key.enabled = entry.Value;
            _hiddenRenderers.Clear();
        }
    }
}
