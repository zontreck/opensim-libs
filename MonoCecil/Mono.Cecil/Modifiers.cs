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

public interface IModifierType
{
    TypeReference ModifierType { get; }
    TypeReference ElementType { get; }
}

public sealed class OptionalModifierType : TypeSpecification, IModifierType
{
    public OptionalModifierType(TypeReference modifierType, TypeReference type)
        : base(type)
    {
        if (modifierType == null)
            throw new ArgumentNullException(Mixin.Argument.modifierType.ToString());
        Mixin.CheckType(type);
        ModifierType = modifierType;
        etype = MD.ElementType.CModOpt;
    }

    public override string Name => base.Name + Suffix;

    public override string FullName => base.FullName + Suffix;

    private string Suffix => " modopt(" + ModifierType + ")";

    public override bool IsValueType
    {
        get => false;
        set => throw new InvalidOperationException();
    }

    public override bool IsOptionalModifier => true;

    public override bool ContainsGenericParameter =>
        ModifierType.ContainsGenericParameter || base.ContainsGenericParameter;

    public TypeReference ModifierType { get; set; }
}

public sealed class RequiredModifierType : TypeSpecification, IModifierType
{
    public RequiredModifierType(TypeReference modifierType, TypeReference type)
        : base(type)
    {
        if (modifierType == null)
            throw new ArgumentNullException(Mixin.Argument.modifierType.ToString());
        Mixin.CheckType(type);
        ModifierType = modifierType;
        etype = MD.ElementType.CModReqD;
    }

    public override string Name => base.Name + Suffix;

    public override string FullName => base.FullName + Suffix;

    private string Suffix => " modreq(" + ModifierType + ")";

    public override bool IsValueType
    {
        get => false;
        set => throw new InvalidOperationException();
    }

    public override bool IsRequiredModifier => true;

    public override bool ContainsGenericParameter =>
        ModifierType.ContainsGenericParameter || base.ContainsGenericParameter;

    public TypeReference ModifierType { get; set; }
}