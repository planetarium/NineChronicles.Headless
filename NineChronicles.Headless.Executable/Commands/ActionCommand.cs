using System;
using System.ComponentModel.DataAnnotations;
using System.IO;
using System.Numerics;
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
            [Argument("PATH", Description = "A file path of base64 encoded action.")] string? filePath = null
        )
        {
            try
            {
                ActivationKey activationKey = ActivationKey.Decode(invitationCode);
                byte[] nonce = ByteUtil.ParseHex(nonceEncoded);
                Nekoyume.Action.ActivateAccount action = activationKey.CreateActivateAccount(nonce);
                var list = new List(
                    new[]
                    {
                        (Text) nameof(Nekoyume.Action.ActivateAccount),
                        action.PlainValue
                    }
                );

                byte[] raw = Codec.Encode(list);
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

        [Command(Description = "Create MonsterCollect action.")]
        public int MonsterCollect(
            [Range(0, 7)] int level,
            [Argument("PATH", Description = "A file path of base64 encoded action.")] string? filePath = null
        )
        {
            try
            {
                Nekoyume.Action.MonsterCollect action = new MonsterCollect
                {
                    level = level
                };

                byte[] raw = Codec.Encode(new List(
                    new[]
                    {
                        (Text) nameof(Nekoyume.Action.MonsterCollect),
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

        [Command(Description = "Create ClaimMonsterCollectionReward action.")]
        public int ClaimMonsterCollectionReward(
            [Argument("AVATAR-ADDRESS", Description = "A hex-encoded avatar address.")] string encodedAddress,
            [Argument("PATH", Description = "A file path of base64 encoded action.")] string? filePath = null
        )
        {
            try
            {
                Address avatarAddress = new Address(ByteUtil.ParseHex(encodedAddress));
                Nekoyume.Action.ClaimMonsterCollectionReward action = new ClaimMonsterCollectionReward
                {
                    avatarAddress = avatarAddress
                };

                byte[] raw = Codec.Encode(new List(
                    new[]
                    {
                        (Text) nameof(Nekoyume.Action.ClaimMonsterCollectionReward),
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

        [Command(Description = "Create TransferAsset action.")]
        public int TransferAsset(
            [Argument("SENDER-ADDRESS", Description = "A hex-encoded sender address.")] string senderAddress,
            [Argument("RECIPIENT-ADDRESS", Description = "A hex-encoded recipient address.")] string recipientAddress,
            [Argument("AMOUNT", Description = "The amount of asset to transfer.")] string amount,
            [Argument("PATH", Description = "A file path of base64 encoded action.")] string? filePath = null,
            [Argument("MEMO", Description = "A memo of asset transfer")] string? memo = null
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
                    Console.Write(encoded);
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
            [Argument("PATH", Description = "A file path of base64 encoded action.")] string? filePath = null
        )
        {
            try
            {
                Nekoyume.Action.Stake action = new Stake(amount);
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
            [Argument("AVATAR-ADDRESS", Description = "A hex-encoded avatar address.")] string encodedAddress,
            [Argument("PATH", Description = "A file path of base64 encoded action.")] string? filePath = null
        )
        {
            try
            {
                Address avatarAddress = new Address(ByteUtil.ParseHex(encodedAddress));
                Nekoyume.Action.ClaimStakeReward action = new ClaimStakeReward(avatarAddress);

                byte[] raw = Codec.Encode(new List(
                    new[]
                    {
                        (Text) nameof(Nekoyume.Action.ClaimStakeReward),
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
            [Argument("AVATAR-ADDRESS", Description = "A hex-encoded avatar address.")] string encodedAddress,
            [Argument("PATH", Description = "A file path of base64 encoded action.")] string? filePath = null
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
