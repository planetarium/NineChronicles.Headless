using System;
using System.ComponentModel.DataAnnotations;
using System.IO;
using System.Linq;
using System.Reflection;
using Bencodex;
using Bencodex.Types;
using Cocona;
using Cocona.Help;
using Libplanet.Action;
using Libplanet.Common;
using Libplanet.Crypto;
using Libplanet.Types.Assets;
using Nekoyume.Action;
using Nekoyume.Model;
using NineChronicles.Headless.Executable.IO;

namespace NineChronicles.Headless.Executable.Commands
{
    public class ActionCommand : CoconaLiteConsoleAppBase
    {
        private static readonly Codec Codec = new Codec();
        private readonly IConsole _console;

        public ActionCommand(IConsole console)
        {
            _console = console;
        }

        [PrimaryCommand]
        public void Help([FromService] ICoconaHelpMessageBuilder helpMessageBuilder)
        {
            _console.Error.WriteLine(helpMessageBuilder.BuildAndRenderForCurrentContext());
        }

        [Command(Description = "Lists all actions' type ids.")]
        public IOrderedEnumerable<string?> List(
            [Option(
                Description = "If true, filter obsoleted actions since the --block-index option."
            )]
            bool excludeObsolete = false,
            [Option(
                Description = "The current block index to filter obsoleted actions."
            )]
            long blockIndex = 0
        )
        {
            Type baseType = typeof(Nekoyume.Action.ActionBase);
            Type obsoleteType = typeof(ActionObsoleteAttribute);

            bool IsTarget(Type type)
            {
                return baseType.IsAssignableFrom(type) &&
                    type.GetCustomAttribute<ActionTypeAttribute>() is { } attr &&
                    (
                        !excludeObsolete ||
                        !type.IsDefined(obsoleteType) ||
                        type
                            .GetCustomAttributes()
                            .OfType<ActionObsoleteAttribute>()
                            .Select(attr => attr.ObsoleteIndex)
                            .FirstOrDefault() > blockIndex
                    );
            }

            var assembly = baseType.Assembly;
            var typeIds = assembly.GetTypes()
                .Where(IsTarget)
                .Select(type => type.GetCustomAttribute<ActionTypeAttribute>()?.TypeIdentifier)
                .OfType<Text>()
                .Select(v => v.Value).ToArray();

            foreach (string? typeId in typeIds
                         .OrderBy(type => type))
            {
                _console.Out.WriteLine(typeId);
            }

            return typeIds.OrderBy(type => type);
        }

        [Command(Description = "Create TransferAsset action.")]
        public int TransferAsset(
            [Argument("SENDER-ADDRESS", Description = "A hex-encoded sender address.")]
            string senderAddress,
            [Argument("RECIPIENT-ADDRESS", Description = "A hex-encoded recipient address.")]
            string recipientAddress,
            [Argument("AMOUNT", Description = "The amount of asset to transfer.")]
            string amount,
            [Argument("PATH", Description = "A file path of base64 encoded action.")]
            string? filePath = null,
            [Argument("MEMO", Description = "A memo of asset transfer")]
            string? memo = null
        )
        {
            try
            {
                // Minter for 9c-mainnet
#pragma warning disable CS0618
                // Use of obsolete method Currency.Legacy(): https://github.com/planetarium/lib9c/discussions/1319
                var currency = Currency.Legacy("NCG", 2, minter: new Address("47d082a115c63e7b58b1532d20e631538eafadde"));
#pragma warning restore CS0618
                FungibleAssetValue amountFungibleAssetValue =
                    FungibleAssetValue.Parse(currency, amount);
                Address sender = new Address(ByteUtil.ParseHex(senderAddress));
                Address recipient = new Address(ByteUtil.ParseHex(recipientAddress));
                Nekoyume.Action.TransferAsset action = new TransferAsset(
                    sender,
                    recipient,
                    amountFungibleAssetValue,
                    memo);

                byte[] raw = Codec.Encode(new List(
                    new[]
                    {
                        (Text) nameof(Nekoyume.Action.TransferAsset),
                        action.PlainValue
                    }
                ));
                string encoded = Convert.ToBase64String(raw);
                if (filePath is null)
                {
                    _console.Out.Write(encoded);
                }
                else
                {
                    File.WriteAllText(filePath, encoded);
                }
                return 0;
            }
            catch (Exception e)
            {
                _console.Error.WriteLine(e);
                return -1;
            }
        }

        [Command(Description = "Create Stake action.")]
        public int Stake(
            long amount,
            Address avatarAddress,
            [Argument("PATH", Description = "A file path of base64 encoded action.")]
            string? filePath = null
        )
        {
            try
            {
                Nekoyume.Action.Stake action = new Stake(amount, avatarAddress);
                byte[] raw = Codec.Encode(new List(
                    new[]
                    {
                        (Text) nameof(Nekoyume.Action.Stake),
                        action.PlainValue
                    }
                ));
                string encoded = Convert.ToBase64String(raw);
                if (filePath is null)
                {
                    _console.Out.Write(encoded);
                }
                else
                {
                    File.WriteAllText(filePath, encoded);
                }

                return 0;
            }
            catch (Exception e)
            {
                _console.Error.WriteLine(e);
                return -1;
            }
        }

        [Command(Description = "Create ClaimStakeReward action.")]
        public int ClaimStakeReward(
            [Argument("AVATAR-ADDRESS", Description = "A hex-encoded avatar address.")]
            string encodedAddress,
            [Argument("PATH", Description = "A file path of base64 encoded action.")]
            string? filePath = null
        )
        {
            try
            {
                Address avatarAddress = new Address(ByteUtil.ParseHex(encodedAddress));
                IClaimStakeReward? action = null;
                action = new ClaimStakeReward(avatarAddress);

                byte[] raw = Codec.Encode(new List(
                    new[]
                    {
                        (Text) action.GetType().Name,
                        action.PlainValue
                    }
                ));
                string encoded = Convert.ToBase64String(raw);
                if (filePath is null)
                {
                    _console.Out.Write(encoded);
                }
                else
                {
                    File.WriteAllText(filePath, encoded);
                }

                return 0;
            }
            catch (Exception e)
            {
                _console.Error.WriteLine(e);
                return -1;
            }
        }

        [Command(Description = "Create MigrateMonsterCollection action.")]
        public int MigrateMonsterCollection(
            [Argument("AVATAR-ADDRESS", Description = "A hex-encoded avatar address.")]
            string encodedAddress,
            [Argument("PATH", Description = "A file path of base64 encoded action.")]
            string? filePath = null
        )
        {
            try
            {
                Address avatarAddress = new Address(ByteUtil.ParseHex(encodedAddress));
                Nekoyume.Action.MigrateMonsterCollection action = new MigrateMonsterCollection(avatarAddress);

                byte[] raw = Codec.Encode(new List(
                    new[]
                    {
                        (Text) nameof(Nekoyume.Action.MigrateMonsterCollection),
                        action.PlainValue
                    }
                ));
                string encoded = Convert.ToBase64String(raw);
                if (filePath is null)
                {
                    _console.Out.Write(encoded);
                }
                else
                {
                    File.WriteAllText(filePath, encoded);
                }

                return 0;
            }
            catch (Exception e)
            {
                _console.Error.WriteLine(e);
                return -1;
            }
        }
    }
}
