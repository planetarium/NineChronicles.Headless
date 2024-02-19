using System;
using System.Collections.Generic;
using System.Linq;
using Bencodex.Types;
using GraphQL.Types;
using Lib9c;
using Libplanet.Explorer.GraphTypes;
using Libplanet.Action.State;
using Nekoyume.Action;
using Nekoyume.Model.State;
using Nekoyume.Module;
using Nekoyume.TableData;
using NineChronicles.Headless.GraphTypes.States.Models;
using NineChronicles.Headless.GraphTypes.States.Models.World;
using NineChronicles.Headless.GraphTypes.States.Models.Item;
using NineChronicles.Headless.GraphTypes.States.Models.Mail;
using NineChronicles.Headless.GraphTypes.States.Models.Quest;

namespace NineChronicles.Headless.GraphTypes.States
{
    public class AvatarStateType : ObjectGraphType<AvatarStateType.AvatarStateContext>
    {
        public class AvatarStateContext : StateContext
        {
            public AvatarStateContext(AvatarState avatarState, IWorldState worldState, long blockIndex, StateMemoryCache stateMemoryCache)
                : base(worldState, blockIndex, stateMemoryCache)
            {
                AvatarState = avatarState;
            }

            public AvatarState AvatarState { get; }
        }

        public AvatarStateType()
        {
            Field<NonNullGraphType<AddressType>>(
                nameof(AvatarState.address),
                description: "Address of avatar.",
                resolve: context => context.Source.AvatarState.address);
            Field<NonNullGraphType<IntGraphType>>(
                nameof(AvatarState.blockIndex),
                description: "Block index at the latest executed action.",
                resolve: context => context.Source.AvatarState.blockIndex);
            Field<NonNullGraphType<IntGraphType>>(
                nameof(AvatarState.characterId),
                description: "Character ID from CharacterSheet.",
                resolve: context => context.Source.AvatarState.characterId);
            Field<NonNullGraphType<LongGraphType>>(
                nameof(AvatarState.dailyRewardReceivedIndex),
                description: "Block index at the DailyReward execution.",
                resolve: context => context.Source.AvatarState.dailyRewardReceivedIndex);
            Field<NonNullGraphType<AddressType>>(
                nameof(AvatarState.agentAddress),
                description: "Address of agent.",
                resolve: context => context.Source.AvatarState.agentAddress);
            Field<NonNullGraphType<IntGraphType>>(
                "index",
                description: "The index of this avatar state among its agent's avatar addresses.",
                resolve: context =>
                {
                    if (context.Source.WorldState.GetAgentState(context.Source.AvatarState.agentAddress) is not
                        { } agentState)
                    {
                        throw new InvalidOperationException();
                    }

                    return agentState.avatarAddresses
                        .First(x => x.Value.Equals(context.Source.AvatarState.address))
                        .Key;
                });
            Field<NonNullGraphType<LongGraphType>>(
                nameof(AvatarState.updatedAt),
                description: "Block index at the latest executed action.",
                resolve: context => context.Source.AvatarState.updatedAt);

            Field<NonNullGraphType<StringGraphType>>(
                nameof(AvatarState.name),
                description: "Avatar name.",
                resolve: context => context.Source.AvatarState.name);
            Field<NonNullGraphType<IntGraphType>>(
                nameof(AvatarState.exp),
                description: "Avatar total EXP.",
                resolve: context => context.Source.AvatarState.exp);
            Field<NonNullGraphType<IntGraphType>>(
                nameof(AvatarState.level),
                description: "Avatar Level.",
                resolve: context => context.Source.AvatarState.level);
            Field<NonNullGraphType<IntGraphType>>(
                nameof(AvatarState.actionPoint),
                description: "Current ActionPoint.",
                resolve: context => context.Source.AvatarState.actionPoint);
            Field<NonNullGraphType<IntGraphType>>(
                nameof(AvatarState.ear),
                description: "Index of ear color.",
                resolve: context => context.Source.AvatarState.ear);
            Field<NonNullGraphType<IntGraphType>>(
                nameof(AvatarState.hair),
                description: "Index of hair color.",
                resolve: context => context.Source.AvatarState.hair);
            Field<NonNullGraphType<IntGraphType>>(
                nameof(AvatarState.lens),
                description: "Index of eye color.",
                resolve: context => context.Source.AvatarState.lens);
            Field<NonNullGraphType<IntGraphType>>(
                nameof(AvatarState.tail),
                description: "Index of tail color.",
                resolve: context => context.Source.AvatarState.tail);

            Field<NonNullGraphType<InventoryType>>(
                nameof(AvatarState.inventory),
                description: "Avatar inventory.",
                resolve: context => context.Source.AvatarState.inventory);
            Field<NonNullGraphType<AddressType>>(
                "inventoryAddress",
                description: "Avatar inventory address.",
                resolve: context => context.Source.AvatarState.address.Derive(SerializeKeys.LegacyInventoryKey));
            Field<NonNullGraphType<ListGraphType<NonNullGraphType<RuneStateType>>>>(
                name: "runes",
                description: "Rune list of avatar",
                resolve: context =>
                {
                    var runeSheet = context.Source.WorldState.GetSheet<RuneSheet>();
                    var runeList = new List<RuneState>();
                    foreach (var rune in runeSheet)
                    {
                        var runeState = context.Source.WorldState.GetLegacyState(
                            RuneState.DeriveAddress(context.Source.AvatarState.address, rune.Id)
                        );
                        if (runeState is not null)
                        {
                            runeList.Add(new RuneState(runeState as List));
                        }
                    }

                    return runeList;
                }
            );
            Field<NonNullGraphType<ListGraphType<NonNullGraphType<AddressType>>>>(
                nameof(AvatarState.combinationSlotAddresses),
                description: "Address list of combination slot.",
                resolve: context => context.Source.AvatarState.combinationSlotAddresses);
            Field<NonNullGraphType<ListGraphType<NonNullGraphType<CombinationSlotStateType>>>>(
                "combinationSlots",
                description: "Combination slots.",
                resolve: context =>
                {
                    var addresses = context.Source.AvatarState.combinationSlotAddresses;
                    return context.Source.WorldState.GetLegacyStates(addresses)
                        .OfType<Dictionary>()
                        .Select(x => new CombinationSlotState(x));
                });
            Field<NonNullGraphType<CollectionMapType>>(
                nameof(AvatarState.itemMap),
                description: "List of acquired item ID.",
                resolve: context => context.Source.AvatarState.itemMap);
            Field<NonNullGraphType<CollectionMapType>>(
                nameof(AvatarState.eventMap),
                description: "List of quest event ID.",
                resolve: context => context.Source.AvatarState.eventMap);
            Field<NonNullGraphType<CollectionMapType>>(
                nameof(AvatarState.monsterMap),
                description: "List of defeated monster ID.",
                resolve: context => context.Source.AvatarState.monsterMap);
            Field<NonNullGraphType<CollectionMapType>>(
                nameof(AvatarState.stageMap),
                description: "List of cleared stage ID.",
                resolve: context => context.Source.AvatarState.stageMap);

            Field<NonNullGraphType<QuestListType>>(
                nameof(AvatarState.questList),
                description: "List of quest.",
                resolve: context => context.Source.AvatarState.questList);
            Field<NonNullGraphType<MailBoxType>>(
                nameof(AvatarState.mailBox),
                description: "List of mail.",
                resolve: context => context.Source.AvatarState.mailBox);
            Field<NonNullGraphType<WorldInformationType>>(
                nameof(AvatarState.worldInformation),
                description: "World & Stage information.",
                resolve: context => context.Source.AvatarState.worldInformation);
        }
    }
}
