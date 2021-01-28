using System;
using Libplanet.Net;
using Libplanet.Headless;
using NineChronicles.Headless.Properties;

namespace NineChronicles.Headless
{
    public static class StandaloneServices
    {
        public static NineChroniclesNodeService CreateHeadless(
            NineChroniclesNodeServiceProperties properties,
            StandaloneContext standaloneContext = null,
            bool ignoreBootstrapFailure = true,
            bool ignorePreloadFailure = true,
            bool strictRendering = false,
            bool authorizedMiner = false,
            bool isDev = false,
            int blockInterval = 10000,
            int reorgInterval = 0
        )
        {
            Progress<PreloadState> progress = null;
            if (!(standaloneContext is null))
            {
                progress = new Progress<PreloadState>(state =>
                {
                    standaloneContext.PreloadStateSubject.OnNext(state);
                });
            }

            properties.Libplanet.DifferentAppProtocolVersionEncountered =
                (Peer peer, AppProtocolVersion peerVersion, AppProtocolVersion localVersion) =>
                {
                    standaloneContext.DifferentAppProtocolVersionEncounterSubject.OnNext(
                        new DifferentAppProtocolVersionEncounter
                        {
                            Peer = peer,
                            PeerVersion = peerVersion,
                            LocalVersion = localVersion,
                        }
                    );

                    // FIXME: 일단은 버전이 다른 피어는 마주쳐도 쌩깐다.
                    return false;
                };

            properties.Libplanet.NodeExceptionOccurred =
                (code, message) =>
                {
                    standaloneContext.NodeExceptionSubject.OnNext(
                        new NodeException(code, message)
                    );
                };

            var service = new NineChroniclesNodeService(
                properties.Libplanet,
                properties.Rpc,
                preloadProgress: progress,
                ignoreBootstrapFailure: ignoreBootstrapFailure,
                ignorePreloadFailure: ignorePreloadFailure,
                strictRendering: strictRendering,
                isDev: isDev,
                blockInterval: blockInterval,
                reorgInterval: reorgInterval,
                authorizedMiner: authorizedMiner);
            service.ConfigureStandaloneContext(standaloneContext);

            return service;
        }

        internal static void ConfigureStandaloneContext(this NineChroniclesNodeService service, StandaloneContext standaloneContext)
        {
            if (!(standaloneContext is null))
            {
                standaloneContext.BlockChain = service.Swarm.BlockChain;
                standaloneContext.Store = service.Store;
                service.BootstrapEnded.WaitAsync().ContinueWith((task) =>
                {
                    standaloneContext.BootstrapEnded = true;
                    standaloneContext.NodeStatusSubject.OnNext(standaloneContext.NodeStatus);
                });
                service.PreloadEnded.WaitAsync().ContinueWith((task) =>
                {
                    standaloneContext.PreloadEnded = true;
                    standaloneContext.NodeStatusSubject.OnNext(standaloneContext.NodeStatus);
                });
            }
        }
    }
}
