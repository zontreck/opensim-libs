//
// AddinStore.cs
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
using System.Xml.Serialization;
using Mono.Addins.Description;
using Mono.Addins.Setup.ProgressMonitoring;

namespace Mono.Addins.Setup;

internal class AddinStore
{
    private readonly SetupService service;

    public AddinStore(SetupService service)
    {
        this.service = service;
    }

    public AddinRegistry Registry => service.Registry;

    internal void ResetCachedData()
    {
    }

    public bool Install(IProgressStatus statusMonitor, params string[] files)
    {
        var packages = new Package [files.Length];
        for (var n = 0; n < files.Length; n++)
            packages[n] = Package.FromFile(files[n]);

        return Install(statusMonitor, packages);
    }

    public bool Install(IProgressStatus statusMonitor, params AddinRepositoryEntry[] addins)
    {
        var packages = new Package [addins.Length];
        for (var n = 0; n < addins.Length; n++)
            packages[n] = Package.FromRepository(addins[n]);

        return Install(statusMonitor, packages);
    }

    internal bool Install(IProgressStatus monitor, params Package[] packages)
    {
        var packs = new PackageCollection();
        packs.AddRange(packages);
        return Install(monitor, packs);
    }

    internal bool Install(IProgressStatus statusMonitor, PackageCollection packs)
    {
        // Make sure the registry is up to date
        service.Registry.Update(statusMonitor);

        var monitor = ProgressStatusMonitor.GetProgressMonitor(statusMonitor);

        PackageCollection toUninstall;
        DependencyCollection unresolved;
        if (!ResolveDependencies(monitor, packs, out toUninstall, out unresolved))
        {
            monitor.ReportError("Not all dependencies could be resolved.", null);
            return false;
        }

        var prepared = new List<Package>();
        var uninstallPrepared = new List<Package>();
        var rollback = false;

        monitor.BeginTask("Installing add-ins...", 100);

        // Prepare install

        monitor.BeginStepTask("Initializing installation", toUninstall.Count + packs.Count + 1, 75);

        foreach (Package mpack in toUninstall)
            try
            {
                mpack.PrepareUninstall(monitor, this);
                uninstallPrepared.Add(mpack);
                if (monitor.IsCancelRequested)
                    throw new InstallException("Installation cancelled.");
                monitor.Step(1);
            }
            catch (Exception ex)
            {
                ReportException(monitor, ex);
                rollback = true;
                break;
            }

        monitor.Step(1);

        foreach (Package mpack in packs)
            try
            {
                mpack.PrepareInstall(monitor, this);
                if (monitor.IsCancelRequested)
                    throw new InstallException("Installation cancelled.");
                prepared.Add(mpack);
                monitor.Step(1);
            }
            catch (Exception ex)
            {
                ReportException(monitor, ex);
                rollback = true;
                break;
            }

        monitor.EndTask();

        monitor.BeginStepTask("Installing", toUninstall.Count + packs.Count + 1, 20);

        // Commit install

        if (!rollback)
            foreach (Package mpack in toUninstall)
                try
                {
                    mpack.CommitUninstall(monitor, this);
                    if (monitor.IsCancelRequested)
                        throw new InstallException("Installation cancelled.");
                    monitor.Step(1);
                }
                catch (Exception ex)
                {
                    ReportException(monitor, ex);
                    rollback = true;
                    break;
                }

        monitor.Step(1);

        if (!rollback)
            foreach (Package mpack in packs)
                try
                {
                    mpack.CommitInstall(monitor, this);
                    if (monitor.IsCancelRequested)
                        throw new InstallException("Installation cancelled.");
                    monitor.Step(1);
                }
                catch (Exception ex)
                {
                    ReportException(monitor, ex);
                    rollback = true;
                    break;
                }

        monitor.EndTask();

        // Rollback if failed

        if (monitor.IsCancelRequested)
            monitor = new NullProgressMonitor();

        if (rollback)
        {
            monitor.BeginStepTask("Finishing installation", (prepared.Count + uninstallPrepared.Count) * 2 + 1, 5);

            foreach (var mpack in prepared)
                try
                {
                    mpack.RollbackInstall(monitor, this);
                    monitor.Step(1);
                }
                catch (Exception ex)
                {
                    ReportException(monitor, ex);
                }

            foreach (var mpack in uninstallPrepared)
                try
                {
                    mpack.RollbackUninstall(monitor, this);
                    monitor.Step(1);
                }
                catch (Exception ex)
                {
                    ReportException(monitor, ex);
                }
        }
        else
        {
            monitor.BeginStepTask("Finishing installation", prepared.Count + uninstallPrepared.Count + 1, 5);
        }

        // Cleanup

        foreach (var mpack in prepared)
            try
            {
                mpack.EndInstall(monitor, this);
                monitor.Step(1);
            }
            catch (Exception ex)
            {
                monitor.Log.WriteLine(ex);
            }

        monitor.Step(1);

        foreach (var mpack in uninstallPrepared)
            try
            {
                mpack.EndUninstall(monitor, this);
                monitor.Step(1);
            }
            catch (Exception ex)
            {
                monitor.Log.WriteLine(ex);
            }

        // Update the extension maps
        service.Registry.Update(statusMonitor);

        monitor.EndTask();

        monitor.EndTask();

        service.SaveConfiguration();
        ResetCachedData();

        return !rollback;
    }

