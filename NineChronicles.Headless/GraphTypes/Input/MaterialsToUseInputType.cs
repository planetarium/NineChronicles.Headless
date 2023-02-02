using System.Collections.Generic;
using GraphQL.Types;

namespace NineChronicles.Headless.GraphTypes.Input;

public class MaterialsToUseInputType : InputObjectGraphType<(int materialId, int quantity)>
{
    public MaterialsToUseInputType()
    {
        Field<NonNullGraphType<IntGraphType>>(
            name: "materialId",
            description: "Material ID to be used."
        );
        Field<NonNullGraphType<IntGraphType>>(
            name: "quantity",
            description: "Item quantity to be used."
        );
    }

    public override object ParseDictionary(IDictionary<string, object?> value)
    {
        return (
            (int)value["materialId"]!,
            (int)value["quantity"]!
        );
    }
}
