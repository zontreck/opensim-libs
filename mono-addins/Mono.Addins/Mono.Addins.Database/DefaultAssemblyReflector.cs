// DefaultAssemblyReflector.cs
//
// Author:
//   Lluis Sanchez Gual <lluis@novell.com>
//
// Copyright (c) 2007 Novell, Inc (http://www.novell.com)
//
// Permission is hereby granted, free of charge, to any person obtaining a copy
// of this software and associated documentation files (the "Software"), to deal
// in the Software without restriction, including without limitation the rights
// to use, copy, modify, merge, publish, distribute, sublicense, and/or sell
// copies of the Software, and to permit persons to whom the Software is
// furnished to do so, subject to the following conditions:
//
// The above copyright notice and this permission notice shall be included in
// all copies or substantial portions of the Software.
//
// THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
// IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
// FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
// AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
// LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
// OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN
// THE SOFTWARE.
//
//

using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Reflection;

namespace Mono.Addins.Database;

internal class DefaultAssemblyReflector : IAssemblyReflector
{
    public void Initialize(IAssemblyLocator locator)
    {
    }

    public object LoadAssembly(string file)
    {
        return Util.LoadAssemblyForReflection(file);
    }

    public void UnloadAssembly(object assembly)
    {
    }

    public string GetAssemblyName(object asm)
    {
        return ((Assembly)asm).GetName().Name;
    }

    public string[] GetResourceNames(object asm)
    {
        return ((Assembly)asm).GetManifestResourceNames();
    }

    public Stream GetResourceStream(object asm, string resourceName)
    {
        return ((Assembly)asm).GetManifestResourceStream(resourceName);
    }

    public object[] GetCustomAttributes(object obj, Type type, bool inherit)
    {
        var aprov = obj as ICustomAttributeProvider;
        if (aprov != null)
            return aprov.GetCustomAttributes(type, inherit);
        return new object [0];
    }

    public object GetCustomAttribute(object obj, Type type, bool inherit)
    {
        foreach (var att in GetCustomAttributes(obj, type, inherit))
            if (type.IsInstanceOfType(att))
                return att;
        return null;
    }

    public List<CustomAttribute> GetRawCustomAttributes(object obj, Type type, bool inherit)
    {
        var aprov = obj as ICustomAttributeProvider;
        var atts = new List<CustomAttribute>();
        if (aprov == null)
            return atts;

        foreach (var at in aprov.GetCustomAttributes(type, inherit))
            atts.Add(ConvertAttribute(at));

        return atts;
    }

    public string GetTypeName(object type)
    {
        return ((Type)type).Name;
    }

    public IEnumerable GetFields(object type)
    {
        return ((Type)type).GetFields(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
    }

    public string GetFieldName(object field)
    {
        return ((FieldInfo)field).Name;
    }

    public string GetFieldTypeFullName(object field)
    {
        return ((FieldInfo)field).FieldType.AssemblyQualifiedName;
    }

    public IEnumerable GetAssemblyTypes(object asm)
    {
        return ((Assembly)asm).GetTypes();
    }

    public IEnumerable GetBaseTypeFullNameList(object type)
    {
        var list = new List<string>();
        var btype = ((Type)type).BaseType;
        while (btype != typeof(object))
        {
            list.Add(btype.FullName);
            btype = btype.BaseType;
        }

        foreach (var iterf in ((Type)type).GetInterfaces()) list.Add(iterf.FullName);
        return list;
    }

    public object LoadAssemblyFromReference(object asmReference)
    {
        return Assembly.Load((AssemblyName)asmReference);
    }

    public IEnumerable GetAssemblyReferences(object asm)
    {
        return ((Assembly)asm).GetReferencedAssemblies();
    }

    public object GetType(object asm, string typeName)
    {
        return ((Assembly)asm).GetType(typeName);
    }

    public string GetTypeFullName(object type)
    {
        return ((Type)type).FullName;
    }

    public bool TypeIsAssignableFrom(object baseType, object type)
    {
        return ((Type)baseType).IsAssignableFrom((Type)type);
    }

    public string GetTypeAssemblyQualifiedName(object type)
    {
        return ((Type)type).AssemblyQualifiedName;
    }

    private CustomAttribute ConvertAttribute(object ob)
    {
        var at = new CustomAttribute();
        var type = ob.GetType();
        at.TypeName = type.AssemblyQualifiedName;

        foreach (var prop in type.GetProperties(BindingFlags.Public | BindingFlags.Instance))
        {
            var val = prop.GetValue(ob, null);
            if (val != null)
            {
                var bt = (NodeAttributeAttribute)Attribute.GetCustomAttribute(prop, typeof(NodeAttributeAttribute),
                    true);
                if (bt != null)
                {
                    var name = string.IsNullOrEmpty(bt.Name) ? prop.Name : bt.Name;
                    at[name] = Convert.ToString(val, CultureInfo.InvariantCulture);
                }
            }
        }

        foreach (var field in type.GetFields(BindingFlags.Public | BindingFlags.Instance))
        {
            var val = field.GetValue(ob);
            if (val != null)
            {
                var bt = (NodeAttributeAttribute)Attribute.GetCustomAttribute(field, typeof(NodeAttributeAttribute),
                    true);
                if (bt != null)
                {
                    var name = string.IsNullOrEmpty(bt.Name) ? field.Name : bt.Name;
                    at[name] = Convert.ToString(val, CultureInfo.InvariantCulture);
                }
            }
        }

        return at;
    }

    public string GetTypeAssemblyName(object type)
    {
        return ((Type)type).Assembly.FullName;
    }
}