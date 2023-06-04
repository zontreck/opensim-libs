//
// TreeNode.cs
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
using System.Collections.Immutable;
using System.Threading;
using Mono.Addins.Description;

namespace Mono.Addins;

internal class TreeNode
{
    protected AddinEngine addinEngine;
    private ExtensionContextTransaction buildTransaction;
    private ImmutableArray<TreeNode> children;

    private ImmutableArray<TreeNode>.Builder childrenBuilder;

    private ExtensionNode extensionNode;

    public TreeNode(AddinEngine addinEngine, string id)
    {
        Id = id;
        this.addinEngine = addinEngine;

        children = ImmutableArray<TreeNode>.Empty;

        // Root node
        if (id.Length == 0)
            ChildrenFromExtensionsLoaded = true;
    }

    public AddinEngine AddinEngine => addinEngine;

    public string Id { get; }

    public ExtensionNode ExtensionNode
    {
        get
        {
            if (extensionNode == null && ExtensionPoint != null)
            {
                var newNode = new ExtensionNode();
                newNode.SetData(addinEngine, ExtensionPoint.RootAddin, null, null);
                AttachExtensionNode(newNode);
            }

            return extensionNode;
        }
    }

    public bool HasExtensionNode => extensionNode != null || ExtensionPoint != null;

    public string AddinId => extensionNode != null ? extensionNode.AddinId : ExtensionPoint?.RootAddin;

    public ExtensionPoint ExtensionPoint { get; set; }

    public ExtensionNodeSet ExtensionNodeSet { get; set; }

    public TreeNode Parent { get; private set; }

    public BaseCondition Condition { get; set; }

    public virtual ExtensionContext Context
    {
        get
        {
            if (Parent != null)
                return Parent.Context;
            return null;
        }
    }

    public bool IsEnabled
    {
        get
        {
            if (Condition == null)
                return true;
            var ctx = Context;
            if (ctx == null)
                return true;
            return Condition.Evaluate(ctx);
        }
    }

    public bool ChildrenFromExtensionsLoaded { get; private set; }

    public IReadOnlyList<TreeNode> Children
    {
        get
        {
            if (IsInChildrenUpdateTransaction) return childrenBuilder;
            EnsureChildrenLoaded(null);
            return (IReadOnlyList<TreeNode>)childrenBuilder ?? children;
        }
    }

    /// <summary>
    ///     Returns true if the tree node has a child update transaction in progress,
    ///     and the current thread is the one that created the transaction.
    /// </summary>
    private bool IsInChildrenUpdateTransaction => childrenBuilder != null && Context.IsCurrentThreadInTransaction;

    internal void AttachExtensionNode(ExtensionNode enode)
    {
        if (Interlocked.CompareExchange(ref extensionNode, enode, null) != null)
            // Another thread already assigned the node
            return;

        if (extensionNode != null)
            extensionNode.SetTreeNode(this);
    }

    public void NotifyAddinUnloaded()
    {
        extensionNode?.NotifyAddinUnloaded();
    }

    public void NotifyAddinLoaded()
    {
        extensionNode?.NotifyAddinLoaded();
    }

    public void AddChildNode(ExtensionContextTransaction transaction, TreeNode node)
    {
        node.SetParent(transaction, this);

        if (childrenBuilder != null)
            childrenBuilder.Add(node);
        else
            children = children.Add(node);

        transaction.ReportChildrenChanged(this);
    }

    public void InsertChild(ExtensionContextTransaction transaction, int n, TreeNode node)
    {
        node.SetParent(transaction, this);

        if (childrenBuilder != null)
            childrenBuilder.Insert(n, node);
        else
            children = children.Insert(n, node);

        transaction.ReportChildrenChanged(this);
    }

    public void RemoveChild(ExtensionContextTransaction transaction, TreeNode node)
    {
        node.SetParent(transaction, null);

        if (childrenBuilder != null)
            childrenBuilder.Remove(node);
        else
            children = children.Remove(node);

        transaction.ReportChildrenChanged(this);
    }

