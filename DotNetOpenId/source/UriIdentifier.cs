using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Text.RegularExpressions;
using DotNetOpenId.RelyingParty;
using DotNetOpenId.Yadis;

namespace DotNetOpenId {
	/// <summary>
	/// A URI style of OpenID Identifier.
	/// </summary>
	[Serializable]
	public sealed class UriIdentifier : Identifier {
		static readonly string[] allowedSchemes = { "http", "https" };
		/// <summary>
		/// Converts a <see cref="UriIdentifier"/> instance to a <see cref="Uri"/> instance.
		/// </summary>
		public static implicit operator Uri(UriIdentifier identifier) {
			if (identifier == null) return null;
			return identifier.Uri;
		}
		/// <summary>
		/// Converts a <see cref="Uri"/> instance to a <see cref="UriIdentifier"/> instance.
		/// </summary>
		public static implicit operator UriIdentifier(Uri identifier) {
			if (identifier == null) return null;
			return new UriIdentifier(identifier);
		}

		internal UriIdentifier(string uri) : this(uri, false) { }
		internal UriIdentifier(string uri, bool requireSslDiscovery)
			: base(requireSslDiscovery) {
			if (string.IsNullOrEmpty(uri)) throw new ArgumentNullException("uri");
			Uri canonicalUri;
			bool schemePrepended;
			if (!TryCanonicalize(uri, out canonicalUri, requireSslDiscovery, out schemePrepended))
				throw new UriFormatException();
			if (requireSslDiscovery && canonicalUri.Scheme != Uri.UriSchemeHttps) {
				throw new ArgumentException(Strings.ExplicitHttpUriSuppliedWithSslRequirement);
			}
			Uri = canonicalUri;
			SchemeImplicitlyPrepended = schemePrepended;
		}
		internal UriIdentifier(Uri uri) : this(uri, false) { }
		internal UriIdentifier(Uri uri, bool requireSslDiscovery)
			: base(requireSslDiscovery) {
			if (uri == null) throw new ArgumentNullException("uri");
			if (!TryCanonicalize(new UriBuilder(uri), out uri))
				throw new UriFormatException();
			if (requireSslDiscovery && uri.Scheme != Uri.UriSchemeHttps) {
				throw new ArgumentException(Strings.ExplicitHttpUriSuppliedWithSslRequirement);
			}
			Uri = uri;
			SchemeImplicitlyPrepended = false;
		}

		internal Uri Uri { get; private set; }
		/// <summary>
		/// Gets whether the scheme was missing when this Identifier was
		/// created and added automatically as part of the normalization
		/// process.
		/// </summary>
		internal bool SchemeImplicitlyPrepended { get; private set; }

		static bool isAllowedScheme(string uri) {
			if (string.IsNullOrEmpty(uri)) return false;
			return Array.FindIndex(allowedSchemes, s => uri.StartsWith(
				s + Uri.SchemeDelimiter, StringComparison.OrdinalIgnoreCase)) >= 0;
		}
		static bool isAllowedScheme(Uri uri) {
			if (uri == null) return false;
			return Array.FindIndex(allowedSchemes, s =>
				uri.Scheme.Equals(s, StringComparison.OrdinalIgnoreCase)) >= 0;
		}
		static bool TryCanonicalize(string uri, out Uri canonicalUri, bool forceHttpsDefaultScheme, out bool schemePrepended) {
			canonicalUri = null;
			schemePrepended = false;
			try {
				// Assume http:// scheme if an allowed scheme isn't given, and strip
				// fragments off.  Consistent with spec section 7.2#3
				if (!isAllowedScheme(uri)) {
					uri = (forceHttpsDefaultScheme ? Uri.UriSchemeHttps : Uri.UriSchemeHttp) +
						Uri.SchemeDelimiter + uri;
					schemePrepended = true;
				}
				// Use a UriBuilder because it helps to normalize the URL as well.
				return TryCanonicalize(new UriBuilder(uri), out canonicalUri);
			} catch (UriFormatException) {
				// We try not to land here with checks in the try block, but just in case.
				return false;
			}
		}
#if UNUSED
		static bool TryCanonicalize(string uri, out string canonicalUri) {
			Uri normalizedUri;
			bool result = TryCanonicalize(uri, out normalizedUri);
			canonicalUri = normalizedUri.AbsoluteUri;
			return result;
		}
#endif
		/// <summary>
		/// Removes the fragment from a URL and sets the host to lowercase.
		/// </summary>
		/// <remarks>
		/// This does NOT standardize an OpenID URL for storage in a database, as
		/// it does nothing to convert the URL to a Claimed Identifier, besides the fact
		/// that it only deals with URLs whereas OpenID 2.0 supports XRIs.
		/// For this, you should lookup the value stored in IAuthenticationResponse.ClaimedIdentifier.
		/// </remarks>
		[System.Diagnostics.CodeAnalysis.SuppressMessage("Microsoft.Globalization", "CA1308:NormalizeStringsToUppercase")]
		static bool TryCanonicalize(UriBuilder uriBuilder, out Uri canonicalUri) {
			uriBuilder.Host = uriBuilder.Host.ToLowerInvariant();
			canonicalUri = uriBuilder.Uri;
			return true;
		}
		internal static bool IsValidUri(string uri) {
			Uri normalized;
			bool schemePrepended;
			return TryCanonicalize(uri, out normalized, false, out schemePrepended);
		}
		internal static bool IsValidUri(Uri uri) {
			if (uri == null) return false;
			if (!uri.IsAbsoluteUri) return false;
			if (!isAllowedScheme(uri)) return false;
			return true;
		}


