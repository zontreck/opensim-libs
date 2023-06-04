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

public abstract class ParameterReference : IMetadataTokenProvider
{
    internal int index = -1;

    protected TypeReference parameter_type;
    internal MetadataToken token;

    internal ParameterReference(string name, TypeReference parameterType)
    {
        if (parameterType == null)
            throw new ArgumentNullException("parameterType");

        this.Name = name ?? string.Empty;
        parameter_type = parameterType;
    }

    public string Name { get; set; }

    public int Index => index;

    public TypeReference ParameterType
    {
        get => parameter_type;
        set => parameter_type = value;
    }

    public MetadataToken MetadataToken
    {
        get => token;
        set => token = value;
    }

    public override string ToString()
    {
        return Name;
    }

    public abstract ParameterDefinition Resolve();
}