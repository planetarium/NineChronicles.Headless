using System.Collections.Generic;
using System.Collections.Immutable;
using System.Threading.Tasks;
using Bencodex.Types;
using GraphQL;
using Libplanet;
using Libplanet.Action;
using Libplanet.Assets;
using Libplanet.Blockchain;
using Libplanet.Blocks;
using Libplanet.Crypto;
using Libplanet.KeyStore;
using Libplanet.Net;
using Libplanet.Standalone.Hosting;
using Nekoyume.Action;
using Nekoyume.Model;
using Nekoyume.Model.State;
using Xunit;
using Xunit.Abstractions;

namespace NineChronicles.Standalone.Tests.GraphTypes
{
    public class StandaloneMutationTest : GraphQLTestBase
    {
        public StandaloneMutationTest(ITestOutputHelper output) : base(output)
        {
        }

        [Fact]
        public async Task CreatePrivateKey()
        {
            // FIXME: passphrase로 "passphrase" 대신 랜덤 문자열을 사용하면 좋을 것 같습니다.
            var result = await ExecuteQueryAsync(
                "mutation { keyStore { createPrivateKey(passphrase: \"passphrase\") { publicKey { address } } } }");
            var createdPrivateKeyAddress = result.Data.As<Dictionary<string, object>>()["keyStore"]
                .As<Dictionary<string, object>>()["createPrivateKey"]
                .As<Dictionary<string, object>>()["publicKey"]
                .As<Dictionary<string, object>>()["address"].As<string>();

            Assert.Contains(KeyStore.List(),
                t => t.Item2.Address.ToString() == createdPrivateKeyAddress);
        }

        [Fact]
        public async Task CreatePrivateKeyWithGivenPrivateKey()
        {
            // FIXME: passphrase로 "passphrase" 대신 랜덤 문자열을 사용하면 좋을 것 같습니다.
            var privateKey = new PrivateKey();
            var privateKeyHex = ByteUtil.Hex(privateKey.ByteArray);
            var result = await ExecuteQueryAsync(
                $"mutation {{ keyStore {{ createPrivateKey(passphrase: \"passphrase\", privateKey: \"{privateKeyHex}\") {{ hex publicKey {{ address }} }} }} }}");
            var privateKeyResult = result.Data.As<Dictionary<string, object>>()["keyStore"]
                .As<Dictionary<string, object>>()["createPrivateKey"]
                .As<Dictionary<string, object>>();
            var createdPrivateKeyHex = privateKeyResult
                .As<Dictionary<string, object>>()["hex"].As<string>();
            var createdPrivateKeyAddress = privateKeyResult
                .As<Dictionary<string, object>>()["publicKey"]
                .As<Dictionary<string, object>>()["address"].As<string>();

            Assert.Equal(privateKey, new PrivateKey(ByteUtil.ParseHex(createdPrivateKeyHex)));
            Assert.Contains(KeyStore.List(),
                t => t.Item2.Address.ToString() == createdPrivateKeyAddress);
        }

        [Fact]
        public async Task RevokePrivateKey()
        {
            var privateKey = new PrivateKey();
            var passphrase = "";

            var protectedPrivateKey = ProtectedPrivateKey.Protect(privateKey, passphrase);
            KeyStore.Add(protectedPrivateKey);

            var address = privateKey.ToAddress();

            var result = await ExecuteQueryAsync(
                $"mutation {{ keyStore {{ revokePrivateKey(address: \"{address.ToHex()}\") {{ address }} }} }}");
            var revokedPrivateKeyAddress = result.Data.As<Dictionary<string, object>>()["keyStore"]
                .As<Dictionary<string, object>>()["revokePrivateKey"]
                .As<Dictionary<string, object>>()["address"].As<string>();

            Assert.DoesNotContain(KeyStore.List(),
                t => t.Item2.Address.ToString() == revokedPrivateKeyAddress);
            Assert.Equal(address.ToString(), revokedPrivateKeyAddress);
        }

