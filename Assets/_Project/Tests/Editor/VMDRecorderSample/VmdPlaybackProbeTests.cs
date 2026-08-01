using NUnit.Framework;
using System.Collections.Generic;
using UnityEngine;

namespace Tests.Editor.VMDRecorderSample
{
    public class VmdPlaybackProbeTests
    {
        [Test]
        public void Given_NewProbe_When_Created_Then_PlaybackIsDefaultOff()
        {
            var probeObject = new GameObject("vmd-playback-probe");

            try
            {
                var probe = probeObject.AddComponent<VmdPlaybackProbe>();

                Assert.That(probe.PlaybackEnabled, Is.False);
                Assert.That(probe.ApplyIkTargets, Is.False);
            }
            finally
            {
                Object.DestroyImmediate(probeObject);
            }
        }

        [Test]
        public void Given_RuntimeMotionPath_When_ConfiguringProbe_Then_EnablesCarrierPlaybackForMovingRootEvidence()
        {
            var probeObject = new GameObject("vmd-playback-probe");

            try
            {
                probeObject.transform.localPosition = new Vector3(0f, -0.02621f, 0f);
                var probe = probeObject.AddComponent<VmdPlaybackProbe>();

                probe.ConfigureRuntimePlayback(
                    "Assets/VMDRecorderSample/vmd-rec.vmd",
                    useCenterAsParentOfAll: true,
                    routeCenterBoneToGroove: false);

                Assert.That(probe.PlaybackEnabled, Is.True);
                Assert.That(probe.ApplyIkTargets, Is.False);
                Assert.That(probe.MotionFilePath, Is.EqualTo("Assets/VMDRecorderSample/vmd-rec.vmd"));
                Assert.That(probe.UseCenterAsParentOfAll, Is.True);
                Assert.That(probe.RouteCenterBoneToGroove, Is.False);
                Assert.That(probe.AnchorCarrierPositionsToInitialPose, Is.True);
                Assert.That(probe.LockParentOfAllPosition, Is.False);
                Assert.That(probe.UseExplicitParentOfAllLockPosition, Is.False);
                Assert.That(Vector3.Distance(probeObject.transform.localPosition, new Vector3(0f, -0.02621f, 0f)), Is.LessThan(0.0001f));
            }
            finally
            {
                Object.DestroyImmediate(probeObject);
            }
        }

        [Test]
        public void Given_RuntimeMotionPath_When_RootMovesAfterConfigure_Then_PrepareForMotionComparisonSampleKeepsCarrierFree()
        {
            var probeObject = new GameObject("vmd-playback-probe");

            try
            {
                var probe = probeObject.AddComponent<VmdPlaybackProbe>();
                probe.ConfigureRuntimePlayback(
                    "Assets/VMDRecorderSample/vmd-rec.vmd",
                    useCenterAsParentOfAll: true,
                    routeCenterBoneToGroove: false);

                probeObject.transform.localPosition = new Vector3(0f, -0.02621f, 0f);
                probe.PrepareForMotionComparisonSample();

                Assert.That(Vector3.Distance(probeObject.transform.localPosition, new Vector3(0f, -0.02621f, 0f)), Is.LessThan(0.0001f));
            }
            finally
            {
                Object.DestroyImmediate(probeObject);
            }
        }

