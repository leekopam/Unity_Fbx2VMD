using System;
using System.Reflection;
using Fbx2Vmd.FBXImporter;
using NUnit.Framework;
using UnityEngine;

namespace Tests.Editor.FBXImporter
{
    public class EditorHumanoidFootContactPlanTests
    {
        private const BindingFlags Flags = BindingFlags.Instance | BindingFlags.Static |
            BindingFlags.Public | BindingFlags.NonPublic;

        [TestCase(true)]
        [TestCase(false)]
        public void Given_RotationAroundFixedToe_When_BuildingContacts_Then_UsesAvailableRotationReference(bool hasFrames)
        {
            var root = new GameObject("접촉점 회전 검증") { hideFlags = HideFlags.HideAndDontSave };
            var foot = new GameObject("발").transform;
            foot.SetParent(root.transform, false);
            var toes = new GameObject("발가락").transform;
            toes.SetParent(foot, false);
            toes.localPosition = Vector3.forward * 0.2f;
            var mesh = new Mesh();
            object sampler = null;
            try
            {
                mesh.vertices = new[] { new Vector3(-0.05f, 0f, -0.1f), new Vector3(0.05f, 0f, -0.1f),
                    new Vector3(-0.05f, 0f, 0.3f), new Vector3(0.05f, 0f, 0.3f) };
                mesh.triangles = new[] { 0, 2, 1, 1, 2, 3 };
                mesh.boneWeights = new[] { Weight(), Weight(), Weight(), Weight() };
                mesh.bindposes = new[] { foot.worldToLocalMatrix };
                var skin = root.AddComponent<SkinnedMeshRenderer>();
                skin.sharedMesh = mesh;
                skin.bones = new[] { foot };
                Type samplerType = typeof(FBXVmdPipeline).Assembly.GetType("Fbx2Vmd.FBXImporter.EditorHumanoidFootSoleSampler", true);
                object[] create = { foot, toes, new[] { skin }, Vector3.up, null };
                Assert.That(samplerType.GetMethod("TryCreate", Flags).Invoke(null, create), Is.True);
                sampler = create[4];
                Assert.That(samplerType.GetMethod("TrySample", Flags).Invoke(sampler, null), Is.True);
                object[] select = { true, Vector3.up, -1, Vector3.zero };
                Assert.That(samplerType.GetMethod("TrySelectContact", Flags).Invoke(sampler, select), Is.True);
                const float ratio = 2f;
                Vector3 pivot = (Vector3)select[3] / ratio;
                var rotations = new[] { Quaternion.identity, Quaternion.AngleAxis(30f, Vector3.right) };
                var origins = new[] { Vector3.zero, pivot - rotations[1] * pivot };
                var tips = new[] { Vector3.forward * 0.1f, origins[1] + rotations[1] * Vector3.forward * 0.1f };
                var sources = new[] { origins, tips };
                var weights = new[] { new Vector2(0f, 1f), new Vector2(0f, 1f) };
                Action<float> evaluate = time =>
                {
                    int frame = Mathf.RoundToInt(time * 60f);
                    foot.SetPositionAndRotation(origins[frame] * ratio, rotations[frame]);
                };
                Type planType = typeof(FBXVmdPipeline).Assembly.GetType("Fbx2Vmd.FBXImporter.EditorHumanoidFootContactPlan", true);
                MethodInfo build = planType.GetMethod("TryBuild", Flags);
                object[] args = { foot, sampler, sources, weights, Quaternion.identity, ratio, 60f, 1f / 60f, evaluate, null,
                    hasFrames ? rotations : null, hasFrames ? (Quaternion?)Quaternion.identity : null,
                    null, 0f, 0f };
                Assert.That(build.Invoke(null, args), Is.True);
                object first = planType.GetMethod("GetSample", Flags).Invoke(args[9], new object[] { 1, 0f });
                object second = planType.GetMethod("GetSample", Flags).Invoke(args[9], new object[] { 1, 1f });
                Vector3 movement = (Vector3)second.GetType().GetField("Anchor", Flags).GetValue(second) -
                    (Vector3)first.GetType().GetField("Anchor", Flags).GetValue(first);
                Vector3 expected = hasFrames ? Vector3.zero : Vector3.ProjectOnPlane((tips[1] - tips[0]) * ratio, Vector3.up);
                Assert.That(Vector3.Distance(movement, expected), Is.LessThan(0.000001f),
                    "발끝을 축으로 회전할 때 발목 이동이 고정 접촉점에 추가되면 안 됨. 회전 정보가 없으면 기존 본 이동 경로를 유지함");
                if (hasFrames)
                {
                    foreach (object[] invalid in new[] {
                        new object[] { null, Quaternion.identity }, new object[] { rotations, null },
                        new object[] { new[] { Quaternion.identity }, Quaternion.identity },
                        new object[] { new[] { Quaternion.identity, default(Quaternion) }, Quaternion.identity },
                        new object[] { rotations, new Quaternion(float.NaN, 0f, 0f, 1f) } })
                    {
                        args[10] = invalid[0];
                        args[11] = invalid[1];
                        Assert.That(build.Invoke(null, args), Is.False, "부분 제공·누락·손상된 회전 정보를 정상 접촉 계획으로 바꾸면 안 됨");
                        Assert.That(args[9], Is.Null);
                    }
                }
            }
            finally
            {
                (sampler as IDisposable)?.Dispose();
                UnityEngine.Object.DestroyImmediate(root);
                UnityEngine.Object.DestroyImmediate(mesh);
            }
        }

