//
// AddinDatabase.cs
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
using System.Collections.Immutable;
using System.IO;
using System.Linq;
using System.Threading;
using Mono.Addins.Description;

namespace Mono.Addins.Database;

internal class AddinDatabase
{
    public const string GlobalDomain = "global";
    public const string UnknownDomain = "unknown";

    public const string VersionTag = "004";

    internal static bool RunningSetupProcess;

    private readonly FileDatabase fileDatabase;
    private readonly object localLock = new();
    private ImmutableArray<Addin> addinSetupInfos;
    private ImmutableArray<Addin> allSetupInfos;

    private bool allSetupInfosLoaded;

    private readonly Dictionary<string, Addin> cachedAddinSetupInfos = new();
    private DatabaseConfiguration config;

    private readonly Dictionary<string, HashSet<string>> dependsOnCache = new();
    private readonly List<object> extensions = new();

    private bool fatalDatabseError;
    private ImmutableAddinHostIndex hostIndex;
    private int lastDomainId;
    private ImmutableArray<Addin> rootSetupInfos;

    public AddinDatabase(AddinEngine addinEngine, AddinRegistry registry)
    {
        AddinEngine = addinEngine;
        Registry = registry;
        AddinDbDir = Path.Combine(registry.AddinCachePath, "addin-db-" + VersionTag);
        fileDatabase = new FileDatabase(AddinDbDir);
    }

    internal AddinEngine AddinEngine { get; }

    private string AddinDbDir { get; }

    public AddinFileSystemExtension FileSystem { get; private set; } = new();

    public string AddinCachePath => Path.Combine(AddinDbDir, "addin-data");

    public string AddinFolderCachePath => Path.Combine(AddinDbDir, "addin-dir-data");

    public string AddinPrivateDataPath => Path.Combine(AddinDbDir, "addin-priv-data");

    public string HostsPath => Path.Combine(AddinDbDir, "hosts");

    private string HostIndexFile => Path.Combine(AddinDbDir, "host-index");

    private string ConfigFile => Path.Combine(AddinDbDir, "config.xml");

    internal bool IsGlobalRegistry => Registry.RegistryPath == AddinRegistry.GlobalRegistryPath;

    public AddinRegistry Registry { get; }

    private DatabaseConfiguration Configuration
    {
        get
        {
            if (config == null)
                lock (localLock)
                {
                    using (fileDatabase.LockRead())
                    {
                        if (fileDatabase.Exists(ConfigFile))
                            config = DatabaseConfiguration.Read(ConfigFile);
                        else
                            config = DatabaseConfiguration.ReadAppConfig();
                    }
                }

            return config;
        }
    }

    public AddinDatabaseTransaction BeginTransaction(AddinEngineTransaction addinEngineTransaction = null)
    {
        return new AddinDatabaseTransaction(this, localLock, addinEngineTransaction);
    }

    public void CopyExtensions(AddinDatabase other)
    {
        lock (extensions)
        {
            foreach (var o in other.extensions)
                RegisterExtension(o);
        }
    }

    public void RegisterExtension(object extension)
    {
        lock (extensions)
        {
            extensions.Add(extension);
            if (extension is AddinFileSystemExtension)
                FileSystem = (AddinFileSystemExtension)extension;
            else
                throw new NotSupportedException();
        }
    }

    public void UnregisterExtension(object extension)
    {
        lock (extensions)
        {
            extensions.Remove(extension);
            if (extension as AddinFileSystemExtension == FileSystem)
                FileSystem = new AddinFileSystemExtension();
            else
                throw new InvalidOperationException();
        }
    }

    public ExtensionNodeSet FindNodeSet(string domain, string addinId, string id)
    {
        return FindNodeSet(domain, addinId, id, new Hashtable());
    }

    private ExtensionNodeSet FindNodeSet(string domain, string addinId, string id, Hashtable visited)
    {
        if (visited.Contains(addinId))
            return null;
        visited.Add(addinId, addinId);
        var addin = GetInstalledAddin(domain, addinId, true, false);
        if (addin == null)
            return null;
        var desc = addin.Description;
        if (desc == null)
            return null;
        foreach (ExtensionNodeSet nset in desc.ExtensionNodeSets)
            if (nset.Id == id)
                return nset;

        // Not found in the add-in. Look on add-ins on which it depends

        foreach (Dependency dep in desc.MainModule.Dependencies)
        {
            var adep = dep as AddinDependency;
            if (adep == null) continue;

            var aid = Addin.GetFullId(desc.Namespace, adep.AddinId, adep.Version);
            var nset = FindNodeSet(domain, aid, id, visited);
            if (nset != null)
                return nset;
        }

        return null;
    }

    public IEnumerable<Addin> GetInstalledAddins(string domain, AddinSearchFlagsInternal flags)
    {
        if (domain == null)
            domain = Registry.CurrentDomain;

        // Get the cached list if the add-in list has already been loaded.
        // The domain doesn't have to be checked again, since it is always the same

        return InternalGetInstalledAddins(domain, null, flags & ~AddinSearchFlagsInternal.LatestVersionsOnly, false);
    }

    private IEnumerable<Addin> InternalGetInstalledAddins(string domain, AddinSearchFlagsInternal type,
        bool dbIsLockedForRead)
    {
        return InternalGetInstalledAddins(domain, null, type, dbIsLockedForRead);
    }

    private IEnumerable<Addin> InternalGetInstalledAddins(string domain, string idFilter, AddinSearchFlagsInternal type,
        bool dbIsLockedForRead)
    {
        if (!allSetupInfosLoaded)
            lock (localLock)
            {
                if (!allSetupInfosLoaded)
                {
                    var adict = new Dictionary<string, Addin>();

                    using (!dbIsLockedForRead ? fileDatabase.LockRead() : null)
                    {
                        // Global add-ins are valid for any private domain
                        if (domain != GlobalDomain)
                            FindInstalledAddins(adict, GlobalDomain);

                        FindInstalledAddins(adict, domain);
                    }

                    var alist = new List<Addin>(adict.Values);
                    UpdateLastVersionFlags(alist);
                    allSetupInfos = alist.ToImmutableArray();
                    addinSetupInfos = alist.Where(addin => !addin.Description.IsRoot).ToImmutableArray();
                    rootSetupInfos = alist.Where(addin => addin.Description.IsRoot).ToImmutableArray();
                    allSetupInfosLoaded = true;
                }
            }

        IEnumerable<Addin> result;

        if ((type & AddinSearchFlagsInternal.IncludeAll) == AddinSearchFlagsInternal.IncludeAll)
            result = allSetupInfos;
        else if ((type & AddinSearchFlagsInternal.IncludeAddins) == AddinSearchFlagsInternal.IncludeAddins)
            result = addinSetupInfos;
        else
            result = rootSetupInfos;

        result = FilterById(result, idFilter);

        if ((type & AddinSearchFlagsInternal.LatestVersionsOnly) == AddinSearchFlagsInternal.LatestVersionsOnly)
            result = result.Where(a => a.IsLatestVersion);

        if ((type & AddinSearchFlagsInternal.ExcludePendingUninstall) ==
            AddinSearchFlagsInternal.ExcludePendingUninstall)
            result = result.Where(a => !IsRegisteredForUninstall(a.Description.Domain, a.Id));
        return result;
    }

    private IEnumerable<Addin> FilterById(IEnumerable<Addin> addins, string id)
    {
        if (id == null)
            return addins;
        return addins.Where(a => Addin.GetIdName(a.Id) == id);
    }

    private void FindInstalledAddins(Dictionary<string, Addin> result, string domain)
    {
        var dir = Path.Combine(AddinCachePath, domain);
        if (Directory.Exists(dir))
            foreach (var file in fileDatabase.GetDirectoryFiles(dir, "*,*.maddin"))
            {
                var id = Path.GetFileNameWithoutExtension(file);
                if (!result.ContainsKey(id))
                {
                    var adesc = GetInstalledDomainAddin(domain, id, true, false, false);
                    if (adesc != null)
                        result.Add(id, adesc);
                }
            }
    }

    private void UpdateLastVersionFlags(List<Addin> addins)
    {
        var versions = new Dictionary<string, string>();
        foreach (var a in addins)
        {
            string last;
            string id, version;
            Addin.GetIdParts(a.Id, out id, out version);
            if (!versions.TryGetValue(id, out last) || Addin.CompareVersions(last, version) > 0)
                versions[id] = version;
        }

        foreach (var a in addins)
        {
            string id, version;
            Addin.GetIdParts(a.Id, out id, out version);
            a.IsLatestVersion = versions[id] == version;
        }
    }

