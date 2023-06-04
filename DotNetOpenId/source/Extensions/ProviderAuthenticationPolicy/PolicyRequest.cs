using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.Text;
using DotNetOpenId.Provider;
using IAuthenticationRequest = DotNetOpenId.RelyingParty.IAuthenticationRequest;

namespace DotNetOpenId.Extensions.ProviderAuthenticationPolicy;

/// <summary>
///     The PAPE request part of an OpenID Authentication request message.
/// </summary>
public sealed class PolicyRequest : IExtensionRequest
{
	/// <summary>
	///     Instantiates a new <see cref="PolicyRequest" />.
	/// </summary>
	public PolicyRequest()
    {
        PreferredPolicies = new List<string>(1);
        PreferredAuthLevelTypes = new List<string>(1);
    }

	/// <summary>
	///     Optional. If the End User has not actively authenticated to the OP within the number of seconds specified in a
	///     manner fitting the requested policies, the OP SHOULD authenticate the End User for this request.
	/// </summary>
	/// <remarks>
	///     The OP should realize that not adhering to the request for re-authentication most likely means that the End User
	///     will not be allowed access to the services provided by the RP. If this parameter is absent in the request, the OP
	///     should authenticate the user at its own discretion.
	/// </remarks>
	public TimeSpan? MaximumAuthenticationAge { get; set; }

	/// <summary>
	///     Zero or more authentication policy URIs that the OP SHOULD conform to when authenticating the user. If multiple
	///     policies are requested, the OP SHOULD satisfy as many as it can.
	/// </summary>
	/// <value>
	///     List of authentication policy URIs obtainable from the <see cref="AuthenticationPolicies" /> class or from a
	///     custom list.
	/// </value>
	/// <remarks>
	///     If no policies are requested, the RP may be interested in other information such as the authentication age.
	/// </remarks>
	public IList<string> PreferredPolicies { get; }

	/// <summary>
	///     Zero or more name spaces of the custom Assurance Level the RP requests, in the order of its preference.
	/// </summary>
	public IList<string> PreferredAuthLevelTypes { get; }

	/// <summary>
	///     Tests equality between two <see cref="PolicyRequest" /> instances.
	/// </summary>
	public override bool Equals(object obj)
    {
        var other = obj as PolicyRequest;
        if (other == null) return false;
        if (MaximumAuthenticationAge != other.MaximumAuthenticationAge) return false;
        if (PreferredPolicies.Count != other.PreferredPolicies.Count) return false;
        foreach (var policy in PreferredPolicies)
            if (!other.PreferredPolicies.Contains(policy))
                return false;
        if (PreferredAuthLevelTypes.Count != other.PreferredAuthLevelTypes.Count) return false;
        foreach (var authLevel in PreferredAuthLevelTypes)
            if (!other.PreferredAuthLevelTypes.Contains(authLevel))
                return false;
        return true;
    }

	/// <summary>
	///     Gets a hash code for this object.
	/// </summary>
	public override int GetHashCode()
    {
        return PreferredPolicies.GetHashCode();
    }

    internal static string SerializePolicies(IList<string> policies)
    {
        return ConcatenateListOfElements(policies);
    }

    private static string SerializeAuthLevels(IList<string> preferredAuthLevelTypes, AliasManager aliases)
    {
        var aliasList = new List<string>();
        foreach (var typeUri in preferredAuthLevelTypes) aliasList.Add(aliases.GetAlias(typeUri));

        return ConcatenateListOfElements(aliasList);
    }

    /// <summary>
    ///     Looks at the incoming fields and figures out what the aliases and name spaces for auth level types are.
    /// </summary>
    internal static AliasManager FindIncomingAliases(IDictionary<string, string> fields)
    {
        var aliasManager = new AliasManager();

        foreach (var pair in fields)
        {
            if (!pair.Key.StartsWith(Constants.AuthLevelNamespaceDeclarationPrefix, StringComparison.Ordinal)) continue;

            var alias = pair.Key.Substring(Constants.AuthLevelNamespaceDeclarationPrefix.Length);
            aliasManager.SetAlias(alias, pair.Value);
        }

        aliasManager.SetPreferredAliasesWhereNotSet(Constants.AuthenticationLevels.PreferredTypeUriToAliasMap);

        return aliasManager;
    }

