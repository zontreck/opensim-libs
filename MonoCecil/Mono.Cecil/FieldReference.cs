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

public class FieldReference : MemberReference
{
    internal FieldReference()
    {
        token = new MetadataToken(TokenType.MemberRef);
    }

    public FieldReference(string name, TypeReference fieldType)
        : base(name)
    {
        Mixin.CheckType(fieldType, Mixin.Argument.fieldType);

        FieldType = fieldType;
        token = new MetadataToken(TokenType.MemberRef);
    }

    public FieldReference(string name, TypeReference fieldType, TypeReference declaringType)
        : this(name, fieldType)
    {
        Mixin.CheckType(declaringType, Mixin.Argument.declaringType);

        DeclaringType = declaringType;
    }

    public TypeReference FieldType { get; set; }

    public override string FullName => FieldType.FullName + " " + MemberFullName();

    public override bool ContainsGenericParameter =>
        FieldType.ContainsGenericParameter || base.ContainsGenericParameter;

    protected override IMemberDefinition ResolveDefinition()
    {
        return Resolve();
    }

    public new virtual FieldDefinition Resolve()
    {
        var module = Module;
        if (module == null)
            throw new NotSupportedException();

        return module.Resolve(this);
    }
}