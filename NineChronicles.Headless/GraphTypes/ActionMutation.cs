using Bencodex.Types;
using GraphQL;
using GraphQL.Types;
using Libplanet;
using Libplanet.Assets;
using Libplanet.Blockchain;
using Libplanet.Explorer.GraphTypes;
using Libplanet.Tx;
using Nekoyume.Action;
using Nekoyume.Model.State;
using Serilog;
using System;
using System.Collections.Generic;
using Libplanet.Action;
using NCAction = Libplanet.Action.PolymorphicAction<Nekoyume.Action.ActionBase>;

namespace NineChronicles.Headless.GraphTypes
{
    public class ActionMutation : ObjectGraphType
    {
        public ActionMutation(NineChroniclesNodeService service)
        {
            Field<NonNullGraphType<TxIdType>>("createAvatar")
                .Description("Create new avatar.")
                .Argument<string>("avatarName", false, "Avatar name.")
                .Argument<int>("avatarIndex", false, "The index of character slot. 0 ~ 2")
                .Argument<int>("hairIndex", false, "The index of character hair color. 0 ~ 8")
                .Argument<int>("lensIndex", false, "The index of character eye color. 0 ~ 8")
                .Argument<int>("earIndex", false, "The index of character ear color. 0 ~ 8")
                .Argument<int>("tailIndex", false, "The index of character tail color. 0 ~ 8")
                .Resolve(context =>
                {
                    try
                    {
                        if (!(service.MinerPrivateKey is { } privateKey))
                        {
                            throw new InvalidOperationException($"{nameof(service.MinerPrivateKey)} is null.");
                        }

                        if (!(service.Swarm?.BlockChain is { } blockChain))
                        {
                            throw new InvalidOperationException($"{nameof(service.Swarm.BlockChain)} is null.");
                        }

                        var avatarName = context.GetArgument<string>("avatarName");
                        var avatarIndex = context.GetArgument<int>("avatarIndex");
                        var hairIndex = context.GetArgument<int>("hairIndex");
                        var lensIndex = context.GetArgument<int>("lensIndex");
                        var earIndex = context.GetArgument<int>("earIndex");
                        var tailIndex = context.GetArgument<int>("tailIndex");
                        var action = new CreateAvatar
                        {
                            index = avatarIndex,
                            hair = hairIndex,
                            lens = lensIndex,
                            ear = earIndex,
                            tail = tailIndex,
                            name = avatarName,
                        };

                        var actions = new NCAction[] { action };
                        Transaction<NCAction> tx = blockChain.MakeTransaction(privateKey, actions);
                        return tx.Id;
                    }
                    catch (Exception e)
                    {
                        var msg = $"Unexpected exception occurred during {typeof(ActionMutation)}: {e}";
                        context.Errors.Add(new ExecutionError(msg, e));
                        Log.Error(msg, e);
                        throw;
                    }
                });

            Field<NonNullGraphType<TxIdType>>("hackAndSlash")
                .Description("Start stage to get material.")
                .Argument<Address>("avatarAddress", false, "Avatar address.")
                .Argument<int>("worldId", false, "World ID containing the stage ID.")
                .Argument<int>("stageId", false, "Stage ID.")
                .Argument<ListGraphType<GuidGraphType>>(
                    "costumeIds",
                    "List of costume id for equip.")
                .Argument<ListGraphType<GuidGraphType>>(
                    "equipmentIds",
                    "List of equipment id for equip.")
                .Argument<ListGraphType<GuidGraphType>>(
                    "consumableIds",
                    "List of consumable id for use.")
                .Argument<ListGraphType<NonNullGraphType<RuneSlotInfoInputType>>>(
                    "runeSlotInfos",
                    "List of rune slot info for equip.",
                    arg => arg.DefaultValue = new List<RuneSlotInfo>())
                .Resolve(context =>
                {
                    try
                    {
                        BlockChain<NCAction>? blockChain = service.Swarm.BlockChain;
                        if (blockChain is null)
                        {
                            throw new InvalidOperationException($"{nameof(blockChain)} is null.");
                        }

                        Address avatarAddress = context.GetArgument<Address>("avatarAddress");
                        int worldId = context.GetArgument<int>("worldId");
                        int stageId = context.GetArgument<int>("stageId");
                        Address rankingMapAddress = context.GetArgument<Address>("rankingMapAddress");
                        List<Guid> costumeIds = context.GetArgument<List<Guid>>("costumeIds") ?? new List<Guid>();
                        List<Guid> equipmentIds = context.GetArgument<List<Guid>>("equipmentIds") ?? new List<Guid>();
                        List<Guid> consumableIds = context.GetArgument<List<Guid>>("consumableIds") ?? new List<Guid>();
                        List<RuneSlotInfo> runeSlotInfos = context.GetArgument<List<RuneSlotInfo>>("runeSlotInfos");

                        var action = new HackAndSlash
                        {
                            AvatarAddress = avatarAddress,
                            WorldId = worldId,
                            StageId = stageId,
                            Costumes = costumeIds,
                            Equipments = equipmentIds,
                            Foods = consumableIds,
                            RuneInfos = runeSlotInfos,
                        };

                        var actions = new NCAction[] { action };
                        Transaction<NCAction> tx = blockChain.MakeTransaction(service.MinerPrivateKey, actions);
                        return tx.Id;
                    }
                    catch (Exception e)
                    {
                        var msg = $"Unexpected exception occurred during {typeof(ActionMutation)}: {e}";
                        context.Errors.Add(new ExecutionError(msg, e));
                        Log.Error(msg, e);
                        throw;
                    }
                });

            Field<NonNullGraphType<TxIdType>>("combinationEquipment")
                .Description("Combine new equipment.")
                .Argument<Address>(
                    "avatarAddress",
                    false,
                    "Avatar address to create equipment.")
                .Argument<int>(
                    "recipeId",
                    false,
                    "EquipmentRecipe ID from EquipmentRecipeSheet.")
                .Argument<int>(
                    "slotIndex",
                    false,
                    "The empty combination slot index to combine equipment. 0 ~ 3")
                .Argument<int?>(
                    "subRecipeId",
                    true,
                    "EquipmentSubRecipe ID from EquipmentSubRecipeSheet.")
                .Resolve(context =>
                {
                    try
                    {
                        BlockChain<NCAction>? blockChain = service.BlockChain;
                        if (blockChain is null)
                        {
                            throw new InvalidOperationException($"{nameof(blockChain)} is null.");
                        }

                        int recipeId = context.GetArgument<int>("recipeId");
                        int slotIndex = context.GetArgument<int>("slotIndex");
                        int? subRecipeId = context.GetArgument<int?>("subRecipeId");
                        Address avatarAddress = context.GetArgument<Address>("avatarAddress");

                        var action = new CombinationEquipment
                        {
                            avatarAddress = avatarAddress,
                            recipeId = recipeId,
                            slotIndex = slotIndex,
                            subRecipeId = subRecipeId
                        };

                        var actions = new NCAction[] { action };
                        Transaction<NCAction> tx = blockChain.MakeTransaction(service.MinerPrivateKey, actions);
                        return tx.Id;
                    }
                    catch (Exception e)
                    {
                        var msg = $"Unexpected exception occurred during {typeof(ActionMutation)}: {e}";
                        context.Errors.Add(new ExecutionError(msg, e));
                        Log.Error(msg, e);
                        throw;
                    }
                });

            Field<NonNullGraphType<TxIdType>>("itemEnhancement")
                .Description("Upgrade equipment.")
                .Argument<Address>(
                    "avatarAddress",
                    false,
                    "Avatar address to upgrade equipment.")
                .Argument<Guid>(
                    "itemId",
                    false,
                    "Equipment Guid for upgrade.")
                .Argument<Guid>(
                    "materialId",
                    false,
                    "Material Guid for equipment upgrade.")
                .Argument<int>(
                    "slotIndex",
                    false,
                    "The empty combination slot index to upgrade equipment. 0 ~ 3")
                .Resolve(context =>
                {
                    try
                    {
                        if (!(service.MinerPrivateKey is { } privatekey))
                        {
                            throw new InvalidOperationException($"{nameof(service.MinerPrivateKey)} is null.");
                        }

                        if (!(service.Swarm?.BlockChain is { } blockChain))
                        {
                            throw new InvalidOperationException($"{nameof(service.Swarm.BlockChain)} is null.");
                        }

                        Guid itemId = context.GetArgument<Guid>("itemId");
                        Guid materialId = context.GetArgument<Guid>("materialId");
                        Address avatarAddress = context.GetArgument<Address>("avatarAddress");
                        int slotIndex = context.GetArgument<int>("slotIndex");

                        var action = new ItemEnhancement
                        {
                            avatarAddress = avatarAddress,
                            slotIndex = slotIndex,
                            itemId = itemId,
                            materialId = materialId,
                        };

                        var actions = new NCAction[] { action };
                        Transaction<NCAction> tx = blockChain.MakeTransaction(privatekey, actions);
                        return tx.Id;
                    }
                    catch (Exception e)
                    {
                        var msg = $"Unexpected exception occurred during {typeof(ActionMutation)}: {e}";
                        context.Errors.Add(new ExecutionError(msg, e));
                        Log.Error(msg, e);
                        throw;
                    }
                });

            Field<NonNullGraphType<TxIdType>>("dailyReward")
                .Description("Get daily reward.")
                .Argument<Address>(
                    "avatarAddress",
                    false,
                    "Avatar address to receive reward.")
                .Resolve(context =>
                {
                    try
                    {
                        if (!(service.MinerPrivateKey is { } privateKey))
                        {
                            throw new InvalidOperationException($"{nameof(service.MinerPrivateKey)} is null.");
                        }

                        if (!(service.BlockChain is { } blockChain))
                        {
                            throw new InvalidOperationException($"{nameof(service.Swarm.BlockChain)} is null.");
                        }

                        Address avatarAddress = context.GetArgument<Address>("avatarAddress");

                        var action = new DailyReward
                        {
                            avatarAddress = avatarAddress
                        };

                        var actions = new NCAction[] { action };
                        Transaction<NCAction> tx = blockChain.MakeTransaction(privateKey, actions);
                        return tx.Id;
                    }
                    catch (Exception e)
                    {
                        var msg = $"Unexpected exception occurred during {typeof(ActionMutation)}: {e}";
                        context.Errors.Add(new ExecutionError(msg, e));
                        Log.Error(msg, e);
                        throw;
                    }
                });
            Field<NonNullGraphType<TxIdType>>("chargeActionPoint")
                .Description("Charge Action Points using Material.")
                .Argument<Address>(
                    "avatarAddress",
                    false,
                    "Avatar to use potion.")
                .Resolve(context =>
                {
                    try
                    {
                        if (!(service.MinerPrivateKey is { } privateKey))
                        {
                            throw new InvalidOperationException($"{nameof(service.MinerPrivateKey)} is null.");
                        }

                        if (!(service.BlockChain is { } blockChain))
                        {
                            throw new InvalidOperationException($"{nameof(service.Swarm.BlockChain)} is null.");
                        }

                        Address avatarAddress = context.GetArgument<Address>("avatarAddress");

                        var action = new ChargeActionPoint
                        {
                            avatarAddress = avatarAddress
                        };

                        var actions = new NCAction[] { action };
                        Transaction<NCAction> tx = blockChain.MakeTransaction(privateKey, actions);
                        return tx.Id;
                    }
                    catch (Exception e)
                    {
                        var msg = $"Unexpected exception occurred during {typeof(ActionMutation)}: {e}";
                        context.Errors.Add(new ExecutionError(msg, e));
                        Log.Error(msg, e);
                        throw;
                    }
                });

            Field<NonNullGraphType<TxIdType>>("combinationConsumable")
                .Description("Combine new Consumable.")
                .Argument<Address>(
                    "avatarAddress",
                    false,
                    "Avatar address to combine consumable.")
                .Argument<int>(
                    "recipeId",
                    false,
                    "ConsumableRecipe ID from ConsumableRecipeSheet.")
                .Argument<int>(
                    "slotIndex",
                    false,
                    "The empty combination slot index to combine consumable. 0 ~ 3")
                .Resolve(context =>
                {
                    try
                    {
                        BlockChain<NCAction>? blockChain = service.BlockChain;
                        if (blockChain is null)
                        {
                            throw new InvalidOperationException($"{nameof(blockChain)} is null.");
                        }

                        int recipeId = context.GetArgument<int>("recipeId");
                        int slotIndex = context.GetArgument<int>("slotIndex");
                        Address avatarAddress = context.GetArgument<Address>("avatarAddress");

                        var action = new CombinationConsumable
                        {
                            avatarAddress = avatarAddress,
                            recipeId = recipeId,
                            slotIndex = slotIndex,
                        };

                        var actions = new NCAction[] { action };
                        Transaction<NCAction> tx = blockChain.MakeTransaction(service.MinerPrivateKey, actions);
                        return tx.Id;
                    }
                    catch (Exception e)
                    {
                        var msg = $"Unexpected exception occurred during {typeof(ActionMutation)}: {e}";
                        context.Errors.Add(new ExecutionError(msg, e));
                        throw;
                    }
                });

            Field<NonNullGraphType<TxIdType>>(nameof(MonsterCollect))
                .Description("Start monster collect.")
                .Argument<int>(
                    "level",
                    false,
                    "The monster collection level.(1 ~ 7)")
                .Resolve(context =>
                {
                    try
                    {
                        BlockChain<NCAction>? blockChain = service.BlockChain;
                        if (blockChain is null)
                        {
                            throw new InvalidOperationException($"{nameof(blockChain)} is null.");
                        }

                        if (service.MinerPrivateKey is null)
                        {
                            throw new InvalidOperationException($"{nameof(service.MinerPrivateKey)} is null.");
                        }

                        int level = context.GetArgument<int>("level");
                        var action = new MonsterCollect
                        {
                            level = level,
                        };

                        var actions = new NCAction[] { action };
                        Transaction<NCAction> tx = blockChain.MakeTransaction(service.MinerPrivateKey, actions);
                        return tx.Id;
                    }
                    catch (Exception e)
                    {
                        var msg = $"Unexpected exception occurred during {typeof(ActionMutation)}: {e}";
                        context.Errors.Add(new ExecutionError(msg, e));
                        throw;
                    }
                });

            Field<NonNullGraphType<TxIdType>>(nameof(ClaimMonsterCollectionReward))
                .Description("Get monster collection reward.")
                .Argument<Address>(
                    "avatarAddress",
                    false,
                    "Address of avatar for get reward.")
                .Resolve(context =>
                {
                    try
                    {
                        BlockChain<NCAction>? blockChain = service.BlockChain;
                        if (blockChain is null)
                        {
                            throw new InvalidOperationException($"{nameof(blockChain)} is null.");
                        }


                        if (service.MinerPrivateKey is null)
                        {
                            throw new InvalidOperationException($"{nameof(service.MinerPrivateKey)} is null.");
                        }

                        Address avatarAddress = context.GetArgument<Address>("avatarAddress");
                        Address agentAddress = service.MinerPrivateKey.ToAddress();
                        AgentState agentState = new AgentState((Dictionary)service.BlockChain.GetState(agentAddress));

                        var action = new ClaimMonsterCollectionReward
                        {
                            avatarAddress = avatarAddress,
                        };

                        var actions = new NCAction[] { action };
                        Transaction<NCAction> tx = blockChain.MakeTransaction(service.MinerPrivateKey, actions);
                        return tx.Id;
                    }
                    catch (Exception e)
                    {
                        var msg = $"Unexpected exception occurred during {typeof(ActionMutation)}: {e}";
                        context.Errors.Add(new ExecutionError(msg, e));
                        throw;
                    }
                });
        }
    }
}
