//
// AddinDescription.cs
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
using System.Text;
using System.Xml;
using Mono.Addins.Database;
using Mono.Addins.Serialization;

namespace Mono.Addins.Description;

/// <summary>
///     An add-in description
/// </summary>
/// <remarks>
///     This class represent an add-in manifest. It has properties for getting
///     all information, and methods for loading and saving files.
/// </remarks>
public class AddinDescription : IBinaryXmlElement
{
    internal static BinaryXmlTypeMap typeMap;
    private string author;
    private string basePath;
    private bool canWrite = true;
    private string category;
    private string compatVersion;
    private ConditionTypeDescriptionCollection conditionTypes;
    private XmlDocument configDoc;
    private string copyright;
    private string description;
    private ExtensionPointCollection extensionPoints;
    private object[] fileInfo;

    private string id;

    private ModuleDescription mainModule;
    private string name;
    private ExtensionNodeSetCollection nodeSets;
    private string ns;
    private ModuleCollection optionalModules;

    private AddinPropertyCollectionImpl properties;
    private string url;
    private Dictionary<string, string> variables;
    private string version;

    static AddinDescription()
    {
        typeMap = new BinaryXmlTypeMap();
        typeMap.RegisterType(typeof(AddinDescription), "AddinDescription");
        typeMap.RegisterType(typeof(Extension), "Extension");
        typeMap.RegisterType(typeof(ExtensionNodeDescription), "Node");
        typeMap.RegisterType(typeof(ExtensionNodeSet), "NodeSet");
        typeMap.RegisterType(typeof(ExtensionNodeType), "NodeType");
        typeMap.RegisterType(typeof(ExtensionPoint), "ExtensionPoint");
        typeMap.RegisterType(typeof(ModuleDescription), "ModuleDescription");
        typeMap.RegisterType(typeof(ConditionTypeDescription), "ConditionType");
        typeMap.RegisterType(typeof(Condition), "Condition");
        typeMap.RegisterType(typeof(AddinDependency), "AddinDependency");
        typeMap.RegisterType(typeof(AssemblyDependency), "AssemblyDependency");
        typeMap.RegisterType(typeof(NodeTypeAttribute), "NodeTypeAttribute");
        typeMap.RegisterType(typeof(AddinFileInfo), "FileInfo");
        typeMap.RegisterType(typeof(AddinProperty), "Property");
    }

    internal AddinDatabase OwnerDatabase { get; set; }

    /// <summary>
    ///     Gets or sets the path to the main addin file.
    /// </summary>
    /// <value>
    ///     The addin file.
    /// </value>
    /// <remarks>
    ///     The add-in file can be either the main assembly of an add-in or an xml manifest.
    /// </remarks>
    public string AddinFile { get; set; }

    /// <summary>
    ///     Gets the addin identifier.
    /// </summary>
    /// <value>
    ///     The addin identifier.
    /// </value>
    public string AddinId => Addin.GetFullId(Namespace, LocalId, Version);

    /// <summary>
    ///     Gets or sets the local identifier.
    /// </summary>
    /// <value>
    ///     The local identifier.
    /// </value>
    public string LocalId
    {
        get => id != null ? ParseString(id) : string.Empty;
        set
        {
            id = value;
            HasUserId = true;
        }
    }

    /// <summary>
    ///     Gets or sets the namespace.
    /// </summary>
    /// <value>
    ///     The namespace.
    /// </value>
    public string Namespace
    {
        get => ns != null ? ParseString(ns) : string.Empty;
        set => ns = value;
    }

    /// <summary>
    ///     Gets or sets the display name of the add-in.
    /// </summary>
    /// <value>
    ///     The name.
    /// </value>
    public string Name
    {
        get
        {
            var val = Properties.GetPropertyValue("Name");
            if (val.Length > 0)
                return val;
            if (name != null && name.Length > 0)
                return ParseString(name);
            if (HasUserId)
                return AddinId;
            if (AddinFile != null)
                return Path.GetFileNameWithoutExtension(AddinFile);
            return string.Empty;
        }
        set => name = value;
    }

    /// <summary>
    ///     Gets or sets the version.
    /// </summary>
    /// <value>
    ///     The version.
    /// </value>
    public string Version
    {
        get => version != null ? ParseString(version) : string.Empty;
        set => version = value;
    }

    /// <summary>
    ///     Gets or sets the version of the add-in with which this add-in is backwards compatible.
    /// </summary>
    /// <value>
    ///     The compat version.
    /// </value>
    public string CompatVersion
    {
        get => compatVersion != null ? ParseString(compatVersion) : string.Empty;
        set => compatVersion = value;
    }

    /// <summary>
    ///     Gets or sets the author.
    /// </summary>
    /// <value>
    ///     The author.
    /// </value>
    public string Author
    {
        get
        {
            var val = Properties.GetPropertyValue("Author");
            if (val.Length > 0)
                return val;
            return ParseString(author) ?? string.Empty;
        }
        set => author = value;
    }

    /// <summary>
    ///     Gets or sets the Url where more information about the add-in can be found.
    /// </summary>
    /// <value>
    ///     The URL.
    /// </value>
    public string Url
    {
        get
        {
            var val = Properties.GetPropertyValue("Url");
            if (val.Length > 0)
                return val;
            return ParseString(url) ?? string.Empty;
        }
        set => url = value;
    }

