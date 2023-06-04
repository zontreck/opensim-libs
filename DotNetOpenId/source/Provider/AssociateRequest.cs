using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Globalization;

namespace DotNetOpenId.Provider;

/// <summary>
///     A request to establish an association.
/// </summary>
[DebuggerDisplay("Mode: {Mode}, AssocType: {assoc_type}, Session: {session.SessionType}, OpenId: {Protocol.Version}")]
internal class AssociateRequest : Request
{
    private string assoc_type;
    private ProviderSession session;

    public AssociateRequest(OpenIdProvider provider)
        : base(provider)
    {
        session = ProviderSession.CreateSession(provider);
        assoc_type = Util.GetRequiredArg(Query, Protocol.openid.assoc_type);
        if (Array.IndexOf(Protocol.Args.SignatureAlgorithm.All, assoc_type) < 0)
            throw new OpenIdException(string.Format(CultureInfo.CurrentCulture,
                Strings.InvalidOpenIdQueryParameterValue,
                Protocol.openid.assoc_type, assoc_type), provider.Query)
            {
                ExtraArgsToReturn = CreateAssociationTypeHints(provider)
            };
    }

    public override bool IsResponseReady =>
        // This type of request can always be responded to immediately.
        true;

    /// <summary>
    ///     Returns the string "associate".
    /// </summary>
    internal override string Mode => Protocol.Args.Mode.associate;

    /// <summary>
    ///     This method is used to throw a carefully crafted exception that will end up getting
    ///     encoded as a response to the RP, given hints as to what
    ///     assoc_type and session_type args we support.
    /// </summary>
    /// <returns>
    ///     A dictionary that should be passed to the OpenIdException
    ///     via the <see cref="OpenIdException.ExtraArgsToReturn" /> property.
    /// </returns>
    internal static IDictionary<string, string> CreateAssociationTypeHints(
        OpenIdProvider provider)
    {
        var protocol = provider.Protocol;
        return new Dictionary<string, string>
        {
            { protocol.openidnp.error_code, protocol.Args.ErrorCode.UnsupportedType },
            { protocol.openidnp.session_type, protocol.Args.SessionType.DH_SHA1 },
            { protocol.openidnp.assoc_type, protocol.Args.SignatureAlgorithm.HMAC_SHA1 }
        };
    }

    /// <summary>
    ///     Respond to this request with an association.
    /// </summary>
    [SuppressMessage("Microsoft.Performance", "CA1820:TestForEmptyStringsUsingStringLength")]
    public EncodableResponse Answer()
    {
        var assoc = Provider.Signatory.CreateAssociation(AssociationRelyingPartyType.Smart, Provider);
        var response = EncodableResponse.PrepareDirectMessage(Protocol);

        response.Fields[Protocol.openidnp.expires_in] =
            assoc.SecondsTillExpiration.ToString(CultureInfo.InvariantCulture);
        response.Fields[Protocol.openidnp.assoc_type] = assoc.GetAssociationType(Protocol);
        response.Fields[Protocol.openidnp.assoc_handle] = assoc.Handle;
        response.Fields[Protocol.openidnp.session_type] = session.SessionType;

        IDictionary<string, string> nvc = session.Answer(assoc.SecretKey);
        foreach (var pair in nvc) response.Fields[pair.Key] = nvc[pair.Key];

        Logger.InfoFormat("Association {0} created.", assoc.Handle);

        return response;
    }

    protected override IEncodable CreateResponse()
    {
        return Answer();
    }

    public override string ToString()
    {
        var returnString = "AssociateRequest._assoc_type = {0}";
        return base.ToString() + Environment.NewLine + string.Format(CultureInfo.CurrentCulture,
            returnString, assoc_type);
    }
}