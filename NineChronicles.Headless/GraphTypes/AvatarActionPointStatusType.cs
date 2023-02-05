using GraphQL.Types;
using Libplanet.Explorer.GraphTypes;
using NineChronicles.Headless.GraphTypes.States.Models.Item;

namespace NineChronicles.Headless.GraphTypes
{
    internal class AvatarActionPointStatusType : ObjectGraphType<AvatarActionPointStatus>
    {
        public AvatarActionPointStatusType()
        {
            Field<NonNullGraphType<LongGraphType>>(
                   nameof(AvatarActionPointStatus.blockIndex),
                   resolve: context => context.Source.blockIndex
                );
            Field<NonNullGraphType<IntGraphType>>(
                nameof(AvatarActionPointStatus.actionPoint),
                resolve: context => context.Source.actionPoint
                );
            Field<NonNullGraphType<LongGraphType>>(
               nameof(AvatarActionPointStatus.experience),
               resolve: context => context.Source.experience
               );
            Field<NonNullGraphType<IntGraphType>>(
               nameof(AvatarActionPointStatus.level),
               resolve: context => context.Source.level
               );
            Field<NonNullGraphType<AddressType>>(
                nameof(AvatarActionPointStatus.avatarAddress),
                resolve: context => context.Source.avatarAddress
                );
            Field<NonNullGraphType<InventoryType>>(
                nameof(AvatarActionPointStatus.inventory),
                description: "Avatar inventory.",
                resolve: context => context.Source.inventory);
        }
    }
}
