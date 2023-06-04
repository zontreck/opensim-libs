using System;
using System.Collections.Generic;
using System.Text;
using System.Xml.XPath;

namespace DotNetOpenId.Yadis;

internal class XrdElement : XrdsNode
{
    public XrdElement(XPathNavigator xrdElement, XrdsDocument parent) :
        base(xrdElement, parent)
    {
    }

    public IEnumerable<ServiceElement> Services
    {
        get
        {
            // We should enumerate them in priority order
            var services = new List<ServiceElement>();
            foreach (XPathNavigator node in Node.Select("xrd:Service", XmlNamespaceResolver))
                services.Add(new ServiceElement(node, this));
            services.Sort();
            return services;
        }
    }


    private int XriResolutionStatusCode
    {
        get
        {
            var n = Node.SelectSingleNode("xrd:Status", XmlNamespaceResolver);
            string codeString;
            if (n == null || string.IsNullOrEmpty(codeString = n.GetAttribute("code", "")))
                throw new OpenIdException(Strings.XriResolutionStatusMissing);
            int code;
            if (!int.TryParse(codeString, out code) || code < 100 || code > 399)
                throw new OpenIdException(Strings.XriResolutionStatusMissing);
            return code;
        }
    }

    public bool IsXriResolutionSuccessful => XriResolutionStatusCode == 100;

    public string CanonicalID
    {
        get
        {
            var n = Node.SelectSingleNode("xrd:CanonicalID", XmlNamespaceResolver);
            return n != null ? n.Value : null;
        }
    }

    public bool IsCanonicalIdVerified
    {
        get
        {
            var n = Node.SelectSingleNode("xrd:Status", XmlNamespaceResolver);
            return n != null && string.Equals(n.GetAttribute("cid", ""), "verified", StringComparison.Ordinal);
        }
    }

    /// <summary>
    ///     Returns services for OP Identifiers.
    /// </summary>
    public IEnumerable<ServiceElement> OpenIdProviderIdentifierServices
    {
        get { return searchForServiceTypeUris(p => p.OPIdentifierServiceTypeURI); }
    }

    /// <summary>
    ///     Returns services for Claimed Identifiers.
    /// </summary>
    public IEnumerable<ServiceElement> OpenIdClaimedIdentifierServices
    {
        get { return searchForServiceTypeUris(p => p.ClaimedIdentifierServiceTypeURI); }
    }

    public IEnumerable<ServiceElement> OpenIdRelyingPartyReturnToServices
    {
        get { return searchForServiceTypeUris(p => p.RPReturnToTypeURI); }
    }

    /// <summary>
    ///     An enumeration of all Service/URI elements, sorted in priority order.
    /// </summary>
    public IEnumerable<UriElement> ServiceUris
    {
        get
        {
            foreach (var service in Services)
            foreach (var uri in service.UriElements)
                yield return uri;
        }
    }

    private IEnumerable<ServiceElement> searchForServiceTypeUris(Util.Func<Protocol, string> p)
    {
        var xpath = new StringBuilder();
        xpath.Append("xrd:Service[");
        foreach (var protocol in Protocol.AllVersions)
        {
            var typeUri = p(protocol);
            if (typeUri == null) continue;
            xpath.Append("xrd:Type/text()='");
            xpath.Append(typeUri);
            xpath.Append("' or ");
        }

        xpath.Length -= 4;
        xpath.Append("]");
        var services = new List<ServiceElement>();
        foreach (XPathNavigator service in Node.Select(xpath.ToString(), XmlNamespaceResolver))
            services.Add(new ServiceElement(service, this));
        // Put the services in their own defined priority order
        services.Sort();
        return services;
    }
}