using System;
using System.Linq;
using System.Reflection;
using Fbx2Vmd.FBXImporter;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace Tests.Editor.FBXImporter
{
    public class EditorHumanoidLegBendCalibrationTests
    {
        [TestCase("Assets/_Project/Model/YYB Hatsune Miku_default/YYB Hatsune Miku_default_1.0ver.fbx")]
        [TestCase("Assets/_Project/FBX/Snake Hip Hop Dance.fbx")]
        public void Given_ReferenceModel_When_CalibratingTwice_Then_PreservesSourceAndDestroysClones(string path)
        {
            GameObject model = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            if (model == null)
                Assert.Ignore("로컬 FBX 입력이 없는 환경에서는 실제 모델 교정 검증을 생략함");
            Animator source = model.GetComponentInChildren<Animator>(true);
            Transform[] transforms = model.GetComponentsInChildren<Transform>(true);
            Vector3[] positions = transforms.Select(t => t.localPosition).ToArray();
            Quaternion[] rotations = transforms.Select(t => t.localRotation).ToArray();
            Vector3[] scales = transforms.Select(t => t.localScale).ToArray();
            int[] objects = Resources.FindObjectsOfTypeAll<GameObject>().Select(g => g.GetInstanceID()).OrderBy(id => id).ToArray();
            bool enabled = source.enabled;
            object[] first = { source, Vector3.zero, Vector3.zero };
            object[] second = { source, Vector3.zero, Vector3.zero };
            Assert.That(Capture(first), Is.True);
            Assert.That(Capture(second), Is.True);
            for (int side = 1; side <= 2; side++)
            {
                Assert.That(((Vector3)first[side]).magnitude, Is.EqualTo(1f).Within(0.000001f));
                Assert.That(Vector3.Distance((Vector3)first[side], (Vector3)second[side]), Is.LessThan(0.000001f));
            }
            for (int i = 0; i < transforms.Length; i++)
            {
                Assert.That(transforms[i].localPosition, Is.EqualTo(positions[i]));
                Assert.That(transforms[i].localRotation, Is.EqualTo(rotations[i]));
                Assert.That(transforms[i].localScale, Is.EqualTo(scales[i]));
            }
            Assert.That(source.enabled, Is.EqualTo(enabled));
            Assert.That(Resources.FindObjectsOfTypeAll<GameObject>().Select(g => g.GetInstanceID()).OrderBy(id => id).ToArray(), Is.EqualTo(objects));
        }

        [Test]
        public void Given_MissingAvatar_When_Calibrating_Then_RejectsWithoutChangingObjects()
        {
            var model = new GameObject("교정 불가 모델");
            try
            {
                object[] args = { model.AddComponent<Animator>(), Vector3.one, Vector3.one };
                Assert.That(Capture(args), Is.False);
                Assert.That((Vector3)args[1], Is.EqualTo(Vector3.zero));
                Assert.That((Vector3)args[2], Is.EqualTo(Vector3.zero));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(model);
            }
        }

        private static bool Capture(object[] args)
        {
            Type type = typeof(FBXVmdPipeline).Assembly.GetType("Fbx2Vmd.FBXImporter.EditorHumanoidLegBendCalibration");
            Assert.That(type, Is.Not.Null, "원본 상태를 보존하는 Avatar 굽힘 교정 필요");
            return (bool)type.GetMethod("TryCapture", BindingFlags.Static | BindingFlags.NonPublic).Invoke(null, args);
        }
    }
}
