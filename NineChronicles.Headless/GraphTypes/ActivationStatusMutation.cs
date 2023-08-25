using GraphQL.Types;

namespace NineChronicles.Headless.GraphTypes
{
    public class ActivationStatusMutation : ObjectGraphType
    {
        public ActivationStatusMutation(NineChroniclesNodeService service)
        {
            DeprecationReason = "Since NCIP-15, it doesn't care account activation.";
        }
    }
}
