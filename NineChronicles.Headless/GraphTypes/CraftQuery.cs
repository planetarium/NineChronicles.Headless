using System.Collections.Generic;
using System.Linq;
using Bencodex;
using GraphQL;
using GraphQL.Types;
using Libplanet.Crypto;
using Libplanet.Explorer.GraphTypes;
using Nekoyume.Action;
using NineChronicles.Headless.GraphTypes.Input;

namespace NineChronicles.Headless.GraphTypes
{
    public class CraftQuery : ObjectGraphType
    {
        private static readonly Codec Codec = new Codec();
        internal StandaloneContext standaloneContext { get; set; }

        public CraftQuery() : this(new StandaloneContext())
        {
        }

        public CraftQuery(StandaloneContext standaloneContext)
        {
            this.standaloneContext = standaloneContext;

            Field<NonNullGraphType<ByteStringType>>(
                "eventConsumableItemCrafts",
                arguments: new QueryArguments(
                    new QueryArgument<NonNullGraphType<AddressType>>
                    {
                        Name = "avatarAddress",
                        Description = "Avatar address to craft event item",
                    },
                    new QueryArgument<NonNullGraphType<IntGraphType>>
                    {
                        Name = "eventScheduleId",
                        Description = "The ID of event schedule",
                    },
                    new QueryArgument<NonNullGraphType<IntGraphType>>
                    {
                        Name = "eventConsumableItemRecipeId",
                        Description = "Recipe ID of event item to craft",
                    },
                    new QueryArgument<NonNullGraphType<IntGraphType>>
                    {
                        Name = "slotIndex",
                        Description = "Target slot index to craft item",
                    }),
                resolve: context =>
                {
                    var avatarAddress = context.GetArgument<Address>("avatarAddress");
                    var eventScheduleId = context.GetArgument<int>("eventScheduleId");
                    var eventConsumableItemRecipeId = context.GetArgument<int>("eventConsumableItemRecipeId");
                    var slotIndex = context.GetArgument<int>("slotIndex");

                    ActionBase action = new EventConsumableItemCrafts
                    {
                        AvatarAddress = avatarAddress,
                        EventScheduleId = eventScheduleId,
                        EventConsumableItemRecipeId = eventConsumableItemRecipeId,
                        SlotIndex = slotIndex,
                    };

                    return Encode(context, action);
                }
            );

            Field<NonNullGraphType<ByteStringType>>(
                "eventMaterialItemCrafts",
                arguments: new QueryArguments(
                    new QueryArgument<NonNullGraphType<AddressType>>
                    {
                        Name = "avatarAddress",
                        Description = "Avatar address to craft item"
                    },
                    new QueryArgument<NonNullGraphType<IntGraphType>>
                    {
                        Name = "eventScheduleId",
                        Description = "The ID of event schedule",
                    },
                    new QueryArgument<NonNullGraphType<IntGraphType>>
                    {
                        Name = "eventMaterialItemRecipeId",
                        Description = "Recipe ID of event item to craft",
                    },
                    new QueryArgument<NonNullGraphType<ListGraphType<NonNullGraphType<MaterialsToUseInputType>>>>
                    {
                        Name = "materialsToUse",
                        Description = "Materials to be used to craft"
                    }
                ),
                resolve: context =>
                {
                    var avatarAddress = context.GetArgument<Address>("avatarAddress");
                    var eventScheduleId = context.GetArgument<int>("eventScheduleId");
                    var eventMaterialItemRecipeId = context.GetArgument<int>("eventMaterialItemRecipeId");
                    var materialsToUseList = context.GetArgument<List<(int, int)>>("materialsToUse");
                    var materialsToUse = materialsToUseList.Aggregate(new Dictionary<int, int>(),
                        (dict, material) =>
                        {
                            dict.TryAdd(material.Item1, material.Item2);
                            return dict;
                        }
                    );
                    ActionBase action = new EventMaterialItemCrafts
                    {
                        AvatarAddress = avatarAddress,
                        EventScheduleId = eventScheduleId,
                        EventMaterialItemRecipeId = eventMaterialItemRecipeId,
                        MaterialsToUse = materialsToUse,
                    };
                    return Encode(context, action);
                }
            );
        }

        internal virtual byte[] Encode(IResolveFieldContext context, ActionBase action)
        {
            return Codec.Encode(action.PlainValue);
        }
    }
}
