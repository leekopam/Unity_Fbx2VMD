using NUnit.Framework;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using UnityEngine;

using BoneNames = UnityHumanoidVMDRecorder.BoneNames;

namespace Tests.Editor.VMDRecorderSample
{
    public class VmdFileWriterTests
    {
        [Test]
        public void Given_MinimalFrames_When_WritingVmd_Then_HeaderAndKeyframeCountMatch()
        {
            List<BoneNames> allBones = Enum
                .GetValues(typeof(BoneNames))
                .Cast<BoneNames>()
                .Where(b => b != BoneNames.None && (int)b > 5)
                .ToList();

            Assume.That(allBones.Count >= 2, "Need at least 2 bones for this test");

            List<BoneNames> activeBones = allBones.Take(2).ToList();

            int frameCount = 5;
            int keyReductionLevel = 2; // Humanoid character VMDs must ignore sparse export.
            uint expectedKeyframeCount = (uint)(activeBones.Count * frameCount);

            var positions = new Dictionary<BoneNames, List<Vector3>>();
            var rotations = new Dictionary<BoneNames, List<Quaternion>>();

            foreach (BoneNames bone in activeBones)
            {
                var p = new List<Vector3>(frameCount);
                var r = new List<Quaternion>(frameCount);
                for (int i = 0; i < frameCount; i++)
                {
                    p.Add(new Vector3(i, i * 2, i * 3));
                    r.Add(Quaternion.identity);
                }

                positions.Add(bone, p);
                rotations.Add(bone, r);
            }

            string tempDir = Path.Combine(Path.GetTempPath(), "fbx2vmd-tests");
            Directory.CreateDirectory(tempDir);
            string filePath = Path.Combine(tempDir, $"vmd_writer_{Guid.NewGuid():N}.vmd");

            try
            {
                VmdFileWriter.WriteVmdFile(
                    modelName: "testModel",
                    filePath: filePath,
                    activeBones: activeBones,
                    frameCount: frameCount,
                    keyReductionLevel: keyReductionLevel,
                    positionDictionarySaved: positions,
                    rotationDictionarySaved: rotations,
                    morphSnapshot: null,
                    useCenterAsParentOfAll: false,
                    routeCenterBoneToGroove: false,
                    centerNameString: "CENTER",
                    grooveNameString: "GROOVE");

                byte[] bytes = File.ReadAllBytes(filePath);

                byte[] signature = System.Text.Encoding.ASCII.GetBytes("Vocaloid Motion Data 0002");
                Assert.GreaterOrEqual(bytes.Length, 30 + 20 + 4, "VMD file must contain header + keyframe count");
                CollectionAssert.AreEqual(signature, bytes.Take(signature.Length).ToArray(), "VMD signature mismatch");

                int keyFrameCountOffset = 30 + 20;
                uint actualKeyframeCount = BitConverter.ToUInt32(bytes, keyFrameCountOffset);
                Assert.AreEqual(expectedKeyframeCount, actualKeyframeCount, "Bone keyframe count mismatch");
            }
            finally
            {
                if (File.Exists(filePath))
                {
                    File.Delete(filePath);
                }
            }
        }

        [Test]
        public void Given_KeyReductionDoesNotDivideFrameCount_When_WritingVmd_Then_AllFramesAreWritten()
        {
            List<BoneNames> activeBones = Enum
                .GetValues(typeof(BoneNames))
                .Cast<BoneNames>()
                .Where(b => b != BoneNames.None && (int)b > 5)
                .Take(1)
                .ToList();

            Assume.That(activeBones.Count, Is.EqualTo(1), "Need one active bone for this test");

            int frameCount = 5;
            int keyReductionLevel = 3; // Humanoid character VMDs still write every frame.
            var positions = new Dictionary<BoneNames, List<Vector3>>();
            var rotations = new Dictionary<BoneNames, List<Quaternion>>();

            foreach (BoneNames bone in activeBones)
            {
                var p = new List<Vector3>(frameCount);
                var r = new List<Quaternion>(frameCount);
                for (int i = 0; i < frameCount; i++)
                {
                    p.Add(new Vector3(i, i * 2, i * 3));
                    r.Add(Quaternion.identity);
                }

                positions.Add(bone, p);
                rotations.Add(bone, r);
            }

            string tempDir = Path.Combine(Path.GetTempPath(), "fbx2vmd-tests");
            Directory.CreateDirectory(tempDir);
            string filePath = Path.Combine(tempDir, $"vmd_writer_last_frame_{Guid.NewGuid():N}.vmd");

            try
            {
                VmdFileWriter.WriteVmdFile(
                    modelName: "testModel",
                    filePath: filePath,
                    activeBones: activeBones,
                    frameCount: frameCount,
                    keyReductionLevel: keyReductionLevel,
                    positionDictionarySaved: positions,
                    rotationDictionarySaved: rotations,
                    morphSnapshot: null,
                    useCenterAsParentOfAll: false,
                    routeCenterBoneToGroove: false,
                    centerNameString: "CENTER",
                    grooveNameString: "GROOVE");

                byte[] bytes = File.ReadAllBytes(filePath);
                int keyFrameCountOffset = 30 + 20;
                uint keyFrameCount = BitConverter.ToUInt32(bytes, keyFrameCountOffset);
                Assert.That(keyFrameCount, Is.EqualTo(5), "Humanoid character VMD export must keep every recorded frame.");

                int firstBoneFrameOffset = keyFrameCountOffset + 4;
                const int boneFrameSize = 111;
                uint finalFrame = BitConverter.ToUInt32(bytes, firstBoneFrameOffset + (boneFrameSize * 4) + 15);
                Assert.That(finalFrame, Is.EqualTo((uint)(frameCount - 1)), "The final VMD key must reach the recorded clip end.");
            }
            finally
            {
                if (File.Exists(filePath))
                {
                    File.Delete(filePath);
                }
            }
        }

        [Test]
        public void Given_CenterAsParentWithoutGrooveRouting_When_WritingVmd_Then_HumanoidCenterStaysOnCenterBone()
        {
            List<BoneNames> activeBones = new List<BoneNames>
            {
                BoneNames.全ての親,
                BoneNames.センター
            };

            int frameCount = 2;
            var positions = new Dictionary<BoneNames, List<Vector3>>
            {
                [BoneNames.全ての親] = new List<Vector3> { Vector3.zero, new Vector3(0f, 1f, 0f) },
                [BoneNames.センター] = new List<Vector3> { new Vector3(10f, 0f, 20f), new Vector3(11f, 0f, 21f) }
            };
            var rotations = new Dictionary<BoneNames, List<Quaternion>>
            {
                [BoneNames.全ての親] = new List<Quaternion> { Quaternion.identity, Quaternion.identity },
                [BoneNames.センター] = new List<Quaternion> { Quaternion.identity, Quaternion.identity }
            };

            string tempDir = Path.Combine(Path.GetTempPath(), "fbx2vmd-tests");
            Directory.CreateDirectory(tempDir);
            string filePath = Path.Combine(tempDir, $"vmd_writer_center_route_{Guid.NewGuid():N}.vmd");

            try
            {
                VmdFileWriter.WriteVmdFile(
                    modelName: "testModel",
                    filePath: filePath,
                    activeBones: activeBones,
                    frameCount: frameCount,
                    keyReductionLevel: 1,
                    positionDictionarySaved: positions,
                    rotationDictionarySaved: rotations,
                    morphSnapshot: null,
                    useCenterAsParentOfAll: true,
                    centerNameString: "センター",
                    grooveNameString: "グルーブ",
                    routeCenterBoneToGroove: false);

                byte[] bytes = File.ReadAllBytes(filePath);
                int firstBoneFrameOffset = 30 + 20 + 4;
                const int boneFrameSize = 111;
                string firstName = ReadShiftJisBoneName(bytes, firstBoneFrameOffset);
                string secondName = ReadShiftJisBoneName(bytes, firstBoneFrameOffset + boneFrameSize);

                Assert.That(firstName, Is.EqualTo("全ての親"), "The parent/global bone should not overwrite MMD center when groove routing is disabled.");
                Assert.That(secondName, Is.EqualTo("センター"), "Humanoid center translation must stay on センター for MMD models without グルーブ.");
            }
            finally
            {
                if (File.Exists(filePath))
                {
                    File.Delete(filePath);
                }
            }
        }

