// 
// PcFileCache.cs
//  
// Author:
//       Lluis Sanchez Gual <lluis@novell.com>
// 
// Copyright (c) 2009 Novell, Inc (http://www.novell.com)
// 
// Permission is hereby granted, free of charge, to any person obtaining a copy
// of this software and associated documentation files (the "Software"), to deal
// in the Software without restriction, including without limitation the rights
// to use, copy, modify, merge, publish, distribute, sublicense, and/or sell
// copies of the Software, and to permit persons to whom the Software is
// furnished to do so, subject to the following conditions:
// 
// The above copyright notice and this permission notice shall be included in
// all copies or substantial portions of the Software.
// 
// THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
// IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
// FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
// AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
// LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
// OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN
// THE SOFTWARE.

using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Threading;
using System.Xml;

namespace Mono.PkgConfig;

internal interface IPcFileCacheContext<TP> where TP : PackageInfo, new()
{
    // In the implementation of this method, the host application can extract
    // information from the pc file and store it in the PackageInfo object
    void StoreCustomData(PcFile pcfile, TP pkg);

    // Should return false if the provided package does not have required
    // custom data
    bool IsCustomDataComplete(string pcfile, TP pkg);

    // Called to report errors
    void ReportError(string message, Exception ex);
}

internal interface IPcFileCacheContext : IPcFileCacheContext<PackageInfo>
{
}

internal abstract class PcFileCache : PcFileCache<PackageInfo>
{
    public PcFileCache(IPcFileCacheContext ctx) : base(ctx)
    {
    }
}

internal abstract class PcFileCache<TP> where TP : PackageInfo, new()
{
    private const string CACHE_VERSION = "2";

    private readonly string cacheFile;
    private readonly IPcFileCacheContext<TP> ctx;
    private IEnumerable<string> defaultPaths;
    private readonly Dictionary<string, List<TP>> filesByFolder = new();
    private bool hasChanges;

    private readonly Dictionary<string, TP> infos = new();

    public PcFileCache(IPcFileCacheContext<TP> ctx)
    {
        this.ctx = ctx;
        try
        {
            var path = CacheDirectory;
            if (!Directory.Exists(path))
                Directory.CreateDirectory(path);
            cacheFile = Path.Combine(path, "pkgconfig-cache-" + CACHE_VERSION + ".xml");

            if (File.Exists(cacheFile))
                Load();
        }
        catch (Exception ex)
        {
            ctx.ReportError("pc file cache could not be loaded.", ex);
        }
    }

    protected abstract string CacheDirectory { get; }

    public object SyncRoot => infos;

    // Updates the pkg-config index, using the default search directories
    public void Update()
    {
        Update(GetDefaultPaths());
    }

    // Updates the pkg-config index, looking for .pc files in the provided directories
    public void Update(IEnumerable<string> pkgConfigDirs)
    {
        foreach (var pcdir in pkgConfigDirs)
        foreach (var pcfile in Directory.GetFiles(pcdir, "*.pc"))
            GetPackageInfo(pcfile);
        Save();
    }

    public IEnumerable<TP> GetPackages()
    {
        return GetPackages(null);
    }

    public IEnumerable<TP> GetPackages(IEnumerable<string> pkgConfigDirs)
    {
        if (pkgConfigDirs == null)
            pkgConfigDirs = GetDefaultPaths();

        foreach (var sp in pkgConfigDirs)
        {
            List<TP> list;
            if (filesByFolder.TryGetValue(Path.GetFullPath(sp), out list))
                foreach (var p in list)
                    yield return p;
        }
    }

    public TP GetPackageInfoByName(string name)
    {
        return GetPackageInfoByName(name, null);
    }

    public TP GetPackageInfoByName(string name, IEnumerable<string> pkgConfigDirs)
    {
        foreach (var p in GetPackages(pkgConfigDirs))
            if (p.Name == name)
                return p;
        return null;
    }

