using System;
using Libplanet.Crypto;
using Libplanet.Extensions.Cocona.Commands;
using Libplanet.KeyStore;
using NineChronicles.Headless.Executable.Tests.KeyStore;
using Xunit;

namespace NineChronicles.Headless.Executable.Tests.Commands
{
    public class KeyCommandTest
    {
        private readonly IKeyStore _keyStore;
        private readonly KeyCommand _keyCommand;

        public KeyCommandTest()
        {
            _keyStore = new InMemoryKeyStore();
            _keyCommand = new KeyCommand(_keyStore);
        }

        [Theory]
        [InlineData("", "")]
        [InlineData("foo", "bar")]
        public void Remove_WithNoPassphrase(string passphrase, string inputPassphrase)
        {
            PrivateKey privateKey = new PrivateKey();
            Guid keyId = _keyStore.Add(ProtectedPrivateKey.Protect(privateKey, passphrase));
            
            Assert.Contains(keyId, _keyStore.ListIds());
            _keyCommand.Remove(keyId, passphrase: inputPassphrase, noPassphrase: true);
            Assert.DoesNotContain(keyId, _keyStore.ListIds());
        }
    }
}
