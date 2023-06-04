//
// Author:
//   Jb Evain (jbevain@gmail.com)
//
// Copyright (c) 2008 - 2015 Jb Evain
// Copyright (c) 2008 - 2011 Novell, Inc.
//
// Licensed under the MIT/X11 license.
//

namespace Mono.Cecil;

public sealed class ExportedType : IMetadataTokenProvider
{
    private uint attributes;
    private readonly ModuleDefinition module;

    private IMetadataScope scope;
    internal MetadataToken token;

    public ExportedType(string @namespace, string name, ModuleDefinition module, IMetadataScope scope)
    {
        this.Namespace = @namespace;
        this.Name = name;
        this.scope = scope;
        this.module = module;
    }

    public string Namespace { get; set; }

    public string Name { get; set; }

    public TypeAttributes Attributes
    {
        get => (TypeAttributes)attributes;
        set => attributes = (uint)value;
    }

    public IMetadataScope Scope
    {
        get
        {
            if (DeclaringType != null)
                return DeclaringType.Scope;

            return scope;
        }
        set
        {
            if (DeclaringType != null)
            {
                DeclaringType.Scope = value;
                return;
            }

            scope = value;
        }
    }

    public ExportedType DeclaringType { get; set; }

    public int Identifier { get; set; }

    public bool IsForwarder
    {
        get => attributes.GetAttributes((uint)TypeAttributes.Forwarder);
        set => attributes = attributes.SetAttributes((uint)TypeAttributes.Forwarder, value);
    }

    public string FullName
    {
        get
        {
            var fullname = string.IsNullOrEmpty(Namespace)
                ? Name
                : Namespace + '.' + Name;

            if (DeclaringType != null)
                return DeclaringType.FullName + "/" + fullname;

            return fullname;
        }
    }

    public MetadataToken MetadataToken
    {
        get => token;
        set => token = value;
    }

    public override string ToString()
    {
        return FullName;
    }

    public TypeDefinition Resolve()
    {
        return module.Resolve(CreateReference());
    }

    internal TypeReference CreateReference()
    {
        return new TypeReference(Namespace, Name, module, scope)
        {
            DeclaringType = DeclaringType != null ? DeclaringType.CreateReference() : null
        };
    }

    #region TypeAttributes

    public bool IsNotPublic
    {
        get => attributes.GetMaskedAttributes((uint)TypeAttributes.VisibilityMask, (uint)TypeAttributes.NotPublic);
        set => attributes = attributes.SetMaskedAttributes((uint)TypeAttributes.VisibilityMask,
            (uint)TypeAttributes.NotPublic, value);
    }

    public bool IsPublic
    {
        get => attributes.GetMaskedAttributes((uint)TypeAttributes.VisibilityMask, (uint)TypeAttributes.Public);
        set => attributes =
            attributes.SetMaskedAttributes((uint)TypeAttributes.VisibilityMask, (uint)TypeAttributes.Public, value);
    }

    public bool IsNestedPublic
    {
        get => attributes.GetMaskedAttributes((uint)TypeAttributes.VisibilityMask, (uint)TypeAttributes.NestedPublic);
        set => attributes = attributes.SetMaskedAttributes((uint)TypeAttributes.VisibilityMask,
            (uint)TypeAttributes.NestedPublic, value);
    }

    public bool IsNestedPrivate
    {
        get => attributes.GetMaskedAttributes((uint)TypeAttributes.VisibilityMask, (uint)TypeAttributes.NestedPrivate);
        set => attributes = attributes.SetMaskedAttributes((uint)TypeAttributes.VisibilityMask,
            (uint)TypeAttributes.NestedPrivate, value);
    }

    public bool IsNestedFamily
    {
        get => attributes.GetMaskedAttributes((uint)TypeAttributes.VisibilityMask, (uint)TypeAttributes.NestedFamily);
        set => attributes = attributes.SetMaskedAttributes((uint)TypeAttributes.VisibilityMask,
            (uint)TypeAttributes.NestedFamily, value);
    }

    public bool IsNestedAssembly
    {
        get => attributes.GetMaskedAttributes((uint)TypeAttributes.VisibilityMask, (uint)TypeAttributes.NestedAssembly);
        set => attributes = attributes.SetMaskedAttributes((uint)TypeAttributes.VisibilityMask,
            (uint)TypeAttributes.NestedAssembly, value);
    }

    public bool IsNestedFamilyAndAssembly
    {
        get => attributes.GetMaskedAttributes((uint)TypeAttributes.VisibilityMask,
            (uint)TypeAttributes.NestedFamANDAssem);
        set => attributes = attributes.SetMaskedAttributes((uint)TypeAttributes.VisibilityMask,
            (uint)TypeAttributes.NestedFamANDAssem, value);
    }