    private void ReportException(IProgressMonitor statusMonitor, Exception ex)
    {
        if (ex is InstallException)
            statusMonitor.ReportError(ex.Message, null);
        else
            statusMonitor.ReportError(null, ex);
    }

    public void Uninstall(IProgressStatus statusMonitor, string id)
    {
        Uninstall(statusMonitor, new[] { id });
    }

    public void Uninstall(IProgressStatus statusMonitor, IEnumerable<string> ids)
    {
        var monitor = ProgressStatusMonitor.GetProgressMonitor(statusMonitor);
        monitor.BeginTask("Uninstalling add-ins", ids.Count());

        foreach (var id in ids)
        {
            var rollback = false;
            var toUninstall = new List<AddinPackage>();
            var uninstallPrepared = new List<Package>();

            var ia = service.Registry.GetAddin(id);
            if (ia == null)
                throw new InstallException("The add-in '" + id + "' is not installed.");

            toUninstall.Add(AddinPackage.FromInstalledAddin(ia));

            var deps = GetDependentAddins(id, true);
            foreach (var dep in deps)
                toUninstall.Add(AddinPackage.FromInstalledAddin(dep));

            monitor.BeginTask("Deleting files", toUninstall.Count * 2 + uninstallPrepared.Count + 1);

            // Prepare install

            foreach (Package mpack in toUninstall)
                try
                {
                    mpack.PrepareUninstall(monitor, this);
                    monitor.Step(1);
                    uninstallPrepared.Add(mpack);
                }
                catch (Exception ex)
                {
                    ReportException(monitor, ex);
                    rollback = true;
                    break;
                }

            // Commit install

            if (!rollback)
                foreach (Package mpack in toUninstall)
                    try
                    {
                        mpack.CommitUninstall(monitor, this);
                        monitor.Step(1);
                    }
                    catch (Exception ex)
                    {
                        ReportException(monitor, ex);
                        rollback = true;
                        break;
                    }

            // Rollback if failed

            if (rollback)
            {
                monitor.BeginTask("Rolling back uninstall", uninstallPrepared.Count);
                foreach (var mpack in uninstallPrepared)
                    try
                    {
                        mpack.RollbackUninstall(monitor, this);
                    }
                    catch (Exception ex)
                    {
                        ReportException(monitor, ex);
                    }

                monitor.EndTask();
            }

            monitor.Step(1);

            // Cleanup

            foreach (var mpack in uninstallPrepared)
                try
                {
                    mpack.EndUninstall(monitor, this);
                    monitor.Step(1);
                }
                catch (Exception ex)
                {
                    monitor.Log.WriteLine(ex);
                }

            monitor.EndTask();
            monitor.Step(1);
        }

        // Update the extension maps
        service.Registry.Update(statusMonitor);

        monitor.EndTask();

        service.SaveConfiguration();
        ResetCachedData();
    }

