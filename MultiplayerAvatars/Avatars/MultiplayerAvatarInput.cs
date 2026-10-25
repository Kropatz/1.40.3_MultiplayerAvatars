using BeatSaber.AvatarCore;
using CustomAvatar.Tracking;
using IPA.Utilities;
using System;
using UnityEngine;

namespace MultiplayerAvatars.Avatars
{
    // Feeds a connected player's synced head/hand poses to a custom avatar.
    // CustomAvatars uses the transforms we hand out as IK targets / constraint sources.
    internal class MultiplayerAvatarInput : IAvatarInput, IDisposable
    {
        private static readonly FieldAccessor<MultiplayerAvatarPoseController, Transform>.Accessor HeadTransform
            = FieldAccessor<MultiplayerAvatarPoseController, Transform>.GetAccessor("_headTransform");
        private static readonly FieldAccessor<MultiplayerAvatarPoseController, Transform>.Accessor LeftSaberTransform
            = FieldAccessor<MultiplayerAvatarPoseController, Transform>.GetAccessor("_leftSaberTransform");
        private static readonly FieldAccessor<MultiplayerAvatarPoseController, Transform>.Accessor RightSaberTransform
            = FieldAccessor<MultiplayerAvatarPoseController, Transform>.GetAccessor("_rightSaberTransform");

        private readonly IAvatarPoseDataProvider? _poseDataProvider;
        private readonly Func<bool>? _swapHands;

        private readonly Transform? _root;
        private readonly Transform _head;
        private readonly Transform _leftHand;
        private readonly Transform _rightHand;

        // Keeps our own transforms in the space the base game avatar is posed in, updated from the pose data provider
        // swapHands: reflected poses keep left/right as-is, so a humanoid avatar needs them swapped to look like a reflection
        internal MultiplayerAvatarInput(IAvatarPoseDataProvider poseDataProvider, Transform parent, Func<bool>? swapHands = null)
        {
            _poseDataProvider = poseDataProvider;
            _swapHands = swapHands;

            _root = new GameObject(nameof(MultiplayerAvatarInput)).transform;
            _root.SetParent(parent, false);
            _head = CreateNode("Head");
            _leftHand = CreateNode("LeftHand");
            _rightHand = CreateNode("RightHand");

            HandlePoseDidChange(_poseDataProvider.currentPose);
            _poseDataProvider.poseDidChangeEvent += HandlePoseDidChange;
        }

        // Uses the head/saber transforms the game itself moves for this player
        private MultiplayerAvatarInput(Transform head, Transform leftHand, Transform rightHand)
        {
            _head = head;
            _leftHand = leftHand;
            _rightHand = rightHand;
        }

        internal static MultiplayerAvatarInput? FromPoseController(MultiplayerAvatarPoseController? poseController)
        {
            if (poseController == null)
                return null;
            var head = HeadTransform(ref poseController);
            var leftHand = LeftSaberTransform(ref poseController);
            var rightHand = RightSaberTransform(ref poseController);
            if (head == null || leftHand == null || rightHand == null)
                return null;
            return new MultiplayerAvatarInput(head, leftHand, rightHand);
        }

        public bool allowMaintainPelvisPosition => true;

        // Our transforms never change identity, so there is nothing to notify about
        public event Action? inputChanged { add { } remove { } }

        public void Dispose()
        {
            if (_poseDataProvider != null)
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
            var swap = _swapHands != null && _swapHands();
            var leftHandPose = swap ? pose.rightHandPose : pose.leftHandPose;
            var rightHandPose = swap ? pose.leftHandPose : pose.rightHandPose;
            _leftHand.SetLocalPositionAndRotation(leftHandPose.position, Normalized(leftHandPose.rotation));
            _rightHand.SetLocalPositionAndRotation(rightHandPose.position, Normalized(rightHandPose.rotation));
        }

        // Poses default to an all-zero quaternion before the first sync state arrives
        private static Quaternion Normalized(Quaternion rotation)
            => rotation == new Quaternion(0f, 0f, 0f, 0f) ? Quaternion.identity : rotation;
    }
}