    /// <summary>
    ///     Gets or sets the copyright.
    /// </summary>
    /// <value>
    ///     The copyright.
    /// </value>
    public string Copyright
    {
        get
        {
            var val = Properties.GetPropertyValue("Copyright");
            if (val.Length > 0)
                return val;
            return ParseString(copyright) ?? string.Empty;
        }
        set => copyright = value;
    }

    /// <summary>
    ///     Gets or sets the description of the add-in.
    /// </summary>
    /// <value>
    ///     The description.
    /// </value>
    public string Description
    {
        get
        {
            var val = Properties.GetPropertyValue("Description");
            if (val.Length > 0)
                return val;
            return ParseString(description) ?? string.Empty;
        }
        set => description = value;
    }

    /// <summary>
    ///     Gets or sets the category of the add-in.
    /// </summary>
    /// <value>
    ///     The category.
    /// </value>
    public string Category
    {
        get
        {
            var val = Properties.GetPropertyValue("Category");
            if (val.Length > 0)
                return val;
            return ParseString(category) ?? string.Empty;
        }
        set => category = value;
    }

    /// <summary>
    ///     Gets the base path for locating external files relative to the add-in.
    /// </summary>
    /// <value>
    ///     The base path.
    /// </value>
    public string BasePath => basePath != null ? basePath : string.Empty;

    /// <summary>
    ///     Gets or sets a value indicating whether this instance is an add-in root.
    /// </summary>
    /// <value>
    ///     <c>true</c> if this instance is an add-in root; otherwise, <c>false</c>.
    /// </value>
    public bool IsRoot { get; set; }

    /// <summary>
    ///     Gets or sets a value indicating whether this add-in is enabled by default.
    /// </summary>
    /// <value>
    ///     <c>true</c> if enabled by default; otherwise, <c>false</c>.
    /// </value>
    public bool EnabledByDefault { get; set; } = true;

    /// <summary>
    ///     Gets or sets the add-in flags.
    /// </summary>
    /// <value>
    ///     The flags.
    /// </value>
    public AddinFlags Flags { get; set; } = AddinFlags.None;

    internal bool HasUserId { get; set; }

    /// <summary>
    ///     Gets a value indicating whether this add-in can be disabled.
    /// </summary>
    /// <value>
    ///     <c>true</c> if this add-in can be disabled; otherwise, <c>false</c>.
    /// </value>
    public bool CanDisable => (Flags & AddinFlags.CantDisable) == 0 && !IsHidden;

    /// <summary>
    ///     Gets a value indicating whether this add-in can be uninstalled.
    /// </summary>
    /// <value>
    ///     <c>true</c> if this instance can be uninstalled; otherwise, <c>false</c>.
    /// </value>
    public bool CanUninstall => (Flags & AddinFlags.CantUninstall) == 0 && !IsHidden;

    /// <summary>
    ///     Gets a value indicating whether this add-in is hidden.
    /// </summary>
    /// <value>
    ///     <c>true</c> if this add-in is hidden; otherwise, <c>false</c>.
    /// </value>
    public bool IsHidden => (Flags & AddinFlags.Hidden) != 0;

    /// <summary>
    ///     Gets all external files
    /// </summary>
    /// <value>
    ///     All files.
    /// </value>
    /// <remarks>
    ///     External files are data files and assemblies explicitly referenced in the Runtime section of the add-in manifest.
    /// </remarks>
    public StringCollection AllFiles
    {
        get
        {
            var col = new StringCollection();
            foreach (var s in MainModule.AllFiles)
                col.Add(s);

            foreach (ModuleDescription mod in OptionalModules)
            foreach (var s in mod.AllFiles)
                col.Add(s);
            return col;
        }
    }

    /// <summary>
    ///     Gets all paths to be ignored by the add-in scanner.
    /// </summary>
    /// <value>
    ///     All paths to be ignored.
    /// </value>
    public StringCollection AllIgnorePaths
    {
        get
        {
            var col = new StringCollection();
            foreach (var s in MainModule.IgnorePaths)
                col.Add(s);

            foreach (ModuleDescription mod in OptionalModules)
            foreach (var s in mod.IgnorePaths)
                col.Add(s);
            return col;
        }
    }

    /// <summary>
    ///     Gets the main module.
    /// </summary>
    /// <value>
    ///     The main module.
    /// </value>
    public ModuleDescription MainModule
    {
        get
        {
            if (mainModule == null)
            {
                if (RootElement == null)
                    mainModule = new ModuleDescription();
                else
                    mainModule = new ModuleDescription(RootElement);
                mainModule.SetParent(this);
            }

            return mainModule;
        }
    }

    /// <summary>
    ///     Gets the optional modules.
    /// </summary>
    /// <value>
    ///     The optional modules.
    /// </value>
    /// <remarks>
    ///     Optional modules can be used to declare extensions which will be registered only if some specified
    ///     add-in dependencies can be satisfied. Dependencies specified in optional modules are 'soft dependencies',
    ///     which means that they don't need to be satisfied in order to load the add-in.
    /// </remarks>
    public ModuleCollection OptionalModules
    {
        get
        {
            if (optionalModules == null)
            {
                optionalModules = new ModuleCollection(this);
                if (RootElement != null)
                    foreach (XmlElement mod in RootElement.SelectNodes("Module"))
                        optionalModules.Add(new ModuleDescription(mod));
            }

            return optionalModules;
        }
    }

