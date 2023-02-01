using System;
using GraphQL;
using GraphQL.Types;
using Libplanet;
using Libplanet.Explorer.GraphTypes;
using Nekoyume.Action;
using NCAction = Libplanet.Action.PolymorphicAction<Nekoyume.Action.ActionBase>;

namespace NineChronicles.Headless.GraphTypes.ActionQueryFieldTypes
{
    public class PetEnhancementFieldType : ActionQueryFieldType
    {
        public PetEnhancementFieldType(Func<IResolveFieldContext, NCAction, byte[]> encoder) :
            base(
                "petEnhancement",
                "This query returns the action `PetEnhancement`.",
                new QueryArguments(
                    new QueryArgument<NonNullGraphType<AddressType>>
                    {
                        Name = "avatarAddress",
                        Description = "Address of avatar.",
                    },
                    new QueryArgument<NonNullGraphType<StringGraphType>>
                    {
                        Name = "petId",
                        Description = "Id of pet.",
                    },
                    new QueryArgument<NonNullGraphType<IntGraphType>>
                    {
                        Name = "targetLevel",
                        Description = "Target enhancement level.",
                    }),
                encoder)
        {
        }

        public override object? Resolve(IResolveFieldContext context)
        {
            var avatarAddress = context.GetArgument<Address>("avatarAddress");
            var petId = context.GetArgument<int>("petId");
            var targetLevel = context.GetArgument<int>("targetLevel");
            var action = new PetEnhancement
            {
                AvatarAddress = avatarAddress,
                PetId = petId,
                TargetLevel = targetLevel,
            };

            return Encode(context, action);
        }
    }
}
