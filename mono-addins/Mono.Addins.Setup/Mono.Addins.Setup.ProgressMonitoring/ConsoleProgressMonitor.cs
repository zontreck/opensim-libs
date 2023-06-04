//
// ConsoleProgressMonitor.cs
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

namespace Mono.Addins.Setup.ProgressMonitoring;

internal class ConsoleProgressMonitor : NullProgressMonitor
{
    private int col = -1;
    private int ilevel;
    private readonly int isize = 3;
    private readonly LogTextWriter logger;
    private readonly int logLevel;

    public ConsoleProgressMonitor() : this(1)
    {
    }

    public ConsoleProgressMonitor(int logLevel)
    {
        this.logLevel = logLevel;
        logger = new LogTextWriter();
        logger.TextWritten += WriteLog;
    }

    public bool WrapText { get; set; } = true;

    public int WrapColumns { get; set; } = 80;

    public bool IndentTasks { get; set; } = true;

    public override int LogLevel => logLevel;

    public override TextWriter Log => logger;

    public override void BeginTask(string name, int totalWork)
    {
        WriteText(name);
        Indent();
    }

    public override void BeginStepTask(string name, int totalWork, int stepSize)
    {
        BeginTask(name, totalWork);
    }

    public override void EndTask()
    {
        Unindent();
    }

    private void WriteLog(string text)
    {
        WriteText(text);
    }

    public override void ReportSuccess(string message)
    {
        WriteText(message);
    }

    public override void ReportWarning(string message)
    {
        if (logLevel != 0)
            WriteText("WARNING: " + message + "\n");
    }

    public override void ReportError(string message, Exception ex)
    {
        if (logLevel == 0)
            return;

        if (message != null && ex != null)
        {
            WriteText("ERROR: " + message + "\n");
            if (logLevel > 1)
                WriteText(ex + "\n");
        }

        if (message != null)
        {
            WriteText("ERROR: " + message + "\n");
        }
        else if (ex != null)
        {
            if (logLevel > 1)
                WriteText("ERROR: " + ex + "\n");
            else
                WriteText("ERROR: " + ex.Message + "\n");
        }
    }

    private void WriteText(string text)
    {
        if (IndentTasks)
            WriteText(text, ilevel);
        else
            WriteText(text, 0);
    }

    private void WriteText(string text, int leftMargin)
    {
        if (text == null || text.Length == 0)
            return;

        var n = 0;
        var maxCols = WrapText ? WrapColumns : int.MaxValue;

        while (n < text.Length)
        {
            if (col == -1)
            {
                Console.Write(new string(' ', leftMargin));
                col = leftMargin;
            }

            var lastWhite = -1;
            var sn = n;
            var eol = false;

            while (col < maxCols && n < text.Length)
            {
                var c = text[n];
                if (c == '\r')
                {
                    n++;
                    continue;
                }

                if (c == '\n')
                {
                    eol = true;
                    break;
                }

                if (char.IsWhiteSpace(c))
                    lastWhite = n;
                col++;
                n++;
            }

            if (lastWhite == -1 || col < maxCols)
                lastWhite = n;
            else if (col >= maxCols)
                n = lastWhite + 1;

            Console.Write(text.Substring(sn, lastWhite - sn));

            if (eol || col >= maxCols)
            {
                col = -1;
                Console.WriteLine();
                if (eol) n++;
            }
        }
    }

    private void Indent()
    {
        ilevel += isize;
        if (col != -1)
        {
            Console.WriteLine();
            col = -1;
        }
    }

    private void Unindent()
    {
        ilevel -= isize;
        if (ilevel < 0) ilevel = 0;
        if (col != -1)
        {
            Console.WriteLine();
            col = -1;
        }
    }
}