using System;
using GraphQL;
using GraphQL.Resolvers;
using GraphQL.Types;
using Libplanet.Explorer.GraphTypes;
using NCAction = Libplanet.Action.PolymorphicAction<Nekoyume.Action.ActionBase>;

namespace NineChronicles.Headless.GraphTypes.ActionQueryFieldTypes
{
    public abstract class ActionQueryFieldType : FieldType, IFieldResolver
    {
        private readonly Func<IResolveFieldContext, NCAction, byte[]> _encoder;

        protected ActionQueryFieldType(
            string name,
            string description,
            QueryArguments arguments,
            Func<IResolveFieldContext, NCAction, byte[]> encoder,
            string? deprecationReason = null)
        {
            _encoder = encoder ?? throw new ArgumentNullException(
                nameof(encoder),
                "Encoder is required.");

            Name = name ?? throw new ArgumentNullException(
                nameof(name),
                "Name is required.");
            Description = description;
            Arguments = arguments;
            Resolver = this;
            DeprecationReason = deprecationReason;
            Type = typeof(NonNullGraphType<ByteStringType>);
        }

        public abstract object? Resolve(IResolveFieldContext context);

        protected byte[] Encode(IResolveFieldContext context, NCAction action) =>
            _encoder(context, action);
    }
}
