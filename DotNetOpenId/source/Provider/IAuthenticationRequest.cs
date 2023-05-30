﻿using System;
using System.Collections.Generic;
using System.Text;

namespace DotNetOpenId.Provider {
	/// <summary>
	/// Instances of this interface represent incoming authentication requests.
	/// This interface provides the details of the request and allows setting
	/// the response.
	/// </summary>
	public interface IAuthenticationRequest : IRequest {
		/// <summary>
		/// Gets the version of OpenID being used by the relying party that sent the request.
		/// </summary>
		ProtocolVersion RelyingPartyVersion { get; }
		/// <summary>
		/// Whether the consumer demands an immediate response.
		/// If false, the consumer is willing to wait for the identity provider
		/// to authenticate the user.
		/// </summary>
		bool Immediate { get; }
		/// <summary>
		/// The URL the consumer site claims to use as its 'base' address.
		/// </summary>
		Realm Realm { get; }
		/// <summary>
		/// Whether verification of the return URL claimed by the Relying Party
		/// succeeded.
		/// </summary>
		/// <remarks>
		/// Return URL verification is only attempted if this property is queried.
		/// The result of the verification is cached per request so calling this
		/// property getter multiple times in one request is not a performance hit.
		/// See OpenID Authentication 2.0 spec section 9.2.1.
		/// </remarks>
		bool IsReturnUrlDiscoverable { get; }
		/// <summary>
		/// Whether the Provider should help the user select a Claimed Identifier
		/// to send back to the relying party.
		/// </summary>
		bool IsDirectedIdentity { get; }
		/// <summary>
		/// A value indicating whether the requesting Relying Party is using a delegated URL.
		/// </summary>
		/// <remarks>
		/// When delegated identifiers are used, the <see cref="ClaimedIdentifier"/> should not
		/// be changed at the Provider during authentication.
		/// Delegation is only detectable on requests originating from OpenID 2.0 relying parties.
		/// A relying party implementing only OpenID 1.x may use delegation and this property will
		/// return false anyway.
		/// </remarks>
		bool IsDelegatedIdentifier { get; }
		/// <summary>
		/// The Local Identifier to this OpenID Provider of the user attempting 
		/// to authenticate.  Check <see cref="IsDirectedIdentity"/> to see if
		/// this value is valid.
		/// </summary>
		/// <remarks>
		/// This may or may not be the same as the Claimed Identifier that the user agent
		/// originally supplied to the relying party.  The Claimed Identifier
		/// endpoint may be delegating authentication to this provider using
		/// this provider's local id, which is what this property contains.
		/// Use this identifier when looking up this user in the provider's user account
		/// list.
		/// </remarks>
		Identifier LocalIdentifier { get; set; }
		/// <summary>
		/// The identifier that the user agent is claiming at the relying party site.
		/// Check <see cref="IsDirectedIdentity"/> to see if this value is valid.
		/// </summary>
		/// <remarks>
		/// <para>This property can only be set if <see cref="IsDelegatedIdentifier"/> is
		/// false, to prevent breaking URL delegation.</para>
		/// <para>This will not be the same as this provider's local identifier for the user
		/// if the user has set up his/her own identity page that points to this 
		/// provider for authentication.</para>
		/// <para>The provider may use this identifier for displaying to the user when
		/// asking for the user's permission to authenticate to the relying party.</para>
		/// </remarks>
		/// <exception cref="InvalidOperationException">Thrown from the setter 
		/// if <see cref="IsDelegatedIdentifier"/> is true.</exception>
		Identifier ClaimedIdentifier { get; set; }
		/// <summary>
		/// Adds an optional fragment (#fragment) portion to the ClaimedIdentifier.
		/// Useful for identifier recycling.
		/// </summary>
		/// <param name="fragment">
		/// Should not include the # prefix character as that will be added internally.
		/// May be null or the empty string to clear a previously set fragment.
		/// </param>
		/// <remarks>
		/// <para>Unlike the <see cref="ClaimedIdentifier"/> property, which can only be set if
		/// using directed identity, this method can be called on any URI claimed identifier.</para>
		/// <para>Because XRI claimed identifiers (the canonical IDs) are never recycled,
		/// this method should<i>not</i> be called for XRIs.</para>
		/// </remarks>
		/// <exception cref="InvalidOperationException">Thrown when this method is called on an XRI.</exception>
		void SetClaimedIdentifierFragment(string fragment);
		/// <summary>
		/// Gets/sets whether the provider has determined that the 
		/// <see cref="ClaimedIdentifier"/> belongs to the currently logged in user
		/// and wishes to share this information with the consumer.
		/// </summary>
		bool? IsAuthenticated { get; set; }
	}
}
