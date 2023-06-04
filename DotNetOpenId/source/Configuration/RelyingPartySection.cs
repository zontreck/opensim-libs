using System.Configuration;
using DotNetOpenId.RelyingParty;

namespace DotNetOpenId.Configuration;

internal class RelyingPartySection : ConfigurationSection
{
    private const string securitySettingsConfigName = "security";

    private const string storeConfigName = "store";

    internal static RelyingPartySection Configuration =>
        (RelyingPartySection)ConfigurationManager.GetSection("dotNetOpenId/relyingParty") ?? new RelyingPartySection();

    [ConfigurationProperty(securitySettingsConfigName)]
    public RelyingPartySecuritySettingsElement SecuritySettings
    {
        get => (RelyingPartySecuritySettingsElement)this[securitySettingsConfigName] ??
               new RelyingPartySecuritySettingsElement();
        set => this[securitySettingsConfigName] = value;
    }

    [ConfigurationProperty(storeConfigName)]
    public StoreConfigurationElement<IRelyingPartyApplicationStore> Store
    {
        get => (StoreConfigurationElement<IRelyingPartyApplicationStore>)this[storeConfigName] ??
               new StoreConfigurationElement<IRelyingPartyApplicationStore>();
        set => this[storeConfigName] = value;
    }
}