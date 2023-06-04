//
// AddinInfo.cs
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

using System.IO;
using System.Xml;
using System.Xml.Serialization;
using Mono.Addins.Description;

namespace Mono.Addins.Setup;

public class AddinInfo : AddinHeader
{
    private string author = "";
    private string category = "";
    private string copyright = "";
    private string description = "";
    private string name = "";
    private string url = "";

    public AddinInfo()
    {
        Dependencies = new DependencyCollection();
        OptionalDependencies = new DependencyCollection();
        Properties = new AddinPropertyCollectionImpl();
    }

    [XmlElement("Id")] public string LocalId { get; set; } = "";

    [XmlArrayItem("Property", typeof(AddinProperty))]
    public AddinPropertyCollectionImpl Properties { get; private set; }

    public string Id => Addin.GetFullId(Namespace, LocalId, Version);

    public string Namespace { get; set; } = "";

    public string Name
    {
        get
        {
            var s = Properties.GetPropertyValue("Name");
            if (s.Length > 0)
                return s;
            if (name != null && name.Length > 0)
                return name;
            var sid = LocalId;
            if (sid.StartsWith("__"))
                sid = sid.Substring(2);
            return Addin.GetFullId(Namespace, sid, null);
        }
        set => name = value;
    }

    public string Version { get; set; } = "";

    public string BaseVersion { get; set; } = "";

    public string Author
    {
        get
        {
            var s = Properties.GetPropertyValue("Author");
            if (s.Length > 0)
                return s;
            return author;
        }
        set => author = value;
    }

    public string Copyright
    {
        get
        {
            var s = Properties.GetPropertyValue("Copyright");
            if (s.Length > 0)
                return s;
            return copyright;
        }
        set => copyright = value;
    }

    public string Url
    {
        get
        {
            var s = Properties.GetPropertyValue("Url");
            if (s.Length > 0)
                return s;
            return url;
        }
        set => url = value;
    }

    public string Description
    {
        get
        {
            var s = Properties.GetPropertyValue("Description");
            if (s.Length > 0)
                return s;
            return description;
        }
        set => description = value;
    }

    public string Category
    {
        get
        {
            var s = Properties.GetPropertyValue("Category");
            if (s.Length > 0)
                return s;
            return category;
        }
        set => category = value;
    }

    AddinPropertyCollection AddinHeader.Properties => Properties;

    public int CompareVersionTo(AddinHeader other)
    {
        return Addin.CompareVersions(Version, other.Version);
    }

    public static AddinInfo ReadFromAddinFile(StreamReader r)
    {
        var doc = new XmlDocument();
        doc.Load(r);
        r.Close();

        var info = new AddinInfo();
        info.LocalId = doc.DocumentElement.GetAttribute("id");
        info.Namespace = doc.DocumentElement.GetAttribute("namespace");
        info.name = doc.DocumentElement.GetAttribute("name");
        if (info.LocalId == "") info.LocalId = info.name;
        info.Version = doc.DocumentElement.GetAttribute("version");
        info.author = doc.DocumentElement.GetAttribute("author");
        info.copyright = doc.DocumentElement.GetAttribute("copyright");
        info.url = doc.DocumentElement.GetAttribute("url");
        info.description = doc.DocumentElement.GetAttribute("description");
        info.category = doc.DocumentElement.GetAttribute("category");
        info.BaseVersion = doc.DocumentElement.GetAttribute("compatVersion");
        var props = new AddinPropertyCollectionImpl();
        info.Properties = props;
        ReadHeader(info, props, doc.DocumentElement);
        ReadDependencies(info.Dependencies, info.OptionalDependencies, doc.DocumentElement);
        return info;
    }

    private static void ReadDependencies(DependencyCollection deps, DependencyCollection opDeps, XmlElement elem)
    {
        foreach (XmlElement dep in elem.SelectNodes("Dependencies/Addin"))
        {
            var adep = new AddinDependency();
            adep.AddinId = dep.GetAttribute("id");
            var v = dep.GetAttribute("version");
            if (v.Length != 0)
                adep.Version = v;
            deps.Add(adep);
        }

        foreach (XmlElement dep in elem.SelectNodes("Dependencies/Assembly"))
        {
            var adep = new AssemblyDependency();
            adep.FullName = dep.GetAttribute("name");
            adep.Package = dep.GetAttribute("package");
            deps.Add(adep);
        }

        foreach (XmlElement mod in elem.SelectNodes("Module"))
            ReadDependencies(opDeps, opDeps, mod);
    }

