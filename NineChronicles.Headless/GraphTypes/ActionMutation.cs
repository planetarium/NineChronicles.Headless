using Bencodex.Types;
using GraphQL;
using GraphQL.Types;
using Libplanet;
using Libplanet.Action.Sys;
using Libplanet.Assets;
using Libplanet.Blockchain;
using Libplanet.Crypto;
using Libplanet.Explorer.GraphTypes;
using Libplanet.PoS;
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
            Field<NonNullGraphType<TxIdType>>("createAvatar",
                description: "Create new avatar.",
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

            Field<NonNullGraphType<TxIdType>>("hackAndSlash",
                description: "Start stage to get material.",
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
                    }
                ),
                resolve: context =>
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

                        var action = new HackAndSlash
                        {
                            AvatarAddress = avatarAddress,
                            WorldId = worldId,
                            StageId = stageId,
                            Costumes = costumeIds,
                            Equipments = equipmentIds,
                            Foods = consumableIds,
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

            Field<NonNullGraphType<TxIdType>>("combinationEquipment",
                description: "Combine new equipment.",
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

            Field<NonNullGraphType<TxIdType>>("itemEnhancement",
                description: "Upgrade equipment.",
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
                    new QueryArgument<NonNullGraphType<GuidGraphType>>
                    {
                        Name = "materialId",
                        Description = "Material Guid for equipment upgrade."
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

            Field<NonNullGraphType<TxIdType>>("dailyReward",
                description: "Get daily reward.",
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
            Field<NonNullGraphType<TxIdType>>("chargeActionPoint",
                description: "Charge Action Points using Material.",
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

            Field<NonNullGraphType<TxIdType>>("combinationConsumable",
                description: "Combine new Consumable.",
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
                }
            );

            Field<NonNullGraphType<TxIdType>>(nameof(MonsterCollect),
                description: "Start monster collect.",
                arguments: new QueryArguments(
                    new QueryArgument<NonNullGraphType<IntGraphType>>
                    {
                        Name = "level",
                        Description = "The monster collection level.(1 ~ 7)"
                    }
                ),
                resolve: context =>
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
                }
            );

            Field<NonNullGraphType<TxIdType>>(nameof(ClaimMonsterCollectionReward),
                description: "Get monster collection reward.",
                arguments: new QueryArguments(
                    new QueryArgument<NonNullGraphType<AddressType>>
                    {
                        Name = "avatarAddress",
                        Description = "Address of avatar for get reward."
                    }
                ),
                resolve: context =>
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
                }
            );

            Field<NonNullGraphType<TxIdType>>("mintGovernanceToken",
                description: "Minting governance token for governance.",
                arguments: new QueryArguments(
                    new QueryArgument<NonNullGraphType<AddressType>>
                    {
                        Name = "recipient",
                        Description = "Address of the recipient to receive minted token."
                    },
                    new QueryArgument<NonNullGraphType<StringGraphType>>
                    {
                        Name = "amount",
                        Description = "Amount of the governance token to be minted."
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
                        Currency currency = Asset.GovernanceToken;
                        Address recipient = context.GetArgument<Address>("recipient");
                        FungibleAssetValue amount =
                        FungibleAssetValue.Parse(currency, context.GetArgument<string>("amount"));

                        var action = new Mint(recipient, amount);

                        Transaction<NCAction> tx = blockChain.MakeTransaction(privateKey, action);
       
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

            Field<NonNullGraphType<TxIdType>>("promoteValidator",
                description: "Promote node to validator.",
                arguments: new QueryArguments(
                    new QueryArgument<NonNullGraphType<ByteStringType>>
                    {
                        Name = "validatorPubKey",
                        Description = "Public key of node to be promoted to validator."
                    },
                    new QueryArgument<NonNullGraphType<StringGraphType>>
                    {
                        Name = "amount",
                        Description = "Amount of the governance token to be initially delegated."
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
                        Currency currency = Asset.GovernanceToken;
                        var rawPublicKey = context.GetArgument<byte[]>("validatorPubKey");
                        PublicKey validatorPubKey = new PublicKey(rawPublicKey);
                        FungibleAssetValue amount =
                        FungibleAssetValue.Parse(currency, context.GetArgument<string>("amount"));

                        var action = new PromoteValidator(validatorPubKey, amount);

                        Transaction<NCAction> tx = blockChain.MakeTransaction(privateKey, action);

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

            Field<NonNullGraphType<TxIdType>>("delegate",
                description: "Delegate to validator.",
                arguments: new QueryArguments(
                    new QueryArgument<NonNullGraphType<ByteStringType>>
                    {
                        Name = "validatorAddress",
                        Description = "Address of validator to delegate."
                    },
                    new QueryArgument<NonNullGraphType<StringGraphType>>
                    {
                        Name = "amount",
                        Description = "Amount of the governance token to be delegated."
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
                        Currency currency = Asset.GovernanceToken;
                        var rawAddress = context.GetArgument<byte[]>("validatorAddress");
                        Address validatorAddress = new Address(rawAddress);
                        FungibleAssetValue amount =
                        FungibleAssetValue.Parse(currency, context.GetArgument<string>("amount"));

                        var action = new Libplanet.Action.Sys.Delegate(validatorAddress, amount);

                        Transaction<NCAction> tx = blockChain.MakeTransaction(privateKey, action);

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

            Field<NonNullGraphType<TxIdType>>("undelegate",
                description: "Undelegate delegation.",
                arguments: new QueryArguments(
                    new QueryArgument<NonNullGraphType<ByteStringType>>
                    {
                        Name = "validatorAddress",
                        Description = "Address of validator to undelegate."
                    },
                    new QueryArgument<NonNullGraphType<StringGraphType>>
                    {
                        Name = "amount",
                        Description = "Amount of the shares to undelegate."
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
                        Currency currency = Asset.Share;
                        var rawAddress = context.GetArgument<byte[]>("validatorAddress");
                        Address validatorAddress = new Address(rawAddress);
                        FungibleAssetValue amount =
                        FungibleAssetValue.Parse(currency, context.GetArgument<string>("amount"));

                        var action = new Undelegate(validatorAddress, amount);

                        Transaction<NCAction> tx = blockChain.MakeTransaction(privateKey, action);

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

            Field<NonNullGraphType<TxIdType>>("cancelUndelegation",
                description: "Cancel undelegation.",
                arguments: new QueryArguments(
                    new QueryArgument<NonNullGraphType<ByteStringType>>
                    {
                        Name = "validatorAddress",
                        Description = "Address of validator to cancel undelegation."
                    },
                    new QueryArgument<NonNullGraphType<StringGraphType>>
                    {
                        Name = "amount",
                        Description = "Amount of the governance token to cancel undelegation."
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
                        Currency currency = Asset.ConsensusToken;
                        var rawAddress = context.GetArgument<byte[]>("validatorAddress");
                        Address validatorAddress = new Address(rawAddress);
                        FungibleAssetValue amount =
                        FungibleAssetValue.Parse(currency, context.GetArgument<string>("amount"));

                        var action = new CancelUndelegation(validatorAddress, amount);

                        Transaction<NCAction> tx = blockChain.MakeTransaction(privateKey, action);

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

            Field<NonNullGraphType<TxIdType>>("redelegate",
                description: "Cancel undelegation.",
                arguments: new QueryArguments(
                    new QueryArgument<NonNullGraphType<ByteStringType>>
                    {
                        Name = "srcValidatorAddress",
                        Description = "Address of source validator to redelegate."
                    },
                    new QueryArgument<NonNullGraphType<ByteStringType>>
                    {
                        Name = "dstValidatorAddress",
                        Description = "Address of destination validator to redelegate."
                    },
                    new QueryArgument<NonNullGraphType<StringGraphType>>
                    {
                        Name = "amount",
                        Description = "Amount of the share to redelegate."
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
                        Currency currency = Asset.Share;
                        var srcRawAddress = context.GetArgument<byte[]>("srcValidatorAddress");
                        Address srcValidatorAddress = new Address(srcRawAddress);
                        var dstRawAddress = context.GetArgument<byte[]>("dstValidatorAddress");
                        Address dstValidatorAddress = new Address(dstRawAddress);
                        FungibleAssetValue amount =
                        FungibleAssetValue.Parse(currency, context.GetArgument<string>("amount"));

                        var action = new Redelegate(srcValidatorAddress, dstValidatorAddress, amount);

                        Transaction<NCAction> tx = blockChain.MakeTransaction(privateKey, action);

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

            Field<NonNullGraphType<TxIdType>>("withdrawDelegator",
                description: "Withdraw rewards of delegation.",
                arguments: new QueryArguments(
                    new QueryArgument<NonNullGraphType<ByteStringType>>
                    {
                        Name = "validatorAddress",
                        Description = "Address of validator to withdraw."
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
   
                        var rawAddress = context.GetArgument<byte[]>("validatorAddress");
                        Address validatorAddress = new Address(rawAddress);

                        var action = new WithdrawDelegator(validatorAddress);

                        Transaction<NCAction> tx = blockChain.MakeTransaction(privateKey, action);

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

            Field<NonNullGraphType<TxIdType>>("withdrawValidator",
                description: "Withdraw commission of validator.",
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

                        var action = new WithdrawValidator();

                        Transaction<NCAction> tx = blockChain.MakeTransaction(privateKey, action);

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
        }
    }
}
