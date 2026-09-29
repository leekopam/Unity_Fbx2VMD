using System;
using System.IO;
using System.Text;
using UnityEngine;

namespace Fbx2Vmd.Profiling
{
    /// <summary>
    /// 런 기록의 스테이지 타임라인을 Chrome Trace Event 포맷(trace.json)으로 보냅니다.
    /// Perfetto UI나 chrome://tracing에서 열 수 있는 사실상 교환 표준입니다.
    /// 스테이지는 Complete("X") 이벤트, 런 결과는 Instant("i") 이벤트로 기록됩니다.
    /// metrics는 시각 정보가 없는 집계값이라 이벤트로 변환하지 않습니다.
    /// </summary>
    public static class ProfilingTraceExporter
    {
        /// <summary>런 기록을 run-&lt;runId&gt;(-&lt;label&gt;).trace.json으로 저장하고 경로를 반환합니다.</summary>
        public static string WriteTrace(ProfilingRunRecord record)
        {
            if (record == null)
            {
                return string.Empty;
            }

            Directory.CreateDirectory(ProfilingReportWriter.OutputDirectory);
            // runId는 아티팩트 JSON에서 온 값이라 파일명 규약으로 정제한다(경로 이탈 방지).
            string fileName =
                $"run-{ProfilingReportWriter.SanitizeFileName(record.runId)}-{ProfilingReportWriter.SanitizeFileName(record.label)}.trace.json";
            string path = Path.Combine(ProfilingReportWriter.OutputDirectory, fileName);
            File.WriteAllText(path, BuildTraceJson(record));
            return path;
        }

        /// <summary>런 기록을 Chrome Trace Event JSON 문자열로 변환합니다(순수 함수, 테스트 대상).</summary>
        public static string BuildTraceJson(ProfilingRunRecord record)
        {
            var sb = new StringBuilder(2048);
            sb.Append("{\"displayTimeUnit\":\"ms\",\"traceEvents\":[");

            bool first = true;
            if (record?.stages != null)
            {
                foreach (ProfilingStageSample stage in record.stages)
                {
                    if (stage == null)
                    {
                        continue;
                    }

                    // 스테이지 시작 시각 = 누적 시각 - 구간 소요. ts/dur는 µs가 포맷 규격이다.
                    long startUs = (long)((stage.sinceRunStartMs - stage.deltaMs) * 1000f);
                    long durationUs = (long)(stage.deltaMs * 1000f);
                    AppendEvent(sb, ref first);
                    sb.Append("{\"name\":\"").Append(EscapeJson(stage.stage)).Append("\",");
                    sb.Append("\"cat\":\"stage\",\"ph\":\"X\",\"ts\":").Append(startUs)
                        .Append(",\"dur\":").Append(durationUs).Append(",\"pid\":1,\"tid\":1,");
                    sb.Append("\"args\":{\"message\":\"").Append(EscapeJson(stage.message)).Append("\"}}");
                }
            }

            if (record != null && !string.IsNullOrEmpty(record.outcome))
            {
                long endUs = (long)(record.totalMs * 1000f);
                AppendEvent(sb, ref first);
                sb.Append("{\"name\":\"outcome: ").Append(EscapeJson(record.outcome)).Append("\",");
                sb.Append("\"cat\":\"run\",\"ph\":\"i\",\"s\":\"p\",\"ts\":").Append(endUs)
                    .Append(",\"pid\":1,\"tid\":1}");
            }

            sb.Append("]}");
            return sb.ToString();
        }

        private static void AppendEvent(StringBuilder sb, ref bool first)
        {
            if (!first)
            {
                sb.Append(',');
            }
            first = false;
        }

        private static string EscapeJson(string text)
        {
            if (string.IsNullOrEmpty(text))
            {
                return string.Empty;
            }

            var sb = new StringBuilder(text.Length + 8);
            foreach (char c in text)
            {
                switch (c)
                {
                    case '"': sb.Append("\\\""); break;
                    case '\\': sb.Append("\\\\"); break;
                    case '\n': sb.Append("\\n"); break;
                    case '\r': sb.Append("\\r"); break;
                    case '\t': sb.Append("\\t"); break;
                    default:
                        if (c < ' ')
                        {
                            sb.Append("\\u").Append(((int)c).ToString("x4"));
                        }
                        else
                        {
                            sb.Append(c);
                        }
                        break;
                }
            }
            return sb.ToString();
        }
    }
}
