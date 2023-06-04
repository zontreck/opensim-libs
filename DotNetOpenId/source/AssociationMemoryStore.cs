using System.Collections.Generic;

namespace DotNetOpenId;

internal class AssociationMemoryStore<TKey> : IAssociationStore<TKey>
{
    private readonly Dictionary<TKey, Associations> serverAssocsTable = new();

    public void StoreAssociation(TKey distinguishingFactor, Association assoc)
    {
        lock (this)
        {
            if (!serverAssocsTable.ContainsKey(distinguishingFactor))
                serverAssocsTable.Add(distinguishingFactor, new Associations());

            var server_assocs = serverAssocsTable[distinguishingFactor];

            server_assocs.Set(assoc);
        }
    }

    public Association GetAssociation(TKey distinguishingFactor)
    {
        lock (this)
        {
            return GetServerAssocs(distinguishingFactor).Best;
        }
    }

    public Association GetAssociation(TKey distinguishingFactor, string handle)
    {
        lock (this)
        {
            return GetServerAssocs(distinguishingFactor).Get(handle);
        }
    }

    public bool RemoveAssociation(TKey distinguishingFactor, string handle)
    {
        lock (this)
        {
            return GetServerAssocs(distinguishingFactor).Remove(handle);
        }
    }

    /// <summary>
    ///     Clears all expired associations from the store.
    /// </summary>
    public void ClearExpiredAssociations()
    {
        lock (this)
        {
            foreach (var assocs in serverAssocsTable.Values) assocs.ClearExpired();
        }
    }

    internal Associations GetServerAssocs(TKey distinguishingFactor)
    {
        lock (this)
        {
            if (!serverAssocsTable.ContainsKey(distinguishingFactor))
                serverAssocsTable.Add(distinguishingFactor, new Associations());

            return serverAssocsTable[distinguishingFactor];
        }
    }
}