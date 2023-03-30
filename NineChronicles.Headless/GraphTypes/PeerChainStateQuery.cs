using System;
using System.Collections.Generic;
using System.Linq;
using GraphQL.Types;
using Log = Serilog.Log;

namespace NineChronicles.Headless.GraphTypes
{
    public class PeerChainStateQuery : ObjectGraphType
    {
        public PeerChainStateQuery(StandaloneContext standaloneContext)
        {
            Field<NonNullGraphType<ListGraphType<StringGraphType>>>(
                name: "state",
                description: "Summary of other peers connected to this node. It consists of address, chain height, and total difficulty.",
                resolve: context =>
                {
                    var service = standaloneContext.NineChroniclesNodeService;
                    if (service is null)
                    {
                        Log.Error($"{nameof(NineChroniclesNodeService)} is null.");
                        return null;
                    }

                    var swarm = service.Swarm;
                    if (!(swarm?.BlockChain is { } chain))
                    {
                        throw new InvalidOperationException($"{nameof(swarm.BlockChain)} is null.");
                    }

                    var chainStates = new List<string>
                    {
                        $"{swarm.AsPeer.Address}, {chain.Tip.Index}"
                    };

                    var peerChainState = swarm.GetPeerChainStateAsync(
                        TimeSpan.FromSeconds(5), default)
                        .Result
                        .Select(
                            state => $"{state.Peer.Address}, {state.TipIndex}");

                    chainStates.AddRange(peerChainState);

                    return chainStates;
                }
            );
        }
    }
}