    /// <summary>
    ///     Gets all modules (including the main module and all optional modules)
    /// </summary>
    /// <value>
    ///     All modules.
    /// </value>
    public ModuleCollection AllModules
    {
        get
        {
            var col = new ModuleCollection(this);
            col.Add(MainModule);
            foreach (ModuleDescription mod in OptionalModules)
                col.Add(mod);
            return col;
        }
    }

    /// <summary>
    ///     Gets the extension node sets.
    /// </summary>
    /// <value>
    ///     The extension node sets.
    /// </value>
    public ExtensionNodeSetCollection ExtensionNodeSets
    {
        get
        {
            if (nodeSets == null)
            {
                nodeSets = new ExtensionNodeSetCollection(this);
                if (RootElement != null)
                    foreach (XmlElement elem in RootElement.SelectNodes("ExtensionNodeSet"))
                        nodeSets.Add(new ExtensionNodeSet(elem));
            }

            return nodeSets;
        }
    }

    /// <summary>
    ///     Gets the extension points.
    /// </summary>
    /// <value>
    ///     The extension points.
    /// </value>
    public ExtensionPointCollection ExtensionPoints
    {
        get
        {
            if (extensionPoints == null)
            {
                extensionPoints = new ExtensionPointCollection(this);
                if (RootElement != null)
                    foreach (XmlElement elem in RootElement.SelectNodes("ExtensionPoint"))
                        extensionPoints.Add(new ExtensionPoint(elem));
            }

            return extensionPoints;
        }
    }

    /// <summary>
    ///     Gets the condition types.
    /// </summary>
    /// <value>
    ///     The condition types.
    /// </value>
    public ConditionTypeDescriptionCollection ConditionTypes
    {
        get
        {
            if (conditionTypes == null)
            {
                conditionTypes = new ConditionTypeDescriptionCollection(this);
                if (RootElement != null)
                    foreach (XmlElement elem in RootElement.SelectNodes("ConditionType"))
                        conditionTypes.Add(new ConditionTypeDescription(elem));
            }

            return conditionTypes;
        }
    }

    /// <summary>
    ///     Gets or sets the add-in localizer.
    /// </summary>
    /// <value>
    ///     The description of the add-in localizer for this add-in.
    /// </value>
    public ExtensionNodeDescription Localizer { get; set; }

    /// <summary>
    ///     Custom properties specified in the add-in header
    /// </summary>
    public AddinPropertyCollection Properties
    {
        get
        {
            if (properties == null)
                properties = new AddinPropertyCollectionImpl(this);
            return properties;
        }
    }

    private XmlElement RootElement
    {
        get
        {
            if (configDoc != null)
                return configDoc.DocumentElement;
            return null;
        }
    }

    /// <summary>
    ///     Gets or sets file where this description is stored
    /// </summary>
    /// <value>
    ///     The file path.
    /// </value>
    public string FileName { get; set; }

    internal string Domain { get; set; }

    internal bool IsExtensionModel => RootElement.LocalName == "ExtensionModel";

    void IBinaryXmlElement.Write(BinaryXmlWriter writer)
    {
        TransferCoreProperties(true);
        writer.WriteValue("id", ParseString(id));
        writer.WriteValue("ns", ParseString(ns));
        writer.WriteValue("isroot", IsRoot);
        writer.WriteValue("name", ParseString(name));
        writer.WriteValue("version", ParseString(version));
        writer.WriteValue("compatVersion", ParseString(compatVersion));
        writer.WriteValue("hasUserId", HasUserId);
        writer.WriteValue("author", ParseString(author));
        writer.WriteValue("url", ParseString(url));
        writer.WriteValue("copyright", ParseString(copyright));
        writer.WriteValue("description", ParseString(description));
        writer.WriteValue("category", ParseString(category));
        writer.WriteValue("basePath", basePath);
        writer.WriteValue("sourceAddinFile", AddinFile);
        writer.WriteValue("defaultEnabled", EnabledByDefault);
        writer.WriteValue("domain", Domain);
        writer.WriteValue("MainModule", MainModule);
        writer.WriteValue("OptionalModules", OptionalModules);
        writer.WriteValue("NodeSets", ExtensionNodeSets);
        writer.WriteValue("ExtensionPoints", ExtensionPoints);
        writer.WriteValue("ConditionTypes", ConditionTypes);
        writer.WriteValue("FilesInfo", fileInfo);
        writer.WriteValue("Localizer", Localizer);
        writer.WriteValue("flags", (int)Flags);
        writer.WriteValue("Properties", properties);
    }

    void IBinaryXmlElement.Read(BinaryXmlReader reader)
    {
        id = reader.ReadStringValue("id");
        ns = reader.ReadStringValue("ns");
        IsRoot = reader.ReadBooleanValue("isroot");
        name = reader.ReadStringValue("name");
        version = reader.ReadStringValue("version");
        compatVersion = reader.ReadStringValue("compatVersion");
        HasUserId = reader.ReadBooleanValue("hasUserId");
        author = reader.ReadStringValue("author");
        url = reader.ReadStringValue("url");
        copyright = reader.ReadStringValue("copyright");
        description = reader.ReadStringValue("description");
        category = reader.ReadStringValue("category");
        basePath = reader.ReadStringValue("basePath");
        AddinFile = reader.ReadStringValue("sourceAddinFile");
        EnabledByDefault = reader.ReadBooleanValue("defaultEnabled");
        Domain = reader.ReadStringValue("domain");
        mainModule = (ModuleDescription)reader.ReadValue("MainModule");
        optionalModules = (ModuleCollection)reader.ReadValue("OptionalModules", new ModuleCollection(this));
        nodeSets = (ExtensionNodeSetCollection)reader.ReadValue("NodeSets", new ExtensionNodeSetCollection(this));
        extensionPoints =
            (ExtensionPointCollection)reader.ReadValue("ExtensionPoints", new ExtensionPointCollection(this));
        conditionTypes =
            (ConditionTypeDescriptionCollection)reader.ReadValue("ConditionTypes",
                new ConditionTypeDescriptionCollection(this));
        fileInfo = (object[])reader.ReadValue("FilesInfo", null);
        Localizer = (ExtensionNodeDescription)reader.ReadValue("Localizer");
        Flags = (AddinFlags)reader.ReadInt32Value("flags");
        properties = (AddinPropertyCollectionImpl)reader.ReadValue("Properties", new AddinPropertyCollectionImpl(this));

        if (mainModule != null)
            mainModule.SetParent(this);
    }

