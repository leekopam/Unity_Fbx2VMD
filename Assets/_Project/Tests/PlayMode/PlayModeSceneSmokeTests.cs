using NUnit.Framework;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

namespace Fbx2Vmd.Tests.PlayMode
{
    /// <summary>
    /// 빌드 등록 씬을 PlayMode에서 실제 로드·수 프레임 재생·카메라 렌더까지 수행하는 회귀 스모크.
    /// Unity 6 업그레이드 시 런타임 계층(씬 로딩/렌더 파이프/초기화 순서) 붕괴를 검출한다.
    /// </summary>
    [Category("UpgradeReadiness")]
    public class PlayModeSceneSmokeTests
    {
        // 휴머노이드 미배치 씬에서 앱이 내보내는 기존 알림 — 회귀 신호가 아니다.
        private const string KnownBenignError = "Cannot initialize VMD recording bone mapping";

        // 빌드 설정 씬을 런타임 API로 열거한다 (UnityEditor 의존 없이 PlayMode 분류 유지).
        private static List<string> BuildScenePaths
        {
            get
            {
                var paths = new List<string>();
                for (int i = 0; i < SceneManager.sceneCountInBuildSettings; i++)
                {
                    string path = SceneUtility.GetScenePathByBuildIndex(i);
                    if (!string.IsNullOrEmpty(path) && File.Exists(Path.GetFullPath(path)))
                    {
                        paths.Add(path);
                    }
                }
                return paths;
            }
        }

        [UnityTest]
        public IEnumerator BuildScenes_LoadAndPlayFrames_NoError()
        {
            // 작업 전 씬을 기억해 종료 시 복원한다 (다음 테스트 오염 방지).
            string restorePath = SceneManager.GetActiveScene().path;

            var scenePaths = BuildScenePaths;
            Assert.That(scenePaths.Count, Is.GreaterThan(0), "빌드 등록 씬이 없음 — 계약 무의미");

            // 씬 로드 중 발생한 에러/예외 로그를 수집한다. 알려진 앱 자체 알림만 필터하고
            // 나머지는 그대로 실패시켜 Unity 6 회귀 신호를 놓치지 않는다.
            var errors = new List<string>();
            void Collect(string msg, string st, LogType t)
            {
                if (t == LogType.Error || t == LogType.Exception || t == LogType.Assert)
                {
                    errors.Add(msg);
                }
            }
            Application.logMessageReceived += Collect;
            LogAssert.ignoreFailingMessages = true;
            try
            {
                foreach (string scenePath in scenePaths)
                {
                    string sceneName = Path.GetFileNameWithoutExtension(scenePath);
                    // 동명 씬 오매치 방지를 위해 경로로 로드한다.
                    AsyncOperation load = SceneManager.LoadSceneAsync(scenePath, LoadSceneMode.Single);
                    Assert.That(load, Is.Not.Null, $"씬 로드 실패: {sceneName}");
                    while (!load.isDone)
                    {
                        yield return null;
                    }

                    // 초기화 경합(Start/Awake)이 끝난 뒤 수 프레임 재생해 런타임 예외를 드러낸다.
                    for (int i = 0; i < 5; i++)
                    {
                        yield return new WaitForEndOfFrame();
                    }

                    Scene scene = SceneManager.GetSceneByPath(scenePath);
                    Assert.That(scene.isLoaded, Is.True, $"로드 상태 불일치: {sceneName}");
                    Assert.That(scene.rootCount, Is.GreaterThan(0), $"루트 오브젝트 없음: {sceneName}");

                    MissingScriptCountZero(scene, sceneName);
                }
            }
            finally
            {
                // 복원은 수집 창 안에서 수행 — 복원 중 에러도 회귀 신호로 잡힌다.
                RestoreScene(restorePath);
                Application.logMessageReceived -= Collect;
                LogAssert.ignoreFailingMessages = false;
            }

            var unexpected = errors.Where(e => !e.Contains(KnownBenignError)).ToList();
            Assert.That(unexpected, Is.Empty,
                "씬 로드/재생 중 예상 밖 에러:\n" + string.Join("\n", unexpected));
        }