        [Test]
        public void Given_ReducedExport_When_WritingMotionCarrierBones_Then_RootCenterAndIkKeepEveryFrame()
        {
            List<BoneNames> activeBones = new List<BoneNames>
            {
                BoneNames.全ての親,
                BoneNames.センター,
                BoneNames.左足ＩＫ,
                BoneNames.右足ＩＫ,
                BoneNames.左つま先ＩＫ,
                BoneNames.右つま先ＩＫ,
                BoneNames.上半身
            };

            int frameCount = 5;
            int keyReductionLevel = 3;
            var positions = new Dictionary<BoneNames, List<Vector3>>();
            var rotations = new Dictionary<BoneNames, List<Quaternion>>();

            foreach (BoneNames bone in activeBones)
            {
                var p = new List<Vector3>(frameCount);
                var r = new List<Quaternion>(frameCount);
                for (int i = 0; i < frameCount; i++)
                {
                    p.Add(new Vector3(i, i * 0.1f, -i));
                    r.Add(Quaternion.identity);
                }

                positions.Add(bone, p);
                rotations.Add(bone, r);
            }

            string tempDir = Path.Combine(Path.GetTempPath(), "fbx2vmd-tests");
            Directory.CreateDirectory(tempDir);
            string filePath = Path.Combine(tempDir, $"vmd_writer_motion_carriers_{Guid.NewGuid():N}.vmd");

            try
            {
                VmdFileWriter.WriteVmdFile(
                    modelName: "testModel",
                    filePath: filePath,
                    activeBones: activeBones,
                    frameCount: frameCount,
                    keyReductionLevel: keyReductionLevel,
                    positionDictionarySaved: positions,
                    rotationDictionarySaved: rotations,
                    morphSnapshot: null,
                    useCenterAsParentOfAll: true,
                    routeCenterBoneToGroove: false,
                    centerNameString: "センター",
                    grooveNameString: "グルーブ");

                byte[] bytes = File.ReadAllBytes(filePath);
                List<ParsedBoneFrame> frames = ReadBoneFrames(bytes);
                /*

                Assert.That(frames.Count(frame => frame.Name == "全ての親"), Is.EqualTo(frameCount));
                Assert.That(frames.Count(frame => frame.Name == "センター"), Is.EqualTo(frameCount));
                Assert.That(frames.Count(frame => frame.Name == "左足ＩＫ"), Is.EqualTo(frameCount));
                Assert.That(frames.Count(frame => frame.Name == "右足ＩＫ"), Is.EqualTo(frameCount));
                Assert.That(frames.Count(frame => frame.Name == "左つま先ＩＫ"), Is.EqualTo(frameCount));
                Assert.That(frames.Count(frame => frame.Name == "右つま先ＩＫ"), Is.EqualTo(frameCount));
                Assert.That(frames.Count(frame => frame.Name == "上半身"), Is.EqualTo(3), "Non-carrier bones may still use reduced keys 0, 3, and final 4.");
                CollectionAssert.AreEqual(new[] { 0u, 1u, 2u, 3u, 4u }, frames.Where(frame => frame.Name == "センター").Select(frame => frame.Frame).ToArray());
                */
                /*
                string[] carrierNames =
                {
                    "?ⓦ겍??┴",
                    "?삠꺍?욍꺖",
                    "藥?떨竊⑼섐",
                    "?녘떨竊⑼섐",
                    "藥╉겇?얍뀍竊⑼섐",
                    "?녈겇?얍뀍竊⑼섐"
                };
                foreach (string carrierName in carrierNames)
                {
                    Assert.That(frames.Count(frame => frame.Name == carrierName), Is.EqualTo(frameCount));
                }

                string nonCarrierName = frames.Select(frame => frame.Name)
                    .First(frameName => !carrierNames.Contains(frameName));
                Assert.That(frames.Count(frame => frame.Name == nonCarrierName), Is.EqualTo(frameCount),
                    "Non-carrier bones must also keep every recorded frame in humanoid character VMD export.");
                CollectionAssert.AreEqual(new[] { 0u, 1u, 2u, 3u, 4u },
                    frames.Where(frame => frame.Name == "?삠꺍?욍꺖").Select(frame => frame.Frame).ToArray());
                */
                uint[] expectedFrames = { 0u, 1u, 2u, 3u, 4u };
                var frameGroups = frames.GroupBy(frame => frame.Name).ToArray();
                Assert.That(frameGroups.Length, Is.EqualTo(activeBones.Count));
                foreach (var frameGroup in frameGroups)
                {
                    Assert.That(frameGroup.Count(), Is.EqualTo(frameCount),
                        "Humanoid character VMD export must keep every recorded frame for every bone.");
                    CollectionAssert.AreEqual(expectedFrames, frameGroup.Select(frame => frame.Frame).ToArray());
                }
            }
            finally
            {
                if (File.Exists(filePath))
                {
                    File.Delete(filePath);
                }
            }
        }

        [Test]
        public void Given_BoneFrames_When_WritingVmd_Then_InterpolationIsLinearNotZeroed()
        {
            List<BoneNames> activeBones = new List<BoneNames> { BoneNames.センター };
            int frameCount = 2;
            var positions = new Dictionary<BoneNames, List<Vector3>>
            {
                [BoneNames.センター] = new List<Vector3> { Vector3.zero, Vector3.one }
            };
            var rotations = new Dictionary<BoneNames, List<Quaternion>>
            {
                [BoneNames.センター] = new List<Quaternion> { Quaternion.identity, Quaternion.identity }
            };

            string tempDir = Path.Combine(Path.GetTempPath(), "fbx2vmd-tests");
            Directory.CreateDirectory(tempDir);
            string filePath = Path.Combine(tempDir, $"vmd_writer_interpolation_{Guid.NewGuid():N}.vmd");

            try
            {
                VmdFileWriter.WriteVmdFile(
                    modelName: "testModel",
                    filePath: filePath,
                    activeBones: activeBones,
                    frameCount: frameCount,
                    keyReductionLevel: 1,
                    positionDictionarySaved: positions,
                    rotationDictionarySaved: rotations,
                    morphSnapshot: null,
                    useCenterAsParentOfAll: true,
                    routeCenterBoneToGroove: false,
                    centerNameString: "センター",
                    grooveNameString: "グルーブ");

                byte[] bytes = File.ReadAllBytes(filePath);
                int firstBoneFrameOffset = 30 + 20 + 4;
                byte[] interpolation = bytes.Skip(firstBoneFrameOffset + 47).Take(64).ToArray();
                byte[] expectedPrefix =
                {
                    20, 20, 20, 20,
                    20, 20, 20, 20,
                    107, 107, 107, 107,
                    107, 107, 107, 107
                };

                CollectionAssert.AreEqual(expectedPrefix, interpolation.Take(expectedPrefix.Length).ToArray());
                Assert.That(interpolation.Any(value => value != 0), Is.True, "All-zero interpolation makes MMD playback visibly stepped on some bones.");
            }
            finally
            {
                if (File.Exists(filePath))
                {
                    File.Delete(filePath);
                }
            }
        }

        [Test]
        public void Given_BottomCenter_When_HipsTilts_Then_CenterKeepsHipsHorizontalPosition()
        {
            var root = new GameObject("root");
            var hips = new GameObject("hips");
            var animatorObject = new GameObject("animator");

            try
            {
                animatorObject.AddComponent<Animator>();
                root.transform.position = Vector3.zero;
                hips.transform.position = Vector3.up;

                var bones = new Dictionary<BoneNames, Transform>
                {
                    [BoneNames.全ての親] = root.transform,
                    [BoneNames.センター] = hips.transform
                };
                var ghost = new VmdBoneGhost(animatorObject.GetComponent<Animator>(), bones, useBottomCenter: true);

                hips.transform.position = new Vector3(0f, 1f, 0f);
                hips.transform.rotation = Quaternion.Euler(0f, 0f, 60f);

                ghost.GhostAll();
                Vector3 center = ghost.GhostDictionary[BoneNames.センター].ghost.position;

                Assert.That(center.x, Is.EqualTo(hips.transform.position.x).Within(0.0001f),
                    "Bottom-center export must not convert torso tilt into center X/Z travel.");
                Assert.That(center.z, Is.EqualTo(hips.transform.position.z).Within(0.0001f),
                    "Bottom-center export must keep horizontal center motion from the source hips/root only.");
                Assert.That(center.y, Is.EqualTo(0f).Within(0.0001f));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(root);
                UnityEngine.Object.DestroyImmediate(hips);
                UnityEngine.Object.DestroyImmediate(animatorObject);
            }
        }