    internal static string ConcatenateListOfElements(IList<string> values)
    {
        Debug.Assert(values != null);
        var valuesList = new StringBuilder();
        foreach (var value in GetUniqueItems(values))
        {
            if (value.Contains(" "))
                throw new FormatException(string.Format(CultureInfo.CurrentCulture,
                    Strings.InvalidUri, value));
            valuesList.Append(value);
            valuesList.Append(" ");
        }

        if (valuesList.Length > 0)
            valuesList.Length -= 1; // remove trailing space
        return valuesList.ToString();
    }

    internal static IEnumerable<T> GetUniqueItems<T>(IList<T> list)
    {
        var itemsSeen = new List<T>(list.Count);
        foreach (var item in list)
        {
            if (itemsSeen.Contains(item)) continue;
            itemsSeen.Add(item);
            yield return item;
        }
    }

    #region IExtensionRequest Members

    IDictionary<string, string> IExtensionRequest.Serialize(IAuthenticationRequest authenticationRequest)
    {
        var fields = new Dictionary<string, string>();

        if (MaximumAuthenticationAge.HasValue)
            fields.Add(Constants.RequestParameters.MaxAuthAge,
                MaximumAuthenticationAge.Value.TotalSeconds.ToString(CultureInfo.InvariantCulture));

        // Even if empty, this parameter is required as part of the request message.
        fields.Add(Constants.RequestParameters.PreferredAuthPolicies, SerializePolicies(PreferredPolicies));

        if (PreferredAuthLevelTypes.Count > 0)
        {
            var authLevelAliases = new AliasManager();
            authLevelAliases.AssignAliases(PreferredAuthLevelTypes,
                Constants.AuthenticationLevels.PreferredTypeUriToAliasMap);

            // Add a definition for each Auth Level Type alias.
            foreach (var alias in authLevelAliases.Aliases)
                fields.Add(Constants.AuthLevelNamespaceDeclarationPrefix + alias, authLevelAliases.ResolveAlias(alias));

            // Now use the aliases for those type URIs to list a preferred order.
            fields.Add(Constants.RequestParameters.PreferredAuthLevelTypes,
                SerializeAuthLevels(PreferredAuthLevelTypes, authLevelAliases));
        }

        return fields;
    }

    bool IExtensionRequest.Deserialize(IDictionary<string, string> fields, IRequest request, string typeUri)
    {
        if (fields == null) return false;
        if (!fields.ContainsKey(Constants.RequestParameters.PreferredAuthPolicies)) return false;

        string maxAuthAge;
        MaximumAuthenticationAge = fields.TryGetValue(Constants.RequestParameters.MaxAuthAge, out maxAuthAge)
            ? TimeSpan.FromSeconds(double.Parse(maxAuthAge, CultureInfo.InvariantCulture))
            : null;

        PreferredPolicies.Clear();
        var preferredPolicies = fields[Constants.RequestParameters.PreferredAuthPolicies].Split(' ');
        foreach (var policy in preferredPolicies)
            if (policy.Length > 0)
                PreferredPolicies.Add(policy);

        PreferredAuthLevelTypes.Clear();
        var authLevelAliases = FindIncomingAliases(fields);
        string preferredAuthLevelAliases;
        if (fields.TryGetValue(Constants.RequestParameters.PreferredAuthLevelTypes, out preferredAuthLevelAliases))
            foreach (var authLevelAlias in preferredAuthLevelAliases.Split(' '))
            {
                if (authLevelAlias.Length == 0) continue;
                PreferredAuthLevelTypes.Add(authLevelAliases.ResolveAlias(authLevelAlias));
            }

        return true;
    }

    #endregion

    #region IExtension Members

    string IExtension.TypeUri => Constants.TypeUri;

    IEnumerable<string> IExtension.AdditionalSupportedTypeUris => new string[0];

    #endregion
}