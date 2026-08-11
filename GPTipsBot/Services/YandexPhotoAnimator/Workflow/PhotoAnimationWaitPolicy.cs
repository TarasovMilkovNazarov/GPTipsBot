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

    public static string FormatRemainingTimeEs(int remainingSeconds)
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

        return "menos de un minuto";
    }

    public static string FormatRemainingTimeFa(int remainingSeconds)
    {
        if (remainingSeconds >= 3600)
        {
            var hours = remainingSeconds / 3600;
            var minutes = (remainingSeconds % 3600 + 29) / 60;
            return minutes > 0 ? $"~{hours} ساعت {minutes} دقیقه" : $"~{hours} ساعت";
        }

        if (remainingSeconds >= 60)
        {
            return $"~{(remainingSeconds + 29) / 60} دقیقه";
        }

        return "کمتر از یک دقیقه";
    }

    public static string FormatRemainingTimeAr(int remainingSeconds)
    {
        if (remainingSeconds >= 3600)
        {
            var hours = remainingSeconds / 3600;
            var minutes = (remainingSeconds % 3600 + 29) / 60;
            return minutes > 0 ? $"~{hours} س {minutes} د" : $"~{hours} س";
        }

        if (remainingSeconds >= 60)
        {
            return $"~{(remainingSeconds + 29) / 60} د";
        }

        return "أقل من دقيقة";
    }

    public static string FormatRemainingTimeForCulture(int remainingSeconds, string? twoLetterLanguage)
    {
        return twoLetterLanguage switch
        {
            "en" => FormatRemainingTimeEn(remainingSeconds),
            "es" => FormatRemainingTimeEs(remainingSeconds),
            "fa" => FormatRemainingTimeFa(remainingSeconds),
            "ar" => FormatRemainingTimeAr(remainingSeconds),
            _ => FormatRemainingTime(remainingSeconds),
        };
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