    public Addin GetInstalledAddin(string domain, string id)
    {
        return GetInstalledAddin(domain, id, false, false);
    }

    public Addin GetInstalledAddin(string domain, string id, bool exactVersionMatch)
    {
        return GetInstalledAddin(domain, id, exactVersionMatch, false);
    }

    public Addin GetInstalledAddin(string domain, string id, bool exactVersionMatch, bool enabledOnly)
    {
        // Try the given domain, and if not found, try the shared domain
        var ad = GetInstalledDomainAddin(domain, id, exactVersionMatch, enabledOnly, true);
        if (ad != null)
            return ad;
        if (domain != GlobalDomain)
            return GetInstalledDomainAddin(GlobalDomain, id, exactVersionMatch, enabledOnly, true);
        return null;
    }

    private Addin GetInstalledDomainAddin(string domain, string id, bool exactVersionMatch, bool enabledOnly,
        bool dbLockCheck)
    {
        var idd = id + " " + domain;
        Addin sinfo;
        bool found;
        lock (cachedAddinSetupInfos)
        {
            found = cachedAddinSetupInfos.TryGetValue(idd, out sinfo);
        }

        if (found)
        {
            if (sinfo != null)
            {
                if (!enabledOnly || sinfo.Enabled)
                    return sinfo;
                if (exactVersionMatch)
                    return null;
            }
            else if (enabledOnly)
            {
                // Ignore the 'not installed' flag when disabled add-ins are allowed
                return null;
            }
        }

        if (dbLockCheck)
            InternalCheck(domain);

        string version, name;
        Addin.GetIdParts(id, out name, out version);

        using (dbLockCheck ? fileDatabase.LockRead() : null)
        {
            if (sinfo == null && !string.IsNullOrEmpty(version))
            {
                // If the same add-in with same version exists in both the global domain and the private domain,
                // take the instance in the global domain. This is an edge case, since in general add-ins will
                // have different versions. Taking the one from global domain in case of colision makes
                // it easier to "replace" an add-in bundled in an app for unit testing purposes.
                // So, look for an exact match in the global domain first:

                string foundDomain = null;

                var path = GetDescriptionPath(GlobalDomain, id);
                if (fileDatabase.Exists(path))
                {
                    foundDomain = GlobalDomain;
                }
                else
                {
                    path = GetDescriptionPath(domain, id);
                    if (fileDatabase.Exists(path))
                        foundDomain = domain;
                }

                if (foundDomain != null)
                {
                    sinfo = new Addin(this, foundDomain, id);
                    lock (cachedAddinSetupInfos)
                    {
                        cachedAddinSetupInfos[idd] = sinfo;
                        if (!enabledOnly || sinfo.Enabled)
                            return sinfo;
                        if (exactVersionMatch)
                        {
                            // Cache lookups with negative result
                            cachedAddinSetupInfos[idd] = null;
                            return null;
                        }
                    }
                }
            }

            // Exact version not found. Look for a compatible version
            if (!exactVersionMatch)
            {
                sinfo = null;
                string bestVersion = null;
                Addin.GetIdParts(id, out name, out version);

                foreach (var ia in InternalGetInstalledAddins(domain, name, AddinSearchFlagsInternal.IncludeAll, true))
                    if ((!enabledOnly || ia.Enabled) &&
                        (version.Length == 0 || ia.SupportsVersion(version)) &&
                        (bestVersion == null || Addin.CompareVersions(bestVersion, ia.Version) > 0))
                    {
                        bestVersion = ia.Version;
                        sinfo = ia;
                    }

                if (sinfo != null)
                {
                    lock (cachedAddinSetupInfos)
                    {
                        cachedAddinSetupInfos[idd] = sinfo;
                    }

                    return sinfo;
                }
            }

            // Cache lookups with negative result
            // Ignore the 'not installed' flag when disabled add-ins are allowed
            if (enabledOnly)
                lock (cachedAddinSetupInfos)
                {
                    cachedAddinSetupInfos[idd] = null;
                }

            return null;
        }
    }

    public void Shutdown()
    {
        ResetCachedData();
    }

    public Addin GetAddinForHostAssembly(string domain, string assemblyLocation)
    {
        InternalCheck(domain);
        Addin ainfo = null;

        lock (cachedAddinSetupInfos)
        {
            if (cachedAddinSetupInfos.TryGetValue(assemblyLocation, out var ob))
                return ob; // this can be null, if the add-in is disabled
        }

        var index = GetAddinHostIndex();
        string addin, addinFile, rdomain;
        if (index.GetAddinForAssembly(assemblyLocation, out addin, out addinFile, out rdomain))
        {
            var sid = addin + " " + rdomain;
            lock (cachedAddinSetupInfos)
            {
                if (!cachedAddinSetupInfos.TryGetValue(sid, out ainfo))
                    ainfo = new Addin(this, rdomain, addin);
                cachedAddinSetupInfos[assemblyLocation] = ainfo;
                cachedAddinSetupInfos[sid] = ainfo;
            }
        }

        return ainfo;
    }


    public bool IsAddinEnabled(string domain, string id)
    {
        var ainfo = GetInstalledAddin(domain, id);
        if (ainfo != null)
            return ainfo.Enabled;
        return false;
    }

    internal bool IsAddinEnabled(string domain, string id, bool exactVersionMatch)
    {
        if (!exactVersionMatch)
            return IsAddinEnabled(domain, id);
        var ainfo = GetInstalledAddin(domain, id, exactVersionMatch, false);
        if (ainfo == null)
            return false;
        return Configuration.IsEnabled(id, ainfo.AddinInfo.EnabledByDefault);
    }

    public void EnableAddin(string domain, string id)
    {
        EnableAddin(domain, id, true);
    }

    public void EnableAddin(string domain, string id, bool exactVersionMatch)
    {
        using var transaction = BeginTransaction();
        EnableAddin(transaction, domain, id, exactVersionMatch);
    }

    private void EnableAddin(AddinDatabaseTransaction dbTransaction, string domain, string id, bool exactVersionMatch)
    {
        var ainfo = GetInstalledAddin(domain, id, exactVersionMatch, false);
        if (ainfo == null)
            // It may be an add-in root
            return;

        if (IsAddinEnabled(domain, id))
            return;

        // Enable required add-ins

        foreach (var dep in ainfo.AddinInfo.Dependencies)
            if (dep is AddinDependency)
            {
                var adep = dep as AddinDependency;
                var adepid = Addin.GetFullId(ainfo.AddinInfo.Namespace, adep.AddinId, adep.Version);
                EnableAddin(dbTransaction, domain, adepid, false);
            }

        Configuration.SetEnabled(dbTransaction, id, true, ainfo.AddinInfo.EnabledByDefault, true);
        SaveConfiguration(dbTransaction);

        if (AddinEngine != null && AddinEngine.IsInitialized)
            AddinEngine.ActivateAddin(dbTransaction.GetAddinEngineTransaction(), id);
    }

    public void DisableAddin(string domain, string id, bool exactVersionMatch = false,
        bool onlyForCurrentSession = false)
    {
        using var transaction = BeginTransaction();
        DisableAddin(transaction, domain, id, exactVersionMatch, onlyForCurrentSession);
    }

    private void DisableAddin(AddinDatabaseTransaction dbTransaction, string domain, string id,
        bool exactVersionMatch = false, bool onlyForCurrentSession = false)
    {
        var ai = GetInstalledAddin(domain, id, true);
        if (ai == null)
            throw new InvalidOperationException("Add-in '" + id + "' not installed.");

        if (!IsAddinEnabled(domain, id, exactVersionMatch))
            return;

        Configuration.SetEnabled(dbTransaction, id, false, ai.AddinInfo.EnabledByDefault, exactVersionMatch,
            onlyForCurrentSession);
        SaveConfiguration(dbTransaction);

        // Disable all add-ins which depend on it

        try
        {
            var idName = Addin.GetIdName(id);

            foreach (var ainfo in GetInstalledAddins(domain, AddinSearchFlagsInternal.IncludeAddins))
            foreach (var dep in ainfo.AddinInfo.Dependencies)
            {
                var adep = dep as AddinDependency;
                if (adep == null)
                    continue;

                var adepid = Addin.GetFullId(ainfo.AddinInfo.Namespace, adep.AddinId, null);
                if (adepid != idName)
                    continue;

                // The add-in that has been disabled, might be a requirement of this one, or maybe not
                // if there is an older version available. Check it now.
                adepid = Addin.GetFullId(ainfo.AddinInfo.Namespace, adep.AddinId, adep.Version);
                var adepinfo = GetInstalledAddin(domain, adepid, false, true);

                if (adepinfo == null)
                {
                    DisableAddin(dbTransaction, domain, ainfo.Id, onlyForCurrentSession: onlyForCurrentSession);
                    break;
                }
            }
        }
        catch
        {
            // If something goes wrong, enable the add-in again
            Configuration.SetEnabled(dbTransaction, id, true, ai.AddinInfo.EnabledByDefault, false,
                onlyForCurrentSession);
            SaveConfiguration(dbTransaction);
            throw;
        }

        if (AddinEngine != null && AddinEngine.IsInitialized)
            AddinEngine.UnloadAddin(dbTransaction.GetAddinEngineTransaction(), id);
    }

