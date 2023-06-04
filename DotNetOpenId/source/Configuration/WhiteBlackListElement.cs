using System.Configuration;

namespace DotNetOpenId.Configuration;

internal class WhiteBlackListElement : ConfigurationElement
{
    private const string nameConfigName = "name";

    [ConfigurationProperty(nameConfigName, IsRequired = true)]
    //[StringValidator(MinLength = 1)]
    public string Name
    {
        get => (string)this[nameConfigName];
        set => this[nameConfigName] = value;
    }
}