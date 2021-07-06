using Libplanet.Action;
using Libplanet.Blockchain;
using Libplanet.Blocks;
using Libplanet;
using System.Numerics;
using System.Collections.Generic;
using System.Linq;
using System;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace NineChronicles.Headless
{
    public static class DictionaryExtensions
    {
        public static Bencodex.Types.Dictionary AsBencodex(this Dictionary<string, object> dictionary)
        {
            var result = new Bencodex.Types.Dictionary();
            var hexChecker = new Regex(@"^(0x|0X)?[a-fA-F0-9]+$");
            foreach(var pair in dictionary)
            {
                result = (Bencodex.Types.Dictionary)result.Add((Bencodex.Types.IKey)(Bencodex.Types.Text)pair.Key, pair.Value switch {
                    JsonElement { ValueKind: JsonValueKind.Number } jsonElement => (Bencodex.Types.Integer)jsonElement.GetInt64(),
                    JsonElement { ValueKind: JsonValueKind.String } jsonElement when hexChecker.IsMatch(jsonElement.GetString()) => (Bencodex.Types.Binary)ByteUtil.ParseHex(jsonElement.GetString()),
                    JsonElement { ValueKind: JsonValueKind.String } jsonElement => (Bencodex.Types.Text)jsonElement.GetString(),
                    JsonElement { ValueKind: JsonValueKind.Object } jsonElement => JsonSerializer.Deserialize<Dictionary<string, object>>(jsonElement.GetRawText()).AsBencodex(),
//                    JsonElement { ValueKind: JsonValueKind.Array } jsonElement => (Bencodex.Types.List)jsonElement.EnumerateArray().ToList,
//                    JsonElement { ValueKind: JsonValueKind.Object } jsonElement when jsonElement.GetBoolean() == true => JsonSerializer.Deserialize<Dictionary<string, object>>(jsonElement.GetRawText()).AsBencodex(),
                    _ => throw new NotSupportedException()
                });
            }

            return result;
        }
    }
}