    public bool IsNestedFamilyOrAssembly
    {
        get => attributes.GetMaskedAttributes((uint)TypeAttributes.VisibilityMask,
            (uint)TypeAttributes.NestedFamORAssem);
        set => attributes = attributes.SetMaskedAttributes((uint)TypeAttributes.VisibilityMask,
            (uint)TypeAttributes.NestedFamORAssem, value);
    }

    public bool IsAutoLayout
    {
        get => attributes.GetMaskedAttributes((uint)TypeAttributes.LayoutMask, (uint)TypeAttributes.AutoLayout);
        set => attributes =
            attributes.SetMaskedAttributes((uint)TypeAttributes.LayoutMask, (uint)TypeAttributes.AutoLayout, value);
    }

    public bool IsSequentialLayout
    {
        get => attributes.GetMaskedAttributes((uint)TypeAttributes.LayoutMask, (uint)TypeAttributes.SequentialLayout);
        set => attributes = attributes.SetMaskedAttributes((uint)TypeAttributes.LayoutMask,
            (uint)TypeAttributes.SequentialLayout, value);
    }

    public bool IsExplicitLayout
    {
        get => attributes.GetMaskedAttributes((uint)TypeAttributes.LayoutMask, (uint)TypeAttributes.ExplicitLayout);
        set => attributes = attributes.SetMaskedAttributes((uint)TypeAttributes.LayoutMask,
            (uint)TypeAttributes.ExplicitLayout, value);
    }

    public bool IsClass
    {
        get => attributes.GetMaskedAttributes((uint)TypeAttributes.ClassSemanticMask, (uint)TypeAttributes.Class);
        set => attributes =
            attributes.SetMaskedAttributes((uint)TypeAttributes.ClassSemanticMask, (uint)TypeAttributes.Class, value);
    }

    public bool IsInterface
    {
        get => attributes.GetMaskedAttributes((uint)TypeAttributes.ClassSemanticMask, (uint)TypeAttributes.Interface);
        set => attributes = attributes.SetMaskedAttributes((uint)TypeAttributes.ClassSemanticMask,
            (uint)TypeAttributes.Interface, value);
    }

    public bool IsAbstract
    {
        get => attributes.GetAttributes((uint)TypeAttributes.Abstract);
        set => attributes = attributes.SetAttributes((uint)TypeAttributes.Abstract, value);
    }

    public bool IsSealed
    {
        get => attributes.GetAttributes((uint)TypeAttributes.Sealed);
        set => attributes = attributes.SetAttributes((uint)TypeAttributes.Sealed, value);
    }

    public bool IsSpecialName
    {
        get => attributes.GetAttributes((uint)TypeAttributes.SpecialName);
        set => attributes = attributes.SetAttributes((uint)TypeAttributes.SpecialName, value);
    }

    public bool IsImport
    {
        get => attributes.GetAttributes((uint)TypeAttributes.Import);
        set => attributes = attributes.SetAttributes((uint)TypeAttributes.Import, value);
    }

    public bool IsSerializable
    {
        get => attributes.GetAttributes((uint)TypeAttributes.Serializable);
        set => attributes = attributes.SetAttributes((uint)TypeAttributes.Serializable, value);
    }

    public bool IsAnsiClass
    {
        get => attributes.GetMaskedAttributes((uint)TypeAttributes.StringFormatMask, (uint)TypeAttributes.AnsiClass);
        set => attributes = attributes.SetMaskedAttributes((uint)TypeAttributes.StringFormatMask,
            (uint)TypeAttributes.AnsiClass, value);
    }

    public bool IsUnicodeClass
    {
        get => attributes.GetMaskedAttributes((uint)TypeAttributes.StringFormatMask, (uint)TypeAttributes.UnicodeClass);
        set => attributes = attributes.SetMaskedAttributes((uint)TypeAttributes.StringFormatMask,
            (uint)TypeAttributes.UnicodeClass, value);
    }

    public bool IsAutoClass
    {
        get => attributes.GetMaskedAttributes((uint)TypeAttributes.StringFormatMask, (uint)TypeAttributes.AutoClass);
        set => attributes = attributes.SetMaskedAttributes((uint)TypeAttributes.StringFormatMask,
            (uint)TypeAttributes.AutoClass, value);
    }

    public bool IsBeforeFieldInit
    {
        get => attributes.GetAttributes((uint)TypeAttributes.BeforeFieldInit);
        set => attributes = attributes.SetAttributes((uint)TypeAttributes.BeforeFieldInit, value);
    }

    public bool IsRuntimeSpecialName
    {
        get => attributes.GetAttributes((uint)TypeAttributes.RTSpecialName);
        set => attributes = attributes.SetAttributes((uint)TypeAttributes.RTSpecialName, value);
    }

    public bool HasSecurity
    {
        get => attributes.GetAttributes((uint)TypeAttributes.HasSecurity);
        set => attributes = attributes.SetAttributes((uint)TypeAttributes.HasSecurity, value);
    }

    #endregion
}