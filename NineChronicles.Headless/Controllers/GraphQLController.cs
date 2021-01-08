using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Threading.Tasks;
using Bencodex.Types;
using Libplanet;
using Libplanet.KeyStore;
using Microsoft.AspNetCore.Mvc;
using Nekoyume;
using Nekoyume.Action;
using Nekoyume.Model.State;
using Libplanet.Crypto;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Hosting;
using Nekoyume.Model.Item;
using NineChronicles.Headless.GraphTypes;
using NineChronicles.Headless.Requests;
using Serilog;


namespace NineChronicles.Headless.Controllers
{
    [ApiController]
    public class GraphQLController : ControllerBase
    {
        private ConcurrentDictionary<Address, long> NotificationRecords { get; }
            = new ConcurrentDictionary<Address, long>();
        private StandaloneContext StandaloneContext { get; }
        private Address _address;

        public const string RunStandaloneEndpoint = "/run-standalone";

        public const string SetPrivateKeyEndpoint = "/set-private-key";

        public const string SetMiningEndpoint = "/set-mining";

        public const string CheckPeerEndpoint = "/check-peer";

        public GraphQLController(StandaloneContext standaloneContext)
        {
            StandaloneContext = standaloneContext;
        }

        [HttpPost(RunStandaloneEndpoint)]
        public IActionResult RunStandalone()
        {
            if (StandaloneContext.NineChroniclesNodeService is null)
            {
                // Waiting node service.
                return new StatusCodeResult(StatusCodes.Status409Conflict);
            }

            try
            {
                IHostBuilder nineChroniclesNodeHostBuilder = Host.CreateDefaultBuilder();
                nineChroniclesNodeHostBuilder =
                    StandaloneContext.NineChroniclesNodeService.Configure(
                        nineChroniclesNodeHostBuilder);
                // FIXME: StandaloneContext has both service and blockchain, which is duplicated.
                StandaloneContext.BlockChain =
                    StandaloneContext.NineChroniclesNodeService.Swarm.BlockChain;
                StandaloneContext.NineChroniclesNodeService.BlockRenderer.EveryBlock()
                    .Subscribe(pair => NotifyRefillActionPoint(pair.NewTip.Index));
                StandaloneContext.NineChroniclesNodeService.ActionRenderer.EveryRender<ActionBase>()
                    .Subscribe(NotifyAction);
                nineChroniclesNodeHostBuilder
                    .RunConsoleAsync()
                    .ContinueWith(task =>
                    {
                        if (task.IsFaulted)
                        {
                            Log.Error(
                                task.Exception,
                                "An unexpected error occurred while running NineChroniclesNodeService.",
                                task.Exception);
                        }
                    });
            }
            catch (Exception e)
            {
                // Unexpected Error.
                Log.Warning(e, "Failed to launch node service. {e}", e);
                return new StatusCodeResult(StatusCodes.Status503ServiceUnavailable);
            }

            return Ok("Node service started.");
        }

        [HttpPost(SetPrivateKeyEndpoint)]
        public IActionResult SetPrivateKey([FromBody] SetPrivateKeyRequest request)
        {
            if (StandaloneContext.NineChroniclesNodeService is null)
            {
                // Waiting node service.
                return new StatusCodeResult(StatusCodes.Status409Conflict);
            }

            var privateKey = new PrivateKey(ByteUtil.ParseHex(request.PrivateKeyString));
            StandaloneContext.NineChroniclesNodeService.PrivateKey = privateKey;
            _address = privateKey.PublicKey.ToAddress();
            var msg = $"Private key set ({privateKey.PublicKey.ToAddress()}).";
            Log.Debug(msg);
            return Ok(msg);
        }

        [HttpPost(SetMiningEndpoint)]
        public IActionResult SetMining([FromBody] SetMiningRequest request)
        {
            if (StandaloneContext.NineChroniclesNodeService is null)
            {
                // Waiting node service.
                return new StatusCodeResult(StatusCodes.Status409Conflict);
            }

            bool mine = request.Mine;
            if (mine)
            {
                StandaloneContext.NineChroniclesNodeService.StartMining();
            }
            else
            {
                StandaloneContext.NineChroniclesNodeService.StopMining();
            }

            StandaloneContext.IsMining = mine;
            return Ok($"Set mining status to {mine}.");
        }

