using Fbx2Vmd.FBXImporter;
using NUnit.Framework;
using System;
using System.Reflection;
using UnityEngine;

namespace Tests.Editor.FBXImporter
{
    public class ReferenceMp4FrameMetricRowTests
    {
        [Test]
        public void Given_FrameMetricRow_When_Serializing_Then_ExcludesRuntimeAnalysisFields()
        {
            Assembly runtimeAssembly = typeof(FBXVmdPipeline).Assembly;
            Type rowType = runtimeAssembly.GetType(
                "Fbx2Vmd.FBXImporter.ReferenceMp4FrameMetricRow",
                throwOnError: false);
            Assert.That(rowType, Is.Not.Null, "참조 영상 프레임 metric DTO를 runner 밖으로 분리해야 합니다.");

            object row = Activator.CreateInstance(rowType, nonPublic: true);
            rowType.GetField("seconds").SetValue(row, 1.25f);
            rowType.GetField("bboxHeightRatio").SetValue(row, 0.75f);
            rowType.GetField("upperLimbSpanRatio").SetValue(row, 0.42f);

            string json = JsonUtility.ToJson(row);

            Assert.That(json, Does.Contain("\"seconds\":1.25"));
            Assert.That(json, Does.Contain("\"bboxHeightRatio\":0.75"));
            Assert.That(json, Does.Not.Contain("upperLimbSpanRatio"));
        }
    }
}
