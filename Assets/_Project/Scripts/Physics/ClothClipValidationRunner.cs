using System.Collections;
using System.Collections.Generic;
using System.Linq;
using MagicaCloth2;
using UnityEngine;
using UnityEngine.Animations;
using UnityEngine.Playables;

namespace Fbx2Vmd.ClothPhysics
{
    /// <summary>
    /// 클립 단위 클로스 QA 배치 러너.
    /// 각 AnimationClip을 PlayableGraph로 재생하며 PhysicsValidationProbe로
    /// 침투/걸림을 샘플링하고, 클립당 골든 캡처(사람 판독용 PNG)와
    /// JSON 리포트를 Docs/Workflow/Local 아래에 남긴다.
    /// 대표 동작군(아이들/걷기/회전/팔 휘두름/굽힘/점프/고속회전/탐색)을
    /// clips 목록에 넣고 플레이 모드에서 ContextMenu로 실행한다.
    /// </summary>
    public class ClothClipValidationRunner : MonoBehaviour
    {
        [Header("대상")]
        [Tooltip("비워두면 같은 오브젝트의 CharacterPhysicsSetup 사용")]
        public CharacterPhysicsSetup setup;
        [Tooltip("비워두면 같은 오브젝트의 PhysicsValidationProbe 사용")]
        public PhysicsValidationProbe probe;

        [Header("배치")]
        public List<AnimationClip> clips = new List<AnimationClip>();
        [Tooltip("클립당 샘플링 시간(초). 클립 길이보다 짧으면 그 길이만큼")]
        [Min(1f)] public float secondsPerClip = 8f;
        [Tooltip("클립 시작 직후 정착 유예 시간(초) — 시크 직후 급변 구간은 측정에서 제외")]
        [Range(0f, 2f)] public float settleSeconds = 0.5f;

        [Header("산출물")]
        [Tooltip("클립당 골든 캡처(마지막 프레임) 저장")]
        public bool captureGolden = true;
        [Tooltip("골든 캡처 저장 폴더 (프로젝트 루트 기준 상대 경로)")]
        public string goldenDir = "Docs/Workflow/Local/golden";
        [Tooltip("JSON 리포트 출력 경로 (프로젝트 루트 기준)")]
        public string reportPath = "Docs/Workflow/Local/runtime/cloth_clip_validation.json";

        [System.NonSerialized] public string lastReport = "";
        [System.NonSerialized] public bool running;
        /// <summary>현재 측정 중인 클립명 — 중단 로그의 진단용</summary>
        [System.NonSerialized] public string currentClip;

        [System.Serializable]
        public class ClipResult
        {
            public string clip;
            public int sampledFrames;
            public float worstExcessPenetrationMm;
            public int warningFrames;
            public int suspectedStuckBones;
            public float baselineWorstMm;
            public int validCloths;
            public float avgFrameMs;
            public string golden;
        }
        [System.Serializable]
        public class Report { public string character; public List<ClipResult> clips = new List<ClipResult>(); }

        [ContextMenu("클립 배치 검증 실행")]
        public void RunAll()
        {
            if (!Application.isPlaying)
            {
                Debug.LogWarning("[ClothClipValidationRunner] 플레이 모드에서만 실행 가능");
                return;
            }
            if (!running)
                StartCoroutine(RunGuarded());
        }

        /// <summary>
        /// Unity는 코루틴이 예외로 죽을 때 Dispose/finally를 실행하지 않는다.
        /// 반복자를 수동 구동해 예외를 잡고, Dispose로 Run 내부의 finally(그래프 정리)를
        /// 강제 실행한다 — running 고정과 PlayableGraph 누수 방지.
        /// </summary>
        IEnumerator RunGuarded()
        {
            running = true;
            var report = new Report { character = name };
            var it = Run(report);
            while (true)
            {
                object step;
                try
                {
                    if (!it.MoveNext())
                        break;
                    step = it.Current;
                }
                catch (System.Exception e)
                {
                    lastReport = $"배치 중단 (클립 '{currentClip}'): {e.Message}";
                    Debug.LogError("[ClothClipValidationRunner] " + lastReport, this);
                    break;
                }
                yield return step;
            }
            (it as System.IDisposable)?.Dispose();
            running = false;
            if (report.clips.Count > 0)
                WriteReport(report);
        }

