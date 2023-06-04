//
// SetupService.cs
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
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Xml;
using ICSharpCode.SharpZipLib.Zip;
using Mono.Addins.Database;
using Mono.Addins.Description;
using Mono.Addins.Setup.ProgressMonitoring;
using Mono.PkgConfig;

namespace Mono.Addins.Setup;

/// <summary>
///     Provides tools for managing add-ins
/// </summary>
/// <remarks>
///     This class can be used to manage the add-ins of an application. It allows installing and uninstalling
///     add-ins, taking into account add-in dependencies. It provides methods for installing add-ins from on-line
///     repositories and tools for generating those repositories.
/// </remarks>
public class SetupService
{
    private const string addinFilesDir = "_addin_files";

    private static AddinsPcFileCache pcFileCache;
    private AddinSystemConfiguration config;
    private string installDirectory;

    private readonly Dictionary<string, AddinRepositoryProvider> providersList = new();

    /// <summary>
    ///     Initializes a new instance
    /// </summary>
    /// <remarks>
    ///     If the add-in manager is initialized (AddinManager.Initialize has been called), then this instance
    ///     will manage the add-in registry of the initialized engine.
    /// </remarks>
    public SetupService()
    {
        if (AddinManager.IsInitialized)
            Registry = AddinManager.Registry;
        else
            Registry = AddinRegistry.GetGlobalRegistry();

        Repositories = new RepositoryRegistry(this);
        Store = new AddinStore(this);
        AddAddinRepositoryProvider("MonoAddins", new MonoAddinsRepositoryProvider(this));
    }

    /// <summary>
    ///     Initializes a new instance
    /// </summary>
    /// <param name="registry">
    ///     Add-in registry to manage
    /// </param>
    public SetupService(AddinRegistry registry)
    {
        Registry = registry;
        Repositories = new RepositoryRegistry(this);
        Store = new AddinStore(this);
        AddAddinRepositoryProvider("MonoAddins", new MonoAddinsRepositoryProvider(this));
    }

    /// <summary>
    ///     The add-in registry being managed
    /// </summary>
    public AddinRegistry Registry { get; }

    internal string RepositoryCachePath => Path.Combine(Registry.RegistryPath, "repository-cache");

    private string RootConfigFile => Path.Combine(Registry.RegistryPath, "addins-setup-v2.config");

    /// <summary>
    ///     This should only be used for migration purposes
    /// </summary>
    private string RootConfigFileOld => Path.Combine(Registry.RegistryPath, "addins-setup.config");

    /// <summary>
    ///     Default add-in namespace of the application (optional). If set, only add-ins that belong to that namespace
    ///     will be shown in add-in lists.
    /// </summary>
    public string ApplicationNamespace { get; set; }

    /// <summary>
    ///     Directory where to install add-ins. If not specified, the 'addins' subdirectory of the
    ///     registry location is used.
    /// </summary>
    public string InstallDirectory
    {
        get
        {
            if (installDirectory != null && installDirectory.Length > 0)
                return installDirectory;
            return Registry.DefaultAddinsFolder;
        }
        set => installDirectory = value;
    }

    /// <summary>
    ///     Returns a RepositoryRegistry which can be used to manage on-line repository references
    /// </summary>
    public RepositoryRegistry Repositories { get; }

    internal AddinStore Store { get; }

    internal AddinSystemConfiguration Configuration
    {
        get
        {
            if (config == null)
            {
                if (File.Exists(RootConfigFile))
                    config = (AddinSystemConfiguration)AddinStore.ReadObject(RootConfigFile,
                        typeof(AddinSystemConfiguration));
                else
                    config = (AddinSystemConfiguration)AddinStore.ReadObject(RootConfigFileOld,
                        typeof(AddinSystemConfiguration));
                if (config == null)
                    config = new AddinSystemConfiguration();
            }

            return config;
        }
    }

    /// <summary>
    ///     Resolves add-in dependencies.
    /// </summary>
    /// <param name="statusMonitor">
    ///     Progress monitor where to show progress status
    /// </param>
    /// <param name="addins">
    ///     List of add-ins to check
    /// </param>
    /// <param name="resolved">
    ///     Packages that need to be installed.
    /// </param>
    /// <param name="toUninstall">
    ///     Packages that need to be uninstalled.
    /// </param>
    /// <param name="unresolved">
    ///     Add-in dependencies that could not be resolved.
    /// </param>
    /// <returns>
    ///     True if all dependencies could be resolved.
    /// </returns>
    /// <remarks>
    ///     This method can be used to get a list of all packages that have to be installed in order to install
    ///     an add-in or set of add-ins. The list of packages to install will include the package that provides the
    ///     add-in, and all packages that provide the add-in dependencies. In some cases, packages may need to
    ///     be installed (for example, when an installed add-in needs to be upgraded).
    /// </remarks>
    public bool ResolveDependencies(IProgressStatus statusMonitor, AddinRepositoryEntry[] addins,
        out PackageCollection resolved, out PackageCollection toUninstall, out DependencyCollection unresolved)
    {
        return Store.ResolveDependencies(statusMonitor, addins, out resolved, out toUninstall, out unresolved);
    }

