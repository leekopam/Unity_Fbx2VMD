using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Fbx2Vmd.FBXImporter;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace Tests.Editor.FBXImporter
{
    public class EditorHumanoidFootGroundingTests
    {
        private const BindingFlags Flags = BindingFlags.Instance | BindingFlags.Static |
            BindingFlags.Public | BindingFlags.NonPublic;

        [Test]
        public void Given_OverlappingContacts_When_SeekingAcrossTransitions_Then_PreservesSupportAndFractionalContinuity()
        {
            const string clipPath = "Assets/Resources/Import_FBX/satisfaction_2.fbx";
            GameObject source = AssetDatabase.LoadAssetAtPath<GameObject>(clipPath);
            if (source == null) Assert.Ignore("로컬 FBX 입력이 없는 환경에서는 실제 접촉 검증을 생략함");
            AnimationClip clip = AssetDatabase.LoadAllAssetsAtPath(clipPath).OfType<AnimationClip>()
                .First(c => !c.name.StartsWith("__preview__", StringComparison.Ordinal));
            Type type = typeof(FBXVmdPipeline).Assembly.GetType("Fbx2Vmd.FBXImporter.HumanoidMotionPlaybackController", true);
            var floor = new GameObject("중첩 지지 검증 바닥") { hideFlags = HideFlags.HideAndDontSave };
            floor.transform.position = new Vector3(0f, 2.95f, 0f);
            floor.AddComponent<BoxCollider>().size = new Vector3(40f, 0.1f, 40f);
            try
            {
                foreach (string path in new[] {
                    "Assets/_Project/Model/YYB Hatsune Miku_default/YYB Hatsune Miku_default_1.0ver.fbx",
                    "Assets/_Project/FBX/Snake Hip Hop Dance.fbx" })
                {
                    GameObject model = UnityEngine.Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>(path));
                    model.hideFlags = HideFlags.HideAndDontSave;
                    model.transform.position = new Vector3(0f, 3f, 0f);
                    foreach (MonoBehaviour script in model.GetComponentsInChildren<MonoBehaviour>(true)) script.enabled = false;
                    object controller = Activator.CreateInstance(type, true);
                    try
                    {
                        Animator animator = model.GetComponentInChildren<Animator>(true);
                        animator.enabled = true;
                        Invoke(controller, "PrepareWithArmDirectionReference", animator, clip, source);
                        Invoke(controller, "SetGroundResponseEnabled", true);
                        object grounding = Field(controller, "_footGrounding");
                        Assert.That((int)Property(grounding, "UnresolvedSupportPairCount"), Is.Zero,
                            "전체 접촉 계획 중 독립 목표로 남은 구간을 숨기지 않음");
                        Transform[] bones = model.GetComponentsInChildren<Transform>(true);
                        var poses = new Dictionary<float, Vector3[]>();
                        var rotations = new Dictionary<float, Quaternion[]>();
                        var supportPositions = new Dictionary<float, Vector3[]>();
                        float[] frames = { 5547f, 5512.99f, 5513f, 5513.01f, 5591f, 5591.99f, 5592f, 5592.01f, 5607f, 5608f, 5547.5f };
                        foreach (float frame in frames.Concat(frames.Reverse()))
                        {
                            Invoke(controller, "Seek", frame / clip.frameRate);
                            AssertStatus(controller, "Applied");
                            Assert.That((float)Property(grounding, "MinimumSoleClearance"), Is.GreaterThan(-0.0001f));
                            Assert.That((float)Property(grounding, "MaximumTargetError"), Is.LessThan(0.0001f));
                            if (frame == 5547f)
                                Assert.That((float)Property(grounding, "MaximumSupportedContactError"), Is.LessThan(0.006f),
                                    "독립 앵커의 거리 불일치로 발생한 수cm 부유를 유지하면 안 됨");
                            if (poses.TryGetValue(frame, out Vector3[] previous)) AssertPose(bones, previous, rotations[frame]);
                            else { poses.Add(frame, bones.Select(t => t.position).ToArray()); rotations.Add(frame, bones.Select(t => t.rotation).ToArray()); }
                            if (frame == 5591f || frame == 5592f)
                                supportPositions[frame] = CaptureLeftRearSupport(grounding);
                        }
                        Transform foot = animator.GetBoneTransform(HumanBodyBones.RightFoot);
                        int index = Array.IndexOf(bones, foot);
                        foreach (float frame in new[] { 5512.99f, 5513.01f })
                            Assert.That(Vector3.Distance(poses[frame][index], poses[5513f][index]), Is.LessThan(0.001f),
                                "접촉 신원 전환 직전·직후 소수 프레임에서 앵커만 보간하면 위치가 튈 수 있음");
                        int leftIndex = Array.IndexOf(bones, animator.GetBoneTransform(HumanBodyBones.LeftFoot));
                        // 원본 발 구르기에 따른 발목 이동과 실제 지지점·앵커의 복귀를 구분함.
                        for (int i = 0; i < 2; i++)
                            Assert.That(Vector3.ProjectOnPlane(supportPositions[5592f][i] - supportPositions[5591f][i],
                                Vector3.up).magnitude, Is.LessThan(0.002f),
                                "남는 뒤꿈치 지지점과 앵커가 이전 위치로 복귀하면 안 됨");
                        Vector3 previousResidual = supportPositions[5591f][0] - supportPositions[5591f][1];
                        Vector3 currentResidual = supportPositions[5592f][0] - supportPositions[5592f][1];
                        Assert.That(Vector3.ProjectOnPlane(currentResidual - previousResidual, Vector3.up).magnitude,
                            Is.LessThan(0.002f), "앵커 이동을 뺀 실제 지지점의 미끄럼을 제한함");
                        foreach (float frame in new[] { 5591.99f, 5592.01f })
                            Assert.That(Vector3.Distance(poses[frame][leftIndex], poses[5592f][leftIndex]), Is.LessThan(0.001f),
                                "지지 해제 경계에서 발목이 불연속적으로 이동하면 안 됨");
                        AssertRotationOnlyGrounding(grounding);
                        AssertPartialFootFramesRejected(controller, grounding, clip);
                    }
                    finally { ((IDisposable)controller).Dispose(); UnityEngine.Object.DestroyImmediate(model); }
                }
            }
            finally { UnityEngine.Object.DestroyImmediate(floor); }
        }

        [Test]
        public void Given_TwoHumanoids_When_PlaybackGroundChanges_Then_UsesSoleAndRestoresDeterministically()
        {
            Type controllerType = typeof(FBXVmdPipeline).Assembly.GetType(
                "Fbx2Vmd.FBXImporter.HumanoidMotionPlaybackController", true);
            Assert.That(controllerType.GetProperty("LastGroundingStatus", Flags), Is.Not.Null,
                "실제 재생의 정밀 접지 적용과 fallback을 구분해야 함");
            const string clipPath = "Assets/Resources/Import_FBX/satisfaction_2.fbx";
            GameObject source = AssetDatabase.LoadAssetAtPath<GameObject>(clipPath);
            if (source == null)
                Assert.Ignore("로컬 FBX 입력이 없는 환경에서는 실제 재생 검증을 생략함");
            AnimationClip clip = AssetDatabase.LoadAllAssetsAtPath(clipPath).OfType<AnimationClip>()
                .First(c => !c.name.StartsWith("__preview__", StringComparison.Ordinal));
            object controller = Activator.CreateInstance(controllerType, true);
            var floor = new GameObject("접지 통합 검증 바닥") { hideFlags = HideFlags.HideAndDontSave };
            BoxCollider collider = floor.AddComponent<BoxCollider>();
            collider.size = new Vector3(40f, 0.1f, 40f);
            floor.transform.position = new Vector3(0f, 2.95f, 0f);
            try
            {
                foreach (string modelPath in new[] {
                    "Assets/_Project/Model/YYB Hatsune Miku_default/YYB Hatsune Miku_default_1.0ver.fbx",
                    "Assets/_Project/FBX/Snake Hip Hop Dance.fbx" })
                {
                    GameObject asset = AssetDatabase.LoadAssetAtPath<GameObject>(modelPath);
                    Assert.That(asset, Is.Not.Null, modelPath);
                    GameObject model = UnityEngine.Object.Instantiate(asset);
                    model.hideFlags = HideFlags.HideAndDontSave;
                    model.transform.position = new Vector3(0f, 3f, 0f);
                    foreach (MonoBehaviour script in model.GetComponentsInChildren<MonoBehaviour>(true))
                        script.enabled = false;
                    try
                    {
                        Animator animator = model.GetComponentInChildren<Animator>(true);
                        animator.enabled = true;
                        Transform[] bones = model.GetComponentsInChildren<Transform>(true);
                        Transform hips = animator.GetBoneTransform(HumanBodyBones.Hips);
                        Invoke(controller, "PrepareWithArmDirectionReference", animator, clip, source);
                        Invoke(controller, "SetGroundResponseEnabled", true);
                        object grounding = controllerType.GetField("_footGrounding", Flags).GetValue(controller);
                        Assert.That(grounding, Is.Not.Null);
                        var poses = new Dictionary<float, Vector3[]>();
                        var rotations = new Dictionary<float, Quaternion[]>();
                        int physicalReachCount = 0;
                        float maximumTargetError = 0f, minimumSoleClearance = 0f, maximumContactError = 0f;
                        float[] frames = { 58f, 92f, 1080f, 1084f, 1089f, 1104f, 9810f,
                            1172f, 1173f, 1268f, 1269f, 1276f, 58.5f,
                            4554f, 4866f, 4867f, 4873f, 4874f, 4964f, 4965f };
                        foreach (float frame in frames.Concat(frames.Reverse()))
                        {
                            floor.SetActive(false);
                            Invoke(controller, "Seek", frame / clip.frameRate);
                            AssertStatus(controller, "NoGround");
                            Vector3[] ungrounded = bones.Select(t => t.position).ToArray();
                            Quaternion[] ungroundedRotations = bones.Select(t => t.rotation).ToArray();
                            Invoke(controller, "EvaluateEditorContactReference", frame / clip.frameRate);
                            AssertUngroundedTargets(animator, bones, ungrounded, ungroundedRotations);
                            Invoke(controller, "Seek", frame / clip.frameRate);
                            AssertPose(bones, ungrounded, ungroundedRotations);
                            Invoke(controller, "EvaluateEditorContactReference", frame / clip.frameRate);
                            Vector3[] positions = bones.Select(t => t.localPosition).ToArray();
                            Vector3[] scales = bones.Select(t => t.localScale).ToArray();
                            floor.SetActive(true);
                            Invoke(controller, "Seek", frame / clip.frameRate);
                            AssertStatus(controller, "Applied");
                            if ((bool)Property(grounding, "UsedPhysicalReach")) physicalReachCount++;
                            maximumTargetError = Mathf.Max(maximumTargetError, (float)Property(grounding, "MaximumTargetError"));
                            minimumSoleClearance = Mathf.Min(minimumSoleClearance, (float)Property(grounding, "MinimumSoleClearance"));
                            maximumContactError = Mathf.Max(maximumContactError, (float)Property(grounding, "MaximumSupportedContactError"));
                            for (int i = 0; i < bones.Length; i++)
                            {
                                if (bones[i] != hips)
                                    Assert.That(Vector3.Distance(bones[i].localPosition, positions[i]), Is.LessThan(0.00002f));
                                Assert.That(bones[i].localScale, Is.EqualTo(scales[i]));
                            }
                            Assert.That((float)Property(grounding, "MaximumTargetError"), Is.LessThan(0.0001f));
                            Assert.That((float)Property(grounding, "MinimumSoleClearance"), Is.GreaterThan(-0.0001f));
                            if (frame == 58f)
                            {
                                Assert.That((int)Property(grounding, "FullySupportedContactCount"), Is.GreaterThan(0));
                                Assert.That((float)Property(grounding, "MaximumSupportedContactError"), Is.LessThan(0.004f),
                                    "완전 지지 구간에서 밑창 비관통만 만족한 공중 부유를 통과시키지 않음");
                            }
                            Vector3[] current = bones.Select(t => t.position).ToArray();
                            Quaternion[] currentRotations = bones.Select(t => t.rotation).ToArray();
                            if (poses.TryGetValue(frame, out Vector3[] previous))
                            {
                                AssertPose(bones, previous, rotations[frame]);
                            }
                            else
                            {
                                poses.Add(frame, current);
                                rotations.Add(frame, currentRotations);
                            }
                            Invoke(controller, "RestoreCurrentPose");
                            AssertStatus(controller, "Applied");
                            AssertPose(bones, current, currentRotations);
                        }

                        int rightFootIndex = Array.IndexOf(bones, animator.GetBoneTransform(HumanBodyBones.RightFoot));
                        foreach (Vector2 boundary in new[] { new Vector2(4866f, 4867f),
                            new Vector2(4873f, 4874f), new Vector2(4964f, 4965f) })
                            Assert.That(Quaternion.Angle(rotations[boundary.x][rightFootIndex],
                                rotations[boundary.y][rightFootIndex]), Is.LessThan(5f),
                                "골반 이동 한도로 접지 경로가 전환되어 발이 수십 도 튀면 안 됨");

                        if (modelPath.Contains("YYB Hatsune Miku_default/"))
                        {
                            Invoke(controller, "Seek", 790f / clip.frameRate);
                            AssertStatus(controller, "Applied");
                            Assert.That(FindSoleClearance(grounding, "_right", false, 3f), Is.LessThan(0.002f),
                                "정지 자세의 뒤꿈치를 앞꿈치만 지지하는 자세로 두면 안 됨");
                            Invoke(controller, "Seek", 1332f / clip.frameRate);
                            AssertStatus(controller, "Applied");
                            Assert.That(FindSoleClearance(grounding, "_left", false, 3f),
                                Is.InRange(0.01f, 0.035f), "앞꿈치 자세를 유지하면서 과도한 뒤꿈치 들림을 제한해야 함");
                            Assert.That(Mathf.Abs(FindSoleClearance(grounding, "_left", true, 3f)),
                                Is.LessThan(0.002f), "앞꿈치 지지점은 바닥에 닿아 있어야 함");
                        }

                        Invoke(controller, "Seek", 58f / clip.frameRate);
                        var self = model.AddComponent<BoxCollider>();
                        self.center = Vector3.up * 0.3f;
                        self.size = new Vector3(1f, 0.05f, 1f);
                        Invoke(controller, "Seek", 58f / clip.frameRate);
                        AssertPose(bones, poses[58f], rotations[58f]);
                        floor.transform.position += Vector3.up * 0.02f;
                        Invoke(controller, "Seek", 58f / clip.frameRate);
                        AssertStatus(controller, "Applied");
                        Assert.That((float)Property(grounding, "MinimumSoleClearance"), Is.GreaterThan(-0.0001f));
                        Assert.That((float)Property(grounding, "MaximumSupportedContactError"), Is.LessThan(0.004f));
                        floor.SetActive(false);
                        Invoke(controller, "Seek", 58f / clip.frameRate);
                        AssertStatus(controller, "NoGround");
                        Vector3[] noGround = bones.Select(t => t.position).ToArray();
                        Quaternion[] noGroundRotations = bones.Select(t => t.rotation).ToArray();
                        floor.SetActive(true);
                        collider.isTrigger = true;
                        Invoke(controller, "Seek", 58f / clip.frameRate);
                        AssertStatus(controller, "NoGround");
                        AssertPose(bones, noGround, noGroundRotations);
                        collider.isTrigger = false;
                        floor.transform.position -= Vector3.up * 0.02f;
                        if (modelPath.Contains("YYB Hatsune Miku_default/"))
                            AssertFailedSecondLegRestores(controller, grounding, bones, clip.frameRate);
                        Invoke(controller, "Stop");
                        Vector3[] stopped = bones.Select(t => t.position).ToArray();
                        Quaternion[] stoppedRotations = bones.Select(t => t.rotation).ToArray();
                        Invoke(controller, "Seek", 0f);
                        AssertPose(bones, stopped, stoppedRotations);
                        AssertDisposeRestoresAndReleases(controller, grounding, hips);
                        TestContext.WriteLine($"{modelPath}: evaluations={frames.Length * 2}, physicalReach={physicalReachCount}, " +
                            $"targetError={maximumTargetError:R}, soleMinimum={minimumSoleClearance:R}, " +
                            $"supportedContactError={maximumContactError:R}");
                    }
                    finally
                    {
                        ((IDisposable)controller).Dispose();
                        UnityEngine.Object.DestroyImmediate(model);
                    }
                }
            }
            finally
            {
                ((IDisposable)controller).Dispose();
                UnityEngine.Object.DestroyImmediate(floor);
            }
        }

        private static object Invoke(object target, string name, params object[] args) =>
            target.GetType().GetMethod(name, Flags).Invoke(target, args);

        private static object Property(object target, string name) =>
            target.GetType().GetProperty(name, Flags).GetValue(target);

        private static object Field(object target, string name) =>
            target.GetType().GetField(name, Flags).GetValue(target);

        private static float FindSoleClearance(object grounding, string side, bool isFront, float floorHeight)
        {
            object leg = Field(grounding, side);
            object sampler = Field(leg, "Sampler");
            Assert.That(Invoke(sampler, "TrySample"), Is.True);
            object[] contact = { isFront, Vector3.up, 0, Vector3.zero };
            Assert.That(Invoke(sampler, "TrySelectContact", contact), Is.True);
            object[] point = { (int)contact[2], Vector3.zero };
            Assert.That(Invoke(sampler, "TryGetLocalPoint", point), Is.True);
            return ((Transform)Field(leg, "Foot")).TransformPoint((Vector3)point[1]).y - floorHeight;
        }

        private static Vector3[] CaptureLeftRearSupport(object grounding)
        {
            object leg = Field(grounding, "_left");
            object sampler = Field(leg, "Sampler");
            Assert.That(((Vector2)Field(leg, "_activeWeights")).x, Is.GreaterThanOrEqualTo(0.999f));
            Assert.That(Invoke(sampler, "TrySample"), Is.True);
            object contact = ((Array)Field(leg, "_activeContacts")).GetValue(0);
            object[] args = { sampler, Vector3.zero };
            Assert.That(Invoke(contact, "TryGetLocalPoint", args), Is.True);
            return new[] { ((Transform)Field(leg, "Foot")).TransformPoint((Vector3)args[1]),
                ((Vector3[])Field(leg, "_anchors"))[0] };
        }

        private static void AssertFailedSecondLegRestores(object controller, object grounding,
            Transform[] bones, float frameRate)
        {
            float time = 1276f / frameRate;
            Invoke(grounding, "RestoreAppliedPose");
            Invoke(controller, "EvaluateEditorContactReference", time);
            Vector3[] original = bones.Select(t => t.position).ToArray();
            Quaternion[] rotations = bones.Select(t => t.rotation).ToArray();
            object right = Field(grounding, "_right");
            FieldInfo reference = right.GetType().GetField("_referenceNormal", Flags);
            object normal = reference.GetValue(right);
            try
            {
                // 첫 다리 적용 뒤 두 번째 다리의 방향 계산 실패를 만들어 부분 적용 잔류를 검사함.
                reference.SetValue(right, new Vector3(float.NaN, 0f, 0f));
                Assert.That((bool)Invoke(grounding, "TryApply", time, Field(controller, "_groundResponse")), Is.False);
                AssertPose(bones, original, rotations);

                FieldInfo owner = controller.GetType().GetField("_footGrounding", Flags);
                owner.SetValue(controller, null);
                try
                {
                    Invoke(controller, "Seek", time);
                    Vector3[] legacy = bones.Select(t => t.position).ToArray();
                    Quaternion[] legacyRotations = bones.Select(t => t.rotation).ToArray();
                    owner.SetValue(controller, grounding);
                    Invoke(controller, "Seek", time);
                    AssertStatus(controller, "Fallback");
                    AssertPose(bones, legacy, legacyRotations);
                }
                finally
                {
                    owner.SetValue(controller, grounding);
                }
            }
            finally
            {
                reference.SetValue(right, normal);
            }
        }

        private static void AssertDisposeRestoresAndReleases(object controller, object grounding, Transform hips)
        {
            Invoke(grounding, "RestoreAppliedPose");
            Invoke(controller, "RestoreFootRotationBindings");
            Vector3 originalHips = hips.localPosition;
            var legs = new[] { Field(grounding, "_left"), Field(grounding, "_right") };
            Transform[] joints = legs.SelectMany(leg => new[] { "Upper", "Lower", "Foot", "Toes" }
                .Select(name => (Transform)Field(leg, name))).ToArray();
            Quaternion[] rotations = joints.Select(t => t.localRotation).ToArray();
            Mesh[] ownedMeshes = legs.SelectMany(leg =>
                ((System.Collections.IEnumerable)Field(Field(leg, "Sampler"), "_surfaces"))
                .Cast<object>().Select(surface => (Mesh)Field(surface, "_baked"))).ToArray();
            Assert.That(ownedMeshes.Length, Is.GreaterThan(0));
            // 해제 기준은 발 방향 정합까지 제거한 원본이며 두 보정을 다시 적용한 뒤 검증함.
            Invoke(controller, "ApplyFootRotationReference", 0f);
            Assert.That((bool)Invoke(grounding, "TryApply", 0f, Field(controller, "_groundResponse")), Is.True);
            ((IDisposable)controller).Dispose();
            Assert.That(hips.localPosition, Is.EqualTo(originalHips));
            for (int i = 0; i < joints.Length; i++)
                Assert.That(Quaternion.Angle(joints[i].localRotation, rotations[i]), Is.LessThan(0.06f));
            Assert.That(ownedMeshes.All(mesh => mesh == null), Is.True, "샘플러 소유 임시 Mesh를 해제해야 함");
            AssertStatus(controller, "Disabled");
        }

        private static void AssertRotationOnlyGrounding(object grounding)
        {
            Invoke(grounding, "RestoreAppliedPose");
            object leg = Field(grounding, "_right");
            Transform upper = (Transform)Field(leg, "Upper"), lower = (Transform)Field(leg, "Lower");
            Transform foot = (Transform)Field(leg, "Foot"), toes = (Transform)Field(leg, "Toes");
            Quaternion upperRotation = upper.localRotation, lowerRotation = lower.localRotation;
            Quaternion footRotation = foot.localRotation, toeRotation = toes.localRotation;
            Quaternion target = Quaternion.AngleAxis(5f, Vector3.up) * foot.rotation;
            Vector3 originalPosition = foot.position;
            float upperLength = Vector3.Distance(upper.position, lower.position);
            float lowerLength = Vector3.Distance(lower.position, foot.position);
            Invoke(leg, "CapturePose");
            leg.GetType().GetField("_target", Flags).SetValue(leg, foot.position);
            leg.GetType().GetField("_targetFootRotation", Flags).SetValue(leg, target);
            try
            {
                object[] args = { 0f, 0f };
                Assert.That((bool)Invoke(leg, "TrySolve", args), Is.True);
                Assert.That(Quaternion.Angle(foot.rotation, target), Is.LessThan(0.06f));
                Assert.That(Vector3.Distance(foot.position, originalPosition), Is.LessThan(0.000001f));
                Assert.That(Vector3.Distance(upper.position, lower.position), Is.EqualTo(upperLength).Within(0.000001f));
                Assert.That(Vector3.Distance(lower.position, foot.position), Is.EqualTo(lowerLength).Within(0.000001f));
                Assert.That(toes.localRotation, Is.EqualTo(toeRotation));
            }
            finally { Invoke(leg, "RestorePose"); }
            Assert.That(upper.localRotation, Is.EqualTo(upperRotation));
            Assert.That(lower.localRotation, Is.EqualTo(lowerRotation));
            Assert.That(Quaternion.Angle(foot.localRotation, footRotation), Is.LessThan(0.06f));
        }

        private static void AssertUngroundedTargets(Animator animator, Transform[] bones,
            Vector3[] positions, Quaternion[] rotations)
        {
            Transform left = animator.GetBoneTransform(HumanBodyBones.LeftUpperLeg);
            Transform right = animator.GetBoneTransform(HumanBodyBones.RightUpperLeg);
            Transform leftFoot = animator.GetBoneTransform(HumanBodyBones.LeftFoot);
            Transform rightFoot = animator.GetBoneTransform(HumanBodyBones.RightFoot);
            for (int i = 0; i < bones.Length; i++)
            {
                // 무릎 방향은 교정하되 발·발끝과 상체의 원래 세계 자세는 보존해야 함.
                bool isLeg = bones[i].IsChildOf(left) || bones[i].IsChildOf(right);
                bool isFoot = bones[i].IsChildOf(leftFoot) || bones[i].IsChildOf(rightFoot);
                if (isLeg && !isFoot) continue;
                Assert.That(Vector3.Distance(bones[i].position, positions[i]), Is.LessThan(0.00003f), bones[i].name);
                Assert.That(Quaternion.Angle(bones[i].rotation, rotations[i]), Is.LessThan(0.06f), bones[i].name);
            }
        }

        private static void AssertPartialFootFramesRejected(object controller, object grounding, AnimationClip clip)
        {
            Type sampleType = typeof(FBXVmdPipeline).Assembly.GetType("Fbx2Vmd.FBXImporter.HumanoidFootContactSample", true);
            object Sample(Quaternion? left, Quaternion? right) => Activator.CreateInstance(sampleType, Flags, null,
                new object[] { Vector3.zero, Vector3.forward, Vector3.right, Vector3.one, null, left, right }, null);
            Action<float> evaluate = time => Invoke(controller, "EvaluateEditorContactReference", time);
            foreach (object[] pair in new[] {
                new[] { Sample(Quaternion.identity, null), Sample(Quaternion.identity, null) },
                new[] { Sample(null, Quaternion.identity), Sample(null, Quaternion.identity) },
                new[] { Sample(Quaternion.identity, Quaternion.identity), Sample(null, null) } })
            {
                Array samples = Array.CreateInstance(sampleType, 2);
                samples.SetValue(pair[0], 0);
                samples.SetValue(pair[1], 1);
                Assert.That(Invoke(grounding, "TryPrepare", samples, 1f, clip, evaluate), Is.False,
                    "한쪽 발 또는 일부 시각의 회전 정보 누락을 기존 경로로 숨기면 안 됨");
            }
        }

        private static void AssertStatus(object controller, string expected) =>
            Assert.That(Property(controller, "LastGroundingStatus").ToString(), Is.EqualTo(expected));

        private static void AssertPose(Transform[] bones, Vector3[] positions, Quaternion[] rotations)
        {
            for (int i = 0; i < bones.Length; i++)
            {
                Assert.That(Vector3.Distance(bones[i].position, positions[i]), Is.LessThan(0.00003f), bones[i].name);
                Assert.That(Quaternion.Angle(bones[i].rotation, rotations[i]), Is.LessThan(0.06f), bones[i].name);
            }
        }
    }
}
