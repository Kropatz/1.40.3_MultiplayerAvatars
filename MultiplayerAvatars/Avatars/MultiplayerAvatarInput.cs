using BeatSaber.AvatarCore;
using CustomAvatar.Tracking;
using System;
using UnityEngine;

namespace MultiplayerAvatars.Avatars
{
    // Feeds a connected player's synced head/hand poses to a custom avatar.
    // CustomAvatars uses the transforms we hand out as IK targets / constraint sources,
    // so we keep our own transforms in the same space the base game avatar is posed in.
    internal class MultiplayerAvatarInput : IAvatarInput, IDisposable
    {
        private readonly IAvatarPoseDataProvider _poseDataProvider;

        private readonly Transform _root;
        private readonly Transform _head;
        private readonly Transform _leftHand;
        private readonly Transform _rightHand;

        internal MultiplayerAvatarInput(IAvatarPoseDataProvider poseDataProvider, Transform parent)
        {
            _poseDataProvider = poseDataProvider;

            _root = new GameObject(nameof(MultiplayerAvatarInput)).transform;
            _root.SetParent(parent, false);
            _head = CreateNode("Head");
            _leftHand = CreateNode("LeftHand");
            _rightHand = CreateNode("RightHand");

            HandlePoseDidChange(_poseDataProvider.currentPose);
            _poseDataProvider.poseDidChangeEvent += HandlePoseDidChange;
        }

        public bool allowMaintainPelvisPosition => true;

        // Our transforms never change identity, so there is nothing to notify about
        public event Action? inputChanged { add { } remove { } }

        public void Dispose()
        {
            _poseDataProvider.poseDidChangeEvent -= HandlePoseDidChange;
            if (_root != null)
                UnityEngine.Object.Destroy(_root.gameObject);
        }

        public bool TryGetTransform(DeviceUse use, out Transform transform)
        {
            switch (use)
            {
                case DeviceUse.Head:
                    transform = _head;
                    return true;
                case DeviceUse.LeftHand:
                    transform = _leftHand;
                    return true;
                case DeviceUse.RightHand:
                    transform = _rightHand;
                    return true;
                default:
                    transform = null!;
                    return false;
            }
        }

        public bool TryGetFingerCurl(DeviceUse use, out FingerCurl curl)
        {
            curl = new FingerCurl(0f, 0f, 0f, 0f, 0f);
            return false;
        }

        private Transform CreateNode(string name)
        {
            var node = new GameObject(name).transform;
            node.SetParent(_root, false);
            return node;
        }

        private void HandlePoseDidChange(AvatarPoseData pose)
        {
            if (_root == null)
                return;
            _head.SetLocalPositionAndRotation(pose.headPose.position, Normalized(pose.headPose.rotation));
            _leftHand.SetLocalPositionAndRotation(pose.leftHandPose.position, Normalized(pose.leftHandPose.rotation));
            _rightHand.SetLocalPositionAndRotation(pose.rightHandPose.position, Normalized(pose.rightHandPose.rotation));
        }

        // Poses default to an all-zero quaternion before the first sync state arrives
        private static Quaternion Normalized(Quaternion rotation)
            => rotation == new Quaternion(0f, 0f, 0f, 0f) ? Quaternion.identity : rotation;
    }
}
