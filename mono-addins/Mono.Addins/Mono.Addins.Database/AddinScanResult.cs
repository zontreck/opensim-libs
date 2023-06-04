//
// AddinScanResult.cs
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
using System.IO;

namespace Mono.Addins.Database;

internal class AddinScanResult : MarshalByRefObject
{
    internal List<string> AddinsToScan = new();
    internal List<string> AddinsToUpdate = new();
    internal List<string> AddinsToUpdateRelations = new();
    public bool CheckOnly;
    public string Domain;
    internal List<FileToScan> FilesToScan = new();
    internal AddinHostIndex HostIndex;
    public bool LocateAssembliesOnly;
    internal List<AddinScanFolderInfo> ModifiedFolderInfos = new();

    public bool RegenerateAllData;

    private bool regenerateRelationData;
    internal List<string> RemovedAddins = new();

    public ScanContext ScanContext { get; } = new();

    public AssemblyIndex AssemblyIndex { get; } = new();

    public bool CleanGeneratedAddinScanDataFiles { get; set; }

    public bool ChangesFound { get; set; }

    public bool RegenerateRelationData
    {
        get => regenerateRelationData;
        set
        {
            regenerateRelationData = value;
            if (value)
                ChangesFound = true;
        }
    }

    public void AddAddinToScan(string addinId)
    {
        if (!AddinsToScan.Contains(addinId))
            AddinsToScan.Add(addinId);
    }

    public void AddRemovedAddin(string addinId)
    {
        if (!RemovedAddins.Contains(addinId))
            RemovedAddins.Add(addinId);
    }

    public void AddFileToScan(string file, AddinScanFolderInfo folderInfo, AddinFileInfo oldFileInfo,
        AddinScanData scanData)
    {
        var di = new FileToScan();
        di.File = file;
        di.AddinScanFolderInfo = folderInfo;
        di.OldFileInfo = oldFileInfo;
        di.ScanDataMD5 = scanData?.MD5;
        FilesToScan.Add(di);
        RegisterModifiedFolderInfo(folderInfo);
    }

    public void RegisterModifiedFolderInfo(AddinScanFolderInfo folderInfo)
    {
        if (!ModifiedFolderInfos.Contains(folderInfo))
            ModifiedFolderInfos.Add(folderInfo);
    }

    public void AddAddinToUpdateRelations(string addinId)
    {
        if (!AddinsToUpdateRelations.Contains(addinId))
            AddinsToUpdateRelations.Add(addinId);
    }

    public void AddAddinToUpdate(string addinId)
    {
        if (!AddinsToUpdate.Contains(addinId))
            AddinsToUpdate.Add(addinId);
    }
}

internal class FileToScan
{
    public AddinScanFolderInfo AddinScanFolderInfo;
    public string File;
    public AddinFileInfo OldFileInfo;
    public string ScanDataMD5;
}

internal class ScanContext
{
    private HashSet<string> filesToIgnore;

    public void AddPathToIgnore(string path)
    {
        if (filesToIgnore == null)
            filesToIgnore = new HashSet<string>();
        filesToIgnore.Add(path);
    }

    public bool IgnorePath(string file)
    {
        if (filesToIgnore == null)
            return false;
        var root = Path.GetPathRoot(file);
        while (root != file)
        {
            if (filesToIgnore.Contains(file))
                return true;
            file = Path.GetDirectoryName(file);
        }

        return false;
    }

    public void AddPathsToIgnore(IEnumerable paths)
    {
        foreach (string p in paths)
            AddPathToIgnore(p);
    }
}