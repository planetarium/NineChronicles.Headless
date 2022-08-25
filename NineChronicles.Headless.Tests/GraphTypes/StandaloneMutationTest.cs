using GraphQL;
using Libplanet;
using Libplanet.Action;
using Libplanet.Assets;
using Libplanet.Blockchain;
using Libplanet.Blocks;
using Libplanet.Crypto;
using Libplanet.KeyStore;
using Libplanet.Tx;
using Nekoyume.Action;
using Nekoyume.Model;
using Nekoyume.Model.State;
using Nekoyume.TableData;
using NineChronicles.Headless.Tests.Common;
using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Threading.Tasks;
using Bencodex.Types;
using GraphQL.Execution;
using Lib9c.Tests;
using NineChronicles.Headless.Executable.Commands;
using NineChronicles.Headless.Executable.IO;
using NineChronicles.Headless.Executable.Tests.IO;
using Xunit;
using Xunit.Abstractions;
using NCAction = Libplanet.Action.PolymorphicAction<Nekoyume.Action.ActionBase>;

namespace NineChronicles.Headless.Tests.GraphTypes
{
    public class StandaloneMutationTest : GraphQLTestBase
    {
        private readonly Dictionary<string, string> _sheets;

        private readonly TableSheets? _tableSheets = null;

        public StandaloneMutationTest(ITestOutputHelper output) : base(output)
        {
            _sheets = TableSheetsImporter.ImportSheets(Path.Join("..", "..", "..", "..", "Lib9c", "Lib9c", "TableCSV"));
            _tableSheets = new TableSheets(_sheets);
        }

        [Fact]
        public async Task CreatePrivateKey()
        {
            // FIXME: passphrase로 "passphrase" 대신 랜덤 문자열을 사용하면 좋을 것 같습니다.
            var result = await ExecuteQueryAsync(
                "mutation { keyStore { createPrivateKey(passphrase: \"passphrase\") { publicKey { address } } } }");
            var data = (Dictionary<string, object>)((ExecutionNode)result.Data!).ToValue()!;
            var createdPrivateKeyAddress = (string)
                ((Dictionary<string, object>)
                    ((Dictionary<string, object>)
                        ((Dictionary<string, object>)
                            data["keyStore"])["createPrivateKey"])["publicKey"])["address"];

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
            var data = (Dictionary<string, object>)((ExecutionNode)result.Data!).ToValue()!;
            var privateKeyResult = (Dictionary<string, object>)
                ((Dictionary<string, object>)
                    data["keyStore"])["createPrivateKey"];
            var createdPrivateKeyHex = (string)privateKeyResult["hex"];
            var createdPrivateKeyAddress = (string)((Dictionary<string, object>)privateKeyResult["publicKey"])["address"];

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
            var data = (Dictionary<string, object>)((ExecutionNode)result.Data!).ToValue()!;
            var revokedPrivateKeyAddress = (string)
                ((Dictionary<string, object>)
                    ((Dictionary<string, object>)
                        data["keyStore"])["revokePrivateKey"])["address"];

            Assert.DoesNotContain(KeyStore.List(),
                t => t.Item2.Address.ToString() == revokedPrivateKeyAddress);
            Assert.Equal(address.ToString(), revokedPrivateKeyAddress);
        }

        [Fact]
        public async Task ActivateAccount()
        {
            var nonce = new byte[] { 0x00, 0x01, 0x02, 0x03 };
            var privateKey = new PrivateKey();
            (ActivationKey activationKey, PendingActivationState pendingActivation) =
                ActivationKey.Create(privateKey, nonce);
            NCAction action = new CreatePendingActivation(pendingActivation);
            BlockChain.MakeTransaction(AdminPrivateKey, new[] { action });
            await BlockChain.MineBlock(AdminPrivateKey);

            var encodedActivationKey = activationKey.Encode();
            var queryResult = await ExecuteQueryAsync(
                $"mutation {{ activationStatus {{ activateAccount(encodedActivationKey: \"{encodedActivationKey}\") }} }}");
            var data = (Dictionary<string, object>)((ExecutionNode)queryResult.Data!).ToValue()!;
            await BlockChain.MineBlock(AdminPrivateKey);

            var result =
                (bool)((Dictionary<string, object>)
                    data["activationStatus"])["activateAccount"];
            Assert.True(result);

            Address userAddress = StandaloneContextFx.NineChroniclesNodeService!.MinerPrivateKey!.ToAddress();
            IValue? state = BlockChain.GetState(
                userAddress.Derive(ActivationKey.DeriveKey)
            );
            Assert.True((Bencodex.Types.Boolean)state);
        }

