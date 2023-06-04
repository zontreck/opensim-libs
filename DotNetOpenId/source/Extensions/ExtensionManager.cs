using System.Collections.Generic;
using DotNetOpenId.Extensions.AttributeExchange;
using DotNetOpenId.Extensions.ProviderAuthenticationPolicy;
using DotNetOpenId.Extensions.SimpleRegistration;

namespace DotNetOpenId.Extensions;

internal class ExtensionManager
{
	/// <summary>
	///     A list of request extensions that may be enumerated over for logging purposes.
	/// </summary>
	internal static Dictionary<IExtensionRequest, string> RequestExtensions = new()
    {
        { new FetchRequest(), "AX fetch" },
        { new StoreRequest(), "AX store" },
        { new PolicyRequest(), "PAPE" },
        { new ClaimsRequest(), "sreg" }
    };
    //internal static List<IExtensionResponse> ResponseExtensions = new List<IExtensionResponse> {
    //    new AttributeExchange.FetchResponse(),
    //    new AttributeExchange.StoreResponse(),
    //    new ProviderAuthenticationPolicy.PolicyResponse(),
    //    new SimpleRegistration.ClaimsResponse(),
    //};
}