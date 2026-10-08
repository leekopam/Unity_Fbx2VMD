using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering.PostProcessing;

namespace Fbx2Vmd.EditorTools
{
    /// <summary>
    /// 무대 공연용 PostProcessProfile(Bloom + 색보정)을 에셋으로 만든다.
    /// 이미 에셋이 있으면 덮지 않는다 — 룩 튜닝은 에셋 편집으로 한다.
    /// </summary>
    public static class ToonStageLookBuilder
    {
        const string ProfileFileName = "ToonStageProfile.asset";

        public static void Build(string presetFolder)
        {
            string path = $"{presetFolder}/{ProfileFileName}";
            if (AssetDatabase.LoadAssetAtPath<PostProcessProfile>(path) != null)
            {
                Debug.Log($"[Toon] 기존 무대 프로필 유지: {path}");
                return;
            }

            var profile = ScriptableObject.CreateInstance<PostProcessProfile>();
            AssetDatabase.CreateAsset(profile, path);

            // AddSettings가 만든 설정 객체는 에셋에 등록해야 직렬화된다.
            // 발광·림라이트 하이라이트가 무대 조명처럼 퍼지도록 블룸.
            Bloom bloom = profile.AddSettings<Bloom>();
            AssetDatabase.AddObjectToAsset(bloom, profile);
            bloom.enabled.value = true;
            bloom.enabled.overrideState = true;
            bloom.intensity.overrideState = true;
            bloom.intensity.value = 2.5f;
            bloom.threshold.overrideState = true;
            bloom.threshold.value = 1.0f;
            bloom.softKnee.overrideState = true;
            bloom.softKnee.value = 0.6f;
            bloom.diffusion.overrideState = true;
            bloom.diffusion.value = 6f;

            // 채도·콘트라스트를 살짝 올려 아이돌 무대 특유의 선명함 확보.
            ColorGrading grading = profile.AddSettings<ColorGrading>();
            AssetDatabase.AddObjectToAsset(grading, profile);
            grading.enabled.value = true;
            grading.enabled.overrideState = true;
            grading.gradingMode.overrideState = true;
            grading.gradingMode.value = GradingMode.HighDefinitionRange;
            grading.saturation.overrideState = true;
            grading.saturation.value = 12f;
            grading.contrast.overrideState = true;
            grading.contrast.value = 8f;

            AssetDatabase.SaveAssets();
            Debug.Log($"[Toon] 무대 프로필 생성: {path}");
        }
    }
}