        [Test]
        public void Given_MmdFootIkExportFloorGuard_When_PositionIsBelowFloor_Then_YIsClamped()
        {
            Vector3 belowFloor = new Vector3(1.25f, -0.35f, -2.5f);

            Vector3 clamped = UnityHumanoidVMDRecorder.ClampMmdFootIkPositionForExport(
                belowFloor,
                enabled: true,
                minY: 0f);

            Assert.That(clamped.x, Is.EqualTo(belowFloor.x));
            Assert.That(clamped.y, Is.EqualTo(0f));
            Assert.That(clamped.z, Is.EqualTo(belowFloor.z));
        }

        [Test]
        public void Given_MmdFootIkExportFloorGuardDisabled_When_PositionIsBelowFloor_Then_YIsPreserved()
        {
            Vector3 belowFloor = new Vector3(1.25f, -0.35f, -2.5f);

            Vector3 unclamped = UnityHumanoidVMDRecorder.ClampMmdFootIkPositionForExport(
                belowFloor,
                enabled: false,
                minY: 0f);

            Assert.That(unclamped, Is.EqualTo(belowFloor));
        }

        [Test]
        public void Given_DefaultRecorderSettings_When_MmdFloorGuardExists_Then_AllExportOffsetsStayNeutral()
        {
            var gameObject = new GameObject("recorder-defaults-test");
            try
            {
                var recorder = gameObject.AddComponent<UnityHumanoidVMDRecorder>();

                Assert.That(recorder.ParentOfAllOffset, Is.EqualTo(Vector3.zero), "MMD floor guard must not move the whole model/root.");
                Assert.That(recorder.MmdFootIkExportOffset, Is.EqualTo(Vector3.zero), "Default recorders, including testPrefab, must not receive YYB-specific foot lift implicitly.");
                Assert.That(recorder.ClampMmdFootIkYToFloor, Is.False, "Default recorders must opt in to MMD floor guard explicitly.");
                Assert.That(recorder.LiftMmdCenterYToKeepFeetAboveFloor, Is.False, "Default recorders must opt in to center/root floor lift explicitly.");
                Assert.That(recorder.FreezeParentOfAllMotionWhenIgnoringInitialPosition, Is.False, "Default recorders must keep the post-reference-video freeze path opt-in.");
                Assert.That(recorder.ClampMmdCenterExportDeltaSpikes, Is.False, "Default recorders must keep the post-reference-video center clamp path opt-in.");
                Assert.That(recorder.MaxMmdCenterExportDeltaPerFrame, Is.LessThanOrEqualTo(0.12f), "MMD center guard must stay stricter than the retarget root-motion spike clamp.");
                Assert.That(recorder.ClampMmdIkExportDeltaSpikes, Is.False, "Default recorders must not alter foot/toe IK targets in the center/root-only floor correction path.");
                Assert.That(recorder.MaxMmdFootIkExportDeltaPerFrame, Is.LessThanOrEqualTo(0.12f), "MMD foot IK movement must stay within the same no-teleport threshold as center export.");
                Assert.That(recorder.MaxMmdToeIkExportDeltaPerFrame, Is.LessThanOrEqualTo(0.12f), "MMD toe IK movement must stay within the same no-teleport threshold as center export.");
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(gameObject);
            }
        }

        [Test]
        public void Given_FootIkEffectiveYBelowFloor_When_ApplyingCenterFloorLift_Then_OnlyCenterYIsRaised()
        {
            var centerPositions = new List<Vector3>
            {
                new Vector3(0.2f, 0.1f, -0.4f),
                new Vector3(0.3f, 0.1f, -0.5f)
            };
            var leftFootPositions = new List<Vector3>
            {
                new Vector3(1f, -0.05f, 2f),
                new Vector3(1f, -0.3f, 2f)
            };
            var rightFootPositions = new List<Vector3>
            {
                new Vector3(-1f, 0.2f, -2f),
                new Vector3(-1f, 0.25f, -2f)
            };
            var originalLeftFoot = new List<Vector3>(leftFootPositions);
            var originalRightFoot = new List<Vector3>(rightFootPositions);

            int lifted = UnityHumanoidVMDRecorder.ApplyMmdCenterFloorLiftFromIkPositions(
                centerPositions,
                leftFootPositions,
                rightFootPositions,
                leftToeIkPositions: null,
                rightToeIkPositions: null,
                safeFrameCount: centerPositions.Count,
                minY: 0f,
                maxCenterDeltaPerFrame: float.PositiveInfinity,
                out float minBefore,
                out float minAfter,
                out float maxCenterLift);

            Assert.That(lifted, Is.EqualTo(1));
            Assert.That(minBefore, Is.EqualTo(-0.2f).Within(0.0001f));
            Assert.That(minAfter, Is.EqualTo(0.001f).Within(0.0001f));
            Assert.That(maxCenterLift, Is.EqualTo(0.201f).Within(0.0001f));
            Assert.That(centerPositions[1].x, Is.EqualTo(0.3f).Within(0.0001f));
            Assert.That(centerPositions[1].y, Is.EqualTo(0.301f).Within(0.0001f));
            Assert.That(centerPositions[1].z, Is.EqualTo(-0.5f).Within(0.0001f));
            Assert.That(leftFootPositions, Is.EqualTo(originalLeftFoot), "Foot IK keys must remain unchanged.");
            Assert.That(rightFootPositions, Is.EqualTo(originalRightFoot), "Foot IK keys must remain unchanged.");
        }

        [Test]
        public void Given_FootIkFloorLiftSpike_When_ApplyingCenterFloorLift_Then_CenterYDeltaIsSmoothedWithoutMovingFeet()
        {
            var centerPositions = new List<Vector3>
            {
                new Vector3(0f, 0.1f, 0f),
                new Vector3(0f, 0.1f, 0f),
                new Vector3(0f, 0.1f, 0f)
            };
            var leftFootPositions = new List<Vector3>
            {
                new Vector3(1f, 0f, 2f),
                new Vector3(1f, -0.3f, 2f),
                new Vector3(1f, 0f, 2f)
            };
            var originalLeftFoot = new List<Vector3>(leftFootPositions);

            int lifted = UnityHumanoidVMDRecorder.ApplyMmdCenterFloorLiftFromIkPositions(
                centerPositions,
                leftFootPositions,
                rightFootIkPositions: null,
                leftToeIkPositions: null,
                rightToeIkPositions: null,
                safeFrameCount: centerPositions.Count,
                minY: 0f,
                maxCenterDeltaPerFrame: 0.12f,
                out float minBefore,
                out float minAfter,
                out float maxCenterLift);

            Assert.That(lifted, Is.EqualTo(3));
            Assert.That(minBefore, Is.EqualTo(-0.2f).Within(0.0001f));
            Assert.That(minAfter, Is.EqualTo(0.001f).Within(0.0001f));
            Assert.That(maxCenterLift, Is.EqualTo(0.201f).Within(0.0001f));
            Assert.That(Mathf.Abs(centerPositions[1].y - centerPositions[0].y), Is.LessThanOrEqualTo(0.119f + 0.0001f));
            Assert.That(Mathf.Abs(centerPositions[2].y - centerPositions[1].y), Is.LessThanOrEqualTo(0.119f + 0.0001f));
            Assert.That(centerPositions[0].y, Is.EqualTo(0.182f).Within(0.0001f));
            Assert.That(centerPositions[1].y, Is.EqualTo(0.301f).Within(0.0001f));
            Assert.That(centerPositions[2].y, Is.EqualTo(0.182f).Within(0.0001f));
            Assert.That(leftFootPositions, Is.EqualTo(originalLeftFoot), "Foot IK keys must remain unchanged.");
        }

