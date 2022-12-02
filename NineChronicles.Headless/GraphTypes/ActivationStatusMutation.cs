using System;
using Bencodex.Types;
using GraphQL;
using GraphQL.Types;
using Nekoyume.Action;
using Nekoyume.Model;
using Nekoyume.Model.State;
using NCAction = Libplanet.Action.PolymorphicAction<Nekoyume.Action.ActionBase>;

namespace NineChronicles.Headless.GraphTypes
{
    public class ActivationStatusMutation : ObjectGraphType
    {
        public ActivationStatusMutation(NineChroniclesNodeService service)
        {
            Field<NonNullGraphType<BooleanGraphType>>("activateAccount")
                .Argument<string>("encodedActivationKey", false)
                .Resolve(context =>
                {
                    try
                    {
                        string encodedActivationKey =
                            context.GetArgument<string>("encodedActivationKey");
                        // FIXME: Private key may not exists at this moment.
                        if (!(service.MinerPrivateKey is { } privateKey))
                        {
                            throw new InvalidOperationException($"{nameof(privateKey)} is null.");
                        }

                        ActivationKey activationKey = ActivationKey.Decode(encodedActivationKey);
                        if (!(service.Swarm?.BlockChain is { } blockChain))
                        {
                            throw new InvalidOperationException($"{nameof(blockChain)} is null.");
                        }

                        IValue state = blockChain.GetState(activationKey.PendingAddress);

                        if (!(state is Bencodex.Types.Dictionary asDict))
                        {
                            context.Errors.Add(new ExecutionError("The given key was already expired."));
                            return false;
                        }

                        var pendingActivationState = new PendingActivationState(asDict);
                        ActivateAccount action = activationKey.CreateActivateAccount(
                            pendingActivationState.Nonce);

                        var actions = new NCAction[] { action };
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
