using System.Linq;
using GraphQL;
using GraphQL.Types;
using Nekoyume.Model.State;

namespace NineChronicles.Headless.GraphTypes.States
{
    public class StakeAchievementsType : ObjectGraphType<LegacyStakeState.StakeAchievements>
    {
        public StakeAchievementsType()
        {
            Field<NonNullGraphType<IntGraphType>>(
                "achievementsByLevel",
                description: "The address of current state.",
                arguments: new QueryArguments(new QueryArgument<NonNullGraphType<IntGraphType>>
                {
                    Name = "level",
                }),
                resolve: context =>
                    Enumerable.Range(0, int.MaxValue)
                        .SkipWhile(x => context.Source.Check(context.GetArgument<int>("level"), x)).First() - 1);
        }
    }
}
