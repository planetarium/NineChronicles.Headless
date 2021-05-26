using System.Collections.Generic;
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
            Field<ListGraphType<NonNullGraphType<AvatarStateType>>>(
                "avatarStates",
                description: "List of avatar.",
                resolve: context =>
                {
                    List<AvatarState> avatarStates = new List<AvatarState>();
                    foreach (var kv in context.Source.agentState.avatarAddresses.OrderBy(a => a.Key))
                    {
                        if (context.Source.accountStateGetter(kv.Value) is { } state)
                        {
                            avatarStates.Add(new AvatarState((Dictionary)state));
                        }
                    }

                    return avatarStates;
                });
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
                    ).GetQuantityString(true);
                });
            Field<NonNullGraphType<LongGraphType>>(
                nameof(AgentState.MonsterCollectionRound),
                description: "Monster collection round of agent.",
                resolve: context => context.Source.agentState.MonsterCollectionRound
            );
            Field<NonNullGraphType<LongGraphType>>(
                "monsterCollectionLevel",
                description: "Current monster collection level.",
                resolve: context =>
                {
                    Address monsterCollectionAddress = MonsterCollectionState.DeriveAddress(context.Source.agentState.address,
                        context.Source.agentState.MonsterCollectionRound);
                    if (context.Source.accountStateGetter(monsterCollectionAddress) is { } state)
                    {
                        return new MonsterCollectionState((Dictionary) state).Level;
                    }

                    return 0;
                });

        }
    }
}
