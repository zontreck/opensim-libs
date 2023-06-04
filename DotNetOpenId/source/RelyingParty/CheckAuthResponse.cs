using System;
using System.Collections.Generic;
using System.Diagnostics;

namespace DotNetOpenId.RelyingParty;

[DebuggerDisplay("IsAuthenticationValid: {IsAuthenticationValid}, OpenId: {Protocol.Version}")]
internal class CheckAuthResponse : DirectResponse
{
    public CheckAuthResponse(OpenIdRelyingParty relyingParty, ServiceEndpoint provider,
        IDictionary<string, string> args)
        : base(relyingParty, provider, args)
    {
    }

    public string InvalidatedAssociationHandle
    {
        get
        {
            if (IsAuthenticationValid) return Util.GetOptionalArg(Args, Protocol.openidnp.invalidate_handle);
            return null;
        }
    }

    public bool IsAuthenticationValid =>
        Protocol.Args.IsValid.True.Equals(
            Util.GetRequiredArg(Args, Protocol.openidnp.is_valid), StringComparison.Ordinal);
}