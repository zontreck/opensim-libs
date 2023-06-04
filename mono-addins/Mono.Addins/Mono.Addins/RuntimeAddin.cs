﻿//
// RuntimeAddin.cs
//
// Author:
//   Lluis Sanchez Gual,
//   Georg Wächter
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
using System.Globalization;
using System.IO;
using System.Reflection;
using System.Reflection.Emit;
using System.Resources;
using Mono.Addins.Database;
using Mono.Addins.Description;
using Mono.Addins.Localization;

namespace Mono.Addins;

/// <summary>
///     Run-time representation of an add-in.
/// </summary>
public class RuntimeAddin
{
    private readonly AddinEngine addinEngine;
    private readonly string baseDirectory;
    private readonly string id;
    private readonly RuntimeAddin parentAddin;

    private RuntimeAddin[] depAddins;
    private bool fullyLoadedAssemblies;

    private readonly Dictionary<string, Assembly> loadedAssemblies = new();
    private AddinLocalizer localizer;
    private ExtensionNodeDescription localizerDescription;

    private string privatePath;
    private ResourceManager[] resourceManagers;

    internal RuntimeAddin(AddinEngine addinEngine, Addin iad)
    {
        this.addinEngine = addinEngine;

        Addin = iad;

        var description = iad.Description;
        id = description.AddinId;
        baseDirectory = description.BasePath;
        Module = description.MainModule;
        Module.RuntimeAddin = this;
        localizerDescription = description.Localizer;
    }

    internal RuntimeAddin(AddinEngine addinEngine, RuntimeAddin parentAddin, ModuleDescription module)
    {
        this.addinEngine = addinEngine;
        this.parentAddin = parentAddin;
        this.Module = module;
        id = parentAddin.id;
        baseDirectory = parentAddin.baseDirectory;
        privatePath = parentAddin.privatePath;
        Addin = parentAddin.Addin;
        module.RuntimeAddin = this;
    }

    internal ModuleDescription Module { get; }

    /// <summary>
    ///     Identifier of the add-in.
    /// </summary>
    public string Id => Addin.GetIdName(id);

    /// <summary>
    ///     Version of the add-in.
    /// </summary>
    public string Version => Addin.GetIdVersion(id);

    internal Addin Addin { get; }

    /// <summary>
    ///     Path to a directory where add-ins can store private configuration or status data
    /// </summary>
    public string PrivateDataPath
    {
        get
        {
            if (privatePath == null)
            {
                privatePath = Addin.PrivateDataPath;
                if (!Directory.Exists(privatePath))
                    Directory.CreateDirectory(privatePath);
            }

            return privatePath;
        }
    }

    /// <summary>
    ///     Localizer which can be used to localize strings defined in this add-in
    /// </summary>
    public AddinLocalizer Localizer
    {
        get
        {
            // If this is an optional module, the localizer is defined in the parent
            if (parentAddin != null)
                return parentAddin.Localizer;
            return LoadLocalizer() ?? addinEngine.DefaultLocalizer;
        }
    }

    /// <summary>
    ///     Returns a string that represents the current RuntimeAddin.
    /// </summary>
    /// <returns>
    ///     A string that represents the current RuntimeAddin.
    /// </returns>
    public override string ToString()
    {
        return Addin.ToString();
    }

    internal bool TryGetAssembly(string assemblyName, out Assembly assembly)
    {
        return loadedAssemblies.TryGetValue(assemblyName, out assembly);
    }

    internal IEnumerable<Assembly> GetLoadedAssemblies()
    {
        return loadedAssemblies.Values;
    }

    private ResourceManager[] GetResourceManagers()
    {
        if (resourceManagers != null)
            return resourceManagers;

        EnsureAssembliesLoaded();
        var managersList = new List<ResourceManager>();

        // Search for embedded resource files
        foreach (var kvp in loadedAssemblies)
        {
            var asm = kvp.Value;
            foreach (var res in asm.GetManifestResourceNames())
                if (res.EndsWith(".resources"))
                    managersList.Add(new ResourceManager(res.Substring(0, res.Length - ".resources".Length), asm));
        }

        return resourceManagers = managersList.ToArray();
    }

