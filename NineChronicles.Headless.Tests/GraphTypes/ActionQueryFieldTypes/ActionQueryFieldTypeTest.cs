using System;
using Bencodex;
using GraphQL;
using GraphQL.Types;
using Libplanet.Explorer.GraphTypes;
using Xunit;
using NCAction = Libplanet.Action.PolymorphicAction<Nekoyume.Action.ActionBase>;

namespace NineChronicles.Headless.Tests.GraphTypes.ActionQueryFieldTypes
{
    public class ActionQueryFieldTypeTest
    {
        private static readonly Codec Codec = new();

        [Theory]
        [InlineData("name", null, false, null)]
        [InlineData("name", "description", false, null)]
        [InlineData("name", "description", true, "deprecationReason")]
        public void Constructor(
            string name,
            string description,
            bool newArguments,
            string deprecationReason)
        {
            var arguments = newArguments ? new QueryArguments() : null;
            var ft = new ActionQueryFieldTypeInheritor(
                name,
                description,
                arguments,
                Encode,
                deprecationReason);
            Assert.Equal(name, ft.Name);
            Assert.Equal(description, ft.Description);
            Assert.Equal(arguments, ft.Arguments);
            Assert.Equal(deprecationReason, ft.DeprecationReason);
            Assert.Equal(typeof(NonNullGraphType<ByteStringType>), ft.Type);
        }

        [Theory]
        [InlineData(null, null, true)]
        [InlineData("name", null, false)]
        [InlineData("name", "description", false)]
        [InlineData("name", "description", true)]
        public void Constructor_Throw_ArgumentNullException(
            string name,
            string description,
            bool newArguments)
        {
            Assert.Throws<ArgumentNullException>(() => new ActionQueryFieldTypeInheritor(
                name,
                description,
                newArguments ? new QueryArguments() : null,
                null));
        }

        [Fact]
        public void Resolve_Throw_NotImplementedException()
        {
            var ft = new ActionQueryFieldTypeInheritor(
                "name",
                "description",
                new QueryArguments(),
                Encode);
            Assert.Throws<NotImplementedException>(() => ft.Resolve(null));
        }

        public static byte[] Encode(
            IResolveFieldContext context,
            NCAction action) =>
            Codec.Encode(action.PlainValue);

        public static NCAction Decode(byte[] bytes)
        {
#pragma warning disable CS0612
            var action = new NCAction();
#pragma warning restore CS0612
            action.LoadPlainValue(Codec.Decode(bytes));
            return action;
        }
    }
}
