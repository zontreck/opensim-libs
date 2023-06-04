//
// ExtensionNodeDescription.cs
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
///     An extension node definition.
/// </summary>
public class ExtensionNodeDescription : ObjectDescription, NodeElement
{
    private string[] attributes;
    private ExtensionNodeDescriptionCollection childNodes;
    private string nodeName;

    /// <summary>
    ///     Initializes a new instance of the <see cref="Mono.Addins.Description.ExtensionNodeDescription" /> class.
    /// </summary>
    /// <param name='nodeName'>
    ///     Node name.
    /// </param>
    public ExtensionNodeDescription(string nodeName)
    {
        this.nodeName = nodeName;
    }

    internal ExtensionNodeDescription(XmlElement elem)
    {
        Element = elem;
        nodeName = elem.LocalName;
    }

    internal ExtensionNodeDescription()
    {
    }

    /// <summary>
    ///     Gets or sets the identifier of the node.
    /// </summary>
    /// <value>
    ///     The identifier.
    /// </value>
    public string Id
    {
        get => GetAttribute("id");
        set => SetAttribute("id", value);
    }

    /// <summary>
    ///     Gets or sets the identifier of the node after which this node has to be inserted
    /// </summary>
    /// <value>
    ///     The identifier of the reference node
    /// </value>
    public string InsertAfter
    {
        get => GetAttribute("insertafter");
        set
        {
            if (value == null || value.Length == 0)
                RemoveAttribute("insertafter");
            else
                SetAttribute("insertafter", value);
        }
    }

    /// <summary>
    ///     Gets or sets the identifier of the node before which this node has to be inserted
    /// </summary>
    /// <value>
    ///     The identifier of the reference node
    /// </value>
    public string InsertBefore
    {
        get => GetAttribute("insertbefore");
        set
        {
            if (value == null || value.Length == 0)
                RemoveAttribute("insertbefore");
            else
                SetAttribute("insertbefore", value);
        }
    }

    /// <summary>
    ///     Gets a value indicating whether this node is a condition.
    /// </summary>
    /// <value>
    ///     <c>true</c> if this node is a condition; otherwise, <c>false</c>.
    /// </value>
    public bool IsCondition => nodeName == "Condition" || nodeName == "ComplexCondition";

    /// <summary>
    ///     Gets the child nodes.
    /// </summary>
    /// <value>
    ///     The child nodes.
    /// </value>
    public ExtensionNodeDescriptionCollection ChildNodes
    {
        get
        {
            if (childNodes == null)
            {
                childNodes = new ExtensionNodeDescriptionCollection(this);
                if (Element != null)
                    foreach (XmlNode nod in Element.ChildNodes)
                        if (nod is XmlElement)
                            childNodes.Add(new ExtensionNodeDescription((XmlElement)nod));
            }

            return childNodes;
        }
    }

    /// <summary>
    ///     Gets or sets the name of the node.
    /// </summary>
    /// <value>
    ///     The name of the node.
    /// </value>
    public string NodeName
    {
        get => nodeName;
        internal set
        {
            if (Element != null)
                throw new InvalidOperationException("Can't change node name of xml element");
            nodeName = value;
        }
    }

    /// <summary>
    ///     Gets the value of an attribute.
    /// </summary>
    /// <returns>
    ///     The value of the attribute, or an empty string if the attribute is not defined.
    /// </returns>
    /// <param name='key'>
    ///     Name of the attribute.
    /// </param>
    public string GetAttribute(string key)
    {
        if (Element != null)
            return Element.GetAttribute(key);

        if (attributes == null)
            return string.Empty;
        for (var n = 0; n < attributes.Length; n += 2)
            if (attributes[n] == key)
                return attributes[n + 1];
        return string.Empty;
    }

    /// <summary>
    ///     Gets the attributes of the node.
    /// </summary>
    /// <value>
    ///     The attributes.
    /// </value>
    public NodeAttribute[] Attributes
    {
        get
        {
            var result = SaveXmlAttributes();
            if (result == null || result.Length == 0)
                return new NodeAttribute [0];
            var ats = new NodeAttribute [result.Length / 2];
            for (var n = 0; n < ats.Length; n++)
            {
                var at = new NodeAttribute();
                at.name = result[n * 2];
                at.value = result[n * 2 + 1];
                ats[n] = at;
            }

            return ats;
        }
    }

    NodeElementCollection NodeElement.ChildNodes => ChildNodes;

