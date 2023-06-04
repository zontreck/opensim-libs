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
using MD = Mono.Cecil.Metadata;

namespace Mono.Cecil;

public sealed class SentinelType : TypeSpecification
{
    public SentinelType(TypeReference type)
        : base(type)
    {
        Mixin.CheckType(type);
        etype = MD.ElementType.Sentinel;
    }

    public override bool IsValueType
    {
        get => false;
        set => throw new InvalidOperationException();
    }

    public override bool IsSentinel => true;
}