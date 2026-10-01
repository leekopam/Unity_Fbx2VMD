using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using Fbx2Vmd.Profiling;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.TestTools;

namespace Fbx2Vmd.Tests.Editor.Profiling
{
    /// <summary>
    /// 프로파일링 리포트 창의 데이터 경로(파일 열거 → 선택 로드 → 차트 빌드 → 범위 집계 →
    /// JEV 사이드카 → 필터 → 재분석)를 EditMode에서 끝까지 검증합니다.
    /// 창은 Assembly-CSharp-Editor 소속이므로 기존 테스트 관례대로 리플렉션으로 접근합니다.
    /// OnGUI가 필요한 렌더 구간만 대화형 에디터 조건([UnityTest]+isBatchMode 게이트)으로 분리합니다.
    /// </summary>
    public class ProfilingReportWindowE2ETests
    {
        private const string WindowTypeName =
            "Fbx2Vmd.Profiling.EditorTools.ProfilingReportWindow, Assembly-CSharp-Editor";
        private const string RunBaseName = "run-20990101-000000-e2etest-x";
        private const int SampleCount = 200;

        private string _reportPath;
        private string _jevPath;

        [SetUp]
        public void SetUp()
        {
            Directory.CreateDirectory(ProfilingReportWriter.OutputDirectory);
            _reportPath = Path.Combine(ProfilingReportWriter.OutputDirectory, RunBaseName + ".json");
            _jevPath = Path.Combine(ProfilingReportWriter.OutputDirectory, RunBaseName + ".jev.json");

            // 프레임 대부분은 정상, 일부를 스파이크(기본 임계 33ms 초과)로 만들어 차트 색상을 검증한다.
            var record = new ProfilingRunRecord
            {
                runId = "e2etest-000000",
                label = "e2e-fixture.fbx",
                outcome = "Success",
                startedUtc = "2099-01-01T00:00:00Z",
                totalMs = 4200f,
                sampledFrameCount = SampleCount,
                frameSamplesMs = BuildSamples(),
            };
            record.stages.Add(new ProfilingStageSample { stage = "LoadingFbx", deltaMs = 200f, calls = 1 });
            record.stages.Add(new ProfilingStageSample { stage = "Retargeting", deltaMs = 3500f, calls = 1 });
            record.stages.Add(new ProfilingStageSample { stage = "WriteVmd", deltaMs = 500f, calls = 1 });
            File.WriteAllText(_reportPath, JsonUtility.ToJson(record, true));
            File.WriteAllText(_jevPath,
                "{\"status\":\"ok\",\"model\":\"e2e\",\"entries\":[{\"kind\":\"bottleneck\",\"stage\":\"Retargeting\"," +
                "\"detail\":\"d\",\"cause\":{\"choice\":\"c\",\"label\":\"l\",\"confidence\":0.9}," +
                "\"deepen\":null,\"error\":null}]}");
        }

        [TearDown]
        public void TearDown()
        {
            if (File.Exists(_reportPath)) File.Delete(_reportPath);
            if (File.Exists(_jevPath)) File.Delete(_jevPath);
        }

        private static float[] BuildSamples()
        {
            var samples = new float[SampleCount];
            for (int i = 0; i < samples.Length; i++)
            {
                samples[i] = 5f;
            }
            // 버킷 최대값 렌더링이 스파이크를 보존하는지 보기 위해 드문드문 배치한다.
            samples[40] = 120f;
            samples[150] = 90f;
            return samples;
        }

        private static Type WindowType =>
            Type.GetType(WindowTypeName);

        private static FieldInfo F(Type t, string name) =>
            t.GetField(name, BindingFlags.Instance | BindingFlags.NonPublic);

        private static MethodInfo M(Type t, string name) =>
            t.GetMethod(name, BindingFlags.Instance | BindingFlags.NonPublic);

