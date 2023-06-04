//
// AddinRegistry.cs
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
using System.Linq;
using System.Xml;
using Mono.Addins.Database;
using Mono.Addins.Description;
using Mono.Addins.Serialization;

namespace Mono.Addins;

/// <summary>
///     An add-in registry.
/// </summary>
/// <remarks>
///     An add-in registry is a data structure used by the add-in engine to locate add-ins to load.
///     A registry can be configured to look for add-ins in several directories. However, add-ins
///     copied to those directories won't be detected until an explicit add-in scan is requested.
///     The registry can be updated by an application by calling Registry.Update(), or by a user by
///     running the 'mautil' add-in setup tool.
///     The registry has information about the location of every add-in and a timestamp of the last
///     check, so the Update method will only scan new or modified add-ins. An application can
///     add a call to Registry.Update() in the Main method to detect all new add-ins every time the
///     app is started.
///     Every add-in added to the registry is parsed and validated, and if there is any error it
///     will be rejected. The registry is also in charge of scanning the add-in assemblies and look
///     for extensions and other information declared using custom attributes. That information is
///     merged with the manifest information (if there is one) to create a complete add-in
///     description ready to be used at run-time.
///     Mono.Addins allows sharing an add-in registry among several applications. In this context,
///     all applications sharing the registry share the same extension point model, and it is
///     possible to implement add-ins which extend several hosts.
/// </remarks>
public class AddinRegistry : IDisposable
{
    private readonly string[] addinDirs;
    private readonly AddinDatabase database;

    /// <summary>
    ///     Initializes a new instance.
    /// </summary>
    /// <param name="registryPath">
    ///     Location of the add-in registry.
    /// </param>
    /// <remarks>
    ///     Creates a new add-in registry located in the provided path.
    ///     The add-in registry will look for add-ins in an 'addins'
    ///     subdirectory of the provided registryPath.
    ///     When specifying a path, it is possible to use a special folder name as root.
    ///     For example: [Personal]/.config/MyApp. In this case, [Personal] will be replaced
    ///     by the location of the Environment.SpecialFolder.Personal folder. Any value
    ///     of the Environment.SpecialFolder enumeration can be used (always between square
    ///     brackets)
    /// </remarks>
    public AddinRegistry(string registryPath) : this(null, registryPath, null, null, null, null)
    {
    }

    /// <summary>
    ///     Initializes a new instance.
    /// </summary>
    /// <param name="registryPath">
    ///     Location of the add-in registry.
    /// </param>
    /// <param name="startupDirectory">
    ///     Location of the application.
    /// </param>
    /// <remarks>
    ///     Creates a new add-in registry located in the provided path.
    ///     The add-in registry will look for add-ins in an 'addins'
    ///     subdirectory of the provided registryPath.
    ///     When specifying a path, it is possible to use a special folder name as root.
    ///     For example: [Personal]/.config/MyApp. In this case, [Personal] will be replaced
    ///     by the location of the Environment.SpecialFolder.Personal folder. Any value
    ///     of the Environment.SpecialFolder enumeration can be used (always between square
    ///     brackets)
    /// </remarks>
    public AddinRegistry(string registryPath, string startupDirectory) : this(null, registryPath, startupDirectory,
        null, null, null)
    {
    }

    /// <summary>
    ///     Initializes a new instance of the <see cref="Mono.Addins.AddinRegistry" /> class.
    /// </summary>
    /// <param name='registryPath'>
    ///     Location of the add-in registry.
    /// </param>
    /// <param name='startupDirectory'>
    ///     Location of the application.
    /// </param>
    /// <param name='addinsDir'>
    ///     Add-ins directory. If the path is relative, it is considered to be relative
    ///     to the configDir directory.
    /// </param>
    /// <remarks>
    ///     Creates a new add-in registry located in the provided path.
    ///     Configuration information about the add-in registry will be stored in
    ///     'registryPath'. The add-in registry will look for add-ins in the provided
    ///     'addinsDir' directory.
    ///     When specifying a path, it is possible to use a special folder name as root.
    ///     For example: [Personal]/.config/MyApp. In this case, [Personal] will be replaced
    ///     by the location of the Environment.SpecialFolder.Personal folder. Any value
    ///     of the Environment.SpecialFolder enumeration can be used (always between square
    ///     brackets)
    /// </remarks>
    public AddinRegistry(string registryPath, string startupDirectory, string addinsDir) : this(null, registryPath,
        startupDirectory, addinsDir, null, null)
    {
    }

