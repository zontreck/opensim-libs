using System;
using System.Configuration;

namespace DotNetOpenId.Configuration;

internal class UntrustedWebRequestSection : ConfigurationSection
{
    private const string readWriteTimeoutConfigName = "readWriteTimeout";

    private const string timeoutConfigName = "timeout";

    private const string maximumBytesToReadConfigName = "maximumBytesToRead";

    private const string maximumRedirectionsConfigName = "maximumRedirections";

    private const string whitelistHostsConfigName = "whitelistHosts";

    private const string blacklistHostsConfigName = "blacklistHosts";

    private const string whitelistHostsRegexConfigName = "whitelistHostsRegex";

    private const string blacklistHostsRegexConfigName = "blacklistHostsRegex";

    public UntrustedWebRequestSection()
    {
        SectionInformation.AllowLocation = false;
    }

    internal static UntrustedWebRequestSection Configuration =>
        (UntrustedWebRequestSection)ConfigurationManager.GetSection("dotNetOpenId/untrustedWebRequest") ??
        new UntrustedWebRequestSection();

    [ConfigurationProperty(readWriteTimeoutConfigName, DefaultValue = "00:00:00.800")]
    [PositiveTimeSpanValidator]
    public TimeSpan ReadWriteTimeout
    {
        get => (TimeSpan)this[readWriteTimeoutConfigName];
        set => this[readWriteTimeoutConfigName] = value;
    }

    [ConfigurationProperty(timeoutConfigName, DefaultValue = "00:00:10")]
    [PositiveTimeSpanValidator]
    public TimeSpan Timeout
    {
        get => (TimeSpan)this[timeoutConfigName];
        set => this[timeoutConfigName] = value;
    }

    [ConfigurationProperty(maximumBytesToReadConfigName, DefaultValue = 1024 * 1024)]
    [IntegerValidator(MinValue = 2048)]
    public int MaximumBytesToRead
    {
        get => (int)this[maximumBytesToReadConfigName];
        set => this[maximumBytesToReadConfigName] = value;
    }

    [ConfigurationProperty(maximumRedirectionsConfigName, DefaultValue = 10)]
    [IntegerValidator(MinValue = 0)]
    public int MaximumRedirections
    {
        get => (int)this[maximumRedirectionsConfigName];
        set => this[maximumRedirectionsConfigName] = value;
    }

    [ConfigurationProperty(whitelistHostsConfigName, IsDefaultCollection = false)]
    [ConfigurationCollection(typeof(WhiteBlackListCollection))]
    public WhiteBlackListCollection WhitelistHosts
    {
        get => (WhiteBlackListCollection)this[whitelistHostsConfigName] ?? new WhiteBlackListCollection();
        set => this[whitelistHostsConfigName] = value;
    }

    [ConfigurationProperty(blacklistHostsConfigName, IsDefaultCollection = false)]
    [ConfigurationCollection(typeof(WhiteBlackListCollection))]
    public WhiteBlackListCollection BlacklistHosts
    {
        get => (WhiteBlackListCollection)this[blacklistHostsConfigName] ?? new WhiteBlackListCollection();
        set => this[blacklistHostsConfigName] = value;
    }

    [ConfigurationProperty(whitelistHostsRegexConfigName, IsDefaultCollection = false)]
    [ConfigurationCollection(typeof(WhiteBlackListCollection))]
    public WhiteBlackListCollection WhitelistHostsRegex
    {
        get => (WhiteBlackListCollection)this[whitelistHostsRegexConfigName] ?? new WhiteBlackListCollection();
        set => this[whitelistHostsRegexConfigName] = value;
    }

    [ConfigurationProperty(blacklistHostsRegexConfigName, IsDefaultCollection = false)]
    [ConfigurationCollection(typeof(WhiteBlackListCollection))]
    public WhiteBlackListCollection BlacklistHostsRegex
    {
        get => (WhiteBlackListCollection)this[blacklistHostsRegexConfigName] ?? new WhiteBlackListCollection();
        set => this[blacklistHostsRegexConfigName] = value;
    }
}