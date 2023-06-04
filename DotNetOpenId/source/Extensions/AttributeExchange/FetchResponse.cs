using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using DotNetOpenId.Provider;
using DotNetOpenId.RelyingParty;

namespace DotNetOpenId.Extensions.AttributeExchange;

/// <summary>
///     The Attribute Exchange Fetch message, response leg.
/// </summary>
public sealed class FetchResponse : IExtensionResponse
{
    private readonly string Mode = "fetch_response";

    private readonly List<AttributeValues> attributesProvided = new();

    /// <summary>
    ///     Enumerates over all the attributes included by the Provider.
    /// </summary>
    public IEnumerable<AttributeValues> Attributes => attributesProvided;

    /// <summary>
    ///     Whether the OpenID Provider intends to honor the request for updates.
    /// </summary>
    public bool UpdateUrlSupported => UpdateUrl != null;

    /// <summary>
    ///     The URL the OpenID Provider will post updates to.  Must be set if the Provider
    ///     supports and will use this feature.
    /// </summary>
    public Uri UpdateUrl { get; set; }

    /// <summary>
    ///     Used by the Provider to add attributes to the response for the relying party.
    /// </summary>
    public void AddAttribute(AttributeValues attribute)
    {
        if (attribute == null) throw new ArgumentNullException("attribute");
        if (containsAttribute(attribute.TypeUri))
            throw new ArgumentException(
                string.Format(CultureInfo.CurrentCulture, Strings.AttributeAlreadyAdded, attribute.TypeUri));
        attributesProvided.Add(attribute);
    }

    /// <summary>
    ///     Used by the Relying Party to get the value(s) returned by the OpenID Provider
    ///     for a given attribute, or null if that attribute was not provided.
    /// </summary>
    public AttributeValues GetAttribute(string attributeTypeUri)
    {
        foreach (var att in attributesProvided)
            if (att.TypeUri == attributeTypeUri)
                return att;
        return null;
    }

    private bool containsAttribute(string typeUri)
    {
        return GetAttribute(typeUri) != null;
    }

    #region IExtensionResponse Members

    string IExtension.TypeUri => Constants.TypeUri;

    IEnumerable<string> IExtension.AdditionalSupportedTypeUris => new string[0];

    IDictionary<string, string> IExtensionResponse.Serialize(IRequest authenticationRequest)
    {
        var fields = new Dictionary<string, string>
        {
            { "mode", Mode }
        };

        if (UpdateUrlSupported)
            fields.Add("update_url", UpdateUrl.AbsoluteUri);

        SerializeAttributes(fields, attributesProvided);

        return fields;
    }

    internal static void SerializeAttributes(Dictionary<string, string> fields, IEnumerable<AttributeValues> attributes)
    {
        Debug.Assert(fields != null && attributes != null);
        var aliasManager = new AliasManager();
        foreach (var att in attributes)
        {
            var alias = aliasManager.GetAlias(att.TypeUri);
            fields.Add("type." + alias, att.TypeUri);
            if (att.Values == null) continue;
            if (att.Values.Count != 1)
            {
                fields.Add("count." + alias, att.Values.Count.ToString(CultureInfo.InvariantCulture));
                for (var i = 0; i < att.Values.Count; i++)
                    fields.Add(string.Format(CultureInfo.InvariantCulture, "value.{0}.{1}", alias, i + 1),
                        att.Values[i]);
            }
            else
            {
                fields.Add("value." + alias, att.Values[0]);
            }
        }
    }

    bool IExtensionResponse.Deserialize(IDictionary<string, string> fields, IAuthenticationResponse response,
        string typeUri)
    {
        if (fields == null) return false;
        string mode;
        fields.TryGetValue("mode", out mode);
        if (mode != Mode) return false;

        string updateUrl;
        fields.TryGetValue("update_url", out updateUrl);
        Uri updateUri;
        if (Uri.TryCreate(updateUrl, UriKind.Absolute, out updateUri))
            UpdateUrl = updateUri;

        foreach (var att in DeserializeAttributes(fields))
            AddAttribute(att);

        return true;
    }

    internal static IEnumerable<AttributeValues> DeserializeAttributes(IDictionary<string, string> fields)
    {
        var aliasManager = parseAliases(fields);
        foreach (var alias in aliasManager.Aliases)
        {
            var att = new AttributeValues(aliasManager.ResolveAlias(alias));
            var count = 1;
            var countSent = false;
            string countString;
            if (fields.TryGetValue("count." + alias, out countString))
            {
                if (!int.TryParse(countString, out count) || count <= 0)
                {
                    Logger.ErrorFormat("Failed to parse count.{0} value to a positive integer.", alias);
                    continue;
                }

                countSent = true;
            }

            if (countSent)
            {
                for (var i = 1; i <= count; i++)
                {
                    string value;
                    if (fields.TryGetValue(string.Format(CultureInfo.InvariantCulture, "value.{0}.{1}", alias, i),
                            out value))
                    {
                        att.Values.Add(value);
                    }
                    else
                    {
                        Logger.ErrorFormat("Missing value for attribute '{0}'.", att.TypeUri);
                    }
                }
            }
            else
            {
                string value;
                if (fields.TryGetValue("value." + alias, out value))
                {
                    att.Values.Add(value);
                }
                else
                {
                    Logger.ErrorFormat("Missing value for attribute '{0}'.", att.TypeUri);
                    continue;
                }
            }

            yield return att;
        }
    }

    private static AliasManager parseAliases(IDictionary<string, string> fields)
    {
        Debug.Assert(fields != null);
        var aliasManager = new AliasManager();
        foreach (var pair in fields)
        {
            if (!pair.Key.StartsWith("type.", StringComparison.Ordinal)) continue;
            var alias = pair.Key.Substring(5);
            if (alias.IndexOfAny(new[] { '.', ',', ':' }) >= 0)
            {
                Logger.ErrorFormat("Illegal characters in alias name '{0}'.", alias);
                continue;
            }

            aliasManager.SetAlias(alias, pair.Value);
        }

        return aliasManager;
    }

    #endregion
}