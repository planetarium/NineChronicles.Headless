using GraphQL.Types;

namespace NineChronicles.Headless.GraphTypes.States.Models.WorldOpen
{
    public class WorldOpenType : ObjectGraphType<WorldOpenResult>
    {
        public WorldOpenType()
        {
            Name = "WorldOpen";
            Field<NonNullGraphType<LongGraphType>>(
                "evaluatedBlockIndex",
                description: "Block the judgement is made at (tip + 1).",
                resolve: context => context.Source.EvaluatedBlockIndex);
            Field<NonNullGraphType<ListGraphType<NonNullGraphType<StringGraphType>>>>(
                "errors",
                description: "Draft validation and identity errors. Always empty without a draft.",
                resolve: context => context.Source.Errors);
            Field<NonNullGraphType<ListGraphType<NonNullGraphType<WorldOpenWorldType>>>>(
                "worlds",
                resolve: context => context.Source.Worlds);
            Field<NonNullGraphType<ListGraphType<NonNullGraphType<WorldOpenEntryType>>>>(
                "opens",
                resolve: context => context.Source.Opens);
        }
    }
}
