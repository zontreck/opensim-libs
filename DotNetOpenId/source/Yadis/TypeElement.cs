using System.Xml.XPath;

namespace DotNetOpenId.Yadis;

internal class TypeElement : XrdsNode
{
    public TypeElement(XPathNavigator typeElement, ServiceElement parent) :
        base(typeElement, parent)
    {
    }

    public string Uri => Node.Value;
}