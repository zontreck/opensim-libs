using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using DotNetOpenId.Provider;
using IAuthenticationRequest = DotNetOpenId.RelyingParty.IAuthenticationRequest;

namespace DotNetOpenId.Extensions.SimpleRegistration;

/// <summary>
///     Carries the request/require/none demand state of the simple registration fields.
/// </summary>
#pragma warning disable 0659, 0661
[SuppressMessage("Microsoft.Usage", "CA2218:OverrideGetHashCodeOnOverridingEquals")]
public sealed class ClaimsRequest : IExtensionRequest
{
    private string typeUriDeserializedFrom;

    /// <summary>
    ///     The level of interest a relying party has in the nickname of the user.
    /// </summary>
    public DemandLevel Nickname { get; set; }

    /// <summary>
    ///     The level of interest a relying party has in the email of the user.
    /// </summary>
    public DemandLevel Email { get; set; }

    /// <summary>
    ///     The level of interest a relying party has in the full name of the user.
    /// </summary>
    public DemandLevel FullName { get; set; }

    /// <summary>
    ///     The level of interest a relying party has in the birthdate of the user.
    /// </summary>
    public DemandLevel BirthDate { get; set; }

    /// <summary>
    ///     The level of interest a relying party has in the gender of the user.
    /// </summary>
    public DemandLevel Gender { get; set; }

    /// <summary>
    ///     The level of interest a relying party has in the postal code of the user.
    /// </summary>
    public DemandLevel PostalCode { get; set; }

    /// <summary>
    ///     The level of interest a relying party has in the Country of the user.
    /// </summary>
    public DemandLevel Country { get; set; }

    /// <summary>
    ///     The level of interest a relying party has in the language of the user.
    /// </summary>
    public DemandLevel Language { get; set; }

    /// <summary>
    ///     The level of interest a relying party has in the time zone of the user.
    /// </summary>
    public DemandLevel TimeZone { get; set; }

    /// <summary>
    ///     The URL the consumer site provides for the authenticating user to review
    ///     for how his claims will be used by the consumer web site.
    /// </summary>
    public Uri PolicyUrl { get; set; }

    /// <summary>
    ///     Sets the profile request properties according to a list of
    ///     field names that might have been passed in the OpenId query dictionary.
    /// </summary>
    /// <param name="fieldNames">
    ///     The list of field names that should receive a given
    ///     <paramref name="requestLevel" />.  These field names should match
    ///     the OpenId specification for field names, omitting the 'openid.sreg' prefix.
    /// </param>
    /// <param name="requestLevel">The none/request/require state of the listed fields.</param>
    internal void SetProfileRequestFromList(ICollection<string> fieldNames, DemandLevel requestLevel)
    {
        foreach (var field in fieldNames)
            switch (field)
            {
                case Constants.nickname:
                    Nickname = requestLevel;
                    break;
                case Constants.email:
                    Email = requestLevel;
                    break;
                case Constants.fullname:
                    FullName = requestLevel;
                    break;
                case Constants.dob:
                    BirthDate = requestLevel;
                    break;
                case Constants.gender:
                    Gender = requestLevel;
                    break;
                case Constants.postcode:
                    PostalCode = requestLevel;
                    break;
                case Constants.country:
                    Country = requestLevel;
                    break;
                case Constants.language:
                    Language = requestLevel;
                    break;
                case Constants.timezone:
                    TimeZone = requestLevel;
                    break;
                default:
                    Logger.WarnFormat("OpenIdProfileRequest.SetProfileRequestFromList: Unrecognized field name '{0}'.",
                        field);
                    break;
            }
    }

    private string[] assembleProfileFields(DemandLevel level)
    {
        var fields = new List<string>(10);
        if (Nickname == level)
            fields.Add(Constants.nickname);
        if (Email == level)
            fields.Add(Constants.email);
        if (FullName == level)
            fields.Add(Constants.fullname);
        if (BirthDate == level)
            fields.Add(Constants.dob);
        if (Gender == level)
            fields.Add(Constants.gender);
        if (PostalCode == level)
            fields.Add(Constants.postcode);
        if (Country == level)
            fields.Add(Constants.country);
        if (Language == level)
            fields.Add(Constants.language);
        if (TimeZone == level)
            fields.Add(Constants.timezone);

        return fields.ToArray();
    }