        [Test]
        public void Given_FootIkFloorLiftAndCenterXZMotion_When_ApplyingCenterFloorLift_Then_TotalCenterDeltaStaysWithinLimit()
        {
            var centerPositions = new List<Vector3>
            {
                new Vector3(0f, 0.1f, 0f),
                new Vector3(0.06f, 0.1f, 0f),
                new Vector3(0.12f, 0.1f, 0f)
            };
            var leftFootPositions = new List<Vector3>
            {
                new Vector3(1f, 0f, 2f),
                new Vector3(1f, -0.3f, 2f),
                new Vector3(1f, 0f, 2f)
            };
            var originalLeftFoot = new List<Vector3>(leftFootPositions);

            int lifted = UnityHumanoidVMDRecorder.ApplyMmdCenterFloorLiftFromIkPositions(
                centerPositions,
                leftFootPositions,
                rightFootIkPositions: null,
                leftToeIkPositions: null,
                rightToeIkPositions: null,
                safeFrameCount: centerPositions.Count,
                minY: 0f,
                maxCenterDeltaPerFrame: 0.1f,
                out float minBefore,
                out float minAfter,
                out float maxCenterLift);

            Assert.That(lifted, Is.EqualTo(3));
            Assert.That(minBefore, Is.EqualTo(-0.2f).Within(0.0001f));
            Assert.That(minAfter, Is.EqualTo(0.001f).Within(0.0001f));
            Assert.That(maxCenterLift, Is.EqualTo(0.201f).Within(0.0001f));
            Assert.That((centerPositions[1] - centerPositions[0]).magnitude, Is.LessThanOrEqualTo(0.099f + 0.0001f));
            Assert.That((centerPositions[2] - centerPositions[1]).magnitude, Is.LessThanOrEqualTo(0.099f + 0.0001f));
            Assert.That(centerPositions[0].y, Is.EqualTo(0.2222538f).Within(0.0001f));
            Assert.That(centerPositions[1].y, Is.EqualTo(0.301f).Within(0.0001f));
            Assert.That(centerPositions[2].y, Is.EqualTo(0.2222538f).Within(0.0001f));
            Assert.That(leftFootPositions, Is.EqualTo(originalLeftFoot), "Foot IK keys must remain unchanged.");
        }

        [Test]
        public void Given_FootIkExportOffset_When_ApplyingFootGuard_Then_OffsetIsAppliedBeforeClamp()
        {
            Vector3 lifted = UnityHumanoidVMDRecorder.ApplyMmdFootIkExportFloorGuard(
                new Vector3(1.25f, -0.35f, -2.5f),
                new Vector3(0f, 1f, 0f),
                enabled: true,
                minY: 0f);

            Assert.That(lifted, Is.EqualTo(new Vector3(1.25f, 0.65f, -2.5f)));
        }

        [Test]
        public void Given_ToeIkLocalPosition_When_FootIkHasExportOffset_Then_ToeDoesNotReceiveExtraLift()
        {
            Vector3 toeLocal = new Vector3(0.2f, 0.12f, 0.5f);

            Vector3 exportedToeLocal = UnityHumanoidVMDRecorder.ClampMmdFootIkPositionForExport(
                toeLocal,
                enabled: true,
                minY: 0f);

            Assert.That(exportedToeLocal, Is.EqualTo(toeLocal), "Toe IK is written relative to foot IK and must not receive the foot export lift a second time.");
        }

        [Test]
        public void Given_ParentRootBelowFloor_When_FootIkIsLocallyOnFloor_Then_EffectiveYIsClamped()
        {
            Vector3 footLocal = new Vector3(0.2f, 0f, 0.5f);
            float parentRootY = -1f;

            Vector3 exportedFootLocal = UnityHumanoidVMDRecorder.ClampMmdFootIkPositionForEffectiveFloor(
                footLocal,
                parentRootY,
                enabled: true,
                minY: 0f);

            Assert.That(exportedFootLocal.y + parentRootY, Is.EqualTo(0f).Within(0.0001f));
            Assert.That(exportedFootLocal.x, Is.EqualTo(footLocal.x));
            Assert.That(exportedFootLocal.z, Is.EqualTo(footLocal.z));
        }

        [Test]
        public void Given_ToeIkLocalPosition_When_ParentFootIkMakesEffectiveYBelowFloor_Then_ToeEffectiveYIsClamped()
        {
            Vector3 toeLocal = new Vector3(0.2f, -0.15f, 0.5f);
            float parentRootY = 0.1f;
            float parentFootIkY = 0.03f;

            Vector3 exportedToeLocal = UnityHumanoidVMDRecorder.ClampMmdToeIkPositionForEffectiveFloor(
                toeLocal,
                parentRootY,
                parentFootIkY,
                enabled: true,
                minY: 0f);

            Assert.That(exportedToeLocal.y, Is.EqualTo(0f).Within(0.0001f));
            Assert.That(exportedToeLocal.y + parentRootY + parentFootIkY, Is.GreaterThanOrEqualTo(0f));
            Assert.That(exportedToeLocal.x, Is.EqualTo(toeLocal.x));
            Assert.That(exportedToeLocal.z, Is.EqualTo(toeLocal.z));
        }

        [Test]
        public void Given_MmdIkExportDeltaSpike_When_ClampingExportPositions_Then_LimitsEveryFrameStep()
        {
            var positions = new List<Vector3>
            {
                Vector3.zero,
                new Vector3(1f, 0f, 0f),
                new Vector3(1.6f, 0f, 0f)
            };

            int clamped = UnityHumanoidVMDRecorder.ClampMmdIkExportDeltaSpikePositions(
                positions,
                safeFrameCount: positions.Count,
                maxDeltaPerFrame: 0.5f,
                out float maxBefore,
                out float maxAfter);

            Assert.That(clamped, Is.EqualTo(2));
            Assert.That(maxBefore, Is.EqualTo(1f).Within(0.0001f));
        Assert.That(maxAfter, Is.LessThan(0.5f));
        Assert.That(positions[1].x, Is.EqualTo(0.499f).Within(0.0001f));
        Assert.That(positions[2].x, Is.EqualTo(0.998f).Within(0.0001f));
        }

        [Test]
        public void Given_MmdIkExportRecoveryTrigger_When_RawStepIsLarge_Then_UsesRecoveryLimitWithoutExceedingIt()
        {
            var positions = new List<Vector3>
            {
                Vector3.zero,
                new Vector3(0.05f, 0f, 0f),
                new Vector3(0.5f, 0f, 0f),
                new Vector3(0.6f, 0f, 0f)
            };

            MethodInfo method = typeof(UnityHumanoidVMDRecorder).GetMethod(
                "ClampMmdIkExportDeltaSpikePositions",
                BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic,
                null,
                new[]
                {
                    typeof(List<Vector3>),
                    typeof(int),
                    typeof(float),
                    typeof(float),
                    typeof(float),
                    typeof(float).MakeByRefType(),
                    typeof(float).MakeByRefType()
                },
                null);

            Assert.That(method, Is.Not.Null, "IK export clamp needs a conditional recovery limit for large raw foot steps.");

            object[] arguments =
            {
                positions,
                positions.Count,
                0.11f,
                0.12f,
                0.30f,
                0f,
                0f
            };

            int clamped = (int)method.Invoke(null, arguments);
            float maxBefore = (float)arguments[5];
            float maxAfter = (float)arguments[6];

            Assert.That(clamped, Is.EqualTo(2));
            Assert.That(maxBefore, Is.EqualTo(0.45f).Within(0.0001f));
            Assert.That(maxAfter, Is.LessThan(0.12f));
            Assert.That(maxAfter, Is.GreaterThan(0.11f));
            Assert.That(positions[1].x, Is.EqualTo(0.05f).Within(0.0001f));
            Assert.That(positions[2].x, Is.EqualTo(0.169f).Within(0.0001f));
            Assert.That(positions[3].x, Is.EqualTo(0.278f).Within(0.0001f));
        }

