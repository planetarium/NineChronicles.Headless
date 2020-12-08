using System;
using Bencodex.Types;
using Libplanet.Action;
using Nekoyume.Action;

namespace NineChronicles.Headless.Tests.Common.Actions
{
    // 테스트를 위해 만든 RewardGold 액션입니다.
    class RewardGold : ActionBase
    {
        public override void LoadPlainValue(IValue plainValue)
        {
        }

        public override IAccountStateDelta Execute(IActionContext context)
        {
            var states = context.PreviousStates;
            if (context.Rehearsal)
            {
                return states.SetState(context.Signer, default(Null));
            }

            var gold = states.TryGetState(context.Signer, out Integer integer) ? integer : (Integer)0;
            gold += 1;

            return states.SetState(context.Signer, gold);
        }

        public override IValue PlainValue => new Null();
    }
}
