using System;
using System.Collections.Generic;
using System.Reflection;
using System.Threading.Tasks;
using Fbx2Vmd.Character;
using RootMotion;
using RootMotion.FinalIK;
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
        private readonly List<BooleanFieldSnapshot> _retargetBooleanSnapshots =
            new List<BooleanFieldSnapshot>();

        private struct BooleanFieldSnapshot
        {
            public object Target;
            public FieldInfo Field;
            public bool Value;

            public BooleanFieldSnapshot(object target, FieldInfo field, bool value)
            {
                Target = target;
                Field = field;
                Value = value;
            }
        }

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

        internal void ConfigureTargetRetargetGuards(
            GameObject targetObject,
            Animator targetAnimator,
            Animator ghostAnimator)
        {
            HumanoidArmTwistRiggingGuard twistRiggingGuard =
                ConfigureArmTwistRiggingGuard(targetObject, targetAnimator);
            ConfigureArmDirectionGuard(targetObject, targetAnimator, ghostAnimator);
            ConfigureArmSwingLimitGuard(targetObject, targetAnimator);
            HumanoidArmSleeveAnchorGuard sleeveAnchorGuard =
                ConfigureArmSleeveAnchorGuard(targetObject, targetAnimator);
            HumanoidArmVisualTwistGuard visualTwistGuard =
                ConfigureArmVisualTwistGuard(targetObject, targetAnimator);
            ConfigureArmDeformationGuard(
                targetObject,
                twistRiggingGuard,
                sleeveAnchorGuard,
                visualTwistGuard);
        }

        internal void PrepareTargetPlaybackState(
            GameObject targetObject,
            Animator targetAnimator,
            bool shouldFaceTargetToCamera)
        {
            if (targetObject == null || targetAnimator == null)
            {
                return;
            }

            targetObject.transform.position = Vector3.zero;
            if (shouldFaceTargetToCamera)
            {
                CameraFacingController.FaceTargetToCamera(targetObject, Camera.main);
            }
            else
            {
                targetObject.transform.rotation = Quaternion.identity;
            }

            targetAnimator.applyRootMotion = false;
            targetAnimator.runtimeAnimatorController = null;
        }

        internal static void RemoveLegacyIkControl(GameObject targetObject)
        {
            if (targetObject == null)
            {
                return;
            }

            IKControl ikControl = targetObject.GetComponent<IKControl>();
            if (ikControl == null)
            {
                return;
            }

            Debug.Log("[FBXImport] 자동 retarget 경로에서 IKControl을 제거합니다.");
            UnityEngine.Object.Destroy(ikControl);
        }

        internal void DisableMmdPostPoseCorrectionForRetarget(
            GameObject targetObject,
            bool shouldDisable)
        {
            if (!shouldDisable || targetObject == null)
            {
                return;
            }

            RestoreMmdPostPoseCorrectionForRetarget();

            int changed = 0;
            Component[] components = targetObject.GetComponentsInChildren<Component>(true);
            foreach (Component component in components)
            {
                if (component == null)
                {
                    continue;
                }

                Type componentType = component.GetType();
                if (!componentType.Name.Contains("MMD4Mecanim"))
                {
                    continue;
                }

                if (TrySetBooleanField(component, "pphShoulderEnabled", false))
                {
                    changed++;
                }
            }

            if (changed > 0)
            {
                Debug.Log($"[FBXImport] Retarget 중 MMD4Mecanim 어깨 PPH 보정 {changed}개를 일시 비활성화했습니다.");
            }
        }

        internal void RestoreMmdPostPoseCorrectionForRetarget()
        {
            for (int i = _retargetBooleanSnapshots.Count - 1; i >= 0; i--)
            {
                BooleanFieldSnapshot snapshot = _retargetBooleanSnapshots[i];
                if (snapshot.Target == null || snapshot.Field == null)
                {
                    continue;
                }

                snapshot.Field.SetValue(snapshot.Target, snapshot.Value);
            }

            _retargetBooleanSnapshots.Clear();
        }

        private bool TrySetBooleanField(object target, string fieldName, bool value)
        {
            FieldInfo field = FindFieldInHierarchy(target.GetType(), fieldName);
            if (field == null || field.FieldType != typeof(bool))
            {
                return false;
            }

            bool currentValue = (bool)field.GetValue(target);
            if (currentValue == value)
            {
                return false;
            }

            _retargetBooleanSnapshots.Add(new BooleanFieldSnapshot(target, field, currentValue));
            field.SetValue(target, value);
            return true;
        }

        private static FieldInfo FindFieldInHierarchy(Type type, string fieldName)
        {
            const BindingFlags flags = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
            for (Type currentType = type; currentType != null; currentType = currentType.BaseType)
            {
                FieldInfo field = currentType.GetField(fieldName, flags);
                if (field != null)
                {
                    return field;
                }
            }

            return null;
        }

        internal void ConfigureFinalIkFootGroundingExperiment(GameObject targetObject)
        {
            if (targetObject == null)
            {
                return;
            }

            GrounderBipedIK existingGrounder = targetObject.GetComponent<GrounderBipedIK>();
            BipedIK existingBipedIk = targetObject.GetComponent<BipedIK>();
            if (!_pipeline.enableFinalIkFootGroundingExperiment)
            {
                if (existingGrounder != null)
                {
                    existingGrounder.weight = 0f;
                    existingGrounder.enabled = false;
                }

                if (existingBipedIk != null)
                {
                    existingBipedIk.fixTransforms = false;
                    existingBipedIk.enabled = false;
                }

                return;
            }

            BipedIK bipedIk = existingBipedIk;
            if (bipedIk == null)
            {
                bipedIk = targetObject.AddComponent<BipedIK>();
            }

            Animator animator = targetObject.GetComponent<Animator>();
            if (animator != null && animator.isHuman)
            {
                BipedReferences references = bipedIk.references;
                BipedReferences.AutoDetectReferences(
                    ref references,
                    targetObject.transform,
                    BipedReferences.AutoDetectParams.Default);
                bipedIk.references = references;
            }

            bipedIk.SetToDefaults();
            bipedIk.fixTransforms = true;
            bipedIk.enabled = true;

            GrounderBipedIK grounder = existingGrounder;
            if (grounder == null)
            {
                grounder = targetObject.AddComponent<GrounderBipedIK>();
            }

            grounder.ik = bipedIk;
            grounder.weight = Mathf.Clamp(_pipeline.finalIkFootGroundingWeight, 0f, 0.25f);
            grounder.spineBend = 0f;
            grounder.spineSpeed = 0f;
            grounder.solver.maxStep = Mathf.Clamp(_pipeline.finalIkFootGroundingMaxStep, 0f, 0.08f);
            grounder.solver.footRadius = Mathf.Clamp(_pipeline.finalIkFootGroundingFootRadius, 0.01f, 0.2f);
            grounder.solver.prediction = Mathf.Clamp(_pipeline.finalIkFootGroundingPrediction, 0f, 0.2f);
            grounder.solver.footRotationWeight = Mathf.Clamp01(_pipeline.finalIkFootGroundingFootRotationWeight);
            grounder.solver.pelvisDamper = Mathf.Clamp01(_pipeline.finalIkFootGroundingPelvisDamper);
            grounder.enabled = grounder.weight > 0f;

            if (_pipeline.logFinalIkFootGroundingExperiment)
            {
                Debug.Log(
                    $"[FBXImport] Final IK foot grounding experiment configured: " +
                    $"weight={grounder.weight:F3}, maxStep={grounder.solver.maxStep:F3}, " +
                    $"footRadius={grounder.solver.footRadius:F3}");
            }
        }

        internal static void RecaptureTargetGuardBaselines(
            GameObject targetObject,
            bool shouldRecapture)
        {
            if (!shouldRecapture || targetObject == null)
            {
                return;
            }

            HumanoidArmDeformationGuard armGuard =
                targetObject.GetComponent<HumanoidArmDeformationGuard>();
            if (armGuard != null && armGuard.enabled)
            {
                armGuard.RecaptureBaseline();
            }

            HumanoidThumbDeformationGuard thumbGuard =
                targetObject.GetComponent<HumanoidThumbDeformationGuard>();
            if (thumbGuard != null && thumbGuard.enabled)
            {
                thumbGuard.RecaptureBaseline();
            }
        }

        internal HumanoidArmTwistRiggingGuard ConfigureArmTwistRiggingGuard(
            GameObject targetObject,
            Animator targetAnimator)
        {
            if (targetObject == null)
            {
                return null;
            }

            HumanoidArmTwistRiggingGuard twistRiggingGuard =
                targetObject.GetComponent<HumanoidArmTwistRiggingGuard>();
            if (!_pipeline.enableAnimationRiggingArmTwistCorrection)
            {
                if (twistRiggingGuard != null)
                {
                    twistRiggingGuard.DisableRigging();
                    twistRiggingGuard.enabled = false;
                }

                DisableTargetRigBuilder(targetObject);
                return null;
            }

            if (twistRiggingGuard == null)
            {
                twistRiggingGuard = targetObject.AddComponent<HumanoidArmTwistRiggingGuard>();
            }

            twistRiggingGuard.enableTwistRigging = true;
            twistRiggingGuard.enabled = true;
            bool configured = twistRiggingGuard.Configure(
                targetAnimator,
                _pipeline.AnimationRiggingArmTwistRigWeight,
                _pipeline.AnimationRiggingUpperArmTwistWeight,
                _pipeline.AnimationRiggingForearmTwistWeight,
                _pipeline.logAnimationRiggingArmTwistCorrection);

            return configured ? twistRiggingGuard : null;
        }

        private static void DisableTargetRigBuilder(GameObject targetObject)
        {
            if (targetObject == null)
            {
                return;
            }

            var rigBuilder =
                targetObject.GetComponent<UnityEngine.Animations.Rigging.RigBuilder>();
            if (rigBuilder == null)
            {
                return;
            }

            if (rigBuilder.graph.IsValid())
            {
                rigBuilder.Clear();
            }

            rigBuilder.enabled = false;
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

        internal HumanoidArmVisualTwistGuard ConfigureArmVisualTwistGuard(
            GameObject targetObject,
            Animator targetAnimator)
        {
            if (targetObject == null)
            {
                return null;
            }

            HumanoidArmVisualTwistGuard visualTwistGuard =
                targetObject.GetComponent<HumanoidArmVisualTwistGuard>();
            if (!_pipeline.enableYybArmVisualTwistCorrection)
            {
                if (visualTwistGuard != null)
                {
                    visualTwistGuard.DisableCorrection();
                    visualTwistGuard.enabled = false;
                }

                return null;
            }

            if (visualTwistGuard == null)
            {
                visualTwistGuard = targetObject.AddComponent<HumanoidArmVisualTwistGuard>();
            }

            visualTwistGuard.enableVisualTwistGuard = true;
            visualTwistGuard.enabled = true;
            bool configured = visualTwistGuard.Configure(
                targetAnimator,
                _pipeline.YybArmVisualUpperArmInfluence,
                _pipeline.YybArmVisualForearmInfluence,
                _pipeline.YybArmVisualUpperArmMaxDegrees,
                _pipeline.YybArmVisualForearmMaxDegrees,
                _pipeline.logYybArmVisualTwistCorrection);

            if (!configured)
            {
                visualTwistGuard.enabled = false;
                return null;
            }

            return visualTwistGuard;
        }

        internal void ConfigureArmDeformationGuard(
            GameObject targetObject,
            HumanoidArmTwistRiggingGuard twistRiggingGuard,
            HumanoidArmSleeveAnchorGuard sleeveAnchorGuard,
            HumanoidArmVisualTwistGuard visualTwistGuard)
        {
            if (!_pipeline.attachTargetArmDeformationGuard || targetObject == null)
            {
                return;
            }

            HumanoidArmDeformationGuard guard =
                targetObject.GetComponent<HumanoidArmDeformationGuard>();
            if (guard == null)
            {
                guard = targetObject.AddComponent<HumanoidArmDeformationGuard>();
            }

            guard.Configure(new ArmDeformationSettings(
                clampMusclesToHumanRange: false,
                enableAnatomicalArmGuard: _pipeline.targetGuardClampAnatomicalArmMuscles,
                stretchMuscleLimit: _pipeline.ArmStretchMuscleLimit,
                upperArmTwistMuscleLimit: _pipeline.UpperArmTwistMuscleLimit,
                lowerArmTwistMuscleLimit: _pipeline.LowerArmTwistMuscleLimit,
                lockHumanoidBonePositions: _pipeline.ShouldLockTargetHumanoidBonePositions,
                logCorrections: _pipeline.logArmDeformationGuardCorrections,
                clampArmStretchMuscles: _pipeline.targetGuardClampArmStretchMuscles,
                lockLimbChildLocalPositions: _pipeline.lockTargetLimbChildLocalPositions,
                lockLimbChildLocalRotations: _pipeline.lockTargetLimbChildLocalRotations));
            guard.SetLimbChildRotationExclusions(BuildLimbChildRotationExclusions(
                twistRiggingGuard?.ControlledTransforms,
                sleeveAnchorGuard?.ControlledTransforms,
                visualTwistGuard?.ControlledTransforms));
            guard.enabled = true;
            guard.RecaptureBaseline();
        }

        private static IEnumerable<Transform> BuildLimbChildRotationExclusions(
            params IEnumerable<Transform>[] controlledTransformGroups)
        {
            foreach (IEnumerable<Transform> controlledTransforms in controlledTransformGroups)
            {
                if (controlledTransforms == null)
                {
                    continue;
                }

                foreach (Transform controlledTransform in controlledTransforms)
                {
                    yield return controlledTransform;
                }
            }
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
