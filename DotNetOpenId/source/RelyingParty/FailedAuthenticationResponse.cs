using System;
using System.Collections.Generic;
using System.Diagnostics;
using DotNetOpenId.Extensions;

namespace DotNetOpenId.RelyingParty;

[DebuggerDisplay("{Exception.Message}")]
internal class FailedAuthenticationResponse : IAuthenticationResponse
{
    public FailedAuthenticationResponse(Exception exception)
    {
        Exception = exception;
    }

    #region IAuthenticationResponse Members

    public IDictionary<string, string> GetCallbackArguments()
    {
        return new Dictionary<string, string>();
    }

    public string GetCallbackArgument(string key)
    {
        return null;
    }

    public T GetExtension<T>() where T : IExtensionResponse, new()
    {
        return default;
    }

    public IExtensionResponse GetExtension(Type extensionType)
    {
        return null;
    }

    public Identifier ClaimedIdentifier => null;

    public string FriendlyIdentifierForDisplay => null;

    public AuthenticationStatus Status => AuthenticationStatus.Failed;

    public Exception Exception { get; private set; }

    #endregion
}