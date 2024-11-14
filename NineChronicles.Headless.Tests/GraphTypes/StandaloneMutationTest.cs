using GraphQL;
using Libplanet.Action;
using Libplanet.Action.State;
using Libplanet.Action.Sys;
using Libplanet.Blockchain;
using Libplanet.Common;
using Libplanet.Crypto;
using Libplanet.KeyStore;
using Libplanet.Store;
using Libplanet.Store.Trie;
using Libplanet.Types.Assets;
using Libplanet.Types.Blocks;
using Libplanet.Types.Consensus;
using Libplanet.Types.Tx;
using Nekoyume.Action;
using Nekoyume.Action.Loader;
using Nekoyume.Model;
using Nekoyume.Model.State;
using Nekoyume.TableData;
using NineChronicles.Headless.Tests.Common;
using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.IO;
using System.Linq;
using System.Numerics;
using System.Text;
using System.Threading.Tasks;
using Bencodex.Types;
using GraphQL.Execution;
using Lib9c;
using Lib9c.Tests;
using Nekoyume;
using Nekoyume.Module;
using NineChronicles.Headless.Executable.Commands;
using NineChronicles.Headless.Executable.IO;
using NineChronicles.Headless.Executable.Tests.IO;
using Xunit;
using Xunit.Abstractions;
using static NineChronicles.Headless.NCActionUtils;

namespace NineChronicles.Headless.Tests.GraphTypes
{
    public class StandaloneMutationTest : GraphQLTestBase
    {
        private readonly Dictionary<string, string> _sheets;

        private readonly TableSheets? _tableSheets = null;

        public StandaloneMutationTest(ITestOutputHelper output) : base(output)
        {
            _sheets = TableSheetsImporter.ImportSheets();
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

            var address = privateKey.Address;

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

        // FIXME: This test is not working because of the PoS reward distribution.
        // Need to fix this test.
        [Theory(Skip = "This feature cannot be tested under PoS reward distribution")]
        [InlineData(null, false)]
        [InlineData("", false)]
        [InlineData("memo", false)]
        [InlineData("_________________________________________________________________________________", true)]
        public async Task Transfer(string? memo, bool error)
        {
            NineChroniclesNodeService service = StandaloneContextFx.NineChroniclesNodeService!;
            Currency goldCurrency = new GoldCurrencyState(
                (Dictionary)BlockChain.GetNextWorldState().GetLegacyState(GoldCurrencyState.Address)
            ).Currency;

            Address senderAddress = service.MinerPrivateKey!.Address;
            var store = service.Store;
            Block block = BlockChain.ProposeBlock(
                service.MinerPrivateKey,
                lastCommit: GenerateBlockCommit(BlockChain.Tip.Index, BlockChain.Tip.Hash, GenesisValidators));
            BlockChain.Append(block, GenerateBlockCommit(block.Index, block.Hash, GenesisValidators));
            block = BlockChain.ProposeBlock(
                service.MinerPrivateKey,
                lastCommit: GenerateBlockCommit(BlockChain.Tip.Index, BlockChain.Tip.Hash, GenesisValidators));
            BlockChain.Append(block, GenerateBlockCommit(block.Index, block.Hash, GenesisValidators));

            // 10 + 10 (mining rewards)
            Assert.Equal(
                20 * goldCurrency,
                BlockChain.GetNextWorldState().GetBalance(senderAddress, goldCurrency)
            );

            var recipientKey = new PrivateKey();
            Address recipient = recipientKey.Address;
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

                Transaction? tx = BlockChain.StagePolicy.Get(
                    blockChain: BlockChain,
                    id: transferTxId,
                    filtered: true);
                Assert.NotNull(tx);
                Assert.IsType<TransferAsset>(ToAction(tx!.Actions!.Single()));
                TransferAsset transferAsset = (TransferAsset)ToAction(tx.Actions!.Single());
                Assert.Equal(memo, transferAsset.Memo);

                var expectedResult = new Dictionary<string, object>
                {
                    ["transfer"] = transferTxIdString,
                };

                Assert.Equal(expectedResult, data);

                block = BlockChain.ProposeBlock(
                    recipientKey,
                    lastCommit: GenerateBlockCommit(BlockChain.Tip.Index, BlockChain.Tip.Hash, GenesisValidators));
                BlockChain.Append(block, GenerateBlockCommit(block.Index, block.Hash, GenesisValidators));

                // 10 + 10 - 17.5(transfer)
                Assert.Equal(
                    FungibleAssetValue.Parse(goldCurrency, "2.5"),
                    BlockChain.GetNextWorldState().GetBalance(senderAddress, goldCurrency)
                );

                // 0 + 17.5(transfer) + 10(mining reward)
                Assert.Equal(
                    FungibleAssetValue.Parse(goldCurrency, "27.5"),
                    BlockChain.GetNextWorldState().GetBalance(recipient, goldCurrency)
                );
            }
        }