    /// <summary>
    ///     Gets a resource string
    /// </summary>
    /// <param name="name">
    ///     Name of the resource
    /// </param>
    /// <returns>
    ///     The value of the resource string, or null if the resource can't be found.
    /// </returns>
    /// <remarks>
    ///     The add-in engine will look for resources in the main add-in assembly and in all included add-in assemblies.
    /// </remarks>
    public string GetResourceString(string name)
    {
        return (string)GetResourceObject(name, true, null);
    }

    /// <summary>
    ///     Gets a resource string
    /// </summary>
    /// <param name="name">
    ///     Name of the resource
    /// </param>
    /// <param name="throwIfNotFound">
    ///     When set to true, an exception will be thrown if the resource is not found.
    /// </param>
    /// <returns>
    ///     The value of the resource string
    /// </returns>
    /// <remarks>
    ///     The add-in engine will look for resources in the main add-in assembly and in all included add-in assemblies.
    /// </remarks>
    public string GetResourceString(string name, bool throwIfNotFound)
    {
        return (string)GetResourceObject(name, throwIfNotFound, null);
    }

    /// <summary>
    ///     Gets a resource string
    /// </summary>
    /// <param name="name">
    ///     Name of the resource
    /// </param>
    /// <param name="throwIfNotFound">
    ///     When set to true, an exception will be thrown if the resource is not found.
    /// </param>
    /// <param name="culture">
    ///     Culture of the resource
    /// </param>
    /// <returns>
    ///     The value of the resource string
    /// </returns>
    /// <remarks>
    ///     The add-in engine will look for resources in the main add-in assembly and in all included add-in assemblies.
    /// </remarks>
    public string GetResourceString(string name, bool throwIfNotFound, CultureInfo culture)
    {
        return (string)GetResourceObject(name, throwIfNotFound, culture);
    }

    /// <summary>
    ///     Gets a resource object
    /// </summary>
    /// <param name="name">
    ///     Name of the resource
    /// </param>
    /// <returns>
    ///     Value of the resource
    /// </returns>
    /// <remarks>
    ///     The add-in engine will look for resources in the main add-in assembly and in all included add-in assemblies.
    /// </remarks>
    public object GetResourceObject(string name)
    {
        return GetResourceObject(name, true, null);
    }

    /// <summary>
    ///     Gets a resource object
    /// </summary>
    /// <param name="name">
    ///     Name of the resource
    /// </param>
    /// <param name="throwIfNotFound">
    ///     When set to true, an exception will be thrown if the resource is not found.
    /// </param>
    /// <returns>
    ///     Value of the resource
    /// </returns>
    /// <remarks>
    ///     The add-in engine will look for resources in the main add-in assembly and in all included add-in assemblies.
    /// </remarks>
    public object GetResourceObject(string name, bool throwIfNotFound)
    {
        return GetResourceObject(name, throwIfNotFound, null);
    }

    /// <summary>
    ///     Gets a resource object
    /// </summary>
    /// <param name="name">
    ///     Name of the resource
    /// </param>
    /// <param name="throwIfNotFound">
    ///     When set to true, an exception will be thrown if the resource is not found.
    /// </param>
    /// <param name="culture">
    ///     Culture of the resource
    /// </param>
    /// <returns>
    ///     Value of the resource
    /// </returns>
    /// <remarks>
    ///     The add-in engine will look for resources in the main add-in assembly and in all included add-in assemblies.
    /// </remarks>
    public object GetResourceObject(string name, bool throwIfNotFound, CultureInfo culture)
    {
        // Look in resources of this add-in
        foreach (var manager in GetAllResourceManagers())
        {
            var t = manager.GetObject(name, culture);
            if (t != null)
                return t;
        }

        // Look in resources of dependent add-ins
        foreach (var addin in GetAllDependencies())
        {
            var t = addin.GetResourceObject(name, false, culture);
            if (t != null)
                return t;
        }

        if (throwIfNotFound)
            throw new InvalidOperationException("Resource object '" + name + "' not found in add-in '" + id + "'");

        return null;
    }