    /// <summary>
    ///     Initializes a new instance of the <see cref="Mono.Addins.AddinRegistry" /> class.
    /// </summary>
    /// <param name='registryPath'>
    ///     Location of the add-in registry.
    /// </param>
    /// <param name='startupDirectory'>
    ///     Location of the application.
    /// </param>
    /// <param name='addinsDir'>
    ///     Add-ins directory. If the path is relative, it is considered to be relative
    ///     to the configDir directory.
    /// </param>
    /// <param name='databaseDir'>
    ///     Location of the add-in database. If the path is relative, it is considered to be relative
    ///     to the configDir directory.
    /// </param>
    /// <remarks>
    ///     Creates a new add-in registry located in the provided path.
    ///     Configuration information about the add-in registry will be stored in
    ///     'registryPath'. The add-in registry will look for add-ins in the provided
    ///     'addinsDir' directory. Cached information about add-ins will be stored in
    ///     the 'databaseDir' directory.
    ///     When specifying a path, it is possible to use a special folder name as root.
    ///     For example: [Personal]/.config/MyApp. In this case, [Personal] will be replaced
    ///     by the location of the Environment.SpecialFolder.Personal folder. Any value
    ///     of the Environment.SpecialFolder enumeration can be used (always between square
    ///     brackets)
    /// </remarks>
    public AddinRegistry(string registryPath, string startupDirectory, string addinsDir, string databaseDir) : this(
        null, registryPath, startupDirectory, addinsDir, databaseDir, null)
    {
    }

    internal AddinRegistry(AddinEngine engine, string registryPath, string startupDirectory, string addinsDir,
        string databaseDir, string additionalGlobalAddinDirectory)
    {
        RegistryPath = Path.GetFullPath(Util.NormalizePath(registryPath));

        if (addinsDir != null)
        {
            addinsDir = Util.NormalizePath(addinsDir);
            if (Path.IsPathRooted(addinsDir))
                this.DefaultAddinsFolder = Path.GetFullPath(addinsDir);
            else
                this.DefaultAddinsFolder = Path.GetFullPath(Path.Combine(RegistryPath, addinsDir));
        }
        else
        {
            this.DefaultAddinsFolder = Path.Combine(RegistryPath, "addins");
        }

        if (databaseDir != null)
        {
            databaseDir = Util.NormalizePath(databaseDir);
            if (Path.IsPathRooted(databaseDir))
                this.AddinCachePath = Path.GetFullPath(databaseDir);
            else
                this.AddinCachePath = Path.GetFullPath(Path.Combine(RegistryPath, databaseDir));
        }
        else
        {
            this.AddinCachePath = Path.GetFullPath(RegistryPath);
        }

        // Look for add-ins in the hosts directory and in the default
        // addins directory
        if (additionalGlobalAddinDirectory != null)
            addinDirs = new[] { DefaultAddinsFolder, additionalGlobalAddinDirectory };
        else
            addinDirs = new[] { DefaultAddinsFolder };

        // Initialize the database after all paths have been set
        database = new AddinDatabase(engine, this);

        // Get the domain corresponding to the startup folder
        if (startupDirectory != null && startupDirectory.Length > 0)
        {
            this.StartupDirectory = Util.NormalizePath(startupDirectory);
            CurrentDomain = database.GetFolderDomain(null, this.StartupDirectory);
        }
        else
        {
            CurrentDomain = AddinDatabase.GlobalDomain;
        }
    }

