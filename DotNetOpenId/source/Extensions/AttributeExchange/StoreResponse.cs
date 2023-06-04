using System.Collections.Generic;
using DotNetOpenId.Provider;
using DotNetOpenId.RelyingParty;

namespace DotNetOpenId.Extensions.AttributeExchange;

/// <summary>
///     The Attribute Exchange Store message, response leg.
/// </summary>
public sealed class StoreResponse : IExtensionResponse
{
    private const string SuccessMode = "store_response_success";
    private const string FailureMode = "store_response_failure";

    /// <summary>
    ///     Whether the storage request succeeded.
    /// </summary>
    public bool Succeeded { get; set; }

    /// <summary>
    ///     The reason for the failure.
    /// </summary>
    public string FailureReason { get; set; }

    #region IExtensionResponse Members

    string IExtension.TypeUri => Constants.TypeUri;

    IEnumerable<string> IExtension.AdditionalSupportedTypeUris => new string[0];

    IDictionary<string, string> IExtensionResponse.Serialize(IRequest authenticationRequest)
    {
        var fields = new Dictionary<string, string>
        {
            { "mode", Succeeded ? SuccessMode : FailureMode }
        };
        if (!Succeeded && !string.IsNullOrEmpty(FailureReason))
            fields.Add("error", FailureReason);

        return fields;
    }

    bool IExtensionResponse.Deserialize(IDictionary<string, string> fields, IAuthenticationResponse response,
        string typeUri)
    {
        if (fields == null) return false;
        string mode;
        if (!fields.TryGetValue("mode", out mode)) return false;
        switch (mode)
        {
            case SuccessMode:
                Succeeded = true;
                break;
            case FailureMode:
                Succeeded = false;
                break;
            default:
                return false;
        }

        if (!Succeeded)
        {
            string error;
            if (fields.TryGetValue("error", out error))
                FailureReason = error;
        }

        return true;
    }

    #endregion
}