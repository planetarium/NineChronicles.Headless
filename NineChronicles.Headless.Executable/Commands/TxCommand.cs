using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using Bencodex;
using Bencodex.Types;
using Cocona;
using CsvHelper;
using Lib9c;
using Libplanet.Action;
using Libplanet.Common;
using Libplanet.Crypto;
using Libplanet.Types.Assets;
using Libplanet.Types.Blocks;
using Libplanet.Types.Tx;
using Nekoyume.Action;
using Nekoyume.Model;
using Nekoyume.Model.State;
using Nekoyume.TableData;
using NineChronicles.Headless.Executable.IO;
using static NineChronicles.Headless.NCActionUtils;

namespace NineChronicles.Headless.Executable.Commands
{
    public class TxCommand : CoconaLiteConsoleAppBase
    {
        private static readonly Codec _codec = new Codec();
        private readonly IConsole _console;

        public TxCommand(IConsole console)
        {
            _console = console;
        }

        [Command(Description = "Create new transaction with given actions and dump it.")]
        public void Sign(
            [Argument("PRIVATE-KEY", Description = "A hex-encoded private key for signing.")]
            string privateKey,
            [Argument("NONCE", Description = "A nonce for new transaction.")]
            long nonce,
            [Argument("GENESIS-HASH", Description = "A hex-encoded genesis block hash.")]
            string genesisHash,
            [Argument("TIMESTAMP", Description = "A datetime for new transaction.")]
            string timestamp,
            [Option("action", new[] { 'a' }, Description = "A path of the file contained base64 encoded actions.")]
            string[] actions,
            [Option("bytes", new[] { 'b' },
                Description = "Print raw bytes instead of base64.  No trailing LF appended.")]
            bool bytes = false,
            [Option("max-gas-price", Description = "maximum price per gas fee.")]
            long? maxGasPrice = null
        )
        {
            List<ActionBase> parsedActions = actions.Select(a =>
            {
                if (File.Exists(a))
                {
                    a = File.ReadAllText(a);
                }

                var decoded = (List)_codec.Decode(Convert.FromBase64String(a));
                string type = (Text)decoded[0];
                Dictionary plainValue = (Dictionary)decoded[1];

                ActionBase action = type switch
                {
                    nameof(Stake) => new Stake(),
                    // FIXME: This `ClaimStakeReward` cases need to reduce to one case.
                    nameof(ClaimStakeReward2) => new ClaimStakeReward2(),
                    nameof(ClaimStakeReward) => new ClaimStakeReward(),
                    nameof(TransferAsset) => new TransferAsset(),
                    _ => throw new CommandExitedException($"Unsupported action type was passed '{type}'", 128)
                };
                action.LoadPlainValue(plainValue);

                return action;
            }).ToList();

            Transaction tx = Transaction.Create(
                nonce: nonce,
                privateKey: new PrivateKey(ByteUtil.ParseHex(privateKey)),
                genesisHash: BlockHash.FromString(genesisHash),
                timestamp: DateTimeOffset.Parse(timestamp),
                gasLimit: parsedActions.Any(a => a is ITransferAssets or ITransferAsset) ? 4 : 1,
                maxGasPrice: maxGasPrice.HasValue ? maxGasPrice.Value * Currencies.Mead : null,
                actions: parsedActions.ToPlainValues()
            );
            byte[] raw = tx.Serialize();

            if (bytes)
            {
                _console.Out.WriteLine(raw);
            }
            else
            {
                _console.Out.WriteLine(Convert.ToBase64String(raw));
            }
        }

