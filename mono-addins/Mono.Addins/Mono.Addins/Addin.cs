//
// Addin.cs
//
// Author:
//   Lluis Sanchez Gual
//
// Copyright (C) 2005 Novell, Inc (http://www.novell.com)
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
using System.Linq;
using Mono.Addins.Database;
using Mono.Addins.Description;

namespace Mono.Addins;

/// <summary>
///     An add-in.
/// </summary>
public class Addin
{
    private AddinInfo addin;
    private readonly AddinDatabase database;
    private WeakReference desc;
    private readonly string domain;
    private bool? isLatestVersion;
    private bool? isUserAddin;
    private string sourceFile;

    internal Addin(AddinDatabase database, string domain, string id)
    {
        this.database = database;
        Id = id;
        this.domain = domain;
        LoadAddinInfo();
    }

    /// <summary>
    ///     Full identifier of the add-in, including namespace and version.
    /// </summary>
    public string Id { get; }

    /// <summary>
    ///     Namespace of the add-in.
    /// </summary>
    public string Namespace => AddinInfo.Namespace;

    /// <summary>
    ///     Identifier of the add-in (without namespace)
    /// </summary>
    public string LocalId => AddinInfo.LocalId;

    /// <summary>
    ///     Version of the add-in
    /// </summary>
    public string Version => AddinInfo.Version;

    /// <summary>
    ///     Display name of the add-in
    /// </summary>
    public string Name => AddinInfo.Name;

    /// <summary>
    ///     Custom properties specified in the add-in header
    /// </summary>
    public AddinPropertyCollection Properties => AddinInfo.Properties;

    internal string PrivateDataPath => Path.Combine(database.AddinPrivateDataPath,
        Path.GetFileNameWithoutExtension(Description.FileName));

    internal AddinInfo AddinInfo
    {
        get
        {
            var addinInfo = addin;

            if (addinInfo == null)
                try
                {
                    addinInfo = addin = AddinInfo.ReadFromDescription(Description);
                }
                catch (Exception ex)
                {
                    throw new InvalidOperationException(
                        "Could not read add-in file: " + database.GetDescriptionPath(domain, Id), ex);
                }

            return addinInfo;
        }
    }

    /// <summary>
    ///     Gets or sets the enabled status of the add-in.
    /// </summary>
    /// <remarks>
    ///     This property can be used to enable or disable an add-in.
    ///     The enabled status of an add-in is stored in the add-in registry,
    ///     so when an add-in is disabled, it will be disabled for all applications
    ///     sharing the same registry.
    ///     When an add-in is enabled or disabled, the extension points currently loaded
    ///     in memory will be properly updated to include or exclude extensions from the add-in.
    /// </remarks>
    public bool Enabled
    {
        get
        {
            if (!IsLatestVersion)
                return false;
            return AddinInfo.IsRoot ? true : database.IsAddinEnabled(Description.Domain, AddinInfo.Id, true);
        }
        set
        {
            if (value)
                database.EnableAddin(Description.Domain, AddinInfo.Id, true);
            else
                database.DisableAddin(Description.Domain, AddinInfo.Id);
        }
    }

    internal bool IsLatestVersion
    {
        get
        {
            if (isLatestVersion == null)
            {
                string id, version;
                GetIdParts(AddinInfo.Id, out id, out version);
                var addins = database.GetInstalledAddins(null,
                    AddinSearchFlagsInternal.IncludeAll | AddinSearchFlagsInternal.LatestVersionsOnly);
                isLatestVersion = addins.Any(a => GetIdName(a.Id) == id && a.Version == version);
            }

            return isLatestVersion.Value;
        }
        set => isLatestVersion = value;
    }

    /// <summary>
    ///     Returns 'true' if the add-in is installed in the user's personal folder
    /// </summary>
    public bool IsUserAddin
    {
        get
        {
            if (isUserAddin == null)
                SetIsUserAddin(Description);
            return isUserAddin.Value;
        }
    }

    /// <summary>
    ///     Path to the add-in file (it can be an assembly or a standalone XML manifest)
    /// </summary>
    public string AddinFile
    {
        get
        {
            if (sourceFile == null && addin == null)
                LoadAddinInfo();
            return sourceFile;
        }
    }

    /// <summary>
    ///     Description of the add-in
    /// </summary>
    public AddinDescription Description
    {
        get
        {
            var addinDescription = (AddinDescription)desc?.Target;
            if (addinDescription != null) return addinDescription;

            var configFile = database.GetDescriptionPath(domain, Id);

            database.ReadAddinDescription(new ConsoleProgressStatus(true), configFile, out addinDescription);

            if (addinDescription == null)
            {
                try
                {
                    if (File.Exists(configFile))
                        // The file is corrupted. Remove it.
                        File.Delete(configFile);
                }
                catch
                {
                    // Ignore
                }

                throw new InvalidOperationException("Could not read add-in description");
            }

            if (addin == null)
            {
                addin = AddinInfo.ReadFromDescription(addinDescription);
                sourceFile = addinDescription.AddinFile;
            }

            SetIsUserAddin(addinDescription);
            if (!isUserAddin.Value)
                addinDescription.Flags |= AddinFlags.CantUninstall;
            desc = new WeakReference(addinDescription);
            return addinDescription;
        }
    }

