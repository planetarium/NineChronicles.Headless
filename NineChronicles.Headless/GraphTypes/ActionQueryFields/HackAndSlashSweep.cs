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
    private void RegisterHackAndSlashSweep()
    {
        Field<NonNullGraphType<ByteStringType>>(
            "hackAndSlashSweep",
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
                new QueryArgument<ListGraphType<NonNullGraphType<RuneSlotInfoInputType>>>
                {
                    Name = "runeSlotInfos",
                    DefaultValue = new List<RuneSlotInfo>(),
                    Description = "List of rune slot info for equip."
                },
                new QueryArgument<NonNullGraphType<IntGraphType>>
                {
                    Name = "actionPoint",
                    Description = "Action point usage to sweep"
                },
                new QueryArgument<IntGraphType>
                {
                    Name = "apStoneCount",
                    Description = "AP stone usage to sweep"
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
                    List<RuneSlotInfo> runeSlotInfos =
                        context.GetArgument<List<RuneSlotInfo>?>("runeSlotInfos") ?? new List<RuneSlotInfo>();
                    int actionPoint = context.GetArgument<int>("actionPoint");
                    int apStoneCount = context.GetArgument<int?>("apStoneCount") ?? 0;

                    ActionBase action = new HackAndSlashSweep
                    {
                        avatarAddress = avatarAddress,
                        worldId = worldId,
                        stageId = stageId,
                        costumes = costumeIds,
                        equipments = equipmentIds,
                        runeInfos = runeSlotInfos,
                        actionPoint = actionPoint,
                        apStoneCount = apStoneCount,
                    };
                    return Encode(context, action);
                }
                catch (Exception e)
                {
                    var msg = $"Unexpected exception occurred during {typeof(HackAndSlashSweep)}: {e}";
                    context.Errors.Add(new ExecutionError(msg, e));
                    throw;
                }
            }
        );
    }
}
