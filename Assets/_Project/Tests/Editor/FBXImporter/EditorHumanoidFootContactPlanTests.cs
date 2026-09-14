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
                    hasFrames ? rotations : null, hasFrames ? (Quaternion?)Quaternion.identity : null };
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

        private static BoneWeight Weight() => new BoneWeight { boneIndex0 = 0, weight0 = 1f };
    }
}
