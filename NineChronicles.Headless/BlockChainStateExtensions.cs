using System.Collections.Generic;
using System.Security.Cryptography;
using Bencodex.Types;
using Libplanet.Action.State;
using Libplanet.Blockchain;
using Libplanet.Common;
using Libplanet.Crypto;
using Libplanet.Types.Blocks;

namespace NineChronicles.Headless;

public static class BlockChainStateExtensions
{
    public static IValue? GetState(
        this BlockChain blockChain,
        Address address,
        Address? account = null,
        BlockHash? blockHash = null) => blockHash is null 
            ? blockChain.GetWorldState().GetAccount(account ?? ReservedAddresses.LegacyAccount).GetState(address) 
            : blockChain.GetWorldState(blockHash).GetAccount(account ?? ReservedAddresses.LegacyAccount).GetState(address);

    public static IReadOnlyList<IValue?> GetStates(
        this BlockChain blockChain,
        IReadOnlyList<Address> addresses,
        Address? account = null,
        BlockHash? blockHash = null) => blockHash is null
            ? blockChain.GetWorldState().GetAccount(account ?? ReservedAddresses.LegacyAccount).GetStates(addresses)
            : blockChain.GetWorldState(blockHash).GetAccount(account ?? ReservedAddresses.LegacyAccount).GetStates(addresses);
}
