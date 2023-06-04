using System;
using System.Xml.XPath;

namespace DotNetOpenId.Yadis;

internal class UriElement : XrdsNode, IComparable<UriElement>
{
    public UriElement(XPathNavigator uriElement, ServiceElement service) :
        base(uriElement, service)
    {
    }

    public int? Priority
    {
        get
        {
            var n = Node.SelectSingleNode("@priority", XmlNamespaceResolver);
            return n != null ? n.ValueAsInt : null;
        }
    }

    public Uri Uri => new(Node.Value);

    public ServiceElement Service => (ServiceElement)ParentNode;

    #region IComparable<UriElement> Members

    public int CompareTo(UriElement other)
    {
        if (other == null) return -1;
        var compare = Service.CompareTo(other.Service);
        if (compare != 0) return compare;

        if (Priority.HasValue && other.Priority.HasValue) return Priority.Value.CompareTo(other.Priority.Value);

        if (Priority.HasValue)
            return -1;
        if (other.Priority.HasValue)
            return 1;
        return 0;
    }

    #endregion
}