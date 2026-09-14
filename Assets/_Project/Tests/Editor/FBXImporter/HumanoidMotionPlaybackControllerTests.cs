using System;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace Tests.Editor.FBXImporter
{
    public class HumanoidMotionPlaybackControllerTests
    {
        private const string TargetAssetPath =
            "Assets/_Project/Model/YYB Hatsune Miku_default/YYB Hatsune Miku_default_1.0ver.fbx";
        private const string ClipAssetPath = "Assets/Resources/Import_FBX/satisfaction_2.fbx";
        private const float TimeTolerance = 0.0001f;

        [TestCase(10366, HumanBodyBones.LeftLowerLeg, HumanBodyBones.LeftFoot)]
        [TestCase(10220, HumanBodyBones.RightLowerLeg, HumanBodyBones.RightFoot)]
        public void Given_NearlyExtendedLeg_When_GroundIsApplied_Then_PreservesBendContinuity(
            int frame, HumanBodyBones lowerBone, HumanBodyBones footBone)
        {
            GameObject target = InstantiateTarget();
            GameObject floor = new GameObject("무릎 방향 연속성 검증 바닥");
            object controller = CreateController();
            target.transform.position = new Vector3(1000f, 0f, 1000f);
            floor.hideFlags = HideFlags.HideAndDontSave;
            floor.transform.position = new Vector3(1000f, -0.05f, 1000f);
            floor.AddComponent<BoxCollider>().size = new Vector3(20f, 0.1f, 20f);
            try
            {
                Animator animator = RequireHumanoidAnimator(target);
                Invoke(controller, "PrepareWithArmDirectionReference", animator,
                    LoadHumanoidClip(), LoadSourceModel());
                Transform lower = animator.GetBoneTransform(lowerBone);
                Transform foot = animator.GetBoneTransform(footBone);
                Invoke(controller, "SeekFrame", frame - 1);
                Vector3 baseline = foot.position - lower.position;
                Invoke(controller, "SeekFrame", frame);
                float baselineStep = Vector3.Angle(baseline, foot.position - lower.position);

                Invoke(controller, "SetGroundResponseEnabled", true);
                Invoke(controller, "SeekFrame", frame - 1);
                Vector3 corrected = foot.position - lower.position;
                Invoke(controller, "SeekFrame", frame);
                float correctedStep = Vector3.Angle(corrected, foot.position - lower.position);
                // 보정 후 거의 일직선인 본에서 굽힘 평면을 다시 구하면 이 재현쌍에서 약20도 급변이 추가됨.
                Assert.That(correctedStep, Is.LessThan(baselineStep + 5f),
                    "기존 발 IK 직전의 굽힘 방향을 유지해 평면 전환에 의한 추가 급변을 막아야 합니다.");
            }
            finally
            {
                DisposeController(controller);
                UnityEngine.Object.DestroyImmediate(floor);
                UnityEngine.Object.DestroyImmediate(target);
            }
        }

        [TestCase(false)]
        // 원본 기준 경로는 Avatar 하단 상향 계약을 유지하며 정밀 경로는 실제 밑창 테스트로 검증함.
        public void Given_GroundCollider_When_Seeking_Then_RaisesFeetAndReleasesWithoutChangingGeometry(
            bool useSourceReference)
        {
            GameObject target = InstantiateTarget();
            GameObject floor = new GameObject("발 지면 반응 검증 바닥");
            object controller = CreateController();
            target.transform.position = new Vector3(1000f, 0f, 1000f);
            floor.hideFlags = HideFlags.HideAndDontSave;
            BoxCollider ground = floor.AddComponent<BoxCollider>();
            ground.size = new Vector3(4f, 0.1f, 4f);
            floor.SetActive(false);

            try
            {
                Animator animator = RequireHumanoidAnimator(target);
                if (useSourceReference)
                    Invoke(controller, "PrepareWithArmDirectionReference",
                        animator, LoadHumanoidClip(), LoadSourceModel());
                else
                    Invoke(controller, "Prepare", animator, LoadHumanoidClip());
                const float time = 58f / 60f;
                Invoke(controller, "Seek", time);
                Transform foot = animator.GetBoneTransform(HumanBodyBones.RightFoot);
                Transform toes = animator.GetBoneTransform(HumanBodyBones.RightToes);
                Transform upper = animator.GetBoneTransform(HumanBodyBones.RightUpperLeg);
                Transform lower = animator.GetBoneTransform(HumanBodyBones.RightLowerLeg);
                Transform[] bones = CaptureHumanoidBones(animator);
                Vector3[] positions = bones.Select(bone => bone.position).ToArray();
                Vector3[] localPositions = bones.Select(bone => bone.localPosition).ToArray();
                Vector3[] scales = bones.Select(bone => bone.localScale).ToArray();
                Quaternion footRotation = foot.rotation;
                Quaternion toeRotation = toes.rotation;
                float upperLength = Vector3.Distance(upper.position, lower.position);
                float lowerLength = Vector3.Distance(lower.position, foot.position);
                Vector3 baselineFoot = foot.position;
                Invoke(controller, "SetGroundResponseEnabled", true);

                // 자기 Collider가 바닥보다 위에 있어도 지면으로 선택하면 안 됨.
                BoxCollider self = target.AddComponent<BoxCollider>();
                self.center = new Vector3(0f, 0.35f, 0f);
                self.size = new Vector3(1f, 0.05f, 1f);
                floor.transform.position = new Vector3(1000f, 0.15f, 1000f);
                floor.SetActive(true);
                Physics.SyncTransforms();
                for (int repeat = 0; repeat < 3; repeat++)
                {
                    Invoke(controller, "Seek", time);
                    Assert.That(foot.position.y - animator.rightFeetBottomHeight,
                        Is.EqualTo(0.2f).Within(0.001f), "실제 바닥 높이가 발 하단 기준점에 반영되어야 합니다.");
                    Assert.That(Quaternion.Angle(foot.rotation, footRotation), Is.LessThan(0.1f));
                    Assert.That(Quaternion.Angle(toes.rotation, toeRotation), Is.LessThan(0.1f));
                    Assert.That(Vector3.Distance(upper.position, lower.position),
                        Is.EqualTo(upperLength).Within(0.0002f));
                    Assert.That(Vector3.Distance(lower.position, foot.position),
                        Is.EqualTo(lowerLength).Within(0.0002f));
                    for (int index = 0; index < bones.Length; index++)
                    {
                        Assert.That(Vector3.Distance(bones[index].localPosition, localPositions[index]),
                            Is.LessThan(0.00001f));
                        Assert.That(bones[index].localScale, Is.EqualTo(scales[index]));
                    }
                    Vector3 raised = foot.position;
                    Invoke(controller, "RestoreCurrentPose");
                    Assert.That(Vector3.Distance(foot.position, raised), Is.LessThan(0.0002f));
                }

                foreach (string condition in new[] { "lowered", "trigger", "absent", "disabled" })
                {
                    floor.transform.position = new Vector3(1000f, condition == "lowered" ? -0.25f : 0.15f, 1000f);
                    ground.isTrigger = condition == "trigger";
                    floor.SetActive(condition != "absent");
                    Invoke(controller, "SetGroundResponseEnabled", condition != "disabled");
                    Physics.SyncTransforms();
                    Invoke(controller, "Seek", time);
                    for (int index = 0; index < bones.Length; index++)
                        Assert.That(Vector3.Distance(bones[index].position, positions[index]),
                            Is.LessThan(0.0002f), $"{condition}: 지면 보정이 남거나 누적되면 안 됩니다.");
                }

                // 발 아래에 공간이 있으면 지면 쪽으로 끌어내리지 않음.
                ground.isTrigger = false;
                floor.transform.position = new Vector3(1000f,
                    baselineFoot.y - animator.rightFeetBottomHeight - 0.1f, 1000f);
                Invoke(controller, "SetGroundResponseEnabled", true);
                Physics.SyncTransforms();
                Invoke(controller, "Seek", time);
                Assert.That(Vector3.Distance(foot.position, baselineFoot), Is.LessThan(0.0002f));
            }
            finally
            {
                DisposeController(controller);
                UnityEngine.Object.DestroyImmediate(floor);
                UnityEngine.Object.DestroyImmediate(target);
            }
        }

        [TestCase(0f)]
        [TestCase(22.116667f)]
        [TestCase(31.1675f)]
        public void Given_RecognizedSleeveBinding_When_SeekingOrRestoring_Then_FollowsFinalUpperArmPose(float time)
        {
            GameObject target = InstantiateTarget();
            object controller = CreateController();
            Animator animator = RequireHumanoidAnimator(target);
            Transform[] supports = new[] { "joint_LeftArmM", "joint_RightArmM" }
                .Select(name => target.GetComponentsInChildren<Transform>(true)
                    .Single(bone => bone.name.EndsWith("." + name, StringComparison.Ordinal)))
                .ToArray();
            Quaternion[] originalRotations = supports.Select(bone => bone.localRotation).ToArray();
            Vector3[] originalPositions = supports.Select(bone => bone.localPosition).ToArray();
            Vector3[] originalScales = supports.Select(bone => bone.localScale).ToArray();
            Transform[] drivers = new[] { HumanBodyBones.LeftUpperArm, HumanBodyBones.RightUpperArm }
                .Select(animator.GetBoneTransform).ToArray();
            SkinnedMeshRenderer skin = target.GetComponentsInChildren<SkinnedMeshRenderer>()
                .Single(renderer => renderer.name == "U_Char_2");
            Quaternion[] offsets = supports.Select((support, index) =>
                (skin.sharedMesh.bindposes[Array.IndexOf(skin.bones, drivers[index])] *
                 skin.sharedMesh.bindposes[Array.IndexOf(skin.bones, support)].inverse).rotation).ToArray();

            try
            {
                Invoke(controller, "PrepareWithArmDirectionReference", animator, LoadHumanoidClip(), LoadSourceModel());
                AssertSupportFollows(drivers, supports, offsets);
                for (int repeat = 0; repeat < 3; repeat++)
                {
                    Invoke(controller, "Seek", time);
                    AssertSupportFollows(drivers, supports, offsets);
                    Invoke(controller, "RestoreCurrentPose");
                    AssertSupportFollows(drivers, supports, offsets);
                    for (int index = 0; index < supports.Length; index++)
                    {
                        Assert.That(Quaternion.Angle(supports[index].localRotation,
                            drivers[index].localRotation * offsets[index]), Is.LessThan(0.05f),
                            "소매 보조 본이 최종 상완 자세를 따라야 하며 반복 탐색 시 기준 회전이 누적되면 안 됩니다.");
                        Assert.That(supports[index].localPosition, Is.EqualTo(originalPositions[index]));
                        Assert.That(supports[index].localScale, Is.EqualTo(originalScales[index]));
                    }
                }
                DisposeController(controller);
                for (int index = 0; index < supports.Length; index++)
                    Assert.That(Quaternion.Angle(supports[index].localRotation, originalRotations[index]),
                        Is.LessThan(0.05f), "세션 종료 시 보조 본의 기존 회전을 복원해야 합니다.");
            }
            finally
            {
                DisposeController(controller);
                UnityEngine.Object.DestroyImmediate(target);
            }
        }

        private static void AssertSupportFollows(Transform[] drivers, Transform[] supports, Quaternion[] offsets)
        {
            for (int index = 0; index < supports.Length; index++)
            {
                Assert.That(Quaternion.Angle(supports[index].localRotation,
                    drivers[index].localRotation * offsets[index]), Is.LessThan(0.05f),
                    "Prepare, Seek, Restore 각 경로에서 최종 상완 추종이 필요합니다.");
            }
        }

        [OneTimeSetUp]
        public void EnsureHumanoidClipImport()
        {
            Type configuratorType = typeof(Fbx2Vmd.FBXImporter.FBXVmdPipeline).Assembly.GetType(
                "Fbx2Vmd.FBXImporter.EditorHumanoidClipImportConfigurator",
                throwOnError: false);
            Assert.That(configuratorType, Is.Not.Null,
                "FBX 기준 입력을 Humanoid로 준비하는 Editor 설정기가 필요합니다.");

            MethodInfo method = configuratorType.GetMethod(
                "EnsureHumanoid",
                BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic);
            Assert.That(method, Is.Not.Null, "EnsureHumanoid 메서드가 필요합니다.");
            method.Invoke(null, new object[] { ClipAssetPath });
        }

        [Test]
        public void Given_PreparedClip_When_TickingWithoutPlay_Then_RemainsReadyAtFirstFrame()
        {
            GameObject target = InstantiateTarget();
            object controller = CreateController();

            try
            {
                Invoke(controller, "Prepare", RequireHumanoidAnimator(target), LoadHumanoidClip());

                Assert.That(ReadProperty(controller, "State").ToString(), Is.EqualTo("Ready"));
                Assert.That(ReadProperty<float>(controller, "CurrentTimeSeconds"),
                    Is.EqualTo(0f).Within(TimeTolerance));

                Invoke(controller, "Tick", 1f);

                Assert.That(ReadProperty(controller, "State").ToString(), Is.EqualTo("Ready"));
                Assert.That(ReadProperty<float>(controller, "CurrentTimeSeconds"),
                    Is.EqualTo(0f).Within(TimeTolerance),
                    "임포트 직후에는 사용자가 재생하기 전까지 시간이 진행되면 안 됩니다.");
            }
            finally
            {
                DisposeController(controller);
                UnityEngine.Object.DestroyImmediate(target);
            }
        }

        [Test]
        public void Given_ReadyClip_When_PlayingAndTicking_Then_AdvancesExactlyOnce()
        {
            GameObject target = InstantiateTarget();
            object controller = CreateController();

            try
            {
                Invoke(controller, "Prepare", RequireHumanoidAnimator(target), LoadHumanoidClip());
                Assert.That((bool)Invoke(controller, "Play"), Is.True);

                Invoke(controller, "Tick", 0.5f);

                Assert.That(ReadProperty(controller, "State").ToString(), Is.EqualTo("Playing"));
                Assert.That(ReadProperty<float>(controller, "CurrentTimeSeconds"),
                    Is.EqualTo(0.5f).Within(TimeTolerance));
            }
            finally
            {
                DisposeController(controller);
                UnityEngine.Object.DestroyImmediate(target);
            }
        }

        [Test]
        public void Given_PlayingClip_When_PausingOnce_Then_HoldsUntilSingleResume()
        {
            GameObject target = InstantiateTarget();
            object controller = CreateController();

            try
            {
                Invoke(controller, "Prepare", RequireHumanoidAnimator(target), LoadHumanoidClip());
                Invoke(controller, "Play");
                Invoke(controller, "Tick", 0.5f);

                Assert.That((bool)Invoke(controller, "Pause"), Is.True);
                Invoke(controller, "Tick", 1f);

                Assert.That(ReadProperty(controller, "State").ToString(), Is.EqualTo("Paused"));
                Assert.That(ReadProperty<float>(controller, "CurrentTimeSeconds"),
                    Is.EqualTo(0.5f).Within(TimeTolerance));

                Assert.That((bool)Invoke(controller, "Play"), Is.True);
                Invoke(controller, "Tick", 0.25f);

                Assert.That(ReadProperty(controller, "State").ToString(), Is.EqualTo("Playing"));
                Assert.That(ReadProperty<float>(controller, "CurrentTimeSeconds"),
                    Is.EqualTo(0.75f).Within(TimeTolerance),
                    "일시정지는 한 번의 재생 명령으로 해제되어야 합니다.");
            }
            finally
            {
                DisposeController(controller);
                UnityEngine.Object.DestroyImmediate(target);
            }
        }

        [Test]
        public void Given_PlayingClip_When_Stopping_Then_ReturnsToReadyFirstFrame()
        {
            GameObject target = InstantiateTarget();
            object controller = CreateController();

            try
            {
                Invoke(controller, "Prepare", RequireHumanoidAnimator(target), LoadHumanoidClip());
                Invoke(controller, "Play");
                Invoke(controller, "Tick", 0.5f);

                Assert.That((bool)Invoke(controller, "Stop"), Is.True);

                Assert.That(ReadProperty(controller, "State").ToString(), Is.EqualTo("Ready"));
                Assert.That(ReadProperty<float>(controller, "CurrentTimeSeconds"),
                    Is.EqualTo(0f).Within(TimeTolerance));
            }
            finally
            {
                DisposeController(controller);
                UnityEngine.Object.DestroyImmediate(target);
            }
        }

        [Test]
        public void Given_RootTranslationClip_When_SeekingRepeatedly_Then_AppliesDeterministicXZPosition()
        {
            GameObject target = InstantiateTarget();
            object controller = CreateController();

            try
            {
                Animator animator = RequireHumanoidAnimator(target);
                animator.transform.rotation = Quaternion.Euler(0f, 37f, 0f);
                Vector3 rootAnchor = animator.transform.position;
                Quaternion rootAnchorRotation = animator.transform.rotation;
                const float movementSampleTime = 31.1675f;

                Invoke(controller, "Prepare", animator, LoadHumanoidClip());
                Invoke(controller, "Seek", movementSampleTime);
                Vector3 firstMovementPosition = animator.transform.position;
                Vector3 expectedMovementPosition = CalculateExpectedRootPosition(
                    LoadHumanoidClip(),
                    rootAnchor,
                    rootAnchorRotation,
                    movementSampleTime);

                Assert.That(
                    Vector2.Distance(
                        new Vector2(rootAnchor.x, rootAnchor.z),
                        new Vector2(firstMovementPosition.x, firstMovementPosition.z)),
                    Is.GreaterThan(0.1f),
                    "RootT XZ 이동이 대상 모델의 월드 위치에 반영되어야 합니다.");
                Assert.That(
                    firstMovementPosition.x,
                    Is.EqualTo(expectedMovementPosition.x).Within(TimeTolerance));
                Assert.That(
                    firstMovementPosition.z,
                    Is.EqualTo(expectedMovementPosition.z).Within(TimeTolerance),
                    "배치 회전을 반영한 RootT 궤적과 대상 루트 위치가 일치해야 합니다.");
                Assert.That(
                    firstMovementPosition.y,
                    Is.EqualTo(rootAnchor.y).Within(TimeTolerance),
                    "XZ 루트 이동이 접지용 Y 위치를 변경하면 안 됩니다.");

                Invoke(controller, "Seek", 0f);
                Assert.That(animator.transform.position.x, Is.EqualTo(rootAnchor.x).Within(TimeTolerance));
                Assert.That(animator.transform.position.z, Is.EqualTo(rootAnchor.z).Within(TimeTolerance));

                Invoke(controller, "Seek", movementSampleTime);
                Assert.That(
                    Vector3.Distance(animator.transform.position, firstMovementPosition),
                    Is.LessThanOrEqualTo(TimeTolerance),
                    "동일 시점 재탐색에서 root 위치가 누적되면 안 됩니다.");
            }
            finally
            {
                DisposeController(controller);
                UnityEngine.Object.DestroyImmediate(target);
            }
        }

        [Test]
        public void Given_PreparedClip_When_SeekingFrame_Then_EvaluatesExactFrameWithoutPlaying()
        {
            GameObject target = InstantiateTarget();
            object controller = CreateController();

            try
            {
                Invoke(controller, "Prepare", RequireHumanoidAnimator(target), LoadHumanoidClip());
                float frameRate = ReadProperty<float>(controller, "ClipFrameRate");
                int lastFrameIndex = ReadProperty<int>(controller, "LastFrameIndex");
                int requestedFrameIndex = Math.Min(30, lastFrameIndex);

                Assert.That(frameRate, Is.GreaterThan(0f));
                Assert.That((bool)Invoke(controller, "SeekFrame", requestedFrameIndex), Is.True);
                Assert.That(ReadProperty<int>(controller, "CurrentFrameIndex"),
                    Is.EqualTo(requestedFrameIndex));
                Assert.That(ReadProperty<float>(controller, "CurrentTimeSeconds"),
                    Is.EqualTo(requestedFrameIndex / frameRate).Within(TimeTolerance));
                Assert.That(ReadProperty(controller, "State").ToString(), Is.EqualTo("Ready"),
                    "프레임 이동만으로 모션이 자동 재생되면 안 됩니다.");
            }
            finally
            {
                DisposeController(controller);
                UnityEngine.Object.DestroyImmediate(target);
            }
        }

        [Test]
        public void Given_SourceArmDirections_When_PreparingCorrection_Then_AlignsWithoutGeometryChanges()
        {
            GameObject sourceTarget = InstantiateSourceModel();
            GameObject baselineTarget = InstantiateTarget();
            GameObject correctedTarget = InstantiateTarget();
            object sourceController = CreateController();
            object baselineController = CreateController();
            object correctedController = CreateController();

            try
            {
                AnimationClip clip = LoadHumanoidClip();
                GameObject sourceModel = LoadSourceModel();
                const float sampleTime = 31.1675f;

                Animator sourceAnimator = RequireHumanoidAnimator(sourceTarget);
                Invoke(sourceController, "Prepare", sourceAnimator, clip);
                Invoke(sourceController, "Seek", sampleTime);

                Animator baselineAnimator = RequireHumanoidAnimator(baselineTarget);
                Invoke(baselineController, "Prepare", baselineAnimator, clip);
                Invoke(baselineController, "Seek", sampleTime);
                Transform[] baselineBones = CaptureHumanoidBones(baselineAnimator);

                Animator correctedAnimator = RequireHumanoidAnimator(correctedTarget);
                Invoke(
                    correctedController,
                    "PrepareWithArmDirectionReference",
                    correctedAnimator,
                    clip,
                    sourceModel);
                Invoke(correctedController, "Seek", sampleTime);
                Transform[] correctedBones = CaptureHumanoidBones(correctedAnimator);

                float baselineError = CalculateArmDirectionMeanError(
                    sourceAnimator,
                    baselineAnimator);
                float correctedError = CalculateArmDirectionMeanError(
                    sourceAnimator,
                    correctedAnimator);
                Assert.That(correctedError, Is.LessThan(baselineError * 0.05f),
                    "Swing 보정은 직접 Humanoid 재생의 팔 방향 오차를 줄여야 합니다.");
                Assert.That(correctedError, Is.LessThan(0.1f),
                    "보정된 상완·전완 방향은 원본 FBX와 0.1도 안에서 일치해야 합니다.");
                AssertEquivalentGeometry(baselineBones, correctedBones);
            }
            finally
            {
                DisposeController(sourceController);
                DisposeController(baselineController);
                DisposeController(correctedController);
                UnityEngine.Object.DestroyImmediate(sourceTarget);
                UnityEngine.Object.DestroyImmediate(baselineTarget);
                UnityEngine.Object.DestroyImmediate(correctedTarget);
            }
        }

        private static object CreateController()
        {
            Type controllerType = typeof(Fbx2Vmd.FBXImporter.FBXVmdPipeline).Assembly.GetType(
                "Fbx2Vmd.FBXImporter.HumanoidMotionPlaybackController",
                throwOnError: false);
            Assert.That(controllerType, Is.Not.Null,
                "명시적 재생 상태를 관리하는 모델 중립 컨트롤러가 필요합니다.");
            return Activator.CreateInstance(controllerType, nonPublic: true);
        }

        private static Vector3 CalculateExpectedRootPosition(
            AnimationClip clip,
            Vector3 anchorPosition,
            Quaternion anchorRotation,
            float timeSeconds)
        {
            AnimationCurve rootX = LoadRootTranslationCurve(clip, "RootT.x");
            AnimationCurve rootZ = LoadRootTranslationCurve(clip, "RootT.z");
            Assert.That(rootX, Is.Not.Null, "RootT.x 곡선이 필요합니다.");
            Assert.That(rootZ, Is.Not.Null, "RootT.z 곡선이 필요합니다.");

            Vector3 clipSpaceOffset = new Vector3(
                rootX.Evaluate(timeSeconds) - rootX.Evaluate(0f),
                0f,
                rootZ.Evaluate(timeSeconds) - rootZ.Evaluate(0f));
            return anchorPosition + anchorRotation * clipSpaceOffset;
        }

        private static AnimationCurve LoadRootTranslationCurve(
            AnimationClip clip,
            string propertyName)
        {
            return AnimationUtility.GetCurveBindings(clip)
                .Where(binding =>
                    binding.type == typeof(Animator) &&
                    string.IsNullOrEmpty(binding.path) &&
                    binding.propertyName == propertyName)
                .Select(binding => AnimationUtility.GetEditorCurve(clip, binding))
                .FirstOrDefault();
        }

        private static object Invoke(object target, string methodName, params object[] arguments)
        {
            MethodInfo method = target.GetType().GetMethod(
                methodName,
                BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            Assert.That(method, Is.Not.Null, $"{methodName} 메서드가 필요합니다.");
            return method.Invoke(target, arguments);
        }

        private static object ReadProperty(object target, string propertyName)
        {
            PropertyInfo property = target.GetType().GetProperty(
                propertyName,
                BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            Assert.That(property, Is.Not.Null, $"{propertyName} 속성이 필요합니다.");
            return property.GetValue(target);
        }

        private static T ReadProperty<T>(object target, string propertyName)
        {
            return (T)ReadProperty(target, propertyName);
        }

        private static void DisposeController(object controller)
        {
            if (controller != null)
            {
                Invoke(controller, "Dispose");
            }
        }

        private static HumanPose CaptureCurrentPose(object controller)
        {
            object[] arguments = { null };
            Assert.That(
                (bool)Invoke(controller, "TryCaptureCurrentPose", arguments),
                Is.True);
            return (HumanPose)arguments[0];
        }

        private static float CalculateArmDirectionMeanError(
            Animator sourceAnimator,
            Animator targetAnimator)
        {
            (HumanBodyBones Start, HumanBodyBones End)[] segments =
            {
                (HumanBodyBones.LeftUpperArm, HumanBodyBones.LeftLowerArm),
                (HumanBodyBones.LeftLowerArm, HumanBodyBones.LeftHand),
                (HumanBodyBones.RightUpperArm, HumanBodyBones.RightLowerArm),
                (HumanBodyBones.RightLowerArm, HumanBodyBones.RightHand)
            };

            return segments.Average(segment => Vector3.Angle(
                CalculateRootSpaceDirection(
                    sourceAnimator,
                    segment.Start,
                    segment.End),
                CalculateRootSpaceDirection(
                    targetAnimator,
                    segment.Start,
                    segment.End)));
        }

        private static Vector3 CalculateRootSpaceDirection(
            Animator animator,
            HumanBodyBones startBone,
            HumanBodyBones endBone)
        {
            Transform start = animator.GetBoneTransform(startBone);
            Transform end = animator.GetBoneTransform(endBone);
            Assert.That(start, Is.Not.Null, $"{startBone} 본이 필요합니다.");
            Assert.That(end, Is.Not.Null, $"{endBone} 본이 필요합니다.");
            return animator.transform.InverseTransformDirection(
                end.position - start.position).normalized;
        }

        private static Transform[] CaptureHumanoidBones(Animator animator)
        {
            return Enumerable.Range(0, (int)HumanBodyBones.LastBone)
                .Select(index => animator.GetBoneTransform((HumanBodyBones)index))
                .Where(bone => bone != null)
                .ToArray();
        }

        private static void AssertEquivalentGeometry(
            Transform[] expectedBones,
            Transform[] actualBones)
        {
            Assert.That(actualBones.Length, Is.EqualTo(expectedBones.Length));
            for (int index = 0; index < expectedBones.Length; index++)
            {
                Assert.That(
                    Vector3.Distance(
                        actualBones[index].localPosition,
                        expectedBones[index].localPosition),
                    Is.LessThanOrEqualTo(TimeTolerance),
                    $"{actualBones[index].name} localPosition이 보정으로 바뀌면 안 됩니다.");
                Assert.That(
                    Vector3.Distance(
                        actualBones[index].localScale,
                        expectedBones[index].localScale),
                    Is.LessThanOrEqualTo(TimeTolerance),
                    $"{actualBones[index].name} localScale이 보정으로 바뀌면 안 됩니다.");
            }
        }

        private static GameObject InstantiateTarget()
        {
            GameObject source = AssetDatabase.LoadAssetAtPath<GameObject>(TargetAssetPath);
            Assert.That(source, Is.Not.Null, $"기준 모델을 찾을 수 없습니다: {TargetAssetPath}");

            GameObject target = UnityEngine.Object.Instantiate(source);
            target.name = "Humanoid Motion Playback Controller Target";
            target.hideFlags = HideFlags.HideAndDontSave;
            target.SetActive(true);
            return target;
        }

        private static GameObject InstantiateSourceModel()
        {
            GameObject target = UnityEngine.Object.Instantiate(LoadSourceModel());
            target.name = "Humanoid Motion Source Reference";
            target.hideFlags = HideFlags.HideAndDontSave;
            target.SetActive(true);
            return target;
        }

        private static Animator RequireHumanoidAnimator(GameObject target)
        {
            Animator animator = target.GetComponentInChildren<Animator>(true);
            Assert.That(animator, Is.Not.Null, "기준 모델에 Animator가 필요합니다.");
            Assert.That(animator.avatar, Is.Not.Null, "기준 모델에 Avatar가 필요합니다.");
            Assert.That(animator.avatar.isValid && animator.avatar.isHuman, Is.True,
                "기준 모델은 유효한 Humanoid Avatar를 사용해야 합니다.");
            return animator;
        }

        private static AnimationClip LoadHumanoidClip()
        {
            AnimationClip clip = AssetDatabase
                .LoadAllAssetsAtPath(ClipAssetPath)
                .OfType<AnimationClip>()
                .FirstOrDefault(candidate =>
                    !candidate.name.StartsWith("__", StringComparison.Ordinal) &&
                    candidate.humanMotion);
            Assert.That(clip, Is.Not.Null, $"Humanoid 클립을 찾을 수 없습니다: {ClipAssetPath}");
            return clip;
        }

        private static GameObject LoadSourceModel()
        {
            GameObject source = AssetDatabase.LoadAssetAtPath<GameObject>(ClipAssetPath);
            Assert.That(source, Is.Not.Null,
                $"Humanoid 기준 모델을 찾을 수 없습니다: {ClipAssetPath}");
            return source;
        }
    }
}
