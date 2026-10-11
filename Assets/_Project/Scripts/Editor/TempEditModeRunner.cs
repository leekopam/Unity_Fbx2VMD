using System.IO;
using UnityEditor;
using UnityEditor.TestTools.TestRunner.Api;
using UnityEngine;
using UnityEngine.TestTools;

namespace Fbx2Vmd.EditorTools
{
    /// <summary>MCP 브리지가 막혔을 때 쓰는 임시 EditMode 러너.
    /// Temp/run_tests.flag 파일이 생기면 EditMode를 돌리고
    /// 결과를 Temp/utf_results.txt에 기록한다.
    /// flag 파일에 테스트 이름을 한 줄씩 쓰면 그것만 필터 실행한다.
    /// 이름은 `FixtureName.TestName` 전체 이름(FQN 포함)이어야 한다.
    /// Temp/cancel_tests.flag 파일이 생기면 진행 중인 런을 모두 취소한다.</summary>
    [InitializeOnLoad]
    public static class TempEditModeRunner
    {
        const string FlagPath = "Temp/run_tests.flag";
        const string CancelPath = "Temp/cancel_tests.flag";
        const string ResultPath = "Temp/utf_results.txt";

        static TestRunnerApi s_api;
        static Cb s_cb;
        static bool s_running;

        static TempEditModeRunner()
        {
            s_api = ScriptableObject.CreateInstance<TestRunnerApi>();
            EditorApplication.update += Poll;
        }

        static void Poll()
        {
            // 취소 요청은 진행 중에도 처리해야 하므로 실행 가드보다 먼저 확인함
            if (File.Exists(CancelPath))
            {
                CancelAllRuns();
            }

            if (s_running
                || !File.Exists(FlagPath)
                || EditorApplication.isCompiling
                || EditorApplication.isPlaying
                || EditorApplication.isPlayingOrWillChangePlaymode)
            {
                return;
            }

            string[] filterNames;
            try
            {
                filterNames = File.ReadAllLines(FlagPath);
                File.Delete(FlagPath);
                File.Delete(ResultPath); // 지난 실행의 스테일 결과를 지워 오독을 막는다.
            }
            catch (IOException)
            {
                return; // 다른 프로세스가 쓰는 중 — 다음 틱에 재시도
            }

            s_running = true;
            try
            {
                s_cb = ScriptableObject.CreateInstance<Cb>();
                s_cb.hideFlags = HideFlags.HideAndDontSave;
                s_api.RegisterCallbacks(s_cb);
                var filter = new Filter { testMode = TestMode.EditMode };
                var names = filterNames == null ? null :
                    System.Linq.Enumerable.ToArray(
                        System.Linq.Enumerable.Where(filterNames,
                            n => !string.IsNullOrWhiteSpace(n)));
                if (names != null && names.Length > 0)
                {
                    filter.testNames = names;
                }

                s_api.Execute(new ExecutionSettings(filter));
                Debug.Log("[TempRunner] EditMode 실행 시작");
            }
            catch (System.Exception e)
            {
                s_running = false;
                File.WriteAllText(ResultPath, "runner-exception: " + e);
            }
        }

        // UTF는 도메인 리로드 후 실행 중 잡을 재개하므로, wedged 런을 끝내려면 명시 취소가 필요함
        static void CancelAllRuns()
        {
            try
            {
                File.Delete(CancelPath);
            }
            catch (IOException)
            {
                return;
            }

            int cancelled = 0;
            try
            {
                const System.Reflection.BindingFlags all =
                    System.Reflection.BindingFlags.Static |
                    System.Reflection.BindingFlags.Instance |
                    System.Reflection.BindingFlags.Public |
                    System.Reflection.BindingFlags.NonPublic;
                var holderType = typeof(TestRunnerApi).Assembly.GetType(
                    "UnityEditor.TestTools.TestRunner.TestRun.TestJobDataHolder");
                // ScriptableSingleton<T>의 instance/s_Instance는 베이스 제네릭 타입에 선언되므로
                // NonPublic 멤버까지 찾으려면 타입 체인을 위로 올라가며 탐색해야 함
                object holder = null;
                for (var t = holderType; t != null && holder == null; t = t.BaseType)
                {
                    holder = t.GetProperty("instance", all)?.GetValue(null)
                        ?? t.GetField("s_Instance", all)?.GetValue(null)
                        ?? t.GetField("s_instance", all)?.GetValue(null);
                }

                var runsField = holderType?.GetField("TestRuns", all);
                var runs = runsField?.GetValue(
                        runsField.IsStatic ? null : holder)
                    as System.Collections.IEnumerable;
                if (runs == null)
                {
                    Debug.LogWarning("[TempRunner] TestJobDataHolder 접근 실패 — 취소 불가");
                    File.WriteAllText("Temp/cancel_done.txt", "cancelled=0 (reflection failed)");
                    return;
                }

                var guids = new System.Collections.Generic.List<string>();
                foreach (var run in runs)
                {
                    var guid = run.GetType().GetField("guid")?.GetValue(run) as string;
                    if (guid != null)
                    {
                        guids.Add(guid);
                    }
                }

                foreach (string guid in guids)
                {
                    if (TestRunnerApi.CancelTestRun(guid))
                    {
                        cancelled++;
                    }
                }
            }
            catch (System.Exception e)
            {
                Debug.LogWarning($"[TempRunner] 취소 실패: {e.Message}");
            }

            // 취소된 런의 콜백이 뒤늦게 RunFinished로 결과 파일을 덮어쓰지 않게 등록 해제
            if (s_cb != null)
            {
                s_api.UnregisterCallbacks(s_cb);
                Object.DestroyImmediate(s_cb);
                s_cb = null;
            }
            // 취소된 wedged 런은 RunFinished가 안 올 수 있으므로 실행 플래그를 해제해 flag 재개를 허용
            s_running = false;
            File.WriteAllText("Temp/cancel_done.txt", $"cancelled={cancelled}");
            Debug.Log($"[TempRunner] 런 취소: {cancelled}건");
        }

        sealed class Cb : ScriptableObject, ICallbacks
        {
            public void RunStarted(ITestAdaptor testsToRun)
            {
                Debug.Log($"[TempRunner] RunStarted tests={testsToRun.TestCaseCount}");
            }

            public void RunFinished(ITestResultAdaptor result)
            {
                s_running = false;
                s_api?.UnregisterCallbacks(this); // 누적 콜백이 다음 런에 중복 기록하지 않게 해제
                if (s_cb != this)
                    return; // 취소된 과거 런의 스테일 콜백 — 결과 파일 오염 방지
                s_cb = null;
                var xml = result.ToXml();
                File.WriteAllText(ResultPath,
                    $"result={result.ResultState} passed={result.PassCount} failed={result.FailCount} " +
                    $"skipped={result.SkipCount} duration={result.Duration:0.0}s\n{xml.OuterXml}");
                Debug.Log($"[TempRunner] RunFinished {result.ResultState} " +
                    $"pass={result.PassCount} fail={result.FailCount}");
                Object.DestroyImmediate(this);
            }

            public void TestStarted(ITestAdaptor test) { }
            public void TestFinished(ITestResultAdaptor result) { }
        }
    }
}
