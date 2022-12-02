using System.Linq;
using GraphQL;
using GraphQL.Types;
using Nekoyume.Model.State;

namespace NineChronicles.Headless.GraphTypes.States
{
    public class StakeAchievementsType : ObjectGraphType<StakeState.StakeAchievements>
    {
        public StakeAchievementsType()
        {
            Field<NonNullGraphType<IntGraphType>>("achievementsByLevel")
                .Description("The address of current state.")
                .Argument<int>("level", false)
                .Resolve(context =>
                    Enumerable.Range(0, int.MaxValue)
                        .SkipWhile(x => context.Source.Check(context.GetArgument<int>("level"), x)).First() - 1);
        }
    }
}
