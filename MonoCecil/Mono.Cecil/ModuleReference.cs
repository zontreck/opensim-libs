//
// Author:
//   Jb Evain (jbevain@gmail.com)
//
// Copyright (c) 2008 - 2015 Jb Evain
// Copyright (c) 2008 - 2011 Novell, Inc.
//
// Licensed under the MIT/X11 license.
//

namespace Mono.Cecil;

public class ModuleReference : IMetadataScope
{
    internal MetadataToken token;

    internal ModuleReference()
    {
        token = new MetadataToken(TokenType.ModuleRef);
    }

    public ModuleReference(string name)
        : this()
    {
        this.Name = name;
    }

    public string Name { get; set; }

    public virtual MetadataScopeType MetadataScopeType => MetadataScopeType.ModuleReference;

    public MetadataToken MetadataToken
    {
        get => token;
        set => token = value;
    }

    public override string ToString()
    {
        return Name;
    }
}