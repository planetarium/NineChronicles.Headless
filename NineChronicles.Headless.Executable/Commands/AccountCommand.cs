using System;
using System.Collections.Generic;
using System.Linq;
using Bencodex;
using Cocona;
using Libplanet.Action.State;
using Libplanet.Blockchain;
using Libplanet.Crypto;
using Libplanet.Store;
using Libplanet.Types.Assets;
using Libplanet.Types.Blocks;
using Libplanet.Types.Tx;
using Nekoyume.Action;
using Nekoyume.Model.State;
using Nekoyume.Module;
using NineChronicles.Headless.Executable.IO;
using Serilog.Core;
using static NineChronicles.Headless.NCActionUtils;
using DevExUtils = Lib9c.DevExtensions.Utils;

namespace NineChronicles.Headless.Executable.Commands
{
    public class AccountCommand : CoconaLiteConsoleAppBase
    {
        private static readonly Codec Codec = new Codec();
        private readonly IConsole _console;

        public AccountCommand(IConsole console)
        {
            _console = console;
        }

        [Command(Description = "Query accounts' balances.")]
        public void Balance(
            [Option('v', Description = "Print more logs.")]
            bool verbose,
            [Option('s', Description = "Path to the chain store.")]
            string storePath,
            [Option(
                'b',
                Description = "Optional block hash/index offset to query balances at.  " +
                              "Tip by default.")]
            string? block = null,
            [Option('c', Description = "Optional chain ID.  Default is the canonical chain ID.")]
            Guid? chainId = null,
            [Argument(Description = "Account address.")]
            string? address = null
        )
        {
            using Logger logger = DevExUtils.ConfigureLogger(verbose);
            (BlockChain chain, IStore store, _, _) =
                DevExUtils.GetBlockChain(logger, storePath, chainId);

            Block offset = DevExUtils.ParseBlockOffset(chain, block);
            _console.Error.WriteLine("The offset block: #{0} {1}.", offset.Index, offset.Hash);

            IWorldState worldState = chain.GetWorldState(offset.Hash);
            Bencodex.Types.Dictionary goldCurrencyStateDict = (Bencodex.Types.Dictionary)
                worldState.GetLegacyState(GoldCurrencyState.Address);
            GoldCurrencyState goldCurrencyState = new GoldCurrencyState(goldCurrencyStateDict);
            Currency gold = goldCurrencyState.Currency;

            if (address is { } addrStr)
            {
                Address addr = DevExUtils.ParseAddress(addrStr);
                FungibleAssetValue balance = worldState.GetBalance(addr, gold);
                _console.Out.WriteLine("{0}\t{1}", addr, balance);
                return;
            }

            var printed = new HashSet<Address>();
            foreach (BlockHash blockHash in chain.BlockHashes)
            {
                BlockDigest digest = GetBlockDigest(store, blockHash);
                _console.Error.WriteLine("Scanning block #{0} {1}...", digest.Index, digest.Hash);
                _console.Error.Flush();
                IEnumerable<Address> addrs = digest.TxIds
                    .Select(txId => store.GetTransaction(new TxId(txId.ToArray())))
                    .SelectMany(tx => tx.Actions is { } ca
                        ? ca.Select(a => ToAction(a))
                            .SelectMany(a =>
                            {
                                if (a is TransferAsset t)
                                {
                                    return new[] { t.Sender, t.Recipient };
                                }

                                if (a is InitializeStates i && i.GoldDistributions is { } l)
                                {
                                    return l.OfType<Bencodex.Types.Dictionary>()
                                        .Select(d => new GoldDistribution(d).Address);
                                }

                                return new Address[0];
                            })
                        : Enumerable.Empty<Address>())
                    .Append(digest.Miner);
                foreach (Address addr in addrs)
                {
                    if (!printed.Contains(addr))
                    {
                        FungibleAssetValue balance = worldState.GetBalance(addr, gold);
                        _console.Out.WriteLine("{0}\t{1}", addr, balance);
                        printed.Add(addr);
                    }
                }
            }
        }

        private static BlockDigest GetBlockDigest(IStore store, BlockHash blockHash)
        {
            BlockDigest? digest = store.GetBlockDigest(blockHash);
            if (digest is { } d)
            {
                return d;
            }

            throw new InvalidOperationException($"Block #{blockHash} is not found in the store.");
        }
    }
}
