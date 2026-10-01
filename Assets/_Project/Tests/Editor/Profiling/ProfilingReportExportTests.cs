using System.Collections.Generic;
using System.IO;
using Fbx2Vmd.Profiling;
using NUnit.Framework;

namespace Fbx2Vmd.Tests.Editor.Profiling
{
    /// <summary>
    /// Phase 4·5 산출물(HTML 인덱스, Chrome Trace JSON)의 렌더링 계약을 검증합니다.
    /// </summary>
    public class ProfilingReportExportTests
    {
        private static ProfilingRunRecord MakeRecord()
        {
            var record = new ProfilingRunRecord
            {
                runId = "20260929-120000-abc123",
                label = "satisfaction_2.fbx",
                startedUtc = "2026-09-29T12:00:00.0000000Z",
                endedUtc = "2026-09-29T12:00:05.0000000Z",
                unityVersion = "2022.3.62f3",
                deviceModel = "TestRig",
                outcome = "Success",
                totalMs = 4000f,
                totalGcAllocBytes = 2048,
                sampledFrameCount = 100,
                sampledFrameSumMs = 1600f,
                sampledFrameMaxMs = 80f,
            };
            record.stages.Add(new ProfilingStageSample
            {
                stage = "LoadingFbx", message = "FBX 로드 중",
                sinceRunStartMs = 2600f, deltaMs = 2600f, gcDeltaBytes = 1024,
            });
            record.stages.Add(new ProfilingStageSample
            {
                stage = "Ready", message = "준비 완료",
                sinceRunStartMs = 3000f, deltaMs = 400f, gcDeltaBytes = 0,
            });
            record.metrics.Add(new ProfilingMetricSample
            {
                name = "HumanoidAvatarBuilder.SetupHumanoid",
                calls = 2, totalMs = 320f, maxMs = 300f, totalGcAllocBytes = 4096,
            });
            record.analysis.Add(new ProfilingFinding
            {
                kind = "Bottleneck", severity = "Warning", stage = "LoadingFbx",
                detail = "병목 후보: LoadingFbx 2600ms",
                suggestedScopes = new List<string> { "FBXImportController.ExtractPrimaryClip" },
            });
            return record;
        }

        [Test]
        public void Html_ContainsRunStageMetricAndFinding()
        {
            string html = ProfilingHtmlReportWriter.BuildHtml(new[] { MakeRecord() });

            StringAssert.Contains("satisfaction_2.fbx", html);
            StringAssert.Contains("Success", html);
            StringAssert.Contains("LoadingFbx", html);
            StringAssert.Contains("HumanoidAvatarBuilder.SetupHumanoid", html);
            StringAssert.Contains("병목 후보", html);
            StringAssert.Contains("TestRig", html);
        }

        [Test]
        public void Html_EscapesInjectedMarkup()
        {
            ProfilingRunRecord record = MakeRecord();
            record.label = "<script>alert(1)</script>";
            record.stages[0].message = "<img onerror=x>";

            string html = ProfilingHtmlReportWriter.BuildHtml(new[] { record });

            Assert.IsFalse(html.Contains("<script>alert"), "런 라벨의 HTML이 이스케이프되지 않았습니다.");
            StringAssert.Contains("&lt;script&gt;", html);
            Assert.IsFalse(html.Contains("<img onerror"), "스테이지 메시지의 HTML이 이스케이프되지 않았습니다.");
        }

        [Test]
        public void Html_LegacyStageCalls_DisplayedAsOne()
        {
            // 구 스키마 리포트는 calls=0으로 역직렬화 — 스테이지는 최소 1회 호출이므로 1로 표시한다.
            var record = new ProfilingRunRecord
            {
                runId = "20260929-120000-legacy",
                label = "legacy.fbx",
                outcome = "Success",
            };
            record.stages.Add(new ProfilingStageSample { stage = "Old", deltaMs = 1f });

            string html = ProfilingHtmlReportWriter.BuildHtml(new[] { record });

            StringAssert.Contains("<td>1</td>", html);
            Assert.IsFalse(html.Contains("<td>0</td>"),
                "구 스키마 스테이지의 호출 수가 0으로 표시되었습니다.");
        }

