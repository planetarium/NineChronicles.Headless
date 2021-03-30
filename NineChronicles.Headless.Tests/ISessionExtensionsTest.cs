using System.Collections.Generic;
using Libplanet.Crypto;
using Xunit;

namespace NineChronicles.Headless.Tests
{
    public class ISessionExtensionsTest
    {
        [Fact]
        public void GetPrivateKey()
        {
            var privateKey = new PrivateKey();
            var session = new InMemorySession(string.Empty, true, new Dictionary<string, byte[]>
            {
                [ISessionExtensions.SessionPrivateKeyKey] = privateKey.ByteArray, 
            });

            Assert.Equal(privateKey, session.GetPrivateKey());
        }

        [Fact]
        public void SetPrivateKey()
        {
            var session = new InMemorySession(string.Empty, true);
            var privateKey = new PrivateKey();

            Assert.False(session.TryGetValue(ISessionExtensions.SessionPrivateKeyKey, out byte[] _));
            session.SetPrivateKey(privateKey);
            Assert.True(session.TryGetValue(ISessionExtensions.SessionPrivateKeyKey, out byte[]? bytes));
            Assert.Equal(privateKey.ByteArray, bytes);
        }
    }
}
