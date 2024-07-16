using Bencodex.Types;
using GraphQL;
using GraphQL.Types;
using Libplanet.Crypto;
using Nekoyume.Action;
using Nekoyume.Model;
using Nekoyume.Model.State;
using System;
using Libplanet.Explorer.GraphTypes;
using Nekoyume.Module;
using Log = Serilog.Log;

namespace NineChronicles.Headless.GraphTypes
{
    public class ActivationStatusQuery : ObjectGraphType
    {
        public ActivationStatusQuery(INodeContext nodeContext, IBlockChainContext blockChainContext)
        {
            DeprecationReason = "Since NCIP-15, it doesn't care account activation.";

            Field<NonNullGraphType<BooleanGraphType>>(
                name: "activated",
                deprecationReason: "Since NCIP-15, it doesn't care account activation.",
                resolve: context =>
                {
                    try
                    {
                        Address userAddress = nodeContext.Address;
                        Address activatedAddress = userAddress.Derive(ActivationKey.DeriveKey);

                        if (blockChainContext.GetWorldState().GetLegacyState(activatedAddress) is Bencodex.Types.Boolean)
                        {
                            return true;
                        }

                        // Preserve previous check code due to migration period.
                        // TODO: Remove this code after v100061+
                        IValue state = blockChainContext.GetWorldState().GetLegacyState(ActivatedAccountsState.Address);

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

            Field<NonNullGraphType<BooleanGraphType>>(
                name: "addressActivated",
                deprecationReason: "Since NCIP-15, it doesn't care account activation.",
                arguments: new QueryArguments(
                    new QueryArgument<NonNullGraphType<AddressType>>
                    {
                        Name = "address"
                    }
                ),
                resolve: context =>
                {
                    try
                    {
                        var userAddress = context.GetArgument<Address>("address");
                        Address activatedAddress = userAddress.Derive(ActivationKey.DeriveKey);

                        if (blockChainContext.GetWorldState().GetLegacyState(activatedAddress) is Bencodex.Types.Boolean)
                        {
                            return true;
                        }

                        // backward for launcher E2E test.
                        // TODO: Remove this code after launcher E2E test fixed.
                        IValue state = blockChainContext.GetWorldState().GetLegacyState(ActivatedAccountsState.Address);

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
