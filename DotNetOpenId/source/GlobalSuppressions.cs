// This file is used by Code Analysis to maintain SuppressMessage 
// attributes that are applied to this project. 
// Project-level suppressions either have no target or are given 
// a specific target and scoped to a namespace, type, member, etc. 
//
// To add a suppression to this file, right-click the message in the 
// Error List, point to "Suppress Message(s)", and click 
// "In Project Suppression File". 
// You do not need to add suppressions to this file manually. 

using System.Diagnostics.CodeAnalysis;

[assembly:
    SuppressMessage("Microsoft.Performance", "CA1810:InitializeReferenceTypeStaticFieldsInline", Scope = "member",
        Target = "Janrain.Yadis.ByteParser.#.cctor()")]
[assembly:
    SuppressMessage("Microsoft.Performance", "CA1802:UseLiteralsWhereAppropriate", Scope = "member",
        Target = "Janrain.Yadis.ByteParser.#startTagExpr")]
[assembly:
    SuppressMessage("Microsoft.Globalization", "CA1305:SpecifyIFormatProvider",
        MessageId = "System.String.Format(System.String,System.Object)", Scope = "member",
        Target = "Janrain.Yadis.ByteParser.#StartTagMatcher(System.String)")]
[assembly:
    SuppressMessage("Microsoft.Performance", "CA1802:UseLiteralsWhereAppropriate", Scope = "member",
        Target = "Janrain.Yadis.ByteParser.#tagExpr")]
[assembly:
    SuppressMessage("Microsoft.Globalization", "CA1305:SpecifyIFormatProvider",
        MessageId = "System.String.Format(System.String,System.Object,System.Object)", Scope = "member",
        Target = "Janrain.Yadis.ByteParser.#TagMatcher(System.String,System.String[])")]
[assembly:
    SuppressMessage("Microsoft.Performance", "CA1804:RemoveUnusedLocals", MessageId = "input", Scope = "member",
        Target = "Janrain.Yadis.ByteParser.#XmlEncoding(System.Byte[],System.Int32,System.Text.Encoding)")]
[assembly:
    SuppressMessage("Microsoft.Performance", "CA1804:RemoveUnusedLocals", MessageId = "values", Scope = "member",
        Target = "Janrain.Yadis.ByteParser.#XmlEncoding(System.String,System.Int32,System.Text.Encoding)")]
[assembly:
    SuppressMessage("Microsoft.Usage", "CA1801:ReviewUnusedParameters", MessageId = "length", Scope = "member",
        Target = "Janrain.Yadis.ByteParser.#XmlEncoding(System.String,System.Int32,System.Text.Encoding)")]
[assembly:
    SuppressMessage("Microsoft.Globalization", "CA1304:SpecifyCultureInfo", MessageId = "System.String.ToLower",
        Scope = "member",
        Target = "Janrain.Yadis.ByteParser.#XmlEncoding(System.String,System.Int32,System.Text.Encoding)")]
[assembly:
    SuppressMessage("Microsoft.Globalization", "CA1305:SpecifyIFormatProvider",
        MessageId = "System.String.Format(System.String,System.Object)", Scope = "member",
        Target = "Janrain.Yadis.ContentType.#.ctor(System.String)")]
[assembly:
    SuppressMessage("Microsoft.Globalization", "CA1305:SpecifyIFormatProvider",
        MessageId = "System.String.Format(System.String,System.Object,System.Object)", Scope = "member",
        Target = "Janrain.Yadis.ContentType.#MediaType")]
[assembly:
    SuppressMessage("Microsoft.Usage", "CA2208:InstantiateArgumentExceptionsCorrectly", Scope = "member",
        Target = "Org.Mentalis.Security.Cryptography.DiffieHellman.#FromXmlString(System.String)")]
[assembly:
    SuppressMessage("Microsoft.Performance", "CA1822:MarkMembersAsStatic", Scope = "member",
        Target =
            "Org.Mentalis.Security.Cryptography.DiffieHellman.#GetNamedParam(System.Security.SecurityElement,System.String)")]
