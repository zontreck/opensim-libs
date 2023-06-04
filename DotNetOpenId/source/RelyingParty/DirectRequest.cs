using System;
using System.Collections.Generic;
using System.Diagnostics;

namespace DotNetOpenId.RelyingParty;

[DebuggerDisplay("OpenId: {Protocol.Version}")]
internal abstract class DirectRequest
{
    protected DirectRequest(OpenIdRelyingParty relyingParty, ServiceEndpoint provider, IDictionary<string, string> args)
    {
        if (relyingParty == null) throw new ArgumentNullException("relyingParty");
        if (provider == null) throw new ArgumentNullException("provider");
        if (args == null) throw new ArgumentNullException("args");
        RelyingParty = relyingParty;
        Provider = provider;
        Args = args;
        if (Protocol.QueryDeclaredNamespaceVersion != null &&
            !Args.ContainsKey(Protocol.openid.ns))
            Args.Add(Protocol.openid.ns, Protocol.QueryDeclaredNamespaceVersion);
    }

    protected ServiceEndpoint Provider { get; }
    protected Protocol Protocol => Provider.Protocol;
    protected internal IDictionary<string, string> Args { get; }
    protected OpenIdRelyingParty RelyingParty { get; }

    protected IDictionary<string, string> GetResponse()
    {
        Logger.DebugFormat("Sending direct message to {0}: {1}{2}", Provider.ProviderEndpoint,
            Environment.NewLine, Util.ToString(Args));
        return RelyingParty.DirectMessageChannel.SendDirectMessageAndGetResponse(Provider, Args);
    }
}