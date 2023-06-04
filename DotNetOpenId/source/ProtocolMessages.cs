using System.Collections.Generic;
using System.IO;

namespace DotNetOpenId;

internal static class ProtocolMessages
{
    public static readonly HttpEncoding Http = new();
    public static readonly KeyValueFormEncoding KeyValueForm = new();
}

internal interface IProtocolMessageEncoding
{
    byte[] GetBytes(IDictionary<string, string> dictionary);
    IDictionary<string, string> GetDictionary(Stream data);
}