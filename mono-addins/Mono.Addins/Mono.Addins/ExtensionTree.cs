//
// ExtensionTree.cs
//
// Author:
//   Lluis Sanchez Gual
//
// Copyright (C) 2007 Novell, Inc (http://www.novell.com)
//
// Permission is hereby granted, free of charge, to any person obtaining
// a copy of this software and associated documentation files (the
// "Software"), to deal in the Software without restriction, including
// without limitation the rights to use, copy, modify, merge, publish,
// distribute, sublicense, and/or sell copies of the Software, and to
// permit persons to whom the Software is furnished to do so, subject to
// the following conditions:
// 
// The above copyright notice and this permission notice shall be
// included in all copies or substantial portions of the Software.
// 
// THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND,
// EXPRESS OR IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF
// MERCHANTABILITY, FITNESS FOR A PARTICULAR PURPOSE AND
// NONINFRINGEMENT. IN NO EVENT SHALL THE AUTHORS OR COPYRIGHT HOLDERS BE
// LIABLE FOR ANY CLAIM, DAMAGES OR OTHER LIABILITY, WHETHER IN AN ACTION
// OF CONTRACT, TORT OR OTHERWISE, ARISING FROM, OUT OF OR IN CONNECTION
// WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE SOFTWARE.
//


using System;
using System.Collections.Generic;
using System.Reflection;
using Mono.Addins.Database;
using Mono.Addins.Description;

namespace Mono.Addins;

internal class ExtensionTree : TreeNode
{
    internal const string AutoIdPrefix = "__nid_";
    private int internalId;

    public ExtensionTree(AddinEngine addinEngine, ExtensionContext context) : base(addinEngine, "")
    {
        Context = context;
    }

    public override ExtensionContext Context { get; }


    public void LoadExtension(ExtensionContextTransaction transaction, TreeNode tnode, string addin,
        Extension extension, List<TreeNode> addedNodes)
    {
        if (tnode == null)
        {
            addinEngine.ReportError(
                "Can't load extensions for path '" + extension.Path + "'. Extension point not defined.", addin, null,
                false);
            return;
        }

        var curPos = -1;
        LoadExtensionElement(transaction, tnode, addin, extension.ExtensionNodes, (ModuleDescription)extension.Parent,
            ref curPos, tnode.Condition, false, addedNodes);
    }

    private void LoadExtensionElement(ExtensionContextTransaction transaction, TreeNode parentNode, string addin,
        ExtensionNodeDescriptionCollection extension, ModuleDescription module, ref int curPos,
        BaseCondition parentCondition, bool inComplextCondition, List<TreeNode> addedNodes)
    {
        foreach (ExtensionNodeDescription elem in extension)
        {
            if (inComplextCondition)
            {
                parentCondition = ReadComplexCondition(elem, parentCondition);
                inComplextCondition = false;
                continue;
            }

            if (elem.NodeName == "ComplexCondition")
            {
                LoadExtensionElement(transaction, parentNode, addin, elem.ChildNodes, module, ref curPos,
                    parentCondition, true, addedNodes);
                continue;
            }

            if (elem.NodeName == "Condition")
            {
                var cond = new Condition(AddinEngine, elem, parentCondition);
                LoadExtensionElement(transaction, parentNode, addin, elem.ChildNodes, module, ref curPos, cond, false,
                    addedNodes);
                continue;
            }

            var pnode = parentNode;
            ExtensionPoint extensionPoint = null;
            while (pnode != null && (extensionPoint = pnode.ExtensionPoint) == null)
                pnode = pnode.Parent;

            var after = elem.GetAttribute("insertafter");
            if (after.Length == 0 && extensionPoint != null && curPos == -1)
                after = extensionPoint.DefaultInsertAfter;
            if (after.Length > 0)
            {
                var i = parentNode.IndexOfChild(after);
                if (i != -1)
                    curPos = i + 1;
            }

            var before = elem.GetAttribute("insertbefore");
            if (before.Length == 0 && extensionPoint != null && curPos == -1)
                before = extensionPoint.DefaultInsertBefore;
            if (before.Length > 0)
            {
                var i = parentNode.IndexOfChild(before);
                if (i != -1)
                    curPos = i;
            }

            // If node position is not explicitly set, add the node at the end
            if (curPos == -1)
                curPos = parentNode.Children.Count;

            // Find the type of the node in this extension
            var ntype = addinEngine.FindType(transaction, parentNode.ExtensionNodeSet, elem.NodeName, addin);

            if (ntype == null)
            {
                addinEngine.ReportError(
                    "Node '" + elem.NodeName + "' not allowed in extension: " + parentNode.GetPath(), addin, null,
                    false);
                continue;
            }

            var id = elem.GetAttribute("id");
            if (id.Length == 0)
                id = AutoIdPrefix + ++internalId;

            var childNode = new TreeNode(addinEngine, id);

            var enode = ReadNode(childNode, addin, ntype, elem, module, transaction);
            if (enode == null)
                continue;

            // Enables bulk update of children
            parentNode.BeginChildrenUpdateTransaction(transaction);

            childNode.Condition = parentCondition;
            childNode.ExtensionNodeSet = ntype;
            parentNode.InsertChild(transaction, curPos, childNode);
            addedNodes.Add(childNode);

            // Load children
            if (elem.ChildNodes.Count > 0)
            {
                var cp = 0;
                LoadExtensionElement(transaction, childNode, addin, elem.ChildNodes, module, ref cp, parentCondition,
                    false, addedNodes);
            }

            curPos++;
        }
    }

