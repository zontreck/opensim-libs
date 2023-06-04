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
using System.Text;
using System.Threading;
using Mono.Collections.Generic;
using MD = Mono.Cecil.Metadata;

namespace Mono.Cecil;

public sealed class GenericInstanceType : TypeSpecification, IGenericInstance, IGenericContext
{
    private Collection<TypeReference> arguments;

    public GenericInstanceType(TypeReference type)
        : base(type)
    {
        IsValueType = type.IsValueType;
        etype = MD.ElementType.GenericInst;
    }

    internal GenericInstanceType(TypeReference type, int arity)
        : this(type)
    {
        arguments = new Collection<TypeReference>(arity);
    }

    public override TypeReference DeclaringType
    {
        get => ElementType.DeclaringType;
        set => throw new NotSupportedException();
    }

    public override string FullName
    {
        get
        {
            var name = new StringBuilder();
            name.Append(base.FullName);
            this.GenericInstanceFullName(name);
            return name.ToString();
        }
    }

    public override bool IsGenericInstance => true;

    public override bool ContainsGenericParameter => this.ContainsGenericParameter() || base.ContainsGenericParameter;

    IGenericParameterProvider IGenericContext.Type => ElementType;

    public bool HasGenericArguments => !arguments.IsNullOrEmpty();

    public Collection<TypeReference> GenericArguments
    {
        get
        {
            if (arguments == null)
                Interlocked.CompareExchange(ref arguments, new Collection<TypeReference>(), null);

            return arguments;
        }
    }
}