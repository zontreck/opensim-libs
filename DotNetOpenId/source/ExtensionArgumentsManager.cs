using System;
using System.Collections.Generic;
using System.Globalization;
using DotNetOpenId.Extensions;
using DotNetOpenId.Extensions.SimpleRegistration;

namespace DotNetOpenId;

internal class ExtensionArgumentsManager : IIncomingExtensions, IOutgoingExtensions
{
	/// <summary>
	///     This contains a set of aliases that we must be willing to implicitly
	///     match to namespaces for backward compatibility with other OpenID libraries.
	/// </summary>
	private static readonly Dictionary<string, string> typeUriToAliasAffinity = new()
    {
        { Constants.sreg_ns, Constants.sreg_compatibility_alias },
        {
            Extensions.ProviderAuthenticationPolicy.Constants.TypeUri,
            Extensions.ProviderAuthenticationPolicy.Constants.pape_compatibility_alias
        }
    };

    private readonly AliasManager aliasManager = new();

    /// <summary>
    ///     A complex dictionary where the key is the Type URI of the extension,
    ///     and the value is another dictionary of the name/value args of the extension.
    /// </summary>
    private readonly Dictionary<string, Dictionary<string, string>> extensions = new();

    /// <summary>
    ///     Whether extensions are being read or written.
    /// </summary>
    private bool isReadMode;

    private Protocol protocol;

    private ExtensionArgumentsManager()
    {
    }

    /// <summary>
    ///     Gets the fields carried by a given OpenId extension.
    /// </summary>
    /// <returns>The fields included in the given extension, or null if the extension is not present.</returns>
    public IDictionary<string, string> GetExtensionArguments(string extensionTypeUri)
    {
        if (!isReadMode) throw new InvalidOperationException();
        if (string.IsNullOrEmpty(extensionTypeUri)) throw new ArgumentNullException("extensionTypeUri");
        Dictionary<string, string> extensionArgs;
        extensions.TryGetValue(extensionTypeUri, out extensionArgs);
        return extensionArgs;
    }

    public bool ContainsExtension(string extensionTypeUri)
    {
        if (!isReadMode) throw new InvalidOperationException();
        return extensions.ContainsKey(extensionTypeUri);
    }

    public void AddExtensionArguments(string extensionTypeUri,
        IDictionary<string, string> arguments)
    {
        if (isReadMode) throw new InvalidOperationException();
        if (string.IsNullOrEmpty(extensionTypeUri)) throw new ArgumentNullException("extensionTypeUri");
        if (arguments == null) throw new ArgumentNullException("arguments");
        if (arguments.Count == 0) return;

        Dictionary<string, string> extensionArgs;
        if (!extensions.TryGetValue(extensionTypeUri, out extensionArgs))
            extensions.Add(extensionTypeUri, extensionArgs = new Dictionary<string, string>());
        if (extensionArgs.Count > 0)
            throw new OpenIdException(string.Format(CultureInfo.CurrentCulture,
                Strings.ExtensionAlreadyAddedWithSameTypeURI, extensionTypeUri));
        foreach (var pair in arguments) extensionArgs.Add(pair.Key, pair.Value);
    }

    public static ExtensionArgumentsManager CreateIncomingExtensions(IDictionary<string, string> query)
    {
        if (query == null) throw new ArgumentNullException("query");
        var mgr = new ExtensionArgumentsManager();
        mgr.protocol = Protocol.Detect(query);
        mgr.isReadMode = true;
        var aliasPrefix = mgr.protocol.openid.ns + ".";
        // First pass looks for namespace aliases
        foreach (var pair in query)
            if (pair.Key.StartsWith(aliasPrefix, StringComparison.Ordinal))
                mgr.aliasManager.SetAlias(pair.Key.Substring(aliasPrefix.Length), pair.Value);
        // For backwards compatibility, add certain aliases if they aren't defined.
        foreach (var pair in typeUriToAliasAffinity)
            if (!mgr.aliasManager.IsAliasAssignedTo(pair.Key) &&
                !mgr.aliasManager.IsAliasUsed(pair.Value))
                mgr.aliasManager.SetAlias(pair.Value, pair.Key);
        // Second pass looks for extensions using those aliases
        foreach (var pair in query)
        {
            if (!pair.Key.StartsWith(mgr.protocol.openid.Prefix, StringComparison.Ordinal)) continue;
            var possibleAlias = pair.Key.Substring(mgr.protocol.openid.Prefix.Length);
            var periodIndex = possibleAlias.IndexOf(".", StringComparison.Ordinal);
            if (periodIndex >= 0) possibleAlias = possibleAlias.Substring(0, periodIndex);
            string typeUri;
            if ((typeUri = mgr.aliasManager.TryResolveAlias(possibleAlias)) != null)
            {
                if (!mgr.extensions.ContainsKey(typeUri))
                    mgr.extensions[typeUri] = new Dictionary<string, string>();
                var key = periodIndex >= 0
                    ? pair.Key.Substring(mgr.protocol.openid.Prefix.Length + possibleAlias.Length + 1)
                    : string.Empty;
                mgr.extensions[typeUri].Add(key, pair.Value);
            }
        }

        return mgr;
    }

    public static ExtensionArgumentsManager CreateOutgoingExtensions(Protocol protocol)
    {
        var mgr = new ExtensionArgumentsManager();
        mgr.protocol = protocol;
        // Affinity for certain alias for backwards compatibility
        foreach (var pair in typeUriToAliasAffinity) mgr.aliasManager.SetAlias(pair.Value, pair.Key);
        return mgr;
    }

    /// <summary>
    ///     Gets the actual arguments to add to a querystring or other response,
    ///     where type URI, alias, and actual key/values are all defined.
    /// </summary>
    public IDictionary<string, string> GetArgumentsToSend(bool includeOpenIdPrefix)
    {
        if (isReadMode) throw new InvalidOperationException();
        var args = new Dictionary<string, string>();
        foreach (var typeUriAndExtension in extensions)
        {
            var typeUri = typeUriAndExtension.Key;
            var extensionArgs = typeUriAndExtension.Value;
            if (extensionArgs.Count == 0) continue;
            var alias = aliasManager.GetAlias(typeUri);
            // send out the alias declaration
            var openidPrefix = includeOpenIdPrefix ? protocol.openid.Prefix : string.Empty;
            args.Add(openidPrefix + protocol.openidnp.ns + "." + alias, typeUri);
            var prefix = openidPrefix + alias;
            foreach (var pair in extensionArgs)
            {
                var key = prefix;
                if (pair.Key.Length > 0) key += "." + pair.Key;
                args.Add(key, pair.Value);
            }
        }

        return args;
    }
}

internal interface IIncomingExtensions
{
	/// <summary>
	///     Gets the key/value pairs of a provider's response for a given OpenID extension.
	/// </summary>
	/// <param name="extensionTypeUri">
	///     The Type URI of the OpenID extension whose arguments are being sought.
	/// </param>
	/// <returns>
	///     Returns key/value pairs for this extension.
	/// </returns>
	IDictionary<string, string> GetExtensionArguments(string extensionTypeUri);

	/// <summary>
	///     Gets whether any arguments for a given extension are present.
	/// </summary>
	bool ContainsExtension(string extensionTypeUri);
}

internal interface IOutgoingExtensions
{
	/// <summary>
	///     Adds query parameters for OpenID extensions to the request directed
	///     at the OpenID provider.
	/// </summary>
	void AddExtensionArguments(string extensionTypeUri, IDictionary<string, string> arguments);
}