    internal void SetBasePath(string path)
    {
        basePath = path;
    }

    internal bool SupportsVersion(string ver)
    {
        return Addin.CompareVersions(ver, Version) >= 0 &&
               (CompatVersion.Length == 0 || Addin.CompareVersions(ver, CompatVersion) <= 0);
    }

    /// <summary>
    ///     Adds an extension point.
    /// </summary>
    /// <returns>
    ///     The extension point.
    /// </returns>
    /// <param name='path'>
    ///     Path that identifies the new extension point.
    /// </param>
    public ExtensionPoint AddExtensionPoint(string path)
    {
        var ep = new ExtensionPoint();
        ep.Path = path;
        ExtensionPoints.Add(ep);
        return ep;
    }

    internal ExtensionNodeDescription FindExtensionNode(string path, bool lookInDeps)
    {
        // Look in the extensions of this add-in

        foreach (Extension ext in MainModule.Extensions)
            if (path.StartsWith(ext.Path + "/"))
            {
                var subp = path.Substring(ext.Path.Length).Trim('/');
                var nodes = ext.ExtensionNodes;
                ExtensionNodeDescription node = null;
                foreach (var p in subp.Split('/'))
                {
                    if (p.Length == 0) continue;
                    node = nodes[p];
                    if (node == null)
                        break;
                    nodes = node.ChildNodes;
                }

                if (node != null)
                    return node;
            }

        if (!lookInDeps || OwnerDatabase == null)
            return null;

        // Look in dependencies

        foreach (Dependency dep in MainModule.Dependencies)
        {
            var adep = dep as AddinDependency;
            if (adep == null) continue;
            var ad = OwnerDatabase.GetInstalledAddin(Domain, adep.FullAddinId);
            if (ad != null && ad.Description != null)
            {
                var node = ad.Description.FindExtensionNode(path, false);
                if (node != null)
                    return node;
            }
        }

        return null;
    }

    internal void ResetXmlDoc()
    {
        configDoc = null;
    }

    internal void StoreFileInfo()
    {
        var allFiles = AllFiles;
        var list = new List<AddinFileInfo>(allFiles.Count);
        foreach (var f in allFiles)
        {
            var file = Path.Combine(BasePath, f);
            var fi = new AddinFileInfo();
            fi.FileName = f;
            fi.Timestamp = File.GetLastWriteTime(file);
            list.Add(fi);
        }

        fileInfo = list.ToArray();
    }

    internal bool FilesChanged()
    {
        // Checks if the files of the add-in have changed.
        if (fileInfo == null)
            return true;

        foreach (AddinFileInfo f in fileInfo)
        {
            var file = Path.Combine(BasePath, f.FileName);
            if (!File.Exists(file))
                return true;
            if (f.Timestamp != File.GetLastWriteTime(file))
                return true;
        }

        return false;
    }

    private void TransferCoreProperties(bool removeProperties)
    {
        if (properties == null)
            return;

        var val = properties.ExtractCoreProperty("Id", removeProperties);
        if (val != null)
            id = val;

        val = properties.ExtractCoreProperty("Namespace", removeProperties);
        if (val != null)
            ns = val;

        val = properties.ExtractCoreProperty("Version", removeProperties);
        if (val != null)
            version = val;

        val = properties.ExtractCoreProperty("CompatVersion", removeProperties);
        if (val != null)
            compatVersion = val;

        val = properties.ExtractCoreProperty("DefaultEnabled", removeProperties);
        if (val != null)
            EnabledByDefault = GetBool(val, true);

        val = properties.ExtractCoreProperty("IsRoot", removeProperties);
        if (val != null)
            IsRoot = GetBool(val, true);

        val = properties.ExtractCoreProperty("Flags", removeProperties);
        if (val != null)
            Flags = (AddinFlags)Enum.Parse(typeof(AddinFlags), val);
    }

    private bool TryGetVariableValue(string name, out string value)
    {
        if (variables != null && variables.TryGetValue(name, out value))
            return true;

        switch (name)
        {
            case "Id":
                value = id;
                return true;
            case "Namespace":
                value = ns;
                return true;
            case "Version":
                value = version;
                return true;
            case "CompatVersion":
                value = compatVersion;
                return true;
            case "DefaultEnabled":
                value = EnabledByDefault.ToString();
                return true;
            case "IsRoot":
                value = IsRoot.ToString();
                return true;
            case "Flags":
                value = Flags.ToString();
                return true;
        }

        if (properties != null && properties.HasProperty(name))
        {
            value = properties.GetPropertyValue(name);
            return true;
        }

        value = null;
        return false;
    }

