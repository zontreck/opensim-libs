using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using Org.Mentalis.Security.Cryptography;

namespace DotNetOpenId.RelyingParty;

internal class AssociateResponse : DirectResponse
{
    public AssociateResponse(OpenIdRelyingParty relyingParty, ServiceEndpoint provider,
        IDictionary<string, string> args, DiffieHellman dh)
        : base(relyingParty, provider, args)
    {
        DH = dh;

        if (Args.ContainsKey(Protocol.openidnp.assoc_handle))
        {
            initializeAssociation();
        }
        else
        {
            // Attempt to recover from an unsupported assoc_type
            if (Protocol.Version.Major >= 2)
                if (Util.GetRequiredArg(Args, Protocol.openidnp.error_code) == Protocol.Args.ErrorCode.UnsupportedType)
                {
                    var assoc_type = Util.GetRequiredArg(Args, Protocol.openidnp.assoc_type);
                    var session_type = Util.GetRequiredArg(Args, Protocol.openidnp.session_type);
                    // If the suggested options are among those we support...
                    if (Array.IndexOf(Protocol.Args.SignatureAlgorithm.All, assoc_type) >= 0 &&
                        Array.IndexOf(Protocol.Args.SessionType.All, session_type) >= 0 &&
                        RelyingParty.Settings.IsAssociationInPermittedRange(Protocol, assoc_type))
                        SecondAttempt =
                            AssociateRequest.Create(RelyingParty, Provider, assoc_type, session_type, false);
                }
        }
    }

    public DiffieHellman DH { get; }

    [SuppressMessage("Microsoft.Performance", "CA1820:TestForEmptyStringsUsingStringLength")]
    public Association Association { get; private set; }

    /// <summary>
    ///     A custom-made associate request to try again when an OP
    ///     doesn't support the settings we suggested, but we support
    ///     the ones the OP suggested.
    /// </summary>
    public AssociateRequest SecondAttempt { get; private set; }

    private void initializeAssociation()
    {
        var assoc_type = Util.GetRequiredArg(Args, Protocol.openidnp.assoc_type);
        if (Array.IndexOf(Protocol.Args.SignatureAlgorithm.All, assoc_type) >= 0)
        {
            byte[] secret;

            string session_type;
            if (!Args.TryGetValue(Protocol.openidnp.session_type, out session_type) ||
                Protocol.Args.SessionType.NoEncryption.Equals(session_type, StringComparison.Ordinal))
                secret = getDecoded(Protocol.openidnp.mac_key);
            else
                try
                {
                    var dh_server_public = getDecoded(Protocol.openidnp.dh_server_public);
                    var enc_mac_key = getDecoded(Protocol.openidnp.enc_mac_key);
                    secret = DiffieHellmanUtil.SHAHashXorSecret(DiffieHellmanUtil.Lookup(Protocol, session_type), DH,
                        dh_server_public, enc_mac_key);
                }
                catch (ArgumentException ex)
                {
                    throw new OpenIdException(string.Format(CultureInfo.CurrentCulture,
                        Strings.InvalidOpenIdQueryParameterValue,
                        Protocol.openid.session_type, session_type), ex);
                }

            var assocHandle = Util.GetRequiredArg(Args, Protocol.openidnp.assoc_handle);
            var expiresIn = new TimeSpan(0, 0,
                Convert.ToInt32(Util.GetRequiredArg(Args, Protocol.openidnp.expires_in), CultureInfo.InvariantCulture));

            try
            {
                Association = HmacShaAssociation.Create(Protocol, assoc_type,
                    assocHandle, secret, expiresIn);
            }
            catch (ArgumentException ex)
            {
                throw new OpenIdException(string.Format(CultureInfo.CurrentCulture,
                    Strings.InvalidOpenIdQueryParameterValue,
                    Protocol.openid.assoc_type, assoc_type), ex);
            }
        }
        else
        {
            throw new OpenIdException(string.Format(CultureInfo.CurrentCulture,
                Strings.InvalidOpenIdQueryParameterValue,
                Protocol.openid.assoc_type, assoc_type));
        }
    }

    private byte[] getDecoded(string key)
    {
        try
        {
            return Convert.FromBase64String(Util.GetRequiredArg(Args, key));
        }
        catch (FormatException ex)
        {
            throw new OpenIdException(string.Format(CultureInfo.CurrentCulture,
                Strings.ExpectedBase64OpenIdQueryParameter, key), null, ex);
        }
    }
}