        [Test]
        public void Given_DisabledOptions_When_ApplyingFrame_Then_DoesNotMutateTransforms()
        {
            VmdMotionData motion = CreateMotion(frameIndex: 7);
            var parentOfAll = new GameObject("parent-of-all");
            var center = new GameObject("center");
            var spine = new GameObject("spine");

            try
            {
                parentOfAll.transform.localPosition = new Vector3(4f, 5f, 6f);
                center.transform.localPosition = new Vector3(-1f, -2f, -3f);
                spine.transform.localRotation = Quaternion.Euler(1f, 2f, 3f);

                Vector3 beforeParent = parentOfAll.transform.localPosition;
                Vector3 beforeCenter = center.transform.localPosition;
                Quaternion beforeSpineRotation = spine.transform.localRotation;

                VmdPlaybackApplyResult result = VmdPlaybackProbe.ApplyFrame(
                    motion,
                    frameIndex: 7,
                    humanoidTargets: new Dictionary<HumanBodyBones, Transform>
                    {
                        [HumanBodyBones.Spine] = spine.transform
                    },
                    parentOfAllTarget: parentOfAll.transform,
                    centerTarget: center.transform,
                    options: VmdPlaybackProbeOptions.Disabled);

                Assert.That(result.Status, Is.EqualTo(VmdPlaybackApplyStatus.Disabled));
                Assert.That(parentOfAll.transform.localPosition, Is.EqualTo(beforeParent));
                Assert.That(center.transform.localPosition, Is.EqualTo(beforeCenter));
                Assert.That(Quaternion.Angle(spine.transform.localRotation, beforeSpineRotation), Is.LessThan(0.001f));
            }
            finally
            {
                Object.DestroyImmediate(parentOfAll);
                Object.DestroyImmediate(center);
                Object.DestroyImmediate(spine);
            }
        }

        [Test]
        public void Given_RootCenterAndHumanoidFrames_When_ApplyingProbe_Then_OnlyNonIkTargetsChange()
        {
            VmdMotionData motion = CreateMotion(frameIndex: 7);
            var parentOfAll = new GameObject("parent-of-all");
            var center = new GameObject("center");
            var spine = new GameObject("spine");
            var leftFoot = new GameObject("left-foot");

            try
            {
                spine.transform.localPosition = new Vector3(0.1f, 0.2f, 0.3f);
                spine.transform.localScale = new Vector3(2f, 2f, 2f);
                leftFoot.transform.localPosition = new Vector3(-2f, -2f, -2f);
                leftFoot.transform.localRotation = Quaternion.Euler(4f, 5f, 6f);

                Vector3 beforeSpinePosition = spine.transform.localPosition;
                Vector3 beforeSpineScale = spine.transform.localScale;
                Vector3 beforeLeftFootPosition = leftFoot.transform.localPosition;
                Quaternion beforeLeftFootRotation = leftFoot.transform.localRotation;

                VmdPlaybackApplyResult result = VmdPlaybackProbe.ApplyFrame(
                    motion,
                    frameIndex: 7,
                    humanoidTargets: new Dictionary<HumanBodyBones, Transform>
                    {
                        [HumanBodyBones.Spine] = spine.transform,
                        [HumanBodyBones.LeftFoot] = leftFoot.transform
                    },
                    parentOfAllTarget: parentOfAll.transform,
                    centerTarget: center.transform,
                    options: VmdPlaybackProbeOptions.DefaultEnabled);

                Assert.That(result.Status, Is.EqualTo(VmdPlaybackApplyStatus.Applied));
                Assert.That(Vector3.Distance(parentOfAll.transform.localPosition, new Vector3(1f, 2f, 3f)), Is.LessThan(0.0001f));
                Assert.That(Vector3.Distance(center.transform.localPosition, new Vector3(-0.25f, 0.5f, 0.75f)), Is.LessThan(0.0001f));
                Assert.That(Quaternion.Angle(spine.transform.localRotation, Quaternion.Euler(10f, 20f, 30f)), Is.LessThan(0.001f));
                Assert.That(spine.transform.localPosition, Is.EqualTo(beforeSpinePosition));
                Assert.That(spine.transform.localScale, Is.EqualTo(beforeSpineScale));
                Assert.That(leftFoot.transform.localPosition, Is.EqualTo(beforeLeftFootPosition));
                Assert.That(Quaternion.Angle(leftFoot.transform.localRotation, beforeLeftFootRotation), Is.LessThan(0.001f));
                Assert.That(result.AppliedCarrierPositions, Is.EqualTo(2));
                Assert.That(result.AppliedHumanoidRotations, Is.EqualTo(1));
                Assert.That(result.SkippedIkTargetFrames, Is.EqualTo(1));
                Assert.That(result.SkippedMorphFrames, Is.EqualTo(1));
            }
            finally
            {
                Object.DestroyImmediate(parentOfAll);
                Object.DestroyImmediate(center);
                Object.DestroyImmediate(spine);
                Object.DestroyImmediate(leftFoot);
            }
        }

