#nullable enable

using System.Collections.Generic;
using System.Security.Cryptography;
using GraphQL;
using GraphQL.Types;
using Libplanet;
using Nekoyume.TableData;

namespace NineChronicles.Headless.GraphTypes;

public class FungibleItemIdInputType : InputObjectGraphType<(string? fungibleItemId, int? itemSheetId)>
{
    public static void SetFields<T>(ComplexGraphType<T> graphType)
    {
        graphType.Field<StringGraphType>(
            name: "fungibleItemId",
            description: "A fungible item id to be loaded.");

        graphType.Field<StringGraphType>(
            name: "itemSheetId",
            description: "(not recommended)A item sheet id to be loaded.\n" +
                         "It can be does not match with the actual fungible item id " +
                         "if the item sheets were patched.");
    }

    public static (string? fungibleItemId, int? itemSheetId) Parse(
        IDictionary<string, object?> value)
    {
        if (value.TryGetValue("fungibleItemId", out var fungibleItemId))
        {
            if (value.TryGetValue("itemSheetId", out _))
            {
                throw new ExecutionError("fungibleItemId and itemSheetId cannot be specified at the same time.");
            }

            return ((string)fungibleItemId!, null);
        }

        if (value.TryGetValue("itemSheetId", out var itemSheetId))
        {
            return (null, int.Parse((string)itemSheetId!));
        }

        throw new ExecutionError("fungibleItemId or itemSheetId must be specified.");
    }

    public FungibleItemIdInputType()
    {
        Name = "FungibleItemIdInput";
        Description = "A fungible item id to be loaded. Use either fungibleItemId or itemSheetId.";
        SetFields(this);
    }

    public override object ParseDictionary(IDictionary<string, object?> value)
    {
        return Parse(value);
    }
}