    // Returns information about a .pc file
    public TP GetPackageInfo(string file)
    {
        TP info, oldInfo = null;
        file = Path.GetFullPath(file);

        var wtime = File.GetLastWriteTime(file);

        lock (infos)
        {
            if (infos.TryGetValue(file, out info))
            {
                if (info.LastWriteTime == wtime)
                    return info;
                oldInfo = info;
            }
        }

        try
        {
            info = ParsePackageInfo(file);
        }
        catch (Exception ex)
        {
            ctx.ReportError("Error while parsing .pc file", ex);
            info = new TP();
        }

        lock (infos)
        {
            if (!info.IsValidPackage)
                info = new TP(); // Create a default empty instance
            info.LastWriteTime = wtime;
            Add(file, info, oldInfo);
            hasChanges = true;
        }

        return info;
    }

    private void Add(string file, TP info, TP replacedInfo)
    {
        infos[file] = info;
        var dir = Path.GetFullPath(Path.GetDirectoryName(file));
        List<TP> list;
        if (!filesByFolder.TryGetValue(dir, out list))
        {
            list = new List<TP>();
            filesByFolder[dir] = list;
        }

        if (replacedInfo != null)
        {
            var i = list.IndexOf(replacedInfo);
            if (i != -1)
            {
                list[i] = info;
                return;
            }
        }

        list.Add(info);
    }

    private FileStream OpenFile(FileAccess access)
    {
        var retries = 6;
        var mode = access == FileAccess.Read ? FileMode.Open : FileMode.Create;
        Exception lastException = null;

        while (retries > 0)
            try
            {
                return new FileStream(cacheFile, mode, access, FileShare.None);
            }
            catch (Exception ex)
            {
                // the file may be locked by another app. Wait a bit and try again
                lastException = ex;
                Thread.Sleep(200);
                retries--;
            }

        ctx.ReportError("File could not be opened: " + cacheFile, lastException);
        return null;
    }

    private void Load()
    {
        // The serializer can't be used because this file is reused in xbuild
        using (var fs = OpenFile(FileAccess.Read))
        {
            if (fs == null)
                return;
            var xr = new XmlTextReader(fs);
            xr.MoveToContent();
            xr.ReadStartElement();
            xr.MoveToContent();

            while (xr.NodeType == XmlNodeType.Element)
                ReadPackage(xr);
        }
    }

    public void Save()
    {
        // The serializer can't be used because this file is reused in xbuild
        lock (infos)
        {
            if (!hasChanges)
                return;

            using (var fs = OpenFile(FileAccess.Write))
            {
                if (fs == null)
                    return;
                var tw = new XmlTextWriter(new StreamWriter(fs));
                tw.Formatting = Formatting.Indented;

                tw.WriteStartElement("PcFileCache");
                foreach (var file in infos) WritePackage(tw, file.Key, file.Value);
                tw.WriteEndElement(); // PcFileCache
                tw.Flush();

                hasChanges = false;
            }
        }
    }

    private void WritePackage(XmlTextWriter tw, string file, TP pinfo)
    {
        tw.WriteStartElement("File");
        tw.WriteAttributeString("path", file);
        tw.WriteAttributeString("lastWriteTime",
            XmlConvert.ToString(pinfo.LastWriteTime, XmlDateTimeSerializationMode.Local));

        if (pinfo.IsValidPackage)
        {
            if (pinfo.Name != null)
                tw.WriteAttributeString("name", pinfo.Name);
            if (pinfo.Version != null)
                tw.WriteAttributeString("version", pinfo.Version);
            if (!string.IsNullOrEmpty(pinfo.Description))
                tw.WriteAttributeString("description", pinfo.Description);
            if (pinfo.CustomData != null)
                foreach (var cd in pinfo.CustomData)
                    tw.WriteAttributeString(cd.Key, cd.Value);
            WritePackageContent(tw, file, pinfo);
        }

        tw.WriteEndElement(); // File
    }

    protected virtual void WritePackageContent(XmlTextWriter tw, string file, TP pinfo)
    {
    }