    internal bool UnknownDomain => CurrentDomain == AddinDatabase.UnknownDomain;

    internal static string GlobalRegistryPath
    {
        get
        {
            var customDir = Environment.GetEnvironmentVariable("MONO_ADDINS_GLOBAL_REGISTRY");
            if (customDir != null && customDir.Length > 0)
                return Path.GetFullPath(Util.NormalizePath(customDir));

            var path = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
            path = Path.Combine(path, "mono.addins");
            return Path.GetFullPath(path);
        }
    }

    internal string CurrentDomain { get; private set; }

    /// <summary>
    ///     Location of the add-in registry.
    /// </summary>
    public string RegistryPath { get; }

    /// <summary>
    ///     Gets a value indicating whether there are pending add-ins to be uninstalled installed
    /// </summary>
    public bool HasPendingUninstalls => database.HasPendingUninstalls(CurrentDomain);

    /// <summary>
    ///     Gets the default add-ins folder of the registry.
    /// </summary>
    /// <remarks>
    ///     For every add-in registry there is an add-in folder where the registry will look for add-ins by default.
    ///     This folder is an "addins" subdirectory of the directory where the repository is located. In most cases,
    ///     this folder will only contain .addins files referencing other more convenient locations for add-ins.
    /// </remarks>
    public string DefaultAddinsFolder { get; }

    internal string AddinCachePath { get; }

    internal IEnumerable<string> GlobalAddinDirectories => addinDirs;

    internal string StartupDirectory { get; }

    /// <summary>
    ///     Disposes the add-in engine.
    /// </summary>
    public void Dispose()
    {
        database.Shutdown();
    }

    /// <summary>
    ///     Gets the global registry.
    /// </summary>
    /// <returns>
    ///     The global registry
    /// </returns>
    /// <remarks>
    ///     The global add-in registry is created in "~/.config/mono.addins",
    ///     and it is the default registry used when none is specified.
    /// </remarks>
    public static AddinRegistry GetGlobalRegistry()
    {
        return GetGlobalRegistry(null, null);
    }

    internal static AddinRegistry GetGlobalRegistry(AddinEngine engine, string startupDirectory)
    {
        string baseDir;
        if (Util.IsWindows)
            baseDir = Environment.GetFolderPath(Environment.SpecialFolder.CommonProgramFiles);
        else
            baseDir = "/etc";

        var globalDir = Path.Combine(baseDir, "mono.addins");

        return new AddinRegistry(engine, GlobalRegistryPath, startupDirectory, null, null, globalDir);
    }

    /// <summary>
    ///     Returns an add-in from the registry.
    /// </summary>
    /// <param name="id">
    ///     Identifier of the add-in.
    /// </param>
    /// <returns>
    ///     The add-in, or 'null' if not found.
    /// </returns>
    /// <remarks>
    ///     The add-in identifier may optionally include a version number, for example: "TextEditor.Xml,1.2"
    /// </remarks>
    public Addin GetAddin(string id)
    {
        if (CurrentDomain == AddinDatabase.UnknownDomain)
            return null;
        var ad = database.GetInstalledAddin(CurrentDomain, id);
        if (ad != null && IsRegisteredForUninstall(ad.Id))
            return null;
        return ad;
    }

    /// <summary>
    ///     Returns an add-in from the registry.
    /// </summary>
    /// <param name="id">
    ///     Identifier of the add-in.
    /// </param>
    /// <param name="exactVersionMatch">
    ///     'true' if the exact add-in version must be found.
    /// </param>
    /// <returns>
    ///     The add-in, or 'null' if not found.
    /// </returns>
    /// <remarks>
    ///     The add-in identifier may optionally include a version number, for example: "TextEditor.Xml,1.2".
    ///     In this case, if the exact version is not found and exactVersionMatch is 'false', it will
    ///     return one than is compatible with the required version.
    /// </remarks>
    public Addin GetAddin(string id, bool exactVersionMatch)
    {
        if (CurrentDomain == AddinDatabase.UnknownDomain)
            return null;
        var ad = database.GetInstalledAddin(CurrentDomain, id, exactVersionMatch);
        if (ad != null && IsRegisteredForUninstall(ad.Id))
            return null;
        return ad;
    }

