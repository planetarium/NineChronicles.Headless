using System.Collections.Immutable;
using System.Reflection;
using Bencodex;
using Bencodex.Types;
using Libplanet.Action;
using Libplanet.Action.Loader;
using Libplanet.Blockchain;
using Libplanet.Extensions.ActionEvaluatorCommonComponents;
using Libplanet.Extensions.RemoteActionEvaluator;
using Microsoft.AspNetCore.Mvc;
using Nekoyume.Action;

namespace Lib9c.StateService.Controllers;

[ApiController]
[Route("/evaluation")]
public class RemoteEvaluationController : ControllerBase
{
    private readonly IBlockChainStates _blockChainStates;
    private readonly ILogger<RemoteEvaluationController> _logger;
    private readonly Codec _codec;

    public RemoteEvaluationController(
        IBlockChainStates blockChainStates,
        ILogger<RemoteEvaluationController> logger,
        Codec codec)
    {
        _blockChainStates = blockChainStates;
        _logger = logger;
        _codec = codec;
    }

    [HttpPost]
    public ActionResult<RemoteEvaluationResponse> GetEvaluation([FromBody] RemoteEvaluationRequest request)
    {
        var decoded = _codec.Decode(request.PreEvaluationBlock);
        if (decoded is not Dictionary dictionary)
        {
            return StatusCode(StatusCodes.Status400BadRequest);
        }

        var preEvaluationBlock = PreEvaluationBlockMarshaller.Unmarshal(dictionary);
        var actionEvaluator =
            new ActionEvaluator(
                context => new RewardGold(),
                _blockChainStates,
                new SingleActionLoader(typeof(PolymorphicAction<ActionBase>)),
                null);
        return Ok(new RemoteEvaluationResponse
        {
            Evaluations = actionEvaluator.Evaluate(preEvaluationBlock).Select(ActionEvaluationMarshaller.Serialize)
                .ToArray(),
        });
    }
}