    private void ReadPackage(XmlReader tr)
    {
        var pinfo = new TP();
        string file = null;

        tr.MoveToFirstAttribute();
        do
        {
            switch (tr.LocalName)
            {
                case "path":
                    file = tr.Value;
                    break;
                case "lastWriteTime":
                    pinfo.LastWriteTime = XmlConvert.ToDateTime(tr.Value, XmlDateTimeSerializationMode.Local);
                    break;
                case "name":
                    pinfo.Name = tr.Value;
                    break;
                case "version":
                    pinfo.Version = tr.Value;
                    break;
                case "description":
                    pinfo.Description = tr.Value;
                    break;
                default:
                    pinfo.SetData(tr.LocalName, tr.Value);
                    break;
            }
        } while (tr.MoveToNextAttribute());

        tr.MoveToElement();

        if (!tr.IsEmptyElement)
        {
            tr.ReadStartElement();
            tr.MoveToContent();
            ReadPackageContent(tr, pinfo);
            tr.MoveToContent();
            tr.ReadEndElement();
        }
        else
        {
            tr.Read();
        }

        tr.MoveToContent();

        if (!pinfo.IsValidPackage || ctx.IsCustomDataComplete(file, pinfo))
            Add(file, pinfo, null);
    }

    protected virtual void ReadPackageContent(XmlReader tr, TP pinfo)
    {
    }


    private TP ParsePackageInfo(string pcfile)
    {
        var file = new PcFile();
        file.Load(pcfile);

        var pinfo = new TP();
        pinfo.Name = Path.GetFileNameWithoutExtension(file.FilePath);

        if (!file.HasErrors)
        {
            pinfo.Version = file.Version;
            pinfo.Description = file.Description;
            ParsePackageInfo(file, pinfo);
            ctx.StoreCustomData(file, pinfo);
        }

        return pinfo;
    }

    protected virtual void ParsePackageInfo(PcFile file, TP pinfo)
    {
    }

    private IEnumerable<string> GetDefaultPaths()
    {
        if (defaultPaths == null)
        {
            var pkgConfigPath = Environment.GetEnvironmentVariable("PKG_CONFIG_PATH");
            var pkgConfigDir = Environment.GetEnvironmentVariable("PKG_CONFIG_LIBDIR");
            defaultPaths = GetPkgconfigPaths(null, pkgConfigPath, pkgConfigDir);
        }

        return defaultPaths;
    }

    public IEnumerable<string> GetPkgconfigPaths(string prefix, string pkgConfigPath, string pkgConfigLibdir)
    {
        char[] sep = { Path.PathSeparator };

        string[] pkgConfigPaths = null;
        if (!string.IsNullOrEmpty(pkgConfigPath))
        {
            pkgConfigPaths = pkgConfigPath.Split(sep, StringSplitOptions.RemoveEmptyEntries);
            if (pkgConfigPaths.Length == 0)
                pkgConfigPaths = null;
        }

        string[] pkgConfigLibdirs = null;
        if (!string.IsNullOrEmpty(pkgConfigLibdir))
        {
            pkgConfigLibdirs = pkgConfigLibdir.Split(sep, StringSplitOptions.RemoveEmptyEntries);
            if (pkgConfigLibdirs.Length == 0)
                pkgConfigLibdirs = null;
        }

        if (prefix == null)
            prefix = PathUp(typeof(int).Assembly.Location, 4);

        var paths = GetUnfilteredPkgConfigDirs(pkgConfigPaths, pkgConfigLibdirs, new[] { prefix });
        return NormaliseAndFilterPaths(paths, Environment.CurrentDirectory);
    }

    private IEnumerable<string> GetUnfilteredPkgConfigDirs(IEnumerable<string> pkgConfigPaths,
        IEnumerable<string> pkgConfigLibdirs, IEnumerable<string> systemPrefixes)
    {
        if (pkgConfigPaths != null)
            foreach (var dir in pkgConfigPaths)
                yield return dir;

        if (pkgConfigLibdirs != null)
        {
            foreach (var dir in pkgConfigLibdirs)
                yield return dir;
        }
        else if (systemPrefixes != null)
        {
            string[] suffixes =
            {
                Path.Combine("lib", "pkgconfig"),
                Path.Combine("lib64", "pkgconfig"),
                Path.Combine("libdata", "pkgconfig"),
                Path.Combine("share", "pkgconfig")
            };
            foreach (var prefix in systemPrefixes)
            foreach (var suffix in suffixes)
                yield return Path.Combine(prefix, suffix);
        }
    }

