using Fbx2Vmd.FBXImporter;
using NUnit.Framework;
using UnityEngine;

namespace Tests.Editor.FBXImporter
{
    public class PoseSpaceLateVisualGroundingCorrectionLifecycleTests
    {
        [Test]
        public void Given_NullOwner_When_InitializingLateVisualGroundingCorrection_Then_DisablesComponent()
        {
            var root = new GameObject("Late Visual Grounding Null Owner Test");

            try
            {
                root.AddComponent<PoseSpaceRetargeter>();
                PoseSpaceLateVisualGroundingCorrection correction = root.AddComponent<PoseSpaceLateVisualGroundingCorrection>();

                correction.Initialize(null);

                Assert.That(correction.enabled, Is.False);
            }
            finally
            {
                Object.DestroyImmediate(root);
            }
        }

        [Test]
        public void Given_RetargeterOwner_When_InitializingLateVisualGroundingCorrection_Then_EnablesComponent()
        {
            var root = new GameObject("Late Visual Grounding Owner Test");

            try
            {
                PoseSpaceRetargeter retargeter = root.AddComponent<PoseSpaceRetargeter>();
                PoseSpaceLateVisualGroundingCorrection correction = root.AddComponent<PoseSpaceLateVisualGroundingCorrection>();

                correction.Initialize(retargeter);

                Assert.That(correction.enabled, Is.True);
            }
            finally
            {
                Object.DestroyImmediate(root);
            }
        }
    }
}
