using System;
using System.Collections;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.IO;
using System.Text;
using System.Globalization;

namespace DotNetOpenId {
	/// <summary>
	/// Indicates the level of strictness to require when decoding a
	/// Key-Value Form encoded dictionary.
	/// </summary>
	public enum KeyValueFormConformanceLevel {
		/// <summary>
		/// Be as forgiving as possible to errors made while encoding.
		/// </summary>
		Loose,
		/// <summary>
		/// Allow for certain errors in encoding attributable to ambiguities
		/// in the OpenID 1.1 spec's description of the encoding.
		/// </summary>
		OpenId11,
		/// <summary>
		/// The strictest mode.  The decoder requires the encoded dictionary
		/// to be in strict compliance with OpenID 2.0's description of
		/// the encoding.
		/// </summary>
		OpenId20,
	}

	/// <summary>
	/// Conversion to and from the Key-Value Form Encoding defined by
	/// OpenID Authentication 2.0 section 4.1.1.
	/// http://openid.net/specs/openid-authentication-2_0.html#anchor4
	/// </summary>
	internal class KeyValueFormEncoding : IProtocolMessageEncoding {
		static readonly char[] illegalKeyCharacters = { '\n', ':' };
		static readonly char[] illegalValueCharacters = { '\n' };
		const string newLineCharacters = "\n";
		static readonly Encoding textEncoding = new UTF8Encoding(false);

		public KeyValueFormEncoding() {
			ConformanceLevel = KeyValueFormConformanceLevel.Loose;
		}
		public KeyValueFormEncoding(KeyValueFormConformanceLevel conformanceLevel) {
			ConformanceLevel = conformanceLevel;
		}
		public KeyValueFormConformanceLevel ConformanceLevel { get; private set; }

		/// <summary>
		/// Encodes key/value pairs to Key-Value Form.
		/// Do not use for dictionaries of signed fields!  Instead use the overload
		/// that accepts a list of in-order keys.
		/// </summary>
		/// <returns>The UTF8 byte array.</returns>
		/// <remarks>
		/// Because dictionaries do not guarantee ordering,
		/// encoding a dictionary without an explicitly given key order
		/// is useless in OpenID scenarios where a signature must match.
		/// </remarks>
		public byte[] GetBytes(IDictionary<string, string> dictionary) {
			string[] keys = new string[dictionary.Count];
			dictionary.Keys.CopyTo(keys, 0);
			return GetBytes(dictionary, keys);
		}

		/// <summary>
		/// Encodes key/value pairs to Key-Value Form.
		/// </summary>
		/// <param name="dictionary">
		/// The dictionary of key/value pairs to convert to a byte stream.
		/// </param>
		/// <param name="keyOrder">
		/// The order in which to encode the key/value pairs.
		/// Useful in scenarios where a byte[] must be exactly reproduced.
		/// </param>
		/// <returns>The UTF8 byte array.</returns>
		[System.Diagnostics.CodeAnalysis.SuppressMessage("Microsoft.Performance", "CA1822:MarkMembersAsStatic")]
		public byte[] GetBytes(IDictionary<string, string> dictionary, IList<string> keyOrder) {
			if (dictionary == null) throw new ArgumentNullException("dictionary");
			if (keyOrder == null) throw new ArgumentNullException("keyOrder");
			if (dictionary.Count != keyOrder.Count) throw new ArgumentException(Strings.KeysListAndDictionaryDoNotMatch);
			MemoryStream ms = new MemoryStream();
			using (StreamWriter sw = new StreamWriter(ms, textEncoding)) {
				sw.NewLine = newLineCharacters;
				foreach (string keyInOrder in keyOrder) {
					string key = keyInOrder.Trim();
					string value = dictionary[key].Trim();
					if (key.IndexOfAny(illegalKeyCharacters) >= 0)
						throw new ArgumentException(string.Format(CultureInfo.CurrentCulture,
							Strings.InvalidCharacterInKeyValueFormInput, key));
					if (value.IndexOfAny(illegalValueCharacters) >= 0)
						throw new ArgumentException(string.Format(CultureInfo.CurrentCulture,
							Strings.InvalidCharacterInKeyValueFormInput, value));

					sw.Write(key);
					sw.Write(':');
					sw.Write(value);
					sw.WriteLine();
				}
			}
			return ms.ToArray();
		}

		/// <summary>
		/// Decodes bytes in Key-Value Form to key/value pairs.
		/// </summary>
		/// <param name="data">The stream of Key-Value Form encoded bytes.</param>
		/// <returns>The deserialized dictionary.</returns>
		public IDictionary<string, string> GetDictionary(Stream data) {
			using (StreamReader reader = new StreamReader(data, textEncoding)) {
				var dict = new Dictionary<string, string>();
				int line_num = 0;
				string line;
				while ((line = reader.ReadLine()) != null) {
					line_num++;
					if (ConformanceLevel == KeyValueFormConformanceLevel.Loose) {
						line = line.Trim();
						if (line.Length == 0) continue;
					}
					string[] parts = line.Split(new[] { ':' }, 2);
					if (parts.Length != 2) {
						throw new ArgumentException(string.Format(CultureInfo.CurrentCulture,
							Strings.InvalidKeyValueFormCharacterMissing, ':', line_num, line));
					}
					if (ConformanceLevel > KeyValueFormConformanceLevel.Loose) {
						if (char.IsWhiteSpace(parts[0], parts[0].Length-1) ||
							char.IsWhiteSpace(parts[1], 0)) {
							throw new ArgumentException(string.Format(CultureInfo.CurrentCulture,
								Strings.InvalidCharacterInKeyValueFormInput, ' ', line_num, line));
						}
					}
					if (ConformanceLevel < KeyValueFormConformanceLevel.OpenId20) {
						parts[0] = parts[0].Trim();
						parts[1] = parts[1].Trim();
					}
					// calling Add method will throw if a key is encountered twice,
					// which we should do.
					dict.Add(parts[0], parts[1]);
				}
				if (ConformanceLevel > KeyValueFormConformanceLevel.Loose) {
					reader.BaseStream.Seek(-1, SeekOrigin.End);
					if (reader.BaseStream.ReadByte() != '\n') {
						throw new ArgumentException(string.Format(CultureInfo.CurrentCulture,
							Strings.InvalidKeyValueFormCharacterMissing, "\\n", line_num, line));
					}
				}
				return dict;
			}
		}
	}
}
