using GraphQL.Types;
using Nekoyume.Model.Mail;

namespace NineChronicles.Headless.GraphTypes.States.Models.Mail
{
    public class MailBoxType : ObjectGraphType<MailBox>
    {
        public MailBoxType()
        {
            Field<NonNullGraphType<IntGraphType>>(nameof(MailBox.Count));
            Field<NonNullGraphType<ListGraphType<NonNullGraphType<MailType>>>>("mails", resolve: context => context.Source);
        }
    }
}
