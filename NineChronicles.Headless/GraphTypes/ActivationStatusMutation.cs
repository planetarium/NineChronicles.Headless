using System;
using Bencodex.Types;
using GraphQL;
using GraphQL.Types;
using Libplanet.Action;
using Libplanet.Blockchain;
using Libplanet.Crypto;
using Nekoyume.Action;
using Nekoyume.Model;
using Nekoyume.Model.State;
using NineChroniclesActionType = Libplanet.Action.PolymorphicAction<Nekoyume.Action.ActionBase>;
using Log = Serilog.Log;

namespace NineChronicles.Headless.GraphTypes
{
    public class ActivationStatusMutation : ObjectGraphType<NineChroniclesNodeService>
    {
        public ActivationStatusMutation()
        {
            Field<NonNullGraphType<BooleanGraphType>>("activateAccount",
                arguments: new QueryArguments(
                    new QueryArgument<NonNullGraphType<StringGraphType>>
                    {
                        Name = "encodedActivationKey",
                    }),
                resolve: context =>
                {
                    try
                    {
                        string encodedActivationKey =
                            context.GetArgument<string>("encodedActivationKey");
                        NineChroniclesNodeService service = context.Source;
                        // FIXME: Private key may not exists at this moment.
                        PrivateKey privateKey = service.PrivateKey;
                        ActivationKey activationKey = ActivationKey.Decode(encodedActivationKey);
                        BlockChain<NineChroniclesActionType> blockChain = service.Swarm.BlockChain;
                        IValue state = blockChain.GetState(activationKey.PendingAddress);

                        if (!(state is Bencodex.Types.Dictionary asDict))
                        {
                            context.Errors.Add(new ExecutionError("The given key was already expired."));
                            return false;
                        }

                        var pendingActivationState = new PendingActivationState(asDict);
                        ActivateAccount action = activationKey.CreateActivateAccount(
                            pendingActivationState.Nonce);

                        var actions = new NineChroniclesActionType[] { action };
                        blockChain.MakeTransaction(privateKey, actions);
                    }
                    catch (ArgumentException ae)
                    {
                        context.Errors.Add(new ExecutionError("The given key isn't in the correct foramt.", ae));
                        return false;
                    }
                    catch (Exception e)
                    {
                        var msg = "Unexpected exception occurred during ActivatedAccountsMutation: {e}";
                        context.Errors.Add(new ExecutionError(msg, e));
                        return false;
                    }

                    return true;
                });
        }
    }
}
