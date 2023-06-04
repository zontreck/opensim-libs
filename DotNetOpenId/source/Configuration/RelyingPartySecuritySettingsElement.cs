using System.Configuration;
using DotNetOpenId.RelyingParty;

namespace DotNetOpenId.Configuration;

internal class RelyingPartySecuritySettingsElement : ConfigurationElement
{
    private const string requireSslConfigName = "requireSsl";

    private const string minimumRequiredOpenIdVersionConfigName = "minimumRequiredOpenIdVersion";

    private const string minimumHashBitLengthConfigName = "minimumHashBitLength";

    private const string maximumHashBitLengthConfigName = "maximumHashBitLength";

    [ConfigurationProperty(requireSslConfigName, DefaultValue = false)]
    public bool RequireSsl
    {
        get => (bool)this[requireSslConfigName];
        set => this[requireSslConfigName] = value;
    }

    [ConfigurationProperty(minimumRequiredOpenIdVersionConfigName, DefaultValue = "V10")]
    public ProtocolVersion MinimumRequiredOpenIdVersion
    {
        get => (ProtocolVersion)this[minimumRequiredOpenIdVersionConfigName];
        set => this[minimumRequiredOpenIdVersionConfigName] = value;
    }

    [ConfigurationProperty(minimumHashBitLengthConfigName, DefaultValue = SecuritySettings.minimumHashBitLengthDefault)]
    public int MinimumHashBitLength
    {
        get => (int)this[minimumHashBitLengthConfigName];
        set => this[minimumHashBitLengthConfigName] = value;
    }

    [ConfigurationProperty(maximumHashBitLengthConfigName,
        DefaultValue = SecuritySettings.maximumHashBitLengthRPDefault)]
    public int MaximumHashBitLength
    {
        get => (int)this[maximumHashBitLengthConfigName];
        set => this[maximumHashBitLengthConfigName] = value;
    }

    public RelyingPartySecuritySettings CreateSecuritySettings()
    {
        var settings = new RelyingPartySecuritySettings();
        settings.RequireSsl = RequireSsl;
        settings.MinimumRequiredOpenIdVersion = MinimumRequiredOpenIdVersion;
        settings.MinimumHashBitLength = MinimumHashBitLength;
        settings.MaximumHashBitLength = MaximumHashBitLength;
        return settings;
    }
}