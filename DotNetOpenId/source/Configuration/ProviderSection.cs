using System.Configuration;
using IProviderAssociationStore = DotNetOpenId.IAssociationStore<DotNetOpenId.AssociationRelyingPartyType>;

namespace DotNetOpenId.Configuration;

internal class ProviderSection : ConfigurationSection
{
    private const string securitySettingsConfigName = "security";

    private const string storeConfigName = "store";

    internal static ProviderSection Configuration =>
        (ProviderSection)ConfigurationManager.GetSection("dotNetOpenId/provider") ?? new ProviderSection();

    [ConfigurationProperty(securitySettingsConfigName)]
    public ProviderSecuritySettingsElement SecuritySettings
    {
        get => (ProviderSecuritySettingsElement)this[securitySettingsConfigName] ??
               new ProviderSecuritySettingsElement();
        set => this[securitySettingsConfigName] = value;
    }

    [ConfigurationProperty(storeConfigName)]
    public StoreConfigurationElement<IProviderAssociationStore> Store
    {
        get => (StoreConfigurationElement<IProviderAssociationStore>)this[storeConfigName] ??
               new StoreConfigurationElement<IProviderAssociationStore>();
        set => this[storeConfigName] = value;
    }
}