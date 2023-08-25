using GraphQL.Types;

namespace NineChronicles.Headless.GraphTypes
{
    public class ActivationStatusQuery : ObjectGraphType
    {
        public ActivationStatusQuery(StandaloneContext standaloneContext)
        {
            DeprecationReason = "Since NCIP-15, it doesn't care account activation.";
        }
    }
}