    private BaseCondition ReadComplexCondition(ExtensionNodeDescription elem, BaseCondition parentCondition)
    {
        if (elem.NodeName == "Or" || elem.NodeName == "And" || elem.NodeName == "Not")
        {
            var conds = new List<BaseCondition>();
            foreach (ExtensionNodeDescription celem in elem.ChildNodes) conds.Add(ReadComplexCondition(celem, null));
            if (elem.NodeName == "Or") return new OrCondition(conds.ToArray(), parentCondition);

            if (elem.NodeName == "And")
            {
                return new AndCondition(conds.ToArray(), parentCondition);
            }

            if (conds.Count != 1)
            {
                addinEngine.ReportError(
                    "Invalid complex condition element '" + elem.NodeName +
                    "'. 'Not' condition can only have one parameter.", null, null, false);
                return new NullCondition();
            }

            return new NotCondition(conds[0], parentCondition);
        }

        if (elem.NodeName == "Condition") return new Condition(AddinEngine, elem, parentCondition);
        addinEngine.ReportError("Invalid complex condition element '" + elem.NodeName + "'.", null, null, false);
        return new NullCondition();
    }

    public ExtensionNode ReadNode(TreeNode tnode, string addin, ExtensionNodeType ntype, ExtensionNodeDescription elem,
        ModuleDescription module, ExtensionContextTransaction transaction)
    {
        try
        {
            if (ntype.Type == null)
                if (!InitializeNodeType(ntype, transaction))
                    return null;

            ExtensionNode node;
            node = Activator.CreateInstance(ntype.Type) as ExtensionNode;
            if (node == null)
            {
                addinEngine.ReportError("Extension node type '" + ntype.Type + "' must be a subclass of ExtensionNode",
                    addin, null, false);
                return null;
            }

            tnode.AttachExtensionNode(node);
            node.SetData(addinEngine, addin, ntype, module);
            node.Read(elem);
            return node;
        }
        catch (Exception ex)
        {
            addinEngine.ReportError(
                "Could not read extension node of type '" + ntype.Type + "' from extension path '" + tnode.GetPath() +
                "'", addin, ex, false);
            return null;
        }
    }

