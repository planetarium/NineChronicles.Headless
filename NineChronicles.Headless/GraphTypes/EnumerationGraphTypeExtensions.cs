using System;
using System.Linq;
using GraphQL;
using GraphQL.Types;

namespace NineChronicles.Headless.GraphTypes;

public static class EnumerationGraphTypeExtensions
{
    public static void AddDeprecatedNames<TClrEnum>(
        this EnumerationGraphType<TClrEnum> graphEnumType,
        Func<string, string>? caseConverter = null)
        where TClrEnum : struct, Enum
    {
        EnumValueDefinition[] officialNames = graphEnumType.Values.ToArray();
        foreach (string name in Enum.GetNames<TClrEnum>())
        {
            TClrEnum member = Enum.Parse<TClrEnum>(name);
            string deprecatedName = caseConverter is { } convert ? convert(name) : name;
            graphEnumType.Add(
                deprecatedName,
                member,
                deprecationReason: $"Member name `{deprecatedName}` is deprecated as it does " +
                                   $"not follow GraphQL naming convention.  " +
                                   $"Use `{name.ToConstantCase()}` instead."
            );
        }

        // As EnumerationGraphType<T>.Add() overwrites underlying value-to-name
        // table, we need to overwrite the table with official names.  Without
        // this, enum values are represented with deprecated names in GraphQL:
        foreach (EnumValueDefinition def in officialNames)
        {
            graphEnumType.Add(def);
        }
    }
}
