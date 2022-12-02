using GraphQL.Types;

namespace NineChronicles.Headless.GraphTypes
{
    public class NotificationEnumType : EnumerationGraphType<NotificationEnum>
    {
        public NotificationEnumType()
        {
            this.AddDeprecatedNames();
        }
    }
}
