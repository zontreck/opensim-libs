using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using System.Net;

namespace DotNetOpenId.Provider;

/// <summary>
///     A request to confirm the identity of a user.
/// </summary>
/// <remarks>
///     This class handles requests for openid modes checkid_immediate and checkid_setup.
/// </remarks>
[DebuggerDisplay(
    "Mode: {Mode}, IsAuthenticated: {IsAuthenticated}, LocalIdentifier: {LocalIdentifier}, OpenId: {Protocol.Version}")]
internal class CheckIdRequest : AssociatedRequest, IAuthenticationRequest
{
    private Identifier claimedIdentifier;
    private bool? isAuthenticated;
    private bool? isReturnUrlDiscoverable;
    private Identifier localIdentifier;

    [SuppressMessage("Microsoft.Performance", "CA1805:DoNotInitializeUnnecessarily")]
    internal CheckIdRequest(OpenIdProvider provider) : base(provider)
    {
        // handle the mandatory protocol fields
        var mode = Util.GetRequiredArg(Query, Protocol.openid.mode);
        if (Protocol.Args.Mode.checkid_immediate.Equals(mode, StringComparison.Ordinal))
            Immediate = true;
        else if (Protocol.Args.Mode.checkid_setup.Equals(mode, StringComparison.Ordinal))
            Immediate = false; // implied
        else
            throw new OpenIdException(string.Format(CultureInfo.CurrentCulture,
                Strings.InvalidOpenIdQueryParameterValue, Protocol.openid.mode, mode), Query);

        // The spec says claimed_id and identity can both be either present or
        // absent.  But for now we don't have or support extensions that don't
        // use these parameters, so we require them.  In the future that may change.
        if (Protocol.Version.Major >= 2)
            claimedIdentifier = Util.GetRequiredIdentifierArg(Query, Protocol.openid.claimed_id);
        localIdentifier = Util.GetRequiredIdentifierArg(Query, Protocol.openid.identity);
        // The spec says return_to is optional, but what good is authenticating
        // a user if the user won't be sent back?
        ReturnTo = Util.GetRequiredUriArg(Query, Protocol.openid.return_to);
        Realm = Util.GetOptionalRealmArg(Query, Protocol.openid.Realm) ?? ReturnTo;
        AssociationHandle = Util.GetOptionalArg(Query, Protocol.openid.assoc_handle);

        if (!Realm.Contains(ReturnTo))
            throw new OpenIdException(string.Format(CultureInfo.CurrentCulture,
                Strings.ReturnToNotUnderRealm, ReturnTo.AbsoluteUri, Realm), Query);

        if (Protocol.Version.Major >= 2)
            if ((LocalIdentifier == Protocol.ClaimedIdentifierForOPIdentifier) ^
                (ClaimedIdentifier == Protocol.ClaimedIdentifierForOPIdentifier))
                throw new OpenIdException(string.Format(CultureInfo.CurrentCulture,
                        Strings.MatchingArgumentsExpected, Protocol.openid.claimed_id,
                        Protocol.openid.identity, Protocol.ClaimedIdentifierForOPIdentifier),
                    Query);

        if (ClaimedIdentifier == Protocol.ClaimedIdentifierForOPIdentifier &&
            Protocol.ClaimedIdentifierForOPIdentifier != null)
        {
            // Force the OP to deal with identifier_select by nulling out the two identifiers.
            IsDirectedIdentity = true;
            claimedIdentifier = null;
            localIdentifier = null;
        }

        // URL delegation is only detectable from 2.0 RPs, since openid.claimed_id isn't included from 1.0 RPs.
        // If the openid.claimed_id is present, and if it's different than the openid.identity argument, then
        // the RP has discovered a claimed identifier that has delegated authentication to this Provider.
        IsDelegatedIdentifier = ClaimedIdentifier != null && ClaimedIdentifier != LocalIdentifier;
    }

    /// <summary>
    ///     The URL to redirect the user agent to after the authentication attempt.
    ///     This must fall "under" the realm URL.
    /// </summary>
    internal Uri ReturnTo { get; }

    internal override string Mode =>
        Immediate ? Protocol.Args.Mode.checkid_immediate : Protocol.Args.Mode.checkid_setup;

