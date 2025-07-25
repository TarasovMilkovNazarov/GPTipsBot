namespace GPTipsBot.Logging;

public class TelegramErrorReportException(Exception innerException) : Exception(null, innerException);