using System;
using System.Collections.Generic;
using DotNetOpenId.Extensions;

namespace DotNetOpenId.RelyingParty;

[Serializable]
internal class AuthenticationResponseSnapshot : IAuthenticationResponse
{
    private IDictionary<string, string> callbackArguments;

    internal AuthenticationResponseSnapshot(IAuthenticationResponse copyFrom)
    {
        if (copyFrom == null) throw new ArgumentNullException("copyFrom");

        ClaimedIdentifier = copyFrom.ClaimedIdentifier;
        FriendlyIdentifierForDisplay = copyFrom.FriendlyIdentifierForDisplay;
        Status = copyFrom.Status;
        callbackArguments = copyFrom.GetCallbackArguments();
    }

    #region IAuthenticationResponse Members

    public IDictionary<string, string> GetCallbackArguments()
    {
        // Return a copy so that the caller cannot change the contents.
        return new Dictionary<string, string>(callbackArguments);
    }

    public string GetCallbackArgument(string key)
    {
        if (string.IsNullOrEmpty(key)) throw new ArgumentNullException("key");

        string value;
        if (callbackArguments.TryGetValue(key, out value)) return value;
        return null;
    }

    public T GetExtension<T>() where T : IExtensionResponse, new()
    {
        throw new NotSupportedException(Strings.NotSupportedByAuthenticationSnapshot);
    }

    public IExtensionResponse GetExtension(Type extensionType)
    {
        throw new NotSupportedException(Strings.NotSupportedByAuthenticationSnapshot);
    }

    public Identifier ClaimedIdentifier { get; private set; }

    public string FriendlyIdentifierForDisplay { get; private set; }

    public AuthenticationStatus Status { get; private set; }

    public Exception Exception => throw new NotSupportedException(Strings.NotSupportedByAuthenticationSnapshot);

    #endregion
}