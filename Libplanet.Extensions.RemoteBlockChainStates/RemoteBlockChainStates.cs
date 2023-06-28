using Bencodex;
using Bencodex.Types;
using GraphQL;
using GraphQL.Client.Http;
using GraphQL.Client.Serializer.SystemTextJson;
using Libplanet.Assets;
using Libplanet.Blockchain;
using Libplanet.Blocks;
using Libplanet.Consensus;
using Libplanet.Crypto;
using Libplanet.Store.Trie;

namespace Libplanet.Extensions.RemoteBlockChainStates
{
    public class RemoteBlockChainStates : IBlockChainStates
    {
        private readonly Uri _explorerEndpoint;
        private readonly GraphQLHttpClient _graphQlHttpClient;

        public RemoteBlockChainStates(Uri explorerEndpoint)
        {
            _explorerEndpoint = explorerEndpoint;
            _graphQlHttpClient =
                new GraphQLHttpClient(_explorerEndpoint, new SystemTextJsonSerializer());
        }

        public IReadOnlyList<IValue?> GetStates(IReadOnlyList<Address> addresses, BlockHash? offset)
        {
            var response = _graphQlHttpClient.SendQueryAsync<GetStatesResponseType>(
                new GraphQLRequest(
                    @"query GetStates($addresses: [Address!]!, $offsetBlockHash: ID!)
                {
                    stateQuery
                    {
                        states(addresses: $addresses, offsetBlockHash: $offsetBlockHash)
                    }
                }",
                    operationName: "GetStates",
                    variables: new
                    {
                        addresses = addresses.Select(x => x.ToString()).ToArray(),
                        offsetBlockHash = offset is { } hash
                            ? ByteUtil.Hex(hash.ByteArray)
                            : throw new NotSupportedException(),
                    })).Result;
            var codec = new Codec();
            return response.Data.StateQuery.States
                .Select(nullableState => nullableState is { } state ? codec.Decode(state) : null).ToList();
        }

        public FungibleAssetValue GetBalance(Address address, Currency currency, BlockHash? offset)
        {
            object? currencyInput = currency.TotalSupplyTrackable ? new
            {
                ticker = currency.Ticker,
                decimalPlaces = currency.DecimalPlaces,
                minters = currency.Minters?.Select(addr => addr.ToString()).ToArray(),
                totalSupplyTrackable = currency.TotalSupplyTrackable,
                maximumSupplyMajorUnit = currency.MaximumSupply.Value.MajorUnit,
                maximumSupplyMinorUnit = currency.MaximumSupply.Value.MinorUnit,
            } : new
            {
                ticker = currency.Ticker,
                decimalPlaces = currency.DecimalPlaces,
                minters = currency.Minters?.Select(addr => addr.ToString()).ToArray(),
                totalSupplyTrackable = currency.TotalSupplyTrackable,
            };
            var response = _graphQlHttpClient.SendQueryAsync<GetBalanceResponseType>(
                new GraphQLRequest(
            @"query GetBalance($owner: Address!, $currency: CurrencyInput!, $offsetBlockHash: ID!)
                {
                    stateQuery
                    {
                        balance(owner: $owner, currency: $currency, offsetBlockHash: $offsetBlockHash)
                        {
                            string
                        }
                    }
                }",
                operationName: "GetBalance",
                variables: new
                {
                    owner = address.ToString(),
                    currency = currencyInput,
                    offsetBlockHash = offset is { } hash
                        ? ByteUtil.Hex(hash.ByteArray)
                        : throw new NotSupportedException(),
                })).Result;

            return FungibleAssetValue.Parse(currency, response.Data.StateQuery.Balance.String.Split()[0]);
        }

        public FungibleAssetValue GetTotalSupply(Currency currency, BlockHash? offset)
        {
            object? currencyInput = currency.TotalSupplyTrackable ? new
            {
                ticker = currency.Ticker,
                decimalPlaces = currency.DecimalPlaces,
                minters = currency.Minters.Select(addr => addr.ToString()).ToArray(),
                totalSupplyTrackable = currency.TotalSupplyTrackable,
                maximumSupplyMajorUnit = currency.MaximumSupply.Value.MajorUnit,
                maximumSupplyMinorUnit = currency.MaximumSupply.Value.MinorUnit,
            } : new
            {
                ticker = currency.Ticker,
                decimalPlaces = currency.DecimalPlaces,
                minters = currency.Minters.Select(addr => addr.ToString()).ToArray(),
                totalSupplyTrackable = currency.TotalSupplyTrackable,
            };
            var response = _graphQlHttpClient.SendQueryAsync<GetTotalSupplyResponseType>(
                new GraphQLRequest(
                    @"query GetTotalSupply(currency: CurrencyInput!, $offsetBlockHash: ID!)
                {
                    stateQuery
                    {
                        totalSupply(currency: $currency, offsetBlockHash: $offsetBlockHash)
                        {
                            string
                        }
                    }
                }",
                    operationName: "GetTotalSupply",
                    variables: new
                    {
                        currency = currencyInput,
                        offsetBlockHash = offset is { } hash
                            ? ByteUtil.Hex(hash.ByteArray)
                            : throw new NotSupportedException(),
                    })).Result;

            return FungibleAssetValue.Parse(currency, response.Data.StateQuery.TotalSupply.String.Split()[0]);
        }

        public ValidatorSet GetValidatorSet(BlockHash? offset)
        {
            var response = _graphQlHttpClient.SendQueryAsync<GetValidatorsResponseType>(
                new GraphQLRequest(
                    @"query GetValidators($offsetBlockHash: ID!)
                {
                    stateQuery
                    {
                        validators(offsetBlockHash: $offsetBlockHash)
                        {
                            publicKey
                            power
                        }
                    }
                }",
                    operationName: "GetValidators",
                    variables: new
                    {
                        offsetBlockHash = offset is { } hash
                            ? ByteUtil.Hex(hash.ByteArray)
                            : throw new NotSupportedException(),
                    })).Result;

            return new ValidatorSet(response.Data.StateQuery.Validators
                .Select(x =>
                    new Validator(new PublicKey(ByteUtil.ParseHex(x.PublicKey)), x.Power))
                .ToList());
        }

        public IBlockStates GetBlockStates(BlockHash? offset)
        {
            throw new NotSupportedException();
        }

        public ITrie? GetTrie(BlockHash offset)
        {
            return null;
        }

        private class GetStatesResponseType
        {
            public StateQueryWithStatesType StateQuery { get; set; }
        }

        private class StateQueryWithStatesType
        {
            public byte[][] States { get; set; }
        }

        private class GetBalanceResponseType
        {
            public StateQueryWithBalanceType StateQuery { get; set; }
        }

        private class StateQueryWithBalanceType
        {
            public FungibleAssetValueWithStringType Balance { get; set; }
        }

        private class FungibleAssetValueWithStringType
        {
            public string String { get; set; }
        }

        private class GetTotalSupplyResponseType
        {
            public StateQueryWithTotalSupplyType StateQuery { get; set; }
        }

        private class StateQueryWithTotalSupplyType
        {
            public FungibleAssetValueWithStringType TotalSupply { get; set; }
        }

        private class GetValidatorsResponseType
        {
            public StateQueryWithValidatorsType StateQuery { get; set; }
        }

        private class StateQueryWithValidatorsType
        {
            public ValidatorType[] Validators { get; set; }
        }

        private class ValidatorType
        {
            public string PublicKey { get; set; }
            public long Power { get; set; }
        }
    }
}
