using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using Fbx2Vmd.Profiling;
using NUnit.Framework;
using Unity.PerformanceTesting;
using UnityEngine;

namespace Fbx2Vmd.Tests.Editor.Profiling
{
    /// <summary>
    /// 프로파일링 계측 자체의 비용과, 대표 파이프라인 구간(FBX 파싱, 본 매핑 로드)의 성능 회귀를 감시합니다.
    /// 픽스처(Assets/_Project/Tests/PerfFixtures/test.fbx)는 추적 대상입니다.
    /// 단, Assimp 네이티브 임포트 회귀는 Windows 에디터 전용입니다(Linux CI에서는 스킵).
    /// </summary>
    public class ProfilingPerformanceTests
    {
        // gitignore 예외로 추적되는 CI 공용 픽스처(5MB MMD 샘플 모델).
        private const string TestFbxPath = "Assets/_Project/Tests/PerfFixtures/test.fbx";

        [Test]
        public void RunRecord_StageTiming_WritesJsonReport()
        {
            PipelineRunProfiler.BeginRun("editmode-selftest");
            using (PerfScope.Measure("SelfTest.Scope"))
            {
                System.Threading.Thread.Sleep(5);
            }
            PipelineRunProfiler.NoteStage("StageA", "첫 스테이지", isTerminal: false);
            PipelineRunProfiler.NoteStage("StageB", "둘째 스테이지", isTerminal: false);
            PipelineRunProfiler.NoteStage("Success", "종료", isTerminal: true);

            string path = PipelineRunProfiler.LastWrittenReportPath;
            Assert.IsTrue(File.Exists(path), $"리포트가 생성되지 않았습니다: {path}");

            ProfilingRunRecord record = ProfilingReportWriter.Load(path);
            Assert.IsNotNull(record);
            Assert.AreEqual("Success", record.outcome);
            Assert.GreaterOrEqual(record.stages.Count, 3, "스테이지 샘플이 누락되었습니다.");
            Assert.GreaterOrEqual(record.metrics.Count, 1, "PerfScope 지표가 누락되었습니다.");
            Assert.Greater(record.totalMs, 0f);
        }

        [Test]
        public void PerfScope_NoActiveRun_DoesNotRecord()
        {
            PipelineRunProfiler.AbortRun();
            using (PerfScope.Measure("NoRun.Scope")) { }
            Assert.IsFalse(PipelineRunProfiler.IsRunActive);
        }

        [Test, Performance]
        public void PerfScope_PerCall_Overhead()
        {
            PipelineRunProfiler.AbortRun();
            const int iterations = 200;
            // 워밍업: 마커 캐시와 JIT 비용을 제외한다.
            for (int i = 0; i < iterations; i++)
            {
                using (PerfScope.Measure("Bench.Warmup")) { }
            }

            var watch = Stopwatch.StartNew();
            for (int i = 0; i < iterations; i++)
            {
                using (PerfScope.Measure("Bench.Scope")) { }
            }
            watch.Stop();

            float perCallMicroseconds = (float)watch.Elapsed.TotalMilliseconds / iterations * 1000f;
            Measure.Custom(
                new SampleGroup("PerfScope.PerCall", SampleUnit.Microsecond),
                perCallMicroseconds);

            // 계측 자체가 측정 대상을 왜곡하지 않도록 절대 상한을 둡니다(베이스라인 무관, 머신 독립적 안전장치).
            Assert.Less(
                perCallMicroseconds, 100f,
                $"PerfScope 1회 호출 비용이 {perCallMicroseconds:F1}µs로 상한(100µs)을 초과했습니다.");

            Assert.IsTrue(
                PerfBaselineGuard.Check("PerfScope.PerCall", perCallMicroseconds, out string message, tolerance: 0.5f),
                message);
        }

