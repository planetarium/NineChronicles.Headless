using System.Linq;
using GraphQL.Types;
using Nekoyume.Model.State;

namespace NineChronicles.Headless.GraphTypes.States
{
    public class AgentStateType : ObjectGraphType<AgentState>
    {
        public AgentStateType()
        {
            Field<NonNullGraphType<AddressType>>(
                nameof(AgentState.address),
                resolve: context => context.Source.address);
            Field<ListGraphType<NonNullGraphType<AddressType>>>(
                nameof(AgentState.avatarAddresses),
                resolve: context => context.Source.avatarAddresses.Select(a => a.Value));
        }
    }
}
