using GraphQL.Types;
using Libplanet.Explorer.GraphTypes;
using Nekoyume.Model.State;
using NineChronicles.Headless.GraphTypes.States.Models.Table;

namespace NineChronicles.Headless.GraphTypes.States
{
    public class MonsterCollectionResultType : ObjectGraphType<MonsterCollectionResult>
    {
        public MonsterCollectionResultType()
        {
            Field<NonNullGraphType<AddressType>>(nameof(MonsterCollectionResult.avatarAddress))
                .Resolve(context => context.Source.avatarAddress);

            Field<NonNullGraphType<ListGraphType<MonsterCollectionRewardInfoType>>>(nameof(MonsterCollectionResult.rewards))
                .Resolve(context => context.Source.rewards);
        }
    }
}
