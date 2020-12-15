using GraphQL.Types;
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
                resolve: context => context.Source.address);
            Field<NonNullGraphType<IntGraphType>>(
                nameof(AvatarState.blockIndex),
                resolve: context => context.Source.blockIndex);
            Field<NonNullGraphType<IntGraphType>>(
                nameof(AvatarState.characterId),
                resolve: context => context.Source.characterId);
            Field<NonNullGraphType<LongGraphType>>(
                nameof(AvatarState.dailyRewardReceivedIndex),
                resolve: context => context.Source.dailyRewardReceivedIndex);
            Field<NonNullGraphType<AddressType>>(
                nameof(AvatarState.agentAddress),
                resolve: context => context.Source.agentAddress);
            Field<NonNullGraphType<AddressType>>(
                nameof(AvatarState.RankingMapAddress),
                resolve: context => context.Source.RankingMapAddress);
            Field<NonNullGraphType<LongGraphType>>(
                nameof(AvatarState.updatedAt),
                resolve: context => context.Source.updatedAt);

            Field<NonNullGraphType<StringGraphType>>(
                nameof(AvatarState.name),
                resolve: context => context.Source.name);
            Field<NonNullGraphType<IntGraphType>>(
                nameof(AvatarState.exp),
                resolve: context => context.Source.exp);
            Field<NonNullGraphType<IntGraphType>>(
                nameof(AvatarState.level),
                resolve: context => context.Source.level);
            Field<NonNullGraphType<IntGraphType>>(
                nameof(AvatarState.actionPoint),
                resolve: context => context.Source.actionPoint);

            Field<NonNullGraphType<IntGraphType>>(
                nameof(AvatarState.ear),
                resolve: context => context.Source.ear);
            Field<NonNullGraphType<IntGraphType>>(
                nameof(AvatarState.hair),
                resolve: context => context.Source.hair);
            Field<NonNullGraphType<IntGraphType>>(
                nameof(AvatarState.lens),
                resolve: context => context.Source.lens);
            Field<NonNullGraphType<IntGraphType>>(
                nameof(AvatarState.tail),
                resolve: context => context.Source.tail);

            Field<NonNullGraphType<InventoryType>>(
                nameof(AvatarState.inventory),
                resolve: context => context.Source.inventory);
            Field<NonNullGraphType<ListGraphType<NonNullGraphType<AddressType>>>>(
                nameof(AvatarState.combinationSlotAddresses),
                resolve: context => context.Source.combinationSlotAddresses);
            Field<NonNullGraphType<CollectionMapType>>(
                nameof(AvatarState.itemMap),
                resolve: context => context.Source.itemMap);
            Field<NonNullGraphType<CollectionMapType>>(
                nameof(AvatarState.eventMap),
                resolve: context => context.Source.eventMap);
            Field<NonNullGraphType<CollectionMapType>>(
                nameof(AvatarState.monsterMap),
                resolve: context => context.Source.monsterMap);
            Field<NonNullGraphType<CollectionMapType>>(
                nameof(AvatarState.stageMap),
                resolve: context => context.Source.stageMap);

            Field<NonNullGraphType<QuestListType>>(
                nameof(AvatarState.questList),
                resolve: context => context.Source.questList);
            Field<NonNullGraphType<MailBoxType>>(
                nameof(AvatarState.mailBox),
                resolve: context => context.Source.mailBox);
            Field<NonNullGraphType<WorldInformationType>>(
                nameof(AvatarState.worldInformation),
                resolve: context => context.Source.worldInformation);
        }
    }
}