    /// <summary>
    ///     Resolves add-in dependencies.
    /// </summary>
    /// <param name="statusMonitor">
    ///     Progress monitor where to show progress status
    /// </param>
    /// <param name="packages">
    ///     Packages that need to be installed.
    /// </param>
    /// <param name="toUninstall">
    ///     Packages that need to be uninstalled.
    /// </param>
    /// <param name="unresolved">
    ///     Add-in dependencies that could not be resolved.
    /// </param>
    /// <returns>
    ///     True if all dependencies could be resolved.
    /// </returns>
    /// <remarks>
    ///     This method can be used to get a list of all packages that have to be installed in order to satisfy
    ///     the dependencies of a package or set of packages. The 'packages' argument must have the list of packages
    ///     to be resolved. When resolving dependencies, if there is any additional package that needs to be installed,
    ///     it will be added to the same 'packages' collection. In some cases, packages may need to
    ///     be installed (for example, when an installed add-in needs to be upgraded). Those packages will be added
    ///     to the 'toUninstall' collection. Packages that could not be resolved are added to the 'unresolved'
    ///     collection.
    /// </remarks>
    public bool ResolveDependencies(IProgressStatus statusMonitor, PackageCollection packages,
        out PackageCollection toUninstall, out DependencyCollection unresolved)
    {
        return Store.ResolveDependencies(statusMonitor, packages, out toUninstall, out unresolved);
    }

    /// <summary>
    ///     Installs add-in packages
    /// </summary>
    /// <param name="statusMonitor">
    ///     Progress monitor where to show progress status
    /// </param>
    /// <param name="files">
    ///     Paths to the packages to install
    /// </param>
    /// <returns>
    ///     True if the installation succeeded
    /// </returns>
    public bool Install(IProgressStatus statusMonitor, params string[] files)
    {
        return Store.Install(statusMonitor, files);
    }

    /// <summary>
    ///     Installs add-in packages from on-line repositories
    /// </summary>
    /// <param name="statusMonitor">
    ///     Progress monitor where to show progress status
    /// </param>
    /// <param name="addins">
    ///     References to the add-ins to be installed
    /// </param>
    /// <returns>
    ///     True if the installation succeeded
    /// </returns>
    public bool Install(IProgressStatus statusMonitor, params AddinRepositoryEntry[] addins)
    {
        return Store.Install(statusMonitor, addins);
    }

    /// <summary>
    ///     Installs add-in packages
    /// </summary>
    /// <param name="statusMonitor">
    ///     Progress monitor where to show progress status
    /// </param>
    /// <param name="packages">
    ///     Packages to install
    /// </param>
    /// <returns>
    ///     True if the installation succeeded
    /// </returns>
    public bool Install(IProgressStatus statusMonitor, PackageCollection packages)
    {
        return Store.Install(statusMonitor, packages);
    }

    /// <summary>
    ///     Uninstalls an add-in.
    /// </summary>
    /// <param name="statusMonitor">
    ///     Progress monitor where to show progress status
    /// </param>
    /// <param name="id">
    ///     Full identifier of the add-in to uninstall.
    /// </param>
    public void Uninstall(IProgressStatus statusMonitor, string id)
    {
        Store.Uninstall(statusMonitor, id);
    }

    /// <summary>
    ///     Uninstalls a set of add-ins
    /// </summary>
    /// <param name='statusMonitor'>
    ///     Progress monitor where to show progress status
    /// </param>
    /// <param name='ids'>
    ///     Full identifiers of the add-ins to uninstall.
    /// </param>
    public void Uninstall(IProgressStatus statusMonitor, IEnumerable<string> ids)
    {
        Store.Uninstall(statusMonitor, ids);
    }

    /// <summary>
    ///     Gets information about an add-in
    /// </summary>
    /// <param name="addin">
    ///     The add-in
    /// </param>
    /// <returns>
    ///     Add-in header data
    /// </returns>
    public static AddinHeader GetAddinHeader(Addin addin)
    {
        return AddinInfo.ReadFromDescription(addin.Description);
    }

    public static AddinHeader ReadAddinHeader(string mpack)
    {
        return AddinPackage.ReadAddinInfo(mpack);
    }

    public AddinRepositoryProvider GetAddinRepositoryProvider(string providerId)
    {
        if (string.IsNullOrEmpty(providerId))
            providerId = "MonoAddins";
        if (providersList.TryGetValue(providerId, out var addinRepositoryProvider))
            return addinRepositoryProvider;
        throw new KeyNotFoundException(providerId);
    }