        [Test]
        public void Given_ApplyIkTargetsEnabled_When_ApplyingProbe_Then_FootIkTargetPositionIsApplied()
        {
            VmdMotionData motion = CreateMotion(frameIndex: 7);
            var parentOfAll = new GameObject("parent-of-all");
            var center = new GameObject("center");
            var leftFoot = new GameObject("left-foot");

            try
            {
                var options = new VmdPlaybackProbeOptions(
                    enabled: true,
                    applyIkTargets: true,
                    useCenterAsParentOfAll: false,
                    routeCenterBoneToGroove: false,
                    centerNameString: VmdUnityTransformConverter.CenterBoneName,
                    grooveNameString: VmdUnityTransformConverter.GrooveBoneName);

                VmdPlaybackApplyResult result = VmdPlaybackProbe.ApplyFrame(
                    motion,
                    frameIndex: 7,
                    humanoidTargets: new Dictionary<HumanBodyBones, Transform>
                    {
                        [HumanBodyBones.LeftFoot] = leftFoot.transform
                    },
                    parentOfAllTarget: parentOfAll.transform,
                    centerTarget: center.transform,
                    options: options);

                Assert.That(result.Status, Is.EqualTo(VmdPlaybackApplyStatus.Applied));
                Assert.That(Vector3.Distance(leftFoot.transform.position, new Vector3(10f, 11f, 12f)), Is.LessThan(0.0001f));
                Assert.That(Quaternion.Angle(leftFoot.transform.localRotation, Quaternion.Euler(40f, 50f, 60f)), Is.LessThan(0.001f));
                Assert.That(result.AppliedIkTargetFrames, Is.EqualTo(1));
                Assert.That(result.SkippedIkTargetFrames, Is.EqualTo(0));
            }
            finally
            {
                Object.DestroyImmediate(parentOfAll);
                Object.DestroyImmediate(center);
                Object.DestroyImmediate(leftFoot);
            }
        }

        [Test]
        public void Given_CenterCarrierMoves_When_ApplyingFootIkTarget_Then_TargetDoesNotReceiveCenterOffset()
        {
            VmdMotionData motion = CreateMotion(frameIndex: 7);
            var parentOfAll = new GameObject("parent-of-all");
            var center = new GameObject("center");
            var leftFoot = new GameObject("left-foot");

            try
            {
                var options = new VmdPlaybackProbeOptions(
                    enabled: true,
                    applyIkTargets: true,
                    useCenterAsParentOfAll: false,
                    routeCenterBoneToGroove: false,
                    centerNameString: VmdUnityTransformConverter.CenterBoneName,
                    grooveNameString: VmdUnityTransformConverter.GrooveBoneName);

                VmdPlaybackApplyResult result = VmdPlaybackProbe.ApplyFrame(
                    motion,
                    frameIndex: 7,
                    humanoidTargets: new Dictionary<HumanBodyBones, Transform>
                    {
                        [HumanBodyBones.LeftFoot] = leftFoot.transform
                    },
                    parentOfAllTarget: parentOfAll.transform,
                    centerTarget: center.transform,
                    options: options);

                Assert.That(result.Status, Is.EqualTo(VmdPlaybackApplyStatus.Applied));
                Assert.That(Vector3.Distance(leftFoot.transform.position, new Vector3(10f, 11f, 12f)), Is.LessThan(0.0001f));
                Assert.That(Vector3.Distance(center.transform.localPosition, new Vector3(-0.25f, 0.5f, 0.75f)), Is.LessThan(0.0001f));
                Assert.That(result.AppliedIkTargetFrames, Is.EqualTo(1));
            }
            finally
            {
                Object.DestroyImmediate(parentOfAll);
                Object.DestroyImmediate(center);
                Object.DestroyImmediate(leftFoot);
            }
        }

