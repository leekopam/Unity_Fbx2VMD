using Fbx2Vmd.FBXImporter;
using NUnit.Framework;
using System;
using System.Reflection;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Tests.Editor.FBXImporter
{
    public sealed class VisualComparisonManualRecorderSelectorTests
    {
        [Test]
        public void Given_InactiveManualRecorder_When_SelectingByHierarchyToken_Then_ActivatesOnlyTargetRecorder()
        {
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            GameObject referenceObject = null;
            GameObject targetObject = null;
            try
            {
                referenceObject = new GameObject("ReferenceAvatar");
                referenceObject.AddComponent<HumanoidSampleCode>();
                targetObject = new GameObject("AlternativeAvatar");
                HumanoidSampleCode targetRecorder = targetObject.AddComponent<HumanoidSampleCode>();
                targetObject.SetActive(false);

                HumanoidSampleCode selected = SelectAndActivate("AlternativeAvatar");

                Assert.That(selected, Is.SameAs(targetRecorder));
                Assert.That(targetObject.activeSelf, Is.True);
                Assert.That(targetObject.activeInHierarchy, Is.True);
                Assert.That(referenceObject.activeSelf, Is.False);
            }
            finally
            {
                if (referenceObject != null)
                {
                    UnityEngine.Object.DestroyImmediate(referenceObject);
                }
                if (targetObject != null)
                {
                    UnityEngine.Object.DestroyImmediate(targetObject);
                }
            }
        }

        private static HumanoidSampleCode SelectAndActivate(string targetNameToken)
        {
            Assembly runtimeAssembly = typeof(FBXVmdPipeline).Assembly;
            Type selectorType = runtimeAssembly.GetType(
                "Fbx2Vmd.FBXImporter.VisualComparisonManualRecorderSelector",
                throwOnError: false);
            Assert.That(selectorType, Is.Not.Null, "범용 수동 recorder 선택기가 필요합니다.");

            MethodInfo method = selectorType.GetMethod(
                "SelectAndActivate",
                BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic);
            Assert.That(method, Is.Not.Null);

            return (HumanoidSampleCode)method.Invoke(null, new object[] { targetNameToken });
        }
    }
}
