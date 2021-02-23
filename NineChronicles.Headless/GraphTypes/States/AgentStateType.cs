using System.Linq;
using Bencodex.Types;
using GraphQL;
using GraphQL.Types;
using Libplanet.Action;
using Libplanet.Assets;
using Libplanet.Blockchain;
using Nekoyume.Action;
using Nekoyume.Model.State;

namespace NineChronicles.Headless.GraphTypes.States
{
    public class AgentStateType : ObjectGraphType<AgentState>
    {
        public AgentStateType(StandaloneContext standaloneContext)
        {
            Field<NonNullGraphType<AddressType>>(
                nameof(AgentState.address),
                description: "Address of agent.",
                resolve: context => context.Source.address);
            Field<ListGraphType<NonNullGraphType<AddressType>>>(
                nameof(AgentState.avatarAddresses),
                description: "Address list of avatar.",
                resolve: context => context.Source.avatarAddresses.Select(a => a.Value));
            Field<NonNullGraphType<StringGraphType>>(
                "gold",
                description: "Current NCG.",
                resolve: context =>
                {
                    if (!(standaloneContext.BlockChain is BlockChain<PolymorphicAction<ActionBase>> blockChain))
                    {
                        throw new ExecutionError(
                            $"{nameof(StandaloneContext)}.{nameof(StandaloneContext.BlockChain)} was not set yet!");
                    }
                    Currency currency = new GoldCurrencyState(
                        (Dictionary)blockChain.GetState(GoldCurrencyState.Address)
                    ).Currency;

                    return blockChain.GetBalance(
                        context.Source.address,
                        currency,
                        blockChain.Tip.Hash
                    ).GetQuantityString();
                });
        }
    }
}
