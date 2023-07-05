#nullable enable
using System;
using System.Collections.Generic;
using System.Linq;
using Bencodex.Types;
using GraphQL;
using GraphQL.Types;
using Libplanet;
using Libplanet.Explorer.GraphTypes;
using Nekoyume.Action;
using Nekoyume.Arena;
using Nekoyume.Extensions;
using Nekoyume.Model;
using Nekoyume.Model.EnumType;
using Nekoyume.Model.State;
using Nekoyume.TableData;
using NineChronicles.Headless.GraphTypes.States;


namespace NineChronicles.Headless.GraphTypes
{
    public class SimultionQuery : ObjectGraphType<StateContext>
    {
        public SimultionQuery()
        {
            Name = "SimultionQuery";
            
            Field<NonNullGraphType<ArenaSimulationStateType>>(
                name: "arenaPercentageCalculator",
                description: "State for championShip arena.",
                arguments: new QueryArguments(
                    new QueryArgument<NonNullGraphType<AddressType>>
                    {
                        Name = "avatarAddress",
                        Description = "Avatar address."
                    },
                    new QueryArgument<NonNullGraphType<AddressType>>
                    {
                        Name = "enemyAvatarAddress",
                        Description = "Enemy Avatar address."
                    },
                    new QueryArgument<NonNullGraphType<IntGraphType>>
                    {
                        Name = "simulationCount",
                        Description = "Amount of simulations, between 1 and 1000"
                    }
                ),
                resolve: context =>
                {
                    Address myAvatarAddress = context.GetArgument<Address>("avatarAddress");
                    Address enemyAvatarAddress = context.GetArgument<Address>("enemyAvatarAddress");
                    int simulationCount = context.GetArgument<int>("simulationCount");

                    var sheets = context.Source.GetSheets(sheetTypes: new[]
                    {
                        typeof(ArenaSheet),
                        typeof(CostumeStatSheet),
                        typeof(ItemRequirementSheet),
                        typeof(EquipmentItemRecipeSheet),
                        typeof(EquipmentItemSubRecipeSheetV2),
                        typeof(EquipmentItemOptionSheet),
                        typeof(RuneListSheet),
                        typeof(MaterialItemSheet),
                        typeof(SkillSheet),
                        typeof(SkillBuffSheet),
                        typeof(StatBuffSheet),
                        typeof(SkillActionBuffSheet),
                        typeof(ActionBuffSheet),
                        typeof(CharacterSheet),
                        typeof(CharacterLevelSheet),
                        typeof(EquipmentItemSetEffectSheet),
                        typeof(WeeklyArenaRewardSheet),
                        typeof(RuneOptionSheet),
                    });

                    if(simulationCount < 1 || simulationCount > 1000)
                    {
                        throw new Exception("arenaPercentageCalculator - Invalid simulationCount");
                    }

                    var myAvatar = context.Source.GetAvatarStateV2(myAvatarAddress);
                    var enemyAvatar = context.Source.GetAvatarStateV2(enemyAvatarAddress);

                    //sheets
                    var arenaSheets = sheets.GetArenaSimulatorSheets();
                    var characterSheet = sheets.GetSheet<CharacterSheet>();

                    if (!characterSheet.TryGetValue(myAvatar.characterId, out var characterRow) || !characterSheet.TryGetValue(enemyAvatar.characterId, out var characterRow2))
                    {
                        throw new SheetRowNotFoundException("CharacterSheet", myAvatar.characterId);
                    }

                    //MyAvatar                
                    var myArenaAvatarStateAdr = ArenaAvatarState.DeriveAddress(myAvatarAddress);
                    var myArenaAvatarState = context.Source.GetArenaAvatarState(myArenaAvatarStateAdr, myAvatar);
                    var myAvatarEquipments = myAvatar.inventory.Equipments;
                    var myAvatarCostumes = myAvatar.inventory.Costumes;
                    List<Guid> myArenaEquipementList = myAvatarEquipments.Where(f=>myArenaAvatarState.Equipments.Contains(f.ItemId)).Select(n => n.ItemId).ToList();
                    List<Guid> myArenaCostumeList = myAvatarCostumes.Where(f=>myArenaAvatarState.Costumes.Contains(f.ItemId)).Select(n => n.ItemId).ToList();

                    var myRuneSlotStateAddress = RuneSlotState.DeriveAddress(myAvatarAddress, BattleType.Arena);
                    var myRuneSlotState = context.Source.TryGetState(myRuneSlotStateAddress, out List myRawRuneSlotState)
                        ? new RuneSlotState(myRawRuneSlotState)
                        : new RuneSlotState(BattleType.Arena);

                    var myRuneStates = new List<RuneState>();
                    var myRuneSlotInfos = myRuneSlotState.GetEquippedRuneSlotInfos();
                    foreach (var address in myRuneSlotInfos.Select(info => RuneState.DeriveAddress(myAvatarAddress, info.RuneId)))
                    {
                        if (context.Source.TryGetState(address, out List rawRuneState))
                        {
                            myRuneStates.Add(new RuneState(rawRuneState));
                        }
                    }

                    //Enemy
                    var enemyArenaAvatarStateAdr = ArenaAvatarState.DeriveAddress(enemyAvatarAddress);
                    var enemyArenaAvatarState = context.Source.GetArenaAvatarState(enemyArenaAvatarStateAdr, enemyAvatar);
                    var enemyAvatarEquipments = enemyAvatar.inventory.Equipments;
                    var enemyAvatarCostumes = enemyAvatar.inventory.Costumes;
                    List<Guid> enemyArenaEquipementList = enemyAvatarEquipments.Where(f=>enemyArenaAvatarState.Equipments.Contains(f.ItemId)).Select(n => n.ItemId).ToList();
                    List<Guid> enemyArenaCostumeList = enemyAvatarCostumes.Where(f=>enemyArenaAvatarState.Costumes.Contains(f.ItemId)).Select(n => n.ItemId).ToList();

                    var enemyRuneSlotStateAddress = RuneSlotState.DeriveAddress(enemyAvatarAddress, BattleType.Arena);
                    var enemyRuneSlotState = context.Source.TryGetState(enemyRuneSlotStateAddress, out List enemyRawRuneSlotState)
                        ? new RuneSlotState(enemyRawRuneSlotState)
                        : new RuneSlotState(BattleType.Arena);

                    var enemyRuneStates = new List<RuneState>();
                    var enemyRuneSlotInfos = enemyRuneSlotState.GetEquippedRuneSlotInfos();
                    foreach (var address in enemyRuneSlotInfos.Select(info => RuneState.DeriveAddress(enemyAvatarAddress, info.RuneId)))
                    {
                        if (context.Source.TryGetState(address, out List rawRuneState))
                        {
                            enemyRuneStates.Add(new RuneState(rawRuneState));
                        }
                    }

                    var myArenaPlayerDigest = new ArenaPlayerDigest(
                        myAvatar,
                        myArenaEquipementList,
                        myArenaCostumeList,
                        myRuneStates);

                    var enemyArenaPlayerDigest = new ArenaPlayerDigest(
                        enemyAvatar,
                        enemyArenaEquipementList,
                        enemyArenaCostumeList,
                        enemyRuneStates);

                    Random rnd  =new Random();          

                    int win = 0;
                    int loss = 0;

                    List<ArenaSimulationResult> arenaResultsList = new List<ArenaSimulationResult>();
                    ArenaSimulationState arenaSimulationState = new ArenaSimulationState();
                    arenaSimulationState.blockIndex = context.Source.BlockIndex;
                    
                    for (var i = 0; i < simulationCount; i++)
                    {
                        ArenaSimulationResult arenaResult = new ArenaSimulationResult();
                        arenaResult.seed = rnd.Next();
                        LocalRandom iRandom = new LocalRandom(arenaResult.seed);
                        var simulator = new ArenaSimulator(iRandom);
                        var log = simulator.Simulate(
                            myArenaPlayerDigest,
                            enemyArenaPlayerDigest,
                            arenaSheets);
                        if(log.Result.ToString() == "Win")
                        {
                            arenaResult.win = true;
                            win++;
                        }
                        else
                        {
                            loss++;
                            arenaResult.win = false;
                        }
                        arenaResultsList.Add(arenaResult);
                    }
                    arenaSimulationState.winPercentage = Math.Round(((decimal)win / simulationCount) * 100m, 2);
                    arenaSimulationState.result = arenaResultsList;
                    return arenaSimulationState;
                });
        }
    }
}