        [HttpPost(CheckPeerEndpoint)]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status409Conflict)]
        [ProducesResponseType(StatusCodes.Status500InternalServerError)]
        public async Task<IActionResult> CheckPeer([FromBody] CheckPeerRequest request)
        {
            if (StandaloneContext.NineChroniclesNodeService is null)
            {
                // Waiting node service.
                return new StatusCodeResult(StatusCodes.Status409Conflict);
            }

            try
            {
                var exist = await StandaloneContext.NineChroniclesNodeService.CheckPeer(request.AddressString);
                if (exist)
                {
                    return Ok($"Found peer {request.AddressString}.");
                }
                
                return BadRequest($"No such peer {request.AddressString}");
            }
            catch (Exception e)
            {
                var msg = $"Unexpected error occurred during CheckPeer request. {e}";
                // Unexpected Error.
                Log.Warning(e, msg);
                return StatusCode(StatusCodes.Status500InternalServerError, msg);
            }
        }

        //TODO : This should be covered in test.
        private void NotifyRefillActionPoint(long newTipIndex)
        {
            List<Tuple<Guid, ProtectedPrivateKey>> tuples =
                StandaloneContext.KeyStore.List().ToList();
            if (!tuples.Any())
            {
                return;
            }

            IEnumerable<Address> playerAddresses = tuples.Select(tuple => tuple.Item2.Address);
            var chain = StandaloneContext.BlockChain;

            List<IValue> states = playerAddresses
                .Select(addr => chain.GetState(addr))
                .Where(value => !(value is null))
                .ToList();

            if (!states.Any())
            {
                return;
            }

            var agentStates =
                states.Select(state => new AgentState((Bencodex.Types.Dictionary) state));
            var avatarStates = agentStates.SelectMany(agentState =>
                agentState.avatarAddresses.Values.Select(address =>
                    new AvatarState((Bencodex.Types.Dictionary) chain.GetState(address))));
            var gameConfigState =
                new GameConfigState((Bencodex.Types.Dictionary) chain.GetState(Addresses.GameConfig));

            bool IsDailyRewardRefilled(long dailyRewardReceivedIndex)
            {
                return newTipIndex >= dailyRewardReceivedIndex + gameConfigState.DailyRewardInterval;
            }

            bool NeedsRefillNotification(AvatarState avatarState)
            {
                if (NotificationRecords.TryGetValue(avatarState.address, out long record))
                {
                    return avatarState.dailyRewardReceivedIndex != record
                           && IsDailyRewardRefilled(avatarState.dailyRewardReceivedIndex);
                }

                return IsDailyRewardRefilled(avatarState.dailyRewardReceivedIndex);
            }

            var avatarStatesToSendNotification = avatarStates
                .Where(NeedsRefillNotification)
                .ToList();

            if (avatarStatesToSendNotification.Any())
            {
                var notification = new Notification(NotificationEnum.Refill);
                StandaloneContext.NotificationSubject.OnNext(notification);
            }

            foreach (var avatarState in avatarStatesToSendNotification)
            {
                Log.Debug(
                    "Record notification for {AvatarAddress}",
                    avatarState.address.ToHex());
                NotificationRecords[avatarState.address] = avatarState.dailyRewardReceivedIndex;
            }
        }

        private void NotifyAction(ActionBase.ActionEvaluation<ActionBase> eval)
        {
            if (eval.OutputStates.UpdatedAddresses.Contains(_address))
            {
                if (eval.Signer == _address)
                {
                    var type = NotificationEnum.Refill;
                    var msg = string.Empty;
                    switch (eval.Action)
                    {
                        case HackAndSlash3 has:
                            type = NotificationEnum.HAS;
                            msg = has.stageId.ToString(CultureInfo.InvariantCulture);
                            break;
                        case CombinationConsumable2 _:
                            type = NotificationEnum.CombinationConsumable;
                            break;
                        case CombinationEquipment3 _:
                            type = NotificationEnum.CombinationEquipment;
                            break;
                        case Buy3 _:
                            type = NotificationEnum.Buyer;
                            break;
                    }
                    var notification = new Notification(type, msg);
                    StandaloneContext.NotificationSubject.OnNext(notification);
                }
                else
                {
                    if (eval.Action is Buy3 buy && buy.sellerAgentAddress == _address)
                    {
                        var notification = new Notification(NotificationEnum.Seller);
                        StandaloneContext.NotificationSubject.OnNext(notification);
                    }
                }
            }
        }
    }
}
