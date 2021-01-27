using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Globalization;
using System.Linq;
using Bencodex.Types;
using Libplanet.Action;
using Nekoyume;
using Nekoyume.Action;
using Nekoyume.Model.Item;
using Nekoyume.Model.State;
using Nekoyume.TableData;

namespace NineChronicles.Headless.Tests
{
    [Serializable]
    [ActionType("set_avatar_state")]
    public class SetAvatarState : GameAction
    {
        public override IValue PlainValue =>
            Bencodex.Types.Dictionary.Empty;

        protected override IImmutableDictionary<string, IValue> PlainValueInternal { get; }

        public override void LoadPlainValue(IValue plainValue)
        {
        }

        protected override void LoadPlainValueInternal(IImmutableDictionary<string, IValue> plainValue)
        {
        }

        public override IAccountStateDelta Execute(IActionContext context)
        {
            var states = context.PreviousStates;
            AgentState existingAgentState = states.GetAgentState(context.Signer);
            var agentState = existingAgentState ?? new AgentState(context.Signer);
            var avatarAddress = context.Signer.Derive(
                string.Format(
                    CultureInfo.InvariantCulture,
                    CreateAvatar2.DeriveFormat,
                    0
                )
            );
            if (context.Rehearsal)
            {
                states = states.SetState(context.Signer, MarkChanged);
                for (var i = 0; i < AvatarState.CombinationSlotCapacity; i++)
                {
                    var slotAddress = avatarAddress.Derive(
                        string.Format(
                            CultureInfo.InvariantCulture,
                            CombinationSlotState.DeriveFormat,
                            i
                        )
                    );
                    states = states.SetState(slotAddress, MarkChanged);
                }

                return states
                    .SetState(avatarAddress, MarkChanged)
                    .SetState(Addresses.Ranking, MarkChanged);
            }

            var avatarState = states.GetAvatarState(avatarAddress);
            if (avatarState is null)
            {
                agentState.avatarAddresses.Add(0, avatarAddress);

                var materialItemSheet = states.GetSheet<MaterialItemSheet>();
                var worldSheet = states.GetSheet<WorldSheet>();
                var worldUnlockSheet = states.GetSheet<WorldUnlockSheet>();
                var equipmentItemSheet = states.GetSheet<EquipmentItemSheet>();
                var costumeSheet = states.GetSheet<CostumeItemSheet>();

                var rankingState = states.GetRankingState();
                var rankingMapAddress = rankingState.UpdateRankingMap(avatarAddress);
                avatarState = CreateAvatar.CreateAvatarState("test", avatarAddress, context, materialItemSheet,
                    rankingMapAddress);

                for (var i = 1; i < GameConfig.RequireClearedStageLevel.ActionsInShop + 1; i++)
                {
                    avatarState.worldInformation.ClearStage(
                        1,
                        i,
                        0,
                        worldSheet,
                        worldUnlockSheet
                    );
                }

                var equipment = ItemFactory.CreateItemUsable(equipmentItemSheet.OrderedList.First(),
                    context.Random.GenerateRandomGuid(), 0);
                var costume = ItemFactory.CreateCostume(costumeSheet.OrderedList.First(),
                    context.Random.GenerateRandomGuid());
                avatarState.inventory.AddItem(equipment);
                avatarState.inventory.AddItem(costume);

                foreach (var address in avatarState.combinationSlotAddresses)
                {
                    var slotState =
                        new CombinationSlotState(address, GameConfig.RequireClearedStageLevel.CombinationEquipmentAction);
                    states = states.SetState(address, slotState.Serialize());
                }

                states = states
                    .SetState(context.Signer, agentState.Serialize())
                    .SetState(avatarAddress, avatarState.Serialize())
                    .SetState(Addresses.Ranking, rankingState.Serialize());
            }

            return states;
        }
    }
}