    private void UpdateEnabledStatus(AddinDatabaseTransaction transaction)
    {
        // Ensure that all enabled addins that have dependencies also have their dependencies enabled.
        var updatedAddins = new HashSet<Addin>();
        var allAddins = GetInstalledAddins(Registry.CurrentDomain,
            AddinSearchFlagsInternal.IncludeAddins | AddinSearchFlagsInternal.LatestVersionsOnly).ToList();
        foreach (var addin in allAddins)
            UpdateEnabledStatus(transaction, Registry.CurrentDomain, addin, allAddins, updatedAddins);
    }

    private void UpdateEnabledStatus(AddinDatabaseTransaction transaction, string domain, Addin addin,
        List<Addin> allAddins, HashSet<Addin> updatedAddins)
    {
        if (!updatedAddins.Add(addin))
            return;

        if (!addin.Enabled)
            return;

        // Make sure all dependencies of this add-in have an up to date enabled status

        foreach (var dep in addin.AddinInfo.Dependencies)
        {
            var adep = dep as AddinDependency;
            if (adep == null)
                continue;

            var adepid = Addin.GetFullId(addin.AddinInfo.Namespace, adep.AddinId, null);
            var dependency = allAddins.FirstOrDefault(a => Addin.GetFullId(a.Namespace, a.LocalId, null) == adepid);
            if (dependency != null)
            {
                UpdateEnabledStatus(transaction, domain, dependency, allAddins, updatedAddins);
                if (!dependency.Enabled)
                {
                    // One of the dependencies is disabled, so this add-in also needs to be disabled.
                    // However, we disabled only for the current configuration, we don't want to change
                    // what the user configured.
                    DisableAddin(transaction, domain, addin.Id, onlyForCurrentSession: true);
                    return;
                }
            }
        }
    }

    public void RegisterForUninstall(string domain, string id, IEnumerable<string> files)
    {
        using var transaction = BeginTransaction();
        DisableAddin(transaction, domain, id, true);
        Configuration.RegisterForUninstall(transaction, id, files);
        SaveConfiguration(transaction);
    }

    public bool IsRegisteredForUninstall(string domain, string addinId)
    {
        return Configuration.IsRegisteredForUninstall(addinId);
    }

    internal bool HasPendingUninstalls(string domain)
    {
        return Configuration.HasPendingUninstalls;
    }

    internal string GetDescriptionPath(string domain, string id)
    {
        return Path.Combine(Path.Combine(AddinCachePath, domain), id + ".maddin");
    }

    private void InternalCheck(string domain)
    {
        // If the database is broken, don't try to regenerate it at every check.
        if (fatalDatabseError)
            return;

        var update = false;
        using (fileDatabase.LockRead())
        {
            if (!Directory.Exists(AddinCachePath)) update = true;
        }

        if (update)
            Update(null, domain);
    }

    private void GenerateAddinExtensionMapsInternal(IProgressStatus monitor, string domain, List<string> addinsToUpdate,
        List<string> addinsToUpdateRelations, List<string> removedAddins)
    {
        var updateData = new AddinUpdateData(this, monitor);

        // Clear cached data
        lock (cachedAddinSetupInfos)
        {
            cachedAddinSetupInfos.Clear();
        }

        // Collect all information

        var addinHash = new AddinIndex();

        if (monitor.LogLevel > 1)
            monitor.Log("Generating add-in extension maps");

        Hashtable changedAddins = null;
        var descriptionsToSave = new List<AddinDescription>();
        var files = new List<string>();

        var partialGeneration = addinsToUpdate != null;
        var domains = GetDomains().Where(d => d == domain || d == GlobalDomain).ToArray();

        // Get the files to be updated

        if (partialGeneration)
        {
            changedAddins = new Hashtable();

            if (monitor.LogLevel > 2)
                monitor.Log("Doing a partial registry update.\nAdd-ins to be updated:");
            // Get the files and ids of all add-ins that have to be updated
            // Include removed add-ins: if there are several instances of the same add-in, removing one of
            // them will make other instances to show up. If there is a single instance, its files are
            // already removed.
            foreach (var sa in addinsToUpdate.Union(removedAddins))
            {
                changedAddins[sa] = sa;
                if (monitor.LogLevel > 2)
                    monitor.Log(" - " + sa);
                foreach (string file in GetAddinFiles(sa, domains))
                    if (!files.Contains(file))
                    {
                        files.Add(file);
                        var an = Path.GetFileNameWithoutExtension(file);
                        changedAddins[an] = an;
                        if (monitor.LogLevel > 2 && an != sa)
                            monitor.Log(" - " + an);
                    }
            }

            if (monitor.LogLevel > 2)
                monitor.Log("Add-ins whose relations have to be updated:");

            // Get the files and ids of all add-ins whose relations have to be updated
            foreach (var sa in addinsToUpdateRelations)
            foreach (string file in GetAddinFiles(sa, domains))
                if (!files.Contains(file))
                {
                    if (monitor.LogLevel > 2)
                    {
                        var an = Path.GetFileNameWithoutExtension(file);
                        monitor.Log(" - " + an);
                    }

                    files.Add(file);
                }
        }
        else
        {
            foreach (var dom in domains)
                files.AddRange(fileDatabase.GetDirectoryFiles(Path.Combine(AddinCachePath, dom), "*.maddin"));
        }

        // Load the descriptions.
        foreach (var file in files)
        {
            AddinDescription conf;
            if (!ReadAddinDescription(monitor, file, out conf))
            {
                SafeDelete(monitor, file);
                continue;
            }

            // If the original file does not exist, the description can be deleted
            if (!FileSystem.FileExists(conf.AddinFile))
            {
                SafeDelete(monitor, file);
                continue;
            }

            // Remove old data from the description. Remove the data of the add-ins that
            // have changed. This data will be re-added later.

            conf.UnmergeExternalData(changedAddins);
            descriptionsToSave.Add(conf);

            addinHash.Add(conf);
        }

        // Sort the add-ins, to make sure add-ins are processed before
        // all their dependencies

        var sorted = addinHash.GetSortedAddins();

        // Register extension points and node sets
        foreach (var conf in sorted)
            CollectExtensionPointData(conf, updateData);

        if (monitor.LogLevel > 2)
            monitor.Log("Registering new extensions:");

        // Register extensions
        foreach (var conf in sorted)
            if (changedAddins == null || changedAddins.ContainsKey(conf.AddinId))
            {
                if (monitor.LogLevel > 2)
                    monitor.Log("- " + conf.AddinId + " (" + conf.Domain + ")");
                CollectExtensionData(monitor, addinHash, conf, updateData);
            }

        // Save the maps
        foreach (var conf in descriptionsToSave)
        {
            ConsolidateExtensions(conf);
            conf.SaveBinary(fileDatabase);
        }

        if (monitor.LogLevel > 1)
        {
            monitor.Log("Addin relation map generated.");
            monitor.Log("  Addins Updated: " + descriptionsToSave.Count);
            monitor.Log("  Extension points: " + updateData.RelExtensionPoints);
            monitor.Log("  Extensions: " + updateData.RelExtensions);
            monitor.Log("  Extension nodes: " + updateData.RelExtensionNodes);
            monitor.Log("  Node sets: " + updateData.RelNodeSetTypes);
        }
    }

