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

public sealed class AssemblyLinkedResource : Resource
{
    public AssemblyLinkedResource(string name, ManifestResourceAttributes flags)
        : base(name, flags)
    {
    }

    public AssemblyLinkedResource(string name, ManifestResourceAttributes flags, AssemblyNameReference reference)
        : base(name, flags)
    {
        this.Assembly = reference;
    }

    public AssemblyNameReference Assembly { get; set; }

    public override ResourceType ResourceType => ResourceType.AssemblyLinked;
}