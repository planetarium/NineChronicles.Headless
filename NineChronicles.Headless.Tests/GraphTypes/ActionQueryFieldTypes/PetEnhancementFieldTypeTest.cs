using System;
using System.Collections.Generic;
using GraphQL;
using GraphQL.Execution;
using Libplanet;
using Libplanet.Crypto;
using Nekoyume.Action;
using NineChronicles.Headless.GraphTypes.ActionQueryFieldTypes;
using Xunit;

namespace NineChronicles.Headless.Tests.GraphTypes.ActionQueryFieldTypes
{
    public class PetEnhancementFieldTypeTest
    {
        [Fact]
        public void Constructor()
        {
            var ft = new PetEnhancementFieldType(ActionQueryFieldTypeTest.Encode);
        }

        [Theory]
        [InlineData(0, 1)]
        [InlineData(int.MaxValue, int.MaxValue)]
        public void Resolve(int petId, int targetLevel)
        {
            var avatarAddr = new PrivateKey().ToAddress();
            var context = new ResolveFieldContext
            {
                Arguments = new Dictionary<string, ArgumentValue>
                {
                    {
                        "avatarAddress",
                        new ArgumentValue(avatarAddr, ArgumentSource.Literal)
                    },
                    {
                        "petId",
                        new ArgumentValue(petId, ArgumentSource.Literal)
                    },
                    {
                        "targetLevel",
                        new ArgumentValue(targetLevel, ArgumentSource.Literal)
                    },
                },
                Errors = new ExecutionErrors(),
                Extensions = new Dictionary<string, object?>(),
            };
            var ft = new PetEnhancementFieldType(ActionQueryFieldTypeTest.Encode);
            var resolve = ft.Resolve(context);
            var bytes = Assert.IsType<byte[]>(resolve);
            var ncAction = ActionQueryFieldTypeTest.Decode(bytes);
            var petEnhancement = Assert.IsType<PetEnhancement>(ncAction.InnerAction);
            Assert.Equal(avatarAddr, petEnhancement.AvatarAddress);
            Assert.Equal(petId, petEnhancement.PetId);
            Assert.Equal(targetLevel, petEnhancement.TargetLevel);
        }

        [Theory]
        [InlineData(null, 0, 1)]
        [InlineData("0x0000000000000000000000000000000000000000", int.MinValue, 1)]
        [InlineData("0x0000000000000000000000000000000000000000", -1, 1)]
        [InlineData("0x0000000000000000000000000000000000000000", 0, int.MinValue)]
        [InlineData("0x0000000000000000000000000000000000000000", 0, 0)]
        public void Resolve_Throw_ExecutionError(
            string? avatarAddressHex,
            int? petId,
            int? targetLevel)
        {
            var arguments = new Dictionary<string, ArgumentValue>();
            if (avatarAddressHex != null)
            {
                arguments.Add(
                    "avatarAddress",
                    new ArgumentValue(
                        new Address(avatarAddressHex),
                        ArgumentSource.Literal));
            }

            if (petId is { } id)
            {
                arguments.Add(
                    "petId",
                    new ArgumentValue(id, ArgumentSource.Literal));
            }

            if (targetLevel is { } level)
            {
                arguments.Add(
                    "targetLevel",
                    new ArgumentValue(level, ArgumentSource.Literal));
            }

            var context = new ResolveFieldContext
            {
                Arguments = arguments,
                Errors = new ExecutionErrors(),
                Extensions = new Dictionary<string, object?>(),
            };
            var ft = new PetEnhancementFieldType(ActionQueryFieldTypeTest.Encode);
            Assert.Throws<ExecutionError>(() => ft.Resolve(context));
        }
    }
}
