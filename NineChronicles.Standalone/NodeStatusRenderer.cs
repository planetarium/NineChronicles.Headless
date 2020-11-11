using System;
using System.Reactive.Linq;
using System.Reactive.Subjects;
using NineChronicles.RPC.Shared.Exceptions;

namespace NineChronicles.Standalone
{
    public class NodeStatusRenderer
    {
        public readonly Subject<bool> PreloadStatusSubject = new Subject<bool>();

        public void PreloadStatus(bool isPreloadEnded)
        {
            PreloadStatusSubject.OnNext(isPreloadEnded);
        }

        public IObservable<bool> EveryChangedStatus() => PreloadStatusSubject.AsObservable();
    }
}
