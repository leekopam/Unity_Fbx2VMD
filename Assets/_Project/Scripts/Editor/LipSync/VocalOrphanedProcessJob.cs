using System;
using System.Diagnostics;
using System.Runtime.InteropServices;

namespace Fbx2Vmd.LipSync
{
    /// <summary>
    /// Windows Job Object에 추적 대상 자식 프로세스를 묶는다.
    /// KILL_ON_JOB_CLOSE로 만들어 에디터 프로세스가 종료·크래시하는 순간
    /// OS가 Job 멤버 전체(자식 프로세스 트리 포함)를 정리한다 —
    /// 마커 소거가 "다음 도메인 초기화"를 기다리는 것과 달리 즉시 동작한다.
    /// Job 핸들은 프로세스 수명 동안 의도적으로 닫지 않는다(닫으면 닫는 시점에
    /// 멤버가 죽거나, KILL 플래그 없이 멤버만 남는다).
    /// 비Windows·할당 실패 환경에서는 조용히 비활성화하고 마커 경로에 맡긴다.
    /// </summary>
    internal static class VocalOrphanedProcessJob
    {
        const uint ExtendedLimitInformationClass = 9;
        const uint JobObjectLimitKillOnJobClose = 0x2000;

        static IntPtr _job = IntPtr.Zero;
        static bool _unavailable;

        /// <summary>프로세스를 킬-온-클로즈 Job에 등록한다.
        /// 성공 시 true — 실패·미지원 환경은 false를 돌려주고 조용히 비활성화한다.</summary>
        internal static bool TryAssign(Process process)
        {
            if (_unavailable || process == null
                || Environment.OSVersion.Platform != PlatformID.Win32NT)
            {
                return false;
            }
            try
            {
                if (_job == IntPtr.Zero)
                {
                    _job = CreateKillOnCloseJob();
                    if (_job == IntPtr.Zero)
                    {
                        _unavailable = true;
                        return false;
                    }
                }
                // 이미 Job에 속한 프로세스(중첩 Job 미지원 환경)·종료된 프로세스의
                // Handle 접근 등은 이 호출만 실패로 돌린다 — 다음 프로세스의
                // 등록까지 막지 않는다.
                return AssignProcessToJobObject(_job, process.Handle);
            }
            catch (Exception)
            {
                return false;
            }
        }

        /// <summary>테스트용 — Job이 실제로 생성 가능한 환경인지.</summary>
        internal static bool IsAvailable
        {
            get
            {
                if (Environment.OSVersion.Platform != PlatformID.Win32NT)
                {
                    return false;
                }
                try
                {
                    if (_job == IntPtr.Zero && !_unavailable)
                    {
                        _job = CreateKillOnCloseJob();
                        if (_job == IntPtr.Zero)
                        {
                            _unavailable = true;
                        }
                    }
                    return _job != IntPtr.Zero;
                }
                catch (Exception)
                {
                    _unavailable = true;
                    return false;
                }
            }
        }

        /// <summary>테스트용 — 프로세스가 어떤 Job에 속하는지 확인한다.</summary>
        internal static bool IsInJob(Process process)
        {
            if (process == null
                || Environment.OSVersion.Platform != PlatformID.Win32NT)
            {
                return false;
            }
            try
            {
                return IsProcessInJob(process.Handle, IntPtr.Zero, out bool inJob)
                    && inJob;
            }
            catch (Exception)
            {
                return false;
            }
        }

        static IntPtr CreateKillOnCloseJob()
        {
            IntPtr job = CreateJobObject(IntPtr.Zero, null);
            if (job == IntPtr.Zero)
            {
                return IntPtr.Zero;
            }
            var limit = new JobObjectExtendedLimitInformation();
            limit.BasicLimitInformation.LimitFlags = JobObjectLimitKillOnJobClose;
            int size = Marshal.SizeOf(limit);
            IntPtr ptr = Marshal.AllocHGlobal(size);
            try
            {
                Marshal.StructureToPtr(limit, ptr, false);
                if (!SetInformationJobObject(
                        job, ExtendedLimitInformationClass, ptr, (uint)size))
                {
                    CloseHandle(job);
                    return IntPtr.Zero;
                }
            }
            finally
            {
                Marshal.FreeHGlobal(ptr);
            }
            return job;
        }

        [StructLayout(LayoutKind.Sequential)]
        struct JobObjectBasicLimitInformation
        {
            public long PerProcessUserTimeLimit;
            public long PerJobUserTimeLimit;
            public uint LimitFlags;
            public UIntPtr MinimumWorkingSetSize;
            public UIntPtr MaximumWorkingSetSize;
            public uint ActiveProcessLimit;
            public UIntPtr Affinity;
            public uint PriorityClass;
            public uint SchedulingClass;
        }

        [StructLayout(LayoutKind.Sequential)]
        struct IoCounters
        {
            public ulong ReadOperationCount;
            public ulong WriteOperationCount;
            public ulong OtherOperationCount;
            public ulong ReadTransferCount;
            public ulong WriteTransferCount;
            public ulong OtherTransferCount;
        }

        [StructLayout(LayoutKind.Sequential)]
        struct JobObjectExtendedLimitInformation
        {
            public JobObjectBasicLimitInformation BasicLimitInformation;
            public IoCounters IoInfo;
            public UIntPtr ProcessMemoryLimit;
            public UIntPtr JobMemoryLimit;
            public UIntPtr PeakProcessMemoryUsed;
            public UIntPtr PeakJobMemoryUsed;
        }

        [DllImport("kernel32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
        static extern IntPtr CreateJobObject(IntPtr attributes, string name);

        [DllImport("kernel32.dll", SetLastError = true)]
        static extern bool SetInformationJobObject(
            IntPtr job, uint infoClass, IntPtr info, uint infoLength);

        [DllImport("kernel32.dll", SetLastError = true)]
        static extern bool AssignProcessToJobObject(IntPtr job, IntPtr process);

        [DllImport("kernel32.dll", SetLastError = true)]
        static extern bool IsProcessInJob(
            IntPtr process, IntPtr job, out bool result);

        [DllImport("kernel32.dll", SetLastError = true)]
        static extern bool CloseHandle(IntPtr handle);
    }
}
