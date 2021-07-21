using System;
using System.Collections.Generic;
using System.Linq;
using Bencodex.Types;
using GraphQL.Types;
using Libplanet;
using Libplanet.Action;
using Libplanet.Assets;
using Libplanet.Explorer.GraphTypes;
using Nekoyume.Action;
using Nekoyume.Model.Quest;
using Nekoyume.Model.State;
using static Lib9c.SerializeKeys;

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
                        avatarStates.Add(context.Source.accountStateGetter.GetAvatarState(kv.Value));
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
                    Address monsterCollectionAddress = MonsterCollectionState.DeriveAddress(
                        context.Source.agentState.address,
                        context.Source.agentState.MonsterCollectionRound
                    );
                    if (context.Source.accountStateGetter(monsterCollectionAddress) is { } state)
                    {
                        return new MonsterCollectionState((Dictionary) state).Level;
                    }

                    return 0;
                });

            Field<NonNullGraphType<BooleanGraphType>>(
                "hasTradedItem",
                resolve: context =>
                {
                    foreach (var (_, avatarAddress) in context.Source.agentState.avatarAddresses.OrderBy(a => a.Key))
                    {
                        var questListAddress = avatarAddress.Derive(LegacyQuestListKey);
                        if (context.Source.accountStateGetter(questListAddress) is { } rawQuestList)
                        {
                            var questList = new QuestList((Dictionary)rawQuestList);
                            var traded = IsTradeQuestCompleted(questList);
                            if (traded)
                            {
                                return true;
                            }

                            continue;
                        }

                        if (context.Source.accountStateGetter(avatarAddress) is { } state)
                        {
                            var avatarState = new AvatarState((Dictionary) state);
                            var traded = IsTradeQuestCompleted(avatarState.questList);
                            if (traded)
                            {
                                return true;
                            }
                        }
                    }

                    return false;
                }
            );
        }

        private static bool IsTradeQuestCompleted(QuestList questList)
        {
            return questList
                .OfType<TradeQuest>()
                .Any(q => q.Complete);
        }
    }
}
