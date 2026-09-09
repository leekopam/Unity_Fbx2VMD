using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace Tests.Editor.FBXImporter
{
    public class NativeSkinningArmSurfaceSelectorTests
    {
        private const string TargetAssetPath =
            "Assets/_Project/Model/YYB Hatsune Miku_default/YYB Hatsune Miku_default_1.0ver.fbx";
        private const int FixtureSleeveVertexIndex = 17178;

        [Test]
        public void Given_HumanoidSkin_When_FindingArmSurfaces_Then_RecoversFixtureSleeveWithoutNames()
        {
            GameObject target = InstantiateTarget();

            try
            {
                Animator animator = target.GetComponent<Animator>();
                Assert.That(animator, Is.Not.Null);
                IList selections = FindAll(animator);
                Assert.That(selections.Count, Is.GreaterThan(0));

                object fixtureSelection = selections
                    .Cast<object>()
                    .FirstOrDefault(selection =>
                    {
                        SkinnedMeshRenderer renderer = ReadProperty<SkinnedMeshRenderer>(
                            selection,
                            "Renderer");
                        int[] vertices = ReadProperty<int[]>(
                            selection,
                            "EvaluatedVertexIndices");
                        return renderer.sharedMesh.vertexCount > FixtureSleeveVertexIndex &&
                            vertices.Contains(FixtureSleeveVertexIndex);
                    });

                Assert.That(fixtureSelection, Is.Not.Null,
                    "제품 선택기가 테스트 전용 Renderer 이름이나 seed 없이 기존 소매 표면을 찾아야 합니다.");
                Assert.That(
                    ReadProperty<int>(fixtureSelection, "ConnectedVertexCount"),
                    Is.EqualTo(1551),
                    "연결 성분은 기존 검증 oracle과 같은 소매 메시를 선택해야 합니다.");
                Assert.That(
                    ReadProperty<int[]>(fixtureSelection, "EvaluatedVertexIndices").Length,
                    Is.InRange(780, 820),
                    "팔 길이 상대 창은 기존 0.09m 진단 범위를 과도하게 벗어나면 안 됩니다.");
                Assert.That(
                    ReadProperty<float>(fixtureSelection, "ArmChainLength"),
                    Is.EqualTo(0.398099f).Within(0.0001f));
                Assert.That(
                    ReadProperty<object>(fixtureSelection, "Side").ToString(),
                    Is.EqualTo("Left"));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(target);
            }
        }

        [Test]
        public void Given_HumanoidSkin_When_FindingArmSurfaces_Then_CoversBothSides()
        {
            GameObject target = InstantiateTarget();

            try
            {
                Animator animator = target.GetComponent<Animator>();
                IList selections = FindAll(animator);
                string[] sides = selections
                    .Cast<object>()
                    .Select(selection => ReadProperty<object>(selection, "Side").ToString())
                    .Distinct()
                    .ToArray();

                Assert.That(sides, Does.Contain("Left"));
                Assert.That(sides, Does.Contain("Right"));
                foreach (object selection in selections)
                {
                    Assert.That(
                        ReadProperty<int>(selection, "MixedWeightVertexCount"),
                        Is.GreaterThanOrEqualTo(4));
                    Assert.That(
                        ReadProperty<int[]>(selection, "EvaluatedVertexIndices").Length,
                        Is.GreaterThanOrEqualTo(4));
                }
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(target);
            }
        }

        private static IList FindAll(Animator animator)
        {
            Type selectorType = typeof(Fbx2Vmd.FBXImporter.FBXVmdPipeline).Assembly.GetType(
                "Fbx2Vmd.FBXImporter.NativeSkinningArmSurfaceSelector",
                throwOnError: false);
            Assert.That(selectorType, Is.Not.Null,
                "모델 중립 팔 표면 선택기가 필요합니다.");
            MethodInfo method = selectorType.GetMethod(
                "FindAll",
                BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic);
            Assert.That(method, Is.Not.Null);
            return (IList)method.Invoke(null, new object[] { animator });
        }

        private static T ReadProperty<T>(object target, string propertyName)
        {
            PropertyInfo property = target.GetType().GetProperty(
                propertyName,
                BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            Assert.That(property, Is.Not.Null, $"{propertyName} 속성이 필요합니다.");
            return (T)property.GetValue(target);
        }

        private static GameObject InstantiateTarget()
        {
            GameObject source = AssetDatabase.LoadAssetAtPath<GameObject>(TargetAssetPath);
            Assert.That(source, Is.Not.Null, $"기준 모델을 찾을 수 없습니다: {TargetAssetPath}");
            GameObject target = UnityEngine.Object.Instantiate(source);
            target.hideFlags = HideFlags.HideAndDontSave;
            target.SetActive(true);
            return target;
        }
    }
}
