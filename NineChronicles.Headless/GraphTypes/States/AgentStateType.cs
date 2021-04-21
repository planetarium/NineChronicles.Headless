using System.Linq;
using Bencodex.Types;
using GraphQL.Types;
using Libplanet;
using Libplanet.Action;
using Libplanet.Assets;
using Libplanet.Explorer.GraphTypes;
using Nekoyume.Model.State;

namespace NineChronicles.Headless.GraphTypes.States
{
    public class AgentStateType : ObjectGraphType<(AgentState agentState, AccountStateGetter accountStateGetter, AccountBalanceGetter accountBalanceGetter)>
    {
        public AgentStateType()
        {
            Field<NonNullGraphType<AddressType>>(
                nameof(AgentState.address),
                description: "Address of agent.",
                resolve: context => context.Source.agentState.address);
            Field<ListGraphType<NonNullGraphType<AddressType>>>(
                nameof(AgentState.avatarAddresses),
                description: "Address list of avatar.",
                resolve: context => context.Source.agentState.avatarAddresses.Select(a => a.Value));
            Field<NonNullGraphType<StringGraphType>>(
                "gold",
                description: "Current NCG.",
                resolve: context =>
                {
                    Currency currency = new GoldCurrencyState(
                        (Dictionary)context.Source.accountStateGetter(GoldCurrencyState.Address)!
                    ).Currency;

                    return context.Source.accountBalanceGetter(
                        context.Source.agentState.address,
                        currency
                    ).GetQuantityString();
                });
            Field<NonNullGraphType<LongGraphType>>(
                nameof(AgentState.StakingRound),
                description: "Staking round of agent.",
                resolve: context => context.Source.agentState.StakingRound
            );
            Field<NonNullGraphType<LongGraphType>>(
                "stakingLevel",
                description: "Current staking level.",
                resolve: context =>
                {
                    Address stakingAddress = StakingState.DeriveAddress(context.Source.agentState.address,
                        context.Source.agentState.StakingRound);
                    if (context.Source.accountStateGetter(stakingAddress) is { } state)
                    {
                        return new StakingState((Dictionary) state).Level;
                    }

                    return null;
                });

        }
    }
}
