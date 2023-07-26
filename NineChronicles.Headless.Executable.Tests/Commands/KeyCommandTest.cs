using System;
using Libplanet.Common;
using Libplanet.Crypto;
using Libplanet.Extensions.Cocona;
using Libplanet.KeyStore;
using NineChronicles.Headless.Executable.Commands;
using NineChronicles.Headless.Executable.IO;
using NineChronicles.Headless.Executable.Tests.IO;
using NineChronicles.Headless.Executable.Tests.KeyStore;
using Xunit;

namespace NineChronicles.Headless.Executable.Tests.Commands
{
    public class KeyCommandTest
    {
        private readonly IKeyStore _keyStore;
        private readonly KeyCommand _keyCommand;
        private readonly IConsole _console;

        public KeyCommandTest()
        {
            _keyStore = new InMemoryKeyStore();
            _console = new StringIOConsole();
            _keyCommand = new KeyCommand(_console, _keyStore);
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

        [Fact]
        public void PublicKey()
        {
            PrivateKey privateKey = new PrivateKey();
            Guid keyId = _keyStore.Add(ProtectedPrivateKey.Protect(privateKey, "1234"));

            Assert.Contains(keyId, _keyStore.ListIds());
            var passPhrase = new PassphraseParameters();
            passPhrase.Passphrase = "1234";
            _keyCommand.PublicKey(keyId, passPhrase);
            Assert.Equal(_console.Out.ToString()!.Trim(), ByteUtil.Hex(privateKey.PublicKey.Format(false)));
        }
    }
}