        [Theory]
        [InlineData(null, false)]
        [InlineData("", false)]
        [InlineData("memo", false)]
        [InlineData("_________________________________________________________________________________", true)]
        public async Task Transfer(string? memo, bool error)
        {
            NineChroniclesNodeService service = StandaloneContextFx.NineChroniclesNodeService!;
            Currency goldCurrency = new GoldCurrencyState(
                (Dictionary)BlockChain.GetState(GoldCurrencyState.Address)
            ).Currency;

            Address senderAddress = service.MinerPrivateKey!.ToAddress();
            var store = service.Store;
            await BlockChain.MineBlock(service.MinerPrivateKey);
            await BlockChain.MineBlock(service.MinerPrivateKey);

            // 10 + 10 (mining rewards)
            Assert.Equal(
                20 * goldCurrency,
                BlockChain.GetBalance(senderAddress, goldCurrency)
            );

            var recipientKey = new PrivateKey();
            Address recipient = recipientKey.ToAddress();
            long txNonce = BlockChain.GetNextTxNonce(senderAddress);

            var args = $"recipient: \"{recipient}\", txNonce: {txNonce}, amount: \"17.5\"";
            if (!(memo is null))
            {
                args += $"memo: \"{memo}\"";
            }

            var query = $"mutation {{ transfer({args}) }}";
            ExecutionResult result = await ExecuteQueryAsync(query);
            var data = (Dictionary<string, object>)((ExecutionNode)result.Data!).ToValue()!;

            if (error)
            {
                Assert.NotNull(result.Errors);
            }
            else
            {
                Assert.Null(result.Errors);

                var stagedTxIds = BlockChain.GetStagedTransactionIds().ToImmutableList();
                Assert.Single(stagedTxIds);
                string transferTxIdString = stagedTxIds.Single().ToString();
                TxId transferTxId = new TxId(ByteUtil.ParseHex(transferTxIdString));

                Transaction<NCAction>? tx = BlockChain.StagePolicy.Get(
                    blockChain: BlockChain,
                    id: transferTxId,
                    filtered: true);
                Assert.NotNull(tx);
                Assert.IsType<TransferAsset>(tx!.CustomActions!.Single().InnerAction);
                TransferAsset transferAsset = (TransferAsset)tx.CustomActions!.Single().InnerAction;
                Assert.Equal(memo, transferAsset.Memo);

                var expectedResult = new Dictionary<string, object>
                {
                    ["transfer"] = transferTxIdString,
                };

                Assert.Equal(expectedResult, data);

                await BlockChain.MineBlock(recipientKey);

                // 10 + 10 - 17.5(transfer)
                Assert.Equal(
                    FungibleAssetValue.Parse(goldCurrency, "2.5"),
                    BlockChain.GetBalance(senderAddress, goldCurrency)
                );

                // 0 + 17.5(transfer) + 10(mining reward)
                Assert.Equal(
                    FungibleAssetValue.Parse(goldCurrency, "27.5"),
                    BlockChain.GetBalance(recipient, goldCurrency)
                );
            }
        }

        [Fact]
        public async Task TransferGold()
        {
            NineChroniclesNodeService service = StandaloneContextFx.NineChroniclesNodeService!;
            Currency goldCurrency = new GoldCurrencyState(
                (Dictionary)BlockChain.GetState(GoldCurrencyState.Address)
            ).Currency;

            Address senderAddress = service.MinerPrivateKey!.ToAddress();

            var store = service.Store;
            await BlockChain.MineBlock(service.MinerPrivateKey);
            await BlockChain.MineBlock(service.MinerPrivateKey);

            // 10 + 10 (mining rewards)
            Assert.Equal(
                20 * goldCurrency,
                BlockChain.GetBalance(senderAddress, goldCurrency)
            );

            var recipientKey = new PrivateKey();
            Address recipient = recipientKey.ToAddress();
            var query = $"mutation {{ transferGold(recipient: \"{recipient}\", amount: \"17.5\") }}";
            ExecutionResult result = await ExecuteQueryAsync(query);
            var data = (Dictionary<string, object>)((ExecutionNode)result.Data!).ToValue()!;

            var stagedTxIds = BlockChain.GetStagedTransactionIds().ToImmutableList();
            Assert.Single(stagedTxIds);

            var expectedResult = new Dictionary<string, object>
            {
                ["transferGold"] = stagedTxIds.Single().ToString(),
            };
            Assert.Null(result.Errors);
            Assert.Equal(expectedResult, data);

            await BlockChain.MineBlock(recipientKey);

            // 10 + 10 - 17.5(transfer)
            Assert.Equal(
                FungibleAssetValue.Parse(goldCurrency, "2.5"),
                BlockChain.GetBalance(senderAddress, goldCurrency)
            );

            // 0 + 17.5(transfer) + 10(mining reward)
            Assert.Equal(
                FungibleAssetValue.Parse(goldCurrency, "27.5"),
                BlockChain.GetBalance(recipient, goldCurrency)
            );
        }

