using GraphQL.Types;
using Libplanet.Explorer.GraphTypes;

namespace NineChronicles.Headless.GraphTypes
{
    public sealed class NotificationType : ObjectGraphType<Notification>
    {
        public NotificationType()
        {
            Field<NonNullGraphType<NotificationEnumType>>(
                name: "type",
                description: "The type of Notification.",
                resolve: context => context.Source.Type);

            Field<StringGraphType>(
                name: "message",
                description: "The message of Notification.",
                resolve: context => context.Source.Message);
            
            Field<NonNullGraphType<AddressType>>(
                name: "receiver",
                description: "The receiver of Notification.",
                resolve: context => context.Source.Receiver);
        }
    }
}
