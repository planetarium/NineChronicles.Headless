using System;
using System.Collections.Generic;
using System.Numerics;
using Bencodex;
using GraphQL;
using GraphQL.Types;
using Libplanet;
using Libplanet.Assets;
using Libplanet.Explorer.GraphTypes;
using Nekoyume.Action;
using NCAction = Libplanet.Action.PolymorphicAction<Nekoyume.Action.ActionBase>;

namespace NineChronicles.Headless.GraphTypes
{
    public class ActionQuery : ObjectGraphType
    {
        private static readonly Codec Codec = new Codec();
        public ActionQuery()
        {
            Field<ByteStringType>(
                name: "stake",
                arguments: new QueryArguments(new QueryArgument<BigIntGraphType>
                {
                    Name = "amount",
                    Description = "An amount to stake.",
                }),
                resolve: context => Codec.Encode(
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
                        CurrencyEnum.NCG => CurrencyType.NCG,
                        CurrencyEnum.CRYSTAL => CurrencyType.CRYSTAL,
                        _ => throw new ExecutionError("Unsupported Currency type.")
                    };
                    var amount = FungibleAssetValue.Parse(currency, context.GetArgument<string>("amount"));
                    var memo = context.GetArgument<string?>("memo");
                    NCAction action = new TransferAsset(sender, recipient, amount, memo);
                    return Codec.Encode(action.PlainValue);
                });
            Field<ByteStringType>(
                name: "faucetAsset",
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
                    }
                ),
                resolve: context =>
                {
                    var sender = context.GetArgument<Address>("sender");
                    var recipient = context.GetArgument<Address>("recipient");
                    Currency currency = context.GetArgument<CurrencyEnum>("currency") switch
                    {
                        CurrencyEnum.NCG => CurrencyType.NCG,
                        CurrencyEnum.CRYSTAL => CurrencyType.CRYSTAL,
                        _ => throw new ExecutionError("Unsupported Currency type.")
                    };
                    var amount = FungibleAssetValue.Parse(currency, context.GetArgument<string>("amount"));
                    NCAction action = new FaucetAsset(sender, recipient, amount);
                    return Codec.Encode(action.PlainValue);
                });
        }
    }
}
