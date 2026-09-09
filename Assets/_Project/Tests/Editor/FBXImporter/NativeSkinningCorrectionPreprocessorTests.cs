using System;
using System.Collections;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace Tests.Editor.FBXImporter
{
    public class NativeSkinningCorrectionPreprocessorTests
    {
        private const string TargetAssetPath =
            "Assets/_Project/Model/YYB Hatsune Miku_default/YYB Hatsune Miku_default_1.0ver.fbx";
        private const string ClipAssetPath =
            "Assets/Resources/Import_FBX/satisfaction_2.fbx";
        private const int FixtureSleeveVertexIndex = 17178;
        private const int CounterexampleFrame = 4476;

        [OneTimeSetUp]
        public void EnsureHumanoidClipImport()
        {
            MethodInfo method = RequireProductType(
                    "EditorHumanoidClipImportConfigurator")
                .GetMethod(
                    "EnsureHumanoid",
                    BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic);
            Assert.That(method, Is.Not.Null);
            method.Invoke(null, new object[] { ClipAssetPath });
        }

        [Test]
        public void Given_OneValidatedFrame_When_Preprocessing_Then_BuildsApplicableSparseCache()
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
                Vector3[] baselineVertices = bakedMesh.vertices;

                Type contractType = contract.GetType();
                Array contracts = Array.CreateInstance(contractType, 1);
                contracts.SetValue(contract, 0);
                Type preprocessorType = RequireProductType(
                    "NativeSkinningCorrectionPreprocessor");
                MethodInfo buildMethod = preprocessorType.GetMethod(
                    "TryBuild",
                    BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic);
                Assert.That(buildMethod, Is.Not.Null);
                Func<int, SkinnedMeshRenderer, Vector3[]> frameReader =
                    (_, requestedRenderer) =>
                    {
                        Assert.That(requestedRenderer, Is.SameAs(renderer));
                        return baselineVertices.ToArray();
                    };
                object[] arguments = { 1, contracts, frameReader, null, null };

                Assert.That((bool)buildMethod.Invoke(null, arguments), Is.True);
                object result = arguments[4];
                Assert.That(result, Is.Not.Null);
                Assert.That(ReadProperty<int>(result, "FrameCount"), Is.EqualTo(1));
                Assert.That(ReadProperty<int>(result, "CorrectedFrameCount"), Is.EqualTo(1));
                Assert.That(ReadProperty<int>(result, "FallbackFrameCount"), Is.EqualTo(1));
                Assert.That(ReadProperty<int>(result, "CorrectionEntryCount"), Is.EqualTo(74));
                Array rendererCorrections = ReadProperty<Array>(
                    result,
                    "RendererCorrections");
                Assert.That(rendererCorrections.Length, Is.EqualTo(1));
                object rendererCorrection = rendererCorrections.GetValue(0);
                Assert.That(
                    ReadProperty<SkinnedMeshRenderer>(rendererCorrection, "Renderer"),
                    Is.SameAs(renderer));
                object cache = ReadProperty<object>(rendererCorrection, "Cache");
                var correctedVertices = baselineVertices.ToList();
                object[] applyArguments = { 0, correctedVertices, 0 };
                Assert.That((bool)Invoke(cache, "TryApply", applyArguments), Is.True);
                Assert.That(applyArguments[2], Is.EqualTo(74));
            }
            finally
            {
                DisposeController(controller);
                UnityEngine.Object.DestroyImmediate(bakedMesh);
                UnityEngine.Object.DestroyImmediate(target);
            }
        }

        [Test]
        public void Given_TwoFrames_When_ProcessingIncrementally_Then_ExposesOnlyCompletedCache()
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
                Vector3[] baselineVertices = bakedMesh.vertices;
                Array contracts = CreateContractArray(contract);
                Func<int, SkinnedMeshRenderer, Vector3[]> frameReader =
                    (_, requestedRenderer) =>
                    {
                        Assert.That(requestedRenderer, Is.SameAs(renderer));
                        return baselineVertices.ToArray();
                    };
                MethodInfo createMethod = RequireProductType(
                        "NativeSkinningCorrectionPreprocessor")
                    .GetMethod(
                        "TryCreateSession",
                        BindingFlags.Static | BindingFlags.Public |
                        BindingFlags.NonPublic);
                Assert.That(createMethod, Is.Not.Null);
                object[] createArguments =
                {
                    2,
                    contracts,
                    frameReader,
                    null,
                    null
                };

                Assert.That((bool)createMethod.Invoke(null, createArguments), Is.True);
                object session = createArguments[4];
                Assert.That(session, Is.Not.Null);
                Assert.That(ReadProperty<int>(session, "ProcessedFrameCount"), Is.Zero);
                Assert.That(ReadProperty<bool>(session, "IsComplete"), Is.False);
                Assert.That(ReadProperty<object>(session, "Result"), Is.Null);

                Assert.That((bool)Invoke(session, "TryProcessNextFrame"), Is.True);
                Assert.That(ReadProperty<int>(session, "ProcessedFrameCount"), Is.EqualTo(1));
                Assert.That(ReadProperty<bool>(session, "IsComplete"), Is.False);
                Assert.That(ReadProperty<object>(session, "Result"), Is.Null,
                    "완료 전 부분 캐시가 외부에 노출되면 안 됩니다.");

                Assert.That((bool)Invoke(session, "TryProcessNextFrame"), Is.True);
                Assert.That(ReadProperty<int>(session, "ProcessedFrameCount"), Is.EqualTo(2));
                Assert.That(ReadProperty<bool>(session, "IsComplete"), Is.True);
                object result = ReadProperty<object>(session, "Result");
                Assert.That(result, Is.Not.Null);
                Assert.That(ReadProperty<int>(result, "CorrectedFrameCount"), Is.EqualTo(2));
                Assert.That(ReadProperty<int>(result, "CorrectionEntryCount"), Is.EqualTo(148));
            }
            finally
            {
                DisposeController(controller);
                UnityEngine.Object.DestroyImmediate(bakedMesh);
                UnityEngine.Object.DestroyImmediate(target);
            }
        }

        [Test]
        public void Given_InvalidFrameVertices_When_Processing_Then_FaultsWithoutResult()
        {
            GameObject target = InstantiateTarget();

            try
            {
                object contract = BuildSurfaceContract(
                    FindFixtureSelection(RequireHumanoidAnimator(target)));
                Array contracts = CreateContractArray(contract);
                Func<int, SkinnedMeshRenderer, Vector3[]> invalidReader =
                    (_, _) => Array.Empty<Vector3>();
                object session = CreateSession(1, contracts, invalidReader);

                Assert.That((bool)Invoke(session, "TryProcessNextFrame"), Is.False);
                Assert.That(ReadProperty<bool>(session, "IsFaulted"), Is.True);
                Assert.That(ReadProperty<bool>(session, "IsComplete"), Is.False);
                Assert.That(ReadProperty<object>(session, "Result"), Is.Null);
                Assert.That(
                    ReadProperty<string>(session, "FailureMessage"),
                    Does.Contain("정점 수"));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(target);
            }
        }

        [Test]
        public void Given_ActiveSession_When_Canceling_Then_DoesNotExposeResult()
        {
            GameObject target = InstantiateTarget();

            try
            {
                object contract = BuildSurfaceContract(
                    FindFixtureSelection(RequireHumanoidAnimator(target)));
                Array contracts = CreateContractArray(contract);
                Func<int, SkinnedMeshRenderer, Vector3[]> unusedReader =
                    (_, _) => throw new InvalidOperationException(
                        "취소한 session은 frame reader를 호출하면 안 됩니다.");
                object session = CreateSession(1, contracts, unusedReader);

                Invoke(session, "Cancel");

                Assert.That(ReadProperty<bool>(session, "IsCanceled"), Is.True);
                Assert.That(ReadProperty<bool>(session, "IsFinished"), Is.True);
                Assert.That(ReadProperty<object>(session, "Result"), Is.Null);
                Assert.That((bool)Invoke(session, "TryProcessNextFrame"), Is.False);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(target);
            }
        }

        [Test, Explicit("제품 사전계산기의 전체 clip 정량 계약을 검증할 때 실행합니다.")]
        public void Given_FullClip_When_Preprocessing_Then_MatchesValidatedCacheOracle()
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
                Type contractType = contract.GetType();
                Array contracts = Array.CreateInstance(contractType, 1);
                contracts.SetValue(contract, 0);
                Invoke(
                    controller,
                    "PrepareWithArmDirectionReference",
                    animator,
                    clip,
                    AssetDatabase.LoadAssetAtPath<GameObject>(ClipAssetPath));
                MethodInfo buildMethod = RequireProductType(
                        "NativeSkinningCorrectionPreprocessor")
                    .GetMethod(
                        "TryBuild",
                        BindingFlags.Static | BindingFlags.Public |
                        BindingFlags.NonPublic);
                Assert.That(buildMethod, Is.Not.Null);
                int frameCount = Mathf.RoundToInt(clip.length * clip.frameRate) + 1;
                Func<int, SkinnedMeshRenderer, Vector3[]> frameReader =
                    (frameIndex, renderer) =>
                    {
                        Assert.That(
                            (bool)Invoke(
                                controller,
                                "Seek",
                                frameIndex / clip.frameRate),
                            Is.True,
                            $"frame {frameIndex} Seek에 실패했습니다.");
                        renderer.BakeMesh(bakedMesh, false);
                        return bakedMesh.vertices;
                    };
                object[] arguments =
                {
                    frameCount,
                    contracts,
                    frameReader,
                    null,
                    null
                };

                Assert.That((bool)buildMethod.Invoke(null, arguments), Is.True);
                object result = arguments[4];
                string message =
                    $"frames={ReadProperty<int>(result, "FrameCount")}, " +
                    $"corrected={ReadProperty<int>(result, "CorrectedFrameCount")}, " +
                    $"fallback={ReadProperty<int>(result, "FallbackFrameCount")}, " +
                    $"entries={ReadProperty<int>(result, "CorrectionEntryCount")}";
                Assert.That(
                    ReadProperty<int>(result, "FrameCount"),
                    Is.EqualTo(12468),
                    message);
                Assert.That(
                    ReadProperty<int>(result, "CorrectedFrameCount"),
                    Is.EqualTo(6044),
                    message);
                Assert.That(
                    ReadProperty<int>(result, "FallbackFrameCount"),
                    Is.EqualTo(40),
                    message);
                Assert.That(
                    ReadProperty<int>(result, "CorrectionEntryCount"),
                    Is.EqualTo(277660),
                    message);
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

        private static Array CreateContractArray(object contract)
        {
            Array contracts = Array.CreateInstance(contract.GetType(), 1);
            contracts.SetValue(contract, 0);
            return contracts;
        }

        private static object CreateSession(
            int frameCount,
            Array contracts,
            Func<int, SkinnedMeshRenderer, Vector3[]> frameReader)
        {
            MethodInfo method = RequireProductType(
                    "NativeSkinningCorrectionPreprocessor")
                .GetMethod(
                    "TryCreateSession",
                    BindingFlags.Static | BindingFlags.Public |
                    BindingFlags.NonPublic);
            Assert.That(method, Is.Not.Null);
            object[] arguments =
            {
                frameCount,
                contracts,
                frameReader,
                null,
                null
            };
            Assert.That((bool)method.Invoke(null, arguments), Is.True);
            Assert.That(arguments[4], Is.Not.Null);
            return arguments[4];
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
            Assert.That(source, Is.Not.Null);
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
