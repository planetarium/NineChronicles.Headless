using System.Collections.Generic;
using System.Linq;
using Libplanet.Store.Trie;

namespace NineChronicles.Headless.Executable.Store;

public class ReplicableKeyValueStore : IKeyValueStore
{
    private readonly IKeyValueStore _readKvStore;
    private readonly IKeyValueStore _writeKvStore;

    public ReplicableKeyValueStore(IKeyValueStore readKvStore, IKeyValueStore writeKvStore)
    {
        _readKvStore = readKvStore;
        _writeKvStore = writeKvStore;
    }

    public void Dispose()
    {
        _readKvStore.Dispose();
        _writeKvStore.Dispose();
    }

    public byte[] Get(in KeyBytes key)
    {
        try
        {
            if (_writeKvStore.Get(key) is { } cachedValue)
            {
                return cachedValue;
            }
        }
        catch (KeyNotFoundException)
        {
            if (_readKvStore.Get(key) is { } value)
            {
                _writeKvStore.Set(key, value);
                return value;
            }
        }

        throw new KeyNotFoundException();
    }

    public void Set(in KeyBytes key, byte[] value)
    {
        _writeKvStore.Set(key, value);
    }

    public void Set(IDictionary<KeyBytes, byte[]> values)
    {
        _writeKvStore.Set(values);
    }

    public void Delete(in KeyBytes key)
    {
        _writeKvStore.Delete(key);
    }

    public void Delete(IEnumerable<KeyBytes> keys)
    {
        _writeKvStore.Delete(keys);
    }

    public bool Exists(in KeyBytes key)
    {
        return _writeKvStore.Exists(key) || _readKvStore.Exists(key);
    }

    public IEnumerable<KeyBytes> ListKeys()
    {
        return _readKvStore.ListKeys().Concat(_writeKvStore.ListKeys()).Distinct();
    }
}
