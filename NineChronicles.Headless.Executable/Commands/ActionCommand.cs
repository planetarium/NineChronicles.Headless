using System;
using System.ComponentModel.DataAnnotations;
using System.IO;
using Bencodex;
using Bencodex.Types;
using Cocona;
using Libplanet;
using Libplanet.Assets;
using Nekoyume.Action;
using Nekoyume.Model;
using NineChronicles.Headless.Executable.IO;
using NCAction = Libplanet.Action.PolymorphicAction<Nekoyume.Action.ActionBase>;

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

        [Command(Description = "Create ActivateAccount action.")]
        public int ActivateAccount(
            [Argument("INVITATION-CODE", Description = "An invitation code.")] string invitationCode,
            [Argument("NONCE", Description = "A hex-encoded nonce for activation.")] string nonceEncoded,
            [Argument("PATH", Description = "A file path of base64 encoded action.")] string filePath
        )
        {
            try
            {
                ActivationKey activationKey = ActivationKey.Decode(invitationCode);
                byte[] nonce = ByteUtil.ParseHex(nonceEncoded);
                Nekoyume.Action.ActivateAccount action = activationKey.CreateActivateAccount(nonce);
                var encoded = new List(
                    new[]
                    {
                        (Text) nameof(Nekoyume.Action.ActivateAccount),
                        action.PlainValue
                    }
                );

                byte[] raw = Codec.Encode(encoded);
                File.WriteAllText(filePath, Convert.ToBase64String(raw));
                return 0;
            }
            catch (Exception e)
            {
                _console.Error.WriteLine(e);
                return -1;
            }
        }

        [Command(Description = "Create MonsterCollect action.")]
        public int MonsterCollect(
            [Range(0, 7)] int level,
            [Argument("PATH", Description = "A file path of base64 encoded action.")] string filePath
        )
        {
            try
            {
                Nekoyume.Action.MonsterCollect action = new MonsterCollect
                {
                    level = level
                };
                var encoded = new List(
                    new[]
                    {
                        (Text) nameof(Nekoyume.Action.MonsterCollect),
                        action.PlainValue
                    }
                );

                byte[] raw = Codec.Encode(encoded);
                File.WriteAllText(filePath, Convert.ToBase64String(raw));
                return 0;
            }
            catch (Exception e)
            {
                _console.Error.WriteLine(e);
                return -1;
            }
        }

        [Command(Description = "Create ClaimMonsterCollectionReward action.")]
        public int ClaimMonsterCollectionReward(
            [Argument("AVATAR-ADDRESS", Description = "A hex-encoded avatar address.")] string encodedAddress,
            [Argument("PATH", Description = "A file path of base64 encoded action.")] string filePath
        )
        {
            try
            {
                Address avatarAddress = new Address(ByteUtil.ParseHex(encodedAddress));
                Nekoyume.Action.ClaimMonsterCollectionReward action = new ClaimMonsterCollectionReward
                {
                    avatarAddress = avatarAddress
                };

                var encoded = new List(
                    new[]
                    {
                        (Text) nameof(Nekoyume.Action.ClaimMonsterCollectionReward),
                        action.PlainValue
                    }
                );

                byte[] raw = Codec.Encode(encoded);
                File.WriteAllText(filePath, Convert.ToBase64String(raw));
                return 0;
            }
            catch (Exception e)
            {
                _console.Error.WriteLine(e);
                return -1;
            }
        }

        [Command(Description = "Create TransferAsset action.")]
        public int TransferAsset(
            [Argument("SENDER-ADDRESS", Description = "A hex-encoded sender address.")] string senderAddress,
            [Argument("RECIPIENT-ADDRESS", Description = "A hex-encoded recipient address.")] string recipientAddress,
            [Argument("AMOUNT", Description = "The amount of asset to transfer.")]  string amount,
            [Argument("PATH", Description = "A file path of base64 encoded action.")] string filePath,
            [Argument("MEMO", Description = "A memo of asset transfer")] string? memo = null
        )
        {
            try
            {
                var currency = new Currency("NCG", 2, minter: null);
                FungibleAssetValue amountFungibleAssetValue =
                    FungibleAssetValue.Parse(currency, amount);
                Address sender = new Address(ByteUtil.ParseHex(senderAddress));
                Address recipient = new Address(ByteUtil.ParseHex(recipientAddress));
                Nekoyume.Action.TransferAsset action = new TransferAsset(
                    sender,
                    recipient,
                    amountFungibleAssetValue,
                    memo);

                var encoded = new List(
                    new[]
                    {
                        (Text) nameof(Nekoyume.Action.TransferAsset),
                        action.PlainValue
                    }
                );

                byte[] raw = Codec.Encode(encoded);
                File.WriteAllText(filePath, Convert.ToBase64String(raw));
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
