using GraphQL.Types;

namespace NineChronicles.Headless.GraphTypes.Diff;

public class RootStateDiffType : ObjectGraphType<RootStateDiffType.Value>
{
    public class Value : IDiffType
    {
        public string Path { get; }
        public StateDiffType.Value[] Diffs { get; }

        public Value(string path, StateDiffType.Value[] diffs)
        {
            Path = path;
            Diffs = diffs;
        }
    }

    public RootStateDiffType()
    {
        Name = "RootStateDiff";

        Field<NonNullGraphType<StringGraphType>>(
            "Path",
            description: "The path to the root state difference."
        );

        Field<NonNullGraphType<ListGraphType<NonNullGraphType<StateDiffType>>>>(
            "Diffs",
            description: "List of state differences under this root."
        );
    }
}
