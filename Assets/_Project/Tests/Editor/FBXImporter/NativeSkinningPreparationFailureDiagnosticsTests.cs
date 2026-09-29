using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
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

        [TestCase("tetoris_001.fbx", 1)]
        [TestCase("tetoris_001.fbx", 117)]
        [TestCase("tetoris_001.fbx", 153)]
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
            foreach (float ratio in new[] { 0.01f, 0.02f, 0.025f, 0.03f, 0.04f, 0.05f, 0.08f })
            {
                object configuration = Activator.CreateInstance(
                    configurationType,
                    BindingFlags.Instance | BindingFlags.NonPublic,
                    null,
                    new object[] { 640, 64, 32, 0.03f, ratio },
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
