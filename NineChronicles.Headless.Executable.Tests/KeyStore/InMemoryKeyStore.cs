using System;
using System.Collections.Generic;
using System.Linq;
using Libplanet.KeyStore;

namespace NineChronicles.Headless.Executable.Tests.KeyStore
{
    public class InMemoryKeyStore : IKeyStore
    {
        private IDictionary<Guid, ProtectedPrivateKey> _protectedPrivateKeys;

        public InMemoryKeyStore()
        {
            _protectedPrivateKeys = new Dictionary<Guid, ProtectedPrivateKey>();
        }

        public IEnumerable<Guid> ListIds()
        {
            return _protectedPrivateKeys.Keys;
        }

        public IEnumerable<Tuple<Guid, ProtectedPrivateKey>> List()
        {
            return _protectedPrivateKeys.Select(pair => new Tuple<Guid, ProtectedPrivateKey>(pair.Key, pair.Value));
        }

        public ProtectedPrivateKey Get(Guid id)
        {
            return _protectedPrivateKeys[id];
        }

        public Guid Add(ProtectedPrivateKey key)
        {
            Guid guid = Guid.NewGuid();
            _protectedPrivateKeys.Add(guid, key);
            return guid;
        }

        public void Remove(Guid id)
        {
            _protectedPrivateKeys.Remove(id);
        }
    }
}
