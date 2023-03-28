using System;
using System.Collections.Immutable;
using System.Text.Json;
using Bencodex;
using Libplanet;
using Libplanet.Action;
using Libplanet.Assets;
using Libplanet.Blockchain;
using Libplanet.Blocks;
using Microsoft.AspNetCore.Mvc;
using Nekoyume.Action;
using NineChronicles.Headless.Responses;

namespace NineChronicles.Headless.Controllers;

[ApiController]
public class StateController : ControllerBase
{
    private readonly IBlockChainStates _blockChainStates;

    public StateController(IBlockChainStates blockChainStates)
    {
        _blockChainStates = blockChainStates;
    }

    [Route("/state/{address}")]
    [HttpGet]
    public StateQueryResult GetState(string address, string offset)
    {
        var state = _blockChainStates.GetStates(
            ImmutableArray<Address>.Empty.Add(new Address(address)),
            BlockHash.FromString(offset))[0];
        return new StateQueryResult
        {
            Result = state is null ? null : Convert.ToBase64String(new Codec().Encode(state)),
        };
    }

    [Route("/balance/{address}/{currency}")]
    [HttpGet]
    public StateQueryResult GetBalance(string address, string currency, string offset)
    {
        return new StateQueryResult
        {
            Result = JsonSerializer.Serialize(_blockChainStates.GetBalance(
                new Address(address),
                new Currency(new Codec().Decode(ByteUtil.ParseHex(currency))),
                BlockHash.FromString(offset))),
        };
    }
}
