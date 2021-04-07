using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Security.Claims;
using System.Threading.Tasks;
using Bencodex.Types;
using Libplanet;
using Libplanet.KeyStore;
using Microsoft.AspNetCore.Mvc;
using Nekoyume;
using Nekoyume.Action;
using Nekoyume.Model.State;
using Libplanet.Crypto;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Session;
using Microsoft.Extensions.Configuration;
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
        private readonly IHttpContextAccessor _httpContextAccessor;
        private readonly IConfiguration _configuration;

        private ConcurrentDictionary<Address, long> NotificationRecords { get; }
            = new ConcurrentDictionary<Address, long>();
        private StandaloneContext StandaloneContext { get; }

        public const string SetPrivateKeyEndpoint = "/set-private-key";

        public const string SetMiningEndpoint = "/set-mining";

        public const string CheckPeerEndpoint = "/check-peer";

        public GraphQLController(StandaloneContext standaloneContext, IHttpContextAccessor httpContextAccessor, IConfiguration configuration)
        {
            _httpContextAccessor = httpContextAccessor;
            _configuration = configuration;
            StandaloneContext = standaloneContext;
        }

        [HttpPost(SetPrivateKeyEndpoint)]
        public IActionResult SetPrivateKey([FromBody] SetPrivateKeyRequest request)
        {
            if (!HasLocalPolicy())
            {
                return Unauthorized();
            }

            if (StandaloneContext.NineChroniclesNodeService is null)
            {
                // Waiting node service.
                return new StatusCodeResult(StatusCodes.Status409Conflict);
            }

            var privateKey = new PrivateKey(ByteUtil.ParseHex(request.PrivateKeyString));
            StandaloneContext.NineChroniclesNodeService.MinerPrivateKey = privateKey;
            var msg = $"Private key set ({StandaloneContext.NineChroniclesNodeService.MinerPrivateKey.PublicKey.ToAddress()}).";
            Log.Information("SetPrivateKey: {Msg}", msg);
            return Ok(msg);
        }

        [HttpPost(SetMiningEndpoint)]
        public IActionResult SetMining([FromBody] SetMiningRequest request)
        {
            if (!HasLocalPolicy())
            {
                return Unauthorized();
            }

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
            if (StandaloneContext.KeyStore is null)
            {
                throw new InvalidOperationException($"{nameof(StandaloneContext.KeyStore)} is null.");
            }

            List<Tuple<Guid, ProtectedPrivateKey>> tuples =
                StandaloneContext.KeyStore.List().ToList();
            if (!tuples.Any())
            {
                return;
            }

            IEnumerable<Address> playerAddresses = tuples.Select(tuple => tuple.Item2.Address);
            var chain = StandaloneContext.BlockChain;
            if (chain is null)
            {
                throw new InvalidOperationException($"{nameof(chain)} is null.");
            }

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

            foreach (var avatarState in avatarStatesToSendNotification)
            {
                Log.Debug(
                    "Record notification for {AvatarAddress}",
                    avatarState.address.ToHex());
                var notification = new Notification(NotificationEnum.Refill, avatarState.address);
                StandaloneContext.NotificationSubject.OnNext(notification);
                NotificationRecords[avatarState.address] = avatarState.dailyRewardReceivedIndex;
            }
        }

        private void NotifyAction(ActionBase.ActionEvaluation<ActionBase> eval)
        {
            List<Tuple<Guid, ProtectedPrivateKey>>? tuples =
                StandaloneContext.KeyStore?.List().ToList();
            if (tuples is null || !tuples.Any())
            {
                return;
            }

            var playerAddresses = tuples.Select(tuple => tuple.Item2.Address).ToHashSet();

            if (playerAddresses.Contains(eval.Signer))
            {
                var type = NotificationEnum.Refill;
                var msg = string.Empty;
                switch (eval.Action)
                {
                    case HackAndSlash4 has:
                        type = NotificationEnum.HAS;
                        msg = has.stageId.ToString(CultureInfo.InvariantCulture);
                        break;
                    case CombinationConsumable3 _:
                        type = NotificationEnum.CombinationConsumable;
                        break;
                    case CombinationEquipment4 _:
                        type = NotificationEnum.CombinationEquipment;
                        break;
                    case Buy4 _:
                        type = NotificationEnum.Buyer;
                        break;
                }
                Log.Information("NotifyAction: Type: {Type} MSG: {Msg}", type, msg);
                var notification = new Notification(type, eval.Signer, msg);
                StandaloneContext.NotificationSubject.OnNext(notification);
            }

            playerAddresses.IntersectWith(eval.OutputStates.UpdatedAddresses);
            if (playerAddresses.Any() && eval.Action is Buy4 buy)
            {
                foreach (Address address in playerAddresses)
                {
                    if (buy.sellerAgentAddress == address)
                    {
                        var notification = new Notification(NotificationEnum.Seller, address);
                        StandaloneContext.NotificationSubject.OnNext(notification);
                    }
                }
            }
        }

        // FIXME: remove this method with DI.
        private bool HasLocalPolicy() => !(_configuration[GraphQLService.SecretTokenKey] is { }) ||
                                         _httpContextAccessor.HttpContext.User.HasClaim("role", "Admin");
    }
}
