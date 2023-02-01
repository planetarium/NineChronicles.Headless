using System;
using System.Collections.Generic;
using GraphQL;
using GraphQL.Execution;
using Libplanet;
using Libplanet.Crypto;
using Nekoyume.Action;
using NineChronicles.Headless.GraphTypes.ActionQueryFieldTypes;
using Xunit;

namespace NineChronicles.Headless.Tests.GraphTypes.ActionQueryFieldTypes;

public class PetEnhancementFieldTypeTest
{
    [Fact]
    public void Constructor()
    {
        var ft = new PetEnhancementFieldType(
            new StandaloneContext(),
            ActionQueryFieldTypeTest.Encode);
    }

    [Fact]
    public void Constructor_Throw_ArgumentNullException()
    {
        Assert.Throws<ArgumentNullException>(() => new PetEnhancementFieldType(
            null,
            ActionQueryFieldTypeTest.Encode));
    }

    [Theory]
    [InlineData(int.MinValue, int.MinValue)]
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
        var ft = new PetEnhancementFieldType(
            new StandaloneContext(),
            ActionQueryFieldTypeTest.Encode);
        var resolve = ft.Resolve(context);
        var bytes = Assert.IsType<byte[]>(resolve);
        var ncAction = ActionQueryFieldTypeTest.Decode(bytes);
        var petEnhancement = Assert.IsType<PetEnhancement>(ncAction.InnerAction);
        Assert.Equal(avatarAddr, petEnhancement.AvatarAddress);
        Assert.Equal(petId, petEnhancement.PetId);
        Assert.Equal(targetLevel, petEnhancement.TargetLevel);
    }
}
