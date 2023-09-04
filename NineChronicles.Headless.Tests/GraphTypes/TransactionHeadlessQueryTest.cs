using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Numerics;
using System.Threading.Tasks;
using Bencodex;
using Bencodex.Types;
using GraphQL;
using GraphQL.Execution;
using GraphQL.NewtonsoftJson;
using Libplanet;
using Libplanet.Action;
using Libplanet.Action.Loader;
using Libplanet.Action.Sys;
using Libplanet.Blockchain;
using Libplanet.Blockchain.Policies;
using Libplanet.Types.Blocks;
using Libplanet.Types.Consensus;
using Libplanet.Crypto;
using Libplanet.Store;
using Libplanet.Store.Trie;
using Libplanet.Types.Tx;
using Nekoyume.Action;
using Nekoyume.Action.Loader;
using NineChronicles.Headless.GraphTypes;
using NineChronicles.Headless.Tests.Common;
using NineChronicles.Headless.Utils;
using Xunit;
using static NineChronicles.Headless.NCActionUtils;

namespace NineChronicles.Headless.Tests.GraphTypes
{
    public class TransactionHeadlessQueryTest
    {
        private readonly BlockChain _blockChain;
        private readonly IStore _store;
        private readonly IStateStore _stateStore;
        private readonly NineChroniclesNodeService _service;
        private readonly PrivateKey _proposer = new PrivateKey();

        public TransactionHeadlessQueryTest()
        {
            _store = new DefaultStore(null);
            _stateStore = new TrieStateStore(new DefaultKeyValueStore(null));
            IBlockPolicy policy = NineChroniclesNodeService.GetTestBlockPolicy();
            var actionEvaluator = new ActionEvaluator(
                _ => policy.BlockAction,
                new BlockChainStates(_store, _stateStore),
                new NCActionLoader());
            Block genesisBlock = BlockChain.ProposeGenesisBlock(
                actionEvaluator,
                transactions: new IAction[]
                    {
                        new Initialize(
                            new ValidatorSet(
                                new[] { new Validator(_proposer.PublicKey, BigInteger.One) }
                                    .ToList()),
                            states: ImmutableDictionary.Create<Address, IImmutableDictionary<Address, IValue>>())
                    }.Select((sa, nonce) => Transaction.Create(nonce, new PrivateKey(), null, new[] { sa.PlainValue }))
                    .ToImmutableList(),
                privateKey: new PrivateKey()
            );

            _blockChain = BlockChain.Create(
                policy,
                new VolatileStagePolicy(),
                _store,
                _stateStore,
                genesisBlock,
                actionEvaluator);
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

            _blockChain.MakeTransaction(userPrivateKey, new ActionBase[] { });
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

            var action = new CreateAvatar()
            {
                index = 0,
                hair = 1,
                lens = 2,
                ear = 3,
                tail = 4,
                name = "action",
            };
            var transaction = _blockChain.MakeTransaction(userPrivateKey, new ActionBase[] { action });
            _blockChain.StageTransaction(transaction);
            Block block = _blockChain.ProposeBlock(_proposer);
            _blockChain.Append(block, GenerateBlockCommit(block.Index, block.Hash, _proposer));
            queryResult = await ExecuteAsync(string.Format(queryFormat, transaction.Id.ToString()));
            var tx = (Transaction)((RootExecutionNode)queryResult.Data.GetValue()).SubFields![0].Result!;

            Assert.Equal(tx.Id, transaction.Id);
            Assert.Equal(tx.Nonce, transaction.Nonce);
            Assert.Equal(tx.Signer, transaction.Signer);
            Assert.Equal(tx.Signature, transaction.Signature);
            Assert.Equal(tx.Timestamp, transaction.Timestamp);
            Assert.Equal(tx.UpdatedAddresses, transaction.UpdatedAddresses);

            var plainValue = ToAction(tx.Actions!.First()).PlainValue.Inspect(true);
            Assert.Equal(ToAction(transaction.Actions!.First()).PlainValue.Inspect(true), plainValue);
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
            ActionBase action = new CreateAvatar()
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
            IUnsignedTx unsignedTx =
                TxMarshaler.DeserializeUnsignedTx(Convert.FromBase64String(base64EncodedUnsignedTx));
            Assert.Equal(publicKey, unsignedTx.PublicKey);
            Assert.Equal(signer, unsignedTx.Signer);
            Assert.Equal(expectedNonce, unsignedTx.Nonce);
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
            IUnsignedTx unsignedTx = new UnsignedTx(
                new TxInvoice(genesisHash: _blockChain.Genesis.Hash),
                new TxSigningMetadata(publicKey, nonce));
            byte[] serializedUnsignedTx = unsignedTx.SerializeUnsignedTx().ToArray();
            // ignore timestamp's millisecond over 6 digits.
            unsignedTx = TxMarshaler.DeserializeUnsignedTx(serializedUnsignedTx);
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
            Transaction signedTx =
                Transaction.Deserialize(Convert.FromBase64String(base64EncodedSignedTx));
            Assert.Equal(signature, signedTx.Signature);
            Assert.Equal(unsignedTx.PublicKey, signedTx.PublicKey);
            Assert.Equal(unsignedTx.Signer, signedTx.Signer);
            Assert.Equal(unsignedTx.Nonce, signedTx.Nonce);
            Assert.Equal(unsignedTx.UpdatedAddresses, signedTx.UpdatedAddresses);
            Assert.Equal(unsignedTx.Timestamp, signedTx.Timestamp);
            Assert.Equal(unsignedTx.Actions, signedTx.Actions);
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
            Transaction tx = Transaction.Create(
                0,
                privateKey,
                _blockChain.Genesis.Hash,
                ImmutableArray<IValue>.Empty);
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
            Transaction tx = Transaction.Create(
                0,
                privateKey,
                _blockChain.Genesis.Hash,
                ImmutableArray<IValue>.Empty);
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

        /*[Fact]
        public async Task TransactionResultIsSuccess()
        {
            var privateKey = new PrivateKey();
            // Because `AddActivatedAccount` doesn't need any prerequisites.
            var action = new AddActivatedAccount(default);
            Transaction tx = _blockChain.MakeTransaction(privateKey, new ActionBase[] { action });
            Block block = _blockChain.ProposeBlock(_proposer);
            _blockChain.Append(block, GenerateBlockCommit(block.Index, block.Hash, _proposer));
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
        }*/

        private Task<ExecutionResult> ExecuteAsync(string query)
        {
            var currencyFactory = new CurrencyFactory(_blockChain.GetWorldState);
            var fungibleAssetValueFactory = new FungibleAssetValueFactory(currencyFactory);
            return GraphQLTestUtils.ExecuteQueryAsync<TransactionHeadlessQuery>(query, standaloneContext: new StandaloneContext
            {
                BlockChain = _blockChain,
                Store = _store,
                NineChroniclesNodeService = _service,
                CurrencyFactory = currencyFactory,
                FungibleAssetValueFactory = fungibleAssetValueFactory,
            });
        }

        private BlockCommit? GenerateBlockCommit(long height, BlockHash hash, PrivateKey validator)
        {
            return height != 0
                ? new BlockCommit(
                    height,
                    0,
                    hash,
                    ImmutableArray<Vote>.Empty
                        .Add(new VoteMetadata(
                            height,
                            0,
                            hash,
                            DateTimeOffset.UtcNow,
                            validator.PublicKey,
                            VoteFlag.PreCommit).Sign(validator)))
                : (BlockCommit?)null;
        }
    }
}
