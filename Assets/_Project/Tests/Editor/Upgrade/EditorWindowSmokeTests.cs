using NUnit.Framework;
using System;
using System.Linq;
using System.Text;
using UnityEditor;

namespace Tests.Editor.Upgrade
{
    /// <summary>
    /// 에디터 윈도우 스모크 계약: Fbx2Vmd 네임스페이스의 모든 EditorWindow를
    /// 열고 닫는다. IMGUI 에디터 도구가 100곳 넘으므로 열기만으로 상당수
    /// 초기화 경로를 커버한다.
    /// </summary>
    [Category("UpgradeReadiness")]
    public class EditorWindowSmokeTests
    {
        [Test]
        public void AllProjectEditorWindows_OpenAndCloseWithoutException()
        {
            var windowTypes = AppDomain.CurrentDomain.GetAssemblies()
                .SelectMany(a => { try { return a.GetTypes(); } catch { return new Type[0]; } })
                .Where(t => !t.IsAbstract
                    && typeof(EditorWindow).IsAssignableFrom(t)
                    && t.Namespace != null
                    && t.Namespace.StartsWith("Fbx2Vmd"))
                .ToList();

            Assert.That(windowTypes.Count, Is.GreaterThan(0),
                "Fbx2Vmd 에디터 윈도우가 하나도 발견되지 않음 — 스캔 자체가 의심스러움");

            var failures = new StringBuilder();

            foreach (Type windowType in windowTypes)
            {
                EditorWindow window = null;
                try
                {
                    window = EditorWindow.GetWindow(windowType);
                    Assert.That(window, Is.Not.Null);
                }
                catch (Exception exception)
                {
                    failures.AppendLine($"{windowType.FullName}: {exception.GetType().Name} {exception.Message}");
                }
                finally
                {
                    if (window != null)
                    {
                        window.Close();
                    }
                }
            }

            UnityEngine.Debug.Log(
                $"[UpgradeReadiness] 에디터 윈도우 스모크: {windowTypes.Count}개 검사");
            Assert.That(failures.ToString(), Is.Empty, "열리지 않는 에디터 윈도우가 있음");
        }
    }
}
