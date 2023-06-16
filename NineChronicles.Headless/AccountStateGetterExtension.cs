using System.Collections.Generic;
using System.Linq;
using Bencodex.Types;
using Libplanet;
using Libplanet.State;
using Nekoyume.Action;
using Nekoyume.Model.State;
using static Lib9c.SerializeKeys;

namespace NineChronicles.Headless
{
    public static class AccountStateGetterExtension
    {
        private static readonly string[] AvatarLegacyKeys =
        {
            LegacyInventoryKey,
            LegacyWorldInformationKey,
            LegacyQuestListKey,
        };

        public static IReadOnlyList<AvatarState> GetAvatarStates(
            this AccountStateGetter accountStateGetter,
            IReadOnlyList<Address> avatarAddresses
        )
        {
            IReadOnlyDictionary<Address, Dictionary> rawAvatarStates = GetRawAvatarStates(accountStateGetter, avatarAddresses);
            var states = new AvatarState[rawAvatarStates.Count];
            var values = rawAvatarStates.Values.ToArray();
            for (int i = 0; i < rawAvatarStates.Count; i++)
            {
                states[i] = new AvatarState(values[i]);
            }

            return states;
        }

        public static AvatarState GetAvatarState(this AccountStateGetter accountStateGetter, Address avatarAddress) =>
            accountStateGetter.GetAvatarStates(new[] { avatarAddress })[0];

        public static IReadOnlyDictionary<Address, Dictionary> GetRawAvatarStates(
            this AccountStateGetter accountStateGetter,
            IReadOnlyList<Address> avatarAddresses
        )
        {
            // Suppose avatarAddresses = [a, b, c]
            // Then,   addresses =       [a,                    b,                    c,
            //                            aInventoryKey,        bInventoryKey,        cInventoryKey,
            //                            aWorldInformationKey, bWorldInformationKey, cWorldInformationKey,
            //                            aQuestListKey,        bQuestListKey,        cQuestListKey]
            var addresses = new Address[avatarAddresses.Count * (AvatarLegacyKeys.Length + 1)];
            for (var i = 0; i < avatarAddresses.Count; i++)
            {
                var a = avatarAddresses[i];
                addresses[i] = a;
                for (int j = 0; j < AvatarLegacyKeys.Length; j++)
                {
                    addresses[avatarAddresses.Count * (j + 1) + i] = a.Derive(AvatarLegacyKeys[j]);
                }
            }

            IReadOnlyList<IValue?> values = accountStateGetter(addresses);
            var states = new Dictionary<Address, Dictionary>(avatarAddresses.Count);
            for (var i = 0; i < avatarAddresses.Count; i++)
            {
                IValue? value = values[i];
                if (!(value is Dictionary serializedAvatar))
                {
                    throw new InvalidAddressException($"Can't find {nameof(AvatarState)} from {avatarAddresses[i]}");
                }

                Dictionary original = serializedAvatar;
                bool v1 = false;
                for (int j = 0; j < AvatarLegacyKeys.Length; j++)
                {
                    if (!(values[avatarAddresses.Count * (j + 1) + i] is { } serialized))
                    {
                        v1 = true;
                        break;
                    }

                    serializedAvatar = serializedAvatar.SetItem(AvatarLegacyKeys[j], serialized);
                }

                states[avatarAddresses[i]] = v1 ? original : serializedAvatar;
            }

            return states;
        }
    }
}
