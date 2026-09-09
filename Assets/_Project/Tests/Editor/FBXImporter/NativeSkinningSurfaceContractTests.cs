using System;
using System.Collections;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace Tests.Editor.FBXImporter
{
    public class NativeSkinningSurfaceContractTests
    {
        private const string TargetAssetPath =
            "Assets/_Project/Model/YYB Hatsune Miku_default/YYB Hatsune Miku_default_1.0ver.fbx";
        private const int FixtureSleeveVertexIndex = 17178;

        [Test]
        public void Given_AutomaticSleeveSelection_When_BuildingContract_Then_MatchesValidatedOracle()
        {
            GameObject target = InstantiateTarget();

            try
            {
                Animator animator = target.GetComponent<Animator>();
                object selection = FindFixtureSelection(animator);
                Type builderType = RequireProductType(
                    "NativeSkinningSurfaceContractBuilder");
                MethodInfo method = builderType.GetMethod(
                    "TryBuild",
                    BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic);
                Assert.That(method, Is.Not.Null);
                object[] arguments = { selection, null };

                Assert.That((bool)method.Invoke(null, arguments), Is.True);
                object contract = arguments[1];
                Assert.That(contract, Is.Not.Null);
                Assert.That(
                    ReadProperty<int>(contract, "VertexCount"),
                    Is.EqualTo(21169));
                Assert.That(
                    ReadProperty<Vector3[]>(contract, "RestVertices").Length,
                    Is.EqualTo(21169));
                Assert.That(
                    ReadProperty<int>(contract, "EvaluatedVertexCount"),
                    Is.EqualTo(799));
                Assert.That(
                    ReadProperty<int>(contract, "FacePairCount"),
                    Is.EqualTo(2431),
                    "자동 선택 계약은 기존 전체 clip 검증에 사용한 면 쌍 수와 같아야 합니다.");
                float[] restAngles = ReadProperty<float[]>(
                    contract,
                    "RestAnglesDegrees");
                Assert.That(restAngles.Length, Is.EqualTo(2431));
                Assert.That(restAngles.All(angle =>
                    !float.IsNaN(angle) &&
                    !float.IsInfinity(angle) &&
                    angle >= 0f &&
                    angle <= 180f), Is.True);
                Array restLengths = ReadProperty<Array>(
                    contract,
                    "FacePairRestLengths");
                Assert.That(restLengths.Length, Is.EqualTo(2431));
                Assert.That(
                    ReadProperty<float>(contract, "ArmChainLength"),
                    Is.EqualTo(0.398099f).Within(0.0001f));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(target);
            }
        }

        private static object FindFixtureSelection(Animator animator)
        {
            Type selectorType = RequireProductType("NativeSkinningArmSurfaceSelector");
            MethodInfo findMethod = selectorType.GetMethod(
                "FindAll",
                BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic);
            IList selections = (IList)findMethod.Invoke(null, new object[] { animator });
            object selection = selections.Cast<object>().FirstOrDefault(candidate =>
                ReadProperty<int[]>(candidate, "EvaluatedVertexIndices")
                    .Contains(FixtureSleeveVertexIndex));
            Assert.That(selection, Is.Not.Null);
            return selection;
        }

        private static Type RequireProductType(string shortName)
        {
            Type type = typeof(Fbx2Vmd.FBXImporter.FBXVmdPipeline).Assembly.GetType(
                "Fbx2Vmd.FBXImporter." + shortName,
                throwOnError: false);
            Assert.That(type, Is.Not.Null, $"{shortName} 제품 타입이 필요합니다.");
            return type;
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
