using System.Collections.Generic;
using System.Linq;
using Bencodex.Types;
using GraphQL.Types;
using Libplanet.Crypto;
using Libplanet.Types.Assets;
using Libplanet.Explorer.GraphTypes;
using Libplanet.Action.State;
using Nekoyume.Action;
using Nekoyume.Helper;
using Nekoyume.Model.Quest;
using Nekoyume.Model.State;
using NineChronicles.Headless.GraphTypes.States.Models;
using static Lib9c.SerializeKeys;

namespace NineChronicles.Headless.GraphTypes.States
{
    public class AgentStateType : ObjectGraphType<AgentStateType.AgentStateContext>
    {
        public class AgentStateContext : StateContext
        {
            public AgentStateContext(AgentState agentState, IAccountState accountState, long blockIndex)
                : base(accountState, blockIndex)
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
            Field<NonNullGraphType<AddressType>>(
                nameof(AgentState.address),
                description: "Address of agent.",
                resolve: context => context.Source.AgentAddress);
            Field<ListGraphType<NonNullGraphType<AvatarStateType>>>(
                "avatarStates",
                description: "List of avatar.",
                resolve: context =>
                {
                    IReadOnlyList<Address> avatarAddresses = context.Source.GetAvatarAddresses();
                    return context.Source.AccountState.GetAvatarStates(avatarAddresses).Select(
                        x => new AvatarStateType.AvatarStateContext(
                            x,
                            context.Source.AccountState,
                            context.Source.BlockIndex));
                });
            Field<NonNullGraphType<StringGraphType>>(
                "gold",
                description: "Current NCG.",
                resolve: context =>
                {
                    Currency currency = new GoldCurrencyState(
                        (Dictionary)context.Source.GetState(GoldCurrencyState.Address)!
                    ).Currency;

                    return context.Source.GetBalance(
                        context.Source.AgentAddress,
                        currency
                    ).GetQuantityString(true);
                });
            Field<NonNullGraphType<LongGraphType>>(
                nameof(AgentState.MonsterCollectionRound),
                description: "Monster collection round of agent.",
                resolve: context => context.Source.AgentState.MonsterCollectionRound
            );
            Field<NonNullGraphType<LongGraphType>>(
                "monsterCollectionLevel",
                description: "Current monster collection level.",
                resolve: context =>
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

            Field<NonNullGraphType<BooleanGraphType>>(
                "hasTradedItem",
                resolve: context =>
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
                }
            );
            Field<NonNullGraphType<StringGraphType>>(
                "crystal",
                description: "Current CRYSTAL.",
                resolve: context => context.Source.GetBalance(
                    context.Source.AgentAddress,
                    CrystalCalculator.CRYSTAL
                ).GetQuantityString(true));
            Field<NonNullGraphType<MeadPledgeType>>(
                "pledge",
                description: "mead pledge information.",
                resolve: context =>
                {
                    var pledgeAddress = context.Source.AgentAddress.GetPledgeAddress();
                    Address? address = null;
                    bool approved = false;
                    int mead = 0;
                    if (context.Source.GetState(pledgeAddress) is List l)
                    {
                        address = l[0].ToAddress();
                        approved = l[1].ToBoolean();
                        mead = l[2].ToInteger();
                    }

                    return (address, approved, mead);
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
