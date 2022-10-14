using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.ComponentModel.DataAnnotations;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using Bencodex;
using Bencodex.Types;
using Cocona;
using Cocona.Help;
using Libplanet;
using Libplanet.Action;
using System.Text.Json;
using Bencodex.Json;
using Libplanet.Assets;
using Libplanet.Blocks;
using Libplanet.Crypto;
using Libplanet.Extensions.Cocona;
using Libplanet.Store;
using Nekoyume.Action;
using Nekoyume.Model;
using Newtonsoft.Json.Linq;
using NineChronicles.Headless.Executable.IO;
using NineChronicles.Headless.Executable.Store;
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

        [PrimaryCommand]
        public void Help([FromService] ICoconaHelpMessageBuilder helpMessageBuilder)
        {
            _console.Error.WriteLine(helpMessageBuilder.BuildAndRenderForCurrentContext());
        }

        [Command(Description = "Create ActivateAccount action.")]
        public int ActivateAccount(
            [Argument("INVITATION-CODE", Description = "An invitation code.")]
            string invitationCode,
            [Argument("NONCE", Description = "A hex-encoded nonce for activation.")]
            string nonceEncoded,
            [Argument("PATH", Description = "A file path of base64 encoded action.")]
            string? filePath = null
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
            Type attrType = typeof(ActionTypeAttribute);
            Type obsoleteType = typeof(ActionObsoleteAttribute);

            bool IsTarget(Type type)
            {
                return baseType.IsAssignableFrom(type) &&
                       type.IsDefined(attrType) &&
                       ActionTypeAttribute.ValueOf(type) is { } &&
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
                .Select(type => ActionTypeAttribute.ValueOf(type))
                .OrderBy(type => type);

            foreach (string? typeId in typeIds)
            {
                _console.Out.WriteLine(typeId);
            }

            return typeIds;
        }


        [Command(Description = "Create MonsterCollect action.")]
        public int MonsterCollect(
            [Range(0, 7)] int level,
            [Argument("PATH", Description = "A file path of base64 encoded action.")]
            string? filePath = null
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
            [Argument("AVATAR-ADDRESS", Description = "A hex-encoded avatar address.")]
            string encodedAddress,
            [Argument("PATH", Description = "A file path of base64 encoded action.")]
            string? filePath = null
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
            [Argument("PATH", Description = "A file path of base64 encoded action.")]
            string? filePath = null
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
            [Argument("AVATAR-ADDRESS", Description = "A hex-encoded avatar address.")]
            string encodedAddress,
            [Argument("PATH", Description = "A file path of base64 encoded action.")]
            string? filePath = null
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

        [Command(Description = "Execute action and calculate next state")]
        public int Execute(
            [Argument("ACTIONS-PATH", Description = "A JSON file path of actions.")]
            string actionsPath,
            [Option("STORE-PATH", Description = "An absolute path of block storage.")]
            string storePath,
            [Option("BLOCK-INDEX", Description = "Target block height to run action. Tip as default.")]
            int blockIndex = -1
        )
        {
            try
            {
                // Read json file and parse actions.
                // NOTE: https://github.com/planetarium/libplanet/blob/main/Libplanet/Action/ActionListJsonConverter.cs
                //       If ActionListJsonConverter to be public, we can use it.
                using var stream = new FileStream(actionsPath, FileMode.Open);
                stream.Seek(0, SeekOrigin.Begin);
                var bytes = new byte[stream.Length];
                while (stream.Position < stream.Length)
                {
                    bytes[stream.Position] = (byte)stream.ReadByte();
                }

                var converter = new BencodexJsonConverter();
                var actionsReader = new Utf8JsonReader(bytes);
                var actionsValue = converter.Read(
                    ref actionsReader,
                    typeof(object),
                    new JsonSerializerOptions());
                if (actionsValue is not List actionsList)
                {
                    throw new CommandExitedException(
                        $"The given actions file, {actionsPath} is not a list.",
                        -1);
                }

                var typeToConvert = typeof(PolymorphicAction<ActionBase>);
                var actions = actionsList.Select(actionValue =>
                {
                    var action = (PolymorphicAction<ActionBase>)Activator.CreateInstance(typeToConvert)!;
                    action.LoadPlainValue(actionValue);
                    var innerAction = action.InnerAction;
                    _console.Out.WriteLine($"inner action type: {innerAction.GetType().FullName}");
                    return action;
                }).ToList();
                // ~Read json file and parse actions.
                // Load store.
                if (!Directory.Exists(storePath))
                {
                    throw new CommandExitedException($"The given STORE-PATH, {storePath} does not found.", -1);
                }

                var store = StoreType.RocksDb.CreateStore(storePath);
                if (store.GetCanonicalChainId() is not { } chainId)
                {
                    throw new CommandExitedException($"There is no canonical chain: {storePath}", -1);
                }

                var blockHash = store.IndexBlockHash(chainId, blockIndex) ??
                                throw new CommandExitedException(
                                    $"The given blockIndex {blockIndex} does not found", -1);
                _console.Out.WriteLine($"block hash: {blockHash}");
                var block = store.GetBlock<NCAction>(blockHash);
                _console.Out.WriteLine($"block state root hash: {block.StateRootHash}");
                // ~Load store.
                // Execute actions.
                // var actionContext = new Context
                // foreach (var action in actions)
                // {
                //     action.Execute()
                // }
                // var state = new State(store, block.StateRootHash);
                // ~Execute actions.
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