    private void ConsolidateExtensions(AddinDescription conf)
    {
        // Merges extensions with the same path

        foreach (ModuleDescription module in conf.AllModules)
        {
            var extensions = new Dictionary<string, Extension>();
            foreach (Extension ext in module.Extensions)
            {
                Extension mainExt;
                if (extensions.TryGetValue(ext.Path, out mainExt))
                {
                    var list = new List<ExtensionNodeDescription>();
                    EnsureInsertionsSorted(ext.ExtensionNodes);
                    list.AddRange(ext.ExtensionNodes);
                    var pos = -1;
                    foreach (var node in list)
                    {
                        ext.ExtensionNodes.Remove(node);
                        AddNodeSorted(mainExt.ExtensionNodes, node, ref pos);
                    }
                }
                else
                {
                    extensions[ext.Path] = ext;
                    EnsureInsertionsSorted(ext.ExtensionNodes);
                }
            }

            // Sort the nodes
        }
    }

    private void EnsureInsertionsSorted(ExtensionNodeDescriptionCollection list)
    {
        // Makes sure that the nodes in the collections are properly sorted wrt insertafter and insertbefore attributes
        var added = new Dictionary<string, ExtensionNodeDescription>();
        var halfSorted = new List<ExtensionNodeDescription>();
        var orderChanged = false;

        for (var n = list.Count - 1; n >= 0; n--)
        {
            var node = list[n];
            if (node.Id.Length > 0)
                added[node.Id] = node;
            if (node.InsertAfter.Length > 0)
            {
                ExtensionNodeDescription relNode;
                if (added.TryGetValue(node.InsertAfter, out relNode))
                {
                    // Out of order. Move it before the referenced node
                    var i = halfSorted.IndexOf(relNode);
                    halfSorted.Insert(i, node);
                    orderChanged = true;
                }
                else
                {
                    halfSorted.Add(node);
                }
            }
            else
            {
                halfSorted.Add(node);
            }
        }

        halfSorted.Reverse();
        var fullSorted = new List<ExtensionNodeDescription>();
        added.Clear();

        foreach (var node in halfSorted)
        {
            if (node.Id.Length > 0)
                added[node.Id] = node;
            if (node.InsertBefore.Length > 0)
            {
                ExtensionNodeDescription relNode;
                if (added.TryGetValue(node.InsertBefore, out relNode))
                {
                    // Out of order. Move it before the referenced node
                    var i = fullSorted.IndexOf(relNode);
                    fullSorted.Insert(i, node);
                    orderChanged = true;
                }
                else
                {
                    fullSorted.Add(node);
                }
            }
            else
            {
                fullSorted.Add(node);
            }
        }

        if (orderChanged)
        {
            list.Clear();
            foreach (var node in fullSorted)
                list.Add(node);
        }
    }

    private void AddNodeSorted(ExtensionNodeDescriptionCollection list, ExtensionNodeDescription node, ref int curPos)
    {
        // Adds the node at the correct position, taking into account insertbefore and insertafter

        if (node.InsertAfter.Length > 0)
        {
            var afterId = node.InsertAfter;
            for (var n = 0; n < list.Count; n++)
                if (list[n].Id == afterId)
                {
                    list.Insert(n + 1, node);
                    curPos = n + 2;
                    return;
                }
        }
        else if (node.InsertBefore.Length > 0)
        {
            var beforeId = node.InsertBefore;
            for (var n = 0; n < list.Count; n++)
                if (list[n].Id == beforeId)
                {
                    list.Insert(n, node);
                    curPos = n + 1;
                    return;
                }
        }

        if (curPos == -1)
            list.Add(node);
        else
            list.Insert(curPos++, node);
    }


    private IEnumerable GetAddinFiles(string fullId, string[] domains)
    {
        // Look for all versions of the add-in, because this id may be the id of a reference,
        // and the exact reference version may not be installed.
        var s = fullId;
        var i = s.LastIndexOf(',');
        if (i != -1)
            s = s.Substring(0, i);
        s += ",*";

        // Look for the add-in in any of the existing folders
        foreach (var domain in domains)
        {
            var mp = GetDescriptionPath(domain, s);
            var dir = Path.GetDirectoryName(mp);
            var pat = Path.GetFileName(mp);
            foreach (var fmp in fileDatabase.GetDirectoryFiles(dir, pat))
                yield return fmp;
        }
    }

    // Collects extension data in a hash table. The key is the path, the value is a list
    // of add-ins ids that extend that path

    private void CollectExtensionPointData(AddinDescription conf, AddinUpdateData updateData)
    {
        foreach (ExtensionNodeSet nset in conf.ExtensionNodeSets)
            try
            {
                updateData.RegisterNodeSet(conf, nset);
                updateData.RelNodeSetTypes++;
            }
            catch (Exception ex)
            {
                throw new InvalidOperationException("Error reading node set: " + nset.Id, ex);
            }

        foreach (ExtensionPoint ep in conf.ExtensionPoints)
            try
            {
                updateData.RegisterExtensionPoint(conf, ep);
                updateData.RelExtensionPoints++;
            }
            catch (Exception ex)
            {
                throw new InvalidOperationException("Error reading extension point: " + ep.Path, ex);
            }
    }

    private void CollectExtensionData(IProgressStatus monitor, AddinIndex addinHash, AddinDescription conf,
        AddinUpdateData updateData)
    {
        var missingDeps = addinHash.GetMissingDependencies(conf, conf.MainModule);
        if (missingDeps.Any())
        {
            var w = "The add-in '" + conf.AddinId +
                    "' could not be updated because some of its dependencies are missing or not compatible:";
            w += BuildMissingAddinsList(addinHash, conf, missingDeps);
            monitor.ReportWarning(w);
            return;
        }

        CollectModuleExtensionData(conf, conf.MainModule, updateData, addinHash);

        foreach (ModuleDescription module in conf.OptionalModules)
        {
            missingDeps = addinHash.GetMissingDependencies(conf, module);
            if (missingDeps.Any())
            {
                if (monitor.LogLevel > 1)
                {
                    var w = "An optional module of the add-in '" + conf.AddinId +
                            "' could not be updated because some of its dependencies are missing or not compatible:";
                    w += BuildMissingAddinsList(addinHash, conf, missingDeps);
                }
            }
            else
            {
                CollectModuleExtensionData(conf, module, updateData, addinHash);
            }
        }
    }

    private string BuildMissingAddinsList(AddinIndex addinHash, AddinDescription conf, IEnumerable<string> missingDeps)
    {
        var w = "";
        foreach (var dep in missingDeps)
        {
            var found = addinHash.GetSimilarExistingAddin(conf, dep);
            if (found == null)
                w += "\n  missing: " + dep;
            else
                w += "\n  required: " + dep + ", found: " + found.AddinId;
        }

        return w;
    }

    private void CollectModuleExtensionData(AddinDescription conf, ModuleDescription module, AddinUpdateData updateData,
        AddinIndex index)
    {
        foreach (Extension ext in module.Extensions)
        {
            updateData.RelExtensions++;
            updateData.RegisterExtension(conf, module, ext);
            AddChildExtensions(conf, module, updateData, index, ext.Path, ext.ExtensionNodes, false);
        }
    }

    private void AddChildExtensions(AddinDescription conf, ModuleDescription module, AddinUpdateData updateData,
        AddinIndex index, string path, ExtensionNodeDescriptionCollection nodes, bool conditionChildren)
    {
        // Don't register conditions as extension nodes.
        if (!conditionChildren)
            updateData.RegisterExtension(conf, module, path);

        foreach (ExtensionNodeDescription node in nodes)
        {
            if (node.NodeName == "ComplexCondition")
                continue;
            updateData.RelExtensionNodes++;
            var id = node.GetAttribute("id");
            if (id.Length != 0)
            {
                var isCondition = node.NodeName == "Condition";
                if (isCondition)
                {
                    // Find the add-in that provides the implementation for this condition.
                    // Store that id in the condition. The add-in engine will ensure the add-in
                    // is loaded when it tries to evaluate this condition.
                    var condAsm = index.FindCondition(conf, module, id);
                    if (condAsm != null)
                        node.SetAttribute(Condition.SourceAddinAttribute, condAsm);
                }

                AddChildExtensions(conf, module, updateData, index, path + "/" + id, node.ChildNodes, isCondition);
            }
        }
    }

    private string[] GetDomains()
    {
        var dirs = fileDatabase.GetDirectories(AddinCachePath);
        var ids = new string [dirs.Length];
        for (var n = 0; n < dirs.Length; n++)
            ids[n] = Path.GetFileName(dirs[n]);
        return ids;
    }

