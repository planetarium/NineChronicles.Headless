using System;
using System.Collections.Generic;
using GraphQL;
using GraphQL.Types;
using Libplanet.Crypto;
using Libplanet.Explorer.GraphTypes;
using Nekoyume.Action;
using Nekoyume.Model;

namespace NineChronicles.Headless.GraphTypes;

public partial class ActionQuery
{
    private void RegisterHackAndSlash()
    {
        Field<NonNullGraphType<ByteStringType>>("hackAndSlash",
            arguments: new QueryArguments(
                new QueryArgument<NonNullGraphType<AddressType>>
                {
                    Name = "avatarAddress",
                    Description = "Avatar address."
                },
                new QueryArgument<NonNullGraphType<IntGraphType>>
                {
                    Name = "worldId",
                    Description = "World ID containing the stage ID."
                },
                new QueryArgument<NonNullGraphType<IntGraphType>>
                {
                    Name = "stageId",
                    Description = "Stage ID."
                },
                new QueryArgument<ListGraphType<GuidGraphType>>
                {
                    Name = "costumeIds",
                    Description = "List of costume id for equip."
                },
                new QueryArgument<ListGraphType<GuidGraphType>>
                {
                    Name = "equipmentIds",
                    Description = "List of equipment id for equip."
                },
                new QueryArgument<ListGraphType<GuidGraphType>>
                {
                    Name = "consumableIds",
                    Description = "List of consumable id for use."
                },
                new QueryArgument<ListGraphType<NonNullGraphType<RuneSlotInfoInputType>>>
                {
                    Name = "runeSlotInfos",
                    DefaultValue = new List<RuneSlotInfo>(),
                    Description = "List of rune slot info for equip."
                },
                new QueryArgument<IntGraphType>
                {
                    Name = "stageBuffId",
                    Description = "Buff ID for this stage"
                }
            ),
            resolve: context =>
            {
                try
                {
                    Address avatarAddress = context.GetArgument<Address>("avatarAddress");
                    int worldId = context.GetArgument<int>("worldId");
                    int stageId = context.GetArgument<int>("stageId");
                    List<Guid> costumeIds = context.GetArgument<List<Guid>?>("costumeIds") ?? new List<Guid>();
                    List<Guid> equipmentIds = context.GetArgument<List<Guid>?>("equipmentIds") ?? new List<Guid>();
                    List<Guid> consumableIds =
                        context.GetArgument<List<Guid>?>("consumableIds") ?? new List<Guid>();
                    List<RuneSlotInfo> runeSlotInfos =
                        context.GetArgument<List<RuneSlotInfo>?>("runeSlotInfos") ?? new List<RuneSlotInfo>();
                    int? stageBuffId = context.GetArgument<int?>("stageBuffId");

                    if (!(StandaloneContext.BlockChain is { } chain))
                    {
                        throw new InvalidOperationException("BlockChain not found in the context");
                    }

                    ActionBase action = new HackAndSlash
                    {
                        AvatarAddress = avatarAddress,
                        WorldId = worldId,
                        StageId = stageId,
                        Costumes = costumeIds,
                        Equipments = equipmentIds,
                        Foods = consumableIds,
                        RuneInfos = runeSlotInfos,
                        StageBuffId = stageBuffId
                    };
                    return Encode(context, action);
                }
                catch (Exception e)
                {
                    var msg = $"Unexpected exception occurred during {typeof(HackAndSlash)}: {e}";
                    context.Errors.Add(new ExecutionError(msg, e));
                    throw;
                }
            }
        );
    }
}
