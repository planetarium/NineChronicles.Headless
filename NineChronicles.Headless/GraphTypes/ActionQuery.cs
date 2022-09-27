using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Numerics;
using System.Text;
using Bencodex;
using Bencodex.Types;
using GraphQL;
using GraphQL.Types;
using Libplanet;
using Libplanet.Action.Sys;
using Libplanet.Assets;
using Libplanet.Crypto;
using Libplanet.Explorer.GraphTypes;
using Libplanet.PoS;
using Nekoyume.Action;
using Nekoyume.Helper;
using Nekoyume.Model.State;
using Nekoyume.TableData;
using Serilog;
using NCAction = Libplanet.Action.PolymorphicAction<Nekoyume.Action.ActionBase>;

namespace NineChronicles.Headless.GraphTypes
{
    public class ActionQuery : ObjectGraphType
    {
        private static readonly Codec Codec = new Codec();

        public ActionQuery(StandaloneContext standaloneContext)
        {
            Field<ByteStringType>(
                name: "stake",
                arguments: new QueryArguments(new QueryArgument<BigIntGraphType>
                {
                    Name = "amount",
                    Description = "An amount to stake.",
                }),
                resolve: context =>
                    Codec.Encode(
                    ((NCAction)new Stake(context.GetArgument<BigInteger>("amount"))).PlainValue));

            Field<ByteStringType>(
                name: "claimStakeReward",
                arguments: new QueryArguments(
                    new QueryArgument<AddressType>
                    {
                        Name = "avatarAddress",
                        Description = "The avatar address to receive staking rewards."
                    }),
                resolve: context =>
                    Codec.Encode(
                        ((NCAction)new ClaimStakeReward(
                            context.GetArgument<Address>("avatarAddress"))).PlainValue));
            Field<NonNullGraphType<ByteStringType>>(
                name: "migrateMonsterCollection",
                arguments: new QueryArguments(
                    new QueryArgument<AddressType>
                    {
                        Name = "avatarAddress",
                        Description = "The avatar address to receive monster collection rewards."
                    }),
                resolve: context =>
                    Codec.Encode(
                        ((NCAction)new MigrateMonsterCollection(
                            context.GetArgument<Address>("avatarAddress"))).PlainValue));
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
                    NCAction action = new Grinding
                    {
                        AvatarAddress = avatarAddress,
                        EquipmentIds = equipmentIds,
                        ChargeAp = chargeAp,
                    };
                    return Codec.Encode(action.PlainValue);
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
                    NCAction action = new UnlockEquipmentRecipe
                    {
                        AvatarAddress = avatarAddress,
                        RecipeIds = recipeIds,
                    };
                    return Codec.Encode(action.PlainValue);
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
                    NCAction action = new UnlockWorld
                    {
                        AvatarAddress = avatarAddress,
                        WorldIds = worldIds,
                    };
                    return Codec.Encode(action.PlainValue);
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
                    new QueryArgument<NonNullGraphType<CurrencyType>>
                    {
                        Description = "A currency type to be transferred.",
                        Name = "currency",
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
                    Currency currency = context.GetArgument<CurrencyEnum>("currency") switch
                    {
                        CurrencyEnum.NCG => new GoldCurrencyState(
                            (Dictionary)standaloneContext.BlockChain!.GetState(GoldCurrencyState.Address)
                        ).Currency,
                        CurrencyEnum.CRYSTAL => CrystalCalculator.CRYSTAL,
                        _ => throw new ExecutionError("Unsupported Currency type.")
                    };
                    var amount = FungibleAssetValue.Parse(currency, context.GetArgument<string>("amount"));
                    var memo = context.GetArgument<string?>("memo");
                    NCAction action = new TransferAsset(sender, recipient, amount, memo);
                    return Codec.Encode(action.PlainValue);
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
                                           type.Name == tableName);
                    }
                    catch (Exception)
                    {
                        throw new ExecutionError("Invalid tableName.");
                    }

