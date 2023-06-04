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
using Mono.Collections.Generic;

namespace Mono.Cecil;

public abstract class MethodSpecification : MethodReference
{
    internal MethodSpecification(MethodReference method)
    {
        Mixin.CheckMethod(method);

        this.ElementMethod = method;
        token = new MetadataToken(TokenType.MethodSpec);
    }

    public MethodReference ElementMethod { get; }

    public override string Name
    {
        get => ElementMethod.Name;
        set => throw new InvalidOperationException();
    }

    public override MethodCallingConvention CallingConvention
    {
        get => ElementMethod.CallingConvention;
        set => throw new InvalidOperationException();
    }

    public override bool HasThis
    {
        get => ElementMethod.HasThis;
        set => throw new InvalidOperationException();
    }

    public override bool ExplicitThis
    {
        get => ElementMethod.ExplicitThis;
        set => throw new InvalidOperationException();
    }

    public override MethodReturnType MethodReturnType
    {
        get => ElementMethod.MethodReturnType;
        set => throw new InvalidOperationException();
    }

    public override TypeReference DeclaringType
    {
        get => ElementMethod.DeclaringType;
        set => throw new InvalidOperationException();
    }

    public override ModuleDefinition Module => ElementMethod.Module;

    public override bool HasParameters => ElementMethod.HasParameters;

    public override Collection<ParameterDefinition> Parameters => ElementMethod.Parameters;

    public override bool ContainsGenericParameter => ElementMethod.ContainsGenericParameter;

    public sealed override MethodReference GetElementMethod()
    {
        return ElementMethod.GetElementMethod();
    }
}