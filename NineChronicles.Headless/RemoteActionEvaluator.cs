using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using Bencodex;
using Bencodex.Types;
using Lib9c.MessagePack.Action;
using Libplanet.Action;
using Libplanet.Blockchain;
using Libplanet.Blocks;
using Libplanet.Tx;
using MessagePack;

namespace NineChronicles.Headless;

public class RemoteActionEvaluator<T> : IActionEvaluator
    where T : IAction, new()
{
    private readonly Uri _endpoint;

    public RemoteActionEvaluator(Uri endpoint)
    {
        _endpoint = endpoint;
    }

    public IReadOnlyList<ActionEvaluation> Evaluate(IPreEvaluationBlock block)
    {
        using var httpClient = new HttpClient();
        var codec = new Codec();

        var dict = BlockMarshaler.MarshalBlock(BlockMarshaler.MarshalPreEvaluationBlockHeader(block),
            BlockMarshaler.MarshalTransactions<T>(block.Transactions.Cast<Transaction<T>>().ToList()));
        var bytes = httpClient.PostAsync(new Uri(_endpoint, "/evaluation"), new ByteArrayContent(codec.Encode(dict)))
            .Result.Content.ReadAsByteArrayAsync().Result;
        var rawEvals = (List)codec.Decode(bytes);

        Libplanet.Action.ActionEvaluation ToActionEvaluation(RemoteActionEvaluation remoteActionEvaluation)
        {
            var action = new T();
            action.LoadPlainValue(remoteActionEvaluation.Action);
            return new ActionEvaluation(
                action,
                remoteActionEvaluation.InputContext,
                remoteActionEvaluation.OutputStates,
                remoteActionEvaluation.Exception,
                remoteActionEvaluation.Logs
            );
        }

        return rawEvals.Cast<Binary>()
            .Select(bs =>
                ToActionEvaluation(MessagePackSerializer.Deserialize<RemoteActionEvaluation>(bs.ToByteArray())))
            .ToList();
    }
}