    /// <summary>
    ///     Saves the add-in description.
    /// </summary>
    /// <param name='fileName'>
    ///     File name where to save this instance
    /// </param>
    /// <remarks>
    ///     Saves the add-in description to the specified file and sets the FileName property.
    /// </remarks>
    public void Save(string fileName)
    {
        FileName = fileName;
        Save();
    }

    /// <summary>
    ///     Saves the add-in description.
    /// </summary>
    /// <exception cref='InvalidOperationException'>
    ///     It is thrown if FileName is not set
    /// </exception>
    /// <remarks>
    ///     The description is saved to the file specified in the FileName property.
    /// </remarks>
    public void Save()
    {
        if (FileName == null)
            throw new InvalidOperationException("File name not specified.");

        SaveXml();

        using (var sw = new StreamWriter(FileName))
        {
            var tw = new XmlTextWriter(sw);
            tw.Formatting = Formatting.Indented;
            configDoc.Save(tw);
        }
    }

    /// <summary>
    ///     Generates an XML representation of the add-in description
    /// </summary>
    /// <returns>
    ///     An XML manifest.
    /// </returns>
    public XmlDocument SaveToXml()
    {
        SaveXml();
        return configDoc;
    }

    private void SaveXml()
    {
        if (!canWrite)
            throw new InvalidOperationException("Can't write incomplete description.");

        XmlElement elem;

        if (configDoc == null)
        {
            configDoc = new XmlDocument();
            configDoc.AppendChild(configDoc.CreateElement("Addin"));
        }

        elem = configDoc.DocumentElement;

        SaveCoreProperty(elem, HasUserId ? id : null, "id", "Id");
        SaveCoreProperty(elem, version, "version", "Version");
        SaveCoreProperty(elem, ns, "namespace", "Namespace");
        SaveCoreProperty(elem, IsRoot ? "true" : null, "isroot", "IsRoot");

        // Name will return the file name when HasUserId=false
        if (!string.IsNullOrEmpty(name))
            elem.SetAttribute("name", name);
        else
            elem.RemoveAttribute("name");

        SaveCoreProperty(elem, compatVersion, "compatVersion", "CompatVersion");
        SaveCoreProperty(elem, EnabledByDefault ? null : "false", "defaultEnabled", "DefaultEnabled");
        SaveCoreProperty(elem, Flags != AddinFlags.None ? Flags.ToString() : null, "flags", "Flags");

        if (author != null && author.Length > 0)
            elem.SetAttribute("author", author);
        else
            elem.RemoveAttribute("author");

        if (url != null && url.Length > 0)
            elem.SetAttribute("url", url);
        else
            elem.RemoveAttribute("url");

        if (copyright != null && copyright.Length > 0)
            elem.SetAttribute("copyright", copyright);
        else
            elem.RemoveAttribute("copyright");

        if (description != null && description.Length > 0)
            elem.SetAttribute("description", description);
        else
            elem.RemoveAttribute("description");

        if (category != null && category.Length > 0)
            elem.SetAttribute("category", category);
        else
            elem.RemoveAttribute("category");

        if (Localizer == null || Localizer.Element == null)
        {
            // Remove old element if it exists
            var oldLoc = (XmlElement)elem.SelectSingleNode("Localizer");
            if (oldLoc != null)
                elem.RemoveChild(oldLoc);
        }

        if (Localizer != null)
            Localizer.SaveXml(elem);

        if (mainModule != null)
        {
            mainModule.Element = elem;
            mainModule.SaveXml(elem);
        }

        if (optionalModules != null)
            optionalModules.SaveXml(elem);

        if (nodeSets != null)
            nodeSets.SaveXml(elem);

        if (extensionPoints != null)
            extensionPoints.SaveXml(elem);

        var oldHeader = (XmlElement)elem.SelectSingleNode("Header");
        if (properties == null || properties.Count == 0)
        {
            if (oldHeader != null)
                elem.RemoveChild(oldHeader);
        }
        else
        {
            if (oldHeader == null)
            {
                oldHeader = elem.OwnerDocument.CreateElement("Header");
                if (elem.FirstChild != null)
                    elem.InsertBefore(oldHeader, elem.FirstChild);
                else
                    elem.AppendChild(oldHeader);
            }
            else
            {
                oldHeader.RemoveAll();
            }

            foreach (var prop in properties)
            {
                var propElem = elem.OwnerDocument.CreateElement(prop.Name);
                if (!string.IsNullOrEmpty(prop.Locale))
                    propElem.SetAttribute("locale", prop.Locale);
                propElem.InnerText = prop.Value ?? string.Empty;
                oldHeader.AppendChild(propElem);
            }
        }

        var oldVars = (XmlElement)elem.SelectSingleNode("Variables");
        if (variables == null || variables.Count == 0)
        {
            if (oldVars != null)
                elem.RemoveChild(oldVars);
        }
        else
        {
            if (oldVars == null)
            {
                oldVars = elem.OwnerDocument.CreateElement("Variables");
                if (elem.FirstChild != null)
                    elem.InsertBefore(oldVars, elem.FirstChild);
                else
                    elem.AppendChild(oldVars);
            }
            else
            {
                oldVars.RemoveAll();
            }

            foreach (var prop in variables)
            {
                var propElem = elem.OwnerDocument.CreateElement(prop.Key);
                propElem.InnerText = prop.Value ?? string.Empty;
                oldVars.AppendChild(propElem);
            }
        }
    }