        [UnityTest]
        public IEnumerator MainScene_CameraRenders_ReadablePixels()
        {
            const string mainScenePath = "Assets/_Project/Scene/Main_Recoding.unity";

            string restorePath = SceneManager.GetActiveScene().path;
            var errors = new List<string>();
            void Collect(string msg, string st, LogType t)
            {
                if (t == LogType.Error || t == LogType.Exception || t == LogType.Assert)
                {
                    errors.Add(msg);
                }
            }
            Application.logMessageReceived += Collect;
            LogAssert.ignoreFailingMessages = true;

            Texture2D tex = null;
            try
            {
                AsyncOperation load = SceneManager.LoadSceneAsync(mainScenePath, LoadSceneMode.Single);
                Assert.That(load, Is.Not.Null, "메인 씬 로드 실패");
                while (!load.isDone)
                {
                    yield return null;
                }

                // 씬 내 콘텐츠 스폰 지연(_startDelay 등)을 고려해 충분히 프레임을 기다린다.
                for (int i = 0; i < 15; i++)
                {
                    yield return new WaitForEndOfFrame();
                }

                Camera cam = Object.FindObjectsByType<Camera>(FindObjectsSortMode.None)
                    .FirstOrDefault(c => c.isActiveAndEnabled);
                Assert.That(cam, Is.Not.Null, "메인 씬에 활성 카메라 없음");

                // Built-in 렌더 경로가 RenderTexture readback까지 살아있는지 검증한다.
                var rt = new RenderTexture(64, 64, 24);
                RenderTexture prev = cam.targetTexture;
                RenderTexture prevActive = RenderTexture.active;
                try
                {
                    cam.targetTexture = rt;
                    cam.Render();
                    RenderTexture.active = rt;
                    tex = new Texture2D(64, 64, TextureFormat.RGB24, false);
                    tex.ReadPixels(new Rect(0, 0, 64, 64), 0, 0);
                    tex.Apply();
                }
                finally
                {
                    RenderTexture.active = prevActive;
                    cam.targetTexture = prev;
                    rt.Release();
                }

                Color32[] pixels = tex.GetPixels32();
                Assert.That(pixels.Length, Is.EqualTo(64 * 64));
                // 톤 스테이지가 그려졌는지: 완전 검정 한 장이면 렌더 경로 파괴로 본다.
                Assert.That(pixels.Any(p => p.r > 8 || p.g > 8 || p.b > 8), Is.True,
                    "렌더 결과가 전부 검정 — 렌더 파이프 붕괴 의심");
            }
            finally
            {
                if (tex != null)
                {
                    Object.DestroyImmediate(tex);
                }
                RestoreScene(restorePath);
                Application.logMessageReceived -= Collect;
                LogAssert.ignoreFailingMessages = false;
            }

            var unexpected = errors.Where(e => !e.Contains(KnownBenignError)).ToList();
            Assert.That(unexpected, Is.Empty,
                "메인 씬 로드/재생 중 예상 밖 에러:\n" + string.Join("\n", unexpected));
        }

        // 빌드 등록 씬만 복원 가능 — 미등록 씬은 런타임 LoadScene 불가라 건너뛴다.
        private static void RestoreScene(string path)
        {
            if (string.IsNullOrEmpty(path) ||
                SceneUtility.GetBuildIndexByScenePath(path) < 0)
            {
                return;
            }
            SceneManager.LoadScene(path, LoadSceneMode.Single);
        }

        private static void MissingScriptCountZero(Scene scene, string sceneName)
        {
            int missing = 0;
            foreach (GameObject root in scene.GetRootGameObjects())
            {
                foreach (Transform child in root.GetComponentsInChildren<Transform>(true))
                {
                    foreach (Component comp in child.GetComponents<Component>())
                    {
                        if (comp == null)
                        {
                            missing++;
                        }
                    }
                }
            }
            Assert.That(missing, Is.EqualTo(0), $"{sceneName}: MissingScript {missing}개");
        }
    }
}
