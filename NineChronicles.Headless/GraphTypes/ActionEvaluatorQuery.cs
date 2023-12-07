using System.Security.Cryptography;
using GraphQL;
using GraphQL.Types;
using Libplanet.Common;
using Libplanet.Explorer.GraphTypes;
using Libplanet.Types.Blocks;
using NineChronicles.Headless.GraphTypes.States;

namespace NineChronicles.Headless.GraphTypes
{
    public class ActionEvaluatorQuery : ObjectGraphType
    {
        public ActionEvaluatorQuery(StandaloneContext standaloneContext)
        {
            Field<NonNullGraphType<ListGraphType<CommittedActionEvaluationType>>>(
                name: "evaluate",
                arguments: new QueryArguments(
                    new QueryArgument<NonNullGraphType<ByteStringType>>
                    {
                        Name = "blockHash"
                    },
                    new QueryArgument<NonNullGraphType<HashDigestSHA256Type>>
                    {
                        Name = "stateRootHash"
                    }
                ),
                resolve: context =>
                {
                    var aev = standaloneContext.NineChroniclesNodeService!.ActionEvaluator;
                    var blockChain = standaloneContext.BlockChain!;
                    byte[] blockHashByteArray = context.GetArgument<byte[]>("blockHash");
                    var blockHash = new BlockHash(blockHashByteArray);
                    var block = blockChain[blockHash];
                    HashDigest<SHA256> stateRootHash = context.GetArgument<HashDigest<SHA256>>("stateRootHash");
                    var evaluations = aev.Evaluate(block, stateRootHash);
                    return evaluations;
                });
        }
    }
}
