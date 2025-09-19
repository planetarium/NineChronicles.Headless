using System;
using System.Collections.Generic;
using System.Linq;
using Bencodex.Types;
using GraphQL;
using GraphQL.Types;
using Libplanet.Crypto;
using Libplanet.Types.Assets;
using Libplanet.Explorer.GraphTypes;
using Libplanet.Action.State;
using Nekoyume.Action;
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
            public AgentStateContext(AgentState agentState, IWorldState worldState, long blockIndex, StateMemoryCache stateMemoryCache)
                : base(worldState, blockIndex, stateMemoryCache)
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

                    // Check if inventory field is requested in the GraphQL query
                    bool inventoryRequested = IsFieldRequested(context, "inventory");

                    return avatarAddresses.Select(avatarAddress =>
                    {
                        try
                        {
                            var avatarState = context.Source.WorldState.GetAvatarState(
                                avatarAddress,
                                getInventory: inventoryRequested);
                            return new AvatarStateType.AvatarStateContext(
                                avatarState,
                                context.Source.WorldState,
                                context.Source.BlockIndex,
                                context.Source.StateMemoryCache);
                        }
                        catch (Exception)
                        {
                            // Log the error and return null to skip this avatar
                            // This prevents the entire query from failing due to one problematic avatar
                            return null;
                        }
                    }).Where(x => x != null);
                });
            Field<NonNullGraphType<StringGraphType>>(
                "gold",
                description: "Current NCG.",
                resolve: context =>
                {
                    Currency currency = new GoldCurrencyState(
                        (Dictionary)context.Source.WorldState.GetLegacyState(GoldCurrencyState.Address)!
                    ).Currency;

                    return context.Source.WorldState.GetBalance(
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
                    if (context.Source.WorldState.GetLegacyState(monsterCollectionAddress) is { } state)
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
                    IEnumerable<AvatarState> avatarStates =
                        avatarAddresses.Select(address => context.Source.WorldState.GetAvatarState(address));
                    return avatarStates.Any(avatarState => IsTradeQuestCompleted(avatarState.questList));
                }
            );
            Field<NonNullGraphType<StringGraphType>>(
                "crystal",
                description: "Current CRYSTAL.",
                resolve: context => context.Source.WorldState.GetBalance(
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
                    if (context.Source.WorldState.GetLegacyState(pledgeAddress) is List l)
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

        private static bool IsFieldRequested(IResolveFieldContext<AgentStateContext> context, string fieldName)
        {
            // Check if the field is requested in the current selection set
            var selectionSet = context.FieldAst?.SelectionSet;
            if (selectionSet == null)
            {
                return false;
            }

            // Look for the field in the selection set
            foreach (var selection in selectionSet.Selections)
            {
                if (selection is GraphQL.Language.AST.Field field && field.Name == fieldName)
                {
                    return true;
                }
            }

            return false;
        }
    }
}