[assembly:
    SuppressMessage("Microsoft.Usage", "CA2208:InstantiateArgumentExceptionsCorrectly", Scope = "member",
        Target =
            "Org.Mentalis.Security.Cryptography.DiffieHellmanManaged.#.ctor(System.Byte[],System.Byte[],System.Byte[])")]
[assembly:
    SuppressMessage("Microsoft.Usage", "CA2208:InstantiateArgumentExceptionsCorrectly", Scope = "member",
        Target =
            "Org.Mentalis.Security.Cryptography.DiffieHellmanManaged.#.ctor(System.Byte[],System.Byte[],System.Int32)")]
[assembly:
    SuppressMessage("Microsoft.Usage", "CA2208:InstantiateArgumentExceptionsCorrectly", Scope = "member",
        Target =
            "Org.Mentalis.Security.Cryptography.DiffieHellmanManaged.#.ctor(System.Int32,System.Int32,Org.Mentalis.Security.Cryptography.DHKeyGeneration)")]
[assembly:
    SuppressMessage("Microsoft.Usage", "CA1801:ReviewUnusedParameters", MessageId = "checkInput", Scope = "member",
        Target =
            "Org.Mentalis.Security.Cryptography.DiffieHellmanManaged.#Initialize(Mono.Math.BigInteger,Mono.Math.BigInteger,Mono.Math.BigInteger,System.Int32,System.Boolean)")]
[assembly:
    SuppressMessage("Microsoft.Globalization", "CA1304:SpecifyCultureInfo", MessageId = "System.String.ToLower",
        Scope = "member", Target = "Janrain.Yadis.FetchRequest.#EncodingFromResp(System.Net.HttpWebResponse)")]
[assembly:
    SuppressMessage("Microsoft.Globalization", "CA1307:SpecifyStringComparison",
        MessageId = "System.String.IndexOf(System.String)", Scope = "member",
        Target = "Janrain.Yadis.FetchRequest.#EncodingFromResp(System.Net.HttpWebResponse)")]
[assembly:
    SuppressMessage("Microsoft.Globalization", "CA1307:SpecifyStringComparison",
        MessageId = "System.String.IndexOf(System.String,System.Int32)", Scope = "member",
        Target = "Janrain.Yadis.FetchRequest.#EncodingFromResp(System.Net.HttpWebResponse)")]
[assembly:
    SuppressMessage("Microsoft.Performance", "CA1804:RemoveUnusedLocals", MessageId = "enc", Scope = "member",
        Target = "Janrain.Yadis.FetchRequest.#GetResponse(System.Boolean)")]
[assembly:
    SuppressMessage("Microsoft.Performance", "CA1822:MarkMembersAsStatic", Scope = "member",
        Target = "Janrain.Yadis.FetchRequest.#MetaContentType(System.String,System.Int32,System.Text.Encoding)")]
[assembly:
    SuppressMessage("Microsoft.Usage", "CA1801:ReviewUnusedParameters", MessageId = "encoding", Scope = "member",
        Target = "Janrain.Yadis.FetchRequest.#MetaContentType(System.String,System.Int32,System.Text.Encoding)")]
[assembly:
    SuppressMessage("Microsoft.Usage", "CA1801:ReviewUnusedParameters", MessageId = "length", Scope = "member",
        Target = "Janrain.Yadis.FetchRequest.#MetaContentType(System.String,System.Int32,System.Text.Encoding)")]
[assembly:
    SuppressMessage("Microsoft.Globalization", "CA1304:SpecifyCultureInfo", MessageId = "System.String.ToLower",
        Scope = "member",
        Target = "Janrain.Yadis.FetchRequest.#MetaContentType(System.String,System.Int32,System.Text.Encoding)")]
[assembly:
    SuppressMessage("Microsoft.Performance", "CA1805:DoNotInitializeUnnecessarily", Scope = "member",
        Target = "Mono.Xml.MiniParser.#.ctor()")]
