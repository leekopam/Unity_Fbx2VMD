using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;
using UnityEngine;

namespace Fbx2Vmd.Profiling
{
    /// <summary>
    /// 누적된 런 기록을 자급족 단일 HTML(index.html)로 보냅니다.
    /// CI 아티팩트 업로드만으로 열람 가능한 공유 산출물이며 외부 리소스 의존이 없습니다.
    /// 모든 동적 문자열은 HTML 이스케이프를 거칩니다.
    /// </summary>
    public static class ProfilingHtmlReportWriter
    {
        public const string IndexFileName = "index.html";

        /// <summary>최근 런 최대 maxRuns개를 모아 index.html을 쓰고 경로를 반환합니다.</summary>
        public static string WriteIndexPage(int maxRuns = 30)
        {
            var runs = new List<ProfilingRunRecord>();
            foreach (string path in ProfilingReportWriter.ListReports())
            {
                if (runs.Count >= maxRuns)
                {
                    break;
                }

                ProfilingRunRecord record = ProfilingReportWriter.Load(path);
                if (record != null)
                {
                    runs.Add(record);
                }
            }

            Directory.CreateDirectory(ProfilingReportWriter.OutputDirectory);
            string outputPath = Path.Combine(ProfilingReportWriter.OutputDirectory, IndexFileName);
            File.WriteAllText(outputPath, BuildHtml(runs));
            return outputPath;
        }

