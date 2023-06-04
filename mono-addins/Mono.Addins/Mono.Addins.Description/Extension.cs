//
// Extension.cs
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
using System.Collections.Specialized;
using System.Xml;
using Mono.Addins.Serialization;

namespace Mono.Addins.Description;

/// <summary>
///     An extension definition.
/// </summary>
/// <remarks>
///     An Extension is a collection of nodes which have to be registered in an extension point.
///     The target extension point is specified in the <see cref="Mono.Addins.Description.Extension" />.Path property.
/// </remarks>
public class Extension : ObjectDescription, IComparable
{
    private ExtensionNodeDescriptionCollection nodes;

    /// <summary>
    ///     Initializes a new instance of the <see cref="Mono.Addins.Description.Extension" /> class.
    /// </summary>
    public Extension()
    {
    }

    /// <summary>
    ///     Initializes a new instance of the <see cref="Mono.Addins.Description.Extension" /> class.
    /// </summary>
    /// <param name='path'>
    ///     Path that identifies the extension point being extended
    /// </param>
    public Extension(string path)
    {
        Path = path;
    }

    /// <summary>
    ///     Initializes a new instance of the <see cref="Mono.Addins.Description.Extension" /> class.
    /// </summary>
    /// <param name='element'>
    ///     XML that describes the extension.
    /// </param>
    public Extension(XmlElement element)
    {
        Element = element;
        Path = element.GetAttribute("path");
    }

    /// <summary>
    ///     Gets or sets the path that identifies the extension point being extended.
    /// </summary>
    /// <value>
    ///     The path.
    /// </value>
    public string Path { get; set; }

    /// <summary>
    ///     Gets the extension nodes.
    /// </summary>
    /// <value>
    ///     The extension nodes.
    /// </value>
    public ExtensionNodeDescriptionCollection ExtensionNodes
    {
        get
        {
            if (nodes == null)
            {
                nodes = new ExtensionNodeDescriptionCollection(this);
                if (Element != null)
                    foreach (XmlNode node in Element.ChildNodes)
                    {
                        var e = node as XmlElement;
                        if (e != null)
                            nodes.Add(new ExtensionNodeDescription(e));
                    }
            }

            return nodes;
        }
    }

    int IComparable.CompareTo(object obj)
    {
        var other = (Extension)obj;
        return Path.CompareTo(other.Path);
    }

    /// <summary>
    ///     Gets the object extended by this extension
    /// </summary>
    /// <returns>
    ///     The extended object can be an <see cref="Mono.Addins.Description.ExtensionPoint" /> or
    ///     an <see cref="Mono.Addins.Description.ExtensionNodeDescription" />.
    /// </returns>
    /// <remarks>
    ///     This method only works when the add-in description to which the extension belongs has been
    ///     loaded from an add-in registry.
    /// </remarks>
    public ObjectDescription GetExtendedObject()
    {
        var desc = ParentAddinDescription;
        if (desc == null)
            return null;
        var ep = FindExtensionPoint(desc, Path);
        if (ep == null && desc.OwnerDatabase != null)
            foreach (Dependency dep in desc.MainModule.Dependencies)
            {
                var adep = dep as AddinDependency;
                if (adep == null) continue;
                var ad = desc.OwnerDatabase.GetInstalledAddin(ParentAddinDescription.Domain, adep.FullAddinId);
                if (ad != null && ad.Description != null)
                {
                    ep = FindExtensionPoint(ad.Description, Path);
                    if (ep != null)
                        break;
                }
            }

        if (ep != null)
        {
            var subp = Path.Substring(ep.Path.Length).Trim('/');
            if (subp.Length == 0)
                return ep; // The extension is directly extending the extension point

            // The extension is extending a node of the extension point

            return desc.FindExtensionNode(Path, true);
        }

        return null;
    }

    /// <summary>
    ///     Gets the node types allowed in this extension.
    /// </summary>
    /// <returns>
    ///     The allowed node types.
    /// </returns>
    /// <remarks>
    ///     This method only works when the add-in description to which the extension belongs has been
    ///     loaded from an add-in registry.
    /// </remarks>
    public ExtensionNodeTypeCollection GetAllowedNodeTypes()
    {
        var ob = GetExtendedObject();
        var ep = ob as ExtensionPoint;
        if (ep != null)
            return ep.NodeSet.GetAllowedNodeTypes();

        var node = ob as ExtensionNodeDescription;
        if (node != null)
        {
            var nt = node.GetNodeType();
            if (nt != null)
                return nt.GetAllowedNodeTypes();
        }

        return new ExtensionNodeTypeCollection();
    }

    private ExtensionPoint FindExtensionPoint(AddinDescription desc, string path)
    {
        foreach (ExtensionPoint ep in desc.ExtensionPoints)
            if (ep.Path == path || path.StartsWith(ep.Path + "/"))
                return ep;
        return null;
    }

    internal override void Verify(string location, StringCollection errors)
    {
        VerifyNotEmpty(location + "Extension", errors, Path, "path");
        ExtensionNodes.Verify(location + "Extension (" + Path + ")/", errors);

        foreach (ExtensionNodeDescription cnode in ExtensionNodes)
            VerifyNode(location, cnode, errors);
    }

    private void VerifyNode(string location, ExtensionNodeDescription node, StringCollection errors)
    {
        var id = node.GetAttribute("id");
        if (id.Length > 0)
            id = "(" + id + ")";
        if (node.NodeName == "Condition" && node.GetAttribute("id").Length == 0)
            errors.Add(location + node.NodeName + id + ": Missing 'id' attribute in Condition element.");
        if (node.NodeName == "ComplexCondition")
        {
            if (node.ChildNodes.Count > 0)
            {
                VerifyConditionNode(location, node.ChildNodes[0], errors);
                for (var n = 1; n < node.ChildNodes.Count; n++)
                    VerifyNode(location + node.NodeName + id + "/", node.ChildNodes[n], errors);
            }
            else
            {
                errors.Add(location + "ComplexCondition: Missing child condition in ComplexCondition element.");
            }
        }

        foreach (ExtensionNodeDescription cnode in node.ChildNodes)
            VerifyNode(location + node.NodeName + id + "/", cnode, errors);
    }

    private void VerifyConditionNode(string location, ExtensionNodeDescription node, StringCollection errors)
    {
        var nodeName = node.NodeName;
        if (nodeName != "Or" && nodeName != "And" && nodeName != "Not" && nodeName != "Condition")
        {
            errors.Add(location + "ComplexCondition: Invalid condition element: " + nodeName);
            return;
        }

        foreach (ExtensionNodeDescription cnode in node.ChildNodes)
            VerifyConditionNode(location, cnode, errors);
    }

    internal override void SaveXml(XmlElement parent)
    {
        if (Element == null)
        {
            Element = parent.OwnerDocument.CreateElement("Extension");
            parent.AppendChild(Element);
        }

        Element.SetAttribute("path", Path);
        if (nodes != null)
            nodes.SaveXml(Element);
    }

    internal override void Write(BinaryXmlWriter writer)
    {
        writer.WriteValue("path", Path);
        writer.WriteValue("Nodes", ExtensionNodes);
    }

    internal override void Read(BinaryXmlReader reader)
    {
        Path = reader.ReadStringValue("path");
        nodes = (ExtensionNodeDescriptionCollection)reader.ReadValue("Nodes",
            new ExtensionNodeDescriptionCollection(this));
    }
}