using GraphQL;
using GraphQL.Types;
using Libplanet.Crypto;
using Libplanet.Blockchain;
using Libplanet.Explorer.GraphTypes;
using Libplanet.Types.Tx;
using Nekoyume.Action;
using Nekoyume.Model.State;
using Serilog;
using System;
using System.Collections.Generic;
using Nekoyume.Module;
using Lib9c;
using Libplanet.Types.Assets;
using Nekoyume.Action.ValidatorDelegation;
using System.Numerics;
using Nekoyume.Action.Guild.Migration;
using Nekoyume.TypedAddress;
using Nekoyume.ValidatorDelegation;
using LiteDB;

namespace NineChronicles.Headless.GraphTypes
{
    public class ActionMutation : ObjectGraphType
    {
        public ActionMutation(NineChroniclesNodeService service)
        {
            DeprecationReason = "This API is insecure and must not be used.";

            Field<NonNullGraphType<TxIdType>>("createAvatar",
                description: "Create new avatar.",
                deprecationReason: DeprecationReason,
                arguments: new QueryArguments(
                    new QueryArgument<NonNullGraphType<StringGraphType>>
                    {
                        Name = "avatarName",
                        Description = "Avatar name."
                    },
                    new QueryArgument<NonNullGraphType<IntGraphType>>
                    {
                        Name = "avatarIndex",
                        Description = "The index of character slot. 0 ~ 2"
                    },
                    new QueryArgument<NonNullGraphType<IntGraphType>>
                    {
                        Name = "hairIndex",
                        Description = "The index of character hair color. 0 ~ 8"
                    },
                    new QueryArgument<NonNullGraphType<IntGraphType>>
                    {
                        Name = "lensIndex",
                        Description = "The index of character eye color. 0 ~ 8"
                    },
                    new QueryArgument<NonNullGraphType<IntGraphType>>
                    {
                        Name = "earIndex",
                        Description = "The index of character ear color. 0 ~ 8"
                    },
                    new QueryArgument<NonNullGraphType<IntGraphType>>
                    {
                        Name = "tailIndex",
                        Description = "The index of character tail color. 0 ~ 8"
                    }
                ),
                resolve: context =>
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

                        var actions = new ActionBase[] { action };
                        Transaction tx = blockChain.MakeTransaction(privateKey, actions);
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

            Field<NonNullGraphType<TxIdType>>("hackAndSlash",
                description: "Start stage to get material.",
                deprecationReason: DeprecationReason,
                arguments: new QueryArguments(
                    new QueryArgument<NonNullGraphType<AddressType>>
                    {
                        Name = "avatarAddress",
                        Description = "Avatar address."
                    },
                    new QueryArgument<NonNullGraphType<IntGraphType>>
                    {
                        Name = "worldId",
                        Description = "World ID containing the stage ID."
                    },
                    new QueryArgument<NonNullGraphType<IntGraphType>>
                    {
                        Name = "stageId",
                        Description = "Stage ID."
                    },
                    new QueryArgument<ListGraphType<GuidGraphType>>
                    {
                        Name = "costumeIds",
                        Description = "List of costume id for equip."
                    },
                    new QueryArgument<ListGraphType<GuidGraphType>>
                    {
                        Name = "equipmentIds",
                        Description = "List of equipment id for equip."
                    },
                    new QueryArgument<ListGraphType<GuidGraphType>>
                    {
                        Name = "consumableIds",
                        Description = "List of consumable id for use."
                    },
                    new QueryArgument<ListGraphType<NonNullGraphType<RuneSlotInfoInputType>>>
                    {
                        Name = "runeSlotInfos",
                        DefaultValue = new List<RuneSlotInfo>(),
                        Description = "List of rune slot info for equip."
                    }
                ),
                resolve: context =>
                {
                    try
                    {
                        BlockChain? blockChain = service.Swarm.BlockChain;
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

                        var actions = new ActionBase[] { action };
                        Transaction tx = blockChain.MakeTransaction(service.MinerPrivateKey, actions);
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

            Field<NonNullGraphType<TxIdType>>("combinationEquipment",
                description: "Combine new equipment.",
                deprecationReason: DeprecationReason,
                arguments: new QueryArguments(
                    new QueryArgument<NonNullGraphType<AddressType>>
                    {
                        Name = "avatarAddress",
                        Description = "Avatar address to create equipment."
                    },
                    new QueryArgument<NonNullGraphType<IntGraphType>>
                    {
                        Name = "recipeId",
                        Description = "EquipmentRecipe ID from EquipmentRecipeSheet."
                    },
                    new QueryArgument<NonNullGraphType<IntGraphType>>
                    {
                        Name = "slotIndex",
                        Description = "The empty combination slot index to combine equipment. 0 ~ 3"
                    },
                    new QueryArgument<IntGraphType>
                    {
                        Name = "subRecipeId",
                        Description = "EquipmentSubRecipe ID from EquipmentSubRecipeSheet."
                    }
                ),
                resolve: context =>
                {
                    try
                    {
                        BlockChain? blockChain = service.BlockChain;
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

                        var actions = new ActionBase[] { action };
                        Transaction tx = blockChain.MakeTransaction(service.MinerPrivateKey, actions);
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

            Field<NonNullGraphType<TxIdType>>("itemEnhancement",
                description: "Upgrade equipment.",
                deprecationReason: DeprecationReason,
                arguments: new QueryArguments(
                    new QueryArgument<NonNullGraphType<AddressType>>
                    {
                        Name = "avatarAddress",
                        Description = "Avatar address to upgrade equipment."
                    },
                    new QueryArgument<NonNullGraphType<GuidGraphType>>
                    {
                        Name = "itemId",
                        Description = "Equipment Guid for upgrade."
                    },
                    new QueryArgument<NonNullGraphType<ListGraphType<NonNullGraphType<GuidGraphType>>>>
                    {
                        Name = "materialIds",
                        Description = "Material Guids for equipment upgrade."
                    },
                    new QueryArgument<NonNullGraphType<IntGraphType>>
                    {
                        Name = "slotIndex",
                        Description = "The empty combination slot index to upgrade equipment. 0 ~ 3"
                    }
                ),
                resolve: context =>
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
                        var materialIds = context.GetArgument<List<Guid>>("materialIds");
                        Address avatarAddress = context.GetArgument<Address>("avatarAddress");
                        int slotIndex = context.GetArgument<int>("slotIndex");

                        var action = new ItemEnhancement
                        {
                            avatarAddress = avatarAddress,
                            slotIndex = slotIndex,
                            itemId = itemId,
                            materialIds = materialIds,
                        };

                        var actions = new ActionBase[] { action };
                        Transaction tx = blockChain.MakeTransaction(privatekey, actions);
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

            Field<NonNullGraphType<TxIdType>>("dailyReward",
                description: "Get daily reward.",
                deprecationReason: DeprecationReason,
                arguments: new QueryArguments(
                    new QueryArgument<NonNullGraphType<AddressType>>
                    {
                        Name = "avatarAddress",
                        Description = "Avatar address to receive reward."
                    }
                ),
                resolve: context =>
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

                        var actions = new ActionBase[] { action };
                        Transaction tx = blockChain.MakeTransaction(privateKey, actions);
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
            Field<NonNullGraphType<TxIdType>>("chargeActionPoint",
                description: "Charge Action Points using Material.",
                deprecationReason: DeprecationReason,
                arguments: new QueryArguments(
                    new QueryArgument<NonNullGraphType<AddressType>>
                    {
                        Name = "avatarAddress",
                        Description = "Avatar to use potion."
                    }
                ),
                resolve: context =>
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

                        var actions = new ActionBase[] { action };
                        Transaction tx = blockChain.MakeTransaction(privateKey, actions);
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

            Field<NonNullGraphType<TxIdType>>("combinationConsumable",
                description: "Combine new Consumable.",
                deprecationReason: DeprecationReason,
                arguments: new QueryArguments(
                    new QueryArgument<NonNullGraphType<AddressType>>
                    {
                        Name = "avatarAddress",
                        Description = "Avatar address to combine consumable."
                    },
                    new QueryArgument<NonNullGraphType<IntGraphType>>
                    {
                        Name = "recipeId",
                        Description = "ConsumableRecipe ID from ConsumableRecipeSheet."
                    },
                    new QueryArgument<NonNullGraphType<IntGraphType>>
                    {
                        Name = "slotIndex",
                        Description = "The empty combination slot index to combine consumable. 0 ~ 3"
                    }
                ),
                resolve: context =>
                {
                    try
                    {
                        BlockChain? blockChain = service.BlockChain;
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

                        var actions = new ActionBase[] { action };
                        Transaction tx = blockChain.MakeTransaction(service.MinerPrivateKey, actions);
                        return tx.Id;
                    }
                    catch (Exception e)
                    {
                        var msg = $"Unexpected exception occurred during {typeof(ActionMutation)}: {e}";
                        context.Errors.Add(new ExecutionError(msg, e));
                        throw;
                    }
                }
            );

            Field<NonNullGraphType<TxIdType>>("promoteValidator",
                description: "Promote validator.",
                arguments: new QueryArguments(
                    new QueryArgument<NonNullGraphType<StringGraphType>>
                    {
                        Name = "validator",
                        Description = "Validator public key to promote."
                    },
                    new QueryArgument<NonNullGraphType<BigIntGraphType>>
                    {
                        Name = "amount",
                        Description = "Amount of NCG to stake."
                    }
                ),
                resolve: context =>
                {
                    try
                    {
                        BlockChain? blockChain = service.BlockChain;
                        if (blockChain is null)
                        {
                            throw new InvalidOperationException($"{nameof(blockChain)} is null.");
                        }
                        string validatorString = context.GetArgument<string>("validator");
                        PublicKey validator = PublicKey.FromHex(validatorString);
                        BigInteger amount = context.GetArgument<BigInteger>("amount");
                        var fav = new FungibleAssetValue(ValidatorDelegatee.ValidatorDelegationCurrency, amount, 0);
                        var action = new PromoteValidator(validator, fav);
                        var actions = new[] { action };
                        Transaction tx = blockChain.MakeTransaction(
                            service.MinerPrivateKey,
                            actions,
                            Currencies.Mead * 1,
                            1L);
                        return tx.Id;
                    }
                    catch (Exception e)
                    {
                        var msg = $"Unexpected exception occurred during {typeof(ActionMutation)}: {e}";
                        context.Errors.Add(new ExecutionError(msg, e));
                        throw;
                    }
                }
            );

            Field<NonNullGraphType<TxIdType>>("transferNCG",
                description: "Transfer ncg to validtor to promote.",
                arguments: new QueryArguments(
                    new QueryArgument<NonNullGraphType<AddressType>>
                    {
                        Name = "validator",
                        Description = "Validator public key to promote."
                    },
                    new QueryArgument<NonNullGraphType<BigIntGraphType>>
                    {
                        Name = "amount",
                        Description = "Amount of NCG to stake."
                    }
                ),
                resolve: context =>
                {
                    try
                    {
                        BlockChain? blockChain = service.BlockChain;
                        if (blockChain is null)
                        {
                            throw new InvalidOperationException($"{nameof(blockChain)} is null.");
                        }

                        var amount = context.GetArgument<BigInteger>("amount");
                        var sender = service.MinerPrivateKey!.Address;
                        var recipient = context.GetArgument<Address>("validator");
                        var currency = blockChain.GetWorldState().GetGoldCurrency();
                        var fav = currency * amount;

#pragma warning disable CS0618
                        var actions = new[] { new TransferAsset(sender, recipient, fav, "To promote") };
#pragma warning restore CS0618

                        Transaction tx = blockChain.MakeTransaction(
                            service.MinerPrivateKey,
                            actions,
                            Currencies.Mead * 4,
                            4L);
                        return tx.Id;
                    }
                    catch (Exception e)
                    {
                        var msg = $"Unexpected exception occurred during {typeof(ActionMutation)}: {e}";
                        context.Errors.Add(new ExecutionError(msg, e));
                        throw;
                    }
                }
            );

            Field<NonNullGraphType<TxIdType>>("transferMead",
                description: "Transfer ncg to validtor to promote.",
                arguments: new QueryArguments(
                    new QueryArgument<NonNullGraphType<AddressType>>
                    {
                        Name = "validator",
                        Description = "Validator public key to promote."
                    },
                    new QueryArgument<NonNullGraphType<BigIntGraphType>>
                    {
                        Name = "amount",
                        Description = "Amount of NCG to stake."
                    }
                ),
                resolve: context =>
                {
                    try
                    {
                        BlockChain? blockChain = service.BlockChain;
                        if (blockChain is null)
                        {
                            throw new InvalidOperationException($"{nameof(blockChain)} is null.");
                        }

                        var amount = context.GetArgument<BigInteger>("amount");
                        var sender = service.MinerPrivateKey!.Address;
                        var recipient = context.GetArgument<Address>("validator");
                        var fav = Currencies.Mead * amount;

#pragma warning disable CS0618
                        var actions = new[] { new TransferAsset(sender, recipient, fav, "To promote") };
#pragma warning restore CS0618

                        Transaction tx = blockChain.MakeTransaction(
                            service.MinerPrivateKey,
                            actions,
                            Currencies.Mead * 4,
                            4L);
                        return tx.Id;
                    }
                    catch (Exception e)
                    {
                        var msg = $"Unexpected exception occurred during {typeof(ActionMutation)}: {e}";
                        context.Errors.Add(new ExecutionError(msg, e));
                        throw;
                    }
                }
            );

            Field<NonNullGraphType<TxIdType>>("stake",
                description: "Claim reward for self delegation of validator.",
                arguments: new QueryArguments(
                    new QueryArgument<NonNullGraphType<IntGraphType>>
                    {
                        Name = "amount",
                        Description = "Amount to stake."
                    }
                ),
                resolve: context =>
                {
                    try
                    {
                        BlockChain? blockChain = service.BlockChain;
                        if (blockChain is null)
                        {
                            throw new InvalidOperationException($"{nameof(blockChain)} is null.");
                        }

                        BigInteger amount = context.GetArgument<BigInteger>("amount");

                        var actions = new[] { new Stake(amount) };
                        Transaction tx = blockChain.MakeTransaction(
                            service.MinerPrivateKey,
                            actions,
                            Currencies.Mead * 1,
                            1L);
                        return tx.Id;
                    }
                    catch (Exception e)
                    {
                        var msg = $"Unexpected exception occurred during {typeof(ActionMutation)}: {e}";
                        context.Errors.Add(new ExecutionError(msg, e));
                        throw;
                    }
                }
            );
        }
    }
}
