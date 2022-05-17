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
    public class StakeStateType : ObjectGraphType<StakeState>
    {
        public StakeStateType()
        {
            Field<NonNullGraphType<AddressType>>(
                nameof(StakeState.address),
                description: "The address of current state.",
                resolve: context => context.Source.address);
            Field<NonNullGraphType<IntGraphType>>(
                nameof(StakeState.StartedBlockIndex),
                description: "The block index the user started to stake.",
                resolve: context => context.Source.StartedBlockIndex);
            Field<NonNullGraphType<IntGraphType>>(
                nameof(StakeState.ReceivedBlockIndex),
                description: "The block index the user received rewards.",
                resolve: context => context.Source.ReceivedBlockIndex);
            Field<NonNullGraphType<LongGraphType>>(
                nameof(StakeState.CancellableBlockIndex),
                description: "The block index the user can cancel the staking.",
                resolve: context => context.Source.CancellableBlockIndex);
            Field<NonNullGraphType<LongGraphType>>(
                "claimableBlockIndex",
                description: "The block index the user can claim rewards.",
                resolve: context => context.Source.ReceivedBlockIndex + StakeState.RewardInterval);
            Field<NonNullGraphType<StakeAchievementsType>>(
                nameof(StakeState.Achievements),
                description: "The staking achievements.",
                resolve: context => context.Source.Achievements);
        }
    }
}
