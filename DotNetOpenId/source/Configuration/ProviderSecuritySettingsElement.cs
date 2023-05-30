﻿using System.Configuration;
using DotNetOpenId.Provider;

namespace DotNetOpenId.Configuration {
	internal class ProviderSecuritySettingsElement : ConfigurationElement {
		public ProviderSecuritySettingsElement() {
		}

		public ProviderSecuritySettings CreateSecuritySettings() {
			ProviderSecuritySettings settings = new ProviderSecuritySettings();
			settings.MinimumHashBitLength = MinimumHashBitLength;
			settings.MaximumHashBitLength = MaximumHashBitLength;
			settings.ProtectDownlevelReplayAttacks = ProtectDownlevelReplayAttacks;
			return settings;
		}

		const string minimumHashBitLengthConfigName = "minimumHashBitLength";
		[ConfigurationProperty(minimumHashBitLengthConfigName, DefaultValue = DotNetOpenId.SecuritySettings.minimumHashBitLengthDefault)]
		public int MinimumHashBitLength {
			get { return (int)this[minimumHashBitLengthConfigName]; }
			set { this[minimumHashBitLengthConfigName] = value; }
		}

		const string maximumHashBitLengthConfigName = "maximumHashBitLength";
		[ConfigurationProperty(maximumHashBitLengthConfigName, DefaultValue = DotNetOpenId.SecuritySettings.maximumHashBitLengthRPDefault)]
		public int MaximumHashBitLength {
			get { return (int)this[maximumHashBitLengthConfigName]; }
			set { this[maximumHashBitLengthConfigName] = value; }
		}

		const string protectDownlevelReplayAttacksConfigName = "protectDownlevelReplayAttacks";
		[ConfigurationProperty(protectDownlevelReplayAttacksConfigName, DefaultValue = false)]
		public bool ProtectDownlevelReplayAttacks {
			get { return (bool)this[protectDownlevelReplayAttacksConfigName]; }
			set { this[protectDownlevelReplayAttacksConfigName] = value; }
		}
	}
}
