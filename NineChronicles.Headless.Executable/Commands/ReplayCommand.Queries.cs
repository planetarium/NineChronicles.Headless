using GraphQL;
using GraphQL.Client.Abstractions;

namespace NineChronicles.Headless.Executable.Commands;

public partial class ReplayCommand
{
    private GetTransactionDataResponse? GetTransactionData(IGraphQLClient client, string id)
    {
        const string transactionQuery = @"
        query GetTransactionData($id: ID!, $txId: TxId!)
        {
            chainQuery {
                transactionQuery {
                    transaction(id: $id) {
                        serializedPayload
                    }
                }
            }
            transaction {
                transactionResult(txId: $txId) {
                    txStatus
                    blockIndex
                    blockHash
                }
            }
        }
        ";

        var request = new GraphQLRequest(
            transactionQuery,
            operationName: "GetTransactionData",
            variables: new { id, txId = id });

        return client.SendQueryAsync<GetTransactionDataResponse>(request).Result.Data;
    }

    private GetTransactionDataResponse? GetBlockData(IGraphQLClient client, string hash)
    {
        const string query = @"
        query GetBlockData($hash: ID!)
        {
            chainQuery { blockQuery {
                block(hash: $hash) {
                    miner
                    preEvaluationHash
                    previousBlock {
                        hash
                    }
                }
            }}
        }
        ";

        var request = new GraphQLRequest(
            query,
            operationName: "GetBlockData",
            variables: new { hash });

        return client.SendQueryAsync<GetTransactionDataResponse>(request).Result.Data;
    }

#pragma warning disable S3459

    private sealed class GetTransactionDataResponse
    {
        public ChainQueryType? ChainQuery { get; set; }
        public TransactionType? Transaction { get; set; }
    }

    private sealed class ChainQueryType
    {
        public TransactionQueryType? TransactionQuery { get; set; }

        public BlockQueryType? BlockQuery { get; set; }
    }

    private sealed class BlockQueryType
    {
        public BlockType? Block { get; set; }
    }

    private sealed class BlockType
    {
        public string? Hash { get; set; }
        public string? Miner { get; set; }
        public string? PreEvaluationHash { get; set; }
        public BlockType? PreviousBlock { get; set; }
    }

    private sealed class TransactionQueryType
    {
        public TransactionQueryTransactionType? Transaction { get; set; }
    }

    private sealed class TransactionQueryTransactionType
    {
        public string? SerializedPayload { get; set; }
    }

    private sealed class TransactionType
    {
        public TransactionTransactionResultType? TransactionResult { get; set; }
    }

    private sealed class TransactionTransactionResultType
    {
        public string? TxStatus { get; set; }
        public int? BlockIndex { get; set; }
        public string? BlockHash { get; set; }
    }
#pragma warning restore S3459
}