        // FIXME: This test is not working because of the PoS reward distribution.
        // Need to fix this test.
        [Fact(Skip = "This feature cannot be tested under PoS reward distribution")]
        public async Task TransferGold()
        {
            NineChroniclesNodeService service = StandaloneContextFx.NineChroniclesNodeService!;
            Currency goldCurrency = new GoldCurrencyState(
                (Dictionary)BlockChain.GetNextWorldState().GetLegacyState(GoldCurrencyState.Address)
            ).Currency;

            Address senderAddress = service.MinerPrivateKey!.Address;

            var store = service.Store;
            Block block = BlockChain.ProposeBlock(
                service.MinerPrivateKey,
                lastCommit: GenerateBlockCommit(BlockChain.Tip.Index, BlockChain.Tip.Hash, GenesisValidators));
            BlockChain.Append(block, GenerateBlockCommit(block.Index, block.Hash, GenesisValidators));
            block = BlockChain.ProposeBlock(
                service.MinerPrivateKey,
                lastCommit: GenerateBlockCommit(BlockChain.Tip.Index, BlockChain.Tip.Hash, GenesisValidators));
            BlockChain.Append(block, GenerateBlockCommit(block.Index, block.Hash, GenesisValidators));

            // 10 + 10 (mining rewards)
            Assert.Equal(
                20 * goldCurrency,
                BlockChain.GetNextWorldState().GetBalance(senderAddress, goldCurrency)
            );

            var recipientKey = new PrivateKey();
            Address recipient = recipientKey.Address;
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

            block = BlockChain.ProposeBlock(
                recipientKey,
                lastCommit: GenerateBlockCommit(BlockChain.Tip.Index, BlockChain.Tip.Hash, GenesisValidators));
            BlockChain.Append(block, GenerateBlockCommit(block.Index, block.Hash, GenesisValidators));

            // 10 + 10 - 17.5(transfer)
            Assert.Equal(
                FungibleAssetValue.Parse(goldCurrency, "2.5"),
                BlockChain.GetNextWorldState().GetBalance(senderAddress, goldCurrency)
            );

            // 0 + 17.5(transfer) + 10(mining reward)
            Assert.Equal(
                FungibleAssetValue.Parse(goldCurrency, "27.5"),
                BlockChain.GetNextWorldState().GetBalance(recipient, goldCurrency)
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
            Assert.Single(tx.Actions);
            var action = (CreateAvatar)ToAction(tx.Actions!.First());
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
            List<Guid> consumableIds,
            List<RuneSlotInfo> runeSlotInfos
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

            if (runeSlotInfos.Any())
            {
                queryArgs += $", runeSlotInfos: [{string.Join(",", runeSlotInfos.Select(r => $"{{slotIndex: {r.SlotIndex}, runeId: {r.RuneId}}}"))}]";
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
            Assert.Single(tx.Actions);
            var action = (HackAndSlash)ToAction(tx.Actions!.First());
            Assert.Equal(avatarAddress, action.AvatarAddress);
            Assert.Equal(worldId, action.WorldId);
            Assert.Equal(stageId, action.StageId);
            Assert.Equal(costumeIds.ToHashSet(), action.Costumes.ToHashSet());
            Assert.Equal(equipmentIds.ToHashSet(), action.Equipments.ToHashSet());
            Assert.Equal(consumableIds.ToHashSet(), action.Foods.ToHashSet());
            for (int i = 0; i < action.RuneInfos.Count; i++)
            {
                var runeSlotInfo = runeSlotInfos[i];
                Assert.Equal(i, runeSlotInfo.SlotIndex);
                Assert.Equal(i + 1, runeSlotInfo.RuneId);
            }
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
                new List<Guid>(),
                new List<RuneSlotInfo>(),
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
                },
                new List<RuneSlotInfo>
                {
                    new(0, 1),
                    new(1, 2),
                },
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
            Assert.Single(tx.Actions);
            var action = (DailyReward)ToAction(tx.Actions!.First());
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
            Assert.Single(tx.Actions);
            var action = (ChargeActionPoint)ToAction(tx.Actions!.First());
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
            Assert.Single(tx.Actions);
            var action = (CombinationEquipment)ToAction(tx.Actions!.First());
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
        public async Task ItemEnhancement(Address avatarAddress, Guid itemId, List<Guid> materialIds, int slotIndex)
        {
            var playerPrivateKey = new PrivateKey();
            var materialQuery = new StringBuilder("[");
            foreach (var materialId in materialIds)
            {
                materialQuery.Append($" \"{materialId}\"");
            }

            materialQuery.Append("]");
            var query = $@"mutation {{
                action {{
                    itemEnhancement(avatarAddress: ""{avatarAddress}"", itemId: ""{itemId}"", materialIds: {materialQuery}, slotIndex: {slotIndex})
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
            Assert.Single(tx.Actions);
            var action = (ItemEnhancement)ToAction(tx.Actions!.First());
            Assert.Equal(avatarAddress, action.avatarAddress);
            Assert.Equal(itemId, action.itemId);
            Assert.Equal(materialIds, action.materialIds);
            Assert.Equal(slotIndex, action.slotIndex);
        }

        public static IEnumerable<object?[]> ItemEnhancementMember => new List<object?[]>
        {
            new object?[]
            {
                new Address(),
                Guid.NewGuid(),
                new List<Guid> {Guid.NewGuid()},
                0,
            },
            new object?[]
            {
                new Address(),
                Guid.NewGuid(),
                new List<Guid> {Guid.NewGuid()},
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
            Assert.Single(tx.Actions);
            var action = (CombinationConsumable)ToAction(tx.Actions!.First());
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
        //     ActionBase createAvatar = new CreateAvatar2
        //     {
        //         index = 0,
        //         hair = 0,
        //         lens = 0,
        //         ear = 0,
        //         tail = 0,
        //         name = "avatar",
        //     };
        //     BlockChain.MakeTransaction(playerPrivateKey, new[] { createAvatar });
        //     await BlockChain.MineBlock(playerPrivateKey.Address);
        //
        //     Assert.NotNull(BlockChain.GetState(playerPrivateKey.Address));
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
        //     Assert.Single(tx.Actions);
        //     var action = (CancelMonsterCollect) tx.Actions!.First().InnerAction;
        //     Assert.Equal(1, action.level);
        //     Assert.Equal(0, action.collectRound);
        // }
        [Fact]
        public async Task Tx()
        {
            Block genesis =
                MakeGenesisBlock(
                    AdminAddress,
#pragma warning disable CS0618
                    // Use of obsolete method Currency.Legacy(): https://github.com/planetarium/lib9c/discussions/1319
                    Currency.Legacy("NCG", 2, null),
#pragma warning restore CS0618
                    ImmutableHashSet<Address>.Empty
                );
            NineChroniclesNodeService service = ServiceBuilder.CreateNineChroniclesNodeService(genesis, ProposerPrivateKey);

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

            Transaction tx =
                Transaction.Create(
                    0,
                    service.MinerPrivateKey!,
                    genesis.Hash,
                    new IValue[] { },
                    gasLimit: 4,
                    maxGasPrice: 1 * Currencies.Mead
                );
            string base64Encoded = Convert.ToBase64String(tx.Serialize());
            query = $"mutation {{ stageTx(payload: \"{base64Encoded}\") }}";
            result = await ExecuteQueryAsync(query);
            // Failed stageTransaction because Insufficient Gas fee.
            // FIXME restore this line
            // Assert.Single(result.Errors!);
            Assert.Null(result.Errors);
        }

        [Fact]
        public async Task Tx_V2()
        {
            Block genesis =
                MakeGenesisBlock(
                    AdminAddress,
#pragma warning disable CS0618
                    // Use of obsolete method Currency.Legacy(): https://github.com/planetarium/lib9c/discussions/1319
                    Currency.Legacy("NCG", 2, null),
#pragma warning restore CS0618
                    ImmutableHashSet<Address>.Empty
                );
            NineChroniclesNodeService service = ServiceBuilder.CreateNineChroniclesNodeService(genesis, ProposerPrivateKey);

            StandaloneContextFx.NineChroniclesNodeService = service;
            StandaloneContextFx.BlockChain = service.Swarm?.BlockChain;

            // Error: empty payload
            var query = $"mutation {{ stageTxV2(payload: \"\") }}";
            ExecutionResult result = await ExecuteQueryAsync(query);
            Assert.NotNull(result.Errors);
            Assert.Null(result.Data!);
            Transaction tx =
                Transaction.Create(
                    0,
                    service.MinerPrivateKey!,
                    genesis.Hash,
                    new IValue[] { },
                    gasLimit: 4,
                    maxGasPrice: 1 * Currencies.Mead
                );
            string base64Encoded = Convert.ToBase64String(tx.Serialize());
            query = $"mutation {{ stageTxV2(payload: \"{base64Encoded}\") }}";
            result = await ExecuteQueryAsync(query);
            // Failed stageTransaction because Insufficient Gas fee.
            // FIXME restore this line
            // Assert.Single(result.Errors!);
            Assert.Null(result.Errors);
        }

        [Fact]
        public async Task StageTransaction()
        {
            Block genesis =
                MakeGenesisBlock(
                    AdminAddress,
#pragma warning disable CS0618
                    // Use of obsolete method Currency.Legacy(): https://github.com/planetarium/lib9c/discussions/1319
                    Currency.Legacy("NCG", 2, null),
#pragma warning restore CS0618
                    ImmutableHashSet<Address>.Empty
                );
            NineChroniclesNodeService service = ServiceBuilder.CreateNineChroniclesNodeService(genesis, ProposerPrivateKey);

            StandaloneContextFx.NineChroniclesNodeService = service;
            StandaloneContextFx.BlockChain = service.Swarm?.BlockChain;

            var pk = new PrivateKey();
            ActionBase patronPrepareAction = new PrepareRewardAssets
            {
                RewardPoolAddress = MeadConfig.PatronAddress,
                Assets = new List<FungibleAssetValue>
                {
                    1 * Currencies.Mead,
                },
            };
            var tx = Transaction.Create(0, pk, BlockChain.Genesis.Hash, new[] { patronPrepareAction.PlainValue });
            var payload = ByteUtil.Hex(tx.Serialize());
            var stageTxMutation = $"mutation {{ stageTransaction(payload: \"{payload}\") }}";
            var stageTxResult = await ExecuteQueryAsync(stageTxMutation);
            Assert.Null(stageTxResult.Errors);

            var block = service.BlockChain.ProposeBlock(ProposerPrivateKey, null);
            service.BlockChain.Append(block, GenerateBlockCommit(1, block.Hash, new List<PrivateKey>() { ProposerPrivateKey }));

            ActionBase action = new ApprovePledge
            {
                PatronAddress = new PrivateKey().Address
            };
            tx = Transaction.Create(0, pk, BlockChain.Genesis.Hash, new[] { action.PlainValue });
            payload = ByteUtil.Hex(tx.Serialize());
            stageTxMutation = $"mutation {{ stageTransaction(payload: \"{payload}\") }}";
            stageTxResult = await ExecuteQueryAsync(stageTxMutation);
            var error = Assert.Single(stageTxResult.Errors!);
            Assert.Contains("gas", error.Message);
        }

        private Block MakeGenesisBlock(
            Address adminAddress,
            Currency currency,
            IImmutableSet<Address> activatedAccounts,
            RankingState0? rankingState = null)
        {
            return BlockChain.ProposeGenesisBlock(
                transactions: ImmutableList<Transaction>.Empty.Add(Transaction.Create(0,
                    AdminPrivateKey, null, new ActionBase[]
                    {
                        new InitializeStates(
                            validatorSet: new ValidatorSet(new List<Validator>
                            {
                                new Validator(ProposerPrivateKey.PublicKey, 10_000_000_000_000_000_000)
                            }),
                            rankingState: rankingState ?? new RankingState0(),
                            shopState: new ShopState(),
                            gameConfigState: new GameConfigState(_sheets[nameof(GameConfigSheet)]),
                            redeemCodeState: new RedeemCodeState(Bencodex.Types.Dictionary.Empty
                                .Add("address", RedeemCodeState.Address.Serialize())
                                .Add("map", Bencodex.Types.Dictionary.Empty)
                            ),
                            adminAddressState: new AdminState(adminAddress, 1500000),
                            activatedAccountsState: new ActivatedAccountsState(activatedAccounts),
                            goldCurrencyState: new GoldCurrencyState(currency),
                            goldDistributions: new GoldDistribution[0],
                            tableSheets: _sheets,
                            pendingActivationStates: new PendingActivationState[] { }
                        ),
                        new PrepareRewardAssets
                        {
                            RewardPoolAddress = MeadConfig.PatronAddress,
                            Assets = new List<FungibleAssetValue>
                            {
                                1 * Currencies.Mead
                            }
                        }
                    }.ToPlainValues())),
                privateKey: AdminPrivateKey);
        }
    }
}