    public Addin[] GetDependentAddins(string id, bool recursive)
    {
        var list = new List<Addin>();
        FindDependentAddins(list, id, recursive);
        return list.ToArray();
    }

    private void FindDependentAddins(List<Addin> list, string id, bool recursive)
    {
        foreach (var iaddin in service.Registry.GetAddins())
        {
            if (list.Contains(iaddin))
                continue;
            foreach (Dependency dep in iaddin.Description.MainModule.Dependencies)
            {
                var adep = dep as AddinDependency;
                if (adep != null && adep.AddinId == id)
                {
                    list.Add(iaddin);
                    if (recursive)
                        FindDependentAddins(list, iaddin.Id, true);
                }
            }
        }
    }

    public bool ResolveDependencies(IProgressStatus statusMonitor, AddinRepositoryEntry[] addins,
        out PackageCollection resolved, out PackageCollection toUninstall, out DependencyCollection unresolved)
    {
        resolved = new PackageCollection();
        for (var n = 0; n < addins.Length; n++)
            resolved.Add(Package.FromRepository(addins[n]));
        return ResolveDependencies(statusMonitor, resolved, out toUninstall, out unresolved);
    }

    public bool ResolveDependencies(IProgressStatus statusMonitor, PackageCollection packages,
        out PackageCollection toUninstall, out DependencyCollection unresolved)
    {
        var monitor = ProgressStatusMonitor.GetProgressMonitor(statusMonitor);
        return ResolveDependencies(monitor, packages, out toUninstall, out unresolved);
    }

    internal bool ResolveDependencies(IProgressMonitor monitor, PackageCollection packages,
        out PackageCollection toUninstall, out DependencyCollection unresolved)
    {
        var requested = new PackageCollection();
        requested.AddRange(packages);

        unresolved = new DependencyCollection();
        toUninstall = new PackageCollection();
        var installedRequired = new PackageCollection();

        for (var n = 0; n < packages.Count; n++)
        {
            var p = packages[n];
            p.Resolve(monitor, this, packages, toUninstall, installedRequired, unresolved);
        }

        if (unresolved.Count != 0)
        {
            foreach (Dependency dep in unresolved)
                monitor.ReportError(string.Format("The package '{0}' could not be found in any repository", dep.Name),
                    null);
            return false;
        }

        // Check that we are not uninstalling packages that are required
        // by packages being installed.

        foreach (Package p in installedRequired)
            if (toUninstall.Contains(p))
            {
                // Only accept to uninstall this package if we are
                // going to install a newer version.
                var foundUpgrade = false;
                foreach (Package tbi in packages)
                    if (tbi.Equals(p) || tbi.IsUpgradeOf(p))
                    {
                        foundUpgrade = true;
                        break;
                    }

                if (!foundUpgrade)
                    return false;
            }

        // Check that we are not trying to uninstall from a directory from
        // which we don't have write permissions

        foreach (Package p in toUninstall)
        {
            var ap = p as AddinPackage;
            if (ap != null)
            {
                var ia = service.Registry.GetAddin(ap.Addin.Id);
                if (File.Exists(ia.AddinFile) && !HasWriteAccess(ia.AddinFile) && IsUserAddin(ia.AddinFile))
                {
                    monitor.ReportError(GetUninstallErrorNoRoot(ap.Addin), null);
                    return false;
                }
            }
        }

        // Check that we are not installing two versions of the same addin

        var resolved = new PackageCollection();
        resolved.AddRange(packages);

        var error = false;

        for (var n = 0; n < packages.Count; n++)
        {
            var ap = packages[n] as AddinPackage;
            if (ap == null) continue;

            for (var k = n + 1; k < packages.Count; k++)
            {
                var otherap = packages[k] as AddinPackage;
                if (otherap == null) continue;

                if (ap.Addin.Id == otherap.Addin.Id)
                {
                    if (ap.IsUpgradeOf(otherap))
                    {
                        if (requested.Contains(otherap))
                        {
                            monitor.ReportError(
                                "Can't install two versions of the same add-in: '" + ap.Addin.Name + "'.", null);
                            error = true;
                        }
                        else
                        {
                            packages.RemoveAt(k);
                        }
                    }
                    else if (otherap.IsUpgradeOf(ap))
                    {
                        if (requested.Contains(ap))
                        {
                            monitor.ReportError(
                                "Can't install two versions of the same add-in: '" + ap.Addin.Name + "'.", null);
                            error = true;
                        }
                        else
                        {
                            packages.RemoveAt(n);
                            n--;
                        }
                    }
                    else
                    {
                        error = true;
                        monitor.ReportError("Can't install two versions of the same add-in: '" + ap.Addin.Name + "'.",
                            null);
                    }

                    break;
                }
            }
        }

        // Don't allow installing add-ins which are scheduled for uninstall

        foreach (Package p in packages)
        {
            var ap = p as AddinPackage;
            if (ap != null && Registry.IsRegisteredForUninstall(ap.Addin.Id))
            {
                error = true;
                monitor.ReportError(
                    "The addin " + ap.Addin.Name + " v" + ap.Addin.Version +
                    " is scheduled for uninstallation. Please restart the application before trying to re-install it.",
                    null);
            }
        }

        return !error;
    }

