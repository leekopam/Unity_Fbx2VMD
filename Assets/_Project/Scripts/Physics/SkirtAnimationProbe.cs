using System.Collections.Generic;
using System.Linq;
using MagicaCloth2;
using UnityEngine;

namespace Fbx2Vmd.ClothPhysics
{
    /// <summary>
    /// 스커트 본이 실제 재생 애니메이션에 의해 움직이는지 판정한다.
    /// 백스톱·각도 복원 등 MC2 포즈 제약은 '애니메이션 포즈'를 기준으로 하므로,
    /// 스커트 본이 클립에서 구동되지 않으면(휴머노이드 머슬 변환 등) 제약 기준이
    /// 바인드 포즈에 고정돼 실효가 떨어진다 — 이 경우 커스텀 스키닝 폴백 대상이다.
    /// </summary>
    public static class SkirtAnimationProbe
    {
        public enum Verdict
        {
            /// <summary>재생 가능한 클립이 없거나 해석 불가</summary>
            NoClip,
            /// <summary>클립이 스커트 본 경로에 커브를 갖는다 — 네이티브 스키닝 유지</summary>
            Animated,
            /// <summary>클립은 있으나 스커트 본을 구동하지 않는다 — 커스텀 스키닝 후보</summary>
            Static,
            /// <summary>스커트 본 목록이 비어있다</summary>
            MissingBones,
        }

        /// <summary>런타임 샘플링 판정용 — 로컬 변화량 임계값 (위치 m / 회전 deg).</summary>
        public const float DefaultPosThreshold = 0.001f;
        public const float DefaultRotThreshold = 1.0f;

        /// <summary>
        /// 본의 프레임별 로컬 변화량 요약. 어떤 프레임이든 임계 초과 시 애니메이션된 것.
        /// </summary>
        public static bool IsAnimated(
            IList<Vector3> positions, IList<Quaternion> rotations,
            float posThreshold = DefaultPosThreshold, float rotThreshold = DefaultRotThreshold)
        {
            if (positions == null || positions.Count < 2)
                return false;
            for (int i = 1; i < positions.Count; i++)
            {
                if (Vector3.Distance(positions[i - 1], positions[i]) > posThreshold)
                    return true;
                if (rotations != null && i < rotations.Count &&
                    Quaternion.Angle(rotations[i - 1], rotations[i]) > rotThreshold)
                    return true;
            }
            return false;
        }

        /// <summary>
        /// Animator의 컨트롤러 클립들을 분석해 스커트 본 구동 여부를 판정한다.
        /// 클립 커브 바인딩은 에디터 API(AnimationUtility)가 필요하다.
        /// 휴머노이드 클립(humanMotion)은 비휴머노이드 본을 구동하지 않으므로 Static으로 간주한다.
        /// </summary>
        public static Verdict EvaluateAnimator(
            Animator animator, ICollection<Transform> skirtBones, out string detail)
        {
            detail = "";
            if (skirtBones == null || skirtBones.Count == 0)
            {
                detail = "스커트 본 없음";
                return Verdict.MissingBones;
            }
            if (animator == null)
            {
                detail = "Animator 없음";
                return Verdict.NoClip;
            }
            var controller = animator.runtimeAnimatorController;
            if (controller == null || controller.animationClips == null ||
                controller.animationClips.Length == 0)
            {
                // 런타임 플레이어(휴머노이드 머슬 재생 등)는 컨트롤러 클립을 노출하지 않는다.
                // 휴머노이드 재생이면 스커트 본은 구조적으로 구동 불가 — Static로 본다.
                if (animator.isHuman)
                {
                    detail = "컨트롤러 클립 없음 + Humanoid — 외부 머슬 재생으로 추정, 스커트 본 비구동";
                    return Verdict.Static;
                }
                detail = "재생 클립을 찾을 수 없음";
                return Verdict.NoClip;
            }
            return EvaluateClips(controller.animationClips, animator.transform,
                skirtBones, out detail);
        }

        /// <summary>클립 목록이 스커트 본 경로를 하나라도 구동하는지 판정한다.</summary>
        public static Verdict EvaluateClips(
            IEnumerable<AnimationClip> clips, Transform characterRoot,
            ICollection<Transform> skirtBones, out string detail)
        {
            detail = "";
            if (skirtBones == null || skirtBones.Count == 0)
            {
                detail = "스커트 본 없음";
                return Verdict.MissingBones;
            }
            var clipList = clips?.Where(c => c != null).ToList();
            if (clipList == null || clipList.Count == 0)
            {
                detail = "클립 없음";
                return Verdict.NoClip;
            }
#if UNITY_EDITOR
            var bonePaths = new HashSet<string>(
                skirtBones.Where(b => b != null).Select(
                    b => UnityEditor.AnimationUtility.CalculateTransformPath(b, characterRoot)));
            int humanoidClips = 0, boundClips = 0;
            foreach (var clip in clipList)
            {
                if (clip.humanMotion)
                {
                    humanoidClips++;
                    continue; // 머슬 클립은 비휴머노이드 본 경로를 구동 불가
                }
                var bindings = UnityEditor.AnimationUtility.GetCurveBindings(clip);
                if (bindings.Any(b => bonePaths.Contains(b.path)))
                    boundClips++;
            }
            if (boundClips > 0)
            {
                detail = $"{boundClips}/{clipList.Count} 클립이 스커트 본을 구동";
                return Verdict.Animated;
            }
            detail = humanoidClips > 0
                ? $"휴머노이드 클립 {humanoidClips}개 — 스커트 본 비구동"
                : $"{clipList.Count}개 클립에 스커트 본 바인딩 없음";
            return Verdict.Static;
#else
            // 빌드에서는 바인딩 조회 불가 — 에디터에서의 설정 결과를 사용
            detail = "플레이어 빌드에서는 클립 분석 불가";
            return Verdict.NoClip;
#endif
        }

        /// <summary>
        /// 커스텀 스키닝 폴백을 클로스 데이터에 적용한다.
        /// 백스톱의 기준 포즈가 실제 신체(다리/허리)를 따르도록 프록시 정점을
        /// 지정 본에 자동 재스키닝한다. 해결된 본이 하나도 없으면 적용하지 않는다.
        /// </summary>
        /// <returns>등록된 본 수 (0이면 미적용)</returns>
        public static int ApplyCustomSkinning(ClothSerializeData sdata, List<Transform> bones)
        {
            var valid = bones?.Where(b => b != null).ToList();
            if (sdata == null || valid == null || valid.Count == 0)
                return 0;
            sdata.customSkinningSetting.enable = true;
            sdata.customSkinningSetting.skinningBones.Clear();
            sdata.customSkinningSetting.skinningBones.AddRange(valid);
            return valid.Count;
        }

        /// <summary>
        /// 커스텀 스키닝 후보 본 — 허리/골반/양 다리. 스커트가 따라야 할 신체다.
        /// </summary>
        public static List<Transform> ResolveSkinningBones(
            System.Func<HumanBodyBones, Transform> resolver)
        {
            var bones = new List<Transform>();
            if (resolver == null)
                return bones;
            foreach (var bb in new[]
            {
                HumanBodyBones.Hips, HumanBodyBones.Spine,
                HumanBodyBones.LeftUpperLeg, HumanBodyBones.LeftLowerLeg,
                HumanBodyBones.RightUpperLeg, HumanBodyBones.RightLowerLeg,
            })
            {
                var t = resolver(bb);
                if (t != null && !bones.Contains(t))
                    bones.Add(t);
            }
            return bones;
        }
    }
}
