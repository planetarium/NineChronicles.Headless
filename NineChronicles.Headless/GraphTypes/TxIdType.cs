using GraphQL.Types;
using GraphQLParser.AST;
using Libplanet;
using Libplanet.Tx;
using System;

namespace NineChronicles.Headless.GraphTypes
{
    public class TxIdType : StringGraphType
    {
        public TxIdType()
        {
            Name = "TxId";
        }

        public override object? Serialize(object? value)
        {
            if (value is TxId txId)
            {
                return txId.ToString();
            }

            return value;
        }

        public override object? ParseValue(object? value)
        {
            switch (value)
            {
                case null:
                    return null;
                case string hex:
                    return new TxId(ByteUtil.ParseHex(hex));
                default:
                    throw new ArgumentException(
                        $"Expected a hexadecimal string but {value}", nameof(value));
            }
        }

        public override object? ParseLiteral(GraphQLValue? value) =>
            value is GraphQLStringValue v ? ParseValue((string)v.Value) : null;
    }
}
