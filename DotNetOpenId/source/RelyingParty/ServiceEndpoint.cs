using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Text;
using DotNetOpenId.Extensions;

namespace DotNetOpenId.RelyingParty;

/// <summary>
///     Represents information discovered about a user-supplied Identifier.
/// </summary>
[DebuggerDisplay(
    "ClaimedIdentifier: {ClaimedIdentifier}, ProviderEndpoint: {ProviderEndpoint}, OpenId: {Protocol.Version}")]
internal class ServiceEndpoint : IXrdsProviderEndpoint
{
    private string friendlyIdentifierForDisplay;

    private Protocol protocol;

    private ServiceEndpoint(Identifier claimedIdentifier, Identifier userSuppliedIdentifier,
        Uri providerEndpoint, Identifier providerLocalIdentifier,
        string[] providerSupportedServiceTypeUris, int? servicePriority, int? uriPriority)
    {
        if (claimedIdentifier == null) throw new ArgumentNullException("claimedIdentifier");
        if (providerEndpoint == null) throw new ArgumentNullException("providerEndpoint");
        if (providerSupportedServiceTypeUris == null)
            throw new ArgumentNullException("providerSupportedServiceTypeUris");
        ClaimedIdentifier = claimedIdentifier;
        UserSuppliedIdentifier = userSuppliedIdentifier;
        ProviderEndpoint = providerEndpoint;
        ProviderLocalIdentifier = providerLocalIdentifier ?? claimedIdentifier;
        ProviderSupportedServiceTypeUris = providerSupportedServiceTypeUris;
        this.servicePriority = servicePriority;
        this.uriPriority = uriPriority;
    }

    /// <summary>
    ///     Used for deserializing <see cref="ServiceEndpoint" /> from authentication responses.
    /// </summary>
    private ServiceEndpoint(Identifier claimedIdentifier, Identifier userSuppliedIdentifier,
        Uri providerEndpoint, Identifier providerLocalIdentifier, Protocol protocol)
    {
        ClaimedIdentifier = claimedIdentifier;
        UserSuppliedIdentifier = userSuppliedIdentifier;
        ProviderEndpoint = providerEndpoint;
        ProviderLocalIdentifier = providerLocalIdentifier ?? claimedIdentifier;
        this.protocol = protocol;
    }

    /// <summary>
    ///     The URL which accepts OpenID Authentication protocol messages.
    /// </summary>
    /// <remarks>
    ///     Obtained by performing discovery on the User-Supplied Identifier.
    ///     This value MUST be an absolute HTTP or HTTPS URL.
    /// </remarks>
    public Uri ProviderEndpoint { get; private set; }

    /// <summary>
    ///     Returns true if the <see cref="ProviderEndpoint" /> is using an encrypted channel.
    /// </summary>
    internal bool IsSecure =>
        string.Equals(ProviderEndpoint.Scheme, Uri.UriSchemeHttps, StringComparison.OrdinalIgnoreCase);

    /*
    /// <summary>
    /// An Identifier for an OpenID Provider.
    /// </summary>
    public Identifier ProviderIdentifier { get; private set; }
    */
    /// <summary>
    ///     An Identifier that was presented by the end user to the Relying Party,
    ///     or selected by the user at the OpenID Provider.
    ///     During the initiation phase of the protocol, an end user may enter
    ///     either their own Identifier or an OP Identifier. If an OP Identifier
    ///     is used, the OP may then assist the end user in selecting an Identifier
    ///     to share with the Relying Party.
    /// </summary>
    public Identifier UserSuppliedIdentifier { get; }

    /// <summary>
    ///     The Identifier that the end user claims to own.
    /// </summary>
    public Identifier ClaimedIdentifier { get; private set; }

    /// <summary>
    ///     An alternate Identifier for an end user that is local to a
    ///     particular OP and thus not necessarily under the end user's
    ///     control.
    /// </summary>
    public Identifier ProviderLocalIdentifier { get; }

    /// <summary>
    ///     Supports the <see cref="IAuthenticationResponse.FriendlyIdentifierForDisplay" /> property.
    /// </summary>
    public string FriendlyIdentifierForDisplay
    {
        get
        {
            if (friendlyIdentifierForDisplay == null)
            {
                var xri = ClaimedIdentifier as XriIdentifier;
                var uri = ClaimedIdentifier as UriIdentifier;
                if (xri != null)
                {
                    if (UserSuppliedIdentifier == null || string.Equals(UserSuppliedIdentifier, ClaimedIdentifier,
                            StringComparison.OrdinalIgnoreCase))
                        friendlyIdentifierForDisplay = ClaimedIdentifier;
                    else
                        friendlyIdentifierForDisplay = UserSuppliedIdentifier;
                }
                else if (uri != null)
                {
                    if (uri != Protocol.ClaimedIdentifierForOPIdentifier)
                    {
                        var displayUri = uri.Uri.Authority + uri.Uri.PathAndQuery;
                        displayUri = displayUri.TrimEnd('/');
                        // Multi-byte unicode characters get encoded by the Uri class for transit.
                        // Since this is for display purposes, we want to reverse this and display a readable
                        // representation of these foreign characters.  
                        friendlyIdentifierForDisplay = Uri.UnescapeDataString(displayUri);
                    }
                }
                else
                {
                    Debug.Fail("Doh!  We never should have reached here.");
                    friendlyIdentifierForDisplay = ClaimedIdentifier;
                }
            }

            return friendlyIdentifierForDisplay;
        }
    }

