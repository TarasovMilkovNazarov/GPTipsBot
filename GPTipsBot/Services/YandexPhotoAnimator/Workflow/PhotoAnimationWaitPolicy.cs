namespace GPTipsBot.Services.YandexPhotoAnimator.Workflow;

internal static class PhotoAnimationWaitPolicy
{
    private const int MinPollIntervalSec = 10;
    private const int MaxPollIntervalSec = 30;
    public const int MinEstimatedSeconds = 30;
    private const double TimeoutMarginFactor = 1.5;
    private const int TimeoutFixedBufferSec = 180;

    public static int CalculateTimeoutSeconds(int initialRemainingSec)
    {
        var estimate = Math.Max(initialRemainingSec, MinEstimatedSeconds);
        return (int)(estimate * TimeoutMarginFactor) + TimeoutFixedBufferSec;
    }

    public static int GetPollIntervalSeconds(int remainingSeconds)
        => Math.Clamp(remainingSeconds / 6, MinPollIntervalSec, MaxPollIntervalSec);

    public static string FormatRemainingTime(int remainingSeconds)
    {
        if (remainingSeconds >= 3600)
        {
            var hours = remainingSeconds / 3600;
            var minutes = (remainingSeconds % 3600 + 29) / 60;
            return minutes > 0 ? $"~{hours} ч {minutes} мин" : $"~{hours} ч";
        }

        if (remainingSeconds >= 60)
        {
            return $"~{(remainingSeconds + 29) / 60} мин";
        }

        return "меньше минуты";
    }

    public static string FormatRemainingTimeEn(int remainingSeconds)
    {
        if (remainingSeconds >= 3600)
        {
            var hours = remainingSeconds / 3600;
            var minutes = (remainingSeconds % 3600 + 29) / 60;
            return minutes > 0 ? $"~{hours} h {minutes} min" : $"~{hours} h";
        }

        if (remainingSeconds >= 60)
        {
            return $"~{(remainingSeconds + 29) / 60} min";
        }

        return "less than a minute";
    }

    public static bool ShouldUpdateProgress(int previousRemainingSec, int currentRemainingSec)
    {
        if (previousRemainingSec <= 0)
        {
            return true;
        }

        var previousMinutes = (previousRemainingSec + 29) / 60;
        var currentMinutes = (currentRemainingSec + 29) / 60;
        if (previousMinutes != currentMinutes)
        {
            return true;
        }

        return Math.Abs(previousRemainingSec - currentRemainingSec) >= 30;
    }
}