        /// <summary>런 목록을 단일 HTML 문서 문자열로 렌더링합니다(순수 함수, 테스트 대상).</summary>
        public static string BuildHtml(IReadOnlyList<ProfilingRunRecord> runs)
        {
            var sb = new StringBuilder(8192);
            sb.Append("<!DOCTYPE html><html lang=\"ko\"><head><meta charset=\"utf-8\">");
            sb.Append("<title>FBX2VMD 프로파일링 리포트</title><style>");
            sb.Append("body{font-family:Segoe UI,Malgun Gothic,sans-serif;margin:24px;color:#222;background:#fafafa}");
            sb.Append(".run{background:#fff;border:1px solid #ddd;border-radius:6px;padding:12px 16px;margin:12px 0}");
            sb.Append(".head{font-weight:600;margin-bottom:6px}");
            sb.Append(".meta{color:#666;font-size:12px;margin-bottom:8px}");
            sb.Append(".badge{display:inline-block;padding:1px 8px;border-radius:10px;font-size:12px;color:#fff;margin-left:6px}");
            sb.Append(".ok{background:#2e7d32}.fail{background:#c62828}.other{background:#757575}");
            sb.Append("table{border-collapse:collapse;width:100%;font-size:12px;margin-top:6px}");
            sb.Append("th,td{border-bottom:1px solid #eee;text-align:left;padding:4px 8px 4px 0;vertical-align:top}");
            sb.Append(".bar{height:10px;background:#1565c0;border-radius:2px;display:inline-block;min-width:1px}");
            sb.Append(".f{padding:6px 8px;border-left:3px solid #999;background:#f5f5f5;margin:4px 0;font-size:12px}");
            sb.Append(".f.Error{border-color:#c62828}.f.Warning{border-color:#ef6c00}.f.Info{border-color:#1565c0}");
            sb.Append(".mono{font-family:Consolas,monospace;font-size:11px;color:#555}");
            sb.Append("</style></head><body>");
            sb.Append("<h1>FBX2VMD 프로파일링 리포트</h1>");
            sb.Append("<p class=\"meta\">생성: ").Append(Escape(DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss")));
            sb.Append(" | 런 수: ").Append(runs == null ? 0 : runs.Count).Append("</p>");

            if (runs != null)
            {
                foreach (ProfilingRunRecord run in runs)
                {
                    if (run != null)
                    {
                        AppendRun(sb, run);
                    }
                }
            }

            sb.Append("</body></html>");
            return sb.ToString();
        }

        private static void AppendRun(StringBuilder sb, ProfilingRunRecord run)
        {
            string outcomeClass = run.outcome == "Success" ? "ok"
                : run.outcome == "Failed" || run.outcome == "Cancelled" ? "fail" : "other";
            sb.Append("<div class=\"run\"><div class=\"head\">");
            sb.Append(Escape(string.IsNullOrEmpty(run.label) ? "(이름 없음)" : run.label));
            sb.Append("<span class=\"badge ").Append(outcomeClass).Append("\">")
                .Append(Escape(run.outcome ?? string.Empty)).Append("</span>");
            sb.Append(" <span class=\"mono\">").Append(F(run.totalMs)).Append("ms</span></div>");
            sb.Append("<div class=\"meta\">");
            sb.Append(Escape(run.startedUtc ?? string.Empty)).Append(" | Unity ").Append(Escape(run.unityVersion ?? string.Empty));
            sb.Append(" | ").Append(Escape(run.deviceModel ?? string.Empty));
            if (!string.IsNullOrEmpty(run.revision))
            {
                sb.Append(" | rev=").Append(Escape(run.revision));
            }
            if (run.sampledFrameCount > 0)
            {
                sb.Append(" | 프레임 ").Append(run.sampledFrameCount).Append("개, 평균 ")
                    .Append(F(run.sampledFrameSumMs / run.sampledFrameCount)).Append("ms, 최대 ")
                    .Append(F(run.sampledFrameMaxMs)).Append("ms");
                if (run.frameP50Ms > 0f)
                {
                    sb.Append(", p50 ").Append(F(run.frameP50Ms)).Append("/p95 ")
                        .Append(F(run.frameP95Ms)).Append("/p99 ").Append(F(run.frameP99Ms)).Append("ms");
                }
            }
            if (run.gcReservedBytes > 0 || run.systemUsedBytes > 0)
            {
                sb.Append(" | 메모리 피크 GC ").Append(F(run.gcReservedBytes / (1024f * 1024f)))
                    .Append("MB / System ").Append(F(run.systemUsedBytes / (1024f * 1024f))).Append("MB");
            }
            // 미기록(-1) 필드는 표시하지 않는다.
            if (run.inputBytes >= 0)
            {
                sb.Append(" | 입력 ").Append(F(run.inputBytes / (1024f * 1024f))).Append("MB");
            }
            if (run.clipLengthSec >= 0f)
            {
                sb.Append(" | 클립 ").Append(F(run.clipLengthSec)).Append("초");
            }
            if (run.boneCount >= 0)
            {
                sb.Append(" | 본 ").Append(run.boneCount).Append("개");
            }
            sb.Append("</div>");

            AppendFindings(sb, run);
            AppendStageTable(sb, run);
            AppendMetricTable(sb, run);
            sb.Append("</div>");
        }

        private static void AppendFindings(StringBuilder sb, ProfilingRunRecord run)
        {
            if (run.analysis == null || run.analysis.Count == 0)
            {
                return;
            }

            foreach (ProfilingFinding finding in run.analysis)
            {
                sb.Append("<div class=\"f ").Append(Escape(finding.severity ?? "Info")).Append("\">");
                sb.Append("<b>").Append(Escape(finding.kind ?? string.Empty)).Append("</b> ");
                sb.Append(Escape(finding.detail ?? string.Empty));
                if (finding.suggestedScopes != null && finding.suggestedScopes.Count > 0)
                {
                    sb.Append("<br><span class=\"mono\">계측 심화 후보: ")
                        .Append(Escape(string.Join(", ", finding.suggestedScopes))).Append("</span>");
                }
                sb.Append("</div>");
            }
        }

        private static void AppendStageTable(StringBuilder sb, ProfilingRunRecord run)
        {
            // 구 버전 리포트의 프레임 단위 하트비트도 표로 읽기 쉽게 스테이지명별로 합산한다.
            List<ProfilingStageSample> stages =
                ProfilingRunAnalyzer.AggregateStagesByName(run.stages);
            if (stages.Count == 0)
            {
                return;
            }

            float maxDelta = 0f;
            foreach (ProfilingStageSample s in stages)
            {
                maxDelta = Mathf.Max(maxDelta, s.deltaMs);
            }

            sb.Append("<table><tr><th>스테이지</th><th>Δms</th><th>호출</th><th>누적ms</th><th>ΔGC(KB)</th><th>비중</th><th>메시지</th></tr>");
            foreach (ProfilingStageSample s in stages)
            {
                int width = maxDelta > 0f ? Mathf.RoundToInt(s.deltaMs / maxDelta * 200f) : 0;
                sb.Append("<tr><td>").Append(Escape(s.stage ?? string.Empty)).Append("</td><td>")
                    // 구 스키마(calls 필드 없음)는 0으로 역직렬화 — 분석기와 동일하게 1회로 간주해 표시한다.
                    .Append(F(s.deltaMs)).Append("</td><td>").Append(Mathf.Max(1, s.calls)).Append("</td><td>")
                    .Append(F(s.sinceRunStartMs)).Append("</td><td>")
                    .Append(F(s.gcDeltaBytes / 1024f)).Append("</td><td>")
                    .Append("<span class=\"bar\" style=\"width:").Append(width).Append("px\"></span></td><td>")
                    .Append(Escape(s.message ?? string.Empty)).Append("</td></tr>");
            }
            sb.Append("</table>");
        }

        private static void AppendMetricTable(StringBuilder sb, ProfilingRunRecord run)
        {
            if (run.metrics == null || run.metrics.Count == 0)
            {
                return;
            }

            sb.Append("<table><tr><th>계측 지표</th><th>호출</th><th>합계ms</th><th>최대ms</th><th>GC(KB)</th></tr>");
            foreach (ProfilingMetricSample m in run.metrics)
            {
                sb.Append("<tr><td class=\"mono\">").Append(Escape(m.name ?? string.Empty)).Append("</td><td>")
                    .Append(m.calls).Append("</td><td>").Append(F(m.totalMs)).Append("</td><td>")
                    .Append(F(m.maxMs)).Append("</td><td>").Append(F(m.totalGcAllocBytes / 1024f))
                    .Append("</td></tr>");
            }
            sb.Append("</table>");
        }

        private static string F(float value)
        {
            return value.ToString("F1", CultureInfo.InvariantCulture);
        }

        private static string Escape(string text)
        {
            if (string.IsNullOrEmpty(text))
            {
                return string.Empty;
            }

            return text
                .Replace("&", "&amp;")
                .Replace("<", "&lt;")
                .Replace(">", "&gt;")
                .Replace("\"", "&quot;")
                .Replace("'", "&#39;");
        }
    }
}