        IEnumerator Run(Report report)
        {
            if (setup == null) setup = GetComponent<CharacterPhysicsSetup>();
            if (probe == null) probe = GetComponent<PhysicsValidationProbe>();
            var animator = setup != null ? setup.targetAnimator : null;
            if (animator == null && setup != null)
                animator = setup.GetComponentInChildren<Animator>(true);

            foreach (var clip in clips.Where(c => c != null))
            {
                currentClip = clip.name;
                var result = new ClipResult { clip = clip.name };
                PlayableGraph graph = default;
                try
                {
                    if (animator != null)
                    {
                        graph = PlayableGraph.Create("clothqa_" + clip.name);
                        var playable = AnimationClipPlayable.Create(graph, clip);
                        var output = AnimationPlayableOutput.Create(graph, "anim", animator);
                        output.SetSourcePlayable(playable);
                        graph.Play();
                    }

                    // 시크 직후 급변 구간은 제외하고 프로브를 재무장한다
                    setup?.ResetPhysics(true);
                    probe?.RebuildTracking();
                    probe?.ResetStats();
                    yield return new WaitForSeconds(settleSeconds);
                    probe?.RebuildTracking();
                    probe?.ResetStats();

                    float watch = Mathf.Min(secondsPerClip, Mathf.Max(clip.length, 0.5f));
                    float t0 = Time.time;
                    int frames = 0;
                    while (Time.time - t0 < watch)
                    {
                        frames++;
                        yield return null;
                    }
                    result.avgFrameMs = frames > 0 ? (Time.time - t0) * 1000f / frames : 0f;
                    if (probe != null)
                    {
                        result.sampledFrames = probe.sampledFrames;
                        result.worstExcessPenetrationMm = probe.worstPenetration * 1000f;
                        result.warningFrames = probe.penetrationFrameCount;
                        result.suspectedStuckBones = probe.suspectedStuckBoneCount;
                        result.baselineWorstMm = probe.baselineWorstPenetration * 1000f;
                    }
                    if (setup != null)
                        result.validCloths = setup.generatedCloths.Count(c => c != null && c.IsValid());

                    if (captureGolden)
                    {
                        string dir = System.IO.Path.GetFullPath(goldenDir);
                        System.IO.Directory.CreateDirectory(dir);
                        string file = System.IO.Path.Combine(dir, $"{name}_{clip.name}.png");
                        UnityEngine.ScreenCapture.CaptureScreenshot(file);
                        result.golden = file;
                        yield return null; // 캡처 완료 프레임
                    }
                }
                finally
                {
                    if (graph.IsValid())
                        graph.Destroy();
                }
                report.clips.Add(result);
            }
            currentClip = null;
        }

        void WriteReport(Report report)
        {
            try
            {
                string path = System.IO.Path.GetFullPath(reportPath);
                System.IO.Directory.CreateDirectory(System.IO.Path.GetDirectoryName(path));
                System.IO.File.WriteAllText(path, JsonUtility.ToJson(report, true));
                var sb = new System.Text.StringBuilder($"[ClothClipValidationRunner] {report.clips.Count}개 클립 완료 → {reportPath}");
                foreach (var c in report.clips)
                    sb.AppendLine($"{c.clip}: 초과침투 {c.worstExcessPenetrationMm:F1}mm, " +
                        $"경고 {c.warningFrames}프레임, 걸림 {c.suspectedStuckBones}, " +
                        $"유효클로스 {c.validCloths}, {c.avgFrameMs:F1}ms");
                lastReport = sb.ToString();
                Debug.Log(lastReport, this);
            }
            catch (System.Exception e)
            {
                lastReport = "리포트 기록 실패: " + e.Message;
                Debug.LogWarning(lastReport, this);
            }
        }
    }
}