    public XmlDocument SaveToVsixXml()
    {
        if (!canWrite)
            throw new InvalidOperationException("Can't write incomplete description.");

        XmlElement packageManifestEl;

        var vsixDoc = new XmlDocument();
        vsixDoc.AppendChild(vsixDoc.CreateElement("PackageManifest"));

        packageManifestEl = vsixDoc.DocumentElement;
        packageManifestEl.SetAttribute("Version", "2.0.0");
        packageManifestEl.SetAttribute("xmlns", "http://schemas.microsoft.com/developer/vsx-schema/2011");

        var metadata = vsixDoc.CreateElement("Metadata");
        var identity = vsixDoc.CreateElement("Identity");
        identity.SetAttribute("Language", "en-US");
        identity.SetAttribute("Id", LocalId);
        identity.SetAttribute("Version", Version);
        identity.SetAttribute("Publisher", Properties.GetPropertyValue("VisualStudio.Publisher"));
        metadata.AppendChild(identity);

        var displayNameEl = vsixDoc.CreateElement("DisplayName");
        displayNameEl.InnerText = Name;
        metadata.AppendChild(displayNameEl);

        var descriptionEl = vsixDoc.CreateElement("Description");
        descriptionEl.SetAttribute("xml:space", "preserve");
        descriptionEl.InnerText = Description;
        metadata.AppendChild(descriptionEl);

        var moreInfoEl = vsixDoc.CreateElement("MoreInfo");
        moreInfoEl.InnerText = Url;
        metadata.AppendChild(moreInfoEl);

        var tagsEl = vsixDoc.CreateElement("Tags");
        if (!string.IsNullOrEmpty(Properties.GetPropertyValue("VisualStudio.Tags")))
            tagsEl.InnerText = Properties.GetPropertyValue("VisualStudio.Tags");
        metadata.AppendChild(tagsEl);

        var categoriesEl = vsixDoc.CreateElement("Categories");
        categoriesEl.InnerText = Category;
        metadata.AppendChild(categoriesEl);

        var galleryFlagsEl = vsixDoc.CreateElement("GalleryFlags");
        var galleryFlags = Properties.GetPropertyValue("VisualStudio.GalleryFlags");
        if (string.IsNullOrEmpty(galleryFlags))
            galleryFlags = "Public";
        galleryFlagsEl.InnerText = galleryFlags;
        metadata.AppendChild(galleryFlagsEl);

        var badgesEl = vsixDoc.CreateElement("Badges");
        //TODO:Add Badges support
        metadata.AppendChild(badgesEl);

        var icon = Properties.GetPropertyValue("Icon32");
        if (!string.IsNullOrEmpty(icon))
        {
            var iconEl = vsixDoc.CreateElement("Icon");
            iconEl.InnerText = icon;
            metadata.AppendChild(iconEl);
        }

        var license = Copyright;
        if (!string.IsNullOrEmpty(license))
        {
            var licenseEl = vsixDoc.CreateElement("License");
            licenseEl.InnerText = license;
            metadata.AppendChild(licenseEl);
        }

        packageManifestEl.AppendChild(metadata);

        var installationEl = vsixDoc.CreateElement("Installation");
        var installationTargetEl = vsixDoc.CreateElement("InstallationTarget");
        installationTargetEl.SetAttribute("Id", "Microsoft.VisualStudio.Mac");
        installationEl.AppendChild(installationTargetEl);
        packageManifestEl.AppendChild(installationEl);

        packageManifestEl.AppendChild(vsixDoc.CreateElement("Dependencies"));

        var assetsEl = vsixDoc.CreateElement("Assets");
        var addinInfoAsset = vsixDoc.CreateElement("Asset");
        addinInfoAsset.SetAttribute("Type", "Microsoft.VisualStudio.Mac.AddinInfo");
        addinInfoAsset.SetAttribute("Path", "addin.info");
        addinInfoAsset.SetAttribute("Addressable", "true");
        assetsEl.AppendChild(addinInfoAsset);

        if (!string.IsNullOrEmpty(icon))
        {
            var iconAsset = vsixDoc.CreateElement("Asset");
            iconAsset.SetAttribute("Type", "Microsoft.VisualStudio.Services.Icons.Default");
            iconAsset.SetAttribute("Path", icon);
            iconAsset.SetAttribute("Addressable", "true");
            assetsEl.AppendChild(iconAsset);
        }

        var propertyToAssetTypeMappings = new Dictionary<string, string>
        {
            { "VisualStudio.License", "Microsoft.VisualStudio.Services.Content.License" },
            { "VisualStudio.Details", "Microsoft.VisualStudio.Services.Content.Details" },
            { "VisualStudio.Changelog", "Microsoft.VisualStudio.Services.Content.Changelog" }
        };

        foreach (var mapping in propertyToAssetTypeMappings)
            if (!string.IsNullOrEmpty(Properties.GetPropertyValue(mapping.Key)))
            {
                var asset = vsixDoc.CreateElement("Asset");
                asset.SetAttribute("Type", mapping.Value);
                asset.SetAttribute("Path", icon);
                asset.SetAttribute("Addressable", "true");
                assetsEl.AppendChild(asset);
            }

        packageManifestEl.AppendChild(assetsEl);

        return vsixDoc;
    }

