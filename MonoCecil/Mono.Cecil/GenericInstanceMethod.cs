//
// Author:
//   Jb Evain (jbevain@gmail.com)
//
// Copyright (c) 2008 - 2015 Jb Evain
// Copyright (c) 2008 - 2011 Novell, Inc.
//
// Licensed under the MIT/X11 license.
//

using System.Text;
using System.Threading;
using Mono.Collections.Generic;

namespace Mono.Cecil;

public sealed class GenericInstanceMethod : MethodSpecification, IGenericInstance, IGenericContext
{
    private Collection<TypeReference> arguments;

    public GenericInstanceMethod(MethodReference method)
        : base(method)
    {
    }

    internal GenericInstanceMethod(MethodReference method, int arity)
        : this(method)
    {
        arguments = new Collection<TypeReference>(arity);
    }

    public override bool IsGenericInstance => true;

    public override bool ContainsGenericParameter => this.ContainsGenericParameter() || base.ContainsGenericParameter;

    public override string FullName
    {
        get
        {
            var signature = new StringBuilder();
            var method = ElementMethod;
            signature.Append(method.ReturnType.FullName)
                .Append(" ")
                .Append(method.DeclaringType.FullName)
                .Append("::")
                .Append(method.Name);
            this.GenericInstanceFullName(signature);
            this.MethodSignatureFullName(signature);
            return signature.ToString();
        }
    }

    IGenericParameterProvider IGenericContext.Method => ElementMethod;

    IGenericParameterProvider IGenericContext.Type => ElementMethod.DeclaringType;

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