        [Test]
        public void Given_PinnedAnchorPolicy_When_SourceDrifts_Then_SupportAnchorStaysFixed()
        {
            // 지지 구간 핀: 앵커가 원본 수평 이동을 따라가지 않고 프레임 간 동일해야 함.
            using (Rig rig = CreateRig(out Transform foot))
            {
                Vector3[] anchors = BuildAnchors(foot, rig.Sampler,
                    frameCount: 5, driftPerFrame: 0.01f,
                    policies: new[] { 0, 1, 1, 1, 0 },
                    pinRelease: 0f, trackStep: 0f, ratio: 2f);
                Assert.That(Vector3.Distance(anchors[1], anchors[2]),
                    Is.LessThan(0.000001f));
                Assert.That(Vector3.Distance(anchors[2], anchors[3]),
                    Is.LessThan(0.000001f));
                // 핀 시작점은 진입 위치를 접어 유지하고, 해제 뒤에는 원본 추종이 이어짐.
                Assert.That(Vector3.Distance(anchors[0], anchors[1]),
                    Is.EqualTo(0.02f).Within(0.000001f));
                Assert.That(Vector3.Distance(anchors[3], anchors[4]),
                    Is.EqualTo(0.02f).Within(0.000001f));
            }
        }

        [Test]
        public void Given_PinOverrun_When_SourceMovesBeyondReleaseDistance_Then_PinReleases()
        {
            using (Rig rig = CreateRig(out Transform foot))
            {
                Vector3[] anchors = BuildAnchors(foot, rig.Sampler,
                    frameCount: 5, driftPerFrame: 0.01f,
                    policies: new[] { 0, 1, 1, 1, 1 },
                    pinRelease: 0.015f, trackStep: 0f, ratio: 2f);
                Assert.That(Vector3.Distance(anchors[1], anchors[2]),
                    Is.LessThan(0.000001f));
                // 0.02 이동이 해제 거리(0.015)를 넘는 프레임 3에서 핀이 풀리되
                // 앵커가 점프하지 않고 그 자리에서 추종을 재개해야 함.
                Assert.That(Vector3.Distance(anchors[2], anchors[3]),
                    Is.LessThan(0.000001f));
                Assert.That(Vector3.Distance(anchors[3], anchors[4]),
                    Is.EqualTo(0.02f).Within(0.000001f));
            }
        }

        [Test]
        public void Given_RapidFreeAnchor_When_Tracking_Then_FreeMotionIsStepLimited()
        {
            using (Rig rig = CreateRig(out Transform foot))
            {
                Vector3[] anchors = BuildAnchors(foot, rig.Sampler,
                    frameCount: 4, driftPerFrame: 0.01f,
                    policies: null, pinRelease: 0f, trackStep: 0.008f, ratio: 2f);
                // 자유 구간 앵커 이동이 프레임당 상한(0.008)을 넘지 않음.
                Assert.That(Vector3.Distance(anchors[0], anchors[1]),
                    Is.EqualTo(0.008f).Within(0.000001f));
                Assert.That(Vector3.Distance(anchors[0], anchors[3]),
                    Is.EqualTo(0.024f).Within(0.000001f));
            }
        }

        [Test]
        public void Given_ContactPointIdentityChange_When_Interpolating_Then_SwitchesDiscretely()
        {
            // 인접 프레임의 접촉점 신원이 다르면 지면과 무관한 가상점을 보간하지 않고
            // 보간 계수가 가리키는 실제 프레임의 피벗·앵커를 그대로 사용해야 함.
            Type planType = typeof(FBXVmdPipeline).Assembly.GetType("Fbx2Vmd.FBXImporter.EditorHumanoidFootContactPlan", true);
            object plan = Activator.CreateInstance(planType, Flags, null, new object[] { 3 }, null);
            Array channels = (Array)planType.GetField("_frames", Flags).GetValue(plan);
            Type frameType = planType.GetNestedType("Frame", Flags);
            Array frames = (Array)channels.GetValue(0);
            Vector3 heelAnchor = Vector3.zero, toeAnchor = Vector3.forward;
            object MakeFrame(int point, Vector3 anchor) => Activator.CreateInstance(
                frameType, Flags, null, new object[] { point, anchor, false }, null);
            frames.SetValue(MakeFrame(0, heelAnchor), 0);
            frames.SetValue(MakeFrame(2, toeAnchor), 1);
            frames.SetValue(MakeFrame(2, toeAnchor), 2);
            MethodInfo getSample = planType.GetMethod("GetSample", Flags);
            object low = getSample.Invoke(plan, new object[] { 0, 0.4f });
            object high = getSample.Invoke(plan, new object[] { 0, 0.6f });
            Assert.That((Vector3)low.GetType().GetField("Anchor", Flags).GetValue(low),
                Is.EqualTo(heelAnchor));
            Assert.That((Vector3)high.GetType().GetField("Anchor", Flags).GetValue(high),
                Is.EqualTo(toeAnchor));
            FieldInfo firstPoint = low.GetType().GetField("_firstPoint", Flags);
            Assert.That((int)firstPoint.GetValue(low), Is.EqualTo(0));
            Assert.That((int)firstPoint.GetValue(high), Is.EqualTo(2));
        }

