using NUnit.Framework;
using System;
using System.IO;
using System.Text;

namespace Tests.Editor.Upgrade
{
    /// <summary>
    /// 네이티브/관리 DLL 존재와 아키텍처 계약.
    /// 에디터는 x64만 지원 — x86 전용 네이티브 DLL은 로드 불가이므로 PE 헤더로 검증한다.
    /// </summary>
    [Category("UpgradeReadiness")]
    public class NativePluginHealthTests
    {
        private const ushort MachineAmd64 = 0x8664;

        private static string ProjectRoot => Directory.GetCurrentDirectory();

        // 존재해야 하는 DLL — 없으면 해당 기능 경로가 죽는다.
        private static readonly string[] RequiredDlls =
        {
            "Assets/Plugins/Assimp-net/assimp.dll",
            "Assets/Resources/AssimpNet.dll",
            "Assets/MMD4Mecanim/Scripts/MMD4Mecanim.dll",
            "Assets/MMD4Mecanim/Scripts/Internal/MMD4Mecanim.dll",
            "Assets/MMD4Mecanim/Editor/MMD4MecanimEditor.dll",
            "Assets/MMD4Mecanim/Editor/PMX2FBX/libfbxsdk.dll",
            "Assets/MMD4Mecanim/Plugins/MMD4MecanimBulletPhysics/x86_64/MMD4MecanimBulletPhysics.dll",
            "Assets/Plugins/UnityStandaloneFileBrowser-master/Assets/StandaloneFileBrowser/Plugins/Ookii.Dialogs.dll",
            "Assets/Plugins/UnityStandaloneFileBrowser-master/Assets/StandaloneFileBrowser/Plugins/System.Windows.Forms.dll",
            "Assets/_Project/Plugins/Editor/NAudio/NAudio.Core.dll",
        };

        // 실행 파일이 x64이므로 Unity가 실제로 로드하는 네이티브 DLL은 AMD64여야 한다.
        // 주의: PMX2FBX의 libfbxsdk/mecab은 x86 전용으로 확인됨(0x014C) —
        // 에디터 프로세스가 로드하는 플러그인이 아니라 사장 툴체인이므로
        // 어서트 대상이 아니라 아래 비트니스 인벤토리에만 기록한다.
        private static readonly string[] NativeX64Dlls =
        {
            "Assets/Plugins/Assimp-net/assimp.dll",
            "Assets/MMD4Mecanim/Plugins/MMD4MecanimBulletPhysics/x86_64/MMD4MecanimBulletPhysics.dll",
        };

        [Test]
        public void RequiredDlls_ExistOnDisk()
        {
            var failures = new StringBuilder();
            // MMD4Mecanim은 gitignored 유료 자산 — 미설치 환경에서는 항목을 건너뛴다
            bool mmdInstalled = Directory.Exists(
                Path.Combine(ProjectRoot, "Assets", "MMD4Mecanim"));

            foreach (string relativePath in RequiredDlls)
            {
                if (!mmdInstalled && relativePath.StartsWith("Assets/MMD4Mecanim/"))
                {
                    continue;
                }
                if (!File.Exists(Path.Combine(ProjectRoot, relativePath)))
                {
                    failures.AppendLine(relativePath);
                }
            }

            Assert.That(failures.ToString(), Is.Empty, "필수 DLL이 없음");
        }

        [Test]
        public void NativeDlls_AreX64()
        {
            var failures = new StringBuilder();
            bool mmdInstalled = Directory.Exists(
                Path.Combine(ProjectRoot, "Assets", "MMD4Mecanim"));

            foreach (string relativePath in NativeX64Dlls)
            {
                if (!mmdInstalled && relativePath.StartsWith("Assets/MMD4Mecanim/"))
                {
                    continue;
                }
                string fullPath = Path.Combine(ProjectRoot, relativePath);
                if (!File.Exists(fullPath))
                {
                    failures.AppendLine($"{relativePath}: 파일 없음");
                    continue;
                }

                ushort machine = ReadPeMachine(fullPath);
                if (machine != MachineAmd64)
                {
                    failures.AppendLine($"{relativePath}: machine=0x{machine:X4} (AMD64 아님)");
                }
            }

            Assert.That(failures.ToString(), Is.Empty, "x64가 아닌 네이티브 DLL이 있음 — 에디터 로드 불가");
        }

        [Test]
        public void DllBitnessInventory_IsExportedForDiff()
        {
            // Assets 아래 모든 DLL의 PE Machine을 JSON으로 기록한다.
            // x86(0x014C) 레거시 DLL이 어디 있는지 한눈에 보이고 버전 간 diff가 된다.
            var report = new StringBuilder("[\n");

            foreach (string path in Directory.GetFiles(
                Path.Combine(ProjectRoot, "Assets"), "*.dll", SearchOption.AllDirectories))
            {
                string relative = path.Substring(ProjectRoot.Length + 1).Replace('\\', '/');
                ushort machine = ReadPeMachine(path);
                report.Append("  { \"file\": \"").Append(relative)
                    .Append("\", \"machine\": \"0x").Append(machine.ToString("X4"))
                    .Append("\" },\n");
            }
            // 후행 콤마 제거 — strict JSON 파서 대응
            if (report.Length > 2)
            {
                report.Remove(report.Length - 2, 1);
            }
            report.Append("]\n");

            string reportPath = UpgradeReadinessReport.Write("dll-bitness.json", report.ToString());
            Assert.That(File.Exists(reportPath), Is.True, "DLL 비트니스 리포트 미작성");
        }

        // PE 헤더의 Machine 필드만 읽는다. MZ→e_lfanew→'PE\0\0'→Machine.
        private static ushort ReadPeMachine(string path)
        {
            byte[] bytes = File.ReadAllBytes(path);
            if (bytes.Length < 0x40 || bytes[0] != 'M' || bytes[1] != 'Z')
            {
                return 0;
            }

            int headerOffset = BitConverter.ToInt32(bytes, 0x3C);
            // headerOffset + 6는 int 오버플로 가능 — 단방향 비교로 바꾼다
            if (headerOffset < 0 || headerOffset > bytes.Length - 6
                || bytes[headerOffset] != 'P' || bytes[headerOffset + 1] != 'E'
                || bytes[headerOffset + 2] != 0 || bytes[headerOffset + 3] != 0)
            {
                return 0;
            }

            return BitConverter.ToUInt16(bytes, headerOffset + 4);
        }
    }
}
