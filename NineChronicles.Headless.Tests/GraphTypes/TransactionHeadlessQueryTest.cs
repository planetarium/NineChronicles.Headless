using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Threading.Tasks;
using System.Security.Cryptography;
using Bencodex;
using GraphQL;
using GraphQL.Execution;
using GraphQL.NewtonsoftJson;
using Libplanet;
using Libplanet.Action;
using Libplanet.Blockchain;
using Libplanet.Blockchain.Policies;
using Libplanet.Crypto;
using Libplanet.Store;
using Libplanet.Store.Trie;
using Libplanet.Tx;
using Nekoyume.Action;
using NineChronicles.Headless.GraphTypes;
using NineChronicles.Headless.Tests.Common;
using Xunit;
using NCAction = Libplanet.Action.PolymorphicAction<Nekoyume.Action.ActionBase>;

namespace NineChronicles.Headless.Tests.GraphTypes
{
    public class TransactionHeadlessQueryTest
    {
        private readonly BlockChain<NCAction> _blockChain;
        private readonly IStore _store;
        private readonly IStateStore _stateStore;
        private readonly NineChroniclesNodeService _service;

        public TransactionHeadlessQueryTest()
        {
            _store = new DefaultStore(null);
            _stateStore = new TrieStateStore(new DefaultKeyValueStore(null));
            _blockChain = new BlockChain<NCAction>(
                new BlockPolicy<NCAction>(),
                new VolatileStagePolicy<NCAction>(),
                _store,
                _stateStore,
                BlockChain<NCAction>.MakeGenesisBlock());
            _service = ServiceBuilder.CreateNineChroniclesNodeService(_blockChain.Genesis, new PrivateKey());
        }

        [Fact]
        public async Task NextTxNonce()
        {
            var userPrivateKey = new PrivateKey();
            var userAddress = userPrivateKey.ToAddress();
            string query = $"{{ nextTxNonce(address: \"{userAddress}\") }}";
            var queryResult = await ExecuteAsync(query);
            var data = (Dictionary<string, object>)((ExecutionNode)queryResult.Data!).ToValue()!;
            Assert.Equal(
                new Dictionary<string, object>
                {
                    ["nextTxNonce"] = 0L
                },
                data
            );

            _blockChain.MakeTransaction(userPrivateKey, new PolymorphicAction<ActionBase>[] { });
            queryResult = await ExecuteAsync(query);
            data = (Dictionary<string, object>)((ExecutionNode)queryResult.Data!).ToValue()!;
            Assert.Equal(
                new Dictionary<string, object>
                {
                    ["nextTxNonce"] = 1L
                },
                data
            );
        }

        [Fact]
        public async Task GetTx()
        {
            var userPrivateKey = new PrivateKey();
            var queryFormat = @"query {{
                getTx(txId: ""{0}"") {{
                    id
                    nonce
                    signer
                    signature
                    timestamp
                    updatedAddresses
                    actions {{
                        inspection
                    }}
                }}
            }}";
            var queryResult = await ExecuteAsync(string.Format(queryFormat, new TxId()));
            var data = (Dictionary<string, object?>)((ExecutionNode)queryResult.Data!).ToValue()!;
            Assert.Equal(
                new Dictionary<string, object?>
                {
                    ["getTx"] = null
                },
                data
            );

            var action = new CreateAvatar2
            {
                index = 0,
                hair = 1,
                lens = 2,
                ear = 3,
                tail = 4,
                name = "action",
            };
            var transaction = _blockChain.MakeTransaction(userPrivateKey, new PolymorphicAction<ActionBase>[] { action });
            _blockChain.StageTransaction(transaction);
            await _blockChain.MineBlock(new PrivateKey());
            queryResult = await ExecuteAsync(string.Format(queryFormat, transaction.Id.ToString()));
            var tx = (Transaction<PolymorphicAction<ActionBase>>)((RootExecutionNode)queryResult.Data.GetValue()).SubFields![0].Result!;

            Assert.Equal(tx.Id, transaction.Id);
            Assert.Equal(tx.Nonce, transaction.Nonce);
            Assert.Equal(tx.Signer, transaction.Signer);
            Assert.Equal(tx.Signature, transaction.Signature);
            Assert.Equal(tx.Timestamp, transaction.Timestamp);
            Assert.Equal(tx.UpdatedAddresses, transaction.UpdatedAddresses);

