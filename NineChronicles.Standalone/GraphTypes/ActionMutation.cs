using GraphQL;
using GraphQL.Types;
using Libplanet;
using Libplanet.Action;
using Libplanet.Blockchain;
using Libplanet.Crypto;
using Nekoyume.Action;
using Serilog;
using System;
using System.Collections.Generic;
using NineChroniclesActionType = Libplanet.Action.PolymorphicAction<Nekoyume.Action.ActionBase>;

namespace NineChronicles.Standalone.GraphTypes
{
    public class ActionMutation : ObjectGraphType<NineChroniclesNodeService>
    {
        public ActionMutation()
        {
            Field<NonNullGraphType<BooleanGraphType>>("createAvatar",
                resolve: context =>
                {
                    try
                    {
                        NineChroniclesNodeService service = context.Source;
                        PrivateKey privatekey = service.PrivateKey;
                        BlockChain<NineChroniclesActionType> blockChain = service.Swarm.BlockChain;
                        Address userAddress = privatekey.PublicKey.ToAddress();
                        Address avatarAddress = userAddress.Derive("avatar_0");

                        var action = new CreateAvatar
                        {
                            avatarAddress = avatarAddress,
                            index = 0,
                            hair = 0,
                            lens = 0,
                            ear = 0,
                            tail = 0,
                            name = "createbymutation",
                        };

                        var actions = new PolymorphicAction<ActionBase>[] { action };
                        blockChain.MakeTransaction(privatekey, actions);
                    }
                    catch (Exception e)
                    {
                        var msg = $"Unexpected exception occurred during {typeof(ActionMutation)}: {e}";
                        context.Errors.Add(new ExecutionError(msg, e));
                        Log.Error(msg, e);
                        return false;
                    }

                    return true;
                });

            Field<NonNullGraphType<BooleanGraphType>>("hackAndSlash",
            arguments: new QueryArguments(
                new QueryArgument<NonNullGraphType<StringGraphType>>
                {
                    Name = "weeklyArenaAddress",
                },
                new QueryArgument<NonNullGraphType<StringGraphType>>
                {
                    Name = "rankingArenaAddress",
                }),
            resolve: context =>
            {
                try
                {
                    NineChroniclesNodeService service = context.Source;
                    PrivateKey privatekey = service.PrivateKey;
                    BlockChain<NineChroniclesActionType> blockChain = service.Swarm.BlockChain;
                    Address userAddress = privatekey.PublicKey.ToAddress();
                    Address avatarAddress = userAddress.Derive("avatar_0");
                    Address weeklyArenaAddress = new Address(context.GetArgument<string>("weeklyArenaAddress"));
                    Address rankingArenaAddress = new Address(context.GetArgument<string>("rankingArenaAddress"));

                    var action = new HackAndSlash
                    {
                        avatarAddress = avatarAddress,
                        worldId = 1,
                        stageId = 1,
                        WeeklyArenaAddress = weeklyArenaAddress,
                        RankingMapAddress = rankingArenaAddress,
                        costumes = new List<int>(),
                        equipments = new List<Guid>(),
                        foods = new List<Guid>(),
                    };

                    var actions = new PolymorphicAction<ActionBase>[] { action };
                    blockChain.MakeTransaction(privatekey, actions);
                }
                catch (Exception e)
                {
                    var msg = $"Unexpected exception occurred during {typeof(ActionMutation)}: {e}";
                    context.Errors.Add(new ExecutionError(msg, e));
                    Log.Error(msg, e);
                    return false;
                }

                return true;
            });

            Field<NonNullGraphType<BooleanGraphType>>("combinationEquipment",
                arguments: new QueryArguments(
                    new QueryArgument<NonNullGraphType<DecimalGraphType>>
                    {
                        Name = "recipeId",
                    },
                    new QueryArgument<NonNullGraphType<DecimalGraphType>>
                    {
                        Name = "slotIndex",
                    },
                    new QueryArgument<DecimalGraphType>
                    {
                        Name = "subRecipeId",
                    }),
                resolve: context =>
                {
                    try
                    {
                        NineChroniclesNodeService service = context.Source;
                        PrivateKey privatekey = service.PrivateKey;
                        BlockChain<NineChroniclesActionType> blockChain = service.Swarm.BlockChain;
                        Address userAddress = privatekey.PublicKey.ToAddress();
                        Address avatarAddress = userAddress.Derive("avatar_0");
                        int recipeId = context.GetArgument<int>("recipeId");
                        int slotIndex = context.GetArgument<int>("slotIndex");
                        int? subRecipeId = context.GetArgument<int>("subRecipeId");

                        var action = new CombinationEquipment
                        {
                            AvatarAddress = avatarAddress,
                            RecipeId = recipeId,
                            SlotIndex = slotIndex,
                            SubRecipeId = subRecipeId
                        };

                        var actions = new PolymorphicAction<ActionBase>[] { action };
                        blockChain.MakeTransaction(privatekey, actions);
                    }
                    catch (Exception e)
                    {
                        var msg = $"Unexpected exception occurred during {typeof(ActionMutation)}: {e}";
                        context.Errors.Add(new ExecutionError(msg, e));
                        Log.Error(msg, e);
                        return false;
                    }

                    return true;
                });

            Field<NonNullGraphType<BooleanGraphType>>("itemEnhancement",
                arguments: new QueryArguments(
                    new QueryArgument<NonNullGraphType<StringGraphType>>
                    {
                        Name = "itemId",
                    },
                    new QueryArgument<NonNullGraphType<StringGraphType>>
                    {
                        Name = "materialIds",
                    }),
                resolve: context =>
                {
                    try
                    {
                        NineChroniclesNodeService service = context.Source;
                        PrivateKey privatekey = service.PrivateKey;
                        BlockChain<NineChroniclesActionType> blockChain = service.Swarm.BlockChain;
                        Address userAddress = privatekey.PublicKey.ToAddress();
                        Address avatarAddress = userAddress.Derive("avatar_0");
                        Guid itemId = Guid.Parse(context.GetArgument<string>("itemId"));
                        Guid materialId = Guid.Parse(context.GetArgument<string>("materialIds"));

                        var action = new ItemEnhancement
                        {
                            avatarAddress = avatarAddress,
                            slotIndex = 0,
                            itemId = itemId,
                            materialIds = new[] { materialId }
                        };

                        var actions = new PolymorphicAction<ActionBase>[] { action };
                        blockChain.MakeTransaction(privatekey, actions);
                    }
                    catch (Exception e)
                    {
                        var msg = $"Unexpected exception occurred during {typeof(ActionMutation)}: {e}";
                        context.Errors.Add(new ExecutionError(msg, e));
                        Log.Error(msg, e);
                        return false;
                    }

                    return true;
                });

            Field<NonNullGraphType<BooleanGraphType>>("buy",
                arguments: new QueryArguments(
                    new QueryArgument<NonNullGraphType<StringGraphType>>
                    {
                        Name = "sellerAgentAddress",
                    },
                    new QueryArgument<NonNullGraphType<StringGraphType>>
                    {
                        Name = "sellerAvatarAddress",
                    },
                    new QueryArgument<NonNullGraphType<StringGraphType>>
                    {
                        Name = "productId",
                    }),
                resolve: context =>
                {
                    try
                    {
                        NineChroniclesNodeService service = context.Source;
                        PrivateKey privatekey = service.PrivateKey;
                        BlockChain<NineChroniclesActionType> blockChain = service.Swarm.BlockChain;
                        Address userAddress = privatekey.PublicKey.ToAddress();
                        Address avatarAddress = userAddress.Derive("avatar_0");
                        Address sellerAgentAddress = new Address(context.GetArgument<string>("sellerAgentAddress"));
                        Address sellerAvatarAddress = new Address(context.GetArgument<string>("sellerAvatarAddress"));
                        Guid productId = Guid.Parse(context.GetArgument<string>("productId"));

                        var action = new Buy
                        {
                            buyerAvatarAddress = avatarAddress,
                            sellerAgentAddress = sellerAgentAddress,
                            sellerAvatarAddress = sellerAvatarAddress,
                            productId = productId,
                        };

                        var actions = new PolymorphicAction<ActionBase>[] { action };
                        blockChain.MakeTransaction(privatekey, actions);
                    }
                    catch (Exception e)
                    {
                        var msg = $"Unexpected exception occurred during {typeof(ActionMutation)}: {e}";
                        context.Errors.Add(new ExecutionError(msg, e));
                        Log.Error(msg, e);
                        return false;
                    }

                    return true;
                });
        }
    }
}
