using System;
using System.Collections.Generic;
using System.Security.Cryptography;

namespace DotNetOpenId.RelyingParty;

internal class ApplicationMemoryStore : AssociationMemoryStore<Uri>, IRelyingPartyApplicationStore
{
    #region IRelyingPartyApplicationStore Members

    private byte[] secretSigningKey;

    public byte[] SecretSigningKey
    {
        get
        {
            if (secretSigningKey == null)
                lock (this)
                {
                    if (secretSigningKey == null)
                    {
                        Logger.Info("Generating new secret signing key.");
                        // initialize in a local variable before setting in field for thread safety.
                        var auth_key = new byte[64];
                        new RNGCryptoServiceProvider().GetBytes(auth_key);
                        secretSigningKey = auth_key;
                    }
                }

            return secretSigningKey;
        }
    }

    private readonly List<Nonce> nonces = new();

    public bool TryStoreNonce(Nonce nonce)
    {
        lock (this)
        {
            if (nonces.Contains(nonce)) return false;
            nonces.Add(nonce);
            return true;
        }
    }

    public void ClearExpiredNonces()
    {
        lock (this)
        {
            var expireds = new List<Nonce>(nonces.Count);
            foreach (var nonce in nonces)
                if (nonce.IsExpired)
                    expireds.Add(nonce);
            foreach (var nonce in expireds)
                nonces.Remove(nonce);
        }
    }

    #endregion
}