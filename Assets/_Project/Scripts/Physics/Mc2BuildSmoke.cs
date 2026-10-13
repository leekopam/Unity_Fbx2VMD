using System.Collections;
using System.Linq;
using MagicaCloth2;
using UnityEngine;

namespace Fbx2Vmd.ClothPhysics
{
    /// <summary>
    /// 플레이어 빌드 스모크 체크 — 빌드에서만 동작한다.
    /// 씬 로드 후 MC2 매니저가 PlayerLoop에 등록됐는지, 생성 클로스가 유효한지
    /// Player.log에 [MC2_SMOKE] 마커 라인으로 남긴다.
    /// 에디터에서는 조용히 종료한다.
    /// </summary>
    public static class Mc2BuildSmoke
    {
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void Install()
        {
#if !UNITY_EDITOR
            var go = new GameObject("MC2_Smoke");
            Object.DontDestroyOnLoad(go);
            go.AddComponent<Runner>();
#endif
        }

#if !UNITY_EDITOR
        class Runner : MonoBehaviour
        {
            IEnumerator Start()
            {
                // 초기 프레임은 엔진 스파이크 구간 — 빌드 완료까지 유예
                for (int i = 0; i < 180; i++)
                    yield return null;
                LogMarker("boot");
                // 부트 씬에 캐릭터가 없고 -mc2Smoke 인자가 있을 때만 녹화 씬을 직접 열어
                // 실제 클로스까지 계측한다 — 무인자 실행에서는 수동적 프로브로만 동작
                if (Object.FindObjectsByType<CharacterPhysicsSetup>(FindObjectsSortMode.InstanceID).Length == 0 && SmokeSceneLoadRequested())
                {
                    var load = UnityEngine.SceneManagement.SceneManager.LoadSceneAsync("Main_Recoding");
                    if (load != null)
                    {
                        // 비완료 AsyncOperation 대비 상한 — 무한 대기면 마커가 안 나와 진단 공백이 된다
                        int waited = 0;
                        while (!load.isDone && waited++ < 1800)
                            yield return null;
                        if (!load.isDone)
                            LogMarker("load_timeout");
                        else
                        {
                            for (int i = 0; i < 300; i++)
                                yield return null;
                            LogMarker("main_recoding");
                        }
                    }
                }
                Destroy(gameObject);
            }

            static bool SmokeSceneLoadRequested()
            {
                var args = System.Environment.GetCommandLineArgs();
                foreach (var a in args)
                    if (string.Equals(a, "-mc2Smoke",
                        System.StringComparison.OrdinalIgnoreCase)) return true;
                return false;
            }

            static void LogMarker(string stage)
            {
                var setups = Object.FindObjectsByType<CharacterPhysicsSetup>(FindObjectsSortMode.InstanceID);
                int total = 0, valid = 0;
                foreach (var s in setups)
                    foreach (var c in s.generatedCloths)
                    {
                        if (c == null) continue;
                        total++;
                        if (c.IsValid()) valid++;
                    }
                Debug.Log($"[MC2_SMOKE] stage={stage} managers={(MagicaManager.Time != null)} " +
                    $"setups={setups.Length} cloths={total} valid={valid} " +
                    $"burst={(Unity.Burst.BurstCompiler.IsEnabled ? 1 : 0)}");
            }
        }
#endif
    }
}