    public void AddAddinRepositoryProvider(string providerId, AddinRepositoryProvider provider)
    {
        providersList[providerId] = provider;
    }

    public void RemoveAddinRepositoryProvider(string providerId)
    {
        providersList.Remove(providerId);
    }

    /// <summary>
    ///     Gets a list of add-ins which depend on an add-in
    /// </summary>
    /// <param name="id">
    ///     Full identifier of an add-in.
    /// </param>
    /// <param name="recursive">
    ///     When set to True, dependencies will be gathered recursivelly
    /// </param>
    /// <returns>
    ///     List of dependent add-ins.
    /// </returns>
    /// <remarks>
    ///     This methods returns a list of add-ins which have the add-in identified by 'id' as a direct
    ///     (or indirect if recursive=True) dependency.
    /// </remarks>
    public Addin[] GetDependentAddins(string id, bool recursive)
    {
        return Store.GetDependentAddins(id, recursive);
    }

    /// <summary>
    ///     Packages an add-in
    /// </summary>
    /// <param name="statusMonitor">
    ///     Progress monitor where to show progress status
    /// </param>
    /// <param name="targetDirectory">
    ///     Directory where to generate the package
    /// </param>
    /// <param name="filePaths">
    ///     Paths to the add-ins to be packaged. Paths can be either the main assembly of an add-in, or an add-in
    ///     manifest (.addin or .addin.xml).
    /// </param>
    /// <remarks>
    ///     This method can be used to create a package for an add-in, which can then be pushed to an on-line
    ///     repository. The package will include the main assembly or manifest of the add-in and any external
    ///     file declared in the add-in metadata.
    /// </remarks>
    public string[] BuildPackage(IProgressStatus statusMonitor, string targetDirectory, params string[] filePaths)
    {
        return BuildPackage(statusMonitor, false, targetDirectory, filePaths);
    }

    /// <summary>
    ///     Packages an add-in
    /// </summary>
    /// <param name="statusMonitor">
    ///     Progress monitor where to show progress status
    /// </param>
    /// <param name="debugSymbols">
    ///     True if debug symbols (.pdb or .mdb) should be included in the package, if they exist
    /// </param>
    /// <param name="targetDirectory">
    ///     Directory where to generate the package
    /// </param>
    /// <param name="filePaths">
    ///     Paths to the add-ins to be packaged. Paths can be either the main assembly of an add-in, or an add-in
    ///     manifest (.addin or .addin.xml).
    /// </param>
    /// <remarks>
    ///     This method can be used to create a package for an add-in, which can then be pushed to an on-line
    ///     repository. The package will include the main assembly or manifest of the add-in and any external
    ///     file declared in the add-in metadata.
    /// </remarks>
    public string[] BuildPackage(IProgressStatus statusMonitor, bool debugSymbols, string targetDirectory,
        params string[] filePaths)
    {
        var outFiles = new List<string>();
        foreach (var file in filePaths)
        {
            var f = BuildPackageInternal(statusMonitor, debugSymbols, targetDirectory, file, PackageFormat.Mpack);
            if (f != null)
                outFiles.Add(f);
        }

        return outFiles.ToArray();
    }

    /// <summary>
    ///     Packages an add-in
    /// </summary>
    /// <param name="statusMonitor">
    ///     Progress monitor where to show progress status
    /// </param>
    /// <param name="debugSymbols">
    ///     True if debug symbols (.pdb or .mdb) should be included in the package, if they exist
    /// </param>
    /// <param name="targetDirectory">
    ///     Directory where to generate the package
    /// </param>
    /// <param name="format">
    ///     Which format to produce .mpack or .vsix
    /// </param>
    /// <param name="filePaths">
    ///     Paths to the add-ins to be packaged. Paths can be either the main assembly of an add-in, or an add-in
    ///     manifest (.addin or .addin.xml).
    /// </param>
    /// <remarks>
    ///     This method can be used to create a package for an add-in, which can then be pushed to an on-line
    ///     repository. The package will include the main assembly or manifest of the add-in and any external
    ///     file declared in the add-in metadata.
    /// </remarks>
    public string[] BuildPackage(IProgressStatus statusMonitor, bool debugSymbols, string targetDirectory,
        PackageFormat format, params string[] filePaths)
    {
        var outFiles = new List<string>();
        foreach (var file in filePaths)
        {
            var f = BuildPackageInternal(statusMonitor, debugSymbols, targetDirectory, file, format);
            if (f != null)
                outFiles.Add(f);
        }

        return outFiles.ToArray();
    }

