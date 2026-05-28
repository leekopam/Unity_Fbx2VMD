using Member_Han.Modules.FBXImporter;
using NUnit.Framework;
using System;
using System.Reflection;

namespace Tests.Editor.FBXImporter
{
    public class PoseSpaceRetargeterGroundingContactTests
    {
        private static readonly Type[] GroundingContactParameterTypes =
        {
            typeof(float),
            typeof(bool),
            typeof(float),
            typeof(bool),
            typeof(float),
            typeof(bool).MakeByRefType()
        };

        private static readonly Type[] PrimaryGroundingContactParameterTypes =
        {
            typeof(float),
            typeof(bool),
            typeof(bool),
            typeof(float),
            typeof(bool),
            typeof(float),
            typeof(bool).MakeByRefType()
        };

        [Test]
        public void Given_NoRendererBounds_When_ResolvingGroundingContact_Then_UsesFootBottom()
        {
            float contactBottomY = ResolveGroundingContactBottomY(
                lowestFootBottomY: 0.12f,
                hasRendererBounds: false,
                rendererMinY: -0.4f,
                rejectRendererGroundingOutliers: true,
                maxRendererFootGroundingSeparation: 0.08f,
                out bool rendererGroundingOutlier);

            Assert.That(contactBottomY, Is.EqualTo(0.12f).Within(0.0001f));
            Assert.That(rendererGroundingOutlier, Is.False);
        }

        [Test]
        public void Given_OutlierRejectionDisabled_When_ResolvingGroundingContact_Then_UsesRendererBounds()
        {
            float contactBottomY = ResolveGroundingContactBottomY(
                lowestFootBottomY: 0.12f,
                hasRendererBounds: true,
                rendererMinY: -0.4f,
                rejectRendererGroundingOutliers: false,
                maxRendererFootGroundingSeparation: 0.08f,
                out bool rendererGroundingOutlier);

            Assert.That(contactBottomY, Is.EqualTo(-0.4f).Within(0.0001f));
            Assert.That(rendererGroundingOutlier, Is.False);
        }

        [Test]
        public void Given_RendererWithinSeparationLimit_When_ResolvingGroundingContact_Then_UsesRendererBounds()
        {
            float contactBottomY = ResolveGroundingContactBottomY(
                lowestFootBottomY: 0.12f,
                hasRendererBounds: true,
                rendererMinY: 0.07f,
                rejectRendererGroundingOutliers: true,
                maxRendererFootGroundingSeparation: 0.08f,
                out bool rendererGroundingOutlier);

            Assert.That(contactBottomY, Is.EqualTo(0.07f).Within(0.0001f));
            Assert.That(rendererGroundingOutlier, Is.False);
        }

        [Test]
        public void Given_RendererOutsideSeparationLimit_When_ResolvingGroundingContact_Then_UsesFootBottomAndReportsOutlier()
        {
            float contactBottomY = ResolveGroundingContactBottomY(
                lowestFootBottomY: 0.12f,
                hasRendererBounds: true,
                rendererMinY: -0.4f,
                rejectRendererGroundingOutliers: true,
                maxRendererFootGroundingSeparation: 0.08f,
                out bool rendererGroundingOutlier);

            Assert.That(contactBottomY, Is.EqualTo(0.12f).Within(0.0001f));
            Assert.That(rendererGroundingOutlier, Is.True);
        }

        [Test]
        public void Given_EstimatedFootRadiusAndRendererWithinLimit_When_ResolvingPrimaryGroundingContact_Then_UsesRendererBounds()
        {
            float contactBottomY = ResolvePrimaryGroundingContactBottomY(
                lowestFootBottomY: 0.064f,
                hasEstimatedFootRadius: true,
                hasRendererBounds: true,
                rendererMinY: -0.009f,
                rejectRendererGroundingOutliers: true,
                maxRendererFootGroundingSeparation: 0.12f,
                out bool rendererGroundingOutlier);

            Assert.That(contactBottomY, Is.EqualTo(-0.009f).Within(0.0001f), "A calibrated foot radius must not make primary grounding ignore the visible renderer floor contact.");
            Assert.That(rendererGroundingOutlier, Is.False);
        }

        private static float ResolveGroundingContactBottomY(
            float lowestFootBottomY,
            bool hasRendererBounds,
            float rendererMinY,
            bool rejectRendererGroundingOutliers,
            float maxRendererFootGroundingSeparation,
            out bool rendererGroundingOutlier)
        {
            MethodInfo method = typeof(PoseSpaceRetargeter).GetMethod(
                "ResolveGroundingContactBottomY",
                BindingFlags.Static | BindingFlags.NonPublic,
                binder: null,
                types: GroundingContactParameterTypes,
                modifiers: null);

            Assert.That(method, Is.Not.Null, "PoseSpaceRetargeter should expose a pure static helper for grounding contact bottom selection.");

            object[] args =
            {
                lowestFootBottomY,
                hasRendererBounds,
                rendererMinY,
                rejectRendererGroundingOutliers,
                maxRendererFootGroundingSeparation,
                false
            };

            float contactBottomY = (float)method.Invoke(null, args);
            rendererGroundingOutlier = (bool)args[5];
            return contactBottomY;
        }

        private static float ResolvePrimaryGroundingContactBottomY(
            float lowestFootBottomY,
            bool hasEstimatedFootRadius,
            bool hasRendererBounds,
            float rendererMinY,
            bool rejectRendererGroundingOutliers,
            float maxRendererFootGroundingSeparation,
            out bool rendererGroundingOutlier)
        {
            MethodInfo method = typeof(PoseSpaceRetargeter).GetMethod(
                "ResolvePrimaryGroundingContactBottomY",
                BindingFlags.Static | BindingFlags.NonPublic,
                binder: null,
                types: PrimaryGroundingContactParameterTypes,
                modifiers: null);

            Assert.That(method, Is.Not.Null, "PoseSpaceRetargeter should expose a pure static helper for primary grounding contact selection.");

            object[] args =
            {
                lowestFootBottomY,
                hasEstimatedFootRadius,
                hasRendererBounds,
                rendererMinY,
                rejectRendererGroundingOutliers,
                maxRendererFootGroundingSeparation,
                false
            };

            float contactBottomY = (float)method.Invoke(null, args);
            rendererGroundingOutlier = (bool)args[6];
            return contactBottomY;
        }
    }
}
