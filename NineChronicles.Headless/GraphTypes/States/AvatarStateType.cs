using System;
using System.Linq;
using Bencodex.Types;
using GraphQL.Types;
using Libplanet.Action;
using Libplanet.Explorer.GraphTypes;
using Nekoyume.Model.State;
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
            public AvatarStateContext(AvatarState avatarState, AccountStateGetter accountStateGetter, AccountBalanceGetter accountBalanceGetter, long blockIndex)
                : base(accountStateGetter, accountBalanceGetter, blockIndex)
            {
                AvatarState = avatarState;
            }

            public AvatarState AvatarState { get; }
        }

        public AvatarStateType()
        {
            Field<NonNullGraphType<AddressType>>(nameof(AvatarState.address))
                .Description("Address of avatar.")
                .Resolve(context => context.Source.AvatarState.address);
            Field<NonNullGraphType<IntGraphType>>(nameof(AvatarState.blockIndex))
                .Description("Block index at the latest executed action.")
                .Resolve(context => context.Source.AvatarState.blockIndex);
            Field<NonNullGraphType<IntGraphType>>(nameof(AvatarState.characterId))
                .Description("Character ID from CharacterSheet.")
                .Resolve(context => context.Source.AvatarState.characterId);
            Field<NonNullGraphType<LongGraphType>>(nameof(AvatarState.dailyRewardReceivedIndex))
                .Description("Block index at the DailyReward execution.")
                .Resolve(context => context.Source.AvatarState.dailyRewardReceivedIndex);
            Field<NonNullGraphType<AddressType>>(nameof(AvatarState.agentAddress))
                .Description("Address of agent.")
                .Resolve(context => context.Source.AvatarState.agentAddress);
            Field<NonNullGraphType<IntGraphType>>("index")
                .Description("The index of this avatar state among its agent's avatar addresses.")
                .Resolve(context =>
                {
                    if (!(context.Source.GetState(context.Source.AvatarState.agentAddress) is Dictionary dictionary))
                    {
                        throw new InvalidOperationException();
                    }

                    var agentState = new AgentState(dictionary);
                    return agentState.avatarAddresses
                        .First(x => x.Value.Equals(context.Source.AvatarState.address))
                        .Key;
                });
            Field<NonNullGraphType<LongGraphType>>(nameof(AvatarState.updatedAt))
                .Description("Block index at the latest executed action.")
                .Resolve(context => context.Source.AvatarState.updatedAt);

            Field<NonNullGraphType<StringGraphType>>(nameof(AvatarState.name))
                .Description("Avatar name.")
                .Resolve(context => context.Source.AvatarState.name);
            Field<NonNullGraphType<IntGraphType>>(nameof(AvatarState.exp))
                .Description("Avatar total EXP.")
                .Resolve(context => context.Source.AvatarState.exp);
            Field<NonNullGraphType<IntGraphType>>(nameof(AvatarState.level))
                .Description("Avatar Level.")
                .Resolve(context => context.Source.AvatarState.level);
            Field<NonNullGraphType<IntGraphType>>(nameof(AvatarState.actionPoint))
                .Description("Current ActionPoint.")
                .Resolve(context => context.Source.AvatarState.actionPoint);
            Field<NonNullGraphType<IntGraphType>>(nameof(AvatarState.ear))
                .Description("Index of ear color.")
                .Resolve(context => context.Source.AvatarState.ear);
            Field<NonNullGraphType<IntGraphType>>(nameof(AvatarState.hair))
                .Description("Index of hair color.")
                .Resolve(context => context.Source.AvatarState.hair);
            Field<NonNullGraphType<IntGraphType>>(nameof(AvatarState.lens))
                .Description("Index of eye color.")
                .Resolve(context => context.Source.AvatarState.lens);
            Field<NonNullGraphType<IntGraphType>>(nameof(AvatarState.tail))
                .Description("Index of tail color.")
                .Resolve(context => context.Source.AvatarState.tail);

            Field<NonNullGraphType<InventoryType>>(nameof(AvatarState.inventory))
                .Description("Avatar inventory.")
                .Resolve(context => context.Source.AvatarState.inventory);
            Field<NonNullGraphType<ListGraphType<NonNullGraphType<AddressType>>>>(
                nameof(AvatarState.combinationSlotAddresses))
                .Description("Address list of combination slot.")
                .Resolve(context => context.Source.AvatarState.combinationSlotAddresses);
            Field<NonNullGraphType<CollectionMapType>>(nameof(AvatarState.itemMap))
                .Description("List of acquired item ID.")
                .Resolve(context => context.Source.AvatarState.itemMap);
            Field<NonNullGraphType<CollectionMapType>>(nameof(AvatarState.eventMap))
                .Description("List of quest event ID.")
                .Resolve(context => context.Source.AvatarState.eventMap);
            Field<NonNullGraphType<CollectionMapType>>(nameof(AvatarState.monsterMap))
                .Description("List of defeated monster ID.")
                .Resolve(context => context.Source.AvatarState.monsterMap);
            Field<NonNullGraphType<CollectionMapType>>(nameof(AvatarState.stageMap))
                .Description("List of cleared stage ID.")
                .Resolve(context => context.Source.AvatarState.stageMap);

            Field<NonNullGraphType<QuestListType>>(nameof(AvatarState.questList))
                .Description("List of quest.")
                .Resolve(context => context.Source.AvatarState.questList);
            Field<NonNullGraphType<MailBoxType>>(nameof(AvatarState.mailBox))
                .Description("List of mail.")
                .Resolve(context => context.Source.AvatarState.mailBox);
            Field<NonNullGraphType<WorldInformationType>>(nameof(AvatarState.worldInformation))
                .Description("World & Stage information.")
                .Resolve(context => context.Source.AvatarState.worldInformation);
        }
    }
}
