//
// ProgressStatusMonitor.cs
//
// Author:
//   Lluis Sanchez Gual
//
// Copyright (C) 2007 Novell, Inc (http://www.novell.com)
//
// Permission is hereby granted, free of charge, to any person obtaining
// a copy of this software and associated documentation files (the
// "Software"), to deal in the Software without restriction, including
// without limitation the rights to use, copy, modify, merge, publish,
// distribute, sublicense, and/or sell copies of the Software, and to
// permit persons to whom the Software is furnished to do so, subject to
// the following conditions:
// 
// The above copyright notice and this permission notice shall be
// included in all copies or substantial portions of the Software.
// 
// THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND,
// EXPRESS OR IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF
// MERCHANTABILITY, FITNESS FOR A PARTICULAR PURPOSE AND
// NONINFRINGEMENT. IN NO EVENT SHALL THE AUTHORS OR COPYRIGHT HOLDERS BE
// LIABLE FOR ANY CLAIM, DAMAGES OR OTHER LIABILITY, WHETHER IN AN ACTION
// OF CONTRACT, TORT OR OTHERWISE, ARISING FROM, OUT OF OR IN CONNECTION
// WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE SOFTWARE.
//


using System;
using System.IO;
using System.Text;

namespace Mono.Addins.Setup.ProgressMonitoring;

internal class ProgressStatusMonitor : MarshalByRefObject, IProgressMonitor
{
    private readonly StringBuilder logBuffer = new();
    private readonly LogTextWriter logger;
    private readonly IProgressStatus status;
    private readonly ProgressTracker tracker = new();

    public ProgressStatusMonitor(IProgressStatus status)
    {
        this.status = status;
        logger = new LogTextWriter();
        logger.TextWritten += WriteLog;
    }

    public void BeginTask(string name, int totalWork)
    {
        FlushLog();
        tracker.BeginTask(name, totalWork);
        status.SetMessage(tracker.CurrentTask);
        status.SetProgress(tracker.GlobalWork);
    }

    public void BeginStepTask(string name, int totalWork, int stepSize)
    {
        FlushLog();
        tracker.BeginStepTask(name, totalWork, stepSize);
        status.SetMessage(tracker.CurrentTask);
        status.SetProgress(tracker.GlobalWork);
    }

    public void Step(int work)
    {
        FlushLog();
        tracker.Step(work);
        status.SetProgress(tracker.GlobalWork);
    }

    public void EndTask()
    {
        FlushLog();
        tracker.EndTask();
        status.SetMessage(tracker.CurrentTask);
        status.SetProgress(tracker.GlobalWork);
    }

    public TextWriter Log => logger;

    public void ReportWarning(string message)
    {
        FlushLog();
        status.ReportWarning(message);
    }

    public void ReportError(string message, Exception ex)
    {
        FlushLog();
        status.ReportError(message, ex);
    }

    public bool IsCancelRequested => status.IsCanceled;

    public void Cancel()
    {
        FlushLog();
        status.Cancel();
    }

    public int LogLevel => status.LogLevel;

    public void Dispose()
    {
        FlushLog();
    }

    public static IProgressMonitor GetProgressMonitor(IProgressStatus status)
    {
        if (status == null)
            return new NullProgressMonitor();
        return new ProgressStatusMonitor(status);
    }

    private void WriteLog(string text)
    {
        var pi = 0;
        var i = text.IndexOf('\n');
        while (i != -1)
        {
            var line = text.Substring(pi, i - pi);
            if (logBuffer.Length > 0)
            {
                logBuffer.Append(line);
                status.Log(logBuffer.ToString());
                logBuffer.Clear();
            }
            else
            {
                status.Log(line);
            }

            pi = i + 1;
            i = text.IndexOf('\n', pi);
        }

        logBuffer.Append(text, pi, text.Length - pi);
    }

    private void FlushLog()
    {
        if (logBuffer.Length > 0)
        {
            status.Log(logBuffer.ToString());
            logBuffer.Clear();
        }
    }
}