    /// <summary>
    ///     Get the URL to cancel this request.
    /// </summary>
    internal Uri CancelUrl
    {
        get
        {
            if (Immediate)
                throw new InvalidOperationException(
                    "Cancel is not an appropriate response to immediate mode requests.");

            var builder = new UriBuilder(ReturnTo);
            var args = new Dictionary<string, string>();
            args.Add(Protocol.openid.mode, Protocol.Args.Mode.cancel);
            UriUtil.AppendQueryArgs(builder, args);

            return builder.Uri;
        }
    }

    /// <summary>
    ///     Encode this request as a URL to GET.
    ///     Only used in response to immediate auth requests from OpenID 1.x RPs.
    /// </summary>
    internal Uri SetupUrl
    {
        get
        {
            if (Protocol.Version.Major >= 2)
            {
                Debug.Fail("This property only applicable to OpenID 1.x RPs.");
                throw new InvalidOperationException();
            }

            Debug.Assert(Provider.Endpoint != null, "The OpenIdProvider should have guaranteed this.");
            var q = new Dictionary<string, string>();

            q.Add(Protocol.openid.mode, Protocol.Args.Mode.checkid_setup);
            q.Add(Protocol.openid.identity, LocalIdentifier.ToString());
            q.Add(Protocol.openid.return_to, ReturnTo.AbsoluteUri);

            if (Realm != null)
                q.Add(Protocol.openid.Realm, Realm);

            if (AssociationHandle != null)
                q.Add(Protocol.openid.assoc_handle, AssociationHandle);

            var builder = new UriBuilder(Provider.Endpoint);
            UriUtil.AppendQueryArgs(builder, q);

            return builder.Uri;
        }
    }

    /// <summary>
    ///     Gets/sets whether the provider has determined that the
    ///     <see cref="ClaimedIdentifier" /> belongs to the currently logged in user
    ///     and wishes to share this information with the consumer.
    /// </summary>
    public bool? IsAuthenticated
    {
        get => isAuthenticated;
        set
        {
            isAuthenticated = value;
            InvalidateResponse();
        }
    }

    /// <summary>
    ///     Whether the consumer demands an immediate response.
    ///     If false, the consumer is willing to wait for the identity provider
    ///     to authenticate the user.
    /// </summary>
    public bool Immediate { get; }

    /// <summary>
    ///     The URL the consumer site claims to use as its 'base' address.
    /// </summary>
    public Realm Realm { get; }

    /// <summary>
    ///     Whether verification of the return URL claimed by the Relying Party
    ///     succeeded.
    /// </summary>
    /// <remarks>
    ///     This property will never throw a WebException or OpenIdException.  Any failures
    ///     occuring during return URL verification results in a false value being returned.
    ///     Details regarding failure may be found in the trace log.
    /// </remarks>
    public bool IsReturnUrlDiscoverable
    {
        get
        {
            Debug.Assert(Realm != null);
            if (!isReturnUrlDiscoverable.HasValue)
            {
                isReturnUrlDiscoverable = false; // assume not until we succeed
                try
                {
                    foreach (var returnUrl in Realm.Discover(false))
                    {
                        Realm discoveredReturnToUrl = returnUrl.RelyingPartyEndpoint;
                        // The spec requires that the return_to URLs given in an RPs XRDS doc
                        // do not contain wildcards.
                        if (discoveredReturnToUrl.DomainWildcard)
                        {
                            Logger.WarnFormat(
                                "Realm {0} contained return_to URL {1} which contains a wildcard, which is not allowed.",
                                Realm, discoveredReturnToUrl);
                            continue;
                        }

                        // Use the same rules as return_to/realm matching to check whether this
                        // URL fits the return_to URL we were given.
                        if (discoveredReturnToUrl.Contains(ReturnTo))
                        {
                            isReturnUrlDiscoverable = true;
                            break; // no need to keep looking after we find a match
                        }
                    }
                }
                catch (OpenIdException ex)
                {
                    Logger.InfoFormat("Relying party discovery at URL {0} failed.  {1}",
                        Realm, ex);
                    // Don't do anything else.  We quietly fail at return_to verification and return false.
                }
                catch (WebException ex)
                {
                    Logger.InfoFormat("Relying party discovery at URL {0} failed.  {1}",
                        Realm, ex);
                    // Don't do anything else.  We quietly fail at return_to verification and return false.
                }
            }

            return isReturnUrlDiscoverable.Value;
        }
    }

    /// <summary>
    ///     Whether the Provider should help the user select a Claimed Identifier
    ///     to send back to the relying party.
    /// </summary>
    public bool IsDirectedIdentity { get; }