    /// <summary>
    ///     Prepares a Simple Registration response extension that is compatible with the
    ///     version of Simple Registration used in the request message.
    /// </summary>
    public ClaimsResponse CreateResponse()
    {
        if (typeUriDeserializedFrom == null)
            throw new InvalidOperationException(Strings.CallDeserializeBeforeCreateResponse);
        return new ClaimsResponse(typeUriDeserializedFrom);
    }

    /// <summary>
    ///     Renders the requested information as a string.
    /// </summary>
    public override string ToString()
    {
        return string.Format(CultureInfo.CurrentCulture, @"Nickname = '{0}' 
Email = '{1}' 
FullName = '{2}' 
Birthdate = '{3}'
Gender = '{4}'
PostalCode = '{5}'
Country = '{6}'
Language = '{7}'
TimeZone = '{8}'", Nickname, Email, FullName, BirthDate, Gender, PostalCode, Country, Language, TimeZone);
    }

    /// <summary>
    ///     Tests equality between two <see cref="ClaimsRequest" /> structs.
    /// </summary>
    public static bool operator ==(ClaimsRequest one, ClaimsRequest other)
    {
        if ((object)one == null && (object)other == null) return true;
        if (((object)one == null) ^ ((object)other == null)) return false;
        return one.Equals(other);
    }

    /// <summary>
    ///     Tests inequality between two <see cref="ClaimsRequest" /> structs.
    /// </summary>
    public static bool operator !=(ClaimsRequest one, ClaimsRequest other)
    {
        return !(one == other);
    }

    /// <summary>
    ///     Tests equality between two <see cref="ClaimsRequest" /> structs.
    /// </summary>
    public override bool Equals(object obj)
    {
        var other = obj as ClaimsRequest;
        if (other == null) return false;

        return
            safeEquals(BirthDate, other.BirthDate) &&
            safeEquals(Country, other.Country) &&
            safeEquals(Language, other.Language) &&
            safeEquals(Email, other.Email) &&
            safeEquals(FullName, other.FullName) &&
            safeEquals(Gender, other.Gender) &&
            safeEquals(Nickname, other.Nickname) &&
            safeEquals(PostalCode, other.PostalCode) &&
            safeEquals(TimeZone, other.TimeZone) &&
            safeEquals(PolicyUrl, other.PolicyUrl);
    }

    private static bool safeEquals(object one, object other)
    {
        if (one == null && other == null) return true;
        if ((one == null) ^ (other == null)) return false;
        return one.Equals(other);
    }

    #region IExtensionRequest Members

    string IExtension.TypeUri => Constants.sreg_ns;

    private static readonly string[] additionalTypeUris =
    {
        Constants.sreg_ns10,
        Constants.sreg_ns11other
    };

    IEnumerable<string> IExtension.AdditionalSupportedTypeUris => additionalTypeUris;

    bool IExtensionRequest.Deserialize(IDictionary<string, string> args, IRequest request, string typeUri)
    {
        if (args == null) return false;
        Debug.Assert(!string.IsNullOrEmpty(typeUri));
        typeUriDeserializedFrom = typeUri;

        string policyUrl;
        if (args.TryGetValue(Constants.policy_url, out policyUrl)
            && !string.IsNullOrEmpty(policyUrl))
            PolicyUrl = new Uri(policyUrl);

        string optionalFields;
        if (args.TryGetValue(Constants.optional, out optionalFields))
            SetProfileRequestFromList(optionalFields.Split(','), DemandLevel.Request);

        string requiredFields;
        if (args.TryGetValue(Constants.required, out requiredFields))
            SetProfileRequestFromList(requiredFields.Split(','), DemandLevel.Require);

        return true;
    }

    /// <summary>
    ///     Adds a description of the information the relying party site would like
    ///     the Provider to include with a positive authentication assertion as an
    ///     extension to an authentication request.
    /// </summary>
    IDictionary<string, string> IExtensionRequest.Serialize(IAuthenticationRequest request)
    {
        var fields = new Dictionary<string, string>();
        if (PolicyUrl != null)
            fields.Add(Constants.policy_url, PolicyUrl.AbsoluteUri);

        fields.Add(Constants.required, string.Join(",", assembleProfileFields(DemandLevel.Require)));
        fields.Add(Constants.optional, string.Join(",", assembleProfileFields(DemandLevel.Request)));

        return fields;
    }

    #endregion
}