        [Test, Performance]
        public void AssimpInspect_TestFbx_Regression()
        {
            if (!File.Exists(TestFbxPath))
            {
                Assert.Ignore("FBX 픽스처가 없습니다. Assets/_Project/Tests/PerfFixtures/test.fbx를 확인하세요.");
            }

            Type importerType = Type.GetType(
                "Fbx2Vmd.FBXImporter.AssimpFBXImporter, Assembly-CSharp");
            Assert.IsNotNull(importerType, "AssimpFBXImporter 타입을 찾지 못했습니다.");
            MethodInfo inspectMethod = importerType.GetMethod(
                "InspectAnimationFile",
                BindingFlags.Static | BindingFlags.Public);
            Assert.IsNotNull(inspectMethod, "InspectAnimationFile을 찾지 못했습니다.");

            const int runs = 5;
            // 워밍업 겸 환경 검사: assimp 네이티브 DLL이 없는 플랫폼(Linux CI 등)에서는
            // 임포트가 실패해 에러 경로 시간을 잴 뿐이므로 회귀 측정 자체를 스킵한다.
            object warmupReport = inspectMethod.Invoke(null, new object[] { TestFbxPath });
            FieldInfo importSucceeded = warmupReport?.GetType().GetField("ImportSucceeded");
            if (importSucceeded == null || !(bool)importSucceeded.GetValue(warmupReport))
            {
                object error = warmupReport?.GetType().GetField("ErrorMessage")?.GetValue(warmupReport);
                Assert.Ignore($"이 환경에서는 Assimp 임포트를 사용할 수 없습니다. {error}");
            }

            float median = MedianOf(runs, () =>
                inspectMethod.Invoke(null, new object[] { TestFbxPath }));

            Measure.Custom(new SampleGroup("Assimp.Inspect.TestFbx", SampleUnit.Millisecond), median);
            Assert.IsTrue(
                PerfBaselineGuard.Check("Assimp.Inspect.TestFbx", median, out string message),
                message);
        }

        [Test, Performance]
        public void BoneMapping_Load_Regression()
        {
            Type controllerType = Type.GetType(
                "Fbx2Vmd.FBXImporter.FBXImportController, Assembly-CSharp");
            Assert.IsNotNull(controllerType, "FBXImportController 타입을 찾지 못했습니다.");
            MethodInfo loadMethod = controllerType.GetMethod(
                "LoadBoneMappingRuntime",
                BindingFlags.Static | BindingFlags.NonPublic);
            Assert.IsNotNull(loadMethod, "LoadBoneMappingRuntime을 찾지 못했습니다.");

            loadMethod.Invoke(null, null); // 워밍업
            const int runs = 10;
            float median = MedianOf(runs, () => loadMethod.Invoke(null, null));

            Measure.Custom(new SampleGroup("BoneMapping.Load", SampleUnit.Millisecond), median);
            Assert.IsTrue(
                PerfBaselineGuard.Check("BoneMapping.Load", median, out string message),
                message);
        }

        [Test]
        public void Analyzer_Bottleneck_FlagsStageOver30Percent()
        {
            var record = new ProfilingRunRecord
            {
                outcome = "Success",
                totalMs = 4000f,
            };
            record.stages.Add(new ProfilingStageSample { stage = "LoadingFbx", deltaMs = 2600f });
            record.stages.Add(new ProfilingStageSample { stage = "Ready", deltaMs = 500f });

            var findings = ProfilingRunAnalyzer.Analyze(record, null);

            Assert.AreEqual(1, findings.Count);
            Assert.AreEqual(ProfilingRunAnalyzer.KindBottleneck, findings[0].kind);
            Assert.AreEqual("LoadingFbx", findings[0].stage);
            Assert.IsNotNull(findings[0].suggestedScopes);
            Assert.IsTrue(
                findings[0].suggestedScopes.Any(s => s.Contains("HumanoidAvatarBuilder")),
                "LoadingFbx 병목에 계측 심화 후보가 있어야 합니다.");
        }

        [Test]
        public void Analyzer_TrendRegression_NeedsHistoryAndFactor()
        {
            var history = new List<ProfilingRunRecord>();
            for (int i = 0; i < 4; i++)
            {
                var past = new ProfilingRunRecord();
                past.stages.Add(new ProfilingStageSample { stage = "Retargeting", deltaMs = 100f });
                history.Add(past);
            }

            var regressed = new ProfilingRunRecord { outcome = "Success", totalMs = 300f };
            regressed.stages.Add(new ProfilingStageSample { stage = "Retargeting", deltaMs = 200f });
            Assert.IsTrue(
                ProfilingRunAnalyzer.Analyze(regressed, history)
                    .Any(f => f.kind == ProfilingRunAnalyzer.KindTrendRegression));

            var normal = new ProfilingRunRecord { outcome = "Success", totalMs = 300f };
            normal.stages.Add(new ProfilingStageSample { stage = "Retargeting", deltaMs = 120f });
            Assert.IsFalse(
                ProfilingRunAnalyzer.Analyze(normal, history)
                    .Any(f => f.kind == ProfilingRunAnalyzer.KindTrendRegression));

            // 이력이 부족하면 회귀 판정하지 않는다.
            Assert.IsFalse(
                ProfilingRunAnalyzer.Analyze(regressed, history.Take(2).ToList())
                    .Any(f => f.kind == ProfilingRunAnalyzer.KindTrendRegression));
        }