    private bool InitializeNodeType(ExtensionNodeType ntype, ExtensionContextTransaction transaction)
    {
        var p = addinEngine.GetAddin(transaction.GetAddinEngineTransaction(), ntype.AddinId);
        if (p == null)
        {
            var engineTransaction = transaction.GetOrCreateAddinEngineTransaction();
            if (!addinEngine.LoadAddin(engineTransaction, null, ntype.AddinId, false))
            {
                addinEngine.ReportError("Add-in not found", ntype.AddinId, null, false);
                return false;
            }

            p = addinEngine.GetAddin(engineTransaction, ntype.AddinId);
        }

        // If no type name is provided, use TypeExtensionNode by default
        if (ntype.TypeName == null || ntype.TypeName.Length == 0 ||
            ntype.TypeName == typeof(TypeExtensionNode).AssemblyQualifiedName)
        {
            // If it has a custom attribute, use the generic version of TypeExtensionNode
            if (ntype.ExtensionAttributeTypeName.Length > 0)
            {
                var attType = p.GetType(ntype.ExtensionAttributeTypeName, false);
                if (attType == null)
                {
                    addinEngine.ReportError(
                        "Custom attribute type '" + ntype.ExtensionAttributeTypeName + "' not found.", ntype.AddinId,
                        null, false);
                    return false;
                }

                if (ntype.ObjectTypeName.Length > 0 ||
                    ntype.TypeName == typeof(TypeExtensionNode).AssemblyQualifiedName)
                    ntype.Type = typeof(TypeExtensionNode<>).MakeGenericType(attType);
                else
                    ntype.Type = typeof(ExtensionNode<>).MakeGenericType(attType);
            }
            else
            {
                ntype.Type = typeof(TypeExtensionNode);
                return true;
            }
        }
        else
        {
            ntype.Type = p.GetType(ntype.TypeName, false);
            if (ntype.Type == null)
            {
                addinEngine.ReportError("Extension node type '" + ntype.TypeName + "' not found.", ntype.AddinId, null,
                    false);
                return false;
            }
        }

        // Check if the type has NodeAttribute attributes applied to fields.
        ExtensionNodeType.FieldData boundAttributeType = null;
        var fields = GetMembersMap(ntype.Type, out boundAttributeType);
        ntype.CustomAttributeMember = boundAttributeType;
        if (fields.Count > 0)
            ntype.Fields = fields;

        // If the node type is bound to a custom attribute and there is a member bound to that attribute,
        // get the member map for the attribute.

        if (boundAttributeType != null)
        {
            if (ntype.ExtensionAttributeTypeName.Length == 0)
                throw new InvalidOperationException("Extension node not bound to a custom attribute.");

            if (!Util.TryParseTypeName(ntype.ExtensionAttributeTypeName, out var type, out _) ||
                type != boundAttributeType.MemberType.FullName)
                throw new InvalidOperationException("Incorrect custom attribute type declaration in " + ntype.Type +
                                                    ". Expected '" + ntype.ExtensionAttributeTypeName + "' found '" +
                                                    boundAttributeType.MemberType.AssemblyQualifiedName + "'");

            fields = GetMembersMap(boundAttributeType.MemberType, out boundAttributeType);
            if (fields.Count > 0)
                ntype.CustomAttributeFields = fields;
        }

        return true;
    }

    private Dictionary<string, ExtensionNodeType.FieldData> GetMembersMap(Type type,
        out ExtensionNodeType.FieldData boundAttributeType)
    {
        string fname;
        var fields = new Dictionary<string, ExtensionNodeType.FieldData>();
        boundAttributeType = null;

        while (type != typeof(object) && type != null)
        {
            foreach (var field in type.GetFields(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance |
                                                 BindingFlags.DeclaredOnly))
            {
                var at = (NodeAttributeAttribute)Attribute.GetCustomAttribute(field, typeof(NodeAttributeAttribute),
                    true);
                if (at != null)
                {
                    var fd = CreateFieldData(field, at, out fname, ref boundAttributeType);
                    if (fd != null)
                        fields[fname] = fd;
                }
            }

            foreach (var prop in type.GetProperties(BindingFlags.Public | BindingFlags.NonPublic |
                                                    BindingFlags.Instance | BindingFlags.DeclaredOnly))
            {
                var at = (NodeAttributeAttribute)Attribute.GetCustomAttribute(prop, typeof(NodeAttributeAttribute),
                    true);
                if (at != null)
                {
                    var fd = CreateFieldData(prop, at, out fname, ref boundAttributeType);
                    if (fd != null)
                        fields[fname] = fd;
                }
            }

            type = type.BaseType;
        }

        return fields;
    }

    private ExtensionNodeType.FieldData CreateFieldData(MemberInfo member, NodeAttributeAttribute at, out string name,
        ref ExtensionNodeType.FieldData boundAttributeType)
    {
        var fdata = new ExtensionNodeType.FieldData();
        fdata.Member = member;
        fdata.Required = at.Required;
        fdata.Localizable = at.Localizable;

        if (at.Name != null && at.Name.Length > 0)
            name = at.Name;
        else
            name = member.Name;

        if (typeof(CustomExtensionAttribute).IsAssignableFrom(fdata.MemberType))
        {
            if (boundAttributeType != null)
                throw new InvalidOperationException("Type '" + member.DeclaringType +
                                                    "' has two members bound to a custom attribute. There can be only one.");
            boundAttributeType = fdata;
            return null;
        }

        return fdata;
    }
}