using GraphQL.Types;

namespace NineChronicles.Headless.GraphTypes
{
    public sealed class NotificationType : ObjectGraphType<Notification>
    {
        public NotificationType()
        {
            Field<NonNullGraphType<NotificationEnumType>>("type")
                .Description("The type of Notification.")
                .Resolve(context => context.Source.Type);

            Field<StringGraphType>("message")
                .Description("The message of Notification.")
                .Resolve(context => context.Source.Message);
        }
    }
}