    public string GetUniqueDomainId()
    {
        if (lastDomainId != 0)
        {
            lastDomainId++;
            return lastDomainId.ToString();
        }

        lastDomainId = 1;
        foreach (var s in fileDatabase.GetDirectories(AddinCachePath))
        {
            var dn = Path.GetFileName(s);
            if (dn == GlobalDomain)
                continue;
            try
            {
                var n = int.Parse(dn);
                if (n >= lastDomainId)
                    lastDomainId = n + 1;
            }
            catch
            {
            }
        }

        return lastDomainId.ToString();
    }

    internal void ResetBasicCachedData()
    {
        lock (localLock)
        {
            allSetupInfosLoaded = false;
        }
    }

    internal void ResetCachedData(AddinDatabaseTransaction dbTransaction = null)
    {
        ResetBasicCachedData();
        hostIndex = null;
        lock (cachedAddinSetupInfos)
        {
            cachedAddinSetupInfos.Clear();
        }

        dependsOnCache.Clear();
        if (AddinEngine != null)
            AddinEngine.ResetCachedData(dbTransaction?.GetAddinEngineTransaction());
    }

    public bool AddinDependsOn(string domain, string id1, string id2)
    {
        var depTree = GetOrCreateAddInDependencyTree(domain, id1);
        return depTree.Contains(id2);
    }

    private HashSet<string> GetOrCreateAddInDependencyTree(string domain, string addin)
    {
        HashSet<string> cache;
        if (dependsOnCache.TryGetValue(addin, out cache)) return cache;

        dependsOnCache[addin] = cache = new HashSet<string>();

        var addin1 = GetInstalledAddin(domain, addin, false);

        // We can assume that if the add-in is not returned here, it may be a root addin.
        if (addin1 == null)
            return cache;

        foreach (var dep in addin1.AddinInfo.Dependencies)
        {
            var adep = dep as AddinDependency;
            if (adep == null)
                continue;

            var depid = Addin.GetFullId(addin1.AddinInfo.Namespace, adep.AddinId, null);
            cache.Add(depid);

            var recursiveDependencies = GetOrCreateAddInDependencyTree(domain, depid);
            cache.UnionWith(recursiveDependencies);
        }

        return cache;
    }

    public void GenerateScanDataFiles(IProgressStatus monitor, string folder, bool recursive)
    {
        var setup = GetSetupHandler();
        setup.GenerateScanDataFiles(monitor, Registry, Path.GetFullPath(folder), recursive);
    }

    public void Repair(IProgressStatus monitor, string domain, ScanOptions context = null)
    {
        using (fileDatabase.LockWrite())
        {
            try
            {
                if (Directory.Exists(AddinCachePath))
                    Directory.Delete(AddinCachePath, true);
                if (Directory.Exists(AddinFolderCachePath))
                    Directory.Delete(AddinFolderCachePath, true);
                if (File.Exists(HostIndexFile))
                    File.Delete(HostIndexFile);
            }
            catch (Exception ex)
            {
                monitor.ReportError(
                    "The add-in registry could not be rebuilt. It may be due to lack of write permissions to the directory: " +
                    AddinDbDir, ex);
            }
        }

        ResetBasicCachedData();

        Update(monitor, domain, context);
    }

    public void Update(IProgressStatus monitor, string domain, ScanOptions context = null,
        AddinEngineTransaction addinEngineTransaction = null)
    {
        if (monitor == null)
            monitor = new ConsoleProgressStatus(false);

        if (RunningSetupProcess)
            return;

        fatalDatabseError = false;

        var tim = DateTime.Now;

        using var dbTransaction = BeginTransaction(addinEngineTransaction);

        RunPendingUninstalls(dbTransaction, monitor);

        var installed = new Hashtable();
        var changesFound = CheckFolders(monitor, domain);

        if (monitor.IsCanceled)
            return;

        if (monitor.LogLevel > 1)
            monitor.Log("Folders checked (" + (int)(DateTime.Now - tim).TotalMilliseconds + " ms)");

        if (changesFound)
        {
            // Something has changed, the add-ins need to be re-scanned, but it has
            // to be done in an external process

            if (domain != null)
                foreach (var ainfo in InternalGetInstalledAddins(domain, AddinSearchFlagsInternal.IncludeAddins, false))
                    installed[ainfo.Id] = ainfo.Id;

            RunScannerProcess(monitor, context);

            ResetCachedData(dbTransaction);

            Registry.NotifyDatabaseUpdated();
        }

        if (fatalDatabseError)
            monitor.ReportError(
                "The add-in database could not be updated. It may be due to file corruption. Try running the setup repair utility",
                null);

        // Update the currently loaded add-ins
        if (changesFound && domain != null && AddinEngine != null && AddinEngine.IsInitialized)
        {
            var newInstalled = new Hashtable();
            foreach (var ainfo in GetInstalledAddins(domain, AddinSearchFlagsInternal.IncludeAddins))
                newInstalled[ainfo.Id] = ainfo.Id;

            foreach (string aid in installed.Keys)
                // Always try to unload, event if the add-in was not currently loaded.
                // Required since the add-ins has to be marked as 'disabled', to avoid
                // extensions from this add-in to be loaded
                if (!newInstalled.Contains(aid))
                    AddinEngine.UnloadAddin(dbTransaction.GetAddinEngineTransaction(), aid);

            foreach (string aid in newInstalled.Keys)
                if (!installed.Contains(aid))
                {
                    var addin = AddinEngine.Registry.GetAddin(aid);
                    if (addin != null)
                        AddinEngine.ActivateAddin(dbTransaction.GetAddinEngineTransaction(), aid);
                }
        }

        UpdateEnabledStatus(dbTransaction);
    }

    private void RunPendingUninstalls(AddinDatabaseTransaction dbTransaction, IProgressStatus monitor)
    {
        var changesDone = false;

        foreach (var adn in Configuration.GetPendingUninstalls())
        {
            var files = new HashSet<string>(adn.Files);
            if (AddinManager.CheckAssembliesLoaded(files))
                continue;

            if (monitor.LogLevel > 1)
                monitor.Log("Uninstalling " + adn.AddinId);

            // Make sure all files can be deleted before doing so
            var canUninstall = true;
            foreach (var f in adn.Files)
            {
                if (!File.Exists(f))
                    continue;
                try
                {
                    File.OpenWrite(f).Close();
                }
                catch
                {
                    canUninstall = false;
                    break;
                }
            }

            if (!canUninstall)
                continue;

            foreach (var f in adn.Files)
                try
                {
                    if (File.Exists(f))
                        File.Delete(f);
                }
                catch
                {
                    canUninstall = false;
                }

            if (canUninstall)
            {
                Configuration.UnregisterForUninstall(dbTransaction, adn.AddinId);
                changesDone = true;
            }
        }

        if (changesDone)
            SaveConfiguration(dbTransaction);
    }

    private void RunScannerProcess(IProgressStatus monitor, ScanOptions context)
    {
        var setup = GetSetupHandler();


        var scanMonitor = monitor;
        context = context ?? new ScanOptions();

        if (FileSystem.GetType() != typeof(AddinFileSystemExtension))
            context.FileSystemExtension = FileSystem;

        var retry = false;
        do
        {
            try
            {
                if (monitor.LogLevel > 1)
                    monitor.Log("Looking for addins");
                setup.Scan(scanMonitor, Registry, null, context);
                retry = false;
            }
            catch (Exception ex)
            {
                var pex = ex as ProcessFailedException;
                if (pex != null)
                    // Get the last logged operation.
                    if (pex.LastLog.StartsWith("scan:", StringComparison.Ordinal))
                    {
                        // It crashed while scanning a file. Add the file to the ignore list and try again.
                        var file = pex.LastLog.Substring(5);
                        context.FilesToIgnore.Add(file);
                        monitor.ReportWarning("Could not scan file: " + file);
                        retry = true;
                        continue;
                    }

                fatalDatabseError = true;
                // If the process has crashed, try to do a new scan, this time using verbose log,
                // to give the user more information about the origin of the crash.
                if (pex != null && !retry)
                {
                    monitor.ReportError(
                        "Add-in scan operation failed. The runtime may have encountered an error while trying to load an assembly.",
                        null);
                    if (monitor.LogLevel <= 1)
                    {
                        // Re-scan again using verbose log, to make it easy to find the origin of the error.
                        retry = true;
                        scanMonitor = new ConsoleProgressStatus(true);
                    }
                }
                else
                {
                    retry = false;
                }

                if (!retry)
                {
                    var pfex = ex as ProcessFailedException;
                    monitor.ReportError("Add-in scan operation failed", pfex != null ? pfex.InnerException : ex);
                    monitor.Cancel();
                    return;
                }
            }
        } while (retry);
    }