    internal void ResolveDependency(IProgressMonitor monitor, Dependency dep, AddinPackage parentPackage,
        PackageCollection toInstall, PackageCollection toUninstall, PackageCollection installedRequired,
        DependencyCollection unresolved)
    {
        var adep = dep as AddinDependency;
        if (adep == null)
            return;

        var nsid = Addin.GetFullId(parentPackage.Addin.Namespace, adep.AddinId, null);

        foreach (Package p in toInstall)
        {
            var ap = p as AddinPackage;
            if (ap != null)
                if (Addin.GetIdName(ap.Addin.Id) == nsid && ((AddinInfo)ap.Addin).SupportsVersion(adep.Version))
                    return;
        }

        var addins = new List<Addin>();
        addins.AddRange(service.Registry.GetAddins());
        addins.AddRange(service.Registry.GetAddinRoots());

        foreach (var addin in addins)
            if (Addin.GetIdName(addin.Id) == nsid && addin.SupportsVersion(adep.Version))
            {
                var p = AddinPackage.FromInstalledAddin(addin);
                if (!installedRequired.Contains(p))
                    installedRequired.Add(p);
                return;
            }

        var avaddins = service.Repositories.GetAvailableAddins();
        foreach (PackageRepositoryEntry avAddin in avaddins)
            if (Addin.GetIdName(avAddin.Addin.Id) == nsid && avAddin.Addin.SupportsVersion(adep.Version))
            {
                toInstall.Add(Package.FromRepository(avAddin));
                return;
            }

        unresolved.Add(adep);
    }

    internal string GetAddinDirectory(AddinInfo info)
    {
        return Path.Combine(service.InstallDirectory, info.Id.Replace(',', '.'));
    }

    internal void RegisterAddin(IProgressMonitor monitor, AddinInfo info, string sourceDir)
    {
        monitor.Log.WriteLine("Installing " + info.Name + " v" + info.Version);
        var addinDir = GetAddinDirectory(info);
        if (!Directory.Exists(addinDir))
            Directory.CreateDirectory(addinDir);
        CopyDirectory(sourceDir, addinDir);

        ResetCachedData();
    }

    private void CopyDirectory(string src, string dest)
    {
        CopyDirectory(src, dest, "");
    }