        [Test]
        public void Given_MmdIkExportRecoveryDebt_When_LagDebtIsLarge_Then_UsesRecoveryLimitWithoutExceedingIt()
        {
            var positions = new List<Vector3>
            {
                Vector3.zero,
                new Vector3(0.20f, 0f, 0f),
                new Vector3(0.31f, 0f, 0f),
                new Vector3(0.42f, 0f, 0f)
            };

            MethodInfo method = typeof(UnityHumanoidVMDRecorder).GetMethod(
                "ClampMmdIkExportDeltaSpikePositions",
                BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic,
                null,
                new[]
                {
                    typeof(List<Vector3>),
                    typeof(int),
                    typeof(float),
                    typeof(float),
                    typeof(float),
                    typeof(float),
                    typeof(float).MakeByRefType(),
                    typeof(float).MakeByRefType()
                },
                null);

            Assert.That(method, Is.Not.Null, "IK export clamp needs a lag-debt recovery limit for accumulated guard delay.");

            object[] arguments =
            {
                positions,
                positions.Count,
                0.11f,
                0.12f,
                0.30f,
                0.08f,
                0f,
                0f
            };

            int clamped = (int)method.Invoke(null, arguments);
            float maxBefore = (float)arguments[6];
            float maxAfter = (float)arguments[7];

            Assert.That(clamped, Is.EqualTo(3));
            Assert.That(maxBefore, Is.EqualTo(0.20f).Within(0.0001f));
            Assert.That(maxAfter, Is.LessThan(0.12f));
            Assert.That(maxAfter, Is.GreaterThan(0.11f));
            Assert.That(positions[1].x, Is.EqualTo(0.119f).Within(0.0001f));
            Assert.That(positions[2].x, Is.EqualTo(0.238f).Within(0.0001f));
            Assert.That(positions[3].x, Is.EqualTo(0.357f).Within(0.0001f));
        }

        [Test]
        public void Given_MmdCenterExportDeltaSpike_When_ClampingExportPositions_Then_LimitsEveryFrameStep()
        {
            var positions = new List<Vector3>
            {
                Vector3.zero,
                new Vector3(0.3f, 0f, 0f),
                new Vector3(0.6f, 0f, 0f)
            };

            var method = typeof(UnityHumanoidVMDRecorder).GetMethod(
                "ClampMmdCenterExportDeltaSpikePositions",
                BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic);

            Assert.That(method, Is.Not.Null, "MMD center export needs its own delta guard; root/IK cadence alone does not prevent center teleports.");

            object[] arguments =
            {
                positions,
                positions.Count,
                0.12f,
                0f,
                0f
            };

            int clamped = (int)method.Invoke(null, arguments);
            float maxBefore = (float)arguments[3];
            float maxAfter = (float)arguments[4];

            Assert.That(clamped, Is.EqualTo(2));
            Assert.That(maxBefore, Is.EqualTo(0.3f).Within(0.0001f));
            Assert.That(maxAfter, Is.LessThan(0.12f));
            Assert.That(positions[1].x, Is.EqualTo(0.119f).Within(0.0001f));
            Assert.That(positions[2].x, Is.EqualTo(0.238f).Within(0.0001f));
        }

        [Test]
        public void Given_IkClampAndCenterLift_When_ApplyingExportSafetyGuards_Then_ClampRunsBeforeFloorLift()
        {
            var center = (BoneNames)1;
            var leftFootIk = (BoneNames)2;
            var positions = new Dictionary<BoneNames, List<Vector3>>
            {
                [center] = new List<Vector3>
                {
                    Vector3.zero,
                    Vector3.zero
                },
                [leftFootIk] = new List<Vector3>
                {
                    new Vector3(0f, -1f, 0f),
                    Vector3.zero
                }
            };

            MethodInfo method = typeof(UnityHumanoidVMDRecorder).GetMethod(
                "ApplyMmdExportSafetyGuards",
                BindingFlags.Instance | BindingFlags.NonPublic);

            Assert.That(method, Is.Not.Null, "The save path must apply IK clamping before center floor lift so clamped IK keys cannot invalidate the floor guard.");

            var gameObject = new GameObject("mmd-export-guard-order-test");
            try
            {
                var recorder = gameObject.AddComponent<UnityHumanoidVMDRecorder>();
                recorder.LiftMmdCenterYToKeepFeetAboveFloor = true;
                recorder.ClampMmdCenterExportDeltaSpikes = true;
                recorder.MaxMmdCenterExportDeltaPerFrame = 10f;
                recorder.ClampMmdIkExportDeltaSpikes = true;
                recorder.MaxMmdFootIkExportDeltaPerFrame = 0.12f;
                recorder.MinMmdFootIkY = 0f;

                typeof(UnityHumanoidVMDRecorder)
                    .GetField("positionDictionarySaved", BindingFlags.Instance | BindingFlags.NonPublic)
                    .SetValue(recorder, positions);

                method.Invoke(recorder, new object[] { 2 });

                float effectiveFootY = positions[center][1].y + positions[leftFootIk][1].y;
                Assert.That(recorder.LastMmdIkExportMaxDeltaAfter, Is.LessThan(0.12f));
                Assert.That(effectiveFootY, Is.GreaterThanOrEqualTo(0.001f - 0.0001f));
                Assert.That(recorder.LastMmdCenterFloorLiftMinEffectiveYAfter, Is.GreaterThanOrEqualTo(0.001f - 0.0001f));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(gameObject);
            }
        }

        [Test]
        public void Given_NeckAndHeadBones_When_WritingVmd_Then_ExportNamesFollowEnumBoneIdentity()
        {
            var neck = (BoneNames)Enum.Parse(typeof(BoneNames), "\u9996");
            var head = (BoneNames)Enum.Parse(typeof(BoneNames), "\u982d");
            var activeBones = new List<BoneNames> { neck, head };
            string filePath = Path.Combine(Path.GetTempPath(), $"vmd_writer_neck_head_{Guid.NewGuid():N}.vmd");

            try
            {
                WriteOneFrameVmd(filePath, activeBones);

                List<ParsedBoneFrame> frames = ReadBoneFrames(File.ReadAllBytes(filePath));

                Assert.That(frames.Select(frame => frame.Name).ToArray(), Is.EqualTo(new[] { "\u9996", "\u982d" }));
            }
            finally
            {
                if (File.Exists(filePath))
                {
                    File.Delete(filePath);
                }
            }
        }

        [Test]
        public void Given_MmdLowerBodyBone_When_WritingVmd_Then_HipsRotationCarrierCanBeExported()
        {
            Assert.That(
                Enum.IsDefined(typeof(BoneNames), "\u4e0b\u534a\u8eab"),
                Is.True,
                "MMD lower-body bone must receive hips rotation instead of routing it to center or groove.");

            var lowerBody = (BoneNames)Enum.Parse(typeof(BoneNames), "\u4e0b\u534a\u8eab");
            string filePath = Path.Combine(Path.GetTempPath(), $"vmd_writer_lower_body_{Guid.NewGuid():N}.vmd");

            try
            {
                WriteOneFrameVmd(filePath, new List<BoneNames> { lowerBody });

                List<ParsedBoneFrame> frames = ReadBoneFrames(File.ReadAllBytes(filePath));

                Assert.That(frames.Select(frame => frame.Name).ToArray(), Is.EqualTo(new[] { "\u4e0b\u534a\u8eab" }));
            }
            finally
            {
                if (File.Exists(filePath))
                {
                    File.Delete(filePath);
                }
            }
        }

        [Test]
        public void Given_MmdCenterAndLowerBody_When_SelectingRotationCarrier_Then_CenterIsIdentityAndLowerBodyKeepsHipsRotation()
        {
            var center = (BoneNames)Enum.Parse(typeof(BoneNames), "\u30bb\u30f3\u30bf\u30fc");
            var lowerBody = (BoneNames)Enum.Parse(typeof(BoneNames), "\u4e0b\u534a\u8eab");
            MethodInfo method = typeof(UnityHumanoidVMDRecorder).GetMethod(
                "ShouldWriteRotation",
                BindingFlags.Instance | BindingFlags.NonPublic);

            Assert.That(method, Is.Not.Null, "Recorder must keep center/groove rotation identity while lower-body carries humanoid hips rotation.");

            var go = new GameObject("vmd-rotation-carrier-test");
            try
            {
                var recorder = go.AddComponent<UnityHumanoidVMDRecorder>();

                Assert.That((bool)method.Invoke(recorder, new object[] { center }), Is.False);
                Assert.That((bool)method.Invoke(recorder, new object[] { lowerBody }), Is.True);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(go);
            }
        }

