using System;
using System.Reflection;
using Fbx2Vmd.FBXImporter;
using NUnit.Framework;

namespace Tests.Editor.FBXImporter
{
    /// <summary>
    /// 라벨링 창이 쓰는 human-labels.csv 서식이 run-product-smoke.mjs 템플릿과
    /// HumanoidFootContactIntentLabelStore 파서의 계약을 지키는지 검증함.
    /// </summary>
    public class FootContactIntentLabelingCsvTests
    {
        private const string ExpectedHeader =
            "from_frame,to_frame,side,contact_label,motion_label,reviewer,notes";
        private const BindingFlags Flags = BindingFlags.Instance | BindingFlags.Static |
            BindingFlags.Public | BindingFlags.NonPublic;
        private static readonly Type StoreType = typeof(FBXVmdPipeline).Assembly
            .GetType("Fbx2Vmd.FBXImporter.HumanoidFootContactIntentLabelStore", true);

        [Test]
        public void Given_Header_When_Comparing_Then_MatchesValidatorContract()
        {
            // validate-human-labels.mjs는 첫 줄을 문자열 그대로 비교하므로
            // 따옴표가 없는 템플릿 서식과 바이트 단위로 같아야 함.
            Assert.That(FootContactIntentLabelingWindow.Header, Is.EqualTo(ExpectedHeader));
            Assert.That(FootContactIntentLabelingWindow.Header, Does.Not.Contain("\""));
        }

        [Test]
        public void Given_CleanRow_When_Formatting_Then_StoreParsesIt()
        {
            // smoke 템플릿과 같은 비따옴표 행이 나와야 함.
            string line = FootContactIntentLabelingWindow.FormatRow(
                1525, 1565, true, "발 전체", "구르기", "검토자", "메모");
            Assert.That(line, Is.EqualTo("1525,1565,left,발 전체,구르기,검토자,메모"));
            Assert.That(TryParseRow(line), Is.True, line);
        }

        [Test]
        public void Given_FreeTextWithCommaQuoteNewline_When_Formatting_Then_EscapesAndParses()
        {
            // 자유 서술의 쉼표·따옴표·줄바꿈이 앞쪽 열 파싱을 깨지 않아야 함.
            string line = FootContactIntentLabelingWindow.FormatRow(
                1, 2, false, "공중", "불확실", "rev,\"x\"", "a,b\nc");
            Assert.That(line,
                Is.EqualTo("1,2,right,공중,불확실,\"rev,\"\"x\"\"\",\"a,b\nc\""));
            Assert.That(TryParseRow(line), Is.True, line);
        }

        private static bool TryParseRow(string line)
        {
            object[] args = { line, null, null };
            return (bool)StoreType.GetMethod("TryParseRow", Flags).Invoke(null, args);
        }
    }
}