    /// <summary>
    ///     Checks version compatibility.
    /// </summary>
    /// <param name="version">
    ///     An add-in version.
    /// </param>
    /// <returns>
    ///     True if the provided version is compatible with this add-in.
    /// </returns>
    /// <remarks>
    ///     This method checks the CompatVersion property to know if the provided version is compatible with the version of
    ///     this add-in.
    /// </remarks>
    public bool SupportsVersion(string version)
    {
        return AddinInfo.SupportsVersion(version);
    }

    /// <summary>
    ///     Returns a <see cref="System.String" /> that represents the current <see cref="Mono.Addins.Addin" />.
    /// </summary>
    /// <returns>
    ///     A <see cref="System.String" /> that represents the current <see cref="Mono.Addins.Addin" />.
    /// </returns>
    public override string ToString()
    {
        return Id;
    }

    private void SetIsUserAddin(AddinDescription adesc)
    {
        var installPath = database.Registry.DefaultAddinsFolder;
        if (installPath[installPath.Length - 1] != Path.DirectorySeparatorChar)
            installPath += Path.DirectorySeparatorChar;
        isUserAddin = adesc != null && Path.GetFullPath(adesc.AddinFile).StartsWith(installPath);
    }

    private void LoadAddinInfo()
    {
        if (addin == null)
            try
            {
                var m = Description;
                sourceFile = m.AddinFile;
                addin = AddinInfo.ReadFromDescription(m);
            }
            catch (Exception ex)
            {
                throw new InvalidOperationException(
                    "Could not read add-in file: " + database.GetDescriptionPath(domain, Id), ex);
            }
    }

    internal void ResetCachedData()
    {
        // The domain may have changed (?!)

        // This check has been commented out because GetFolderDomain will fail if sourceFile changed
        // or if there is no folder info for the add-in (it may happen when using pre-generated add-in
        // scan data files).
        // A domain change at run-time is an unlikely scenario and not properly supported anyway in
        // other parts of the code. In general, changes in an already loaded add-in are not supported.

//			if (sourceFile != null)
//				domain = database.GetFolderDomain (null, Path.GetDirectoryName (sourceFile));

        desc = null;
        addin = null;
    }

    /// <summary>
    ///     Compares two add-in versions
    /// </summary>
    /// <returns>
    ///     -1 if v1 is greater than v2, 0 if v1 == v2, 1 if v1 less than v2
    /// </returns>
    /// <param name='v1'>
    ///     A version
    /// </param>
    /// <param name='v2'>
    ///     A version
    /// </param>
    public static int CompareVersions(string v1, string v2)
    {
        var a1 = v1.Split('.');
        var a2 = v2.Split('.');

        for (var n = 0; n < a1.Length; n++)
        {
            if (n >= a2.Length)
                return -1;
            if (a1[n].Length == 0)
            {
                if (a2[n].Length != 0)
                    return 1;
                continue;
            }

            try
            {
                var n1 = int.Parse(a1[n]);
                var n2 = int.Parse(a2[n]);
                if (n1 < n2)
                    return 1;
                if (n1 > n2)
                    return -1;
            }
            catch
            {
                return 1;
            }
        }

        if (a2.Length > a1.Length)
            return 1;
        return 0;
    }

    /// <summary>
    ///     Returns the identifier of an add-in
    /// </summary>
    /// <returns>
    ///     The full identifier.
    /// </returns>
    /// <param name='ns'>
    ///     Namespace of the add-in
    /// </param>
    /// <param name='id'>
    ///     Name of the add-in
    /// </param>
    /// <param name='version'>
    ///     Version of the add-in
    /// </param>
    public static string GetFullId(string ns, string id, string version)
    {
        string res;
        if (id.StartsWith("::", StringComparison.Ordinal))
            res = id.Substring(2);
        else if (ns != null && ns.Length > 0)
            res = ns + "." + id;
        else
            res = id;

        if (version != null && version.Length > 0)
            return res + "," + version;
        return res;
    }

    /// <summary>
    ///     Given a full add-in identifier, returns the namespace and name of the add-in (it removes the version number)
    /// </summary>
    /// <param name='addinId'>
    ///     Add-in identifier.
    /// </param>
    public static string GetIdName(string addinId)
    {
        var i = addinId.IndexOf(',');
        if (i != -1)
            return addinId.Substring(0, i);
        return addinId;
    }

    /// <summary>
    ///     Given a full add-in identifier, returns the version the add-in
    /// </summary>
    /// <returns>
    ///     The version.
    /// </returns>
    public static string GetIdVersion(string addinId)
    {
        var i = addinId.IndexOf(',');
        if (i != -1)
            return addinId.Substring(i + 1).Trim();
        return string.Empty;
    }

    /// <summary>
    ///     Splits a full add-in identifier in name and version
    /// </summary>
    /// <param name='addinId'>
    ///     Add-in identifier.
    /// </param>
    /// <param name='name'>
    ///     The resulting name
    /// </param>
    /// <param name='version'>
    ///     The resulting version
    /// </param>
    public static void GetIdParts(string addinId, out string name, out string version)
    {
        var i = addinId.IndexOf(',');
        if (i != -1)
        {
            name = addinId.Substring(0, i);
            version = addinId.Substring(i + 1).Trim();
        }
        else
        {
            name = addinId;
            version = string.Empty;
        }
    }
}