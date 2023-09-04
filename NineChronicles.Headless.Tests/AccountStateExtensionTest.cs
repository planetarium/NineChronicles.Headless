using Libplanet.Action.State;
using Nekoyume.Action.Extensions;
using Nekoyume.Model.Exceptions;
using Nekoyume.Module;
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
            IWorld mockWorld = new MockWorld();

            mockWorld = backward
                ? AvatarModule.SetAvatarState(
                    mockWorld,
                    Fixtures.AvatarAddress,
                    Fixtures.AvatarStateFX)
                : AvatarModule.SetAvatarStateV2(
                    mockWorld,
                    Fixtures.AvatarAddress,
                    Fixtures.AvatarStateFX);
            mockWorld = inventoryExist
                ? LegacyModule.SetState(
                    mockWorld,
                    Fixtures.AvatarAddress.Derive(LegacyInventoryKey),
                    Fixtures.AvatarStateFX.inventory.Serialize())
                : mockWorld;
            mockWorld = worldInformationExist
                ? LegacyModule.SetState(
                    mockWorld,
                    Fixtures.AvatarAddress.Derive(LegacyWorldInformationKey),
                    Fixtures.AvatarStateFX.worldInformation.Serialize())
                : mockWorld;
            mockWorld = questListExist
                ? LegacyModule.SetState(
                    mockWorld,
                    Fixtures.AvatarAddress.Derive(LegacyQuestListKey),
                    Fixtures.AvatarStateFX.questList.Serialize())
                : mockWorld;

            if (exc)
            {
                Assert.Throws<InvalidAddressException>(
                    () => AvatarModule.GetAvatarState(mockWorld, default));
            }
            else
            {
                var avatarState = AvatarModule.GetAvatarState(mockWorld, Fixtures.AvatarAddress);

                Assert.NotNull(avatarState.inventory);
                Assert.NotNull(avatarState.worldInformation);
                Assert.NotNull(avatarState.questList);
            }
        }
    }
}
