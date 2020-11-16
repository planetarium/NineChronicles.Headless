using System;
using System.Collections.Generic;
using System.Reactive.Linq;
using System.Reactive.Subjects;
using Libplanet;
using NineChronicles.RPC.Shared.Exceptions;

namespace NineChronicles.Standalone
{
    public class ExceptionRenderer
    {
        // FIXME: Need to move to NodeStatusRenderer
        public readonly Subject<Address> AgentAddressSubject = new Subject<Address>();

        public readonly Subject<(RPCException, string)> ExceptionRenderSubject = new Subject<(RPCException, string)>();

        public void RenderAgentAddress(Address agentAddress)
        {
            AgentAddressSubject.OnNext(agentAddress);
        }

        public void RenderException(RPCException code, string msg)
        {
            ExceptionRenderSubject.OnNext((code, msg));
        }

        public IObservable<Address> EveryAgentAddress() => AgentAddressSubject.AsObservable();

        public IObservable<(RPCException, string)> EveryException() => ExceptionRenderSubject.AsObservable();
    }
}
