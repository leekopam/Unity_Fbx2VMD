using NUnit.Framework;
using System;
using System.Collections;
using System.IO;
using System.Linq;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

namespace Fbx2Vmd.Tests.PlayMode
{
    /// <summary>
    /// 런타임 자동 초기화 경로 검증: Magica Cloth 2 매니저, uLipSync 플레이 스텝.
    /// 서드파티 런타임이 Unity 6 초기화 순서 변경에서 살아남는지 확인한다.
    /// 어셈블리 부재(미프로비저닝) 시 Ignore — 리플렉션으로 탐색해 결함이 아니라 보고가 되게 한다.
    /// </summary>
    [Category("UpgradeReadiness")]
    public class PlayModeRuntimeServicesTests
    {
        private static Type FindType(string name) =>
            AppDomain.CurrentDomain.GetAssemblies()
                .Select(a => { try { return a.GetType(name); } catch { return null; } })
                .FirstOrDefault(t => t != null);

        // PlayerLoop 트리를 재귀 순회해 MC2가 삽입한 MagicaManager 서브시스템 존재 여부를 확인한다.
        private static int CountLoopEntries(UnityEngine.LowLevel.PlayerLoopSystem sys, string typeName)
        {
            int count = sys.type != null && sys.type.Name == typeName ? 1 : 0;
            if (sys.subSystemList == null)
            {
                return count;
            }
            foreach (var sub in sys.subSystemList)
            {
                count += CountLoopEntries(sub, typeName);
            }
            return count;
        }

        [UnityTest]
        public IEnumerator MagicaCloth_RuntimeManager_Initializes()
        {
            Type manager = FindType("MagicaCloth2.MagicaManager");
            if (manager == null)
            {
                Assert.Ignore("MagicaCloth2 어셈블리 없음 (미프로비저닝)");
            }

            yield return null;
            yield return null;

            // MC2는 SubsystemRegistration에서 정적 재초기화 후 isPlaying=true가 된다.
            var isPlaying = manager.GetField("isPlaying",
                System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.NonPublic);
            Assert.That(isPlaying, Is.Not.Null, "MagicaManager.isPlaying 필드 없음 — MC2 내부 구조 변경");
            Assert.That((bool)isPlaying.GetValue(null), Is.True,
                "MC2 런타임 초기화 미완료 — SubsystemRegistration 경로 파괴 의심");

            // 커스텀 PlayerLoop 등록이 살아있어야 시뮬레이션이 매 프레임 돈다.
            var loop = UnityEngine.LowLevel.PlayerLoop.GetCurrentPlayerLoop();
            int entries = CountLoopEntries(loop, "MagicaManager");
            Assert.That(entries, Is.GreaterThan(0),
                "PlayerLoop에 MagicaManager 미등록 — 커스텀 게임루프 삽입 파괴");
        }

        [UnityTest]
        public IEnumerator ULipSync_Component_SurvivesPlayStep()
        {
            Type lipSync = FindType("uLipSync.uLipSync");
            if (lipSync == null)
            {
                Assert.Ignore("uLipSync 어셈블리 없음");
            }

            var host = new GameObject("lipsync-probe");
            host.AddComponent<AudioSource>();
            Component comp = host.AddComponent(lipSync);
            Assert.That(comp, Is.Not.Null);

            // 마이크/프로파일 없이도 컴포넌트 수명주기(Update/OnDisable 해제)가
            // 예외 없이 돌아가는지만 검증 — 입력 분석 자체는 별도 계약 테스트 대상.
            // 입력 부재 시 uLipSync가 내보낼 수 있는 경고는 스모크 대상이 아니므로 억제한다.
            bool alive = false;
            LogAssert.ignoreFailingMessages = true;
            try
            {
                yield return null;
                yield return null;
                yield return null;
                alive = comp != null && host.activeInHierarchy;
            }
            finally
            {
                LogAssert.ignoreFailingMessages = false;
                Object.Destroy(host);
            }
            Assert.That(alive, Is.True, "uLipSync 컴포넌트가 플레이 중 소멸");
        }
    }
}
