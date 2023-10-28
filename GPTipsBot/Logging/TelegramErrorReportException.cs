namespace GPTipsBot.Logging;

public class TelegramErrorReportException : Exception
{
    public TelegramErrorReportException(Exception innerException) : base(null, innerException)
    {
    }
}