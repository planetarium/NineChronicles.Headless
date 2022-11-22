using GraphQL.Types;
using Nekoyume.Model.EnumType;
using Nekoyume.TableData;

namespace NineChronicles.Headless.GraphTypes.States.Models.Table
{
    public class RuneListRowType : ObjectGraphType<RuneListSheet.Row>
    {
        public RuneListRowType()
        {
            Field<NonNullGraphType<IntGraphType>>(nameof(RuneListSheet.Row.Id), resolve: context => context.Source.Id);
            Field<NonNullGraphType<IntGraphType>>(nameof(RuneListSheet.Row.Grade), resolve: context => context.Source.Grade);
            Field<NonNullGraphType<RuneTypeEnumType>>(nameof(RuneListSheet.Row.RuneType), resolve: context => context.Source.RuneType);
            Field<NonNullGraphType<IntGraphType>>(nameof(RuneListSheet.Row.RequiredLevel), resolve: context => context.Source.RequiredLevel);
            Field<NonNullGraphType<RuneUsePlaceEnumType>>(nameof(RuneListSheet.Row.UsePlace), resolve: context => context.Source.UsePlace);
        }
    }
}
