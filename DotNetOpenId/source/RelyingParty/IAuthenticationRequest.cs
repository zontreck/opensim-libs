﻿using System;
using System.Collections.Generic;
using System.Text;
using DotNetOpenId.Extensions;

namespace DotNetOpenId.RelyingParty {
	/// <summary>
	/// Instances of this interface represent relying party authentication 
	/// requests that may be queried/modified in specific ways before being
	/// routed to the OpenID Provider.
	/// </summary>
	public interface IAuthenticationRequest {
		/// <summary>
		/// Makes a dictionary of key/value pairs available when the authentication is completed.
		/// </summary>
		/// <remarks>
		/// <para>Note that these values are NOT protected against tampering in transit.  No 
		/// security-sensitive data should be stored using this method.</para>
		/// <para>The values stored here can be retrieved using 
		/// <see cref="IAuthenticationResponse.GetCallbackArguments"/>.</para>
		/// <para>Since the data set here is sent in the querystring of the request and some
		/// servers place limits on the size of a request URL, this data should be kept relatively
		/// small to ensure successful authentication.  About 1.5KB is about all that should be stored.</para>
		/// </remarks>
		void AddCallbackArguments(IDictionary<string, string> arguments);
		/// <summary>
		/// Makes a key/value pair available when the authentication is completed.
		/// </summary>
		/// <remarks>
		/// <para>Note that these values are NOT protected against tampering in transit.  No 
		/// security-sensitive data should be stored using this method.</para>
		/// <para>The value stored here can be retrieved using 
		/// <see cref="IAuthenticationResponse.GetCallbackArgument"/>.</para>
		/// <para>Since the data set here is sent in the querystring of the request and some
		/// servers place limits on the size of a request URL, this data should be kept relatively
		/// small to ensure successful authentication.  About 1.5KB is about all that should be stored.</para>
		/// </remarks>
		void AddCallbackArguments(string key, string value);
		/// <summary>
		/// Adds an OpenID extension to the request directed at the OpenID provider.
		/// </summary>
		void AddExtension(IExtensionRequest extension);
		
		/// <summary>
		/// Gets/sets the mode the Provider should use during authentication.
		/// </summary>
		AuthenticationRequestMode Mode { get; set; }
		/// <summary>
		/// Gets the HTTP response the relying party should send to the user agent 
		/// to redirect it to the OpenID Provider to start the OpenID authentication process.
		/// </summary>
		IResponse RedirectingResponse { get; }
		/// <summary>
		/// Gets the URL that the user agent will return to after authentication
		/// completes or fails at the Provider.
		/// </summary>
		Uri ReturnToUrl { get; }
		/// <summary>
		/// Gets the URL that identifies this consumer web application that
		/// the Provider will display to the end user.
		/// </summary>
		Realm Realm { get; }
		/// <summary>
		/// Gets the Claimed Identifier that the User Supplied Identifier
		/// resolved to.  Null if the user provided an OP Identifier 
		/// (directed identity).
		/// </summary>
		/// <remarks>
		/// Null is returned if the user is using the directed identity feature
		/// of OpenID 2.0 to make it nearly impossible for a relying party site
		/// to improperly store the reserved OpenID URL used for directed identity
		/// as a user's own Identifier.  
		/// However, to test for the Directed Identity feature, please test the
		/// <see cref="IsDirectedIdentity"/> property rather than testing this 
		/// property for a null value.
		/// </remarks>
		Identifier ClaimedIdentifier { get; }
		/// <summary>
		/// Gets whether the authenticating user has chosen to let the Provider
		/// determine and send the ClaimedIdentifier after authentication.
		/// </summary>
		bool IsDirectedIdentity { get; }
		/// <summary>
		/// Gets information about the OpenId Provider, as advertised by the
		/// OpenId discovery documents found at the <see cref="ClaimedIdentifier"/>
		/// location.
		/// </summary>
		IProviderEndpoint Provider { get; }
		/// <summary>
		/// The detected version of OpenID implemented by the Provider.
		/// </summary>
		[Obsolete("Use Provider.Version instead.")]
		Version ProviderVersion { get; }
	}
}