[assembly:
    SuppressMessage("Microsoft.Performance", "CA1802:UseLiteralsWhereAppropriate", Scope = "member",
        Target = "Mono.Xml.MiniParser.#INPUT_RANGE")]
[assembly:
    SuppressMessage("Microsoft.Maintainability", "CA1502:AvoidExcessiveComplexity", Scope = "member",
        Target = "Mono.Xml.MiniParser.#Parse(Mono.Xml.MiniParser+IReader,Mono.Xml.MiniParser+IHandler)")]
[assembly:
    SuppressMessage("Microsoft.Maintainability", "CA1505:AvoidUnmaintainableCode", Scope = "member",
        Target = "Mono.Xml.MiniParser.#Parse(Mono.Xml.MiniParser+IReader,Mono.Xml.MiniParser+IHandler)")]
[assembly:
    SuppressMessage("Microsoft.Performance", "CA1804:RemoveUnusedLocals", MessageId = "pred", Scope = "member",
        Target = "Mono.Xml.MiniParser.#Parse(Mono.Xml.MiniParser+IReader,Mono.Xml.MiniParser+IHandler)")]
[assembly:
    SuppressMessage("Microsoft.Performance", "CA1804:RemoveUnusedLocals", MessageId = "prevCh", Scope = "member",
        Target = "Mono.Xml.MiniParser.#Parse(Mono.Xml.MiniParser+IReader,Mono.Xml.MiniParser+IHandler)")]
[assembly:
    SuppressMessage("Microsoft.Globalization", "CA1304:SpecifyCultureInfo",
        MessageId = "System.Char.ToLower(System.Char)", Scope = "member",
        Target = "Mono.Xml.MiniParser.#Parse(Mono.Xml.MiniParser+IReader,Mono.Xml.MiniParser+IHandler)")]
[assembly:
    SuppressMessage("Microsoft.Globalization", "CA1305:SpecifyIFormatProvider",
        MessageId = "System.Int32.Parse(System.String,System.Globalization.NumberStyles)", Scope = "member",
        Target = "Mono.Xml.MiniParser.#Parse(Mono.Xml.MiniParser+IReader,Mono.Xml.MiniParser+IHandler)")]
[assembly:
    SuppressMessage("Microsoft.Globalization", "CA1305:SpecifyIFormatProvider",
        MessageId = "System.String.Format(System.String,System.Object)", Scope = "member",
        Target = "Mono.Xml.MiniParser.#Parse(Mono.Xml.MiniParser+IReader,Mono.Xml.MiniParser+IHandler)")]
[assembly:
    SuppressMessage("Microsoft.Globalization", "CA1305:SpecifyIFormatProvider",
        MessageId = "System.String.Format(System.String,System.Object,System.Object)", Scope = "member",
        Target = "Mono.Xml.MiniParser.#Parse(Mono.Xml.MiniParser+IReader,Mono.Xml.MiniParser+IHandler)")]
[assembly:
    SuppressMessage("Microsoft.Design", "CA1064:ExceptionsShouldBePublic", Scope = "type",
        Target = "Mono.Xml.MiniParser+XMLError")]
[assembly:
    SuppressMessage("Microsoft.Design", "CA1032:ImplementStandardExceptionConstructors", Scope = "type",
        Target = "Mono.Xml.MiniParser+XMLError")]
[assembly:
    SuppressMessage("Microsoft.Usage", "CA2237:MarkISerializableTypesWithSerializable", Scope = "type",
        Target = "Mono.Xml.MiniParser+XMLError")]
[assembly:
    SuppressMessage("Microsoft.Globalization", "CA1305:SpecifyIFormatProvider",
        MessageId = "System.String.Format(System.String,System.Object,System.Object,System.Object)", Scope = "member",
        Target = "Mono.Xml.MiniParser+XMLError.#ToString()")]
[assembly:
    SuppressMessage("Microsoft.Usage", "CA2201:DoNotRaiseReservedExceptionTypes", Scope = "member",
        Target = "Mono.Math.Prime.PrimalityTests.#GetSPPRounds(Mono.Math.BigInteger,Mono.Math.Prime.ConfidenceFactor)")]
