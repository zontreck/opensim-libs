//
// AddinScanFolderInfo.cs
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
using System.Collections;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.IO;
using Mono.Addins.Serialization;

namespace Mono.Addins.Database;

internal class AddinScanFolderInfo : IBinaryXmlElement
{
    private static readonly BinaryXmlTypeMap typeMap = new(
        typeof(AddinScanFolderInfo),
        typeof(AddinFileInfo)
    );

    private readonly Hashtable files = new();

    internal AddinScanFolderInfo()
    {
    }

    public AddinScanFolderInfo(string folder)
    {
        this.Folder = folder;
    }

    public AddinScanFolderInfo(AddinScanFolderInfo other)
    {
        files = new Hashtable(other.files);
        Folder = other.Folder;
        FileName = other.FileName;
        RootsDomain = other.RootsDomain;
        SharedFolder = other.SharedFolder;
        FolderHasScanDataIndex = other.FolderHasScanDataIndex;
    }

    public string FileName { get; private set; }

    public string Folder { get; private set; }

    public string Domain
    {
        get
        {
            if (SharedFolder)
                return AddinDatabase.GlobalDomain;
            return RootsDomain;
        }
        set
        {
            RootsDomain = value;
            SharedFolder = true;
        }
    }

    public string RootsDomain { get; set; }

    public bool SharedFolder { get; set; } = true;

    public bool FolderHasScanDataIndex { get; set; }

    void IBinaryXmlElement.Write(BinaryXmlWriter writer)
    {
        if (files.Count == 0)
        {
            RootsDomain = null;
            SharedFolder = true;
        }

        writer.WriteValue("folder", Folder);
        writer.WriteValue("files", files);
        writer.WriteValue("domain", RootsDomain);
        writer.WriteValue("sharedFolder", SharedFolder);
        writer.WriteValue("folderHasDataIndex", FolderHasScanDataIndex);
    }

    void IBinaryXmlElement.Read(BinaryXmlReader reader)
    {
        Folder = reader.ReadStringValue("folder");
        reader.ReadValue("files", files);
        RootsDomain = reader.ReadStringValue("domain");
        SharedFolder = reader.ReadBooleanValue("sharedFolder");
        FolderHasScanDataIndex = reader.ReadBooleanValue("folderHasDataIndex");
    }

    public static AddinScanFolderInfo Read(FileDatabase filedb, string file)
    {
        var finfo = (AddinScanFolderInfo)filedb.ReadSharedObject(file, typeMap);
        if (finfo != null)
            finfo.FileName = file;
        return finfo;
    }

    public static AddinScanFolderInfo Read(FileDatabase filedb, string basePath, string folderPath)
    {
        string fileName;
        var finfo = (AddinScanFolderInfo)filedb.ReadSharedObject(basePath, GetDomain(folderPath), ".data",
            Path.GetFullPath(folderPath), typeMap, out fileName);
        if (finfo != null)
            finfo.FileName = fileName;
        return finfo;
    }

    internal static string GetDomain(string path)
    {
        path = Path.GetFullPath(path);
        var s = path.Replace(Path.DirectorySeparatorChar, '_');
        s = s.Replace(Path.AltDirectorySeparatorChar, '_');
        s = s.Replace(Path.VolumeSeparatorChar, '_');
        s = s.Trim('_');
        if (Util.IsWindows) s = s.ToLowerInvariant();

        return s;
    }

    public void Write(FileDatabase filedb, string basePath)
    {
        filedb.WriteSharedObject(basePath, GetDomain(Folder), ".data", Path.GetFullPath(Folder), FileName, typeMap,
            this);
    }

    public string GetExistingLocalDomain()
    {
        foreach (AddinFileInfo info in files.Values)
            if (info.Domain != null && info.Domain != AddinDatabase.GlobalDomain)
                return info.Domain;
        return AddinDatabase.GlobalDomain;
    }

