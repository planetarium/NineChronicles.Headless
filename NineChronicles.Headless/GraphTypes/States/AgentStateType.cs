using System.Collections.Generic;
using System.Linq;
using Bencodex.Types;
using GraphQL.Types;
using Libplanet.Crypto;
using Libplanet.Types.Assets;
using Libplanet.Explorer.GraphTypes;
using Libplanet.Action.State;
using Nekoyume.Action.Extensions;
using Nekoyume.Helper;
using Nekoyume.Model.Quest;
using Nekoyume.Model.State;
using Nekoyume.Module;
using NineChronicles.Headless.GraphTypes.States.Models;
using static Lib9c.SerializeKeys;

namespace NineChronicles.Headless.GraphTypes.States
{
    public class AgentStateType : ObjectGraphType<AgentStateType.AgentStateContext>
    {
        public class AgentStateContext : StateContext
        {
            public AgentStateContext(AgentState agentState, IWorldState worldState, long blockIndex)
                : base(worldState, blockIndex)
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
                    return avatarAddresses.Select(address => new AvatarStateType.AvatarStateContext(
                        AvatarModule.GetAvatarState(context.Source.WorldState, address),
                        context.Source.WorldState,
                        context.Source.BlockIndex));
                });
            Field<NonNullGraphType<StringGraphType>>(
                "gold",
                description: "Current NCG.",
                resolve: context =>
                {
                    Currency currency = new GoldCurrencyState(
                        (Dictionary)LegacyModule.GetState(
                            context.Source.WorldState,
                            GoldCurrencyState.Address)!
                    ).Currency;

                    return LegacyModule.GetBalance(
                        context.Source.WorldState,
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
                    if (LegacyModule.GetState(
                            context.Source.WorldState,
                            monsterCollectionAddress) is { } state)
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

                    IReadOnlyList<IValue?> values = LegacyModule.GetStates(
                        context.Source.WorldState,
                        addresses);
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
                resolve: context => LegacyModule.GetBalance(
                    context.Source.WorldState,
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
                    if (LegacyModule.GetState(context.Source.WorldState, pledgeAddress) is List l)
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