        [Test]
        public void Analyzer_Failure_ReportsTerminalStageMessage()
        {
            var record = new ProfilingRunRecord { outcome = "Failed" };
            record.stages.Add(new ProfilingStageSample { stage = "Retargeting", message = "품질 계약 실패" });
            record.stages.Add(new ProfilingStageSample { stage = "Failed", message = "frame 2 계약 실패" });

            var findings = ProfilingRunAnalyzer.Analyze(record, null);

            Assert.AreEqual(1, findings.Count);
            Assert.AreEqual(ProfilingRunAnalyzer.KindFailure, findings[0].kind);
            Assert.AreEqual(ProfilingRunAnalyzer.SeverityError, findings[0].severity);
            StringAssert.Contains("frame 2", findings[0].detail);
        }

        [Test]
        public void Analyzer_FrameSpike_And_SuccessRun_NoFalsePositive()
        {
            var spiky = new ProfilingRunRecord
            {
                outcome = "Success",
                totalMs = 100f,
                sampledFrameCount = 100,
                sampledFrameSumMs = 1600f,   // 평균 16ms
                sampledFrameMaxMs = 80f,     // 평균 5배 스파이크
            };
            Assert.IsTrue(
                ProfilingRunAnalyzer.Analyze(spiky, null)
                    .Any(f => f.kind == ProfilingRunAnalyzer.KindFrameSpike));

            var clean = new ProfilingRunRecord { outcome = "Success", totalMs = 100f };
            Assert.AreEqual(0, ProfilingRunAnalyzer.Analyze(clean, null).Count);
        }

        [Test]
        public void NoteStage_RepeatedSameStage_CoalescesIntoOneSample()
        {
            PipelineRunProfiler.BeginRun("coalesce-selftest");
            PipelineRunProfiler.NoteStage("Selected", "선택", isTerminal: false);
            PipelineRunProfiler.NoteStage("Retargeting", "준비 1/3", isTerminal: false);
            System.Threading.Thread.Sleep(2);
            PipelineRunProfiler.NoteStage("Retargeting", "준비 2/3", isTerminal: false);
            PipelineRunProfiler.NoteStage("Retargeting", "준비 3/3", isTerminal: false);
            PipelineRunProfiler.NoteStage("Ready", "대기", isTerminal: false);
            PipelineRunProfiler.NoteStage("Success", "완료", isTerminal: true);

            ProfilingRunRecord record =
                ProfilingReportWriter.Load(PipelineRunProfiler.LastWrittenReportPath);
            Assert.IsNotNull(record);
            var retargeting = record.stages.Where(s => s.stage == "Retargeting").ToList();
            Assert.AreEqual(1, retargeting.Count, "동일 스테이지의 연속 하트비트가 병합되지 않았습니다.");
            Assert.AreEqual("준비 3/3", retargeting[0].message);
            Assert.Greater(retargeting[0].deltaMs, 0f);
            // 스키마 불변식: 누적 시각 - 소요 = 구간 시작(≥0). trace보내기가 이 불변식에 의존한다.
            Assert.GreaterOrEqual(retargeting[0].sinceRunStartMs - retargeting[0].deltaMs, 0f);
            // 다른 스테이지는 병합되지 않고 각각 남는다.
            Assert.IsTrue(record.stages.Any(s => s.stage == "Selected"));
            Assert.IsTrue(record.stages.Any(s => s.stage == "Ready"));
        }

