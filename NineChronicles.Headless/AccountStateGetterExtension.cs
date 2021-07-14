using Bencodex.Types;
using Libplanet;
using Libplanet.Action;
using Nekoyume.Action;
using Nekoyume.Model.State;
using static Lib9c.SerializeKeys;

namespace NineChronicles.Headless
{
    public static class AccountStateGetterExtension
    {
        public static AvatarState GetAvatarState(this AccountStateGetter accountStateGetter, Address avatarAddress)
        {
            if (accountStateGetter(avatarAddress) is Dictionary dictionary)
            {
                string[] keys =
                {
                    LegacyInventoryKey,
                    LegacyWorldInformationKey,
                    LegacyQuestListKey,
                };

                bool v1 = false;
                var serializedAvatar = dictionary;

                foreach (var key in keys)
                {
                    var keyAddress = avatarAddress.Derive(key);
                    var serialized = accountStateGetter(keyAddress);
                    if (serialized is null)
                    {
                        v1 = true;
                        break;
                    }

                    serializedAvatar = serializedAvatar.SetItem(key, serialized);
                }

                if (v1)
                {
                    return new AvatarState(dictionary);
                }

                return new AvatarState(serializedAvatar);
            }

            throw new InvalidAddressException($"Can't find {nameof(AvatarState)} from {avatarAddress}");
        }
    }
}
