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
    public class AvatarStateType : ObjectGraphType<(AvatarState avatarState, AccountStateGetter accountStateGetter)>
    {
        public AvatarStateType()
        {
            Field<NonNullGraphType<AddressType>>(
                nameof(AvatarState.address),
                description: "Address of avatar.",
                resolve: context => context.Source.avatarState.address);
            Field<NonNullGraphType<IntGraphType>>(
                nameof(AvatarState.blockIndex),
                description: "Block index at the latest executed action.",
                resolve: context => context.Source.avatarState.blockIndex);
            Field<NonNullGraphType<IntGraphType>>(
                nameof(AvatarState.characterId),
                description: "Character ID from CharacterSheet.",
                resolve: context => context.Source.avatarState.characterId);
            Field<NonNullGraphType<LongGraphType>>(
                nameof(AvatarState.dailyRewardReceivedIndex),
                description: "Block index at the DailyReward execution.",
                resolve: context => context.Source.avatarState.dailyRewardReceivedIndex);
            Field<NonNullGraphType<AddressType>>(
                nameof(AvatarState.agentAddress),
                description: "Address of agent.",
                resolve: context => context.Source.avatarState.agentAddress);
            Field<NonNullGraphType<AddressType>>(
                nameof(AvatarState.RankingMapAddress),
                description: "Address of the RankingMapState where this avatar information is recorded.",
                resolve: context => context.Source.avatarState.RankingMapAddress);
            Field<NonNullGraphType<LongGraphType>>(
                nameof(AvatarState.updatedAt),
                description: "Block index at the latest executed action.",
                resolve: context => context.Source.avatarState.updatedAt);

            Field<NonNullGraphType<StringGraphType>>(
                nameof(AvatarState.name),
                description: "Avatar name.",
                resolve: context => context.Source.avatarState.name);
            Field<NonNullGraphType<IntGraphType>>(
                nameof(AvatarState.exp),
                description: "Avatar total EXP.",
                resolve: context => context.Source.avatarState.exp);
            Field<NonNullGraphType<IntGraphType>>(
                nameof(AvatarState.level),
                description: "Avatar Level.",
                resolve: context => context.Source.avatarState.level);
            Field<NonNullGraphType<IntGraphType>>(
                nameof(AvatarState.actionPoint),
                description: "Current ActionPoint.",
                resolve: context => context.Source.avatarState.actionPoint);

            Field<NonNullGraphType<IntGraphType>>(
                nameof(AvatarState.ear),
                description: "Index of ear color.",
                resolve: context => context.Source.avatarState.ear);
            Field<NonNullGraphType<IntGraphType>>(
                nameof(AvatarState.hair),
                description: "Index of hair color.",
                resolve: context => context.Source.avatarState.hair);
            Field<NonNullGraphType<IntGraphType>>(
                nameof(AvatarState.lens),
                description: "Index of eye color.",
                resolve: context => context.Source.avatarState.lens);
            Field<NonNullGraphType<IntGraphType>>(
                nameof(AvatarState.tail),
                description: "Index of tail color.",
                resolve: context => context.Source.avatarState.tail);

            Field<NonNullGraphType<InventoryType>>(
                nameof(AvatarState.inventory),
                description: "Avatar inventory.",
                resolve: context => (context.Source.avatarState.inventory, context.Source.accountStateGetter));
            Field<NonNullGraphType<ListGraphType<NonNullGraphType<AddressType>>>>(
                nameof(AvatarState.combinationSlotAddresses),
                description: "Address list of combination slot.",
                resolve: context => context.Source.avatarState.combinationSlotAddresses);
            Field<NonNullGraphType<CollectionMapType>>(
                nameof(AvatarState.itemMap),
                description: "List of acquired item ID.",
                resolve: context => context.Source.avatarState.itemMap);
            Field<NonNullGraphType<CollectionMapType>>(
                nameof(AvatarState.eventMap),
                description: "List of quest event ID.",
                resolve: context => context.Source.avatarState.eventMap);
            Field<NonNullGraphType<CollectionMapType>>(
                nameof(AvatarState.monsterMap),
                description: "List of defeated monster ID.",
                resolve: context => context.Source.avatarState.monsterMap);
            Field<NonNullGraphType<CollectionMapType>>(
                nameof(AvatarState.stageMap),
                description: "List of cleared stage ID.",
                resolve: context => context.Source.avatarState.stageMap);

            Field<NonNullGraphType<QuestListType>>(
                nameof(AvatarState.questList),
                description: "List of quest.",
                resolve: context => context.Source.avatarState.questList);
            Field<NonNullGraphType<MailBoxType>>(
                nameof(AvatarState.mailBox),
                description: "List of mail.",
                resolve: context => context.Source.avatarState.mailBox);
            Field<NonNullGraphType<WorldInformationType>>(
                nameof(AvatarState.worldInformation),
                description: "World & Stage information.",
                resolve: context => context.Source.avatarState.worldInformation);
        }
    }
}
