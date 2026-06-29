using NUnit.Framework;
using UnityEngine;

namespace Tests.Editor.VMDRecorderSample
{
    public class VmdUnityTransformConverterTests
    {
        [Test]
        public void Given_WriterVmdPosition_When_ConvertingToUnityMeters_Then_ScaleAndAxesAreInverted()
        {
            Vector3 vmdPosition = new Vector3(-2.5f, 1.25f, 3.75f);

            Vector3 unityPosition = VmdUnityTransformConverter.ConvertVmdPositionToUnityMeters(vmdPosition);

            Assert.That(unityPosition.x, Is.EqualTo(0.2f).Within(0.0001f));
            Assert.That(unityPosition.y, Is.EqualTo(0.1f).Within(0.0001f));
            Assert.That(unityPosition.z, Is.EqualTo(-0.3f).Within(0.0001f));
        }

        [Test]
        public void Given_UnityPosition_When_RoundTrippingThroughVmdPosition_Then_PositionMatches()
        {
            Vector3 source = new Vector3(0.25f, -0.1f, 0.5f);

            Vector3 vmdPosition = VmdUnityTransformConverter.ConvertUnityMetersToVmdPosition(source);
            Vector3 roundTripped = VmdUnityTransformConverter.ConvertVmdPositionToUnityMeters(vmdPosition);

            Assert.That(Vector3.Distance(roundTripped, source), Is.LessThan(0.0001f));
        }

        [Test]
        public void Given_WriterVmdRotation_When_ConvertingToUnityRotation_Then_MixedRotationRoundTrips()
        {
            Quaternion unityRotation = Quaternion.Euler(13f, 27f, -9f);
            Quaternion writerVmdRotation = UnityHumanoidVMDRecorder.ConvertUnityRotationToVmdRotation(unityRotation);

            Quaternion converted = VmdUnityTransformConverter.ConvertVmdRotationToUnityRotation(writerVmdRotation);

            Assert.That(Quaternion.Angle(converted, unityRotation), Is.LessThan(0.001f));
        }

        [Test]
        public void Given_CenterRoutedToGroove_When_ResolvingCarrierNames_Then_WriterRoutingIsPreserved()
        {
            Assert.That(
                VmdUnityTransformConverter.ResolveParentOfAllCarrierName(
                    useCenterAsParentOfAll: true,
                    routeCenterBoneToGroove: true),
                Is.EqualTo("センター"));
            Assert.That(
                VmdUnityTransformConverter.ResolveCenterCarrierName(
                    useCenterAsParentOfAll: true,
                    routeCenterBoneToGroove: true),
                Is.EqualTo("グルーブ"));
        }

        [Test]
        public void Given_CenterNotRoutedToGroove_When_ResolvingCarrierNames_Then_DefaultNamesArePreserved()
        {
            Assert.That(
                VmdUnityTransformConverter.ResolveParentOfAllCarrierName(
                    useCenterAsParentOfAll: true,
                    routeCenterBoneToGroove: false),
                Is.EqualTo("全ての親"));
            Assert.That(
                VmdUnityTransformConverter.ResolveCenterCarrierName(
                    useCenterAsParentOfAll: true,
                    routeCenterBoneToGroove: false),
                Is.EqualTo("センター"));
        }
    }
}
