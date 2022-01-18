using GraphQL.Types;
using Libplanet.Explorer.GraphTypes;
using Nekoyume.Model.State;
using NineChronicles.Headless.GraphTypes.States.Models;
using NineChronicles.Headless.GraphTypes.States.Models.World;
using NineChronicles.Headless.GraphTypes.States.Models.Item;
using NineChronicles.Headless.GraphTypes.States.Models.Mail;
using NineChronicles.Headless.GraphTypes.States.Models.Quest;

namespace NineChronicles.Headless.GraphTypes.States
{
    public class AvatarStateType : ObjectGraphType<AvatarState>
    {
        public AvatarStateType()
        {
            Field<NonNullGraphType<AddressType>>(
                nameof(AvatarState.address),
                description: "Address of avatar.",
                resolve: context => context.Source.address);
            Field<NonNullGraphType<IntGraphType>>(
                nameof(AvatarState.blockIndex),
                description: "Block index at the latest executed action.",
                resolve: context => context.Source.blockIndex);
            Field<NonNullGraphType<IntGraphType>>(
                nameof(AvatarState.characterId),
                description: "Character ID from CharacterSheet.",
                resolve: context => context.Source.characterId);
            Field<NonNullGraphType<LongGraphType>>(
                nameof(AvatarState.dailyRewardReceivedIndex),
                description: "Block index at the DailyReward execution.",
                resolve: context => context.Source.dailyRewardReceivedIndex);
            Field<NonNullGraphType<AddressType>>(
                nameof(AvatarState.agentAddress),
                description: "Address of agent.",
                resolve: context => context.Source.agentAddress);
            Field<NonNullGraphType<LongGraphType>>(
                nameof(AvatarState.updatedAt),
                description: "Block index at the latest executed action.",
                resolve: context => context.Source.updatedAt);

            Field<NonNullGraphType<StringGraphType>>(
                nameof(AvatarState.name),
                description: "Avatar name.",
                resolve: context => context.Source.name);
            Field<NonNullGraphType<IntGraphType>>(
                nameof(AvatarState.exp),
                description: "Avatar total EXP.",
                resolve: context => context.Source.exp);
            Field<NonNullGraphType<IntGraphType>>(
                nameof(AvatarState.level),
                description: "Avatar Level.",
                resolve: context => context.Source.level);
            Field<NonNullGraphType<IntGraphType>>(
                nameof(AvatarState.actionPoint),
                description: "Current ActionPoint.",
                resolve: context => context.Source.actionPoint);
            Field<NonNullGraphType<IntGraphType>>(
                nameof(AvatarState.ear),
                description: "Index of ear color.",
                resolve: context => context.Source.ear);
            Field<NonNullGraphType<IntGraphType>>(
                nameof(AvatarState.hair),
                description: "Index of hair color.",
                resolve: context => context.Source.hair);
            Field<NonNullGraphType<IntGraphType>>(
                nameof(AvatarState.lens),
                description: "Index of eye color.",
                resolve: context => context.Source.lens);
            Field<NonNullGraphType<IntGraphType>>(
                nameof(AvatarState.tail),
                description: "Index of tail color.",
                resolve: context => context.Source.tail);

            Field<NonNullGraphType<InventoryType>>(
                nameof(AvatarState.inventory),
                description: "Avatar inventory.",
                resolve: context => context.Source.inventory);
            Field<NonNullGraphType<ListGraphType<NonNullGraphType<AddressType>>>>(
                nameof(AvatarState.combinationSlotAddresses),
                description: "Address list of combination slot.",
                resolve: context => context.Source.combinationSlotAddresses);
            Field<NonNullGraphType<CollectionMapType>>(
                nameof(AvatarState.itemMap),
                description: "List of acquired item ID.",
                resolve: context => context.Source.itemMap);
            Field<NonNullGraphType<CollectionMapType>>(
                nameof(AvatarState.eventMap),
                description: "List of quest event ID.",
                resolve: context => context.Source.eventMap);
            Field<NonNullGraphType<CollectionMapType>>(
                nameof(AvatarState.monsterMap),
                description: "List of defeated monster ID.",
                resolve: context => context.Source.monsterMap);
            Field<NonNullGraphType<CollectionMapType>>(
                nameof(AvatarState.stageMap),
                description: "List of cleared stage ID.",
                resolve: context => context.Source.stageMap);

            Field<NonNullGraphType<QuestListType>>(
                nameof(AvatarState.questList),
                description: "List of quest.",
                resolve: context => context.Source.questList);
            Field<NonNullGraphType<MailBoxType>>(
                nameof(AvatarState.mailBox),
                description: "List of mail.",
                resolve: context => context.Source.mailBox);
            Field<NonNullGraphType<WorldInformationType>>(
                nameof(AvatarState.worldInformation),
                description: "World & Stage information.",
                resolve: context => context.Source.worldInformation);
        }
    }
}