        [Test]
        public void Given_GhostBoneUnderRotatedParent_When_CapturingRotationDiagnostic_Then_ExporterUsesGhostLocalRotation()
        {
            var animatorObject = new GameObject("animator");
            var root = new GameObject("root");
            var center = new GameObject("center");
            var lowerBody = new GameObject("lowerBody");
            var upperBody = new GameObject("upperBody");

            try
            {
                var animator = animatorObject.AddComponent<Animator>();
                center.transform.SetParent(root.transform, false);
                lowerBody.transform.SetParent(center.transform, false);
                upperBody.transform.SetParent(lowerBody.transform, false);

                var parentWorldRotation = Quaternion.Euler(0f, 45f, 0f);
                var childLocalRotation = Quaternion.Euler(0f, 0f, 30f);
                var upperBodyBone = (BoneNames)6;
                var bones = new Dictionary<BoneNames, Transform>
                {
                    [(BoneNames)0] = root.transform,
                    [(BoneNames)1] = center.transform,
                    [(BoneNames)52] = lowerBody.transform,
                    [upperBodyBone] = upperBody.transform
                };
                var ghost = new VmdBoneGhost(animator, bones, useBottomCenter: false);

                lowerBody.transform.rotation = parentWorldRotation;
                upperBody.transform.rotation = parentWorldRotation * childLocalRotation;

                ghost.GhostAll();
                VmdBoneRotationDiagnostic diagnostic = ghost.CaptureRotationDiagnostic(upperBodyBone);
                Quaternion expectedVmdRotation = UnityHumanoidVMDRecorder.ConvertUnityRotationToVmdRotation(childLocalRotation);

                Assert.That(diagnostic.SourceMode, Is.EqualTo("ghost_local"));
                Assert.That(Quaternion.Angle(diagnostic.SourceWorldRotation, upperBody.transform.rotation), Is.LessThan(0.001f));
                Assert.That(Quaternion.Angle(diagnostic.GhostWorldRotation, upperBody.transform.rotation), Is.LessThan(0.001f));
                Assert.That(Quaternion.Angle(diagnostic.GhostLocalRotation, childLocalRotation), Is.LessThan(0.001f));
                Assert.That(Quaternion.Angle(diagnostic.GhostLocalRotation, upperBody.transform.rotation), Is.GreaterThan(1f),
                    "The exporter diagnostic must prove this path records ghost local rotation, not raw world rotation.");
                Assert.That(Quaternion.Angle(diagnostic.VmdRotation, expectedVmdRotation), Is.LessThan(0.001f));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(upperBody);
                UnityEngine.Object.DestroyImmediate(lowerBody);
                UnityEngine.Object.DestroyImmediate(center);
                UnityEngine.Object.DestroyImmediate(root);
                UnityEngine.Object.DestroyImmediate(animatorObject);
            }
        }

        [Test]
        public void Given_GhostBoneWithNonIdentityRestParent_When_CapturingRotationDiagnostic_Then_ReportsSourceLocalDeltaResidual()
        {
            var animatorObject = new GameObject("animator");
            var root = new GameObject("root");
            var center = new GameObject("center");
            var lowerBody = new GameObject("lowerBody");
            var upperBody = new GameObject("upperBody");

            try
            {
                var animator = animatorObject.AddComponent<Animator>();
                center.transform.SetParent(root.transform, false);
                lowerBody.transform.SetParent(center.transform, false);
                upperBody.transform.SetParent(lowerBody.transform, false);

                Quaternion parentRestLocal = Quaternion.Euler(8f, 35f, -4f);
                Quaternion childRestLocal = Quaternion.Euler(11f, -6f, 19f);
                lowerBody.transform.localRotation = parentRestLocal;
                upperBody.transform.localRotation = childRestLocal;

                var upperBodyBone = (BoneNames)6;
                var bones = new Dictionary<BoneNames, Transform>
                {
                    [(BoneNames)0] = root.transform,
                    [(BoneNames)1] = center.transform,
                    [(BoneNames)52] = lowerBody.transform,
                    [upperBodyBone] = upperBody.transform
                };
                var ghost = new VmdBoneGhost(animator, bones, useBottomCenter: false);

                Quaternion parentCurrentLocal = Quaternion.Euler(-5f, 50f, 7f);
                Quaternion childCurrentLocal = Quaternion.Euler(17f, 4f, 43f);
                lowerBody.transform.localRotation = parentCurrentLocal;
                upperBody.transform.localRotation = childCurrentLocal;

                ghost.GhostAll();
                VmdBoneRotationDiagnostic diagnostic = ghost.CaptureRotationDiagnostic(upperBodyBone);
                Quaternion expectedLocalDelta = childCurrentLocal * Quaternion.Inverse(childRestLocal);

                Assert.That(Quaternion.Angle(diagnostic.SourceOriginalLocalRotation, childRestLocal), Is.LessThan(0.001f));
                Assert.That(Quaternion.Angle(diagnostic.SourceCurrentLocalRotation, childCurrentLocal), Is.LessThan(0.001f));
                Assert.That(Quaternion.Angle(diagnostic.SourceLocalDeltaRotation, expectedLocalDelta), Is.LessThan(0.001f));
                Assert.That(diagnostic.GhostVsSourceLocalDeltaAngleDegrees, Is.GreaterThan(1f),
                    "This diagnostic isolates rest-basis residual: ghost local export is not the same as the source local rotation delta when rest parent basis is non-identity.");
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(upperBody);
                UnityEngine.Object.DestroyImmediate(lowerBody);
                UnityEngine.Object.DestroyImmediate(center);
                UnityEngine.Object.DestroyImmediate(root);
                UnityEngine.Object.DestroyImmediate(animatorObject);
            }
        }

        [Test]
        public void Given_GhostBoneWithNonIdentityRestParent_When_CapturingRotationDiagnostic_Then_ParentRestBasisCorrectionMatchesSourceDelta()
        {
            var animatorObject = new GameObject("animator");
            var root = new GameObject("root");
            var center = new GameObject("center");
            var lowerBody = new GameObject("lowerBody");
            var upperBody = new GameObject("upperBody");

            try
            {
                var animator = animatorObject.AddComponent<Animator>();
                center.transform.SetParent(root.transform, false);
                lowerBody.transform.SetParent(center.transform, false);
                upperBody.transform.SetParent(lowerBody.transform, false);

                Quaternion parentRestLocal = Quaternion.Euler(8f, 35f, -4f);
                Quaternion childRestLocal = Quaternion.Euler(11f, -6f, 19f);
                lowerBody.transform.localRotation = parentRestLocal;
                upperBody.transform.localRotation = childRestLocal;

                var upperBodyBone = (BoneNames)6;
                var lowerBodyBone = (BoneNames)52;
                var bones = new Dictionary<BoneNames, Transform>
                {
                    [(BoneNames)0] = root.transform,
                    [(BoneNames)1] = center.transform,
                    [lowerBodyBone] = lowerBody.transform,
                    [upperBodyBone] = upperBody.transform
                };
                var ghost = new VmdBoneGhost(animator, bones, useBottomCenter: false);

                Quaternion parentCurrentLocal = Quaternion.Euler(-5f, 50f, 7f);
                Quaternion childCurrentLocal = Quaternion.Euler(17f, 4f, 43f);
                lowerBody.transform.localRotation = parentCurrentLocal;
                upperBody.transform.localRotation = childCurrentLocal;

                ghost.GhostAll();
                VmdBoneRotationDiagnostic diagnostic = ghost.CaptureRotationDiagnostic(upperBodyBone);
                Quaternion expectedLocalDelta = childCurrentLocal * Quaternion.Inverse(childRestLocal);
                Quaternion expectedVmdRotation = UnityHumanoidVMDRecorder.ConvertUnityRotationToVmdRotation(expectedLocalDelta);

                Assert.That(diagnostic.ParentBoneName, Is.EqualTo(lowerBodyBone));
                Assert.That(Quaternion.Angle(diagnostic.SourceParentOriginalLocalRotation, parentRestLocal), Is.LessThan(0.001f));
                Assert.That(diagnostic.GhostVsSourceLocalDeltaAngleDegrees, Is.GreaterThan(1f));
                Assert.That(diagnostic.ParentRestBasisCorrectedGhostVsSourceLocalDeltaAngleDegrees, Is.LessThan(0.001f),
                    "The parent rest-basis correction should isolate whether ghost local export can be converted back to source local delta.");
                Assert.That(Quaternion.Angle(diagnostic.ParentRestBasisCorrectedGhostLocalRotation, expectedLocalDelta), Is.LessThan(0.001f));
                Assert.That(Quaternion.Angle(diagnostic.ParentRestBasisCorrectedVmdRotation, expectedVmdRotation), Is.LessThan(0.001f));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(upperBody);
                UnityEngine.Object.DestroyImmediate(lowerBody);
                UnityEngine.Object.DestroyImmediate(center);
                UnityEngine.Object.DestroyImmediate(root);
                UnityEngine.Object.DestroyImmediate(animatorObject);
            }
        }

