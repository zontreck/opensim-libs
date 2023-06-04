//
// Author:
//   Jb Evain (jbevain@gmail.com)
//
// Copyright (c) 2008 - 2015 Jb Evain
// Copyright (c) 2008 - 2011 Novell, Inc.
//
// Licensed under the MIT/X11 license.
//

using System.Threading;
using Mono.Collections.Generic;

namespace Mono.Cecil;

public sealed class MethodReturnType : IConstantProvider, ICustomAttributeProvider, IMarshalInfoProvider
{
    internal IMethodSignature method;
    internal ParameterDefinition parameter;

    public MethodReturnType(IMethodSignature method)
    {
        this.method = method;
    }

    public IMethodSignature Method => method;

    public TypeReference ReturnType { get; set; }

    internal ParameterDefinition Parameter
    {
        get
        {
            if (parameter == null)
                Interlocked.CompareExchange(ref parameter, new ParameterDefinition(ReturnType, method), null);

            return parameter;
        }
    }

    public ParameterAttributes Attributes
    {
        get => Parameter.Attributes;
        set => Parameter.Attributes = value;
    }

    public string Name
    {
        get => Parameter.Name;
        set => Parameter.Name = value;
    }

    public bool HasDefault
    {
        get => parameter != null && parameter.HasDefault;
        set => Parameter.HasDefault = value;
    }

    public bool HasFieldMarshal
    {
        get => parameter != null && parameter.HasFieldMarshal;
        set => Parameter.HasFieldMarshal = value;
    }

    public MetadataToken MetadataToken
    {
        get => Parameter.MetadataToken;
        set => Parameter.MetadataToken = value;
    }

    public bool HasConstant
    {
        get => parameter != null && parameter.HasConstant;
        set => Parameter.HasConstant = value;
    }

    public object Constant
    {
        get => Parameter.Constant;
        set => Parameter.Constant = value;
    }

    public bool HasCustomAttributes => parameter != null && parameter.HasCustomAttributes;

    public Collection<CustomAttribute> CustomAttributes => Parameter.CustomAttributes;

    public bool HasMarshalInfo => parameter != null && parameter.HasMarshalInfo;

    public MarshalInfo MarshalInfo
    {
        get => Parameter.MarshalInfo;
        set => Parameter.MarshalInfo = value;
    }
}