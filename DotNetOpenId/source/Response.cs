using System;
using System.Diagnostics;
using System.Net;

namespace DotNetOpenId;

/// <summary>
///     A response to an OpenID request in terms the host web site can forward to the user agent.
/// </summary>
internal class Response : IResponse
{
	/// <param name="code">The HTTP status code.</param>
	/// <param name="headers">
	///     The collection of any HTTP headers that should be included.  Cannot be null, but can be an empty
	///     collection.
	/// </param>
	/// <param name="body">The payload of the response, if any.  Cannot be null, but can be an empty array.</param>
	/// <param name="encodableMessage">
	///     Used to assist testing to decipher the field contents of a Response.
	/// </param>
	internal Response(HttpStatusCode code, WebHeaderCollection headers, byte[] body, IEncodable encodableMessage)
    {
        if (headers == null) throw new ArgumentNullException("headers");
        if (body == null) throw new ArgumentNullException("body");
        Debug.Assert(encodableMessage != null, "For testing, this is useful to have.");
        Code = code;
        Headers = headers ?? new WebHeaderCollection();
        Body = body;
        EncodableMessage = encodableMessage;
    }

    internal IEncodable EncodableMessage { get; }

    /// <summary>
    ///     Gets the indirect message as it would appear as a single URI request.
    /// </summary>
    internal Uri IndirectMessageAsRequestUri
    {
        get
        {
            if (EncodableMessage != null && EncodableMessage.RedirectUrl != null &&
                EncodableMessage.EncodingType == EncodingType.IndirectMessage)
            {
                var builder = new UriBuilder(EncodableMessage.RedirectUrl);
                UriUtil.AppendQueryArgs(builder, EncodableMessage.EncodedFields);
                return builder.Uri;
            }

            throw new InvalidOperationException();
        }
    }

    public HttpStatusCode Code { get; }
    public WebHeaderCollection Headers { get; }
    public byte[] Body { get; }

    /// <summary>
    ///     Not currently implemented.
    /// </summary>
    public void Send()
    {
    }
}