    private void SetParent(ExtensionContextTransaction transaction, TreeNode newParent)
    {
        if (Parent == newParent)
            return;

        if (Parent != null && newParent != null)
            throw new InvalidOperationException("Node already has a parent");

        var currentCtx = Context;
        if (currentCtx != null && currentCtx != transaction.Context)
            throw new InvalidOperationException("Invalid context");

        Parent = newParent;

        if (newParent != null)
        {
            if (newParent.Context != null)
                OnAttachedToContext(transaction);
        }
        else
        {
            if (currentCtx != null)
                OnDetachedFromContext(transaction);
        }
    }

    private void OnAttachedToContext(ExtensionContextTransaction transaction)
    {
        // Once the node is part of a context, let's register the condition
        if (Condition != null)
            transaction.RegisterNodeCondition(this, Condition);

        // Propagate event to children
        foreach (var child in GetLoadedChildren())
            child.OnAttachedToContext(transaction);
    }

    private void OnDetachedFromContext(ExtensionContextTransaction transaction)
    {
        // Node being removed, unregister from context
        if (Condition != null)
            transaction.UnregisterNodeCondition(this, Condition);

        // Propagate event to children
        foreach (var child in GetLoadedChildren())
            child.OnDetachedFromContext(transaction);
    }

    public ExtensionNode GetExtensionNode(string path, string childId)
    {
        var node = GetNode(path, childId);
        return node != null ? node.ExtensionNode : null;
    }

    public ExtensionNode GetExtensionNode(string path)
    {
        var node = GetNode(path);
        return node != null ? node.ExtensionNode : null;
    }

    public TreeNode GetNode(string path, string childId)
    {
        if (childId == null || childId.Length == 0)
            return GetNode(path);
        return GetNode(path + "/" + childId);
    }

    public TreeNode GetNode(string path)
    {
        return GetNode(path, false);
    }

    public TreeNode GetNode(string path, bool buildPath)
    {
        return GetNode(path, buildPath, null);
    }

    public TreeNode GetNode(string path, bool buildPath, ExtensionContextTransaction transaction)
    {
        if (path.StartsWith("/"))
            path = path.Substring(1);

        var parts = path.Split('/');
        var curNode = this;

        foreach (var part in parts)
        {
            curNode.EnsureChildrenLoaded(transaction);
            var node = curNode.GetChildNode(part);
            if (node != null)
            {
                curNode = node;
                continue;
            }

            if (buildPath)
            {
                transaction = BeginContextTransaction(transaction, out var dispose);
                try
                {
                    // Check again inside the lock, just in case
                    curNode.EnsureChildrenLoaded(transaction);
                    node = curNode.GetChildNode(part);
                    if (node != null)
                    {
                        curNode = node;
                        continue;
                    }

                    var newNode = new TreeNode(addinEngine, part);
                    curNode.AddChildNode(transaction, newNode);
                    curNode = newNode;
                }
                finally
                {
                    if (dispose)
                        transaction.Dispose();
                }
            }
            else
            {
                return null;
            }
        }

        return curNode;
    }

    public TreeNode GetChildNode(string id)
    {
        var childrenList = Children;
        foreach (var node in childrenList)
            if (node.Id == id)
                return node;
        return null;
    }

    public int IndexOfChild(string id)
    {
        var childrenList = Children;
        for (var n = 0; n < childrenList.Count; n++)
            if (childrenList[n].Id == id)
                return n;
        return -1;
    }

    private void EnsureChildrenLoaded(ExtensionContextTransaction transaction)
    {
        if (IsInChildrenUpdateTransaction)
            return;

        if (!ChildrenFromExtensionsLoaded)
        {
            transaction = BeginContextTransaction(transaction, out var disposeTransaction);
            try
            {
                if (!ChildrenFromExtensionsLoaded)
                    if (ExtensionPoint != null)
                    {
                        BeginChildrenUpdateTransaction(transaction);
                        Context.LoadExtensions(transaction, GetPath(), this);
                        // We have to keep the reference to the extension point, since add-ins may be loaded/unloaded
                    }
            }
            finally
            {
                ChildrenFromExtensionsLoaded = true;
                if (disposeTransaction)
                    transaction.Dispose();
            }
        }
    }

