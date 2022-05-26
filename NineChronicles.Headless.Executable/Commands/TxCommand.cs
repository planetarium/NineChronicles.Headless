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
            [Argument("TIMESTAMP", Description = "A datetime for new transaction.")] string timestamp,
            [Option("action", new[] { 'a' }, Description = "A path of the file contained base64 encoded actions.")] string[] actions
        )
        {
            List<NCAction> parsedActions = actions.Select(a =>
            {
                if (File.Exists(a))
                {
                    a = File.ReadAllText(a);
                }

                var decoded = (List)Codec.Decode(Convert.FromBase64String(a));
                string type = (Text)decoded[0];
                Dictionary plainValue = (Dictionary)decoded[1];

                ActionBase action = type switch
                {
                    nameof(ActivateAccount) => new ActivateAccount(),
                    nameof(MonsterCollect) => new MonsterCollect(),
                    nameof(ClaimMonsterCollectionReward) => new ClaimMonsterCollectionReward(),
                    nameof(Stake) => new Stake(),
                    nameof(ClaimStakeReward) => new ClaimStakeReward(),
                    nameof(TransferAsset) => new TransferAsset(),
                    nameof(MigrateMonsterCollection) => new MigrateMonsterCollection(),
                    _ => throw new CommandExitedException($"Unsupported action type was passed '{type}'", 128)
                };
                action.LoadPlainValue(plainValue);

                return (NCAction)action;
            }).ToList();
            
            Console.WriteLine(privateKey);
            Console.WriteLine(genesisHash);
            Console.WriteLine(timestamp);

            Transaction<NCAction> tx = Transaction<NCAction>.Create(
                nonce: nonce,
                privateKey: new PrivateKey(ByteUtil.ParseHex(privateKey)),
                genesisHash: BlockHash.FromString(genesisHash),
                timestamp: DateTimeOffset.Parse(timestamp),
                actions: parsedActions
            );
            byte[] raw = tx.Serialize(true);

            _console.Out.WriteLine(Convert.ToBase64String(raw));
        }
    }
}
