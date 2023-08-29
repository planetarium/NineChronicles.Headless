using Nekoyume.Action.Extensions;
using Nekoyume.Model.Exceptions;
using NineChronicles.Headless.Tests.Common;
using Xunit;
using static Lib9c.SerializeKeys;

namespace NineChronicles.Headless.Tests
{
    public class AccountStateExtensionTest
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
            MockAccountState mockAccountState = MockAccountState.Empty;

            mockAccountState = backward
                ? mockAccountState.SetState(Fixtures.AvatarAddress, Fixtures.AvatarStateFX.Serialize())
                : mockAccountState.SetState(Fixtures.AvatarAddress, Fixtures.AvatarStateFX.SerializeV2());
            mockAccountState = inventoryExist
                ? mockAccountState.SetState(
                    Fixtures.AvatarAddress.Derive(LegacyInventoryKey),
                    Fixtures.AvatarStateFX.inventory.Serialize())
                : mockAccountState;
            mockAccountState = worldInformationExist
                ? mockAccountState.SetState(
                    Fixtures.AvatarAddress.Derive(LegacyWorldInformationKey),
                    Fixtures.AvatarStateFX.worldInformation.Serialize())
                : mockAccountState;
            mockAccountState = questListExist
                ? mockAccountState.SetState(
                    Fixtures.AvatarAddress.Derive(LegacyQuestListKey),
                    Fixtures.AvatarStateFX.questList.Serialize())
                : mockAccountState;

            if (exc)
            {
                Assert.Throws<InvalidAddressException>(() => mockAccountState.GetAvatarState(default));
            }
            else
            {
                var avatarState = mockAccountState.GetAvatarState(Fixtures.AvatarAddress);

                Assert.NotNull(avatarState.inventory);
                Assert.NotNull(avatarState.worldInformation);
                Assert.NotNull(avatarState.questList);
            }
        }
    }
}
