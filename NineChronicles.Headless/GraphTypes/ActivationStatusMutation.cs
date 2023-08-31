using GraphQL.Types;

namespace NineChronicles.Headless.GraphTypes
{
    public class ActivationStatusMutation : ObjectGraphType
    {
        public ActivationStatusMutation(NineChroniclesNodeService service)
        {
            DeprecationReason = "Since NCIP-15, it doesn't care account activation.";

            Field<NonNullGraphType<BooleanGraphType>>(
                "activateAccount",
                deprecationReason: "Since NCIP-15, it doesn't care account activation.",
                arguments: new QueryArguments(
                    new QueryArgument<NonNullGraphType<StringGraphType>>
                    {
                        Name = "encodedActivationKey",
                    }),
                resolve: context => false);
        }
    }
}
