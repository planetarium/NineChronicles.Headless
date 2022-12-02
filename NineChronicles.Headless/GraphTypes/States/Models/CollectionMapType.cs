using System.Linq;
using GraphQL.Types;
using Nekoyume.Model;

namespace NineChronicles.Headless.GraphTypes.States.Models
{
    public class CollectionMapType : ObjectGraphType<CollectionMap>
    {
        public CollectionMapType()
        {
            Field<NonNullGraphType<IntGraphType>>(nameof(CollectionMap.Count));
            Field<NonNullGraphType<ListGraphType<NonNullGraphType<ListGraphType<IntGraphType>>>>>(
                "pairs")
                .Resolve(context => context.Source.Keys.Select(k => new[] { k, context.Source[k], }));
        }
    }
}
