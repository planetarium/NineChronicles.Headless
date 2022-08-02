using System;
using System.Reactive.Linq;
using System.Reactive.Subjects;
using NineChronicles.RPC.Shared.Exceptions;

namespace NineChronicles.Headless
{
    public class NodeStatusRenderer
    {
        public readonly Subject<bool> PreloadStatusSubject = new Subject<bool>();

        public void PreloadStatus(bool isPreloadStarted = false)
        {
            PreloadStatusSubject.OnNext(isPreloadStarted);
        }

        public IObservable<bool> EveryChangedStatus() => PreloadStatusSubject.AsObservable();
    }
}
