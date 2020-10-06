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
                    catch(Exception e)
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
                    catch(Exception e)
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
