using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using Bencodex;
using Bencodex.Types;
using Google.Protobuf.WellKnownTypes;
using GraphQL;
using GraphQL.Types;
using Libplanet.Crypto;
using Libplanet.Types.Assets;
using Libplanet.Explorer.GraphTypes;
using Nekoyume.Action;
using Nekoyume.Model;
using Nekoyume.Model.State;
using Nekoyume.Module;
using Nekoyume.TableData;

namespace NineChronicles.Headless.GraphTypes
{
    public partial class ActionQuery : ObjectGraphType
    {
        private static readonly Codec Codec = new();
        internal StandaloneContext StandaloneContext { get; set; }

        public ActionQuery(StandaloneContext standaloneContext)
        {
            StandaloneContext = standaloneContext;

            Field<ByteStringType>(
                name: "stake",
                arguments: new QueryArguments(new QueryArgument<BigIntGraphType>
                {
                    Name = "amount",
                    Description = "An amount to stake.",
                }),
                resolve: context => Encode(
                    context,
                    new Stake(context.GetArgument<BigInteger>("amount"))));

            Field<ByteStringType>(
                name: "claimStakeReward",
                arguments: new QueryArguments(
                    new QueryArgument<AddressType>
                    {
                        Name = "avatarAddress",
                        Description = "The avatar address to receive staking rewards."
                    }),
                resolve: context =>
                {
                    if (!(standaloneContext.BlockChain is { } chain))
                    {
                        throw new InvalidOperationException("BlockChain not found in the context");
                    }

                    return Encode(
                        context,
                        // Due to claim_stake_reward5's compatibility issue, force to latest action temporarily.
                        // TODO: Restore it with the action factory pattern after v200070.
                        new ClaimStakeReward(context.GetArgument<Address>("avatarAddress")));
                }
            );
            Field<NonNullGraphType<ByteStringType>>(
                name: "migrateMonsterCollection",
                arguments: new QueryArguments(
                    new QueryArgument<AddressType>
                    {
                        Name = "avatarAddress",
                        Description = "The avatar address to receive monster collection rewards."
                    }),
                resolve: context => Encode(
                    context,
                    new MigrateMonsterCollection(
                        context.GetArgument<Address>("avatarAddress"))));
            Field<ByteStringType>(
                name: "grinding",
                arguments: new QueryArguments(
                    new QueryArgument<NonNullGraphType<AddressType>>
                    {
                        Name = "avatarAddress",
                        Description = "Address of avatar.",
                    },
                    new QueryArgument<NonNullGraphType<ListGraphType<GuidGraphType>>>
                    {
                        Name = "equipmentIds",
                        Description = "List of equipment ItemId.",
                    },
                    new QueryArgument<BooleanGraphType>
                    {
                        Name = "chargeAp",
                        Description = "Flag to Charge Action Point.",
                    }
                ),
                resolve: context =>
                {
                    var avatarAddress = context.GetArgument<Address>("avatarAddress");
                    var equipmentIds = context.GetArgument<List<Guid>>("equipmentIds");
                    var chargeAp = context.GetArgument<bool>("chargeAp");
                    ActionBase action = new Grinding
                    {
                        AvatarAddress = avatarAddress,
                        EquipmentIds = equipmentIds,
                        ChargeAp = chargeAp,
                    };
                    return Encode(context, action);
                });
            Field<ByteStringType>(
                name: "unlockEquipmentRecipe",
                arguments: new QueryArguments(
                    new QueryArgument<NonNullGraphType<AddressType>>
                    {
                        Name = "avatarAddress",
                        Description = "Address of avatar.",
                    },
                    new QueryArgument<NonNullGraphType<ListGraphType<IntGraphType>>>
                    {
                        Name = "recipeIds",
                        Description = "List of EquipmentRecipeSheet row ids to unlock.",
                    }
                ),
                resolve: context =>
                {
                    var avatarAddress = context.GetArgument<Address>("avatarAddress");
                    var recipeIds = context.GetArgument<List<int>>("recipeIds");
                    ActionBase action = new UnlockEquipmentRecipe
                    {
                        AvatarAddress = avatarAddress,
                        RecipeIds = recipeIds,
                    };
                    return Encode(context, action);
                });
            Field<ByteStringType>(
                name: "unlockWorld",
                arguments: new QueryArguments(
                    new QueryArgument<NonNullGraphType<AddressType>>
                    {
                        Name = "avatarAddress",
                        Description = "Address of avatar.",
                    },
                    new QueryArgument<NonNullGraphType<ListGraphType<IntGraphType>>>
                    {
                        Name = "worldIds",
                        Description = "List of WorldUnlockSheet row world_id_to_unlock.",
                    }
                ),
                resolve: context =>
                {
                    var avatarAddress = context.GetArgument<Address>("avatarAddress");
                    var worldIds = context.GetArgument<List<int>>("worldIds");
                    ActionBase action = new UnlockWorld
                    {
                        AvatarAddress = avatarAddress,
                        WorldIds = worldIds,
                    };
                    return Encode(context, action);
                });
            Field<ByteStringType>(
                name: "transferAsset",
                arguments: new QueryArguments(
                    new QueryArgument<NonNullGraphType<AddressType>>
                    {
                        Description = "Address of sender.",
                        Name = "sender",
                    },
                    new QueryArgument<NonNullGraphType<AddressType>>
                    {
                        Description = "Address of recipient.",
                        Name = "recipient",
                    },
                    new QueryArgument<NonNullGraphType<StringGraphType>>
                    {
                        Description = "A string value to be transferred.",
                        Name = "amount",
                    },
                    new QueryArgument<CurrencyEnumType>
                    {
                        Description = "A enum value of currency to be transferred.",
                        Name = "currency",
                    },
                    new QueryArgument<CurrencyInputType>
                    {
                        Description = "A currency to be transferred.",
                        Name = "rawCurrency",
                    },
                    new QueryArgument<StringGraphType>
                    {
                        Description = "A 80-max length string to note.",
                        Name = "memo",
                    }
                ),
                resolve: context =>
                {
                    var sender = context.GetArgument<Address>("sender");
                    var recipient = context.GetArgument<Address>("recipient");
                    var nullableRawCurrency = context.GetArgument<Currency?>("rawCurrency");
                    var nullableCurrencyEnum = context.GetArgument<CurrencyEnum?>("currency");

                    Currency currency;
                    if (nullableRawCurrency is not null && nullableCurrencyEnum is not null)
                    {
                        throw new ExecutionError("Only one of currency and rawCurrency must be set.");
                    }
                    if (nullableCurrencyEnum is { } currencyEnum)
                    {
                        if (!standaloneContext.CurrencyFactory!.TryGetCurrency(currencyEnum, out var currencyFromEnum))
                        {
                            throw new ExecutionError($"Currency {currencyEnum} is not found.");
                        }

                        currency = currencyFromEnum;
                    }
                    else if (nullableRawCurrency is { } rawCurrency)
                    {
                        currency = rawCurrency;
                    }
                    else
                    {
                        throw new ExecutionError("Either currency or rawCurrency must be set.");
                    }

                    var amount = FungibleAssetValue.Parse(currency, context.GetArgument<string>("amount"));
                    var memo = context.GetArgument<string?>("memo");
                    ActionBase action = new TransferAsset(sender, recipient, amount, memo);
                    return Encode(context, action);
                });
            Field<NonNullGraphType<ByteStringType>>(
                name: "patchTableSheet",
                arguments: new QueryArguments(
                    new QueryArgument<NonNullGraphType<StringGraphType>>
                    {
                        Description = "name of table sheet.",
                        Name = "tableName",
                    },
                    new QueryArgument<NonNullGraphType<StringGraphType>>
                    {
                        Description = "table data.",
                        Name = "tableCsv",
                    }
                ),
                resolve: context =>
                {
                    var tableName = context.GetArgument<string>("tableName");
                    var tableCsv = context.GetArgument<string>("tableCsv");
                    try
                    {
                        var _ = typeof(ISheet).Assembly
                            .GetTypes()
                            .First(type => type.Namespace is { } @namespace &&
                                           @namespace.StartsWith($"{nameof(Nekoyume)}.{nameof(Nekoyume.TableData)}") &&
                                           !type.IsAbstract &&
                                           typeof(ISheet).IsAssignableFrom(type) &&
                                           tableName.Split('_').First() == type.Name);
                    }
                    catch (Exception)
                    {
                        throw new ExecutionError("Invalid tableName.");
                    }

                    // TODO validate row data.
                    ActionBase action = new PatchTableSheet
                    {
                        TableName = tableName,
                        TableCsv = tableCsv
                    };
                    return Encode(context, action);
                }
            );
            Field<NonNullGraphType<ByteStringType>>(
                name: "raid",
                arguments: new QueryArguments(
                    new QueryArgument<NonNullGraphType<AddressType>>
                    {
                        Description = "address of avatar state.",
                        Name = "avatarAddress",
                    },
                    new QueryArgument<ListGraphType<GuidGraphType>>
                    {
                        Description = "list of equipment id.",
                        DefaultValue = new List<Guid>(),
                        Name = "equipmentIds",
                    },
                    new QueryArgument<ListGraphType<GuidGraphType>>
                    {
                        Description = "list of costume id.",
                        DefaultValue = new List<Guid>(),
                        Name = "costumeIds",
                    },
                    new QueryArgument<ListGraphType<GuidGraphType>>
                    {
                        Description = "list of food id.",
                        DefaultValue = new List<Guid>(),
                        Name = "foodIds",
                    },
                    new QueryArgument<BooleanGraphType>
                    {
                        Description = "refill ticket by NCG.",
                        DefaultValue = false,
                        Name = "payNcg",
                    },
                    new QueryArgument<ListGraphType<NonNullGraphType<RuneSlotInfoInputType>>>
                    {
                        Description = "list of rune slot",
                        DefaultValue = new List<RuneSlotInfo>(),
                        Name = "runeSlotInfos"
                    }
                ),
                resolve: context =>
                {
                    var avatarAddress = context.GetArgument<Address>("avatarAddress");
                    var equipmentIds = context.GetArgument<List<Guid>>("equipmentIds");
                    var costumeIds = context.GetArgument<List<Guid>>("costumeIds");
                    var foodIds = context.GetArgument<List<Guid>>("foodIds");
                    var payNcg = context.GetArgument<bool>("payNcg");
                    var runeSlotInfos = context.GetArgument<List<RuneSlotInfo>>("runeSlotInfos");

                    ActionBase action = new Raid
                    {
                        AvatarAddress = avatarAddress,
                        EquipmentIds = equipmentIds,
                        CostumeIds = costumeIds,
                        FoodIds = foodIds,
                        PayNcg = payNcg,
                        RuneInfos = runeSlotInfos,
                    };
                    return Encode(context, action);
                }
            );
            Field<NonNullGraphType<ByteStringType>>(
                "claimRaidReward",
                arguments: new QueryArguments(
                    new QueryArgument<NonNullGraphType<AddressType>>
                    {
                        Name = "avatarAddress",
                        Description = "address of avatar state to receive reward."
                    }
                ),
                resolve: context =>
                {
                    var avatarAddress = context.GetArgument<Address>("avatarAddress");

                    ActionBase action = new ClaimRaidReward(avatarAddress);
                    return Encode(context, action);
                }
            );
            Field<NonNullGraphType<ByteStringType>>(
                "claimWorldBossKillReward",
                arguments: new QueryArguments(
                    new QueryArgument<NonNullGraphType<AddressType>>
                    {
                        Name = "avatarAddress",
                        Description = "address of avatar state to receive reward."
                    }
                ),
                resolve: context =>
                {
                    var avatarAddress = context.GetArgument<Address>("avatarAddress");

                    ActionBase action = new ClaimWordBossKillReward
                    {
                        AvatarAddress = avatarAddress,
                    };
                    return Encode(context, action);
                }
            );
            Field<NonNullGraphType<ByteStringType>>(
                "prepareRewardAssets",
                arguments: new QueryArguments(
                    new QueryArgument<NonNullGraphType<AddressType>>
                    {
                        Name = "rewardPoolAddress",
                        Description = "address of reward pool for charge reward."
                    },
                    new QueryArgument<NonNullGraphType<ListGraphType<NonNullGraphType<FungibleAssetValueInputType>>>>
                    {
                        Name = "assets",
                        Description = "list of FungibleAssetValue for charge reward."
                    }
                ),
                resolve: context =>
                {
                    var assets = context.GetArgument<List<FungibleAssetValue>>("assets");
                    var rewardPoolAddress = context.GetArgument<Address>("rewardPoolAddress");

                    ActionBase action = new PrepareRewardAssets
                    {
                        Assets = assets,
                        RewardPoolAddress = rewardPoolAddress,
                    };
                    return Encode(context, action);
                }
            );
            Field<NonNullGraphType<ByteStringType>>(
                "transferAssets",
                arguments: new QueryArguments(
                    new QueryArgument<NonNullGraphType<AddressType>>
                    {
                        Description = "Address of sender.",
                        Name = "sender",
                    },
                    new QueryArgument<NonNullGraphType<ListGraphType<NonNullGraphType<RecipientsInputType>>>>
                    {
                        Description = "List of tuples that recipients' address and asset amount to be sent",
                        Name = "recipients",
                    },
                    new QueryArgument<StringGraphType>
                    {
                        Description = "A 80-max length string to note.",
                        Name = "memo",
                    }
                ),
                resolve: context =>
                {
                    var sender = context.GetArgument<Address>("sender");
                    var recipients =
                        context.GetArgument<List<(Address recipient, FungibleAssetValue amount)>>("recipients");
                    var memo = context.GetArgument<string?>("memo");
                    if (recipients.Count > TransferAssets.RecipientsCapacity)
                    {
                        throw new ExecutionError(
                            $"recipients must be less than or equal {TransferAssets.RecipientsCapacity}.");
                    }

                    ActionBase action = new TransferAssets(sender, recipients, memo);
                    return Encode(context, action);
                }
            );
            Field<NonNullGraphType<ByteStringType>>(
                "activateAccount",
                deprecationReason: "Since NCIP-15, it doesn't care account activation.",
                arguments: new QueryArguments(
                    new QueryArgument<NonNullGraphType<StringGraphType>>
                    {
                        Name = "activationCode",
                        Description = "Activation code that you've get."
                    }
                ),
                resolve: context =>
                {
                    var activationCode = context.GetArgument<string>("activationCode");
                    var activationKey = ActivationKey.Decode(activationCode);
                    if (standaloneContext.BlockChain!.GetWorldState().GetLegacyState(activationKey.PendingAddress) is Dictionary dictionary)
                    {
                        var pending = new PendingActivationState(dictionary);
                        var action = activationKey.CreateActivateAccount(pending.Nonce);
                        if (pending.Verify(action))
                        {
                            return Encode(context, action);
                        }

                        throw new ExecutionError("Failed to verify activateAccount action.");
                    }

                    throw new InvalidOperationException("BlockChain not found in the context");
                }
            );
            Field<NonNullGraphType<ByteStringType>>(
                "createAvatar",
                arguments: new QueryArguments(
                    new QueryArgument<NonNullGraphType<IntGraphType>>
                    {
                        Name = "index",
                        Description = "index of avatar in `AgentState.avatarAddresses`.(0~2)",
                    },
                    new QueryArgument<NonNullGraphType<StringGraphType>>
                    {
                        Name = "name",
                        Description = "name of avatar.(2~20 characters)",
                    },
                    new QueryArgument<IntGraphType>
                    {
                        Name = "hair",
                        Description = "hair index of avatar.",
                        DefaultValue = 0,
                    },
                    new QueryArgument<IntGraphType>
                    {
                        Name = "lens",
                        Description = "lens index of avatar.",
                        DefaultValue = 0,
                    },
                    new QueryArgument<IntGraphType>
                    {
                        Name = "ear",
                        Description = "ear index of avatar.",
                        DefaultValue = 0,
                    },
                    new QueryArgument<IntGraphType>
                    {
                        Name = "tail",
                        Description = "tail index of avatar.",
                        DefaultValue = 0,
                    }),
                resolve: context =>
                {
                    var index = context.GetArgument<int>("index");
                    if (index < 0 || index > 2)
                    {
                        throw new ExecutionError(
                            $"Invalid index({index}). It must be 0~2.");
                    }

                    var name = context.GetArgument<string>("name");
                    if (name.Length < 2 || name.Length > 20)
                    {
                        throw new ExecutionError(
                            $"Invalid name({name}). It must be 2~20 characters.");
                    }

                    var hair = context.GetArgument<int>("hair");
                    var lens = context.GetArgument<int>("lens");
                    var ear = context.GetArgument<int>("ear");
                    var tail = context.GetArgument<int>("tail");

                    ActionBase action = new CreateAvatar
                    {
                        index = index,
                        name = name,
                        hair = hair,
                        lens = lens,
                        ear = ear,
                        tail = tail,
                    };
                    return Encode(context, action);
                });
            Field<NonNullGraphType<ByteStringType>>(
                "runeEnhancement",
                arguments: new QueryArguments(
                    new QueryArgument<NonNullGraphType<AddressType>>
                    {
                        Name = "avatarAddress",
                        Description = "The avatar address to enhance rune."
                    },
                    new QueryArgument<NonNullGraphType<IntGraphType>>
                    {
                        Name = "runeId",
                        Description = "Rune ID to enhance."
                    },
                    new QueryArgument<IntGraphType>
                    {
                        Name = "tryCount",
                        Description = "The try count to enhance rune"
                    }),
                resolve: context =>
                {
                    var avatarAddress = context.GetArgument<Address>("avatarAddress");
                    var runeId = context.GetArgument<int>("runeId");
                    var tryCount = context.GetArgument<int?>("tryCount") ?? 1;
                    if (tryCount <= 0)
                    {
                        throw new ExecutionError($"tryCount must be positive: {tryCount} is invalid.");
                    }

                    ActionBase action = new RuneEnhancement
                    {
                        AvatarAddress = avatarAddress,
                        RuneId = runeId,
                        TryCount = tryCount
                    };
                    return Encode(context, action);
                });

            RegisterHackAndSlash();
            RegisterHackAndSlashSweep();
            RegisterDailyReward();
            RegisterCombinationEquipment();
            RegisterItemEnhancement();
            RegisterRapidCombination();
            RegisterCombinationConsumable();
            RegisterMead();
            RegisterGarages();
            RegisterSummon();
            RegisterClaimItems();

            Field<NonNullGraphType<CraftQuery>>(
                name: "craftQuery",
                description: "Query to craft/enhance items/foods",
                resolve: context => new CraftQuery(standaloneContext)
            );

#if LIB9C_DEV_EXTENSIONS
            RegisterFieldsForDevEx();
#endif
        }

        internal virtual byte[] Encode(IResolveFieldContext context, ActionBase action)
        {
            return Codec.Encode(action.PlainValue);
        }
    }
}
