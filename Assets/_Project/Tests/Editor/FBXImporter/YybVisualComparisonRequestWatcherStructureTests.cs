using System.Linq;
using System.Reflection;
using Fbx2Vmd.FBXImporter;
using NUnit.Framework;

namespace Tests.Editor.FBXImporter
{
    public sealed class YybVisualComparisonRequestWatcherStructureTests
    {
        [Test]
        public void Given_SharedRuntimeValueNormalizer_When_CheckingWatcher_Then_NoDuplicateNormalizationMethodsRemain()
        {
            string[] duplicateMethodNames =
            {
                "NormalizePositiveFloat",
                "NormalizeFiniteFloat"
            };

            string[] remainingMethodNames = typeof(YybVisualComparisonRequestWatcher)
                .GetMethods(BindingFlags.NonPublic | BindingFlags.Static)
                .Where(method => duplicateMethodNames.Contains(method.Name))
                .Select(method => method.Name)
                .ToArray();

            Assert.That(
                remainingMethodNames,
                Is.Empty,
                $"공유 값 정규화기와 중복된 private 메서드가 남아 있습니다: {string.Join(", ", remainingMethodNames)}");
        }
    }
}
