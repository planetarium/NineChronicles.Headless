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
using Nekoyume.Helper;
using Nekoyume.Model.Quest;
using Nekoyume.Model.State;
using static Lib9c.SerializeKeys;

namespace NineChronicles.Headless.GraphTypes.States
{
    public class AgentStateType : ObjectGraphType<AgentStateType.AgentStateContext>
    {
        public class AgentStateContext : StateContext
        {
            public AgentStateContext(AgentState agentState, AccountStateGetter accountStateGetter, AccountBalanceGetter accountBalanceGetter, long blockIndex)
                : base(accountStateGetter, accountBalanceGetter, blockIndex)
            {
                AgentState = agentState;
            }

            public AgentState AgentState { get; }

            public Address AgentAddress => AgentState.address;

            public IReadOnlyList<Address> GetAvatarAddresses() =>
                AgentState.avatarAddresses.OrderBy(pair => pair.Key).Select(pair => pair.Value).ToArray();
        }

        public AgentStateType()
        {
            Field<NonNullGraphType<AddressType>>(nameof(AgentState.address))
                .Description("Address of agent.")
                .Resolve(context => context.Source.AgentAddress);
            Field<ListGraphType<NonNullGraphType<AvatarStateType>>>("avatarStates")
                .Description("List of avatar.")
                .Resolve(context =>
                {
                    IReadOnlyList<Address> avatarAddresses = context.Source.GetAvatarAddresses();
                    return context.Source.AccountStateGetter.GetAvatarStates(avatarAddresses).Select(
                        x => new AvatarStateType.AvatarStateContext(
                            x,
                            context.Source.AccountStateGetter,
                            context.Source.AccountBalanceGetter,
                            context.Source.BlockIndex));
                });
            Field<NonNullGraphType<StringGraphType>>("gold")
                .Description("Current NCG.")
                .Resolve(context =>
                {
                    Currency currency = new GoldCurrencyState(
                        (Dictionary)context.Source.GetState(GoldCurrencyState.Address)!
                    ).Currency;

                    return context.Source.GetBalance(
                        context.Source.AgentAddress,
                        currency
                    ).GetQuantityString(true);
                });
            Field<NonNullGraphType<LongGraphType>>(nameof(AgentState.MonsterCollectionRound))
                .Description("Monster collection round of agent.")
                .Resolve(context => context.Source.AgentState.MonsterCollectionRound);
            Field<NonNullGraphType<LongGraphType>>("monsterCollectionLevel")
                .Description("Current monster collection level.")
                .Resolve(context =>
                {
                    Address monsterCollectionAddress = MonsterCollectionState.DeriveAddress(
                        context.Source.AgentAddress,
                        context.Source.AgentState.MonsterCollectionRound
                    );
                    if (context.Source.GetState(monsterCollectionAddress) is { } state)
                    {
                        return new MonsterCollectionState((Dictionary)state).Level;
                    }

                    return 0;
                });

            Field<NonNullGraphType<BooleanGraphType>>("hasTradedItem")
                .Resolve(context =>
                {
                    IReadOnlyList<Address> avatarAddresses = context.Source.GetAvatarAddresses();
                    var addresses = new Address[avatarAddresses.Count * 2];
                    for (int i = 0; i < avatarAddresses.Count; i++)
                    {
                        addresses[i] = avatarAddresses[i].Derive(LegacyQuestListKey);
                        addresses[avatarAddresses.Count + i] = avatarAddresses[i];
                    }

                    IReadOnlyList<IValue?> values = context.Source.GetStates(addresses);
                    for (int i = 0; i < avatarAddresses.Count; i++)
                    {
                        if (values[i] is { } rawQuestList)
                        {
                            var questList = new QuestList((Dictionary)rawQuestList);
                            var traded = IsTradeQuestCompleted(questList);
                            if (traded)
                            {
                                return true;
                            }
                        }
                        else if (values[avatarAddresses.Count + i] is { } state)
                        {
                            var avatarState = new AvatarState((Dictionary)state);
                            var traded = IsTradeQuestCompleted(avatarState.questList);
                            if (traded)
                            {
                                return true;
                            }
                        }
                    }

                    return false;
                });
            Field<NonNullGraphType<StringGraphType>>("crystal")
                .Description("Current CRYSTAL.")
                .Resolve(context =>
                    context.Source.GetBalance(
                        context.Source.AgentAddress,
                        CrystalCalculator.CRYSTAL
                    ).GetQuantityString(true));
        }

        private static bool IsTradeQuestCompleted(QuestList questList)
        {
            return questList
                .OfType<TradeQuest>()
                .Any(q => q.Complete);
        }
    }
}
