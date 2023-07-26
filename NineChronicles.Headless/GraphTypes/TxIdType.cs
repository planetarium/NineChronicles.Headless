using GraphQL.Language.AST;
using GraphQL.Types;
using Libplanet.Common;
using Libplanet.Types.Tx;
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

        public override object? ParseLiteral(IValue value)
        {
            if (value is StringValue)
            {
                return ParseValue(value.Value);
            }

            return null;
        }
    }
}
