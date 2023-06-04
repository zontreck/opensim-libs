using System;
using System.Collections.Generic;
using System.Xml.XPath;

namespace DotNetOpenId.Yadis;

internal class ServiceElement : XrdsNode, IComparable<ServiceElement>
{
    public ServiceElement(XPathNavigator serviceElement, XrdElement parent) :
        base(serviceElement, parent)
    {
    }

    public XrdElement Xrd => (XrdElement)ParentNode;

    public int? Priority
    {
        get
        {
            var n = Node.SelectSingleNode("@priority", XmlNamespaceResolver);
            return n != null ? n.ValueAsInt : null;
        }
    }

    public IEnumerable<UriElement> UriElements
    {
        get
        {
            var uris = new List<UriElement>();
            foreach (XPathNavigator node in Node.Select("xrd:URI", XmlNamespaceResolver))
                uris.Add(new UriElement(node, this));
            uris.Sort();
            return uris;
        }
    }

    public IEnumerable<TypeElement> TypeElements
    {
        get
        {
            foreach (XPathNavigator node in Node.Select("xrd:Type", XmlNamespaceResolver))
                yield return new TypeElement(node, this);
        }
    }

    public string[] TypeElementUris
    {
        get
        {
            var types = Node.Select("xrd:Type", XmlNamespaceResolver);
            var typeUris = new string[types.Count];
            var i = 0;
            foreach (XPathNavigator type in types) typeUris[i++] = type.Value;
            return typeUris;
        }
    }

    public Identifier ProviderLocalIdentifier
    {
        get
        {
            var n = Node.SelectSingleNode("xrd:LocalID", XmlNamespaceResolver)
                    ?? Node.SelectSingleNode("openid10:Delegate", XmlNamespaceResolver);
            return n != null ? n.Value : null;
        }
    }

    #region IComparable<ServiceElement> Members

    public int CompareTo(ServiceElement other)
    {
        if (other == null) return -1;
        if (Priority.HasValue && other.Priority.HasValue) return Priority.Value.CompareTo(other.Priority.Value);

        if (Priority.HasValue)
            return -1;
        if (other.Priority.HasValue)
            return 1;
        return 0;
    }

    #endregion
}