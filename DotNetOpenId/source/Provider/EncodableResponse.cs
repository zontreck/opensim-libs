using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;

namespace DotNetOpenId.Provider;

[DebuggerDisplay("OpenId: {Protocol.Version}")]
internal class EncodableResponse : MarshalByRefObject, IEncodable
{
    private EncodableResponse(Protocol protocol)
    {
        if (protocol == null) throw new ArgumentNullException("protocol");
        Signed = new List<string>();
        Fields = new Dictionary<string, string>();
        Protocol = protocol;
    }

    private EncodableResponse(Protocol protocol, Uri baseRedirectUrl, string preferredAssociationHandle)
        : this(protocol)
    {
        if (baseRedirectUrl == null) throw new ArgumentNullException("baseRedirectUrl");
        RedirectUrl = baseRedirectUrl;
        PreferredAssociationHandle = preferredAssociationHandle;
    }

    public IDictionary<string, string> Fields { get; }
    public List<string> Signed { get; }
    public Protocol Protocol { get; private set; }
    public bool NeedsSigning => Signed.Count > 0;
    public string PreferredAssociationHandle { get; private set; }

    public static EncodableResponse PrepareDirectMessage(Protocol protocol)
    {
        var response = new EncodableResponse(protocol);
        if (protocol.QueryDeclaredNamespaceVersion != null)
            response.Fields.Add(protocol.openidnp.ns, protocol.QueryDeclaredNamespaceVersion);
        return response;
    }

    public static EncodableResponse PrepareIndirectMessage(Protocol protocol, Uri baseRedirectUrl,
        string preferredAssociationHandle)
    {
        var response = new EncodableResponse(protocol, baseRedirectUrl, preferredAssociationHandle);
        if (protocol.QueryDeclaredNamespaceVersion != null)
            response.Fields.Add(protocol.openidnp.ns, protocol.QueryDeclaredNamespaceVersion);
        return response;
    }

    public override string ToString()
    {
        var returnString = string.Format(CultureInfo.CurrentCulture,
            "Response.NeedsSigning = {0}", NeedsSigning);
        foreach (var key in Fields.Keys)
            returnString += Environment.NewLine + string.Format(CultureInfo.CurrentCulture,
                "ResponseField[{0}] = '{1}'", key, Fields[key]);
        return returnString;
    }

    #region IEncodable Members

    public EncodingType EncodingType =>
        RedirectUrl != null ? EncodingType.IndirectMessage : EncodingType.DirectResponse;

    public IDictionary<string, string> EncodedFields
    {
        get
        {
            var nvc = new Dictionary<string, string>();

            foreach (var pair in Fields)
                if (EncodingType == EncodingType.IndirectMessage)
                    nvc.Add(Protocol.openid.Prefix + pair.Key, pair.Value);
                else
                    nvc.Add(pair.Key, pair.Value);

            return nvc;
        }
    }

    public Uri RedirectUrl { get; set; }

    #endregion
}