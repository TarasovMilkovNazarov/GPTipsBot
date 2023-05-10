using Microsoft.Extensions.Logging;
using System.Diagnostics;
using System.Text;

namespace GPTipsBot.Extensions
{


    public static class LoggerExtensions
    {
        public static void LogWithStackTrace(this ILogger logger, LogLevel logLevel, string message, int methodCount = 20)
        {
            if (logger.IsEnabled(logLevel))
            {
                StackTrace stackTrace = new StackTrace();
                StringBuilder sb = new StringBuilder();
                sb.AppendLine(message);
            
                // Limit the stack trace to the specified number of methods
                int frameCount = Math.Min(methodCount, stackTrace.FrameCount);
                for (int i = 0; i < frameCount; i++)
                {
                    StackFrame frame = stackTrace.GetFrame(i);
                    // Append information about the method (e.g., frame.GetMethod(), frame.GetFileName())
                    sb.AppendLine($"Method: {frame.GetMethod()}"); // Modify this line to fit your desired output
                }
            
                logger.Log(logLevel, sb.ToString());
            }
        }
    }

}
