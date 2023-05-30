using System;
using System.Collections.Specialized;
using System.Text;
using System.Net;
using System.Web;
using System.Diagnostics;

namespace DotNetOpenId {
	/// <summary>
	/// A response to an OpenID request in terms the host web site can forward to the user agent.
	/// </summary>
	class Response : IResponse {
		/// <param name="code">The HTTP status code.</param>
		/// <param name="headers">The collection of any HTTP headers that should be included.  Cannot be null, but can be an empty collection.</param>
		/// <param name="body">The payload of the response, if any.  Cannot be null, but can be an empty array.</param>
		/// <param name="encodableMessage">
		/// Used to assist testing to decipher the field contents of a Response.
		/// </param>
		internal Response(HttpStatusCode code, WebHeaderCollection headers, byte[] body, IEncodable encodableMessage) {
			if (headers == null) throw new ArgumentNullException("headers");
			if (body == null) throw new ArgumentNullException("body");
			Debug.Assert(encodableMessage != null, "For testing, this is useful to have.");
			Code = code;
			Headers = headers ?? new WebHeaderCollection();
			Body = body;
			EncodableMessage = encodableMessage;
		}

		public HttpStatusCode Code { get; private set; }
		public WebHeaderCollection Headers { get; private set; }
		public byte[] Body { get; private set; }
		internal IEncodable EncodableMessage { get; private set; }

		/// <summary>
		/// Not currently implemented.
		/// </summary>
		public void Send() {
		}

		/// <summary>
		/// Gets the indirect message as it would appear as a single URI request.
		/// </summary>
		internal Uri IndirectMessageAsRequestUri {
			get {
				if (EncodableMessage != null && EncodableMessage.RedirectUrl != null && EncodableMessage.EncodingType == EncodingType.IndirectMessage) {
					UriBuilder builder = new UriBuilder(EncodableMessage.RedirectUrl);
					UriUtil.AppendQueryArgs(builder, EncodableMessage.EncodedFields);
					return builder.Uri;
				} else {
					throw new InvalidOperationException();
				}
			}
		}
	}
}
