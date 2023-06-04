//
// AddinPackage.cs
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
using System.Linq;
using ICSharpCode.SharpZipLib.Zip;
using Mono.Addins.Database;
using Mono.Addins.Description;

namespace Mono.Addins.Setup;

internal class AddinPackage : Package
{
    private string configFile;
    private bool disablingOnUninstall;
    private Addin iaddin;
    private AddinInfo info;
    private bool installed;
    private string packFile;
    private string tempFolder;
    private bool uninstallingLoaded;
    private string url;

    public AddinHeader Addin => info;

    public override string Name => info.Name + " v" + info.Version;

    public static AddinPackage PackageFromRepository(AddinRepositoryEntry repAddin)
    {
        var pack = new AddinPackage();
        pack.info = (AddinInfo)repAddin.Addin;
        pack.url = new Uri(new Uri(repAddin.RepositoryUrl), repAddin.Url).ToString();
        return pack;
    }

    public static AddinPackage PackageFromFile(string file)
    {
        var pack = new AddinPackage();
        pack.info = ReadAddinInfo(file);
        pack.packFile = file;
        return pack;
    }

    public static AddinPackage FromInstalledAddin(Addin sinfo)
    {
        var pack = new AddinPackage();
        pack.info = AddinInfo.ReadFromDescription(sinfo.Description);
        return pack;
    }

    internal static AddinInfo ReadAddinInfo(string file)
    {
        var zfile = new ZipFile(file);
        try
        {
            foreach (ZipEntry ze in zfile)
                if (ze.Name == "addin.info")
                    using (var s = zfile.GetInputStream(ze))
                    {
                        return AddinInfo.ReadFromAddinFile(new StreamReader(s));
                    }
        }
        finally
        {
            zfile.Close();
        }

        throw new InstallException("Addin configuration file not found in package.");
    }

    internal override bool IsUpgradeOf(Package p)
    {
        var ap = p as AddinPackage;
        if (ap == null) return false;
        return info.SupportsVersion(ap.info.Version);
    }

    public override bool Equals(object ob)
    {
        var ap = ob as AddinPackage;
        if (ap == null) return false;
        return ap.info.Id == info.Id && ap.info.Version == info.Version;
    }

    public override int GetHashCode()
    {
        return (info.Id + info.Version).GetHashCode();
    }

    internal override void PrepareInstall(IProgressMonitor monitor, AddinStore service)
    {
        if (service.Registry.IsRegisteredForUninstall(info.Id))
            throw new InstallException("The addin " + info.Name + " v" + info.Version +
                                       " is scheduled for uninstallation. Please restart the application before trying to install it again.");
        if (service.Registry.GetAddin(Addins.Addin.GetFullId(info.Namespace, info.Id, info.Version), true) != null)
            throw new InstallException("The addin " + info.Name + " v" + info.Version + " is already installed.");

        if (url != null)
            packFile = service.DownloadFile(monitor, url);

        tempFolder = CreateTempFolder();

        // Extract the files			
        using (var fs = new FileStream(packFile, FileMode.Open, FileAccess.Read))
        {
            var zip = new ZipFile(fs);
            try
            {
                foreach (ZipEntry entry in zip)
                {
                    string name;
                    if (Path.PathSeparator == '\\')
                        name = entry.Name.Replace('/', '\\');
                    else
                        name = entry.Name.Replace('\\', '/');
                    var path = Path.Combine(tempFolder, name);
                    var dir = Path.GetDirectoryName(path);
                    if (!Directory.Exists(dir))
                        Directory.CreateDirectory(dir);

                    var buffer = new byte [8192];
                    var n = 0;
                    var inStream = zip.GetInputStream(entry);
                    Stream outStream = null;
                    try
                    {
                        outStream = File.Create(path);
                        while ((n = inStream.Read(buffer, 0, buffer.Length)) > 0)
                            outStream.Write(buffer, 0, n);
                    }
                    finally
                    {
                        inStream.Close();
                        if (outStream != null)
                            outStream.Close();
                    }
                }
            }
            finally
            {
                zip.Close();
            }
        }

        foreach (var s in Directory.GetFiles(tempFolder))
            if (Path.GetFileName(s) == "addin.info")
            {
                configFile = s;
                break;
            }

        if (configFile == null)
            throw new InstallException("Add-in information file not found in package.");
    }

    internal override void CommitInstall(IProgressMonitor monitor, AddinStore service)
    {
        service.RegisterAddin(monitor, info, tempFolder);
        installed = true;
    }

    internal override void RollbackInstall(IProgressMonitor monitor, AddinStore service)
    {
        if (installed)
        {
            iaddin = service.Registry.GetAddin(info.Id);
            if (iaddin != null)
                CommitUninstall(monitor, service);
        }
    }

    internal override void EndInstall(IProgressMonitor monitor, AddinStore service)
    {
        if (url != null && packFile != null)
            File.Delete(packFile);
        if (tempFolder != null)
            Directory.Delete(tempFolder, true);
    }

    internal override void Resolve(IProgressMonitor monitor, AddinStore service, PackageCollection toInstall,
        PackageCollection toUninstall, PackageCollection installedRequired, DependencyCollection unresolved)
    {
        var ia = service.Registry.GetAddin(Addins.Addin.GetIdName(info.Id));

        if (ia != null)
        {
            Package p = FromInstalledAddin(ia);
            if (!toUninstall.Contains(p))
                toUninstall.Add(p);

            if (!info.SupportsVersion(ia.Version))
            {
                // This addin breaks the api of the currently installed one,
                // it has to be removed, together with all dependencies

                var ainfos = service.GetDependentAddins(info.Id, true);
                foreach (var ainfo in ainfos)
                {
                    p = FromInstalledAddin(ainfo);
                    if (!toUninstall.Contains(p))
                        toUninstall.Add(p);
                }
            }
        }

        foreach (Dependency dep in info.Dependencies)
            service.ResolveDependency(monitor, dep, this, toInstall, toUninstall, installedRequired, unresolved);
    }

