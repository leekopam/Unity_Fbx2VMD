using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Security.Cryptography;
using System.Text;
using Fbx2Vmd.FBXImporter;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace Tests.Editor.FBXImporter
{
    /// <summary>
    /// testPrefab의 Native 준비 실패 프레임에서 실패 계약별 면 쌍·본 가중치·
    /// 팔 방향 보정 전후 각도를 계측하는 진단 전용 테스트.
    /// </summary>
    public class NativeSkinningPreparationFailureDiagnosticsTests
    {
        private const string TestPrefabAssetPath =
            "Assets/Plugins/VMDRecorderSample/Models/TestModel/testPrefab.prefab";
        private const int MaximumPairsPerContract = 8;

        [TestCase(2389,
            "4345C16B38EEF8B3DC992AB88ED15EF84F60F30C21EAB4C3733BF4910A2F8B6E",
            "CEFA75F2FC59F34E38CB0C07D01BE77E592FEF4FA180216A1B1640A956319641",
            "32899FAF26B3B7F5E550605FE9788D5F0954866CD2C6AEFDFC95BD6ED4ED4423")]
        [TestCase(3540,
            "7B8A60B56FE3600BBD9278B91D8341488C84E32C5F382883952FDC14A30CF012",
            "B973CADB10DDBC6FEF4F212CC13A35B46C0ED818D3336DCC970228FBA51F255F",
            "0FF6C13902FBD19CE7178303AC97E1712B43E9F0500408E4F8C168DD83C3E796")]
        public void Given_RecoveredYybSurface_When_ApplyingVolumeCorrection_Then_PreservesOriginalQuality(
            int frame, string baselineHash, string surfaceHash, string matricesHash)
        {
            const string modelPath =
                "Assets/_Project/Model/YYB Hatsune Miku_default/YYB Hatsune Miku_default_1.0ver.fbx";
            string directory = Path.GetFullPath(Path.Combine(Application.dataPath,
                "../Docs/Workflow/Local/evidence/jev-batch2/20261024-resume/native-replay"));
            string[] paths = { Path.Combine(directory, $"yyb-f{frame}-baseline.bin"),
                Path.Combine(directory, $"yyb-f{frame}-surface.bin"),
                Path.Combine(directory, $"yyb-f{frame}-matrices.bin") };
            GameObject asset = AssetDatabase.LoadAssetAtPath<GameObject>(modelPath);
            if (asset == null || paths.Any(path => !File.Exists(path)))
                Assert.Ignore("로컬 YYB 체적 보정 회귀 fixture 미제공");
            string[] hashes = { baselineHash, surfaceHash, matricesHash };
            for (int index = 0; index < paths.Length; index++)
            {
                using (SHA256 hash = SHA256.Create())
                using (FileStream stream = File.OpenRead(paths[index]))
                    Assert.That(BitConverter.ToString(hash.ComputeHash(stream)).Replace("-", ""),
                        Is.EqualTo(hashes[index]));
            }

            GameObject target = UnityEngine.Object.Instantiate(asset);
            target.hideFlags = HideFlags.HideAndDontSave;
            try
            {
                object contract = FindAll(RequireHumanoidAnimator(target))
                    .Cast<object>()
                    .Select(BuildSurfaceContract)
                    .First(value => ReadProperty<SkinnedMeshRenderer>(value, "Renderer").name == "U_Char_2");
                var renderer = ReadProperty<SkinnedMeshRenderer>(contract, "Renderer");
                Assert.That(ReadProperty<object>(contract, "Side").ToString(), Is.EqualTo("Left"));
                Vector3[] ReadVertices(string path)
                {
                    using (var reader = new BinaryReader(File.OpenRead(path)))
                    {
                        int count = reader.ReadInt32();
                        Assert.That(count, Is.EqualTo(renderer.sharedMesh.vertexCount));
                        Assert.That(reader.BaseStream.Length, Is.EqualTo(4L + count * 12L));
                        var vertices = new Vector3[count];
                        for (int index = 0; index < count; index++)
                            vertices[index] = new Vector3(reader.ReadSingle(), reader.ReadSingle(), reader.ReadSingle());
                        return vertices;
                    }
                }
                Vector3[] baseline = ReadVertices(paths[0]);
                Vector3[] surface = ReadVertices(paths[1]);
                Vector3[] original = (Vector3[])surface.Clone();
                Matrix4x4[] matrices;
                using (var reader = new BinaryReader(File.OpenRead(paths[2])))
                {
                    int count = reader.ReadInt32();
                    Assert.That(count, Is.EqualTo(renderer.bones.Length));
                    Assert.That(reader.BaseStream.Length, Is.EqualTo(4L + count * 64L));
                    matrices = new Matrix4x4[count];
                    for (int bone = 0; bone < count; bone++)
                        for (int element = 0; element < 16; element++)
                            matrices[bone][element] = reader.ReadSingle();
                }
                Type calculator = RequireProductType("NativeSkinningSurfaceCorrectionCalculator");
                int pairCount = ReadProperty<Array>(contract, "FacePairs").Length;
                var active = new HashSet<int>();
                InvokeStatic(calculator, "UpdateActiveSharpFoldPairs",
                    new object[] { baseline, contract, Enumerable.Range(0, pairCount), active });
                int[] recovery = active.Where(index => (bool)InvokeStatic(calculator,
                    "IsRestShapeRecoveryPair", new object[] { baseline, contract, index })).ToArray();
                object edges = InvokeStatic(calculator, "BuildFrameEdges", new object[] { baseline, contract });
                object Evaluate(IReadOnlyList<Vector3> candidate) => InvokeStatic(calculator,
                    "EvaluateResult", new object[] { baseline, candidate, contract, edges, active.Count, recovery });
                Assert.That((bool)InvokeStatic(calculator, "IsWithinStrongQuality",
                    new[] { Evaluate(surface), contract }), Is.True, "표면 복원 입력은 먼저 품질을 통과해야 합니다.");
                NativeSkinningSurfaceContract[] contracts = FindAll(RequireHumanoidAnimator(target))
                    .Cast<object>().Select(BuildSurfaceContract).Cast<NativeSkinningSurfaceContract>()
                    .Where(value => value.Renderer == renderer).ToArray();
                Assert.That((bool)InvokeStatic(typeof(NativeSkinningCorrectionPreprocessSession),
                    "AreCombinedCorrectionsWithinQuality", new object[]
                    {
                        baseline, new Dictionary<int, Vector3>(), contracts
                    }), Is.False, "보정값이 비어도 결함이 남은 최종 정점을 통과시키면 안 됩니다.");
                Assert.That(NativeSkinningCorrectionPreprocessor.TryCreateSession(1, contracts,
                    (frameIndex, requestedRenderer) => baseline, null, out var session), Is.True);
                var prepared = new NativeSkinningPreparedFrame(0,
                    new Dictionary<SkinnedMeshRenderer, Vector3[]> { [renderer] = baseline },
                    new Dictionary<SkinnedMeshRenderer, Matrix4x4[]> { [renderer] = matrices });
                Assert.That(session.TryProcessPreparedFrames(new[] { prepared }), Is.True, session.FailureMessage);
                Assert.That(session.IsComplete, Is.True);
                Vector3[] finalVertices = (Vector3[])baseline.Clone();
                Assert.That(session.Result.RendererCorrections.Single().Cache.TryApply(0, finalVertices, out _), Is.True);
                object quality = Evaluate(finalVertices);
                Assert.That((bool)InvokeStatic(calculator, "IsWithinStrongQuality", new[] { quality, contract }),
                    Is.True, $"원본 기준 잔여접힘={ReadProperty<int>(quality, "ResidualSharpFoldCount")}, " +
                        $"누적변형={ReadProperty<float>(quality, "MaximumRestEdgeStrainIncrease")}");
                foreach (NativeSkinningSurfaceContract rendererContract in contracts)
                    Assert.That(NativeSkinningSurfaceCorrectionCalculator.IsCorrectedFrameWithinStrongQuality(
                        baseline, finalVertices, rendererContract), Is.True, rendererContract.Side.ToString());
                Assert.That((bool)InvokeStatic(typeof(NativeSkinningCorrectionPreprocessSession),
                    "AreCombinedCorrectionsWithinQuality", new object[]
                    {
                        renderer.sharedMesh.vertices, new Dictionary<int, Vector3>(), contracts
                    }), Is.True, "원본 모델의 정상 형상은 빈 보정값으로도 통과해야 합니다.");
                CollectionAssert.AreEqual(original, surface);
            }
            finally { UnityEngine.Object.DestroyImmediate(target); }
        }

        [Test]
        public void Given_RecoveredSurfaceDump_When_ApplyingVolumeCorrection_Then_PreservesRecoveredShape()
        {
            string directory = Path.GetFullPath(Path.Combine(Application.dataPath,
                "../Docs/Workflow/Local/evidence/jev-batch2/20261024-resume/native-replay"));
            string surfacePath = Path.Combine(directory, "f12291-surface.bin");
            string matricesPath = Path.Combine(directory, "f12291-matrices.bin");
            if (!File.Exists(surfacePath) || !File.Exists(matricesPath) ||
                AssetDatabase.LoadAssetAtPath<GameObject>(TestPrefabAssetPath) == null)
                Assert.Ignore("로컬 체적 보정 회귀 fixture 미제공");

            foreach (var fixture in new[]
            {
                new { Path = surfacePath, Hash = "5899BFB1D6E526D0941B11EF10FD89C5780910B2790856464AF102FE71624C80" },
                new { Path = matricesPath, Hash = "2C4A76744389F513CAF722A21D94AC70745B14630DC5835225247114427A9BB0" }
            })
            {
                using (SHA256 hash = SHA256.Create())
                using (FileStream stream = File.OpenRead(fixture.Path))
                    Assert.That(BitConverter.ToString(hash.ComputeHash(stream)).Replace("-", ""),
                        Is.EqualTo(fixture.Hash));
            }

            GameObject target = InstantiateTestPrefab();
            try
            {
                object contract = BuildSurfaceContract(FindAll(RequireHumanoidAnimator(target))[4]);
                var renderer = ReadProperty<SkinnedMeshRenderer>(contract, "Renderer");
                Assert.That(renderer.name, Is.EqualTo("U_Char_0"));
                Assert.That(ReadProperty<object>(contract, "Side").ToString(), Is.EqualTo("Right"));
                Vector3[] surface;
                using (var reader = new BinaryReader(File.OpenRead(surfacePath)))
                {
                    int count = reader.ReadInt32();
                    Assert.That(count, Is.EqualTo(renderer.sharedMesh.vertexCount));
                    Assert.That(reader.BaseStream.Length, Is.EqualTo(4L + count * 12L));
                    surface = new Vector3[count];
                    for (int index = 0; index < count; index++)
                        surface[index] = new Vector3(reader.ReadSingle(), reader.ReadSingle(), reader.ReadSingle());
                }
                Matrix4x4[] matrices;
                using (var reader = new BinaryReader(File.OpenRead(matricesPath)))
                {
                    int count = reader.ReadInt32();
                    Assert.That(count, Is.EqualTo(renderer.bones.Length));
                    Assert.That(reader.BaseStream.Length, Is.EqualTo(4L + count * 64L));
                    matrices = new Matrix4x4[count];
                    for (int bone = 0; bone < count; bone++)
                        for (int element = 0; element < 16; element++)
                            matrices[bone][element] = reader.ReadSingle();
                }
                Vector3[] original = (Vector3[])surface.Clone();
                Type surfaceCalculator = RequireProductType("NativeSkinningSurfaceCorrectionCalculator");
                int pairCount = ReadProperty<Array>(contract, "FacePairs").Length;
                int[] preservedPairs = Enumerable.Range(0, pairCount).Where(index =>
                    !(bool)InvokeStatic(surfaceCalculator, "IsRestShapeRecoveryPairUnresolved",
                        new object[] { surface, contract, index })).ToArray();
                Assert.That(preservedPairs.Length, Is.GreaterThan(0));
                object[] arguments = { surface, renderer.sharedMesh.vertices,
                    renderer.sharedMesh.boneWeights, matrices, contract, null };
                Assert.That((bool)InvokeStatic(RequireProductType("NativeSkinningDualQuaternionBlendCalculator"),
                    "TryCalculate", arguments), Is.True);
                var corrected = ReadProperty<IReadOnlyList<Vector3>>(arguments[5], "BlendedVertices");
                int[] collapsedPairs = preservedPairs.Where(index =>
                    (bool)InvokeStatic(surfaceCalculator, "IsRestShapeRecoveryPairUnresolved",
                        new object[] { corrected, contract, index })).ToArray();
                Assert.That(collapsedPairs, Is.Empty, "체적 보정이 복원된 표면을 다시 붕괴시키면 안 됩니다.");
                CollectionAssert.AreEqual(original, surface);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(target);
            }
        }

        [TestCase(1499, 0)]
        [TestCase(1499, 1)]
        [TestCase(1499, -1)]
        [TestCase(1977, 0)]
        [TestCase(1977, 1)]
        [TestCase(1977, -1)]
        [TestCase(9675, 0)]
        [TestCase(9675, 1)]
        [TestCase(9675, -1)]
        public void Given_LocalFailureDump_When_CalculatingValidatedCorrection_Then_PreservesInputAndPassesStrictQuality(
            int frame, int perturbationSign)
        {
            string dumpPath = Path.GetFullPath(Path.Combine(Application.dataPath,
                $"../Docs/Workflow/Local/evidence/jev-batch2/20261024-resume/native-replay/f{frame}-U_Char_0.bin"));
            if (!File.Exists(dumpPath) ||
                AssetDatabase.LoadAssetAtPath<GameObject>(TestPrefabAssetPath) == null)
            {
                Assert.Ignore($"로컬 fixture 미제공: testPrefab 또는 {dumpPath}");
            }

            string expectedHash = frame == 1499
                ? "B8960B65E282205B9F7D3A1AAA9FCF17CD1192C7CCE41DAE210910691536DB0C"
                : frame == 1977
                    ? "A0F08599A7A83482831CC7116467A24C79FC3AB2B98B95793AFCEEBC3D9263C0"
                    : "487E30D2A102FA4FA40ADB8A681BA2A5C6EA3F4D97463000F8D83CD08B13DCFD";
            using (SHA256 hash = SHA256.Create())
            using (FileStream stream = File.OpenRead(dumpPath))
            {
                Assert.That(BitConverter.ToString(hash.ComputeHash(stream)).Replace("-", ""),
                    Is.EqualTo(expectedHash), "다른 모션의 덤프를 회귀 기준으로 사용할 수 없습니다.");
            }

            GameObject target = InstantiateTestPrefab();
            try
            {
                Animator animator = RequireHumanoidAnimator(target);
                object contract = BuildSurfaceContract(FindAll(animator)[frame == 9675 ? 0 : 4]);
                SkinnedMeshRenderer renderer = ReadProperty<SkinnedMeshRenderer>(contract, "Renderer");
                Assert.That(renderer.name, Is.EqualTo("U_Char_0"));
                Assert.That(ReadProperty<object>(contract, "Side").ToString(),
                    Is.EqualTo(frame == 9675 ? "Left" : "Right"));
                Vector3[] vertices;
                using (var reader = new BinaryReader(File.OpenRead(dumpPath)))
                {
                    int count = reader.ReadInt32();
                    Assert.That(count, Is.EqualTo(renderer.sharedMesh.vertexCount));
                    Assert.That(reader.BaseStream.Length, Is.EqualTo(4L + count * 12L));
                    vertices = new Vector3[count];
                    float delta = perturbationSign * 0.0000001f;
                    for (int index = 0; index < count; index++)
                    {
                        vertices[index] = new Vector3(reader.ReadSingle(), reader.ReadSingle(), reader.ReadSingle()) +
                            new Vector3(index % 2 == 0 ? delta : -delta,
                                index % 3 == 0 ? delta : -delta, index % 5 == 0 ? delta : -delta);
                    }
                }
                Vector3[] original = (Vector3[])vertices.Clone();
                Type calculator = RequireProductType("NativeSkinningSurfaceCorrectionCalculator");
                object[] arguments = { vertices, contract, null };
                Assert.That((bool)InvokeStatic(calculator, "TryCalculateStrongValidatedFrame", arguments), Is.True);
                object result = arguments[2];
                Assert.That(result, Is.Not.Null);
                Assert.That((bool)InvokeStatic(calculator, "IsWithinStrongQuality", new[] { result, contract }), Is.True);
                Assert.That(ReadProperty<int>(result, "ResidualSharpFoldCount"), Is.Zero);
                Assert.That(ReadProperty<int>(result, "NewSharpFoldCount"), Is.Zero);
                Assert.That(ReadProperty<int>(result, "NewDegenerateFaceCount"), Is.Zero);
                Assert.That(ReadProperty<int>(result, "ReversedFaceCount"), Is.Zero);
                CollectionAssert.AreEqual(original, vertices, "계산 입력은 보존해야 합니다.");
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(target);
            }
        }

        [TestCase("tetoris_001.fbx", 1)]
        [TestCase("tetoris_001.fbx", 117)]
        [TestCase("tetoris_001.fbx", 153)]
        [TestCase("tetoris_001.fbx", 553)]
        [TestCase("tetoris_001.fbx", 562)]
        [TestCase("tetoris_001.fbx", 563)]
        [TestCase("tetoris_001.fbx", 8101)]
        [TestCase("satisfaction_2.fbx", 2)]
        [Explicit("로컬 FBX fixture가 필요한 실패 프레임 진단 계측입니다.")]
        public void Given_FailingFrame_When_PreparingNativeSkinning_Then_DumpsContractDiagnostics(
            string clipFileName,
            int failingFrame)
        {
            string clipAssetPath = $"Assets/Resources/Import_FBX/{clipFileName}";
            RequireLocalFixture(clipAssetPath);
            EnsureHumanoidClipImport(clipAssetPath);
            GameObject target = InstantiateTestPrefab();
            object controller = CreateController();
            try
            {
                Animator animator = RequireHumanoidAnimator(target);
                AnimationClip clip = LoadHumanoidClip(clipAssetPath);
                GameObject sourceModel =
                    AssetDatabase.LoadAssetAtPath<GameObject>(clipAssetPath);
                Invoke(controller, "PrepareWithArmDirectionReference",
                    animator, clip, sourceModel);
                // 제품 경로(FBXVmdPipeline)는 지면 반응 접지를 켜고 준비하므로 동일하게 맞춤.
                Invoke(controller, "SetGroundResponseEnabled", true);
                // 제품 준비 경로처럼 0번 프레임부터 순차 탐색해 접촉 유지 상태를 동일하게 누적함.
                for (int frameIndex = 0; frameIndex < failingFrame; frameIndex++)
                {
                    Assert.That((bool)Invoke(controller, "SeekFrame", frameIndex),
                        Is.True, $"frame {frameIndex} 순차 탐색이 필요합니다.");
                }
                float timeSeconds = failingFrame / clip.frameRate;
                var report = new StringBuilder();
                report.Append(
                    $"[NativePrepDiag] clip={clipFileName} frame={failingFrame}");

                MeasureArmDirectionAngles(controller, animator, timeSeconds, report);
                object[] selections = FindAll(animator).Cast<object>().ToArray();
                var contracts = new List<object>(selections.Length);
                foreach (object selection in selections)
                {
                    object contract = BuildSurfaceContract(selection);
                    contracts.Add(contract);
                    int index = contracts.Count - 1;
                    report.Append($"\n contract[{index}] renderer=" +
                        $"{ReadProperty<SkinnedMeshRenderer>(contract, "Renderer").name}" +
                        $" side={ReadProperty<object>(contract, "Side")}" +
                        $" pairs={ReadProperty<Array>(contract, "FacePairs").Length}" +
                        $" lowerArm={BoneName(contract)}");
                }
                Assert.That(contracts.Count, Is.GreaterThan(0),
                    "testPrefab에서 팔 표면 계약이 만들어져야 합니다.");

                var bakedMeshes = new List<Mesh>();
                var verticesByRenderer = new Dictionary<SkinnedMeshRenderer, Vector3[]>();
                try
                {
                    foreach (SkinnedMeshRenderer renderer in contracts
                                 .Select(contract => ReadProperty<SkinnedMeshRenderer>(
                                     contract, "Renderer"))
                                 .Distinct())
                    {
                        var baked = new Mesh();
                        bakedMeshes.Add(baked);
                        renderer.BakeMesh(baked, false);
                        verticesByRenderer[renderer] = baked.vertices;
                    }
                    // 제품 실패 로그의 대표 쌍(contract 0의 pair 182) 각도를 측정해 입력 동일성을 검증함.
                DumpPairBaseline(contracts, verticesByRenderer, report);
                    // 제품 경로가 덤프한 실패 입력이 있으면 EditMode 입력과 비교하고 동일 입력으로 솔버를 재현함.
                    CompareWithProductDump(failingFrame, contracts, verticesByRenderer, report);
                int failingContractCount = DumpFailingContracts(
                        contracts, verticesByRenderer, report);
                    Debug.Log(report.ToString());
                    Assert.That(failingContractCount, Is.Zero,
                        "testPrefab 실패 프레임의 품질 계약이 모두 통과해야 합니다.\n" + report);
                }
                finally
                {
                    foreach (Mesh mesh in bakedMeshes)
                    {
                        UnityEngine.Object.DestroyImmediate(mesh);
                    }
                }
            }
            finally
            {
                DisposeController(controller);
                UnityEngine.Object.DestroyImmediate(target);
            }
        }

        // 방향 보정 전 자세는 내부 플레이어 평가로, 보정 후는 Seek 결과로 계측함.
        private static void MeasureArmDirectionAngles(
            object controller,
            Animator animator,
            float timeSeconds,
            StringBuilder report)
        {
            object player = ReadField<object>(controller, "_player");
            Invoke(player, "EvaluateAt", timeSeconds);
            MeasureArmDirections(animator,
                out Vector3 leftUpperBefore, out Vector3 leftForearmBefore,
                out Vector3 rightUpperBefore, out Vector3 rightForearmBefore);

            object referencePlayer = ReadField<object>(controller, "_poseReferencePlayer");
            object[] arguments = { timeSeconds, null };
            MethodInfo method = referencePlayer.GetType().GetMethod(
                "TryEvaluateArmDirectionsAt",
                BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            Assert.That(method, Is.Not.Null);
            Assert.That((bool)method.Invoke(referencePlayer, arguments), Is.True,
                "기준 팔 방향 샘플링이 필요합니다.");
            object reference = arguments[1];

            Assert.That((bool)Invoke(controller, "Seek", timeSeconds), Is.True);
            MeasureArmDirections(animator,
                out Vector3 leftUpperAfter, out Vector3 leftForearmAfter,
                out Vector3 rightUpperAfter, out Vector3 rightForearmAfter);

            AppendDirectionLine(report, "L.upper", leftUpperBefore, leftUpperAfter,
                ReadProperty<Vector3>(reference, "LeftUpperArm"));
            AppendDirectionLine(report, "L.fore", leftForearmBefore, leftForearmAfter,
                ReadProperty<Vector3>(reference, "LeftForearm"));
            AppendDirectionLine(report, "R.upper", rightUpperBefore, rightUpperAfter,
                ReadProperty<Vector3>(reference, "RightUpperArm"));
            AppendDirectionLine(report, "R.fore", rightForearmBefore, rightForearmAfter,
                ReadProperty<Vector3>(reference, "RightForearm"));
        }

        private static void AppendDirectionLine(
            StringBuilder report,
            string label,
            Vector3 before,
            Vector3 after,
            Vector3 reference)
        {
            report.Append($"\n dir[{label}] errBefore={Vector3.Angle(before, reference):F2}" +
                $" appliedDelta={Vector3.Angle(before, after):F2}" +
                $" errAfter={Vector3.Angle(after, reference):F2}");
        }

        private static void MeasureArmDirections(
            Animator animator,
            out Vector3 leftUpper,
            out Vector3 leftForearm,
            out Vector3 rightUpper,
            out Vector3 rightForearm)
        {
            Transform root = animator.transform;
            Vector3 Segment(HumanBodyBones start, HumanBodyBones end)
            {
                Transform startBone = animator.GetBoneTransform(start);
                Transform endBone = animator.GetBoneTransform(end);
                Assert.That(startBone, Is.Not.Null);
                Assert.That(endBone, Is.Not.Null);
                return root.InverseTransformDirection(
                    endBone.position - startBone.position).normalized;
            }
            leftUpper = Segment(HumanBodyBones.LeftUpperArm, HumanBodyBones.LeftLowerArm);
            leftForearm = Segment(HumanBodyBones.LeftLowerArm, HumanBodyBones.LeftHand);
            rightUpper = Segment(HumanBodyBones.RightUpperArm, HumanBodyBones.RightLowerArm);
            rightForearm = Segment(HumanBodyBones.RightLowerArm, HumanBodyBones.RightHand);
        }

        // 계약별로 빠른 보정→강한 보정 순서로 평가하고 품질 계약 실패만 상세 출력함.
        private static int DumpFailingContracts(
            IReadOnlyList<object> contracts,
            IReadOnlyDictionary<SkinnedMeshRenderer, Vector3[]> verticesByRenderer,
            StringBuilder report)
        {
            int failingContractCount = 0;
            for (int index = 0; index < contracts.Count; index++)
            {
                object contract = contracts[index];
                SkinnedMeshRenderer renderer =
                    ReadProperty<SkinnedMeshRenderer>(contract, "Renderer");
                Vector3[] vertices = verticesByRenderer[renderer];
                object correction = CalculateCorrection(
                    vertices, contract, out bool passed, report);
                if (passed)
                {
                    continue;
                }

                failingContractCount++;
                report.Append(
                    $"\n FAIL contract[{index}] side={ReadProperty<object>(contract, "Side")}" +
                    $" renderer={renderer.name}");
                if (correction == null)
                {
                    report.Append(" correction=none");
                    continue;
                }
                report.Append(
                    $" initialFold={ReadProperty<int>(correction, "InitialSharpFoldCount")}" +
                    $" residualFold={ReadProperty<int>(correction, "ResidualSharpFoldCount")}" +
                    $" newFold={ReadProperty<int>(correction, "NewSharpFoldCount")}" +
                    $" degenerate={ReadProperty<int>(correction, "NewDegenerateFaceCount")}" +
                    $" reversed={ReadProperty<int>(correction, "ReversedFaceCount")}" +
                    $" restRecovery={ReadProperty<bool>(correction, "UsedRestShapeRecovery")}");
                DumpFailingPairs(contract, correction, vertices, report);
            }
            return failingContractCount;
        }

        private static object CalculateCorrection(
            Vector3[] vertices,
            object contract,
            out bool passed,
            StringBuilder report)
        {
            Type calculatorType = RequireProductType(
                "NativeSkinningSurfaceCorrectionCalculator");
            object[] fastArguments = { vertices, contract, null };
            bool fastCalculated = (bool)InvokeStatic(
                calculatorType,
                "TryCalculateFastValidatedFrame",
                fastArguments);
            object fastCorrection = fastArguments[2];
            if (fastCalculated && fastCorrection != null &&
                (bool)InvokeStatic(
                    calculatorType,
                    "IsWithinFastQuality",
                    new[] { fastCorrection, contract }))
            {
                passed = true;
                return fastCorrection;
            }
            if (!fastCalculated)
            {
                passed = false;
                return fastCorrection;
            }
            object[] strongArguments = { vertices, contract, null };
            InvokeStatic(
                calculatorType,
                "TryCalculateStrongValidatedFrame",
                strongArguments);
            object strongCorrection = strongArguments[2];
            passed = strongCorrection != null &&
                (bool)InvokeStatic(
                    calculatorType,
                    "IsWithinStrongQuality",
                    new[] { strongCorrection, contract });
            if (!passed && strongCorrection != null)
            {
                DumpDisplacementBudgetRetries(
                    calculatorType, vertices, contract, strongCorrection, report);
            }
            return strongCorrection ?? fastCorrection;
        }

        // 강한 보정이 실패한 계약에서 변위 예산 비율만 키워 잔여 급접힘 해소 여부를 측정함.
        private static void DumpDisplacementBudgetRetries(
            Type calculatorType,
            Vector3[] vertices,
            object contract,
            object strongCorrection,
            StringBuilder report)
        {
            Type configurationType = calculatorType.GetNestedType(
                "CorrectionConfiguration", BindingFlags.NonPublic);
            MethodInfo tryCalculate = calculatorType.GetMethod(
                "TryCalculate", BindingFlags.Static | BindingFlags.NonPublic);
            Assert.That(configurationType, Is.Not.Null);
            Assert.That(tryCalculate, Is.Not.Null);
            if (ReadProperty<int>(strongCorrection, "NewSharpFoldCount") > 0)
            {
                DumpSecondPass(calculatorType, tryCalculate, vertices,
                    strongCorrection, contract, report);
            }
            // Mono 기본 바인더는 선택적 인수를 채우지 않으므로 worksetRingCount 기본값을 명시한다.
            int defaultRings = ReadConst<int>(
                "NativeSkinningSurfaceCorrectionCalculator", "DefaultWorksetRingCount");
            foreach (float ratio in new[] { 0.01f, 0.02f, 0.025f, 0.03f, 0.04f, 0.05f, 0.08f })
            {
                object configuration = Activator.CreateInstance(
                    configurationType,
                    BindingFlags.Instance | BindingFlags.NonPublic,
                    null,
                    new object[] { 640, 64, 32, 0.03f, ratio, defaultRings },
                    null);
                object[] retryArguments =
                    { vertices, vertices, contract, configuration, false, false, null };
                try
                {
                    bool calculated = (bool)tryCalculate.Invoke(null, retryArguments);
                    object retry = retryArguments[6];
                    if (!calculated || retry == null)
                    {
                        report.Append($"\n retry[ratio={ratio:F2}]=uncalculated");
                        continue;
                    }
                    report.Append(
                        $"\n retry[ratio={ratio:F2}]" +
                        $" residual={ReadProperty<int>(retry, "ResidualSharpFoldCount")}" +
                        $" new={ReadProperty<int>(retry, "NewSharpFoldCount")}" +
                        $" maxDisp={ReadProperty<float>(retry, "MaximumVertexDisplacement"):F4}" +
                        $" strain={ReadProperty<float>(retry, "MaximumEdgeLengthStrain"):F4}" +
                        $" safe={ReadProperty<bool>(retry, "IsSafe")}");
                }
                catch (Exception exception)
                {
                    report.Append($"\n retry[ratio={ratio:F2}]=threw:{exception.Message}");
                }
            }
        }

        // strain 한계 바인딩 여부를 확인하기 위해 신장 예산만 키워 재시도함.
        private static void DumpStrainBudgetRetries(
            Type calculatorType,
            Vector3[] vertices,
            object contract,
            StringBuilder report)
        {
            Type configurationType = calculatorType.GetNestedType(
                "CorrectionConfiguration", BindingFlags.NonPublic);
            MethodInfo tryCalculate = calculatorType.GetMethod(
                "TryCalculate", BindingFlags.Static | BindingFlags.NonPublic);
            // Mono 기본 바인더는 선택적 인수를 채우지 않으므로 worksetRingCount 기본값을 명시한다.
            int defaultRings = ReadConst<int>(
                "NativeSkinningSurfaceCorrectionCalculator", "DefaultWorksetRingCount");
            foreach (float strain in new[] { 0.035f, 0.04f, 0.05f, 0.06f, 0.08f, 0.10f, 0.15f })
            {
                object configuration = Activator.CreateInstance(
                    configurationType,
                    BindingFlags.Instance | BindingFlags.NonPublic,
                    null,
                    new object[] { 640, 64, 32, strain, 0.055f, defaultRings },
                    null);
                object[] retryArguments =
                    { vertices, vertices, contract, configuration, false, false, null };
                try
                {
                    bool calculated = (bool)tryCalculate.Invoke(null, retryArguments);
                    object retry = retryArguments[6];
                    if (!calculated || retry == null)
                    {
                        report.Append($"\n strainRetry[{strain:F3}]=uncalculated");
                        continue;
                    }
                    report.Append(
                        $"\n strainRetry[{strain:F3}]" +
                        $" residual={ReadProperty<int>(retry, "ResidualSharpFoldCount")}" +
                        $" new={ReadProperty<int>(retry, "NewSharpFoldCount")}" +
                        $" maxDisp={ReadProperty<float>(retry, "MaximumVertexDisplacement"):F4}" +
                        $" strain={ReadProperty<float>(retry, "MaximumEdgeLengthStrain"):F4}" +
                        $" safe={ReadProperty<bool>(retry, "IsSafe")}");
                }
                catch (Exception exception)
                {
                    report.Append($"\n strainRetry[{strain:F3}]=threw:{exception.Message}");
                }
            }
        }

        // 잔여 접힘이 남은 결과를 초기값으로 재투입하거나 workset 링을 넓혀
        // 수렴 정체(local optimum) 탈출 여부를 측정함.
        private static void DumpChainAndRingRetries(
            Type calculatorType,
            Vector3[] baselineVertices,
            object contract,
            object firstCorrection,
            StringBuilder report)
        {
            Type configurationType = calculatorType.GetNestedType(
                "CorrectionConfiguration", BindingFlags.NonPublic);
            MethodInfo tryCalculate = calculatorType.GetMethod(
                "TryCalculate", BindingFlags.Static | BindingFlags.NonPublic);
            var firstCorrected = ((IReadOnlyList<Vector3>)ReadProperty<object>(
                firstCorrection, "CorrectedVertices")).ToArray();

            // 2차 패스: 1차 보정 결과를 초기 추정으로 다시 계산함.
            int defaultRings = ReadConst<int>(
                "NativeSkinningSurfaceCorrectionCalculator", "DefaultWorksetRingCount");
            foreach (float strain in new[] { 0.03f, 0.05f })
            {
                object configuration = Activator.CreateInstance(
                    configurationType,
                    BindingFlags.Instance | BindingFlags.NonPublic,
                    null,
                    new object[] { 640, 64, 32, strain, 0.055f, defaultRings },
                    null);
                object[] arguments =
                    { baselineVertices, firstCorrected, contract, configuration, false, false, null };
                try
                {
                    bool calculated = (bool)tryCalculate.Invoke(null, arguments);
                    object second = arguments[6];
                    report.Append(calculated && second != null
                        ? $"\n chain[strain={strain:F2}]" +
                          $" residual={ReadProperty<int>(second, "ResidualSharpFoldCount")}" +
                          $" new={ReadProperty<int>(second, "NewSharpFoldCount")}" +
                          $" maxDisp={ReadProperty<float>(second, "MaximumVertexDisplacement"):F4}" +
                          $" strain={ReadProperty<float>(second, "MaximumEdgeLengthStrain"):F4}" +
                          $" safe={ReadProperty<bool>(second, "IsSafe")}"
                        : $"\n chain[strain={strain:F2}]=uncalculated");
                }
                catch (Exception exception)
                {
                    report.Append(
                        $"\n chain[strain={strain:F2}]=threw:{exception.Message}");
                }
            }

            // workset 링 스윕: 더 넓은 국소 영역에서 정체 해소 여부를 봄.
            foreach (int rings in new[] { 3, 4, 5 })
            {
                object configuration = Activator.CreateInstance(
                    configurationType,
                    BindingFlags.Instance | BindingFlags.NonPublic,
                    null,
                    new object[] { 640, 64, 32, 0.05f, 0.055f, rings },
                    null);
                object[] arguments =
                    { baselineVertices, baselineVertices, contract, configuration, false, false, null };
                try
                {
                    bool calculated = (bool)tryCalculate.Invoke(null, arguments);
                    object retry = arguments[6];
                    report.Append(calculated && retry != null
                        ? $"\n ringRetry[rings={rings}]" +
                          $" residual={ReadProperty<int>(retry, "ResidualSharpFoldCount")}" +
                          $" new={ReadProperty<int>(retry, "NewSharpFoldCount")}" +
                          $" maxDisp={ReadProperty<float>(retry, "MaximumVertexDisplacement"):F4}" +
                          $" strain={ReadProperty<float>(retry, "MaximumEdgeLengthStrain"):F4}" +
                          $" safe={ReadProperty<bool>(retry, "IsSafe")}"
                        : $"\n ringRetry[rings={rings}]=uncalculated");
                }
                catch (Exception exception)
                {
                    report.Append(
                        $"\n ringRetry[rings={rings}]=threw:{exception.Message}");
                }
            }
        }

        // 신규 접힘이 생긴 강한 보정 결과를 초기값으로 다시 계산해 해소 여부를 측정함.
        private static void DumpSecondPass(
            Type calculatorType,
            MethodInfo tryCalculate,
            Vector3[] baselineVertices,
            object firstPass,
            object contract,
            StringBuilder report)
        {
            object strongConfiguration = ReadConst<object>(
                "NativeSkinningSurfaceCorrectionCalculator", "StrongConfiguration");
            var corrected = ((IReadOnlyList<Vector3>)ReadProperty<object>(
                firstPass, "CorrectedVertices")).ToArray();
            object[] arguments =
                { baselineVertices, corrected, contract, strongConfiguration, false, false, null };
            try
            {
                bool calculated = (bool)tryCalculate.Invoke(null, arguments);
                object secondPass = arguments[6];
                report.Append(calculated && secondPass != null
                    ? $"\n secondpass residual={ReadProperty<int>(secondPass, "ResidualSharpFoldCount")}" +
                      $" new={ReadProperty<int>(secondPass, "NewSharpFoldCount")}" +
                      $" maxDisp={ReadProperty<float>(secondPass, "MaximumVertexDisplacement"):F4}" +
                      $" strain={ReadProperty<float>(secondPass, "MaximumEdgeLengthStrain"):F4}" +
                      $" safe={ReadProperty<bool>(secondPass, "IsSafe")}"
                    : "\n secondpass=uncalculated");
            }
            catch (Exception exception)
            {
                report.Append($"\n secondpass=threw:{exception.Message}");
            }
        }

        private static void DumpFailingPairs(
            object contract,
            object correction,
            Vector3[] baselineVertices,
            StringBuilder report)
        {
            var correctedVertices =
                (IReadOnlyList<Vector3>)ReadProperty<object>(correction, "CorrectedVertices");
            Array facePairs = ReadProperty<Array>(contract, "FacePairs");
            float[] restAngles = ReadProperty<float[]>(contract, "RestAnglesDegrees");
            float maximumRestAngle = ReadConst<float>(
                "NativeSkinningSurfaceDefectDetector",
                "MaximumCorrectableRestAngleDegrees");
            float minimumSharpAngle = ReadConst<float>(
                "NativeSkinningSurfaceDefectDetector",
                "MinimumSharpFoldAngleDegrees");
            int lowerArmBoneIndex = ReadProperty<int>(contract, "LowerArmBoneIndex");
            float armChainLength = ReadProperty<float>(contract, "ArmChainLength");
            object strongConfiguration = ReadConst<object>(
                "NativeSkinningSurfaceCorrectionCalculator",
                "StrongConfiguration");
            float displacementRatioLimit = ReadProperty<float>(
                strongConfiguration, "MaximumTotalDisplacementToArmLengthRatio");
            float displacementLimit = armChainLength * displacementRatioLimit;
            int[] triangles = ReadProperty<int[]>(contract, "Triangles");
            SkinnedMeshRenderer renderer =
                ReadProperty<SkinnedMeshRenderer>(contract, "Renderer");
            BoneWeight[] boneWeights = renderer.sharedMesh.boneWeights;
            Transform[] bones = renderer.bones;

            int dumped = 0;
            for (int pairIndex = 0;
                 pairIndex < facePairs.Length && dumped < MaximumPairsPerContract;
                 pairIndex++)
            {
                if (restAngles[pairIndex] > maximumRestAngle ||
                    !TryMeasureAngle(correctedVertices, facePairs.GetValue(pairIndex),
                        out float correctedAngle) ||
                    correctedAngle <= minimumSharpAngle)
                {
                    continue;
                }
                TryMeasureAngle(baselineVertices, facePairs.GetValue(pairIndex),
                    out float baselineAngle);
                report.Append(
                    $"\n  pair[{pairIndex}] rest={restAngles[pairIndex]:F2}" +
                    $" base={baselineAngle:F2} corr={correctedAngle:F2}" +
                    $" verts={DescribePairVertices(facePairs.GetValue(pairIndex), bones, boneWeights, lowerArmBoneIndex)}");
                report.Append(
                    $"\n    stall disp={DescribePairDisplacements(facePairs.GetValue(pairIndex), baselineVertices, correctedVertices, displacementLimit)}" +
                    $" area={DescribePairFaceAreas(facePairs.GetValue(pairIndex), baselineVertices, correctedVertices, triangles)}");
                dumped++;
            }
            report.Append($" residualPairsDumped={dumped}");

            // 잔존이 아닌 신규 접힘(기준 ≤150도 → 보정 후 >150도) 쌍도 따로 덤프한다.
            int newDumped = 0;
            for (int pairIndex = 0;
                 pairIndex < facePairs.Length && newDumped < MaximumPairsPerContract;
                 pairIndex++)
            {
                if (!TryMeasureAngle(correctedVertices, facePairs.GetValue(pairIndex),
                        out float correctedNewAngle) ||
                    correctedNewAngle <= minimumSharpAngle)
                {
                    continue;
                }
                TryMeasureAngle(baselineVertices, facePairs.GetValue(pairIndex),
                    out float baselineNewAngle);
                if (baselineNewAngle > minimumSharpAngle)
                {
                    continue;
                }
                report.Append(
                    $"\n  newfold pair[{pairIndex}] rest={restAngles[pairIndex]:F2}" +
                    $" base={baselineNewAngle:F2} corr={correctedNewAngle:F2}" +
                    $" verts={DescribePairVertices(facePairs.GetValue(pairIndex), bones, boneWeights, lowerArmBoneIndex)}");
                report.Append(
                    $"\n    disp={DescribePairDisplacements(facePairs.GetValue(pairIndex), baselineVertices, correctedVertices, displacementLimit)}");
                newDumped++;
            }
            report.Append($" newFoldPairsDumped={newDumped}");
        }

        // 제품 실패 로그의 baseline 각도와 비교해 진단 입력이 제품과 같은지 판별함.
        private static void DumpPairBaseline(
            IReadOnlyList<object> contracts,
            IReadOnlyDictionary<SkinnedMeshRenderer, Vector3[]> verticesByRenderer,
            StringBuilder report)
        {
            foreach (object contract in contracts)
            {
                string side = ReadProperty<object>(contract, "Side")?.ToString();
                int probeIndex = side == "Left" ? 182 : side == "Right" ? 286 : -1;
                Array facePairs = ReadProperty<Array>(contract, "FacePairs");
                if (probeIndex < 0 || facePairs.Length <= probeIndex)
                {
                    continue;
                }
                SkinnedMeshRenderer renderer =
                    ReadProperty<SkinnedMeshRenderer>(contract, "Renderer");
                Vector3[] vertices = verticesByRenderer[renderer];
                object pair = facePairs.GetValue(probeIndex);
                if (TryMeasureAngle(vertices, pair, out float baselineAngle))
                {
                    report.Append(
                        $"\n probe {side} pair[{probeIndex}] base={baselineAngle:F2}" +
                        $" rest={ReadProperty<float[]>(contract, "RestAnglesDegrees")[probeIndex]:F2}");
                }
                // 제품 경로와 동일한 강한 보정을 돌려 실패 쌍의 보정 각도를 직접 비교함.
                Type calculatorType = RequireProductType(
                    "NativeSkinningSurfaceCorrectionCalculator");
                object[] strongArguments = { vertices, contract, null };
                bool strongCalculated = (bool)InvokeStatic(
                    calculatorType, "TryCalculateStrongValidatedFrame", strongArguments);
                object strongCorrection = strongArguments[2];
                if (!strongCalculated || strongCorrection == null)
                {
                    report.Append(
                        $"\n probe {side} pair[{probeIndex}] strong=uncalculated");
                    continue;
                }
                IReadOnlyList<Vector3> correctedVertices =
                    (IReadOnlyList<Vector3>)ReadProperty<object>(
                        strongCorrection, "CorrectedVertices");
                report.Append(
                    $"\n probe {side} pair[{probeIndex}]" +
                    $" corr={(TryMeasureAngle(correctedVertices, pair, out float corrected) ? $"{corrected:F2}" : "n/a")}" +
                    $" residual={ReadProperty<int>(strongCorrection, "ResidualSharpFoldCount")}" +
                    $" maxDisp={ReadProperty<float>(strongCorrection, "MaximumVertexDisplacement"):F4}" +
                    $" strain={ReadProperty<float>(strongCorrection, "MaximumEdgeLengthStrain"):F4}" +
                    $" safe={ReadProperty<bool>(strongCorrection, "IsSafe")}" +
                    $" quality={InvokeStatic(calculatorType, "IsWithinStrongQuality", new[] { strongCorrection, contract })}");
            }
        }

        // 제품 경로가 덤프한 실패 입력을 읽어 EditMode 입력과 정점 단위로 비교하고
        // 덤프 입력으로 강한 보정을 재현해 제품 실패 재현 여부를 확인함.
        private static void CompareWithProductDump(
            int failingFrame,
            IReadOnlyList<object> contracts,
            IReadOnlyDictionary<SkinnedMeshRenderer, Vector3[]> verticesByRenderer,
            StringBuilder report)
        {
            string directory = Path.GetFullPath(Path.Combine(
                Application.dataPath,
                "../Docs/Workflow/Local/runtime/native-prep-dumps"));
            foreach (object contract in contracts)
            {
                SkinnedMeshRenderer renderer =
                    ReadProperty<SkinnedMeshRenderer>(contract, "Renderer");
                string dumpPath = Path.Combine(directory,
                    $"f{failingFrame}-{renderer.name}.bin");
                if (!File.Exists(dumpPath))
                {
                    continue;
                }

                Vector3[] dumped;
                using (var reader = new BinaryReader(File.OpenRead(dumpPath)))
                {
                    int count = reader.ReadInt32();
                    dumped = new Vector3[count];
                    for (int index = 0; index < count; index++)
                    {
                        dumped[index] = new Vector3(
                            reader.ReadSingle(),
                            reader.ReadSingle(),
                            reader.ReadSingle());
                    }
                }
                Vector3[] editModeVertices = verticesByRenderer[renderer];
                if (dumped.Length != editModeVertices.Length)
                {
                    report.Append(
                        $"\n dump[{renderer.name}] 길이 불일치 product={dumped.Length} edit={editModeVertices.Length}");
                    continue;
                }

                // 정점 단위 최대 차이와 상위 인덱스를 기록함.
                var diffs = Enumerable.Range(0, dumped.Length)
                    .Select(index => (index, diff: Vector3.Distance(
                        dumped[index], editModeVertices[index])))
                    .OrderByDescending(entry => entry.diff)
                    .ToArray();
                report.Append(
                    $"\n dump[{renderer.name}] maxDiff={diffs[0].diff:F6}" +
                    $" diffCount(>1e-4)={diffs.Count(entry => entry.diff > 0.0001f)}/{dumped.Length}" +
                    $" top={string.Join(",", diffs.Take(8).Select(entry => $"v{entry.index}:{entry.diff:F4}"))}");

                // 제품 입력으로 강한 보정을 돌려 실패가 재현되는지 확인함.
                Type calculatorType = RequireProductType(
                    "NativeSkinningSurfaceCorrectionCalculator");
                object[] strongArguments = { dumped, contract, null };
                bool strongCalculated = (bool)InvokeStatic(
                    calculatorType, "TryCalculateStrongValidatedFrame", strongArguments);
                object strongCorrection = strongArguments[2];
                if (!strongCalculated || strongCorrection == null)
                {
                    report.Append(
                        $"\n dump[{renderer.name}] strong=uncalculated");
                    continue;
                }
                report.Append(
                    $"\n dump[{renderer.name}] strong" +
                    $" residual={ReadProperty<int>(strongCorrection, "ResidualSharpFoldCount")}" +
                    $" new={ReadProperty<int>(strongCorrection, "NewSharpFoldCount")}" +
                    $" maxDisp={ReadProperty<float>(strongCorrection, "MaximumVertexDisplacement"):F4}" +
                    $" strain={ReadProperty<float>(strongCorrection, "MaximumEdgeLengthStrain"):F4}" +
                    $" quality={InvokeStatic(calculatorType, "IsWithinStrongQuality", new[] { strongCorrection, contract })}");

                // 덤프 입력에서 실패하면 비율 스윕으로 해소 가능한 예산을 탐색함.
                if (ReadProperty<int>(strongCorrection, "ResidualSharpFoldCount") > 0)
                {
                    DumpDisplacementBudgetRetries(
                        calculatorType, dumped, contract, strongCorrection, report);
                    DumpStrainBudgetRetries(
                        calculatorType, dumped, contract, report);
                    DumpChainAndRingRetries(
                        calculatorType, dumped, contract, strongCorrection, report);
                }

                // 임계 부근 쌍의 baseline 각도를 product 입력과 edit 입력에서 비교함.
                Array facePairs = ReadProperty<Array>(contract, "FacePairs");
                float[] restAngles =
                    ReadProperty<float[]>(contract, "RestAnglesDegrees");
                float maximumRestAngle = ReadConst<float>(
                    "NativeSkinningSurfaceDefectDetector",
                    "MaximumCorrectableRestAngleDegrees");
                // 제품 실패 쌍(pair 182)의 덤프 baseline 각도를 직접 계측함.
                string sideName = ReadProperty<object>(contract, "Side")?.ToString();
                int probeIndex = sideName == "Left" ? 182 : sideName == "Right" ? 286 : -1;
                if (probeIndex >= 0 && facePairs.Length > probeIndex &&
                    TryMeasureAngle(dumped, facePairs.GetValue(probeIndex),
                        out float dumpedProbeAngle))
                {
                    TryMeasureAngle(editModeVertices,
                        facePairs.GetValue(probeIndex), out float editProbeAngle);
                    report.Append(
                        $"\n dump[{renderer.name}] probe pair[{probeIndex}]" +
                        $" prodBase={dumpedProbeAngle:F2} editBase={editProbeAngle:F2}");
                }
                var diverging = new List<string>();
                for (int pairIndex = 0; pairIndex < facePairs.Length; pairIndex++)
                {
                    if (restAngles[pairIndex] > maximumRestAngle ||
                        !TryMeasureAngle(dumped, facePairs.GetValue(pairIndex),
                            out float productAngle) ||
                        !TryMeasureAngle(editModeVertices,
                            facePairs.GetValue(pairIndex),
                            out float editAngle))
                    {
                        continue;
                    }
                    if (Mathf.Abs(productAngle - editAngle) > 0.05f &&
                        (productAngle > 140f || editAngle > 140f))
                    {
                        diverging.Add(
                            $"p{pairIndex}:prod={productAngle:F2}/edit={editAngle:F2}");
                    }
                }
                report.Append(
                    $"\n dump[{renderer.name}] divergingPairs={diverging.Count}" +
                    (diverging.Count > 0
                        ? " " + string.Join(" ", diverging.Take(12))
                        : string.Empty));

                // 평행이동 민감도 분리: edit 입력을 product 오프셋만큼 이동시켜 솔버 재현 여부를 봄.
                Vector3 offset = dumped[0] - editModeVertices[0];
                Vector3[] shiftedEdit = editModeVertices
                    .Select(vertex => vertex + offset)
                    .ToArray();
                object[] shiftedArguments = { shiftedEdit, contract, null };
                bool shiftedCalculated = (bool)InvokeStatic(
                    calculatorType, "TryCalculateStrongValidatedFrame",
                    shiftedArguments);
                object shiftedCorrection = shiftedArguments[2];
                string shiftedStrong = shiftedCalculated && shiftedCorrection != null
                    ? $"residual={ReadProperty<int>(shiftedCorrection, "ResidualSharpFoldCount")} maxDisp={ReadProperty<float>(shiftedCorrection, "MaximumVertexDisplacement"):F4} strain={ReadProperty<float>(shiftedCorrection, "MaximumEdgeLengthStrain"):F4} quality={InvokeStatic(calculatorType, "IsWithinStrongQuality", new[] { shiftedCorrection, contract })}"
                    : "uncalculated";
                report.Append(
                    $"\n dump[{renderer.name}] shiftedEdit(offset={offset.magnitude:F4})" +
                    $" strong={shiftedStrong}");

                // 계약 평가 정점 범위에서의 pose 차이를 분리 측정함.
                int[] evaluated =
                    ReadProperty<int[]>(contract, "EvaluatedVertexIndices");
                if (evaluated != null && evaluated.Length > 0)
                {
                    float armMax = evaluated.Max(index =>
                        Vector3.Distance(dumped[index], editModeVertices[index]));
                    var evaluatedSet = new HashSet<int>(evaluated);
                    float otherMax = Enumerable.Range(0, dumped.Length)
                        .Where(index => !evaluatedSet.Contains(index))
                        .Max(index => Vector3.Distance(
                            dumped[index], editModeVertices[index]));
                    report.Append(
                        $"\n dump[{renderer.name}] regionDiff" +
                        $" evaluated={armMax:F4} other={otherMax:F4}" +
                        $" evaluatedCount={evaluated.Length}");
                }

                // 강체 변환 여부: 정점 쌍 거리가 보존되면 product=edit+강체 변환.
                int rigiditySamples = 0;
                float rigidityMaxError = 0f;
                var rng = new System.Random(182);
                for (int sample = 0; sample < 200; sample++)
                {
                    int a = rng.Next(dumped.Length);
                    int b = rng.Next(dumped.Length);
                    if (a == b)
                    {
                        continue;
                    }
                    float error = Mathf.Abs(
                        Vector3.Distance(dumped[a], dumped[b]) -
                        Vector3.Distance(
                            editModeVertices[a], editModeVertices[b]));
                    rigidityMaxError = Mathf.Max(rigidityMaxError, error);
                    rigiditySamples++;
                }
                report.Append(
                    $"\n dump[{renderer.name}] rigidity" +
                    $" samples={rigiditySamples} maxPairDistErr={rigidityMaxError:F5}");

                // 회전 민감도: edit 정점을 임의 축으로 소량 회전 후 솔버 재현 여부.
                Vector3 centroid = Vector3.zero;
                foreach (Vector3 vertex in editModeVertices)
                {
                    centroid += vertex;
                }
                centroid /= editModeVertices.Length;
                Quaternion probeRotation = Quaternion.AngleAxis(
                    4f, Vector3.up);
                Vector3[] rotatedEdit = editModeVertices
                    .Select(vertex => centroid +
                        probeRotation * (vertex - centroid))
                    .ToArray();
                object[] rotatedArguments = { rotatedEdit, contract, null };
                bool rotatedCalculated = (bool)InvokeStatic(
                    calculatorType, "TryCalculateStrongValidatedFrame",
                    rotatedArguments);
                object rotatedCorrection = rotatedArguments[2];
                string rotatedStrong = rotatedCalculated && rotatedCorrection != null
                    ? $"residual={ReadProperty<int>(rotatedCorrection, "ResidualSharpFoldCount")} maxDisp={ReadProperty<float>(rotatedCorrection, "MaximumVertexDisplacement"):F4} strain={ReadProperty<float>(rotatedCorrection, "MaximumEdgeLengthStrain"):F4} quality={InvokeStatic(calculatorType, "IsWithinStrongQuality", new[] { rotatedCorrection, contract })}"
                    : "uncalculated";
                report.Append(
                    $"\n dump[{renderer.name}] rotatedEdit(4deg)" +
                    $" strong={rotatedStrong}");
            }
        }

        private static string DescribePairVertices(
            object pair,
            Transform[] bones,
            BoneWeight[] boneWeights,
            int lowerArmBoneIndex)
        {
            int[] vertices = PairVertexIndices(pair);
            return string.Join("/",
                vertices.Select(vertex => DescribeVertexWeights(
                    vertex, bones, boneWeights, lowerArmBoneIndex)));
        }

        // 정체 원인 구별: 클램프 포화 여부(disp/limit→1.0)와 면 퇴화 여부(면적)를 측정함.
        private static string DescribePairDisplacements(
            object pair,
            IReadOnlyList<Vector3> baselineVertices,
            IReadOnlyList<Vector3> correctedVertices,
            float displacementLimit)
        {
            int[] vertices = PairVertexIndices(pair);
            return string.Join("/", vertices.Select(vertex =>
            {
                float displacement = Vector3.Distance(
                    baselineVertices[vertex], correctedVertices[vertex]);
                float ratio = displacementLimit > 0f
                    ? displacement / displacementLimit
                    : 0f;
                return $"v{vertex}:{displacement:F4}({ratio:P0})";
            }));
        }

        private static string DescribePairFaceAreas(
            object pair,
            IReadOnlyList<Vector3> baselineVertices,
            IReadOnlyList<Vector3> correctedVertices,
            int[] triangles)
        {
            int firstFace = ReadProperty<int>(pair, "FirstFace");
            int secondFace = ReadProperty<int>(pair, "SecondFace");
            return $"f{firstFace}={FaceArea(baselineVertices, triangles, firstFace):E2}" +
                $"→{FaceArea(correctedVertices, triangles, firstFace):E2}" +
                $"/f{secondFace}={FaceArea(baselineVertices, triangles, secondFace):E2}" +
                $"→{FaceArea(correctedVertices, triangles, secondFace):E2}";
        }

        private static float FaceArea(
            IReadOnlyList<Vector3> vertices,
            int[] triangles,
            int faceIndex)
        {
            int offset = faceIndex * 3;
            if (offset < 0 || offset + 2 >= triangles.Length)
            {
                return -1f;
            }
            Vector3 a = vertices[triangles[offset]];
            Vector3 b = vertices[triangles[offset + 1]];
            Vector3 c = vertices[triangles[offset + 2]];
            return Vector3.Cross(b - a, c - a).magnitude * 0.5f;
        }

        private static int[] PairVertexIndices(object pair)
        {
            return new[]
            {
                ReadProperty<int>(pair, "FirstEdge"),
                ReadProperty<int>(pair, "SecondEdge"),
                ReadProperty<int>(pair, "FirstOpposite"),
                ReadProperty<int>(pair, "SecondOpposite")
            };
        }

        private static string DescribeBoneName(Transform[] bones, int boneIndex)
        {
            return boneIndex >= 0 && boneIndex < bones.Length && bones[boneIndex] != null
                ? bones[boneIndex].name
                : $"bone{boneIndex}";
        }

        private static string DescribeVertexWeights(
            int vertex,
            Transform[] bones,
            BoneWeight[] boneWeights,
            int lowerArmBoneIndex)
        {
            BoneWeight weight = boneWeights[vertex];
            var ordered = new (int bone, float value)[]
                {
                    (weight.boneIndex0, weight.weight0),
                    (weight.boneIndex1, weight.weight1),
                    (weight.boneIndex2, weight.weight2),
                    (weight.boneIndex3, weight.weight3)
                }
                .Where(entry => entry.value > 0f)
                .OrderByDescending(entry => entry.value)
                .Take(2)
                .Select(entry =>
                    $"{DescribeBoneName(bones, entry.bone)}:{entry.value:F2}");
            float lowerArmWeight = new[] { weight.boneIndex0, weight.boneIndex1,
                    weight.boneIndex2, weight.boneIndex3 }
                .Select((bone, index) => bone == lowerArmBoneIndex
                    ? new[] { weight.weight0, weight.weight1, weight.weight2,
                              weight.weight3 }[index]
                    : 0f)
                .Sum();
            return $"v{vertex}<{string.Join("+", ordered)}|lo={lowerArmWeight:F2}>";
        }

        private static bool TryMeasureAngle(
            IReadOnlyList<Vector3> vertices,
            object pair,
            out float angleDegrees)
        {
            Type builderType = RequireProductType("NativeSkinningSurfaceContractBuilder");
            object[] arguments = { vertices, pair, null };
            bool success = (bool)InvokeStatic(builderType, "TryMeasureAngle", arguments);
            angleDegrees = success ? (float)arguments[2] : 0f;
            return success;
        }

        private static string BoneName(object contract)
        {
            SkinnedMeshRenderer renderer =
                ReadProperty<SkinnedMeshRenderer>(contract, "Renderer");
            int index = ReadProperty<int>(contract, "LowerArmBoneIndex");
            Transform[] bones = renderer.bones;
            return index >= 0 && index < bones.Length && bones[index] != null
                ? bones[index].name
                : $"bone{index}";
        }

        private static T ReadConst<T>(string typeName, string fieldName)
        {
            FieldInfo field = RequireProductType(typeName).GetField(
                fieldName,
                BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic);
            Assert.That(field, Is.Not.Null, $"{typeName}.{fieldName} 상수가 필요합니다.");
            return (T)field.GetValue(null);
        }

        private static IList FindAll(Animator animator)
        {
            MethodInfo method = RequireProductType("NativeSkinningArmSurfaceSelector")
                .GetMethod(
                    "FindAll",
                    BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic);
            Assert.That(method, Is.Not.Null);
            return (IList)method.Invoke(null, new object[] { animator });
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

        private static void RequireLocalFixture(string clipAssetPath)
        {
            if (AssetDatabase.LoadAssetAtPath<GameObject>(clipAssetPath) == null ||
                AssetDatabase.LoadAssetAtPath<GameObject>(TestPrefabAssetPath) == null)
            {
                Assert.Ignore($"로컬 fixture를 찾을 수 없습니다: {clipAssetPath}");
            }
        }

        private static void EnsureHumanoidClipImport(string clipAssetPath)
        {
            MethodInfo method = RequireProductType("EditorHumanoidClipImportConfigurator")
                .GetMethod(
                    "EnsureHumanoid",
                    BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic);
            Assert.That(method, Is.Not.Null);
            method.Invoke(null, new object[] { clipAssetPath });
        }

        private static GameObject InstantiateTestPrefab()
        {
            GameObject source = AssetDatabase.LoadAssetAtPath<GameObject>(TestPrefabAssetPath);
            Assert.That(source, Is.Not.Null);
            GameObject target = UnityEngine.Object.Instantiate(source);
            target.name = "Native Skinning Preparation Diagnostics Target";
            target.hideFlags = HideFlags.HideAndDontSave;
            target.SetActive(true);
            return target;
        }

        private static Animator RequireHumanoidAnimator(GameObject target)
        {
            Animator animator = target.GetComponentInChildren<Animator>(true);
            Assert.That(animator, Is.Not.Null);
            Assert.That(animator.avatar, Is.Not.Null);
            Assert.That(animator.avatar.isValid && animator.avatar.isHuman, Is.True,
                "testPrefab Animator는 Humanoid여야 합니다.");
            return animator;
        }

        private static AnimationClip LoadHumanoidClip(string clipAssetPath)
        {
            AnimationClip clip = AssetDatabase.LoadAllAssetsAtPath(clipAssetPath)
                .OfType<AnimationClip>()
                .FirstOrDefault(candidate =>
                    !candidate.name.StartsWith("__", StringComparison.Ordinal) &&
                    candidate.humanMotion);
            Assert.That(clip, Is.Not.Null);
            return clip;
        }

        private static object CreateController()
        {
            Type controllerType = RequireProductType("HumanoidMotionPlaybackController");
            return Activator.CreateInstance(controllerType, nonPublic: true);
        }

        private static Type RequireProductType(string typeName)
        {
            Type type = typeof(Fbx2Vmd.FBXImporter.FBXVmdPipeline).Assembly.GetType(
                $"Fbx2Vmd.FBXImporter.{typeName}",
                throwOnError: false);
            Assert.That(type, Is.Not.Null, $"제품 타입 {typeName}이 필요합니다.");
            return type;
        }

        private static object Invoke(object target, string methodName, params object[] arguments)
        {
            MethodInfo method = target.GetType().GetMethod(
                methodName,
                BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            Assert.That(method, Is.Not.Null, $"{methodName} 메서드가 필요합니다.");
            return method.Invoke(target, arguments);
        }

        private static object InvokeStatic(
            Type type,
            string methodName,
            object[] arguments)
        {
            MethodInfo method = type.GetMethod(
                methodName,
                BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic);
            Assert.That(method, Is.Not.Null, $"{type.Name}.{methodName} 메서드가 필요합니다.");
            return method.Invoke(null, arguments);
        }

        private static T ReadProperty<T>(object target, string propertyName)
        {
            PropertyInfo property = target.GetType().GetProperty(
                propertyName,
                BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            Assert.That(property, Is.Not.Null, $"{propertyName} 속성이 필요합니다.");
            return (T)property.GetValue(target);
        }

        private static T ReadField<T>(object target, string fieldName)
        {
            FieldInfo field = target.GetType().GetField(
                fieldName,
                BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            Assert.That(field, Is.Not.Null, $"{fieldName} 필드가 필요합니다.");
            return (T)field.GetValue(target);
        }

        private static void DisposeController(object controller)
        {
            if (controller != null)
            {
                Invoke(controller, "Dispose");
            }
        }
    }
}