        [Test]
        public void Analyzer_SlicedStage_AggregatedForBottleneck()
        {
            // 구 버전 리포트 호환: 프레임 단위로 쪼개진 동일 스테이지는 합산해 1건만 판정한다.
            var record = new ProfilingRunRecord { outcome = "Success", totalMs = 4000f };
            for (int i = 0; i < 20; i++)
            {
                record.stages.Add(new ProfilingStageSample { stage = "Retargeting", deltaMs = 130f });
            }
            record.stages.Add(new ProfilingStageSample { stage = "LoadingFbx", deltaMs = 300f });

            var findings = ProfilingRunAnalyzer.Analyze(record, null);

            var bottlenecks = findings.Where(f => f.kind == ProfilingRunAnalyzer.KindBottleneck).ToList();
            Assert.AreEqual(1, bottlenecks.Count, "슬라이스된 스테이지가 건수만큼 finding을 만들면 안 됩니다.");
            Assert.AreEqual("Retargeting", bottlenecks[0].stage);
        }

        [Test]
        public void Analyzer_TrendRegression_ReportsOncePerStage_WithSlicedHistory()
        {
            var history = new List<ProfilingRunRecord>();
            for (int i = 0; i < 4; i++)
            {
                var past = new ProfilingRunRecord();
                for (int k = 0; k < 10; k++)
                {
                    past.stages.Add(new ProfilingStageSample { stage = "Retargeting", deltaMs = 10f });
                }
                history.Add(past); // 런별 합계 100ms
            }

            var current = new ProfilingRunRecord { outcome = "Success", totalMs = 300f };
            for (int k = 0; k < 4; k++)
            {
                current.stages.Add(new ProfilingStageSample { stage = "Retargeting", deltaMs = 50f });
            }
            // 합계 200ms > 과거 100ms x 1.5 이며 +50ms 이상 → 회귀 1건.

            var findings = ProfilingRunAnalyzer.Analyze(current, history)
                .Where(f => f.kind == ProfilingRunAnalyzer.KindTrendRegression).ToList();
            Assert.AreEqual(1, findings.Count, "같은 스테이지 회귀는 1건으로 집계되어야 합니다.");
        }

        [Test]
        public void Analyzer_InteractiveStages_NotFlaggedAsBottleneck()
        {
            var record = new ProfilingRunRecord { outcome = "AbortedByNewRun", totalMs = 3000f };
            record.stages.Add(new ProfilingStageSample { stage = "LoadingFbx", deltaMs = 300f });
            for (int i = 0; i < 100; i++)
            {
                record.stages.Add(new ProfilingStageSample { stage = "PreviewPaused", deltaMs = 20f });
            }
            // PreviewPaused 합계 2000ms는 전체의 66%지만 사용자 대기라 병목이 아니다.

            var findings = ProfilingRunAnalyzer.Analyze(record, null);

            Assert.IsFalse(
                findings.Any(f => f.kind == ProfilingRunAnalyzer.KindBottleneck && f.stage == "PreviewPaused"),
                "사용자 대기 상태가 병목으로 잡혔습니다.");
        }

        [Test]
        public void AggregateStages_SumsDeltasKeepsOrder()
        {
            var stages = new List<ProfilingStageSample>
            {
                new ProfilingStageSample { stage = "A", deltaMs = 10f, gcDeltaBytes = 100 },
                new ProfilingStageSample { stage = "B", deltaMs = 20f, gcDeltaBytes = 50 },
                new ProfilingStageSample { stage = "A", deltaMs = 5f, gcDeltaBytes = 60 },
            };

            var aggregated = ProfilingRunAnalyzer.AggregateStagesByName(stages);

            Assert.AreEqual(2, aggregated.Count);
            Assert.AreEqual("A", aggregated[0].stage);
            Assert.AreEqual(15f, aggregated[0].deltaMs, 0.001f);
            Assert.AreEqual(160, aggregated[0].gcDeltaBytes);
            Assert.AreEqual("B", aggregated[1].stage);
        }

        private static float MedianOf(int runs, Action action)
        {
            var samples = new float[runs];
            for (int i = 0; i < runs; i++)
            {
                var watch = Stopwatch.StartNew();
                action();
                watch.Stop();
                samples[i] = (float)watch.Elapsed.TotalMilliseconds;
            }

            Array.Sort(samples);
            return samples[runs / 2];
        }
    }
}