    /// <summary>
    ///     Gets the list of services available at this OP Endpoint for the
    ///     claimed Identifier.  May be null.
    /// </summary>
    public string[] ProviderSupportedServiceTypeUris { get; }

    /// <summary>
    ///     Gets the OpenID protocol used by the Provider.
    /// </summary>
    public Protocol Protocol
    {
        get
        {
            if (protocol == null) protocol = Protocol.Detect(ProviderSupportedServiceTypeUris);
            if (protocol != null) return protocol;
            throw new InvalidOperationException("Unable to determine the version of OpenID the Provider supports.");
        }
    }

    Uri IProviderEndpoint.Uri => ProviderEndpoint;

    public bool IsTypeUriPresent(string typeUri)
    {
        return IsExtensionSupported(typeUri);
    }

    public bool IsExtensionSupported<T>() where T : IExtension, new()
    {
        var extension = new T();
        return IsExtensionSupported(extension);
    }

    public bool IsExtensionSupported(Type extensionType)
    {
        if (extensionType == null) throw new ArgumentNullException("extensionType");
        if (!typeof(IExtension).IsAssignableFrom(extensionType))
            throw new ArgumentException(string.Format(CultureInfo.CurrentCulture,
                    Strings.TypeMustImplementX, typeof(IExtension).FullName),
                "extensionType");
        var extension = (IExtension)Activator.CreateInstance(extensionType);
        return IsExtensionSupported(extension);
    }

    Version IProviderEndpoint.Version => Protocol.Version;

    internal static ServiceEndpoint CreateForProviderIdentifier(
        Identifier providerIdentifier, Uri providerEndpoint,
        string[] providerSupportedServiceTypeUris, int? servicePriority, int? uriPriority)
    {
        var protocol = Protocol.Detect(providerSupportedServiceTypeUris);

        return new ServiceEndpoint(protocol.ClaimedIdentifierForOPIdentifier, providerIdentifier,
            providerEndpoint, protocol.ClaimedIdentifierForOPIdentifier,
            providerSupportedServiceTypeUris, servicePriority, uriPriority);
    }

    internal static ServiceEndpoint CreateForClaimedIdentifier(
        Identifier claimedIdentifier, Identifier providerLocalIdentifier,
        Uri providerEndpoint,
        string[] providerSupportedServiceTypeUris, int? servicePriority, int? uriPriority)
    {
        return CreateForClaimedIdentifier(claimedIdentifier, null, providerLocalIdentifier,
            providerEndpoint, providerSupportedServiceTypeUris, servicePriority, uriPriority);
    }

    internal static ServiceEndpoint CreateForClaimedIdentifier(
        Identifier claimedIdentifier, Identifier userSuppliedIdentifier, Identifier providerLocalIdentifier,
        Uri providerEndpoint,
        string[] providerSupportedServiceTypeUris, int? servicePriority, int? uriPriority)
    {
        return new ServiceEndpoint(claimedIdentifier, userSuppliedIdentifier, providerEndpoint,
            providerLocalIdentifier, providerSupportedServiceTypeUris, servicePriority, uriPriority);
    }

    public bool IsExtensionSupported(string extensionUri)
    {
        if (ProviderSupportedServiceTypeUris == null)
            throw new InvalidOperationException("Cannot lookup extension support on a rehydrated ServiceEndpoint.");
        return Array.IndexOf(ProviderSupportedServiceTypeUris, extensionUri) >= 0;
    }

    public bool IsExtensionSupported(IExtension extension)
    {
        if (extension == null) throw new ArgumentNullException("extension");

        // Consider the primary case.
        if (IsExtensionSupported(extension.TypeUri)) return true;
        // Consider the secondary cases.
        if (extension.AdditionalSupportedTypeUris != null)
            foreach (var extensionTypeUri in extension.AdditionalSupportedTypeUris)
                if (IsExtensionSupported(extensionTypeUri))
                    return true;
        return false;
    }

