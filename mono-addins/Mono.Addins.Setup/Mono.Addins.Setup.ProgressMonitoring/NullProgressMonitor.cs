//
// NullProgressMonitor.cs
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
using System.Collections.Generic;
using System.IO;

namespace Mono.Addins.Setup.ProgressMonitoring;

internal class NullProgressMonitor : MarshalByRefObject, IProgressMonitor
{
    private bool done;
    private List<ProgressError> errors;
    private List<string> messages;
    private List<string> warnings;

    public string[] Messages
    {
        get
        {
            if (messages != null)
                return messages.ToArray();
            return new string [0];
        }
    }

    public string[] Warnings
    {
        get
        {
            if (warnings != null)
                return warnings.ToArray();
            return new string [0];
        }
    }

    public ProgressError[] Errors
    {
        get
        {
            if (errors != null)
                return errors.ToArray();
            return new ProgressError [0];
        }
    }

    public virtual void BeginTask(string name, int totalWork)
    {
    }

    public virtual void EndTask()
    {
    }

    public virtual void BeginStepTask(string name, int totalWork, int stepSize)
    {
    }

    public virtual void Step(int work)
    {
    }

    public virtual TextWriter Log => TextWriter.Null;

    public virtual void ReportWarning(string message)
    {
        if (warnings == null)
            warnings = new List<string>();
        messages.Add(message);
    }

    public virtual void ReportError(string message, Exception ex)
    {
        if (errors == null)
            errors = new List<ProgressError>();

        if (message == null && ex != null)
        {
            message = ex.Message;
        }
        else if (message != null && ex != null)
        {
            if (!message.EndsWith(".")) message += ".";
            message += " " + ex.Message;
        }

        errors.Add(new ProgressError(message, ex));
    }

    public bool IsCancelRequested { get; private set; }

    public void Cancel()
    {
        IsCancelRequested = true;
    }

    public virtual int LogLevel => 1;

    public virtual void Dispose()
    {
        lock (this)
        {
            if (done) return;
            done = true;
        }

        OnCompleted();
    }

    public virtual void ReportSuccess(string message)
    {
        if (messages == null)
            messages = new List<string>();
        messages.Add(message);
    }

    protected virtual void OnCompleted()
    {
    }
}

internal class ProgressError
{
    public ProgressError(string message, Exception ex)
    {
        Exception = ex;
        Message = message;
    }

    public string Message { get; }

    public Exception Exception { get; }
}