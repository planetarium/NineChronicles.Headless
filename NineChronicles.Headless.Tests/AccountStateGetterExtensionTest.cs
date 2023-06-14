using System.Collections.Generic;
using System.Linq;
using Bencodex.Types;
using Libplanet;
using Libplanet.State;
using Nekoyume.Action;
using Xunit;
using static Lib9c.SerializeKeys;

namespace NineChronicles.Headless.Tests
{
    public class AccountStateGetterExtensionTest
    {
        [Theory]
        [InlineData(true, false, false, false, false)]
        [InlineData(true, false, false, false, true)]
        [InlineData(false, true, true, true, false)]
        [InlineData(false, false, true, true, true)]
        [InlineData(false, true, false, true, true)]
        [InlineData(false, true, true, false, true)]
        public void GetAvatarState(bool backward, bool inventoryExist, bool worldInformationExist, bool questListExist, bool exc)
        {
            IValue? GetStateMock(Address address)
            {
                if (backward && Fixtures.AvatarAddress == address)
                {
                    return Fixtures.AvatarStateFX.Serialize();
                }
                if (Fixtures.AvatarAddress == address)
                {
                    return Fixtures.AvatarStateFX.SerializeV2();
                }

                if (Fixtures.AvatarAddress.Derive(LegacyInventoryKey) == address && inventoryExist)
                {
                    return Fixtures.AvatarStateFX.inventory.Serialize();
                }
                if (Fixtures.AvatarAddress.Derive(LegacyWorldInformationKey) == address && worldInformationExist)
                {
                    return Fixtures.AvatarStateFX.worldInformation.Serialize();
                }
                if (Fixtures.AvatarAddress.Derive(LegacyQuestListKey) == address && questListExist)
                {
                    return Fixtures.AvatarStateFX.questList.Serialize();
                }

                return null;
            }

            IReadOnlyList<IValue?> GetStatesMock(IReadOnlyList<Address> addresses) =>
                addresses.Select(GetStateMock).ToArray();

            var getter = (AccountStateGetter)GetStatesMock;

            if (exc)
            {
                Assert.Throws<InvalidAddressException>(() => getter.GetAvatarState(default));
            }
            else
            {
                var avatarState = getter.GetAvatarState(Fixtures.AvatarAddress);

                Assert.NotNull(avatarState.inventory);
                Assert.NotNull(avatarState.worldInformation);
                Assert.NotNull(avatarState.questList);
            }
        }
    }
}