    /// <summary>
    ///     A value indicating whether the requesting Relying Party is using a delegated URL.
    /// </summary>
    /// <remarks>
    ///     When delegated identifiers are used, the <see cref="ClaimedIdentifier" /> should not
    ///     be changed at the Provider during authentication.
    ///     Delegation is only detectable on requests originating from OpenID 2.0 relying parties.
    ///     A relying party implementing only OpenID 1.x may use delegation and this property will
    ///     return false anyway.
    /// </remarks>
    public bool IsDelegatedIdentifier { get; }

    /// <summary>
    ///     The user identifier used by this particular provider.
    /// </summary>
    public Identifier LocalIdentifier
    {
        get => localIdentifier;
        set
        {
            // Keep LocalIdentifier and ClaimedIdentifier in sync for directed identity.
            if (IsDirectedIdentity)
            {
                if (ClaimedIdentifier != null && ClaimedIdentifier != value)
                    throw new InvalidOperationException(Strings.IdentifierSelectRequiresMatchingIdentifiers);

                claimedIdentifier = value;
            }

            localIdentifier = value;
        }
    }

    /// <summary>
    ///     The identifier this user is claiming to control.
    /// </summary>
    public Identifier ClaimedIdentifier
    {
        get => claimedIdentifier;
        set
        {
            // Keep LocalIdentifier and ClaimedIdentifier in sync for directed identity.
            if (IsDirectedIdentity)
            {
                if (LocalIdentifier != null && LocalIdentifier != value)
                    throw new InvalidOperationException(Strings.IdentifierSelectRequiresMatchingIdentifiers);

                localIdentifier = value;
            }

            if (IsDelegatedIdentifier)
                throw new InvalidOperationException(Strings.ClaimedIdentifierCannotBeSetOnDelegatedAuthentication);

            claimedIdentifier = value;
        }
    }

    /// <summary>
    ///     Adds an optional fragment (#fragment) portion to a URI ClaimedIdentifier.
    ///     Useful for identifier recycling.
    /// </summary>
    /// <param name="fragment">
    ///     Should not include the # prefix character as that will be added internally.
    ///     May be null or the empty string to clear a previously set fragment.
    /// </param>
    /// <remarks>
    ///     <para>
    ///         Unlike the <see cref="ClaimedIdentifier" /> property, which can only be set if
    ///         using directed identity, this method can be called on any URI claimed identifier.
    ///     </para>
    ///     <para>
    ///         Because XRI claimed identifiers (the canonical IDs) are never recycled,
    ///         this method should<i>not</i> be called for XRIs.
    ///     </para>
    /// </remarks>
    /// <exception cref="InvalidOperationException">
    ///     Thrown when this method is called on an XRI, or on a directed identity request
    ///     before the <see cref="ClaimedIdentifier" /> property is set.
    /// </exception>
    public void SetClaimedIdentifierFragment(string fragment)
    {
        if (IsDirectedIdentity && ClaimedIdentifier == null)
            throw new InvalidOperationException(Strings.ClaimedIdentifierMustBeSetFirst);
        if (ClaimedIdentifier is XriIdentifier) throw new InvalidOperationException(Strings.FragmentNotAllowedOnXRIs);

        var builder = new UriBuilder(ClaimedIdentifier);
        builder.Fragment = fragment;
        claimedIdentifier = builder.Uri;
    }

    /// <summary>
    ///     Indicates whether this request has all the information necessary to formulate a response.
    /// </summary>
    public override bool IsResponseReady =>
        // The null checks on the identifiers is to make sure that an identifier_select
        // has been resolved to actual identifiers.
        IsAuthenticated.HasValue &&
        (!IsAuthenticated.Value || !IsDirectedIdentity || (LocalIdentifier != null && ClaimedIdentifier != null));

    protected override IEncodable CreateResponse()
    {
        Debug.Assert(IsAuthenticated.HasValue, "This should be checked internally before CreateResponse is called.");
        return AssertionMessage.CreateAssertion(this);
    }

    public override string ToString()
    {
        var returnString = @"
CheckIdRequest.Immediate = '{0}'
CheckIdRequest.Realm = '{1}'
CheckIdRequest.Identity = '{2}' 
CheckIdRequest._mode = '{3}' 
CheckIdRequest.ReturnTo = '{4}' 
";

        return base.ToString() + string.Format(CultureInfo.CurrentCulture,
            returnString, Immediate, Realm, LocalIdentifier, Mode, ReturnTo);
    }
}