        public void TransferAsset(
            [Argument("SENDER", Description = "An address of sender.")]
            string sender,
            [Argument("RECIPIENT", Description = "An address of recipient.")]
            string recipient,
            [Argument("AMOUNT", Description = "An amount of gold to transfer.")]
            int goldAmount,
            [Argument("GENESIS-BLOCK", Description = "A genesis block containing InitializeStates.")]
            string genesisBlock
        )
        {
            byte[] genesisBytes = File.ReadAllBytes(genesisBlock);
            var genesisDict = (Bencodex.Types.Dictionary)_codec.Decode(genesisBytes);
            IReadOnlyList<Transaction> genesisTxs =
                BlockMarshaler.UnmarshalBlockTransactions(genesisDict);
            var initStates = (InitializeStates)ToAction(genesisTxs.Single().Actions!.Single());
            Currency currency = new GoldCurrencyState(initStates.GoldCurrency).Currency;

            var action = new TransferAsset(
                new Address(sender),
                new Address(recipient),
                currency * goldAmount
            );

            var bencoded = new List(
                (Text)nameof(TransferAsset),
                action.PlainValue
            );

            byte[] raw = _codec.Encode(bencoded);
            _console.Out.Write(ByteUtil.Hex(raw));
        }

        [Command(Description = "Create PatchTable action and dump it.")]
        public void PatchTable(
            [Argument("TABLE-PATH", Description = "A table file path for patch.")]
            string tablePath
        )
        {
            var tableName = Path.GetFileName(tablePath);
            if (tableName.EndsWith(".csv"))
            {
                tableName = tableName.Split(".csv")[0];
            }

            _console.Error.Write("----------------\n");
            _console.Error.Write(tableName);
            _console.Error.Write("\n----------------\n");
            var tableCsv = File.ReadAllText(tablePath);
            _console.Error.Write(tableCsv);

            var type = typeof(ISheet).Assembly
                .GetTypes()
                .First(type => type.Namespace is { } @namespace &&
                               @namespace.StartsWith($"{nameof(Nekoyume)}.{nameof(Nekoyume.TableData)}") &&
                               !type.IsAbstract &&
                               typeof(ISheet).IsAssignableFrom(type) &&
                               type.Name == tableName);
            var sheet = (ISheet)Activator.CreateInstance(type)!;
            sheet.Set(tableCsv);

            var action = new PatchTableSheet
            {
                TableName = tableName,
                TableCsv = tableCsv
            };

            var bencoded = new List(
                (Text)nameof(PatchTableSheet),
                action.PlainValue
            );

            byte[] raw = _codec.Encode(bencoded);
            _console.Out.WriteLine(ByteUtil.Hex(raw));
        }

        [Command(Description = "Create AddRedeemCode action and dump it.")]
        public void AddRedeemCode(
            [Argument("TABLE-PATH", Description = "A table file path for RedeemCodeListSheet")]
            string tablePath
        )
        {
            var tableCsv = File.ReadAllText(tablePath);
            var action = new AddRedeemCode
            {
                redeemCsv = tableCsv
            };
            var encoded = new List(
                (Text)nameof(Nekoyume.Action.AddRedeemCode),
                action.PlainValue
            );
            byte[] raw = _codec.Encode(encoded);
            _console.Out.WriteLine(ByteUtil.Hex(raw));
        }

        [Command(Description = "Create RenewAdminState action and dump it.")]
        public void RenewAdminState(
            [Argument("NEW-VALID-UNTIL")] long newValidUntil
        )
        {
            RenewAdminState action = new RenewAdminState(newValidUntil);
            var encoded = new List(
                (Text)nameof(Nekoyume.Action.RenewAdminState),
                action.PlainValue
            );
            byte[] raw = _codec.Encode(encoded);
            _console.Out.WriteLine(ByteUtil.Hex(raw));
        }

        [Command(Description = "Create ActvationKey-nonce pairs and dump them as csv")]
        public void CreateActivationKeys(
            [Argument("COUNT", Description = "An amount of pairs")]
            int count
        )
        {
            var rng = new Random();
            var nonce = new byte[4];
            _console.Out.WriteLine("EncodedActivationKey,NonceHex");
            foreach (int i in Enumerable.Range(0, count))
            {
                PrivateKey key;
                while (true)
                {
                    key = new PrivateKey();
                    if (key.ToByteArray().Length == 32)
                    {
                        break;
                    }
                }

                rng.NextBytes(nonce);
                var (ak, _) = ActivationKey.Create(key, nonce);
                _console.Out.WriteLine($"{ak.Encode()},{ByteUtil.Hex(nonce)}");
            }
        }
    }
}
