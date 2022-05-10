using System;
using System.Collections.Generic;
using System.Numerics;
using Bencodex;
using GraphQL;
using GraphQL.Types;
using Libplanet;
using Libplanet.Explorer.GraphTypes;
using Nekoyume.Action;

namespace NineChronicles.Headless.GraphTypes
{
    public class ActionQuery : ObjectGraphType
    {
        private static readonly Codec Codec = new Codec();
        public ActionQuery()
        {
            // TODO restore when merge development
            // var codec = new Codec();
            // Field<ByteStringType>(
            //     name: "stake",
            //     arguments: new QueryArguments(new QueryArgument<BigIntGraphType>
            //     {
            //         Name = "amount",
            //         Description = "An amount to stake.",
            //     }),
            //     resolve: context => codec.Encode(new Stake(context.GetArgument<BigInteger>("amount")).PlainValue));
            //
            // Field<ByteStringType>(
            //     name: "claimStakeReward",
            //     arguments: new QueryArguments(
            //         new QueryArgument<AddressType>
            //         {
            //             Name = "avatarAddress",
            //             Description = "The avatar address to receive staking rewards."
            //         }),
            //     resolve: context =>
            //         codec.Encode(new ClaimStakeReward(context.GetArgument<Address>("avatarAddress")).PlainValue));
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
                    var action = new Grinding
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
                    var action = new UnlockEquipmentRecipe
                    {
                        AvatarAddress = avatarAddress,
                        RecipeIds = recipeIds,
                    };
                    return Codec.Encode(action.PlainValue);
                });
        }
    }
}
