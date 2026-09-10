using System;
using System.Collections;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace Tests.Editor.FBXImporter
{
    public class NativeSkinningDualQuaternionBlendCalculatorTests
    {
        private const string TargetAssetPath =
            "Assets/_Project/Model/YYB Hatsune Miku_default/YYB Hatsune Miku_default_1.0ver.fbx";
        private const string ClipAssetPath =
            "Assets/Resources/Import_FBX/satisfaction_2.fbx";
        private const int FixtureSleeveVertexIndex = 17178;
        private const int UserReportedFoldFrame = 1327;

        [OneTimeSetUp]
        public void EnsureHumanoidClipImport()
        {
            Type configuratorType = RequireProductType(
                "EditorHumanoidClipImportConfigurator");
            MethodInfo method = configuratorType.GetMethod(
                "EnsureHumanoid",
                BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic);
            Assert.That(method, Is.Not.Null);
            method.Invoke(null, new object[] { ClipAssetPath });
        }

        [Test]
        public void Given_SurfaceRestRecovery_When_BlendingTowardDualQuaternion_Then_ProducesSafeVolumeCorrection()
        {
            GameObject target = InstantiateTarget();
            object controller = CreateController();
            var bakedMesh = new Mesh();

            try
            {
                Animator animator = RequireHumanoidAnimator(target);
                AnimationClip clip = LoadHumanoidClip();
                object selection = FindFixtureSelection(animator);
                object contract = BuildSurfaceContract(selection);
                SkinnedMeshRenderer renderer = ReadProperty<SkinnedMeshRenderer>(
                    selection,
                    "Renderer");
                Invoke(
                    controller,
                    "PrepareWithArmDirectionReference",
                    animator,
                    clip,
                    AssetDatabase.LoadAssetAtPath<GameObject>(ClipAssetPath));
                Assert.That(
                    (bool)Invoke(
                        controller,
                        "Seek",
                        UserReportedFoldFrame / clip.frameRate),
                    Is.True);
                renderer.BakeMesh(bakedMesh, false);

                Type surfaceCalculatorType = RequireProductType(
                    "NativeSkinningSurfaceCorrectionCalculator");
                MethodInfo surfaceMethod = surfaceCalculatorType.GetMethod(
                    "TryCalculateFastValidatedFrame",
                    BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic);
                Assert.That(surfaceMethod, Is.Not.Null);
                object[] surfaceArguments = { bakedMesh.vertices, contract, null };
                Assert.That(
                    (bool)surfaceMethod.Invoke(null, surfaceArguments),
                    Is.True);
                object surfaceResult = surfaceArguments[2];
                Assert.That(
                    ReadProperty<bool>(surfaceResult, "UsedRestShapeRecovery"),
                    Is.True,
                    "휴식 형상 붕괴가 검출된 프레임만 체적 보정을 이어서 적용해야 합니다.");
                object surfaceCorrectedVertices = ReadProperty<object>(
                    surfaceResult,
                    "CorrectedVertices");

                Type samplerType = RequireProductType(
                    "NativeSkinningBoneTransformSampler");
                MethodInfo sampleMethod = samplerType.GetMethod(
                    "TrySample",
                    BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic);
                Assert.That(sampleMethod, Is.Not.Null);
                object[] sampleArguments = { renderer, null };
                Assert.That(
                    (bool)sampleMethod.Invoke(null, sampleArguments),
                    Is.True,
                    "현재 자세의 본 행렬을 메시 로컬 공간으로 표본화해야 합니다.");

                Type calculatorType = RequireProductType(
                    "NativeSkinningDualQuaternionBlendCalculator");
                MethodInfo calculateMethod = calculatorType.GetMethod(
                    "TryCalculate",
                    BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic);
                Assert.That(calculateMethod, Is.Not.Null);
                Mesh sourceMesh = renderer.sharedMesh;
                object[] calculateArguments =
                {
                    surfaceCorrectedVertices,
                    sourceMesh.vertices,
                    sourceMesh.boneWeights,
                    sampleArguments[1],
                    contract,
                    null
                };

                bool wasCalculated =
                    (bool)calculateMethod.Invoke(null, calculateArguments);
                Assert.That(wasCalculated, Is.True);
                object result = calculateArguments[5];
                float blend = ReadProperty<float>(result, "Blend");
                float armChainLength = ReadProperty<float>(contract, "ArmChainLength");
                Assert.That(blend, Is.GreaterThanOrEqualTo(0.025f));
                Assert.That(blend, Is.LessThanOrEqualTo(0.300001f));
                int[] changedVertexIndices = ReadProperty<int[]>(
                    result,
                    "ChangedVertexIndices");
                Assert.That(changedVertexIndices.Length, Is.GreaterThanOrEqualTo(20));
                CollectionAssert.IsSubsetOf(
                    changedVertexIndices,
                    ReadProperty<int[]>(contract, "EvaluatedVertexIndices"),
                    "체적 보정은 팔꿈치 평가 창 밖의 손목·상완 표면을 바꾸면 안 됩니다.");
                Assert.That(
                    ReadProperty<float>(result, "MaximumVertexDisplacement"),
                    Is.GreaterThanOrEqualTo(armChainLength * 0.0005f),
                    "화면에 거의 영향을 주지 못했던 기존 국소 보정보다 충분한 표면 이동이 필요합니다.");
                Assert.That(
                    ReadProperty<float>(result, "MaximumRestEdgeStrainIncrease"),
                    Is.LessThanOrEqualTo(0.050001f));
                Assert.That(ReadProperty<int>(result, "NewDegenerateFaceCount"), Is.Zero);
                Assert.That(ReadProperty<int>(result, "ReversedFaceCount"), Is.Zero);

                Type vertexCalculatorType = RequireProductType(
                    "NativeSkinningDualQuaternionVertexCalculator");
                MethodInfo buildTransformsMethod = vertexCalculatorType.GetMethod(
                    "TryBuildTransforms",
                    BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic);
                Assert.That(buildTransformsMethod, Is.Not.Null);
                object[] transformArguments = { sampleArguments[1], null };
                Assert.That(
                    (bool)buildTransformsMethod.Invoke(null, transformArguments),
                    Is.True);
                MethodInfo preparedMethod = calculatorType.GetMethod(
                    "TryCalculateWithPreparedTransforms",
                    BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic);
                Assert.That(
                    preparedMethod,
                    Is.Not.Null,
                    "동일 프레임의 본 변환을 계약마다 다시 만들지 않는 계산 경계가 필요합니다.");
                object blendContract = BuildBlendContract(
                    sourceMesh.boneWeights,
                    contract);
                object[] preparedArguments =
                {
                    surfaceCorrectedVertices,
                    sourceMesh.vertices,
                    transformArguments[1],
                    blendContract,
                    null
                };
                Assert.That(
                    (bool)preparedMethod.Invoke(null, preparedArguments),
                    Is.True);
                object preparedResult = preparedArguments[4];
                Assert.That(
                    ReadProperty<float>(preparedResult, "Blend"),
                    Is.EqualTo(blend).Within(0.000001f));
                CollectionAssert.AreEqual(
                    changedVertexIndices,
                    ReadProperty<int[]>(preparedResult, "ChangedVertexIndices"));
            }
            finally
            {
                DisposeController(controller);
                UnityEngine.Object.DestroyImmediate(bakedMesh);
                UnityEngine.Object.DestroyImmediate(target);
            }
        }

        private static object FindFixtureSelection(Animator animator)
        {
            Type selectorType = RequireProductType(
                "NativeSkinningArmSurfaceSelector");
            MethodInfo findMethod = selectorType.GetMethod(
                "FindAll",
                BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic);
            Assert.That(findMethod, Is.Not.Null);
            return ((IEnumerable)findMethod.Invoke(null, new object[] { animator }))
                .Cast<object>()
                .Single(selection => ReadProperty<int[]>(
                        selection,
                        "EvaluatedVertexIndices")
                    .Contains(FixtureSleeveVertexIndex));
        }

        private static object BuildSurfaceContract(object selection)
        {
            Type builderType = RequireProductType(
                "NativeSkinningSurfaceContractBuilder");
            MethodInfo buildMethod = builderType.GetMethod(
                "TryBuild",
                BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic);
            Assert.That(buildMethod, Is.Not.Null);
            object[] arguments = { selection, null };
            Assert.That((bool)buildMethod.Invoke(null, arguments), Is.True);
            return arguments[1];
        }

        private static object BuildBlendContract(
            BoneWeight[] boneWeights,
            object surfaceContract)
        {
            Type builderType = RequireProductType(
                "NativeSkinningDualQuaternionBlendContractBuilder");
            MethodInfo buildMethod = builderType.GetMethod(
                "TryBuild",
                BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic);
            Assert.That(buildMethod, Is.Not.Null);
            object[] arguments = { boneWeights, surfaceContract, null };
            Assert.That((bool)buildMethod.Invoke(null, arguments), Is.True);
            return arguments[2];
        }

        private static GameObject InstantiateTarget()
        {
            GameObject asset = AssetDatabase.LoadAssetAtPath<GameObject>(
                TargetAssetPath);
            Assert.That(asset, Is.Not.Null);
            GameObject target = UnityEngine.Object.Instantiate(asset);
            target.hideFlags = HideFlags.HideAndDontSave;
            target.SetActive(true);
            return target;
        }

        private static Animator RequireHumanoidAnimator(GameObject target)
        {
            Animator animator = target.GetComponentInChildren<Animator>(true);
            Assert.That(animator, Is.Not.Null);
            Assert.That(animator.avatar, Is.Not.Null);
            Assert.That(animator.avatar.isValid && animator.avatar.isHuman, Is.True);
            return animator;
        }

        private static AnimationClip LoadHumanoidClip()
        {
            AnimationClip clip = AssetDatabase.LoadAllAssetsAtPath(ClipAssetPath)
                .OfType<AnimationClip>()
                .FirstOrDefault(candidate =>
                    !candidate.name.StartsWith("__", StringComparison.Ordinal) &&
                    candidate.humanMotion);
            Assert.That(clip, Is.Not.Null);
            return clip;
        }

        private static object CreateController()
        {
            Type controllerType = RequireProductType(
                "HumanoidMotionPlaybackController");
            return Activator.CreateInstance(controllerType, nonPublic: true);
        }

        private static object Invoke(
            object target,
            string methodName,
            params object[] arguments)
        {
            MethodInfo method = target.GetType().GetMethod(
                methodName,
                BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            Assert.That(method, Is.Not.Null);
            return method.Invoke(target, arguments);
        }

        private static void DisposeController(object controller)
        {
            if (controller != null)
            {
                Invoke(controller, "Dispose");
            }
        }

        private static Type RequireProductType(string shortName)
        {
            Type type = typeof(Fbx2Vmd.FBXImporter.FBXVmdPipeline).Assembly.GetType(
                $"Fbx2Vmd.FBXImporter.{shortName}",
                throwOnError: false);
            Assert.That(type, Is.Not.Null, $"{shortName} 타입이 필요합니다.");
            return type;
        }

        private static T ReadProperty<T>(object target, string propertyName)
        {
            Assert.That(target, Is.Not.Null);
            PropertyInfo property = target.GetType().GetProperty(
                propertyName,
                BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            Assert.That(property, Is.Not.Null);
            return (T)property.GetValue(target);
        }
    }
}
