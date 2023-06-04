//
// Repository.cs
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
using System.Threading;
using System.Xml.Serialization;

namespace Mono.Addins.Setup;

public class Repository
{
    private RepositoryEntryCollection addins;
    private RepositoryEntryCollection repositories;
    internal string url;

    public string Name { get; set; }

    public string Url
    {
        get => url;
        set => url = value;
    }

    internal string CachedFilesDir { get; set; }

    [XmlElement("Repository", Type = typeof(ReferenceRepositoryEntry))]
    public RepositoryEntryCollection Repositories
    {
        get
        {
            if (repositories == null)
                repositories = new RepositoryEntryCollection(this);
            return repositories;
        }
    }

    [XmlElement("Addin", Type = typeof(PackageRepositoryEntry))]
    public RepositoryEntryCollection Addins
    {
        get
        {
            if (addins == null)
                addins = new RepositoryEntryCollection(this);
            return addins;
        }
    }

    public RepositoryEntry FindEntry(string url)
    {
        if (Repositories != null)
            foreach (RepositoryEntry e in Repositories)
                if (e.Url == url)
                    return e;
        if (Addins != null)
            foreach (RepositoryEntry e in Addins)
                if (e.Url == url)
                    return e;
        return null;
    }

    public void AddEntry(RepositoryEntry entry)
    {
        entry.owner = this;
        if (entry is ReferenceRepositoryEntry)
            Repositories.Add(entry);
        else
            Addins.Add(entry);
    }

    public void RemoveEntry(RepositoryEntry entry)
    {
        if (entry is PackageRepositoryEntry)
            Addins.Remove(entry);
        else
            Repositories.Remove(entry);
    }

    public IAsyncResult BeginDownloadSupportFile(string name, AsyncCallback cb, object state)
    {
        var res = new FileAsyncResult();
        res.AsyncState = state;
        res.Callback = cb;

        var cachedFile = Path.Combine(CachedFilesDir, Path.GetFileName(name));
        if (File.Exists(cachedFile))
        {
            res.FilePath = cachedFile;
            res.CompletedSynchronously = true;
            res.SetDone();
            return res;
        }

        var u = new Uri(new Uri(Url), name);
        if (u.Scheme == "file")
        {
            res.FilePath = u.AbsolutePath;
            res.CompletedSynchronously = true;
            res.SetDone();
            return res;
        }

        res.FilePath = cachedFile;
        var request = DownloadFileRequest.DownloadFile(u.ToString(), false).ContinueWith(t =>
        {
            try
            {
                using (var resp = t.Result)
                {
                    var dir = Path.GetDirectoryName(res.FilePath);
                    lock (this)
                    {
                        if (!Directory.Exists(dir))
                            Directory.CreateDirectory(dir);
                    }

                    if (File.Exists(res.FilePath))
                    {
                        res.SetDone();
                        return;
                    }

                    var buffer = new byte [8092];
                    using (var s = resp.Stream)
                    {
                        using (var f = File.OpenWrite(res.FilePath))
                        {
                            var nr = 0;
                            while ((nr = s.Read(buffer, 0, buffer.Length)) > 0)
                                f.Write(buffer, 0, nr);
                        }
                    }

                    res.SetDone();
                }
            }
            catch (Exception ex)
            {
                res.Error = ex;
            }
        });
        return res;
    }

    public Stream EndDownloadSupportFile(IAsyncResult ares)
    {
        var res = ares as FileAsyncResult;
        if (res == null)
            throw new InvalidOperationException("Invalid IAsyncResult instance");
        if (res.Error != null)
            throw res.Error;
        return File.OpenRead(res.FilePath);
    }
}

internal class FileAsyncResult : IAsyncResult
{
    public AsyncCallback Callback;
    private ManualResetEvent done;
    public Exception Error;

    public string FilePath;

    public object AsyncState { get; set; }

    public WaitHandle AsyncWaitHandle
    {
        get
        {
            lock (this)
            {
                if (done == null)
                    done = new ManualResetEvent(IsCompleted);
            }

            return done;
        }
    }

    public bool CompletedSynchronously { get; set; }

    public bool IsCompleted { get; set; }

    public void SetDone()
    {
        lock (this)
        {
            IsCompleted = true;
            if (done != null)
                done.Set();
        }

        if (Callback != null)
            Callback(this);
    }
}