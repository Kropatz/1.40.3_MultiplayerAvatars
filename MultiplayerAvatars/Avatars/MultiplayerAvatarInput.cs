using BeatSaber.AvatarCore;
using CustomAvatar.Avatar;
using CustomAvatar.Tracking;
using System;
using UnityEngine;

namespace MultiplayerAvatars.Avatars
{
    internal sealed class MultiplayerAvatarInput : IAvatarInput, IDisposable
    {
        private readonly IAvatarPoseDataProvider _provider;
        private AvatarPoseData _pose;
        private readonly Transform _root;
        private readonly Transform _head;
        private readonly Transform _left;
        private readonly Transform _right;

        internal MultiplayerAvatarInput(IAvatarPoseDataProvider provider, Transform parent)
        {
            _provider = provider;
            _root = new GameObject("MultiplayerAvatars Tracking").transform;
            _root.SetParent(parent, false);
            _head = CreateTarget("Head");
            _left = CreateTarget("Left Hand");
            _right = CreateTarget("Right Hand");
            OnPoseChanged(provider.currentPose);
            provider.poseDidChangeEvent += OnPoseChanged;
        }

        private Transform CreateTarget(string name)
        {
            var target = new GameObject(name).transform;
            target.SetParent(_root, false);
            return target;
        }

        private void OnPoseChanged(AvatarPoseData pose)
        {
            _pose = pose;
            _head.SetLocalPositionAndRotation(pose.headPose.position, pose.headPose.rotation);
            _left.SetLocalPositionAndRotation(pose.leftHandPose.position, pose.leftHandPose.rotation);
            _right.SetLocalPositionAndRotation(pose.rightHandPose.position, pose.rightHandPose.rotation);
            inputChanged?.Invoke();
        }

        public void Dispose()
        {
            _provider.poseDidChangeEvent -= OnPoseChanged;
            if (_root != null) UnityEngine.Object.Destroy(_root.gameObject);
        }
        public bool allowMaintainPelvisPosition => true;
        public event Action? inputChanged;

        public bool TryGetFingerCurl(DeviceUse use, out FingerCurl curl)
        {
            curl = new FingerCurl(0f, 0f, 0f, 0f, 0f);
            return false;
        }

        public bool TryGetPose(DeviceUse use, out Pose pose)
        {
            switch (use)
            {
                case DeviceUse.Head: pose = _pose.headPose; return true;
                case DeviceUse.LeftHand: pose = _pose.leftHandPose; return true;
                case DeviceUse.RightHand: pose = _pose.rightHandPose; return true;
                default: pose = Pose.identity; return false;
            }
        }

        // Custom Avatars' IK and constraints require real world-space targets.
        public bool TryGetTransform(DeviceUse use, out Transform transform)
        {
            switch (use)
            {
                case DeviceUse.Head: transform = _head; return true;
                case DeviceUse.LeftHand: transform = _left; return true;
                case DeviceUse.RightHand: transform = _right; return true;
                default: transform = null!; return false;
            }
        }
    }
}
