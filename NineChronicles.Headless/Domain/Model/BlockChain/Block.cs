using System;
using System.Collections.Generic;
using System.Security.Cryptography;
using Libplanet.Common;
using Libplanet.Crypto;
using Libplanet.Types.Blocks;
using Libplanet.Types.Tx;

namespace NineChronicles.Headless.Domain.Model.BlockChain;

using StateRootHash = HashDigest<SHA256>;

public record Block(
#pragma warning disable SA1313
    BlockHash Hash,
    BlockHash? PreviousHash,
    Address Miner,
    long Index,
    DateTimeOffset Timestamp,
    StateRootHash StateRootHash,
    IEnumerable<Transaction> Transactions);
#pragma warning restore SA1313
