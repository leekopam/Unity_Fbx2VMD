using System;
using System.Collections;
using System.Linq;
using System.Text;
using Fbx2Vmd.Profiling;
using MagicaCloth2;
using Unity.Profiling;
using UnityEngine;
using UnityEngine.Profiling;

namespace Fbx2Vmd.ClothPhysics
{
    /// <summary>
    /// Phase 5 — 캐릭터 물리 자동 설정의 성능 계측 러너.
    /// 같은 씬에서 동적 구성과 Pre-build 경로를 교대로 측정해 비교한다.
    /// 측정값은 기존 프로파일링 파이프라인(PipelineRunProfiler → run JSON)에 기록되고,
    /// MC2 내부 ProfilerMarker(InitCloth, StartClothUpdate.*)는 ProfilerRecorder로 읽는다.
    ///
    /// 빌드 검증 절차: 이 컴포넌트를 씬에 두고 Development Build로 빌드한 뒤
    /// -fbx2vmd-physperf 인자로 실행하면 자동 계측 후
    /// Application.persistentDataPath/Profiling 아래 run-*.json에 기록된다.
    /// (ProfilerRecorder는 Editor/Development Build에서만 유효)
    /// </summary>
    public class PhysicsPerfRunner : MonoBehaviour
    {
        public const string AutoRunCommandLineArg = "-fbx2vmd-physperf";

        [Tooltip("비워두면 같은 오브젝트의 CharacterPhysicsSetup 사용")]
        public CharacterPhysicsSetup setup;

        [Tooltip("측정 중 재생할 검증 시퀀스 (선택)")]
        public PhysicsValidationSequence validationSequence;

        [Tooltip("프로파일링 리포트 라벨")]
        public string label = "physics-setup";

        [Tooltip("워밍업 프레임 수")]
        [Range(10, 300)]
        public int warmupFrames = 30;

        [Tooltip("본 계측 프레임 수")]
        [Range(30, 1200)]
        public int measureFrames = 180;

        [Tooltip("MC2 비동기 빌드 완료 대기 상한 (프레임)")]
        [Range(10, 1200)]
        public int buildTimeoutFrames = 180;

        [Tooltip("시작 시 자동 실행. 빌드에서는 " + AutoRunCommandLineArg + " 인자로도 자동 실행")]
        public bool autoRunOnStart;

        [Header("결과")]
        [TextArea]
        [System.NonSerialized] public string lastSummary = "";
        public bool isRunning;

        /// <summary>씬 로드 직후 첫 빌드(직렬화 클로스)의 초기화 계측 — 프레임 수와 InitCloth/PreBuild 누적(ns)</summary>
        public int sceneLoadInitFrames = -1;
        public long sceneLoadInitNs;
        public long sceneLoadPreBuildNs;

        [Tooltip("시작 프레임 스파이크 분해용 — 시작부터 기록할 프레임 수")]
        [Range(5, 120)]
        public int startupProbeFrames = 40;

        /// <summary>시작 프레임별 deltaTime(ms)·InitCloth/PreBuild 비용(ns) — 스파이크 원인 분해용</summary>
        [System.NonSerialized] public float[] startupFrameMs = new float[0];
        [System.NonSerialized] public long[] startupInitNs = new long[0];
        [System.NonSerialized] public long[] startupPreBuildNs = new long[0];

        Coroutine routine;
        ProfilerRecorder sceneInitRecorder;
        ProfilerRecorder scenePreBuildRecorder;
        int sceneProbeFrames;
        int startupFramesRecorded;
        // 계측 중 바꾼 setup.usePreBuild의 원복용 백업 — 코루틴 finally 외에
        // OnDisable 경로(코루틴 강제 종료)에서도 복원할 수 있게 필드로 둔다.
        bool usePreBuildPatched;
        bool savedUsePreBuild;

        static bool AutoRunRequested =>
            Environment.GetCommandLineArgs().Any(
                a => string.Equals(a, AutoRunCommandLineArg,
                    StringComparison.OrdinalIgnoreCase));

