using GraphQL.Types;

namespace NineChronicles.Headless.GraphTypes.States.Models.Mail
{
    public class MailType : ObjectGraphType<Nekoyume.Model.Mail.Mail>
    {
        public MailType()
        {
            Field<NonNullGraphType<GuidGraphType>>(
                nameof(Nekoyume.Model.Mail.Mail.id),
                resolve: context => context.Source.id);
            Field<NonNullGraphType<LongGraphType>>(
                nameof(Nekoyume.Model.Mail.Mail.requiredBlockIndex),
                resolve: context => context.Source.requiredBlockIndex);
            Field<NonNullGraphType<LongGraphType>>(
                nameof(Nekoyume.Model.Mail.Mail.blockIndex),
                resolve: context => context.Source.blockIndex);
        }
    }
}