            var plainValue = tx.CustomActions!.First().PlainValue.Inspect(true);
            Assert.Equal(transaction.CustomActions!.First().PlainValue.Inspect(true), plainValue);
        }

        [Theory]
        [InlineData(null)]
        [InlineData(0)]
        [InlineData(100)]
        public async Task CreateUnsignedTx(long? nonce)
        {
            var privateKey = new PrivateKey();
            PublicKey publicKey = privateKey.PublicKey;
            Address signer = publicKey.ToAddress();
            long expectedNonce = nonce ?? _blockChain.GetNextTxNonce(signer);
            NCAction action = new CreateAvatar2
            {
                index = 0,
                hair = 1,
                lens = 2,
                ear = 3,
                tail = 4,
                name = "action",
            };

            var codec = new Codec();
            var queryFormat = @"query {{
                createUnsignedTx(publicKey: ""{0}"", plainValue: ""{1}"", nonce: {2})
            }}";
            var queryResult = await ExecuteAsync(string.Format(
                queryFormat,
                Convert.ToBase64String(publicKey.Format(false)),
                Convert.ToBase64String(codec.Encode(action.PlainValue)),
                expectedNonce.ToString()));
            var base64EncodedUnsignedTx = (string)(
                (Dictionary<string, object>)((ExecutionNode)queryResult.Data!).ToValue()!)["createUnsignedTx"];
            Transaction<NCAction> unsignedTx =
                Transaction<NCAction>.Deserialize(Convert.FromBase64String(base64EncodedUnsignedTx), validate: false);
            Assert.Empty(unsignedTx.Signature);
            Assert.Equal(publicKey, unsignedTx.PublicKey);
            Assert.Equal(signer, unsignedTx.Signer);
            Assert.Equal(expectedNonce, unsignedTx.Nonce);
            Assert.Contains(signer, unsignedTx.UpdatedAddresses);
        }

        [Theory]
        [InlineData("", "")]  // Empty String
        [InlineData(
            "026d06672c8d5c63c1a81c89fec5ab7faab523fd98895492d33ac32aa94dbd7c6c",
            "6475373a747970655f69647532323a636f6d62696e6174696f6e5f65717569706d656e743575363a76616c756573647531333a61" +
            "76617461724164647265737332303a467740d542f9ad3ac49ca3cb02cc9e3c761d22b975323a696431363a497d2e326a102a499a" +
            "b6d5ad1c56b37875383a726563697065496475313a3175393a736c6f74496e64657875313a307531313a73756252656369706549" +
            "646e6565"
        )]  // Not encoded by base64
        public async Task CreateUnsignedTxThrowsErrorsIfIncorrectArgumentPassed(string publicKey, string plainValue)
        {
            var queryFormat = @"query {{
                createUnsignedTx(publicKey: ""{0}"", plainValue: ""{1}"")
            }}";
            var result = await ExecuteAsync(string.Format(queryFormat, publicKey, plainValue));
            Assert.NotNull(result.Errors);
        }

        [Fact]
        public async Task AttachSignature()
        {
            var privateKey = new PrivateKey();
            PublicKey publicKey = privateKey.PublicKey;
            Address signer = publicKey.ToAddress();
            long nonce = _blockChain.GetNextTxNonce(signer);
            Transaction<NCAction> unsignedTx = Transaction<NCAction>.CreateUnsigned(
                nonce,
                publicKey,
                _blockChain.Genesis.Hash,
                ImmutableArray<NCAction>.Empty);
            byte[] serializedUnsignedTx = unsignedTx.Serialize(false);
            // ignore timestamp's millisecond over 6 digits.
            unsignedTx = Transaction<NCAction>.Deserialize(serializedUnsignedTx, false);
            byte[] signature = privateKey.Sign(serializedUnsignedTx);

            var queryFormat = @"query {{
                attachSignature(unsignedTransaction: ""{0}"", signature: ""{1}"")
            }}";
            var result = await ExecuteAsync(string.Format(
                queryFormat,
                Convert.ToBase64String(serializedUnsignedTx),
                Convert.ToBase64String(signature)));
            Assert.Null(result.Errors);
            var base64EncodedSignedTx = (string)(
                (Dictionary<string, object>)((ExecutionNode)result.Data!).ToValue()!)["attachSignature"];
            Transaction<NCAction> signedTx =
                Transaction<NCAction>.Deserialize(Convert.FromBase64String(base64EncodedSignedTx));
            Assert.Equal(signature, signedTx.Signature);
            Assert.Equal(unsignedTx.PublicKey, signedTx.PublicKey);
            Assert.Equal(unsignedTx.Signer, signedTx.Signer);
            Assert.Equal(unsignedTx.Nonce, signedTx.Nonce);
            Assert.Equal(unsignedTx.UpdatedAddresses, signedTx.UpdatedAddresses);
            Assert.Equal(unsignedTx.Timestamp, signedTx.Timestamp);
            Assert.Equal(unsignedTx.CustomActions, signedTx.CustomActions);
        }

        [Theory]
        [InlineData("", "")]  // Empty String
        [InlineData(
            "64313a616c65313a6733323a1fc6e506d66c8c04283309518f06f1ef21ed9329366b60380fbf3bf35668c72a313a6e693065313a" +
            "7036353a04338573b6979f6bafcd04bcbb299d4790370ae303053a74d7f9c5251fa8074a08206660afd47acb1a9d6873d7051513" +
            "fce050082a801d67f7320c8b2a8e587a2a313a7332303a6fe1c708ace1030b857f3dbb603df831f59b6940313a747532373a3230" +
            "32312d30372d30395430363a34333a33392e3436373835355a313a756c6565",
            "30440220433e7ef8055d89b1e7954af8f650aa1e19e11be16a6c0e6e606484f58d827f6502204030c18376184a24bff7933bb3d2" +
            "60994dc2d0c1721a939ae504fde6e3d8dc71"
        )]  // Not encoded by base64
        public async Task AttachSignatureThrowsErrorsIfIncorrectArgumentPassed(string unsignedTransaction, string signature)
        {
            var query = @$"query {{
                attachSignature(unsignedTransaction: ""{unsignedTransaction}"", signature: ""{signature}"")
            }}";
            var result = await ExecuteAsync(query);
            Assert.NotNull(result.Errors);
        }

        [Fact]
        public async Task TransactionResultIsStaging()
        {
            var privateKey = new PrivateKey();
            Transaction<NCAction> tx = Transaction<NCAction>.Create(
                0,
                privateKey,
                _blockChain.Genesis.Hash,
                ImmutableArray<NCAction>.Empty);
            _blockChain.StageTransaction(tx);
            var queryFormat = @"query {{
                transactionResult(txId: ""{0}"") {{
                    blockHash
                    txStatus
                }}
            }}";
            var result = await ExecuteAsync(string.Format(
                queryFormat,
                tx.Id.ToString()));
            Assert.NotNull(result.Data);
            var transactionResult =
                ((Dictionary<string, object>)((ExecutionNode)result.Data!).ToValue()!)["transactionResult"];
            var txStatus = (string)((Dictionary<string, object>)transactionResult)["txStatus"];
            Assert.Equal("STAGING", txStatus);
        }

        [Fact]
        public async Task TransactionResultIsInvalid()
        {
            var privateKey = new PrivateKey();
            Transaction<NCAction> tx = Transaction<NCAction>.Create(
                0,
                privateKey,
                _blockChain.Genesis.Hash,
                ImmutableArray<NCAction>.Empty);
            var queryFormat = @"query {{
                transactionResult(txId: ""{0}"") {{
                    blockHash
                    txStatus
                }}
            }}";
            var result = await ExecuteAsync(string.Format(
                queryFormat,
                tx.Id.ToString()));
            Assert.NotNull(result.Data);
            var transactionResult =
                ((Dictionary<string, object>)((ExecutionNode)result.Data!).ToValue()!)["transactionResult"];
            var txStatus = (string)((Dictionary<string, object>)transactionResult)["txStatus"];
            Assert.Equal("INVALID", txStatus);
        }

        [Fact]
        public async Task TransactionResultIsSuccess()
        {
            var privateKey = new PrivateKey();
            var action = new DumbTransferAction(new Address(), new Address());
            Transaction<NCAction> tx = _blockChain.MakeTransaction(privateKey, new NCAction[] { action });
            await _blockChain.MineBlock(new PrivateKey());
            var queryFormat = @"query {{
                transactionResult(txId: ""{0}"") {{
                    blockHash
                    txStatus
                }}
            }}";
            var result = await ExecuteAsync(string.Format(
                queryFormat,
                tx.Id.ToString()));
            Assert.NotNull(result.Data);
            var transactionResult =
                ((Dictionary<string, object>)((ExecutionNode)result.Data!).ToValue()!)["transactionResult"];
            var txStatus = (string)((Dictionary<string, object>)transactionResult)["txStatus"];
            Assert.Equal("SUCCESS", txStatus);
        }

        private Task<ExecutionResult> ExecuteAsync(string query)
        {
            return GraphQLTestUtils.ExecuteQueryAsync<TransactionHeadlessQuery>(query, standaloneContext: new StandaloneContext
            {
                BlockChain = _blockChain,
                Store = _store,
                NineChroniclesNodeService = _service
            });
        }
    }
}