        [Fact]
        public async Task ActivateAccount()
        {
            var adminPrivateKey = new PrivateKey();
            var adminAddress = adminPrivateKey.ToAddress();
            var activateAccounts = new[] { adminAddress }.ToImmutableHashSet();

            Block<PolymorphicAction<ActionBase>> genesis =
                BlockChain<PolymorphicAction<ActionBase>>.MakeGenesisBlock(
                    new PolymorphicAction<ActionBase>[]
                    {
                        new InitializeStates(
                            rankingState: new RankingState(),
                            shopState: new ShopState(),
                            gameConfigState: new GameConfigState(),
                            redeemCodeState: new RedeemCodeState(Bencodex.Types.Dictionary.Empty
                                .Add("address", RedeemCodeState.Address.Serialize())
                                .Add("map", Bencodex.Types.Dictionary.Empty)
                            ),
                            adminAddressState: new AdminState(adminAddress, 1500000),
                            activatedAccountsState: new ActivatedAccountsState(activateAccounts),
                            goldCurrencyState: new GoldCurrencyState(new Currency("NCG", 2, minter: null)),
                            goldDistributions: new GoldDistribution[0],
                            tableSheets: new Dictionary<string, string>(),
                            pendingActivationStates: new PendingActivationState[]{ }
                        ),
                    }
                );

            var apvPrivateKey = new PrivateKey();
            var apv = AppProtocolVersion.Sign(apvPrivateKey, 0);
            var userPrivateKey = new PrivateKey();
            var properties = new LibplanetNodeServiceProperties<PolymorphicAction<ActionBase>>
            {
                Host = System.Net.IPAddress.Loopback.ToString(),
                AppProtocolVersion = apv,
                GenesisBlock = genesis,
                StorePath = null,
                StoreStatesCacheSize = 2,
                PrivateKey = userPrivateKey,
                Port = null,
                MinimumDifficulty = 4096,
                NoMiner = true,
                Render = false,
                Peers = ImmutableHashSet<Peer>.Empty,
                TrustedAppProtocolVersionSigners = null,
            };

            var service = new NineChroniclesNodeService(properties, null);
            service.PrivateKey = userPrivateKey;
            StandaloneContextFx.NineChroniclesNodeService = service;
            StandaloneContextFx.BlockChain = service.Swarm.BlockChain;

            var blockChain = StandaloneContextFx.BlockChain;

            var nonce = new byte[] {0x00, 0x01, 0x02, 0x03};
            var privateKey = new PrivateKey();
            (ActivationKey activationKey, PendingActivationState pendingActivation) =
                ActivationKey.Create(privateKey, nonce);
            PolymorphicAction<ActionBase> action = new CreatePendingActivation(pendingActivation);
            blockChain.MakeTransaction(adminPrivateKey, new[] {action});
            await blockChain.MineBlock(adminAddress);

            var encodedActivationKey = activationKey.Encode();
            var queryResult = await ExecuteQueryAsync(
                $"mutation {{ activationStatus {{ activateAccount(encodedActivationKey: \"{encodedActivationKey}\") }} }}");
            await blockChain.MineBlock(adminAddress);

            var result = (bool)queryResult.Data
                .As<Dictionary<string, object>>()["activationStatus"]
                .As<Dictionary<string, object>>()["activateAccount"];
            Assert.True(result);

            var state = (Bencodex.Types.Dictionary)blockChain.GetState(
                ActivatedAccountsState.Address);
            var activatedAccountsState = new ActivatedAccountsState(state);
            var userAddress = userPrivateKey.ToAddress();
            Assert.True(activatedAccountsState.Accounts.Contains(userAddress));
        }

        [Fact]
        public async Task TransferGold()
        {
            var senderPrivateKey = new PrivateKey();
            Address senderAddress = senderPrivateKey.ToAddress();
            var goldCurrency = new Currency("NCG", 2, minter: null);
            Block<PolymorphicAction<ActionBase>> genesis =
                BlockChain<PolymorphicAction<ActionBase>>.MakeGenesisBlock(
                    new PolymorphicAction<ActionBase>[]
                    {
                        new InitializeStates(
                            rankingState: new RankingState(),
                            shopState: new ShopState(),
                            gameConfigState: new GameConfigState(),
                            redeemCodeState: new RedeemCodeState(Bencodex.Types.Dictionary.Empty
                                .Add("address", RedeemCodeState.Address.Serialize())
                                .Add("map", Bencodex.Types.Dictionary.Empty)
                            ),
                            adminAddressState: new AdminState(default, 0),
                            activatedAccountsState: new ActivatedAccountsState(),
                            goldCurrencyState: new GoldCurrencyState(goldCurrency),
                            goldDistributions: new GoldDistribution[0],
                            tableSheets: new Dictionary<string, string>(),
                            pendingActivationStates: new PendingActivationState[]{ }
                        ),
                    }
                );
            var properties = new LibplanetNodeServiceProperties<PolymorphicAction<ActionBase>>
            {
                Host = System.Net.IPAddress.Loopback.ToString(),
                AppProtocolVersion = default,
                GenesisBlock = genesis,
                StorePath = null,
                StoreStatesCacheSize = 2,
                PrivateKey = senderPrivateKey,
                Port = null,
                MinimumDifficulty = 4096,
                NoMiner = true,
                Render = false,
                Peers = ImmutableHashSet<Peer>.Empty,
                TrustedAppProtocolVersionSigners = null,
            };
            var service = new NineChroniclesNodeService(properties, null)
            {
                PrivateKey = senderPrivateKey
            };
            StandaloneContextFx.NineChroniclesNodeService = service;
            StandaloneContextFx.BlockChain = service.Swarm.BlockChain;

            var blockChain = StandaloneContextFx.BlockChain;
            await blockChain.MineBlock(senderAddress);
            await blockChain.MineBlock(senderAddress);

            // 10 + 10 (mining rewards)
            Assert.Equal(
                20 * goldCurrency, 
                blockChain.GetBalance(senderAddress, goldCurrency)
            );

            Address recipient = new PrivateKey().ToAddress();
            var query = $"mutation {{ transferGold(recipient: \"{recipient}\", amount: \"17.5\") }}";
            ExecutionResult result = await ExecuteQueryAsync(query);

            var expectedResult = new Dictionary<string, object>
            {
                ["transferGold"] = true,
            };
            Assert.Null(result.Errors);
            Assert.Equal(expectedResult, result.Data);
            
            await blockChain.MineBlock(recipient);

            // 10 + 10 - 17.5(transfer)
            Assert.Equal(
                FungibleAssetValue.Parse(goldCurrency, "2.5"),
                blockChain.GetBalance(senderAddress, goldCurrency)
            );

            // 0 + 17.5(transfer) + 10(mining reward)
            Assert.Equal(
                FungibleAssetValue.Parse(goldCurrency, "27.5"),
                blockChain.GetBalance(recipient, goldCurrency)
            );
        }