    private bool DatabaseInfrastructureCheck(IProgressStatus monitor)
    {
        // Do some sanity check, to make sure the basic database infrastructure can be created

        var hasChanges = false;

        try
        {
            if (!Directory.Exists(AddinCachePath))
            {
                Directory.CreateDirectory(AddinCachePath);
                hasChanges = true;
            }

            if (!Directory.Exists(AddinFolderCachePath))
            {
                Directory.CreateDirectory(AddinFolderCachePath);
                hasChanges = true;
            }

            // Make sure we can write in those folders

            Util.CheckWrittableFloder(AddinCachePath);
            Util.CheckWrittableFloder(AddinFolderCachePath);

            fatalDatabseError = false;
        }
        catch (Exception ex)
        {
            monitor.ReportError("Add-in cache directory could not be created", ex);
            fatalDatabseError = true;
            monitor.Cancel();
        }

        return hasChanges;
    }


    internal bool CheckFolders(IProgressStatus monitor, string domain)
    {
        using (fileDatabase.LockRead())
        {
            var scanResult = new AddinScanResult();
            scanResult.CheckOnly = true;
            scanResult.Domain = domain;
            InternalScanFolders(monitor, scanResult);
            return scanResult.ChangesFound;
        }
    }

    internal void ScanFolders(IProgressStatus monitor, string currentDomain, string folderToScan, ScanOptions context)
    {
        var res = new AddinScanResult();
        res.Domain = currentDomain;
        res.ScanContext.AddPathsToIgnore(context.FilesToIgnore);
        res.CleanGeneratedAddinScanDataFiles = context.CleanGeneratedAddinScanDataFiles;
        ScanFolders(monitor, res);
    }

    internal void GenerateScanDataFilesInProcess(IProgressStatus monitor, string folderToScan, bool recursive)
    {
        using (var visitor = new AddinScanDataFileGenerator(this, Registry, folderToScan))
        {
            visitor.VisitFolder(monitor, folderToScan, null, recursive);
        }
    }

    private void ScanFolders(IProgressStatus monitor, AddinScanResult scanResult)
    {
        // All changes are done in a transaction, which won't be committed until
        // all files have been updated.

        if (!fileDatabase.BeginTransaction())
            // The database is already being updated. Can't do anything for now.
            return;

        try
        {
            // Perform the add-in scan

            InternalScanFolders(monitor, scanResult);

            fileDatabase.CommitTransaction();
        }
        catch
        {
            fileDatabase.RollbackTransaction();
            throw;
        }
    }

    private void InternalScanFolders(IProgressStatus monitor, AddinScanResult scanResult)
    {
        try
        {
            FileSystem.ScanStarted();
            InternalScanFolders2(monitor, scanResult);
        }
        finally
        {
            FileSystem.ScanFinished();
        }
    }

    private void InternalScanFolders2(IProgressStatus monitor, AddinScanResult scanResult)
    {
        var tim = DateTime.Now;

        DatabaseInfrastructureCheck(monitor);
        if (monitor.IsCanceled)
            return;

        try
        {
            scanResult.HostIndex = new AddinHostIndex(GetAddinHostIndex());
        }
        catch (Exception ex)
        {
            if (scanResult.CheckOnly)
            {
                scanResult.ChangesFound = true;
                return;
            }

            monitor.ReportError("Add-in root index is corrupt. The add-in database will be regenerated.", ex);
            scanResult.RegenerateAllData = true;
        }

        var updater = new AddinRegistryUpdater(this, scanResult);

        // Check if any of the previously scanned folders has been deleted

        foreach (var file in Directory.EnumerateFiles(AddinFolderCachePath, "*.data"))
        {
            AddinScanFolderInfo folderInfo;
            var res = ReadFolderInfo(monitor, file, out folderInfo);
            var validForDomain = scanResult.Domain == null || folderInfo.Domain == GlobalDomain ||
                                 folderInfo.Domain == scanResult.Domain;
            if (!res || (validForDomain && !FileSystem.DirectoryExists(folderInfo.Folder)))
            {
                if (res)
                {
                    // Folder has been deleted. Remove the add-ins it had.
                    updater.UpdateDeletedAddins(monitor, folderInfo);
                }
                else
                {
                    // Folder info file corrupt. Regenerate all.
                    scanResult.ChangesFound = true;
                    scanResult.RegenerateRelationData = true;
                }

                if (!scanResult.CheckOnly)
                    SafeDelete(monitor, file);
                else if (scanResult.ChangesFound)
                    return;
            }
        }

        // Look for changes in the add-in folders

        if (Registry.StartupDirectory != null)
            updater.VisitFolder(monitor, Registry.StartupDirectory, null, false);

        if (scanResult.CheckOnly && (scanResult.ChangesFound || monitor.IsCanceled))
            return;

        if (scanResult.Domain == null)
            updater.VisitFolder(monitor, HostsPath, GlobalDomain, false);

        if (scanResult.CheckOnly && (scanResult.ChangesFound || monitor.IsCanceled))
            return;

        foreach (var dir in Registry.GlobalAddinDirectories)
        {
            if (scanResult.CheckOnly && (scanResult.ChangesFound || monitor.IsCanceled))
                return;
            updater.VisitFolder(monitor, dir, GlobalDomain, true);
        }

        if (scanResult.CheckOnly || !scanResult.ChangesFound)
            return;

        // Scan the files which have been modified

        // AssemblyIndex will contain all assemblies that were
        // found while looking for add-ins. Use it to resolve assemblies
        // while scanning those add-ins.

        using (var scanner = new AddinScanner(this, scanResult.AssemblyIndex))
        {
            foreach (var file in scanResult.FilesToScan)
                scanner.ScanFile(monitor, file, scanResult, scanResult.CleanGeneratedAddinScanDataFiles);
        }

        // Save folder info

        foreach (var finfo in scanResult.ModifiedFolderInfos)
            SaveFolderInfo(monitor, finfo);

        if (monitor.LogLevel > 1)
            monitor.Log("Folders scan completed (" + (int)(DateTime.Now - tim).TotalMilliseconds + " ms)");

        SaveAddinHostIndex(scanResult);
        ResetCachedData();

        if (!scanResult.ChangesFound)
        {
            if (monitor.LogLevel > 1)
                monitor.Log("No changes found");
            return;
        }

        tim = DateTime.Now;
        try
        {
            if (scanResult.RegenerateRelationData)
            {
                if (monitor.LogLevel > 1)
                    monitor.Log("Regenerating all add-in relations.");
                scanResult.AddinsToUpdate = null;
                scanResult.AddinsToUpdateRelations = null;
            }

            GenerateAddinExtensionMapsInternal(monitor, scanResult.Domain, scanResult.AddinsToUpdate,
                scanResult.AddinsToUpdateRelations, scanResult.RemovedAddins);
        }
        catch (Exception ex)
        {
            fatalDatabseError = true;
            monitor.ReportError(
                "The add-in database could not be updated. It may be due to file corruption. Try running the setup repair utility",
                ex);
        }

        if (monitor.LogLevel > 1)
            monitor.Log("Add-in relations analyzed (" + (int)(DateTime.Now - tim).TotalMilliseconds + " ms)");

        SaveAddinHostIndex(scanResult);

        hostIndex = scanResult.HostIndex.ToImmutableAddinHostIndex();
    }

    public void ParseAddin(IProgressStatus progressStatus, string domain, string file, string outFile, bool inProcess)
    {
        if (!inProcess)
        {
            var setup = GetSetupHandler();
            setup.GetAddinDescription(progressStatus, Registry, Path.GetFullPath(file), outFile);
            return;
        }

        using (fileDatabase.LockRead())
        {
            // First of all, check if the file belongs to a registered add-in
            AddinScanFolderInfo finfo;
            if (GetFolderInfoForPath(progressStatus, Path.GetDirectoryName(file), out finfo) && finfo != null)
            {
                var afi = finfo.GetAddinFileInfo(file);
                if (afi != null && afi.IsAddin)
                {
                    AddinDescription adesc;
                    GetAddinDescription(progressStatus, afi.Domain, afi.AddinId, file, out adesc);
                    if (adesc != null)
                        adesc.Save(outFile);
                    return;
                }
            }

            var sr = new AddinScanResult();
            sr.Domain = domain;

            var res = new AssemblyLocatorVisitor(this, Registry, true);

            using (var scanner = new AddinScanner(this, res))
            {
                var desc = scanner.ScanSingleFile(progressStatus, file, sr);
                if (desc != null)
                {
                    // Reset the xml doc so that it is not reused when saving. We want a brand new document
                    desc.ResetXmlDoc();
                    desc.Save(outFile);
                }
            }
        }
    }

