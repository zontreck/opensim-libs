using System;
using System.Globalization;
using System.IO;
using System.Reflection;
using log4net;

namespace DotNetOpenId.Loggers;

internal class Log4NetLogger : ILog
{
    private readonly log4net.ILog log4netLogger;

    private Log4NetLogger(log4net.ILog logger)
    {
        log4netLogger = logger;
    }

    private static bool isLog4NetPresent
    {
        get
        {
            try
            {
                Assembly.Load("log4net");
                return true;
            }
            catch (FileNotFoundException)
            {
                return false;
            }
        }
    }

    /// <summary>
    ///     Returns a new log4net logger if it exists, or returns null if the assembly cannot be found.
    /// </summary>
    internal static ILog Initialize()
    {
        return isLog4NetPresent ? createLogger() : null;
    }

    /// <summary>
    ///     Creates the log4net.LogManager.  Call ONLY once log4net.dll is known to be present.
    /// </summary>
    private static ILog createLogger()
    {
        return new Log4NetLogger(LogManager.GetLogger("DotNetOpenId"));
    }

    #region ILog Members

    public void Debug(object message)
    {
        log4netLogger.Debug(message);
    }

    public void Debug(object message, Exception exception)
    {
        log4netLogger.Debug(message, exception);
    }

    public void DebugFormat(string format, params object[] args)
    {
        log4netLogger.DebugFormat(CultureInfo.InvariantCulture, format, args);
    }

    public void DebugFormat(string format, object arg0)
    {
        log4netLogger.DebugFormat(format, arg0);
    }

    public void DebugFormat(string format, object arg0, object arg1)
    {
        log4netLogger.DebugFormat(format, arg0, arg1);
    }

    public void DebugFormat(string format, object arg0, object arg1, object arg2)
    {
        log4netLogger.DebugFormat(format, arg0, arg1, arg2);
    }

    public void DebugFormat(IFormatProvider provider, string format, params object[] args)
    {
        log4netLogger.DebugFormat(provider, format, args);
    }

    public void Info(object message)
    {
        log4netLogger.Info(message);
    }

    public void Info(object message, Exception exception)
    {
        log4netLogger.Info(message, exception);
    }

    public void InfoFormat(string format, params object[] args)
    {
        log4netLogger.InfoFormat(CultureInfo.InvariantCulture, format, args);
    }

    public void InfoFormat(string format, object arg0)
    {
        log4netLogger.InfoFormat(format, arg0);
    }

    public void InfoFormat(string format, object arg0, object arg1)
    {
        log4netLogger.InfoFormat(format, arg0, arg1);
    }

    public void InfoFormat(string format, object arg0, object arg1, object arg2)
    {
        log4netLogger.InfoFormat(format, arg0, arg1, arg2);
    }

    public void InfoFormat(IFormatProvider provider, string format, params object[] args)
    {
        log4netLogger.InfoFormat(provider, format, args);
    }

    public void Warn(object message)
    {
        log4netLogger.Warn(message);
    }

    public void Warn(object message, Exception exception)
    {
        log4netLogger.Warn(message, exception);
    }

    public void WarnFormat(string format, params object[] args)
    {
        log4netLogger.WarnFormat(CultureInfo.InvariantCulture, format, args);
    }

    public void WarnFormat(string format, object arg0)
    {
        log4netLogger.WarnFormat(format, arg0);
    }

    public void WarnFormat(string format, object arg0, object arg1)
    {
        log4netLogger.WarnFormat(format, arg0, arg1);
    }

    public void WarnFormat(string format, object arg0, object arg1, object arg2)
    {
        log4netLogger.WarnFormat(format, arg0, arg1, arg2);
    }

    public void WarnFormat(IFormatProvider provider, string format, params object[] args)
    {
        log4netLogger.WarnFormat(provider, format, args);
    }

    public void Error(object message)
    {
        log4netLogger.Error(message);
    }

    public void Error(object message, Exception exception)
    {
        log4netLogger.Error(message, exception);
    }

    public void ErrorFormat(string format, params object[] args)
    {
        log4netLogger.ErrorFormat(CultureInfo.InvariantCulture, format, args);
    }

    public void ErrorFormat(string format, object arg0)
    {
        log4netLogger.ErrorFormat(format, arg0);
    }

    public void ErrorFormat(string format, object arg0, object arg1)
    {
        log4netLogger.ErrorFormat(format, arg0, arg1);
    }

    public void ErrorFormat(string format, object arg0, object arg1, object arg2)
    {
        log4netLogger.ErrorFormat(format, arg0, arg1, arg2);
    }

    public void ErrorFormat(IFormatProvider provider, string format, params object[] args)
    {
        log4netLogger.ErrorFormat(provider, format, args);
    }

    public void Fatal(object message)
    {
        log4netLogger.Fatal(message);
    }

    public void Fatal(object message, Exception exception)
    {
        log4netLogger.Fatal(message, exception);
    }

    public void FatalFormat(string format, params object[] args)
    {
        log4netLogger.FatalFormat(CultureInfo.InvariantCulture, format, args);
    }

    public void FatalFormat(string format, object arg0)
    {
        log4netLogger.FatalFormat(format, arg0);
    }

    public void FatalFormat(string format, object arg0, object arg1)
    {
        log4netLogger.FatalFormat(format, arg0, arg1);
    }

    public void FatalFormat(string format, object arg0, object arg1, object arg2)
    {
        log4netLogger.FatalFormat(format, arg0, arg1, arg2);
    }

    public void FatalFormat(IFormatProvider provider, string format, params object[] args)
    {
        log4netLogger.FatalFormat(provider, format, args);
    }

    public bool IsDebugEnabled => log4netLogger.IsDebugEnabled;

    public bool IsInfoEnabled => log4netLogger.IsInfoEnabled;

    public bool IsWarnEnabled => log4netLogger.IsWarnEnabled;

    public bool IsErrorEnabled => log4netLogger.IsErrorEnabled;

    public bool IsFatalEnabled => log4netLogger.IsFatalEnabled;

    #endregion
}