    /// <summary>
    ///     Gets a type defined in the add-in
    /// </summary>
    /// <param name="typeName">
    ///     Full name of the type
    /// </param>
    /// <returns>
    ///     A type.
    /// </returns>
    /// <remarks>
    ///     The type will be looked up in the assemblies that implement the add-in,
    ///     and recursively in all add-ins on which it depends.
    ///     This method throws an InvalidOperationException if the type can't be found.
    /// </remarks>
    public Type GetType(string typeName)
    {
        return GetType(typeName, true);
    }

    /// <summary>
    ///     Gets a type defined in the add-in
    /// </summary>
    /// <param name="typeName">
    ///     Full name of the type
    /// </param>
    /// <param name="throwIfNotFound">
    ///     Indicates whether the method should throw an exception if the type can't be found.
    /// </param>
    /// <returns>
    ///     A <see cref="Type" />
    /// </returns>
    /// <remarks>
    ///     The type will be looked up in the assemblies that implement the add-in,
    ///     and recursively in all add-ins on which it depends.
    ///     If the type can't be found, this method throw a InvalidOperationException if
    ///     'throwIfNotFound' is 'true', or 'null' otherwise.
    /// </remarks>
    public Type GetType(string typeName, bool throwIfNotFound)
    {
        // Try looking in Mono.Addins without loading the addin assemblies.
        var type = Type.GetType(typeName, false);
        if (type == null)
            // decode the name if it's qualified
            if (Util.TryParseTypeName(typeName, out var t, out var assemblyName))
                type = GetType_Expensive(t, assemblyName ?? "");

        if (throwIfNotFound && type == null)
            throw new InvalidOperationException("Type '" + typeName + "' not found in add-in '" + id + "'");
        return type;
    }

    private Type GetType_Expensive(string typeName, string assemblyName)
    {
        // Look in the addin assemblies and in dependent add-ins.
        // PERF: Unrolled from GetAllAssemblies and GetAllDependencies to avoid allocations.
        EnsureAssembliesLoaded();

        foreach (var kvp in loadedAssemblies)
        {
            var assembly = kvp.Value;
            if (string.IsNullOrEmpty(assemblyName) || assembly.GetName().Name == assemblyName)
            {
                var type = assembly.GetType(typeName, false);
                if (type != null)
                    return type;
            }
        }

        var addins = GetDepAddins();
        if (addins != null)
            foreach (var addin in addins)
            {
                var t = addin.GetType_Expensive(typeName, assemblyName);
                if (t != null)
                    return t;
            }

        return parentAddin?.GetType_Expensive(typeName, assemblyName);
    }

    private IEnumerable<ResourceManager> GetAllResourceManagers()
    {
        foreach (var rm in GetResourceManagers())
            yield return rm;

        if (parentAddin != null)
            foreach (var rm in parentAddin.GetResourceManagers())
                yield return rm;
    }

    private IEnumerable<Assembly> GetAllAssemblies()
    {
        foreach (var asm in loadedAssemblies.Values)
            yield return asm;

        // Look in the parent addin assemblies

        if (parentAddin != null)
            foreach (var asm in parentAddin.loadedAssemblies.Values)
                yield return asm;
    }

    private IEnumerable<RuntimeAddin> GetAllDependencies()
    {
        // Look in the dependent add-ins
        foreach (var addin in GetDepAddins())
            yield return addin;

        if (parentAddin != null)
            // Look in the parent dependent add-ins
            foreach (var addin in parentAddin.GetDepAddins())
                yield return addin;
    }

    /// <summary>
    ///     Creates an instance of a type defined in the add-in
    /// </summary>
    /// <param name="typeName">
    ///     Name of the type.
    /// </param>
    /// <returns>
    ///     A new instance of the type
    /// </returns>
    /// <remarks>
    ///     The type will be looked up in the assemblies that implement the add-in,
    ///     and recursively in all add-ins on which it depends.
    ///     This method throws an InvalidOperationException if the type can't be found.
    ///     The specified type must have a default constructor.
    /// </remarks>
    public object CreateInstance(string typeName)
    {
        return CreateInstance(typeName, true);
    }

