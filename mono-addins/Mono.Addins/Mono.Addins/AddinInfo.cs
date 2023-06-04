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

using System.Collections.Immutable;
using System.Linq;
using Mono.Addins.Description;

namespace Mono.Addins;

internal class AddinInfo
{
    private string author = "";
    private string category = "";
    private string copyright = "";
    private string description = "";
    private string name = "";
    private string url = "";

    private AddinInfo()
    {
        Dependencies = ImmutableArray<Dependency>.Empty;
        OptionalDependencies = ImmutableArray<Dependency>.Empty;
    }

    public string Id => Addin.GetFullId(Namespace, LocalId, Version);

    public string LocalId { get; private set; } = "";

    public string Namespace { get; private set; } = "";

    public bool IsRoot { get; private set; }

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
    }

    public string Version { get; private set; } = "";

    public string BaseVersion { get; private set; } = "";

    public string Author
    {
        get
        {
            var s = Properties.GetPropertyValue("Author");
            if (s.Length > 0)
                return s;
            return author;
        }
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
    }

    public bool EnabledByDefault { get; private set; } = true;

    public ImmutableArray<Dependency> Dependencies { get; private set; }

    public ImmutableArray<Dependency> OptionalDependencies { get; private set; }

    public AddinPropertyCollection Properties { get; private set; }

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
        info.IsRoot = description.IsRoot;
        info.EnabledByDefault = description.EnabledByDefault;

        info.Dependencies = ImmutableArray<Dependency>.Empty.AddRange(description.MainModule.Dependencies);
        info.OptionalDependencies =
            ImmutableArray<Dependency>.Empty.AddRange(
                description.OptionalModules.SelectMany(module => module.Dependencies));
        info.Properties = description.Properties;

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

    public int CompareVersionTo(AddinInfo other)
    {
        return Addin.CompareVersions(Version, other.Version);
    }
}