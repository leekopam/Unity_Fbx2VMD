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
    public class NativeSkinningSurfaceCorrectionCalculatorTests
    {
        private const string TargetAssetPath =
            "Assets/_Project/Model/YYB Hatsune Miku_default/YYB Hatsune Miku_default_1.0ver.fbx";
        private static string ClipAssetPath;
        private HistoricalHumanoidClipFixture _fixture;
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
        public void CreateHistoricalHumanoidClipFixture()
        {
            _fixture = new HistoricalHumanoidClipFixture();
            ClipAssetPath = _fixture.ClipAssetPath;
        }

        [OneTimeTearDown]
        public void DeleteHistoricalHumanoidClipFixture()
        {
            _fixture?.Dispose();
            ClipAssetPath = null;
        }

        [TestCase(0.0498f, false)]
        [TestCase(0.04995f, true)]
        [TestCase(0.06f, true)]
        public void Given_RecoveryCandidate_When_CheckingStrain_Then_PreservesPlaybackMargin(
            float strainIncrease, bool expectedDefect)
        {
            var baseline = new[] { Vector3.zero, Vector3.right };
            var candidate = new[] { Vector3.zero, Vector3.right * (1f + strainIncrease) };
            Type calculator = RequireProductType("NativeSkinningSurfaceCorrectionCalculator");
            const BindingFlags flags = BindingFlags.Public | BindingFlags.NonPublic |
                BindingFlags.Static | BindingFlags.Instance;
            Type edgeType = calculator.GetNestedType("LocalEdge", flags);
            Assert.That(edgeType, Is.Not.Null);
            object edge = edgeType.GetConstructors(flags).Single().Invoke(new object[]
            {
                0, 1, 0, 1, 1f, 1f
            });
            Array edges = Array.CreateInstance(edgeType, 1);
            edges.SetValue(edge, 0);
            MethodInfo check = calculator.GetMethod("HasRestShapeRecoveryStrainDefect", flags);
            Assert.That(check, Is.Not.Null);

            Assert.That((bool)check.Invoke(null, new object[] { baseline, candidate, edges }),
                Is.EqualTo(expectedDefect));
        }

        [Test]
        public void Given_RoundedPlaybackEdge_When_ProjectingRecovery_Then_ReusesDeltaWithinStrainLimit()
        {
            // YYB 6130의 동일 모서리에서 탐색 이력에 따른 반올림 차이를 재현함.
            var baseline = new[]
            {
                new Vector3(0.2578931152820587f, 1.1593098640441895f, 0.05783587694168091f),
                new Vector3(0.2606698274612427f, 1.1630269289016724f, 0.057139575481414795f)
            };
            var playback = new[]
            {
                new Vector3(0.2578931450843811f, 1.1593098640441895f, 0.05783587694168091f),
                new Vector3(0.26066985726356506f, 1.1630268096923828f, 0.057139575481414795f)
            };
            var storedDelta = new[]
            {
                new Vector3(0.000120818615f, -0.0000929832458f, 0.000145033f),
                new Vector3(0.000288486481f, 0.0000141859055f, 0.0000111162663f)
            };
            const float restLength = 0.004112801f;
            const float qualityLimit = 0.0500001f;
            Func<Vector3[], Vector3[], float> strain = (input, delta) =>
                Mathf.Abs(Vector3.Distance(input[0] + delta[0], input[1] + delta[1]) / restLength - 1f) -
                Mathf.Abs(Vector3.Distance(input[0], input[1]) / restLength - 1f);
            Assert.That(strain(baseline, storedDelta), Is.LessThanOrEqualTo(qualityLimit));
            Assert.That(strain(playback, storedDelta), Is.GreaterThan(qualityLimit));

            Type calculator = RequireProductType("NativeSkinningSurfaceCorrectionCalculator");
            const BindingFlags flags = BindingFlags.Public | BindingFlags.NonPublic |
                BindingFlags.Static | BindingFlags.Instance;
            Type edgeType = calculator.GetNestedType("LocalEdge", flags);
            Type worksetType = calculator.GetNestedType("LocalWorkset", flags);
            Assert.That(edgeType, Is.Not.Null);
            Assert.That(worksetType, Is.Not.Null);
            object edge = edgeType.GetConstructors(flags).Single().Invoke(new object[]
            {
                0, 1, 0, 1, Vector3.Distance(baseline[0], baseline[1]), restLength
            });
            Array edges = Array.CreateInstance(edgeType, 1);
            edges.SetValue(edge, 0);
            object workset = worksetType.GetConstructors(flags).Single().Invoke(new object[]
            {
                new[] { 0, 1 }, new[] { 0, 1 }, Array.Empty<int>(), Array.Empty<int>(), edges
            });
            Vector3[] proposed = baseline.Select((value, index) => value + storedDelta[index]).ToArray();
            MethodInfo project = calculator.GetMethod("ProjectRestShapeStrainBounds", flags);
            Assert.That(project, Is.Not.Null);
            project.Invoke(null, new object[] { baseline, proposed, workset });
            Vector3[] generatedDelta = proposed.Select((value, index) => value - baseline[index]).ToArray();

            Assert.That(generatedDelta.Any(delta => delta.sqrMagnitude > 0f), Is.True);
            Assert.That(strain(baseline, generatedDelta), Is.LessThanOrEqualTo(qualityLimit));
            Assert.That(strain(playback, generatedDelta), Is.LessThanOrEqualTo(qualityLimit));
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
                    Is.EqualTo(0.00239240704f).Within(0.0000001f));
                Assert.That(
                    ReadProperty<float>(result, "MaximumEdgeLengthStrain"),
                    Is.EqualTo(0.0150148664f).Within(0.000001f));
                Assert.That(
                    ReadProperty<int[]>(result, "CorrectedVertexIndices").Length,
                    Is.EqualTo(74));
                MethodInfo validatedMethod = calculatorType.GetMethod(
                    "TryCalculateStrongValidatedFrame",
                    BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic);
                Assert.That(validatedMethod, Is.Not.Null);
                object[] validatedArguments = { bakedMesh.vertices, contract, null };
                Assert.That((bool)validatedMethod.Invoke(null, validatedArguments), Is.True);
                object validatedResult = validatedArguments[2];
                CollectionAssert.AreEqual(
                    ReadProperty<IReadOnlyList<Vector3>>(result, "CorrectedVertices"),
                    ReadProperty<IReadOnlyList<Vector3>>(validatedResult, "CorrectedVertices"));
                CollectionAssert.AreEqual(
                    ReadProperty<int[]>(result, "CorrectedVertexIndices"),
                    ReadProperty<int[]>(validatedResult, "CorrectedVertexIndices"));
            }
            finally
            {
                DisposeController(controller);
                UnityEngine.Object.DestroyImmediate(bakedMesh);
                UnityEngine.Object.DestroyImmediate(target);
            }
        }

        [TestCase(0f)]
        [TestCase(0.0000001f)]
        [TestCase(-0.0000001f)]
        public void Given_HeadroomFoldFrame_When_CalculatingStrongCorrection_Then_PreservesStrictQuality(
            float coordinateOffset)
        {
            const BindingFlags flags = BindingFlags.Public | BindingFlags.NonPublic |
                BindingFlags.Static | BindingFlags.Instance;
            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(
                "Assets/Plugins/VMDRecorderSample/Models/TestModel/testPrefab.prefab");
            Assert.That(prefab, Is.Not.Null);
            GameObject target = UnityEngine.Object.Instantiate(prefab);
            target.hideFlags = HideFlags.HideAndDontSave;
            try
            {
                Animator animator = RequireHumanoidAnimator(target);
                MethodInfo select = RequireProductType("NativeSkinningArmSurfaceSelector")
                    .GetMethod("FindAll", flags);
                IList selections = (IList)select.Invoke(null, new object[] { animator });
                object selection = selections.Cast<object>().First(candidate =>
                    ReadProperty<SkinnedMeshRenderer>(candidate, "Renderer").name == "U_Char_0" &&
                    ReadProperty<object>(candidate, "Side").ToString() == "Left");
                object contract = BuildSurfaceContract(selection);
                SkinnedMeshRenderer renderer = ReadProperty<SkinnedMeshRenderer>(
                    contract, "Renderer");
                Dictionary<string, Matrix4x4> pose = CreateHeadroomFoldPose();
                Matrix4x4[] matrices = renderer.bones.Select(bone =>
                    pose.TryGetValue(bone.name, out Matrix4x4 matrix)
                        ? matrix : Matrix4x4.identity).ToArray();
                int[] triangles = ReadProperty<int[]>(contract, "Triangles");
                int[] faces = ReadProperty<int[]>(contract, "AffectedFaceIndices");
                Assert.That(faces.SelectMany(face => triangles.Skip(face * 3).Take(3))
                    .Distinct().Count(), Is.EqualTo(149));
                BoneWeight[] weights = renderer.sharedMesh.boneWeights;
                IEnumerable<int> pairVertices = ReadProperty<IEnumerable>(contract, "FacePairs")
                    .Cast<object>().SelectMany(pair => new[] {
                        ReadProperty<int>(pair, "FirstEdge"),
                        ReadProperty<int>(pair, "SecondEdge"),
                        ReadProperty<int>(pair, "FirstOpposite"),
                        ReadProperty<int>(pair, "SecondOpposite") });
                foreach (int vertex in faces.SelectMany(face =>
                    triangles.Skip(face * 3).Take(3)).Concat(pairVertices).Distinct())
                {
                    BoneWeight weight = weights[vertex];
                    int[] indices = { weight.boneIndex0, weight.boneIndex1,
                        weight.boneIndex2, weight.boneIndex3 };
                    float[] values = { weight.weight0, weight.weight1,
                        weight.weight2, weight.weight3 };
                    for (int influence = 0; influence < values.Length; influence++)
                    {
                        if (values[influence] > 0f)
                        {
                            Assert.That(pose.ContainsKey(
                                renderer.bones[indices[influence]].name), Is.True,
                                "표면에 영향을 주는 모든 본의 실제 자세가 필요합니다.");
                        }
                    }
                }

                var baseline = new Vector3[renderer.sharedMesh.vertexCount];
                MethodInfo sample = RequireProductType("NativeSkinningLinearBlendCalculator")
                    .GetMethod("TryComputeBaseline", flags);
                Assert.That((bool)sample.Invoke(null, new object[] {
                    renderer.sharedMesh.vertices, weights, matrices, baseline }), Is.True);
                for (int vertex = 0; vertex < baseline.Length; vertex++)
                {
                    baseline[vertex].x += coordinateOffset;
                }
                Vector3[] original = baseline.ToArray();
                Type calculator = RequireProductType("NativeSkinningSurfaceCorrectionCalculator");
                object[] arguments = { baseline, contract, null };
                Assert.That((bool)calculator.GetMethod("TryCalculateStrongValidatedFrame", flags)
                    .Invoke(null, arguments), Is.True);
                object result = arguments[2];
                Assert.That(ReadProperty<int>(result, "InitialSharpFoldCount"), Is.EqualTo(3));
                Assert.That((bool)calculator.GetMethod("IsWithinStrongQuality", flags)
                    .Invoke(null, new[] { result, contract }), Is.True);
                Assert.That(ReadProperty<bool>(result, "UsedRestShapeRecovery"), Is.False);
                CollectionAssert.AreEqual(original, baseline);

                IReadOnlyList<Vector3> corrected = ReadProperty<IReadOnlyList<Vector3>>(
                    result, "CorrectedVertices");
                Vector3[] replayed = baseline.ToArray();
                foreach (int vertex in ReadProperty<int[]>(result, "CorrectedVertexIndices"))
                {
                    replayed[vertex] += corrected[vertex] - baseline[vertex];
                }
                object edges = calculator.GetMethod("BuildFrameEdges", flags)
                    .Invoke(null, new object[] { baseline, contract });
                object replayedResult = calculator.GetMethod("EvaluateResult", flags)
                    .Invoke(null, new object[] { baseline, replayed, contract, edges,
                        3, Array.Empty<int>() });
                Assert.That((bool)calculator.GetMethod("IsWithinStrongQuality", flags)
                    .Invoke(null, new[] { replayedResult, contract }), Is.True,
                    "저장된 희소 보정량의 재적용도 원본 기준 품질을 보존해야 합니다.");
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(target);
            }
        }

        private static Dictionary<string, Matrix4x4> CreateHeadroomFoldPose()
        {
            // testPrefab 6423의 표면 149정점에 영향을 주는 여섯 본을 고정함.
            // 다른 본은 이 계약 밖이므로 원본 FBX 재임포트 없이 실패 자세를 재현함.
            return new Dictionary<string, Matrix4x4>
            {
                ["37.joint_Torso3"] = new Matrix4x4(
                    new Vector4(0.961225748f, 0.0786845312f, -0.264298975f, 0f),
                    new Vector4(-0.0642664805f, 0.995956242f, 0.0627764463f, 0f),
                    new Vector4(0.26816982f, -0.0433567613f, 0.962395608f, 0f),
                    new Vector4(0.0586901754f, -0.0812749714f, -0.104389913f, 1f)),
                ["64.joint_LeftShoulder"] = new Matrix4x4(
                    new Vector4(0.894796848f, -0.416743994f, -0.160197288f, 0f),
                    new Vector4(0.412553489f, 0.908941746f, -0.0602038205f, 0f),
                    new Vector4(0.170699611f, -0.0122197755f, 0.985247374f, 0f),
                    new Vector4(-0.632031858f, 0.0323685184f, 0.0758851841f, 1f)),
                ["65.joint_LeftArm"] = new Matrix4x4(
                    new Vector4(0.405990303f, -0.912267089f, -0.0542254858f, 0f),
                    new Vector4(0.663611054f, 0.335085452f, -0.668833315f, 0f),
                    new Vector4(0.628324866f, 0.235555246f, 0.74143213f, 0f),
                    new Vector4(-1.04631758f, 0.792116344f, 0.954974294f, 1f)),
                ["89.joint_J_Sec_L_UpperArm"] = new Matrix4x4(
                    new Vector4(0.405990303f, -0.912267089f, -0.0542254858f, 0f),
                    new Vector4(0.663611054f, 0.335085452f, -0.668833315f, 0f),
                    new Vector4(0.628324866f, 0.235555246f, 0.74143213f, 0f),
                    new Vector4(-1.0463177f, 0.792116106f, 0.954974473f, 1f)),
                ["66.joint_LeftElbow"] = new Matrix4x4(
                    new Vector4(-0.824297607f, -0.565128744f, 0.034101516f, 0f),
                    new Vector4(-0.242579877f, 0.298119575f, -0.923189938f, 0f),
                    new Vector4(0.511554837f, -0.769255579f, -0.382828116f, 0f),
                    new Vector4(-0.278523207f, 0.949202955f, 1.30775058f, 1f)),
                ["88.joint_J_Sec_L_LowerArm"] = new Matrix4x4(
                    new Vector4(-0.824297607f, -0.565128744f, 0.034101516f, 0f),
                    new Vector4(-0.242579877f, 0.298119575f, -0.923189938f, 0f),
                    new Vector4(0.511554837f, -0.769255579f, -0.382828116f, 0f),
                    new Vector4(-0.278523237f, 0.949203074f, 1.30775058f, 1f)),
            };
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