    /// <summary>
    ///     Gets the type of the node.
    /// </summary>
    /// <returns>
    ///     The node type.
    /// </returns>
    /// <remarks>
    ///     This method only works when the add-in description to which the node belongs has been
    ///     loaded from an add-in registry.
    /// </remarks>
    public ExtensionNodeType GetNodeType()
    {
        if (Parent is Extension)
        {
            var ext = (Extension)Parent;
            object ob = ext.GetExtendedObject();
            if (ob is ExtensionPoint)
            {
                var ep = (ExtensionPoint)ob;
                return ep.NodeSet.GetAllowedNodeTypes()[NodeName];
            }

            if (ob is ExtensionNodeDescription)
            {
                var pn = (ExtensionNodeDescription)ob;
                var pt = pn.GetNodeType();
                if (pt != null)
                    return pt.GetAllowedNodeTypes()[NodeName];
            }
        }
        else if (Parent is ExtensionNodeDescription)
        {
            var pt = ((ExtensionNodeDescription)Parent).GetNodeType();
            if (pt != null)
                return pt.GetAllowedNodeTypes()[NodeName];
        }

        return null;
    }

    /// <summary>
    ///     Gets the extension path under which this node is registered
    /// </summary>
    /// <returns>
    ///     The parent path.
    /// </returns>
    /// <remarks>
    ///     For example, if the id of the node is 'ThisNode', and the node is a child of another node with id 'ParentNode', and
    ///     that parent node is defined in an extension with the path '/Core/MainExtension', then the parent path is
    ///     'Core/MainExtension/ParentNode'.
    /// </remarks>
    public string GetParentPath()
    {
        if (Parent is Extension) return ((Extension)Parent).Path;

        if (Parent is ExtensionNodeDescription)
        {
            var pn = (ExtensionNodeDescription)Parent;
            return pn.GetParentPath() + "/" + pn.Id;
        }

        return string.Empty;
    }

    internal override void Verify(string location, StringCollection errors)
    {
        if (nodeName == null || nodeName.Length == 0)
            errors.Add(location + "Node: NodeName can't be empty.");
        ChildNodes.Verify(location + NodeName + "/", errors);
    }

    internal override void SaveXml(XmlElement parent)
    {
        CreateElement(parent, nodeName);

        if (attributes != null)
            for (var n = 0; n < attributes.Length; n += 2)
                Element.SetAttribute(attributes[n], attributes[n + 1]);

        ChildNodes.SaveXml(Element);
    }

    /// <summary>
    ///     Sets the value of an attribute.
    /// </summary>
    /// <param name='key'>
    ///     Name of the attribute
    /// </param>
    /// <param name='value'>
    ///     The value.
    /// </param>
    public void SetAttribute(string key, string value)
    {
        if (Element != null)
        {
            Element.SetAttribute(key, value);
            return;
        }

        if (value == null)
            value = string.Empty;

        if (attributes == null)
        {
            attributes = new string [2];
            attributes[0] = key;
            attributes[1] = value;
            return;
        }

        for (var n = 0; n < attributes.Length; n += 2)
            if (attributes[n] == key)
            {
                attributes[n + 1] = value;
                return;
            }

        var newList = new string [attributes.Length + 2];
        attributes.CopyTo(newList, 0);
        attributes = newList;
        attributes[attributes.Length - 2] = key;
        attributes[attributes.Length - 1] = value;
    }

    /// <summary>
    ///     Removes an attribute.
    /// </summary>
    /// <param name='name'>
    ///     Name of the attribute to remove.
    /// </param>
    public void RemoveAttribute(string name)
    {
        if (Element != null)
        {
            Element.RemoveAttribute(name);
            return;
        }

        if (attributes == null)
            return;

        for (var n = 0; n < attributes.Length; n += 2)
            if (attributes[n] == name)
            {
                var newar = new string [attributes.Length - 2];
                Array.Copy(attributes, 0, newar, 0, n);
                Array.Copy(attributes, n + 2, newar, n, attributes.Length - n - 2);
                attributes = newar;
                break;
            }
    }

    private string[] SaveXmlAttributes()
    {
        if (Element != null)
        {
            var result = new string [Element.Attributes.Count * 2];
            for (var n = 0; n < result.Length; n += 2)
            {
                var at = Element.Attributes[n / 2];
                result[n] = at.LocalName;
                result[n + 1] = at.Value;
            }

            return result;
        }

        return attributes;
    }

    internal override void Write(BinaryXmlWriter writer)
    {
        writer.WriteValue("nodeName", nodeName);
        writer.WriteValue("attributes", SaveXmlAttributes());
        writer.WriteValue("ChildNodes", ChildNodes);
    }

    internal override void Read(BinaryXmlReader reader)
    {
        nodeName = reader.ReadStringValue("nodeName");
        attributes = (string[])reader.ReadValue("attributes");
        childNodes =
            (ExtensionNodeDescriptionCollection)reader.ReadValue("ChildNodes",
                new ExtensionNodeDescriptionCollection(this));
    }
}