    private IEnumerable<string> NormaliseAndFilterPaths(IEnumerable<string> paths, string workingDirectory)
    {
        var filtered = new Dictionary<string, string>();
        foreach (var p in paths)
        {
            var path = p;
            if (!Path.IsPathRooted(path))
                path = Path.Combine(workingDirectory, path);
            path = Path.GetFullPath(path);
            if (filtered.ContainsKey(path))
                continue;
            filtered.Add(path, path);
            try
            {
                if (!Directory.Exists(path))
                    continue;
            }
            catch (IOException ex)
            {
                ctx.ReportError("Error checking for directory '" + path + "'.", ex);
            }

            yield return path;
        }
    }

    private static string PathUp(string path, int up)
    {
        if (up == 0)
            return path;
        for (var i = path.Length - 1; i >= 0; i--)
            if (path[i] == Path.DirectorySeparatorChar)
            {
                up--;
                if (up == 0)
                    return path.Substring(0, i);
            }

        return null;
    }
}

internal class PcFile
{
    private readonly Dictionary<string, string> variables = new();

    public string Description { get; set; }

    public string FilePath { get; set; }

    public bool HasErrors { get; set; }

    public string Libs { get; set; }

    public string Name { get; set; }

    public string Version { get; set; }

    public string GetVariable(string varName)
    {
        string val;
        variables.TryGetValue(varName, out val);
        return val;
    }

    public void Load(string pcfile)
    {
        FilePath = pcfile;
        variables.Add("pcfiledir", Path.GetDirectoryName(pcfile));
        using (var reader = new StreamReader(pcfile))
        {
            string line;
            while ((line = reader.ReadLine()) != null)
            {
                var i = line.IndexOf(':');
                var j = line.IndexOf('=');
                var k = Math.Min(i != -1 ? i : int.MaxValue, j != -1 ? j : int.MaxValue);
                if (k == int.MaxValue)
                    continue;
                var var = line.Substring(0, k).Trim();
                var value = line.Substring(k + 1).Trim();
                value = Evaluate(value);

                if (k == j)
                    // Is variable
                    variables[var] = value;
                else
                    switch (var)
                    {
                        case "Name":
                            Name = value;
                            break;
                        case "Description":
                            Description = value;
                            break;
                        case "Version":
                            Version = value;
                            break;
                        case "Libs":
                            Libs = value;
                            break;
                    }
            }
        }
    }

    private string Evaluate(string value)
    {
        var i = value.IndexOf("${");
        if (i == -1)
            return value;

        var sb = new StringBuilder();
        var last = 0;
        while (i != -1 && i < value.Length)
        {
            sb.Append(value, last, i - last);
            if (i == 0 || value[i - 1] != '$')
            {
                // Evaluate if var is not escaped
                i += 2;
                var n = value.IndexOf('}', i);
                if (n == -1 || n == i)
                {
                    // Closing bracket not found or empty name
                    HasErrors = true;
                    return value;
                }

                var rname = value.Substring(i, n - i);
                string rval;
                if (variables.TryGetValue(rname, out rval))
                {
                    sb.Append(rval);
                }
                else
                {
                    HasErrors = true;
                    return value;
                }

                i = n + 1;
                last = i;
            }
            else
            {
                last = i++;
            }

            if (i < value.Length - 1)
                i = value.IndexOf("${", i);
        }

        sb.Append(value, last, value.Length - last);
        return sb.ToString();
    }
}

internal class PackageInfo
{
    public string Name { get; set; }

    public string Version { get; set; }

    public string Description { get; set; }

    internal Dictionary<string, string> CustomData { get; private set; }

    internal DateTime LastWriteTime { get; set; }

    internal bool HasCustomData => CustomData != null && CustomData.Count > 0;

    protected internal virtual bool IsValidPackage => HasCustomData;

    public string GetData(string name)
    {
        if (CustomData == null)
            return null;
        string res;
        CustomData.TryGetValue(name, out res);
        return res;
    }

    public void SetData(string name, string value)
    {
        if (CustomData == null)
            CustomData = new Dictionary<string, string>();
        CustomData[name] = value;
    }

    public void RemoveData(string name)
    {
        if (CustomData != null)
            CustomData.Remove(name);
    }
}