    private IReadOnlyList<TreeNode> GetLoadedChildren()
    {
        if (IsInChildrenUpdateTransaction)
            return childrenBuilder;
        return children;
    }

    private ExtensionContextTransaction BeginContextTransaction(ExtensionContextTransaction currentTransaction,
        out bool dispose)
    {
        if (currentTransaction != null)
        {
            dispose = false;
            return currentTransaction;
        }

        if (IsInChildrenUpdateTransaction)
        {
            dispose = false;
            return buildTransaction;
        }

        dispose = true;
        return Context.BeginTransaction();
    }

    public void BeginChildrenUpdateTransaction(ExtensionContextTransaction transaction)
    {
        // If a transaction already started, just reuse it
        if (buildTransaction != null)
            return;

        childrenBuilder = children.ToBuilder();
        buildTransaction = transaction;

        transaction.RegisterChildrenUpdateTransaction(this);
    }

    internal void CommitChildrenUpdateTransaction()
    {
        if (buildTransaction == null)
            throw new InvalidOperationException("No transaction started");

        children = childrenBuilder.ToImmutable();

        var transaction = buildTransaction;

        childrenBuilder = null;
        buildTransaction = null;

        transaction.ReportChildrenChanged(this);
    }

    public string GetPath()
    {
        var num = 0;
        var node = this;
        while (node != null)
        {
            num++;
            node = node.Parent;
        }

        var ids = new string [num];

        node = this;
        while (node != null)
        {
            ids[--num] = node.Id;
            node = node.Parent;
        }

        return string.Join("/", ids);
    }

    public void NotifyAddinLoaded(RuntimeAddin ad, bool recursive)
    {
        if (extensionNode != null && extensionNode.AddinId == ad.Addin.Id)
            extensionNode.NotifyAddinLoaded();
        if (recursive && ChildrenFromExtensionsLoaded)
            foreach (var node in Children)
                node.NotifyAddinLoaded(ad, true);
    }

    public ExtensionPoint FindLoadedExtensionPoint(string path)
    {
        if (path.StartsWith("/"))
            path = path.Substring(1);

        var parts = path.Split('/');
        var curNode = this;

        foreach (var part in parts)
        {
            var node = curNode.GetChildNode(part);
            if (node != null)
            {
                curNode = node;
                if (!curNode.ChildrenFromExtensionsLoaded)
                    return null;
                if (curNode.ExtensionPoint != null)
                    return curNode.ExtensionPoint;
                continue;
            }

            return null;
        }

        return null;
    }

    public void FindAddinNodes(string id, List<TreeNode> nodes)
    {
        if (id != null && ExtensionPoint != null && ExtensionPoint.RootAddin == id)
            // It is an extension point created by the add-in. All nodes below this
            // extension point will be added to the list, even if they come from other add-ins.
            id = null;

        if (ChildrenFromExtensionsLoaded)
            // Deep-first search, to make sure children are removed before the parent.
            foreach (var node in Children)
                node.FindAddinNodes(id, nodes);

        if (id == null || AddinId == id)
            nodes.Add(this);
    }

    public bool NotifyChildrenChanged()
    {
        if (extensionNode != null)
            return extensionNode.NotifyChildChanged();
        return false;
    }

    public void ResetCachedData(ExtensionContextTransaction transaction)
    {
        if (ExtensionPoint != null)
        {
            var aid = Addin.GetIdName(ExtensionPoint.ParentAddinDescription.AddinId);
            var ad = addinEngine.GetAddin(transaction.GetAddinEngineTransaction(), aid);
            if (ad != null)
                ExtensionPoint = ad.Addin.Description.ExtensionPoints[GetPath()];
        }

        if (childrenBuilder != null)
            foreach (var cn in childrenBuilder)
                cn.ResetCachedData(transaction);

        foreach (var cn in children)
            cn.ResetCachedData(transaction);
    }
}