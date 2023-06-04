using System;
using System.Collections.Generic;

namespace DotNetOpenId.RelyingParty;

internal class IndirectMessageRequest : IEncodable
{
    public IndirectMessageRequest(Uri receivingUrl, IDictionary<string, string> fields)
    {
        if (receivingUrl == null) throw new ArgumentNullException("receivingUrl");
        if (fields == null) throw new ArgumentNullException("fields");
        RedirectUrl = receivingUrl;
        EncodedFields = fields;
    }

    #region IEncodable Members

    public EncodingType EncodingType => EncodingType.IndirectMessage;
    public IDictionary<string, string> EncodedFields { get; }
    public Uri RedirectUrl { get; }

    #endregion
}