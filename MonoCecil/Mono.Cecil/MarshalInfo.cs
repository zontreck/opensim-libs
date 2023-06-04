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

public class MarshalInfo
{
    internal NativeType native;

    public MarshalInfo(NativeType native)
    {
        this.native = native;
    }

    public NativeType NativeType
    {
        get => native;
        set => native = value;
    }
}

public sealed class ArrayMarshalInfo : MarshalInfo
{
    internal NativeType element_type;
    internal int size;
    internal int size_parameter_index;
    internal int size_parameter_multiplier;

    public ArrayMarshalInfo()
        : base(NativeType.Array)
    {
        element_type = NativeType.None;
        size_parameter_index = -1;
        size = -1;
        size_parameter_multiplier = -1;
    }

    public NativeType ElementType
    {
        get => element_type;
        set => element_type = value;
    }

    public int SizeParameterIndex
    {
        get => size_parameter_index;
        set => size_parameter_index = value;
    }

    public int Size
    {
        get => size;
        set => size = value;
    }

    public int SizeParameterMultiplier
    {
        get => size_parameter_multiplier;
        set => size_parameter_multiplier = value;
    }
}

public sealed class CustomMarshalInfo : MarshalInfo
{
    internal string cookie;

    internal Guid guid;
    internal TypeReference managed_type;
    internal string unmanaged_type;

    public CustomMarshalInfo()
        : base(NativeType.CustomMarshaler)
    {
    }

    public Guid Guid
    {
        get => guid;
        set => guid = value;
    }

    public string UnmanagedType
    {
        get => unmanaged_type;
        set => unmanaged_type = value;
    }

    public TypeReference ManagedType
    {
        get => managed_type;
        set => managed_type = value;
    }

    public string Cookie
    {
        get => cookie;
        set => cookie = value;
    }
}

public sealed class SafeArrayMarshalInfo : MarshalInfo
{
    internal VariantType element_type;

    public SafeArrayMarshalInfo()
        : base(NativeType.SafeArray)
    {
        element_type = VariantType.None;
    }

    public VariantType ElementType
    {
        get => element_type;
        set => element_type = value;
    }
}

public sealed class FixedArrayMarshalInfo : MarshalInfo
{
    internal NativeType element_type;
    internal int size;

    public FixedArrayMarshalInfo()
        : base(NativeType.FixedArray)
    {
        element_type = NativeType.None;
    }

    public NativeType ElementType
    {
        get => element_type;
        set => element_type = value;
    }

    public int Size
    {
        get => size;
        set => size = value;
    }
}

public sealed class FixedSysStringMarshalInfo : MarshalInfo
{
    internal int size;

    public FixedSysStringMarshalInfo()
        : base(NativeType.FixedSysString)
    {
        size = -1;
    }

    public int Size
    {
        get => size;
        set => size = value;
    }
}