    internal override void PrepareUninstall(IProgressMonitor monitor, AddinStore service)
    {
        iaddin = service.Registry.GetAddin(info.Id, true);
        if (iaddin == null)
            throw new InstallException(string.Format("The add-in '{0}' is not installed.", info.Name));

        var conf = iaddin.Description;

        if (!File.Exists(iaddin.AddinFile))
        {
            monitor.ReportWarning(string.Format(
                "The add-in '{0}' is scheduled for uninstalling, but the add-in file could not be found.", info.Name));
            return;
        }

        // The add-in is a core application add-in. It can't be uninstalled, so it will be disabled.
        if (!service.IsUserAddin(iaddin.AddinFile))
        {
            disablingOnUninstall = true;
            return;
        }

        // If the add-in assemblies are loaded, or if there is any file with a write lock, delay the uninstallation
        var files = new HashSet<string>(GetInstalledFiles(conf));
        if (AddinManager.CheckAssembliesLoaded(files) || files.Any(f => HasWriteLock(f)))
        {
            uninstallingLoaded = true;
            return;
        }

        if (!service.HasWriteAccess(iaddin.AddinFile))
            throw new InstallException(AddinStore.GetUninstallErrorNoRoot(info));

        foreach (var path in GetInstalledFiles(conf))
            if (!service.HasWriteAccess(path))
                throw new InstallException(AddinStore.GetUninstallErrorNoRoot(info));

        tempFolder = CreateTempFolder();
        CopyAddinFiles(monitor, conf, iaddin.AddinFile, tempFolder);
    }

    private bool HasWriteLock(string file)
    {
        if (!File.Exists(file))
            return false;
        try
        {
            File.OpenWrite(file).Close();
            return false;
        }
        catch
        {
            return true;
        }
    }

    private IEnumerable<string> GetInstalledFiles(AddinDescription conf)
    {
        var basePath = Path.GetDirectoryName(conf.AddinFile);
        foreach (var relPath in conf.AllFiles)
        {
            var afile = Path.Combine(basePath, Util.NormalizePath(relPath));
            if (File.Exists(afile))
                yield return afile;
        }

        foreach (var p in conf.Properties)
        {
            string file;
            try
            {
                file = Path.Combine(basePath, p.Value);
                if (!File.Exists(file))
                    file = null;
            }
            catch
            {
                file = null;
            }

            if (file != null)
                yield return file;
        }
    }

    internal override void CommitUninstall(IProgressMonitor monitor, AddinStore service)
    {
        if (disablingOnUninstall)
        {
            disablingOnUninstall = false;
            service.Registry.DisableAddin(info.Id, true);
            return;
        }

        var conf = iaddin.Description;

        var basePath = Path.GetDirectoryName(conf.AddinFile);

        if (uninstallingLoaded)
        {
            var files = new List<string>();
            files.Add(iaddin.AddinFile);
            foreach (var f in GetInstalledFiles(conf))
                files.Add(f);
            service.Registry.RegisterForUninstall(info.Id, files);
            return;
        }

        if (tempFolder == null)
            return;

        monitor.Log.WriteLine("Uninstalling " + info.Name + " v" + info.Version);

        foreach (var path in GetInstalledFiles(conf))
            File.Delete(path);

        File.Delete(iaddin.AddinFile);

        RecDeleteDir(monitor, basePath);

        monitor.Log.WriteLine("Done");
    }

    private void RecDeleteDir(IProgressMonitor monitor, string path)
    {
        if (Directory.GetFiles(path).Length != 0)
            return;

        foreach (var dir in Directory.GetDirectories(path))
            RecDeleteDir(monitor, dir);

        try
        {
            Directory.Delete(path);
        }
        catch
        {
            monitor.ReportWarning("Directory " + path + " could not be deleted.");
        }
    }

    internal override void RollbackUninstall(IProgressMonitor monitor, AddinStore service)
    {
        disablingOnUninstall = false;
        if (tempFolder != null)
        {
            var conf = iaddin.Description;
            var configFile = Path.Combine(tempFolder, Path.GetFileName(iaddin.AddinFile));

            var addinDir = Path.GetDirectoryName(iaddin.AddinFile);
            CopyAddinFiles(monitor, conf, configFile, addinDir);
        }
    }

    internal override void EndUninstall(IProgressMonitor monitor, AddinStore service)
    {
        if (tempFolder != null)
            Directory.Delete(tempFolder, true);
        tempFolder = null;
    }

    private void CopyAddinFiles(IProgressMonitor monitor, AddinDescription conf, string configFile, string destPath)
    {
        if (!Directory.Exists(destPath))
            Directory.CreateDirectory(destPath);

        var dfile = Path.Combine(destPath, Path.GetFileName(configFile));
        if (File.Exists(dfile))
            File.Delete(dfile);

        File.Copy(configFile, dfile);

        var basePath = Path.GetDirectoryName(configFile);

        foreach (var relPath in conf.AllFiles)
        {
            var path = Path.Combine(basePath, Util.NormalizePath(relPath));
            if (!File.Exists(path))
                continue;

            var destf = Path.Combine(destPath, Path.GetDirectoryName(relPath));
            if (!Directory.Exists(destf))
                Directory.CreateDirectory(destf);

            dfile = Path.Combine(destPath, relPath);
            if (File.Exists(dfile))
                File.Delete(dfile);

            File.Copy(path, dfile);
        }
    }
}