    private string BuildPackageInternal(IProgressStatus monitor, bool debugSymbols, string targetDirectory,
        string filePath, PackageFormat format)
    {
        var conf = Registry.GetAddinDescription(monitor, filePath);
        if (conf == null)
        {
            monitor.ReportError("Could not read add-in file: " + filePath, null);
            return null;
        }

        var basePath = Path.GetDirectoryName(Path.GetFullPath(filePath));

        if (targetDirectory == null)
            targetDirectory = basePath;

        // Generate the file name

        string localId;
        if (conf.LocalId.Length == 0)
            localId = Path.GetFileNameWithoutExtension(filePath);
        else
            localId = conf.LocalId;

        string ext;
        var name = Addin.GetFullId(conf.Namespace, localId, null);
        var version = conf.Version;

        switch (format)
        {
            case PackageFormat.Mpack:
                ext = ".mpack";
                break;
            case PackageFormat.Vsix:
                ext = ".vsix";
                break;
            case PackageFormat.NuGet:
                ext = ".nupkg";
                if (string.IsNullOrEmpty(version))
                {
                    monitor.ReportError("Add-in doesn't have a version", null);
                    return null;
                }

                version = GetNuGetVersion(version);
                break;
            default:
                throw new NotSupportedException(format.ToString());
        }

        var outFilePath = Path.Combine(targetDirectory, name);
        if (!string.IsNullOrEmpty(version))
            outFilePath += "." + version;
        outFilePath += ext;

        var s = new ZipOutputStream(File.Create(outFilePath));
        s.SetLevel(5);

        if (format == PackageFormat.Vsix)
        {
            var doc = new XmlDocument();
            doc.PreserveWhitespace = false;
            doc.LoadXml(conf.SaveToVsixXml().OuterXml);
            AddXmlFile(s, doc, "extension.vsixmanifest");
        }

        if (format == PackageFormat.NuGet)
        {
            var doc = GenerateNuspec(conf);
            AddXmlFile(s, doc, Addin.GetIdName(conf.AddinId).ToLower() + ".nuspec");
        }

        // Generate a stripped down description of the add-in in a file, since the complete
        // description may be declared as assembly attributes

        var infoDoc = new XmlDocument();
        infoDoc.PreserveWhitespace = false;
        infoDoc.LoadXml(conf.SaveToXml().OuterXml);
        CleanDescription(infoDoc.DocumentElement);

        AddXmlFile(s, infoDoc, "addin.info");

        // Now add the add-in files

        var files = new HashSet<string>();

        files.Add(Path.GetFileName(Util.NormalizePath(filePath)));

        foreach (var f in conf.AllFiles)
        {
            var file = Util.NormalizePath(f);
            files.Add(file);
            if (debugSymbols)
            {
                if (File.Exists(Path.ChangeExtension(file, ".pdb")))
                    files.Add(Path.ChangeExtension(file, ".pdb"));
                else if (File.Exists(file + ".mdb"))
                    files.Add(file + ".mdb");
            }
        }

        foreach (var prop in conf.Properties)
            try
            {
                var file = Util.NormalizePath(prop.Value);
                if (File.Exists(Path.Combine(basePath, file))) files.Add(file);
            }
            catch
            {
                // Ignore errors
            }

        //add satellite assemblies for assemblies in the list
        var satelliteFinder = new SatelliteAssemblyFinder();
        foreach (var f in files.ToList())
        foreach (var satellite in satelliteFinder.FindSatellites(Path.Combine(basePath, f)))
        {
            var relativeSatellite = satellite.Substring(basePath.Length + 1);
            files.Add(relativeSatellite);
        }

        monitor.Log("Creating package " + Path.GetFileName(outFilePath));

        foreach (var file in files)
        {
            var fp = Path.Combine(basePath, file);
            using (var fs = File.OpenRead(fp))
            {
                var buffer = new byte [fs.Length];
                fs.Read(buffer, 0, buffer.Length);

                var fileName = Path.DirectorySeparatorChar == '\\' ? file.Replace('\\', '/') : file;
                if (format == PackageFormat.NuGet)
                    fileName = Path.Combine("addin", fileName);
                var entry = new ZipEntry(fileName) { Size = fs.Length };
                s.PutNextEntry(entry);
                s.Write(buffer, 0, buffer.Length);
                s.CloseEntry();
            }
        }

        if (format == PackageFormat.Vsix)
        {
            files.Add("addin.info");
            files.Add("extension.vsixmanifest");
            var doc = new XmlDocument();
            doc.PreserveWhitespace = false;
            var xmlDeclaration = doc.CreateXmlDeclaration("1.0", "UTF-8", null);
            var root = doc.DocumentElement;
            doc.InsertBefore(xmlDeclaration, root);
            var alreadyAddedExtensions = new HashSet<string>();
            var typesEl = doc.CreateElement("Types");
            typesEl.SetAttribute("xmlns", "http://schemas.openxmlformats.org/package/2006/content-types");
            foreach (var file in files)
            {
                var extension = Path.GetExtension(file);
                if (string.IsNullOrEmpty(extension))
                    continue;
                if (extension.StartsWith(".", StringComparison.Ordinal))
                    extension = extension.Substring(1);
                if (alreadyAddedExtensions.Contains(extension))
                    continue;
                alreadyAddedExtensions.Add(extension);
                var typeEl = doc.CreateElement("Default");
                typeEl.SetAttribute("Extension", extension);
                typeEl.SetAttribute("ContentType", GetContentType(extension));
                typesEl.AppendChild(typeEl);
            }

            doc.AppendChild(typesEl);
            AddXmlFile(s, doc, "[Content_Types].xml");
        }

        s.Finish();
        s.Close();
        return outFilePath;
    }

