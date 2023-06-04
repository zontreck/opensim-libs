using System;
using System.Configuration;

namespace DotNetOpenId.Configuration;

internal class StoreConfigurationElement<T> : ConfigurationElement
{
    private const string customStoreTypeConfigName = "type";

    [ConfigurationProperty(customStoreTypeConfigName)]
    //[SubclassTypeValidator(typeof(T))]
    public string TypeName
    {
        get => (string)this[customStoreTypeConfigName];
        set => this[customStoreTypeConfigName] = value;
    }

    public Type CustomStoreType => string.IsNullOrEmpty(TypeName) ? null : Type.GetType(TypeName);

    public T CreateInstanceOfStore(T defaultValue)
    {
        return CustomStoreType != null ? (T)Activator.CreateInstance(CustomStoreType) : defaultValue;
    }
}