        private static Vector3[] BuildAnchors(Transform foot, object sampler,
            int frameCount, float driftPerFrame, int[] policies,
            float pinRelease, float trackStep, float ratio)
        {
            var sources = new[] { new Vector3[frameCount], new Vector3[frameCount] };
            var weights = new Vector2[frameCount];
            for (int index = 0; index < frameCount; index++)
            {
                sources[0][index] = Vector3.right * index * driftPerFrame;
                weights[index] = new Vector2(1f, 0f);
            }

            Assembly assembly = typeof(FBXVmdPipeline).Assembly;
            Type policyType = assembly.GetType("Fbx2Vmd.FBXImporter.HumanoidFootAnchorPolicy", true);
            Array policyArray = null;
            if (policies != null)
            {
                policyArray = Array.CreateInstance(policyType, frameCount);
                for (int index = 0; index < frameCount; index++)
                    policyArray.SetValue(Enum.ToObject(policyType, policies[index]), index);
            }

            Type planType = assembly.GetType("Fbx2Vmd.FBXImporter.EditorHumanoidFootContactPlan", true);
            MethodInfo build = planType.GetMethod("TryBuild", Flags);
            object[] args = { foot, sampler, sources, weights, Quaternion.identity, ratio, 60f, 1f / 60f,
                new Action<float>(_ => foot.SetPositionAndRotation(Vector3.zero, Quaternion.identity)),
                null, null, null, policyArray, pinRelease, trackStep };
            Assert.That(build.Invoke(null, args), Is.True);
            MethodInfo getSample = planType.GetMethod("GetSample", Flags);
            var anchors = new Vector3[frameCount];
            for (int index = 0; index < frameCount; index++)
            {
                object sample = getSample.Invoke(args[9], new object[] { 0, (float)index });
                anchors[index] = (Vector3)sample.GetType().GetField("Anchor", Flags).GetValue(sample);
            }
            return anchors;
        }

        private sealed class Rig : IDisposable
        {
            internal GameObject Root;
            internal Mesh Mesh;
            internal object Sampler;
            public void Dispose()
            {
                (Sampler as IDisposable)?.Dispose();
                UnityEngine.Object.DestroyImmediate(Root);
                UnityEngine.Object.DestroyImmediate(Mesh);
            }
        }

        private static Rig CreateRig(out Transform foot)
        {
            var root = new GameObject("앵커 정책 검증") { hideFlags = HideFlags.HideAndDontSave };
            foot = new GameObject("발").transform;
            foot.SetParent(root.transform, false);
            var toes = new GameObject("발가락").transform;
            toes.SetParent(foot, false);
            toes.localPosition = Vector3.forward * 0.2f;
            var mesh = new Mesh();
            mesh.vertices = new[] { new Vector3(-0.05f, 0f, -0.1f), new Vector3(0.05f, 0f, -0.1f),
                new Vector3(-0.05f, 0f, 0.3f), new Vector3(0.05f, 0f, 0.3f) };
            mesh.triangles = new[] { 0, 2, 1, 1, 2, 3 };
            mesh.boneWeights = new[] { Weight(), Weight(), Weight(), Weight() };
            mesh.bindposes = new[] { foot.worldToLocalMatrix };
            var skin = root.AddComponent<SkinnedMeshRenderer>();
            skin.sharedMesh = mesh;
            skin.bones = new[] { foot };
            Type samplerType = typeof(FBXVmdPipeline).Assembly.GetType("Fbx2Vmd.FBXImporter.EditorHumanoidFootSoleSampler", true);
            object[] create = { foot, toes, new[] { skin }, Vector3.up, null };
            Assert.That(samplerType.GetMethod("TryCreate", Flags).Invoke(null, create), Is.True);
            foot.SetPositionAndRotation(Vector3.zero, Quaternion.identity);
            Assert.That(samplerType.GetMethod("TrySample", Flags).Invoke(create[4], null), Is.True);
            return new Rig { Root = root, Mesh = mesh, Sampler = create[4] };
        }

        private static BoneWeight Weight() => new BoneWeight { boneIndex0 = 0, weight0 = 1f };
    }
}
