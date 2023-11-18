using System;
using System.Collections.Generic;
using System.Linq;
using GraphQL.Types;

namespace NineChronicles.Headless.GraphTypes;

public class TableCsvType : ObjectGraphType<Dictionary<string, string>>
{
    class TableValueType : ObjectGraphType<(string, byte[])>
    {
        public TableValueType()
        {
            Field<NonNullGraphType<StringGraphType>>("tableName", resolve: context => context.Source.Item1);
            Field<NonNullGraphType<ListGraphType<ByteGraphType>>>("tableCsv", resolve: context => context.Source.Item2);
        }
    }

    public TableCsvType()
    {
        Field<NonNullGraphType<ListGraphType<ListGraphType<StringGraphType>>>>(
            "tables",
            resolve: context =>
            {
                return context.Source.Keys.Select(k => new[] { k, context.Source[k], });
                // return context.Source.Select(pair => (pair.Key, pair.Value)).ToList();
            });
    }
}