[assembly:
    SuppressMessage("Microsoft.Performance", "CA1804:RemoveUnusedLocals", MessageId = "PostTrialDivisionTest",
        Scope = "member",
        Target =
            "Mono.Math.Prime.Generator.SequentialSearchPrimeGeneratorBase.#GenerateNewPrime(System.Int32,System.Object)")]
[assembly:
    SuppressMessage("Microsoft.Performance", "CA1800:DoNotCastUnnecessarily", Scope = "member",
        Target = "Janrain.Yadis.ServiceNode.#CompareTo(System.Object)")]
[assembly:
    SuppressMessage("Microsoft.Globalization", "CA1305:SpecifyIFormatProvider",
        MessageId = "System.Convert.ToInt32(System.String)", Scope = "member",
        Target = "Janrain.Yadis.ServiceNode.#Priority")]
[assembly:
    SuppressMessage("Microsoft.Performance", "CA1800:DoNotCastUnnecessarily", Scope = "member",
        Target = "Janrain.Yadis.UriNode.#CompareTo(System.Object)")]
[assembly:
    SuppressMessage("Microsoft.Globalization", "CA1305:SpecifyIFormatProvider",
        MessageId = "System.Convert.ToInt32(System.String)", Scope = "member",
        Target = "Janrain.Yadis.UriNode.#Priority")]
[assembly:
    SuppressMessage("Microsoft.Usage", "CA2235:MarkAllNonSerializableFields", Scope = "member",
        Target = "Janrain.Yadis.Xrd.#xmldoc")]
[assembly:
    SuppressMessage("Microsoft.Usage", "CA2235:MarkAllNonSerializableFields", Scope = "member",
        Target = "Janrain.Yadis.Xrd.#xmlnsManager")]
[assembly:
    SuppressMessage("Microsoft.Usage", "CA2235:MarkAllNonSerializableFields", Scope = "member",
        Target = "Janrain.Yadis.XrdNode.#node")]
[assembly:
    SuppressMessage("Microsoft.Usage", "CA2235:MarkAllNonSerializableFields", Scope = "member",
        Target = "Janrain.Yadis.XrdNode.#xmldoc")]
[assembly:
    SuppressMessage("Microsoft.Usage", "CA2235:MarkAllNonSerializableFields", Scope = "member",
        Target = "Janrain.Yadis.XrdNode.#xmlnsManager")]
[assembly:
    SuppressMessage("Microsoft.Performance", "CA1810:InitializeReferenceTypeStaticFieldsInline", Scope = "member",
        Target = "Janrain.Yadis.Yadis.#.cctor()")]
[assembly:
    SuppressMessage("Microsoft.Globalization", "CA1304:SpecifyCultureInfo", MessageId = "System.String.ToLower",
        Scope = "member", Target = "Janrain.Yadis.Yadis.#Discover(System.Uri)")]
[assembly:
    SuppressMessage("Microsoft.Globalization", "CA1304:SpecifyCultureInfo", MessageId = "System.String.ToLower",
        Scope = "member", Target = "Janrain.Yadis.Yadis.#MetaYadisLoc(System.String)")]
[assembly:
    SuppressMessage("Microsoft.Design", "CA1020:AvoidNamespacesWithFewTypes", Scope = "namespace",
        Target = "DotNetOpenId.Extensions")]
[assembly:
    SuppressMessage("Microsoft.Usage", "CA1801:ReviewUnusedParameters", MessageId = "sign", Scope = "member",
        Target = "Mono.Math.BigInteger.#.ctor(Mono.Math.BigInteger+Sign,System.UInt32)")]
[assembly:
    SuppressMessage("Microsoft.Usage", "CA1801:ReviewUnusedParameters", MessageId = "notUsed", Scope = "member",
        Target = "Mono.Math.BigInteger.#isProbablePrime(System.Int32)")]
