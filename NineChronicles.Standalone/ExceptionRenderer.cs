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
        public readonly Subject<(Address agentAddress, List<Address> avatarAddresses)> AgentAndAvatarAddressesSubject =
            new Subject<(Address agentAddress, List<Address> avatarAddresses)>();

        public readonly Subject<(RPCException, string)> ExceptionRenderSubject = new Subject<(RPCException, string)>();

        public void RenderAgentAndAvatarAddresses(Address agentAddress, List<Address> avatarAddresses)
        {
            AgentAndAvatarAddressesSubject.OnNext((agentAddress, avatarAddresses));
        }

        public void RenderException(RPCException code, string msg)
        {
            ExceptionRenderSubject.OnNext((code, msg));
        }

        public IObservable<(Address agentAddress, List<Address> avatarAddresses)> EveryAgentAndAvatarAddresses() =>
            AgentAndAvatarAddressesSubject.AsObservable();

        public IObservable<(RPCException, string)> EveryException() => ExceptionRenderSubject.AsObservable();
    }
}
