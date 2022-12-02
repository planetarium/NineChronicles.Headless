using System;
using GraphQL;
using GraphQL.Types;
using Nekoyume.TableData;

namespace NineChronicles.Headless.GraphTypes.States.Models.Table
{
    public class StakeRewardEnumType : EnumerationGraphType<StakeRegularRewardSheet.StakeRewardType>
    {
        public StakeRewardEnumType()
        {
            this.AddDeprecatedNames(StringExtensions.ToPascalCase);
        }
    }
}
