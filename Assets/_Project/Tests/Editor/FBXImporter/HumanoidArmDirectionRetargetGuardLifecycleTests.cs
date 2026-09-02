using System;
using System.Reflection;
using Fbx2Vmd.FBXImporter;
using NUnit.Framework;
using UnityEngine;

namespace Tests.Editor.FBXImporter
{
    public class HumanoidArmDirectionRetargetGuardLifecycleTests
    {
        [Test]
        public void Given_ArmDirectionGuardGhostAnimatorDestroyed_When_LateUpdateRuns_Then_DisablesWithoutMissingReference()
        {
            var ghostObject = new GameObject("destroyed ghost animator");
            var targetObject = new GameObject("target arm direction guard");
            try
            {
                var ghostAnimator = ghostObject.AddComponent<Animator>();
                var targetAnimator = targetObject.AddComponent<Animator>();
                var guard = targetObject.AddComponent<HumanoidArmDirectionRetargetGuard>();
                guard.enableDirectionRetarget = true;
                guard.enabled = true;

                SetField(guard, "_ghostAnimator", ghostAnimator);
                SetField(guard, "_targetAnimator", targetAnimator);
                SetField(guard, "_configured", true);
                AddArmDirectionRetargetSegment(
                    guard,
                    HumanBodyBones.LeftUpperArm,
                    HumanBodyBones.LeftLowerArm);

                UnityEngine.Object.DestroyImmediate(ghostObject);

                Assert.DoesNotThrow(() => InvokeInstance(guard, "LateUpdate"));
                Assert.That(guard.enabled, Is.False);
                Assert.That(GetField<bool>(guard, "_configured"), Is.False);
            }
            finally
            {
                if (targetObject != null)
                {
                    UnityEngine.Object.DestroyImmediate(targetObject);
                }

                if (ghostObject != null)
                {
                    UnityEngine.Object.DestroyImmediate(ghostObject);
                }
            }
        }

        private static T GetField<T>(object instance, string fieldName)
        {
            Assert.That(instance, Is.Not.Null);
            FieldInfo field = instance.GetType().GetField(
                fieldName,
                BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            if (field != null)
            {
                return (T)field.GetValue(instance);
            }

            PropertyInfo property = instance.GetType().GetProperty(
                fieldName,
                BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            Assert.That(property, Is.Not.Null, $"Expected field or property '{fieldName}' to exist.");
            return (T)property.GetValue(instance);
        }

        private static void SetField<T>(object instance, string fieldName, T value)
        {
            Assert.That(instance, Is.Not.Null);
            FieldInfo field = instance.GetType().GetField(
                fieldName,
                BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            if (field != null)
            {
                field.SetValue(instance, value);
                return;
            }

            PropertyInfo property = instance.GetType().GetProperty(
                fieldName,
                BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            Assert.That(property, Is.Not.Null, $"Expected field or property '{fieldName}' to exist.");
            property.SetValue(instance, value);
        }

        private static void AddArmDirectionRetargetSegment(
            HumanoidArmDirectionRetargetGuard guard,
            HumanBodyBones sourceBone,
            HumanBodyBones endBone)
        {
            Assert.That(guard, Is.Not.Null);
            FieldInfo segmentsField = typeof(HumanoidArmDirectionRetargetGuard).GetField(
                "_segments",
                BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.That(segmentsField, Is.Not.Null, "Arm direction guard must keep testable segment state.");

            Type segmentType = typeof(HumanoidArmDirectionRetargetGuard).GetNestedType(
                "SegmentMapping",
                BindingFlags.NonPublic);
            Assert.That(segmentType, Is.Not.Null, "Arm direction guard segment mapping must remain available.");

            object segment = Activator.CreateInstance(
                segmentType,
                sourceBone,
                endBone,
                Quaternion.identity,
                1f,
                30f);
            Assert.That(segment, Is.Not.Null);

            var segments = (System.Collections.IList)segmentsField.GetValue(guard);
            segments.Add(segment);
        }

        private static void InvokeInstance(object instance, string methodName)
        {
            Assert.That(instance, Is.Not.Null);
            MethodInfo method = instance.GetType().GetMethod(
                methodName,
                BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            Assert.That(method, Is.Not.Null, $"Expected instance method '{methodName}' to exist.");

            method.Invoke(instance, null);
        }
    }
}
