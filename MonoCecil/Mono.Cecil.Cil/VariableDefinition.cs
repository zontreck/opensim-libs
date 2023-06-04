//
// Author:
//   Jb Evain (jbevain@gmail.com)
//
// Copyright (c) 2008 - 2015 Jb Evain
// Copyright (c) 2008 - 2011 Novell, Inc.
//
// Licensed under the MIT/X11 license.
//

namespace Mono.Cecil.Cil;

public sealed class VariableDefinition : VariableReference
{
    public VariableDefinition(TypeReference variableType)
        : base(variableType)
    {
    }

    public bool IsPinned => variable_type.IsPinned;

    public override VariableDefinition Resolve()
    {
        return this;
    }
}