                    // TODO validate row data.
                    NCAction action = new PatchTableSheet
                    {
                        TableName = tableName,
                        TableCsv = tableCsv
                    };
                    return Codec.Encode(action.PlainValue);
                }
            );
            Field<ByteStringType>(
                name: "promoteValidator",
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
                    Currency currency = Asset.GovernanceToken;
                    var rawPublicKey = context.GetArgument<byte[]>("validatorPubKey");
                    PublicKey validatorPubKey = new PublicKey(rawPublicKey);
                    FungibleAssetValue amount =
                    FungibleAssetValue.Parse(currency, context.GetArgument<string>("amount"));
                    var action = new PromoteValidator(validatorPubKey, amount);
                    return Codec.Encode(Registry.Serialize(action));
                });
            Field<ByteStringType>(
                name: "delegate",
                arguments: new QueryArguments(
                    new QueryArgument<NonNullGraphType<AddressType>>
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
                    Currency currency = Asset.GovernanceToken;
                    Address validatorAddress = context.GetArgument<Address>("validatorAddress");
                    FungibleAssetValue amount =
                    FungibleAssetValue.Parse(currency, context.GetArgument<string>("amount"));
                    var action = new Libplanet.Action.Sys.Delegate(validatorAddress, amount);
                    return Codec.Encode(Registry.Serialize(action));
                });
            Field<ByteStringType>(
                name: "undelegate",
                arguments: new QueryArguments(
                    new QueryArgument<NonNullGraphType<AddressType>>
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
                    Currency currency = Asset.Share;
                    Address validatorAddress = context.GetArgument<Address>("validatorAddress");
                    FungibleAssetValue amount =
                    FungibleAssetValue.Parse(currency, context.GetArgument<string>("amount"));
                    var action = new Undelegate(validatorAddress, amount);
                    return Codec.Encode(Registry.Serialize(action));
                });
            Field<ByteStringType>(
                name: "cancelUndelegation",
                arguments: new QueryArguments(
                    new QueryArgument<NonNullGraphType<AddressType>>
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
                    Currency currency = Asset.ConsensusToken;
                    Address validatorAddress = context.GetArgument<Address>("validatorAddress");
                    FungibleAssetValue amount =
                    FungibleAssetValue.Parse(currency, context.GetArgument<string>("amount"));
                    var action = new CancelUndelegation(validatorAddress, amount);
                    return Codec.Encode(Registry.Serialize(action));
                });
            Field<ByteStringType>(
                name: "redelegate",
                arguments: new QueryArguments(
                    new QueryArgument<NonNullGraphType<AddressType>>
                    {
                        Name = "srcValidatorAddress",
                        Description = "Address of source validator to redelegate."
                    },
                    new QueryArgument<NonNullGraphType<AddressType>>
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
                    Currency currency = Asset.Share;
                    Address srcValidatorAddress = context.GetArgument<Address>("srcValidatorAddress");
                    Address dstValidatorAddress = context.GetArgument<Address>("dstValidatorAddress");
                    FungibleAssetValue amount =
                    FungibleAssetValue.Parse(currency, context.GetArgument<string>("amount"));
                    var action = new Redelegate(srcValidatorAddress, dstValidatorAddress, amount);
                    return Codec.Encode(Registry.Serialize(action));
                });

            Field<ByteStringType>(
                name: "withdrawDelegator",
                arguments: new QueryArguments(
                    new QueryArgument<NonNullGraphType<AddressType>>
                    {
                        Name = "validatorAddress",
                        Description = "Address of validator to withdraw."
                    }
                ),
                resolve: context =>
                {
                    Address validatorAddress = context.GetArgument<Address>("validatorAddress");
                    var action = new WithdrawDelegator(validatorAddress);
                    return Codec.Encode(Registry.Serialize(action));
                });

            Field<ByteStringType>(
                name: "withdrawValidator",
                resolve: context =>
                {
                    var action = new WithdrawValidator();
                    return Codec.Encode(Registry.Serialize(action));
                });
        }
    }
}
