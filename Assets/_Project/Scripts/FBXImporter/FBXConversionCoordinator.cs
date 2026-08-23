using System;
using System.Threading.Tasks;
using UnityEngine;

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

        internal static bool TryResolveTargetAnimator(
            GameObject targetObject,
            out Animator targetAnimator,
            out string errorMessage)
        {
            targetAnimator = null;
            if (targetObject == null)
            {
                errorMessage = "Target Character가 지정되어 있지 않습니다.";
                return false;
            }

            targetAnimator = targetObject.GetComponent<Animator>();
            if (targetAnimator == null ||
                targetAnimator.avatar == null ||
                !targetAnimator.avatar.isValid ||
                !targetAnimator.avatar.isHuman)
            {
                targetAnimator = null;
                errorMessage = "Target Character에 유효한 Humanoid Avatar가 없습니다.";
                return false;
            }

            errorMessage = string.Empty;
            return true;
        }

        internal HumanoidArmDirectionRetargetGuard ConfigureArmDirectionGuard(
            GameObject targetObject,
            Animator targetAnimator,
            Animator ghostAnimator)
        {
            if (targetObject == null)
            {
                return null;
            }

            HumanoidArmDirectionRetargetGuard directionGuard =
                targetObject.GetComponent<HumanoidArmDirectionRetargetGuard>();
            if (!_pipeline.enableYybArmDirectionRetargetCorrection)
            {
                if (directionGuard != null)
                {
                    directionGuard.DisableCorrection();
                    directionGuard.enabled = false;
                }

                return null;
            }

            if (directionGuard == null)
            {
                directionGuard = targetObject.AddComponent<HumanoidArmDirectionRetargetGuard>();
            }

            directionGuard.enableDirectionRetarget = true;
            directionGuard.enabled = true;
            bool configured = directionGuard.Configure(
                ghostAnimator,
                targetAnimator,
                _pipeline.YybArmDirectionUpperArmWeight,
                _pipeline.YybArmDirectionForearmWeight,
                _pipeline.YybArmDirectionUpperArmMaxDegrees,
                _pipeline.YybArmDirectionForearmMaxDegrees,
                _pipeline.YybArmDirectionLeftSideWeightScale,
                _pipeline.YybArmDirectionRightSideWeightScale,
                _pipeline.logYybArmDirectionRetargetCorrection);

            if (!configured)
            {
                directionGuard.enabled = false;
                return null;
            }

            return directionGuard;
        }

        internal HumanoidArmSwingLimitGuard ConfigureArmSwingLimitGuard(
            GameObject targetObject,
            Animator targetAnimator)
        {
            if (targetObject == null)
            {
                return null;
            }

            HumanoidArmSwingLimitGuard swingLimitGuard =
                targetObject.GetComponent<HumanoidArmSwingLimitGuard>();
            if (!_pipeline.enableYybArmSwingLimitCorrection)
            {
                if (swingLimitGuard != null)
                {
                    swingLimitGuard.enableSwingLimit = false;
                    swingLimitGuard.enabled = false;
                }

                return null;
            }

            if (swingLimitGuard == null)
            {
                swingLimitGuard = targetObject.AddComponent<HumanoidArmSwingLimitGuard>();
            }

            swingLimitGuard.Configure(
                targetAnimator,
                true,
                _pipeline.YybArmSwingLimitWeight,
                _pipeline.YybArmSwingMaxDownDot,
                _pipeline.YybArmSwingMinHandHorizontalRatio,
                _pipeline.YybArmSwingMaxHandBelowShoulderRatio,
                _pipeline.YybArmSwingHorizontalReachLimitWeight,
                _pipeline.YybArmSwingMaxHandHorizontalReachRatio,
                _pipeline.YybArmSwingHorizontalReachMaxHandBelowShoulderRatio,
                _pipeline.YybArmSwingHorizontalReachMinElbowAngleAfterApply,
                _pipeline.YybArmSwingRaisedPoseHorizontalReachLimitWeight,
                _pipeline.YybArmSwingRaisedPoseMinUpperArmDownDot,
                _pipeline.YybArmSwingRaisedPoseMaxHandBelowShoulderRatio,
                _pipeline.YybArmSwingRaisedPoseMaxHandHorizontalReachRatio,
                _pipeline.logYybArmSwingLimitCorrection);

            return swingLimitGuard;
        }

        internal HumanoidArmSleeveAnchorGuard ConfigureArmSleeveAnchorGuard(
            GameObject targetObject,
            Animator targetAnimator)
        {
            if (targetObject == null)
            {
                return null;
            }

            HumanoidArmSleeveAnchorGuard sleeveAnchorGuard =
                targetObject.GetComponent<HumanoidArmSleeveAnchorGuard>();
            if (!_pipeline.enableYybArmSleeveAnchorCorrection)
            {
                if (sleeveAnchorGuard != null)
                {
                    sleeveAnchorGuard.DisableCorrection();
                    sleeveAnchorGuard.enabled = false;
                }

                return null;
            }

            if (sleeveAnchorGuard == null)
            {
                sleeveAnchorGuard = targetObject.AddComponent<HumanoidArmSleeveAnchorGuard>();
            }

            sleeveAnchorGuard.enableSleeveAnchor = true;
            sleeveAnchorGuard.enabled = true;
            bool configured = sleeveAnchorGuard.Configure(
                targetAnimator,
                _pipeline.YybArmSleeveAnchorInfluence,
                _pipeline.YybArmShoulderCapAnchorInfluence,
                _pipeline.YybArmSleeveAnchorMaxDegrees,
                _pipeline.logYybArmSleeveAnchorCorrection);

            if (!configured)
            {
                sleeveAnchorGuard.enabled = false;
                return null;
            }

            return sleeveAnchorGuard;
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
    }
}
