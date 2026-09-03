using System;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;

namespace Tests.Editor.FBXImporter
{
    public class TargetRendererIsolationValidatorTests
    {
        [Test]
        public void Given_EnabledRendererOutsideTarget_When_Validating_Then_ReturnsFailureWithOffenderName()
        {
            var target = new GameObject("격리 대상");
            var offender = new GameObject("외부 Renderer");
            target.AddComponent<SkinnedMeshRenderer>();
            offender.AddComponent<SkinnedMeshRenderer>();

            try
            {
                Renderer[] renderers =
                {
                    target.GetComponent<Renderer>(),
                    offender.GetComponent<Renderer>()
                };

                bool isValid = InvokeValidator(target, renderers, out string failureMessage);

                Assert.That(isValid, Is.False);
                Assert.That(failureMessage, Does.Contain(offender.name));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(target);
                UnityEngine.Object.DestroyImmediate(offender);
            }
        }

        [Test]
        public void Given_OnlyTargetRendererEnabled_When_Validating_Then_ReturnsSuccess()
        {
            var target = new GameObject("격리 대상");
            var disabledOutside = new GameObject("비활성 외부 Renderer");
            target.AddComponent<SkinnedMeshRenderer>();
            disabledOutside.AddComponent<SkinnedMeshRenderer>();
            disabledOutside.GetComponent<Renderer>().enabled = false;

            try
            {
                Renderer[] renderers =
                {
                    target.GetComponent<Renderer>(),
                    disabledOutside.GetComponent<Renderer>()
                };

                bool isValid = InvokeValidator(target, renderers, out string failureMessage);

                Assert.That(isValid, Is.True);
                Assert.That(failureMessage, Is.Empty);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(target);
                UnityEngine.Object.DestroyImmediate(disabledOutside);
            }
        }

        private static bool InvokeValidator(
            GameObject target,
            Renderer[] renderers,
            out string failureMessage)
        {
            Type validatorType = typeof(MotionComparisonProbe).Assembly.GetType(
                "TargetRendererIsolationValidator",
                throwOnError: false);
            Assert.That(validatorType, Is.Not.Null,
                "대상 외 Renderer를 거부하는 격리 검사 타입이 필요합니다.");

            MethodInfo method = validatorType.GetMethod(
                "TryValidate",
                BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic);
            Assert.That(method, Is.Not.Null, "TryValidate 메서드가 필요합니다.");

            object[] arguments = { target, renderers, string.Empty };
            bool result = (bool)method.Invoke(null, arguments);
            failureMessage = arguments[2] as string ?? string.Empty;
            return result;
        }
    }
}