        void Start()
        {
            sceneInitRecorder = StartRecorder("InitCloth");
            scenePreBuildRecorder = StartRecorder("ClothProcess.PreBuild");
            if (autoRunOnStart || AutoRunRequested)
                Run();
        }

        void Update()
        {
            // 시작 N프레임의 프레임 시간·마커 비용 기록 — 첫 프레임 스파이크(셰이더/JIT/MC2) 분해용.
            // 클로스 유효화와 무관하게 고정 프레임 수만큼 기록한다.
            if (startupFramesRecorded < startupProbeFrames)
            {
                int i = startupFramesRecorded;
                if (startupFrameMs.Length < startupProbeFrames)
                {
                    startupFrameMs = new float[startupProbeFrames];
                    startupInitNs = new long[startupProbeFrames];
                    startupPreBuildNs = new long[startupProbeFrames];
                }
                startupFrameMs[i] = Time.unscaledDeltaTime * 1000f;
                if (sceneInitRecorder.Valid)
                    startupInitNs[i] = sceneInitRecorder.LastValue;
                if (scenePreBuildRecorder.Valid)
                    startupPreBuildNs[i] = scenePreBuildRecorder.LastValue;
                startupFramesRecorded++;
            }

            // 씬 로드 시점의 직렬화 클로스 초기화를 계측 — 프리빌드 클로스의 init 비용은
            // 씬 로드에서 지불되므로 패스 계측으로는 잡을 수 없어 별도 추적한다.
            if (sceneLoadInitFrames >= 0)
                return;
            if (setup == null)
                setup = GetComponent<CharacterPhysicsSetup>();
            sceneProbeFrames++;
            if (sceneInitRecorder.Valid)
                sceneLoadInitNs += sceneInitRecorder.LastValue;
            if (scenePreBuildRecorder.Valid)
                sceneLoadPreBuildNs += scenePreBuildRecorder.LastValue;
            var cloths = setup != null ? setup.generatedCloths : null;
            if (cloths != null && cloths.Count > 0 &&
                cloths.All(c => c != null && c.IsValid()))
            {
                sceneLoadInitFrames = sceneProbeFrames;
                DisposeRecorder(ref sceneInitRecorder);
                DisposeRecorder(ref scenePreBuildRecorder);
            }
            else if (sceneProbeFrames > buildTimeoutFrames)
            {
                sceneLoadInitFrames = sceneProbeFrames; // 타임아웃 — 유효화 실패도 기록
                DisposeRecorder(ref sceneInitRecorder);
                DisposeRecorder(ref scenePreBuildRecorder);
            }
        }

        /// <summary>계측 실행. 플레이 모드에서만 동작.</summary>
        [ContextMenu("물리 성능 계측 실행")]
        public void Run()
        {
            if (!Application.isPlaying)
            {
                lastSummary = "플레이 모드에서만 실행 가능합니다.";
                Debug.LogWarning("[PhysicsPerfRunner] " + lastSummary, this);
                return;
            }
            if (isRunning)
                return;
            routine = StartCoroutine(RunMeasurement());
        }