    private static void AddXmlFile(ZipOutputStream s, XmlDocument doc, string fileName)
    {
        var ms = new MemoryStream();
        var tw = new XmlTextWriter(ms, Encoding.UTF8);
        tw.Formatting = Formatting.Indented;
        doc.WriteTo(tw);
        tw.Flush();
        var data = ms.ToArray();
        var infoEntry = new ZipEntry(fileName) { Size = data.Length };
        s.PutNextEntry(infoEntry);
        s.Write(data, 0, data.Length);
        s.CloseEntry();
    }

    private XmlDocument GenerateNuspec(AddinDescription conf)
    {
        var doc = new XmlDocument();
        var nugetNs = "http://schemas.microsoft.com/packaging/2013/05/nuspec.xsd";
        var rootElement = doc.CreateElement("package", nugetNs);
        doc.AppendChild(rootElement);
        var metadataElement = doc.CreateElement("metadata", nugetNs);
        rootElement.AppendChild(metadataElement);

        var prop = doc.CreateElement("id", nugetNs);
        prop.InnerText = Addin.GetIdName(conf.AddinId);
        metadataElement.AppendChild(prop);

        prop = doc.CreateElement("version", nugetNs);
        prop.InnerText = GetNuGetVersion(conf.Version);
        metadataElement.AppendChild(prop);

        prop = doc.CreateElement("packageTypes", nugetNs);
        prop.InnerText = "VisualStudioMacExtension";
        metadataElement.AppendChild(prop);

        if (!string.IsNullOrEmpty(conf.Author))
        {
            prop = doc.CreateElement("authors", nugetNs);
            prop.InnerText = conf.Author;
            metadataElement.AppendChild(prop);
        }

        if (!string.IsNullOrEmpty(conf.Description))
        {
            prop = doc.CreateElement("description", nugetNs);
            prop.InnerText = conf.Description;
            metadataElement.AppendChild(prop);
        }

        if (!string.IsNullOrEmpty(conf.Name))
        {
            prop = doc.CreateElement("title", nugetNs);
            prop.InnerText = conf.Name;
            metadataElement.AppendChild(prop);
        }

        var depsElement = doc.CreateElement("dependencies", nugetNs);
        metadataElement.AppendChild(depsElement);

        foreach (var dep in conf.MainModule.Dependencies.OfType<AddinDependency>())
        {
            var depElem = doc.CreateElement("dependency", nugetNs);
            depElem.SetAttribute("id", Addin.GetFullId(conf.Namespace, dep.AddinId, null));
            depElem.SetAttribute("version", GetNuGetVersion(dep.Version));
            depsElement.AppendChild(depElem);
        }

        return doc;
    }

    private static string GetNuGetVersion(string version)
    {
        if (Version.TryParse(version, out var parsedVersion))
            // NuGet versions always have at least 3 components
            if (parsedVersion.Build == -1)
                version += ".0";
        return version;
    }

    private static string GetContentType(string extension)
    {
        switch (extension)
        {
            case "txt": return "text/plain";
            case "pkgdef": return "text/plain";
            case "xml": return "text/xml";
            case "vsixmanifest": return "text/xml";
            case "htm or html": return "text/html";
            case "rtf": return "application/rtf";
            case "pdf": return "application/pdf";
            case "gif": return "image/gif";
            case "jpg or jpeg": return "image/jpg";
            case "tiff": return "image/tiff";
            case "vsix": return "application/zip";
            case "zip": return "application/zip";
            case "dll": return "application/octet-stream";
            case "info": return "text/xml"; //Mono.Addins info file
            default: return "application/octet-stream";
        }
    }

    private void CleanDescription(XmlElement parent)
    {
        var todelete = new ArrayList();

        foreach (XmlNode nod in parent.ChildNodes)
        {
            var elem = nod as XmlElement;
            if (elem == null)
            {
                todelete.Add(nod);
                continue;
            }

            if (elem.LocalName == "Module")
                CleanDescription(elem);
            else if (elem.LocalName != "Dependencies" && elem.LocalName != "Runtime" && elem.LocalName != "Header")
                todelete.Add(elem);
        }

        foreach (XmlNode e in todelete)
            parent.RemoveChild(e);
    }