    /// <summary>
    ///     Gets all add-ins or add-in roots registered in the registry.
    /// </summary>
    /// <returns>
    ///     The addins.
    /// </returns>
    /// <param name='flags'>
    ///     Flags.
    /// </param>
    public Addin[] GetModules(AddinSearchFlags flags)
    {
        if (CurrentDomain == AddinDatabase.UnknownDomain)
            return new Addin [0];
        var f = (AddinSearchFlagsInternal)(int)flags;
        return database.GetInstalledAddins(CurrentDomain, f | AddinSearchFlagsInternal.ExcludePendingUninstall)
            .ToArray();
    }

    /// <summary>
    ///     Gets all add-ins registered in the registry.
    /// </summary>
    /// <returns>
    ///     Add-ins registered in the registry.
    /// </returns>
    public Addin[] GetAddins()
    {
        return GetModules(AddinSearchFlags.IncludeAddins);
    }

    /// <summary>
    ///     Gets all add-in roots registered in the registry.
    /// </summary>
    /// <returns>
    ///     Descriptions of all add-in roots.
    /// </returns>
    public Addin[] GetAddinRoots()
    {
        return GetModules(AddinSearchFlags.IncludeRoots);
    }

    /// <summary>
    ///     Loads an add-in description
    /// </summary>
    /// <param name="progressStatus">
    ///     Progress tracker.
    /// </param>
    /// <param name="file">
    ///     Name of the file to load
    /// </param>
    /// <returns>
    ///     An add-in description
    /// </returns>
    /// <remarks>
    ///     This method loads an add-in description from a file. The file can be an XML manifest or an
    ///     assembly that implements an add-in.
    /// </remarks>
    public AddinDescription GetAddinDescription(IProgressStatus progressStatus, string file)
    {
        if (CurrentDomain == AddinDatabase.UnknownDomain)
            return null;
        var outFile = Path.GetTempFileName();
        try
        {
            database.ParseAddin(progressStatus, CurrentDomain, file, outFile, false);
        }
        catch
        {
            File.Delete(outFile);
            throw;
        }

        try
        {
            var desc = AddinDescription.Read(outFile);
            if (desc != null)
            {
                desc.AddinFile = file;
                desc.OwnerDatabase = database;
            }

            return desc;
        }
        catch
        {
            // Errors are already reported using the progress status object
            return null;
        }
        finally
        {
            File.Delete(outFile);
        }
    }

    /// <summary>
    ///     Reads an XML add-in manifest
    /// </summary>
    /// <param name="file">
    ///     Path to the XML file
    /// </param>
    /// <returns>
    ///     An add-in description
    /// </returns>
    public AddinDescription ReadAddinManifestFile(string file)
    {
        var desc = AddinDescription.Read(file);
        if (CurrentDomain != AddinDatabase.UnknownDomain)
        {
            desc.OwnerDatabase = database;
            desc.Domain = CurrentDomain;
        }

        return desc;
    }

    /// <summary>
    ///     Reads an XML add-in manifest
    /// </summary>
    /// <param name="reader">
    ///     Reader that contains the XML
    /// </param>
    /// <param name="baseFile">
    ///     Base path to use to discover add-in files
    /// </param>
    /// <returns>
    ///     An add-in description
    /// </returns>
    public AddinDescription ReadAddinManifestFile(TextReader reader, string baseFile)
    {
        if (CurrentDomain == AddinDatabase.UnknownDomain)
            return null;
        var desc = AddinDescription.Read(reader, baseFile);
        desc.OwnerDatabase = database;
        desc.Domain = CurrentDomain;
        return desc;
    }