    /// <summary>
    ///     Saves the discovered information about this endpoint
    ///     for later comparison to validate assertions.
    /// </summary>
    internal void Serialize(TextWriter writer)
    {
        writer.WriteLine(ClaimedIdentifier);
        writer.WriteLine(ProviderLocalIdentifier);
        writer.WriteLine(UserSuppliedIdentifier);
        writer.WriteLine(ProviderEndpoint);
        writer.WriteLine(Protocol.Version);
        // No reason to serialize priority. We only needed priority to decide whether to use this endpoint.
    }

    /// <summary>
    ///     Reads previously discovered information about an endpoint
    ///     from a solicited authentication assertion for validation.
    /// </summary>
    /// <returns>
    ///     A <see cref="ServiceEndpoint" /> object that has everything
    ///     except the <see cref="ProviderSupportedServiceTypeUris" />
    ///     deserialized.
    /// </returns>
    internal static ServiceEndpoint Deserialize(TextReader reader)
    {
        var claimedIdentifier = Identifier.Parse(reader.ReadLine());
        var providerLocalIdentifier = Identifier.Parse(reader.ReadLine());
        var userSuppliedIdentifier = reader.ReadLine();
        if (userSuppliedIdentifier.Length == 0) userSuppliedIdentifier = null;
        var providerEndpoint = new Uri(reader.ReadLine());
        var protocol = Util.FindBestVersion(p => p.Version, new[] { new Version(reader.ReadLine()) });
        return new ServiceEndpoint(claimedIdentifier, userSuppliedIdentifier,
            providerEndpoint, providerLocalIdentifier, protocol);
    }

    internal static ServiceEndpoint ParseFromAuthResponse(IDictionary<string, string> query,
        Identifier userSuppliedIdentifier)
    {
        var protocol = Protocol.Detect(query);
        Debug.Assert(protocol.openid.op_endpoint != null, "This method should only be called in OpenID 2.0 contexts.");
        return new ServiceEndpoint(
            Util.GetRequiredArg(query, protocol.openid.claimed_id),
            userSuppliedIdentifier,
            new Uri(Util.GetRequiredArg(query, protocol.openid.op_endpoint)),
            Util.GetRequiredArg(query, protocol.openid.identity),
            protocol);
    }

    public static bool operator ==(ServiceEndpoint se1, ServiceEndpoint se2)
    {
        if (((object)se1 == null) ^ ((object)se2 == null)) return false;
        if ((object)se1 == null) return true;
        return se1.Equals(se2);
    }

    public static bool operator !=(ServiceEndpoint se1, ServiceEndpoint se2)
    {
        return !(se1 == se2);
    }

    public override bool Equals(object obj)
    {
        var other = obj as ServiceEndpoint;
        if (other == null) return false;
        // We specifically do not check our ProviderSupportedServiceTypeUris array
        // or the priority field
        // as that is not persisted in our tokens, and it is not part of the 
        // important assertion validation that is part of the spec.
        return
            ClaimedIdentifier == other.ClaimedIdentifier &&
            ProviderEndpoint == other.ProviderEndpoint &&
            ProviderLocalIdentifier == other.ProviderLocalIdentifier &&
            Protocol == other.Protocol;
    }

    public override int GetHashCode()
    {
        return ClaimedIdentifier.GetHashCode();
    }

    public override string ToString()
    {
        var builder = new StringBuilder();
        builder.AppendLine("ClaimedIdentifier: " + ClaimedIdentifier);
        builder.AppendLine("ProviderLocalIdentifier: " + ProviderLocalIdentifier);
        builder.AppendLine("ProviderEndpoint: " + ProviderEndpoint.AbsoluteUri);
        builder.AppendLine("OpenID version: " + Protocol.Version);
        builder.AppendLine("Service Type URIs:");
        if (ProviderSupportedServiceTypeUris != null)
            foreach (var serviceTypeUri in ProviderSupportedServiceTypeUris)
            {
                builder.Append("\t");
                var matchingExtension = Util.FirstOrDefault(ExtensionManager.RequestExtensions,
                    ext => ext.Key.TypeUri == serviceTypeUri);
                if (matchingExtension.Key != null)
                    builder.AppendLine(string.Format(CultureInfo.CurrentCulture, "{0} ({1})", serviceTypeUri,
                        matchingExtension.Value));
                else
                    builder.AppendLine(serviceTypeUri);
            }
        else
            builder.AppendLine("\t(unavailable)");

        builder.Length -= Environment.NewLine.Length; // trim last newline
        return builder.ToString();
    }

    #region IXrdsProviderEndpoint Members

    private readonly int? servicePriority;

    /// <summary>
    ///     Gets the priority associated with this service that may have been given
    ///     in the XRDS document.
    /// </summary>
    int? IXrdsProviderEndpoint.ServicePriority => servicePriority;

    private readonly int? uriPriority;

    /// <summary>
    ///     Gets the priority associated with the service endpoint URL.
    /// </summary>
    int? IXrdsProviderEndpoint.UriPriority => uriPriority;

    #endregion
}