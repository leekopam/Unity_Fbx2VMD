using Fbx2Vmd.FBXImporter;
using NUnit.Framework;
using System;
using System.Reflection;
using UnityEngine;

namespace Tests.Editor.FBXImporter
{
    public class ManualPoseReferenceApplierMuscleOutputTests
    {
        private static Type ManualPoseReferenceApplierType =>
            typeof(PoseSpaceRetargeter).Assembly.GetType(
                "Fbx2Vmd.FBXImporter.ManualPoseReferenceApplier",
                throwOnError: true);

        private static readonly Type[] BoundedMuscleOutputReferenceParameterTypes =
        {
            typeof(float),
            typeof(float),
            typeof(float),
            typeof(float),
            typeof(float)
        };

        private static readonly Type[] ApplyBoundedMuscleOutputReferenceParameterTypes =
        {
            typeof(HumanPose).MakeByRefType(),
            typeof(HumanPose),
            typeof(int),
            typeof(float),
            typeof(float)
        };

        [Test]
        public void Given_BoundedMuscleOutputReference_When_OutputDriftsFromInput_Then_BlendsTowardInputWithinLimit()
        {
            Assert.That(
                CalculateBoundedMuscleOutputReference(
                    inputValue: 0.25f,
                    outputValue: 0.33f,
                    weight: 0f,
                    maxDelta: 0.02f,
                    fallbackValue: 0.33f),
                Is.EqualTo(0.33f).Within(0.0001f),
                "Weight zero must keep the current SetHumanPose output unchanged.");

            Assert.That(
                CalculateBoundedMuscleOutputReference(
                    inputValue: 0.25f,
                    outputValue: 0.33f,
                    weight: 1f,
                    maxDelta: 0.02f,
                    fallbackValue: 0.33f),
                Is.EqualTo(0.31f).Within(0.0001f),
                "The correction must be capped so the diagnostic cannot hard-snap a muscle value.");

            Assert.That(
                CalculateBoundedMuscleOutputReference(
                    inputValue: 0.25f,
                    outputValue: 0.33f,
                    weight: 0.5f,
                    maxDelta: 0.02f,
                    fallbackValue: 0.33f),
                Is.EqualTo(0.32f).Within(0.0001f),
                "Partial weight should apply a bounded fraction of the output-to-input correction.");
        }

        [Test]
        public void Given_NonFiniteMuscleValues_When_CalculatingBoundedOutputReference_Then_PreservesFallbackPolicy()
        {
            Assert.That(
                CalculateBoundedMuscleOutputReference(
                    inputValue: 0.25f,
                    outputValue: float.NaN,
                    weight: 1f,
                    maxDelta: 0.02f,
                    fallbackValue: 0.12f),
                Is.EqualTo(0.12f));
            Assert.That(
                CalculateBoundedMuscleOutputReference(
                    inputValue: 0.25f,
                    outputValue: float.NaN,
                    weight: 1f,
                    maxDelta: 0.02f,
                    fallbackValue: float.NaN),
                Is.NaN);
            Assert.That(
                CalculateBoundedMuscleOutputReference(
                    inputValue: float.PositiveInfinity,
                    outputValue: 0.33f,
                    weight: 1f,
                    maxDelta: 0.02f,
                    fallbackValue: 0.12f),
                Is.EqualTo(0.33f));
        }

        [Test]
        public void Given_ValidMuscleIndex_When_ApplyingBoundedReference_Then_UpdatesOnlySelectedMuscle()
        {
            HumanPose outputPose = new HumanPose { muscles = new[] { 0.1f, 0.33f } };
            HumanPose inputPose = new HumanPose { muscles = new[] { 0.4f, 0.25f } };

            bool changed = TryApplyBoundedMuscleOutputReference(
                ref outputPose,
                inputPose,
                muscleIndex: 1,
                weight: 1f,
                maxDelta: 0.02f);

            Assert.That(changed, Is.True);
            Assert.That(outputPose.muscles[0], Is.EqualTo(0.1f).Within(0.0001f));
            Assert.That(outputPose.muscles[1], Is.EqualTo(0.31f).Within(0.0001f));
        }

        [Test]
        public void Given_InvalidOrUnchangedMuscle_When_ApplyingBoundedReference_Then_PreservesOutput()
        {
            HumanPose outputPose = new HumanPose { muscles = new[] { 0.33f } };
            HumanPose inputPose = new HumanPose { muscles = new[] { 0.25f } };

            Assert.That(
                TryApplyBoundedMuscleOutputReference(ref outputPose, inputPose, -1, 1f, 0.02f),
                Is.False);
            Assert.That(outputPose.muscles[0], Is.EqualTo(0.33f).Within(0.0001f));

            inputPose.muscles[0] = 0.33f;
            Assert.That(
                TryApplyBoundedMuscleOutputReference(ref outputPose, inputPose, 0, 1f, 0.02f),
                Is.False);
            Assert.That(outputPose.muscles[0], Is.EqualTo(0.33f).Within(0.0001f));
        }

        [Test]
        public void Given_BoundedMuscleOutputCalculation_When_CheckingOwnership_Then_UsesDedicatedApplier()
        {
            Assert.That(
                ManualPoseReferenceApplierType.GetMethod(
                    "CalculateBoundedMuscleOutputReference",
                    BindingFlags.Static | BindingFlags.NonPublic,
                    binder: null,
                    types: BoundedMuscleOutputReferenceParameterTypes,
                    modifiers: null),
                Is.Not.Null);
            Assert.That(
                FindManualPoseReferenceApplierMethod("TryApplyBoundedMuscleOutputReference"),
                Is.Not.Null);
            Assert.That(
                typeof(PoseSpaceRetargeter).GetMember(
                    "ApplyBoundedSetHumanPoseRightLegTwistOutput",
                    BindingFlags.Static | BindingFlags.NonPublic),
                Is.Empty);
        }

        private static float CalculateBoundedMuscleOutputReference(
            float inputValue,
            float outputValue,
            float weight,
            float maxDelta,
            float fallbackValue)
        {
            MethodInfo method = ManualPoseReferenceApplierType.GetMethod(
                "CalculateBoundedMuscleOutputReference",
                BindingFlags.Static | BindingFlags.NonPublic,
                binder: null,
                types: BoundedMuscleOutputReferenceParameterTypes,
                modifiers: null);

            Assert.That(method, Is.Not.Null,
                "ManualPoseReferenceApplier should own bounded muscle output reference calculation.");
            return (float)method.Invoke(null, new object[]
            {
                inputValue,
                outputValue,
                weight,
                maxDelta,
                fallbackValue
            });
        }

        private static bool TryApplyBoundedMuscleOutputReference(
            ref HumanPose outputPose,
            HumanPose inputPose,
            int muscleIndex,
            float weight,
            float maxDelta)
        {
            MethodInfo method = FindManualPoseReferenceApplierMethod("TryApplyBoundedMuscleOutputReference");
            Assert.That(method, Is.Not.Null);

            object[] arguments = { outputPose, inputPose, muscleIndex, weight, maxDelta };
            bool changed = (bool)method.Invoke(null, arguments);
            outputPose = (HumanPose)arguments[0];
            return changed;
        }

        private static MethodInfo FindManualPoseReferenceApplierMethod(string methodName)
        {
            return ManualPoseReferenceApplierType.GetMethod(
                methodName,
                BindingFlags.Static | BindingFlags.NonPublic,
                binder: null,
                types: ApplyBoundedMuscleOutputReferenceParameterTypes,
                modifiers: null);
        }
    }
}
