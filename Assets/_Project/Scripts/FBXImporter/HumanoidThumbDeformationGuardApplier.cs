using UnityEngine;

namespace Fbx2Vmd.FBXImporter
{
    internal sealed class HumanoidThumbDeformationGuardOptions
    {
        internal float ProximalMaxLocalAngle { get; set; }
        internal float IntermediateMaxLocalAngle { get; set; }
        internal float DistalMaxLocalAngle { get; set; }
        internal Vector3 ProximalRotationOffset { get; set; }
        internal bool MirrorRightProximalRotationOffset { get; set; }
        internal Vector3 LeftProximalRotationOffset { get; set; }
        internal Vector3 RightProximalRotationOffset { get; set; }
        internal bool LogCorrections { get; set; }
        internal bool ClampHumanoidThumbRotations { get; set; }
        internal bool SyncDetachedBaseHelpers { get; set; }
        internal bool SyncDetachedBaseHelperPositions { get; set; }
        internal float DetachedBaseHelperSyncWeight { get; set; }
        internal float DetachedBaseHelperMaxLocalAngle { get; set; }
        internal float DetachedBaseHelperMaxPositionOffset { get; set; }
        internal Vector3 LeftDetachedBaseHelperDeltaAxisOffset { get; set; }
        internal Vector3 RightDetachedBaseHelperDeltaAxisOffset { get; set; }
        internal Vector3 LeftDetachedBaseHelperTargetRotationOffset { get; set; }
        internal Vector3 RightDetachedBaseHelperTargetRotationOffset { get; set; }
        internal bool StabilizeDetachedBasePalm { get; set; }
        internal float DetachedBasePalmStabilizeWeight { get; set; }
        internal float DetachedBasePalmMaxLocalAngle { get; set; }
        internal bool EnableVisualLengthGuard { get; set; }
        internal float ProjectionMinPalmNormal { get; set; }
        internal float ProjectionMaxPalmNormal { get; set; }
        internal float ProjectionGuardWeight { get; set; }
        internal float IndexMaxSpreadAngle { get; set; }
        internal float IndexSpreadGuardWeight { get; set; }
        internal float MaxSegmentBendAngle { get; set; }
        internal float SegmentStraightenWeight { get; set; }
        internal bool SuppressPoseShapingWithManualReference { get; set; }
        internal bool StabilizeWebbingCrease { get; set; }
        internal float WebbingCreaseStabilizeWeight { get; set; }
        internal float WebbingCreaseMaxLocalAngle { get; set; }
        internal float WebbingCreaseMaxPositionOffset { get; set; }

        internal bool ShouldApply =>
            ClampHumanoidThumbRotations ||
            SyncDetachedBaseHelpers ||
            StabilizeDetachedBasePalm ||
            StabilizeWebbingCrease;
    }

    /// <summary>
    /// 대상 모델의 엄지 변형 Guard 컴포넌트 수명주기와 설정 적용을 담당함.
    /// </summary>
    internal static class HumanoidThumbDeformationGuardApplier
    {
        internal static bool Apply(
            GameObject targetObject,
            Animator targetAnimator,
            PoseSpaceRetargeter linkedRetargeter,
            HumanoidThumbDeformationGuardOptions options)
        {
            if (targetObject == null || targetAnimator == null || options == null)
            {
                return false;
            }

            // 현재 세션 retargeter 생성 이후 Guard를 연결함.
            // 배치 smoke에서 이전 Ghost가 남을 수 있어 자동 재탐색을 피함.
            HumanoidThumbDeformationGuard thumbGuard =
                targetObject.GetComponent<HumanoidThumbDeformationGuard>();
            if (!options.ShouldApply)
            {
                if (thumbGuard != null)
                {
                    thumbGuard.enabled = false;
                }

                return false;
            }

            if (thumbGuard == null)
            {
                thumbGuard = targetObject.AddComponent<HumanoidThumbDeformationGuard>();
            }

            thumbGuard.Configure(
                targetAnimator,
                linkedRetargeter,
                options.ProximalMaxLocalAngle,
                options.IntermediateMaxLocalAngle,
                options.DistalMaxLocalAngle,
                options.ProximalRotationOffset,
                options.MirrorRightProximalRotationOffset,
                options.LeftProximalRotationOffset,
                options.RightProximalRotationOffset,
                options.LogCorrections,
                options.ClampHumanoidThumbRotations,
                options.SyncDetachedBaseHelpers,
                options.SyncDetachedBaseHelperPositions,
                options.DetachedBaseHelperSyncWeight,
                options.DetachedBaseHelperMaxLocalAngle,
                options.DetachedBaseHelperMaxPositionOffset,
                options.LeftDetachedBaseHelperDeltaAxisOffset,
                options.RightDetachedBaseHelperDeltaAxisOffset,
                options.LeftDetachedBaseHelperTargetRotationOffset,
                options.RightDetachedBaseHelperTargetRotationOffset,
                options.StabilizeDetachedBasePalm,
                options.DetachedBasePalmStabilizeWeight,
                options.DetachedBasePalmMaxLocalAngle,
                options.EnableVisualLengthGuard,
                options.ProjectionMinPalmNormal,
                options.ProjectionMaxPalmNormal,
                options.ProjectionGuardWeight,
                options.IndexMaxSpreadAngle,
                options.IndexSpreadGuardWeight,
                options.MaxSegmentBendAngle,
                options.SegmentStraightenWeight,
                options.SuppressPoseShapingWithManualReference,
                options.StabilizeWebbingCrease,
                options.WebbingCreaseStabilizeWeight,
                options.WebbingCreaseMaxLocalAngle,
                options.WebbingCreaseMaxPositionOffset);
            thumbGuard.enabled = true;
            thumbGuard.RecaptureBaseline();
            return true;
        }
    }
}