        [Test]
        public void Given_IkSourceDiagnostics_When_ApplyingFootIkTarget_Then_UsesSourceWorldPositionInsteadOfExportedOffset()
        {
            VmdMotionData motion = CreateMotion(frameIndex: 7);
            var parentOfAll = new GameObject("parent-of-all");
            var center = new GameObject("center");
            var leftFoot = new GameObject("left-foot");

            try
            {
                var expectedSourceWorldPosition = new Vector3(0.25f, 0.5f, -0.75f);
                var options = new VmdPlaybackProbeOptions(
                    enabled: true,
                    applyIkTargets: true,
                    useCenterAsParentOfAll: false,
                    routeCenterBoneToGroove: false,
                    centerNameString: VmdUnityTransformConverter.CenterBoneName,
                    grooveNameString: VmdUnityTransformConverter.GrooveBoneName);
                var ikSourceWorldPositions = new Dictionary<string, Vector3>
                {
                    ["7|2"] = expectedSourceWorldPosition
                };

                VmdPlaybackApplyResult result = VmdPlaybackProbe.ApplyFrame(
                    motion,
                    frameIndex: 7,
                    humanoidTargets: new Dictionary<HumanBodyBones, Transform>
                    {
                        [HumanBodyBones.LeftFoot] = leftFoot.transform
                    },
                    parentOfAllTarget: parentOfAll.transform,
                    centerTarget: center.transform,
                    options: options,
                    carrierReference: VmdPlaybackCarrierReference.Empty,
                    ikSourceWorldPositions: ikSourceWorldPositions);

                Assert.That(result.Status, Is.EqualTo(VmdPlaybackApplyStatus.Applied));
                Assert.That(Vector3.Distance(leftFoot.transform.position, expectedSourceWorldPosition), Is.LessThan(0.0001f));
                Assert.That(result.AppliedIkTargetFrames, Is.EqualTo(1));
            }
            finally
            {
                Object.DestroyImmediate(parentOfAll);
                Object.DestroyImmediate(center);
                Object.DestroyImmediate(leftFoot);
            }
        }

        [Test]
        public void Given_CarrierReference_When_ApplyingLaterFrame_Then_ReplaysDeltaFromInitialPose()
        {
            VmdMotionData motion = CreateCarrierMotion(firstFrameIndex: 0, laterFrameIndex: 7);
            var parentOfAll = new GameObject("parent-of-all");
            var center = new GameObject("center");

            try
            {
                parentOfAll.transform.localPosition = new Vector3(0f, 10f, 0f);
                center.transform.localPosition = new Vector3(0f, 20f, 0f);

                VmdPlaybackCarrierReference carrierReference = VmdPlaybackProbe.CaptureCarrierReference(
                    motion,
                    frameIndex: 0,
                    parentOfAllTarget: parentOfAll.transform,
                    centerTarget: center.transform,
                    options: VmdPlaybackProbeOptions.DefaultEnabled);

                VmdPlaybackApplyResult result = VmdPlaybackProbe.ApplyFrame(
                    motion,
                    frameIndex: 7,
                    humanoidTargets: new Dictionary<HumanBodyBones, Transform>(),
                    parentOfAllTarget: parentOfAll.transform,
                    centerTarget: center.transform,
                    options: VmdPlaybackProbeOptions.DefaultEnabled,
                    carrierReference: carrierReference);

                Assert.That(result.Status, Is.EqualTo(VmdPlaybackApplyStatus.Applied));
                Assert.That(Vector3.Distance(parentOfAll.transform.localPosition, new Vector3(0f, 10.25f, 0f)), Is.LessThan(0.0001f));
                Assert.That(Vector3.Distance(center.transform.localPosition, new Vector3(0f, 20.1f, 0f)), Is.LessThan(0.0001f));
                Assert.That(result.AppliedCarrierPositions, Is.EqualTo(2));
            }
            finally
            {
                Object.DestroyImmediate(parentOfAll);
                Object.DestroyImmediate(center);
            }
        }

