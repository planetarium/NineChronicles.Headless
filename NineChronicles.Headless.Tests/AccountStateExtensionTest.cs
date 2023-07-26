using Nekoyume.Action;
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
            MockState mockState = MockState.Empty;

            mockState = backward
                ? mockState.SetState(Fixtures.AvatarAddress, Fixtures.AvatarStateFX.Serialize())
                : mockState.SetState(Fixtures.AvatarAddress, Fixtures.AvatarStateFX.SerializeV2());
            mockState = inventoryExist
                ? mockState.SetState(
                    Fixtures.AvatarAddress.Derive(LegacyInventoryKey),
                    Fixtures.AvatarStateFX.inventory.Serialize())
                : mockState;
            mockState = worldInformationExist
                ? mockState.SetState(
                    Fixtures.AvatarAddress.Derive(LegacyWorldInformationKey),
                    Fixtures.AvatarStateFX.worldInformation.Serialize())
                : mockState;
            mockState = questListExist
                ? mockState.SetState(
                    Fixtures.AvatarAddress.Derive(LegacyQuestListKey),
                    Fixtures.AvatarStateFX.questList.Serialize())
                : mockState;

            if (exc)
            {
                Assert.Throws<InvalidAddressException>(() => mockState.GetAvatarState(default));
            }
            else
            {
                var avatarState = mockState.GetAvatarState(Fixtures.AvatarAddress);

                Assert.NotNull(avatarState.inventory);
                Assert.NotNull(avatarState.worldInformation);
                Assert.NotNull(avatarState.questList);
            }
        }
    }
}
