using System;
using System.Diagnostics;
using System.Security.Cryptography;

namespace DotNetOpenId;

internal class HmacShaAssociation : Association
{
    private static readonly HmacSha[] HmacShaAssociationTypes =
    {
        new()
        {
            CreateHasher = secretKey => new HMACSHA512(secretKey),
            GetAssociationType = protocol => protocol.Args.SignatureAlgorithm.HMAC_SHA512,
            BaseHashAlgorithm = new SHA512Managed()
        },
        new()
        {
            CreateHasher = secretKey => new HMACSHA384(secretKey),
            GetAssociationType = protocol => protocol.Args.SignatureAlgorithm.HMAC_SHA384,
            BaseHashAlgorithm = new SHA384Managed()
        },
        new()
        {
            CreateHasher = secretKey => new HMACSHA256(secretKey),
            GetAssociationType = protocol => protocol.Args.SignatureAlgorithm.HMAC_SHA256,
            BaseHashAlgorithm = new SHA256Managed()
        },
        new()
        {
            CreateHasher = secretKey => new HMACSHA1(secretKey),
            GetAssociationType = protocol => protocol.Args.SignatureAlgorithm.HMAC_SHA1,
            BaseHashAlgorithm = new SHA1Managed()
        }
    };

    private readonly HmacSha typeIdentity;

    private HmacShaAssociation(HmacSha typeIdentity, string handle, byte[] secret, TimeSpan totalLifeLength)
        : base(handle, secret, totalLifeLength, DateTime.UtcNow)
    {
        if (typeIdentity == null) throw new ArgumentNullException("typeIdentity");

        Debug.Assert(secret.Length == typeIdentity.SecretLength);
        this.typeIdentity = typeIdentity;
    }

    public static HmacShaAssociation Create(Protocol protocol, string associationType,
        string handle, byte[] secret, TimeSpan totalLifeLength)
    {
        foreach (var shaType in HmacShaAssociationTypes)
            if (string.Equals(shaType.GetAssociationType(protocol), associationType, StringComparison.Ordinal))
                return new HmacShaAssociation(shaType, handle, secret, totalLifeLength);
        throw new ArgumentOutOfRangeException("associationType");
    }

    public static HmacShaAssociation Create(int secretLength,
        string handle, byte[] secret, TimeSpan totalLifeLength)
    {
        foreach (var shaType in HmacShaAssociationTypes)
            if (shaType.SecretLength == secretLength)
                return new HmacShaAssociation(shaType, handle, secret, totalLifeLength);
        throw new ArgumentOutOfRangeException("secretLength");
    }

    /// <summary>
    ///     Returns the length of the shared secret (in bytes).
    /// </summary>
    public static int GetSecretLength(Protocol protocol, string associationType)
    {
        foreach (var shaType in HmacShaAssociationTypes)
            if (string.Equals(shaType.GetAssociationType(protocol), associationType, StringComparison.Ordinal))
                return shaType.SecretLength;
        throw new ArgumentOutOfRangeException("associationType");
    }

    /// <summary>
    ///     Looks for the longest hash length for a given protocol for which we have an association,
    ///     and perhaps a matching Diffie-Hellman session type.
    /// </summary>
    /// <param name="protocol">The OpenID version that dictates which associations are available.</param>
    /// <param name="minimumHashSizeInBits">The minimum required hash length given security settings.</param>
    /// <param name="maximumHashSizeInBits">
    ///     The maximum hash length to even attempt.  Useful for the RP side where we support
    ///     SHA512 but most OPs do not -- why waste time trying?
    /// </param>
    /// <param name="requireMatchingDHSessionType">True for HTTP associations, False for HTTPS associations.</param>
    /// <param name="associationType">The resulting association type's well known protocol name.  (i.e. HMAC-SHA256)</param>
    /// <param name="sessionType">
    ///     The resulting session type's well known protocol name, if a matching one is available.  (i.e.
    ///     DH-SHA256)
    /// </param>
    internal static bool TryFindBestAssociation(Protocol protocol,
        int? minimumHashSizeInBits, int? maximumHashSizeInBits, bool requireMatchingDHSessionType,
        out string associationType, out string sessionType)
    {
        if (protocol == null) throw new ArgumentNullException("protocol");
        associationType = null;
        sessionType = null;

        // We assume this enumeration is in decreasing bit length order.
        foreach (var sha in HmacShaAssociationTypes)
        {
            var hashSizeInBits = sha.SecretLength * 8;
            if (maximumHashSizeInBits.HasValue && hashSizeInBits > maximumHashSizeInBits.Value)
                continue;
            if (minimumHashSizeInBits.HasValue && hashSizeInBits < minimumHashSizeInBits.Value)
                break;
            sessionType = DiffieHellmanUtil.GetNameForSize(protocol, hashSizeInBits);
            if (requireMatchingDHSessionType && sessionType == null)
                continue;
            associationType = sha.GetAssociationType(protocol);
            return true;
        }

        return false;
    }

    internal static bool IsDHSessionCompatible(Protocol protocol, string associationType, string sessionType)
    {
        // Under HTTPS, no DH encryption is required regardless of association type.
        if (string.Equals(sessionType, protocol.Args.SessionType.NoEncryption, StringComparison.Ordinal)) return true;
        // When there _is_ a DH session, it must match in hash length with the association type.
        foreach (var sha in HmacShaAssociationTypes)
            if (string.Equals(associationType, sha.GetAssociationType(protocol), StringComparison.Ordinal))
            {
                var hashSizeInBits = sha.SecretLength * 8;
                var matchingSessionName = DiffieHellmanUtil.GetNameForSize(protocol, hashSizeInBits);
                if (string.Equals(sessionType, matchingSessionName, StringComparison.Ordinal)) return true;
            }

        return false;
    }

    internal override string GetAssociationType(Protocol protocol)
    {
        return typeIdentity.GetAssociationType(protocol);
    }

    protected override HashAlgorithm CreateHasher()
    {
        return typeIdentity.CreateHasher(SecretKey);
    }

    private class HmacSha
    {
        internal HashAlgorithm BaseHashAlgorithm;
        internal Util.Func<byte[], HashAlgorithm> CreateHasher;
        internal Util.Func<Protocol, string> GetAssociationType;

        /// <summary>
        ///     The size of the hash (in bytes).
        /// </summary>
        internal int SecretLength => BaseHashAlgorithm.HashSize / 8;
    }
}