        IEnumerator RunMeasurement()
        {
            isRunning = true;
            if (setup == null)
                setup = GetComponent<CharacterPhysicsSetup>();
            if (setup == null)
            {
                lastSummary = "실패: CharacterPhysicsSetup이 없습니다.";
                Debug.LogError("[PhysicsPerfRunner] " + lastSummary, this);
                isRunning = false;
                routine = null;
                yield break;
            }

            var summary = new StringBuilder("[PhysicsPerfRunner] 계측 시작\n");
            summary.AppendLine($"sceneLoadInit: frames={sceneLoadInitFrames} initClothNs={sceneLoadInitNs} preBuildNs={sceneLoadPreBuildNs}");
            // 시작 프레임 상위 3개 — 스파이크 프레임과 그 안의 MC2 비용 비중
            var top = startupFrameMs
                .Select((ms, i) => (ms, i))
                .OrderByDescending(x => x.ms)
                .Take(3);
            foreach (var (ms, i) in top)
                summary.AppendLine(
                    $"startup frame[{i}]: {ms:F1}ms initClothNs={startupInitNs[i]} preBuildNs={startupPreBuildNs[i]}");
#if !UNITY_EDITOR && !DEVELOPMENT_BUILD
            // PipelineRunProfiler 호출은 에디터/Development Build에서만 컴파일된다
            summary.AppendLine("경고: 릴리즈 빌드라 계측이 비활성입니다. Development Build로 실행하세요.");
            lastSummary = summary.ToString();
            Debug.LogWarning(lastSummary, this);
            isRunning = false;
            routine = null;
            yield break;
#else
            // PreBuild를 먼저 측정 — Dynamic의 Setup() 재생성이 씬 직렬화 베이크 참조를 파괴하기 때문
            savedUsePreBuild = setup.usePreBuild;
            usePreBuildPatched = true;
            try
            {
                // BeginRun/SetContext도 예외가 나면 미완결 런과 isRunning이 남으므로 try 안에서 시작한다.
                PipelineRunProfiler.BeginRun(label);
                PipelineRunProfiler.SetContext(
                    boneCount: setup.transform.GetComponentsInChildren<Transform>(true).Length);

                yield return MeasurePass("PreBuild", true, summary);
                yield return MeasurePass("Dynamic", false, summary);

                PipelineRunProfiler.EndRun("Completed");
                summary.AppendLine($"리포트: {PipelineRunProfiler.LastWrittenReportPath}");
                lastSummary = summary.ToString();
                Debug.Log(lastSummary, this);
            }
            finally
            {
                // 계측이 바꾼 설정 필드·실행 플래그를 원복해 이후 플레이 세션 동작을 오염시키지 않는다.
                // setup 파괴 시에도 플래그 리셋은 실행되도록 null 확인을 둔다.
                RestorePatchedSettings();
                // 예외·중단으로 빠졌을 때 미완결 런이 남지 않게 폐기한다 (완료 후에는 no-op).
                PipelineRunProfiler.AbortRun();
                isRunning = false;
                routine = null;
            }
#endif
        }

        /// <summary>계측 중 변경한 설정 필드를 원복한다.</summary>
        void RestorePatchedSettings()
        {
            if (!usePreBuildPatched)
                return;
            if (setup != null)
                setup.usePreBuild = savedUsePreBuild;
            usePreBuildPatched = false;
        }

        /// <summary>
        /// 진행 중인 계측을 중단한다. Unity는 코루틴 중단 시 finally를 실행해
        /// 레코더·설정 정리가 완료되지만, 보장되지 않는 경로 대비로 플래그도 여기서 리셋한다.
        /// </summary>
        [ContextMenu("물리 성능 계측 중단")]
        public void Stop()
        {
            if (routine != null)
            {
                StopCoroutine(routine);
                routine = null;
            }
            RestorePatchedSettings();
            isRunning = false;
        }

        void OnDisable()
        {
            Stop();
        }

        void OnDestroy()
        {
            // Start()에서 만든 씬 로드 계측 레코더는 컴포넌트 수명과 함께 해제한다.
            DisposeRecorder(ref sceneInitRecorder);
            DisposeRecorder(ref scenePreBuildRecorder);
        }