        [Test]
        public void Given_GhostBoneWithNonIdentityRestParent_When_CapturingRotationDiagnostic_Then_ExportRotationUsesParentRestBasisCorrection()
        {
            var animatorObject = new GameObject("animator");
            var root = new GameObject("root");
            var center = new GameObject("center");
            var lowerBody = new GameObject("lowerBody");
            var upperBody = new GameObject("upperBody");

            try
            {
                var animator = animatorObject.AddComponent<Animator>();
                center.transform.SetParent(root.transform, false);
                lowerBody.transform.SetParent(center.transform, false);
                upperBody.transform.SetParent(lowerBody.transform, false);

                Quaternion parentRestLocal = Quaternion.Euler(8f, 35f, -4f);
                Quaternion childRestLocal = Quaternion.Euler(11f, -6f, 19f);
                lowerBody.transform.localRotation = parentRestLocal;
                upperBody.transform.localRotation = childRestLocal;

                var upperBodyBone = (BoneNames)6;
                var lowerBodyBone = (BoneNames)52;
                var bones = new Dictionary<BoneNames, Transform>
                {
                    [(BoneNames)0] = root.transform,
                    [(BoneNames)1] = center.transform,
                    [lowerBodyBone] = lowerBody.transform,
                    [upperBodyBone] = upperBody.transform
                };
                var ghost = new VmdBoneGhost(animator, bones, useBottomCenter: false);

                Quaternion parentCurrentLocal = Quaternion.Euler(-5f, 50f, 7f);
                Quaternion childCurrentLocal = Quaternion.Euler(17f, 4f, 43f);
                lowerBody.transform.localRotation = parentCurrentLocal;
                upperBody.transform.localRotation = childCurrentLocal;

                ghost.GhostAll();
                VmdBoneRotationDiagnostic diagnostic = ghost.CaptureRotationDiagnostic(upperBodyBone);
                Quaternion expectedLocalDelta = childCurrentLocal * Quaternion.Inverse(childRestLocal);
                Quaternion expectedVmdRotation = UnityHumanoidVMDRecorder.ConvertUnityRotationToVmdRotation(expectedLocalDelta);

                Assert.That(diagnostic.ExportSourceMode, Is.EqualTo("parent_rest_basis_corrected_ghost_local"));
                Assert.That(diagnostic.GhostVsSourceLocalDeltaAngleDegrees, Is.GreaterThan(1f));
                Assert.That(diagnostic.ExportVsSourceLocalDeltaAngleDegrees, Is.LessThan(0.001f));
                Assert.That(Quaternion.Angle(diagnostic.VmdRotation, expectedVmdRotation), Is.GreaterThan(1f),
                    "The raw ghost local VMD rotation should remain visible as a diagnostic but should not be the corrected export rotation.");
                Assert.That(Quaternion.Angle(diagnostic.ExportVmdRotation, expectedVmdRotation), Is.LessThan(0.001f));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(upperBody);
                UnityEngine.Object.DestroyImmediate(lowerBody);
                UnityEngine.Object.DestroyImmediate(center);
                UnityEngine.Object.DestroyImmediate(root);
                UnityEngine.Object.DestroyImmediate(animatorObject);
            }
        }

        [Test]
        public void Given_ExportRotationDiagnostics_When_BuildingCsv_Then_WorstResidualFramesAreReported()
        {
            var recorderObject = new GameObject("export-rotation-diagnostics");
            try
            {
                var recorder = recorderObject.AddComponent<UnityHumanoidVMDRecorder>();
                var bone = (BoneNames)6;
                recorder.RecordExportRotationDiagnostic(
                    3,
                    BuildRotationDiagnostic(bone, ghostAngle: 2f, correctedAngle: 0.1f, exportAngle: 0.2f));
                recorder.RecordExportRotationDiagnostic(
                    7,
                    BuildRotationDiagnostic(bone, ghostAngle: 9f, correctedAngle: 1.5f, exportAngle: 0.7f));

                string csv = UnityHumanoidVMDRecorder.BuildExportRotationDiagnosticsCsv(
                    recorder.GetExportRotationDiagnosticAggregates());
                string[] lines = csv.Trim().Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries);

                Assert.That(lines[0], Does.Contain("boneName,boneIndex,sampleCount"));
                Assert.That(lines.Length, Is.EqualTo(2));

                string[] columns = lines[1].Split(',');
                Assert.That(columns[1], Is.EqualTo(((int)bone).ToString()));
                Assert.That(columns[2], Is.EqualTo("2"));
                Assert.That(columns[3], Is.EqualTo("7"));
                Assert.That(columns[4], Is.EqualTo("9"));
                Assert.That(columns[5], Is.EqualTo("7"));
                Assert.That(columns[6], Is.EqualTo("1.5"));
                Assert.That(columns[7], Is.EqualTo("7"));
                Assert.That(columns[8], Is.EqualTo("0.7"));
                Assert.That(columns[9], Is.EqualTo("parent_rest_basis_corrected_ghost_local"));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(recorderObject);
            }
        }