    /// <summary>
    ///     Creates an instance of a type defined in the add-in
    /// </summary>
    /// <param name="typeName">
    ///     Name of the type.
    /// </param>
    /// <param name="throwIfNotFound">
    ///     Indicates whether the method should throw an exception if the type can't be found.
    /// </param>
    /// <returns>
    ///     A new instance of the type
    /// </returns>
    /// <remarks>
    ///     The type will be looked up in the assemblies that implement the add-in,
    ///     and recursively in all add-ins on which it depends.
    ///     If the type can't be found, this method throw a InvalidOperationException if
    ///     'throwIfNotFound' is 'true', or 'null' otherwise.
    ///     The specified type must have a default constructor.
    /// </remarks>
    public object CreateInstance(string typeName, bool throwIfNotFound)
    {
        var type = GetType(typeName, throwIfNotFound);
        if (type == null)
            return null;
        return Activator.CreateInstance(type, true);
    }

    /// <summary>
    ///     Gets the path of an add-in file
    /// </summary>
    /// <param name="fileName">
    ///     Relative path of the file
    /// </param>
    /// <returns>
    ///     Full path of the file
    /// </returns>
    /// <remarks>
    ///     This method can be used to get the full path of a data file deployed together with the add-in.
    /// </remarks>
    public string GetFilePath(string fileName)
    {
        return Path.Combine(baseDirectory, fileName);
    }

    /// <summary>
    ///     Gets the path of an add-in file
    /// </summary>
    /// <param name="filePath">
    ///     Components of the file path
    /// </param>
    /// <returns>
    ///     Full path of the file
    /// </returns>
    /// <remarks>
    ///     This method can be used to get the full path of a data file deployed together with the add-in.
    /// </remarks>
    public string GetFilePath(params string[] filePath)
    {
        return Path.Combine(baseDirectory, string.Join("" + Path.DirectorySeparatorChar, filePath));
    }

    /// <summary>
    ///     Gets the content of a resource
    /// </summary>
    /// <param name="resourceName">
    ///     Name of the resource
    /// </param>
    /// <returns>
    ///     Content of the resource, or null if not found
    /// </returns>
    /// <remarks>
    ///     The add-in engine will look for resources in the main add-in assembly and in all included add-in assemblies.
    /// </remarks>
    public Stream GetResource(string resourceName)
    {
        return GetResource(resourceName, false);
    }

    /// <summary>
    ///     Gets the content of a resource
    /// </summary>
    /// <param name="resourceName">
    ///     Name of the resource
    /// </param>
    /// <param name="throwIfNotFound">
    ///     When set to true, an exception will be thrown if the resource is not found.
    /// </param>
    /// <returns>
    ///     Content of the resource.
    /// </returns>
    /// <remarks>
    ///     The add-in engine will look for resources in the main add-in assembly and in all included add-in assemblies.
    /// </remarks>
    public Stream GetResource(string resourceName, bool throwIfNotFound)
    {
        EnsureAssembliesLoaded();

        // Look in the addin assemblies

        foreach (var asm in GetAllAssemblies())
        {
            var res = asm.GetManifestResourceStream(resourceName);
            if (res != null)
                return res;
        }

        // Look in the dependent add-ins
        foreach (var addin in GetAllDependencies())
        {
            var res = addin.GetResource(resourceName);
            if (res != null)
                return res;
        }

        if (throwIfNotFound)
            throw new InvalidOperationException("Resource '" + resourceName + "' not found in add-in '" + id + "'");

        return null;
    }

    /// <summary>
    ///     Returns information about how the given resource has been persisted
    /// </summary>
    /// <param name="resourceName">
    ///     Name of the resource
    /// </param>
    /// <returns>
    ///     Resource information, or null if the resource doesn't exist
    /// </returns>
    public ManifestResourceInfo GetResourceInfo(string resourceName)
    {
        EnsureAssembliesLoaded();

        // Look in the addin assemblies

        foreach (var asm in GetAllAssemblies())
        {
            var res = asm.GetManifestResourceInfo(resourceName);
            if (res != null)
            {
                // Mono doesn't set the referenced assembly
                if (res.ReferencedAssembly == null)
                    return new ManifestResourceInfo(asm, res.FileName, res.ResourceLocation);
                return res;
            }
        }

        // Look in the dependent add-ins
        foreach (var addin in GetAllDependencies())
        {
            var res = addin.GetResourceInfo(resourceName);
            if (res != null)
                return res;
        }

        return null;
    }

