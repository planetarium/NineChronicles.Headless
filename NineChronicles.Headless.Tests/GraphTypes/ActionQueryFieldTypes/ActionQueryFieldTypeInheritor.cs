using System;
using GraphQL;
using GraphQL.Types;
using NineChronicles.Headless.GraphTypes.ActionQueryFieldTypes;
using NCAction = Libplanet.Action.PolymorphicAction<Nekoyume.Action.ActionBase>;

namespace NineChronicles.Headless.Tests.GraphTypes.ActionQueryFieldTypes
{
    public class ActionQueryFieldTypeInheritor : ActionQueryFieldType
    {
        public ActionQueryFieldTypeInheritor(
            string name,
            string description,
            QueryArguments arguments,
            Func<IResolveFieldContext, NCAction, byte[]> encoder,
            string? deprecationReason = null) :
            base(name, description, arguments, encoder, deprecationReason)
        {
        }

        public override object? Resolve(IResolveFieldContext context)
        {
            throw new NotImplementedException();
        }
    }
}
