using System;

internal readonly struct HumanoidRecordingTick
{
    public readonly float DisplayTimeSeconds;
    public readonly float Progress01;
    public readonly bool ShouldFinish;

    public HumanoidRecordingTick(float displayTimeSeconds, float progress01, bool shouldFinish)
    {
        DisplayTimeSeconds = displayTimeSeconds;
        Progress01 = Clamp01(progress01);
        ShouldFinish = shouldFinish;
    }

    private static float Clamp01(float value)
    {
        if (value < 0f) return 0f;
        if (value > 1f) return 1f;
        return value;
    }
}

internal sealed class HumanoidRecordingSession
{
    private readonly float _recordingFrameRate;

    public bool IsActive { get; private set; }
    public float TotalDurationSeconds { get; private set; }
    public float CurrentTimerSeconds { get; private set; }
    public int TargetFrameCount { get; private set; }
    public bool FinishByRecordedFrameCount { get; private set; }

    public HumanoidRecordingSession(float recordingFrameRate)
    {
        if (recordingFrameRate <= 0f || float.IsNaN(recordingFrameRate) || float.IsInfinity(recordingFrameRate))
        {
            throw new ArgumentOutOfRangeException(nameof(recordingFrameRate), recordingFrameRate, "Recording frame rate must be positive.");
        }

        _recordingFrameRate = recordingFrameRate;
    }

    public void Start(float totalDurationSeconds, int targetFrameCount, bool finishByRecordedFrameCount)
    {
        IsActive = true;
        TotalDurationSeconds = Math.Max(0f, totalDurationSeconds);
        TargetFrameCount = Math.Max(0, targetFrameCount);
        FinishByRecordedFrameCount = finishByRecordedFrameCount && TargetFrameCount > 0;
        CurrentTimerSeconds = 0f;
    }

    public void Stop()
    {
        IsActive = false;
    }

    public HumanoidRecordingTick Tick(float deltaTimeSeconds, int recordedFrameNumber)
    {
        if (!IsActive)
        {
            return new HumanoidRecordingTick(
                displayTimeSeconds: 0f,
                progress01: 0f,
                shouldFinish: false);
        }

        CurrentTimerSeconds += Math.Max(0f, deltaTimeSeconds);

        float displayTimeSeconds = FinishByRecordedFrameCount
            ? recordedFrameNumber / _recordingFrameRate
            : CurrentTimerSeconds;

        float progress01 = FinishByRecordedFrameCount && TargetFrameCount > 0
            ? (float)recordedFrameNumber / TargetFrameCount
            : (TotalDurationSeconds > 0f ? CurrentTimerSeconds / TotalDurationSeconds : 0f);

        bool shouldFinish = FinishByRecordedFrameCount && TargetFrameCount > 0
            ? recordedFrameNumber >= TargetFrameCount
            : CurrentTimerSeconds >= TotalDurationSeconds;

        return new HumanoidRecordingTick(
            displayTimeSeconds: displayTimeSeconds,
            progress01: progress01,
            shouldFinish: shouldFinish);
    }
}