    /// <summary>
    ///     Generates an on-line repository
    /// </summary>
    /// <param name="statusMonitor">
    ///     Progress monitor where to show progress status
    /// </param>
    /// <param name="path">
    ///     Path to the directory that contains the add-ins and that is going to be published
    /// </param>
    /// <remarks>
    ///     This method generates the index files required to publish a directory as an online repository
    ///     of add-ins.
    /// </remarks>
    public void BuildRepository(IProgressStatus statusMonitor, string path)
    {
        var mainPath = Path.Combine(path, "main.mrep");
        var allAddins = new List<PackageRepositoryEntry>();

        var rootrep = (Repository)AddinStore.ReadObject(mainPath, typeof(Repository));
        if (rootrep == null)
            rootrep = new Repository();

        var monitor = ProgressStatusMonitor.GetProgressMonitor(statusMonitor);
        BuildRepository(monitor, rootrep, path, "root.mrep", allAddins);
        AddinStore.WriteObject(mainPath, rootrep);
        GenerateIndexPage(rootrep, allAddins, path);
        monitor.Log.WriteLine("Updated main.mrep");
    }

    private void BuildRepository(IProgressMonitor monitor, Repository rootrep, string rootPath, string relFilePath,
        List<PackageRepositoryEntry> allAddins)
    {
        var lastModified = DateTime.MinValue;

        var mainFile = Path.Combine(rootPath, relFilePath);
        var mainPath = Path.GetDirectoryName(mainFile);
        var supportFileDir = Path.Combine(mainPath, addinFilesDir);

        if (File.Exists(mainFile))
            lastModified = File.GetLastWriteTime(mainFile);

        var mainrep = (Repository)AddinStore.ReadObject(mainFile, typeof(Repository));
        if (mainrep == null) mainrep = new Repository();

        var repEntry = (ReferenceRepositoryEntry)rootrep.FindEntry(relFilePath);
        var rootLastModified = repEntry != null ? repEntry.LastModified : DateTime.MinValue;

        var modified = false;

        monitor.Log.WriteLine("Checking directory: " + mainPath);
        foreach (var file in Directory.EnumerateFiles(mainPath, "*.mpack"))
        {
            var date = File.GetLastWriteTime(file);
            var fname = Path.GetFileName(file);
            var entry = (PackageRepositoryEntry)mainrep.FindEntry(fname);

            if (entry != null && date > rootLastModified)
            {
                mainrep.RemoveEntry(entry);
                DeleteSupportFiles(supportFileDir, entry.Addin);
                entry = null;
            }

            if (entry == null)
            {
                entry = new PackageRepositoryEntry();
                var p = (AddinPackage)Package.FromFile(file);
                entry.Addin = (AddinInfo)p.Addin;
                entry.Url = fname;
                entry.Addin.Properties.SetPropertyValue("DownloadSize", new FileInfo(file).Length.ToString());
                ExtractSupportFiles(supportFileDir, file, entry.Addin);
                mainrep.AddEntry(entry);
                modified = true;
                monitor.Log.WriteLine("Added addin: " + fname);
            }

            allAddins.Add(entry);
        }

        var toRemove = new List<PackageRepositoryEntry>();
        foreach (PackageRepositoryEntry entry in mainrep.Addins)
            if (!File.Exists(Path.Combine(mainPath, entry.Url)))
            {
                toRemove.Add(entry);
                modified = true;
            }

        foreach (var entry in toRemove)
        {
            DeleteSupportFiles(supportFileDir, entry.Addin);
            mainrep.RemoveEntry(entry);
        }

        if (modified)
        {
            AddinStore.WriteObject(mainFile, mainrep);
            monitor.Log.WriteLine("Updated " + relFilePath);
            lastModified = File.GetLastWriteTime(mainFile);
        }

        if (repEntry != null)
        {
            if (repEntry.LastModified < lastModified)
                repEntry.LastModified = lastModified;
        }
        else if (modified)
        {
            repEntry = new ReferenceRepositoryEntry();
            repEntry.LastModified = lastModified;
            repEntry.Url = relFilePath;
            rootrep.AddEntry(repEntry);
        }

        foreach (var dir in Directory.EnumerateDirectories(mainPath))
        {
            if (Path.GetFileName(dir) == addinFilesDir)
                continue;
            var based = dir.Substring(rootPath.Length + 1);
            BuildRepository(monitor, rootrep, rootPath, Path.Combine(based, "main.mrep"), allAddins);
        }
    }

    private void DeleteSupportFiles(string targetDir, AddinInfo ainfo)
    {
        foreach (var prop in ainfo.Properties)
            if (prop.Value.StartsWith(addinFilesDir + Path.DirectorySeparatorChar))
            {
                var file = Path.Combine(targetDir, Path.GetFileName(prop.Value));
                if (File.Exists(file))
                    File.Delete(file);
            }

        if (Directory.Exists(targetDir) && !Directory.EnumerateFileSystemEntries(targetDir).Any())
            Directory.Delete(targetDir, true);
    }

