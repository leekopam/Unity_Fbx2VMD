using System;
using System.Collections;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace Tests.Editor.FBXImporter
{
    public class NativeSkinningSurfaceCorrectionCalculatorTests
    {
        private const string TargetAssetPath =
            "Assets/_Project/Model/YYB Hatsune Miku_default/YYB Hatsune Miku_default_1.0ver.fbx";
        private const string ClipAssetPath =
            "Assets/Resources/Import_FBX/satisfaction_2.fbx";
        private const int FixtureSleeveVertexIndex = 17178;
        private const int UserReportedFoldFrame = 1327;
        private const int ConnectedRecoveryFrame = 584;
        private const int MixedPassOverlapFrame = 717;
        private const int QualityPreservingBlendFrame = 754;
        private const int CompressionRecoveryFrame = 759;
        private const int RecoveryOnlyFrame = 985;
        private const int RestStrainConflictFrame = 1950;
        private const int AdjacentSharpFoldFrame = 3714;
        private const int SlowConvergenceFrame = 6379;
        private const int ExtendedConvergenceFrame = 6416;
        private const int OrientationConflictFrame = 7602;
        private const int CounterexampleFrame = 4476;

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
        public void Given_UserReportedElbowFrame_When_CalculatingCorrection_Then_DetectsVisibleFold()
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
                Type calculatorType = RequireProductType(
                    "NativeSkinningSurfaceCorrectionCalculator");
                MethodInfo calculateMethod = calculatorType.GetMethod(
                    "TryCalculateFast",
                    BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic);
                Assert.That(calculateMethod, Is.Not.Null);
                object[] arguments = { bakedMesh.vertices, contract, null };

                Assert.That((bool)calculateMethod.Invoke(null, arguments), Is.True);
                object result = arguments[2];
                int initialFoldCount = ReadProperty<int>(
                    result,
                    "InitialSharpFoldCount");
                Assert.That(
                    initialFoldCount,
                    Is.GreaterThan(0),
                    "사용자가 제보한 frame 1327의 소매 접힘을 탐지하지 못하면 현재 이면각 계약만으로 해결됐다고 볼 수 없습니다.");
                Assert.That(
                    ReadProperty<int[]>(result, "CorrectedVertexIndices").Length,
                    Is.GreaterThan(0),
                    "frame 1327의 휴식 형상 붕괴는 감지뿐 아니라 실제 정점 보정으로 이어져야 합니다.");
                Assert.That(ReadProperty<bool>(result, "IsSafe"), Is.True);
                Assert.That(
                    ReadProperty<bool>(result, "UsedRestShapeRecovery"),
                    Is.True);
                Assert.That(
                    ReadProperty<int>(result, "UnresolvedRestShapeRecoveryCount"),
                    Is.Zero);
                Assert.That(
                    ReadProperty<float>(result, "MaximumRestEdgeStrainIncrease"),
                    Is.LessThanOrEqualTo(0.050001f),
                    "복원 과정은 기준 자세보다 새로운 엣지 변형을 5% 넘게 추가하면 안 됩니다.");
                Assert.That(
                    ReadProperty<float>(
                        result,
                        "MinimumRestShapeAngleImprovementDegrees"),
                    Is.GreaterThan(0f),
                    "휴식 형상 복원은 붕괴된 면 쌍의 이면각을 실제로 개선해야 합니다.");
                float ratioBefore = ReadProperty<float>(
                    result,
                    "MinimumRestEdgeLengthRatioBeforeRecovery");
                float ratioAfter = ReadProperty<float>(
                    result,
                    "MinimumRestEdgeLengthRatioAfterRecovery");
                Assert.That(
                    ratioAfter - ratioBefore,
                    Is.GreaterThanOrEqualTo(0.05f),
                    "최소 엣지 비율의 순회복은 허용 가능한 새 휴식 변형 상한보다 커야 합니다.");
                MethodInfo qualityMethod = calculatorType.GetMethod(
                    "IsWithinFastQuality",
                    BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic);
                Assert.That(qualityMethod, Is.Not.Null);
                Assert.That(
                    (bool)qualityMethod.Invoke(null, new[] { result, contract }),
                    Is.True,
                    "휴식 형상 복원을 현재 프레임 대비 길이 변화로 오판하면 전처리 단계에서 결과가 폐기됩니다.");
            }
            finally
            {
                DisposeController(controller);
                UnityEngine.Object.DestroyImmediate(bakedMesh);
                UnityEngine.Object.DestroyImmediate(target);
            }
        }

        [Test]
        public void Given_FoldFreeSurface_When_CalculatingFastCorrection_Then_ReusesInputVertices()
        {
            GameObject target = InstantiateTarget();

            try
            {
                Animator animator = RequireHumanoidAnimator(target);
                object selection = FindFixtureSelection(animator);
                object contract = BuildSurfaceContract(selection);
                SkinnedMeshRenderer renderer = ReadProperty<SkinnedMeshRenderer>(
                    selection,
                    "Renderer");
                Vector3[] vertices = renderer.sharedMesh.vertices;
                Type calculatorType = RequireProductType(
                    "NativeSkinningSurfaceCorrectionCalculator");
                MethodInfo calculateMethod = calculatorType.GetMethod(
                    "TryCalculateFast",
                    BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic);
                Assert.That(calculateMethod, Is.Not.Null);
                object[] arguments = { vertices, contract, null };

                Assert.That((bool)calculateMethod.Invoke(null, arguments), Is.True);
                object result = arguments[2];
                Assert.That(result, Is.Not.Null);
                Assert.That(ReadProperty<int>(result, "InitialSharpFoldCount"), Is.Zero);
                Assert.That(
                    ReadProperty<object>(result, "CorrectedVertices"),
                    Is.SameAs(vertices));
                Assert.That(
                    ReadProperty<int[]>(result, "CorrectedVertexIndices"),
                    Is.Empty);

                MethodInfo validatedFrameMethod = calculatorType.GetMethod(
                    "TryCalculateFastValidatedFrame",
                    BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic);
                Assert.That(validatedFrameMethod, Is.Not.Null,
                    "프레임을 한 번 검증한 사전 계산기는 전체 메시 검사를 계약마다 반복하면 안 됩니다.");
                object[] validatedArguments = { vertices, contract, null };
                Assert.That(
                    (bool)validatedFrameMethod.Invoke(null, validatedArguments),
                    Is.True);
                Assert.That(
                    ReadProperty<object>(validatedArguments[2], "CorrectedVertices"),
                    Is.SameAs(vertices));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(target);
            }
        }

        [Test]
        public void Given_MixedFoldTypes_When_CalculatingFastCorrection_Then_ResolvesBothPasses()
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
                    (bool)Invoke(controller, "Seek", 571f / clip.frameRate),
                    Is.True);
                renderer.BakeMesh(bakedMesh, false);
                Type calculatorType = RequireProductType(
                    "NativeSkinningSurfaceCorrectionCalculator");
                MethodInfo calculateMethod = calculatorType.GetMethod(
                    "TryCalculateFast",
                    BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic);
                Assert.That(calculateMethod, Is.Not.Null);
                object[] arguments = { bakedMesh.vertices, contract, null };

                Assert.That((bool)calculateMethod.Invoke(null, arguments), Is.True);
                object result = arguments[2];
                Assert.That(
                    ReadProperty<int>(result, "InitialSharpFoldCount"),
                    Is.EqualTo(3));
                Assert.That(
                    ReadProperty<int>(result, "ResidualSharpFoldCount"),
                    Is.Zero,
                    "휴식 형상 붕괴와 기존 급접힘이 함께 있어도 두 보정 패스를 모두 완료해야 합니다.");
                Assert.That(ReadProperty<int>(result, "NewSharpFoldCount"), Is.Zero);
                Assert.That(ReadProperty<int>(result, "NewDegenerateFaceCount"), Is.Zero);
                Assert.That(ReadProperty<int>(result, "ReversedFaceCount"), Is.Zero);
                Assert.That(ReadProperty<bool>(result, "UsedRestShapeRecovery"), Is.True);
                Assert.That(
                    ReadProperty<int>(result, "UnresolvedRestShapeRecoveryCount"),
                    Is.Zero);
                Assert.That(ReadProperty<bool>(result, "IsSafe"), Is.True);
                MethodInfo qualityMethod = calculatorType.GetMethod(
                    "IsWithinFastQuality",
                    BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic);
                Assert.That(qualityMethod, Is.Not.Null);
                Assert.That(
                    (bool)qualityMethod.Invoke(null, new[] { result, contract }),
                    Is.True);
            }
            finally
            {
                DisposeController(controller);
                UnityEngine.Object.DestroyImmediate(bakedMesh);
                UnityEngine.Object.DestroyImmediate(target);
            }
        }

        [Test]
        public void Given_ConnectedRestShapeCollapses_When_CalculatingFastCorrection_Then_ResolvesDetectorContract()
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
                        ConnectedRecoveryFrame / clip.frameRate),
                    Is.True);
                renderer.BakeMesh(bakedMesh, false);
                Type calculatorType = RequireProductType(
                    "NativeSkinningSurfaceCorrectionCalculator");
                MethodInfo calculateMethod = calculatorType.GetMethod(
                    "TryCalculateFast",
                    BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic);
                Assert.That(calculateMethod, Is.Not.Null);
                object[] arguments = { bakedMesh.vertices, contract, null };

                Assert.That((bool)calculateMethod.Invoke(null, arguments), Is.True);
                object result = arguments[2];
                Assert.That(ReadProperty<int>(result, "InitialSharpFoldCount"), Is.EqualTo(5));
                Assert.That(ReadProperty<int>(result, "ResidualSharpFoldCount"), Is.Zero);
                Assert.That(ReadProperty<int>(result, "NewSharpFoldCount"), Is.Zero);
                Assert.That(ReadProperty<int>(result, "NewDegenerateFaceCount"), Is.Zero);
                Assert.That(ReadProperty<int>(result, "ReversedFaceCount"), Is.Zero);
                Assert.That(ReadProperty<bool>(result, "UsedRestShapeRecovery"), Is.True);
                Assert.That(
                    ReadProperty<int>(result, "UnresolvedRestShapeRecoveryCount"),
                    Is.Zero,
                    "연결된 휴식 형상 붕괴는 개별 이면각의 단조 변화가 아니라 최종 검출기 계약으로 판정해야 합니다.");
                Assert.That(ReadProperty<bool>(result, "IsSafe"), Is.True);
                MethodInfo qualityMethod = calculatorType.GetMethod(
                    "IsWithinFastQuality",
                    BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic);
                Assert.That(qualityMethod, Is.Not.Null);
                Assert.That(
                    (bool)qualityMethod.Invoke(null, new[] { result, contract }),
                    Is.True);
            }
            finally
            {
                DisposeController(controller);
                UnityEngine.Object.DestroyImmediate(bakedMesh);
                UnityEngine.Object.DestroyImmediate(target);
            }
        }

        [Test]
        public void Given_MixedPassOverlap_When_CalculatingFastCorrection_Then_UsesCoupledProjection()
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
                        MixedPassOverlapFrame / clip.frameRate),
                    Is.True);
                renderer.BakeMesh(bakedMesh, false);
                Type calculatorType = RequireProductType(
                    "NativeSkinningSurfaceCorrectionCalculator");
                MethodInfo calculateMethod = calculatorType.GetMethod(
                    "TryCalculateFast",
                    BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic);
                Assert.That(calculateMethod, Is.Not.Null);
                object[] arguments = { bakedMesh.vertices, contract, null };

                Assert.That((bool)calculateMethod.Invoke(null, arguments), Is.True);
                object result = arguments[2];
                Assert.That(ReadProperty<int>(result, "InitialSharpFoldCount"), Is.EqualTo(4));
                Assert.That(ReadProperty<int>(result, "ResidualSharpFoldCount"), Is.Zero);
                Assert.That(ReadProperty<int>(result, "UnresolvedRestShapeRecoveryCount"), Is.Zero);
                Assert.That(ReadProperty<int>(result, "NewSharpFoldCount"), Is.Zero);
                Assert.That(ReadProperty<int>(result, "NewDegenerateFaceCount"), Is.Zero);
                Assert.That(ReadProperty<int>(result, "ReversedFaceCount"), Is.Zero);
                Assert.That(
                    ReadProperty<float>(result, "MaximumRestEdgeStrainIncrease"),
                    Is.LessThanOrEqualTo(0.050001f));
                Assert.That(ReadProperty<bool>(result, "IsSafe"), Is.True);
                MethodInfo qualityMethod = calculatorType.GetMethod(
                    "IsWithinFastQuality",
                    BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic);
                Assert.That(qualityMethod, Is.Not.Null);
                Assert.That(
                    (bool)qualityMethod.Invoke(null, new[] { result, contract }),
                    Is.True);
            }
            finally
            {
                DisposeController(controller);
                UnityEngine.Object.DestroyImmediate(bakedMesh);
                UnityEngine.Object.DestroyImmediate(target);
            }
        }

        [Test]
        public void Given_CoupledProjectionIntroducesAdjacentDefect_When_CalculatingFastCorrection_Then_SelectsQualityPreservingBlend()
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
                        QualityPreservingBlendFrame / clip.frameRate),
                    Is.True);
                renderer.BakeMesh(bakedMesh, false);
                Type calculatorType = RequireProductType(
                    "NativeSkinningSurfaceCorrectionCalculator");
                MethodInfo calculateMethod = calculatorType.GetMethod(
                    "TryCalculateFast",
                    BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic);
                Assert.That(calculateMethod, Is.Not.Null);
                object[] arguments = { bakedMesh.vertices, contract, null };

                Assert.That((bool)calculateMethod.Invoke(null, arguments), Is.True);
                object result = arguments[2];
                Assert.That(ReadProperty<int>(result, "InitialSharpFoldCount"), Is.EqualTo(3));
                Assert.That(ReadProperty<int>(result, "ResidualSharpFoldCount"), Is.Zero);
                Assert.That(ReadProperty<int>(result, "UnresolvedRestShapeRecoveryCount"), Is.Zero);
                Assert.That(ReadProperty<int>(result, "NewSharpFoldCount"), Is.Zero);
                Assert.That(ReadProperty<int>(result, "NewDegenerateFaceCount"), Is.Zero);
                Assert.That(ReadProperty<int>(result, "ReversedFaceCount"), Is.Zero);
                Assert.That(
                    ReadProperty<float>(result, "MaximumRestEdgeStrainIncrease"),
                    Is.LessThanOrEqualTo(0.050001f));
                Assert.That(
                    ReadProperty<int[]>(result, "CorrectedVertexIndices"),
                    Is.Not.Empty);
                MethodInfo qualityMethod = calculatorType.GetMethod(
                    "IsWithinFastQuality",
                    BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic);
                Assert.That(qualityMethod, Is.Not.Null);
                Assert.That(
                    (bool)qualityMethod.Invoke(null, new[] { result, contract }),
                    Is.True);
            }
            finally
            {
                DisposeController(controller);
                UnityEngine.Object.DestroyImmediate(bakedMesh);
                UnityEngine.Object.DestroyImmediate(target);
            }
        }

        [TestCase(CompressionRecoveryFrame, 3)]
        [TestCase(RecoveryOnlyFrame, 2)]
        [TestCase(RestStrainConflictFrame, 5)]
        [TestCase(AdjacentSharpFoldFrame, 5)]
        [TestCase(SlowConvergenceFrame, 4)]
        [TestCase(ExtendedConvergenceFrame, 3)]
        [TestCase(OrientationConflictFrame, 4)]
        public void Given_RecoveryProjectionConflictsWithSurfaceConstraints_When_CalculatingFastCorrection_Then_RecoversCompressedEdges(
            int frameIndex,
            int expectedInitialFoldCount)
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
                        frameIndex / clip.frameRate),
                    Is.True);
                renderer.BakeMesh(bakedMesh, false);
                Type calculatorType = RequireProductType(
                    "NativeSkinningSurfaceCorrectionCalculator");
                MethodInfo calculateMethod = calculatorType.GetMethod(
                    "TryCalculateFast",
                    BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic);
                Assert.That(calculateMethod, Is.Not.Null);
                object[] arguments = { bakedMesh.vertices, contract, null };

                Assert.That((bool)calculateMethod.Invoke(null, arguments), Is.True);
                object result = arguments[2];
                Assert.That(
                    ReadProperty<int>(result, "InitialSharpFoldCount"),
                    Is.EqualTo(expectedInitialFoldCount));
                Assert.That(ReadProperty<int>(result, "ResidualSharpFoldCount"), Is.Zero);
                Assert.That(ReadProperty<int>(result, "UnresolvedRestShapeRecoveryCount"), Is.Zero);
                Assert.That(ReadProperty<int>(result, "NewSharpFoldCount"), Is.Zero);
                Assert.That(ReadProperty<int>(result, "NewDegenerateFaceCount"), Is.Zero);
                Assert.That(ReadProperty<int>(result, "ReversedFaceCount"), Is.Zero);
                Assert.That(
                    ReadProperty<float>(result, "MaximumRestEdgeStrainIncrease"),
                    Is.LessThanOrEqualTo(0.050001f));
                MethodInfo qualityMethod = calculatorType.GetMethod(
                    "IsWithinFastQuality",
                    BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic);
                Assert.That(qualityMethod, Is.Not.Null);
                Assert.That(
                    (bool)qualityMethod.Invoke(null, new[] { result, contract }),
                    Is.True);
            }
            finally
            {
                DisposeController(controller);
                UnityEngine.Object.DestroyImmediate(bakedMesh);
                UnityEngine.Object.DestroyImmediate(target);
            }
        }

        [Test]
        public void Given_ValidatedFoldFrame_When_CalculatingStrongCorrection_Then_MatchesDiagnosticOracle()
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
                    (bool)Invoke(controller, "Seek", CounterexampleFrame / clip.frameRate),
                    Is.True);
                renderer.BakeMesh(bakedMesh, false);

                Type calculatorType = RequireProductType(
                    "NativeSkinningSurfaceCorrectionCalculator");
                MethodInfo calculateMethod = calculatorType.GetMethod(
                    "TryCalculateStrong",
                    BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic);
                Assert.That(calculateMethod, Is.Not.Null);
                object[] arguments = { bakedMesh.vertices, contract, null };

                Assert.That((bool)calculateMethod.Invoke(null, arguments), Is.True);
                object result = arguments[2];
                Assert.That(result, Is.Not.Null);
                Assert.That(ReadProperty<int>(result, "InitialSharpFoldCount"), Is.EqualTo(5));
                Assert.That(ReadProperty<int>(result, "ResidualSharpFoldCount"), Is.Zero);
                Assert.That(ReadProperty<int>(result, "NewSharpFoldCount"), Is.Zero);
                Assert.That(ReadProperty<int>(result, "NewDegenerateFaceCount"), Is.Zero);
                Assert.That(ReadProperty<int>(result, "ReversedFaceCount"), Is.Zero);
                Assert.That(ReadProperty<bool>(result, "IsSafe"), Is.True);
                Assert.That(
                    ReadProperty<float>(result, "MaximumVertexDisplacement"),
                    Is.EqualTo(0.00203031185f).Within(0.0000001f));
                Assert.That(
                    ReadProperty<float>(result, "MaximumEdgeLengthStrain"),
                    Is.EqualTo(0.0211385787f).Within(0.000001f));
                Assert.That(
                    ReadProperty<int[]>(result, "CorrectedVertexIndices").Length,
                    Is.EqualTo(74));
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
            MethodInfo method = RequireProductType("NativeSkinningArmSurfaceSelector")
                .GetMethod(
                    "FindAll",
                    BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic);
            Assert.That(method, Is.Not.Null);
            IList selections = (IList)method.Invoke(null, new object[] { animator });
            object selection = selections.Cast<object>().FirstOrDefault(candidate =>
                ReadProperty<int[]>(candidate, "EvaluatedVertexIndices")
                    .Contains(FixtureSleeveVertexIndex));
            Assert.That(selection, Is.Not.Null);
            return selection;
        }

        private static object BuildSurfaceContract(object selection)
        {
            MethodInfo method = RequireProductType("NativeSkinningSurfaceContractBuilder")
                .GetMethod(
                    "TryBuild",
                    BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic);
            Assert.That(method, Is.Not.Null);
            object[] arguments = { selection, null };
            Assert.That((bool)method.Invoke(null, arguments), Is.True);
            return arguments[1];
        }

        private static Type RequireProductType(string shortName)
        {
            Type type = typeof(Fbx2Vmd.FBXImporter.FBXVmdPipeline).Assembly.GetType(
                "Fbx2Vmd.FBXImporter." + shortName,
                throwOnError: false);
            Assert.That(type, Is.Not.Null, $"{shortName} 제품 타입이 필요합니다.");
            return type;
        }

        private static object CreateController()
        {
            return Activator.CreateInstance(
                RequireProductType("HumanoidMotionPlaybackController"),
                nonPublic: true);
        }

        private static object Invoke(
            object target,
            string methodName,
            params object[] arguments)
        {
            MethodInfo method = target.GetType().GetMethod(
                methodName,
                BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            Assert.That(method, Is.Not.Null, $"{methodName} 메서드가 필요합니다.");
            return method.Invoke(target, arguments);
        }

        private static T ReadProperty<T>(object target, string propertyName)
        {
            PropertyInfo property = target.GetType().GetProperty(
                propertyName,
                BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            Assert.That(property, Is.Not.Null, $"{propertyName} 속성이 필요합니다.");
            return (T)property.GetValue(target);
        }

        private static void DisposeController(object controller)
        {
            if (controller != null)
            {
                Invoke(controller, "Dispose");
            }
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
    }
}
