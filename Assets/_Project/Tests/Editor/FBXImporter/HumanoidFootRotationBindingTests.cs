using System;
using System.Linq;
using System.Reflection;
using Fbx2Vmd.FBXImporter;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace Tests.Editor.FBXImporter
{
    public class HumanoidFootRotationBindingTests
    {
        private const BindingFlags Flags = BindingFlags.Instance | BindingFlags.Static |
            BindingFlags.Public | BindingFlags.NonPublic;

        [Test]
        public void Given_DifferentBoneAxes_When_ApplyingRepeatedly_Then_AlignsBothAxesAndRestoresNativePose()
        {
            var root = new GameObject("발 기준축 검증") { hideFlags = HideFlags.HideAndDontSave };
            try
            {
                Transform foot = new GameObject("발").transform;
                foot.SetParent(root.transform, false);
                foot.localPosition = new Vector3(0.1f, 0.2f, 0.3f);
                foot.localRotation = Quaternion.Euler(23f, 51f, -17f);
                Transform toes = new GameObject("발끝").transform;
                toes.SetParent(foot, false);
                toes.position = foot.position + new Vector3(0.03f, -0.08f, 0.2f);
                toes.localRotation = Quaternion.Euler(7f, 12f, -3f);
                root.transform.rotation = Quaternion.Euler(0f, 93f, 0f);
                Quaternion basis = CaptureLocalFrame(foot, toes, root.transform.up);
                object binding = CreateBinding(foot, toes, root.transform.up);
                foot.localRotation = Quaternion.Euler(-39f, 72f, 29f);
                Quaternion original = foot.localRotation;
                Vector3 position = foot.position;
                Quaternion toeRotation = toes.localRotation;
                Vector3 toePosition = toes.localPosition;
                Quaternion desired = root.transform.rotation * Quaternion.Euler(31f, -18f, 14f);
                Assert.That(Invoke(binding, "TryApplyWorldFrame", desired), Is.True);
                AssertAxes(foot.rotation * basis, desired);
                Assert.That(Invoke(binding, "TryApplyWorldFrame", Quaternion.Euler(-13f, 67f, 22f)), Is.True);
                Quaternion applied = foot.localRotation;
                Assert.That(Invoke(binding, "TryApplyWorldFrame", new Quaternion(0f, 0f, 0f, 0f)), Is.False);
                AssertAxes(foot.localRotation, applied);
                Assert.That(Vector3.Distance(foot.position, position), Is.LessThan(0.000001f));
                Assert.That(toes.localPosition, Is.EqualTo(toePosition));
                Assert.That(toes.localRotation, Is.EqualTo(toeRotation));
                Assert.That(foot.localScale, Is.EqualTo(Vector3.one));
                Invoke(binding, "RestoreAppliedRotation");
                Invoke(binding, "RestoreAppliedRotation");
                AssertAxes(foot.localRotation, original);
            }
            finally { UnityEngine.Object.DestroyImmediate(root); }
        }

        [Test]
        public void Given_UnsupportedFoot_When_CreatingOrApplying_Then_RejectsWithoutMutation()
        {
            var root = new GameObject("미지원 발 기준축 검증") { hideFlags = HideFlags.HideAndDontSave };
            try
            {
                Transform toes = new GameObject("발끝").transform;
                toes.SetParent(root.transform, false);
                toes.localPosition = Vector3.up;
                Assert.That(TryCreateBinding(root.transform, toes, Vector3.up), Is.Null);
                toes.localPosition = Vector3.forward;
                Assert.That(TryCreateBinding(root.transform, null, Vector3.up), Is.Null);
                root.transform.localScale = new Vector3(-1f, 1f, 1f);
                Assert.That(TryCreateBinding(root.transform, toes, Vector3.up), Is.Null);
                root.transform.localScale = Vector3.one;
                object binding = CreateBinding(root.transform, toes, Vector3.up);
                root.transform.localScale = new Vector3(1f, 2f, 1f);
                Assert.That(Invoke(binding, "TryApplyWorldFrame", Quaternion.Euler(10f, 20f, 30f)), Is.False);
                Assert.That(root.transform.localRotation, Is.EqualTo(Quaternion.identity));
            }
            finally { UnityEngine.Object.DestroyImmediate(root); }
        }

        [TestCase("Assets/_Project/Model/YYB Hatsune Miku_default/YYB Hatsune Miku_default_1.0ver.fbx")]
        [TestCase("Assets/_Project/FBX/Snake Hip Hop Dance.fbx")]
        public void Given_TwoHumanoids_When_PreparingAndSeeking_Then_PreservesSourceFootFramesAndFailureRollback(string modelPath)
        {
            const string clipPath = "Assets/Resources/Import_FBX/satisfaction_2.fbx";
            GameObject source = AssetDatabase.LoadAssetAtPath<GameObject>(clipPath);
            if (source == null) Assert.Ignore("로컬 원본 FBX가 없는 환경에서는 실제 모델 검증을 생략함");
            AnimationClip clip = AssetDatabase.LoadAllAssetsAtPath(clipPath).OfType<AnimationClip>()
                .First(c => !c.name.StartsWith("__preview__", StringComparison.Ordinal));
            GameObject model = UnityEngine.Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>(modelPath));
            model.hideFlags = HideFlags.HideAndDontSave;
            model.transform.SetPositionAndRotation(new Vector3(5000f, 0f, 5000f), Quaternion.Euler(0f, 90f, 0f));
            foreach (MonoBehaviour script in model.GetComponentsInChildren<MonoBehaviour>(true)) script.enabled = false;
            object controller = Activator.CreateInstance(FindType("HumanoidMotionPlaybackController"), true);
            object reference = Activator.CreateInstance(FindType("EditorHumanoidPoseReferencePlayer"), true);
            try
            {
                Animator target = model.GetComponentInChildren<Animator>(true);
                target.enabled = true;
                Transform[] feet = { target.GetBoneTransform(HumanBodyBones.LeftFoot), target.GetBoneTransform(HumanBodyBones.RightFoot) };
                Transform[] toes = { target.GetBoneTransform(HumanBodyBones.LeftToes), target.GetBoneTransform(HumanBodyBones.RightToes) };
                Quaternion[] targetBases = feet.Select((f, i) => CaptureLocalFrame(f, toes[i], target.transform.up)).ToArray();
                Quaternion placement = target.transform.rotation;
                Invoke(reference, "InitializeFromSourceModel", source, clip);
                Animator sourceAnimator = (Animator)Field(reference, "_referenceAnimator");
                Transform[] sourceFeet = { sourceAnimator.GetBoneTransform(HumanBodyBones.LeftFoot), sourceAnimator.GetBoneTransform(HumanBodyBones.RightFoot) };
                Transform[] sourceToes = { sourceAnimator.GetBoneTransform(HumanBodyBones.LeftToes), sourceAnimator.GetBoneTransform(HumanBodyBones.RightToes) };
                Quaternion[] sourceBases = sourceFeet.Select((f, i) => CaptureLocalFrame(f, sourceToes[i], sourceAnimator.transform.up)).ToArray();
                placement *= Quaternion.Inverse(sourceAnimator.transform.rotation);
                Invoke(controller, "PrepareWithArmDirectionReference", target, clip, source);
                Assert.That(Property(controller, "HasFootRotationReference"), Is.True);
                Invoke(controller, "SetGroundResponseEnabled", true);
                foreach (float frame in new[] { 0f, 5512f, 1172f, 5512f })
                {
                    float time = frame / clip.frameRate;
                    Invoke(Field(reference, "_animationPlayer"), "EvaluateAt", time);
                    Quaternion[] expected = sourceFeet.Select((f, i) => placement * f.rotation * sourceBases[i]).ToArray();
                    Invoke(controller, "EvaluateEditorContactReference", time);
                    for (int i = 0; i < feet.Length; i++) AssertAxes(feet[i].rotation * targetBases[i], expected[i]);
                    Assert.That(Invoke(controller, "Seek", time), Is.True);
                    Assert.That(Property(controller, "LastGroundingStatus").ToString(), Is.EqualTo("NoGround"));
                    for (int i = 0; i < feet.Length; i++) AssertAxes(feet[i].rotation * targetBases[i], expected[i]);
                    Assert.That(Invoke(controller, "RestoreCurrentPose"), Is.True);
                    for (int i = 0; i < feet.Length; i++) AssertAxes(feet[i].rotation * targetBases[i], expected[i]);
                }
                // 오른발 실패 전에 적용된 왼발까지 원본으로 돌아가야 함.
                Invoke(controller, "RestoreFootRotationBindings");
                Quaternion leftOriginal = feet[0].localRotation;
                feet[1].localScale = new Vector3(1f, 2f, 1f);
                TargetInvocationException failure = Assert.Throws<TargetInvocationException>(() =>
                    Invoke(controller, "ApplyFootRotationReference", 0f));
                Assert.That(failure.InnerException, Is.TypeOf<InvalidOperationException>());
                AssertAxes(feet[0].localRotation, leftOriginal);
                feet[1].localScale = Vector3.one;
                Quaternion[] originalRotations = feet.Select(f => f.localRotation).ToArray();
                Invoke(controller, "ApplyFootRotationReference", 0f);
                ((IDisposable)controller).Dispose();
                for (int i = 0; i < feet.Length; i++) AssertAxes(feet[i].localRotation, originalRotations[i]);
                Assert.That(Property(controller, "HasFootRotationReference"), Is.False);
            }
            finally
            {
                ((IDisposable)controller).Dispose();
                ((IDisposable)reference).Dispose();
                UnityEngine.Object.DestroyImmediate(model);
            }
        }

        private static Quaternion CaptureLocalFrame(Transform foot, Transform toes, Vector3 up) =>
            Quaternion.Inverse(foot.rotation) * Quaternion.LookRotation(
                Vector3.ProjectOnPlane(toes.position - foot.position, up).normalized, up);

        private static void AssertAxes(Quaternion actual, Quaternion expected)
        {
            Assert.That(Vector3.Distance(actual * Vector3.forward, expected * Vector3.forward), Is.LessThan(0.00001f));
            Assert.That(Vector3.Distance(actual * Vector3.up, expected * Vector3.up), Is.LessThan(0.00001f));
        }

        private static object CreateBinding(Transform foot, Transform toes, Vector3 up)
        {
            object binding = TryCreateBinding(foot, toes, up);
            Assert.That(binding, Is.Not.Null);
            return binding;
        }

        private static object TryCreateBinding(Transform foot, Transform toes, Vector3 up)
        {
            object[] args = { foot, toes, up, null };
            bool created = (bool)FindType("HumanoidFootRotationBinding").GetMethod("TryCreate", Flags).Invoke(null, args);
            return created ? args[3] : null;
        }

        private static Type FindType(string name) => typeof(FBXVmdPipeline).Assembly.GetType("Fbx2Vmd.FBXImporter." + name, true);
        private static object Invoke(object target, string name, params object[] args) => target.GetType().GetMethod(name, Flags).Invoke(target, args);
        private static object Field(object target, string name) => target.GetType().GetField(name, Flags).GetValue(target);
        private static object Property(object target, string name) => target.GetType().GetProperty(name, Flags).GetValue(target);
    }
}