        [Fact]
        public async Task CreateAvatar()
        {
            var playerPrivateKey = new PrivateKey();
            Address playerAddress = playerPrivateKey.ToAddress();
            var goldCurrency = new Currency("NCG", 2, minter: null);
            var sheets = TableSheetsImporter.ImportSheets();
            var ranking = new RankingState();
            for (var i = 0; i < RankingState.RankingMapCapacity; i++)
            {
                ranking.RankingMap[RankingState.Derive(i)] = new HashSet<Address>().ToImmutableHashSet();
            }
            Block<PolymorphicAction<ActionBase>> genesis =
                BlockChain<PolymorphicAction<ActionBase>>.MakeGenesisBlock(
                    new PolymorphicAction<ActionBase>[]
                    {
                        new InitializeStates(
                            rankingState: ranking,
                            shopState: new ShopState(),
                            gameConfigState: new GameConfigState(),
                            redeemCodeState: new RedeemCodeState(Bencodex.Types.Dictionary.Empty
                                .Add("address", RedeemCodeState.Address.Serialize())
                                .Add("map", Bencodex.Types.Dictionary.Empty)
                            ),
                            adminAddressState: new AdminState(default, 0),
                            activatedAccountsState: new ActivatedAccountsState(),
                            goldCurrencyState: new GoldCurrencyState(goldCurrency),
                            goldDistributions: new GoldDistribution[0],
                            tableSheets: sheets,
                            pendingActivationStates: new PendingActivationState[]{ }
                        ),
                    }
                );
            var properties = new LibplanetNodeServiceProperties<PolymorphicAction<ActionBase>>
            {
                Host = System.Net.IPAddress.Loopback.ToString(),
                AppProtocolVersion = default,
                GenesisBlock = genesis,
                StorePath = null,
                StoreStatesCacheSize = 2,
                PrivateKey = playerPrivateKey,
                Port = null,
                MinimumDifficulty = 4096,
                NoMiner = true,
                Render = false,
                Peers = ImmutableHashSet<Peer>.Empty,
                TrustedAppProtocolVersionSigners = null,
            };
            var service = new NineChroniclesNodeService(properties, null)
            {
                PrivateKey = playerPrivateKey
            };
            StandaloneContextFx.NineChroniclesNodeService = service;
            StandaloneContextFx.BlockChain = service.Swarm.BlockChain;

            var blockChain = StandaloneContextFx.BlockChain;

            var query = $"mutation {{ action {{ createAvatar }} }}";
            ExecutionResult result = await ExecuteQueryAsync(query);

            var isCreated = (bool)result.Data
                .As<Dictionary<string, object>>()["action"]
                .As<Dictionary<string, object>>()["createAvatar"];

            Assert.True(isCreated);

            await blockChain.MineBlock(playerAddress);
            var playerState = (Bencodex.Types.Dictionary)blockChain.GetState(playerAddress);
            var agentState = new AgentState(playerState);

            Assert.Equal(playerAddress, agentState.address);
            Assert.True(agentState.avatarAddresses.ContainsKey(0));

            var avatar = new AvatarState((Bencodex.Types.Dictionary)blockChain.GetState(agentState.avatarAddresses[0]));

            Assert.Equal("createbymutation", avatar.name);
            
        }
    }
}