    private void SaveCoreProperty(XmlElement elem, string val, string attr, string prop)
    {
        if (properties != null && properties.HasProperty(prop))
        {
            elem.RemoveAttribute(attr);
            if (!string.IsNullOrEmpty(val))
                properties.SetPropertyValue(prop, val);
            else
                properties.RemoveProperty(prop);
        }
        else if (string.IsNullOrEmpty(val))
        {
            elem.RemoveAttribute(attr);
        }
        else
        {
            elem.SetAttribute(attr, val);
        }
    }


    /// <summary>
    ///     Load an add-in description from a file
    /// </summary>
    /// <param name='configFile'>
    ///     The file.
    /// </param>
    public static AddinDescription Read(string configFile)
    {
        AddinDescription config;
        using (Stream s = File.OpenRead(configFile))
        {
            config = Read(s, Path.GetDirectoryName(configFile));
        }

        config.FileName = configFile;
        return config;
    }

    /// <summary>
    ///     Load an add-in description from a stream
    /// </summary>
    /// <param name='stream'>
    ///     The stream
    /// </param>
    /// <param name='basePath'>
    ///     The path to be used to resolve relative file paths.
    /// </param>
    public static AddinDescription Read(Stream stream, string basePath)
    {
        return Read(new StreamReader(stream), basePath);
    }

    /// <summary>
    ///     Load an add-in description from a text reader
    /// </summary>
    /// <param name='reader'>
    ///     The text reader
    /// </param>
    /// <param name='basePath'>
    ///     The path to be used to resolve relative file paths.
    /// </param>
    public static AddinDescription Read(TextReader reader, string basePath)
    {
        var config = new AddinDescription();

        try
        {
            config.configDoc = new XmlDocument();
            config.configDoc.Load(reader);
        }
        catch (Exception ex)
        {
            throw new InvalidOperationException("The add-in configuration file is invalid: " + ex.Message, ex);
        }

        var elem = config.configDoc.DocumentElement;
        if (elem.LocalName == "ExtensionModel")
            return config;

        var varsElem = (XmlElement)elem.SelectSingleNode("Variables");
        if (varsElem != null)
            foreach (XmlNode node in varsElem.ChildNodes)
            {
                var prop = node as XmlElement;
                if (prop == null)
                    continue;
                if (config.variables == null)
                    config.variables = new Dictionary<string, string>();
                config.variables[prop.LocalName] = prop.InnerText;
            }

        config.id = elem.GetAttribute("id");
        config.ns = elem.GetAttribute("namespace");
        config.name = elem.GetAttribute("name");
        config.version = elem.GetAttribute("version");
        config.compatVersion = elem.GetAttribute("compatVersion");
        config.author = elem.GetAttribute("author");
        config.url = elem.GetAttribute("url");
        config.copyright = elem.GetAttribute("copyright");
        config.description = elem.GetAttribute("description");
        config.category = elem.GetAttribute("category");
        config.basePath = elem.GetAttribute("basePath");
        config.Domain = "global";

        var s = elem.GetAttribute("isRoot");
        if (s.Length == 0) s = elem.GetAttribute("isroot");
        config.IsRoot = GetBool(s, false);

        config.EnabledByDefault = GetBool(elem.GetAttribute("defaultEnabled"), true);

        var prot = elem.GetAttribute("flags");
        if (prot.Length == 0)
            config.Flags = AddinFlags.None;
        else
            config.Flags = (AddinFlags)Enum.Parse(typeof(AddinFlags), prot);

        var localizerElem = (XmlElement)elem.SelectSingleNode("Localizer");
        if (localizerElem != null)
            config.Localizer = new ExtensionNodeDescription(localizerElem);

        var headerElem = (XmlElement)elem.SelectSingleNode("Header");
        if (headerElem != null)
            foreach (XmlNode node in headerElem.ChildNodes)
            {
                var prop = node as XmlElement;
                if (prop == null)
                    continue;
                config.Properties.SetPropertyValue(prop.LocalName, prop.InnerText, prop.GetAttribute("locale"));
            }

        config.TransferCoreProperties(false);

        if (config.id.Length > 0)
            config.HasUserId = true;

        return config;
    }

    internal string ParseString(string input)
    {
        if (input == null || input.Length < 4)
            return input;

        var i = input.IndexOf("$(");
        if (i == -1)
            return input;

        var result = new StringBuilder(input.Length);
        result.Append(input, 0, i);

        while (i < input.Length)
        {
            if (input[i] == '$')
            {
                i++;

                if (i >= input.Length || input[i] != '(')
                {
                    result.Append('$');
                    continue;
                }

                i++;
                var start = i;
                while (i < input.Length && input[i] != ')')
                    i++;

                var tag = input.Substring(start, i - start);

                string tagValue;
                if (TryGetVariableValue(tag, out tagValue))
                {
                    result.Append(tagValue);
                }
                else
                {
                    result.Append('$');
                    i = start - 1;
                }
            }
            else
            {
                result.Append(input[i]);
            }

            i++;
        }

        return result.ToString();
    }

    private static bool GetBool(string s, bool defval)
    {
        if (s.Length == 0)
            return defval;
        return s == "true" || s == "yes";
    }

    internal static AddinDescription ReadBinary(FileDatabase fdb, string configFile)
    {
        var description = (AddinDescription)fdb.ReadSharedObject(configFile, typeMap);
        if (description != null)
        {
            description.FileName = configFile;
            description.canWrite = !fdb.IgnoreDescriptionData;
        }

        return description;
    }