    public string GetFolderDomain(IProgressStatus progressStatus, string path)
    {
        AddinScanFolderInfo folderInfo;

        if (GetFolderInfoForPath(progressStatus, path, out folderInfo) && folderInfo == null)
        {
            if (path.Length > 0 && path[path.Length - 1] != Path.DirectorySeparatorChar)
                // Try again by appending a directory separator at the end. Some directories are registered like this.
                GetFolderInfoForPath(progressStatus, path + Path.DirectorySeparatorChar, out folderInfo);
            else if (path.Length > 0 && path[path.Length - 1] == Path.DirectorySeparatorChar)
                // Try again by removing the directory separator at the end. Some directories are registered like this.
                GetFolderInfoForPath(progressStatus, path.TrimEnd(Path.DirectorySeparatorChar), out folderInfo);
        }

        if (folderInfo != null && !string.IsNullOrEmpty(folderInfo.Domain))
            return folderInfo.Domain;
        return UnknownDomain;
    }

    public string GetFolderConfigFile(string path)
    {
        path = Path.GetFullPath(path);

        var s = path.Replace("_", "__");
        s = s.Replace(Path.DirectorySeparatorChar, '_');
        s = s.Replace(Path.AltDirectorySeparatorChar, '_');
        s = s.Replace(Path.VolumeSeparatorChar, '_');

        return Path.Combine(AddinFolderCachePath, s + ".data");
    }

    internal void UninstallAddin(IProgressStatus monitor, string domain, string addinId, string addinFile,
        AddinScanResult scanResult)
    {
        AddinDescription desc;

        if (!GetAddinDescription(monitor, domain, addinId, addinFile, out desc))
        {
            // If we can't get information about the old assembly, just regenerate all relation data
            scanResult.RegenerateRelationData = true;
            return;
        }

        scanResult.AddRemovedAddin(addinId);

        // If the add-in didn't exist, there is nothing left to do

        if (desc == null)
            return;

        // If the add-in already existed, the dependencies of the old add-in need to be re-analyzed

        Util.AddDependencies(desc, scanResult);
        if (desc.IsRoot)
            scanResult.HostIndex.RemoveHostData(desc.AddinId, desc.AddinFile);

        RemoveAddinDescriptionFile(monitor, desc.FileName);
    }

    public bool GetAddinDescription(IProgressStatus monitor, string domain, string addinId, string addinFile,
        out AddinDescription description)
    {
        // If the same add-in is installed in different folders (in the same domain) there will be several .maddin files for it,
        // using the suffix "_X" where X is a number > 1 (for example: someAddin,1.0.maddin, someAddin,1.0.maddin_2, someAddin,1.0.maddin_3, ...)
        // We need to return the .maddin whose AddinFile matches the one being requested

        addinFile = Path.GetFullPath(addinFile);
        var altNum = 1;
        var baseFile = GetDescriptionPath(domain, addinId);
        var file = baseFile;
        var failed = false;

        do
        {
            if (!ReadAddinDescription(monitor, file, out description))
            {
                // Remove the AddinDescription here since it is corrupted.
                // Avoids creating alternate versions of corrupted files when later calling SaveDescription.
                RemoveAddinDescriptionFile(monitor, file);
                failed = true;
                continue;
            }

            if (description == null)
                break;
            if (Path.GetFullPath(description.AddinFile) == addinFile)
                return true;
            file = baseFile + "_" + ++altNum;
        } while (fileDatabase.Exists(file));

        // File not found. Return false only if there has been any read error.
        description = null;
        return failed;
    }

    private bool RemoveAddinDescriptionFile(IProgressStatus monitor, string file)
    {
        // Removes an add-in description and shifts up alternate instances of the description file
        // (so xxx,1.0.maddin_2 will become xxx,1.0.maddin, xxx,1.0.maddin_3 -> xxx,1.0.maddin_2, etc)

        if (!SafeDelete(monitor, file))
            return false;

        int dversion;
        if (file.EndsWith(".maddin"))
        {
            dversion = 2;
        }
        else
        {
            var i = file.LastIndexOf('_');
            dversion = 1 + int.Parse(file.Substring(i + 1));
            file = file.Substring(0, i);
        }

        while (fileDatabase.Exists(file + "_" + dversion))
        {
            var newFile = dversion == 2 ? file : file + "_" + (dversion - 1);
            try
            {
                fileDatabase.Rename(file + "_" + dversion, newFile);
            }
            catch (Exception ex)
            {
                if (monitor.LogLevel > 1)
                {
                    monitor.Log("Could not rename file '" + file + "_" + dversion + "' to '" + newFile + "'");
                    monitor.Log(ex.ToString());
                }
            }

            dversion++;
        }

        var dir = Path.GetDirectoryName(file);
        if (fileDatabase.DirectoryIsEmpty(dir))
            SafeDeleteDir(monitor, dir);

        if (dversion == 2)
            // All versions of the add-in removed.
            SafeDeleteDir(monitor, Path.Combine(AddinPrivateDataPath, Path.GetFileNameWithoutExtension(file)));

        return true;
    }

    public bool ReadAddinDescription(IProgressStatus monitor, string file, out AddinDescription description)
    {
        try
        {
            description = AddinDescription.ReadBinary(fileDatabase, file);
            if (description != null)
                description.OwnerDatabase = this;
            return true;
        }
        catch (Exception ex)
        {
            if (monitor == null)
                throw;
            description = null;
            monitor.ReportError("Could not read folder info file", ex);
            return false;
        }
    }

    public bool SaveDescription(IProgressStatus monitor, AddinDescription desc, string replaceFileName)
    {
        try
        {
            if (replaceFileName != null)
            {
                desc.SaveBinary(fileDatabase, replaceFileName);
            }
            else
            {
                var file = GetDescriptionPath(desc.Domain, desc.AddinId);
                var dir = Path.GetDirectoryName(file);
                if (!fileDatabase.DirExists(dir))
                    fileDatabase.CreateDir(dir);
                if (fileDatabase.Exists(file))
                {
                    // Another AddinDescription already exists with the same name.
                    // Create an alternate AddinDescription file
                    var altNum = 2;
                    while (fileDatabase.Exists(file + "_" + altNum))
                        altNum++;
                    file = file + "_" + altNum;
                }

                desc.SaveBinary(fileDatabase, file);
            }

            return true;
        }
        catch (Exception ex)
        {
            monitor.ReportError("Add-in info file could not be saved", ex);
            return false;
        }
    }

    public bool AddinDescriptionExists(string domain, string addinId)
    {
        var file = GetDescriptionPath(domain, addinId);
        return fileDatabase.Exists(file);
    }

    public bool ReadFolderInfo(IProgressStatus monitor, string file, out AddinScanFolderInfo folderInfo)
    {
        try
        {
            folderInfo = AddinScanFolderInfo.Read(fileDatabase, file);
            return true;
        }
        catch (Exception ex)
        {
            folderInfo = null;
            monitor.ReportError("Could not read folder info file", ex);
            return false;
        }
    }

    public bool GetFolderInfoForPath(IProgressStatus monitor, string path, out AddinScanFolderInfo folderInfo)
    {
        try
        {
            folderInfo = AddinScanFolderInfo.Read(fileDatabase, AddinFolderCachePath, path);
            return true;
        }
        catch (Exception ex)
        {
            folderInfo = null;
            if (monitor != null)
                monitor.ReportError("Could not read folder info file", ex);
            return false;
        }
    }

    public bool SaveFolderInfo(IProgressStatus monitor, AddinScanFolderInfo folderInfo)
    {
        try
        {
            folderInfo.Write(fileDatabase, AddinFolderCachePath);
            return true;
        }
        catch (Exception ex)
        {
            monitor.ReportError("Could not write folder info file", ex);
            return false;
        }
    }

    public bool DeleteFolderInfo(IProgressStatus monitor, AddinScanFolderInfo folderInfo)
    {
        return SafeDelete(monitor, folderInfo.FileName);
    }