        [Test]
        public void Trace_EmitsStageCompleteEvents_WithMicrosecondTimings()
        {
            string json = ProfilingTraceExporter.BuildTraceJson(MakeRecord());

            StringAssert.Contains("\"traceEvents\"", json);
            StringAssert.Contains("\"displayTimeUnit\":\"ms\"", json);
            // 스테이지 2개 → Complete 이벤트 2개 + 결과 Instant 이벤트 1개
            Assert.AreEqual(2, CountOccurrences(json, "\"ph\":\"X\""));
            Assert.AreEqual(1, CountOccurrences(json, "\"ph\":\"i\""));
            // 첫 스테이지: ts=0, dur=2600ms→2600000µs
            StringAssert.Contains("\"ts\":0,\"dur\":2600000", json);
            // 둘째 스테이지: ts=2600ms→2600000µs, dur=400ms→400000µs
            StringAssert.Contains("\"ts\":2600000,\"dur\":400000", json);
            StringAssert.Contains("outcome: Success", json);
        }

        [Test]
        public void Trace_EscapesJsonSpecialChars()
        {
            ProfilingRunRecord record = MakeRecord();
            record.stages[0].stage = "Stage\"Q\"";
            record.stages[0].message = "줄바꿈\n포함";

            string json = ProfilingTraceExporter.BuildTraceJson(record);

            StringAssert.Contains("Stage\\\"Q\\\"", json);
            StringAssert.Contains("줄바꿈\\n포함", json);
            // 이스케이프되지 않은 따옴표가 남으면 JSON이 깨진다.
            Assert.IsFalse(json.Contains("\"Stage\"Q\"\""));
        }

        [Test]
        public void WriteIndexPage_WritesHtmlFile()
        {
            string path = ProfilingHtmlReportWriter.WriteIndexPage();
            Assert.IsTrue(File.Exists(path), $"index.html이 생성되지 않았습니다: {path}");
            StringAssert.EndsWith(ProfilingHtmlReportWriter.IndexFileName, path);
            StringAssert.Contains("<html", File.ReadAllText(path));
        }

        [Test]
        public void ListReports_ExcludesTraceAndJevSidecars()
        {
            string dir = ProfilingReportWriter.OutputDirectory;
            Directory.CreateDirectory(dir);
            string report = Path.Combine(dir, "run-sidecartest-000000-run.json");
            string trace = Path.Combine(dir, "run-sidecartest-000000-run.trace.json");
            string jev = Path.Combine(dir, "run-sidecartest-000000-run.jev.json");
            File.WriteAllText(report, "{}");
            File.WriteAllText(trace, "{}");
            File.WriteAllText(jev, "{}");
            try
            {
                string[] reports = ProfilingReportWriter.ListReports();
                CollectionAssert.Contains(reports, report);
                CollectionAssert.DoesNotContain(reports, trace, "trace 산출물이 런 목록에 섞였습니다.");
                CollectionAssert.DoesNotContain(reports, jev, "jev 산출물이 런 목록에 섞였습니다.");
            }
            finally
            {
                File.Delete(report);
                File.Delete(trace);
                File.Delete(jev);
            }
        }

        [Test]
        public void WriteTrace_SanitizesRunIdInFileName()
        {
            ProfilingRunRecord record = MakeRecord();
            record.runId = "../../escape";

            string path = ProfilingTraceExporter.WriteTrace(record);
            try
            {
                Assert.IsTrue(File.Exists(path), $"trace 파일이 생성되지 않았습니다: {path}");
                Assert.AreEqual(ProfilingReportWriter.OutputDirectory, Path.GetDirectoryName(path),
                    "runId가 출력 디렉터리를 벗어났습니다.");
            }
            finally
            {
                if (File.Exists(path))
                {
                    File.Delete(path);
                }
            }
        }

        private static int CountOccurrences(string haystack, string needle)
        {
            int count = 0;
            int index = 0;
            while ((index = haystack.IndexOf(needle, index, System.StringComparison.Ordinal)) >= 0)
            {
                count++;
                index += needle.Length;
            }
            return count;
        }
    }
}
