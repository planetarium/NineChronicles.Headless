using System;
using System.Reactive.Linq;
using System.Reactive.Subjects;
using NineChronicles.RPC.Shared.Exceptions;

namespace NineChronicles.Headless
{
    public class ExceptionRenderer
    {
        public readonly Subject<(RPCException, string)> ExceptionRenderSubject = new Subject<(RPCException, string)>();

        public void RenderException(RPCException code, string msg)
        {
            ExceptionRenderSubject.OnNext((code, msg));
        }

        public IObservable<(RPCException, string)> EveryException() => ExceptionRenderSubject.AsObservable();
    }
}