    /// <summary>
    ///     Checks whether an add-in is enabled.
    /// </summary>
    /// <param name="id">
    ///     Identifier of the add-in.
    /// </param>
    /// <returns>
    ///     'true' if the add-in is enabled.
    /// </returns>
    public bool IsAddinEnabled(string id)
    {
        if (CurrentDomain == AddinDatabase.UnknownDomain)
            return false;
        return database.IsAddinEnabled(CurrentDomain, id);
    }

    /// <summary>
    ///     Enables an add-in.
    /// </summary>
    /// <param name="id">
    ///     Identifier of the add-in
    /// </param>
    /// <remarks>
    ///     If the enabled add-in depends on other add-ins which are disabled,
    ///     those will automatically be enabled too.
    /// </remarks>
    public void EnableAddin(string id)
    {
        if (CurrentDomain == AddinDatabase.UnknownDomain)
            return;
        database.EnableAddin(CurrentDomain, id, true);
    }

    /// <summary>
    ///     Disables an add-in.
    /// </summary>
    /// <param name="id">
    ///     Identifier of the add-in.
    /// </param>
    /// <remarks>
    ///     When an add-in is disabled, all extension points it defines will be ignored
    ///     by the add-in engine. Other add-ins which depend on the disabled add-in will
    ///     also automatically be disabled.
    /// </remarks>
    public void DisableAddin(string id)
    {
        if (CurrentDomain == AddinDatabase.UnknownDomain)
            return;
        database.DisableAddin(CurrentDomain, id);
    }

    /// <summary>
    ///     Disables an add-in.
    /// </summary>
    /// <param name="id">
    ///     Identifier of the add-in.
    /// </param>
    /// <param name="exactVersionMatch">
    ///     If true, it disables the add-in that exactly matches the provided version. If false, it disables
    ///     all versions of add-ins with the same Id
    /// </param>
    /// <remarks>
    ///     When an add-in is disabled, all extension points it defines will be ignored
    ///     by the add-in engine. Other add-ins which depend on the disabled add-in will
    ///     also automatically be disabled.
    /// </remarks>
    public void DisableAddin(string id, bool exactVersionMatch)
    {
        if (CurrentDomain == AddinDatabase.UnknownDomain)
            return;
        database.DisableAddin(CurrentDomain, id, exactVersionMatch);
    }

    /// <summary>
    ///     Registers a set of add-ins for uninstallation.
    /// </summary>
    /// <param name='id'>
    ///     Identifier of the add-in
    /// </param>
    /// <param name='files'>
    ///     Files to be uninstalled
    /// </param>
    /// <remarks>
    ///     This method can be used to instruct the add-in manager to uninstall
    ///     an add-in the next time the registry is updated. This is useful
    ///     when an add-in manager can't delete an add-in because if it is
    ///     loaded.
    /// </remarks>
    public void RegisterForUninstall(string id, IEnumerable<string> files)
    {
        database.RegisterForUninstall(CurrentDomain, id, files);
    }

    /// <summary>
    ///     Determines whether an add-in is registered for uninstallation
    /// </summary>
    /// <returns>
    ///     <c>true</c> if the add-in is registered for uninstallation
    /// </returns>
    /// <param name='addinId'>
    ///     Identifier of the add-in
    /// </param>
    public bool IsRegisteredForUninstall(string addinId)
    {
        return database.IsRegisteredForUninstall(CurrentDomain, addinId);
    }

    /// <summary>
    ///     Internal use only
    /// </summary>
    public void DumpFile(string file)
    {
        BinaryXmlReader.DumpFile(file);
    }

    /// <summary>
    ///     Resets the configuration files of the registry
    /// </summary>
    public void ResetConfiguration()
    {
        database.ResetConfiguration();
    }