    private void ExtractSupportFiles(string targetDir, string file, AddinInfo ainfo)
    {
        var r = new Random();
        var zfile = new ZipFile(file);
        try
        {
            foreach (var prop in ainfo.Properties)
            {
                var ze = zfile.GetEntry(prop.Value);
                if (ze != null)
                {
                    string fname;
                    do
                    {
                        fname = Path.Combine(targetDir, r.Next().ToString("x") + Path.GetExtension(prop.Value));
                    } while (File.Exists(fname));

                    if (!Directory.Exists(targetDir))
                        Directory.CreateDirectory(targetDir);

                    using (var f = File.OpenWrite(fname))
                    {
                        using (var s = zfile.GetInputStream(ze))
                        {
                            var buffer = new byte [8092];
                            var nr = 0;
                            while ((nr = s.Read(buffer, 0, buffer.Length)) > 0)
                                f.Write(buffer, 0, nr);
                        }
                    }

                    prop.Value = Path.Combine(addinFilesDir, Path.GetFileName(fname));
                }
            }
        }
        finally
        {
            zfile.Close();
        }
    }

    private void GenerateIndexPage(Repository rep, List<PackageRepositoryEntry> addins, string basePath)
    {
        var sw = new StreamWriter(Path.Combine(basePath, "index.html"));
        sw.WriteLine("<html><body>");
        sw.WriteLine("<h1>Add-in Repository</h1>");
        if (rep.Name != null && rep.Name != "")
            sw.WriteLine("<h2>" + rep.Name + "</h2>");
        sw.WriteLine("<p>This is a list of add-ins available in this repository.</p>");
        sw.WriteLine("<table border=1><thead><tr><th>Add-in</th><th>Version</th><th>Description</th></tr></thead>");

        foreach (var entry in addins)
            sw.WriteLine("<tr><td>" + entry.Addin.Name + "</td><td>" + entry.Addin.Version + "</td><td>" +
                         entry.Addin.Description + "</td></tr>");

        sw.WriteLine("</table>");
        sw.WriteLine("</body></html>");
        sw.Close();
    }

    internal void SaveConfiguration()
    {
        if (config != null) AddinStore.WriteObject(RootConfigFile, config);
    }

    internal void ResetConfiguration()
    {
        if (File.Exists(RootConfigFile))
            File.Delete(RootConfigFile);
        if (File.Exists(RootConfigFileOld))
            File.Delete(RootConfigFileOld);
        ResetAddinInfo();
    }

    internal void ResetAddinInfo()
    {
        if (Directory.Exists(RepositoryCachePath))
            Directory.Delete(RepositoryCachePath, true);
    }

    /// <summary>
    ///     Gets a reference to an extensible application
    /// </summary>
    /// <param name="name">
    ///     Name of the application
    /// </param>
    /// <returns>
    ///     The Application object. Null if not found.
    /// </returns>
    public static Application GetExtensibleApplication(string name)
    {
        return GetExtensibleApplication(name, null);
    }

    /// <summary>
    ///     Gets a reference to an extensible application
    /// </summary>
    /// <param name="name">
    ///     Name of the application
    /// </param>
    /// <param name="searchPaths">
    ///     Custom paths where to look for the application.
    /// </param>
    /// <returns>
    ///     The Application object. Null if not found.
    /// </returns>
    public static Application GetExtensibleApplication(string name, IEnumerable<string> searchPaths)
    {
        var pcc = GetAddinsPcFileCache(searchPaths);
        var pi = pcc.GetPackageInfoByName(name, searchPaths);
        if (pi != null)
            return new Application(pi);
        return null;
    }

    /// <summary>
    ///     Gets a lis of all known extensible applications
    /// </summary>
    /// <returns>
    ///     A list of applications.
    /// </returns>
    public static Application[] GetExtensibleApplications()
    {
        return GetExtensibleApplications(null);
    }

    /// <summary>
    ///     Gets a lis of all known extensible applications
    /// </summary>
    /// <param name="searchPaths">
    ///     Custom paths where to look for applications.
    /// </param>
    /// <returns>
    ///     A list of applications.
    /// </returns>
    public static Application[] GetExtensibleApplications(IEnumerable<string> searchPaths)
    {
        var list = new List<Application>();

        var pcc = GetAddinsPcFileCache(searchPaths);
        foreach (var pinfo in pcc.GetPackages(searchPaths))
            if (pinfo.IsValidPackage)
                list.Add(new Application(pinfo));
        return list.ToArray();
    }

    private static AddinsPcFileCache GetAddinsPcFileCache(IEnumerable<string> searchPaths)
    {
        if (pcFileCache == null)
        {
            pcFileCache = new AddinsPcFileCache();
            if (searchPaths != null)
                pcFileCache.Update(searchPaths);
            else
                pcFileCache.Update();
        }

        return pcFileCache;
    }