		internal override IEnumerable<ServiceEndpoint> Discover() {
			List<ServiceEndpoint> endpoints = new List<ServiceEndpoint>();
			// Attempt YADIS discovery
			DiscoveryResult yadisResult = Yadis.Yadis.Discover(this, IsDiscoverySecureEndToEnd);
			if (yadisResult != null) {
				if (yadisResult.IsXrds) {
					XrdsDocument xrds = new XrdsDocument(yadisResult.ResponseText);
					var xrdsEndpoints = xrds.CreateServiceEndpoints(yadisResult.NormalizedUri);
					// Filter out insecure endpoints if high security is required.
					if (IsDiscoverySecureEndToEnd) {
						xrdsEndpoints = Util.Where(xrdsEndpoints, se => se.IsSecure);
					}
					endpoints.AddRange(xrdsEndpoints);
				}
			}
			return endpoints;
		}

		internal override Identifier TrimFragment() {
			// If there is no fragment, we have no need to rebuild the Identifier.
			if (Uri.Fragment == null || Uri.Fragment.Length == 0)
				return this;

			// Strip the fragment.
			UriBuilder builder = new UriBuilder(Uri);
			builder.Fragment = null;
			return builder.Uri;
		}

		internal override bool TryRequireSsl(out Identifier secureIdentifier) {
			// If this Identifier is already secure, reuse it.
			if (IsDiscoverySecureEndToEnd) {
				secureIdentifier = this;
				return true;
			}

			// If this identifier already uses SSL for initial discovery, return one
			// that guarantees it will be used throughout the discovery process.
			if (String.Equals(Uri.Scheme, Uri.UriSchemeHttps, StringComparison.OrdinalIgnoreCase)) {
				secureIdentifier = new UriIdentifier(this.Uri, true);
				return true;
			}

			// Otherwise, try to make this Identifier secure by normalizing to HTTPS instead of HTTP.
			if (SchemeImplicitlyPrepended) {
				UriBuilder newIdentifierUri = new UriBuilder(this.Uri);
				newIdentifierUri.Scheme = Uri.UriSchemeHttps;
				if (newIdentifierUri.Port == 80) {
					newIdentifierUri.Port = 443;
				}
				secureIdentifier = new UriIdentifier(newIdentifierUri.Uri, true);
				return true;
			}

			// This identifier is explicitly NOT https, so we cannot change it.
			secureIdentifier = new NoDiscoveryIdentifier(this);
			return false;
		}

		/// <summary>
		/// Tests equality between this URI and another URI.
		/// </summary>
		public override bool Equals(object obj) {
			UriIdentifier other = obj as UriIdentifier;
			if (other == null) return false;
			return this.Uri == other.Uri;
		}

		/// <summary>
		/// Returns the hash code of this XRI.
		/// </summary>
		public override int GetHashCode() {
			return Uri.GetHashCode();
		}

		/// <summary>
		/// Returns the string form of the URI.
		/// </summary>
		public override string ToString() {
			return Uri.AbsoluteUri;
		}
	}
}