    public bool SafeDelete(IProgressStatus monitor, string file)
    {
        try
        {
            fileDatabase.Delete(file);
            return true;
        }
        catch (Exception ex)
        {
            if (monitor.LogLevel > 1)
            {
                monitor.Log("Could not delete file: " + file);
                monitor.Log(ex.ToString());
            }

            return false;
        }
    }

    public bool SafeDeleteDir(IProgressStatus monitor, string dir)
    {
        try
        {
            fileDatabase.DeleteDir(dir);
            return true;
        }
        catch (Exception ex)
        {
            if (monitor.LogLevel > 1)
            {
                monitor.Log("Could not delete directory: " + dir);
                monitor.Log(ex.ToString());
            }

            return false;
        }
    }

    private ImmutableAddinHostIndex GetAddinHostIndex()
    {
        if (hostIndex != null)
            return hostIndex;

        using (fileDatabase.LockRead())
        {
            if (fileDatabase.Exists(HostIndexFile))
                hostIndex = AddinHostIndex.ReadAsImmutable(fileDatabase, HostIndexFile);
            else
                hostIndex = new ImmutableAddinHostIndex();
        }

        return hostIndex;
    }

    private void SaveAddinHostIndex(AddinScanResult scanResult)
    {
        if (scanResult.HostIndex != null)
            scanResult.HostIndex.Write(fileDatabase, HostIndexFile);
    }

    internal string GetUniqueAddinId(string file, string oldId, string ns, string version)
    {
        var baseId = "__" + Path.GetFileNameWithoutExtension(file);

        if (Path.GetExtension(baseId) == ".addin")
            baseId = Path.GetFileNameWithoutExtension(baseId);

        var name = baseId;
        var id = Addin.GetFullId(ns, name, version);

        // If the old Id is already an automatically generated one, reuse it
        if (oldId != null && oldId.StartsWith(id))
            return name;

        var n = 1;
        while (AddinIdExists(id))
        {
            name = baseId + "_" + n;
            id = Addin.GetFullId(ns, name, version);
            n++;
        }

        return name;
    }

    private bool AddinIdExists(string id)
    {
        foreach (var d in fileDatabase.GetDirectories(AddinCachePath))
            if (fileDatabase.Exists(Path.Combine(d, id + ".addin")))
                return true;
        return false;
    }

    private ISetupHandler GetSetupHandler()
    {
        // .NET Core doesn't support domains, so it will always use SetupLocal, but it will
        // avoid loading assemblies by forcing the use of the cecil reflector
#if NET461
			if (fs.RequiresIsolation)
				return new SetupDomain ();
			else
#endif
        return new SetupLocal();
    }

    public void ResetConfiguration()
    {
        if (File.Exists(ConfigFile))
            File.Delete(ConfigFile);
        config = null;
        ResetCachedData();
    }

    private void SaveConfiguration(AddinDatabaseTransaction dbTransaction)
    {
        if (config != null)
            using (fileDatabase.LockWrite())
            {
                config.Write(ConfigFile);
            }
    }
}

internal class AddinIndex
{
    private readonly Dictionary<string, List<AddinDescription>> addins = new();

    public void Add(AddinDescription desc)
    {
        var id = Addin.GetFullId(desc.Namespace, desc.LocalId, null);
        List<AddinDescription> list;
        if (!addins.TryGetValue(id, out list))
            addins[id] = list = new List<AddinDescription>();
        list.Add(desc);
    }

    private List<AddinDescription> FindDescriptions(string domain, string fullid)
    {
        // Returns all registered add-ins which are compatible with the provided
        // fullid. Compatible means that the id is the same and the version is within
        // the range of compatible versions of the add-in.

        var res = new List<AddinDescription>();
        var id = Addin.GetIdName(fullid);
        List<AddinDescription> list;
        if (!addins.TryGetValue(id, out list))
            return res;
        var version = Addin.GetIdVersion(fullid);
        foreach (var desc in list)
            if ((desc.Domain == domain || domain == AddinDatabase.GlobalDomain) && desc.SupportsVersion(version))
                res.Add(desc);
        return res;
    }

    public IEnumerable<string> GetMissingDependencies(AddinDescription desc, ModuleDescription mod)
    {
        foreach (Dependency dep in mod.Dependencies)
        {
            var adep = dep as AddinDependency;
            if (adep == null)
                continue;
            var descs = FindDescriptions(desc.Domain, adep.FullAddinId);
            if (descs.Count == 0)
                yield return adep.FullAddinId;
        }
    }

    public AddinDescription GetSimilarExistingAddin(AddinDescription conf, string addinId)
    {
        var domain = conf.Domain;
        List<AddinDescription> list;
        if (!addins.TryGetValue(Addin.GetIdName(addinId), out list))
            return null;
        var version = Addin.GetIdVersion(addinId);
        foreach (var desc in list)
            if ((desc.Domain == domain || domain == AddinDatabase.GlobalDomain) && !desc.SupportsVersion(version))
                return desc;
        return null;
    }

    public string FindCondition(AddinDescription desc, ModuleDescription mod, string conditionId)
    {
        foreach (ConditionTypeDescription ctd in desc.ConditionTypes)
            if (ctd.Id == conditionId)
                return desc.AddinId;

        foreach (Dependency dep in mod.Dependencies)
        {
            var adep = dep as AddinDependency;

            if (adep == null)
                continue;
            var descs = FindDescriptions(desc.Domain, adep.FullAddinId);
            foreach (var d in descs)
            {
                var c = FindCondition(d, d.MainModule, conditionId);
                if (c != null)
                    return c;
            }
        }

        return null;
    }

    public List<AddinDescription> GetSortedAddins()
    {
        var inserted = new HashSet<string>();
        var lists = new Dictionary<string, List<AddinDescription>>();

        foreach (var dlist in addins.Values)
        foreach (var desc in dlist)
            InsertSortedAddin(inserted, lists, desc);

        // Merge all domain lists into a single list.
        // Make sure the global domain is inserted the last

        List<AddinDescription> global;
        lists.TryGetValue(AddinDatabase.GlobalDomain, out global);
        lists.Remove(AddinDatabase.GlobalDomain);

        var sortedAddins = new List<AddinDescription>();
        foreach (var dl in lists.Values) sortedAddins.AddRange(dl);
        if (global != null)
            sortedAddins.AddRange(global);
        return sortedAddins;
    }

    private void InsertSortedAddin(HashSet<string> inserted, Dictionary<string, List<AddinDescription>> lists,
        AddinDescription desc)
    {
        var sid = desc.AddinId + " " + desc.Domain;
        if (!inserted.Add(sid))
            return;

        foreach (ModuleDescription mod in desc.AllModules)
        foreach (Dependency dep in mod.Dependencies)
        {
            var adep = dep as AddinDependency;
            if (adep == null)
                continue;
            var descs = FindDescriptions(desc.Domain, adep.FullAddinId);
            if (descs.Count > 0)
                foreach (var sd in descs)
                    InsertSortedAddin(inserted, lists, sd);
        }

        List<AddinDescription> list;
        if (!lists.TryGetValue(desc.Domain, out list))
            lists[desc.Domain] = list = new List<AddinDescription>();

        list.Add(desc);
    }
}

internal class AddinDatabaseTransaction : IDisposable
{
    private readonly AddinDatabase addinDatabase;
    private readonly object localLock;
    private AddinEngineTransaction addinEngineTransaction;
    private bool addinEngineTransactionStarted;

    public AddinDatabaseTransaction(AddinDatabase addinDatabase, object localLock,
        AddinEngineTransaction addinEngineTransaction)
    {
        this.addinDatabase = addinDatabase;
        this.localLock = localLock;
        this.addinEngineTransaction = addinEngineTransaction;
        Monitor.Enter(localLock);
    }

    public void Dispose()
    {
        Monitor.Exit(localLock);
        if (addinEngineTransactionStarted)
            addinEngineTransaction.Dispose();
    }

    public AddinEngineTransaction GetAddinEngineTransaction()
    {
        if (addinEngineTransaction != null)
            return addinEngineTransaction;
        addinEngineTransactionStarted = true;
        return addinEngineTransaction = addinDatabase.AddinEngine.BeginEngineTransaction();
    }
}

// Keep in sync with AddinSearchFlags
[Flags]
internal enum AddinSearchFlagsInternal
{
    IncludeAddins = 1,
    IncludeRoots = 1 << 1,
    IncludeAll = IncludeAddins | IncludeRoots,
    LatestVersionsOnly = 1 << 3,
    ExcludePendingUninstall = 1 << 4
}