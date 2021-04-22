using System;
using System.Reactive.Subjects;
using Bencodex.Types;
using Libplanet;
using Libplanet.Blockchain;
using Libplanet.KeyStore;
using Libplanet.Net;
using Libplanet.Headless;
using Libplanet.Store;
using Nekoyume.Action;
using Nekoyume.Model.State;
using NineChronicles.Headless.GraphTypes;
using Serilog;
using NineChroniclesActionType = Libplanet.Action.PolymorphicAction<Nekoyume.Action.ActionBase>;

namespace NineChronicles.Headless
{
    public class StandaloneContext
    {
        public BlockChain<NineChroniclesActionType>? BlockChain { get; set; }
        public IKeyStore? KeyStore { get; set; }
        public bool BootstrapEnded { get; set; }
        public bool PreloadEnded { get; set; }
        public bool IsMining { get; set; }
        public ReplaySubject<NodeStatusType> NodeStatusSubject { get; } = new ReplaySubject<NodeStatusType>();
        public ReplaySubject<PreloadState> PreloadStateSubject { get; } = new ReplaySubject<PreloadState>();
        public ReplaySubject<DifferentAppProtocolVersionEncounter> DifferentAppProtocolVersionEncounterSubject { get; }
            = new ReplaySubject<DifferentAppProtocolVersionEncounter>();
        public ReplaySubject<Notification> NotificationSubject { get; } = new ReplaySubject<Notification>(1);
        public ReplaySubject<NodeException> NodeExceptionSubject { get; } = new ReplaySubject<NodeException>();
        public ReplaySubject<StakingState> StakingStateSubject { get; } = new ReplaySubject<StakingState>();
        public NineChroniclesNodeService? NineChroniclesNodeService { get; private set; }
        public NodeStatusType NodeStatus => new NodeStatusType()
        {
            BootstrapEnded = BootstrapEnded,
            PreloadEnded = PreloadEnded,
            IsMining = IsMining,
        };

        public IStore? Store { get; internal set; }

        public void SetNineChroniclesNodeService(NineChroniclesNodeService? service)
        {
            NineChroniclesNodeService = service;
            if (!(NineChroniclesNodeService is null))
            {
                NineChroniclesNodeService.BlockRenderer.EveryBlock().Subscribe(pair => RenderBlock());
                NineChroniclesNodeService.ActionRenderer.EveryRender<ActionBase>().Subscribe(RenderAction);
                NineChroniclesNodeService.ActionRenderer.EveryRender<Stake>().Subscribe(RenderStake);
                NineChroniclesNodeService.ActionRenderer.EveryRender<CancelStaking>().Subscribe(RenderCancelStaking);
                NineChroniclesNodeService.ActionRenderer.EveryRender<ClaimStakingReward>().Subscribe(RenderClaimStakingReward);
            }
        }

        private void RenderBlock()
        {
            if (NineChroniclesNodeService is null)
            {
                throw new InvalidOperationException(
                    $"{nameof(StandaloneContext.NineChroniclesNodeService)} is null.");
            }

            if (NineChroniclesNodeService.MinerPrivateKey is null)
            {
                Log.Information("PrivateKey is not set. please call SetPrivateKey() first");
            }
        }

        private void RenderAction(ActionBase.ActionEvaluation<ActionBase> eval)
        {
            if (NineChroniclesNodeService is null)
            {
                throw new InvalidOperationException(
                    $"{nameof(StandaloneContext.NineChroniclesNodeService)} is null.");
            }

            if (NineChroniclesNodeService.MinerPrivateKey is null)
            {
                Log.Information("PrivateKey is not set. please call SetPrivateKey() first");
            }
        }

        private void RenderStake(ActionBase.ActionEvaluation<Stake> eval)
        {
            if (NineChroniclesNodeService is null)
            {
                throw new InvalidOperationException(
                    $"{nameof(NineChroniclesNodeService)} is null.");
            }

            if (NineChroniclesNodeService.MinerPrivateKey is null)
            {
                Log.Information("PrivateKey is not set. please call SetPrivateKey() first");
                return;
            }

            Address agentAddress = NineChroniclesNodeService.MinerPrivateKey.ToAddress();
            if (eval.Signer == agentAddress && eval.Exception is null)
            {
                Address stakingAddress = StakingState.DeriveAddress(agentAddress, eval.Action.stakingRound);
                if (eval.OutputStates.GetState(stakingAddress) is { } state)
                {
                    StakingState stakingState = new StakingState((Dictionary) state);
                    StakingStateSubject.OnNext(stakingState);
                }
            }
        }

        private void RenderCancelStaking(ActionBase.ActionEvaluation<CancelStaking> eval)
        {
            if (NineChroniclesNodeService is null)
            {
                throw new InvalidOperationException(
                    $"{nameof(NineChroniclesNodeService)} is null.");
            }

            if (NineChroniclesNodeService.MinerPrivateKey is null)
            {
                Log.Information("PrivateKey is not set. please call SetPrivateKey() first");
                return;
            }

            Address agentAddress = NineChroniclesNodeService.MinerPrivateKey.ToAddress();
            if (eval.Signer == agentAddress && eval.Exception is null)
            {
                Address stakingAddress = StakingState.DeriveAddress(agentAddress, eval.Action.stakingRound);
                if (eval.OutputStates.GetState(stakingAddress) is { } state)
                {
                    StakingState stakingState = new StakingState((Dictionary) state);
                    StakingStateSubject.OnNext(stakingState);
                }
            }
        }

        private void RenderClaimStakingReward(ActionBase.ActionEvaluation<ClaimStakingReward> eval)
        {
            if (NineChroniclesNodeService is null)
            {
                throw new InvalidOperationException(
                    $"{nameof(NineChroniclesNodeService)} is null.");
            }

            if (NineChroniclesNodeService.MinerPrivateKey is null)
            {
                Log.Information("PrivateKey is not set. please call SetPrivateKey() first");
                return;
            }

            Address agentAddress = NineChroniclesNodeService.MinerPrivateKey.ToAddress();
            if (eval.Signer == agentAddress && eval.Exception is null)
            {
                Address stakingAddress = StakingState.DeriveAddress(agentAddress, eval.Action.stakingRound);
                if (eval.OutputStates.GetState(stakingAddress) is { } state)
                {
                    StakingState stakingState = new StakingState((Dictionary) state);
                    StakingStateSubject.OnNext(stakingState);
                }
            }
        }
    }
}