    internal void NotifyDatabaseUpdated()
    {
        if (StartupDirectory != null)
            CurrentDomain = database.GetFolderDomain(null, StartupDirectory);
    }

    /// <summary>
    ///     Updates the add-in registry.
    /// </summary>
    /// <remarks>
    ///     This method must be called after modifying, installing or uninstalling add-ins.
    ///     When calling Update, every add-in added to the registry is parsed and validated,
    ///     and if there is any error it will be rejected. It will also cache add-in information
    ///     needed at run-time.
    ///     If during the update operation the registry finds new add-ins or detects that some
    ///     add-ins have been deleted, the loaded extension points will be updated to include
    ///     or exclude extension nodes from those add-ins.
    /// </remarks>
    public void Update()
    {
        Update(new ConsoleProgressStatus(false));
    }

    /// <summary>
    ///     Updates the add-in registry.
    /// </summary>
    /// <param name="monitor">
    ///     Progress monitor to keep track of the update operation.
    /// </param>
    /// <remarks>
    ///     This method must be called after modifying, installing or uninstalling add-ins.
    ///     When calling Update, every add-in added to the registry is parsed and validated,
    ///     and if there is any error it will be rejected. It will also cache add-in information
    ///     needed at run-time.
    ///     If during the update operation the registry finds new add-ins or detects that some
    ///     add-ins have been deleted, the loaded extension points will be updated to include
    ///     or exclude extension nodes from those add-ins.
    /// </remarks>
    public void Update(IProgressStatus monitor)
    {
        database.Update(monitor, CurrentDomain);
    }

    internal void Update(IProgressStatus monitor, AddinEngineTransaction addinEngineTransaction)
    {
        database.Update(monitor, CurrentDomain, addinEngineTransaction: addinEngineTransaction);
    }

    /// <summary>
    ///     Regenerates the cached data of the add-in registry.
    /// </summary>
    /// <param name="monitor">
    ///     Progress monitor to keep track of the rebuild operation.
    /// </param>
    public void Rebuild(IProgressStatus monitor)
    {
        var context = new ScanOptions();
        context.CleanGeneratedAddinScanDataFiles = true;
        database.Repair(monitor, CurrentDomain, context);

        // A full rebuild may cause the domain to change
        if (!string.IsNullOrEmpty(StartupDirectory))
            CurrentDomain = database.GetFolderDomain(null, StartupDirectory);
    }

    /// <summary>
    ///     Generates add-in data cache files for add-ins in the provided folder
    ///     and any other directory included through a .addins file.
    ///     If folder is not provided, it scans the startup directory.
    /// </summary>
    /// <param name="monitor">
    ///     Progress monitor to keep track of the rebuild operation.
    /// </param>
    /// <param name="folder">
    ///     Folder that contains the add-ins to be scanned.
    /// </param>
    /// <param name="recursive">
    ///     If true, sub-directories are scanned recursively
    /// </param>
    public void GenerateAddinScanDataFiles(IProgressStatus monitor, string folder = null, bool recursive = false)
    {
        database.GenerateScanDataFiles(monitor, folder ?? StartupDirectory, recursive);
    }

    /// <summary>
    ///     Registers an extension. Only AddinFileSystemExtension extensions are supported right now.
    /// </summary>
    /// <param name='extension'>
    ///     The extension to register
    /// </param>
    public void RegisterExtension(object extension)
    {
        database.RegisterExtension(extension);
    }

    /// <summary>
    ///     Unregisters an extension.
    /// </summary>
    /// <param name='extension'>
    ///     The extension to unregister
    /// </param>
    public void UnregisterExtension(object extension)
    {
        database.UnregisterExtension(extension);
    }

    internal void CopyExtensionsFrom(AddinRegistry other)
    {
        database.CopyExtensions(other.database);
    }

    internal Addin GetAddinForHostAssembly(string filePath)
    {
        if (CurrentDomain == AddinDatabase.UnknownDomain)
            return null;
        return database.GetAddinForHostAssembly(CurrentDomain, filePath);
    }