    private void CopyDirectory(string src, string dest, string subdir)
    {
        var destDir = Path.Combine(dest, subdir);

        if (!Directory.Exists(destDir))
            Directory.CreateDirectory(destDir);

        foreach (var file in Directory.GetFiles(src))
            if (Path.GetFileName(file) != "addin.info")
                File.Copy(file, Path.Combine(destDir, Path.GetFileName(file)), true);

        foreach (var dir in Directory.GetDirectories(src))
            CopyDirectory(dir, dest, Path.Combine(subdir, Path.GetFileName(dir)));
    }

    internal object DownloadObject(IProgressMonitor monitor, string url, Type type)
    {
        string file = null;
        try
        {
            file = DownloadFile(monitor, url);
            return ReadObject(file, type);
        }
        finally
        {
            if (file != null)
                File.Delete(file);
        }
    }

    private static XmlSerializer GetSerializer(Type type)
    {
        if (type == typeof(AddinSystemConfiguration))
            return new AddinSystemConfigurationSerializer();
        if (type == typeof(Repository))
            return new RepositorySerializer();
        return new XmlSerializer(type);
    }

    internal static object ReadObject(string file, Type type)
    {
        if (!File.Exists(file))
            return null;

        var r = new StreamReader(file);
        try
        {
            var ser = GetSerializer(type);
            return ser.Deserialize(r);
        }
        catch
        {
            return null;
        }
        finally
        {
            r.Close();
        }
    }

    internal static void WriteObject(string file, object obj)
    {
        var dir = Path.GetDirectoryName(file);
        if (!Directory.Exists(dir))
            Directory.CreateDirectory(dir);
        var s = new StreamWriter(file);
        try
        {
            var ser = GetSerializer(obj.GetType());
            ser.Serialize(s, obj);
            s.Close();
        }
        catch
        {
            s.Close();
            if (File.Exists(file))
                File.Delete(file);
            throw;
        }
    }

    internal string DownloadFile(IProgressMonitor monitor, string url)
    {
        if (url.StartsWith("file://", StringComparison.Ordinal))
        {
            var tmpfile = Path.GetTempFileName();
            var path = new Uri(url).LocalPath;
            File.Delete(tmpfile);
            File.Copy(path, tmpfile);
            return tmpfile;
        }

        string file = null;
        FileStream fs = null;
        Stream s = null;

        try
        {
            monitor.BeginTask("Requesting " + url, 2);
            var task = DownloadFileRequest.DownloadFile(url, true);
            task.Wait();

            using (var request = task.Result)
            {
                monitor.Step(1);
                monitor.BeginTask("Downloading " + url, request.ContentLength);

                file = Path.GetTempFileName();
                fs = new FileStream(file, FileMode.Create, FileAccess.Write);
                s = request.Stream;
                var buffer = new byte [4096];

                int n;
                while ((n = s.Read(buffer, 0, buffer.Length)) != 0)
                {
                    monitor.Step(n);
                    fs.Write(buffer, 0, n);
                    if (monitor.IsCancelRequested)
                        throw new InstallException("Installation cancelled.");
                }

                fs.Close();
                s.Close();
                return file;
            }
        }
        catch
        {
            if (fs != null)
                fs.Close();
            if (s != null)
                s.Close();
            if (file != null)
                File.Delete(file);
            throw;
        }
        finally
        {
            monitor.EndTask();
        }
    }

    internal bool HasWriteAccess(string file)
    {
        var f = new FileInfo(file);
        return !f.Exists || !f.IsReadOnly;
    }

    internal bool IsUserAddin(string addinFile)
    {
        var installPath = service.InstallDirectory;
        if (installPath[installPath.Length - 1] != Path.DirectorySeparatorChar)
            installPath += Path.DirectorySeparatorChar;
        return Path.GetFullPath(addinFile).StartsWith(installPath);
    }

    internal static string GetUninstallErrorNoRoot(AddinHeader ainfo)
    {
        return string.Format("The add-in '{0} v{1}' can't be uninstalled with the current user permissions.",
            ainfo.Name, ainfo.Version);
    }
}