    public string GetDomain(bool isRoot)
    {
        if (isRoot)
            return RootsDomain;
        return Domain;
    }

    public void Reset()
    {
        files.Clear();
    }

    public DateTime GetLastScanTime(string file)
    {
        var info = (AddinFileInfo)files[file];
        if (info == null)
            return DateTime.MinValue;
        return info.LastScan;
    }

    public AddinFileInfo GetAddinFileInfo(string file)
    {
        return (AddinFileInfo)files[file];
    }

    public AddinFileInfo SetLastScanTime(string file, string addinId, bool isRoot, DateTime time, bool scanError,
        string scanDataMD5 = null)
    {
        var info = (AddinFileInfo)files[file];
        if (info == null)
        {
            info = new AddinFileInfo();
            info.File = file;
            files[file] = info;
        }

        info.LastScan = time;
        info.AddinId = addinId;
        info.IsRoot = isRoot;
        info.ScanError = scanError;
        info.ScanDataMD5 = scanDataMD5;
        if (addinId != null)
            info.Domain = GetDomain(isRoot);
        else
            info.Domain = null;
        return info;
    }

    public List<AddinFileInfo> GetMissingAddins(AddinFileSystemExtension fs)
    {
        var missing = new List<AddinFileInfo>();

        if (!fs.DirectoryExists(Folder))
        {
            // All deleted
            foreach (AddinFileInfo info in files.Values)
                if (info.IsAddin)
                    missing.Add(info);
            files.Clear();
            return missing;
        }

        var toDelete = new List<string>();
        foreach (AddinFileInfo info in files.Values)
            if (!fs.FileExists(info.File))
            {
                if (info.IsAddin)
                    missing.Add(info);
                toDelete.Add(info.File);
            }
            else if (info.IsAddin && info.Domain != GetDomain(info.IsRoot))
            {
                missing.Add(info);
            }

        foreach (var file in toDelete)
            files.Remove(file);

        return missing;
    }
}

internal class AddinFileInfo : IBinaryXmlElement
{
    public string AddinId;
    public string Domain;
    public string File;
    public StringCollection IgnorePaths;
    public bool IsRoot;
    public DateTime LastScan;
    public string ScanDataMD5;
    public bool ScanError;

    public bool IsAddin => AddinId != null && AddinId.Length != 0;

    void IBinaryXmlElement.Write(BinaryXmlWriter writer)
    {
        writer.WriteValue("File", File);
        writer.WriteValue("LastScan", LastScan);
        writer.WriteValue("AddinId", AddinId);
        writer.WriteValue("IsRoot", IsRoot);
        writer.WriteValue("ScanError", ScanError);
        writer.WriteValue("Domain", Domain);
        writer.WriteValue("IgnorePaths", IgnorePaths);
        writer.WriteValue("MD5", ScanDataMD5);
    }

    void IBinaryXmlElement.Read(BinaryXmlReader reader)
    {
        File = reader.ReadStringValue("File");
        LastScan = reader.ReadDateTimeValue("LastScan");
        AddinId = reader.ReadStringValue("AddinId");
        IsRoot = reader.ReadBooleanValue("IsRoot");
        ScanError = reader.ReadBooleanValue("ScanError");
        Domain = reader.ReadStringValue("Domain");
        IgnorePaths = (StringCollection)reader.ReadValue("IgnorePaths", new StringCollection());
        ScanDataMD5 = reader.ReadStringValue("MD5");
    }

    public void AddPathToIgnore(string path)
    {
        if (IgnorePaths == null)
            IgnorePaths = new StringCollection();
        IgnorePaths.Add(path);
    }

    public bool HasChanged(AddinFileSystemExtension fs, string md5)
    {
        // Special case: if an md5 is stored, this method can only return a valid result
        // if compared with another md5. If no md5 is provided for comparison, then always consider
        // the file to be changed.

        if (ScanDataMD5 != null)
            return md5 != ScanDataMD5;

        return fs.GetLastWriteTime(File) != LastScan;
    }
}