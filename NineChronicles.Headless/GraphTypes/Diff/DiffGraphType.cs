using GraphQL.Types;

namespace NineChronicles.Headless.GraphTypes.Diff;

public class DiffGraphType : UnionGraphType
{
    public DiffGraphType()
    {
        Type<RootStateDiffType>();
        Type<StateDiffType>();
    }
}
