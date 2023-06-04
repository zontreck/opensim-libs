//
// Author:
//   Jb Evain (jbevain@gmail.com)
//
// Copyright (c) 2008 - 2015 Jb Evain
// Copyright (c) 2008 - 2011 Novell, Inc.
//
// Licensed under the MIT/X11 license.
//

using System;

namespace Mono.Cecil;

public abstract class TypeSpecification : TypeReference
{
    internal TypeSpecification(TypeReference type)
        : base(null, null)
    {
        ElementType = type;
        token = new MetadataToken(TokenType.TypeSpec);
    }

    public TypeReference ElementType { get; }

    public override string Name
    {
        get => ElementType.Name;
        set => throw new InvalidOperationException();
    }

    public override string Namespace
    {
        get => ElementType.Namespace;
        set => throw new InvalidOperationException();
    }

    public override IMetadataScope Scope
    {
        get => ElementType.Scope;
        set => throw new InvalidOperationException();
    }

    public override ModuleDefinition Module => ElementType.Module;

    public override string FullName => ElementType.FullName;

    public override bool ContainsGenericParameter => ElementType.ContainsGenericParameter;

    public override MetadataType MetadataType => (MetadataType)etype;

    public override TypeReference GetElementType()
    {
        return ElementType.GetElementType();
    }
}