        /// <summary>
        /// GUI 없이 호출 가능한 데이터 경계를 한 흐름으로 검증한다.
        /// (배치 모드 포함 — Texture2D.SetPixels는 GPU 없이도 동작한다.)
        /// </summary>
        [Test]
        public void Window_DataPipeline_LoadChartStatsJevFilterReanalyze()
        {
            Type t = WindowType;
            Assert.IsNotNull(t, "ProfilingReportWindow 타입을 찾지 못했습니다.");
            var window = ScriptableObject.CreateInstance(t);
            try
            {
                // 목록 열거: 합성 리포트가 잡히고 사이드카는 걸러져야 한다.
                string[] reports = ProfilingReportWriter.ListReports();
                int index = Array.FindIndex(reports,
                    p => string.Equals(p, _reportPath, StringComparison.OrdinalIgnoreCase));
                Assert.GreaterOrEqual(index, 0, "합성 리포트가 런 목록에 없습니다.");
                CollectionAssert.DoesNotContain(reports, _jevPath, "jev 사이드카가 런 목록에 섞였습니다.");

                F(t, "_reportFiles").SetValue(window, reports);
                F(t, "_leftIndex").SetValue(window, index);
                F(t, "_thresholds").SetValue(window, ProfilingAnalysisThresholds.Load());

                // 선택 → 디스크 로드 → 레코드 캐시
                var record = (ProfilingRunRecord)M(t, "LoadAt").Invoke(window, new object[] { index });
                Assert.IsNotNull(record);
                Assert.AreEqual("e2e-fixture.fbx", record.label);
                Assert.AreEqual(SampleCount, record.frameSamplesMs.Length);

                // 차트 빌드: 폭=min(샘플수,1024), 배경과 다른 픽셀이 존재해야 한다.
                var tex = (Texture2D)M(t, "BuildChartTexture").Invoke(window, new object[] { record });
                try
                {
                    Assert.IsNotNull(tex);
                    Assert.AreEqual(Mathf.Min(SampleCount, 1024), tex.width);
                    var px = tex.GetPixels();
                    int nonBg = 0, spikeLike = 0;
                    foreach (Color c in px)
                    {
                        if (Mathf.Abs(c.r - 0.13f) > 0.05f || Mathf.Abs(c.g - 0.13f) > 0.05f)
                        {
                            nonBg++;
                            // 주황 스파이크(0.95,0.45,0.25)·노란 기준선(1,0.9,0.3)은 감마 변환 후에도
                            // r이 g보다 크게 남으므로 색 공간과 무관하게 걸러낸다.
                            if (c.r > c.g + 0.1f) spikeLike++;
                        }
                    }
                    Assert.Greater(nonBg, 0, "차트에 막대가 그려지지 않았습니다.");
                    Assert.Greater(spikeLike, 0, "스파이크 표시(기준선 또는 초과 막대)가 없습니다.");
                }
                finally
                {
                    UnityEngine.Object.DestroyImmediate(tex);
                }

                // 범위 집계: 선택 구간 통계가 스파이크를 포함해 올바르게 나와야 한다.
                var stats = ProfilingRunAnalyzer.FrameStats(record.frameSamplesMs, 30, 60);
                Assert.AreEqual(30, stats.count);
                Assert.AreEqual(5f, stats.minMs, 0.01f);
                Assert.AreEqual(120f, stats.maxMs, 0.01f);
                Assert.Greater(stats.meanMs, 5f);

                // JEV 사이드카 진단 로드
                object diag = M(t, "LoadJevDiagnosis").Invoke(window, new object[] { index });
                Assert.IsNotNull(diag, "jev 사이드카 진단이 로드되지 않았습니다.");
                var entries = diag.GetType().GetField("entries").GetValue(diag) as IList;
                Assert.AreEqual(1, entries.Count);

                // 텍스트 필터
                var passes = M(t, "PassesFilters");
                F(t, "_listFilter").SetValue(window, "e2etest");
                Assert.IsTrue((bool)passes.Invoke(window, new object[] { index }));
                F(t, "_listFilter").SetValue(window, "no-such-run");
                Assert.IsFalse((bool)passes.Invoke(window, new object[] { index }));
                F(t, "_listFilter").SetValue(window, "");

                // 재분석 경로(임계값 저장 버튼과 동일한 호출)
                M(t, "ReanalyzeSelectedRun").Invoke(window, null);
                var findings = F(t, "_analysisOverride").GetValue(window) as IList;
                Assert.IsNotNull(findings, "재분석 결과가 생성되지 않았습니다.");
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(window);
            }
        }

        /// <summary>
        /// 실제 OnGUI 펌프에서 창이 리포트를 그리는지 본다.
        /// 배치 모드에는 GUI가 없으므로 대화형 에디터에서만 의미가 있다.
        /// </summary>
        [UnityTest]
        public IEnumerator Window_OnGui_RendersChartAndSurvivesFakeNull_InteractiveOnly()
        {
            if (Application.isBatchMode)
            {
                Assert.Ignore("OnGUI는 배치 모드에서 실행되지 않습니다.");
            }

            Type t = WindowType;
            Assert.IsNotNull(t);
            // 이미 열려 있는 사용자 창을 닫지 않도록 별도 인스턴스를 띄운다.
            var window = (EditorWindow)ScriptableObject.CreateInstance(t);
            window.Show();
            try
            {
                // 실제 목록과 섞이지 않게 RefreshReports로 다시 읽고 합성 리포트를 찾는다.
                M(t, "RefreshReports").Invoke(window, null);
                string[] reports = (string[])F(t, "_reportFiles").GetValue(window);
                int index = Array.FindIndex(reports,
                    p => string.Equals(p, _reportPath, StringComparison.OrdinalIgnoreCase));
                Assert.GreaterOrEqual(index, 0);

                F(t, "_leftIndex").SetValue(window, index);
                window.Repaint();
                // OnGUI는 에디터 프레임 사이에서만 실행되므로 몇 프레임 기다린다.
                for (int i = 0; i < 4; i++)
                {
                    yield return null;
                }

                Assert.AreEqual(index, (int)F(t, "_detailIndex").GetValue(window),
                    "OnGUI가 선택 런 상세 패널을 그리지 못했습니다.");
                var tex = (Texture2D)F(t, "_chartTexture").GetValue(window);
                Assert.IsTrue(tex != null && tex, "OnGUI에서 프레임 차트 텍스처가 만들어지지 않았습니다.");

                // Unity가 네이티브만 파괴한 텍스처(fake-null)를 넣으면 다음 OnGUI에서 재빌드돼야 한다.
                F(t, "_chartTexture").SetValue(window, tex); // 참조 유지
                UnityEngine.Object.DestroyImmediate(tex);
                window.Repaint();
                for (int i = 0; i < 4; i++)
                {
                    yield return null;
                }
                var rebuilt = (Texture2D)F(t, "_chartTexture").GetValue(window);
                Assert.IsTrue(rebuilt != null && rebuilt,
                    "파괴된 차트 텍스처가 OnGUI에서 재빌드되지 않았습니다.");
            }
            finally
            {
                window.Close();
            }
        }
    }
}