        [Test]
        public void Given_ExportRotationDiagnosticSamples_When_BuildingCsv_Then_PerFrameRowsAreReported()
        {
            var recorderObject = new GameObject("export-rotation-diagnostic-samples");
            try
            {
                var recorder = recorderObject.AddComponent<UnityHumanoidVMDRecorder>();
                var bone = (BoneNames)6;
                recorder.RecordExportRotationDiagnostic(
                    3,
                    BuildRotationDiagnostic(bone, ghostAngle: 2f, correctedAngle: 0.1f, exportAngle: 0.2f));
                recorder.RecordExportRotationDiagnostic(
                    7,
                    BuildRotationDiagnostic(bone, ghostAngle: 9f, correctedAngle: 1.5f, exportAngle: 0.7f));

                string csv = UnityHumanoidVMDRecorder.BuildExportRotationDiagnosticSamplesCsv(
                    recorder.GetExportRotationDiagnosticSamples());
                string[] lines = csv.Trim().Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries);

                Assert.That(lines[0], Does.Contain("frameNumber,boneName,boneIndex"));
                Assert.That(lines.Length, Is.EqualTo(3));

                string[] columns = lines[2].Split(',');
                Assert.That(columns[0], Is.EqualTo("7"));
                Assert.That(columns[2], Is.EqualTo(((int)bone).ToString()));
                Assert.That(columns[4], Is.EqualTo("parent_rest_basis_corrected_ghost_local"));
                Assert.That(columns[7], Is.EqualTo("0.7"));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(recorderObject);
            }
        }

        [Test]
        public void Given_ExportIkSourceDiagnostics_When_BuildingCsv_Then_PerFrameRowsAreReported()
        {
            var recorderObject = new GameObject("export-ik-source-diagnostic-samples");
            try
            {
                var recorder = recorderObject.AddComponent<UnityHumanoidVMDRecorder>();
                recorder.RecordExportIkSourceDiagnostic(
                    12,
                    340,
                    1.25f,
                    (BoneNames)2,
                    new Vector3(1f, 0f, -2f),
                    new Vector3(1.3f, 0.1f, -2.4f),
                    new Vector3(0.2f, 0.1f, -0.3f),
                    new Vector3(0.25f, 0.1f, -0.35f));

                string csv = UnityHumanoidVMDRecorder.BuildExportIkSourceDiagnosticsCsv(
                    recorder.GetExportIkSourceDiagnosticSamples());
                string[] lines = csv.Trim().Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries);

                Assert.That(lines[0], Does.Contain("recorderFrame,unityFrame,sampleTime"));
                Assert.That(lines[0], Does.Contain("directFootWorldPosition"));
                Assert.That(lines[0], Does.Contain("directFootRootPosition"));
                Assert.That(lines.Length, Is.EqualTo(2));

                string[] columns = lines[1].Split(',');
                Assert.That(columns[0], Is.EqualTo("12"));
                Assert.That(columns[1], Is.EqualTo("340"));
                Assert.That(columns[2], Is.EqualTo("1.25"));
                Assert.That(columns[4], Is.EqualTo("2"));
                Assert.That(columns[5], Is.EqualTo("1|0|-2"));
                Assert.That(columns[6], Is.EqualTo("1.3|0.1|-2.4"));
                Assert.That(columns[7], Is.EqualTo("0.2|0.1|-0.3"));
                Assert.That(columns[8], Is.EqualTo("0.25|0.1|-0.35"));
                Assert.That(columns[9], Is.EqualTo("0|0|0"));
                Assert.That(columns[10], Is.EqualTo("0|0|0"));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(recorderObject);
            }
        }

        [Test]
        public void Given_MovingModelRootNode_When_ResolvingFootIkRootReference_Then_UsesMovingRoot()
        {
            var recorderObject = new GameObject("moving-root-reference-recorder");
            var modelRootObject = new GameObject("461.!Root");
            try
            {
                recorderObject.transform.position = new Vector3(1f, 0f, 2f);
                modelRootObject.transform.SetParent(recorderObject.transform, worldPositionStays: true);
                modelRootObject.transform.position = new Vector3(1.25f, 0f, 2.5f);

                var recorder = recorderObject.AddComponent<UnityHumanoidVMDRecorder>();
                recorder.UseAbsoluteCoordinateSystem = true;
                recorder.IgnoreInitialPosition = false;

                InvokePrivate(recorder, "SetInitialPositionAndRotation");
                var rootReference = (Vector3)InvokePrivate(recorder, "GetCurrentRootPositionForIkReference");

                Assert.That(rootReference.x, Is.EqualTo(modelRootObject.transform.position.x).Within(0.0001f));
                Assert.That(rootReference.y, Is.EqualTo(modelRootObject.transform.position.y).Within(0.0001f));
                Assert.That(rootReference.z, Is.EqualTo(modelRootObject.transform.position.z).Within(0.0001f));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(modelRootObject);
                UnityEngine.Object.DestroyImmediate(recorderObject);
            }
        }

        [Test]
        public void Given_PostGuardIkExportPositions_When_BuildingIkSourceDiagnostics_Then_ReportsFinalWriterInput()
        {
            var samples = new[]
            {
                new UnityHumanoidVMDRecorder.ExportIkSourceDiagnosticSample(
                    recorderFrameNumber: 2,
                    unityFrameNumber: 120,
                    sampleTime: 0.5f,
                    boneName: (BoneNames)2,
                    rootReferencePosition: Vector3.zero,
                    sourceWorldPosition: new Vector3(1f, 2f, 3f),
                    sourceRelativePosition: new Vector3(0.1f, 0.2f, 0.3f),
                    exportedUnityPosition: new Vector3(0.25f, 0.1f, -0.35f))
            };
            var finalVmdPositions = new Dictionary<BoneNames, List<Vector3>>
            {
                [(BoneNames)2] = new List<Vector3>
                {
                    Vector3.zero,
                    Vector3.zero,
                    new Vector3(-2.5f, 1.25f, 3.75f)
                }
            };

            List<UnityHumanoidVMDRecorder.ExportIkSourceDiagnosticSample> finalSamples =
                UnityHumanoidVMDRecorder.BuildFinalExportIkSourceDiagnosticSamples(
                    samples,
                    finalVmdPositions,
                    safeFrameCount: 3);
            string csv = UnityHumanoidVMDRecorder.BuildExportIkSourceDiagnosticsCsv(finalSamples);
            string[] lines = csv.Trim().Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries);

            string[] columns = lines[1].Split(',');
            Assert.That(columns[8], Is.EqualTo("0.2|0.1|-0.3"),
                "IK source diagnostics must report the post-guard VMD writer input converted back to Unity meters.");
        }

        private static VmdBoneRotationDiagnostic BuildRotationDiagnostic(
            BoneNames boneName,
            float ghostAngle,
            float correctedAngle,
            float exportAngle)
        {
            return new VmdBoneRotationDiagnostic(
                boneName,
                "ghost_local",
                Quaternion.identity,
                Quaternion.identity,
                Quaternion.identity,
                Quaternion.identity,
                BoneNames.None,
                Quaternion.identity,
                Quaternion.identity,
                Quaternion.identity,
                Quaternion.identity,
                ghostAngle,
                Quaternion.identity,
                Quaternion.identity,
                correctedAngle,
                "parent_rest_basis_corrected_ghost_local",
                Quaternion.identity,
                Quaternion.identity,
                exportAngle);
        }

        private static object InvokePrivate(object target, string methodName)
        {
            MethodInfo method = target.GetType().GetMethod(methodName, BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.That(method, Is.Not.Null, methodName);
            return method.Invoke(target, null);
        }

        private static void WriteOneFrameVmd(string filePath, List<BoneNames> activeBones)
        {
            const int frameCount = 1;
            var positions = new Dictionary<BoneNames, List<Vector3>>();
            var rotations = new Dictionary<BoneNames, List<Quaternion>>();

            foreach (BoneNames bone in activeBones)
            {
                positions.Add(bone, new List<Vector3> { Vector3.zero });
                rotations.Add(bone, new List<Quaternion> { Quaternion.identity });
            }

            VmdFileWriter.WriteVmdFile(
                modelName: "testModel",
                filePath: filePath,
                activeBones: activeBones,
                frameCount: frameCount,
                keyReductionLevel: 1,
                positionDictionarySaved: positions,
                rotationDictionarySaved: rotations,
                morphSnapshot: null,
                useCenterAsParentOfAll: false,
                routeCenterBoneToGroove: false,
                centerNameString: "CENTER",
                grooveNameString: "GROOVE");
        }

        private static string ReadShiftJisBoneName(byte[] bytes, int boneFrameOffset)
        {
            const int boneNameLength = 15;
            int length = 0;
            while (length < boneNameLength && bytes[boneFrameOffset + length] != 0)
            {
                length++;
            }

            return System.Text.Encoding.GetEncoding("shift_jis").GetString(bytes, boneFrameOffset, length);
        }

        private static List<ParsedBoneFrame> ReadBoneFrames(byte[] bytes)
        {
            const int headerLength = 30 + 20;
            const int boneFrameSize = 111;
            uint count = BitConverter.ToUInt32(bytes, headerLength);
            int offset = headerLength + 4;
            var frames = new List<ParsedBoneFrame>();
            for (int i = 0; i < count; i++)
            {
                frames.Add(new ParsedBoneFrame(
                    ReadShiftJisBoneName(bytes, offset),
                    BitConverter.ToUInt32(bytes, offset + 15)));
                offset += boneFrameSize;
            }

            return frames;
        }

        private readonly struct ParsedBoneFrame
        {
            public ParsedBoneFrame(string name, uint frame)
            {
                Name = name;
                Frame = frame;
            }

            public string Name { get; }
            public uint Frame { get; }
        }
    }
}