    internal RuntimeAddin GetModule(ModuleDescription module)
    {
        // If requesting the root module, return this
        if (module == module.ParentAddinDescription.MainModule)
            return this;

        if (module.RuntimeAddin != null)
            return module.RuntimeAddin;

        var addin = new RuntimeAddin(addinEngine, this, module);
        return addin;
    }

    private AddinLocalizer LoadLocalizer()
    {
        if (localizerDescription != null)
        {
            var cls = localizerDescription.GetAttribute("type");

            // First try getting one of the stock localizers. If none of found try getting the type.
            // They are not encoded as an assembly qualified name
            object fob = null;
            if (cls.IndexOf(',') == -1)
            {
                var t = GetType().Assembly.GetType("Mono.Addins.Localization." + cls + "Localizer", false);
                if (t != null)
                    fob = Activator.CreateInstance(t);
            }

            if (fob == null)
                fob = CreateInstance(cls, true);

            var factory = fob as IAddinLocalizerFactory;
            if (factory == null)
                throw new InvalidOperationException("Localizer factory type '" + cls +
                                                    "' must implement IAddinLocalizerFactory");
            localizer = new AddinLocalizer(factory.CreateLocalizer(this, localizerDescription));
            localizerDescription = null;
        }

        return localizer;
    }

    private RuntimeAddin[] GetDepAddins()
    {
        if (depAddins != null)
            return depAddins;

        var plugList = new List<RuntimeAddin>();
        var ns = Addin.Description.Namespace;

        // Collect dependent ids
        foreach (Dependency dep in Module.Dependencies)
        {
            var pdep = dep as AddinDependency;
            if (pdep != null)
            {
                var adn = addinEngine.GetAddin(Addin.GetFullId(ns, pdep.AddinId, pdep.Version));
                if (adn != null)
                    plugList.Add(adn);
                else
                    addinEngine.ReportError("Add-in dependency not loaded: " + pdep.FullAddinId,
                        Module.ParentAddinDescription.AddinId, null, false);
            }
        }

        return depAddins = plugList.ToArray();
    }

    internal void RegisterAssemblyLoad(string assemblyName, Assembly assembly)
    {
        loadedAssemblies.Add(assemblyName, assembly);
    }

    private void LoadModule(ModuleDescription module)
    {
        // Load the assemblies
        for (var i = 0; i < module.Assemblies.Count; ++i)
        {
            if (loadedAssemblies.TryGetValue(module.AssemblyNames[i], out var asm))
                continue;

            // Backwards compat: Load all the addins on demand if an assembly name
            // is not supplied for the type.

            // don't load the assembly if it's already loaded
            var asmPath = GetFilePath(module.Assemblies[i]);
            foreach (var a in AppDomain.CurrentDomain.GetAssemblies())
            {
                // Sorry, you can't load addins from
                // dynamic assemblies as get_Location
                // throws a NotSupportedException
                if (a is AssemblyBuilder || a.IsDynamic) continue;

                try
                {
                    if (a.Location == asmPath)
                    {
                        asm = a;
                        break;
                    }
                }
                catch (NotSupportedException)
                {
                    // Some assemblies don't have a location
                }
            }

            if (asm == null) asm = Assembly.LoadFrom(asmPath);

            RegisterAssemblyLoad(module.AssemblyNames[i], asm);
        }
    }

    internal void UnloadExtensions(ExtensionContextTransaction transaction)
    {
        addinEngine.UnregisterAddinNodeSets(transaction, id);
    }

    private bool CheckAddinDependencies(ModuleDescription module, bool forceLoadAssemblies)
    {
        foreach (Dependency dep in module.Dependencies)
        {
            var pdep = dep as AddinDependency;
            if (pdep == null)
                continue;
            var addin = addinEngine.GetAddin(pdep.FullAddinId);
            if (addin == null)
                return false;
            if (forceLoadAssemblies)
                addin.EnsureAssembliesLoaded();
        }

        return true;
    }

    internal void EnsureAssembliesLoaded()
    {
        if (fullyLoadedAssemblies)
            return;
        fullyLoadedAssemblies = true;

        // Load the assemblies of the module
        CheckAddinDependencies(Module, true);
        LoadModule(Module);
        addinEngine.ReportAddinAssembliesLoad(id);
    }
}