        [Test]
        public void Given_LockedParentCarrier_When_ApplyingLaterFrame_Then_KeepsRootAtInitialPoseAndReplaysCenterDelta()
        {
            VmdMotionData motion = CreateCarrierMotion(firstFrameIndex: 0, laterFrameIndex: 7);
            var parentOfAll = new GameObject("parent-of-all");
            var center = new GameObject("center");

            try
            {
                Vector3 initialParentPosition = new Vector3(0f, 10f, 0f);
                parentOfAll.transform.localPosition = initialParentPosition;
                center.transform.localPosition = new Vector3(0f, 20f, 0f);

                var options = new VmdPlaybackProbeOptions(
                    enabled: true,
                    applyIkTargets: false,
                    useCenterAsParentOfAll: false,
                    routeCenterBoneToGroove: false,
                    centerNameString: VmdUnityTransformConverter.CenterBoneName,
                    grooveNameString: VmdUnityTransformConverter.GrooveBoneName,
                    anchorCarrierPositionsToInitialPose: true,
                    lockParentOfAllPosition: true);

                VmdPlaybackCarrierReference carrierReference = VmdPlaybackProbe.CaptureCarrierReference(
                    motion,
                    frameIndex: 0,
                    parentOfAllTarget: parentOfAll.transform,
                    centerTarget: center.transform,
                    options: options);

                VmdPlaybackApplyResult result = VmdPlaybackProbe.ApplyFrame(
                    motion,
                    frameIndex: 7,
                    humanoidTargets: new Dictionary<HumanBodyBones, Transform>(),
                    parentOfAllTarget: parentOfAll.transform,
                    centerTarget: center.transform,
                    options: options,
                    carrierReference: carrierReference);

                Assert.That(result.Status, Is.EqualTo(VmdPlaybackApplyStatus.Applied));
                Assert.That(Vector3.Distance(parentOfAll.transform.localPosition, initialParentPosition), Is.LessThan(0.0001f));
                Assert.That(Vector3.Distance(center.transform.localPosition, new Vector3(0f, 20.1f, 0f)), Is.LessThan(0.0001f));
                Assert.That(result.AppliedCarrierPositions, Is.EqualTo(2));
            }
            finally
            {
                Object.DestroyImmediate(parentOfAll);
                Object.DestroyImmediate(center);
            }
        }

        [Test]
        public void Given_ExplicitLockedParentCarrier_When_ApplyingLaterFrame_Then_KeepsRootAtConfiguredPosition()
        {
            VmdMotionData motion = CreateCarrierMotion(firstFrameIndex: 0, laterFrameIndex: 7);
            var parentOfAll = new GameObject("parent-of-all");
            var center = new GameObject("center");

            try
            {
                parentOfAll.transform.localPosition = new Vector3(0f, 10f, 0f);
                center.transform.localPosition = new Vector3(0f, 20f, 0f);

                var options = new VmdPlaybackProbeOptions(
                    enabled: true,
                    applyIkTargets: false,
                    useCenterAsParentOfAll: false,
                    routeCenterBoneToGroove: false,
                    centerNameString: VmdUnityTransformConverter.CenterBoneName,
                    grooveNameString: VmdUnityTransformConverter.GrooveBoneName,
                    anchorCarrierPositionsToInitialPose: true,
                    lockParentOfAllPosition: true,
                    useExplicitParentOfAllLockPosition: true,
                    parentOfAllLockPosition: Vector3.zero);

                VmdPlaybackCarrierReference carrierReference = VmdPlaybackProbe.CaptureCarrierReference(
                    motion,
                    frameIndex: 0,
                    parentOfAllTarget: parentOfAll.transform,
                    centerTarget: center.transform,
                    options: options);

                VmdPlaybackApplyResult result = VmdPlaybackProbe.ApplyFrame(
                    motion,
                    frameIndex: 7,
                    humanoidTargets: new Dictionary<HumanBodyBones, Transform>(),
                    parentOfAllTarget: parentOfAll.transform,
                    centerTarget: center.transform,
                    options: options,
                    carrierReference: carrierReference);

                Assert.That(result.Status, Is.EqualTo(VmdPlaybackApplyStatus.Applied));
                Assert.That(Vector3.Distance(parentOfAll.transform.localPosition, Vector3.zero), Is.LessThan(0.0001f));
                Assert.That(Vector3.Distance(center.transform.localPosition, new Vector3(0f, 20.1f, 0f)), Is.LessThan(0.0001f));
                Assert.That(result.AppliedCarrierPositions, Is.EqualTo(2));
            }
            finally
            {
                Object.DestroyImmediate(parentOfAll);
                Object.DestroyImmediate(center);
            }
        }

