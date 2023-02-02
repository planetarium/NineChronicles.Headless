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
                    new QueryArgument<NonNullGraphType<IntGraphType>>
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
            var avatarAddr = context.GetArgument<Address>("avatarAddress");
            if (avatarAddr == default)
            {
                throw new ExecutionError("Invalid avatarAddress.");
            }

            var petId = context.GetArgument<int>("petId");
            if (petId < 0)
            {
                throw new ExecutionError(
                    "Invalid petId. petId must greater than or equal to 0");
            }

            var targetLevel = context.GetArgument<int>("targetLevel");
            if (targetLevel < 1)
            {
                throw new ExecutionError(
                    "Invalid targetLevel. targetLevel must greater than or equal to 1");
            }

            var action = new PetEnhancement
            {
                AvatarAddress = avatarAddr,
                PetId = petId,
                TargetLevel = targetLevel,
            };

            return Encode(context, action);
        }
    }
}