    private static void ReadHeader(AddinInfo info, AddinPropertyCollectionImpl properties, XmlElement elem)
    {
        elem = elem.SelectSingleNode("Header") as XmlElement;
        if (elem == null)
            return;
        foreach (XmlNode xprop in elem.ChildNodes)
        {
            var prop = xprop as XmlElement;
            if (prop != null)
                switch (prop.LocalName)
                {
                    case "Id":
                        info.LocalId = prop.InnerText;
                        break;
                    case "Namespace":
                        info.Namespace = prop.InnerText;
                        break;
                    case "Version":
                        info.Version = prop.InnerText;
                        break;
                    case "CompatVersion":
                        info.BaseVersion = prop.InnerText;
                        break;
                    default:
                    {
                        var aprop = new AddinProperty();
                        aprop.Name = prop.LocalName;
                        if (prop.HasAttribute("locale"))
                            aprop.Locale = prop.GetAttribute("locale");
                        aprop.Value = prop.InnerText;
                        properties.Add(aprop);
                        break;
                    }
                }
        }
    }

    internal static AddinInfo ReadFromDescription(AddinDescription description)
    {
        var info = new AddinInfo();
        info.LocalId = description.LocalId;
        info.Namespace = description.Namespace;
        info.name = description.Name;
        info.Version = description.Version;
        info.author = description.Author;
        info.copyright = description.Copyright;
        info.url = description.Url;
        info.description = description.Description;
        info.category = description.Category;
        info.BaseVersion = description.CompatVersion;
        info.Properties = new AddinPropertyCollectionImpl(description.Properties);

        foreach (Dependency dep in description.MainModule.Dependencies)
            info.Dependencies.Add(dep);

        foreach (ModuleDescription mod in description.OptionalModules)
        foreach (Dependency dep in mod.Dependencies)
            info.OptionalDependencies.Add(dep);
        return info;
    }

    public bool SupportsVersion(string version)
    {
        if (Addin.CompareVersions(Version, version) > 0)
            return false;
        if (BaseVersion == "")
            return true;
        return Addin.CompareVersions(BaseVersion, version) >= 0;
    }

#pragma warning disable CS0612 // Type or member is obsolete

    [XmlArrayItem("AddinDependency", typeof(AddinDependency))]
    [XmlArrayItem("NativeDependency", typeof(NativeDependency))]
    [XmlArrayItem("AssemblyDependency", typeof(AssemblyDependency))]
    public DependencyCollection Dependencies { get; }

    [XmlArrayItem("AddinDependency", typeof(AddinDependency))]
    [XmlArrayItem("NativeDependency", typeof(NativeDependency))]
    [XmlArrayItem("AssemblyDependency", typeof(AssemblyDependency))]
    public DependencyCollection OptionalDependencies { get; }

#pragma warning restore CS0612 // Type or member is obsolete
}

/// <summary>
///     Basic add-in information
/// </summary>
public interface AddinHeader
{
	/// <summary>
	///     Full identifier of the add-in
	/// </summary>
	string Id { get; }

	/// <summary>
	///     Display name of the add-in
	/// </summary>
	string Name { get; }

	/// <summary>
	///     Namespace of the add-in
	/// </summary>
	string Namespace { get; }

	/// <summary>
	///     Version of the add-in
	/// </summary>
	string Version { get; }

	/// <summary>
	///     Version with which this add-in is compatible
	/// </summary>
	string BaseVersion { get; }

	/// <summary>
	///     Add-in author
	/// </summary>
	string Author { get; }

	/// <summary>
	///     Add-in copyright
	/// </summary>
	string Copyright { get; }

	/// <summary>
	///     Web page URL with more information about the add-in
	/// </summary>
	string Url { get; }

	/// <summary>
	///     Description of the add-in
	/// </summary>
	string Description { get; }

	/// <summary>
	///     Category of the add-in
	/// </summary>
	string Category { get; }

	/// <summary>
	///     Dependencies of the add-in
	/// </summary>
	DependencyCollection Dependencies { get; }

	/// <summary>
	///     Optional dependencies of the add-in
	/// </summary>
	DependencyCollection OptionalDependencies { get; }

	/// <summary>
	///     Custom properties specified in the add-in header
	/// </summary>
	AddinPropertyCollection Properties { get; }

	/// <summary>
	///     Compares the versions of two add-ins
	/// </summary>
	/// <param name="other">
	///     Another add-in
	/// </param>
	/// <returns>
	///     Result of comparison
	/// </returns>
	int CompareVersionTo(AddinHeader other);
}