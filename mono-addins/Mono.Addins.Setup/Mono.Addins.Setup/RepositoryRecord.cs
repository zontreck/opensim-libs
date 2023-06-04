//
// RepositoryRecord.cs
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
using System.ComponentModel;
using System.IO;
using System.Xml.Serialization;

namespace Mono.Addins.Setup;

internal class RepositoryRecord : AddinRepository
{
    [XmlAttribute("id")] public string Id { get; set; }

    public bool IsReference { get; set; }

    public string CachedFilesDir =>
        Path.Combine(Path.GetDirectoryName(File), Path.GetFileNameWithoutExtension(File) + "_files");

    public string ProviderId { get; set; }

    public string File { get; set; }

    public string Url { get; set; }

    public string Name { get; set; }

    public string Title => Name != null && Name != "" ? Name : Url;

    public DateTime LastModified { get; set; } = new(1900, 1, 1);

    [DefaultValue(true)] public bool Enabled { get; set; } = true;

    public Repository GetCachedRepository()
    {
        var repo = (Repository)AddinStore.ReadObject(File, typeof(Repository));
        if (repo != null)
            repo.CachedFilesDir = CachedFilesDir;
        return repo;
    }

    public void ClearCachedRepository()
    {
        if (System.IO.File.Exists(File))
            System.IO.File.Delete(File);
        if (Directory.Exists(CachedFilesDir))
            Directory.Delete(CachedFilesDir, true);
    }

    internal void UpdateCachedRepository(Repository newRep)
    {
        newRep.url = Url;
        if (newRep.Name == null)
            newRep.Name = new Uri(Url).Host;
        AddinStore.WriteObject(File, newRep);
        if (Name == null)
            Name = newRep.Name;
        newRep.CachedFilesDir = CachedFilesDir;
    }
}

/// <summary>
///     An on-line add-in repository
/// </summary>
public interface AddinRepository
{
	/// <summary>
	///     Path to the cached add-in repository file
	/// </summary>
	string File { get; }

	/// <summary>
	///     Url of the repository
	/// </summary>
	string Url { get; }

	/// <summary>
	///     Do not use. Use Title instead.
	/// </summary>
	string Name { get; set; }

	/// <summary>
	///     Title of the repository
	/// </summary>
	string Title { get; }

	/// <summary>
	///     Last change timestamp
	/// </summary>
	DateTime LastModified { get; }

	/// <summary>
	///     Gets a value indicating whether this <see cref="Mono.Addins.Setup.AddinRepository" /> is enabled.
	/// </summary>
	/// <value>
	///     <c>true</c> if enabled; otherwise, <c>false</c>.
	/// </value>
	bool Enabled { get; }

	/// <summary>
	///     Defineds type of repository provider.
	/// </summary>
	/// <value>Provider string id.</value>
	string ProviderId { get; }
}