    internal bool AddinDependsOn(string id1, string id2)
    {
        return database.AddinDependsOn(CurrentDomain, id1, id2);
    }

    internal void ScanFolders(IProgressStatus monitor, string folderToScan, ScanOptions context)
    {
        database.ScanFolders(monitor, CurrentDomain, folderToScan, context);
    }

    internal void GenerateScanDataFilesInProcess(IProgressStatus monitor, string folderToScan, bool recursive)
    {
        database.GenerateScanDataFilesInProcess(monitor, folderToScan, recursive);
    }

    internal void ParseAddin(IProgressStatus progressStatus, string file, string outFile)
    {
        database.ParseAddin(progressStatus, CurrentDomain, file, outFile, true);
    }

    internal void RegisterGlobalAddinDirectory(string dir)
    {
    }

    internal bool CreateHostAddinsFile(string hostFile)
    {
        hostFile = Path.GetFullPath(hostFile);
        var baseName = Path.GetFileNameWithoutExtension(hostFile);
        if (!Directory.Exists(database.HostsPath))
            Directory.CreateDirectory(database.HostsPath);

        foreach (var s in Directory.EnumerateFiles(database.HostsPath, baseName + "*.addins"))
            try
            {
                using (var sr = new StreamReader(s))
                {
                    var tr = new XmlTextReader(sr);
                    tr.MoveToContent();
                    var host = tr.GetAttribute("host-reference");
                    if (host == hostFile)
                        return false;
                }
            }
            catch
            {
                // Ignore this file
            }

        var file = Path.Combine(database.HostsPath, baseName) + ".addins";
        var n = 1;
        while (File.Exists(file))
        {
            file = Path.Combine(database.HostsPath, baseName) + "_" + n + ".addins";
            n++;
        }

        using (var sw = new StreamWriter(file))
        {
            var tw = new XmlTextWriter(sw);
            tw.Formatting = Formatting.Indented;
            tw.WriteStartElement("Addins");
            tw.WriteAttributeString("host-reference", hostFile);
            tw.WriteStartElement("Directory");
            tw.WriteAttributeString("shared", "false");
            tw.WriteString(Path.GetDirectoryName(hostFile));
            tw.WriteEndElement();
            tw.Close();
        }

        return true;
    }

#pragma warning disable 1591
    [Obsolete]
    public static string[] GetRegisteredStartupFolders(string registryPath)
    {
        var dbDir = Path.Combine(registryPath, "addin-db-" + AddinDatabase.VersionTag);
        dbDir = Path.Combine(dbDir, "hosts");

        if (!Directory.Exists(dbDir))
            return new string [0];

        var dirs = new ArrayList();

        foreach (var s in Directory.GetFiles(dbDir, "*.addins"))
            try
            {
                using (var sr = new StreamReader(s))
                {
                    var tr = new XmlTextReader(sr);
                    tr.MoveToContent();
                    var host = tr.GetAttribute("host-reference");
                    host = Path.GetDirectoryName(host);
                    if (!dirs.Contains(host))
                        dirs.Add(host);
                }
            }
            catch
            {
                // Ignore this file
            }

        return (string[])dirs.ToArray(typeof(string));
    }
#pragma warning restore 1591
}

/// <summary>
///     Addin search flags.
/// </summary>
[Flags]
public enum AddinSearchFlags
{
	/// <summary>
	///     Add-ins are included in the search
	/// </summary>
	IncludeAddins = 1,

	/// <summary>
	///     Add-in roots are included in the search
	/// </summary>
	IncludeRoots = 1 << 1,

	/// <summary>
	///     Both add-in and add-in roots are included in the search
	/// </summary>
	IncludeAll = IncludeAddins | IncludeRoots,

	/// <summary>
	///     Only the latest version of every add-in or add-in root is included in the search
	/// </summary>
	LatestVersionsOnly = 1 << 3
}