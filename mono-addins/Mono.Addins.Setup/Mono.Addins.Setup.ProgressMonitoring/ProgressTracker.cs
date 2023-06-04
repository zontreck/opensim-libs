//
// ProgressTracker.cs
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

namespace Mono.Addins.Setup.ProgressMonitoring;

internal class ProgressTracker
{
    private bool done;

    private readonly List<Task> tasks = new();

    private Task LastTask => tasks[tasks.Count - 1];

    public string CurrentTask
    {
        get
        {
            if (tasks.Count == 0) return null;
            return LastTask.Name;
        }
    }

    public double CurrentTaskWork
    {
        get
        {
            if (tasks.Count == 0) return 0;
            return LastTask.GetWorkPercent(0);
        }
    }

    public bool UnknownWork
    {
        get
        {
            if (tasks.Count == 0) return false;
            return LastTask.TotalWork <= 1;
        }
    }

    public double GlobalWork
    {
        get
        {
            if (done) return 1.0;

            double work = 0;
            double totalSize = 0;
            for (var n = tasks.Count - 1; n >= 0; n--)
            {
                var t = tasks[n];
                work += Math.Max(0, t.GetWorkPercent(work) * t.StepSize);
                totalSize += t.StepSize;
            }

            if (totalSize > 0)
                work /= totalSize;
            return Math.Min(1.0, work);
        }
    }

    public bool InProgress => !done;

    public void Reset()
    {
        done = false;
        tasks.Clear();
    }

    public void BeginTask(string name, int totalWork)
    {
        var t = new Task();
        t.Name = name;
        t.TotalWork = totalWork;
        tasks.Add(t);
    }

    public void BeginStepTask(string name, int totalWork, int stepSize)
    {
        var t = new Task();
        t.StepSize = stepSize;
        t.IsStep = true;
        t.Name = name;
        t.TotalWork = totalWork;
        tasks.Add(t);
    }

    public void EndTask()
    {
        if (tasks.Count > 0)
        {
            var t = LastTask;
            tasks.RemoveAt(tasks.Count - 1);
            if (t.IsStep)
                Step(t.StepSize);
        }
    }

    public void Step(int work)
    {
        if (tasks.Count == 0) return;
        var t = LastTask;
        t.CurrentWork += work;
        if (t.CurrentWork > t.TotalWork)
            t.CurrentWork = t.TotalWork;
    }

    public void Done()
    {
        done = true;
        tasks.Clear();
    }

    private class Task
    {
        public int CurrentWork;
        public bool IsStep;
        public string Name;
        public int StepSize = 1;
        public int TotalWork;

        public double GetWorkPercent(double part)
        {
            if (TotalWork <= 0) return 0;
            if (CurrentWork >= TotalWork) return 1.0;
            return (CurrentWork + part) / TotalWork;
        }
    }
}