    private class SatelliteAssemblyFinder
    {
        private readonly HashSet<string> cultureNames = new(StringComparer.OrdinalIgnoreCase);
        private readonly Dictionary<string, List<string>> cultureSubdirCache = new();

        public SatelliteAssemblyFinder()
        {
            foreach (var cultureName in CultureInfo.GetCultures(CultureTypes.AllCultures))
                cultureNames.Add(cultureName.Name);
        }

        private List<string> GetCultureSubdirectories(string directory)
        {
            if (!cultureSubdirCache.TryGetValue(directory, out var cultureDirs))
            {
                cultureDirs = Directory.EnumerateDirectories(directory)
                    .Where(d => cultureNames.Contains(Path.GetFileName(d)))
                    .ToList();

                cultureSubdirCache[directory] = cultureDirs;
            }

            return cultureDirs;
        }

        public IEnumerable<string> FindSatellites(string assemblyPath)
        {
            if (!assemblyPath.EndsWith(".dll", StringComparison.OrdinalIgnoreCase)) yield break;

            var satelliteName = Path.GetFileNameWithoutExtension(assemblyPath) + ".resources.dll";

            foreach (var cultureDir in GetCultureSubdirectories(Path.GetDirectoryName(assemblyPath)))
            {
                var cultureName = Path.GetFileName(cultureDir);
                var satellitePath = Path.Combine(cultureDir, satelliteName);
                if (File.Exists(satellitePath)) yield return satellitePath;
            }
        }
    }
}

internal class AddinsPcFileCacheContext : IPcFileCacheContext
{
    public bool IsCustomDataComplete(string pcfile, PackageInfo pkg)
    {
        return true;
    }

    public void StoreCustomData(PcFile pcfile, PackageInfo pkg)
    {
    }

    public void ReportError(string message, Exception ex)
    {
        Console.WriteLine(message);
        Console.WriteLine(ex);
    }
}

internal class AddinsPcFileCache : PcFileCache
{
    public AddinsPcFileCache() : base(new AddinsPcFileCacheContext())
    {
    }

    protected override string CacheDirectory
    {
        get
        {
            var path = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
            path = Path.Combine(path, "mono.addins");
            return path;
        }
    }

    protected override void ParsePackageInfo(PcFile file, PackageInfo pinfo)
    {
        var rootPath = file.GetVariable("MonoAddinsRoot");
        var regPath = file.GetVariable("MonoAddinsRegistry");
        var addinsPath = file.GetVariable("MonoAddinsInstallPath");
        var databasePath = file.GetVariable("MonoAddinsCachePath");
        var testCmd = file.GetVariable("MonoAddinsTestCommand");
        if (string.IsNullOrEmpty(rootPath) || string.IsNullOrEmpty(regPath))
            return;
        pinfo.SetData("MonoAddinsRoot", rootPath);
        pinfo.SetData("MonoAddinsRegistry", regPath);
        pinfo.SetData("MonoAddinsInstallPath", addinsPath);
        pinfo.SetData("MonoAddinsCachePath", databasePath);
        pinfo.SetData("MonoAddinsTestCommand", testCmd);
    }
}

/// <summary>
///     A registered extensible application
/// </summary>
public class Application
{
    private AddinRegistry registry;

    internal Application(PackageInfo pinfo)
    {
        Name = pinfo.Name;
        Description = pinfo.Description;
        StartupPath = pinfo.GetData("MonoAddinsRoot");
        RegistryPath = pinfo.GetData("MonoAddinsRegistry");
        AddinsPath = pinfo.GetData("MonoAddinsInstallPath");
        AddinCachePath = pinfo.GetData("MonoAddinsCachePath");
        TestCommand = pinfo.GetData("MonoAddinsTestCommand");
    }

    /// <summary>
    ///     Add-in registry of the application
    /// </summary>
    public AddinRegistry Registry
    {
        get
        {
            if (registry == null)
                registry = new AddinRegistry(RegistryPath, StartupPath, AddinsPath, AddinCachePath);
            return registry;
        }
    }

    /// <summary>
    ///     Description of the application
    /// </summary>
    public string Description { get; }

    /// <summary>
    ///     Name of the application
    /// </summary>
    public string Name { get; }

    /// <summary>
    ///     Path to the add-in registry
    /// </summary>
    public string RegistryPath { get; }

    /// <summary>
    ///     Path to the directory that contains the main executable assembly of the application
    /// </summary>
    public string StartupPath { get; }

    /// <summary>
    ///     Command to be used to execute the application in add-in development mode.
    /// </summary>
    public string TestCommand { get; }

    /// <summary>
    ///     Path to the default add-ins directory for the aplpication
    /// </summary>
    public string AddinsPath { get; }

    /// <summary>
    ///     Path to the add-in cache for the application
    /// </summary>
    public string AddinCachePath { get; }
}