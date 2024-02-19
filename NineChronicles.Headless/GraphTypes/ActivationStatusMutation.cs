using System;
using Bencodex.Types;
using GraphQL;
using GraphQL.Types;
using Libplanet.Action;
using Nekoyume.Action;
using Nekoyume.Model;
using Nekoyume.Model.State;
using Nekoyume.Module;

namespace NineChronicles.Headless.GraphTypes
{
    public class ActivationStatusMutation : ObjectGraphType
    {
        public ActivationStatusMutation(NineChroniclesNodeService service)
        {
            DeprecationReason = "Since NCIP-15, it doesn't care account activation.";

            Field<NonNullGraphType<BooleanGraphType>>("activateAccount",
                deprecationReason: "Since NCIP-15, it doesn't care account activation.",
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

                        IValue state = blockChain.GetWorldState().GetLegacyState(activationKey.PendingAddress);

                        if (!(state is Bencodex.Types.Dictionary asDict))
                        {
                            context.Errors.Add(new ExecutionError("The given key was already expired."));
                            return false;
                        }

                        var pendingActivationState = new PendingActivationState(asDict);
                        ActivateAccount action = activationKey.CreateActivateAccount(
                            pendingActivationState.Nonce);

                        var actions = new IAction[] { action };
                        blockChain.MakeTransaction(privateKey, actions);
                    }
                    catch (ArgumentException ae)
                    {
                        context.Errors.Add(new ExecutionError("The given key isn't in the correct format.", ae));
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
