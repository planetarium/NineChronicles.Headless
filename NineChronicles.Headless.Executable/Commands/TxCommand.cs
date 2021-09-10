using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Bencodex;
using Bencodex.Types;
using Cocona;
using Libplanet;
using Libplanet.Blocks;
using Libplanet.Crypto;
using Libplanet.Tx;
using Nekoyume.Action;
using Nekoyume.Model;
using Nekoyume.Model.State;
using NineChronicles.Headless.Executable.IO;
using NCAction = Libplanet.Action.PolymorphicAction<Nekoyume.Action.ActionBase>;

namespace NineChronicles.Headless.Executable.Commands
{
    public class TxCommand : CoconaLiteConsoleAppBase
    {
        private static readonly Codec Codec = new Codec();
        private readonly IConsole _console;

        public TxCommand(IConsole console)
        {
            _console = console;
        }

        [Command(Description = "Create new transaction with given actions and dump it.")]
        public void Sign(
            [Argument("PRIVATE-KEY", Description = "A hex-encoded private key for signing.")] string privateKey,
            [Argument("NONCE", Description = "A nonce for new transaction.")] long nonce,
            [Argument("GENESIS-HASH", Description = "A hex-encoded genesis block hash.")] string genesisHash,
            [Option("action", new[] { 'a' }, Description = "A hex-encoded actions or a path of the file contained it.")] string[] actions,
            [Argument("TIMESTAMP", Description = "A datetime for new transaction.")] string? timestamp = null,
            [Option("bytes", new[] { 'b' }, Description = "Print raw bytes instead of hexadecimal.  No trailing LF appended.")] bool bytes = false
        )
        {
            List<NCAction> parsedActions = actions.Select(a =>
            {
                if (File.Exists(a))
                {
                    a = File.ReadAllText(a);
                }

                var decoded = (List)Codec.Decode(ByteUtil.ParseHex(a));
                string type = (Text)decoded[0];
                Dictionary plainValue = (Dictionary)decoded[1];

                var action = type switch
                {
                    nameof(ActivateAccount) => new ActivateAccount(),
                    _ => throw new CommandExitedException($"Unsupported action type was passed '{type}'", 128),
                };
                action.LoadPlainValue(plainValue);

                return (NCAction)action;
            }).ToList();

            Transaction<NCAction> tx = Transaction<NCAction>.Create(
                nonce: nonce,
                privateKey: new PrivateKey(ByteUtil.ParseHex(privateKey)),
                genesisHash: BlockHash.FromString(genesisHash),
                timestamp: (timestamp is null) ? DateTimeOffset.UtcNow : DateTimeOffset.Parse(timestamp),
                actions: parsedActions
            );
            byte[] raw = tx.Serialize(true);

            if (bytes)
            {
                _console.Out.Write(ByteUtil.Hex(raw));
            }
            else
            {
                _console.Out.WriteLine(ByteUtil.Hex(raw));
            }
        }
    }
}
