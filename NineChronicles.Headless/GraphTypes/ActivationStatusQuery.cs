using Bencodex.Types;
using GraphQL;
using GraphQL.Types;
using Libplanet;
using Nekoyume.Action;
using Nekoyume.Model;
using Nekoyume.Model.State;
using System;
using Log = Serilog.Log;

namespace NineChronicles.Headless.GraphTypes
{
    public class ActivationStatusQuery : ObjectGraphType
    {
        public ActivationStatusQuery(StandaloneContext standaloneContext)
        {
            Field<NonNullGraphType<BooleanGraphType>>(
                name: "activated",
                resolve: context =>
                {
                    var service = standaloneContext.NineChroniclesNodeService;

                    if (service is null)
                    {
                        return false;
                    }

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

                        Address userAddress = privateKey.ToAddress();
                        Address activatedAddress = userAddress.Derive(ActivationKey.DeriveKey);

                        if (blockChain.GetState(activatedAddress) is Bencodex.Types.Boolean)
                        {
                            return true;
                        }

                        // Preserve previous check code due to migration period.
                        // TODO: Remove this code after v100061+
                        IValue state = blockChain.GetState(ActivatedAccountsState.Address);

                        if (state is Bencodex.Types.Dictionary asDict)
                        {
                            var activatedAccountsState = new ActivatedAccountsState(asDict);
                            var activatedAccounts = activatedAccountsState.Accounts;
                            return activatedAccounts.Count == 0
                                   || activatedAccounts.Contains(userAddress);
                        }

                        return true;
                    }
                    catch (Exception e)
                    {
                        var msg = "Unexpected exception occurred during ActivationStatusQuery: {e}";
                        context.Errors.Add(new ExecutionError(msg, e));
                        Log.Error(msg, e);
                        return false;
                    }
                }
            );
        }
    }
}