        [Theory]
        [MemberData(nameof(CreateAvatarMember))]
        public async Task CreateAvatar(string name, int index, int hair, int lens, int ear, int tail)
        {
            var playerPrivateKey = new PrivateKey();
            var ranking = new RankingState();
            for (var i = 0; i < RankingState.RankingMapCapacity; i++)
            {
                ranking.RankingMap[RankingState.Derive(i)] = new HashSet<Address>().ToImmutableHashSet();
            }
            var query = $@"mutation {{
                action {{
                    createAvatar(avatarName: ""{name}"", avatarIndex: {index}, hairIndex: {hair}, lensIndex: {lens}, earIndex: {ear}, tailIndex: {tail})
                    }}
                }}";
            ExecutionResult result = await ExecuteQueryAsync(query);
            var data = (Dictionary<string, object>)((ExecutionNode)result.Data!).ToValue()!;
            Assert.Null(result.Errors);

            var txIds = BlockChain.GetStagedTransactionIds();
            Assert.Single(txIds);
            var tx = BlockChain.GetTransaction(txIds.First());
            var expected = new Dictionary<string, object>
            {
                ["action"] = new Dictionary<string, object>
                {
                    ["createAvatar"] = tx.Id.ToString(),
                }
            };
            Assert.Equal(expected, data!);
            Assert.Single(tx.CustomActions);
            var action = (CreateAvatar)tx.CustomActions!.First().InnerAction;
            Assert.Equal(name, action.name);
            Assert.Equal(index, action.index);
            Assert.Equal(hair, action.hair);
            Assert.Equal(lens, action.lens);
            Assert.Equal(ear, action.ear);
            Assert.Equal(tail, action.tail);
        }

        public static IEnumerable<object?[]> CreateAvatarMember => new List<object?[]>
        {
            new object?[]
            {
                "createByMutation",
                1,
                2,
                3,
                4,
                5,
            },
            new object?[]
            {
                "createByMutation2",
                2,
                3,
                4,
                5,
                6,
            },
            new object?[]
            {
                "createByMutation3",
                0,
                0,
                0,
                0,
                0,
            },
        };

        [Theory]
        [MemberData(nameof(HackAndSlashMember))]
        public async Task HackAndSlash(
            Address avatarAddress,
            int worldId,
            int stageId,
            List<Guid> costumeIds,
            List<Guid> equipmentIds,
            List<Guid> consumableIds
        )
        {
            var playerPrivateKey = new PrivateKey();
            var ranking = new RankingState();
            for (var i = 0; i < RankingState.RankingMapCapacity; i++)
            {
                ranking.RankingMap[RankingState.Derive(i)] = new HashSet<Address>().ToImmutableHashSet();
            }
            var queryArgs = $"avatarAddress: \"{avatarAddress}\", worldId: {worldId}, stageId: {stageId}";
            if (costumeIds.Any())
            {
                queryArgs += $", costumeIds: [{string.Join(",", costumeIds.Select(r => string.Format($"\"{r}\"")))}]";
            }
            if (equipmentIds.Any())
            {
                queryArgs += $", equipmentIds: [{string.Join(",", equipmentIds.Select(r => string.Format($"\"{r}\"")))}]";
            }
            if (consumableIds.Any())
            {
                queryArgs += $", consumableIds: [{string.Join(",", consumableIds.Select(r => string.Format($"\"{r}\"")))}]";
            }
            var query = @$"mutation {{ action {{ hackAndSlash({queryArgs}) }} }}";
            ExecutionResult result = await ExecuteQueryAsync(query);
            var data = (Dictionary<string, object>)((ExecutionNode)result.Data!).ToValue()!;
            Assert.Null(result.Errors);

            var txIds = BlockChain.GetStagedTransactionIds();
            Assert.Single(txIds);
            var tx = BlockChain.GetTransaction(txIds.First());
            var expected = new Dictionary<string, object>
            {
                ["action"] = new Dictionary<string, object>
                {
                    ["hackAndSlash"] = tx.Id.ToString(),
                }
            };
            Assert.Equal(expected, data);
            Assert.Single(tx.CustomActions);
            var action = (HackAndSlash)tx.CustomActions!.First().InnerAction;
            Assert.Equal(avatarAddress, action.AvatarAddress);
            Assert.Equal(worldId, action.WorldId);
            Assert.Equal(stageId, action.StageId);
            Assert.Equal(costumeIds, action.Costumes);
            Assert.Equal(equipmentIds, action.Equipments);
            Assert.Equal(consumableIds, action.Foods);
        }

        public static IEnumerable<object?[]> HackAndSlashMember => new List<object?[]>
        {
            new object?[]
            {
                new Address(),
                1,
                2,
                new List<Guid>(),
                new List<Guid>(),
                new List<Guid>()
            },
            new object?[]
            {
                new Address(),
                2,
                3,
                new List<Guid>
                {
                    Guid.NewGuid(),
                },
                new List<Guid>
                {
                    Guid.NewGuid(),
                    Guid.NewGuid(),
                },
                new List<Guid>
                {
                    Guid.NewGuid(),
                    Guid.NewGuid(),
                    Guid.NewGuid(),
                }
            },
            new object?[]
            {
                new Address(),
                2,
                3,
                new List<Guid>
                {
                    Guid.NewGuid(),
                },
                new List<Guid>
                {
                    Guid.NewGuid(),
                    Guid.NewGuid(),
                },
                new List<Guid>
                {
                    Guid.NewGuid(),
                    Guid.NewGuid(),
                    Guid.NewGuid(),
                }
            },
        };

        [Fact]
        public async Task DailyReward()
        {
            var playerPrivateKey = new PrivateKey();
            var ranking = new RankingState();
            for (var i = 0; i < RankingState.RankingMapCapacity; i++)
            {
                ranking.RankingMap[RankingState.Derive(i)] = new HashSet<Address>().ToImmutableHashSet();
            }
            var avatarAddress = new Address();
            var query = $@"mutation {{
                action {{
                    dailyReward(avatarAddress: ""{avatarAddress}"")
                    }}
                }}";
            ExecutionResult result = await ExecuteQueryAsync(query);
            var data = (Dictionary<string, object>)((ExecutionNode)result.Data!).ToValue()!;
            Assert.Null(result.Errors);

            var txIds = BlockChain.GetStagedTransactionIds();
            Assert.Single(txIds);
            var tx = BlockChain.GetTransaction(txIds.First());
            var expected = new Dictionary<string, object>
            {
                ["action"] = new Dictionary<string, object>
                {
                    ["dailyReward"] = tx.Id.ToString(),
                }
            };
            Assert.Equal(expected, data);
            Assert.Single(tx.CustomActions);
            var action = (DailyReward)tx.CustomActions!.First().InnerAction;
            Assert.Equal(avatarAddress, action.avatarAddress);
        }

        [Fact]
        public async Task ChargeActionPoint()
        {
            var playerPrivateKey = new PrivateKey();
            var ranking = new RankingState();
            for (var i = 0; i < RankingState.RankingMapCapacity; i++)
            {
                ranking.RankingMap[RankingState.Derive(i)] = new HashSet<Address>().ToImmutableHashSet();
            }
            var avatarAddress = new Address();
            var query = $@"mutation {{
                action {{
                    chargeActionPoint(avatarAddress: ""{avatarAddress}"")
                    }}
                }}";
            ExecutionResult result = await ExecuteQueryAsync(query);
            var data = (Dictionary<string, object>)((ExecutionNode)result.Data!).ToValue()!;
            Assert.Null(result.Errors);

            var txIds = BlockChain.GetStagedTransactionIds();
            Assert.Single(txIds);
            var tx = BlockChain.GetTransaction(txIds.First());
            var expected = new Dictionary<string, object>
            {
                ["action"] = new Dictionary<string, object>
                {
                    ["chargeActionPoint"] = tx.Id.ToString(),
                }
            };
            Assert.Equal(expected, data);
            Assert.Single(tx.CustomActions);
            var action = (ChargeActionPoint)tx.CustomActions!.First().InnerAction;
            Assert.Equal(avatarAddress, action.avatarAddress);
        }

        [Theory]
        [MemberData(nameof(CombinationEquipmentMember))]
        public async Task CombinationEquipment(Address avatarAddress, int recipeId, int slotIndex, int? subRecipeId)
        {
            var playerPrivateKey = new PrivateKey();
            var queryArgs = $"avatarAddress: \"{avatarAddress}\", recipeId: {recipeId} slotIndex: {slotIndex}";
            if (!(subRecipeId is null))
            {
                queryArgs += $"subRecipeId: {subRecipeId}";
            }
            var query = @$"mutation {{ action {{ combinationEquipment({queryArgs}) }} }}";
            var result = await ExecuteQueryAsync(query);
            var data = (Dictionary<string, object>)((ExecutionNode)result.Data!).ToValue()!;
            Assert.Null(result.Errors);

            var txIds = BlockChain.GetStagedTransactionIds();
            Assert.Single(txIds);
            var tx = BlockChain.GetTransaction(txIds.First());
            var expected = new Dictionary<string, object>
            {
                ["action"] = new Dictionary<string, object>
                {
                    ["combinationEquipment"] = tx.Id.ToString(),
                }
            };
            Assert.Equal(expected, data);
            Assert.Single(tx.CustomActions);
            var action = (CombinationEquipment)tx.CustomActions!.First().InnerAction;
            Assert.Equal(avatarAddress, action.avatarAddress);
            Assert.Equal(recipeId, action.recipeId);
            Assert.Equal(slotIndex, action.slotIndex);
            Assert.Equal(subRecipeId, action.subRecipeId);
        }

        public static IEnumerable<object?[]> CombinationEquipmentMember => new List<object?[]>
        {
            new object?[]
            {
                new Address(),
                1,
                0,
                0,
            },
            new object?[]
            {
                new Address(),
                1,
                2,
                1,
            },
            new object?[]
            {
                new Address(),
                2,
                3,
                null,
            },
        };

        [Theory]
        [MemberData(nameof(ItemEnhancementMember))]
        public async Task ItemEnhancement(Address avatarAddress, Guid itemId, Guid materialId, int slotIndex)
        {
            var playerPrivateKey = new PrivateKey();
            var query = $@"mutation {{
                action {{
                    itemEnhancement(avatarAddress: ""{avatarAddress}"", itemId: ""{itemId}"", materialId: ""{materialId}"", slotIndex: {slotIndex})
                }}
            }}";
            var result = await ExecuteQueryAsync(query);
            var data = (Dictionary<string, object>)((ExecutionNode)result.Data!).ToValue()!;
            Assert.Null(result.Errors);

            var txIds = BlockChain.GetStagedTransactionIds();
            Assert.Single(txIds);
            var tx = BlockChain.GetTransaction(txIds.First());
            var expected = new Dictionary<string, object>
            {
                ["action"] = new Dictionary<string, object>
                {
                    ["itemEnhancement"] = tx.Id.ToString(),
                }
            };
            Assert.Equal(expected, data);
            Assert.Single(tx.CustomActions);
            var action = (ItemEnhancement)tx.CustomActions!.First().InnerAction;
            Assert.Equal(avatarAddress, action.avatarAddress);
            Assert.Equal(itemId, action.itemId);
            Assert.Equal(materialId, action.materialId);
            Assert.Equal(slotIndex, action.slotIndex);
        }

        public static IEnumerable<object?[]> ItemEnhancementMember => new List<object?[]>
        {
            new object?[]
            {
                new Address(),
                Guid.NewGuid(),
                Guid.NewGuid(),
                0,
            },
            new object?[]
            {
                new Address(),
                Guid.NewGuid(),
                Guid.NewGuid(),
                3,
            },
        };


        [Theory]
        [MemberData(nameof(CombinationConsumableMember))]
        public async Task CombinationConsumable(Address avatarAddress, int recipeId, int slotIndex)
        {
            var playerPrivateKey = new PrivateKey();
            var query = $@"mutation {{
                action {{
                    combinationConsumable(avatarAddress: ""{avatarAddress}"", recipeId: {recipeId}, slotIndex: {slotIndex})
                }}
            }}";
            var result = await ExecuteQueryAsync(query);
            var data = (Dictionary<string, object>)((ExecutionNode)result.Data!).ToValue()!;
            Assert.Null(result.Errors);

            var txIds = BlockChain.GetStagedTransactionIds();
            Assert.Single(txIds);
            var tx = BlockChain.GetTransaction(txIds.First());
            var expected = new Dictionary<string, object>
            {
                ["action"] = new Dictionary<string, object>
                {
                    ["combinationConsumable"] = tx.Id.ToString(),
                }
            };
            Assert.Equal(expected, data);
            Assert.Single(tx.CustomActions);
            var action = (CombinationConsumable)tx.CustomActions!.First().InnerAction;
            Assert.Equal(avatarAddress, action.avatarAddress);
            Assert.Equal(recipeId, action.recipeId);
            Assert.Equal(slotIndex, action.slotIndex);
        }

        public static IEnumerable<object?[]> CombinationConsumableMember => new List<object?[]>
        {
            new object?[]
            {
                new Address(),
                1,
                0,
            },
            new object?[]
            {
                new Address(),
                2,
                3,
            },
        };

        [Fact]
        public async Task MonsterCollect()
        {
            const string query = @"mutation {
                action {
                    monsterCollect(level: 1)
                }
            }";

            PolymorphicAction<ActionBase> createAvatar = new CreateAvatar2
            {
                index = 0,
                hair = 0,
                lens = 0,
                ear = 0,
                tail = 0,
                name = "avatar",
            };
            var playerPrivateKey = StandaloneContextFx.NineChroniclesNodeService!.MinerPrivateKey!;
            BlockChain.MakeTransaction(playerPrivateKey, new[] { createAvatar });
            await BlockChain.MineBlock(playerPrivateKey);

            Assert.NotNull(BlockChain.GetState(playerPrivateKey.ToAddress()));
            var result = await ExecuteQueryAsync(query);
            var data = (Dictionary<string, object>)((ExecutionNode)result.Data!).ToValue()!;
            Assert.Null(result.Errors);

            var txIds = BlockChain.GetStagedTransactionIds();
            Assert.Single(txIds);
            var tx = BlockChain.GetTransaction(txIds.First());
            var expected = new Dictionary<string, object>
            {
                ["action"] = new Dictionary<string, object>
                {
                    ["monsterCollect"] = tx.Id.ToString(),
                }
            };
            Assert.Equal(expected, data);
            Assert.Single(tx.CustomActions);
            var action = (MonsterCollect)tx.CustomActions!.First().InnerAction;
            Assert.Equal(1, action.level);
        }

        [Fact]
        public async Task ClaimMonsterCollectionReward()
        {
            var playerPrivateKey = StandaloneContextFx.NineChroniclesNodeService!.MinerPrivateKey!;
            var avatarAddress = playerPrivateKey.ToAddress();
            string query = $@"mutation {{
                action {{
                    claimMonsterCollectionReward(avatarAddress: ""{avatarAddress}"")
                }}
            }}";

            PolymorphicAction<ActionBase> createAvatar = new CreateAvatar2
            {
                index = 0,
                hair = 0,
                lens = 0,
                ear = 0,
                tail = 0,
                name = "avatar",
            };
            BlockChain.MakeTransaction(playerPrivateKey, new[] { createAvatar });
            await BlockChain.MineBlock(playerPrivateKey);

            Assert.NotNull(BlockChain.GetState(playerPrivateKey.ToAddress()));

            var result = await ExecuteQueryAsync(query);
            var data = (Dictionary<string, object>)((ExecutionNode)result.Data!).ToValue()!;
            Assert.Null(result.Errors);

            var txIds = BlockChain.GetStagedTransactionIds();
            Assert.Single(txIds);
            var tx = BlockChain.GetTransaction(txIds.First());
            var expected = new Dictionary<string, object>
            {
                ["action"] = new Dictionary<string, object>
                {
                    ["claimMonsterCollectionReward"] = tx.Id.ToString(),
                }
            };
            Assert.Equal(expected, data);
            Assert.Single(tx.CustomActions);
            var action = (ClaimMonsterCollectionReward)tx.CustomActions!.First().InnerAction;
            Assert.Equal(avatarAddress, action.avatarAddress);
        }

        // [Fact]
        // public async Task CancelMonsterCollect()
        // {
        //     var playerPrivateKey = StandaloneContextFx.NineChroniclesNodeService!.MinerPrivateKey!;
        //
        //     const string query = @"mutation {
        //         action {
        //             cancelMonsterCollect(level: 1)
        //         }
        //     }";
        //
        //     PolymorphicAction<ActionBase> createAvatar = new CreateAvatar2
        //     {
        //         index = 0,
        //         hair = 0,
        //         lens = 0,
        //         ear = 0,
        //         tail = 0,
        //         name = "avatar",
        //     };
        //     BlockChain.MakeTransaction(playerPrivateKey, new[] { createAvatar });
        //     await BlockChain.MineBlock(playerPrivateKey.ToAddress());
        //
        //     Assert.NotNull(BlockChain.GetState(playerPrivateKey.ToAddress()));
        //
        //     var result = await ExecuteQueryAsync(query);
        //     Assert.Null(result.Errors);
        //
        //     var txIds = BlockChain.GetStagedTransactionIds();
        //     Assert.Single(txIds);
        //     var tx = BlockChain.GetTransaction(txIds.First());
        //     var expected = new Dictionary<string, object>
        //     {
        //         ["action"] = new Dictionary<string, object>
        //         {
        //             ["cancelMonsterCollect"] = tx.Id.ToString(),
        //         }
        //     };
        //     Assert.Equal(expected, result.Data!);
        //     Assert.Single(tx.CustomActions);
        //     var action = (CancelMonsterCollect) tx.CustomActions!.First().InnerAction;
        //     Assert.Equal(1, action.level);
        //     Assert.Equal(0, action.collectRound);
        // }
        [Fact]
        public async Task Tx()
        {
            Block<PolymorphicAction<ActionBase>> genesis =
                MakeGenesisBlock(
                    default,
#pragma warning disable CS0618
                    // Use of obsolete method Currency.Legacy(): https://github.com/planetarium/lib9c/discussions/1319
                    Currency.Legacy("NCG", 2, null),
#pragma warning restore CS0618
                    ImmutableHashSet<Address>.Empty
                );
            NineChroniclesNodeService service = ServiceBuilder.CreateNineChroniclesNodeService(genesis, new PrivateKey());

            StandaloneContextFx.NineChroniclesNodeService = service;
            StandaloneContextFx.BlockChain = service.Swarm?.BlockChain;

            // Error: empty payload
            var query = $"mutation {{ stageTx(payload: \"\") }}";
            ExecutionResult result = await ExecuteQueryAsync(query);
            var data = (Dictionary<string, object>)((ExecutionNode)result.Data!).ToValue()!;
            Assert.NotNull(result.Errors);
            Assert.Equal(
                new Dictionary<string, object>
                {
                    ["stageTx"] = false,
                },
                data
            );

            Transaction<PolymorphicAction<ActionBase>> tx =
                Transaction<PolymorphicAction<ActionBase>>.Create(
                    0,
                    service.MinerPrivateKey!,
                    genesis.Hash,
                    new PolymorphicAction<ActionBase>[] { }
                );
            string base64Encoded = Convert.ToBase64String(tx.Serialize(true));
            query = $"mutation {{ stageTx(payload: \"{base64Encoded}\") }}";
            result = await ExecuteQueryAsync(query);
            data = (Dictionary<string, object>)((ExecutionNode)result.Data!).ToValue()!;
            Assert.Null(result.Errors);
            Assert.Equal(
                new Dictionary<string, object>
                {
                    ["stageTx"] = true,
                },
                data
            );
            Block<PolymorphicAction<ActionBase>> mined =
                await BlockChain.MineBlock(service.MinerPrivateKey);
            Assert.Contains(tx, mined.Transactions);
        }

        [Fact]
        public async Task Tx_V2()
        {
            Block<PolymorphicAction<ActionBase>> genesis =
                MakeGenesisBlock(
                    default,
#pragma warning disable CS0618
                    // Use of obsolete method Currency.Legacy(): https://github.com/planetarium/lib9c/discussions/1319
                    Currency.Legacy("NCG", 2, null),
#pragma warning restore CS0618
                    ImmutableHashSet<Address>.Empty
                );
            NineChroniclesNodeService service = ServiceBuilder.CreateNineChroniclesNodeService(genesis, new PrivateKey());

            StandaloneContextFx.NineChroniclesNodeService = service;
            StandaloneContextFx.BlockChain = service.Swarm?.BlockChain;

            // Error: empty payload
            var query = $"mutation {{ stageTxV2(payload: \"\") }}";
            ExecutionResult result = await ExecuteQueryAsync(query);
            Assert.NotNull(result.Errors);
            Assert.Null(result.Data!);
            Transaction<PolymorphicAction<ActionBase>> tx =
                Transaction<PolymorphicAction<ActionBase>>.Create(
                    0,
                    service.MinerPrivateKey!,
                    genesis.Hash,
                    new PolymorphicAction<ActionBase>[] { }
                );
            string base64Encoded = Convert.ToBase64String(tx.Serialize(true));
            query = $"mutation {{ stageTxV2(payload: \"{base64Encoded}\") }}";
            result = await ExecuteQueryAsync(query);
            var data = (Dictionary<string, object>)((ExecutionNode)result.Data!).ToValue()!;
            Assert.Null(result.Errors);
            Assert.Equal(
                new Dictionary<string, object>
                {
                    ["stageTxV2"] = tx.Id.ToHex(),
                },
                data
            );
            Block<PolymorphicAction<ActionBase>> mined =
                await BlockChain.MineBlock(service.MinerPrivateKey!);
            Assert.Contains(tx, mined.Transactions);
        }

        [Fact]
        public async Task Tx_ActivateAccount()
        {
            var nonce = new byte[] { 0x00, 0x01, 0x02, 0x03 };
            var privateKey = new PrivateKey();
            (ActivationKey activationKey, PendingActivationState pendingActivation) =
                ActivationKey.Create(privateKey, nonce);
            NCAction action = new CreatePendingActivation(pendingActivation);
            BlockChain.MakeTransaction(AdminPrivateKey, new[] { action });
            await BlockChain.MineBlock(AdminPrivateKey);
            var encodedActivationKey = activationKey.Encode();
            var actionCommand = new ActionCommand(new StandardConsole());
            var filePath = Path.Combine(Path.GetTempPath(), Path.GetTempFileName());
            actionCommand.ActivateAccount(encodedActivationKey, ByteUtil.Hex(nonce), filePath);
            var console = new StringIOConsole();
            var txCommand = new TxCommand(console);
            var timeStamp = DateTimeOffset.UtcNow;
            txCommand.Sign(ByteUtil.Hex(privateKey.ByteArray), BlockChain.GetNextTxNonce(privateKey.ToAddress()), ByteUtil.Hex(BlockChain.Genesis.Hash.ByteArray), timeStamp.ToString(), new[] { filePath });
            var output = console.Out.ToString();
            output = output.Trim();
            var queryResult = await ExecuteQueryAsync(
                $"mutation {{ stageTx(payload: \"{output}\") }}");
            var data = (Dictionary<string, object>)((ExecutionNode)queryResult.Data!).ToValue()!;
            await BlockChain.MineBlock(AdminPrivateKey);

            var result = (bool)data["stageTx"];
            Assert.True(result);

            IValue? state = BlockChain.GetState(privateKey.ToAddress().Derive(ActivationKey.DeriveKey));
            Assert.True((Bencodex.Types.Boolean)state);
        }

        private Block<PolymorphicAction<ActionBase>> MakeGenesisBlock(
            Address adminAddress,
            Currency curreny,
            IImmutableSet<Address> activatedAccounts,
            RankingState0? rankingState = null
        ) => BlockChain<PolymorphicAction<ActionBase>>.MakeGenesisBlock(
            new PolymorphicAction<ActionBase>[]
            {
                new InitializeStates(
                    rankingState: rankingState ?? new RankingState0(),
                    shopState: new ShopState(),
                    gameConfigState: new GameConfigState(_sheets[nameof(GameConfigSheet)]),
                    redeemCodeState: new RedeemCodeState(Bencodex.Types.Dictionary.Empty
                        .Add("address", RedeemCodeState.Address.Serialize())
                        .Add("map", Bencodex.Types.Dictionary.Empty)
                    ),
                    adminAddressState: new AdminState(adminAddress, 1500000),
                    activatedAccountsState: new ActivatedAccountsState(activatedAccounts),
                    goldCurrencyState: new GoldCurrencyState(curreny),
                    goldDistributions: new GoldDistribution[0],
                    tableSheets: _sheets,
                    pendingActivationStates: new PendingActivationState[]{ }
                ),
            }, blockAction: ServiceBuilder.BlockPolicy.BlockAction
        );
    }
}
