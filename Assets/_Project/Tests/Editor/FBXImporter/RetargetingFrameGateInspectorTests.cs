using System.Reflection;
using Fbx2Vmd.FBXImporter;
using NUnit.Framework;

namespace Tests.Editor.FBXImporter
{
    public class RetargetingFrameGateInspectorTests
    {
        [Test]
        public void Given_RightSleeveSilhouetteOffsetFrameGate_When_ExposedInInspector_Then_Frame90IsSelectable()
        {
            AssertRangeMaxAtLeast<FBXVmdPipeline>("_yybRightSleeveSilhouetteLocalOffsetFrameGateStart", 90f);
            AssertRangeMaxAtLeast<FBXVmdPipeline>("_yybRightSleeveSilhouetteLocalOffsetFrameGateEnd", 90f);
            AssertRangeMaxAtLeast<PoseSpaceRetargeter>("_yybRightSleeveSilhouetteLocalOffsetFrameGateStart", 90f);
            AssertRangeMaxAtLeast<PoseSpaceRetargeter>("_yybRightSleeveSilhouetteLocalOffsetFrameGateEnd", 90f);
        }

        [Test]
        public void Given_PostSetHumanPoseEndpointFrameGate_When_ExposedInInspector_Then_LegacyFrameWindowIsSelectable()
        {
            const float discoveredLegacyGateEnd = 3553f;
            string[] fieldNames =
            {
                "_postSetHumanPoseRightEndpointPositionReferenceFrameGateStart",
                "_postSetHumanPoseRightEndpointPositionReferenceFrameGateEnd"
            };

            foreach (string fieldName in fieldNames)
            {
                AssertRangeMaxAtLeast<FBXVmdPipeline>(fieldName, discoveredLegacyGateEnd);
                AssertRangeMaxAtLeast<PoseSpaceRetargeter>(fieldName, discoveredLegacyGateEnd);
            }
        }

        private static void AssertRangeMaxAtLeast<T>(string fieldName, float expectedMax) where T : class
        {
            FieldInfo field = typeof(T).GetField(
                fieldName,
                BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            Assert.That(field, Is.Not.Null, $"{typeof(T).Name}.{fieldName} must exist.");

            var range = field.GetCustomAttribute<UnityEngine.RangeAttribute>();
            Assert.That(range, Is.Not.Null, $"{typeof(T).Name}.{fieldName} must expose an Inspector range.");
            Assert.That(
                range.max,
                Is.GreaterThanOrEqualTo(expectedMax),
                $"{typeof(T).Name}.{fieldName} Inspector range must include the discovered legacy frame gate {expectedMax:0}.");
        }
    }
}