    internal void SaveBinary(FileDatabase fdb, string file)
    {
        FileName = file;
        SaveBinary(fdb);
    }

    internal void SaveBinary(FileDatabase fdb)
    {
        if (!canWrite)
            throw new InvalidOperationException("Can't write incomplete description.");
        fdb.WriteSharedObject(AddinFile, FileName, typeMap, this);
//			BinaryXmlReader.DumpFile (configFile);
    }

    /// <summary>
    ///     Verify this instance.
    /// </summary>
    /// <remarks>
    ///     This method checks all the definitions in the description and returns a list of errors.
    ///     If the returned list is empty, it means that the description is valid.
    /// </remarks>
    public StringCollection Verify()
    {
        return Verify(new AddinFileSystemExtension());
    }

    internal StringCollection Verify(AddinFileSystemExtension fs)
    {
        var errors = new StringCollection();

        if (IsRoot)
            if (OptionalModules.Count > 0)
                errors.Add("Root add-in hosts can't have optional modules.");

        if (AddinId.Length == 0 || Version.Length == 0)
            if (ExtensionPoints.Count > 0)
                errors.Add("Add-ins which define new extension points must have an Id and Version.");

        MainModule.Verify("", errors);
        OptionalModules.Verify("", errors);
        ExtensionNodeSets.Verify("", errors);
        ExtensionPoints.Verify("", errors);
        ConditionTypes.Verify("", errors);

        foreach (ExtensionNodeSet nset in ExtensionNodeSets)
            if (nset.Id.Length == 0)
                errors.Add("Attribute 'id' can't be empty for global node sets.");

        string bp = null;
        if (BasePath.Length > 0)
            bp = BasePath;
        else if (AddinFile != null && AddinFile.Length > 0)
            bp = Path.GetDirectoryName(AddinFile);
        else if (FileName != null && FileName.Length > 0)
            bp = Path.GetDirectoryName(FileName);

        if (bp != null)
            foreach (var file in AllFiles)
            {
                var asmFile = Path.Combine(bp, Util.NormalizePath(file));
                if (!fs.FileExists(asmFile))
                    errors.Add("The file '" + asmFile + "' referenced in the manifest could not be found.");
            }

        if (Localizer != null && Localizer.GetAttribute("type").Length == 0)
            errors.Add("The attribute 'type' in the Location element is required.");

        // Ensure that there are no duplicated properties

        if (properties != null)
        {
            var props = new HashSet<string>();
            foreach (var prop in properties)
                if (!props.Add(prop.Name + " " + prop.Locale))
                    errors.Add(string.Format("Property {0} specified more than once",
                        prop.Name + (prop.Locale != null ? " (" + prop.Locale + ")" : "")));
        }

        return errors;
    }

    internal void SetExtensionsAddinId(string addinId)
    {
        foreach (ExtensionPoint ep in ExtensionPoints)
            ep.SetExtensionsAddinId(addinId);

        foreach (ExtensionNodeSet ns in ExtensionNodeSets)
            ns.SetExtensionsAddinId(addinId);
    }

    internal void UnmergeExternalData(Hashtable addins)
    {
        // Removes extension types and extension sets coming from other add-ins.
        foreach (ExtensionPoint ep in ExtensionPoints)
            ep.UnmergeExternalData(AddinId, addins);

        foreach (ExtensionNodeSet ns in ExtensionNodeSets)
            ns.UnmergeExternalData(AddinId, addins);
    }

    internal void MergeExternalData(AddinDescription other)
    {
        // Removes extension types and extension sets coming from other add-ins.
        foreach (ExtensionPoint ep in other.ExtensionPoints)
        {
            var tep = ExtensionPoints[ep.Path];
            if (tep != null)
                tep.MergeWith(AddinId, ep);
        }

        foreach (ExtensionNodeSet ns in other.ExtensionNodeSets)
        {
            var tns = ExtensionNodeSets[ns.Id];
            if (tns != null)
                tns.MergeWith(AddinId, ns);
        }
    }

    internal static AddinDescription Merge(AddinDescription desc1, AddinDescription desc2)
    {
        if (!desc2.IsExtensionModel)
        {
            var tmp = desc1;
            desc1 = desc2;
            desc2 = tmp;
        }

        ((AddinPropertyCollectionImpl)desc1.Properties).AddRange(desc2.Properties);
        desc1.ExtensionPoints.AddRange(desc2.ExtensionPoints);
        desc1.ExtensionNodeSets.AddRange(desc2.ExtensionNodeSets);
        desc1.ConditionTypes.AddRange(desc2.ConditionTypes);
        desc1.OptionalModules.AddRange(desc2.OptionalModules);
        foreach (var s in desc2.MainModule.Assemblies)
            desc1.MainModule.Assemblies.Add(s);
        foreach (var s in desc2.MainModule.DataFiles)
            desc1.MainModule.DataFiles.Add(s);
        desc1.MainModule.MergeWith(desc2.MainModule);
        return desc1;
    }
}

internal class AddinFileInfo : IBinaryXmlElement
{
    public string FileName { get; set; }

    public DateTime Timestamp { get; set; }

    public void Read(BinaryXmlReader reader)
    {
        FileName = reader.ReadStringValue("fileName");
        Timestamp = reader.ReadDateTimeValue("timestamp");
    }

    public void Write(BinaryXmlWriter writer)
    {
        writer.WriteValue("fileName", FileName);
        writer.WriteValue("timestamp", Timestamp);
    }
}