        private static VmdMotionData CreateMotion(uint frameIndex)
        {
            var frames = new List<VmdBoneFrame>
            {
                new VmdBoneFrame(
                    "\u5168\u3066\u306e\u89aa",
                    frameIndex,
                    VmdUnityTransformConverter.ConvertUnityMetersToVmdPosition(new Vector3(1f, 2f, 3f)),
                    Quaternion.identity,
                    new byte[64]),
                new VmdBoneFrame(
                    "\u30bb\u30f3\u30bf\u30fc",
                    frameIndex,
                    VmdUnityTransformConverter.ConvertUnityMetersToVmdPosition(new Vector3(-0.25f, 0.5f, 0.75f)),
                    Quaternion.identity,
                    new byte[64]),
                new VmdBoneFrame(
                    "\u4e0a\u534a\u8eab",
                    frameIndex,
                    VmdUnityTransformConverter.ConvertUnityMetersToVmdPosition(new Vector3(8f, 8f, 8f)),
                    VmdUnityTransformConverter.ConvertUnityRotationToVmdRotation(Quaternion.Euler(10f, 20f, 30f)),
                    new byte[64]),
                new VmdBoneFrame(
                    "\u5de6\u8db3\uff29\uff2b",
                    frameIndex,
                    VmdUnityTransformConverter.ConvertUnityMetersToVmdPosition(new Vector3(9f, 9f, 9f)),
                    VmdUnityTransformConverter.ConvertUnityRotationToVmdRotation(Quaternion.Euler(40f, 50f, 60f)),
                    new byte[64])
            };
            var morphs = new List<VmdMorphFrame>
            {
                new VmdMorphFrame("blink", frameIndex, 1f)
            };

            return new VmdMotionData(
                "Vocaloid Motion Data 0002",
                "probeModel",
                frames,
                morphs,
                cameraFrameCount: 0,
                lightFrameCount: 0,
                selfShadowFrameCount: 0,
                ikFrameCount: 0);
        }

        private static VmdMotionData CreateCarrierMotion(uint firstFrameIndex, uint laterFrameIndex)
        {
            var frames = new List<VmdBoneFrame>
            {
                new VmdBoneFrame(
                    "\u5168\u3066\u306e\u89aa",
                    firstFrameIndex,
                    VmdUnityTransformConverter.ConvertUnityMetersToVmdPosition(new Vector3(1f, 2f, 3f)),
                    Quaternion.identity,
                    new byte[64]),
                new VmdBoneFrame(
                    "\u30bb\u30f3\u30bf\u30fc",
                    firstFrameIndex,
                    VmdUnityTransformConverter.ConvertUnityMetersToVmdPosition(new Vector3(-0.25f, 0.5f, 0.75f)),
                    Quaternion.identity,
                    new byte[64]),
                new VmdBoneFrame(
                    "\u5168\u3066\u306e\u89aa",
                    laterFrameIndex,
                    VmdUnityTransformConverter.ConvertUnityMetersToVmdPosition(new Vector3(1f, 2.25f, 3f)),
                    Quaternion.identity,
                    new byte[64]),
                new VmdBoneFrame(
                    "\u30bb\u30f3\u30bf\u30fc",
                    laterFrameIndex,
                    VmdUnityTransformConverter.ConvertUnityMetersToVmdPosition(new Vector3(-0.25f, 0.6f, 0.75f)),
                    Quaternion.identity,
                    new byte[64])
            };

            return new VmdMotionData(
                "Vocaloid Motion Data 0002",
                "carrierModel",
                frames,
                new List<VmdMorphFrame>(),
                cameraFrameCount: 0,
                lightFrameCount: 0,
                selfShadowFrameCount: 0,
                ikFrameCount: 0);
        }
    }
}