[assembly:
    SuppressMessage("Microsoft.Usage", "CA2201:DoNotRaiseReservedExceptionTypes", Scope = "member",
        Target = "Mono.Math.BigInteger.#op_Multiply(Mono.Math.BigInteger,Mono.Math.BigInteger)")]
[assembly:
    SuppressMessage("Microsoft.Usage", "CA2201:DoNotRaiseReservedExceptionTypes", Scope = "member",
        Target = "Mono.Math.BigInteger+ModulusRing.#BarrettReduction(Mono.Math.BigInteger)")]
[assembly:
    SuppressMessage("Microsoft.Naming", "CA1703:ResourceStringsShouldBeSpelledCorrectly", MessageId = "returnto",
        Scope = "resource", Target = "DotNetOpenId.Strings.resources")]
[assembly: SuppressMessage("Microsoft.Usage", "CA2243:AttributeStringLiteralsShouldParseCorrectly")]
[assembly:
    SuppressMessage("Microsoft.Naming", "CA1703:ResourceStringsShouldBeSpelledCorrectly", MessageId = "Diffie-Hellman",
        Scope = "resource", Target = "DotNetOpenId.Strings.resources")]
[assembly:
    SuppressMessage("Microsoft.Performance", "CA1805:DoNotInitializeUnnecessarily", Scope = "member",
        Target = "DotNetOpenId.Protocol+QueryArguments+Modes.#.ctor()")]
[assembly:
    SuppressMessage("Microsoft.Performance", "CA1805:DoNotInitializeUnnecessarily", Scope = "member",
        Target = "DotNetOpenId.Protocol+QueryArguments+SessionTypes.#.ctor()")]
[assembly:
    SuppressMessage("Microsoft.Performance", "CA1805:DoNotInitializeUnnecessarily", Scope = "member",
        Target = "DotNetOpenId.Protocol+QueryArguments+SignatureAlgorithms.#.ctor()")]
[assembly:
    SuppressMessage("Microsoft.Naming", "CA1704:IdentifiersShouldBeSpelledCorrectly", MessageId = "Xrds",
        Scope = "type", Target = "DotNetOpenId.XrdsPublisher")]
[assembly:
    SuppressMessage("Microsoft.Design", "CA1020:AvoidNamespacesWithFewTypes", Scope = "namespace",
        Target = "DotNetOpenId.Extensions.SimpleRegistration")]
[assembly: SuppressMessage("Microsoft.Design", "CA2210:AssembliesShouldHaveValidStrongNames")]
[assembly:
    SuppressMessage("Microsoft.Naming", "CA1704:IdentifiersShouldBeSpelledCorrectly", MessageId = "Nist",
        Scope = "member",
        Target = "DotNetOpenId.Extensions.ProviderAuthenticationPolicy.PolicyResponse.#NistAssuranceLevel")]
[assembly:
    SuppressMessage("Microsoft.Design", "CA1020:AvoidNamespacesWithFewTypes", Scope = "namespace",
        Target = "DotNetOpenId.Extensions.ProviderAuthenticationPolicy")]
[assembly:
    SuppressMessage("Microsoft.Naming", "CA1704:IdentifiersShouldBeSpelledCorrectly", MessageId = "Whitelist",
        Scope = "member", Target = "DotNetOpenId.UntrustedWebRequest.#WhitelistHostsRegEx")]
[assembly:
    SuppressMessage("Microsoft.Naming", "CA1704:IdentifiersShouldBeSpelledCorrectly", MessageId = "Whitelist",
        Scope = "member", Target = "DotNetOpenId.UntrustedWebRequest.#WhitelistHostsRegex")]
[assembly:
    SuppressMessage("Microsoft.Naming", "CA1704:IdentifiersShouldBeSpelledCorrectly", MessageId = "Whitelist",
        Scope = "member", Target = "DotNetOpenId.UntrustedWebRequest.#WhitelistHosts")]
[assembly:
    SuppressMessage("Microsoft.Naming", "CA1704:IdentifiersShouldBeSpelledCorrectly", MessageId = "Xri", Scope = "type",
        Target = "DotNetOpenId.XriIdentifier")]