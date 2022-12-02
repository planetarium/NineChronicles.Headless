using GraphQL.Types;
using Libplanet.Explorer.GraphTypes;
using Nekoyume.Model.State;

namespace NineChronicles.Headless.GraphTypes.States
{
    public class ArenaInfoType : ObjectGraphType<ArenaInfo>
    {
        public ArenaInfoType()
        {
            Field<NonNullGraphType<AddressType>>(nameof(ArenaInfo.AgentAddress))
                .Resolve(context => context.Source.AgentAddress);
            Field<NonNullGraphType<AddressType>>(nameof(ArenaInfo.AvatarAddress))
                .Resolve(context => context.Source.AvatarAddress);
            Field<NonNullGraphType<StringGraphType>>(nameof(ArenaInfo.AvatarName))
                .Resolve(context => context.Source.AvatarName);
            Field<NonNullGraphType<ArenaRecordType>>(nameof(ArenaInfo.ArenaRecord))
                .Resolve(context => context.Source.ArenaRecord);
            Field<NonNullGraphType<BooleanGraphType>>(nameof(ArenaInfo.Active))
                .Resolve(context => context.Source.Active);
            Field<NonNullGraphType<IntGraphType>>(nameof(ArenaInfo.DailyChallengeCount))
                .Resolve(context => context.Source.DailyChallengeCount);
            Field<NonNullGraphType<IntGraphType>>(nameof(ArenaInfo.Score))
                .Resolve(context => context.Source.Score);
        }
    }
}
