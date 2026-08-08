using System;
using System.Threading.Tasks;

namespace Fbx2Vmd.FBXImporter
{
    /// <summary>
    /// FBX에서 VMD로 변환하는 흐름을 조정하기 위해 추출함.
    /// ConvertAsync는 ProcessFBXAsync를 대체하고 RunSessionAsync는 ProcessFBXSessionAsync를 감쌈.
    /// B-6d 리팩터링으로 분리함.
    /// </summary>
    public class FBXConversionCoordinator
    {
        private readonly FBXVmdPipeline _pipeline;

        public FBXConversionCoordinator(FBXVmdPipeline pipeline)
        {
            _pipeline = pipeline ?? throw new ArgumentNullException(nameof(pipeline));
        }

        /// <summary>
        /// 요청을 검증하고 세션 기반 흐름에 위임함.
        /// </summary>
        public async Task<FBXConversionResult> ConvertAsync(FBXConversionRequest request)
        {
            if (request == null || string.IsNullOrWhiteSpace(request.SourcePath))
                return FBXConversionResult.Fail("FBX 변환 요청이 비어 있습니다.");

            if (_pipeline.IsProcessing)
                return FBXConversionResult.Fail("이미 FBX 처리 중입니다.");

            try
            {
                FBXConversionResult result = await _pipeline.ProcessFBXSessionAsync(request.SourcePath);
                if (!result.IsSuccess)
                    return result;

                return result;
            }
            catch (Exception e)
            {
                return FBXConversionResult.Fail(e.Message);
            }
        }

        /// <summary>
        /// 파이프라인 내부 세션 흐름에 위임하는 얇은 래퍼임.
        /// </summary>
        public Task<FBXConversionResult> RunSessionAsync(FBXConversionRequest request)
        {
            return _pipeline.ProcessFBXSessionAsync(request.SourcePath);
        }

        /// <summary>
        /// 세션 상태 변경을 단일 조정자 경로로 전달함.
        /// </summary>
        internal void SetSessionState(
            FBXVmdPipeline.FBXSessionState state,
            string message,
            float progress,
            bool shouldLog = true)
        {
            _pipeline.ApplySessionState(state, message, progress, shouldLog);
        }
    }
}
