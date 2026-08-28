using System;
using System.Diagnostics;
using System.IO;

namespace Fbx2Vmd.Settings
{
    internal sealed class MainRecordingSettingsPlayerProcessSession
    {
        private const string ElectronRunAsNodeEnvironmentVariableName = "ELECTRON_RUN_AS_NODE";

        private Process startedProcess;

        internal MainRecordingSettingsActionResult TryLaunch(
            Func<MainRecordingSettingsLaunchPlan> createPlan)
        {
            if (IsRunning())
            {
                return MainRecordingSettingsActionResult.Success("Web 설정창이 이미 실행 중입니다.");
            }

            MainRecordingSettingsLaunchPlan plan = createPlan();
            if (!Directory.Exists(plan.WorkingDirectory))
            {
                return MainRecordingSettingsActionResult.Failure(
                    $"Web 설정창 폴더를 찾을 수 없습니다: {plan.WorkingDirectory}");
            }

            if (!File.Exists(plan.ExecutablePath))
            {
                return MainRecordingSettingsActionResult.Failure(
                    $"Web 설정창 실행 파일을 찾을 수 없습니다: {plan.ExecutablePath}");
            }

            try
            {
                startedProcess = LaunchProcess(plan);
                if (startedProcess == null)
                {
                    return MainRecordingSettingsActionResult.Failure("Web 설정창 프로세스 시작 결과가 비어 있습니다.");
                }

                return MainRecordingSettingsActionResult.Success("Web 설정창을 실행했습니다.");
            }
            catch (Exception exception)
            {
                return MainRecordingSettingsActionResult.Failure(
                    $"Web 설정창 실행에 실패했습니다: {exception.Message}");
            }
        }

        internal void CloseQuietly()
        {
            Process process = startedProcess;
            startedProcess = null;
            if (process == null)
            {
                return;
            }

            try
            {
                if (!process.HasExited)
                {
                    process.CloseMainWindow();
                }
            }
            catch (Exception exception)
            {
                UnityEngine.Debug.LogWarning(
                    "[MainRecordingSettingsLauncher] Web 설정창 종료 요청에 실패했습니다. " +
                    exception.Message);
            }
            finally
            {
                process.Dispose();
            }
        }

        internal bool IsRunning()
        {
            return IsProcessRunning(startedProcess);
        }

        private static bool IsProcessRunning(Process process)
        {
            if (process == null)
            {
                return false;
            }

            try
            {
                return !process.HasExited;
            }
            catch (Exception)
            {
                return false;
            }
        }

        private static Process LaunchProcess(MainRecordingSettingsLaunchPlan plan)
        {
            return Process.Start(CreateProcessStartInfo(plan));
        }

        private static ProcessStartInfo CreateProcessStartInfo(MainRecordingSettingsLaunchPlan plan)
        {
            var startInfo = new ProcessStartInfo
            {
                FileName = plan.ExecutablePath,
                Arguments = plan.Arguments,
                WorkingDirectory = plan.WorkingDirectory,
                UseShellExecute = false,
                CreateNoWindow = false,
            };
            startInfo.Environment[MainRecordingSettingsPathResolver.EnvironmentVariableName] = plan.SettingsPath;
            startInfo.Environment.Remove(ElectronRunAsNodeEnvironmentVariableName);

            return startInfo;
        }
    }
}
