using System;
using System.Collections.Generic;
using System.Diagnostics;
using Unity.Profiling;

namespace Fbx2Vmd.Profiling
{
    /// <summary>
    /// using 블록으로 코드 구간을 계측합니다.
    /// 실행 중인 변환 런이 있으면 지표로 기록하고, 없으면 ProfilerMarker만 방출합니다.
    /// ProfilerMarker는 릴리즈 빌드에서 컴파일 제거되므로 오버헤드가 없습니다.
    /// </summary>
    public struct PerfScope : IDisposable
    {
        private static readonly Dictionary<string, ProfilerMarker> Markers =
            new Dictionary<string, ProfilerMarker>();

        private readonly string _name;
        private readonly long _gcStartBytes;
        private Stopwatch _watch;

        private PerfScope(string name)
        {
            _name = name;
            _watch = null;
            _gcStartBytes = 0;
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            _watch = Stopwatch.StartNew();
            _gcStartBytes = GC.GetAllocatedBytesForCurrentThread();
            GetMarker(name).Begin();
#endif
        }

        /// <summary>구간 계측을 시작합니다.</summary>
        public static PerfScope Measure(string name)
        {
            return new PerfScope(name);
        }

        public void Dispose()
        {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            GetMarker(_name).End();
            _watch.Stop();
            long gcBytes = GC.GetAllocatedBytesForCurrentThread() - _gcStartBytes;
            PipelineRunProfiler.RecordMetric(_name, (float)_watch.Elapsed.TotalMilliseconds, gcBytes);
#endif
        }

        private static ProfilerMarker GetMarker(string name)
        {
            if (!Markers.TryGetValue(name, out ProfilerMarker marker))
            {
                marker = new ProfilerMarker($"Fbx2Vmd.{name}");
                Markers[name] = marker;
            }

            return marker;
        }
    }
}
