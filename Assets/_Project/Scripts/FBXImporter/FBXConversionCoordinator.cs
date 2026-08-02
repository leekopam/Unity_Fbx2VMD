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
                return FBXConversionResult.Fail("FBX conversion request is empty.");

            if (_pipeline.IsProcessing)
                return FBXConversionResult.Fail("Already processing.");

            try
            {
                await _pipeline.ProcessFBXSessionAsync(request.SourcePath);
                return FBXConversionResult.Succeed(string.Empty);
            }
            catch (Exception e)
            {
                return FBXConversionResult.Fail(e.Message);
            }
        }

        /// <summary>
        /// 파이프라인 내부 세션 흐름에 위임하는 얇은 래퍼임.
        /// </summary>
        public Task RunSessionAsync(FBXConversionRequest request)
        {
            return _pipeline.ProcessFBXSessionAsync(request.SourcePath);
        }
    }
}
