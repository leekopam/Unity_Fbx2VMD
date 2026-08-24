using Fbx2Vmd.FBXImporter;
using NUnit.Framework;
using System;
using System.Reflection;
using UnityEngine;

namespace Tests.Editor.FBXImporter
{
    public class HumanoidThumbDeformationGuardApplierTests
    {
        [Test]
        public void Given_DisabledOptions_When_ApplyingToExistingGuard_Then_DisablesWithoutAddingComponent()
        {
            ResolveApplier(out Type applierType, out Type optionsType, out MethodInfo applyMethod);
            var targetObject = new GameObject("DisabledThumbGuardTarget");
            try
            {
                Animator animator = targetObject.AddComponent<Animator>();
                HumanoidThumbDeformationGuard guard =
                    targetObject.AddComponent<HumanoidThumbDeformationGuard>();
                guard.enabled = true;
                object options = Activator.CreateInstance(optionsType, nonPublic: true);

                bool applied = (bool)applyMethod.Invoke(
                    null,
                    new[] { targetObject, animator, null, options });

                Assert.That(applied, Is.False);
                Assert.That(guard.enabled, Is.False);
                Assert.That(
                    targetObject.GetComponents<HumanoidThumbDeformationGuard>(),
                    Has.Length.EqualTo(1));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(targetObject);
            }
        }

        [Test]
        public void Given_EnabledOptions_When_Applying_Then_AddsAndEnablesGuard()
        {
            ResolveApplier(out Type applierType, out Type optionsType, out MethodInfo applyMethod);
            object options = Activator.CreateInstance(optionsType, nonPublic: true);
            optionsType.GetProperty(
                    "ClampHumanoidThumbRotations",
                    BindingFlags.Instance | BindingFlags.NonPublic)
                ?.SetValue(options, true);

            var targetObject = new GameObject("EnabledThumbGuardTarget");
            try
            {
                Animator animator = targetObject.AddComponent<Animator>();

                bool applied = (bool)applyMethod.Invoke(
                    null,
                    new[] { targetObject, animator, null, options });
                HumanoidThumbDeformationGuard guard =
                    targetObject.GetComponent<HumanoidThumbDeformationGuard>();

                Assert.That(applied, Is.True);
                Assert.That(guard, Is.Not.Null);
                Assert.That(guard.enabled, Is.True);
                Assert.That(
                    targetObject.GetComponents<HumanoidThumbDeformationGuard>(),
                    Has.Length.EqualTo(1));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(targetObject);
            }
        }

        [Test]
        public void Given_DefaultPipelineSettings_When_SnapshottingOptions_Then_PreservesEffectiveValues()
        {
            var pipelineObject = new GameObject("ThumbGuardOptionsPipeline");
            try
            {
                var pipeline = pipelineObject.AddComponent<FBXVmdPipeline>();
                MethodInfo createOptionsMethod = typeof(FBXVmdPipeline).GetMethod(
                    "CreateThumbDeformationGuardOptions",
                    BindingFlags.Instance | BindingFlags.NonPublic);

                Assert.That(createOptionsMethod, Is.Not.Null);
                object options = createOptionsMethod.Invoke(pipeline, null);
                Type optionsType = options.GetType();

                Assert.That(
                    ReadOption(optionsType, options, "ClampHumanoidThumbRotations"),
                    Is.EqualTo(pipeline.EffectiveThumbLocalRotationGuard));
                Assert.That(
                    ReadOption(optionsType, options, "ProximalMaxLocalAngle"),
                    Is.EqualTo(pipeline.EffectiveThumbProximalMaxLocalAngle));
                Assert.That(
                    ReadOption(optionsType, options, "ProjectionMinPalmNormal"),
                    Is.EqualTo(pipeline.EffectiveThumbProjectionMinPalmNormal));
                Assert.That(
                    ReadOption(optionsType, options, "SuppressPoseShapingWithManualReference"),
                    Is.EqualTo(pipeline.PreserveManualThumbPoseWithReference));
                Assert.That(ReadOption(optionsType, options, "ShouldApply"), Is.True);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(pipelineObject);
            }
        }

        private static void ResolveApplier(
            out Type applierType,
            out Type optionsType,
            out MethodInfo applyMethod)
        {
            applierType = typeof(FBXVmdPipeline).Assembly.GetType(
                "Fbx2Vmd.FBXImporter.HumanoidThumbDeformationGuardApplier",
                throwOnError: false);
            optionsType = typeof(FBXVmdPipeline).Assembly.GetType(
                "Fbx2Vmd.FBXImporter.HumanoidThumbDeformationGuardOptions",
                throwOnError: false);

            Assert.That(applierType, Is.Not.Null);
            Assert.That(optionsType, Is.Not.Null);
            applyMethod = applierType.GetMethod(
                "Apply",
                BindingFlags.Static | BindingFlags.NonPublic);
            Assert.That(applyMethod, Is.Not.Null);
        }

        private static object ReadOption(Type optionsType, object options, string propertyName)
        {
            PropertyInfo property = optionsType.GetProperty(
                propertyName,
                BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.That(property, Is.Not.Null, propertyName);
            return property.GetValue(options);
        }
    }
}
