using System.Configuration;
using DotNetOpenId.Provider;

namespace DotNetOpenId.Configuration;

internal class ProviderSecuritySettingsElement : ConfigurationElement
{
    private const string minimumHashBitLengthConfigName = "minimumHashBitLength";

    private const string maximumHashBitLengthConfigName = "maximumHashBitLength";

    private const string protectDownlevelReplayAttacksConfigName = "protectDownlevelReplayAttacks";

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

    [ConfigurationProperty(protectDownlevelReplayAttacksConfigName, DefaultValue = false)]
    public bool ProtectDownlevelReplayAttacks
    {
        get => (bool)this[protectDownlevelReplayAttacksConfigName];
        set => this[protectDownlevelReplayAttacksConfigName] = value;
    }

    public ProviderSecuritySettings CreateSecuritySettings()
    {
        var settings = new ProviderSecuritySettings();
        settings.MinimumHashBitLength = MinimumHashBitLength;
        settings.MaximumHashBitLength = MaximumHashBitLength;
        settings.ProtectDownlevelReplayAttacks = ProtectDownlevelReplayAttacks;
        return settings;
    }
}