        /// <summary>한 경로(동적/프리빌드)의 설정·초기화·정상 상태 비용을 계측한다.</summary>
        IEnumerator MeasurePass(string tag, bool usePreBuild, StringBuilder summary)
        {
            setup.usePreBuild = usePreBuild;

            // 씬 직렬화 등으로 이미 베이크된 프리빌드 데이터가 있으면 재생성 없이 리빌드만 측정한다.
            // Setup()으로 재생성하면 클로스의 serializeData2가 초기화되어 프리빌드 참조가 소실된다.
            bool reuseBaked = usePreBuild && setup.generatedCloths.Count > 0 &&
                setup.generatedCloths.All(c => c != null &&
                    c.GetSerializeData2().preBuildData.preBuildScriptableObject != null);

            // Setup() 벽시계 비용 — 스테이지 deltaMs에 기록된다.
            PipelineRunProfiler.NoteStage($"Physics.Setup.{tag}", "setup() 호출", false);
            // 프리빌드 경로는 InitCloth를 타지 않고 역직렬화 마커를 탄다 — 별도 계측 필요
            var initRecorder = StartRecorder("InitCloth");
            var preBuildRecorder = StartRecorder("ClothProcess.PreBuild");
            var simRecorder = default(ProfilerRecorder);
            var meshRecorder = default(ProfilerRecorder);
            try
            {
                if (reuseBaked)
                {
                    // 이미 씬 로드 시 프리빌드 경로로 빌드 완료. BuildAndRun()은 빌드된 클로스에서
                    // 예외를 던지므로 재빌드하지 않고, 초기화 비용은 씬 로드 계측값을 사용한다.
                    summary.AppendLine(
                        $"{tag}: 직렬화 프리빌드 재사용 — init은 씬 로드 계측(sceneLoadInit) 참조");
                }
                else
                {
                    setup.Setup();
#if UNITY_EDITOR
                    if (usePreBuild)
                    {
                        // 런타임 생성 클로스는 베이크 데이터가 없으므로 에디터에서 즉시 베이크.
                        // 같은 프레임에 주입해야 Start()의 자동 빌드가 프리빌드 경로를 탄다.
                        PipelineRunProfiler.NoteStage($"Physics.PreBuildBake.{tag}", "에디터 베이크", false);
                        int baked = BakeMissingPreBuildData();
                        summary.AppendLine($"{tag}: 프리빌드 데이터 {baked}개 에디터 베이크");
                    }
#else
                    if (usePreBuild)
                    {
                        summary.AppendLine($"{tag}: 베이크 데이터 없음 — 씬 직렬화 프리빌드가 필요해 패스 건너뜀");
                        yield break;
                    }
#endif
                }
                long memBefore = UnityEngine.Profiling.Profiler.GetTotalAllocatedMemoryLong();
                PipelineRunProfiler.NoteStage($"Physics.InitWait.{tag}",
                    $"cloths={setup.generatedCloths.Count}", false);

                // MC2 비동기 빌드 완료까지 대기 — 프레임 수가 곧 초기화 지연
                int waited = 0;
                double initNs = 0, preBuildNs = 0;
                while (waited < buildTimeoutFrames &&
                       setup.generatedCloths.Any(c => c == null || !c.IsValid()))
                {
                    initNs += ReadNs(initRecorder);
                    preBuildNs += ReadNs(preBuildRecorder);
                    waited++;
                    yield return null;
                }
                initNs += ReadNs(initRecorder);
                preBuildNs += ReadNs(preBuildRecorder);

                int validCount = setup.generatedCloths.Count(c => c != null && c.IsValid());
                // enabled만으로는 부족 — 프리빌드 ScriptableObject가 실제 있어야 데이터 경로가 됨
                int preBuiltCount = setup.generatedCloths.Count(c =>
                    c != null &&
                    c.GetSerializeData2().preBuildData.preBuildScriptableObject != null);
                long memAfter = UnityEngine.Profiling.Profiler.GetTotalAllocatedMemoryLong();
                string initInfo =
                    $"waitFrames={waited} valid={validCount}/{setup.generatedCloths.Count} " +
                    $"initClothNs={(long)initNs} preBuildNs={(long)preBuildNs} " +
                    $"memDeltaKB={(memAfter - memBefore) / 1024} " +
                    $"preBuildData={preBuiltCount}";
                PipelineRunProfiler.NoteStage($"Physics.Warmup.{tag}", initInfo, false);
                summary.AppendLine($"{tag}: {initInfo}{(waited >= buildTimeoutFrames ? " [타임아웃]" : "")}");

                for (int i = 0; i < warmupFrames; i++)
                {
                    PipelineRunProfiler.SampleFrame(Time.deltaTime);
                    yield return null;
                }

                simRecorder = StartRecorder("StartClothUpdate.Time");
                meshRecorder = StartRecorder("WriteMesh");
                double simNsSum = 0, meshNsSum = 0, simNsMax = 0;
                int simFrames = 0;
                PipelineRunProfiler.NoteStage($"Physics.Measure.{tag}", "", false);
                if (validationSequence != null && !validationSequence.isRunning)
                    validationSequence.Run();
                for (int i = 0; i < measureFrames; i++)
                {
                    yield return null;
                    PipelineRunProfiler.SampleFrame(Time.deltaTime);
                    double simNs = ReadNs(simRecorder);
                    double meshNs = ReadNs(meshRecorder);
                    simNsSum += simNs;
                    meshNsSum += meshNs;
                    if (simNs > simNsMax)
                        simNsMax = simNs;
                    if (simNs > 0)
                        simFrames++;
                }

                double simMeanMs = simFrames > 0 ? simNsSum / simFrames / 1e6 : 0;
                string measureInfo = $"simMean={simMeanMs:F3}ms simMax={simNsMax / 1e6:F3}ms " +
                    $"meshNsTotal={(long)meshNsSum} markerFrames={simFrames}";
                PipelineRunProfiler.NoteStage($"Physics.MeasureDone.{tag}", measureInfo, false);
                summary.AppendLine($"{tag} 계측: {measureInfo}");
            }
            finally
            {
                // 정상 종료·조기 break·예외 어느 경로로 빠져도 레코더를 해제한다.
                DisposeRecorder(ref initRecorder);
                DisposeRecorder(ref preBuildRecorder);
                DisposeRecorder(ref simRecorder);
                DisposeRecorder(ref meshRecorder);
            }
        }

#if UNITY_EDITOR
        /// <summary>
        /// 에디터 전용 — 프리빌드 데이터가 없는 클로스에 ScriptableObject를 만들어 베이크한다.
        /// MC2 공식 에디터 경로(PreBuildDataCreation)와 동일한 데이터를 생성하며 저장 대화상자는 띄우지 않는다.
        /// </summary>
        int BakeMissingPreBuildData()
        {
            const string dir = "Assets/_Project/Physics/PreBuild";
            if (!UnityEditor.AssetDatabase.IsValidFolder("Assets/_Project/Physics"))
                UnityEditor.AssetDatabase.CreateFolder("Assets/_Project", "Physics");
            if (!UnityEditor.AssetDatabase.IsValidFolder(dir))
                UnityEditor.AssetDatabase.CreateFolder("Assets/_Project/Physics", "PreBuild");

            int baked = 0;
            foreach (var cloth in setup.generatedCloths)
            {
                if (cloth == null)
                    continue;
                var pb = cloth.GetSerializeData2().preBuildData;
                pb.enabled = true;
                if (pb.preBuildScriptableObject == null)
                {
                    var so = ScriptableObject.CreateInstance<PreBuildScriptableObject>();
                    string path = UnityEditor.AssetDatabase.GenerateUniqueAssetPath(
                        $"{dir}/MagicaPreBuild_{label}_{cloth.name}.asset");
                    UnityEditor.AssetDatabase.CreateAsset(so, path);
                    pb.preBuildScriptableObject = so;
                }
                var res = PreBuildDataCreation.CreatePreBuildData(cloth, false);
                if (res.IsSuccess())
                    baked++;
                else
                    Debug.LogWarning(
                        $"[PhysicsPerfRunner] 프리빌드 베이크 실패({cloth.name}): {res.GetResultString()}", cloth);
            }
            return baked;
        }
#endif

        static ProfilerRecorder StartRecorder(string markerName)
        {
            try
            {
                var rec = ProfilerRecorder.StartNew(ProfilerCategory.Scripts, markerName, 1);
                if (!rec.Valid)
                    Debug.LogWarning(
                        $"[PhysicsPerfRunner] 마커 '{markerName}' 미검출 — " +
                        "Development Build가 아니면 0으로 기록됩니다.");
                return rec;
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[PhysicsPerfRunner] 레코더 시작 실패({markerName}): {e.Message}");
                return default;
            }
        }

        static double ReadNs(ProfilerRecorder rec)
        {
            try
            {
                return rec.Valid ? rec.LastValue : 0;
            }
            catch
            {
                return 0;
            }
        }

        static void DisposeRecorder(ref ProfilerRecorder rec)
        {
            if (rec.Valid)
                rec.Dispose();
            rec = default;
        }
    }
}
